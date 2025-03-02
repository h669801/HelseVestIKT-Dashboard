using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

        public async Task GetSteamGamesAsync()
        {
            string apiUrl = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={steamApiKey}&steamid={steamUserId}&include_appinfo=true";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = await response.Content.ReadAsStringAsync();
                    JObject data = JObject.Parse(jsonResult);

                    foreach (var game in data["response"]["games"])
                    {
                        string gameName = game["name"].ToString();
                        int appId = (int)game["appid"];
                        Console.WriteLine($"Spill: {gameName} - AppID: {appId}");
                    }
                }
                else
                {
                    Console.WriteLine("Kunne ikke hente spilldata.");
                }
            }
        }
    }
}
