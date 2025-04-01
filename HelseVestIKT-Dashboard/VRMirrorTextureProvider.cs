using System;
using Valve.VR;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX;

namespace HelseVestIKT_Dashboard
{
    public class VRMirrorTextureProvider
    {
        // Felt for å lagre mirror texture
        private Texture_t _mirrorTexture;
        private IntPtr _shaderResourceView;

        /// <summary>
        /// Henter mirror texture for angitt øye (venstre eller høyre) fra OpenVR.
        /// </summary>
        /// <param name="d3dDevice">En gyldig DXGI.Device.</param>
        /// <param name="eye">Øyet du ønsker texture for.</param>
        /// <returns>En SharpDX.Direct3D11.Texture2D som inneholder mirror texture.</returns>
        public Texture2D GetMirrorTexture(SharpDX.DXGI.Device d3dDevice, EVREye eye)
        {
            _shaderResourceView = IntPtr.Zero;
            IntPtr mirrorTexturePtr = IntPtr.Zero;

            // Bruk parameteren "eye" i stedet for å hardkode.
            EVRCompositorError error = OpenVR.Compositor.GetMirrorTextureD3D11(
                eye,
                d3dDevice.NativePointer,
                ref mirrorTexturePtr
            );
            Console.WriteLine($"GetMirrorTextureD3D11 returned error: {error}");
            if (error != EVRCompositorError.None)
            {
                throw new Exception("GetMirrorTextureD3D11 feilet med error: " + error);
            }

            // Opprett og sett _mirrorTexture med den returnerte pekeren.
            _mirrorTexture = new Texture_t();
            _mirrorTexture.handle = mirrorTexturePtr;

            // Her "wrapper" vi den native pekeren til en SharpDX.Texture2D.
            // Dette krever at _mirrorTexture.handle peker til et gyldig ID3D11Texture2D.
            Texture2D mirrorTexture2D = SharpDX.ComObject.FromPointer<Texture2D>(_mirrorTexture.handle);

            return mirrorTexture2D;
        }
    }
}
