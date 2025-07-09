using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HelseVestIKT_Dashboard.Services
{
	
		public class StatusBarService
		{
			private readonly UIElement _statusBar;

			public StatusBarService(UIElement bar)
			{
				_statusBar = bar;
				_statusBar.Visibility = Visibility.Collapsed;
			}

			public void Show() => _statusBar.Visibility = Visibility.Visible;
			public void Hide()  => _statusBar.Visibility = Visibility.Collapsed;
		}
}
