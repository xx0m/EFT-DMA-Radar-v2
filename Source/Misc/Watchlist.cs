using System.Text.Json.Serialization;
using System.Text.Json;

namespace eft_dma_radar
{
    public class Watchlist
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        [JsonIgnore]
        private const string WatchlistDirectory = "Configuration\\Watchlist\\";

        [JsonIgnore]
        private static readonly object _lock = new();

        /// <summary>
        /// Allows storage of multiple watchlist profiles.
        /// </summary>
        [JsonIgnore]
        public List<Profile> Profiles { get; set; }

        public Watchlist()
        {
            Profiles = new List<Profile>();
        }

        /// <summary>
        /// Attempt to load Watchlist.json
        /// </summary>
        public static bool TryLoadWatchlist(out Watchlist watchlistManager)
        {
            lock (_lock)
            {
                try
                {
                    watchlistManager = new Watchlist();

                    if (!Directory.Exists(WatchlistDirectory))
                        Directory.CreateDirectory(WatchlistDirectory);

                    var jsonFiles = Directory.GetFiles(WatchlistDirectory, "*.json");

                    foreach (var file in jsonFiles)
                    {
                        var json = File.ReadAllText(file);
                        var profile = JsonSerializer.Deserialize<Watchlist.Profile>(json);
                        watchlistManager.Profiles.Add(profile);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    watchlistManager = null;
                    return false;
                }
            }
        }
        /// <summary>
        /// Save to Watchlist.json
        /// </summary>
        public static void SaveWatchlist(Watchlist watchlistManager)
        {
            lock (_lock)
            {
                if (!Directory.Exists(WatchlistDirectory))
                    Directory.CreateDirectory(WatchlistDirectory);

                var existingFiles = Directory.GetFiles(WatchlistDirectory, "*.json");

                foreach (var profile in watchlistManager.Profiles)
                {
                    var newFileName = $"{WatchlistDirectory}{profile.Name}.json";

                    var existingFile = existingFiles.FirstOrDefault(file => file.Equals(newFileName, StringComparison.OrdinalIgnoreCase));

                    if (existingFile == null)
                    {
                        var oldFile = existingFiles.FirstOrDefault(file => file.Contains(profile.Name));
                        if (oldFile != null)
                        {
                            File.Delete(oldFile);
                        }
                    }

                    var json = JsonSerializer.Serialize<Watchlist.Profile>(profile, _jsonOptions);
                    File.WriteAllText(newFileName, json);
                }

                var filesToDelete = existingFiles.Except(watchlistManager.Profiles.Select(filter => $"{WatchlistDirectory}{filter.Name}.json"));
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }
            }
        }

        public void AddEmptyProfile()
        {
            this.Profiles.Add(new Watchlist.Profile()
            {
                Name = "Default",
                Entries = new List<Watchlist.Entry>()
            });

            Watchlist.SaveWatchlist(this);
        }

        public void AddEmptyEntry(Profile profile)
        {
            profile.Entries.Add(new Watchlist.Entry
            {
                AccountID = "New Entry",
                Tag = "n/a",
                IsStreamer = false,
                Platform = 0,
                PlatformUsername = "n/a"
            });

            Watchlist.SaveWatchlist(this);
        }

        public void AddEntry(Profile profile, string accountID, string tag)
        {
            profile.Entries.Add(new Watchlist.Entry
            {
                AccountID = accountID,
                Tag = tag,
                IsStreamer = false,
                Platform = 0,
                PlatformUsername = "n/a"
            });

            Watchlist.SaveWatchlist(this);
        }

        public void UpdateProfile(Profile profile, int index)
        {
            this.Profiles.RemoveAt(index);
            this.Profiles.Insert(index, profile);

            Watchlist.SaveWatchlist(this);
        }

        public void RemoveProfile(Profile profile)
        {
            this.Profiles.Remove(profile);
            Watchlist.SaveWatchlist(this);
        }

        public void RemoveProfile(int index)
        {
            this.Profiles.RemoveAt(index);
            Watchlist.SaveWatchlist(this);
        }

        public void UpdateEntry(Profile profile, Entry entry, int index)
        {
            profile.Entries.RemoveAt(index);
            profile.Entries.Insert(index, entry);

            Watchlist.SaveWatchlist(this);
        }

        public void RemoveEntry(Profile profile, Entry entry)
        {
            profile.Entries.Remove(entry);
            Watchlist.SaveWatchlist(this);
        }

        public bool IsOnWatchlist(string accountID, out Entry entry)
        {
            foreach (var profile in this.Profiles)
            {
                entry = profile.Entries.Find(e => e.AccountID == accountID);
                if (entry != null)
                {
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public static async Task<bool> IsLive(Entry entry)
        {
            switch (entry.Platform)
            {
                case 0:
                    return await TwitchCheck(entry);
                case 1:
                    return await YoutubeCheck(entry);
                default:
                    return false;
            }
        }

        public static async Task<bool> YoutubeCheck(Entry entry)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync($"https://youtube.com/@{entry.PlatformUsername}/live");
                    var sourceCode = await response.Content.ReadAsStringAsync();

                    return sourceCode.Contains("hqdefault_live.jpg");
                }
                catch (Exception ex)
                {
                    Program.Log("Error occurred: " + ex.Message);
                    return false;
                }
            }
        }

        public static async Task<bool> TwitchCheck(Entry entry)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync($"https://twitch.tv/{entry.PlatformUsername}");
                    var sourceCode = await response.Content.ReadAsStringAsync();

                    return sourceCode.Contains("isLiveBroadcast");
                }
                catch (Exception ex)
                {
                    Program.Log("Error occurred: " + ex.Message);
                    return false;
                }
            }
        }

        public class Profile
        {
            public string Name { get; set; }
            public List<Entry> Entries { get; set; }

            public Profile()
            {
                Entries = new List<Entry>();
            }
        }

        public class Entry
        {
            public string AccountID { get; set; }
            public string Tag { get; set; }
            public bool IsStreamer { get; set; }
            public int Platform { get; set; }
            public string PlatformUsername { get; set; }
        }
    }
}
