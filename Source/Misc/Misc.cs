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
    /// <summary>
    /// Global Program Configuration (Config.json)
    /// </summary>
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
    /// <summary>
    /// Contains weapon info for Primary Weapons.
    /// </summary>
    public struct PlayerWeaponInfo
    {
        public string ThermalScope;
        public string AmmoType;

        public override string ToString()
        {
            var result = string.Empty;
            if (AmmoType is not null) result += AmmoType;
            if (ThermalScope is not null)
            {
                if (result != string.Empty) result += $", {ThermalScope}";
                else result += ThermalScope;
            }
            if (result == string.Empty) return null;
            return result;
        }
    }
    /// <summary>
    /// Defines Player Unit Type (Player,PMC,Scav,etc.)
    /// </summary>
    public enum PlayerType
    {
        /// <summary>
        /// Default value if a type cannot be established.
        /// </summary>
        Default,
        /// <summary>
        /// The primary player running this application/radar.
        /// </summary>
        LocalPlayer,
        /// <summary>
        /// Teammate of LocalPlayer.
        /// </summary>
        Teammate,
        /// <summary>
        /// Hostile/Enemy PMC.
        /// </summary>
        PMC,
        /// <summary>
        /// Normal AI Bot Scav.
        /// </summary>
        AIScav,
        /// <summary>
        /// Difficult AI Raider.
        /// </summary>
        AIRaider,
        /// <summary>
        /// Difficult AI Rogue.
        /// </summary>
        AIRogue,
        /// <summary>
        /// Difficult AI Boss.
        /// </summary>
        AIBoss,
        /// <summary>
        /// Player controlled Scav.
        /// </summary>
        PScav,
        /// <summary>
        /// 'Special' Human Controlled Hostile PMC/Scav (on the watchlist, or a special account type).
        /// </summary>
        SpecialPlayer,
        /// <summary>
        /// Hostile/Enemy BEAR PMC.
        /// </summary>
        BEAR,
        /// <summary>
        /// Hostile/Enemy USEC PMC.
        /// </summary>
        USEC,
        /// <summary>
        /// Offline LocalPlayer.
        /// </summary>
        AIOfflineScav,
        AISniperScav,
        AIBossGuard,
        AIBossFollower,
        
    }
    /// <summary>
    /// Defines Role for an AI Bot Player.
    /// </summary>
    public struct AIRole
    {
        /// <summary>
        /// Name of Bot Player.
        /// </summary>
        public string Name;
        /// <summary>
        /// Type of Bot Player.
        /// </summary>
        public PlayerType Type;
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
    #endregion

    #region Helpers
    public static class Helpers
    {
        /// <summary>
        /// Returns the 'type' of player based on the 'role' value.
        /// </summary>
        /// 
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

        public static string[] BossNames = new string[]
        {
            "Big Pipe",
            "Birdeye",
            "BTR",
            "Cultist Priest",
            "Glukhar",
            "Kaban",
            "Killa",
            "Knight",
            "Kollontay",
            "Raider",
            "Renegade",
            "Reshala",
            "Sanitar",
            "Shturman",
            "Tagilla",
            "Zryachiy"
        };

        public static string[] RaiderGuardRogueNames = {
            "Afraid", // Rogues
            "Andresto",
            "Applejuice",
            "Arizona",
            "Auron",
            "Badboy",
            "Baddie",
            "Beard",
            "Beverly",
            "Bison",
            "Blackbird",
            "Blade",
            "Blakemore",
            "Boatswain",
            "Boogerman",
            "Brockley",
            "Browski",
            "Bullet",
            "Bunny",
            "Butcher",
            "Chester",
            "Churchill",
            "Cliffhanger",
            "Condor",
            "Cook",
            "Corsair",
            "Cougar",
            "Coyote",
            "Crooked",
            "Cross",
            "Dakota",
            "Dawg",
            "Deceit",
            "Denver",
            "Diggi",
            "Donutop",
            "Duke",
            "Dustin",
            "Enzo",
            "Esquilo",
            "Father",
            "Firion",
            "Floridaman",
            "Foxy",
            "Frenzy",
            "Garandthumb",
            "Goat",
            "Golden",
            "Grandpa",
            "Greyzone",
            "Grim",
            "Grommet",
            "Gunporn",
            "Handsome",
            "Haunted",
            "Hellshrimp",
            "Honorable",
            "Hypno",
            "Instructor",
            "Iowa",
            "Ironfists",
            "James",
            "Jeff",
            "Jersey",
            "John",
            "Juggernaut",
            "Justkilo",
            "Kanzas",
            "Kentucky",
            "Kry",
            "Lancaster",
            "Lee",
            "Legia",
            "Litton",
            "Lost",
            "Lunar",
            "Madknight",
            "Mamba",
            "Marooner",
            "Marquesses",
            "Meldon",
            "Melo",
            "Michigan",
            "Mike",
            "Momma",
            "Mortal",
            "Mother",
            "Nevada",
            "Nine-hole",
            "Noisy",
            "Nukem",
            "Ocean",
            "Oklahoma",
            "OneEye",
            "Oskar",
            "Panther",
            "Philbo",
            "Quebec",
            "Raccoon",
            "Rage",
            "Rambo",
            "Rassler",
            "Receit",
            "Rib-eye",
            "Riot",
            "Rock",
            "Rocket",
            "Ronflex",
            "Ronny",
            "Rossler",
            "RoughDog",
            "Zero-Zero",
            "Sektant", // Cultists
            "Scar",
            "Scottsdale",
            "Seafarer",
            "Shadow",
            "SharkBait",
            "Sharkkiller",
            "Sherifu",
            "Sherman",
            "Shifty",
            "Slayer",
            "Sly",
            "Snake",
            "Sneaky",
            "Sniperlife",
            "Solem",
            "Solidus",
            "Spectator-6",
            "Spyke",
            "Stamper",
            "Striker",
            "Texas",
            "Three-Teeth",
            "Trent",
            "Trickster",
            "Triggerhappy",
            "Two-Finger",
            "Vicious",
            "Victor",
            "Voodoo",
            "Voss",
            "Wadley",
            "Walker",
            "Weasel",
            "Whale-Eye",
            "Whisky",
            "Whitemane",
            "Woodrow",
            "Wrath",
            "Zed",
            "Zero-Zero",
            "Aimbotkin",
            "Baklazhan", // kaban guards
            "Bazil",
            "Begezhan",
            "Belyash",
            "Bibop",
            "Cheburek",
            "Dihlofos",
            "Dikhlofos",
            "Docha",
            "Flamberg",
            "Gladius",
            "Gromila",
            "Gus",
            "Kant",
            "Kalyanshchik",
            "Kapral",
            "Karas",
            "Kartezhnik",
            "Katorzhnik",
            "Khetchkok",
            "Khvost",
            "Kolt",
            "Kompot",
            "Kozyrek",
            "Kudeyar",
            "Mastak",
            "Mauzer",
            "Medoed",
            "Miposhka",
            "Mosin",
            "Moydodyr",
            "Naperstochnik",
            "Poker",
            "Polzuchiy",
            "Shtempel",
            "Snayler",
            "Sokhatyy",
            "Supermen",
            "Tihiy",
            "Varan",
            "Vasiliy",
            "Verhniy",
            "Zevaka",
            "Afganec",
            "Alfons", // Glukhar guards
            "Assa",
            "Baks",
            "Balu",
            "Banschik",
            "Barguzin",
            "Basmach",
            "Batar",
            "Batya",
            "Belyy",
            "Bob",
            "Borec",
            "Byk",
            "BZT",
            "Calabrissa",
            "Chelovek",
            "Chempion",
            "Chepushila",
            "Dnevalnyy",
            "Drossel",
            "Dum",
            "Fedya",
            "Gepe",
            "Gepard",
            "Gorbatyy",
            "Gotka",
            "Grif",
            "Grustnyy",
            "Kadrovik",
            "Karabin",
            "Karaul",
            "Kastet",
            "Katok",
            "Kocherga",
            "Kosoy",
            "Krot",
            "Kuling",
            "Kumulyativ",
            "Kuzya",
            "Letyoha",
            "Lysyy",
            "Lyutyy",
            "Maga",
            "Matros",
            "Mihalych",
            "Mysh",
            "Nakat",
            "Nemonas",
            "Oficer",
            "Omeh",
            "Oskolochnyy",
            "Otbityy",
            "Patron",
            "Pluton",
            "Radar",
            "Rayan",
            "Rembo",
            "Ryaha",
            "Salobon",
            "Sapog",
            "Seryy",
            "Shapka",
            "Shustryy",
            "Sibiryak",
            "Signal",
            "Sobr",
            "Specnaz",
            "Stvol",
            "Sych",
            "Tankist",
            "Tihohod",
            "Toropyga",
            "Trubochist",
            "Utyug",
            "Valet",
            "Vegan",
            "Veteran",
            "Vityok",
            "Zampolit",
            "Zarya",
            "Zhirnyy",
            "Zh-12",
            "Zimniy",
            "Anton Zavodskoy", // Reshala guards
            "Damirka Zavodskoy",
            "Filya Zavodskoy",
            "Gena Zavodskoy",
            "Grisha Zavodskoy",
            "Kolyan Zavodskoy",
            "Kuling Zavodskoy",
            "Lesha Zavodskoy",
            "Nikita Zavodskoy",
            "Sanya Zavodskoy",
            "Shtopor Zavodskoy",
            "Skif Zavodskoy",
            "Stas Zavodskoy",
            "Toha Zavodskoy",
            "Torpeda Zavodskoy",
            "Vasya Zavodskoy",
            "Vitek Zavodskoy",
            "Zhora Zavodskoy",
            "Dimon Svetloozerskiy", // Shturman guards
            "Enchik Svetloozerskiy",
            "Kachok Svetloozerskiy",
            "Krysa Svetloozerskiy",
            "Malchik Svetloozerskiy",
            "Marat Svetloozerskiy",
            "Mels Svetloozerskiy",
            "Motlya Svetloozerskiy",
            "Motyl Svetloozerskiy",
            "Pashok Svetloozerskiy",
            "Plyazhnik Svetloozerskiy",
            "Robinzon Svetloozerskiy",
            "Sanya Svetloozerskiy",
            "Shmyga Svetloozerskiy",
            "Tokha Svetloozerskiy",
            "Ugryum Svetloozerskiy",
            "Vovan Svetloozerskiy",
            "Akula", // scav raiders
            "Assa",
            "BZT",
            "Balu",
            "Bankir",
            "Barrakuda",
            "Bars",
            "Berkut",
            "Bob",
            "Dikobraz",
            "Gadyuka",
            "Gepard",
            "Grif",
            "Grizzli",
            "Gyurza",
            "Irbis",
            "Jaguar",
            "Kalan",
            "Karakurt",
            "Kayman",
            "Kobra",
            "Kondor",
            "Krachun",
            "Krasnyy volk",
            "Krechet",
            "Kuling",
            "Leopard",
            "Lev",
            "Lis",
            "Loggerhed",
            "Lyutty",
            "Maga",
            "Mangust",
            "Manul",
            "Mantis",
            "Medved",
            "Nosorog",
            "Orel",
            "Orlan",
            "Padalshchik",
            "Pantera",
            "Pchel",
            "Piton",
            "Piranya",
            "Puma",
            "Radar",
            "Rosomaha",
            "Rys",
            "Sapsan",
            "Sekach",
            "Shakal",
            "Signal",
            "Skorpion",
            "Stervyatnik",
            "Tarantul",
            "Taypan",
            "Tigr",
            "Varan",
            "Vegan",
            "Vepr",
            "Veteran",
            "Volk",
            "Voron",
            "Yaguar",
            "Yastreb",
            "Zubr",
            "Akusher", // Sanitar guards
            "Khirurg",
            "Anesteziolog",
            "Dermatolog",
            "Farmacevt",
            "Feldsher",
            "Fiziolog",
            "Glavvrach",
            "Gomeopat",
            "Hirurg",
            "Immunolog",
            "Kardiolog",
            "Laborant",
            "Lasha Ortoped",
            "Lor",
            "Medbrat",
            "Medsestra",
            "Nevrolog",
            "Okulist",
            "Paracetamol",
            "Pilyulya",
            "Proktolog",
            "Propital",
            "Psihiatr",
            "Psikhiatr",
            "Pyotr Planchik",
            "Revmatolog",
            "Rodion Bubesh",
            "Scavvaf",
            "Shpric",
            "Stomatolog",
            "Terapevt",
            "Travmatolog",
            "Trupovoz",
            "Urolog",
            "Vaha Geroy",
            "Venerolog",
            "Zaveduyuschiy",
            "Zaveduyushchiy",
            "Zhgut",
            "Arsenal", // Kollontay guards
            "Basyak",
            "Begemotik",
            "Dezhurka",
            "Furazhka",
            "Glavdur",
            "Kozyrek Desatnik",
            "Mayor",
            "Peps",
            "Serzhant",
            "Slonolyub",
            "Sluzhebka",
            "Starley Desatnik",
            "Starley brat",
            "Starley",
            "Starshiy brat",
            "Strelok brat",
            "Tatyanka Desatnik",
            "Tatyanka",
            "Vasya Desantnik",
            "Visyak",
            "Baba Yaga", // Follower of Morana
            "Buran",
            "Domovoy",
            "Gamayun",
            "Gololed",
            "Gorynych",
            "Hladnik",
            "Hladovit",
            "Holodec",
            "Holodryg",
            "Holodun",
            "Ineevik",
            "Ineyko",
            "Ineynik",
            "Karachun",
            "Kikimora",
            "Koleda",
            "Kupala",
            "Ledorez",
            "Ledovik",
            "Ledyanik",
            "Ledyanoy",
            "Liho",
            "Merzlotnik",
            "Mor",
            "Morozec",
            "Morozina",
            "Morozko",
            "Moroznik",
            "Obmoroz",
            "Poludnik",
            "Serebryak",
            "Severyanin",
            "Sirin",
            "Skvoznyak",
            "Snegobur",
            "Snegoed",
            "Snegohod",
            "Snegovey",
            "Snegovik",
            "Snezhin",
            "Sosulnik",
            "Striga",
            "Studen",
            "Stuzhaylo",
            "Stuzhevik",
            "Sugrobnik",
            "Sugrobus",
            "Talasum",
            "Tryasovica",
            "Tuchevik",
            "Uraganische",
            "Vetrenik",
            "Vetrozloy",
            "Vihrevoy",
            "Viy",
            "Vodyanoy",
            "Vyugar",
            "Vyugovik",
            "Zimar",
            "Zimnik",
            "Zimobor",
            "Zimogor",
            "Zimorod",
            "Zimovey",
            "Zlomraz",
            "Zloveschun"
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
