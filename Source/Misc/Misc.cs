using System.Diagnostics;
using System.Text;

namespace eft_dma_radar
{
    // Small & Miscellaneous Classes/Enums Go here

    #region Program Classes
    /// <summary>
    /// Custom Debug Stopwatch class to measure performance.
    /// </summary>
    public class DebugStopwatch
    {
        private readonly Stopwatch _sw;
        private readonly string _name;

        /// <summary>
        /// Constructor. Starts stopwatch.
        /// </summary>
        /// <param name="name">(Optional) Name of stopwatch.</param>
        public DebugStopwatch(string name = null)
        {
            _name = name;
            _sw = new Stopwatch();
            _sw.Start();
        }

        /// <summary>
        /// End stopwatch and display result to Debug Output.
        /// </summary>
        public void Stop()
        {
            _sw.Stop();
            TimeSpan ts = _sw.Elapsed;
            Debug.WriteLine($"{_name} Stopwatch Runtime: {ts.Ticks} ticks");
        }
    }

    public class ItemAnimation
    {
        public LootItem Item { get; set; }
        public float AnimationTime { get; set; }
        public float MaxAnimationTime { get; set; } = 1f;
        public int RepetitionCount { get; set; } = 1;
        public int MaxRepetitions { get; set; }

        public ItemAnimation(LootItem item)
        {
            Item = item;
            AnimationTime = 0f;
            RepetitionCount = 0;
        }
    }

    public class Hotkey
    {
        public string Action { get; set; }
        public Keys Key { get; set; }
        public HotkeyType Type { get; set; }
    }

    public class HotkeyKey
    {
        public string Name { get; }
        public Keys Key { get; }

