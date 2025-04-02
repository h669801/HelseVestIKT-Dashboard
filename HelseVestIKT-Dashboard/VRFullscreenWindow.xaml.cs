using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Forms.Integration;

namespace HelseVestIKT_Dashboard
{
	/// <summary>
	/// Interaction logic for VRFullscreenWindow.xaml
	/// </summary>
	/// 


	public partial class VRFullscreenWindow : Window
    {

		private VRFullscreenWindow _fullScreenWindow = null;
	
		public VRFullscreenWindow()
        {
            InitializeComponent();
        }

		public void SetVRContent(WindowsFormsHost vrHost)
		{
			FullscreenGrid.Children.Clear();
			FullscreenGrid.Children.Add(vrHost);
			vrHost.Visibility = Visibility.Visible;	

		}

		public void RemoveVRContent(WindowsFormsHost vrHost)
		{
			if (FullscreenGrid.Children.Contains(vrHost))
			{
				FullscreenGrid.Children.Remove(vrHost);
			}
		}

	}
}
