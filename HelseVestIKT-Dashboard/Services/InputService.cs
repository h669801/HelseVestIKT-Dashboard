using HelseVestIKT_Dashboard.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HelseVestIKT_Dashboard.Services
{
    public class InputService
    {
		public void HandleKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			//Blokkerer ALT + F4
			if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
				e.Handled = true;

			//Blokkerer Windows-tast
			if (e.Key == Key.LWin || e.Key == Key.RWin)
				e.Handled = true;

			if (e.Key == Key.M)
				e.Handled = true;
			

			if (e.Key == Key.H)
				e.Handled = true;
		}
	}
}
