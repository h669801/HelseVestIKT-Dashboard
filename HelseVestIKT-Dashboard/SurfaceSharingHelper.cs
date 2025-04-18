using System.Windows;
using Vortice.Direct3D11;                 // For D3D11 types (ID3D11Device, Texture2DDescription, etc.)
using Vortice.DXGI;                       // For DXGI types (Format, SampleDescription)
using Vortice.Direct3D;                   // For ResourceUsage and related enums
using SharpDX.Direct3D9;                  // For D3D9 types 
using Format9 = SharpDX.Direct3D9.Format;
using PresentParameters9 = SharpDX.Direct3D9.PresentParameters;
using SwapEffect9 = SharpDX.Direct3D9.SwapEffect;
using DeviceEx = SharpDX.Direct3D9.DeviceEx;
using Direct3DEx = SharpDX.Direct3D9.Direct3DEx;
using CreateFlags = SharpDX.Direct3D9.CreateFlags;
using DeviceType = SharpDX.Direct3D9.DeviceType;
using MultisampleType = SharpDX.Direct3D9.MultisampleType;

namespace HelseVestIKT_Dashboard
{
	public static class SurfaceSharingHelper
	{
		// Declare a static field to hold the shared D3D11 texture.
		private static ID3D11Texture2D? _sharedTexture;

		/// <summary>
		/// Creates a shareable D3D11 texture, obtains its DXGI shared handle, 
		/// and uses it to create a Direct3D9Ex render target surface that can be used as
		/// the back buffer for WPF's D3DImage.
		/// </summary>
		/// <param name="device">The Direct3D11 device.</param>
		/// <param name="width">Texture width.</param>
		/// <param name="height">Texture height.</param>
		/// <returns>A native pointer to the created Direct3D9 surface.</returns>
		public static IntPtr CreateSharedSurface(ID3D11Device device, uint width, uint height)
		{
			// Create a D3D11 texture description with a shareable resource.
			var desc = new Texture2DDescription
			{
				Width = width,
				Height = height,
				MipLevels = 1u,
				ArraySize = 1u,
				Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
				SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
				Usage = Vortice.Direct3D11.ResourceUsage.Default,
				BindFlags = Vortice.Direct3D11.BindFlags.RenderTarget,
				CPUAccessFlags = Vortice.Direct3D11.CpuAccessFlags.None,
				MiscFlags = Vortice.Direct3D11.ResourceOptionFlags.Shared // Mark the resource as shareable
			};

			// Create the shared texture on the D3D11 device and store it in the static field.
			_sharedTexture = device.CreateTexture2D(desc);

			// Query the DXGI interface to obtain the shared handle.
			using var dxgiResource = _sharedTexture.QueryInterface<IDXGIResource>();
			IntPtr sharedHandle = dxgiResource.SharedHandle;
			if (sharedHandle == IntPtr.Zero)
				throw new Exception("Failed to get DXGI shared handle.");

			// Create a Direct3D9Ex device using SharpDX.Direct3D9.
			var direct3D = new Direct3DEx();
			var presentParams = new PresentParameters9
			{
				Windowed = true,
				SwapEffect = SwapEffect9.Discard
			};

			// Use the main window's handle (ensure Application.Current.MainWindow is initialized).
			var dummyWindowHandle = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
			presentParams.DeviceWindowHandle = dummyWindowHandle;

			var d3d9DeviceEx = new DeviceEx(
				direct3D,
				0,
				DeviceType.Hardware,
				dummyWindowHandle,
				CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
				presentParams);

			// Create a Direct3D9 texture using the shared handle.
			var shareTexture9 = new SharpDX.Direct3D9.Texture(
				d3d9DeviceEx,
				(int)width,
				(int)height,
				1,
				SharpDX.Direct3D9.Usage.RenderTarget,
				Format9.A8R8G8B8,   // Using the alias to reference SharpDX.Direct3D9.Format
				Pool.Default,
				ref sharedHandle);

			// Get the surface level from the texture.
			var surface = shareTexture9.GetSurfaceLevel(0);
			return surface.NativePointer;
		}

		public static IntPtr CreateSharedSurfaceFromHandle(ID3D11Device device, IntPtr sharedHandle, uint width, uint height)
		{
			// Create a Direct3D9Ex device (similar to your method)
			var direct3D = new Direct3DEx();
			var presentParams = new PresentParameters9
			{
				Windowed = true,
				SwapEffect = SwapEffect9.Discard
			};
			var dummyWindowHandle = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
			presentParams.DeviceWindowHandle = dummyWindowHandle;
			var d3d9DeviceEx = new DeviceEx(
				direct3D,
				0,
				DeviceType.Hardware,
				dummyWindowHandle,
				CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
				presentParams);

			// Use the shared handle to create a D3D9 texture.
			var shareTexture9 = new SharpDX.Direct3D9.Texture(
				d3d9DeviceEx,
				(int)width,
				(int)height,
				1,
				SharpDX.Direct3D9.Usage.RenderTarget,
				Format9.A8R8G8B8,
				Pool.Default,
				ref sharedHandle);

			var surface = shareTexture9.GetSurfaceLevel(0);
			return surface.NativePointer;
		}


	}
}
