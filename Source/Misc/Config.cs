using System.Text.Json.Serialization;
using System.Text.Json;

namespace eft_dma_radar
{
    public class Config
    {
        #region Json Properties
        [JsonPropertyName("aimviewEnabled")]
        public bool AimviewEnabled { get; set; }

        [JsonPropertyName("aimviewFOV")]
        public float AimViewFOV { get; set; }

        [JsonPropertyName("autoLootRefresh")]
        public bool AutoLootRefresh { get; set; }

        [JsonPropertyName("autoRefreshSettings")]
        public Dictionary<string, int> AutoRefreshSettings { get; set; }

        [JsonPropertyName("chamsEnabled")]
        public bool ChamsEnabled { get; set; }

        [JsonPropertyName("defaultZoom")]
        public int DefaultZoom { get; set; }

        [JsonPropertyName("enemyCount")]
        public bool EnemyCount { get; set; }

        [JsonPropertyName("extendedReach")]
        public bool ExtendedReach { get; set; }

        [JsonPropertyName("font")]
        public int Font { get; set; }

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        [JsonPropertyName("freezeTimeOfDay")]
        public bool FreezeTimeOfDay { get; set; }

        [JsonPropertyName("importantLootOnly")]
        public bool ImportantLootOnly { get; set; }

        [JsonPropertyName("infiniteStamina")]
        public bool InfiniteStamina { get; set; }

        [JsonPropertyName("instantADS")]
        public bool InstantADS { get; set; }

        [JsonPropertyName("jumpPowerStrength")]
        public int JumpPowerStrength { get; set; }

        [JsonPropertyName("loggingEnabled")]
        public bool LoggingEnabled { get; set; }

        [JsonPropertyName("magDrillSpeed")]
        public int MagDrillSpeed { get; set; }

        [JsonPropertyName("mainThermalSetting")]
        public ThermalSettings MainThermalSetting { get; set; }

        [JsonPropertyName("masterSwitch")]
        public bool MasterSwitch { get; set; }

        [JsonPropertyName("maxDistance")]
        public float MaxDistance { get; set; }

        [JsonPropertyName("maxSkills")]
        public Dictionary<string, bool> MaxSkills { get; set; }

        [JsonPropertyName("minCorpseValue")]
        public int MinCorpseValue { get; set; }

        [JsonPropertyName("minImportantLootValue")]
        public int MinImportantLootValue { get; set; }

        [JsonPropertyName("minLootValue")]
        public int MinLootValue { get; set; }

        [JsonPropertyName("minSubItemValue")]
        public int MinSubItemValue { get; set; }

        [JsonPropertyName("nightVision")]
        public bool NightVision { get; set; }

        [JsonPropertyName("noRecoilSway")]
        public bool NoRecoilSway { get; set; }

        [JsonPropertyName("noVisor")]
        public bool NoVisor { get; set; }

        [JsonPropertyName("opticThermalSetting")]
        public ThermalSettings OpticThermalSetting { get; set; }

        [JsonPropertyName("opticThermalVision")]
        public bool OpticThermalVision { get; set; }

        [JsonPropertyName("paintColors")]
        public Dictionary<string, PaintColor.Colors> PaintColors { get; set; }

        [JsonPropertyName("playerAimLine")]
        public int PlayerAimLineLength { get; set; }

        [JsonPropertyName("primaryTeammateAcctId")]
        public string PrimaryTeammateId { get; set; }

        [JsonPropertyName("processLoot")]
        public bool ProcessLoot { get; set; }

        [JsonPropertyName("questHelper")]
        public bool QuestHelper { get; set; }

        [JsonPropertyName("showCorpses")]
        public bool ShowCorpses { get; set; }

        [JsonPropertyName("showExfilNames")]
        public bool ShowExfilNames { get; set; }

        [JsonPropertyName("showHoverArmor")]
        public bool ShowHoverArmor { get; set; }

        [JsonPropertyName("showLoot")]
        public bool ShowLoot { get; set; }

