using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	public static class Win32
	{
		
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		// —— Kernel32 —— 
		[DllImport("kernel32.dll")]
		public static extern bool AllocConsole();

		// —— User32: grunnleggende vindus-API —— 
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		// —— SetWindowPos —— 
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetWindowPos(
			IntPtr hWnd,
			IntPtr hWndInsertAfter,
			int X, int Y, int cx, int cy,
			uint uFlags);

		public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
		public const uint SWP_NOMOVE = 0x0002;
		public const uint SWP_NOSIZE = 0x0001;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;

		// —— Get/SetWindowLong —— 
		[DllImport("user32.dll", SetLastError = true)]
		public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		public const int GWL_STYLE = -16;
		public const int GWL_EXSTYLE = -20;
		public const int WS_CHILD = 0x40000000;
		public const int WS_POPUP = unchecked((int)0x80000000);
		public const int WS_CAPTION = 0x00C00000;
		public const int WS_BORDER = 0x00800000;
		public const int WS_THICKFRAME = 0x00040000;
		public const int WS_SYSMENU = 0x00080000;
		public const int WS_MINIMIZEBOX = 0x00020000;
		public const int WS_MAXIMIZEBOX = 0x00010000;
		public const int WS_EX_TRANSPARENT = 0x00000020;

		// —— SendInput for å simulere ESC —— 
		[StructLayout(LayoutKind.Sequential)]
		public struct INPUT
		{
			public uint type;
			public InputUnion u;
			public static int Size => Marshal.SizeOf<INPUT>();
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct InputUnion
		{
			[FieldOffset(0)] public KEYBDINPUT ki;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct KEYBDINPUT
		{
			public ushort wVk;
			public ushort wScan;
			public uint dwFlags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		public const int INPUT_KEYBOARD = 1;
		public const uint KEYEVENTF_KEYUP = 0x0002;
		public const ushort VK_ESCAPE = 0x1B;

		[DllImport("user32.dll", SetLastError = true)]
		public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

		// —— Finn vindu på delstrenger —— 
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		public static IntPtr FindOverlayWindow()
		{
			IntPtr found = IntPtr.Zero;
			const int maxTitleLength = 256;

			EnumWindows((hWnd, lParam) =>
			{
				var sb = new StringBuilder(maxTitleLength);
				if (GetWindowText(hWnd, sb, maxTitleLength) > 0)
				{
					string title = sb.ToString();
					if (title.Contains("VR View") || title.Contains("VR-visning"))
					{
						found = hWnd;
						return false;   // stopp søket
					}
				}
				return true;            // fortsett søket
			}, IntPtr.Zero);

			return found;
		}

		/// <summary>
		/// Embed VR-overlay, gjør vinduet til barn, transparent for input, og fjerner rammer og systemstiler.
		/// </summary>
		public static void EmbedOverlay(IntPtr overlayHandle, IntPtr hostHandle, int width, int height)
		{
			// Hent og oppdater stil
			int style = GetWindowLong(overlayHandle, GWL_STYLE);
			// Fjern uønskede vindustiler for å låse vinduet til host
			style &= ~WS_POPUP;
			style &= ~WS_CAPTION;
			style &= ~WS_BORDER;
			style &= ~WS_THICKFRAME;
			style &= ~WS_SYSMENU;
			style &= ~WS_MINIMIZEBOX;
			style &= ~WS_MAXIMIZEBOX;
			// Legg til barn-stil
			style |= WS_CHILD;
			SetWindowLong(overlayHandle, GWL_STYLE, style);

			// Legg til transparent exstyle for input-pass-through
			int ex = GetWindowLong(overlayHandle, GWL_EXSTYLE);
			ex |= WS_EX_TRANSPARENT;
			SetWindowLong(overlayHandle, GWL_EXSTYLE, ex);

			// Sett parent og posisjon
			SetParent(overlayHandle, hostHandle);
			SetWindowPos(overlayHandle, IntPtr.Zero, 0, 0, width, height,
				SWP_NOZORDER | SWP_NOACTIVATE);
		}
	}
}
