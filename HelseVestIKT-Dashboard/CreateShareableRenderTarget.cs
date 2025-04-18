using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace HelseVestIKT_Dashboard
{
	public class CreateShareableRenderTarget
	{
		/// <summary>
		/// Creates a shareable D3D11 texture render target, clears it to green for testing,
		/// and returns the created texture.
		/// </summary>
		/// <param name="device">The D3D11 device.</param>
		/// <param name="width">The texture width.</param>
		/// <param name="height">The texture height.</param>
		/// <returns>The created shareable render target texture.</returns>
		public static ID3D11Texture2D CreateTarget(ID3D11Device device, uint width, uint height)
		{
			// Define the texture description.
			var textureDesc = new Texture2DDescription
			{
				Width = width,
				Height = height,
				MipLevels = 1u,
				ArraySize = 1u,
				Format = Format.B8G8R8A8_UNorm,           // Use the desired format.
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.RenderTarget,
				CPUAccessFlags = CpuAccessFlags.None,
				MiscFlags = ResourceOptionFlags.Shared       // Mark this texture as shared.
			};

			// Create the texture.
			ID3D11Texture2D offscreenTexture = device.CreateTexture2D(textureDesc);

			// Create a render target view (RTV) for the texture.
			using ID3D11RenderTargetView rtv = device.CreateRenderTargetView(offscreenTexture);

			// Clear the RTV to a test color (green) using the immediate context.
			device.ImmediateContext.ClearRenderTargetView(rtv, new Color4(0f, 1f, 0f, 1f));

			// Return the created texture.
			return offscreenTexture;
		}
	}
}
