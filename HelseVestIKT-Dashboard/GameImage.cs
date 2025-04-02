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
			string basePath = $@"C:\Program Files (x86)\Steam\appcache\librarycache\{appID}";

			if (!Directory.Exists(basePath))
			{
				Console.WriteLine($"Mappen for appID {appID} finnes ikke: {basePath}");
				return null;
			}

			string[] files = Directory.GetFiles(basePath, "library_600x900.jpg", SearchOption.AllDirectories);
			if (files.Length == 0)
			{
				Console.WriteLine($"Fant ingen 'library_600x900.jpg' i mappen {basePath}");
				return null;
			}

			string localPath = files[0];
			
			/*
			// Construct the local file path.
			// Adjust this path if your Steam installation is in a different location.
			string localPath = $@"C:\Program Files (x86)\Steam\appcache\librarycache\{appID}\library_600x900.jpg";

			if (!File.Exists(localPath))
			{
				Console.WriteLine($"Local image file not found: {localPath}");
				return null;
			}
			*/

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

        // Metode for å laste ikon fra en exe-fil
        public static BitmapImage? LoadIconFromExe(string exePath)
        {
            // Fjern null-tegn (U+0000) fra banen
            exePath = exePath.Replace("\0", "").Trim();

            Console.WriteLine("Testing File.Exists in LoadIconFromExe for: " + exePath);
            Console.WriteLine("Path length: " + exePath.Length);
            foreach (char c in exePath)
            {
                Console.WriteLine($"Char: '{c}' (U+{((int)c):X4})");
            }
            Console.WriteLine("Exists? " + File.Exists(exePath));

            if (!File.Exists(exePath))
            {
                Console.WriteLine($"Exe file not found: {exePath}");
                return null;
            }

            try
            {
                System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var bmp = icon.ToBitmap())
                        {
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        ms.Seek(0, SeekOrigin.Begin);

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = ms;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze(); // For trådsikkerhet.
                        return bitmapImage;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading icon from exe {exePath}: {ex.Message}");
                return null;
            }
        }
    }
}
