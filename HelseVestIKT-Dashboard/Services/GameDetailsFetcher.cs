using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HelseVestIKT_Dashboard.Models;
using System.Net;

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
				JToken? appNode = json[game.AppID];
				if (appNode?["success"]?.Value<bool>() == true)
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
			client.DefaultRequestHeaders.Add("User-Agent", "HelseVestIKT-Dashboard/1.0");
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.DefaultRequestHeaders.Add("Referer", "https://store.steampowered.com");

			/* // Sett cache-filsti til AppData\Local\HelseVestIKT_Dashboard\cache.json
			 string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			 string appFolder = Path.Combine(appDataFolder, "HelseVestIKT_Dashboard");
			 Directory.CreateDirectory(appFolder); // Sørg for at mappen eksisterer
			 cacheFilePath = Path.Combine(appFolder, "cache.json");

			 if (!File.Exists(cacheFilePath))
			 {
				 File.WriteAllText(cacheFilePath, "[]");
			 }
			*/

			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			var assetsFolder = Path.Combine(baseDir, "Assets");
			Directory.CreateDirectory(assetsFolder);
			cacheFilePath = Path.Combine(assetsFolder, "cache.json");

			if (!File.Exists(cacheFilePath))
				File.WriteAllText(cacheFilePath, "[]");
		}

		private async Task<HttpResponseMessage> SendHttpRequestWithRetryAsync(string url)
		{
			if (cache.TryGetValue(url, out var cachedResponse))
				return cachedResponse;
			
			for (int i = 0; i < MaxRetries; i++)
			{
				HttpResponseMessage response = await client.GetAsync(url);
				if (response.IsSuccessStatusCode)
				{
					cache[url] = response;
					SaveCache();
					return response;
				}		
				else if (response.StatusCode == (HttpStatusCode)429)
				{
					await Task.Delay(DelayMilliseconds * (i + 1)); // Exponential backoff
					continue;
				}
				// for any other non-success code, read the body for diagnostics then throw
				var body = await response.Content.ReadAsStringAsync();
				Console.WriteLine($"Request to {url} failed ({(int)response.StatusCode}): {body}");
				response.EnsureSuccessStatusCode();  // will throw
			}

			// if we get here, all retries failed
			throw new HttpRequestException($"Failed to GET '{url}' after {MaxRetries} retries.");
		}

		/*private void AddGenres(Game game, JObject json)
		{
			var genres = json[game.AppID]["data"]["genres"];
			if (genres != null)
			{
				var genreList = genres.Select(g => g["description"].Value<string>()).ToList();
				game.Genres = genreList;
			}
		}*/

		private void AddGenres(Game game, JObject json)
		{
			// Hent “data.genres” trygt som JArray – eller null hvis ikke finnes
			var genresToken = json[game.AppID]?["data"]?["genres"] as JArray;

			if (genresToken == null)
			{
				// Ingen genres tilgjengelig
				game.Genres = new List<string>();
			}
			else
			{
				game.Genres = genresToken
					.Select(g => g["description"]?.Value<string>() ?? "")
					.Where(desc => !string.IsNullOrWhiteSpace(desc))
					.ToList();
			}
		}


		private void AddSteamGame(Game game)
		{
			game.IsSteamGame = true;
		}

		/*private void AddVR(Game game, JObject json)
		{
			var categories = json[game.AppID]["data"]["categories"];
			bool isVR = categories.Any(c => c["id"].Value<int>() == 53 || c["id"].Value<int>() == 54);
			game.IsVR = isVR;
		} */

		private void AddVR(Game game, JObject json)
		{
			var vrToken = json[game.AppID]?["data"]?["categories"] as JArray;
			if (vrToken == null)
			{
				game.IsVR = false;
			}
			else
			{
				game.IsVR = vrToken.Any(c => c["id"]?.Value<int>() == 53
										  || c["id"]?.Value<int>() == 54);
			}
		}



		/*private void AddSinglePlayer(Game game, JObject json)
		{
			var categories = json[game.AppID]["data"]["categories"];
			bool isSinglePlayer = categories.Any(c => c["id"].Value<int>() == 2) && !categories.Any(c => c["id"].Value<int>() == 1 || c["id"].Value<int>() == 9);
			game.IsSinglePlayer = isSinglePlayer;
		}*/

		private void AddSinglePlayer(Game game, JObject json)
		{
			// 1) Hent ut “data.categories” som JArray – eller null om det ikke finnes
			var categoriesToken = json[game.AppID]?["data"]?["categories"] as JArray;

			// 2) Hvis det ikke finnes, sett IsSinglePlayer = false og returner
			if (categoriesToken == null)
			{
				game.IsSinglePlayer = false;
				return;
			}

			// 3) Ellers bruk Any trygt på det faktiske arrayet
			bool hasSP = categoriesToken.Any(c => c["id"]?.Value<int>() == 2);
			bool hasMP = categoriesToken.Any(c => c["id"]?.Value<int>() == 1);
			bool hasCoop = categoriesToken.Any(c => c["id"]?.Value<int>() == 9);

			game.IsSinglePlayer = hasSP && !(hasMP || hasCoop);
		}



		/*private async Task CheckRecentlyPlayedAsync(Game game)
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
		*/

		private async Task CheckRecentlyPlayedAsync(Game game)
		{
			string url =
			  $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v0001/?key={APIKey}&steamid={UserID}&format=json";

			// assume SendHttpRequestWithRetryAsync never returns null
			HttpResponseMessage response = await SendHttpRequestWithRetryAsync(url);
			string responseBody = await response.Content.ReadAsStringAsync();
			JObject json = JObject.Parse(responseBody);

			// Grab it as a JArray (or null if it doesn’t exist)
			var playedArray = json["response"]?["games"] as JArray;

			// If playedArray is null, the whole expression becomes false
			game.IsRecentlyPlayed =
			  playedArray
				?.Any(g => (string?)g["appid"] == game.AppID)  // returns bool?
			  ?? false;                                         // coalesce null into false
		}




		private void LoadCache()
		{
			if (!File.Exists(cacheFilePath)) return;

			// Read & parse once
			var cacheContent = File.ReadAllText(cacheFilePath);
			JObject? cachedResponses;
			try
			{
				cachedResponses = JObject.Parse(cacheContent);
			}
			catch (JsonException)
			{
				// invalid JSON → bail out
				return;
			}

			foreach (var prop in cachedResponses.Properties())
			{
				// safe: if prop.Value is null, we use empty string
				string body = prop.Value?.ToString() ?? "";

				var response = new HttpResponseMessage
				{
					Content = new StringContent(body)
				};

				cache[prop.Name] = response;
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