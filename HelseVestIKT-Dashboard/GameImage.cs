using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace HelseVestIKT_Dashboard
{
    public static class SteamImageHelper
    {
        public static async Task<BitmapImage?> GetSteamGameImageAsync(string appId)
        {
            string apiUrl = $"http://store.steampowered.com/api/appdetails?appids={appId}";
            using HttpClient client = new HttpClient();
            var response = await client.GetStringAsync(apiUrl);

            using JsonDocument doc = JsonDocument.Parse(response);

            // Check if the JSON contains an element for the given appId
            if (!doc.RootElement.TryGetProperty(appId, out JsonElement appElement))
            {
                // The appId key was not found
                return null;
            }

            // Check if the "success" property exists and is true
            if (!appElement.TryGetProperty("success", out JsonElement successElement) ||
                !successElement.GetBoolean())
            {
                return null;
            }

            // Check for the "data" property and then "header_image"
            if (!appElement.TryGetProperty("data", out JsonElement dataElement) ||
                !dataElement.TryGetProperty("header_image", out JsonElement headerImageElement))
            {
                return null;
            }

            string? imageUrl = headerImageElement.GetString();
            if (string.IsNullOrEmpty(imageUrl))
            {
                return null;
            }

            // Create the BitmapImage from the URL
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }
}
