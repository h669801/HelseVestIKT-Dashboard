using Silk.NET.OpenXR;
using Silk.NET.Core.Native;
using System;
using SystemId = System.UInt64;
using Silk.NET.Core.Loader;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct3D9;



namespace HelseVestIKT_Dashboard
{
    public class OpenXRManager : IDisposable
    {
        private XrLoader _xrLoader; // Felt for xrLoader
        public Instance Instance { get; private set; }
        public ulong SystemId { get; private set; } // Alias for SystemId = UInt64
        public bool IsInitialized { get; private set; } = false;


        // Grafikkrelatert: lagre sesjonen og den D3D11-enheten du oppretter
        public Session Session { get; private set; }
        public SharpDX.Direct3D11.Device D3D11Device { get; private set; }

        // Swapchain- og view-relaterte variabler
        private Swapchain[] _swapchains;
        private ViewConfigurationView[] _viewConfigs;
        // Her lagres de individuelle swapchain-bildene (teksturene) for hver view
        private SwapchainImageD3D11KHR[][] _swapchainImages;
        private bool _isRunning = false;

        // Definerer delegeringstypen for xrEnumerateViewConfigurationViews
        private unsafe delegate* unmanaged<Instance, ulong, ViewConfigurationType, uint, uint*, ViewConfigurationView*, Result> _enumerateViewConfigurationViews;

        private SharpDX.Direct3D11.Texture2D _sharedTexture;



        //TEST
        private SharpDX.Direct3D11.Buffer _vertexBuffer;
        private SharpDX.Direct3D11.VertexShader _vertexShader;
        private SharpDX.Direct3D11.PixelShader _pixelShader;
        private SharpDX.Direct3D11.InputLayout _inputLayout;


        //public unsafe struct ApplicationInfo
        //{
        //    public fixed byte ApplicationName[128];
        //    public uint ApplicationVersion;
        //    public fixed byte EngineName[128];
        //    public uint EngineVersion;
        //    public ulong ApiVersion;
        //}

