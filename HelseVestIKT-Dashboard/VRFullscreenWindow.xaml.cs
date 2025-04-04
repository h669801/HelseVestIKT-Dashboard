using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Forms.Integration;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HelseVestIKT_Dashboard
{
	/// <summary>
	/// Interaction logic for VRFullscreenWindow.xaml
	/// </summary>
	/// 
	public partial class VRFullscreenWindow : Window
    {

		// P/Invoke for å finne vinduet og endre foreldre
		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		private VRFullscreenWindow _fullScreenWindow = null;
	
		public VRFullscreenWindow()
        {
			InitializeComponent();

			if (VRHostFullscreen.Child == null)
			{
				VRHostFullscreen.Child = new System.Windows.Forms.Panel();
			}

			this.Loaded += FullScreenWindow_Loaded;
		}

		private async void FullScreenWindow_Loaded(object sender, RoutedEventArgs e)
		{
			VRHostFullscreen.UpdateLayout();
			await EmbedSteamVRSpeactatorAsync();
			

		}
		
		public async Task EmbedSteamVRSpeactatorAsync()
		{
			const int maxAttempts = 20;
			const int dealyMs = 5000;
			IntPtr spectatorHandle = IntPtr.Zero;

			for (int attempt = 0; attempt < maxAttempts; attempt++)
			{
				spectatorHandle = FindWindow(null, "VR View");
				if (spectatorHandle != IntPtr.Zero)
				{
					Console.WriteLine("Fant Steam VR Spectator Vindu");
					break;

				}
				Console.WriteLine($"Attempt {attempt + 1}: Steam VR Spectator vindu ikke funnet");
				await Task.Delay(dealyMs);
			}

			if (spectatorHandle != IntPtr.Zero)
			{

				var panel = VRHostFullscreen.Child as System.Windows.Forms.Panel;
				if (panel != null)
				{
					panel.CreateControl();
					IntPtr hostHandle = panel.Handle;
					if (hostHandle != IntPtr.Zero)
					{
						SetParent(spectatorHandle, hostHandle);
						Console.WriteLine("Embedded Steam VR Spectator vindu");
					}
					else
					{
						Console.WriteLine("Kunne ikke hente håndtak til VRHostFullscreen.");
					}
				}
				// Her henter vi håndtaket til VRHostFullscreen, som er et WindowsFormsHost-element definert i XAML
				var source = (HwndSource)PresentationSource.FromVisual(VRHostFullscreen);
				if (source != null)
				{
					IntPtr hostHandle = source.Handle;
					SetParent(spectatorHandle, hostHandle);
					Console.WriteLine("Embedded Steam VR Spectator vindu");
				}
				else
				{
					Console.WriteLine("Kunne ikke hente håndtak til VRHostFullscreen.");
				}
			}
			else
			{
				Console.WriteLine("Kunne ikke finne SteamVR Spectator Window etter venting");
			}
		}

		public void SetVRContent(WindowsFormsHost vrHost)
		{
			FullscreenGrid.Children.Clear();
			FullscreenGrid.Children.Add(vrHost);
			vrHost.Visibility = Visibility.Visible;	
		}

		public void RemoveVRContent(WindowsFormsHost vrHost)
		{
			if (FullscreenGrid.Children.Contains(vrHost))
			{
				FullscreenGrid.Children.Remove(vrHost);
			}
		}

	}
}
