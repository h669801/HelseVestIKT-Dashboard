using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using static System.Windows.Forms.DataFormats;

namespace HelseVestIKT_Dashboard
{
    public class VRMirrorTextureProvider
    {
        // Disse feltene holder referanser til mirror texture og tilhørende shader resource view.
        private Texture_t _mirrorTexture;
        private IntPtr _shaderResourceView;

        /// <summary>
        /// Henter mirror texture for angitt øye (venstre eller høyre) fra OpenVR.
        /// </summary>
        /// <param name="d3dDevice">En gyldig Direct3D11 Device.</param>
        /// <param name="eye">Hvilket øye (EVREye) du ønsker texture for.</param>
        /// <returns>En SharpDX.Direct3D11.Texture2D som inneholder mirror texture.</returns>
        public Texture2D GetMirrorTexture(SharpDX.DXGI.Device d3dDevice, EVREye eye)
        {
            // Initialiser texture-struct og resource view
            _shaderResourceView = IntPtr.Zero;
            IntPtr mirrorTexturePtr = IntPtr.Zero;

            // Kall GetMirrorTextureD3D11 med ref til mirrorTexturePtr
            EVRCompositorError error = OpenVR.Compositor.GetMirrorTextureD3D11(
                EVREye.Eye_Left,
                d3dDevice.NativePointer,
                ref mirrorTexturePtr
            );
            if (error != EVRCompositorError.None)
            {
                throw new Exception("GetMirrorTextureD3D11 feilet med error: " + error);
            }

            // Merk: _mirrorTexture.handle er en IntPtr til den interne ID3D11Texture2D.
            // For å bruke den i SharpDX, må vi "wrappe" denne pekeren i en Texture2D.
            // Dette krever at du kjenner dimensjonene og formatet til texture. 
            // I et reelt scenario bør du hente denne informasjonen dynamisk eller kjenne konfigurasjonen.
            var textureDesc = new Texture2DDescription
            {
                Width = 1920,          // Eksempelverdi – erstatt med korrekt bredde
                Height = 1080,         // Eksempelverdi – erstatt med korrekt høyde
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm, // Formatet må matche det som OpenVR returnerer
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            };

            // Her antar vi at _mirrorTexture.handle peker til et gyldig ID3D11Texture2D.
            // Denne konstruksjonen er pseudokode og kan kreve tilpasning, da SharpDX ikke nødvendigvis tilbyr
            // en direkte måte å wrappe en rå pointer på. Du må ofte bruke native interop eller D3D11Texture2D.FromNativePointer() 
            // (dersom tilgjengelig) for å gjøre dette.
            Texture2D mirrorTexture2D = SharpDX.ComObject.FromPointer<Texture2D>(_mirrorTexture.handle);

            return mirrorTexture2D;
        }
    }
}