        [JsonPropertyName("showLootValue")]
        public bool ShowLootValue { get; set; }

        [JsonPropertyName("showNames")]
        public bool ShowNames { get; set; }

        [JsonPropertyName("showRadarStats")]
        public bool ShowRadarStats { get; set; }

        [JsonPropertyName("showSubItems")]
        public bool ShowSubItems { get; set; }

        [JsonPropertyName("thermalVision")]
        public bool ThermalVision { get; set; }

        [JsonPropertyName("throwPowerStrength")]
        public int ThrowPowerStrength { get; set; }

        [JsonPropertyName("timeOfDay")]
        public float TimeOfDay { get; set; }

        [JsonPropertyName("uiScale")]
        public int UIScale { get; set; }

        [JsonPropertyName("vsync")]
        public bool VSync { get; set; }
        #endregion

        #region Json Ignore
        [JsonIgnore]
        public Dictionary<string, PaintColor.Colors> DefaultPaintColors = new Dictionary<string, PaintColor.Colors>()
        {
            // AI
            ["Boss"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 255 },
            ["BossGuard"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["BossFollower"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["Raider"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["Rogue"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["Cultist"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["FollowerOfMorana"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
            ["Scav"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 0 },
            ["Other"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },

            // Players
            ["PlayerScav"] = new PaintColor.Colors { A = 255, R = 255, G = 165, B = 0 },
            ["USEC"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 0 },
            ["BEAR"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 255 },
            ["LocalPlayer"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
            ["Teammate"] = new PaintColor.Colors { A = 255, R = 50, G = 205, B = 50 },
            ["TeamHover"] = new PaintColor.Colors { A = 255, R = 125, G = 252, B = 50 },
            ["Special"] = new PaintColor.Colors { A = 255, R = 255, G = 105, B = 180 },

            // Exfils
            ["ExfilActiveText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
            ["ExfilActiveIcon"] = new PaintColor.Colors { A = 255, R = 50, G = 205, B = 50 },
            ["ExfilPendingText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
            ["ExfilPendingIcon"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 0 },
            ["ExfilClosedText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
            ["ExfilClosedIcon"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 0 },

            // Loot/Quests
            ["RegularLoot"] = new PaintColor.Colors { A = 255, R = 245, G = 245, B = 245 },
            ["ImportantLoot"] = new PaintColor.Colors { A = 255, R = 64, G = 224, B = 208 },
            ["QuestItem"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 128 },
            ["QuestZone"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 128 },

            // Other
            ["TextOutline"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 0 },
            ["DeathMarker"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 0 },
            ["Chams"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 0 },
            ["Primary"] = new PaintColor.Colors { A = 255, R = 80, G = 80, B = 80 },
            ["PrimaryDark"] = new PaintColor.Colors { A = 255, R = 50, G = 50, B = 50 },
            ["PrimaryLight"] = new PaintColor.Colors { A = 255, R = 130, G = 130, B = 130 },
            ["Accent"] = new PaintColor.Colors { A = 255, R = 255, G = 128, B = 0 }
        };

        [JsonIgnore]
        public List<LootFilterManager.Filter> Filters
        {
            get => LootFilterManager.Filters;
        }

        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        [JsonIgnore]
        private static readonly object _lock = new();

        [JsonIgnore]
        public LootFilterManager LootFilterManager
        {
            get => Program.LootFilterManager;
        }

        [JsonIgnore]
        public ParallelOptions ParallelOptions
        {
            get; set;
        }

        [JsonIgnore]
        public List<Watchlist.Profile> Profiles
        {
            get => Watchlist.Profiles;
        }

        [JsonIgnore]
        private const string SettingsDirectory = "Configuration\\";

        [JsonIgnore]
        public Watchlist Watchlist
        {
            get => Program.Watchlist;
        }
        #endregion

        public Config()
        {
            AimviewEnabled = false;
            AimViewFOV = 30;
            AutoLootRefresh = false;
            AutoRefreshSettings = new Dictionary<string, int>
            {
                ["Customs"] = 30,
                ["Factory"] = 30,
                ["Ground Zero"] = 30,
                ["Interchange"] = 30,
                ["Lighthouse"] = 30,
                ["Reserve"] = 30,
                ["Shoreline"] = 30,
                ["Streets of Tarkov"] = 30,
                ["The Lab"] = 30,
                ["Woods"] = 30
            };
            ChamsEnabled = false;
            DefaultZoom = 100;
            EnemyCount = false;
            ExtendedReach = false;
            Font = 0;
            FontSize = 13;
            FreezeTimeOfDay = false;
            ImportantLootOnly = false;
            InfiniteStamina = false;
            InstantADS = false;
            JumpPowerStrength = 0;
            LoggingEnabled = false;
            MagDrillSpeed = 1;
            MainThermalSetting = new ThermalSettings(1f, 0.0011f, -0.1f, 0);
            MasterSwitch = false;
            MaxDistance = 325;
            MaxSkills = new Dictionary<string, bool>
            {
                ["Endurance"] = false,
                ["Strength"] = false,
                ["Vitality"] = false,
                ["Health"] = false,
                ["Stress Resistance"] = false,
                ["Metabolism"] = false,
                ["Immunity"] = false,
                ["Perception"] = false,
                ["Intellect"] = false,
                ["Attention"] = false,
                ["Covert Movement"] = false,
                ["Throwables"] = false,
                ["Surgery"] = false,
                ["Search"] = false,
                ["Mag Drills"] = false,
                ["Light Vests"] = false,
                ["Heavy Vests"] = false,
            };
            MinCorpseValue = 100000;
            MinImportantLootValue = 300000;
            MinLootValue = 90000;
            MinSubItemValue = 15000;
            NightVision = false;
            NoRecoilSway = false;
            NoVisor = false;
            OpticThermalSetting = new ThermalSettings(1f, 0.0011f, -0.1f, 0);
            OpticThermalVision = false;
            PaintColors = DefaultPaintColors;
            ParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 2 };
            PlayerAimLineLength = 1000;
            PrimaryTeammateId = null;
            ProcessLoot = true;
            QuestHelper = true;
            ShowCorpses = false;
            ShowExfilNames = false;
            ShowHoverArmor = false;
            ShowLoot = true;
            ShowLootValue = false;
            ShowNames = false;
            ShowRadarStats = false;
            ShowSubItems = false;
            ThermalVision = false;
            ThrowPowerStrength = 1;
            TimeOfDay = 12f;
            UIScale = 100;
            VSync = true;
        }

        /// <summary>
        /// Attempt to load Config.json
        /// </summary>
        /// <param name="config">'Config' instance to populate.</param>
        /// <returns></returns>
        public static bool TryLoadConfig(out Config config)
        {
            lock (_lock)
            {
                if (!Directory.Exists(SettingsDirectory))
                    Directory.CreateDirectory(SettingsDirectory);

                try
                {
                    if (!File.Exists($"{SettingsDirectory}Settings.json"))
                        throw new FileNotFoundException("Settings.json does not exist!");

                    var json = File.ReadAllText($"{SettingsDirectory}Settings.json");

                    config = JsonSerializer.Deserialize<Config>(json);
                    return true;
                }
                catch (Exception ex)
                {
                    Program.Log($"TryLoadConfig - {ex.Message}\n{ex.StackTrace}");
                    config = null;
                    return false;
                }
            }
        }
        /// <summary>
        /// Save to Config.json
        /// </summary>
        /// <param name="config">'Config' instance</param>
        public static void SaveConfig(Config config)
        {
            lock (_lock)
            {
                if (!Directory.Exists(SettingsDirectory))
                    Directory.CreateDirectory(SettingsDirectory);

                var json = JsonSerializer.Serialize<Config>(config, _jsonOptions);
                File.WriteAllText($"{SettingsDirectory}Settings.json", json);
            }
        }
    }
}
