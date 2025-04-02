using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard
{
    public class GameDetailsFetcher
    {
        public string APIKey { get; set; }
        public string UserID { get; set; }
        private static readonly HttpClient client = new HttpClient();
        private const int MaxRetries = 5;
        private const int DelayMilliseconds = 1000;

        public GameDetailsFetcher(string apiKey, string userID)
        {
            APIKey = apiKey;
            UserID = userID;
        }

        public async Task AddDetailsAsync(Game game)
        {
            string url = $"https://store.steampowered.com/api/appdetails?appids={game.AppID}&key={APIKey}";
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

        private async Task<HttpResponseMessage> SendHttpRequestWithRetryAsync(string url)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    await Task.Delay(DelayMilliseconds * (i + 1)); // Exponential backoff
                }
                else
                {
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

            if (response != null)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(responseBody);

                if (json["response"]["games"].Any(g => g["appid"].Value<string>() == game.AppID))
                {
                    game.IsRecentlyPlayed = true;
                }
            }
        }
    }
}
