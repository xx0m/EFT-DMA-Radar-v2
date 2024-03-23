using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.VisualBasic.Devices;
using Offsets;

namespace eft_dma_radar.Source.Misc
{

    public class PlayerProfileAPI
    {
        public async static Task<PlayerProfile> TryLoadProfileData(string name)
        {
            try
            {
                RequestProfile profile = await SearchProfile(name);

                if (profile == null)
                    return null;

                PlayerProfile playerProfile = await GetProfileData(profile.aid);

                return playerProfile;
            }
            catch
            {
                return null;
            }
        }
        
        public async static Task<RequestProfile> SearchProfile(string name)
        {
            var client = new HttpClient();

            var response = await client.GetAsync($"https://player.tarkov.dev/name/{name}");
            var json = await response.Content.ReadAsStringAsync();

            var profiles = JsonSerializer.Deserialize<List<RequestProfile>>(json);

            return profiles.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
        }

        public async static Task<PlayerProfile> GetProfileData(string aid)
        {
            var client = new HttpClient();

            var response = await client.GetAsync($"https://player.tarkov.dev/account/{aid}");
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<PlayerProfile>(json);
        }

        public class RequestProfile
        {
            [JsonPropertyName("aid")]
            public string aid { get; set; }

            [JsonPropertyName("name")]
            public string name { get; set; }
        }

        public class PlayerProfile
        {
            [JsonPropertyName("id")]
            public string id { get; set; }

            [JsonPropertyName("aid")]
            public int aid { get; set; }

            [JsonPropertyName("info")]
            public Info info { get; set; }

            [JsonPropertyName("achievements")]
            public Dictionary<string, long> achievements { get; set; }

            [JsonPropertyName("favoriteItems")]
            public List<object> favoriteItems { get; set; }

            [JsonPropertyName("pmcStats")]
            public PmcStats pmcStats { get; set; }

            [JsonPropertyName("scavStats")]
            public ScavStats scavStats { get; set; }

            public PlayerProfile()
            {
                id = "0";
                aid = 0;
                info = new Info();
                achievements = new Dictionary<string, long>();
                favoriteItems = new List<object>();
                pmcStats = new PmcStats();
                scavStats = new ScavStats();
            }
        }

        public class Info
        {
            public string nickname { get; set; }
            public string side { get; set; }
            public int experience { get; set; }
            public int memberCategory { get; set; }
            public bool bannedState { get; set; }
            public int bannedUntil { get; set; }
            public int registrationDate { get; set; }
        }

        public class Eft
        {
            public int totalInGameTime { get; set; }
            public OverAllCounters overAllCounters { get; set; }
        }

        public class OverAllCounters
        {
            public List<OverAllCounterItem> Items { get; set; }
        }

        public class OverAllCounterItem
        {
            public List<string> Key { get; set; }
            public int Value { get; set; }
        }

        public class PmcStats
        {
            public Eft eft { get; set; }
        }

        public class ScavStats
        {
            public Eft eft { get; set; }
        }
    }
}