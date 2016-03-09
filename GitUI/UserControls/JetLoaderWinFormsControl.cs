using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace GitUI.UserControls
{
	public class JetLoaderWinFormsControl : Control
	{
		private readonly JetLoaderAnimationRenderer _renderer = new JetLoaderAnimationRenderer();

		private readonly Timer _timer;

		public JetLoaderWinFormsControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			Size = _renderer.Size.ToSize();

			_timer = new Timer() {Interval = (int)TimeSpan.FromSeconds(1.0 / 30).TotalMilliseconds};
			_timer.Tick += delegate { if(_renderer.Loop()) Invalidate(); };
			_timer.Enabled = Visible;
			VisibleChanged += delegate { _timer.Enabled = Visible; };
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if(disposing)
				_timer.Dispose();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			_renderer.Loop();

			Graphics dc = e.Graphics;
			dc.Clear(Color.Transparent);

			dc.InterpolationMode = InterpolationMode.Bicubic;
			dc.CompositingMode = CompositingMode.SourceOver;
			dc.CompositingQuality = CompositingQuality.HighQuality;
			dc.PixelOffsetMode = PixelOffsetMode.HighQuality;
			dc.SmoothingMode = SmoothingMode.HighQuality;

			Size area = ClientSize;
			if((area.Width > _renderer.Size.Width) || (area.Height > _renderer.Size.Height))
			{
				using(var brush = new SolidBrush(BackColor))
					dc.FillRectangle(brush, ClientRectangle);
			}
			SizeF diff = area - _renderer.Size;
			dc.TranslateTransform(diff.Width / 2, diff.Height / 2);
			_renderer.Draw(dc);
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
		}

		/// <summary>
		/// Renderer implementation for the JetBrains loader animation.
		/// </summary>
		private class JetLoaderAnimationRenderer
		{
			private readonly Color[] _brushes = new Color[Parameters.TicksPerColor * Parameters.Colors.Length];

			private uint _lastRenderMs = unchecked((uint)Environment.TickCount);

			private readonly Particle[] _particles = new Particle[Parameters.LivingParticles];

			private PointF _position;

			private float _radius;

			private float _radiusSpeed;

			private readonly Random _random = new Random();

			private readonly SizeF _size = new SizeF(Parameters.DefaultSize, Parameters.DefaultSize); // TODO: param?

			private SizeF _speed;

			private int _tick;

			public JetLoaderAnimationRenderer()
			{
				//State
				_position = new PointF();
				_radius = 8;
				_speed = new SizeF(1.5f, 0.5f);
				_radiusSpeed = 0.05f;
				_tick = 0;

				for(int a = Parameters.InitialTicks; a-- > 0;)
					Step();
			}

			public SizeF Size
			{
				get
				{
					return _size;
				}
			}

			public void Draw(Graphics dc)
			{
				dc.FillRectangle(Brushes.White, new RectangleF(new PointF(), _size));
				for(int a = Parameters.LivingParticles, nParticle = (_tick + 1) % Parameters.LivingParticles; a-- > 0; nParticle = (nParticle + 1) % Parameters.LivingParticles)
				{
					using(var brush = new SolidBrush(Mix(_brushes[_particles[nParticle].BrushIndex], Color.White, 1.0f - a / (float)Parameters.LivingParticles)))
					{
						var radsize = new SizeF(_particles[nParticle].Radius, _particles[nParticle].Radius);
						dc.FillEllipse(brush, new RectangleF(_particles[nParticle].Center - radsize, radsize + radsize));
					}
				}

				var rectBlack = new RectangleF(new PointF(), _size);
				rectBlack.Inflate(-(1 - .625f) * _size.Width / 2, -(1 - .625f) * _size.Height / 2);
				dc.FillRectangle(Brushes.Black, rectBlack);

				if(((_tick / Parameters.CaretBlinkTimeTicks) & 1) != 0)
				{
					RectangleF rectSpaceForCaret = rectBlack;
					rectSpaceForCaret.Inflate(new SizeF(-Parameters.DefaultSize / 16, -Parameters.DefaultSize / 16 * 1.25f));
					dc.FillRectangle(Brushes.White, new RectangleF(new PointF(rectSpaceForCaret.Left, rectSpaceForCaret.Bottom - Parameters.DefaultSize * 3 / 64), new SizeF(Parameters.DefaultSize * 7 / 32, Parameters.DefaultSize * 3 / 64)));
				}
			}

			/// <summary>
			/// Runs missed steps. Returns if there were steps and render should be invalidated.
			/// </summary>
			/// <returns></returns>
			public bool Loop()
			{
				// Run missed steps
				uint nowMs = unchecked((uint)Environment.TickCount);
				uint diffMs = unchecked(nowMs - _lastRenderMs);
				uint iters = diffMs / Parameters.MsPerTick;
				if(iters == 0)
					return false;
				_lastRenderMs = unchecked(_lastRenderMs + iters * Parameters.MsPerTick);
				if(iters >= _particles.Length) // Were suspended
					iters = 0;
				while(iters -- > 0)
					Step();

				return true;
			}

			private int GetCurrentColorBrushIndex()
			{
				int brushindex = _tick % (Parameters.TicksPerColor * Parameters.Colors.Length);
				if(_brushes[brushindex] == default(Color))
				{
					Color colorPrev = Parameters.Colors[(_tick / Parameters.TicksPerColor) % Parameters.Colors.Length];
					Color colorNext = Parameters.Colors[(_tick / Parameters.TicksPerColor + 1) % Parameters.Colors.Length];

					Color colorThisPoint = Mix(colorNext, colorPrev, (_tick % Parameters.TicksPerColor) / (float)Parameters.TicksPerColor);

					_brushes[brushindex] = colorThisPoint;
				}

				return brushindex;
			}

			private float HandleLimits(float coord, float radius, float speed, float limit)
			{
				float randomizedSpeedChange = (float)(_random.NextDouble() * Parameters.BaseSpeed - Parameters.BaseSpeed / 2);

				if(coord + (radius * 2) + Parameters.BaseSpeed >= limit)
				{
					return -(Parameters.BaseSpeed + randomizedSpeedChange);
				}
				if(coord <= Parameters.BaseSpeed)
				{
					return Parameters.BaseSpeed + randomizedSpeedChange;
				}
				return speed;
			}

			private static Color Mix(Color colorA, Color colorB, float fraction)
			{
				double num = 1.0 - fraction;
				return Color.FromArgb((int)((double)colorA.R * fraction + colorB.R * num), (int)((double)colorA.G * fraction + colorB.G * num), (int)((double)colorA.B * fraction + colorB.B * num));
			}

			private void Step()
			{
				_tick++;

				StepCoordinates();
				StepRadius();

				_particles[_tick % Parameters.LivingParticles] = new Particle() {Radius = _radius, Center = _position + new SizeF(_radius, _radius), BrushIndex = GetCurrentColorBrushIndex()};
			}

			private void StepCoordinates()
			{
				_position += _speed;

				float hSpeed = HandleLimits(_position.X, _radius, _speed.Width, _size.Width);
				float vSpeed = HandleLimits(_position.Y, _radius, _speed.Height, _size.Height);
				_speed = new SizeF(hSpeed, vSpeed);
			}

			private void StepRadius()
			{
				_radius += _radiusSpeed;

				if(_radius > Parameters.RadiusMax || _radius < Parameters.RadiusMin)
				{
					_radiusSpeed = -_radiusSpeed;
				}
			}

			public static class Parameters
			{
				public static readonly float BaseSpeed = 1.0f;

				public static readonly int CaretBlinkTimeTicks = 53;

				public static readonly Color[] Colors = new[] {"#D73CEA", "#9135E0", "#5848F4", "#25B7FF", "#59BD00", "#FBAC02", "#E32581"}.Select(s => (Color)new ColorConverter().ConvertFromInvariantString(s)).ToArray();

				public static readonly float DefaultSize = 64;

				public static readonly int InitialTicks = 100;

				public static readonly int LivingParticles = 128;

				public static readonly uint MsPerTick = 10;

				public static readonly float RadiusMax = 10;

				public static readonly float RadiusMin = 6;

				public static readonly int TicksPerColor = 40;
			}

			private struct Particle
			{
				public int BrushIndex;

				public PointF Center;

				public float Radius;
			}
		}
	}
}