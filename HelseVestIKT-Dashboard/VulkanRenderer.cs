using Silk.NET.Core;
using Silk.NET.Vulkan;
using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan.Extensions.KHR;
//using Silk.NET.Vulkan.Extensions.KHR.Win32Surface;


public unsafe class VulkanRenderer : IDisposable
{
    private Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;

    // Grafikk-kø og tilhørende indekser
    private Queue _graphicsQueue;
    private uint _graphicsQueueFamilyIndex;

    // Kommando pool og primær kommando buffer
    private CommandPool _commandPool;
    private CommandBuffer _commandBuffer;

    // Synkroniseringsobjekter
    private Silk.NET.Vulkan.Semaphore _imageAvailableSemaphore;
    private Silk.NET.Vulkan.Semaphore _renderFinishedSemaphore;
    private Fence _inFlightFence;

    // Nye felt for overflate (surface) og swapchain
    private SurfaceKHR _surface;
    private SwapchainKHR _swapchain;
    private Silk.NET.Vulkan.Image[] _swapchainImages;
    private DeviceMemory[] _swapchainImageMemories;
    private Format _swapchainImageFormat;
    private Extent2D _swapchainExtent;

    public VulkanRenderer()
    {
        _vk = Vk.GetApi();
        //_vk = Vk.GetApi("vulkan-1.dll");

    }

    /// <summary>
    /// Hovedinitialisering: instans, enhet, kommandoressurser, osv.
    /// </summary>
    public void Initialize()
    {
        CreateInstance();
        SelectPhysicalDevice();
        FindQueueFamilies();
        CreateLogicalDevice();
        CreateCommandPool();
        CreateCommandBuffers();
        CreateSynchronizationObjects();
    }

    // --- Eksisterende metoder ---

    private void CreateInstance()
    {
        ApplicationInfo appInfo = new ApplicationInfo()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("MyVulkanApp"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("MyEngine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version11
        };

        // Legg til nødvendige instansutvidelser for overflatehåndtering.
        string[] instanceExtensions = new string[]
        {
        "VK_KHR_surface",
        "VK_KHR_win32_surface"
        };

        // Konverter strengene til pekere som kreves av Vulkan.
        IntPtr[] extensionNamePtrs = new IntPtr[instanceExtensions.Length];
        for (int i = 0; i < instanceExtensions.Length; i++)
        {
            extensionNamePtrs[i] = Marshal.StringToHGlobalAnsi(instanceExtensions[i]);
        }

        fixed (IntPtr* ppEnabledExtensionNames = extensionNamePtrs)
        {
            InstanceCreateInfo instanceCreateInfo = new InstanceCreateInfo()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)instanceExtensions.Length,
                PpEnabledExtensionNames = (byte**)ppEnabledExtensionNames,
            };

            if (_vk.CreateInstance(&instanceCreateInfo, null, out _instance) != Result.Success)
            {
                throw new Exception("Kunne ikke opprette Vulkan-instansen.");
            }
        }

