using System.Net;
using System.Text.Json.Serialization;

namespace eft_dma_radar
{
    public class KDManager
    {

        private static List<string> proxies = new List<string>
        {
            "http://89.37.219.212:8080",
        };

        private static Random random = new Random();
        private static bool useProxy = true;
        /// <summary>
        /// Returns Kill/Death ratio.
        /// </summary>
        public static async Task<float> GetKD(string accountID)
        {
            try
            {

                HttpClient client;
                if (useProxy && proxies.Any())
                {
                    var proxyAddress = proxies[random.Next(proxies.Count)];
                    var proxy = new WebProxy(proxyAddress);
                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true,
                    };
                    client = new HttpClient(httpClientHandler);
                }
                else
                {
                    client = new HttpClient();
                }
                useProxy = !useProxy;

                //Console.WriteLine($"Requesting player profile for account ID: {accountID}");
                //var response = await client.GetAsync($"https://player.tarkov.dev/account/{accountID}");

                //if (response.IsSuccessStatusCode)
                //{
                //    var content = await response.Content.ReadAsStringAsync();
                //    var playerProfile = JsonSerializer.Deserialize<PlayerProfile>(content);
                //
                //    if (playerProfile is not null)
                //    {
                //        var killsItem = playerProfile.pmcStats.eft.overAllCounters.Items.FirstOrDefault(x => x.Key.Contains("Kills"));
                //        var deathsItem = playerProfile.pmcStats.eft.overAllCounters.Items.FirstOrDefault(x => x.Key.Contains("Deaths"));
                //        int kills = killsItem is not null ? killsItem.Value : 0;
                //        int deaths = deathsItem is not null ? deathsItem.Value : 0;
                //
                //        if (deaths == 0)
                //        {
                //            return kills;
                //       }
                //
                //        return (float)kills / deaths;
                //    }
                //}else
                //{
                //    //Console.WriteLine($"Failed to get player profile for account ID: {accountID}");
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                // Log the exception or handle it as needed.
            }

            return -1f;
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