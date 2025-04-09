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
using NAudio.Utils;

namespace HelseVestIKT_Dashboard
{
	/// <summary>
	/// Interaction logic for VRFullscreenWindow.xaml
	/// </summary>
	/// 
	public partial class VRFullscreenWindow : Window
    {


        private OpenXRManager _openXRManager;

        private D3DImage _d3dImage;

        // P/Invoke for å finne vinduet og endre foreldre
        [DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		private VRFullscreenWindow _fullScreenWindow = null;
	
		public VRFullscreenWindow()
        {
			InitializeComponent();

			//if (VRHostFullscreen.Child == null)
			//{
			//	VRHostFullscreen.Child = new System.Windows.Forms.Panel();
			//}

			this.Loaded += FullScreenWindow_Loaded;
		}

		private async void FullScreenWindow_Loaded(object sender, RoutedEventArgs e)
		{
			//VRHostFullscreen.UpdateLayout();
			//await EmbedSteamVRSpeactatorAsync();



            _openXRManager = new OpenXRManager();
            bool success = await Task.Run(() => _openXRManager.Initialize());
            if (!success)
            {
                Console.WriteLine("OpenXR initialisering feilet.");
                return;
            }

            // Opprett D3DImage for interop med Direct3D11
            _d3dImage = new D3DImage();
            D3DImageHost.Source = _d3dImage;

            // Starte en løkke eller timer for å oppdatere D3DImage med det rendrte innholdet.
            // Dette er bare et eksempel; du må tilpasse oppdateringslogikken etter ditt behov.
            CompositionTarget.Rendering += OnRendering;

        }


        private void OnRendering(object sender, EventArgs e)
        {
            _d3dImage.Lock();
            IntPtr sharedTexPtr = _openXRManager.GetSharedTexture();
            // Pass på at du bruker riktig type for backbufferet; her antas D3DResourceType.IDirect3DSurface9,
            // men konfigurasjonen avhenger av hvordan du setter opp delingen mellom D3D11 og D3D9.
            _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, sharedTexPtr);
            _d3dImage.AddDirtyRect(new Int32Rect(0, 0, _d3dImage.PixelWidth, _d3dImage.PixelHeight));
            _d3dImage.Unlock();
        }


  //      public async Task EmbedSteamVRSpeactatorAsync()
		//{
		//	const int maxAttempts = 20;
		//	const int dealyMs = 5000;
		//	IntPtr spectatorHandle = IntPtr.Zero;

		//	for (int attempt = 0; attempt < maxAttempts; attempt++)
		//	{
		//		spectatorHandle = FindWindow(null, "VR-visning");
		//		if (spectatorHandle != IntPtr.Zero)
		//		{
		//			Console.WriteLine("Fant Steam VR Spectator Vindu");
		//			break;

		//		}
		//		Console.WriteLine($"Attempt {attempt + 1}: Steam VR Spectator Vindu ikke funnet");
		//		await Task.Delay(dealyMs);
		//	}

		//	if (spectatorHandle != IntPtr.Zero)
		//	{

		//		var panel = VRHostFullscreen.Child as System.Windows.Forms.Panel;
		//		if (panel != null)
		//		{
		//			panel.CreateControl();
		//			IntPtr hostHandle = panel.Handle;
		//			if (hostHandle != IntPtr.Zero)
		//			{
		//				SetParent(spectatorHandle, hostHandle);
		//				Console.WriteLine("Embedded Steam VR Spectator Window");

  //                      // Nå tvinger vi vinduet til å ha samme størrelse som FullscreenGrid
  //                      NativeMethods.SetWindowPos(
  //                          spectatorHandle,
  //                          IntPtr.Zero,
  //                          0,
  //                          0,
  //                          (int)FullscreenGrid.ActualWidth,
  //                          (int)FullscreenGrid.ActualHeight,
  //                          NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
  //                  }
		//			else
		//			{
		//				Console.WriteLine("Kunne ikke hente håndtak til VRHostFullscreen.");
		//			}
		//		}
		//		// Her henter vi håndtaket til VRHostFullscreen, som er et WindowsFormsHost-element definert i XAML
		//		var source = (HwndSource)PresentationSource.FromVisual(VRHostFullscreen);
		//		if (source != null)
		//		{
		//			IntPtr hostHandle = source.Handle;
		//			SetParent(spectatorHandle, hostHandle);
		//			Console.WriteLine("Embedded Steam VR Spectator Window");
		//		}
		//		else
		//		{
		//			Console.WriteLine("Kunne ikke hente håndtak til VRHostFullscreen.");
		//		}
		//	}
		//	else
		//	{
		//		Console.WriteLine("Kunne ikke finne SteamVR Spectator Window etter venting");
		//	}
		//}

		//public void SetVRContent(WindowsFormsHost vrHost)
		//{
		//	FullscreenGrid.Children.Clear();
		//	FullscreenGrid.Children.Add(vrHost);
		//	vrHost.Visibility = Visibility.Visible;	

		//}

		//public void RemoveVRContent(WindowsFormsHost vrHost)
		//{
		//	if (FullscreenGrid.Children.Contains(vrHost))
		//	{
		//		FullscreenGrid.Children.Remove(vrHost);
		//	}
		//}

	}
}
