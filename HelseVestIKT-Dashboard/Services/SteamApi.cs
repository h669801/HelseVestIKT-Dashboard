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
			// Fjern slash på slutten, og legg til include_played_free_games
			string apiUrl =
			  $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1" +
			  $"?key={steamApiKey}" +
			  $"&steamid={steamUserId}" +
			  $"&include_appinfo=true" +
			  $"&include_played_free_games=true";

			using var client = new HttpClient();
			HttpResponseMessage response;
			try
			{
				response = await client.GetAsync(apiUrl);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[SteamApi] HTTP-kall feilet: {ex.Message}");
				return games;
			}

			if (!response.IsSuccessStatusCode)
			{
				Console.WriteLine($"[SteamApi] Kunne ikke hente spilldata: {(int)response.StatusCode} {response.ReasonPhrase}");
				return games;
			}

			// 2) Parse JSON and build bare-bones Game objects
			string json = await response.Content.ReadAsStringAsync();
			JObject? root;
			try
			{
				root = JObject.Parse(json);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[SteamApi] JSON-parse feilet: {ex.Message}");
				return games;
			}

			var data = root["response"]?["games"] as JArray;
			if (data == null)
			{
				Console.WriteLine("[SteamApi] Ingen spillfelt i JSON-svaret.");
				return games;
			}

			games = data
			  .Select(g => new Game
			  {
				  AppID = (string)g["appid"],
				  Title = (string)g["name"],
				  GameImage = null
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
