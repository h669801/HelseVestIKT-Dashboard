using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace HelseVestIKT_Dashboard
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
			List<Game> games = new List<Game>();
			// Include app info to get image URLs.
			string apiUrl = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={steamApiKey}&steamid={steamUserId}&include_appinfo=true";

			using (HttpClient client = new HttpClient())
			{
				HttpResponseMessage response = await client.GetAsync(apiUrl);
				if (response.IsSuccessStatusCode)
				{
					string jsonResult = await response.Content.ReadAsStringAsync();
					Console.WriteLine(jsonResult);
					JObject data = JObject.Parse(jsonResult);

					foreach (var game in data["response"]["games"])
					{
						string title = game["name"].ToString();
						string appID = game["appid"].ToString();

						// Try to load the local image based on the appID.
						BitmapImage? gameImage = GameImage.LoadLocalGameImage(appID);

						// If the local image isn't available, fall back to using the online capsule image.
						if (gameImage == null)
						{
							string? capsuleImageUrl = game["capsule_image"]?.ToString();
							if (!string.IsNullOrEmpty(capsuleImageUrl))
							{
								gameImage = await GameImage.LoadOnlineGameImageAsync(capsuleImageUrl);
							}
						}

						games.Add(new Game
						{
							AppID = appID,
							Title = title,
							GameImage = gameImage
						});
					}
				}
				else
				{
					Console.WriteLine("Kunne ikke hente spilldata.");
				}
			}
			return games;
		}
	}
}
