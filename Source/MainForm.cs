using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using MaterialSkin;
using MaterialSkin.Controls;
using eft_dma_radar.Properties;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static Vmmsharp.LeechCore;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System;
using Offsets;

namespace eft_dma_radar
{
    public partial class frmMain : MaterialForm
    {
        private readonly Config _config;
        private readonly Watchlist _watchlist;
        private readonly LootFilterManager _lootFilterManager;
        private readonly AIFactionManager _aiFactions;
        private readonly SKGLControl _mapCanvas;
        private readonly Stopwatch _fpsWatch = new();
        private readonly object _renderLock = new();
        private readonly object _loadMapBitmapsLock = new();
        private readonly System.Timers.Timer _mapChangeTimer = new(900);
        private readonly List<Map> _maps = new(); // Contains all maps from \\Maps folder
        private readonly List<string> FONTS_TO_USE = new List<string>()
        {
            "Arial",
            "Calibri",
            "Candara",
            "Consolas",
            "Constantia",
            "Corbel",
            "Helvetica",
            "Lato",
            "Roboto",
            "Segoe UI",
            "Tahoma",
            "Trebuchet MS",
            "Verdana",
        };

        private bool _isFreeMapToggled = false;
        private float _uiScale = 1.0f;
        private float _aimviewWindowSize = 200;
        private Player _closestPlayerToMouse = null;
        private LootableObject _closestItemToMouse = null;
        private QuestItem _closestTaskItemToMouse = null;
        private QuestZone _closestTaskZoneToMouse = null;
        private bool _isDragging = false;
        private Point _lastMousePosition = Point.Empty;
        private int? _mouseOverGroup = null;
        private int _fps = 0;
        private int _mapSelectionIndex = 0;
        private Map _selectedMap;
        private SKBitmap[] _loadedBitmaps;
        private MapPosition _mapPanPosition = new();
        private Watchlist.Entry _lastWatchlistEntry;
        private string _lastFactionEntry;
        private List<Player> _watchlistMatchPlayers = new();

        private const int ZOOM_INTERVAL = 10;
        private int targetZoomValue = 0;
        private System.Windows.Forms.Timer zoomTimer;

        private const float DRAG_SENSITIVITY = 3.5f;

        private const double PAN_SMOOTHNESS = 0.1;
        private const int PAN_INTERVAL = 10;
        private SKPoint targetPanPosition;
        private System.Windows.Forms.Timer panTimer;

        private int lastLootItemCount = -1;

        private System.Windows.Forms.Timer itemAnimationTimer;
        private bool isAnimationRunning = false;
        private List<ItemAnimation> activeItemAnimations = new List<ItemAnimation>();
        private LootItem itemToPing = null;
        private bool _isRefreshingLootItems = false;

        #region Getters
        /// <summary>
        /// Radar has found Escape From Tarkov process and is ready.
        /// </summary>
        private bool Ready
        {
            get => Memory.Ready;
        }

        /// <summary>
        /// Radar has found Local Game World.
        /// </summary>
        private bool InGame
        {
            get => Memory.InGame;
        }

        private bool IsAtHideout
        {
            get => Memory.InHideout;
        }
        private string MapName
        {
            get => Memory.MapName;
        }

        private string MapNameFormatted
        {
            get => Memory.MapNameFormatted;
        }

        /// <summary>
        /// LocalPlayer (who is running Radar) 'Player' object.
        /// </summary>
        private Player LocalPlayer
        {
            get => Memory.Players?.FirstOrDefault(x => x.Value.Type is PlayerType.LocalPlayer).Value;
        }

        /// <summary>
        /// All Players in Local Game World (including dead/exfil'd) 'Player' collection.
        /// </summary>
        private ReadOnlyDictionary<string, Player> AllPlayers
        {
            get => Memory.Players;
        }

        /// <summary>
        /// Contains all loot in Local Game World.
        /// </summary>
        private LootManager Loot
        {
            get => Memory.Loot;
        }
        /// <summary>
        /// Contains all 'Hot' grenades in Local Game World, and their position(s).
        /// </summary>
        private ReadOnlyCollection<Grenade> Grenades
        {
            get => Memory.Grenades;
        }

        /// <summary>
        /// Contains all 'Exfils' in Local Game World, and their status/position(s).
        /// </summary>
        private ReadOnlyCollection<Exfil> Exfils
        {
            get => Memory.Exfils;
        }

        /// <summary>
        /// Contains all information related to quests
        /// </summary>
        private QuestManager QuestManager
        {
            get => Memory.QuestManager;
        }

        private ReadOnlyCollection<PlayerCorpse> Corpses
        {
            get => Memory.Corpses;
        }
        #endregion

        #region Constructor
        /// <summary>
        /// GUI Constructor.
        /// </summary>
        public frmMain()
        {
            _config = Program.Config;
            _watchlist = Program.Watchlist;
            _lootFilterManager = Program.LootFilterManager;
            _aiFactions = Program.AIFactionManager;

            InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.EnforceBackcolorOnAllComponents = true;
            materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.Grey800, Primary.Grey800, Primary.Indigo100, Accent.Orange400, TextShade.WHITE);

            LoadConfig();
            LoadMaps();

            _mapCanvas = skMapCanvas;
            _mapCanvas.VSync = _config.VSync;

            _mapChangeTimer.AutoReset = false;
            _mapChangeTimer.Elapsed += MapChangeTimer_Elapsed;

            this.DoubleBuffered = true;

            _fpsWatch.Start();

            zoomTimer = new System.Windows.Forms.Timer();
            zoomTimer.Interval = ZOOM_INTERVAL;
            zoomTimer.Tick += ZoomTimer_Tick;

            panTimer = new System.Windows.Forms.Timer();
            panTimer.Interval = PAN_INTERVAL;
            panTimer.Tick += PanTimer_Tick;
        }
        #endregion

        #region Overrides
        /// <summary>
        /// Form closing event.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true; // Cancel shutdown
            this.Enabled = false; // Lock window
            Memory.Loot?.StopAutoRefresh();
            Memory.Toolbox?.StopToolbox();

