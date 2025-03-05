using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace HelseVestIKT_Dashboard
{
	public static class GameImage
	{
		/// <summary>
		/// Loads an image from your local Steam library cache based on the appID.
		/// </summary>
		public static BitmapImage? LoadLocalGameImage(string appID)
		{
			// Construct the local file path.
			// Adjust this path if your Steam installation is in a different location.
			string localPath = $@"C:\Program Files (x86)\Steam\appcache\librarycache\{appID}\library_600x900.jpg";

			if (!File.Exists(localPath))
			{
				Console.WriteLine($"Local image file not found: {localPath}");
				return null;
			}

			try
			{
				BitmapImage bitmap = new BitmapImage();
				bitmap.BeginInit();
				bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.EndInit();
				bitmap.Freeze(); // Allows cross-thread access.
				return bitmap;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading local image for AppID {appID}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Downloads and creates a BitmapImage from an online URL.
		/// </summary>
		public static async Task<BitmapImage?> LoadOnlineGameImageAsync(string imageUrl)
		{
			try
			{
				using (HttpClient client = new HttpClient())
				{
					byte[] imageData = await client.GetByteArrayAsync(imageUrl);
					BitmapImage bitmap = new BitmapImage();
					using (var stream = new MemoryStream(imageData))
					{
						bitmap.BeginInit();
						bitmap.CacheOption = BitmapCacheOption.OnLoad;
						bitmap.StreamSource = stream;
						bitmap.EndInit();
						bitmap.Freeze(); // Allows cross-thread access.
					}
					return bitmap;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading online image from {imageUrl}: {ex.Message}");
				return null;
			}
		}
	}
}
