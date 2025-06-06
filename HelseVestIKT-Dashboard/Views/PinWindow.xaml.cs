﻿using System;
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


namespace HelseVestIKT_Dashboard.Views
{
	/// <summary>
	/// Interaction logic for PinWindow.xaml
	/// </summary>
	public partial class PinWindow : Window
	{
		private const string CorrectPin = "1234"; // Bytt til sikker lagring/hashing!

		public bool IsAuthenticated { get; private set; }

		public PinWindow()
		{
			InitializeComponent();
			PinBox.Focus();
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			if (PinBox.Password == CorrectPin)
			{
				IsAuthenticated = true;
				DialogResult = true;
				Close();
			}
			else
			{
				System.Windows.MessageBox.Show("Feil PIN", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
				IsAuthenticated = false;
			}
	
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			IsAuthenticated = false;
			DialogResult = false;
			Close();
		}

	}
}
