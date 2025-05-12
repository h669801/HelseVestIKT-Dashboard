using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HelseVestIKT_Dashboard
{
    public class SteamProfile
    {
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Wrapper for all profile data, including which profile was used last.
    /// </summary>
    public class ProfilesFile
    {
        /// <summary>Last used profile name.</summary>
        public string LastProfileName { get; set; } = string.Empty;

        /// <summary>List of all Steam profiles.</summary>
        public List<SteamProfile> Profiles { get; set; } = new List<SteamProfile>();
    }

    public static class ProfileStore
    {
        private static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HelseVestIKT_Dashboard",
                "profiles.json"
            );

        /// <summary>
        /// Load profiles and last used profile from JSON, handling both new and old formats.
        /// </summary>
        public static ProfilesFile Load()
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(FilePath))
            {
                // No file yet: return default
                return new ProfilesFile
                {
                    LastProfileName = string.Empty,
                    Profiles = new List<SteamProfile>
                    {
                        new SteamProfile { Name = "Default", ApiKey = string.Empty, UserId = string.Empty }
                    }
                };
            }

            var json = File.ReadAllText(FilePath);
            ProfilesFile data = null;

            // Try new object format
            try
            {
                data = JsonConvert.DeserializeObject<ProfilesFile>(json);
            }
            catch (JsonSerializationException)
            {
                // JSON is likely an array of SteamProfile
            }

            if (data == null)
            {
                // Fallback: old array format
                List<SteamProfile> oldProfiles;
                try
                {
                    oldProfiles = JsonConvert.DeserializeObject<List<SteamProfile>>(json)
                                  ?? new List<SteamProfile>();
                }
                catch
                {
                    oldProfiles = new List<SteamProfile>();
                }

                if (oldProfiles.Count == 0)
                {
                    oldProfiles.Add(new SteamProfile { Name = "Default", ApiKey = string.Empty, UserId = string.Empty });
                }

                data = new ProfilesFile
                {
                    Profiles = oldProfiles,
                    LastProfileName = oldProfiles[0].Name
                };

                // Persist upgraded format
                Save(data);
                return data;
            }

            // Ensure at least one profile
            if (data.Profiles == null || data.Profiles.Count == 0)
            {
                data.Profiles = new List<SteamProfile>
                {
                    new SteamProfile { Name = "Default", ApiKey = string.Empty, UserId = string.Empty }
                };
            }
            return data;
        }

        /// <summary>
        /// Save all profiles and last used profile to JSON.
        /// </summary>
        public static void Save(ProfilesFile data)
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
}
