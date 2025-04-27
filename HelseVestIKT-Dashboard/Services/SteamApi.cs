using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
{
	public class SteamApi
	{
		private readonly string steamApiKey;
		private readonly string steamUserId;

		public SteamApi(string steamApiKey, string steamUserId)
		{
			this.steamApiKey = steamApiKey;
			this.steamUserId = steamUserId;
		}

		public async Task<List<Game>> GetSteamGamesAsync()
		{
			var games = new List<Game>();

			// 1) Call the Steam API
			string apiUrl =
			  $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={steamApiKey}&steamid={steamUserId}&include_appinfo=true";
			using var client = new HttpClient();
			var response = await client.GetAsync(apiUrl);
			if (!response.IsSuccessStatusCode)
			{
				Console.WriteLine("Kunne ikke hente spilldata.");
				return games;
			}

			// 2) Parse JSON and build bare‐bones Game objects
			var json = await response.Content.ReadAsStringAsync();
			var data = JObject.Parse(json)["response"]["games"];
			games = data
			  .Select(g => new Game
			  {
				  AppID = (string)g["appid"],
				  Title = (string)g["name"],
				  GameImage = null  // fill in below
			  })
			  .ToList();

			// 3) In parallel, load each image (local first, then Steam’s logo URL)
			var loadTasks = games.Select(async game =>
			{
				// a) Local cache?
				var local = GameImage.LoadLocalGameImage(game.AppID);
				if (local != null)
				{
					game.GameImage = local;
					return;
				}

				// b) Steam’s online logo suffix
				var jsonObj = data.First(x => (string)x["appid"] == game.AppID);
				var suffix = (string?)jsonObj["img_logo_url"];
				if (!string.IsNullOrEmpty(suffix))
				{
					var url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppID}/{suffix}.jpg";
					try
					{
						game.GameImage = await GameImage.LoadOnlineGameImageAsync(url);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Feil ved lasting av bilde for {game.Title}: {ex.Message}");
					}
				}

				// c) Fallback if still null
				if (game.GameImage == null)
				{
					game.GameImage = new BitmapImage(
						new Uri("pack://application:,,,/Assets/Bilder/Helse_Vest_Kuler_Logo.png"));
				}
			});

			await Task.WhenAll(loadTasks);

			return games;
		}
	}
}
