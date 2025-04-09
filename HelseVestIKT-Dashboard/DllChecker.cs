using System;
using System.Runtime.InteropServices;

public static class DllChecker
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    //private static extern IntPtr GetModuleHandle(string lpModuleName);

    //public static bool IsDllLoaded(string dllName)
    //{
    //    IntPtr handle = GetModuleHandle(dllName);
    //    return handle != IntPtr.Zero;
    //}



    public static void TestLoadLibrary()
    {
        IntPtr handle = LoadLibrary("openxr_loader.dll");
        if (handle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Console.WriteLine($"LoadLibrary feilet med feilkode: {error}");
        }
        else
        {
            Console.WriteLine("LoadLibrary: openxr_loader.dll ble lastet.");
        }
    }


    public static void TestLoadFullPath()
    {
        try
        {
            // Oppgi full sti til din openxr_loader.dll
            string fullPath = @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64\openxr_loader.dll";
            nint handle = NativeLibrary.Load(fullPath);
            if (handle == 0)
            {
                Console.WriteLine("NativeLibrary.Load feilet.");
            }
            else
            {
                Console.WriteLine("NativeLibrary.Load: openxr_loader.dll ble lastet med full sti.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Feil ved lastning: {ex.Message}");
        }
    }
}