        public unsafe bool Initialize()
        {

            DllChecker.TestLoadLibrary();
            DllChecker.TestLoadFullPath();

            //if (DllChecker.IsDllLoaded("openxr_loader.dll"))
            //{
            //    Console.WriteLine("openxr_loader.dll er lastet.");
            //}
            //else
            //{
            //    Console.WriteLine("openxr_loader.dll er ikke lastet.");
            //}


            var loader = LibraryLoader.GetPlatformDefaultLoader();
            if (loader == null)
            {
                Console.WriteLine("Feil: PlatformDefaultLoader er null.");
                return false;
            }


            // 1. Opprett xrLoader og hent xr-objektet
            //_xrLoader = new XrLoader(LibraryLoader.GetPlatformDefaultLoader());
            //var xr = _xrLoader.OpenXR;

            Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json");

            var customLoader = new CustomLibraryLoader(new[] { @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64" });
            _xrLoader = new XrLoader(customLoader);
            var xr = _xrLoader.OpenXR;
            Console.WriteLine(xr);

            if (xr == null)
            {
                Console.WriteLine("Feil: xr er null. Sjekk at OpenXR-runtime er riktig installert og at alle avhengigheter er på plass.");
                return false;
            }

            // 2. Forbered ApplicationInfo og InstanceCreateInfo
            ApplicationInfo appInfo = new ApplicationInfo();
            SetFixedString(appInfo.ApplicationName, "My OpenXR WPF App", 128);
            SetFixedString(appInfo.EngineName, "MyEngine", 128);
            appInfo.ApplicationVersion = 1;
            appInfo.EngineVersion = 1;
            appInfo.ApiVersion = 1;

            string[] extensions = Array.Empty<string>();
            byte** enabledExtensionNames = stackalloc byte*[extensions.Length];
            for (int i = 0; i < extensions.Length; i++)
            {
                enabledExtensionNames[i] = (byte*)SilkMarshal.StringToPtr(extensions[i], NativeStringEncoding.UTF8);
            }

            InstanceCreateInfo createInfo = new InstanceCreateInfo(StructureType.InstanceCreateInfo)
            {
                ApplicationInfo = appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                EnabledExtensionNames = enabledExtensionNames
            };

            // 3. Opprett OpenXR-instansen
            Instance instance = default;
            Result result = xr.CreateInstance(&createInfo, ref instance);
            if (result != Result.Success)
            {
                Console.WriteLine("Feil: Kunne ikke opprette OpenXR-instans.");
                return false;
            }
            Instance = instance;
            Console.WriteLine("OpenXR-instans opprettet!");

            // 4. Hent prosedyreadressen for xrEnumerateViewConfigurationViews
            byte* functionNamePtr = (byte*)SilkMarshal.StringToPtr("xrEnumerateViewConfigurationViews", NativeStringEncoding.UTF8);
            Silk.NET.Core.PfnVoidFunction procAddr;
            Result procResult = xr.GetInstanceProcAddr(Instance, functionNamePtr, &procAddr);
            SilkMarshal.Free((nint)functionNamePtr);
            if (procResult != Result.Success || procAddr.Handle == default(delegate* unmanaged[Cdecl]<void>))
            {
                Console.WriteLine("Feil: Kunne ikke hente xrEnumerateViewConfigurationViews-prosedyren.");
                return false;
            }
            _enumerateViewConfigurationViews = (delegate* unmanaged<Instance, ulong, ViewConfigurationType, uint, uint*, ViewConfigurationView*, Result>)(void*)procAddr.Handle;

            // 5. Hent system-ID for HMD
            SystemGetInfo systemGetInfo = new SystemGetInfo(StructureType.SystemGetInfo)
            {
                FormFactor = FormFactor.HeadMountedDisplay
            };
            ulong systemIdLocal = default;
            result = xr.GetSystem(Instance, &systemGetInfo, ref systemIdLocal);
            if (result != Result.Success)
            {
                Console.WriteLine("Feil: Kunne ikke hente OpenXR-system.");
                xr.DestroyInstance(Instance);
                return false;
            }
            SystemId = systemIdLocal;
            Console.WriteLine($"OpenXR-system-ID: {SystemId}");

            // 6. Opprett Direct3D 11-enheten med BgraSupport (kreves for WPF-interoperabilitet)
            D3D11Device = new SharpDX.Direct3D11.Device(
                SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
            Console.WriteLine("Direct3D11-enhet opprettet.");

            // 7. Sett opp grafikkbinding for D3D11
            GraphicsBindingD3D11KHR graphicsBinding = new GraphicsBindingD3D11KHR
            {
                Type = StructureType.GraphicsBindingD3D11Khr,
                Next = null,
                Device = (void*)D3D11Device.NativePointer
            };

            // 8. Opprett en OpenXR-sesjon med D3D11-binding
            SessionCreateInfo sessionCreateInfo = new SessionCreateInfo(StructureType.SessionCreateInfo)
            {
                Next = &graphicsBinding,
                SystemId = SystemId
            };
            Session session = default;
            result = xr.CreateSession(Instance, &sessionCreateInfo, ref session);
            if (result != Result.Success)
            {
                Console.WriteLine("Feil: Kunne ikke opprette OpenXR-sesjon.");
                xr.DestroyInstance(Instance);
                return false;
            }
            Session = session;
            Console.WriteLine("OpenXR-sesjon opprettet!");

            // 9. Opprett swapchains basert på view-konfigurasjoner
            if (!CreateSwapchains())
            {
                Console.WriteLine("Feil: Kunne ikke opprette swapchains.");
                return false;
            }

            // 10. Hent swapchain-bildene fra hver swapchain
            if (!CreateSwapchainImages())
            {
                Console.WriteLine("Feil: Kunne ikke hente swapchain-bilder.");
                return false;
            }

            // 11. Initialiser rendering-ressurser (vertex buffers, shaders, input layout, etc.)
            InitializeRenderingResources();

            // 12. Start render-loop på en egen tråd
            _isRunning = true;
            Task.Run(() => RenderLoop());

            IsInitialized = true;
            return true;
        }




        // Opprett swapchains basert på view-konfigurasjoner
        private unsafe bool CreateSwapchains()
        {
            var xr = _xrLoader.OpenXR;

            // Hent antall views ved hjelp av den lastede funksjonen.
            uint viewCount = 0;
            _enumerateViewConfigurationViews(Instance, SystemId, ViewConfigurationType.PrimaryStereo, 0, &viewCount, null);
            if (viewCount == 0)
            {
                Console.WriteLine("Ingen views tilgjengelig.");
                return false;
            }

            _viewConfigs = new ViewConfigurationView[viewCount];
            for (int i = 0; i < viewCount; i++)
            {
                _viewConfigs[i] = new ViewConfigurationView(StructureType.ViewConfigurationView);
            }

            // Bruk en fixed-blokk for å få en pointer til arrayen.
            fixed (ViewConfigurationView* pViewConfigs = _viewConfigs)
            {
                _enumerateViewConfigurationViews(Instance, SystemId, ViewConfigurationType.PrimaryStereo, viewCount, &viewCount, pViewConfigs);
            }

            _swapchains = new Swapchain[viewCount];
            for (int i = 0; i < viewCount; i++)
            {
                var viewConfig = _viewConfigs[i];
                SwapchainCreateInfo swapchainCreateInfo = new SwapchainCreateInfo(StructureType.SwapchainCreateInfo)
                {
                    // Bruk de riktige flaggene: ColorAttachmentBit og TransferDstBit
                    UsageFlags = SwapchainUsageFlags.ColorAttachmentBit | SwapchainUsageFlags.TransferDstBit,
                    // Bruk SharpDX.DXGI.Format for format
                    Format = (long)SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    SampleCount = viewConfig.RecommendedSwapchainSampleCount,
                    Width = viewConfig.RecommendedImageRectWidth,
                    Height = viewConfig.RecommendedImageRectHeight,
                    FaceCount = 1,
                    ArraySize = 1,
                    MipCount = 1
                };

                Swapchain swapchain = default;
                Result result = xr.CreateSwapchain(Session, &swapchainCreateInfo, ref swapchain);
                if (result != Result.Success)
                {
                    Console.WriteLine($"Feil: Kunne ikke opprette swapchain for view {i}");
                    return false;
                }
                _swapchains[i] = swapchain;
                Console.WriteLine($"Swapchain for view {i} opprettet med størrelse {swapchainCreateInfo.Width}x{swapchainCreateInfo.Height}");
            }
            return true;
        }


        // Hent swapchain-bildene (teksturene) for hver swapchain
        private unsafe bool CreateSwapchainImages()
        {
            var xr = _xrLoader.OpenXR;
            _swapchainImages = new SwapchainImageD3D11KHR[_swapchains.Length][];
            for (int i = 0; i < _swapchains.Length; i++)
            {
                uint imageCount = 0;
                Result result = xr.EnumerateSwapchainImages(_swapchains[i], 0, ref imageCount, null);
                if (result != Result.Success || imageCount == 0)
                {
                    Console.WriteLine($"Feil: Kunne ikke hente antall swapchain-bilder for view {i}.");
                    return false;
                }
                SwapchainImageD3D11KHR[] images = new SwapchainImageD3D11KHR[imageCount];
                for (int j = 0; j < imageCount; j++)
                {
                    images[j] = new SwapchainImageD3D11KHR(StructureType.SwapchainImageD3D11Khr);
                }
                fixed (SwapchainImageD3D11KHR* pImages = images)
                {
                    result = xr.EnumerateSwapchainImages(_swapchains[i], imageCount, ref imageCount, (SwapchainImageBaseHeader*)pImages);
                }
                if (result != Result.Success)
                {
                    Console.WriteLine($"Feil: Kunne ikke hente swapchain-bilder for view {i}.");
                    return false;
                }
                _swapchainImages[i] = images;
                Console.WriteLine($"Hentet {imageCount} swapchain-bilder for view {i}.");
            }
            return true;
        }



        // Render-loop som håndterer frame-syklusen og swapchain-bildene
        private unsafe void RenderLoop()
        {
            var xr = _xrLoader.OpenXR;
            while (_isRunning)
            {
                // Vent på neste frame
                FrameWaitInfo frameWaitInfo = new FrameWaitInfo(StructureType.FrameWaitInfo);
                FrameState frameState = new FrameState(StructureType.FrameState);
                xr.WaitFrame(Session, &frameWaitInfo, &frameState);

                // Start frame
                FrameBeginInfo frameBeginInfo = new FrameBeginInfo(StructureType.FrameBeginInfo);
                xr.BeginFrame(Session, &frameBeginInfo);

                // Gå gjennom hver swapchain (f.eks. for hvert øye)
                for (int i = 0; i < _swapchains.Length; i++)
                {
                    // Acquirér et swapchain-image
                    SwapchainImageAcquireInfo acquireInfo = new SwapchainImageAcquireInfo(StructureType.SwapchainImageAcquireInfo);
                    uint imageIndex = 0;
                    xr.AcquireSwapchainImage(_swapchains[i], &acquireInfo, ref imageIndex);

                    // Vent på at bildet er klart
                    SwapchainImageWaitInfo imageWaitInfo = new SwapchainImageWaitInfo(StructureType.SwapchainImageWaitInfo)
                    {
                        Timeout = 1000000000 // 1 sekund i nanosekunder
                    };
                    xr.WaitSwapchainImage(_swapchains[i], &imageWaitInfo);

                    // Her utføres renderingen med Direct3D11
                    RenderSwapchainImage(i, imageIndex);

                    // Frigjør swapchain-imaget
                    SwapchainImageReleaseInfo releaseInfo = new SwapchainImageReleaseInfo(StructureType.SwapchainImageReleaseInfo);
                    xr.ReleaseSwapchainImage(_swapchains[i], &releaseInfo);
                }

                // Avslutt frame – her sender du også med layer-informasjon hvis du har opprettet slike
                FrameEndInfo frameEndInfo = new FrameEndInfo(StructureType.FrameEndInfo)
                {
                    DisplayTime = frameState.PredictedDisplayTime,
                    EnvironmentBlendMode = EnvironmentBlendMode.Opaque,
                    LayerCount = 0,
                    Layers = null
                };
                xr.EndFrame(Session, &frameEndInfo);
            }
        }

        // Rendering av et spesifikt swapchain-bilde med Direct3D11
        private unsafe void RenderSwapchainImage(int viewIndex, uint imageIndex)
        {
            // Sjekk at vi har swapchain-bilder for den gitte viewIndex
            if (_swapchainImages == null || viewIndex >= _swapchainImages.Length)
            {
                Console.WriteLine($"Swapchain images not available for view {viewIndex}.");
                return;
            }

            // Hent swapchain-bildet for angitt view og imageIndex.
            // Her antas det at strukturen har et felt som heter D3D11Texture.
            // Dersom feltet heter noe annet (f.eks. D3D11TexturePtr), oppdater koden.
            SwapchainImageD3D11KHR image = _swapchainImages[viewIndex][imageIndex];
            IntPtr texturePtr = (IntPtr)image.Texture;

            // Konverter den native pekeren til en SharpDX resource ved hjelp av COM-interoperabilitet.
            // Dette krever at den underliggende ressursen implementerer IUnknown.
            var resource = (SharpDX.Direct3D11.Resource)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(texturePtr);

            // Opprett et RenderTargetView fra resource-objektet.
            using (var rtv = new SharpDX.Direct3D11.RenderTargetView(D3D11Device, resource))
            {
                // Hent view-konfigurasjonen for å sette opp viewport.
                var viewConfig = _viewConfigs[viewIndex];
                // Bruk SharpDX.Viewport (som ligger i SharpDX-namespace, ikke Direct3D11)
                RawViewportF viewport = new RawViewportF
                {
                    X = 0,
                    Y = 0,
                    Width = viewConfig.RecommendedImageRectWidth,
                    Height = viewConfig.RecommendedImageRectHeight,
                    MinDepth = 0.0f,
                    MaxDepth = 1.0f
                };
                D3D11Device.ImmediateContext.Rasterizer.SetViewport(viewport);

                // Tøm render target view med en valgt clear-farge.
                SharpDX.Mathematics.Interop.RawColor4 clearColor = new SharpDX.Mathematics.Interop.RawColor4(0.1f, 0.2f, 0.3f, 1.0f);
                D3D11Device.ImmediateContext.ClearRenderTargetView(rtv, clearColor);
                D3D11Device.ImmediateContext.OutputMerger.SetRenderTargets(rtv);

                //TEST
                // Nå legger vi til ytterligere rendering-logikk – for eksempel å tegne en enkel trekant:
                D3D11Device.ImmediateContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
                // Bind vertex buffer (hvis du har initialisert _vertexBuffer)
                var vertexBufferBinding = new SharpDX.Direct3D11.VertexBufferBinding(_vertexBuffer, SharpDX.Utilities.SizeOf<Vertex>(), 0);
                D3D11Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
                // Sett shaders
                D3D11Device.ImmediateContext.VertexShader.Set(_vertexShader);
                D3D11Device.ImmediateContext.PixelShader.Set(_pixelShader);
                // Utfør draw-kall for 3 vertices (en trekant)
                D3D11Device.ImmediateContext.Draw(3, 0);
                //TEST


                Console.WriteLine($"Rendering for view {viewIndex}, image index {imageIndex}");

                // Her legger du inn ytterligere rendering-logikk (draw calls, shader-binding, osv.)
            }
        }


        public void Dispose()
        {
            if (IsInitialized)
            {
                var xr = _xrLoader.OpenXR;
                xr.DestroyInstance(Instance);
                IsInitialized = false;
            }
        }


        //TEST
        public struct Vertex
        {
            public SharpDX.Mathematics.Interop.RawVector3 Position;
            public SharpDX.Mathematics.Interop.RawVector4 Color;
            public Vertex(SharpDX.Mathematics.Interop.RawVector3 position, SharpDX.Mathematics.Interop.RawVector4 color)
            {
                Position = position;
                Color = color;
            }
        }

        //TEST
        private void InitializeRenderingResources()
        {
            // Definer trekanthjørnene
            Vertex[] vertices = new Vertex[]
            {
        new Vertex(new SharpDX.Mathematics.Interop.RawVector3(0.0f, 0.5f, 0.5f), new SharpDX.Mathematics.Interop.RawVector4(1, 0, 0, 1)),    // topp
        new Vertex(new SharpDX.Mathematics.Interop.RawVector3(0.5f, -0.5f, 0.5f), new SharpDX.Mathematics.Interop.RawVector4(0, 1, 0, 1)),   // høyre
        new Vertex(new SharpDX.Mathematics.Interop.RawVector3(-0.5f, -0.5f, 0.5f), new SharpDX.Mathematics.Interop.RawVector4(0, 0, 1, 1))   // venstre
            };

            // Lag vertex buffer
            _vertexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, SharpDX.Direct3D11.BindFlags.VertexBuffer, vertices);
            
            // Kompiler shaders – her bruker vi prekompilerte HLSL-filer som eksempel.
            // Du kan bruke SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile for å kompilere i runtime.
            var vertexShaderByteCode = SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("VertexShader.hlsl", "main", "vs_4_0");
            _vertexShader = new SharpDX.Direct3D11.VertexShader(D3D11Device, vertexShaderByteCode);
            var pixelShaderByteCode = SharpDX.D3DCompiler.ShaderBytecode.CompileFromFile("PixelShader.hlsl", "main", "ps_4_0");
            _pixelShader = new SharpDX.Direct3D11.PixelShader(D3D11Device, pixelShaderByteCode);

            // Opprett input layout som matcher shaderens input-signatur og Vertex-strukturen.
            _inputLayout = new SharpDX.Direct3D11.InputLayout(D3D11Device,
                SharpDX.D3DCompiler.ShaderSignature.GetInputSignature(vertexShaderByteCode),
                new[]
                {
            new SharpDX.Direct3D11.InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0, 0),
            new SharpDX.Direct3D11.InputElement("COLOR", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 12, 0)
                });

            // Sett input layout til immediate context
            D3D11Device.ImmediateContext.InputAssembler.InputLayout = _inputLayout;

            // Frigjør shader bytecode (kan også lagres om nødvendig)
            vertexShaderByteCode.Dispose();
            pixelShaderByteCode.Dispose();
        }



        private void CreateSharedTexture(int width, int height)
        {
            var textureDesc = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                BindFlags = SharpDX.Direct3D11.BindFlags.RenderTarget,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.Shared
            };

            _sharedTexture = new SharpDX.Direct3D11.Texture2D(D3D11Device, textureDesc);
        }




        public IntPtr GetSharedTexture()
        {
            // Hvis den delte teksturen ikke allerede er opprettet, lager vi den.
            if (_sharedTexture == null)
            {
                int width = _viewConfigs != null && _viewConfigs.Length > 0
    ? (int)_viewConfigs[0].RecommendedImageRectWidth
    : 800;  // fallback-bredde
                int height = _viewConfigs != null && _viewConfigs.Length > 0
                    ? (int)_viewConfigs[0].RecommendedImageRectHeight
                    : 600;  // fallback-høyde


                CreateSharedTexture(width, height);
            }

            // Bruk QueryInterface for å få DXGI.Resource fra _sharedTexture og hent SharedHandle.
            using (var dxgiResource = _sharedTexture.QueryInterface<SharpDX.DXGI.Resource>())
            {
                return dxgiResource.SharedHandle;
            }
        }





        public unsafe static void SetFixedString(byte* destination, string value, int maxLength)
        {
            // Få UTF8-bytene for strengen
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            int len = Math.Min(bytes.Length, maxLength - 1);
            for (int i = 0; i < len; i++)
            {
                destination[i] = bytes[i];
            }
            // Null-terminer
            destination[len] = 0;
        }
    }

}
