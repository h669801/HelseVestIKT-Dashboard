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
		private const int MaxVREmbedAttempts = 20;

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
		}

		private void EmbedVRView(IntPtr vrViewHandle)
		{
			// Hent child-handle fra host
			var childHandle = (_vrhost.Child as System.Windows.Forms.Control)?.Handle ?? IntPtr.Zero;
			if (childHandle == IntPtr.Zero) return;

			// Re-parent
			Win32.SetParent(vrViewHandle, childHandle);

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

		public void ResizeHost(object sender, EventArgs e)
		{
			IntPtr vrViewHandle = Win32.FindWindowByTitleSubstrings("VR View", "VR-visning");
			if (vrViewHandle == IntPtr.Zero) return;

			int width = (int)_vrhost.ActualWidth;
			int height = (int)_vrhost.ActualHeight;
			Win32.SetWindowPos(vrViewHandle, IntPtr.Zero, 0, 0, width, height,
				Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		}

		public void StartVREmbedRetry()
		{
			_alreadyEmbedded = false;
			_vrEmbedAttempts = 0;
			_vrEmbedTimer?.Stop();
			_vrEmbedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
			_vrEmbedTimer.Tick += VREmbedTimer_Tick;
			_vrEmbedTimer.Start();
		}

		private void VREmbedTimer_Tick(object sender, EventArgs e)
		{
			if (_alreadyEmbedded) return;

			_vrEmbedAttempts++;
			IntPtr vrViewHandle = Win32.FindWindowByTitleSubstrings("VR View", "VR-visning");
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
	}
}