        // Rydd opp strengeallokeringene
        for (int i = 0; i < instanceExtensions.Length; i++)
        {
            Marshal.FreeHGlobal(extensionNamePtrs[i]);
        }
        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
    }


    private void SelectPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
        if (deviceCount == 0)
            throw new Exception("Ingen GPU med Vulkan støtte funnet.");

        PhysicalDevice[] devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
        {
            _vk.EnumeratePhysicalDevices(_instance, &deviceCount, devicesPtr);
        }
        _physicalDevice = devices[0];
    }

    private void FindQueueFamilies()
    {
        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, null);
        if (queueFamilyCount == 0)
            throw new Exception("Ingen køfamilier tilgjengelig.");

        QueueFamilyProperties* queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, queueFamilies);

        bool found = false;
        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.QueueGraphicsBit) != 0)
            {
                _graphicsQueueFamilyIndex = i;
                found = true;
                break;
            }
        }
        if (!found)
            throw new Exception("Ingen egnet grafikk-køfamilie funnet.");
    }

    private void CreateLogicalDevice()
    {
        float queuePriority = 1.0f;
        DeviceQueueCreateInfo queueCreateInfo = new DeviceQueueCreateInfo()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamilyIndex,
            QueueCount = 1,
        };
        queueCreateInfo.PQueuePriorities = &queuePriority;

        // Legg til "VK_KHR_swapchain" i tillegg til de eksterne minneudvidelsene
        string[] deviceExtensions = new string[]
        {
            "VK_KHR_external_memory",
            "VK_KHR_external_memory_win32",
            "VK_KHR_swapchain"
        };

        IntPtr* extensionNames = stackalloc IntPtr[deviceExtensions.Length];
        for (int i = 0; i < deviceExtensions.Length; i++)
        {
            extensionNames[i] = Marshal.StringToHGlobalAnsi(deviceExtensions[i]);
        }

        DeviceCreateInfo deviceCreateInfo = new DeviceCreateInfo()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)extensionNames,
        };

        if (_vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, out _device) != Result.Success)
        {
            throw new Exception("Klarte ikke å opprette den logiske enheten.");
        }

        for (int i = 0; i < deviceExtensions.Length; i++)
        {
            Marshal.FreeHGlobal(extensionNames[i]);
        }

        _vk.GetDeviceQueue(_device, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
    }

    private void CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
        };

        if (_vk.CreateCommandPool(_device, &poolInfo, null, out _commandPool) != Result.Success)
            throw new Exception("Kunne ikke opprette kommando pool.");
    }

    private void CreateCommandBuffers()
    {
        CommandBufferAllocateInfo allocInfo = new CommandBufferAllocateInfo()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        CommandBuffer* commandBuffers = stackalloc CommandBuffer[1];
        if (_vk.AllocateCommandBuffers(_device, &allocInfo, commandBuffers) != Result.Success)
            throw new Exception("Kunne ikke allokere kommando buffer.");

        _commandBuffer = commandBuffers[0];
    }

    private void CreateSynchronizationObjects()
    {
        SemaphoreCreateInfo semaphoreInfo = new SemaphoreCreateInfo()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new FenceCreateInfo()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.FenceCreateSignaledBit,
        };

        if (_vk.CreateSemaphore(_device, &semaphoreInfo, null, out _imageAvailableSemaphore) != Result.Success ||
            _vk.CreateSemaphore(_device, &semaphoreInfo, null, out _renderFinishedSemaphore) != Result.Success ||
            _vk.CreateFence(_device, &fenceInfo, null, out _inFlightFence) != Result.Success)
        {
            throw new Exception("Kunne ikke opprette synkroniseringsobjekter.");
        }
    }

    // --- Swapchain-relaterte metoder ---

    /// <summary>
    /// Oppretter en Win32-overflate basert på vindushåndtak og HINSTANCE.
    /// </summary>
    /// <param name="windowHandle">Håndtak til vinduet (HWND)</param>
    /// <param name="hInstance">HINSTANCE til applikasjonen</param>
    public void CreateSurface(IntPtr windowHandle, IntPtr hInstance)
    {
        if (!_vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface) ||
        !_vk.TryGetInstanceExtension(_instance, out KhrWin32Surface khrWin32Surface))
        {
            throw new Exception("Required Vulkan extensions are not available.");
        }
        Console.WriteLine(windowHandle);
        Win32SurfaceCreateInfoKHR surfaceCreateInfo = new Win32SurfaceCreateInfoKHR()
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hinstance = hInstance,
            Hwnd = windowHandle,
        };

        if (khrWin32Surface.CreateWin32Surface(_instance, &surfaceCreateInfo, null, out _surface) != Result.Success)
        {
            throw new Exception("Could not create Win32 surface.");
        }

    }

    /// <summary>
    /// Oppretter en swapchain basert på den opprettede overflaten.
    /// Denne metoden henter overflatekapabiliteter og formater før
    /// den oppretter swapchainen, henter ut bildene og allokerer eksportabelt minne.
    /// </summary>
    public void CreateSwapchain()
    {
        if (!_vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface) ||
        !_vk.TryGetDeviceExtension(_instance, _device, out KhrSwapchain khrSwapchain))
        {
            throw new Exception("KHR_swapchain extension is not available.");
        }

        SurfaceCapabilitiesKHR capabilities;
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out capabilities);

        // 2. Hent støttede overflateformater
        uint formatCount = 0;
        khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null);
        if (formatCount == 0)
            throw new Exception("Ingen overflateformater tilgjengelig.");
        SurfaceFormatKHR[] formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* formatsPtr = formats)
        {
            khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formatsPtr);
        }
        // Velg foretrukket format – for eksempel B8G8R8A8_UNORM
        SurfaceFormatKHR chosenFormat = formats[0];
        for (int i = 0; i < formats.Length; i++)
        {
            if (formats[i].Format == Format.B8G8R8A8Unorm)
            {
                chosenFormat = formats[i];
                break;
            }
        }
        _swapchainImageFormat = chosenFormat.Format;

        // 3. Hent presentasjonsmoduser
        uint presentModeCount = 0;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, null); if (presentModeCount == 0)
            throw new Exception("Ingen presentasjonsmoduser tilgjengelig.");
        PresentModeKHR[] presentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* presentModesPtr = presentModes)
        {
            khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, presentModesPtr);
        }
        // Velg foretrukket presentasjonsmodus, f.eks. MAILBOX eller hvis ikke tilgjengelig FIFO
        PresentModeKHR chosenPresentMode = PresentModeKHR.FifoKhr; for (int i = 0; i < presentModes.Length; i++)
        {
            if (presentModes[i] == PresentModeKHR.MailboxKhr)
            {
                chosenPresentMode = PresentModeKHR.MailboxKhr;
                break;
            }
        }

        // 4. Bestem swapchain-oppløsning (extent)
        Extent2D extent;
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            extent = capabilities.CurrentExtent;
        }
        else
        {
            // Om oppløsningen er udefinert, velg en ønsket størrelse
            extent = new Extent2D { Width = 1280, Height = 720 };
            extent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, extent.Width));
            extent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, extent.Height));
        }
        _swapchainExtent = extent;

        // 5. Bestem antall bilder i swapchainen
        uint imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        // 6. Opprett SwapchainCreateInfo
        SwapchainCreateInfoKHR swapchainCreateInfo = new SwapchainCreateInfoKHR()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = chosenFormat.Format,
            ImageColorSpace = chosenFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            // Bruk ImageUsageFlags som passer ditt scenario; her brukte vi ColorAttachment og TransferDstBit
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = chosenPresentMode,
            Clipped = true,
            OldSwapchain = default
        };
        // For enkelthets skyld bruker vi eksklusiv deling (kun én køfamilie)
        swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
        swapchainCreateInfo.QueueFamilyIndexCount = 0;
        swapchainCreateInfo.PQueueFamilyIndices = null;

        // 7. Opprett swapchainen
        SwapchainKHR swapchain;
        if (khrSwapchain.CreateSwapchain(_device, &swapchainCreateInfo, null, out swapchain) != Result.Success)
            throw new Exception("Klarte ikke å opprette swapchain.");
        _swapchain = swapchain;

        // 8. Hent swapchain-bildene
        uint actualImageCount = 0;
        khrSwapchain.GetSwapchainImages(_device, _swapchain, &actualImageCount, null);
        _swapchainImages = new Silk.NET.Vulkan.Image[actualImageCount];
        fixed (Silk.NET.Vulkan.Image* imagesPtr = _swapchainImages)
        {
            khrSwapchain.GetSwapchainImages(_device, _swapchain, &actualImageCount, imagesPtr);
        }

        // 9. For hvert swapchain-bilde, alloker og bind eksportabelt minne
        _swapchainImageMemories = new DeviceMemory[actualImageCount];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            _swapchainImageMemories[i] = AllocateExportableMemory(_swapchainImages[i]);
        }
    }

    /// <summary>
    /// En eksempelmetode som henter et delt Win32-håndtak for det aktive swapchain-bildet.
    /// For enkelthets skyld hentes bilde med indeks 0. I en komplett implementasjon må du
    /// håndtere korrekt bildeutvelgelse og synkronisering.
    /// </summary>
    /// <returns>Win32-håndtak for minnet knyttet til swapchain-bildet</returns>
    public IntPtr GetCurrentSharedHandle()
    {
        // Her velger vi forenklet bilde-indeks 0
        uint imageIndex = 0;
        DeviceMemory memory = _swapchainImageMemories[imageIndex];

        if (!_vk.TryGetDeviceExtension(_instance, _device, out KhrExternalMemoryWin32 khrExternalMemoryWin32))
        {
            throw new Exception("KHR_external_memory_win32 extension is not available.");
        }

        MemoryGetWin32HandleInfoKHR handleInfo = new MemoryGetWin32HandleInfoKHR()
        {
            SType = StructureType.MemoryGetWin32HandleInfoKhr,
            Memory = memory,
            HandleType = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
        };

        IntPtr sharedHandle;
        if (khrExternalMemoryWin32.GetMemoryWin32Handle(_device, &handleInfo, &sharedHandle) != Result.Success)
            throw new Exception("Kunne ikke hente Win32-håndtak for minnet.");
        return sharedHandle;
    }

    // --- Eksisterende metoder for eksportabel bilde, allokering og rendering ---

    public Silk.NET.Vulkan.Image CreateExportableImage(Format format, uint width, uint height)
    {
        ImageCreateInfo imageInfo = new ImageCreateInfo()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D { Width = width, Height = height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = Silk.NET.Vulkan.ImageLayout.Undefined,
        };

        ExternalMemoryImageCreateInfo externalMemoryInfo = new ExternalMemoryImageCreateInfo()
        {
            SType = StructureType.ExternalMemoryImageCreateInfo,
            HandleTypes = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
        };

        imageInfo.PNext = &externalMemoryInfo;

        Silk.NET.Vulkan.Image image;
        if (_vk.CreateImage(_device, &imageInfo, null, &image) != Result.Success)
            throw new Exception("Klarte ikke å opprette et eksportabelt bilde.");
        return image;
    }

    public DeviceMemory AllocateExportableMemory(Silk.NET.Vulkan.Image image)
    {
        MemoryRequirements memRequirements;
        _vk.GetImageMemoryRequirements(_device, image, out memRequirements);

        uint memoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit);

        ExportMemoryAllocateInfo exportAllocInfo = new ExportMemoryAllocateInfo()
        {
            SType = StructureType.ExportMemoryAllocateInfo,
            HandleTypes = ExternalMemoryHandleTypeFlags.ExternalMemoryHandleTypeOpaqueWin32Bit,
        };

        MemoryAllocateInfo allocInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
            PNext = &exportAllocInfo,
        };

        DeviceMemory memory;
        if (_vk.AllocateMemory(_device, &allocInfo, null, out memory) != Result.Success)
            throw new Exception("Kunne ikke allokere enhetens minne.");

        if (_vk.BindImageMemory(_device, image, memory, 0) != Result.Success)
            throw new Exception("Kunne ikke binde bilde med minnet.");
        return memory;
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        PhysicalDeviceMemoryProperties memProperties;
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 &&
                (memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }
        throw new Exception("Kunne ikke finne en passende minnetype.");
    }

    public void RenderFrame()
    {
        // 1. Vent på at forrige frame skal være ferdig
        _vk.WaitForFences(_device, 1, in _inFlightFence, true, ulong.MaxValue);
        _vk.ResetFences(_device, 1, in _inFlightFence);

        if (!_vk.TryGetDeviceExtension(_instance, _device, out KhrSwapchain khrSwapchain))
            throw new Exception("KHR_swapchain extension is not available.");

        // 2. Hent neste tilgjengelige swapchain-bilde
        uint imageIndex = 0;
        var result = khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, &imageIndex);
        if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("Feil ved innhenting av swapchain-bilde.");
        }

        // 3. Begynn opptak av kommando-buffer
        CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = 0,
            PInheritanceInfo = null,
        };

        if (_vk.BeginCommandBuffer(_commandBuffer, &beginInfo) != Result.Success)
            throw new Exception("Kunne ikke begynne å spille inn kommando buffer.");

        // --- Her legger du inn dine rendering-kommandoer ---
        // For eksempel: layout-overganger, draw calls, pipeline barriers osv.
        // Du må sørge for at kommandoene retter seg mot _swapchainImages[imageIndex].

        if (_vk.EndCommandBuffer(_commandBuffer) != Result.Success)
            throw new Exception("Kunne ikke avslutte kommando buffer opptak.");

        // 4. Sett opp submit-info med korrekte synkroniseringsobjekter 
        // (her bør du inkludere ventende semaphorer og pipeline stage flags)
        // Opprett en peker til ventestadiene; her venter vi på at color attachment output skal være klar.
        PipelineStageFlags* waitStages = stackalloc PipelineStageFlags[1]
        { PipelineStageFlags.PipelineStageColorAttachmentOutputBit };

        // Sett opp SubmitInfo-strukturen med korrekte pekere.
        fixed (Silk.NET.Vulkan.Semaphore* pImageAvail = &_imageAvailableSemaphore)
        fixed (CommandBuffer* pCmdBuffer = &_commandBuffer)
        fixed (Silk.NET.Vulkan.Semaphore* pRenderFinished = &_renderFinishedSemaphore)
        {
            SubmitInfo submitInfo = new SubmitInfo()
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = pImageAvail,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = pCmdBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = pRenderFinished,
            };


            if (_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, _inFlightFence) != Result.Success)
                throw new Exception("Feil ved innsending av kommando buffer til køen.");

            // 5. Presentasjon: send bildet til skjermen
            fixed (SwapchainKHR* swapchainPtr = &_swapchain)
            {
                PresentInfoKHR presentInfo = new PresentInfoKHR()
                {
                    SType = StructureType.PresentInfoKhr,
                    WaitSemaphoreCount = 1,
                    // PWaitSemaphores: here you should assign the semaphore for render finished
                    SwapchainCount = 1,
                    PSwapchains = swapchainPtr, // Use the pinned pointer
                    PImageIndices = &imageIndex,
                };

                var presentResult = khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
                if (presentResult == Result.ErrorOutOfDateKhr || presentResult == Result.SuboptimalKhr)
                {
                    RecreateSwapchain();
                }
                else if (presentResult != Result.Success)
                {
                    throw new Exception("Error during image presentation.");
                }
            }


            // Du kan også kalle QueueWaitIdle om det er nødvendig, selv om dette kan redusere ytelsen.
            _vk.QueueWaitIdle(_graphicsQueue);
        }
    }



    public void RecreateSwapchain()
    {
        // Vent til GPU-en er inaktiv før du recreater swapchainen
        _vk.DeviceWaitIdle(_device);

        // Frigjør gamle swapchain-ressurser
        for (int i = 0; i < _swapchainImageMemories.Length; i++)
        {
            _vk.FreeMemory(_device, _swapchainImageMemories[i], null);
        }

        // Ensure the KhrSwapchain extension is loaded and used to destroy the swapchain
        if (_vk.TryGetDeviceExtension(_instance, _device, out KhrSwapchain khrSwapchain))
        {
            khrSwapchain.DestroySwapchain(_device, _swapchain, null);
        }
        else
        {
            throw new Exception("KHR_swapchain extension is not available.");
        }

        // Opprett en ny swapchain med de nye parametrene
        CreateSwapchain();
    }


    public void Dispose()
    {
        //_vk.DestroySwapchainKHR(_device, _swapchain, null);
        //_vk.DestroySurfaceKHR(_instance, _surface, null);

        if (_vk.TryGetDeviceExtension(_instance, _device, out KhrSwapchain khrSwapchain))
        {
            khrSwapchain.DestroySwapchain(_device, _swapchain, null);
        }
        if (_vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface))
        {
            khrSurface.DestroySurface(_instance, _surface, null);
        }

        _vk.DestroyFence(_device, _inFlightFence, null);
        _vk.DestroySemaphore(_device, _renderFinishedSemaphore, null);
        _vk.DestroySemaphore(_device, _imageAvailableSemaphore, null);
        _vk.DestroyCommandPool(_device, _commandPool, null);
        _vk.DestroyDevice(_device, null);
        _vk.DestroyInstance(_instance, null);
    }
}
