using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;

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
		public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
		public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

		public const uint SWP_NOMOVE = 0x0002;
		public const uint SWP_NOSIZE = 0x0001;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;
		public const uint SWO_SHOWWINDOW = 0x0040;

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

		// —— Nye definisjoner for å gjenopprette vindu ——
		public const int WS_EX_LAYERED = 0x00080000;   // Layered vindu
		public const int WS_EX_NOACTIVATE = 0x08000000;   // Ikke aktiver ved klikk
		public const uint SWP_FRAMECHANGED = 0x0020;       // Tving repaint av ramme
		public const uint SWP_SHOWWINDOW = 0x0040;       // Vis vindu
		public const int SW_RESTORE = 9;            // Gjenopprett vindu

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
		public const int SW_SHOWNORMAL = 5;
		// —— Nytt: P/Invoke for å lese tastetrykk —— 

		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);

		private const int VK_MENU = 0x12;  // Alt
		private const int VK_TAB = 0x09;  // Tab
		private const int VK_CONTROL = 0x11;  // Ctrl
		private const int VK_LWIN = 0x5B;  // Venstre Win
		private const int VK_RWIN = 0x5C;  // Høyre Win
		private const int VK_F4 = 0X73;  // F4

		// —— Hook-infrastruktur —— 

		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private const int WM_SYSKEYDOWN = 0x0104;

		private static IntPtr _hookId = IntPtr.Zero;
		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
		private static LowLevelKeyboardProc _proc = HookCallback;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
													  IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll")]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
													IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);

		/// <summary>
		/// Aktiverer blokkering av Alt+Tab, Windows-taster og Ctrl+Esc.
		/// </summary>
		public static void EnableKeyBlock()
		{
			if (_hookId == IntPtr.Zero)
			{
				using var proc = Process.GetCurrentProcess();
				using var module = proc.MainModule;
				_hookId = SetWindowsHookEx(
					WH_KEYBOARD_LL,
					_proc,
					GetModuleHandle(module.ModuleName),
					0);
			}
		}

		/// <summary>
		/// Deaktiverer tastatur-hooken.
		/// </summary>
		public static void DisableKeyBlock()
		{
			if (_hookId != IntPtr.Zero)
			{
				UnhookWindowsHookEx(_hookId);
				_hookId = IntPtr.Zero;
			}
		}

		private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0 &&
			   (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
			{
				int vk = Marshal.ReadInt32(lParam);

				bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
				bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

				// Blokker Alt+Tab
				if (alt && vk == VK_TAB) return (IntPtr)1;
				// Blokker Ctrl+Esc
				if (ctrl && vk == VK_ESCAPE) return (IntPtr)1;
				// Blokker Windows-taster
				if (vk == VK_LWIN || vk == VK_RWIN) return (IntPtr)1;
				// Blokker Alt+F4
				if (alt && vk == VK_F4) return (IntPtr)1;
			}

			return CallNextHookEx(_hookId, nCode, wParam, lParam);
		}

		/// <summary>
		/// Setter vinduet til alltid øverst (TopMost).
		/// </summary>
		public static void SetWindowTopMost(IntPtr hWnd)
		{
			SetWindowPos(hWnd, HWND_TOPMOST,
						 0, 0, 0, 0,
						 SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
		}

		/// <summary>
		/// Bringer et eksternt vindu til forgrunnen, uten å beholde TopMost permanent.
		/// </summary>
		public static void BringToFront(IntPtr hWnd)
		{
			// 1) Gjenopprett hvis minimert
			ShowWindow(hWnd, SW_RESTORE);
			// 2) Bruk z-rekkefølge: sett øverst i stabelen (men ikke TopMost)
			SetWindowPos(
				hWnd,
				HWND_TOPMOST,
				0, 0, 0, 0,
				SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
			);
			// 3) Gi fokus
			SetForegroundWindow(hWnd);
		}


		//RESTART WINDOWS LOGIKK
		public const uint EWX_REBOOT = 0x00000002;
		public const uint EWX_FORCE = 0x00000004;
		public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
		public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
		public const uint TOKEN_QUERY = 0x0008;
		public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct TOKEN_PRIVILEGES
		{
			public uint PrivilegeCount;
			public LUID_AND_ATTRIBUTES Privileges;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct LUID_AND_ATTRIBUTES
		{
			public LUID Luid;
			public uint Attributes;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct LUID
		{
			public uint LowPart;
			public int HighPart;
		}

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool OpenProcessToken(
			IntPtr ProcessHandle,
			uint DesiredAccess,
			out IntPtr TokenHandle);

		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern bool LookupPrivilegeValue(
			string? lpSystemName,
			string lpName,
			out LUID lpLuid);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool AdjustTokenPrivileges(
			IntPtr TokenHandle,
			bool DisableAllPrivileges,
			ref TOKEN_PRIVILEGES NewState,
			uint BufferLength,
			IntPtr PreviousState,
			IntPtr ReturnLength);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool ExitWindowsEx(
			uint uFlags,
			uint dwReason);

		/// <summary>
		/// Starter en umiddelbar restart av Windows, tvinger alle apper til å lukke.
		/// Krever at prosessen kjører som administrator og har shutdown‐privilegiet.
		/// </summary>
		public static void RestartWindows()
		{
			// 1) Hent prosess‐token
			OpenProcessToken(
				Process.GetCurrentProcess().Handle,
				TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
				out var tokenHandle);

			// 2) Hent LUID
			LookupPrivilegeValue(
				null,
				SE_SHUTDOWN_NAME,
				out var luid);

			// 3) Aktiver privilege
			var tp = new TOKEN_PRIVILEGES
			{
				PrivilegeCount = 1,
				Privileges = new LUID_AND_ATTRIBUTES
				{
					Luid = luid,
					Attributes = SE_PRIVILEGE_ENABLED
				}
			};
			AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);

			// 4) Restart
			if (!ExitWindowsEx(EWX_REBOOT | EWX_FORCE, 0))
			{
				var err = Marshal.GetLastWin32Error();
				throw new System.ComponentModel.Win32Exception(err, "ExitWindowsEx feilet");
			}


		}
	}
}