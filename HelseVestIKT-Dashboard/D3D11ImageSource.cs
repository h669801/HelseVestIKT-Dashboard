using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Windows;
using System.Windows.Interop;
using SharpDX.Direct3D9;
using SharpDX.Direct3D11;
using Device9 = SharpDX.Direct3D9.DeviceEx;

namespace HelseVestIKT_Dashboard
{ 
    public class D3D11ImageSource : D3DImage, IDisposable
    {
        private Device9 _d3d9Device;
        private Texture _d3d9SharedTexture;
        private IntPtr _d3d9SurfacePtr = IntPtr.Zero;

        public D3D11ImageSource(Device9 d3d9Device)
        {
            _d3d9Device = d3d9Device;
        }

        /// <summary>
        /// Setter D3D11-teksturen som skal vises. Teksturen må være opprettet med D3D11_RESOURCE_MISC_SHARED.
        /// </summary>
        /// <param name="d3d11Texture">D3D11 delt tekstur</param>
        public void SetD3D11SharedTexture(SharpDX.Direct3D11.Texture2D d3d11Texture)
        {
            // Hent det delte håndtaket fra D3D11-teksturen via DXGI
            using (var resource = d3d11Texture.QueryInterface<SharpDX.DXGI.Resource>())
            {
                IntPtr sharedHandle = resource.SharedHandle;
                if (sharedHandle == IntPtr.Zero)
                    throw new Exception("Teksturen er ikke delt (SharedHandle er null).");

                // Hent teksturens dimensjoner (du må kjenne til format og størrelse)
                var desc = d3d11Texture.Description;
                // Merk: Formatkonvertering kan være nødvendig. Her antar vi Format.A8R8G8B8.
                Format format9 = Format.A8R8G8B8;

                // Opprett en D3D9-tekstur ved å bruke den delte handle
                _d3d9SharedTexture = new Texture(
                    _d3d9Device,
                    desc.Width,
                    desc.Height,
                    1,
                    Usage.RenderTarget,
                    format9,
                    Pool.Default,
                    ref sharedHandle);

                // Hent overflaten (surface) fra D3D9-teksturen
                using (var surface = _d3d9SharedTexture.GetSurfaceLevel(0))
                {
                    _d3d9SurfacePtr = surface.NativePointer;
                    // Sett denne overflaten som back buffer for D3DImage
                    Lock();
                    SetBackBuffer(D3DResourceType.IDirect3DSurface9, _d3d9SurfacePtr);
                    Unlock();
                }
            }
        }

        /// <summary>
        /// Kaller denne metoden i en render loop for å oppdatere bildet.
        /// </summary>
        public void InvalidateD3DImage()
        {
            Lock();
            AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
            Unlock();
        }

        public void Dispose()
        {
            if (_d3d9SharedTexture != null)
            {
                _d3d9SharedTexture.Dispose();
                _d3d9SharedTexture = null;
            }
        }
    }

}
