using System.Text.Json.Serialization;
using System.Text.Json;

namespace eft_dma_radar
{
    public class Config
    {
        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };
        [JsonIgnore]
        private static readonly object _lock = new();
        /// <summary>
        /// Enables Vertical Sync in GUI Render.
        /// </summary>
        [JsonPropertyName("vsyncEnabled")]
        public bool Vsync { get; set; }
        /// <summary>
        /// Player/Teammates Aimline Length (Max: 1000)
        /// </summary>
        [JsonPropertyName("playerAimLine")]
        public int PlayerAimLineLength { get; set; }
        /// <summary>
        /// Last used 'Zoom' level.
        /// </summary>
        [JsonPropertyName("defaultZoom")]
        public int DefaultZoom { get; set; }
        /// <summary>
        /// UI Scale Value (50-200 , default: 100)
        /// </summary>
        [JsonPropertyName("uiScale")]
        public int UIScale { get; set; }
        /// <summary>
        /// Enables loot output on map.
        /// </summary>
        [JsonPropertyName("lootEnabled")]
        public bool LootEnabled { get; set; }
        /// <summary>
        /// Enables Quest Helper output on map.
        /// </summary>
        [JsonPropertyName("questHelperEnabled")]
        public bool QuestHelperEnabled { get; set; }
        /// <summary>
        /// Enables Aimview window in Main Window.
        /// </summary>
        [JsonPropertyName("aimviewEnabled")]
        public bool AimviewEnabled { get; set; }
        /// <summary>
        /// Hides player names & extended player info in Radar GUI.
        /// </summary>
        [JsonPropertyName("hideNames")]
        public bool HideNames { get; set; }
        /// <summary>
        /// Enables/disables showing non-important loot
        /// </summary>
        [JsonPropertyName("importantLootOnly")]
        public bool ImportantLootOnly { get; set; }
        /// <summary>
        /// Enables/disables showing loot value
        /// </summary>
        [JsonPropertyName("hideLootValue")]
        public bool HideLootValue { get; set; }
        /// <summary>
        /// Primary Teammate ACCT ID (for secondary Aimview)
        /// </summary>
        [JsonPropertyName("primaryTeammateAcctId")]
        public string PrimaryTeammateId { get; set; }
        /// <summary>
        /// Enables logging output to 'log.txt'
        /// </summary>
        [JsonPropertyName("loggingEnabled")]
        public bool LoggingEnabled { get; set; }
        /// <summary>
        /// Max game distance to render targets in Aimview, 
        /// and to display dynamic aimlines between two players.
        /// </summary>
        [JsonPropertyName("maxDistance")]
        public float MaxDistance { get; set; }
        /// <summary>
        /// 'Field of View' in degrees to display targets in the Aimview window.
        /// </summary>
        [JsonPropertyName("aimviewFOV")]
        public float AimViewFOV { get; set; }
        /// <summary>
        /// Minimum loot value (rubles) to display 'normal loot' on map.
        /// </summary>
        [JsonPropertyName("minLootValue")]
        public int MinLootValue { get; set; }
        /// <summary>
        /// Minimum loot value (rubles) to display 'important loot' on map.
        /// </summary>
        [JsonPropertyName("minImportantLootValue")]
        public int MinImportantLootValue { get; set; }

        /// <summary>
        /// Enables / disables thermal vision.
        /// </summary>
        [JsonPropertyName("thermalVisionEnabled")]
        public bool ThermalVisionEnabled { get; set; }

        /// <summary>
        /// Enables / disables night vision.
        /// </summary>
        [JsonPropertyName("nightVisionEnabled")]
        public bool NightVisionEnabled { get; set; }

        /// <summary>
        /// Enables / disables thermal optic vision.
        /// </summary>
        [JsonPropertyName("opticThermalVisionEnabled")]
        public bool OpticThermalVisionEnabled { get; set; }

        /// <summary>
        /// Enables / disables no visor.
        /// </summary>
        [JsonPropertyName("noVisorEnabled")]
        public bool NoVisorEnabled { get; set; }

        /// <summary>
        /// Enables / disables no recoil.
        /// </summary>
        [JsonPropertyName("noRecoilSwayEnabled")]
        public bool NoRecoilSwayEnabled { get; set; }

        /// <summary>
        /// Enables / disables max / infinite stamina.
        /// </summary>
        [JsonPropertyName("maxStaminaEnabled")]
        public bool MaxStaminaEnabled { get; set; }

        /// <summary>
        /// Enables / disables double search.
        /// </summary>
        [JsonPropertyName("doubleSearchEnabled")]
        public bool DoubleSearchEnabled { get; set; }

        /// <summary>
        /// Enables / disables jump power modification
        /// </summary>
        [JsonPropertyName("jumpPowerEnabled")]
        public bool JumpPowerEnabled { get; set; }

        /// <summary>
        /// Changes jump power strength
        /// </summary>
        [JsonPropertyName("jumpPowerStrength")]
        public int JumpPowerStrength { get; set; }

