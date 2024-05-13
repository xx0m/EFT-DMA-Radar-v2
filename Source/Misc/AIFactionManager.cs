using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using static eft_dma_radar.Watchlist;
using static eft_dma_radar.AIFactionManager;

namespace eft_dma_radar
{
    public class AIFactionManager
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        [JsonIgnore]
        private const string AIFactionDirectory = "Configuration\\";

        [JsonIgnore]
        private static readonly object _lock = new();

        /// <summary>
        /// Allows storage of AI names + PlayerTypes.
        /// </summary>
        [JsonIgnore]
        public List<Faction> Factions { get; set; }

        public AIFactionManager()
        {
            Factions = new List<Faction>();
        }

        public static bool TryLoadAIFactions(out AIFactionManager aiFactionManager)
        {
            lock (_lock)
            {
                try
                {
                    aiFactionManager = new AIFactionManager();

                    if (!Directory.Exists(AIFactionDirectory))
                        Directory.CreateDirectory(AIFactionDirectory);

                    var json = File.ReadAllText($"{AIFactionDirectory}AIFactions.json");

                    var factions = JsonSerializer.Deserialize<List<Faction>>(json);
                    aiFactionManager.Factions.AddRange(factions ?? new List<Faction>());


                    return true;
                }
                catch (Exception ex)
                {
                    aiFactionManager = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// Save to AIFactions.json
        /// </summary>
        /// <param name="aiManager">'AIManager' instance</param>
        public static void SaveFactions(AIFactionManager aiManager)
        {
            lock (_lock)
            {
                if (!Directory.Exists(AIFactionDirectory))
                    Directory.CreateDirectory(AIFactionDirectory);

                var json = JsonSerializer.Serialize<List<Faction>>(aiManager.Factions, _jsonOptions);
                File.WriteAllText($"{AIFactionDirectory}AIFactions.json", json);
            }
        }

        public void AddEmptyFaction()
        {
            this.Factions.Add(new Faction()
            {
                Name = "Default",
                Names = new List<string>(),
                PlayerType = PlayerType.Scav
            });

            AIFactionManager.SaveFactions(this);
        }

        public void AddEmptyEntry(Faction faction)
        {
            faction.Names.Add("New Entry");

            AIFactionManager.SaveFactions(this);
        }

        public void AddEntry(Faction faction, string name)
        {
            faction.Names.Add(name);

            AIFactionManager.SaveFactions(this);
        }

        public void UpdateFaction(Faction faction, int index)
        {
            this.Factions.RemoveAt(index);
            this.Factions.Insert(index, faction);

            AIFactionManager.SaveFactions(this);
        }

        public void RemoveFaction(Faction faction)
        {
            this.Factions.Remove(faction);
            AIFactionManager.SaveFactions(this);
        }

        public void RemoveFaction(int index)
        {
            this.Factions.RemoveAt(index);
            AIFactionManager.SaveFactions(this);
        }

        public void UpdateEntry(Faction faction, string name, int index)
        {
            faction.Names.RemoveAt(index);
            faction.Names.Insert(index, name);

            AIFactionManager.SaveFactions(this);
        }

        public void RemoveEntry(Faction faction, string name)
        {
            faction.Names.Remove(name);
            AIFactionManager.SaveFactions(this);
        }

        public bool IsInFaction(string name, out PlayerType playerType)
        {
            foreach (var faction in this.Factions)
            {
                if (faction.Names.Contains(name))
                {
                    playerType = faction.PlayerType;
                    return true;
                }
            }

            playerType = PlayerType.Scav;
            return false;
        }

        public class Faction
        {
            public string Name { get; set; }
            public List<string> Names { get; set; }
            public PlayerType PlayerType { get; set; }
        }
    }
}