        public HotkeyKey(string name, Keys key)
        {
            this.Name = name;
            this.Key = key;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
    #endregion

    #region Custom EFT Classes
    public class ThermalSettings
    {
        public float ColorCoefficient { get; set; }
        public float MinTemperature { get; set; }
        public float RampShift { get; set; }
        public int ColorScheme { get; set; }

        public ThermalSettings() { }

        public ThermalSettings(float colorCoefficient, float minTemp, float rampShift, int colorScheme)
        {
            this.ColorCoefficient = colorCoefficient;
            this.MinTemperature = minTemp;
            this.RampShift = rampShift;
            this.ColorScheme = colorScheme;
        }
    }

    public class PlayerInformationSettings
    {
        public bool Name { get; set; }
        public bool Height { get; set; }
        public bool Distance { get; set; }
        public bool Aimline { get; set; }
        public int AimlineLength { get; set; }
        public int AimlineOpacity { get; set; }
        public int Font { get; set; }
        public int FontSize { get; set; }
        public bool Flags { get; set; }
        public bool ActiveWeapon { get; set; }
        public bool Thermal { get; set; }
        public bool NightVision { get; set; }
        public bool Gear { get; set; }
        public bool AmmoType { get; set; }
        public bool Group { get; set; }
        public bool Value { get; set; }
        public bool Health { get; set; }
        public bool Tag { get; set; }
        public int FlagsFont { get; set; }
        public int FlagsFontSize { get; set; }

        public PlayerInformationSettings(
            bool name, bool height, bool distance, bool aimline,
            int aimlineLength, int aimlineOpacity, int font, int fontSize,
            bool flags, bool activeWeapon, bool thermal, bool nightVision,
            bool gear, bool ammoType, bool group, bool value, bool health, 
            bool tag, int flagsFont, int flagsFontSize)
        {
            this.Name = name;
            this.Height = height;
            this.Distance = distance;
            this.Aimline = aimline;
            this.AimlineLength = aimlineLength;
            this.AimlineOpacity = aimlineOpacity;
            this.Font = font;
            this.FontSize = fontSize;
            this.Flags = flags;
            this.ActiveWeapon = activeWeapon;
            this.Thermal = thermal;
            this.NightVision = nightVision;
            this.Gear = gear;
            this.AmmoType = ammoType;
            this.Group = group;
            this.Value = value;
            this.Health = health;
            this.Tag = tag;
            this.FlagsFont = flagsFont;
            this.FlagsFontSize = flagsFontSize;
        }
    }

    public struct AimlineSettings
    {
        public bool Enabled;
        public int Length;
        public int Opacity;
    }
    #endregion

    #region EFT Enums
    [Flags]
    public enum MemberCategory : int
    {
        Default = 0, // Standard Account
        Developer = 1,
        UniqueId = 2, // EOD Account
        Trader = 4,
        Group = 8,
        System = 16,
        ChatModerator = 32,
        ChatModeratorWithPermamentBan = 64,
        UnitTest = 128,
        Sherpa = 256,
        Emissary = 512
    }

    /// <summary>
    /// Defines Player Unit Type (Player,PMC,Scav,etc.)
    /// </summary>
    public enum PlayerType
    {
        Default,
        LocalPlayer,
        Teammate,
        PMC,
        Scav,
        Raider,
        Rogue,
        Boss,
        PlayerScav,
        SpecialPlayer,
        BEAR,
        USEC,
        OfflineScav,
        SniperScav,
        BossGuard,
        BossFollower,
        FollowerOfMorana,
        Cultist,
        Zombie
    }

    public enum PlayerBones
    {
        HumanBase = 0,
        HumanPelvis = 14,
        HumanLThigh1 = 15,
        HumanLThigh2 = 16,
        HumanLCalf = 17,
        HumanLFoot = 18,
        HumanLToe = 19,
        HumanRThigh1 = 20,
        HumanRThigh2 = 21,
        HumanRCalf = 22,
        HumanRFoot = 23,
        HumanRToe = 24,
        HumanSpine1 = 29,
        HumanSpine2 = 36,
        HumanSpine3 = 37,
        HumanLCollarbone = 89,
        HumanLUpperarm = 90,
        HumanLForearm1 = 91,
        HumanLForearm2 = 92,
        HumanLForearm3 = 93,
        HumanLPalm = 94,
        HumanRCollarbone = 110,
        HumanRUpperarm = 111,
        HumanRForearm1 = 112,
        HumanRForearm2 = 113,
        HumanRForearm3 = 114,
        HumanRPalm = 115,
        HumanNeck = 132,
        HumanHead = 133
    };

    public enum HotkeyType
    {
        OnKey,
        Toggle
    }

    public enum HotkeyAction
    {
        Chams,
        ImportantLoot,
        NoRecoil,
        NoSway,
        OpticalThermal,
        ShowContainers,
        ShowCorpses,
        ShowLoot,
        Thirdperson,
        ThermalVision,
        TimeScale,
        ZoomIn,
        ZoomOut
    }
    #endregion

    #region Helpers
    public static class Helpers
    {
        /// <summary>
        /// Returns the 'type' of player based on the 'role' value.
        /// </summary>
        public static readonly Dictionary<char, string> CyrillicToLatinMap = new Dictionary<char, string>
        {
                {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"},
                {'Е', "E"}, {'Ё', "E"}, {'Ж', "Zh"}, {'З', "Z"}, {'И', "I"},
                {'Й', "Y"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"},
                {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"},
                {'У', "U"}, {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "Ts"}, {'Ч', "Ch"},
                {'Ш', "Sh"}, {'Щ', "Shch"}, {'Ъ', ""}, {'Ы', "Y"}, {'Ь', ""},
                {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"},
                {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"},
                {'е', "e"}, {'ё', "e"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
                {'й', "y"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
                {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"},
                {'у', "u"}, {'ф', "f"}, {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"},
                {'ш', "sh"}, {'щ', "shch"}, {'ъ', ""}, {'ы', "y"}, {'ь', ""},
                {'э', "e"}, {'ю', "yu"}, {'я', "ya"}
        };

        public static string TransliterateCyrillic(string input)
        {
            StringBuilder output = new StringBuilder();

            foreach (char c in input)
            {
                output.Append(CyrillicToLatinMap.TryGetValue(c, out var latinEquivalent) ? latinEquivalent : c.ToString());
            }

            return output.ToString();
        }
    }
    #endregion
}