        /// <summary>
        /// Enables / disables throw power modification.
        /// </summary>
        [JsonPropertyName("throwPowerEnabled")]
        public bool ThrowPowerEnabled { get; set; }

        /// <summary>
        /// Changes throw power strength.
        /// </summary>
        [JsonPropertyName("throwPowerStrength")]
        public int ThrowPowerStrength { get; set; }

        /// <summary>
        /// Enables / disables faster mag drills.
        /// </summary>
        [JsonPropertyName("magDrillsEnabled")]
        public bool MagDrillsEnabled { get; set; }

        /// <summary>
        /// Changes mag load/unload speed
        /// </summary>
        [JsonPropertyName("magDrillSpeed")]
        public int MagDrillSpeed { get; set; }

        /// <summary>
        /// Enables / disables juggernaut.
        /// </summary>
        [JsonPropertyName("increaseMaxWeightEnabled")]
        public bool IncreaseMaxWeightEnabled { get; set; }

        /// <summary>
        /// Enables / disables juggernaut.
        /// </summary>
        [JsonPropertyName("instantADSEnabled")]
        public bool InstantADSEnabled { get; set; }

        /// <summary>
        /// Enables / disables max / infinite stamina.
        /// </summary>
        [JsonPropertyName("showHoverArmor")]
        public bool ShowHoverArmor { get; set; }

        /// <summary>
        /// Enables / disables exfil names.
        /// </summary>
        [JsonPropertyName("hideExfilNames")]
        public bool HideExfilNames { get; set; }

        /// <summary>
        /// Enables / disables text outlines.
        /// </summary>
        [JsonPropertyName("hideTextOutline")]
        public bool HideTextOutline { get; set; }

        /// <summary>
        /// Enables / disables memory writing master switch.
        /// </summary>
        [JsonPropertyName("masterSwitchEnabled")]
        public bool MasterSwitchEnabled { get; set; }

        /// <summary>
        /// Enables / disables extended reach.
        /// </summary>
        [JsonPropertyName("extendedReachEnabled")]
        public bool ExtendedReachEnabled { get; set; }

        /// <summary>
        /// Enables / disables infinite stamina.
        /// </summary>
        [JsonPropertyName("infiniteStaminaEnabled")]
        public bool InfiniteStaminaEnabled { get; set; }

        /// <summary>
        /// Enables / disables showing corpses.
        /// </summary>
        [JsonPropertyName("showCorpsesEnabled")]
        public bool ShowCorpsesEnabled { get; set; }

        /// <summary>
        /// Enables / disables showing sub items.
        /// </summary>
        [JsonPropertyName("showSubItemsEnabled")]
        public bool ShowSubItemsEnabled { get; set; }

        /// <summary>
        /// Minimum loot value (rubles) to display 'corpses' on map.
        /// </summary>
        [JsonPropertyName("minCorpseValue")]
        public int MinCorpseValue { get; set; }

        /// <summary>
        /// Minimum loot value (rubles) to display 'sub items' on map.
        /// </summary>
        [JsonPropertyName("minSubItemValue")]
        public int MinSubItemValue { get; set; }

        /// <summary>
        /// Enables / disables auto loot refresh.
        /// </summary>
        [JsonPropertyName("autoLootRefreshEnabled")]
        public bool AutoLootRefreshEnabled { get; set; }

        /// <summary>
        /// Allows storage of multiple loot filters.
        /// </summary>
        [JsonPropertyName("lootFilters")]
        public List<LootFilter> Filters { get; set; }

        /// <summary>
        /// Regular Thermal Vision Settings
        /// </summary>
        [JsonPropertyName("mainThermalSetting")]
        public ThermalSettings MainThermalSetting { get; set; }

        /// <summary>
        /// Optical Thermal Vision Settings
        /// </summary>
        [JsonPropertyName("opticThermalSetting")]
        public ThermalSettings OpticThermalSetting { get; set; }

        /// <summary>
        /// Allows storage of colors for ai scav, pscav etc.
        /// </summary>
        [JsonPropertyName("paintColors")]
        //public List<PaintColor> PaintColors { get; set; }
        public Dictionary<string, PaintColor.Colors> PaintColors { get; set; }

        [JsonPropertyName("autoRefreshSettings")]
        public Dictionary<string, int> AutoRefreshSettings { get; set; }

        /// <summary>
        /// Enables / disables locking time.
        /// </summary>
        [JsonPropertyName("lockTimeOfDay")]
        public bool LockTimeOfDay { get; set; }

        /// <summary>
        /// Enables / disables locking time.
        /// </summary>
        [JsonPropertyName("timeOfDay")]
        public float TimeOfDay { get; set; }

        /// <summary>
        /// Enables / disables chams.
        /// </summary>
        [JsonPropertyName("chamsEnabled")]
        public bool ChamsEnabled { get; set; }

        /// <summary>
        /// Enables / disables faster search speed.
        /// </summary>
        [JsonPropertyName("searchSpeedEnabled")]
        public bool SearchSpeedEnabled { get; set; }

