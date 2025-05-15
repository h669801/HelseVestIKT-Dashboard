using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using HelseVestIKT_Dashboard.Infrastructure;

namespace HelseVestIKT_Dashboard.Services
{
	public class VREmbedder
	{
		private readonly WindowsFormsHost _vrhost;
		private readonly System.Windows.Controls.Panel _panel;
		private readonly UIElement _gameLibraryArea;
		private readonly UIElement _returnButton;

		private bool _alreadyEmbedded;
		private int _vrEmbedAttempts;
		private DispatcherTimer _vrEmbedTimer;
		private const int MaxVREmbedAttempts = 50;

		public VREmbedder(
			WindowsFormsHost vrHost,
			System.Windows.Controls.Panel panel,
			UIElement gameLibraryArea,
			UIElement returnButton)
		{
			_vrhost = vrHost;
			_panel = panel;
			_gameLibraryArea = gameLibraryArea;
			_returnButton = returnButton;

			_vrhost.SizeChanged += ResizeHostHelper;
		}

		private void EmbedVRView(IntPtr vrViewHandle)
		{
			if (vrViewHandle == IntPtr.Zero) return;

			// Hent WinForms-host‐handle
			var hostHandle = (_vrhost.Child as System.Windows.Forms.Control)?.Handle ?? IntPtr.Zero;
			if (hostHandle == IntPtr.Zero) return;

			// hent DPI‐scale
			var source = PresentationSource.FromVisual(_vrhost);
			double sx = source.CompositionTarget.TransformToDevice.M11;
			double sy = source.CompositionTarget.TransformToDevice.M22;

			// regn om og rund opp
			int pixelW = (int)Math.Ceiling(_vrhost.ActualWidth * sx);
			int pixelH = (int)Math.Ceiling(_vrhost.ActualHeight * sy);

			// valgfritt: +1 for å være helt sikker
			pixelW += 1;
			pixelH += 1;

			Win32.EmbedOverlay(vrViewHandle, hostHandle, pixelW, pixelH);


			// Fjerner rammer, legger på WS_CHILD + transparent EXSTYLE, og setter størrelse
			Win32.EmbedOverlay(vrViewHandle, hostHandle, pixelW, pixelH);
		}

		/*
		// Fjern rammer
			int style = Win32.GetWindowLong(vrViewHandle, Win32.GWL_STYLE);
			style &= ~(Win32.WS_CAPTION | Win32.WS_BORDER);
			style |= Win32.WS_CHILD;
			Win32.SetWindowLong(vrViewHandle, Win32.GWL_STYLE, style);

			// Strekk til host-størrelse
			int w = (int)_vrhost.ActualWidth;
			int h = (int)_vrhost.ActualHeight;
			Win32.SetWindowPos(vrViewHandle, IntPtr.Zero, 0, 0, w, h,
				Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		}
		 */


