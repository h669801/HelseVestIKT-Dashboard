using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct3D9;
using System.Windows;
using System.Windows.Interop;

namespace HelseVestIKT_Dashboard
{
    public class D3D9Interop
    {
        public DeviceEx D3D9Device { get; private set; }

        public D3D9Interop(IntPtr windowHandle)
        {
            // Opprett Direct3D9Ex-kontext
            Direct3DEx d3dContext = new Direct3DEx();
            PresentParameters presentParams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = windowHandle,
                PresentationInterval = PresentInterval.Immediate
            };

            D3D9Device = new DeviceEx(
                d3dContext,
                0,
                DeviceType.Hardware,
                IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded,
                presentParams);
        }
    }

}