        /// <summary>
        /// Changes search speed.
        /// </summary>
        [JsonPropertyName("searchSpeed")]
        public int SearchSpeed { get; set; }

        /// <summary>
        /// Enables / disables locking time.
        /// </summary>
        [JsonPropertyName("threadSpinDelay")]
        public int ThreadSpinDelay { get; set; }

        [JsonIgnore]
        public ParallelOptions ParallelOptions { get; set; }

        public Config()
        {
            Vsync = true;
            PlayerAimLineLength = 1000;
            DefaultZoom = 100;
            UIScale = 100;
            LootEnabled = true;
            QuestHelperEnabled = true;
            AimviewEnabled = false;
            HideNames = false;
            ImportantLootOnly = false;
            HideLootValue = false;
            NoRecoilSwayEnabled = false;
            MaxStaminaEnabled = false;
            LoggingEnabled = false;
            ShowHoverArmor = false;
            MaxDistance = 325;
            AimViewFOV = 30;
            MinLootValue = 90000;
            MinImportantLootValue = 300000;
            PrimaryTeammateId = null;
            Filters = new List<LootFilter>();

            PaintColors = new Dictionary<string, PaintColor.Colors>
            {
                ["AIScav"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 0 },
                ["PScav"] = new PaintColor.Colors { A = 255, R = 255, G = 165, B = 0 },
                ["AIRaider"] = new PaintColor.Colors { A = 255, R = 128, G = 0, B = 128 },
                ["Boss"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 255 },
                ["USEC"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 0 },
                ["BEAR"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 255 },
                ["LocalPlayer"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
                ["Teammate"] = new PaintColor.Colors { A = 255, R = 50, G = 205, B = 50 },
                ["TeamHover"] = new PaintColor.Colors { A = 255, R = 125, G = 252, B = 50 },
                ["Special"] = new PaintColor.Colors { A = 255, R = 255, G = 105, B = 180 },
                ["RegularLoot"] = new PaintColor.Colors { A = 255, R = 245, G = 245, B = 245 },
                ["ImportantLoot"] = new PaintColor.Colors { A = 255, R = 64, G = 224, B = 208 },
                ["QuestItem"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 128 },
                ["QuestZone"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 128 },
                ["ExfilActiveText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
                ["ExfilActiveIcon"] = new PaintColor.Colors { A = 255, R = 50, G = 205, B = 50 },
                ["ExfilPendingText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
                ["ExfilPendingIcon"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 0 },
                ["ExfilClosedText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 },
                ["ExfilClosedIcon"] = new PaintColor.Colors { A = 255, R = 255, G = 0, B = 0 },
                ["TextOutline"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 0 },
                ["DeathMarker"] = new PaintColor.Colors { A = 255, R = 0, G = 0, B = 0 }
            };

            NightVisionEnabled = false;
            ThermalVisionEnabled = false;
            NoVisorEnabled = false;
            OpticThermalVisionEnabled = false;
            DoubleSearchEnabled = false;
            JumpPowerEnabled = false;
            JumpPowerStrength = 4;
            ThrowPowerEnabled = false;
            ThrowPowerStrength = 4;
            MagDrillsEnabled = false;
            MagDrillSpeed = 5;
            IncreaseMaxWeightEnabled = false;
            InstantADSEnabled = false;
            HideExfilNames = false;
            HideTextOutline = false;
            MasterSwitchEnabled = false;
            InfiniteStaminaEnabled = false;
            ExtendedReachEnabled = false;
            ShowCorpsesEnabled = false;
            ShowSubItemsEnabled = false;
            MinCorpseValue = 100000;
            MinSubItemValue = 15000;
            AutoLootRefreshEnabled = false;

            MainThermalSetting = new ThermalSettings(0.5f, 0.001f, -0.5f, 0);
            OpticThermalSetting = new ThermalSettings(0.5f, 0.001f, -0.5f, 0);

            AutoRefreshSettings = new Dictionary<string, int>
            {
                ["Customs"] = 30,
                ["Factory"] = 30,
                ["GroundZero"] = 30,
                ["Interchange"] = 30,
                ["Lighthouse"] = 30,
                ["Reserve"] = 30,
                ["Shoreline"] = 30,
                ["Streets of Tarkov"] = 30,
                ["Labs"] = 30,
                ["Woods"] = 30
            };

            LockTimeOfDay = false;
            TimeOfDay = 12f;

            SearchSpeedEnabled = false;
            SearchSpeed = 4;

            ThreadSpinDelay = 100;

            ChamsEnabled = false;

            ParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 1 };
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
                try
                {
                    if (!File.Exists("Config.json")) throw new FileNotFoundException("Config.json does not exist!");
                    var json = File.ReadAllText("Config.json");
                    config = JsonSerializer.Deserialize<Config>(json);
                    return true;
                }
                catch (Exception ex)
                {
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
                var json = JsonSerializer.Serialize<Config>(config, _jsonOptions);
                File.WriteAllText("Config.json", json);
            }
        }
    }
}