		private void ResizeHostHelper(object sender, SizeChangedEventArgs e)
		{
			if (!_alreadyEmbedded) return;

			var overlay = Win32.FindOverlayWindow();
			if (overlay == IntPtr.Zero) return;

			var source = PresentationSource.FromVisual(_vrhost);
			double sx = source?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
			double sy = source?.CompositionTarget.TransformToDevice.M22 ?? 1.0;

			int w = (int)(_vrhost.ActualWidth * sx);
			int h = (int)(_vrhost.ActualHeight * sy);

			Win32.SetWindowPos(overlay, IntPtr.Zero, 0, 0, w, h,
				Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		}


		public void ResizeHost(object sender, EventArgs e)
		{
			IntPtr vrViewHandle = Win32.FindOverlayWindow();
			if (vrViewHandle == IntPtr.Zero) return;

			int width = (int)_vrhost.ActualWidth;
			int height = (int)_vrhost.ActualHeight;
			Win32.SetWindowPos(vrViewHandle, IntPtr.Zero, 0, 0, width, height,
				Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		}

		public void StartVREmbedRetry()
		{
			// <<< UPDATED: Kortere polling-interval og umiddelbar embedding >>>
			_alreadyEmbedded = false;
			_vrEmbedAttempts = 0;
			_vrEmbedTimer?.Stop();
			_vrEmbedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };  // Poll hvert 200ms
			_vrEmbedTimer.Tick += VREmbedTimer_TickHelper;
			_vrEmbedTimer.Start();
			VREmbedTimer_TickHelper(this, EventArgs.Empty);  // Umiddelbar kall for første embed
		}

		private void VREmbedTimer_TickHelper(object sender, EventArgs e)
		{
			if (_alreadyEmbedded) return;

			_vrEmbedAttempts++;
			var overlay = Win32.FindOverlayWindow();
			if (overlay != IntPtr.Zero)
			{
				_vrEmbedTimer.Stop();
				EmbedVRView(overlay);
				_alreadyEmbedded = true;
			}
			else if (_vrEmbedAttempts >= MaxVREmbedAttempts)
			{
				_vrEmbedTimer.Stop();
				Console.WriteLine("Kunne ikke embedde VR View etter maks antall forsøk.");
			}
		}

		private void VREmbedTimer_Tick(object sender, EventArgs e)
		{
			if (_alreadyEmbedded) return;

			_vrEmbedAttempts++;
			IntPtr vrViewHandle = Win32.FindOverlayWindow();
			if (vrViewHandle != IntPtr.Zero)
			{
				_vrEmbedTimer.Stop();
				EmbedVRView(vrViewHandle);
				_alreadyEmbedded = true;
			}
			else if (_vrEmbedAttempts >= MaxVREmbedAttempts)
			{
				_vrEmbedTimer.Stop();
				Console.WriteLine("Kunne ikke embedde VR View etter maks antall forsøk.");
			}
		}


		public async Task EmbedVRSpectatorAsync()
		{
			int attempts = 0;
			while (attempts < MaxVREmbedAttempts)
			{
				var overlay = Win32.FindOverlayWindow();
				if (overlay != IntPtr.Zero)
				{
					EmbedVRView(overlay);
					_alreadyEmbedded = true;
					Console.WriteLine($"VR View embedded etter {attempts + 1} forsøk.");
					return;
				}

				attempts++;
				await Task.Delay(3000);
			}

			Console.WriteLine("Kunne ikke embedde VR View etter flere forsøk.");
		}

		/*
		public async Task EmbedVRSpectatorAsync()
		{
			int attempts = 0;
			while (attempts < MaxVREmbedAttempts)
			{
				IntPtr vrViewHandle = Win32.FindWindowByTitleSubstrings("VR View", "VR-visning");
				if (vrViewHandle != IntPtr.Zero)
				{
					EmbedVRView(vrViewHandle);
					Console.WriteLine($"VR View embedded etter {attempts + 1} forsøk.");
					return;
				}

				attempts++;
				await Task.Delay(3000);
			}

			Console.WriteLine("Kunne ikke embedde VR View etter flere forsøk.");
		}
		*/

		public void EnterFullScreen()
		{
			// Skjul spillbibliotek, vis return‐knapp og host
			_gameLibraryArea.Visibility = Visibility.Collapsed;
			_returnButton.Visibility = Visibility.Visible;
			_vrhost.Visibility = Visibility.Visible;

			// Sett z‐index
			System.Windows.Controls.Panel.SetZIndex(_vrhost, 100);
		}

		public void ExitFullScreen()
		{
			_gameLibraryArea.Visibility = Visibility.Visible;
			_returnButton.Visibility = Visibility.Collapsed;
			_vrhost.Visibility = Visibility.Collapsed;

			System.Windows.Controls.Panel.SetZIndex(_vrhost, 0);
		}

		/// <summary>
		/// Løser overlay-vinduet fra host, gjenoppretter vanlige vindusstiler
		/// </summary>
		// I VREmbedder.cs
		public void DetachOverlay()
		{
			// Finn overlay-vinduet (SteamVR sin “VR View”)
			IntPtr overlay = Win32.FindOverlayWindow();
			if (overlay == IntPtr.Zero)
				return;

			// Fjern det som barn av vårt host-vindu
			Win32.SetParent(overlay, IntPtr.Zero);

			// 2) Gjenopprett stil: fjern CHILD, legg på POPUP + rammer
			int style = Win32.GetWindowLong(overlay, Win32.GWL_STYLE);
			style &= ~Win32.WS_CHILD;                               // fjern child‐flagget
			style |= Win32.WS_POPUP                                  // gjør det til top‐level‐vindu
				   | Win32.WS_CAPTION
				   | Win32.WS_BORDER
				   | Win32.WS_THICKFRAME
				   | Win32.WS_SYSMENU
				   | Win32.WS_MINIMIZEBOX
				   | Win32.WS_MAXIMIZEBOX;
			Win32.SetWindowLong(overlay, Win32.GWL_STYLE, style);

			// 4) Hent og nullstill eks­stil(fjerner transparent/ layered)
			int ex = Win32.GetWindowLong(overlay, Win32.GWL_EXSTYLE);
			ex &= ~(Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);
			Win32.SetWindowLong(overlay, Win32.GWL_EXSTYLE, ex);

			// 5) Påfør endringen (tving ramme-refresh og vis vinduet)
			const uint flags =
				  Win32.SWP_NOMOVE
				| Win32.SWP_NOSIZE
				| Win32.SWP_NOZORDER
				| Win32.SWP_NOACTIVATE
				| Win32.SWP_FRAMECHANGED   // tving NCPAINT
				| Win32.SWP_SHOWWINDOW;    // vis igjen hvis skjult

			Win32.SetWindowPos(overlay, IntPtr.Zero, 0, 0, 0, 0, flags);

			// 6) Hvis du fortsatt opplever at det er minimert, kan du tvinge Restore:
			Win32.ShowWindow(overlay, Win32.SW_RESTORE);

			// Reset intern flag slik at du kan embedde på nytt neste gang
			_alreadyEmbedded = false;
		}
	}
}
