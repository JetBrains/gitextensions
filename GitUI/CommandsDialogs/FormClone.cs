﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitCommands.Config;
using GitCommands.Repository;

using GitUI.UserControls;

using ResourceManager;

namespace GitUI.CommandsDialogs
{
    public partial class FormClone : GitModuleForm
    {
        private readonly TranslationString _infoNewRepositoryLocation =
            new TranslationString("The repository will be cloned to a new directory located here:"  + Environment.NewLine +
                                  "{0}");

        private readonly TranslationString _infoDirectoryExists =
            new TranslationString("(Directory already exists)");

        private readonly TranslationString _infoDirectoryNew =
            new TranslationString("(New directory)");

        private readonly TranslationString _questionOpenRepo =
            new TranslationString("The repository has been cloned successfully." + Environment.NewLine +
                                  "Do you want to open the new repository \"{0}\" now?");

        private readonly TranslationString _questionOpenRepoCaption =
            new TranslationString("Open");

        private bool openedFromProtocolHandler;
        private readonly string url;
        private EventHandler<GitModuleEventArgs> GitModuleChanged;

        // for translation only
        private FormClone()
            : this(null, null, false, null)
        {
        }

        public FormClone(GitUICommands aCommands, string url, bool openedFromProtocolHandler, EventHandler<GitModuleEventArgs> GitModuleChanged)
            : base(aCommands)
        {
            this.GitModuleChanged = GitModuleChanged;
            InitializeComponent();
            Translate();
            this.openedFromProtocolHandler = openedFromProtocolHandler;
            this.url = url;
        }

