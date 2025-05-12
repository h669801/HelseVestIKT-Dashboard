using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
{
	public class GameDetailsFetcher
	{
		public string APIKey { get; set; }
		public string UserID { get; set; }
		private static readonly HttpClient client = new HttpClient();
		private const int MaxRetries = 5;
		private const int DelayMilliseconds = 1000;
		private static readonly ConcurrentDictionary<string, HttpResponseMessage> cache = new ConcurrentDictionary<string, HttpResponseMessage>();
        private static readonly string cacheFilePath;
        //private static readonly string cacheFilePath = "cache.json";

        public GameDetailsFetcher(string apiKey, string userID)
		{
			APIKey = apiKey;
			UserID = userID;
			LoadCache();
		}

		public async Task AddDetailsAsync(Game game)
		{
			string url = $"https://store.steampowered.com/api/appdetails?appids={game.AppID}";
			HttpResponseMessage response = await SendHttpRequestWithRetryAsync(url);

			if (response != null)
			{
				string responseBody = await response.Content.ReadAsStringAsync();
				JObject json = JObject.Parse(responseBody);
				if (json[game.AppID]["success"].Value<bool>())
				{
					AddGenres(game, json);
					AddSinglePlayer(game, json);
					AddSteamGame(game);
					AddVR(game, json);
				}
			}

			await CheckRecentlyPlayedAsync(game); // Checks if the game is recently played
		}

		static GameDetailsFetcher()
		{
			client.DefaultRequestHeaders.Add("User-Agent", "YourAppName/1.0");
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.DefaultRequestHeaders.Add("Referer", "https://store.steampowered.com");

            // Sett cache-filsti til AppData\Local\HelseVestIKT_Dashboard\cache.json
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataFolder, "HelseVestIKT_Dashboard");
            Directory.CreateDirectory(appFolder); // Sørg for at mappen eksisterer
            cacheFilePath = Path.Combine(appFolder, "cache.json");
        }

		private async Task<HttpResponseMessage> SendHttpRequestWithRetryAsync(string url)
		{
			if (cache.TryGetValue(url, out HttpResponseMessage cachedResponse))
			{
				return cachedResponse;
			}

			for (int i = 0; i < MaxRetries; i++)
			{
				HttpResponseMessage response = await client.GetAsync(url);
				if (response.IsSuccessStatusCode)
				{
					cache[url] = response;
					SaveCache();
					return response;
				}
				else if (response.StatusCode == (System.Net.HttpStatusCode)429)
				{
					await Task.Delay(DelayMilliseconds * (i + 1)); // Exponential backoff
				}
				else
				{
					string responseContent = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"Request to {url} failed with status code {response.StatusCode}. Response content: {responseContent}");

					if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
					{
						Console.WriteLine("403 Forbidden: Check if the API key is valid and has the necessary permissions.");
						await Task.Delay(DelayMilliseconds * (i + 1));
					}

					response.EnsureSuccessStatusCode();
				}
			}
			return null;
		}

		private void AddGenres(Game game, JObject json)
		{
			var genres = json[game.AppID]["data"]["genres"];
			if (genres != null)
			{
				var genreList = genres.Select(g => g["description"].Value<string>()).ToList();
				game.Genres = genreList;
			}
		}

		private void AddSteamGame(Game game)
		{
			game.IsSteamGame = true;
		}

		private void AddVR(Game game, JObject json)
		{
			var categories = json[game.AppID]["data"]["categories"];
			bool isVR = categories.Any(c => c["id"].Value<int>() == 53 || c["id"].Value<int>() == 54);
			game.IsVR = isVR;
		}

		private void AddSinglePlayer(Game game, JObject json)
		{
			var categories = json[game.AppID]["data"]["categories"];
			bool isSinglePlayer = categories.Any(c => c["id"].Value<int>() == 2) && !categories.Any(c => c["id"].Value<int>() == 1 || c["id"].Value<int>() == 9);
			game.IsSinglePlayer = isSinglePlayer;
		}

		private async Task CheckRecentlyPlayedAsync(Game game)
		{
			string url = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key={APIKey}&steamid={UserID}&format=json";
			HttpResponseMessage response = await SendHttpRequestWithRetryAsync(url);

			if (response == null) return;

			string responseBody = await response.Content.ReadAsStringAsync();
			JObject json = JObject.Parse(responseBody);

			// Safely grab the "games" token as an array
			var gamesToken = json["response"]?["games"] as JArray;
			if (gamesToken != null &&
				gamesToken.Any(g => (string)g["appid"] == game.AppID))
			{
				game.IsRecentlyPlayed = true;
			}
		}

		private void LoadCache()
		{
			if (File.Exists(cacheFilePath))
			{
				var cacheContent = File.ReadAllText(cacheFilePath);
				var cachedResponses = JObject.Parse(cacheContent);
				foreach (var item in cachedResponses)
				{
					var response = new HttpResponseMessage
					{
						Content = new StringContent(item.Value.ToString())
					};
					cache[item.Key] = response;
				}
			}
		}

		private void SaveCache()
		{
			var cacheContent = new JObject();
			foreach (var item in cache)
			{
				cacheContent[item.Key] = item.Value.Content.ReadAsStringAsync().Result;
			}
			File.WriteAllText(cacheFilePath, cacheContent.ToString());
		}
	}
}