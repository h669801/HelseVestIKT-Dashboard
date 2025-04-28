using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	public class Win32
	{

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		public const int GWL_STYLE = -16;
		public const int WS_CHILD = 0x40000000;
		public const int WS_CAPTION = 0x00C00000;
		public const int WS_BORDER = 0x00800000;
		public const uint SWP_NOZORDER = 0x0004;
		public const uint SWP_NOACTIVATE = 0x0010;


		// Nye metoder for å finne vinduer basert på delstreng i vindustittel
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		/// <summary>
		/// Finn det første top-level vinduet hvis tittel inneholder noen av de oppgitte nøkkel-strengene.
		/// </summary>
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
					string title = sb.ToString();
					foreach (var part in substrings)
					{
						if (title.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							found = hWnd;
							return false; // stopp enum
						}
					}
				}
				return true; // fortsett enum
			}, IntPtr.Zero);

			return found;
		}
	}
}


