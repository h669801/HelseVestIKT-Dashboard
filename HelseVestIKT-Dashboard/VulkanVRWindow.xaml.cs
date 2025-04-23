using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HelseVestIKT_Dashboard
{
    /// <summary>
    /// Interaction logic for VulkanVRWindow.xaml
    /// </summary>
    public partial class VulkanVRWindow : Window
    {
        private VulkanRenderer _vulkanRenderer;
        private D3DInterop _d3dInterop;
        // For eksempel, anta at swapchain-bildet er 1280x720.
        private const int SwapchainWidth = 1280;
        private const int SwapchainHeight = 720;

        public VulkanVRWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                // Initialiser VulkanRenderer og opprett overflate/swapchain:
                _vulkanRenderer = new VulkanRenderer();

                // Hent vindushåndtak og HINSTANCE fra ditt vindu:
                var windowHandle = new WindowInteropHelper(this).Handle;
                var hInstance = Marshal.GetHINSTANCE(typeof(VulkanVRWindow).Module);

                Console.WriteLine(windowHandle); // Sjekk at det ikke er 0
                _vulkanRenderer.CreateSurface(windowHandle, hInstance);
                _vulkanRenderer.Initialize(); // Eller kall eventuelt Initialize() før CreateSurface, avhengig av logikken.
                _vulkanRenderer.CreateSwapchain();

                _d3dInterop = new D3DInterop(SwapchainWidth, SwapchainHeight);
                VulkanImageControl.Source = _d3dInterop.D3DImageSource;

                CompositionTarget.Rendering += CompositionTarget_Rendering;
            };
        }



        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Sjekk at både Vulkan-rendereren og D3DInterop-instansen er initialisert.
            if (_vulkanRenderer == null || _d3dInterop == null)
                return;

            try
            {
                // Hent det delte Win32-håndtaket til den siste swapchain-bildet.
                // Denne metoden er en del av din VulkanRenderer-implementasjon.
                // For eksempel: IntPtr sharedHandle = _vulkanRenderer.GetCurrentSharedHandle();
                IntPtr sharedHandle = _vulkanRenderer.GetCurrentSharedHandle();

                // Hvis håndtaket er gyldig, oppdater D3DImage med den nye teksturdataen.
                if (sharedHandle != IntPtr.Zero)
                {
                    // Oppdatering kan inkludere låsing/oppdatering av D3DImage internt.
                    _d3dInterop.UpdateSharedTexture(sharedHandle);
                }
                // Om håndtaket er ugyldig (for eksempel under en overgang eller en feil),
                // kan du velge å ikke oppdatere bildet denne rammen.
            }
            catch (Exception ex)
            {
                // Logg feilen, eventuelt vis feilmelding eller håndter den på passende vis.
                System.Diagnostics.Debug.WriteLine("Feil under oppdatering av D3DImage: " + ex.Message);
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _d3dInterop?.Dispose();
            _vulkanRenderer?.Dispose();
            base.OnClosed(e);
        }
    }
}
