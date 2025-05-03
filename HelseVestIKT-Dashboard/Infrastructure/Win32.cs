using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	public static class Win32
	{
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
		public const int WS_CHILD = 0x40000000;
		public const int WS_CAPTION = 0x00C00000;
		public const int WS_BORDER = 0x00800000;

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

		public static IntPtr FindWindowByTitleSubstrings(params string[] substrings)
		{
			IntPtr found = IntPtr.Zero;
			EnumWindows((hWnd, _) =>
			{
				int len = GetWindowTextLength(hWnd);
				if (len > 0)
				{
					var sb = new StringBuilder(len + 1);
					GetWindowText(hWnd, sb, sb.Capacity);
					var title = sb.ToString();
					foreach (var s in substrings)
						if (title.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							found = hWnd;
							return false; // stopp videre
						}
				}
				return true; // fortsett
			}, IntPtr.Zero);
			return found;
		}
	}
}
