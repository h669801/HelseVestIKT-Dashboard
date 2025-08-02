using System;
using System.Globalization;
using System.Windows.Data;

namespace HelseVestIKT_Dashboard.Converters
{
	/// <summary>
	/// Converts a boolean IsLocked flag into the corresponding button text.
	/// True  → "Lås opp applikasjon"
	/// False → "Lås applikasjon"
	/// </summary>
	public class BoolToLockTextConverter : IValueConverter
	{
		/// <inheritdoc/>
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isLocked)
			{
				return isLocked
					? "Lås opp applikasjon"
					: "Lås applikasjon";
			}

			// Fallback if the binding isn't a bool
			return "Lås applikasjon";
		}

		/// <inheritdoc/>
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