            Config.SaveConfig(_config); // Save Config to Config.json
            Memory.Shutdown(); // Wait for Memory Thread to gracefully exit
            e.Cancel = false; // Ready to close
            base.OnFormClosing(e); // Proceed with closing
        }

        /// <summary>
        /// Process hotkey presses.sc
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => keyData switch
        {
            Keys.F1 => ZoomIn(5),
            Keys.F2 => ZoomOut(5),
            Keys.F4 => swAimview.Checked = !swAimview.Checked,
            Keys.F5 => ToggleMap(),
            Keys.Control | Keys.N => swNightVision.Checked = !swNightVision.Checked,
            Keys.Control | Keys.T => swThermalVision.Checked = !swThermalVision.Checked,
            _ => base.ProcessCmdKey(ref msg, keyData),
        };

        /// <summary>
        /// Process mousewheel events.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (tabControlMain.SelectedIndex == 0) // Main Radar Tab should be open
            {
                var zoomSens = (double)_config.ZoomSensitivity / 100;
                int zoomDelta = -(int)(e.Delta * zoomSens);

                if (zoomDelta < 0)
                    ZoomIn(-zoomDelta);
                else if (zoomDelta > 0)
                    ZoomOut(zoomDelta);

                if (this._isFreeMapToggled && zoomDelta < 0) // Only move the zoom position when scrolling in
                {
                    var mousePos = this._mapCanvas.PointToClient(Cursor.Position);
                    var mapParams = GetMapLocation();
                    var mapMousePos = new SKPoint(
                        mapParams.Bounds.Left + mousePos.X / mapParams.XScale,
                        mapParams.Bounds.Top + mousePos.Y / mapParams.YScale
                    );

                    this.targetPanPosition = mapMousePos;

                    if (!this.panTimer.Enabled)
                        this.panTimer.Start();
                }

                return;
            }

            base.OnMouseWheel(e);
        }
        #endregion

        #region GUI Events / Functions
        #region General Helper Functions
        private bool ToggleMap()
        {
            if (!btnToggleMap.Enabled)
                return false;

            if (_mapSelectionIndex == _maps.Count - 1)
                _mapSelectionIndex = 0; // Start over when end of maps reached
            else
                _mapSelectionIndex++; // Move onto next map

            tabRadar.Text = $"Radar ({_maps[_mapSelectionIndex].Name})";
            _mapChangeTimer.Restart(); // Start delay

            return true;
        }

        private void InitiateWatchlist()
        {
            if (_watchlist.Profiles.Count == 0)
            {
                _watchlist.AddEmptyProfile();
            }

            UpdateWatchlistProfiles();
        }

        private void InitiateColors()
        {
            UpdatePaintColorControls();
            UpdateThemeColors();
        }

        private void InitiateLootFilter()
        {
            if (_config.Filters.Count == 0)
            {
                var newFilter = new LootFilterManager.Filter()
                {
                    Order = 1,
                    IsActive = true,
                    Name = "Default",
                    Items = new List<String>(),
                    Color = new PaintColor.Colors
                    {
                        R = 255,
                        G = 255,
                        B = 255,
                        A = 255
                    }
                };

                _config.Filters.Add(newFilter);
                LootFilterManager.SaveLootFilterManager(_lootFilterManager);
            }

            cboLootFilterItemsToAdd.Items.AddRange(TarkovDevManager.AllItems.Select(x => x.Value).OrderBy(x => x.Name).Take(25).ToArray());
            cboLootFilterItemsToAdd.DisplayMember = "Name";

            UpdateLootFilters();
        }

        private void InitiateFactions()
        {
            if (_aiFactions.Factions.Count == 0)
                _aiFactions.AddEmptyFaction();

            UpdateFactionPlayerTypes();
            UpdateFactions();
        }

        private void InitiateAutoMapRefreshItems()
        {
            cboAutoRefreshMap.Items.AddRange(_config.LootItemRefreshSettings.Keys.ToArray());
            cboAutoRefreshMap.SelectedIndex = _selectedMap is not null ? cboAutoRefreshMap.FindStringExact(_selectedMap.Name) : 0;
        }

        private void InitiateUIScaling()
        {
            _uiScale = (.01f * _config.UIScale);

            #region Update Paints/Text
            SKPaints.TextBaseOutline.StrokeWidth = 2 * _uiScale;
            SKPaints.TextRadarStatus.TextSize = 48 * _uiScale;
            SKPaints.PaintBase.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintMouseoverGroup.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintDeathMarker.StrokeWidth = 3 * _uiScale;
            SKPaints.LootPaint.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintTransparentBacker.StrokeWidth = 1 * _uiScale;
            SKPaints.PaintAimviewCrosshair.StrokeWidth = 1 * _uiScale;
            SKPaints.PaintGrenades.StrokeWidth = 3 * _uiScale;
            SKPaints.PaintExfilOpen.StrokeWidth = 1 * _uiScale;
            SKPaints.PaintExfilPending.StrokeWidth = 1 * _uiScale;
            SKPaints.PaintExfilClosed.StrokeWidth = 1 * _uiScale;
            #endregion

            _aimviewWindowSize = 200 * _uiScale;

            InitiateFontSizes();
        }

        private void InitiateFonts()
        {
            var fontToUse = SKTypeface.FromFamilyName(cboGlobalFont.Text);
            SKPaints.TextMouseoverGroup.Typeface = fontToUse;
            SKPaints.TextBase.Typeface = fontToUse;
            SKPaints.LootText.Typeface = fontToUse;
            SKPaints.TextBaseOutline.Typeface = fontToUse;
            SKPaints.TextRadarStatus.Typeface = fontToUse;
        }

        private void InitiateSKColors()
        {
            foreach (var paintColor in _config.PaintColors)
            {
                var value = paintColor.Value;
                var color = new SKColor(value.R, value.G, value.B, value.A);

                Extensions.SKColors.Add(paintColor.Key, color);
            }
        }

        private void InitiateFontSizes()
        {
            SKPaints.TextMouseoverGroup.TextSize = _config.GlobalFontSize * _uiScale;
            SKPaints.TextBase.TextSize = _config.GlobalFontSize * _uiScale;
            SKPaints.LootText.TextSize = _config.GlobalFontSize * _uiScale;
            SKPaints.TextBaseOutline.TextSize = _config.GlobalFontSize * _uiScale;

            foreach (var setting in _config.PlayerInformationSettings)
            {
                var key = setting.Key;
                var value = setting.Value;

                Extensions.PlayerTypeTextPaints[key].TextSize = value.FontSize * _uiScale;
                Extensions.PlayerTypeFlagTextPaints[key].TextSize = value.FlagsFontSize * _uiScale;
            }
        }

        private DialogResult ShowErrorDialog(string message)
        {
            return new MaterialDialog(this, "Error", message, "OK", false, "", true).ShowDialog(this);
        }

        private DialogResult ShowConfirmationDialog(string message, string title)
        {
            return new MaterialDialog(this, title, message, "Yes", true, "No", true).ShowDialog(this);
        }

        private void LoadMaps()
        {
            var dir = new DirectoryInfo($"{Environment.CurrentDirectory}\\Maps");
            if (!dir.Exists)
                dir.Create();

            var configs = dir.GetFiles("*.json");
            //Debug.WriteLine($"Found {configs.Length} .json map configs.");
            if (configs.Length == 0)
                throw new IOException("No .json map configs found!");

            foreach (var config in configs)
            {
                var name = Path.GetFileNameWithoutExtension(config.Name);
                //Debug.WriteLine($"Loading Map: {name}");
                var mapConfig = MapConfig.LoadFromFile(config.FullName); // Assuming LoadFromFile is updated to handle new JSON format
                //Add map ID to map config
                var mapID = mapConfig.MapID[0];
                var map = new Map(name.ToUpper(), mapConfig, config.FullName, mapID);
                // Assuming map.ConfigFile now has a 'mapLayers' property that is a List of a new type matching the JSON structure
                map.ConfigFile.MapLayers = map.ConfigFile
                    .MapLayers
                    .OrderBy(x => x.MinHeight)
                    .ToList();

                _maps.Add(map);
            }
        }

        private void CheckConfigDictionaries()
        {
            UpdateDictionary(_config.PaintColors, _config.DefaultPaintColors);
            UpdateDictionary(_config.LootItemRefreshSettings, _config.DefaultAutoRefreshSettings);
            UpdateDictionary(_config.Chams, _config.DefaultChamsSettings);
            UpdateDictionary(_config.LootContainerSettings, _config.DefaultContainerSettings);
            UpdateDictionary(_config.LootPing, _config.DefaultLootPingSettings);
            UpdateDictionary(_config.MaxSkills, _config.DefaultMaxSkillsSettings);
            UpdateDictionary(_config.PlayerInformationSettings, _config.DefaultPlayerInformationSettings);
        }

        private void UpdateDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, Dictionary<TKey, TValue> defaultDictionary)
        {
            if (dictionary.Count != defaultDictionary.Count)
            {
                foreach (var setting in defaultDictionary)
                {
                    if (!dictionary.ContainsKey(setting.Key))
                        dictionary.TryAdd(setting.Key, setting.Value);
                }
            }
        }

        private void LoadConfig()
        {
            this.CheckConfigDictionaries();
            this.InitiateSKColors();
            this.SetupFonts();

            #region Settings
            #region General
            // Radar
            swRadarStats.Checked = _config.RadarStats;
            mcRadarStats.Visible = _config.RadarStats;
            swRadarVsync.Checked = _config.VSync;
            swRadarEnemyCount.Checked = _config.EnemyCount;
            mcRadarEnemyStats.Visible = _config.EnemyCount;
            mcRadarLootItemViewer.Visible = _config.LootItemViewer;
            swPvEMode.Checked = _config.PvEMode;

            btnTriggerUnityCrash.Visible = _config.PvEMode;

            // User Interface
            swLooseLoot.Checked = _config.LooseLoot;
            swAimview.Checked = _config.Aimview;
            swExfilNames.Checked = _config.ExfilNames;
            swHoverArmor.Checked = _config.HoverArmor;
            txtTeammateID.Text = _config.PrimaryTeammateId;
            sldrZoomSensitivity.Value = _config.ZoomSensitivity;

            sldrUIScale.Value = _config.UIScale;
            cboGlobalFont.SelectedIndex = _config.GlobalFont;
            sldrFontSize.Value = _config.GlobalFontSize;
            #endregion

            #region Memory Writing
            swMasterSwitch.Checked = _config.MasterSwitch;

            // Global Features
            mcSettingsMemoryWritingGlobal.Enabled = _config.MasterSwitch;
            swThirdperson.Checked = _config.Thirdperson;
            swFreezeTime.Checked = _config.FreezeTimeOfDay;
            sldrTimeOfDay.Enabled = _config.FreezeTimeOfDay;
            sldrTimeOfDay.Value = (int)_config.TimeOfDay;
            swInfiniteStamina.Checked = _config.InfiniteStamina;
            swTimeScale.Checked = _config.TimeScale;
            sldrTimeScaleFactor.Enabled = _config.TimeScale;
            sldrTimeScaleFactor.Value = (int)(_config.TimeScaleFactor * 10);
            lblSettingsMemoryWritingTimeScaleFactor.Text = $"x{(_config.TimeScaleFactor)}";
            lblSettingsMemoryWritingTimeScaleFactor.Enabled = _config.TimeScale;

            swLootThroughWalls.Checked = _config.LootThroughWalls;
            sldrLootThroughWallsDistance.Enabled = _config.LootThroughWalls;

            swExtendedReach.Checked = _config.ExtendedReach;
            sldrExtendedReachDistance.Enabled = _config.ExtendedReach;

            // Gear Features
            mcSettingsMemoryWritingGear.Enabled = _config.MasterSwitch;
            swNoRecoilSway.Checked = _config.NoRecoilSway;
            swInstantADS.Checked = _config.InstantADS;
            swNoVisor.Checked = _config.NoVisor;
            swThermalVision.Checked = _config.ThermalVision;
            swOpticalThermal.Checked = _config.OpticThermalVision;
            swNightVision.Checked = _config.NightVision;
            swNoWeaponMalfunctions.Checked = _config.NoWeaponMalfunctions;

            // Max Skill Buff Management
            mcSettingsMemoryWritingSkillBuffs.Enabled = _config.MasterSwitch;
            swMaxEndurance.Checked = _config.MaxSkills["Endurance"];
            swMaxStrength.Checked = _config.MaxSkills["Strength"];
            swMaxVitality.Checked = _config.MaxSkills["Vitality"];
            swMaxHealth.Checked = _config.MaxSkills["Health"];
            swMaxStressResistance.Checked = _config.MaxSkills["Stress Resistance"];
            swMaxMetabolism.Checked = _config.MaxSkills["Metabolism"];
            swMaxImmunity.Checked = _config.MaxSkills["Immunity"];
            swMaxPerception.Checked = _config.MaxSkills["Perception"];
            swMaxIntellect.Checked = _config.MaxSkills["Intellect"];
            swMaxAttention.Checked = _config.MaxSkills["Attention"];
            swMaxCovertMovement.Checked = _config.MaxSkills["Covert Movement"];
            swMaxThrowables.Checked = _config.MaxSkills["Throwables"];
            swMaxSurgery.Checked = _config.MaxSkills["Surgery"];
            swMaxSearch.Checked = _config.MaxSkills["Search"];
            swMaxMagDrills.Checked = _config.MaxSkills["Mag Drills"];
            swMaxLightVests.Checked = _config.MaxSkills["Light Vests"];
            swMaxHeavyVests.Checked = _config.MaxSkills["Heavy Vests"];

            sldrMagDrillsSpeed.Enabled = _config.MaxSkills["Mag Drills"];
            sldrMagDrillsSpeed.Value = _config.MagDrillSpeed;
            sldrThrowStrength.Enabled = _config.MaxSkills["Strength"];
            sldrThrowStrength.Value = _config.ThrowPowerStrength;

            // Thermal Features
            mcSettingsMemoryWritingThermal.Enabled = _config.MasterSwitch;
            mcSettingsMemoryWritingThermal.Enabled = (_config.ThermalVision || _config.OpticThermalVision);
            cboThermalType.SelectedIndex = _config.ThermalVision ? 0 : (_config.OpticThermalVision ? 1 : 0);
            cboThermalColorScheme.SelectedIndex = _config.ThermalVision ? _config.MainThermalSetting.ColorScheme : (_config.OpticThermalVision ? _config.OpticThermalSetting.ColorScheme : 0);
            sldrThermalColorCoefficient.Value = (int)(_config.MainThermalSetting.ColorCoefficient * 100);
            sldrMinTemperature.Value = (int)((_config.MainThermalSetting.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            sldrThermalRampShift.Value = (int)((_config.MainThermalSetting.RampShift + 1.0f) * 100.0f);

            // Chams
            mcSettingsMemoryWritingChams.Enabled = _config.MasterSwitch;
            var enabled = _config.Chams["Enabled"];
            swChams.Checked = enabled;
            swChamsPMCs.Checked = _config.Chams["PMCs"];
            swChamsPlayerScavs.Checked = _config.Chams["PlayerScavs"];
            swChamsBosses.Checked = _config.Chams["Bosses"];
            swChamsRogues.Checked = _config.Chams["Rogues"];
            swChamsCultists.Checked = _config.Chams["Cultists"];
            swChamsScavs.Checked = _config.Chams["Scavs"];
            swChamsTeammates.Checked = _config.Chams["Teammates"];
            swChamsCorpses.Checked = _config.Chams["Corpses"];
            swChamsRevert.Checked = _config.Chams["RevertOnClose"];

            this.ToggleChamsControls();
            #endregion

            #region Loot
            // General
            var processLootEnabled = _config.ProcessLoot;
            swProcessLoot.Checked = processLootEnabled;
            this.UpdateLootControls();
            swFilteredOnly.Checked = _config.ImportantLootOnly;
            swSubItems.Checked = _config.SubItems;
            swItemValue.Checked = _config.LootValue;
            swLooseLoot.Checked = _config.LooseLoot;
            swCorpses.Checked = _config.LootCorpses;
            swAutoLootRefresh.Checked = _config.LootItemRefresh;

            // Quest Helper
            var questHelperEnabled = _config.QuestHelper;
            swQuestHelper.Checked = questHelperEnabled;
            this.UpdateQuestControls();
            swUnknownQuestItems.Checked = _config.UnknownQuestItems;
            swQuestItems.Checked = _config.QuestItems;
            swQuestLootItems.Checked = _config.QuestLootItems;
            swQuestLocations.Checked = _config.QuestLocations;
            swAutoTaskRefresh.Checked = _config.QuestTaskRefresh;
            sldrAutoTaskRefreshDelay.Value = _config.QuestTaskRefreshDelay;

            // Minimum Loot Value
            sldrMinRegularLoot.Value = _config.MinLootValue / 1000;
            sldrMinImportantLoot.Value = _config.MinImportantLootValue / 1000;
            sldrMinCorpse.Value = _config.MinCorpseValue / 1000;
            sldrMinSubItems.Value = _config.MinSubItemValue / 1000;

            // Loot Ping
            sldrLootPingAnimationSpeed.Value = _config.LootPing["AnimationSpeed"];
            sldrLootPingMaxRadius.Value = _config.LootPing["Radius"];
            sldrLootPingRepetition.Value = _config.LootPing["Repetition"];

            // Container settings
            swContainers.Checked = _config.LootContainerSettings["Enabled"];
            lstContainers.Enabled = _config.LootContainerSettings["Enabled"];
            #endregion
            #endregion

            this.InitiateContainerList();
            this.UpdatePlayerInformationSettings();
            this.UpdatePvEControls();
            this.InitiateAutoMapRefreshItems();
            this.InitiateFactions();
            this.InitiateLootFilter();
            this.InitiateWatchlist();
            this.InitiateColors();
            this.InitiateFonts();
            this.InitiateUIScaling();
        }
        #endregion

        #region General Event Handlers
        private async void frmMain_Shown(object sender, EventArgs e)
        {
            while (_mapCanvas.GRContext is null)
                await Task.Delay(1);

            _mapCanvas.GRContext.SetResourceCacheLimit(503316480); // Fixes low FPS on big maps

            while (true)
            {
                await Task.Run(() => Thread.SpinWait(50000)); // High performance async delay
                _mapCanvas.Refresh(); // draw next frame
            }
        }

        private void MapChangeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.BeginInvoke(
                new MethodInvoker(
                    delegate
                    {
                        btnToggleMap.Enabled = false;
                        btnToggleMap.Text = "Loading...";
                    }
                )
            );

            lock (_renderLock)
            {
                try
                {
                    _selectedMap = _maps[_mapSelectionIndex]; // Swap map

                    if (_loadedBitmaps is not null)
                    {
                        foreach (var bitmap in _loadedBitmaps)
                            bitmap?.Dispose(); // Cleanup resources
                    }

                    _loadedBitmaps = new SKBitmap[_selectedMap.ConfigFile.MapLayers.Count];

                    for (int i = 0; i < _loadedBitmaps.Length; i++)
                    {
                        using (
                            var stream = File.Open(
                                _selectedMap.ConfigFile.MapLayers[i].Filename,
                                FileMode.Open,
                                FileAccess.Read))
                        {
                            _loadedBitmaps[i] = SKBitmap.Decode(stream); // Load new bitmap(s)
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"ERROR loading {_selectedMap.ConfigFile.MapLayers[0].Filename}: {ex}"
                    );
                }
                finally
                {
                    this.BeginInvoke(
                        new MethodInvoker(
                            delegate
                            {
                                btnToggleMap.Enabled = true;
                                btnToggleMap.Text = "Toggle Map (F5)";
                            }
                        )
                    );
                }
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedIndex == 2)
            {
                GenerateCards(flpPlayerLoadoutsPlayers, x => x.IsHumanHostileActive, x => x.IsPMC, x => x.Value);
                GenerateCards(flpPlayerLoadoutsBosses, x => x.IsHostileActive && !x.IsHuman && x.IsBossRaider, x => x.Type == PlayerType.Boss, x => x.IsBossRaider, x => x.Value);
                GenerateCards(flpPlayerLoadoutsAI, x => x.IsHostileActive && !x.IsHuman && !x.IsBossRaider, x => x.Value);
            }
        }

        private void GenerateCards(FlowLayoutPanel panel, Func<Player, bool> filter, params Func<Player, IComparable>[] orderBy)
        {
            panel.Controls.Clear();

            if (!this.InGame)
                return;

            var enemyPlayers = this.AllPlayers?
                .Select(x => x.Value)
                .Where(filter)
                .ToList();

            if (enemyPlayers is null)
                return;

            foreach (var orderByFunc in orderBy.Reverse())
            {
                enemyPlayers = enemyPlayers.OrderByDescending(orderByFunc).ToList();
            }

            foreach (var player in enemyPlayers)
            {
                var playerCard = new MaterialCard();
                playerCard.Width = panel.Width - 30;
                playerCard.Margin = new Padding(5, 0, 0, 10);

                var tableLayoutPanel = new TableLayoutPanel();
                tableLayoutPanel.ColumnCount = 1;
                tableLayoutPanel.AutoSize = true;
                tableLayoutPanel.Dock = DockStyle.Top;
                playerCard.Controls.Add(tableLayoutPanel);

                var titleLabel = new MaterialLabel();
                titleLabel.Text = $"{player.Name} ({player.Type}){(player.GroupID != -1 ? $" G:{player.GroupID}" : "")}";
                titleLabel.AutoSize = true;
                titleLabel.Dock = DockStyle.Top;
                tableLayoutPanel.Controls.Add(titleLabel);

                var gearPanel = new FlowLayoutPanel();
                gearPanel.FlowDirection = FlowDirection.TopDown;
                gearPanel.AutoSize = true;
                gearPanel.Dock = DockStyle.Top;
                tableLayoutPanel.Controls.Add(gearPanel);

                if (player.Gear is not null)
                {
                    foreach (var slot in player.Gear)
                    {
                        var gearItem = slot.Value;
                        var gearName = gearItem.Long;

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.AmmoType))
                            gearName += $" ({gearItem.GearInfo.AmmoType})";

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.Thermal))
                            gearName += $" ({gearItem.GearInfo.Thermal})";

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.NightVision))
                            gearName += $" ({gearItem.GearInfo.NightVision})";

                        var gearLabel = new MaterialLabel();
                        gearLabel.Text = $"{GearManager.GetGearSlotName(slot.Key)}: {gearName.Trim()}";
                        gearLabel.Margin = new Padding(0, 5, 0, 0);
                        gearLabel.AutoSize = true;
                        gearLabel.FontType = MaterialSkinManager.fontType.Body2;
                        gearPanel.Controls.Add(gearLabel);
                    }
                }
                else
                {
                    var errorLabel = new MaterialLabel();
                    errorLabel.Text = "ERROR retrieving gear";
                    errorLabel.Margin = new Padding(0, 5, 0, 0);
                    errorLabel.AutoSize = true;
                    gearPanel.Controls.Add(errorLabel);
                }

                int titleHeight = titleLabel.GetPreferredSize(new Size(playerCard.Width, 0)).Height;
                int gearPanelHeight = gearPanel.GetPreferredSize(new Size(playerCard.Width, 0)).Height;
                playerCard.Height = titleHeight + gearPanelHeight;

                panel.Controls.Add(playerCard);
            }
        }
        #endregion

        #region Radar Tab
        #region Helper Functions
        private void ResetVariables()
        {
            this._selectedMap = null;

            lblRadarFPSValue.Text = "0";
            lblRadarMemSValue.Text = "0";
            lblRadarLooseLootValue.Text = "0";
            lblRadarContainersValue.Text = "0";
            lblRadarCorpsesValue.Text = "0";

            lblRadarPMCsValue.Text = "0";
            lblRadarPMCsValue.UseAccent = false;
            lblRadarPMCsValue.HighEmphasis = false;
            lblRadarPlayerScavsValue.Text = "0";
            lblRadarPlayerScavsValue.UseAccent = false;
            lblRadarPlayerScavsValue.HighEmphasis = false;
            lblRadarAIScavsValue.Text = "0";
            lblRadarAIScavsValue.UseAccent = false;
            lblRadarAIScavsValue.HighEmphasis = false;
            lblRadarRoguesValue.Text = "0";
            lblRadarRoguesValue.UseAccent = false;
            lblRadarRoguesValue.HighEmphasis = false;
            lblRadarBossesValue.Text = "0";
            lblRadarBossesValue.UseAccent = false;
            lblRadarBossesValue.HighEmphasis = false;

            lastLootItemCount = 0;
            itemToPing = null;
            lstLootItems.Items.Clear();

            ClearItemRefs();
            ClearPlayerRefs();
            ClearTaskItemRefs();
            ClearTaskZoneRefs();
        }

        private void UpdateWindowTitle()
        {
            bool inGame = this.InGame;
            var localPlayer = this.LocalPlayer;

            if (inGame && localPlayer is not null)
            {
                UpdateSelectedMap();

                if (_fpsWatch.ElapsedMilliseconds >= 1000)
                {
                    // RE-ENABLE & EXPLORE WHAT THIS DOES
                    //_mapCanvas.GRContext.PurgeResources(); // Seems to fix mem leak issue on increasing resource cache

                    #region Radar Stats
                    var fps = _fps;
                    var memTicks = Memory.Ticks;
                    var looseLoot = Loot?.TotalLooseLoot ?? 0;
                    var containers = Loot?.TotalContainers ?? 0;
                    var corpses = Loot?.TotalCorpses ?? 0;

                    if (lblRadarFPSValue.Text != fps.ToString())
                        lblRadarFPSValue.Text = $"{fps}";

                    if (lblRadarMemSValue.Text != memTicks.ToString())
                        lblRadarMemSValue.Text = $"{memTicks}";

                    if (lblRadarLooseLootValue.Text != looseLoot.ToString())
                        lblRadarLooseLootValue.Text = $"{looseLoot}";

                    if (lblRadarContainersValue.Text != containers.ToString())
                        lblRadarContainersValue.Text = $"{containers}";

                    if (lblRadarCorpsesValue.Text != corpses.ToString())
                        lblRadarCorpsesValue.Text = $"{corpses}";
                    #endregion

                    #region Enemy Stats
                    var playerCounts = AllPlayers
                        .Where(x => x.Value.IsAlive && x.Value.IsActive)
                        .GroupBy(x => x.Value.Type)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var enemyPMCs = playerCounts.GetValueOrDefault(PlayerType.USEC, 0) + playerCounts.GetValueOrDefault(PlayerType.BEAR, 0);
                    var playerScavs = playerCounts.GetValueOrDefault(PlayerType.PlayerScav, 0);
                    var aiScavs = playerCounts.GetValueOrDefault(PlayerType.Scav, 0);
                    var rogues = playerCounts.GetValueOrDefault(PlayerType.Raider) +
                                 playerCounts.GetValueOrDefault(PlayerType.Rogue) +
                                 playerCounts.GetValueOrDefault(PlayerType.BossFollower) +
                                 playerCounts.GetValueOrDefault(PlayerType.BossGuard) +
                                 playerCounts.GetValueOrDefault(PlayerType.Cultist);

                    var bosses = playerCounts.GetValueOrDefault(PlayerType.Boss, 0);

                    if (lblRadarPMCsValue.Text != enemyPMCs.ToString())
                    {
                        lblRadarPMCsValue.Text = $"{enemyPMCs}";
                        lblRadarPMCsValue.UseAccent = enemyPMCs > 0;
                        lblRadarPMCsValue.HighEmphasis = enemyPMCs > 0;
                    }

                    if (lblRadarPlayerScavsValue.Text != playerScavs.ToString())
                    {
                        lblRadarPlayerScavsValue.Text = $"{playerScavs}";
                        lblRadarPlayerScavsValue.UseAccent = playerScavs > 0;
                        lblRadarPlayerScavsValue.HighEmphasis = playerScavs > 0;
                    }


                    if (lblRadarAIScavsValue.Text != aiScavs.ToString())
                    {
                        lblRadarAIScavsValue.Text = $"{aiScavs}";
                        lblRadarAIScavsValue.UseAccent = aiScavs > 0;
                        lblRadarAIScavsValue.HighEmphasis = aiScavs > 0;
                    }


                    if (lblRadarRoguesValue.Text != rogues.ToString())
                    {
                        lblRadarRoguesValue.Text = $"{rogues}";
                        lblRadarRoguesValue.UseAccent = rogues > 0;
                        lblRadarRoguesValue.HighEmphasis = rogues > 0;
                    }

                    if (lblRadarBossesValue.Text != bosses.ToString())
                    {
                        lblRadarBossesValue.Text = $"{bosses}";
                        lblRadarBossesValue.UseAccent = bosses > 0;
                        lblRadarBossesValue.HighEmphasis = bosses > 0;
                    }

                    #endregion

                    _fpsWatch.Restart();
                    _fps = 0;
                }
                else
                {
                    _fps++;
                }
            }
        }

        private void UpdateSelectedMap()
        {
            string currentMap = this.MapName;
            string currentMapPrefix = currentMap.ToLower().Substring(0, Math.Min(4, currentMap.Length));

            if (_selectedMap is null || !_selectedMap.MapID.ToLower().StartsWith(currentMapPrefix))
            {
                var selectedMapName = _maps.FirstOrDefault(x => x.MapID.ToLower().StartsWith(currentMapPrefix) || x.MapID.ToLower() == currentMap.ToLower());

                if (selectedMapName is not null)
                {
                    _selectedMap = selectedMapName;

                    // Init map
                    CleanupLoadedBitmaps();
                    LoadMapBitmaps();

                    int selectedIndex = cboAutoRefreshMap.FindString(this.MapNameFormatted);
                    cboAutoRefreshMap.SelectedIndex = selectedIndex != 0 ? selectedIndex : 0;
                }
            }
        }

        private void CleanupLoadedBitmaps()
        {
            if (_loadedBitmaps is not null)
            {
                Parallel.ForEach(_loadedBitmaps, bitmap =>
                {
                    bitmap?.Dispose();
                });

                _loadedBitmaps = null;
            }
        }

        private void LoadMapBitmaps()
        {
            var mapLayers = _selectedMap.ConfigFile.MapLayers;
            _loadedBitmaps = new SKBitmap[mapLayers.Count];

            Parallel.ForEach(mapLayers, (mapLayer, _, _) =>
            {
                lock (_loadMapBitmapsLock)
                {
                    using (var stream = File.Open(mapLayer.Filename, FileMode.Open, FileAccess.Read))
                    {
                        _loadedBitmaps[mapLayers.IndexOf(mapLayer)] = SKBitmap.Decode(stream);
                        _loadedBitmaps[mapLayers.IndexOf(mapLayer)].SetImmutable();
                    }
                }
            });
        }

        private bool IsReadyToRender()
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            bool isAtHideout = this.IsAtHideout;
            bool localPlayerExists = this.LocalPlayer is not null;
            bool selectedMapLoaded = this._selectedMap is not null;

            if (!isReady)
                return false; // Game process not running

            if (isAtHideout)
                return false; // Main menu or hideout

            if (!inGame)
                return false; // Waiting for raid start

            if (!localPlayerExists)
                return false; // Cannot find local player

            if (!selectedMapLoaded)
                return false; // Map not loaded

            return true; // Ready to render
        }

        private int GetMapLayerIndex(float playerHeight)
        {
            for (int i = _loadedBitmaps.Length - 1; i >= 0; i--)
            {
                if (playerHeight > _selectedMap.ConfigFile.MapLayers[i].MinHeight)
                {
                    return i;
                }
            }

            return 0; // Default to the first layer if no match is found
        }

        private MapParameters GetMapParameters(MapPosition localPlayerPos)
        {
            int mapLayerIndex = GetMapLayerIndex(localPlayerPos.Height);

            var bitmap = _loadedBitmaps[mapLayerIndex];
            float zoomFactor = 0.01f * _config.DefaultZoom;
            float zoomWidth = bitmap.Width * zoomFactor;
            float zoomHeight = bitmap.Height * zoomFactor;

            var bounds = new SKRect(
                localPlayerPos.X - zoomWidth / 2,
                localPlayerPos.Y - zoomHeight / 2,
                localPlayerPos.X + zoomWidth / 2,
                localPlayerPos.Y + zoomHeight / 2
            ).AspectFill(_mapCanvas.CanvasSize);

            return new MapParameters
            {
                UIScale = _uiScale,
                MapLayerIndex = mapLayerIndex,
                Bounds = bounds,
                XScale = (float)_mapCanvas.Width / bounds.Width, // Set scale for this frame
                YScale = (float)_mapCanvas.Height / bounds.Height // Set scale for this frame
            };
        }

        private MapParameters GetMapLocation()
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = localPlayer.Position;
                var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);

                if (_isFreeMapToggled)
                {
                    _mapPanPosition.Height = localPlayerMapPos.Height;
                    return GetMapParameters(_mapPanPosition);
                }
                else
                {
                    _lastMousePosition = Point.Empty;
                    _mapPanPosition = localPlayerMapPos;
                    targetPanPosition = localPlayerMapPos.GetPoint();
                    return GetMapParameters(localPlayerMapPos);
                }
            }
            else
            {
                return GetMapParameters(_mapPanPosition);
            }
        }

        private static bool IsAggressorFacingTarget(SKPoint aggressor, float aggressorDegrees, SKPoint target, float distance)
        {
            double maxDiff = 31.3573 - 3.51726 * Math.Log(Math.Abs(0.626957 - 15.6948 * distance)); // Max degrees variance based on distance variable
            if (maxDiff < 1f)
                maxDiff = 1f; // Non linear equation, handle low/negative results

            var radians = Math.Atan2(target.Y - aggressor.Y, target.X - aggressor.X); // radians
            var degs = radians.ToDegrees();

            if (degs < 0)
                degs += 360f; // handle if negative

            var diff = Math.Abs(degs - aggressorDegrees); // Get angular difference (in degrees)
            return diff <= maxDiff; // See if calculated degrees is within max difference
        }

        private void DrawMap(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var localPlayerPos = localPlayer.Position;

            if (mcRadarMapSetup.Visible) // Print coordinates (to make it easy to setup JSON configs)
            {
                lblRadarMapSetup.Text = $"Map Setup - Unity X,Y,Z: {localPlayerPos.X}, {localPlayerPos.Y}, {localPlayerPos.Z}";
            }
            else if (lblRadarMapSetup.Text != "Map Setup" && !mcRadarMapSetup.Visible)
            {
                lblRadarMapSetup.Text = "Map Setup";
            }

            // Prepare to draw Game Map
            var mapParams = GetMapLocation();

            var mapCanvasBounds = new SKRect() // Drawing Destination
            {
                Left = _mapCanvas.Left,
                Right = _mapCanvas.Right,
                Top = _mapCanvas.Top,
                Bottom = _mapCanvas.Bottom
            };

            // Draw Game Map
            canvas.DrawBitmap(
                _loadedBitmaps[mapParams.MapLayerIndex],
                mapParams.Bounds,
                mapCanvasBounds,
                SKPaints.PaintBitmap
            );
        }

        private void DrawPlayers(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;

            if (this.InGame && localPlayer is not null)
            {
                var allPlayers = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.IsActive && x.IsAlive && !x.HasExfild); // Skip exfil'd players

                if (allPlayers is not null)
                {
                    var friendlies = allPlayers?.Where(x => x.IsFriendlyActive);
                    var localPlayerPos = localPlayer.Position;
                    var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);
                    var mouseOverGroup = _mouseOverGroup;
                    var mapParams = GetMapLocation();

                    foreach (var player in allPlayers) // Draw PMCs
                    {
                        var playerPos = player.Position;
                        var playerMapPos = playerPos.ToMapPos(_selectedMap);
                        var playerZoomedPos = playerMapPos.ToZoomedPos(mapParams);

                        player.ZoomedPosition = new Vector2() // Cache Position as Vec2 for MouseMove event
                        {
                            X = playerZoomedPos.X,
                            Y = playerZoomedPos.Y
                        };

                        int aimlineLength = 15;

                        if (player.Type is not PlayerType.Teammate && !player.IsLocalPlayer)
                        {
                            if (friendlies is not null)
                                foreach (var friendly in friendlies)
                                {
                                    var friendlyPos = friendly.Position;
                                    var friendlyDist = Vector3.Distance(playerPos, friendlyPos);

                                    if (friendlyDist > _config.MaxDistance)
                                        continue; // max range, no lines across entire map

                                    var friendlyMapPos = friendlyPos.ToMapPos(_selectedMap);

                                    if (IsAggressorFacingTarget(playerMapPos.GetPoint(), player.Rotation.X, friendlyMapPos.GetPoint(), friendlyDist))
                                    {
                                        aimlineLength = 1000; // Lengthen aimline
                                        break;
                                    }
                                }
                        }

                        // Draw Player
                        DrawPlayer(canvas, player, playerZoomedPos, aimlineLength, mouseOverGroup, localPlayerMapPos);
                    }
                }
            }
        }

        private void DrawPlayer(SKCanvas canvas, Player player, MapPosition playerZoomedPos, int aimlineLength, int? mouseOverGrp, MapPosition localPlayerMapPos)
        {
            if (this.InGame && this.LocalPlayer is not null)
            {
                var type = player.Type.ToString();

                if (player.IsPMC && player.Type is not PlayerType.Teammate && !player.IsLocalPlayer)
                    type = "PMC";

                var playerSettings = _config.PlayerInformationSettings[type];
                var height = playerZoomedPos.Height - localPlayerMapPos.Height;
                var dist = Vector3.Distance(this.LocalPlayer.Position, player.Position);
                var aimlineSettings = new AimlineSettings
                {
                    Enabled = playerSettings.Aimline,
                    Length = aimlineLength,
                    Opacity = playerSettings.AimlineOpacity
                };

                List<string> aboveLines = new List<string>();
                List<string> belowLines = new List<string>();
                List<string> rightLines = new List<string>();
                List<string> leftLines = new List<string>();

                if (playerSettings.Name)
                {
                    string name = player.ErrorCount > 10 ? "ERROR" : player.Name;
                    aboveLines.Add(name);
                }

                if (playerSettings.Height)
                    leftLines.Add($"{Math.Round(height)}");

                if (playerSettings.Distance)
                    belowLines.Add($"{Math.Round(dist)}");

                if (playerSettings.Flags)
                {
                    if (playerSettings.Health && player.Health != -1)
                        rightLines.Add(player.HealthStatus);

                    if (playerSettings.ActiveWeapon)
                        if (!string.IsNullOrEmpty(player.WeaponInfo.Name))
                            rightLines.Add(player.WeaponInfo.Name ?? "N/A");

                    if (playerSettings.AmmoType)
                        if (!string.IsNullOrEmpty(player.WeaponInfo.AmmoType))
                            rightLines.Add(player.WeaponInfo.AmmoType ?? "N/A");

                    if (playerSettings.Thermal)
                        if (player.HasThermal)
                            rightLines.Add("T");

                    if (playerSettings.NightVision)
                        if (player.HasNVG)
                            rightLines.Add("NVG");

                    if (playerSettings.Value)
                        rightLines.Add($"${TarkovDevManager.FormatNumber(player.Value)}");

                    if (playerSettings.Group && player.GroupID != -1)
                        rightLines.Add(player.GroupID.ToString());

                    if (playerSettings.Tag && !string.IsNullOrEmpty(player.Tag))
                        rightLines.Add("Example Tag");
                }

                if (aimlineSettings.Length == 15)
                    aimlineSettings.Length = playerSettings.AimlineLength;

                if (player.ErrorCount > 10)
                {
                    aboveLines.Clear();
                    belowLines.Clear();
                    rightLines.Clear();
                    leftLines.Clear();
                    belowLines.Add("ERROR");
                }

                playerZoomedPos.DrawPlayerText(
                    canvas,
                    player,
                    aboveLines.ToArray(),
                    belowLines.ToArray(),
                    rightLines.ToArray(),
                    leftLines.ToArray(),
                    mouseOverGrp
                );

                playerZoomedPos.DrawPlayerMarker(
                    canvas,
                    player,
                    aimlineSettings,
                    mouseOverGrp
                );
            }
        }

        private void DrawItemAnimations(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var mapParams = GetMapLocation();

                foreach (var animation in activeItemAnimations)
                {
                    var itemZoomedPos = animation.Item.Position
                                            .ToMapPos(_selectedMap)
                                            .ToZoomedPos(mapParams);

                    var animationTime = animation.AnimationTime % animation.MaxAnimationTime;
                    var maxRadius = _config.LootPing["Radius"];

                    var cycleScale = Lerp(0f, 1f, animationTime / (animation.MaxAnimationTime / 2f));
                    if (animationTime > animation.MaxAnimationTime / 2f)
                        cycleScale = Lerp(1f, 0f, (animationTime - animation.MaxAnimationTime / 2f) / (animation.MaxAnimationTime / 2f));

                    var radius = maxRadius * cycleScale;

                    using (var paint = new SKPaint())
                    {
                        paint.Color = Extensions.SKColorFromPaintColor("LootPing", (byte)(255 * cycleScale));
                        paint.Style = SKPaintStyle.Stroke;
                        paint.StrokeWidth = 3f;

                        canvas.DrawCircle(itemZoomedPos.X, itemZoomedPos.Y, radius, paint);
                    }
                }
            }
        }

        private void DrawLoot(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                if (_config.ProcessLoot) // Draw loot (if enabled)
                {
                    var loot = this.Loot;
                    if (loot is not null)
                    {
                        if (loot.Filter is null)
                            loot.ApplyFilter();

                        var filter = loot.Filter;

                        if (filter is not null)
                        {
                            var localPlayerMapPos = localPlayer.Position.ToMapPos(_selectedMap);
                            var mapParams = GetMapLocation();

                            // ghetto auto refresh but works
                            if (loot.HasCachedItems && loot.Loot?.Count != lastLootItemCount)
                            {
                                lastLootItemCount = loot.Loot.Count;

                                if (lstLootItems.Items.Count != lastLootItemCount)
                                    RefreshLootListItems();
                            }

                            foreach (var item in filter)
                            {
                                if (item is null || (this._config.ImportantLootOnly && !item.Important && !item.AlwaysShow))
                                    continue;

                                float position = item.Position.Z - localPlayerMapPos.Height;

                                var itemZoomedPos = item.Position
                                                        .ToMapPos(_selectedMap)
                                                        .ToZoomedPos(mapParams);

                                item.ZoomedPosition = new Vector2() // Cache Position as Vec2 for MouseMove event
                                {
                                    X = itemZoomedPos.X,
                                    Y = itemZoomedPos.Y
                                };

                                itemZoomedPos.DrawLootableObject(
                                    canvas,
                                    item,
                                    position
                                );
                            }
                        }
                    }
                }
            }
        }

        private void DrawQuestItems(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                if (_config.QuestHelper && !Memory.IsScav) // Draw quest items (if enabled)
                {
                    if (this.QuestManager is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(_selectedMap);
                        var mapParams = GetMapLocation();

                        var questItems = this.QuestManager.QuestItems;
                        if (_config.QuestItems && questItems is not null)
                        {
                            var items = _config.UnknownQuestItems ?
                                questItems.Where(x => x?.Position.X != 0 && x?.Name == "????") :
                                questItems.Where(x => x?.Position.X != 0 && x?.Name != "????");

                            foreach (var item in items)
                            {
                                if (item is null || item.Complete)
                                    continue;

                                float position = item.Position.Z - localPlayerMapPos.Height;
                                var itemZoomedPos = item.Position
                                                        .ToMapPos(_selectedMap)
                                                        .ToZoomedPos(mapParams);

                                item.ZoomedPosition = new Vector2() // Cache Position as Vec2 for MouseMove event
                                {
                                    X = itemZoomedPos.X,
                                    Y = itemZoomedPos.Y
                                };

                                itemZoomedPos.DrawQuestItem(
                                    canvas,
                                    item,
                                    position
                                );
                            }
                        }

                        var questZones = this.QuestManager.QuestZones;
                        if (_config.QuestLocations && questZones is not null)
                        {
                            foreach (var zone in questZones.Where(x => x.MapName.ToLower() == Memory.MapNameFormatted.ToLower() && !x.Complete))
                            {
                                float position = zone.Position.Z - localPlayerMapPos.Height;
                                var questZoneZoomedPos = zone.Position
                                                        .ToMapPos(_selectedMap)
                                                        .ToZoomedPos(mapParams);

                                zone.ZoomedPosition = new Vector2() // Cache Position as Vec2 for MouseMove event
                                {
                                    X = questZoneZoomedPos.X,
                                    Y = questZoneZoomedPos.Y
                                };

                                questZoneZoomedPos.DrawTaskZone(
                                    canvas,
                                    zone,
                                    position
                                );
                            }
                        }
                    }
                }
            }
        }

        private void DrawGrenades(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                var grenades = this.Grenades;
                if (grenades is not null)
                {
                    var mapParams = GetMapLocation();

                    foreach (var grenade in grenades)
                    {
                        var grenadeZoomedPos = grenade
                            .Position
                            .ToMapPos(_selectedMap)
                            .ToZoomedPos(mapParams);

                        grenadeZoomedPos.DrawGrenade(canvas);
                    }
                }
            }
        }

        private void DrawCorpses(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                var corpses = this.Corpses;
                if (corpses is not null)
                {
                    var mapParams = GetMapLocation();

                    foreach (var corpse in corpses)
                    {
                        var corpseZoomedPos = corpse
                            .Position
                            .ToMapPos(_selectedMap)
                            .ToZoomedPos(mapParams);

                        corpseZoomedPos.DrawDeathMarker(canvas);
                    }
                }
            }
        }

        private void DrawExfils(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                var exfils = this.Exfils;
                if (exfils is not null)
                {
                    var localPlayerMapPos = this.LocalPlayer.Position.ToMapPos(_selectedMap);
                    var mapParams = GetMapLocation();

                    foreach (var exfil in exfils)
                    {
                        var exfilZoomedPos = exfil
                            .Position
                            .ToMapPos(_selectedMap)
                            .ToZoomedPos(mapParams);

                        exfilZoomedPos.DrawExfil(
                            canvas,
                            exfil,
                            localPlayerMapPos.Height
                        );
                    }
                }
            }
        }

        private void DrawAimview(SKCanvas canvas)
        {
            if (_config.Aimview)
            {
                var aimviewPlayers = this.AllPlayers?
                    .Select(x => x.Value)
                    .Where(x => x.IsActive && x.IsAlive);

                if (aimviewPlayers is not null)
                {
                    var isItemListVisible = lstLootItems.Visible;
                    var localPlayerAimviewBoundsX = mcRadarLootItemViewer.Location.X;
                    var localPlayerAimviewBoundsY = mcRadarLootItemViewer.Location.Y - _aimviewWindowSize - 5;

                    var localPlayerAimviewBounds = new SKRect()
                    {
                        Left = (isItemListVisible ? localPlayerAimviewBoundsX : _mapCanvas.Left),
                        Right = (isItemListVisible ? localPlayerAimviewBoundsX : _mapCanvas.Left) + _aimviewWindowSize,
                        Bottom = (isItemListVisible ? localPlayerAimviewBoundsY + _aimviewWindowSize : _mapCanvas.Bottom),
                        Top = (isItemListVisible ? localPlayerAimviewBoundsY : _mapCanvas.Bottom - _aimviewWindowSize)
                    };

                    var isRadarStatsVisible = mcRadarStats.Visible || mcRadarEnemyStats.Visible;
                    var primaryTeammateAimviewBoundsX = mcRadarStats.Location.X - mcRadarStats.Width;
                    var primaryTeammateAimviewBoundsY = mcRadarStats.Location.Y - _aimviewWindowSize - 5;

                    var primaryTeammateAimviewBounds = new SKRect()
                    {
                        Left = (isRadarStatsVisible ? primaryTeammateAimviewBoundsX : _mapCanvas.Right - _aimviewWindowSize),
                        Right = (isRadarStatsVisible ? primaryTeammateAimviewBoundsX + _aimviewWindowSize : _mapCanvas.Right),
                        Bottom = (isRadarStatsVisible ? primaryTeammateAimviewBoundsY + _aimviewWindowSize : _mapCanvas.Bottom),
                        Top = (isRadarStatsVisible ? primaryTeammateAimviewBoundsY : _mapCanvas.Bottom - _aimviewWindowSize)
                    };

                    var primaryTeammate = this.AllPlayers?
                        .Select(x => x.Value)
                        .FirstOrDefault(x => x.AccountID == txtTeammateID.Text);

                    // Draw LocalPlayer Aimview
                    RenderAimview(
                        canvas,
                        localPlayerAimviewBounds,
                        this.LocalPlayer,
                        aimviewPlayers
                    );

                    // Draw Primary Teammate Aimview
                    RenderAimview(
                        canvas,
                        primaryTeammateAimviewBounds,
                        primaryTeammate,
                        aimviewPlayers
                    );
                }
            }
        }

        private void DrawToolTips(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var mapParams = GetMapLocation();

            if (localPlayer is not null)
            {
                if (_closestPlayerToMouse is not null)
                {
                    var playerZoomedPos = _closestPlayerToMouse
                        .Position
                        .ToMapPos(_selectedMap)
                        .ToZoomedPos(mapParams);
                    playerZoomedPos.DrawToolTip(canvas, _closestPlayerToMouse);
                }
            }

            if (_closestItemToMouse is not null)
            {
                var itemZoomedPos = _closestItemToMouse
                    .Position
                    .ToMapPos(_selectedMap)
                    .ToZoomedPos(mapParams);
                itemZoomedPos.DrawLootableObjectToolTip(canvas, _closestItemToMouse);
            }

            if (_closestTaskZoneToMouse is not null)
            {
                var taskZoneZoomedPos = _closestTaskZoneToMouse
                    .Position
                    .ToMapPos(_selectedMap)
                    .ToZoomedPos(mapParams);
                taskZoneZoomedPos.DrawToolTip(canvas, _closestTaskZoneToMouse);
            }

            if (_closestTaskItemToMouse is not null)
            {
                var taskItemZoomedPos = _closestTaskItemToMouse
                    .Position
                    .ToMapPos(_selectedMap)
                    .ToZoomedPos(mapParams);
                taskItemZoomedPos.DrawToolTip(canvas, _closestTaskItemToMouse);
            }
        }

        private void DrawStatusText(SKCanvas canvas)
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            bool isAtHideout = this.IsAtHideout;
            var localPlayer = this.LocalPlayer;
            var selectedMap = this._selectedMap;

            string statusText;
            if (!isReady)
            {
                statusText = "Game Process Not Running";
            }
            else if (isAtHideout)
            {
                statusText = "Main Menu or Hideout...";
            }
            else if (!inGame)
            {
                statusText = "Waiting for Raid Start...";

                if (selectedMap is not null)
                    ResetVariables();
            }
            else if (localPlayer is null)
            {
                statusText = "Cannot find LocalPlayer";
            }
            else if (selectedMap is null)
            {
                statusText = "Loading Map";
            }
            else
            {
                return; // No status text to draw
            }

            var centerX = _mapCanvas.Width / 2;
            var centerY = _mapCanvas.Height / 2;

            canvas.DrawText(statusText, centerX, centerY, SKPaints.TextRadarStatus);
        }

        private void RenderAimview(SKCanvas canvas, SKRect drawingLocation, Player sourcePlayer, IEnumerable<Player> aimviewPlayers)
        {
            if (sourcePlayer is null || !sourcePlayer.IsActive || !sourcePlayer.IsAlive)
                return;

            canvas.DrawRect(drawingLocation, SKPaints.PaintTransparentBacker); // draw backer

            var myPosition = sourcePlayer.Position;
            var myRotation = sourcePlayer.Rotation;
            var normalizedDirection = NormalizeDirection(myRotation.X);
            var pitch = CalculatePitch(myRotation.Y);

            DrawCrosshair(canvas, drawingLocation);

            if (aimviewPlayers is not null)
            {
                foreach (var player in aimviewPlayers)
                {
                    if (player == sourcePlayer)
                        continue; // don't draw self

                    if (ShouldDrawPlayer(myPosition, player.Position, _config.MaxDistance))
                        DrawPlayer(canvas, drawingLocation, myPosition, player, normalizedDirection, pitch);
                }
            }

            // Draw loot objects
            // requires rework for height difference
            //var loot = this.Loot; // cache ref
            //if (loot is not null && loot.Filter is not null)
            //{
            //    foreach (var item in loot.Filter)
            //    {
            //        if (ShouldDrawLootObject(myPosition, item.Position, _config.MaxDistance))
            //            DrawLootableObject(canvas, drawingLocation, myPosition, sourcePlayer.ZoomedPosition, item, normalizedDirection, pitch);
            //    }
            //}
        }

        private float NormalizeDirection(float direction)
        {
            var normalizedDirection = -direction;

            if (normalizedDirection < 0)
                normalizedDirection += 360;

            return normalizedDirection;
        }

        private bool IsInFOV(float drawX, float drawY, SKRect drawingLocation)
        {
            return drawX < drawingLocation.Right
                   && drawX > drawingLocation.Left
                   && drawY > drawingLocation.Top
                   && drawY < drawingLocation.Bottom;
        }

        private bool ShouldDrawPlayer(Vector3 myPosition, Vector3 playerPosition, float maxDistance)
        {
            var dist = Vector3.Distance(myPosition, playerPosition);
            return dist <= maxDistance;
        }

        private bool ShouldDrawLootObject(Vector3 myPosition, Vector3 lootPosition, float maxDistance)
        {
            var dist = Vector3.Distance(myPosition, lootPosition);
            return dist <= maxDistance;
        }

        private void HandleSplitPlanes(ref float angleX, float normalizedDirection)
        {
            if (angleX >= 360 - _config.AimViewFOV && normalizedDirection <= _config.AimViewFOV)
            {
                var diff = 360 + normalizedDirection;
                angleX -= diff;
            }
            else if (angleX <= _config.AimViewFOV && normalizedDirection >= 360 - _config.AimViewFOV)
            {
                var diff = 360 - normalizedDirection;
                angleX += diff;
            }
        }

        private float CalculatePitch(float pitch)
        {
            if (pitch >= 270)
                return 360 - pitch;
            else
                return -pitch;
        }

        private float CalculateAngleY(float heightDiff, float dist, float pitch)
        {
            return (float)(180 / Math.PI * Math.Atan(heightDiff / dist)) - pitch;
        }

        private float CalculateYPosition(float angleY, float windowSize)
        {
            return angleY / _config.AimViewFOV * windowSize + windowSize / 2;
        }

        private float CalculateAngleX(float opposite, float adjacent, float normalizedDirection)
        {
            float angleX = (float)(180 / Math.PI * Math.Atan(opposite / adjacent));

            if (adjacent < 0 && opposite > 0)
                angleX += 180;
            else if (adjacent < 0 && opposite < 0)
                angleX += 180;
            else if (adjacent > 0 && opposite < 0)
                angleX += 360;

            HandleSplitPlanes(ref angleX, normalizedDirection);

            angleX -= normalizedDirection;
            return angleX;
        }

        private float CalculateXPosition(float angleX, float windowSize)
        {
            return angleX / _config.AimViewFOV * windowSize + windowSize / 2;
        }

        private float CalculateCircleSize(float dist)
        {
            return (float)(31.6437 - 5.09664 * Math.Log(0.591394 * dist + 70.0756));
        }

        private void DrawCrosshair(SKCanvas canvas, SKRect drawingLocation)
        {
            canvas.DrawLine(
                drawingLocation.Left,
                drawingLocation.Bottom - (_aimviewWindowSize / 2),
                drawingLocation.Right,
                drawingLocation.Bottom - (_aimviewWindowSize / 2),
                SKPaints.PaintAimviewCrosshair
            );

            canvas.DrawLine(
                drawingLocation.Right - (_aimviewWindowSize / 2),
                drawingLocation.Top,
                drawingLocation.Right - (_aimviewWindowSize / 2),
                drawingLocation.Bottom,
                SKPaints.PaintAimviewCrosshair
            );
        }

        private void DrawPlayer(SKCanvas canvas, SKRect drawingLocation, Vector3 myPosition, Player player, float normalizedDirection, float pitch)
        {
            var playerPos = player.Position;
            float dist = Vector3.Distance(myPosition, playerPos);
            float heightDiff = playerPos.Z - myPosition.Z;
            float angleY = CalculateAngleY(heightDiff, dist, pitch);
            float y = CalculateYPosition(angleY, _aimviewWindowSize);

            float opposite = playerPos.Y - myPosition.Y;
            float adjacent = playerPos.X - myPosition.X;
            float angleX = CalculateAngleX(opposite, adjacent, normalizedDirection);
            float x = CalculateXPosition(angleX, _aimviewWindowSize);

            float drawX = drawingLocation.Right - x;
            float drawY = drawingLocation.Bottom - y;

            if (IsInFOV(drawX, drawY, drawingLocation))
            {
                float circleSize = CalculateCircleSize(dist);
                canvas.DrawCircle(drawX, drawY, circleSize * _uiScale, player.GetAimviewPaint());
            }
        }

        private SKPoint GetScreenPosition(SKCanvas canvas, SKRect drawingLocation, Vector3 myPosition, Vector3 bonePosition, float normalizedDirection, float pitch)
        {
            float dist = Vector3.Distance(myPosition, bonePosition);
            float heightDiff = bonePosition.Z - myPosition.Z;
            float angleY = CalculateAngleY(heightDiff, dist, pitch);
            float y = CalculateYPosition(angleY, _aimviewWindowSize);

            float opposite = bonePosition.Y - myPosition.Y;
            float adjacent = bonePosition.X - myPosition.X;
            float angleX = CalculateAngleX(opposite, adjacent, normalizedDirection);
            float x = CalculateXPosition(angleX, _aimviewWindowSize);

            float drawX = drawingLocation.Right - x;
            float drawY = drawingLocation.Bottom - y;

            return new SKPoint(drawX, drawY);
        }

        public Vector2 GetScreen(Vector3 playerPos, Vector3 localPos, Vector4 screen, Vector2 Angles, float fov)
        {
            float num = playerPos.Z - localPos.Z;
            float num2 = Vector3.Distance(localPos, playerPos);
            float num3 = playerPos.X - localPos.X;
            float num4 = playerPos.Y - localPos.Y;
            float num5 = (float)(57.29577951308232 * Math.Atan((double)(num4 / num3)));
            float num6 = (float)(57.29577951308232 * Math.Atan((double)(num / num2))) - Angles.Y;
            if (num3 < 0f && num4 > 0f)
            {
                num5 += 180f;
            }
            else if (num3 < 0f && num4 < 0f)
            {
                num5 += 180f;
            }
            else if (num3 > 0f && num4 < 0f)
            {
                num5 += 360f;
            }
            if (num5 >= 360f - fov && Angles.X <= fov)
            {
                float num7 = 360f + Angles.X;
                num5 -= num7;
            }
            else if (num5 <= fov && Angles.X >= 360f - fov)
            {
                float num8 = 360f - Angles.X;
                num5 += num8;
            }
            else
            {
                num5 -= Angles.X;
            }
            float num9 = num5 / fov * screen.Z + screen.Z / 2f;
            float num10 = num6 / fov * screen.W + screen.W / 2f;
            float num11 = screen.X + screen.Z - num9;
            float num12 = screen.Y + screen.W - num10;
            return new Vector2(num11, num12);
        }

        private void DrawLootableObject(SKCanvas canvas, SKRect drawingLocation, Vector3 myPosition, Vector2 myZoomedPos, LootableObject lootableObject, float normalizedDirection, float pitch)
        {
            var lootableObjectPos = lootableObject.Position;
            float dist = Vector3.Distance(myPosition, lootableObjectPos);
            float heightDiff = lootableObjectPos.Z - myPosition.Z;
            float angleY = CalculateAngleY(heightDiff, dist, pitch);
            float y = CalculateYPosition(angleY, _aimviewWindowSize);

            float opposite = lootableObjectPos.Y - myPosition.Y;
            float adjacent = lootableObjectPos.X - myPosition.X;
            float angleX = CalculateAngleX(opposite, adjacent, normalizedDirection);
            float x = CalculateXPosition(angleX, _aimviewWindowSize);

            float drawX = drawingLocation.Right - x;
            float drawY = drawingLocation.Bottom - y;

            if (IsInFOV(drawX, drawY, drawingLocation))
            {
                float circleSize = CalculateCircleSize(dist);
                canvas.DrawCircle(drawX, drawY, circleSize * _uiScale, SKPaints.LootPaint);
            }
        }

        private void ClearPlayerRefs()
        {
            _closestPlayerToMouse = null;
            _mouseOverGroup = null;
        }

        private void ClearItemRefs()
        {
            _closestItemToMouse = null;
        }

        private void ClearTaskItemRefs()
        {
            _closestTaskItemToMouse = null;
        }

        private void ClearTaskZoneRefs()
        {
            _closestTaskZoneToMouse = null;
        }

        private T FindClosestObject<T>(IEnumerable<T> objects, Vector2 position, Func<T, Vector2> positionSelector, float threshold)
            where T : class
        {
            if (objects is null || !objects.Any())
                return null;

            var closestObject = objects.Aggregate(
                (x1, x2) =>
                    x2 == null || Vector2.Distance(positionSelector(x1), position)
                    < Vector2.Distance(positionSelector(x2), position)
                        ? x1
                        : x2
            );

            if (closestObject is not null && Vector2.Distance(positionSelector(closestObject), position) < threshold)
                return closestObject;

            return null;
        }

        private void PanTimer_Tick(object sender, EventArgs e)
        {
            var panDifference = new SKPoint(
                this.targetPanPosition.X - this._mapPanPosition.X,
                this.targetPanPosition.Y - this._mapPanPosition.Y
            );

            if (panDifference.Length > 0.1)
            {
                this._mapPanPosition.X += (float)(panDifference.X * PAN_SMOOTHNESS);
                this._mapPanPosition.Y += (float)(panDifference.Y * PAN_SMOOTHNESS);
            }
            else
            {
                this.panTimer.Stop();
            }
        }

        private void ZoomTimer_Tick(object sender, EventArgs e)
        {
            int zoomDifference = this.targetZoomValue - _config.DefaultZoom;

            if (zoomDifference != 0)
            {
                int zoomStep = Math.Sign(zoomDifference);
                _config.DefaultZoom += zoomStep;
            }
            else
            {
                this.zoomTimer.Stop();
            }
        }

        private bool ZoomIn(int amt)
        {
            this.targetZoomValue = Math.Max(10, _config.DefaultZoom - amt);
            this.zoomTimer.Start();

            return true;
        }

        private bool ZoomOut(int amt)
        {
            this.targetZoomValue = Math.Min(200, _config.DefaultZoom + amt);
            this.zoomTimer.Start();

            return false;
        }

        private float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            var shouldStopAnimation = true;

            foreach (var animation in activeItemAnimations)
            {
                animation.AnimationTime += 0.016f;

                if (animation.AnimationTime >= animation.MaxAnimationTime)
                {
                    animation.AnimationTime = 0f;
                    animation.RepetitionCount++;
                }

                if (animation.RepetitionCount < animation.MaxRepetitions)
                    shouldStopAnimation = false;
            }

            if (shouldStopAnimation)
            {
                itemAnimationTimer.Stop();
                itemAnimationTimer.Dispose();
                isAnimationRunning = false;
            }

            activeItemAnimations.RemoveAll(animation => animation.RepetitionCount >= animation.MaxRepetitions);

            _mapCanvas.Invalidate();
        }

        private List<LootItem> GetLootAndContainerItems()
        {
            var lootItems = new List<LootItem>();

            if (this.Loot?.Loot is null)
                return lootItems;

            lootItems.AddRange(this.Loot.Loot.OfType<LootItem>());

            foreach (var container in this.Loot.Loot.OfType<LootContainer>())
            {
                lootItems.AddRange(container.Items);
            }

            foreach (var corpse in this.Loot.Loot.OfType<LootCorpse>())
            {
                foreach (var gearItem in corpse.Items)
                {
                    var itemToAdd = gearItem.Item;
                    var itemsToAdd = gearItem.Loot;

                    if (itemToAdd?.Item is not null)
                        lootItems.Add(itemToAdd);


                    if (itemsToAdd.Count > 0)
                        lootItems.AddRange(itemsToAdd);
                }
            }

            return lootItems;
        }

        private void RefreshLootListItems()
        {
            if (_isRefreshingLootItems)
                return;

            _isRefreshingLootItems = true;

            if (this.Loot?.Loot?.Count < 1 || !_config.LootItemViewer)
            {
                _isRefreshingLootItems = false;
                return;
            }

            lstLootItems.Items.Clear();

            var lootItems = this.GetLootAndContainerItems();

            if (lootItems.Count < 1)
            {
                _isRefreshingLootItems = false;
                return;
            }

            var itemToFind = txtLootItemFilter.Text.Trim();

            var mergedLootItems = lootItems
                 .GroupBy(lootItem => lootItem.ID)
                 .Select(group =>
                 {
                     var count = group.Count();
                     var firstItem = group.First();

                     return new
                     {
                         LootItem = new LootItem
                         {
                             ID = firstItem.ID,
                             Name = firstItem.Name,
                             Position = firstItem.Position,
                             Important = firstItem.Important,
                             AlwaysShow = firstItem.AlwaysShow,
                             Value = firstItem.Value,
                             Color = firstItem.Color
                         },
                         Quantity = count
                     };
                 })
                 .OrderByDescending(x => x.LootItem.Value)
                 .ToList();

            if (!string.IsNullOrEmpty(itemToFind))
            {
                mergedLootItems = mergedLootItems
                    .Where(item => item.LootItem.Name.IndexOf(itemToFind, StringComparison.OrdinalIgnoreCase) != -1)
                    .ToList();
            }

            if (mergedLootItems.Count < 1)
            {
                _isRefreshingLootItems = false;
                return;
            }

            var listViewItems = mergedLootItems.Select(item => new ListViewItem
            {
                Text = item.Quantity.ToString(),
                Tag = item.LootItem,
                SubItems =
                {
                    item.LootItem.Name,
                    TarkovDevManager.FormatNumber(item.LootItem.Value)
                }
            }).ToArray();

            lstLootItems.Items.AddRange(listViewItems);

            if (itemToPing is not null)
            {
                int itemIndex = lstLootItems.Items.Cast<ListViewItem>()
                                                  .ToList()
                                                  .FindIndex(item => item.SubItems[1].Text == itemToPing.Name);

                if (itemIndex != -1)
                {
                    lstLootItems.SelectedIndices.Clear();
                    lstLootItems.SelectedIndices.Add(itemIndex);
                }
            }

            _isRefreshingLootItems = false;
        }
        #endregion

        #region Event Handlers
        private void btnToggleMapFree_Click(object sender, EventArgs e)
        {
            if (_isFreeMapToggled)
            {
                btnToggleMapFree.Icon = Resources.tick;
                _isFreeMapToggled = false;

                lock (_renderLock)
                {
                    var localPlayer = this.LocalPlayer;
                    if (localPlayer is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(_selectedMap);
                        _mapPanPosition = new MapPosition()
                        {
                            X = localPlayerMapPos.X,
                            Y = localPlayerMapPos.Y,
                            Height = localPlayerMapPos.Height
                        };
                    }
                }
            }
            else
            {
                btnToggleMapFree.Icon = Resources.cross;
                _isFreeMapToggled = true;
            }
        }

        private void btnMapSetupApply_Click(object sender, EventArgs e)
        {
            if (float.TryParse(txtMapSetupX.Text, out float x)
                && float.TryParse(txtMapSetupY.Text, out float y)
                && float.TryParse(txtMapSetupScale.Text, out float scale))
            {
                lock (_renderLock)
                {
                    if (_selectedMap is not null)
                    {
                        _selectedMap.ConfigFile.X = x;
                        _selectedMap.ConfigFile.Y = y;
                        _selectedMap.ConfigFile.Scale = scale;
                        _selectedMap.ConfigFile.Save(_selectedMap);
                    }
                }
            }
            else
                ShowErrorDialog("Invalid value(s) provided in the map setup textboxes.");
        }

        private void skMapCanvas_MouseMovePlayer(object sender, MouseEventArgs e)
        {
            if (this.InGame && this.LocalPlayer is not null) // Must be in-game
            {
                var mouse = new Vector2(e.X, e.Y);

                var players = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.Type is not PlayerType.LocalPlayer && !x.HasExfild); // Get all players except LocalPlayer & Exfil'd Players

                var loot = this.Loot?.Filter?.Select(x => x);
                var tasksItems = this.QuestManager?.QuestItems?.Select(x => x);
                var tasksZones = this.QuestManager?.QuestZones?.Select(x => x);

                _closestPlayerToMouse = FindClosestObject(players, mouse, x => x.ZoomedPosition, 12 * _uiScale);
                if (_closestPlayerToMouse is not null)
                {
                    if (_closestPlayerToMouse.IsHumanHostile && _closestPlayerToMouse.GroupID != -1)
                        _mouseOverGroup = _closestPlayerToMouse.GroupID;
                    else
                        _mouseOverGroup = null;
                }
                else
                    ClearPlayerRefs();

                if (_config.ProcessLoot)
                    _closestItemToMouse = FindClosestObject(loot, mouse, x => x.ZoomedPosition, 12 * _uiScale);
                else
                    ClearItemRefs();

                _closestTaskItemToMouse = FindClosestObject(tasksItems, mouse, x => x.ZoomedPosition, 12 * _uiScale);
                if (_closestTaskItemToMouse == null)
                    ClearTaskItemRefs();

                _closestTaskZoneToMouse = FindClosestObject(tasksZones, mouse, x => x.ZoomedPosition, 12);
                if (_closestTaskZoneToMouse == null)
                    ClearTaskZoneRefs();

                if (this._isDragging && this._isFreeMapToggled)
                {
                    if (!this._lastMousePosition.IsEmpty)
                    {
                        int dx = e.X - this._lastMousePosition.X;
                        int dy = e.Y - this._lastMousePosition.Y;

                        dx = (int)(dx * DRAG_SENSITIVITY);
                        dy = (int)(dy * DRAG_SENSITIVITY);

                        this.targetPanPosition.X -= dx;
                        this.targetPanPosition.Y -= dy;

                        if (!this.panTimer.Enabled)
                            this.panTimer.Start();
                    }

                    this._lastMousePosition = e.Location;
                }
            }
            else if (this.InGame && Memory.LocalPlayer is null)
            {
                ClearPlayerRefs();
                ClearItemRefs();
                ClearTaskItemRefs();
                ClearTaskZoneRefs();
            }
        }

        private void skMapCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this._isFreeMapToggled)
            {
                this._isDragging = true;
                this._lastMousePosition = e.Location;
            }
        }

        private void skMapCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (this._isDragging)
            {
                this._isDragging = false;
                this._lastMousePosition = e.Location;
            }
        }

        private void skMapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            try
            {
                SKCanvas canvas = e.Surface.Canvas;
                canvas.Clear();

                UpdateWindowTitle();

                if (IsReadyToRender())
                {
                    lock (_renderLock)
                    {
                        DrawMap(canvas);

                        if (!_config.ProcessLoot && _config.LootCorpses)
                            DrawCorpses(canvas);

                        if (_config.ProcessLoot && (_config.LooseLoot ||
                            _config.LootCorpses ||
                            _config.LootContainerSettings["Enabled"] ||
                            (_config.QuestHelper && _config.QuestLootItems)))
                        {
                            DrawItemAnimations(canvas);
                            DrawLoot(canvas);
                        }

                        if (_config.QuestHelper)
                            DrawQuestItems(canvas);

                        DrawGrenades(canvas);

                        DrawExfils(canvas);

                        DrawPlayers(canvas);

                        if (_config.Aimview)
                            DrawAimview(canvas);

                        DrawToolTips(canvas);
                    }
                }
                else
                    DrawStatusText(canvas);

                canvas.Flush();
            }
            catch { }
        }

        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            ToggleMap();
        }

        private void swRadarStats_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarStats.Checked;
            _config.RadarStats = enabled;
            mcRadarStats.Visible = enabled;
        }

        private void btnPingSelectedItem_Click(object sender, EventArgs e)
        {
            if (itemToPing is null)
                return;

            var lootItems = this.GetLootAndContainerItems();
            var itemsToPing = lootItems.Where(item => item.ID == itemToPing.ID).ToList();

            activeItemAnimations.Clear();

            foreach (var item in itemsToPing)
            {
                activeItemAnimations.Add(new ItemAnimation(item)
                {
                    MaxAnimationTime = ((float)_config.LootPing["AnimationSpeed"] / 1000),
                    MaxRepetitions = _config.LootPing["Repetition"]
                });
            }

            isAnimationRunning = false;
            itemAnimationTimer?.Stop();
            itemAnimationTimer?.Dispose();

            itemAnimationTimer = new System.Windows.Forms.Timer();
            itemAnimationTimer.Interval = 16;
            itemAnimationTimer.Tick += AnimationTimer_Tick;
            itemAnimationTimer.Start();
            isAnimationRunning = true;
        }

        private void txtLootItemFilter_TextChanged(object sender, EventArgs e)
        {
            this.RefreshLootListItems();
        }

        private void btnToggleLootItemViewer_Click(object sender, EventArgs e)
        {
            mcRadarLootItemViewer.Visible = _config.LootItemViewer = !_config.LootItemViewer;
            if (_config.LootItemViewer)
                this.RefreshLootListItems();
        }

        private void lstLootItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = lstLootItems.SelectedItems.Count > 0 ? lstLootItems.SelectedItems[0]?.Tag as LootItem : null;

            itemToPing = selectedItem;
        }
        #endregion
        #endregion

        #region Settings
        #region General
        #region Helper Functions
        private void UpdatePvEControls()
        {
            var pveMode = Memory.IsPvEMode;
            var maxLTWDistance = (pveMode ? 250 : 40);
            var maxReachDistance = (pveMode ? 250 : 40);
            var LTWDistance = (pveMode ? _config.LootThroughWallsDistancePvE : _config.LootThroughWallsDistance) * 10;
            var reachDistance = (pveMode ? _config.ExtendedReachDistancePvE : _config.ExtendedReachDistance) * 10;

            btnTriggerUnityCrash.Visible = pveMode;

            sldrLootThroughWallsDistance.RangeMax = maxLTWDistance;
            sldrLootThroughWallsDistance.ValueMax = maxLTWDistance;

            sldrExtendedReachDistance.RangeMax = maxReachDistance;
            sldrExtendedReachDistance.ValueMax = maxReachDistance;

            sldrLootThroughWallsDistance.Value = (int)LTWDistance;
            sldrExtendedReachDistance.Value = (int)reachDistance;

            lblSettingsMemoryWritingLootThroughWallsDistance.Enabled = _config.LootThroughWalls;
            lblSettingsMemoryWritingLootThroughWallsDistance.Text = $"x{(LTWDistance / 10)}";

            lblSettingsMemoryWritingExtendedReachDistance.Enabled = _config.ExtendedReach;
            lblSettingsMemoryWritingExtendedReachDistance.Text = $"x{(reachDistance / 10)}";

            if (Memory.LocalPlayer is not null)
            {
                if (Memory.Toolbox is not null)
                    Memory.Toolbox.UpdateExtendedReachDistance = true;

                if (Memory.PlayerManager is not null)
                    Memory.PlayerManager.UpdateLootThroughWallsDistance = true;
            }
        }

        private string GetActivePlayerType()
        {
            return cboPlayerInfoType.SelectedItem?.ToString()?.Replace(" ", "");
        }

        private void UpdatePlayerTextFont(PlayerInformationSettings playerInfoSettings)
        {
            var playerType = this.GetActivePlayerType();
            var playerText = Extensions.PlayerTypeTextPaints[playerType];
            playerText.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[playerInfoSettings.Font]);

            Extensions.PlayerTypeTextPaints[playerType] = playerText;
        }

        private void UpdatePlayerTextSize(PlayerInformationSettings playerInfoSettings)
        {
            var playerType = this.GetActivePlayerType();
            var playerText = Extensions.PlayerTypeTextPaints[playerType];
            playerText.TextSize = playerInfoSettings.FontSize * _uiScale;

            Extensions.PlayerTypeTextPaints[playerType] = playerText;
        }

        private void UpdatePlayerFlagTextFont(PlayerInformationSettings playerInfoSettings)
        {
            var playerType = this.GetActivePlayerType();
            var playerText = Extensions.PlayerTypeFlagTextPaints[playerType];
            playerText.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[playerInfoSettings.FlagsFont]);

            Extensions.PlayerTypeFlagTextPaints[playerType] = playerText;
        }

        private void UpdatePlayerFlagTextSize(PlayerInformationSettings playerInfoSettings)
        {
            var playerType = this.GetActivePlayerType();
            var playerText = Extensions.PlayerTypeFlagTextPaints[playerType];
            playerText.TextSize = playerInfoSettings.FlagsFontSize * _uiScale;

            Extensions.PlayerTypeFlagTextPaints[playerType] = playerText;
        }

        private void SetupFonts()
        {
            cboGlobalFont.Items.Clear();
            cboPlayerInfoFont.Items.Clear();
            cboPlayerInfoFlagsFont.Items.Clear();

            foreach (string font in FONTS_TO_USE)
            {
                cboGlobalFont.Items.Add(font);
                cboPlayerInfoFont.Items.Add(font);
                cboPlayerInfoFlagsFont.Items.Add(font);
            }

            foreach (var playerSetting in _config.PlayerInformationSettings)
            {
                var settings = playerSetting.Value;
                var textPaint = SKPaints.TextBase.Clone();
                textPaint.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[settings.Font]);
                textPaint.TextSize = settings.FontSize * _uiScale;

                var flagsTextPaint = SKPaints.TextBase.Clone();
                flagsTextPaint.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[settings.FlagsFont]);
                flagsTextPaint.TextSize = settings.FlagsFontSize * _uiScale;

                Extensions.PlayerTypeTextPaints.Add(playerSetting.Key, textPaint);
                Extensions.PlayerTypeFlagTextPaints.Add(playerSetting.Key, flagsTextPaint);
            }
        }

        private PlayerInformationSettings GetPlayerInfoSettings()
        {
            var playerType = this.GetActivePlayerType();
            return !string.IsNullOrEmpty(playerType) && _config.PlayerInformationSettings.TryGetValue(playerType, out var settings) ? settings : null;
        }

        private bool TryGetPlayerInfoSettings(out PlayerInformationSettings settings)
        {
            settings = this.GetPlayerInfoSettings();
            return settings != null;
        }

        private void UpdatePlayerInformationSettings()
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            var selectedType = cboPlayerInfoType.Text;

            if (selectedType.Equals("LocalPlayer", StringComparison.OrdinalIgnoreCase) ||
                selectedType.Equals("Teammate", StringComparison.OrdinalIgnoreCase))
            {
                sldrPlayerInfoAimlineLength.RangeMax = 500;
                sldrPlayerInfoAimlineLength.ValueMax = 500;
            }
            else if (sldrPlayerInfoAimlineLength.RangeMax == 500)
            {
                sldrPlayerInfoAimlineLength.RangeMax = 60;
                sldrPlayerInfoAimlineLength.ValueMax = 60;
            }

            swPlayerInfoName.Checked = playerInfoSettings.Name;
            swPlayerInfoHeight.Checked = playerInfoSettings.Height;
            swPlayerInfoDistance.Checked = playerInfoSettings.Distance;

            swPlayerInfoAimline.Checked = playerInfoSettings.Aimline;
            sldrPlayerInfoAimlineLength.Value = playerInfoSettings.AimlineLength;
            sldrPlayerInfoAimlineOpacity.Value = playerInfoSettings.AimlineOpacity;
            sldrPlayerInfoAimlineLength.Enabled = playerInfoSettings.Aimline;
            sldrPlayerInfoAimlineOpacity.Enabled = playerInfoSettings.Aimline;

            cboPlayerInfoFont.SelectedIndex = playerInfoSettings.Font;
            sldrPlayerInfoFontSize.Value = playerInfoSettings.FontSize;

            var flagsChecked = playerInfoSettings.Flags;
            swPlayerInfoFlags.Checked = flagsChecked;
            swPlayerInfoActiveWeapon.Checked = playerInfoSettings.ActiveWeapon;
            swPlayerInfoThermal.Checked = playerInfoSettings.Thermal;
            swPlayerInfoNightVision.Checked = playerInfoSettings.NightVision;
            swPlayerInfoAmmoType.Checked = playerInfoSettings.AmmoType;
            swPlayerInfoGroup.Checked = playerInfoSettings.Group;
            swPlayerInfoValue.Checked = playerInfoSettings.Value;
            swPlayerInfoHealth.Checked = playerInfoSettings.Health;
            swPlayerInfoTag.Checked = playerInfoSettings.Tag;

            swPlayerInfoActiveWeapon.Enabled = flagsChecked;
            swPlayerInfoThermal.Enabled = flagsChecked;
            swPlayerInfoNightVision.Enabled = flagsChecked;
            swPlayerInfoAmmoType.Enabled = flagsChecked;
            swPlayerInfoGroup.Enabled = flagsChecked;
            swPlayerInfoValue.Enabled = flagsChecked;
            swPlayerInfoHealth.Enabled = flagsChecked;
            swPlayerInfoTag.Enabled = flagsChecked;

            cboPlayerInfoFlagsFont.SelectedIndex = playerInfoSettings.FlagsFont;
            sldrPlayerInfoFlagsFontSize.Value = playerInfoSettings.FlagsFontSize;

            cboPlayerInfoFlagsFont.Enabled = flagsChecked;
            sldrPlayerInfoFlagsFontSize.Enabled = flagsChecked;
        }
        #endregion
        #region Event Handlers
        private void swMapHelper_CheckedChanged(object sender, EventArgs e)
        {
            if (swMapHelper.Checked)
            {
                mcRadarMapSetup.Visible = true;
                txtMapSetupX.Text = _selectedMap?.ConfigFile.X.ToString() ?? "0";
                txtMapSetupY.Text = _selectedMap?.ConfigFile.Y.ToString() ?? "0";
                txtMapSetupScale.Text = _selectedMap?.ConfigFile.Scale.ToString() ?? "0";
            }
            else
                mcRadarMapSetup.Visible = false;
        }

        private void swAimview_CheckedChanged(object sender, EventArgs e)
        {
            _config.Aimview = swAimview.Checked;
        }

        private void swExfilNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.ExfilNames = swExfilNames.Checked;
        }

        private void swHoverArmor_CheckedChanged(object sender, EventArgs e)
        {
            _config.HoverArmor = swHoverArmor.Checked;
        }

        private void sldrUIScale_onValueChanged(object sender, int newValue)
        {
            _config.UIScale = newValue;
            _uiScale = (.01f * newValue);

            InitiateUIScaling();
        }

        private void btnRestartRadar_Click(object sender, EventArgs e)
        {
            Memory.Restart();
        }

        private void swRadarVsync_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarVsync.Checked;
            _config.VSync = enabled;

            if (_mapCanvas is not null)
                _mapCanvas.VSync = enabled;
        }

        private void swPvEMode_CheckedChanged(object sender, EventArgs e)
        {
            _config.PvEMode = swPvEMode.Checked;

            UpdatePvEControls();
        }

        private void swRadarEnemyCount_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarEnemyCount.Checked;

            _config.EnemyCount = enabled;
            mcRadarEnemyStats.Visible = enabled;
        }

        private void cboFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            _config.GlobalFont = cboGlobalFont.SelectedIndex;

            InitiateFonts();
        }

        private void sldrFontSize_onValueChanged(object sender, int newValue)
        {
            _config.GlobalFontSize = newValue;

            InitiateFontSizes();
        }

        private void sldrZoomSensitivity_onValueChanged(object sender, int newValue)
        {
            _config.ZoomSensitivity = newValue;
        }

        private void cboPlayerInfoType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            this.UpdatePlayerInformationSettings();
        }

        private void swPlayerInfoName_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Name = swPlayerInfoName.Checked;
        }

        private void swPlayerInfoHeight_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Height = swPlayerInfoHeight.Checked;
        }

        private void swPlayerInfoDistance_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Distance = swPlayerInfoDistance.Checked;
        }

        private void swPlayerInfoAimline_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            var aimlineChecked = swPlayerInfoAimline.Checked;

            playerInfoSettings.Aimline = aimlineChecked;

            sldrPlayerInfoAimlineLength.Enabled = aimlineChecked;
            sldrPlayerInfoAimlineOpacity.Enabled = aimlineChecked;
        }

        private void sldrPlayerInfoAimlineLength_onValueChanged(object sender, int newValue)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            if (newValue < sldrPlayerInfoAimlineLength.RangeMin)
                newValue = sldrPlayerInfoAimlineLength.RangeMin;

            playerInfoSettings.AimlineLength = newValue;
        }

        private void sldrPlayerInfoAimlineOpacity_onValueChanged(object sender, int newValue)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            if (newValue < sldrPlayerInfoAimlineOpacity.RangeMin)
                newValue = sldrPlayerInfoAimlineOpacity.RangeMin;

            playerInfoSettings.AimlineOpacity = newValue;
        }

        private void cboPlayerInfoFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Font = cboPlayerInfoFont.SelectedIndex;

            this.UpdatePlayerTextFont(playerInfoSettings);
        }

        private void sldrPlayerInfoFontSize_onValueChanged(object sender, int newValue)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.FontSize = newValue;

            this.UpdatePlayerTextSize(playerInfoSettings);
        }

        private void swPlayerInfoFlags_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            var flagsChecked = swPlayerInfoFlags.Checked;

            playerInfoSettings.Flags = flagsChecked;
            swPlayerInfoActiveWeapon.Enabled = flagsChecked;
            swPlayerInfoThermal.Enabled = flagsChecked;
            swPlayerInfoNightVision.Enabled = flagsChecked;
            swPlayerInfoAmmoType.Enabled = flagsChecked;
            swPlayerInfoGroup.Enabled = flagsChecked;
            swPlayerInfoValue.Enabled = flagsChecked;
            swPlayerInfoHealth.Enabled = flagsChecked;
            swPlayerInfoTag.Enabled = flagsChecked;

            cboPlayerInfoFlagsFont.Enabled = flagsChecked;
            sldrPlayerInfoFlagsFontSize.Enabled = flagsChecked;
        }

        private void swPlayerInfoActiveWeapon_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.ActiveWeapon = swPlayerInfoActiveWeapon.Checked;
        }

        private void swPlayerInfoThermal_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Thermal = swPlayerInfoThermal.Checked;
        }

        private void swPlayerInfoNightVision_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.NightVision = swPlayerInfoNightVision.Checked;
        }

        private void swPlayerInfoAmmoType_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.AmmoType = swPlayerInfoAmmoType.Checked;
        }

        private void swPlayerInfoGroup_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Group = swPlayerInfoGroup.Checked;
        }

        private void swPlayerInfoValue_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Value = swPlayerInfoValue.Checked;
        }

        private void swPlayerInfoHealth_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Health = swPlayerInfoHealth.Checked;
        }

        private void swPlayerInfoTag_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Tag = swPlayerInfoTag.Checked;
        }

        private void cboPlayerInfoFlagsFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.FlagsFont = cboPlayerInfoFlagsFont.SelectedIndex;

            this.UpdatePlayerFlagTextFont(playerInfoSettings);
        }

        private void sldrPlayerInfoFlagsFontSize_onValueChanged(object sender, int newValue)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.FlagsFontSize = newValue;

            this.UpdatePlayerFlagTextSize(playerInfoSettings);
        }

        private void btnTriggerUnityCrash_Click(object sender, EventArgs e)
        {
            if (Memory.IsOfflinePvE && Memory.InGame && Memory.LocalPlayer is not null)
                Memory.Chams.TriggerUnityCrash(Memory.LocalPlayer, 100UL);
        }
        #endregion
        #endregion

        #region Memory Writing
        #region Helper Functions
        private ThermalSettings GetSelectedThermalSetting()
        {
            return cboThermalType.SelectedItem?.ToString() == "Main" ? _config.MainThermalSetting : _config.OpticThermalSetting;
        }

        private void RefreshChams()
        {
            Memory.Chams?.ChamsDisable();
            Memory.Chams?.ChamsEnable();
        }
        #endregion
        #region Event Handlers
        private void swThirdperson_CheckedChanged(object sender, EventArgs e)
        {
            _config.Thirdperson = swThirdperson.Checked;
        }

        private void swFreezeTime_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swFreezeTime.Checked;
            _config.FreezeTimeOfDay = enabled;

            sldrTimeOfDay.Enabled = enabled;
        }

        private void sldrTimeOfDay_onValueChanged(object sender, int newValue)
        {
            _config.TimeOfDay = (float)sldrTimeOfDay.Value;
        }

        private void swTimeScale_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swTimeScale.Checked;
            _config.TimeScale = enabled;
            sldrTimeScaleFactor.Enabled = enabled;

            lblSettingsMemoryWritingTimeScaleFactor.Enabled = enabled;
        }

        private void sldrTimeScaleFactor_onValueChanged(object sender, int newValue)
        {
            if (newValue < 10)
                newValue = 10;
            else if (newValue > 18)
                newValue = 18;

            _config.TimeScaleFactor = (float)newValue / 10;
            lblSettingsMemoryWritingTimeScaleFactor.Text = $"x{(_config.TimeScaleFactor)}";
        }

        private void swLootThroughWalls_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swLootThroughWalls.Checked;
            _config.LootThroughWalls = enabled;

            sldrLootThroughWallsDistance.Enabled = enabled;
            lblSettingsMemoryWritingLootThroughWallsDistance.Enabled = enabled;
        }

        private void sldrLootThroughWallsDistance_onValueChanged(object sender, int newValue)
        {
            var pveMode = _config.PvEMode;
            var distance = (float)newValue / 10;

            if (pveMode)
                _config.LootThroughWallsDistancePvE = distance;
            else
            {
                if (distance > 3)
                    distance = 3;

                _config.LootThroughWallsDistance = distance;
            }

            lblSettingsMemoryWritingLootThroughWallsDistance.Text = $"x{distance}";

            if (Memory.LocalPlayer is not null)
                Memory.PlayerManager.UpdateLootThroughWallsDistance = true;
        }

        private void swExtendedReach_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swExtendedReach.Checked;
            _config.ExtendedReach = enabled;

            sldrExtendedReachDistance.Enabled = enabled;
            lblSettingsMemoryWritingExtendedReachDistance.Enabled = enabled;
        }

        private void sldrExtendedReachDistance_onValueChanged(object sender, int newValue)
        {
            var pveMode = _config.PvEMode;
            var distance = (float)newValue / 10;

            if (pveMode)
                _config.ExtendedReachDistancePvE = distance;
            else
            {
                if (distance > 4f)
                    distance = 4f;

                _config.ExtendedReachDistance = distance;
            }

            lblSettingsMemoryWritingExtendedReachDistance.Text = $"x{distance}";

            if (Memory.LocalPlayer is not null)
                Memory.Toolbox.UpdateExtendedReachDistance = true;
        }

        private void swNoRecoilSway_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoRecoilSway = swNoRecoilSway.Checked;
        }

        private void swInstantADS_CheckedChanged(object sender, EventArgs e)
        {
            _config.InstantADS = swInstantADS.Checked;
        }

        private void swNoVisor_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoVisor = swNoVisor.Checked;
        }

        private void swThermalVision_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swThermalVision.Checked;
            _config.ThermalVision = enabled;

            mcSettingsMemoryWritingThermal.Enabled = enabled || _config.OpticThermalVision;
        }

        private void swOpticalThermal_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swOpticalThermal.Checked;
            _config.OpticThermalVision = enabled;

            mcSettingsMemoryWritingThermal.Enabled = enabled || _config.ThermalVision;
        }

        private void swNightVision_CheckedChanged(object sender, EventArgs e)
        {
            _config.NightVision = swNightVision.Checked;
        }

        private void swNoWeaponMalfunctions_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoWeaponMalfunctions = swNoWeaponMalfunctions.Checked;
        }

        private void sldrMagDrillsSpeed_onValueChanged(object sender, int newValue)
        {
            _config.MagDrillSpeed = newValue;

            if (_config.MaxSkills["Mag Drills"] && Memory.LocalPlayer is not null)
            {
                var loadSpeedSkill = Memory.PlayerManager.Skills["MagDrills"]["LoadSpeed"];
                loadSpeedSkill.MaxValue = (float)newValue;

                var unloadSpeedSkill = Memory.PlayerManager.Skills["MagDrills"]["UnloadSpeed"];
                unloadSpeedSkill.MaxValue = (float)newValue;

                Memory.PlayerManager?.SetMaxSkill(loadSpeedSkill);
                Memory.PlayerManager?.SetMaxSkill(unloadSpeedSkill);
            }
        }

        private void swInfiniteStamina_CheckedChanged(object sender, EventArgs e)
        {
            _config.InfiniteStamina = swInfiniteStamina.Checked;
        }

        private void sldrThrowStrength_onValueChanged(object sender, int newValue)
        {
            _config.ThrowPowerStrength = newValue;

            if (_config.MaxSkills["Strength"] && Memory.LocalPlayer is not null)
            {
                var throwDistanceSkill = Memory.PlayerManager.Skills["Strength"]["BuffThrowDistanceInc"];
                throwDistanceSkill.MaxValue = (float)newValue / 100;

                Memory.PlayerManager?.SetMaxSkill(throwDistanceSkill);
            }
        }

        private void cboThermalType_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();

            var colorCoefficient = (int)(thermalSettings.ColorCoefficient * 100);
            var minTemperature = (int)((thermalSettings.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            var rampShift = (int)((thermalSettings.RampShift + 1.0f) * 100.0f);

            sldrThermalColorCoefficient.Value = colorCoefficient;
            sldrMinTemperature.Value = minTemperature;
            sldrThermalRampShift.Value = rampShift;
            cboThermalColorScheme.SelectedIndex = thermalSettings.ColorScheme;
        }

        private void cboThermalColorScheme_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorScheme = cboThermalColorScheme.SelectedIndex;
        }

        private void sldrThermalColorCoefficient_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorCoefficient = (float)Math.Round(newValue / 100.0f, 4, MidpointRounding.AwayFromZero);
        }

        private void sldrMinTemperature_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.MinTemperature = (float)Math.Round((0.01f - 0.001f) * (newValue / 100.0f) + 0.001f, 4, MidpointRounding.AwayFromZero);
        }

        private void sldrThermalRampShift_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.RampShift = (float)Math.Round((newValue / 100.0f) - 1.0f, 4, MidpointRounding.AwayFromZero);
        }

        private void swMasterSwitch_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = swMasterSwitch.Checked;
            _config.MasterSwitch = isChecked;

            mcSettingsMemoryWritingGlobal.Enabled = isChecked;
            mcSettingsMemoryWritingGear.Enabled = isChecked;
            mcSettingsMemoryWritingThermal.Enabled = isChecked;
            mcSettingsMemoryWritingSkillBuffs.Enabled = isChecked;
            mcSettingsMemoryWritingChams.Enabled = isChecked;

            if (isChecked)
                Memory.Toolbox?.StartToolbox();
            else
                Memory.Toolbox?.StopToolbox();
        }

        private void swMaxEndurance_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Endurance"] = swMaxEndurance.Checked;
        }

        private void swMaxStrength_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swMaxStrength.Checked;
            _config.MaxSkills["Strength"] = enabled;

            sldrThrowStrength.Enabled = enabled;
        }

        private void swMaxVitality_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Vitality"] = swMaxVitality.Checked;
        }

        private void swMaxHealth_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Health"] = swMaxHealth.Checked;
        }

        private void swMaxStressResistance_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Stress Resistance"] = swMaxStressResistance.Checked;
        }

        private void swMaxMetabolism_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Metabolism"] = swMaxMetabolism.Checked;
        }

        private void swMaxImmunity_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Immunity"] = swMaxImmunity.Checked;
        }

        private void swMaxPerception_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Perception"] = swMaxPerception.Checked;
        }

        private void swMaxIntellect_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Intellect"] = swMaxIntellect.Checked;
        }

        private void swMaxAttention_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Attention"] = swMaxAttention.Checked;
        }

        private void swMaxCovertMovement_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Covert Movement"] = swMaxCovertMovement.Checked;
        }

        private void swMaxThrowables_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Throwables"] = swMaxThrowables.Checked;
        }

        private void swMaxSurgery_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Surgery"] = swMaxSurgery.Checked;
        }

        private void swMaxSearch_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Search"] = swMaxSearch.Checked;
        }

        private void swMaxMagDrills_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swMaxMagDrills.Checked;
            _config.MaxSkills["Mag Drills"] = enabled;
            sldrMagDrillsSpeed.Enabled = enabled;
        }

        private void swMaxLightVests_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Light Vests"] = swMaxLightVests.Checked;
        }

        private void swMaxHeavyVests_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Heavy Vests"] = swMaxHeavyVests.Checked;
        }

        private void ToggleChamsControls()
        {
            var isChecked = swChams.Checked;

            swChamsPMCs.Enabled = isChecked;
            swChamsPlayerScavs.Enabled = isChecked;
            swChamsBosses.Enabled = isChecked;
            swChamsRogues.Enabled = isChecked;
            swChamsCultists.Enabled = isChecked;
            swChamsScavs.Enabled = isChecked;
            swChamsTeammates.Enabled = isChecked;
            swChamsCorpses.Enabled = isChecked;
            swChamsRevert.Enabled = isChecked;
        }

        private void swChams_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = swChams.Checked;
            _config.Chams["Enabled"] = isChecked;

            this.ToggleChamsControls();
        }

        private void swChamsPlayers_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["PMCs"] = swChamsPMCs.Checked;
            RefreshChams();
        }

        private void swChamsPlayerScavs_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["PlayerScavs"] = swChamsPlayerScavs.Checked;
            RefreshChams();
        }

        private void swChamsBosses_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Bosses"] = swChamsBosses.Checked;
            RefreshChams();
        }

        private void swChamsRogues_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Rogues"] = swChamsRogues.Checked;
            RefreshChams();
        }

        private void swChamsCultists_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Cultists"] = swChamsCultists.Checked;
            RefreshChams();
        }

        private void swChamsScavs_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Scavs"] = swChamsScavs.Checked;
            RefreshChams();
        }

        private void swChamsTeammates_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Teammates"] = swChamsTeammates.Checked;
            RefreshChams();
        }

        private void swChamsCorpses_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["Corpses"] = swChamsCorpses.Checked;
            RefreshChams();
        }

        private void swChamsRevert_CheckedChanged(object sender, EventArgs e)
        {
            _config.Chams["RevertOnClose"] = swChamsRevert.Checked;
        }
        #endregion
        #endregion

        #region Loot
        #region Helper Functions
        private void InitiateContainerList()
        {
            var containers = TarkovDevManager.AllLootContainers
                                              .Select(x => x.Value.Name)
                                              .OrderByDescending(x => x)
                                              .Distinct();

            foreach (var container in containers)
            {
                MaterialCheckbox checkbox = new MaterialCheckbox();
                checkbox.Text = container;

                if (_config.LootContainerSettings.ContainsKey(container))
                    checkbox.Checked = _config.LootContainerSettings[container];

                checkbox.CheckedChanged += ContainerCheckbox_CheckedChanged;
                lstContainers.Items.Add(checkbox);
            }
        }

        private void UpdateLootControls()
        {
            var questHelper = swQuestHelper.Checked;
            var processLoot = swProcessLoot.Checked;
            var lootRefresh = swAutoLootRefresh.Checked;

            swLooseLoot.Enabled = processLoot;
            swCorpses.Enabled = processLoot;
            swItemValue.Enabled = processLoot;
            swAutoLootRefresh.Enabled = processLoot;
            cboAutoRefreshMap.Enabled = (processLoot && lootRefresh);
            sldrAutoLootRefreshDelay.Enabled = (processLoot && lootRefresh);

            btnRefreshLoot.Enabled = processLoot;

            cboAutoRefreshMap.Enabled = (processLoot && lootRefresh);

            swQuestItems.Enabled = (processLoot && questHelper);
            swQuestLootItems.Enabled = (processLoot && questHelper);
            swUnknownQuestItems.Enabled = (processLoot && questHelper);

            mcSettingsLootMinRubleValue.Enabled = processLoot;
            mcSettingsLootPing.Enabled = processLoot;
        }

        private void UpdateQuestControls()
        {
            var questHelper = swQuestHelper.Checked;
            var processLoot = swProcessLoot.Checked;

            swQuestItems.Enabled = (processLoot && questHelper);
            swQuestLootItems.Enabled = (processLoot && questHelper);
            swQuestLocations.Enabled = questHelper;
            swAutoTaskRefresh.Enabled = questHelper;
            sldrAutoTaskRefreshDelay.Enabled = questHelper;
            swUnknownQuestItems.Enabled = (processLoot && questHelper);

            btnRefreshTasks.Enabled = questHelper;
        }
        #endregion
        #region Event Handlers
        // General
        private void swProcessLoot_CheckedChanged(object sender, EventArgs e)
        {
            var processLoot = swProcessLoot.Checked;
            _config.ProcessLoot = processLoot;

            this.UpdateLootControls();
            this.UpdateQuestControls();

            if (!processLoot)
                return;

            if (_config.LootItemRefresh)
                Memory.Loot?.StartAutoRefresh();
            else
                Memory.Loot?.RefreshLoot(true);
        }

        private void btnRefreshLoot_Click(object sender, EventArgs e)
        {
            lstLootItems.Items.Clear();

            Memory.Loot?.RefreshLoot(true);
        }

        private void swLooseLoot_CheckedChanged(object sender, EventArgs e)
        {
            var looseLoot = swLooseLoot.Checked;

            _config.LooseLoot = looseLoot;

            Memory.Loot?.ApplyFilter();
        }

        private void swFilteredOnly_CheckedChanged(object sender, EventArgs e)
        {
            _config.ImportantLootOnly = swFilteredOnly.Checked;
        }

        private void swSubItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.SubItems = swSubItems.Checked;
        }

        private void swItemValue_CheckedChanged(object sender, EventArgs e)
        {
            _config.LootValue = swItemValue.Checked;
        }

        private void swCorpses_CheckedChanged(object sender, EventArgs e)
        {
            _config.LootCorpses = swCorpses.Checked;
        }

        private void swAutoLootRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAutoLootRefresh.Checked;
            var processLoot = swProcessLoot.Checked;
            _config.LootItemRefresh = enabled;

            cboAutoRefreshMap.Enabled = (processLoot && enabled);
            sldrAutoLootRefreshDelay.Enabled = (processLoot && enabled);

            if (!processLoot)
                return;

            if (enabled)
                Memory.Loot?.StartAutoRefresh();
            else
                Memory.Loot?.StopAutoRefresh();
        }

        private void cboAutoRefreshMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !_config.LootItemRefreshSettings.ContainsKey(mapName))
                return;

            sldrAutoLootRefreshDelay.Value = _config.LootItemRefreshSettings[mapName];
        }

        private void sldrAutoRefreshDelay_onValueChanged(object sender, int newValue)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !_config.LootItemRefreshSettings.ContainsKey(mapName))
                return;

            if (newValue != _config.LootItemRefreshSettings[mapName])
                _config.LootItemRefreshSettings[mapName] = newValue;
        }

        // Quest Helper
        private void swQuestHelper_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swQuestHelper.Checked;

            _config.QuestHelper = enabled;

            this.UpdateQuestControls();
        }

        private void swQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuestItems = swQuestItems.Checked;
        }

        private void swQuestLootItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuestLootItems = swQuestLootItems.Checked;
            Memory.Loot?.ApplyFilter();
        }

        private void swQuestLocations_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuestLocations = swQuestLocations.Checked;
        }

        private void swAutoTaskRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAutoTaskRefresh.Checked;

            _config.QuestTaskRefresh = enabled;
            sldrAutoTaskRefreshDelay.Enabled = enabled;

            if (enabled)
                Memory.QuestManager?.StartAutoRefresh();
            else
                Memory.QuestManager?.StopAutoRefresh();
        }

        private void sldrAutoTaskRefreshDelay_onValueChanged(object sender, int newValue)
        {
            if (newValue < 1)
                newValue = 1;

            _config.QuestTaskRefreshDelay = newValue;
        }

        private void swUnknownQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.UnknownQuestItems = swUnknownQuestItems.Checked;
        }

        private void btnRefreshTasks_Click(object sender, EventArgs e)
        {
            Memory.QuestManager?.RefreshQuests(true);
        }

        // Minimum Ruble Value
        private void sldrMinRegularLoot_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 10)
            {
                int value = newValue * 1000;
                _config.MinLootValue = value;

                Loot?.ApplyFilter();
            }
        }

        private void sldrMinImportantLoot_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 250)
            {
                int value = newValue * 1000;
                _config.MinImportantLootValue = value;

                Loot?.ApplyFilter();
            }
        }

        private void sldrMinCorpse_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 10)
            {
                int value = newValue * 1000;
                _config.MinCorpseValue = value;

                Loot?.ApplyFilter();
            }
        }

        private void sldrMinSubItems_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 5)
            {
                int value = newValue * 1000;
                _config.MinSubItemValue = value;

                Loot?.ApplyFilter();
            }

        }

        // Loot Ping
        private void sldrLootPingAnimationSpeed_onValueChanged(object sender, int newValue)
        {
            _config.LootPing["AnimationSpeed"] = newValue;
        }

        private void sldrLootPingMaxRadius_onValueChanged(object sender, int newValue)
        {
            _config.LootPing["Radius"] = newValue;
        }

        private void sldrLootPingRepetition_onValueChanged(object sender, int newValue)
        {
            if (newValue < 1)
                newValue = 1;

            _config.LootPing["Repetition"] = newValue;
        }

        // Container Settings
        private void ContainerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            var checkbox = (MaterialCheckbox)sender;

            _config.LootContainerSettings[checkbox.Text] = checkbox.Checked;

            Memory.Loot?.ApplyFilter();
        }

        private void swContainers_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swContainers.Checked;

            _config.LootContainerSettings["Enabled"] = enabled;
            lstContainers.Enabled = enabled;

            Memory.Loot?.ApplyFilter();
        }
        #endregion
        #endregion

        #region AI Factions
        #region Helper Functions
        private AIFactionManager.Faction GetActiveFaction()
        {
            var itemCount = lstFactions.SelectedItems.Count;
            return itemCount > 0 ? lstFactions.SelectedItems[0].Tag as AIFactionManager.Faction : null;
        }

        private void RefreshPlayerTypeByFaction(AIFactionManager.Faction faction)
        {
            var enemyAI = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => !x.IsHuman && faction.Names.Contains(x.Name))
                .ToList();

            enemyAI?.ForEach(player =>
            {
                _aiFactions.IsInFaction(player.Name, out var playerType);
                player.Type = playerType;
            });
        }

        private void RefreshPlayerTypeByName(string name)
        {
            var enemyAI = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => !x.IsHuman && x.Name == name)
                .ToList();

            enemyAI?.ForEach(Player =>
            {
                _aiFactions.IsInFaction(Player.Name, out var playerType);
                Player.Type = playerType;
            });
        }

        private void UpdateFactions(int index = 0)
        {
            var factions = _aiFactions.Factions;

            lstFactions.Items.Clear();
            lstFactions.Items.AddRange(factions.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());

            if (lstFactions.Items.Count > 0)
            {
                var itemToSelect = lstFactions.Items[index];
                itemToSelect.Selected = true;
                UpdateFactionData();
                UpdateFactionEntriesList();
            }
        }

        private void UpdateFactionData()
        {
            var selectedFaction = GetActiveFaction();
            txtFactionName.Text = selectedFaction?.Name ?? "";
            cboFactionType.SelectedItem = (selectedFaction?.PlayerType ?? PlayerType.Boss);
            cboFactionType.Refresh();
        }

        private void UpdateFactionPlayerTypes()
        {
            cboFactionType.Items.Clear();
            cboFactionType.Items.Add(PlayerType.Boss);
            cboFactionType.Items.Add(PlayerType.BossGuard);
            cboFactionType.Items.Add(PlayerType.BossFollower);
            cboFactionType.Items.Add(PlayerType.Raider);
            cboFactionType.Items.Add(PlayerType.Rogue);
            cboFactionType.Items.Add(PlayerType.Cultist);
            cboFactionType.Items.Add(PlayerType.FollowerOfMorana);
        }

        private void UpdateFactionEntryData()
        {
            txtFactionEntryName.Text = _lastFactionEntry ?? "";
        }

        private void UpdateFactionEntriesList()
        {
            var selectedFaction = GetActiveFaction();
            var factionEntries = selectedFaction?.Names ?? Enumerable.Empty<string>();

            lstFactionEntries.Items.Clear();
            lstFactionEntries.Items.AddRange(factionEntries.Select(entry => new ListViewItem
            {
                Text = entry,
                Tag = entry,
            }).OrderByDescending(entry => entry.Name).ToArray());
        }

        private bool HasUnsavedFactionChanges()
        {
            var selectedFaction = GetActiveFaction();
            if (selectedFaction is null)
                return false;

            var selectedPlayerType = (PlayerType)cboFactionType.SelectedItem;
            return txtFactionName.Text != selectedFaction.Name || selectedPlayerType != selectedFaction.PlayerType;
        }

        private bool HasUnsavedFactionEntryChanges()
        {
            if (_lastFactionEntry is null)
                return false;

            return txtFactionEntryName.Text != _lastFactionEntry;
        }

        private void SaveFaction()
        {
            if (HasUnsavedFactionChanges())
            {
                if (string.IsNullOrEmpty(txtFactionName.Text))
                {
                    ShowErrorDialog("Add some text to the faction name textbox (minimum 1 character)");
                    return;
                }
                var selectedFaction = GetActiveFaction();
                int index = _aiFactions.Factions.IndexOf(selectedFaction);

                selectedFaction.Name = txtFactionName.Text;
                selectedFaction.PlayerType = (PlayerType)cboFactionType.SelectedItem;

                _aiFactions.UpdateFaction(selectedFaction, index);

                UpdateFactions(lstFactions.SelectedIndices[0]);
                RefreshPlayerTypeByFaction(selectedFaction);
            }
        }

        private void SaveFactionEntry()
        {
            if (HasUnsavedFactionEntryChanges())
            {
                if (string.IsNullOrEmpty(txtFactionEntryName.Text))
                {
                    ShowErrorDialog("Add some text to the entry name textbox (minimum 1 character)");
                    return;
                }

                var selectedFaction = GetActiveFaction();
                var entry = _lastFactionEntry;
                var index = selectedFaction.Names.IndexOf(entry);

                entry = txtFactionEntryName.Text;

                _aiFactions.UpdateEntry(selectedFaction, entry, index);

                _lastFactionEntry = entry;

                UpdateFactionEntriesList();
                RefreshPlayerTypeByName(entry);
            }
        }

        private void RemoveFactionEntry(AIFactionManager.Faction selectedFaction, string name)
        {
            if (ShowConfirmationDialog("Are you sure you want to remove this entry?", "Are you sure?") == DialogResult.OK)
            {
                _aiFactions.RemoveEntry(selectedFaction, name);
                _lastFactionEntry = null;

                UpdateFactionEntriesList();
                UpdateFactionEntryData();
                RefreshPlayerTypeByName(name);
            }
        }
        #endregion

        #region Event Handlers
        private void txtFactionEntryName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var selectedFaction = GetActiveFaction();

                var newEntryName = txtFactionEntryName.Text;
                var existingEntry = selectedFaction.Names.FirstOrDefault(entry => entry == newEntryName);

                if (existingEntry is not null)
                {
                    ShowErrorDialog($"An entry with the name '{newEntryName}' already exists. Please edit or delete the existing entry.");
                    return;
                }

                SaveFactionEntry();
            }
        }

        private void txtFactionName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var selectedFaction = GetActiveFaction();

                var newFactionName = txtFactionName.Text;
                var existingEntry = _aiFactions.Factions.FirstOrDefault(entry => entry.Name == newFactionName);

                if (existingEntry is not null)
                {
                    ShowErrorDialog($"A faction with the name '{newFactionName}' already exists. Please edit or delete the existing faction.");
                    return;
                }

                SaveFaction();
            }
        }

        private void btnAddFactionEntry_Click(object sender, EventArgs e)
        {
            var selectedFaction = GetActiveFaction();

            if (selectedFaction is null)
                return;

            var existingEntry = selectedFaction.Names.FirstOrDefault(entry => entry == "New Entry");

            if (existingEntry is not null)
            {
                ShowErrorDialog($"An entry with the name '{existingEntry}' already exists. Please edit or delete the existing entry.");
                return;
            }

            _aiFactions.AddEmptyEntry(selectedFaction);
            UpdateFactionEntriesList();
        }

        private void btnAddFaction_Click(object sender, EventArgs e)
        {
            var existingFaction = _aiFactions.Factions.FirstOrDefault(faction => faction.Name == "Default");

            if (existingFaction is not null)
            {
                ShowErrorDialog($"A faction with the name '{existingFaction.Name}' already exists. Please edit or delete the existing faction.");
                return;
            }

            _aiFactions.AddEmptyFaction();
            UpdateFactions();
        }

        private void btnRemoveFactionEntry_Click(object sender, EventArgs e)
        {
            var selectedFaction = GetActiveFaction();
            var selectedEntry = _lastFactionEntry;

            if (selectedFaction is not null)
                RemoveFactionEntry(selectedFaction, selectedEntry);
        }

        private void btnRemoveFaction_Click(object sender, EventArgs e)
        {
            var selectedFaction = GetActiveFaction();

            if (selectedFaction is null)
                return;

            var factions = _aiFactions.Factions;

            if (factions.Count == 1)
            {
                if (ShowConfirmationDialog("Removing the last faction will automatically create a new one", "Warning") == DialogResult.OK)
                {
                    _aiFactions.RemoveFaction(lstFactions.SelectedIndices[0]);
                    _aiFactions.AddEmptyFaction();
                    RefreshPlayerTypeByFaction(selectedFaction);
                }
            }
            else
            {
                if (ShowConfirmationDialog("Are you sure you want to delete this faction?", "Warning") == DialogResult.OK)
                {
                    _aiFactions.RemoveFaction(selectedFaction);
                    RefreshPlayerTypeByFaction(selectedFaction);
                }
            }

            UpdateFactions();
        }

        private void cboFactionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveFaction();
        }

        private void lstFactionEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveFactionEntry();
            _lastFactionEntry = lstFactionEntries.FocusedItem?.Text ?? null;
            UpdateFactionEntryData();
        }

        private void lstFactions_SelectedIndexChanged(object sender, EventArgs e)
        {
            _lastFactionEntry = null;

            UpdateFactionData();
            UpdateFactionEntriesList();
            UpdateFactionEntryData();
        }
        #endregion
        #endregion

        #region Colors
        #region Helper Functions
        private void UpdateThemeColors()
        {
            Color primary = picOtherPrimary.BackColor;
            Color darkPrimary = picOtherPrimaryDark.BackColor;
            Color lightPrimary = picOtherPrimaryLight.BackColor;
            Color accent = picOtherAccent.BackColor;

            MaterialSkinManager.Instance.ColorScheme = new ColorScheme(primary, darkPrimary, lightPrimary, accent, TextShade.WHITE);

            UpdatePaintColorControls();

            this.Invalidate();
            this.Refresh();
        }

        private Color PaintColorToColor(string name)
        {
            PaintColor.Colors color = _config.PaintColors[name];
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private Color DefaultPaintColorToColor(string name)
        {
            PaintColor.Colors color = _config.DefaultPaintColors[name];
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private void UpdatePaintColorControls()
        {
            var colors = _config.PaintColors;

            Action<PictureBox, string> setColor = (pictureBox, name) =>
            {
                if (colors.ContainsKey(name))
                {
                    pictureBox.BackColor = PaintColorToColor(name);
                }
                else
                {
                    colors.Add(name, _config.DefaultPaintColors[name]);
                    pictureBox.BackColor = DefaultPaintColorToColor(name);
                }
            };

            // AI
            setColor(picAIBoss, "Boss");
            setColor(picAIBossGuard, "BossGuard");
            setColor(picAIBossFollower, "BossFollower");
            setColor(picAIRaider, "Raider");
            setColor(picAIRogue, "Rogue");
            setColor(picAICultist, "Cultist");
            setColor(picAIFollowerOfMorana, "FollowerOfMorana");
            setColor(picAIOther, "Other");
            setColor(picAIScav, "Scav");

            // Players
            setColor(picPlayersUSEC, "USEC");
            setColor(picPlayersBEAR, "BEAR");
            setColor(picPlayersScav, "PlayerScav");
            setColor(picPlayersLocalPlayer, "LocalPlayer");
            setColor(picPlayersTeammate, "Teammate");
            setColor(picPlayersTeamHover, "TeamHover");
            setColor(picPlayersSpecial, "Special");

            // Exfils
            setColor(picExfilActiveText, "ExfilActiveText");
            setColor(picExfilActiveIcon, "ExfilActiveIcon");
            setColor(picExfilPendingText, "ExfilPendingText");
            setColor(picExfilPendingIcon, "ExfilPendingIcon");
            setColor(picExfilClosedText, "ExfilClosedText");
            setColor(picExfilClosedIcon, "ExfilClosedIcon");

            // Loot/Quests
            setColor(picLootRegular, "RegularLoot");
            setColor(picLootImportant, "ImportantLoot");
            setColor(picQuestItem, "QuestItem");
            setColor(picQuestZone, "QuestZone");
            setColor(picRequiredQuestItem, "RequiredQuestItem");
            setColor(picLootPing, "LootPing");

            // Other
            setColor(picOtherTextOutline, "TextOutline");
            setColor(picOtherDeathMarker, "DeathMarker");
            setColor(picOtherChams, "Chams");
            setColor(picOtherPrimary, "Primary");
            setColor(picOtherPrimaryDark, "PrimaryDark");
            setColor(picOtherPrimaryLight, "PrimaryLight");
            setColor(picOtherAccent, "Accent");
        }

        private void UpdatePaintColorByName(string name, PictureBox pictureBox)
        {
            if (colDialog.ShowDialog() == DialogResult.OK)
            {
                Color col = colDialog.Color;
                pictureBox.BackColor = col;

                var paintColorToUse = new PaintColor.Colors
                {
                    A = col.A,
                    R = col.R,
                    G = col.G,
                    B = col.B
                };

                if (_config.PaintColors.ContainsKey(name))
                {
                    _config.PaintColors[name] = paintColorToUse;

                    if (Extensions.SKColors.ContainsKey(name))
                        Extensions.SKColors[name] = new SKColor(col.R, col.G, col.B, col.A);
                }
                else
                {
                    _config.PaintColors.Add(name, paintColorToUse);
                }
            }
        }
        #endregion

        #region Event Handlers
        // AI
        private void picAIBoss_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Boss", picAIBoss);
        }

        private void picAIBossGuard_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("BossGuard", picAIBossGuard);
        }

        private void picAIBossFollower_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("BossFollower", picAIBossFollower);
        }

        private void picAIRaider_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Raider", picAIRaider);
        }

        private void picAIRogue_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Rogue", picAIRogue);
        }

        private void picAICultist_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Cultist", picAICultist);
        }

        private void picAIFollowerOfMorana_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("FollowerOfMorana", picAIFollowerOfMorana);
        }

        private void picAIScav_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Scav", picAIScav);
        }

        private void picAIOther_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Other", picAIOther);
        }

        // Players
        private void picPlayersUSEC_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("USEC", picPlayersUSEC);
        }

        private void picPlayersBEAR_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("BEAR", picPlayersBEAR);
        }

        private void picPlayersScav_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("PlayerScav", picPlayersScav);
        }

        private void picPlayersLocalPlayer_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("LocalPlayer", picPlayersLocalPlayer);
        }

        private void picPlayersTeammate_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Teammate", picPlayersTeammate);
        }

        private void picPlayersTeamHover_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("TeamHover", picPlayersTeamHover);
        }

        private void picPlayersSpecial_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Special", picPlayersSpecial);
        }

        // Exfiltration
        private void picExfilActiveText_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilActiveText", picExfilActiveText);
        }

        private void picExfilActiveIcon_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilActiveIcon", picExfilActiveIcon);
        }

        private void picExfilPendingText_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilPendingText", picExfilPendingText);
        }

        private void picExfilPendingIcon_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilPendingIcon", picExfilPendingIcon);
        }

        private void picExfilClosedText_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilClosedText", picExfilClosedText);
        }

        private void picExfilClosedIcon_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilClosedIcon", picExfilClosedIcon);
        }

        // Loot / Quests
        private void picLootRegular_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("RegularLoot", picLootRegular);
        }

        private void picLootImportant_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ImportantLoot", picLootImportant);
        }

        private void picQuestItem_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("QuestItem", picQuestItem);
        }

        private void picQuestZone_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("QuestZone", picQuestZone);
        }

        private void picRequiredQuestItem_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("RequiredQuestItem", picRequiredQuestItem);
        }

        private void picLootPing_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("LootPing", picLootPing);
        }

        // Other
        private void picOtherTextOutline_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("TextOutline", picOtherTextOutline);
        }

        private void picOtherDeathMarker_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("DeathMarker", picOtherDeathMarker);
        }

        private void picOtherChams_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Chams", picOtherChams);
        }

        private void picOtherPrimary_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Primary", picOtherPrimary);
            UpdateThemeColors();
        }

        private void picOtherPrimaryDark_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("PrimaryDark", picOtherPrimaryDark);
            UpdateThemeColors();
        }

        private void picOtherPrimaryLight_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("PrimaryLight", picOtherPrimaryLight);
            UpdateThemeColors();
        }

        private void picOtherAccent_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Accent", picOtherAccent);
            UpdateThemeColors();
        }

        private void btnResetTheme_Click(object sender, EventArgs e)
        {
            _config.PaintColors["Primary"] = _config.DefaultPaintColors["Primary"];
            _config.PaintColors["PrimaryDark"] = _config.DefaultPaintColors["PrimaryDark"];
            _config.PaintColors["PrimaryLight"] = _config.DefaultPaintColors["PrimaryLight"];
            _config.PaintColors["Accent"] = _config.DefaultPaintColors["Accent"];

            picOtherPrimary.BackColor = DefaultPaintColorToColor("Primary");
            picOtherPrimaryDark.BackColor = DefaultPaintColorToColor("PrimaryDark");
            picOtherPrimaryLight.BackColor = DefaultPaintColorToColor("PrimaryLight");
            picOtherAccent.BackColor = DefaultPaintColorToColor("Accent");

            UpdateThemeColors();
        }
        #endregion
        #endregion
        #endregion

        #region Watchlist
        #region Helper Functions
        private Watchlist.Profile GetActiveWatchlistProfile()
        {
            var itemCount = lstWatchlistProfiles.SelectedItems.Count;
            return itemCount > 0 ? lstWatchlistProfiles.SelectedItems[0].Tag as Watchlist.Profile : null;
        }

        private void RefreshWatchlistStatusesByProfile(Watchlist.Profile profile)
        {
            var enemyPlayers = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive && profile.Entries.Any(entry => entry.AccountID == x.AccountID))
                .ToList();

            enemyPlayers?.ForEach(player => player.RefreshWatchlistStatus());
        }

        private void RefreshWatchlistStatuses()
        {
            var enemyPlayers = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive)
                .ToList();

            enemyPlayers?.ForEach(player => player.RefreshWatchlistStatus());
        }

        private void RefreshWatchlistStatus(string accountID)
        {
            var enemyPlayer = this.AllPlayers
                ?.Select(x => x.Value)
                .FirstOrDefault(x => x.IsHumanHostileActive && x.AccountID == accountID);

            enemyPlayer?.RefreshWatchlistStatus();
        }

        private void UpdateWatchlistProfiles(int index = 0)
        {
            var profiles = _watchlist.Profiles;

            lstWatchlistProfiles.Items.Clear();
            lstWatchlistProfiles.Items.AddRange(profiles.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());

            if (lstWatchlistProfiles.Items.Count > 0)
            {
                var itemToSelect = lstWatchlistProfiles.Items[index];
                itemToSelect.Selected = true;
                UpdateWatchlistEntriesList();
            }
        }

        private void UpdateWatchlistProfileData()
        {
            var selectedProfile = GetActiveWatchlistProfile();
            txtWatchlistProfileName.Text = selectedProfile?.Name ?? "";
        }

        private void UpdateWatchlistEntryData()
        {
            txtWatchlistAccountID.Text = _lastWatchlistEntry?.AccountID ?? "";
            txtWatchlistTag.Text = _lastWatchlistEntry?.Tag ?? "";
            txtWatchlistPlatformUsername.Text = _lastWatchlistEntry?.PlatformUsername ?? "";
            swWatchlistIsStreamer.Checked = _lastWatchlistEntry?.IsStreamer ?? false;
            rdbTwitch.Checked = _lastWatchlistEntry?.Platform == 0;
            rdbYoutube.Checked = _lastWatchlistEntry?.Platform == 1;
        }

        private void UpdateWatchlistEntriesList()
        {
            var selectedProfile = GetActiveWatchlistProfile();
            var watchlistEntries = selectedProfile?.Entries ?? Enumerable.Empty<Watchlist.Entry>();

            lstWatchlistEntries.Items.Clear();
            lstWatchlistEntries.Items.AddRange(watchlistEntries.Select(entry => new ListViewItem
            {
                Text = entry.AccountID,
                Tag = entry,
                SubItems = { entry.Tag, entry.PlatformUsername }
            }).ToArray());
        }

        private bool HasUnsavedWatchlistProfileChanges()
        {
            var selectedProfile = GetActiveWatchlistProfile();
            return (selectedProfile is not null && lstWatchlistProfiles.Text != selectedProfile.Name);
        }

        private bool HasUnsavedWatchlistEntryChanges()
        {
            if (_lastWatchlistEntry is null)
                return false;

            return txtWatchlistAccountID.Text != _lastWatchlistEntry.AccountID ||
                    txtWatchlistTag.Text != _lastWatchlistEntry.Tag ||
                    txtWatchlistPlatformUsername.Text != _lastWatchlistEntry.PlatformUsername ||
                    swWatchlistIsStreamer.Checked != _lastWatchlistEntry.IsStreamer ||
                    (rdbTwitch.Checked ? 0 : 1) != _lastWatchlistEntry.Platform;
        }

        private void SaveWatchlistProfile()
        {
            if (string.IsNullOrEmpty(txtWatchlistProfileName.Text))
            {
                ShowErrorDialog("Add some text to the profile name textbox (minimum 1 character)");
                return;
            }

            if (HasUnsavedWatchlistProfileChanges())
            {
                var selectedProfile = GetActiveWatchlistProfile();
                int index = _watchlist.Profiles.IndexOf(selectedProfile);

                selectedProfile.Name = txtWatchlistProfileName.Text;

                _watchlist.UpdateProfile(selectedProfile, index);

                UpdateWatchlistProfiles(lstWatchlistProfiles.SelectedIndices[0]);
                RefreshWatchlistStatusesByProfile(selectedProfile);
            }
        }

        private void SaveWatchlistEntry()
        {
            var selectedProfile = GetActiveWatchlistProfile();

            if (HasUnsavedWatchlistEntryChanges())
            {
                if (string.IsNullOrEmpty(txtWatchlistAccountID.Text) ||
                    string.IsNullOrEmpty(txtWatchlistTag.Text) ||
                    string.IsNullOrEmpty(txtWatchlistPlatformUsername.Text))
                {
                    ShowErrorDialog("Add some text to the account id / tag / platform username textboxes (minimum 1 character)");
                    return;
                }

                var entry = _lastWatchlistEntry;
                var index = selectedProfile.Entries.IndexOf(entry);

                entry = new Watchlist.Entry()
                {
                    AccountID = txtWatchlistAccountID.Text,
                    Tag = txtWatchlistTag.Text,
                    IsStreamer = swWatchlistIsStreamer.Checked,
                    Platform = rdbTwitch.Checked ? 0 : 1,
                    PlatformUsername = txtWatchlistPlatformUsername.Text
                };

                _watchlist.UpdateEntry(selectedProfile, entry, index);

                _lastWatchlistEntry = entry;

                UpdateWatchlistEntriesList();
                RefreshWatchlistStatus(entry.AccountID);
            }
        }

        private void RemoveWatchlistEntry(Watchlist.Profile selectedProfile, Watchlist.Entry selectedEntry)
        {
            if (ShowConfirmationDialog("Are you sure you want to remove this entry?", "Are you sure?") == DialogResult.OK)
            {
                _watchlist.RemoveEntry(selectedProfile, selectedEntry);

                _lastWatchlistEntry = null;

                UpdateWatchlistEntriesList();
                RefreshWatchlistStatus(selectedEntry.AccountID);
                UpdateWatchlistEntryData();
            }
        }

        private void UpdateWatchlistPlayers(bool clearItems)
        {
            var enemyPlayers = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive)
                .ToList();

            _watchlistMatchPlayers = new List<Player>();

            if (!clearItems)
            {
                foreach (var player in lstWatchlistPlayerList.Items)
                {
                    _watchlistMatchPlayers.Add((Player)player);
                }
            }

            if (enemyPlayers is not null)
            {
                foreach (var player in enemyPlayers)
                {
                    if (!_watchlistMatchPlayers.Any(p => p.Name == player.Name))
                    {
                        _watchlistMatchPlayers.Add(player);
                    }
                }
            }

            lstWatchlistPlayerList.Items.AddRange(_watchlistMatchPlayers.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).OrderBy(entry => entry.Name).ToArray());
        }
        #endregion

        #region Event Handlers
        private void txtWatchlistAccountID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveWatchlistEntry();
            }
        }

        private void txtWatchlistTag_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveWatchlistEntry();
            }
        }

        private void txtWatchlistPlatformUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveWatchlistEntry();
            }
        }

        private void swWatchlistIsStreamer_CheckedChanged(object sender, EventArgs e)
        {
            SaveWatchlistEntry();
        }

        private void rdbTwitch_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbTwitch.Checked)
                SaveWatchlistEntry();
        }

        private void rdbYoutube_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbYoutube.Checked)
                SaveWatchlistEntry();
        }

        private void txtWatchlistProfileName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SaveWatchlistProfile();
            }
        }

        private void btnAddWatchlistEntry_Click(object sender, EventArgs e)
        {
            var selectedProfile = GetActiveWatchlistProfile();
            var selectedPlayer = lstWatchlistPlayerList.SelectedItems.Count > 0 ? lstWatchlistPlayerList.SelectedItems[0].Tag as Player : null;

            if (selectedProfile is null)
                return;

            var existingEntry = selectedProfile.Entries.FirstOrDefault(entry => entry.AccountID == (selectedPlayer?.AccountID ?? "New Entry"));

            if (existingEntry is not null)
            {
                ShowErrorDialog($"An entry with the account id '{existingEntry.AccountID}' already exists. Please edit or delete the existing entry.");
                return;
            }

            if (selectedPlayer is not null)
            {
                _watchlist.AddEntry(selectedProfile, selectedPlayer.AccountID, selectedPlayer.Name);
                RefreshWatchlistStatuses();
            }
            else
            {
                _watchlist.AddEmptyEntry(selectedProfile);
            }

            UpdateWatchlistEntriesList();
        }

        private void btnAddWatchlistProfile_Click(object sender, EventArgs e)
        {
            var existingProfile = _watchlist.Profiles.FirstOrDefault(profile => profile.Name == "Default");

            if (existingProfile is not null)
            {
                ShowErrorDialog($"A profile with the name '{existingProfile.Name}' already exists. Please edit or delete the existing profile.");
                return;
            }

            _watchlist.AddEmptyProfile();
            UpdateWatchlistProfiles();
        }

        private void btnRemoveWatchlistProfile_Click(object sender, EventArgs e)
        {
            var selectedProfile = GetActiveWatchlistProfile();

            if (selectedProfile is null)
                return;

            var profiles = _watchlist.Profiles;

            if (profiles.Count == 1)
            {
                if (ShowConfirmationDialog("Removing the last profile will automatically create a default one", "Warning") == DialogResult.OK)
                {
                    _watchlist.RemoveProfile(lstWatchlistProfiles.SelectedIndices[0]);
                    _watchlist.AddEmptyProfile();
                    RefreshWatchlistStatusesByProfile(selectedProfile);
                }
            }
            else
            {
                if (ShowConfirmationDialog("Are you sure you want to delete this profile?", "Warning") == DialogResult.OK)
                {
                    _watchlist.RemoveProfile(selectedProfile);
                    RefreshWatchlistStatusesByProfile(selectedProfile);
                }
            }

            UpdateWatchlistProfiles();
        }

        private void btnRemoveWatchlistEntry_Click(object sender, EventArgs e)
        {
            var selectedWatchlist = GetActiveWatchlistProfile();
            var selectedEntry = _lastWatchlistEntry;

            if (selectedWatchlist is not null)
                RemoveWatchlistEntry(selectedWatchlist, selectedEntry);
        }

        private void lstViewWatchlistEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveWatchlistEntry();
            _lastWatchlistEntry = lstWatchlistEntries.SelectedItems.Count > 0 ? (Watchlist.Entry)lstWatchlistEntries.SelectedItems[0].Tag : null;
            UpdateWatchlistEntryData();
        }

        private void lstWatchlistProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            _lastWatchlistEntry = null;

            UpdateWatchlistProfileData();
            UpdateWatchlistEntriesList();
            UpdateWatchlistEntryData();
        }

        private void btnResetPlayerlist_Click(object sender, EventArgs e)
        {
            UpdateWatchlistPlayers(true);
        }
        #endregion
        #endregion

        #region Loot Filter
        #region Helper Functions
        private LootFilterManager.Filter GetActiveLootFilter()
        {
            var itemCount = lstLootFilters.SelectedItems.Count;
            return itemCount > 0 ? lstLootFilters.SelectedItems[0].Tag as LootFilterManager.Filter : null;
        }

        private bool HasUnsavedFilterChanges()
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null)
                return false;

            return txtLootFilterName.Text != selectedFilter.Name ||
                   swLootFilterActive.Checked != selectedFilter.IsActive ||
                   picLootFilterColor.BackColor != Color.FromArgb(selectedFilter.Color.A, selectedFilter.Color.R, selectedFilter.Color.G, selectedFilter.Color.B);
        }

        private void UpdateLootFilters(int index = 0)
        {
            var lootFilters = _config.Filters.OrderBy(lf => lf.Order).ToList();

            lstLootFilters.Items.Clear();
            lstLootFilters.Items.AddRange(lootFilters.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());

            if (lstLootFilters.Items.Count > 0)
            {
                var itemToSelect = lstLootFilters.Items[index];
                itemToSelect.Selected = true;
                UpdateLootFilterData();
                UpdateLootFilterEntriesList();
            }

            this.Loot?.ApplyFilter();
        }

        private void UpdateLootFilterData()
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null)
                return;

            txtLootFilterName.Text = selectedFilter.Name;
            picLootFilterColor.BackColor = Color.FromArgb(selectedFilter.Color.A, selectedFilter.Color.R, selectedFilter.Color.G, selectedFilter.Color.B);
            swLootFilterActive.Checked = selectedFilter.IsActive;
        }

        private void SaveLootFilterChanges()
        {
            if (string.IsNullOrEmpty(txtLootFilterName.Text))
            {
                ShowErrorDialog("Add some text to the loot filter name textbox (minimum 1 character)");
                return;
            }

            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null)
                return;

            if (HasUnsavedFilterChanges())
            {
                int index = _config.Filters.IndexOf(selectedFilter);

                selectedFilter.Name = txtLootFilterName.Text;
                selectedFilter.IsActive = swLootFilterActive.Checked;
                selectedFilter.Color = new PaintColor.Colors
                {
                    R = picLootFilterColor.BackColor.R,
                    G = picLootFilterColor.BackColor.G,
                    B = picLootFilterColor.BackColor.B,
                    A = picLootFilterColor.BackColor.A
                };

                _lootFilterManager.UpdateFilter(selectedFilter, index);
                UpdateLootFilters(lstLootFilters.SelectedIndices[0]);
            }
        }

        private void UpdateLootFilterOrders()
        {
            for (int i = 0; i < _config.Filters.Count; i++)
            {
                _config.Filters[i].Order = i + 1;
            }
        }

        private void UpdateLootFilterEntriesList()
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter?.Items is null)
                return;

            var lootList = TarkovDevManager.AllItems.Values.ToList();
            var matchingLoot = lootList.Where(loot => selectedFilter.Items.Contains(loot.Item.id))
                                       .OrderBy(l => l.Item.name)
                                       .ToList();

            lstLootFilterEntries.Items.Clear();
            lstLootFilterEntries.Items.AddRange(matchingLoot.Select(item => new ListViewItem
            {
                Text = item.Name,
                Tag = item,
                SubItems =
                {
                    TarkovDevManager.FormatNumber(item.Value)
                }
            }).ToArray());
        }
        #endregion

        #region Event Handlers
        private void txtLootFilterItemToSearch_TextChanged(object sender, EventArgs e)
        {
            var itemToSearch = txtLootFilterItemToSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemToSearch))
                return;

            var lootList = TarkovDevManager.AllItems.Values
                .Where(x => x.Name.IndexOf(itemToSearch, StringComparison.OrdinalIgnoreCase) != -1)
                .OrderBy(x => x.Name)
                .Take(25)
                .ToArray();

            cboLootFilterItemsToAdd.DataSource = lootList;
            cboLootFilterItemsToAdd.DisplayMember = "Name";
        }

        private void btnAddLootFilterItem_Click(object sender, EventArgs e)
        {
            if (cboLootFilterItemsToAdd.SelectedIndex == -1)
                return;

            var selectedFilter = GetActiveLootFilter();
            var selectedItem = cboLootFilterItemsToAdd.SelectedItem as LootItem;

            if (selectedFilter?.Items is not null && selectedItem is not null && !selectedFilter.Items.Contains(selectedItem.ID))
            {
                var listItem = new ListViewItem(new[]
                {       selectedItem.Item.name,
                        TarkovDevManager.FormatNumber(selectedItem.Value),
                    })
                {
                    Tag = selectedItem
                };

                lstLootFilterEntries.Items.Add(listItem);
                selectedFilter.Items.Add(selectedItem.Item.id);
                LootFilterManager.SaveLootFilterManager(_lootFilterManager);
                Loot?.ApplyFilter();
            }
        }

        private void btnRemoveLootFilterItem_Click(object sender, EventArgs e)
        {
            if (lstLootFilterEntries.SelectedItems.Count < 1)
                return;

            var selectedFilter = GetActiveLootFilter();
            var selectedItem = lstLootFilterEntries.SelectedItems[0];

            if (selectedItem?.Tag is LootItem lootItem)
            {
                selectedItem.Remove();
                _lootFilterManager.RemoveFilterItem(selectedFilter, lootItem.Item.id);
                this.Loot?.ApplyFilter();
            }
        }

        private void btnFilterPriorityUp_Click(object sender, EventArgs e)
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null || selectedFilter.Order == 1)
                return;

            int index = selectedFilter.Order - 1;
            var swapFilter = _config.Filters.FirstOrDefault(f => f.Order == index);

            if (swapFilter is not null)
            {
                selectedFilter.Order = swapFilter.Order;
                swapFilter.Order = index + 1;
                LootFilterManager.SaveLootFilterManager(_lootFilterManager);
                UpdateLootFilters(index - 1);
            }
        }

        private void btnFilterPriorityDown_Click(object sender, EventArgs e)
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null || selectedFilter.Order == _config.Filters.Count)
                return;

            int index = selectedFilter.Order;
            var swapFilter = _config.Filters.FirstOrDefault(f => f.Order == index + 1);

            if (swapFilter is not null)
            {
                selectedFilter.Order = swapFilter.Order;
                swapFilter.Order = index;
                LootFilterManager.SaveLootFilterManager(_lootFilterManager);
                UpdateLootFilters(index);
            }
        }

        private void btnAddFilter_Click(object sender, EventArgs e)
        {
            var existingFilter = _config.Filters.FirstOrDefault(filter => filter.Name == "New Filter");

            if (existingFilter is not null)
            {
                ShowErrorDialog("A loot filter with the name 'New Filter' already exists. Please rename or delete the existing filter.");
                return;
            }

            _lootFilterManager.AddEmptyProfile();
            UpdateLootFilters(_config.Filters.Count - 1);
        }

        private void btnRemoveFilter_Click(object sender, EventArgs e)
        {
            var selectedFilter = GetActiveLootFilter();

            if (selectedFilter is null)
                return;

            if (_config.Filters.Count == 1)
            {
                if (ShowConfirmationDialog("Removing the last filter will automatically create a blank one. Are you sure you want to proceed?", "Warning") == DialogResult.OK)
                {
                    _lootFilterManager.RemoveFilter(lstLootFilters.SelectedIndices[0]);
                    _lootFilterManager.AddEmptyProfile();
                    UpdateLootFilters();
                }
            }
            else
            {
                if (ShowConfirmationDialog("Are you sure you want to delete this filter?", "Warning") == DialogResult.OK)
                {
                    _lootFilterManager.RemoveFilter(selectedFilter);
                    UpdateLootFilterOrders();
                    UpdateLootFilters();
                }
            }
        }

        private void txtLootFilterName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter)
            {
                SaveLootFilterChanges();
            }
        }

        private void picLootFilterColor_Click(object sender, EventArgs e)
        {
            if (colDialog.ShowDialog() == DialogResult.OK)
            {
                picLootFilterColor.BackColor = colDialog.Color;
                SaveLootFilterChanges();
            }
        }

        private void swLootFilterActive_CheckedChanged(object sender, EventArgs e)
        {
            SaveLootFilterChanges();
        }

        private void lstLootFilters_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateLootFilterData();
            UpdateLootFilterEntriesList();
        }
        #endregion
        #endregion
        #endregion
    }
}