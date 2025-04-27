using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HelseVestIKT_Dashboard
{
    public class SteamProfile
    {
        public string Name { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string UserId { get; set; } = "";
    }

    public static class ProfileStore
    {
        // Full sti til profildata-filen under %LOCALAPPDATA%
        private static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HelseVestIKT_Dashboard",
                "profiles.json"
            );

        public static List<SteamProfile> Load()
        {
            if (!File.Exists(FilePath))
            {
                // Hvis ingen fil finnes, opprett en default-profilliste
                return new List<SteamProfile>
                {
                    new SteamProfile { Name = "Default", ApiKey = "", UserId = "" }
                };
            }

            var json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<List<SteamProfile>>(json)
                   ?? new List<SteamProfile>();
        }

        public static void Save(List<SteamProfile> profiles)
        {
            // Sørg for at mappen finnes
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
}
