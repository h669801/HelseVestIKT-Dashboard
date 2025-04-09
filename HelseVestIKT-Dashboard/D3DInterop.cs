using SharpDX.Direct3D9; // Du kan bruke SharpDX (eller SlimDX) for dette
using System;
using System.Windows;
using System.Windows.Interop;

public class D3DInterop : IDisposable
{
    private Direct3DEx _d3d;
    private DeviceEx _device;
    private Texture _sharedTexture;
    public D3DImage D3DImageSource { get; private set; }

    public int TextureWidth { get; private set; }
    public int TextureHeight { get; private set; }

    public D3DInterop(int width, int height)
    {
        TextureWidth = width;
        TextureHeight = height;
        InitializeD3D9();
        D3DImageSource = new D3DImage();
    }

    private void InitializeD3D9()
    {
        // Opprett et Direct3DEx-objekt
        _d3d = new Direct3DEx();

        // Opprett presentasjonsparametere for enheten. Merk at vi oppretter en windowed enhet.
        PresentParameters presentParams = new PresentParameters
        {
            Windowed = true,
            SwapEffect = SwapEffect.Discard,
            DeviceWindowHandle = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle,
            PresentationInterval = PresentInterval.Immediate
        };

        // Opprett en D3D9Ex-enhet. Bruk CreateFlags.Multithreaded for sikkerhet ved bruk fra flere tråder.
        _device = new DeviceEx(_d3d, 0, DeviceType.Hardware, IntPtr.Zero,
            CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
            presentParams);
    }

    /// <summary>
    /// Oppdaterer D3DImage med en delt tekstur importert via et Win32-håndtak.
    /// Håndtaket kommer fra Vulkan ved hjelp av vkGetMemoryWin32HandleKHR.
    /// </summary>
    /// <param name="sharedHandle">Win32 håndtaket til den delte minnesressursen</param>
    public void UpdateSharedTexture(IntPtr sharedHandle)
    {
        // Frigjør gammel tekstur om nødvendig
        _sharedTexture?.Dispose();

        // Opprett en tekstur fra det delte håndtaket.
        // Her antas det at formatet og oppløsningen er A8R8G8B8 og TextureWidth x TextureHeight.
        // Merk: Direkte import fra håndtak krever at du har brukt de riktige flaggene under Vulkan-minnetildelingen.
        _sharedTexture = new Texture(_device, TextureWidth, TextureHeight, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default, ref sharedHandle);

        // Hent overflaten fra teksturens nivå 0
        Surface surface = _sharedTexture.GetSurfaceLevel(0);

        // Oppdater D3DImage med den nye overflaten
        D3DImageSource.Lock();
        D3DImageSource.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
        D3DImageSource.Unlock();
    }

    public void Dispose()
    {
        _sharedTexture?.Dispose();
        _device?.Dispose();
        _d3d?.Dispose();
    }
}
