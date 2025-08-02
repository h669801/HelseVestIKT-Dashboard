﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace HelseVestIKT_Dashboard.Converters
{
	public class InverseBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo c)
		  => (value is bool b && b)
			  ? Visibility.Collapsed
			  : Visibility.Visible;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo c)
		  => throw new NotImplementedException();
	}
}
