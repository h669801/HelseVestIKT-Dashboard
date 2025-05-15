using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HelseVestIKT_Dashboard.Models

{
	public static class StockIcons
	{
		// Structure for SHGetStockIconInfo
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHSTOCKICONINFO
		{
			public uint cbSize;
			public IntPtr hIcon;
			public int iSysImageIndex;
			public int iIcon;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szPath;
		}

		// Stock icon IDs (check documentation for exact values)
		public enum SHSTOCKICONID : uint
		{
			SIID_VOLUME = 20, // This ID represents the speaker/volume icon.
							  // You can add more stock icon IDs if needed.
		}

		// Flags for SHGetStockIconInfo
		public const uint SHGSI_ICON = 0x000000100;
		public const uint SHGSI_SMALLICON = 0x000000001;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		public static extern int SHGetStockIconInfo(SHSTOCKICONID siid, uint uFlags, ref SHSTOCKICONINFO psii);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool DestroyIcon(IntPtr hIcon);

		public static ImageSource GetVolumeIcon()
		{
			SHSTOCKICONINFO info = new SHSTOCKICONINFO();
			info.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));
			int result = SHGetStockIconInfo(SHSTOCKICONID.SIID_VOLUME, SHGSI_ICON | SHGSI_SMALLICON, ref info);
			if (result == 0 && info.hIcon != IntPtr.Zero)
			{
				ImageSource img = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				DestroyIcon(info.hIcon); // Free the HICON resource
				return img;
			}
			return null;
		}
	}
}
