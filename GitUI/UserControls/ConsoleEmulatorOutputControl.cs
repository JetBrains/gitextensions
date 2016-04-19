using System.Windows.Forms;
using System.Xml;

using ConEmu.WinForms;

using GitCommands;
using GitCommands.Utils;

using JetBrains.Annotations;

using Microsoft.Build.Utilities;

namespace GitUI.UserControls
{
	/// <summary>
	/// An output control which inserts a fully-functional console emulator window.
	/// </summary>
	public class ConsoleEmulatorOutputControl : ConsoleOutputControl
	{
		private int _nLastExitCode;

		[NotNull]
		private readonly ConEmuControl _terminal;

		public ConsoleEmulatorOutputControl()
		{
			Controls.Add(_terminal = new ConEmuControl() {Dock = DockStyle.Fill, AutoStartInfo = null /* don't spawn terminal until we have gotten the command */});
		}

		public override int ExitCode
		{
			get
			{
				return _nLastExitCode;
			}
		}

		public override bool IsDisplayingFullProcessOutput
		{
			get
			{
				return true;
			}
		}

		public static bool IsSupportedInThisEnvironment
		{
			get
			{
				return EnvUtils.RunningOnWindows(); // ConEmu only works in WinNT
			}
		}

		public override void AppendMessageFreeThreaded(string text)
		{
			ConEmuSession session = _terminal.RunningSession;
			if(session != null)
				session.WriteOutputText(text);
		}

		public override void KillProcess()
		{
			ConEmuSession session = _terminal.RunningSession;
			if(session != null)
				session.SendControlCAsync();
		}

		public override void Reset()
		{
			ConEmuSession session = _terminal.RunningSession;
			if(session != null)
				session.CloseConsoleEmulator();
		}

		public override void StartProcess(string command, string arguments, string workdir)
		{
			var cmdl = new CommandLineBuilder();
			cmdl.AppendFileNameIfNotNull(command /* do the escaping for it */);
			cmdl.AppendSwitch(arguments /* expecting to be already escaped */);

			var startinfo = new ConEmuStartInfo();
			startinfo.ConsoleProcessCommandLine = cmdl.ToString();
			startinfo.StartupDirectory = workdir;
			startinfo.WhenConsoleProcessExits = WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage;
			startinfo.AnsiStreamChunkReceivedEventSink = (sender, args) => FireDataReceived(new TextEventArgs(args.GetText(GitModule.SystemEncoding)));
			startinfo.ConsoleProcessExitedEventSink = (sender, args) =>
			{
				_nLastExitCode = args.ExitCode;
				FireProcessExited();
			};
			startinfo.ConsoleEmulatorClosedEventSink = delegate { FireTerminated(); };
			startinfo.IsEchoingConsoleCommandLine = true;

			XmlDocument xmlConfig = startinfo.BaseConfiguration;
			XmlNode xmlVanilla = xmlConfig.SelectSingleNode("//key[@name='.Vanilla']");
			if(xmlVanilla != null)
			{
				for(int a = 0; a < 0x10; a++)
				{
					int valyes = (a & 8) != 0 ? 0x00 : 0x80;
					int valno = 0xFF;

					XmlElement xmlColor;
					xmlVanilla.AppendChild(xmlColor = xmlConfig.CreateElement("value"));
					xmlColor.SetAttribute("name", string.Format("ColorTable{0:00}", a));
					xmlColor.SetAttribute("type", "dword");
					xmlColor.SetAttribute("data", string.Format("FF{0:X2}{1:X2}{2:X2}", (a & 1) != 0 ? valyes : valno, (a & 2) != 0 ? valyes : valno, (a & 4) != 0 ? valyes : valno));
				}
			}

			_terminal.Start(startinfo);
		}
	}
}