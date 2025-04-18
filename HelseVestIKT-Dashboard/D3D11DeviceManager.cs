using System;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using Vortice.Direct3D;

namespace HelseVestIKT_Dashboard
{
	public class D3D11DeviceManager
	{
		public ID3D11Device Device { get; private set; }
		public ID3D11DeviceContext DeviceContext { get; private set; }

		// Native pointer to the D3D11 device.
		private IntPtr _devicePtr;
		public IntPtr DevicePointer => _devicePtr;

		// Singleton instance.
		public static D3D11DeviceManager Instance { get; } = new D3D11DeviceManager();

		// Static helper to get the device pointer.
		public static IntPtr GetDevicePointer() => Instance.DevicePointer;

		public D3D11DeviceManager()
		{
			CreateDevice();
		}

		private void CreateDevice()
		{
			// Use BGRA support flag required for sharing with WPF's D3DImage.
			DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
			ID3D11Device device;
			ID3D11DeviceContext context;

			// Create the Direct3D 11 device with hardware acceleration.
			D3D11CreateDevice(
				null,                 // Default adapter.
				DriverType.Hardware,  // Use the hardware driver.
				flags,
				null,                 // Use default feature levels.
				out device,
				out context);

			Device = device;
			DeviceContext = context;

			// Store the native pointer so it can be used for interop (e.g., with OpenXR or OpenVR).
			_devicePtr = device.NativePointer;
		}
	}
}
