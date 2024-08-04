using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace eft_dma_radar
{
    static class Program
    {
        private static readonly Mutex _mutex;
        private static readonly bool _singleton;
        private static readonly Config _config;
        private static readonly LootFilterManager _lootFilterManager;
        private static readonly Watchlist _watchlist;
        private static readonly AIFactionManager _aiFactionManager;
        private static readonly object _logLock = new();
        private static readonly StreamWriter _log;

        /// <summary>
        /// Global Program Configuration.
        /// </summary>
        public static Config Config
        {
            get => _config;
        }

        public static Watchlist Watchlist
        {
            get => _watchlist;
        }

        public static AIFactionManager AIFactionManager
        {
            get => _aiFactionManager;
        }

        public static LootFilterManager LootFilterManager
        {
            get => _lootFilterManager;
        }

        #region Static Constructor
        static Program()
        {
            _mutex = new Mutex(true, "9A19103F-16F7-4668-BE54-9A1E7A4F7556", out _singleton);

            if (Config.TryLoadConfig(out _config) is not true)
                _config = new Config();

            if (LootFilterManager.TryLoadLootFilterManager(out _lootFilterManager) is not true)
                _lootFilterManager = new LootFilterManager();

            if (Watchlist.TryLoadWatchlist(out _watchlist) is not true)
                _watchlist = new Watchlist();

            if (AIFactionManager.TryLoadAIFactions(out _aiFactionManager) is not true)
                _aiFactionManager = new AIFactionManager();

            if (_config.Logging)
            {
                _log = File.AppendText("log.txt");
                _log.AutoFlush = true;
            }
        }
        #endregion

        #region Program Entry Point
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode; // allow russian chars
            try
            {
                if (_singleton)
                {
                    RuntimeHelpers.RunClassConstructor(typeof(TarkovDevManager).TypeHandle); // invoke static constructor
                    RuntimeHelpers.RunClassConstructor(typeof(Memory).TypeHandle); // invoke static constructor
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(true);
                    Application.Run(new frmMain());
                }
                else
                {
                    throw new Exception("The Application Is Already Running!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "EFT Radar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Public logging method, writes to Debug Trace, and a Log File (if enabled in Config.Json)
        /// </summary>
        public static void Log(string msg)
        {
            Debug.WriteLine(msg);
            if (_config?.Logging ?? false)
            {
                lock (_logLock) // Sync access to File IO
                {
                    _log.WriteLine($"{DateTime.Now}: {msg}");
                }
            }
        }
        /// <summary>
        /// Hide the 'Program Console Window'.
        /// </summary>
        public static void HideConsole()
        {
            ShowWindow(GetConsoleWindow(), ((_config?.Logging ?? false) ? 1 : 0)); // 0 : SW_HIDE
        }
        #endregion

        #region P/Invokes
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        #endregion
    }
}