        protected override void OnRuntimeLoad(EventArgs e)
        {
            base.OnRuntimeLoad(e);
            FillFromDropDown();

            _NO_TRANSLATE_To.Text = AppSettings.DefaultCloneDestinationPath;

            if (url.IsNotNullOrWhitespace())
            {
                _NO_TRANSLATE_From.Text = url;
            }
            else
            {
                // Try to be more helpful to the user.
                // Use the cliboard text as a potential source URL.
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.Text))
                    {
                        string text = Clipboard.GetText(TextDataFormat.Text) ?? string.Empty;

                        // See if it's a valid URL.
                        string lowerText = text.ToLowerInvariant();
                        if (lowerText.StartsWith("http") ||
                            lowerText.StartsWith("git") ||
                            lowerText.StartsWith("ssh"))
                        {
                            _NO_TRANSLATE_From.Text = text;
                        }
                    }
                }
                catch (Exception)
                {
                    // We tried.
                }
                //if the From field is empty, then fill it with the current repository remote URL in hope
                //that the cloned repository is hosted on the same server
                if (_NO_TRANSLATE_From.Text.IsNullOrWhiteSpace())
                {
                    var currentBranchRemote = Module.GetSetting(string.Format("branch.{0}.remote", Module.GetSelectedBranch()));
                    if (currentBranchRemote.IsNullOrEmpty())
                    {
                        var remotes = Module.GetRemotes();

                        if (remotes.Any(s => s.Equals("origin", StringComparison.InvariantCultureIgnoreCase)))
                            currentBranchRemote = "origin";
                        else
                            currentBranchRemote = remotes.FirstOrDefault();
                    }

                    string pushUrl = Module.GetPathSetting(string.Format(SettingKeyString.RemotePushUrl, currentBranchRemote));
                    if (pushUrl.IsNullOrEmpty())
                    {
                        pushUrl = Module.GetPathSetting(string.Format(SettingKeyString.RemoteUrl, currentBranchRemote));
                    }


                    _NO_TRANSLATE_From.Text = pushUrl;
                }
            }

            try
            {
                //if there is no destination directory, then use the parent directory of the current repository
                if (_NO_TRANSLATE_To.Text.IsNullOrWhiteSpace() && Module.WorkingDir.IsNotNullOrWhitespace())
                    _NO_TRANSLATE_To.Text = Path.GetDirectoryName(Module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar));
            }
            catch (Exception)
            { }

            FromTextUpdate(null, null);
        }

        private void OkClick(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.Default;
                _branchListLoader.Cancel();

                var dirTo = Path.Combine(_NO_TRANSLATE_To.Text, _NO_TRANSLATE_NewDirectory.Text);

                Repositories.AddMostRecentRepository(_NO_TRANSLATE_From.Text);

                if (!Directory.Exists(dirTo))
                    Directory.CreateDirectory(dirTo);

                var cloneCmd = GitCommandHelpers.CloneCmd(_NO_TRANSLATE_From.Text, dirTo,
                            CentralRepository.Checked, cbIntializeAllSubmodules.Checked, Branches.Text, null);
                using (var fromProcess = new FormRemoteProcess(Module, AppSettings.GitCommand, cloneCmd))
                {
                    fromProcess.SetUrlTryingToConnect(_NO_TRANSLATE_From.Text);
                    fromProcess.ShowDialog(this);

                    if (fromProcess.ErrorOccurred() || Module.InTheMiddleOfPatch())
                        return;
                }

                Repositories.AddMostRecentRepository(dirTo);

                if (openedFromProtocolHandler && AskIfNewRepositoryShouldBeOpened(dirTo))
                {
                    Hide();
                    GitUICommands uiCommands = new GitUICommands(dirTo);
                    uiCommands.StartBrowseDialog();
                }
                else if (ShowInTaskbar == false && GitModuleChanged != null &&
                    AskIfNewRepositoryShouldBeOpened(dirTo))
                    GitModuleChanged(this, new GitModuleEventArgs(new GitModule(dirTo)));

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Exception: " + ex.Message, "Clone failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool AskIfNewRepositoryShouldBeOpened(string dirTo)
        {
            return MessageBox.Show(this, string.Format(_questionOpenRepo.Text, dirTo), _questionOpenRepoCaption.Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private void FromBrowseClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { SelectedPath = _NO_TRANSLATE_From.Text })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _NO_TRANSLATE_From.Text = dialog.SelectedPath;
            }

            FromTextUpdate(sender, e);
        }

        private void ToBrowseClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { SelectedPath = _NO_TRANSLATE_To.Text })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _NO_TRANSLATE_To.Text = dialog.SelectedPath;
            }

            ToTextUpdate(sender, e);
        }

        private void FillFromDropDown()
        {
            System.ComponentModel.BindingList<Repository> repos = Repositories.RemoteRepositoryHistory.Repositories;
            if (_NO_TRANSLATE_From.Items.Count != repos.Count)
            {
                _NO_TRANSLATE_To.Items.Clear();
                foreach (Repository repo in repos)
                    _NO_TRANSLATE_From.Items.Add(repo.Path);
            }
        }

        private void ToDropDown(object sender, EventArgs e)
        {
            System.ComponentModel.BindingList<Repository> repos = Repositories.RepositoryHistory.Repositories;
            if (_NO_TRANSLATE_To.Items.Count != repos.Count)
            {
                _NO_TRANSLATE_To.Items.Clear();
                foreach (Repository repo in repos)
                    _NO_TRANSLATE_To.Items.Add(repo.Path);
            }
        }


        private void LoadSshKeyClick(object sender, EventArgs e)
        {
            BrowseForPrivateKey.BrowseAndLoad(this);
        }

        private void FormCloneLoad(object sender, EventArgs e)
        {
            if (!GitCommandHelpers.Plink())
                LoadSSHKey.Visible = false;
        }


        private void FromSelectedIndexChanged(object sender, EventArgs e)
        {
            FromTextUpdate(sender, e);
        }

        private void FromTextUpdate(object sender, EventArgs e)
        {
            string path = PathUtil.GetRepositoryName(_NO_TRANSLATE_From.Text);

            if (path != "")
            {
              _NO_TRANSLATE_NewDirectory.Text = path;
            }

            Branches.DataSource = null;

            ToTextUpdate(sender, e);
        }

        private void ToTextUpdate(object sender, EventArgs e)
        {
            string destinationPath = string.Empty;

            if (string.IsNullOrEmpty(_NO_TRANSLATE_To.Text))
                destinationPath += "[" + label2.Text + "]";
            else
                destinationPath += _NO_TRANSLATE_To.Text.TrimEnd(new[] { '\\', '/' });

            destinationPath += "\\";

            if (string.IsNullOrEmpty(_NO_TRANSLATE_NewDirectory.Text))
                destinationPath += "[" + label3.Text + "]";
            else
                destinationPath += _NO_TRANSLATE_NewDirectory.Text;

            Info.Text = string.Format(_infoNewRepositoryLocation.Text, destinationPath);

            if (destinationPath.Contains("[") || destinationPath.Contains("]"))
            {
                Info.ForeColor = Color.Red;
                return;
            }

            if (Directory.Exists(destinationPath))
            {
                if (Directory.GetDirectories(destinationPath).Length > 0 || Directory.GetFiles(destinationPath).Length > 0)
                {
                    Info.Text += " " + _infoDirectoryExists.Text;
                    Info.ForeColor = Color.Red;
                }
                else
                {
                    Info.ForeColor = Color.Black;
                }
            }
            else
            {
                Info.Text += " " + _infoDirectoryNew.Text;
                Info.ForeColor = Color.Black;
            }
        }

        private void NewDirectoryTextChanged(object sender, EventArgs e)
        {
            ToTextUpdate(sender, e);
        }

        private void ToSelectedIndexChanged(object sender, EventArgs e)
        {
            ToTextUpdate(sender, e);
        }

        private readonly AsyncLoader _branchListLoader = new AsyncLoader();

        private void UpdateBranches(RemoteActionResult<IList<GitRef>> branchList)
        {
            Cursor = Cursors.Default;

            if (branchList.HostKeyFail)
            {
                string remoteUrl = _NO_TRANSLATE_From.Text;

                if (FormRemoteProcess.AskForCacheHostkey(this, Module, remoteUrl))
                {
                    LoadBranches();
                }
            }
            else if (branchList.AuthenticationFail)
            {
                string loadedKey;
                if (FormPuttyError.AskForKey(this, out loadedKey))
                {
                    LoadBranches();
                }
            }
            else
            {
                string text = Branches.Text;
                Branches.DataSource = branchList.Result;
                if (branchList.Result.Any(a => a.LocalName == text))
                {
                    Branches.Text = text;
                }
            }
        }

        private void LoadBranches()
        {
            Branches.DisplayMember = "LocalName";
            string from = _NO_TRANSLATE_From.Text;
            Cursor = Cursors.AppStarting;
            _branchListLoader.Load(() => Module.GetRemoteRefs(from, false, true), UpdateBranches);
        }

        private void Branches_DropDown(object sender, EventArgs e)
        {
            LoadBranches();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _branchListLoader.Cancel();

                _branchListLoader.Dispose();

                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
