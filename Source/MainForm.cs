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

        private const double ZoomSensitivity = 0.3;
        private const int ZoomInterval = 10;
        private int targetZoomValue = 0;
        private System.Windows.Forms.Timer zoomTimer;

        private const float DragSensitivity = 3.5f;

        private const double PanSmoothness = 0.1;
        private const int PanInterval = 10;
        private SKPoint targetPanPosition;
        private System.Windows.Forms.Timer panTimer;

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
            zoomTimer.Interval = ZoomInterval;
            zoomTimer.Tick += ZoomTimer_Tick;

            panTimer = new System.Windows.Forms.Timer();
            panTimer.Interval = PanInterval;
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

            Config.SaveConfig(_config); // Save Config to Config.json
            Memory.Toolbox?.StopToolbox();
            Memory.Loot?.StopAutoRefresh();
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
            Keys.F3 => swShowLoot.Checked = !swShowLoot.Checked,
            Keys.F4 => swAimview.Checked = !swAimview.Checked,
            Keys.F5 => ToggleMap(),
            Keys.F6 => swNames.Checked = !swNames.Checked,
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
                int zoomDelta = -(int)(e.Delta * ZoomSensitivity);

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
                    Color = new LootFilterManager.Filter.Colors()
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
            cboAutoRefreshMap.Items.AddRange(_config.AutoRefreshSettings.Keys.ToArray());
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

            InitiateFontSize();
        }

        private void InitiateFont()
        {
            var fontToUse = SKTypeface.FromFamilyName(cboFont.Text);
            SKPaints.TextMouseoverGroup.Typeface = fontToUse;
            SKPaints.TextBase.Typeface = fontToUse;
            SKPaints.LootText.Typeface = fontToUse;
            SKPaints.TextBaseOutline.Typeface = fontToUse;
            SKPaints.TextRadarStatus.Typeface = fontToUse;
        }

        private void InitiateFontSize()
        {
            SKPaints.TextMouseoverGroup.TextSize = _config.FontSize * _uiScale;
            SKPaints.TextBase.TextSize = _config.FontSize * _uiScale;
            SKPaints.LootText.TextSize = _config.FontSize * _uiScale;
            SKPaints.TextBaseOutline.TextSize = _config.FontSize * _uiScale;
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

        private void LoadConfig()
        {
            #region Settings
            #region General
            // Radar
            swRadarStats.Checked = _config.ShowRadarStats;
            mcRadarStats.Visible = _config.ShowRadarStats;
            swRadarVsync.Checked = _config.VSync;
            swRadarEnemyCount.Checked = _config.EnemyCount;
            mcRadarEnemyStats.Visible = _config.EnemyCount;

            // User Interface
            swShowLoot.Checked = _config.ShowLoot;
            swQuestHelper.Checked = _config.QuestHelper;
            swUnknownQuestItems.Visible = _config.QuestHelper;
            swUnknownQuestItems.Checked = _config.UnknownQuestItems;
            swAimview.Checked = _config.AimviewEnabled;
            swExfilNames.Checked = _config.ShowExfilNames;
            swNames.Checked = _config.ShowNames;
            swHoverArmor.Checked = _config.ShowHoverArmor;
            txtTeammateID.Text = _config.PrimaryTeammateId;
            sldrAimlineLength.Value = _config.PlayerAimLineLength;
            sldrZoomDistance.Value = _config.DefaultZoom;

            sldrUIScale.Value = _config.UIScale;
            cboFont.SelectedIndex = _config.Font;
            sldrFontSize.Value = _config.FontSize;
            #endregion

            #region Memory Writing
            swMasterSwitch.Checked = _config.MasterSwitch;

            // Global Features
            mcSettingsMemoryWritingGlobal.Enabled = _config.MasterSwitch;
            swChams.Checked = _config.ChamsEnabled;
            swExtendedReach.Checked = _config.ExtendedReach;
            swFreezeTime.Checked = _config.FreezeTimeOfDay;
            sldrTimeOfDay.Visible = _config.FreezeTimeOfDay;
            sldrTimeOfDay.Value = (int)_config.TimeOfDay;
            swInfiniteStamina.Checked = _config.InfiniteStamina;

            // Gear Features
            mcSettingsMemoryWritingGear.Enabled = _config.MasterSwitch;
            swNoRecoilSway.Checked = _config.NoRecoilSway;
            swInstantADS.Checked = _config.InstantADS;
            swNoVisor.Checked = _config.NoVisor;
            swThermalVision.Checked = _config.ThermalVision;
            swOpticalThermal.Checked = _config.OpticThermalVision;
            swNightVision.Checked = _config.NightVision;

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

            sldrMagDrillsSpeed.Visible = _config.MaxSkills["Mag Drills"];
            sldrMagDrillsSpeed.Value = _config.MagDrillSpeed;
            sldrJumpStrength.Visible = _config.MaxSkills["Strength"];
            sldrJumpStrength.Value = _config.JumpPowerStrength;
            sldrThrowStrength.Visible = _config.MaxSkills["Strength"];
            sldrThrowStrength.Value = _config.ThrowPowerStrength;

            // Thermal Features
            mcSettingsMemoryWritingThermal.Enabled = _config.MasterSwitch;
            mcSettingsMemoryWritingThermal.Visible = _config.ThermalVision || _config.OpticThermalVision;
            cboThermalType.SelectedIndex = _config.ThermalVision ? 0 : (_config.OpticThermalVision ? 1 : 0);
            cboThermalColorScheme.SelectedIndex = _config.ThermalVision ? _config.MainThermalSetting.ColorScheme : (_config.OpticThermalVision ? _config.OpticThermalSetting.ColorScheme : 0);
            sldrThermalColorCoefficient.Value = (int)(_config.MainThermalSetting.ColorCoefficient * 100);
            sldrMinTemperature.Value = (int)((_config.MainThermalSetting.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            sldrThermalRampShift.Value = (int)((_config.MainThermalSetting.RampShift + 1.0f) * 100.0f);
            #endregion

            #region Loot
            // General
            swProcessLoot.Checked = _config.ProcessLoot;
            swFilteredOnly.Checked = _config.ImportantLootOnly;
            swSubItems.Checked = _config.ShowSubItems;
            swItemValue.Checked = _config.ShowLootValue;
            swCorpses.Checked = _config.ShowCorpses;
            swAutoRefresh.Enabled = _config.ProcessLoot;
            swAutoRefresh.Checked = _config.AutoLootRefresh;
            cboAutoRefreshMap.Enabled = _config.ProcessLoot;
            cboAutoRefreshMap.Visible = _config.AutoLootRefresh;
            sldrAutoRefreshDelay.Enabled = _config.ProcessLoot;
            sldrAutoRefreshDelay.Visible = _config.AutoLootRefresh;

            // Minimum Loot Value
            sldrMinRegularLoot.Value = _config.MinLootValue / 1000;
            sldrMinImportantLoot.Value = _config.MinImportantLootValue / 1000;
            sldrMinCorpse.Value = _config.MinCorpseValue / 1000;
            sldrMinSubItems.Value = _config.MinSubItemValue / 1000;
            #endregion
            #endregion

            InitiateAutoMapRefreshItems();
            InitiateFactions();
            InitiateLootFilter();
            InitiateWatchlist();
            InitiateColors();
            InitiateFont();
            InitiateUIScaling();
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
                GenerateCards(flpPlayerLoadoutsAI, x => x.IsHostileActive && !x.IsHuman, x => x.Type == PlayerType.Boss, x => x.IsBossRaider, x => x.Value);

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
                        var gearLabel = new MaterialLabel();
                        gearLabel.Text = $"{GearManager.GetGearSlotName(slot.Key)}: {slot.Value.Long}";
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
            float zoomFactor = 0.01f * sldrZoomDistance.Value;
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
                    return GetMapParameters(localPlayerMapPos);
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

                    // Draw LocalPlayer

                    var localPlayerZoomedPos = localPlayerMapPos.ToZoomedPos(mapParams);
                    localPlayerZoomedPos.DrawPlayerMarker(
                        canvas,
                        localPlayer,
                        sldrAimlineLength.Value,
                        null
                    );


                    foreach (var player in allPlayers) // Draw PMCs
                    {
                        if (player.Type == PlayerType.LocalPlayer || !player.IsAlive)
                            continue; // Already drawn current player, move on

                        var playerPos = player.Position;
                        var playerMapPos = playerPos.ToMapPos(_selectedMap);
                        var playerZoomedPos = playerMapPos.ToZoomedPos(mapParams);

                        player.ZoomedPosition = new Vector2() // Cache Position as Vec2 for MouseMove event
                        {
                            X = playerZoomedPos.X,
                            Y = playerZoomedPos.Y
                        };

                        int aimlineLength = 15;

                        if (player.Type is not PlayerType.Teammate)
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
                        else if (player.Type is PlayerType.Teammate)
                        {
                            aimlineLength = sldrAimlineLength.Value; // Allies use player's aim length
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
                string[] lines = null;
                var height = playerZoomedPos.Height - localPlayerMapPos.Height;

                var dist = Vector3.Distance(this.LocalPlayer.Position, player.Position);

                if (_config.ShowNames) // show full names & info
                {
                    lines = new string[2]
                    {
                        string.Empty,
                        $"{(int)Math.Round(height)},{(int)Math.Round(dist)}"
                    };

                    string name = player.Name;

                    if (player.ErrorCount > 10)
                        name = "ERROR"; // In case POS stops updating, let us know!

                    if ((player.IsHuman || player.IsBossRaider) && player.Health != -1)
                        lines[0] += $"{name} ({player.Health})";
                    else
                        lines[0] += $"{name}";
                }
                else // just height & hp (for humans)
                {
                    lines = new string[1] { $"{(int)Math.Round(height)},{(int)Math.Round(dist)}" };

                    if ((player.IsHuman || player.IsBossRaider) && player.Health != -1)
                        lines[0] += $" ({player.Health})";
                    if (player.ErrorCount > 10)
                        lines[0] = "ERROR"; // In case POS stops updating, let us know!
                }

                playerZoomedPos.DrawPlayerText(
                    canvas,
                    player,
                    lines,
                    mouseOverGrp
                );

                playerZoomedPos.DrawPlayerMarker(
                    canvas,
                    player,
                    aimlineLength,
                    mouseOverGrp
                );
            }
        }

        private void DrawLoot(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                if (_config.ProcessLoot && _config.ShowLoot) // Draw loot (if enabled)
                {
                    var loot = this.Loot;
                    if (loot is not null)
                    {
                        if (loot.Filter is null)
                        {
                            loot.ApplyFilter();
                        }

                        var filter = loot.Filter;

                        if (filter is not null)
                        {
                            var localPlayerMapPos = localPlayer.Position.ToMapPos(_selectedMap);
                            var mapParams = GetMapLocation();

                            foreach (var item in filter)
                            {
                                if (item is null || (this._config.ImportantLootOnly && !item.Important && !item.AlwaysShow) || (item is LootCorpse && !this._config.ShowCorpses))
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
                        if (questItems is not null)
                        {
                            var items = _config.UnknownQuestItems ? questItems.Where(x => x?.Position.X != 0 && x?.Name == "????") : questItems.Where(x => x?.Position.X != 0 && x?.Name != "????");
                            foreach (var item in items)
                            {
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
                        if (questZones is not null)
                        {
                            foreach (var zone in questZones.Where(x => x.MapName.ToLower() == _selectedMap.Name.ToLower()))
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
            if (_config.AimviewEnabled)
            {
                var aimviewPlayers = this.AllPlayers?
                    .Select(x => x.Value)
                    .Where(x => x.IsActive && x.IsAlive);

                if (aimviewPlayers is not null)
                {
                    var localPlayerAimviewBounds = new SKRect()
                    {
                        Left = _mapCanvas.Left,
                        Right = _mapCanvas.Left + _aimviewWindowSize,
                        Bottom = _mapCanvas.Bottom,
                        Top = _mapCanvas.Bottom - _aimviewWindowSize
                    };

                    var primaryTeammateAimviewBounds = new SKRect()
                    {
                        Left = _mapCanvas.Right - _aimviewWindowSize,
                        Right = _mapCanvas.Right,
                        Bottom = _mapCanvas.Bottom,
                        Top = _mapCanvas.Bottom - _aimviewWindowSize
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
                }
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
            {
                ShowErrorDialog("Invalid value(s) provided in the map setup textboxes.");
            }
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

                if (_config.ProcessLoot && _config.ShowLoot)
                    _closestItemToMouse = FindClosestObject(loot, mouse, x => x.ZoomedPosition, 12 * _uiScale);
                else
                    ClearItemRefs();

                _closestTaskItemToMouse = FindClosestObject(tasksItems, mouse, x => x.ZoomedPosition, 12 * _uiScale);
                if (_closestTaskItemToMouse == null)
                    ClearTaskItemRefs();

                _closestTaskZoneToMouse = FindClosestObject(tasksZones, mouse, x => x.ZoomedPosition, 12);
                if (_closestTaskZoneToMouse == null)
                    ClearTaskZoneRefs();
            }
            else if (this.InGame && Memory.LocalPlayer is null)
            {
                ClearPlayerRefs();
                ClearItemRefs();
                ClearTaskItemRefs();
                ClearTaskZoneRefs();
            }

            if (this._isDragging && this._isFreeMapToggled)
            {
                if (!this._lastMousePosition.IsEmpty)
                {
                    int dx = e.X - this._lastMousePosition.X;
                    int dy = e.Y - this._lastMousePosition.Y;

                    float sensitivityFactor = DragSensitivity;
                    dx = (int)(dx * sensitivityFactor);
                    dy = (int)(dy * sensitivityFactor);

                    this.targetPanPosition.X -= dx;
                    this.targetPanPosition.Y -= dy;

                    if (!this.panTimer.Enabled)
                        this.panTimer.Start();
                }

                this._lastMousePosition = e.Location;
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

                        DrawPlayers(canvas);

                        if (!_config.ShowLoot && _config.ShowCorpses)
                            DrawCorpses(canvas);

                        if (_config.ProcessLoot && _config.ShowLoot)
                            DrawLoot(canvas);

                        if (_config.QuestHelper)
                            DrawQuestItems(canvas);

                        DrawGrenades(canvas);

                        DrawExfils(canvas);

                        if (_config.AimviewEnabled)
                            DrawAimview(canvas);

                        DrawToolTips(canvas);
                    }
                }
                else
                {
                    DrawStatusText(canvas);
                }

                canvas.Flush();
            }
            catch { }
        }

        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            ToggleMap();
        }

        private void PanTimer_Tick(object sender, EventArgs e)
        {
            var panDifference = new SKPoint(
                this.targetPanPosition.X - this._mapPanPosition.X,
                this.targetPanPosition.Y - this._mapPanPosition.Y
            );

            if (panDifference.Length > 0.1)
            {
                this._mapPanPosition.X += (float)(panDifference.X * PanSmoothness);
                this._mapPanPosition.Y += (float)(panDifference.Y * PanSmoothness);
            }
            else
            {
                this.panTimer.Stop();
            }
        }

        private void ZoomTimer_Tick(object sender, EventArgs e)
        {
            int zoomDifference = this.targetZoomValue - sldrZoomDistance.Value;

            if (zoomDifference != 0)
            {
                int zoomStep = Math.Sign(zoomDifference);
                sldrZoomDistance.Value += zoomStep;
            }
            else
            {
                this.zoomTimer.Stop();
            }
        }

        private bool ZoomIn(int amt)
        {
            this.targetZoomValue = Math.Max(sldrZoomDistance.RangeMin, sldrZoomDistance.Value - amt);
            this.zoomTimer.Start();

            return true;
        }

        private bool ZoomOut(int amt)
        {
            this.targetZoomValue = Math.Min(sldrZoomDistance.RangeMax, sldrZoomDistance.Value + amt);
            this.zoomTimer.Start();

            return false;
        }

        private void swRadarStats_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarStats.Checked;
            _config.ShowRadarStats = enabled;
            mcRadarStats.Visible = enabled;
        }
        #endregion
        #endregion

        #region Settings
        #region General
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

        private void swShowLoot_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowLoot = swShowLoot.Checked;
        }

        private void swQuestHelper_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swQuestHelper.Checked;
            _config.QuestHelper = enabled;
            swUnknownQuestItems.Visible = enabled;
        }

        private void swUnknownQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.UnknownQuestItems = swUnknownQuestItems.Checked;
        }

        private void swAimview_CheckedChanged(object sender, EventArgs e)
        {
            _config.AimviewEnabled = swAimview.Checked;
        }

        private void swExfilNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowExfilNames = swExfilNames.Checked;
        }

        private void swNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowNames = swNames.Checked;
        }

        private void swHoverArmor_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowHoverArmor = swHoverArmor.Checked;
        }

        private void sldrZoomDistance_onValueChanged(object sender, int newValue)
        {
            _config.DefaultZoom = newValue;
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

        private void swRadarEnemyCount_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarEnemyCount.Checked;

            _config.EnemyCount = enabled;
            mcRadarEnemyStats.Visible = enabled;
        }

        private void cboFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            _config.Font = cboFont.SelectedIndex;
            InitiateFont();
        }

        private void sldrFontSize_onValueChanged(object sender, int newValue)
        {
            _config.FontSize = newValue;
            InitiateFontSize();
        }
        #endregion
        #endregion

        #region Memory Writing
        #region Helper Functions
        private ThermalSettings GetSelectedThermalSetting()
        {
            return cboThermalType.SelectedItem?.ToString() == "Main" ? _config.MainThermalSetting : _config.OpticThermalSetting;
        }
        #endregion
        #region Event Handlers
        private void swChams_CheckedChanged(object sender, EventArgs e)
        {
            _config.ChamsEnabled = swChams.Checked;
        }

        private void swExtendedReach_CheckedChanged(object sender, EventArgs e)
        {
            _config.ExtendedReach = swExtendedReach.Checked;
        }

        private void swFreezeTime_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swFreezeTime.Checked;
            _config.FreezeTimeOfDay = enabled;

            sldrTimeOfDay.Visible = enabled;
        }

        private void sldrTimeOfDay_onValueChanged(object sender, int newValue)
        {
            _config.TimeOfDay = (float)sldrTimeOfDay.Value;
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

            mcSettingsMemoryWritingThermal.Visible = enabled || _config.OpticThermalVision;
        }

        private void swOpticalThermal_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swOpticalThermal.Checked;
            _config.OpticThermalVision = enabled;

            mcSettingsMemoryWritingThermal.Visible = enabled || _config.ThermalVision;
        }

        private void swNightVision_CheckedChanged(object sender, EventArgs e)
        {
            _config.NightVision = swNightVision.Checked;
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

        private void sldrJumpStrength_onValueChanged(object sender, int newValue)
        {
            _config.JumpPowerStrength = newValue;

            if (_config.MaxSkills["Strength"] && Memory.LocalPlayer is not null)
            {
                var jumpHeightSkill = Memory.PlayerManager.Skills["Strength"]["BuffJumpHeightInc"];
                jumpHeightSkill.MaxValue = 0.2f + ((float)newValue / 100);

                Memory.PlayerManager?.SetMaxSkill(jumpHeightSkill);
            }
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

            sldrThrowStrength.Visible = enabled;
            sldrJumpStrength.Visible = enabled;
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
            sldrMagDrillsSpeed.Visible = enabled;
        }

        private void swMaxLightVests_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Light Vests"] = swMaxLightVests.Checked;
        }

        private void swMaxHeavyVests_CheckedChanged(object sender, EventArgs e)
        {
            _config.MaxSkills["Heavy Vests"] = swMaxHeavyVests.Checked;
        }
        #endregion
        #endregion

        #region Loot
        #region Event Handlers
        // General
        private void swProcessLoot_CheckedChanged(object sender, EventArgs e)
        {
            var processLoot = swProcessLoot.Checked;
            _config.ProcessLoot = processLoot;

            swShowLoot.Enabled = processLoot;
            swAutoRefresh.Enabled = processLoot;
            cboAutoRefreshMap.Enabled = processLoot;
            sldrAutoRefreshDelay.Enabled = processLoot;

            if (!processLoot)
                return;

            if (_config.AutoLootRefresh)
                Memory.Loot?.StartAutoRefresh();
            else
                Memory.Loot?.RefreshLoot(true);
        }

        private void btnRefreshLoot_Click(object sender, EventArgs e)
        {
            Memory.Loot?.RefreshLoot(true);
        }

        private void swFilteredOnly_CheckedChanged(object sender, EventArgs e)
        {
            _config.ImportantLootOnly = swFilteredOnly.Checked;
        }

        private void swSubItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowSubItems = swSubItems.Checked;

            Memory.Loot?.ApplyFilter();
        }

        private void swItemValue_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowLootValue = swItemValue.Checked;
        }

        private void swCorpses_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowCorpses = swCorpses.Checked;
        }

        private void swAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAutoRefresh.Checked;
            _config.AutoLootRefresh = enabled;

            cboAutoRefreshMap.Visible = enabled;
            sldrAutoRefreshDelay.Visible = enabled;

            if (enabled)
                Memory.Loot?.StartAutoRefresh();
            else
                Memory.Loot?.StopAutoRefresh();
        }

        private void cboAutoRefreshMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !_config.AutoRefreshSettings.ContainsKey(mapName))
                return;

            sldrAutoRefreshDelay.Value = _config.AutoRefreshSettings[mapName];
        }

        private void sldrAutoRefreshDelay_onValueChanged(object sender, int newValue)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !_config.AutoRefreshSettings.ContainsKey(mapName))
                return;

            if (newValue != _config.AutoRefreshSettings[mapName])
            {
                _config.AutoRefreshSettings[mapName] = newValue;
            }
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
                selectedFilter.Color = new LootFilterManager.Filter.Colors
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
        private void txtLootFilterItemToSearch_KeyDown(object sender, KeyEventArgs e)
        {
            var itemToSearch = txtLootFilterItemToSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemToSearch))
                return;

            var lootList = TarkovDevManager.AllItems.Values
                .Where(x => x.Name.Contains(itemToSearch, StringComparison.OrdinalIgnoreCase))
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