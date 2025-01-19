using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using MaterialSkin;
using MaterialSkin.Controls;
using eft_dma_radar.Properties;
using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using System.Globalization;
using Offsets;
using System.Security.Policy;
using System.ComponentModel;
using System.Collections.Generic;
using static eft_dma_radar.LootFilterManager;

namespace eft_dma_radar
{
    public partial class frmMain : MaterialForm
    {
        private readonly Config config;
        private readonly Watchlist watchlist;
        private readonly LootFilterManager lootFilterManager;
        private readonly AIFactionManager aiFactions;
        private readonly SKGLControl mapCanvas;
        private readonly Stopwatch fpsWatch = new();
        private readonly object renderLock = new();
        private readonly object loadMapBitmapsLock = new();
        private readonly System.Timers.Timer mapChangeTimer = new(900);
        private List<Map> maps = new();
        private static readonly string[] FONTS_TO_USE = {
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

        private bool isFreeMapToggled = false;
        private float uiScale = 1.0f;
        private Player closestPlayerToMouse = null;
        private LootableObject closestItemToMouse = null;
        private QuestItem closestTaskItemToMouse = null;
        private QuestZone closestTaskZoneToMouse = null;

        private Point lastMousePosition = Point.Empty;
        private int? mouseOverGroup = null;
        private int fps = 0;
        private int mapSelectionIndex = 0;
        private Map selectedMap;
        private SKBitmap[] loadedBitmaps;
        private MapPosition mapPanPosition = new();
        private Watchlist.Entry lastWatchlistEntry;
        private string lastFactionEntry;
        private Hotkey lastHotkeyEntry;
        private List<Player> watchlistMatchPlayers = new();

        private bool hotkeyUpdating = false;

        private const int TARGET_FPS = 144;
        private const int FRAME_DELAY = 1000 / TARGET_FPS;
        private const int MIN_AIMVIEW_SIZE = 100;
        private const int MAX_AIMVIEW_SIZE = 4000;

        private SKPoint currentPanPosition;
        private SKPoint targetPanPosition;

        private float currentZoom = 1f;
        private float targetZoom = 1f;
        private bool isZooming = false;
        private bool isPanning = false;
        private bool isDraggingMap = false;
        private bool isDraggingAimview = false;
        private bool isResizingAimview = false;

        private const float MAX_ZOOM = 0.5f;
        private const float MIN_ZOOM = 10f;
        private const float DRAG_SENSITIVITY = 1.5f;
        private const float PAN_SENSITIVITY = 0.3f;

        private Point aimviewMouseDownPosition;

        private const int AIMVIEW_OBJECT_SPACING = 8;

        private MapParameters cachedMapParams;
        private DateTime lastMapParamsUpdate = DateTime.MinValue;

        private int lastLootItemCount = -1;

        private List<ItemAnimation> activeItemAnimations = new List<ItemAnimation>();
        private LootItem itemToPing = null;

        private bool isItemPingAnimationRunning = false;
        private bool isRefreshingLootItems = false;

        private System.Timers.Timer inputCheckTimer;
        private System.Timers.Timer itemPingAnimationTimer;
        private System.Timers.Timer playerlistUpdateTimer;

        private HashSet<Keys> previouslyPressedKeys = new HashSet<Keys>();
        private Dictionary<HotkeyAction, Action<bool>> hotkeyActions;
        private Dictionary<Keys, string> keyDisplayNames;
        private readonly string[] hotkeyCboActions =
        {
            "Chams",
            "Important Loot",
            "Optical Thermal",
            "Recoil",
            "Show Containers",
            "Show Corpses",
            "Show Loot",
            "Thirdperson",
            "Thermal Vision",
            "Time Scale",
            "Weapon Sway",
            "Zoom In",
            "Zoom Out"
        };

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
        private List<Grenade> Grenades
        {
            get => Memory.Grenades;
        }

        /// <summary>
        /// Contains all 'Hot' grenades in Local Game World, and their position(s).
        /// </summary>
        private List<Tripwire> Tripwires
        {
            get => Memory.Tripwires;
        }

        /// <summary>
        /// Contains all 'Exfils' in Local Game World, and their status/position(s).
        /// </summary>
        private List<Exfil> Exfils
        {
            get => Memory.Exfils;
        }

        private List<Transit> Transits
        {
            get => Memory.Transits;
        }

        /// <summary>
        /// Contains all information related to quests
        /// </summary>
        private QuestManager QuestManager
        {
            get => Memory.QuestManager;
        }

        private List<PlayerCorpse> Corpses
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
            this.config = Program.Config;
            this.watchlist = Program.Watchlist;
            this.lootFilterManager = Program.LootFilterManager;
            this.aiFactions = Program.AIFactionManager;

            this.InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.EnforceBackcolorOnAllComponents = true;
            materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.Grey800, Primary.Grey800, Primary.Indigo100, Accent.Orange400, TextShade.WHITE);

            this.mapCanvas = skMapCanvas;
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
            this.Loot?.StopAutoRefresh();
            Memory.Toolbox?.StopToolbox();

            Config.SaveConfig(this.config); // Save Config to Config.json
            Memory.Shutdown(); // Wait for Memory Thread to gracefully exit
            e.Cancel = false; // Ready to close
            base.OnFormClosing(e); // Proceed with closing
        }

        /// <summary>
        /// Process hotkey presses.sc
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => keyData switch
        {
            Keys.F1 => this.ZoomIn(5, Cursor.Position),
            Keys.F2 => this.ZoomOut(5),
            Keys.F4 => swAimview.Checked = !swAimview.Checked,
            Keys.F5 => this.ToggleMap(),
            Keys.Control | Keys.N => swNightVision.Checked = !swNightVision.Checked,
            Keys.Control | Keys.T => swThermalVision.Checked = !swThermalVision.Checked,
            _ => base.ProcessCmdKey(ref msg, keyData),
        };

        /// <summary>
        /// Process mousewheel events.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                if (e.Delta > 0)
                    this.ZoomIn(5, e.Location);
                else
                    this.ZoomOut(5);

                return;
            }

            base.OnMouseWheel(e);
        }
        #endregion

        #region GUI Events / Functions
        #region General Helper Functions
        private void InitializeInputCheckTimer()
        {
            if (InputManager.IsManagerLoaded)
            {
                this.inputCheckTimer = new System.Timers.Timer(25);
                this.inputCheckTimer.Elapsed += this.InputCheckTimer_Tick;
                this.inputCheckTimer.Start();
            }
        }

        private void InitializePlayerlistTimer()
        {
            this.playerlistUpdateTimer = new System.Timers.Timer(5000);
            this.playerlistUpdateTimer.Elapsed += this.PlayerListUpdateTimer_Tick;
            this.playerlistUpdateTimer.Start();
        }

        private void InitializeDoubleBuffering()
        {
            this.DoubleBuffered = true;

            this.mapCanvas.PaintSurface += (sender, e) =>
            {
                e.Surface.Canvas.Clear();
                this.skMapCanvas_PaintSurface(sender, e);
            };
        }

        private async void AnimateZoomAndPan()
        {
            while (Math.Abs(this.currentZoom - this.targetZoom) > 0.001f ||
                   SKPoint.Distance(this.currentPanPosition, this.targetPanPosition) > 0.1f)
            {
                this.currentZoom = this.currentZoom + (targetZoom - currentZoom) * 0.1f;
                this.currentPanPosition = new SKPoint(
                    currentPanPosition.X + (targetPanPosition.X - currentPanPosition.X) * 0.1f,
                    currentPanPosition.Y + (targetPanPosition.Y - currentPanPosition.Y) * 0.1f
                );

                this.config.DefaultZoom = (int)(currentZoom * 100);

                this.mapPanPosition = new MapPosition()
                {
                    X = currentPanPosition.X,
                    Y = currentPanPosition.Y,
                    Height = mapPanPosition.Height
                };

                this.InvalidateMapParams();
                await Task.Delay(FRAME_DELAY);
                this.mapCanvas.Invalidate();
            }

            this.currentZoom = this.targetZoom;
            this.currentPanPosition = this.targetPanPosition;
            this.isZooming = false;
            this.isPanning = false;
            this.InvalidateMapParams();
        }

        private async void AdjustPanPositionForZoom(Point cursorPosition, float zoomAmount)
        {
            var mapParams = this.GetMapLocation();
            var cursorMapPos = new SKPoint(
                mapParams.Bounds.Left + cursorPosition.X / mapParams.XScale,
                mapParams.Bounds.Top + cursorPosition.Y / mapParams.YScale
            );

            var panToCursorVector = new SKPoint(
                cursorMapPos.X - this.currentPanPosition.X,
                cursorMapPos.Y - this.currentPanPosition.Y
            );

            this.targetPanPosition = new SKPoint(
                this.currentPanPosition.X + panToCursorVector.X * PAN_SENSITIVITY * Math.Abs(zoomAmount),
                this.currentPanPosition.Y + panToCursorVector.Y * PAN_SENSITIVITY * Math.Abs(zoomAmount)
            );
        }

        private bool IsInViewport(MapPosition mapPosition, MapParameters mapParams)
        {
            var zoomedPos = mapPosition.ToZoomedPos(mapParams);
            return zoomedPos.X >= 0 && zoomedPos.X <= this.mapCanvas.Width &&
                   zoomedPos.Y >= 0 && zoomedPos.Y <= this.mapCanvas.Height;
        }

        private bool ToggleMap()
        {
            if (!btnToggleMap.Enabled)
                return false;

            if (this.mapSelectionIndex == this.maps.Count - 1)
                this.mapSelectionIndex = 0; // Start over when end of maps reached
            else
                this.mapSelectionIndex++; // Move onto next map

            tabRadar.Text = $"Radar ({this.maps[this.mapSelectionIndex].Name})";
            mapChangeTimer.Restart(); // Start delay

            return true;
        }

        private void InitializeWatchlist()
        {
            if (this.watchlist.Profiles.Count == 0)
                this.watchlist.AddEmptyProfile();

            this.UpdateWatchlistProfiles();
        }

        private void InitializeColors()
        {
            this.UpdatePaintColorControls();
            this.UpdateThemeColors();
        }

        private void InitializeLootFilter()
        {
            if (this.config.Filters.Count == 0)
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

                this.config.Filters.Add(newFilter);
                LootFilterManager.SaveLootFilterManager(this.lootFilterManager);
            }

            cboLootFilterItemsToAdd.Items.AddRange(TarkovDevManager.AllItems.Select(x => x.Value).OrderBy(x => x.Name).Take(25).ToArray());
            cboLootFilterItemsToAdd.DisplayMember = "Name";

            this.UpdateLootFilters();
        }

        private void InitializeFactions()
        {
            if (this.aiFactions.Factions.Count == 0)
                this.aiFactions.AddEmptyFaction();

            this.UpdateFactionPlayerTypes();
            this.UpdateFactions();
        }

        private void InitializeAutoMapRefreshItems()
        {
            cboAutoRefreshMap.Items.AddRange(this.config.LootItemRefreshSettings.Keys.ToArray());
            cboAutoRefreshMap.SelectedIndex = this.selectedMap is not null ? cboAutoRefreshMap.FindStringExact(this.selectedMap.Name) : 0;
        }

        private void InitializeUIScaling()
        {
            this.uiScale = (.01f * this.config.UIScale);

            #region Update Paints/Text
            SKPaints.TextBaseOutline.StrokeWidth = 2 * this.uiScale;
            SKPaints.TextRadarStatus.TextSize = 48 * this.uiScale;
            SKPaints.PaintBase.StrokeWidth = 3 * this.uiScale;
            SKPaints.PaintMouseoverGroup.StrokeWidth = 3 * this.uiScale;
            SKPaints.PaintDeathMarker.StrokeWidth = 3 * this.uiScale;
            SKPaints.LootPaint.StrokeWidth = 3 * this.uiScale;
            SKPaints.PaintTransparentBacker.StrokeWidth = 1 * this.uiScale;
            SKPaints.PaintAimviewCrosshair.StrokeWidth = 1 * this.uiScale;
            SKPaints.PaintGrenades.StrokeWidth = 3 * this.uiScale;
            SKPaints.PaintExfilOpen.StrokeWidth = 1 * this.uiScale;
            SKPaints.PaintExfilPending.StrokeWidth = 1 * this.uiScale;
            SKPaints.PaintExfilClosed.StrokeWidth = 1 * this.uiScale;
            #endregion

            this.InitializeFontSizes();
        }

        private void InitializeFonts()
        {
            var fontToUse = SKTypeface.FromFamilyName(cboGlobalFont.Text);
            SKPaints.TextMouseoverGroup.Typeface = fontToUse;
            SKPaints.TextBase.Typeface = fontToUse;
            SKPaints.LootText.Typeface = fontToUse;
            SKPaints.TextBaseOutline.Typeface = fontToUse;
            SKPaints.TextRadarStatus.Typeface = fontToUse;
        }

        private void InitializeSKColors()
        {
            foreach (var paintColor in this.config.PaintColors)
            {
                var value = paintColor.Value;
                var color = new SKColor(value.R, value.G, value.B, value.A);

                Extensions.SKColors.Add(paintColor.Key, color);
            }
        }

        private void InitializeFontSizes()
        {
            SKPaints.TextMouseoverGroup.TextSize = this.config.GlobalFontSize * this.uiScale;
            SKPaints.TextBase.TextSize = this.config.GlobalFontSize * this.uiScale;
            SKPaints.LootText.TextSize = this.config.GlobalFontSize * this.uiScale;
            SKPaints.TextBaseOutline.TextSize = this.config.GlobalFontSize * this.uiScale;

            foreach (var setting in this.config.PlayerInformationSettings)
            {
                var key = setting.Key;
                var value = setting.Value;

                Extensions.PlayerTypeTextPaints[key].TextSize = value.FontSize * this.uiScale;
                Extensions.PlayerTypeFlagTextPaints[key].TextSize = value.FlagsFontSize * this.uiScale;
            }
        }

        private void InitializeHotkeys()
        {
            cboHotkeyAction.Items.AddRange(this.hotkeyCboActions);
            this.InitializeHotkeyKeys();
            this.UpdateHotkeyEntriesList();
        }

        private void InitializeHotkeyKeys()
        {
            var keyItems = new List<HotkeyKey>();
            this.keyDisplayNames = new Dictionary<Keys, string>();

            for (int i = 0; i <= 9; i++)
            {
                var key = (Keys)(48 + i);
                var displayName = i.ToString();
                keyItems.Add(new HotkeyKey(displayName, key));
                this.keyDisplayNames[key] = displayName;
            }

            for (int i = 1; i <= 12; i++)
            {
                var key = (Keys)(111 + i);
                var displayName = $"F{i}";
                keyItems.Add(new HotkeyKey(displayName, key));
                this.keyDisplayNames[key] = displayName;
            }

            for (char c = 'A'; c <= 'Z'; c++)
            {
                var key = (Keys)c;
                var displayName = c.ToString();
                keyItems.Add(new HotkeyKey(displayName, key));
                this.keyDisplayNames[key] = displayName;
            }

            var specialKeys = new[]
            {
                new HotkeyKey("Space", Keys.Space),
                new HotkeyKey("Enter", Keys.Enter),
                new HotkeyKey("Shift", Keys.Shift),
                new HotkeyKey("Ctrl", Keys.Control),
                new HotkeyKey("Alt", Keys.Menu),
                new HotkeyKey("Mouse1", Keys.LButton),
                new HotkeyKey("Mouse2", Keys.RButton),
                new HotkeyKey("Mouse3", Keys.MButton),
                new HotkeyKey("Mouse4", Keys.XButton1),
                new HotkeyKey("Mouse5", Keys.XButton2),
                new HotkeyKey("Left", Keys.Left),
                new HotkeyKey("Right", Keys.Right),
                new HotkeyKey("Up", Keys.Up),
                new HotkeyKey("Down", Keys.Down),
                new HotkeyKey("Insert", Keys.Insert),
                new HotkeyKey("Home", Keys.Home),
                new HotkeyKey("Page Up", Keys.PageUp),
                new HotkeyKey("Page Down", Keys.PageDown),
                new HotkeyKey("Delete", Keys.Delete),
                new HotkeyKey("End", Keys.End),
                new HotkeyKey("Num 0", Keys.NumPad0),
                new HotkeyKey("Num 1", Keys.NumPad1),
                new HotkeyKey("Num 2", Keys.NumPad2),
                new HotkeyKey("Num 3", Keys.NumPad3),
                new HotkeyKey("Num 4", Keys.NumPad4),
                new HotkeyKey("Num 5", Keys.NumPad5),
                new HotkeyKey("Num 6", Keys.NumPad6),
                new HotkeyKey("Num 7", Keys.NumPad7),
                new HotkeyKey("Num 8", Keys.NumPad8),
                new HotkeyKey("Num 9", Keys.NumPad9),
                new HotkeyKey("Num .", Keys.Decimal),
                new HotkeyKey("Num +", Keys.Add),
                new HotkeyKey("Num -", Keys.Subtract),
                new HotkeyKey("Num *", Keys.Multiply),
                new HotkeyKey("Num /", Keys.Divide)
            };

            keyItems.AddRange(specialKeys);

            foreach (var item in specialKeys)
            {
                this.keyDisplayNames[item.Key] = item.Name;
            }

            cboHotkeyKey.DataSource = keyItems.ToList();
            cboHotkeyKey.DisplayMember = "Name";
            cboHotkeyKey.ValueMember = "Key";
            cboHotkeyKey.SelectedIndex = -1;
        }

        private void InitializeHotkeyActions()
        {
            this.hotkeyActions = new Dictionary<HotkeyAction, Action<bool>>
            {
                { HotkeyAction.Chams, this.SetChams },
                { HotkeyAction.ImportantLoot, this.SetImportantLootOnly },
                { HotkeyAction.OpticalThermal, this.SetOpticalThermal },
                { HotkeyAction.Recoil, this.SetRecoil },
                { HotkeyAction.ShowContainers, this.SetShowContainers },
                { HotkeyAction.ShowCorpses, this.SetShowCorpses },
                { HotkeyAction.ShowLoot, this.SetShowLoot },
                { HotkeyAction.Thirdperson, this.SetThirdperson },
                { HotkeyAction.ThermalVision, this.SetThermalVision },
                { HotkeyAction.TimeScale, this.SetTimescale },
                { HotkeyAction.WeaponSway, this.SetWeaponSway },
            };
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
            if (configs.Length == 0)
                throw new IOException("No .json map configs found!");

            this.maps = configs.AsParallel().Select(config =>
            {
                var name = Path.GetFileNameWithoutExtension(config.Name);
                var mapConfig = MapConfig.LoadFromFile(config.FullName);
                var mapID = mapConfig.MapID[0];
                var map = new Map(name.ToUpper(), mapConfig, config.FullName, mapID);
                map.ConfigFile.MapLayers = map.ConfigFile.MapLayers.OrderBy(x => x.MinHeight).ToList();
                return map;
            }).ToList();
        }

        private void CheckConfigDictionaries()
        {
            this.UpdateDictionary(this.config.PaintColors, Config.DefaultPaintColors);
            this.UpdateDictionary(this.config.LootItemRefreshSettings, Config.DefaultAutoRefreshSettings);
            this.UpdateDictionary(this.config.Chams, Config.DefaultChamsSettings);
            this.UpdateDictionary(this.config.LootContainerSettings, Config.DefaultContainerSettings);
            this.UpdateDictionary(this.config.LootPing, Config.DefaultLootPingSettings);
            this.UpdateDictionary(this.config.MaxSkills, Config.DefaultMaxSkillsSettings);
            this.UpdateDictionary(this.config.PlayerInformationSettings, Config.DefaultPlayerInformationSettings);
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
            this.InitializeSKColors();
            this.SetupFonts();

            #region Settings
            #region General
            // Radar
            swRadarStats.Checked = this.config.RadarStats;
            mcRadarStats.Visible = this.config.RadarStats;
            swRadarVsync.Checked = this.config.VSync;
            swRadarEnemyCount.Checked = this.config.EnemyCount;
            mcRadarEnemyStats.Visible = this.config.EnemyCount;
            mcRadarLootItemViewer.Visible = this.config.LootItemViewer;
            swPvEMode.Checked = this.config.PvEMode;

            btnTriggerUnityCrash.Visible = this.config.PvEMode;

            // User Interface
            swExfilNames.Checked = this.config.ExfilNames;
            swHoverArmor.Checked = this.config.HoverArmor;
            swTraderPrices.Checked = this.config.TraderPrices;
            txtTeammateID.Text = this.config.PrimaryTeammateId;
            sldrZoomSensitivity.Value = this.config.ZoomSensitivity;

            sldrUIScale.Value = this.config.UIScale;
            cboGlobalFont.SelectedIndex = this.config.GlobalFont;
            sldrFontSize.Value = this.config.GlobalFontSize;

            // Aimview
            var aimviewSettings = this.config.AimviewSettings;
            swAimview.Checked = aimviewSettings.Enabled;
            sldrAVWidth.Value = aimviewSettings.Width;
            sldrAVHeight.Value = aimviewSettings.Height;
            txtTeammateID.Text = aimviewSettings.TeammateID;

            var playerSettings = aimviewSettings.ObjectSettings["Player"];
            swAVPlayers.Checked = playerSettings.Enabled;
            swAVPlayerDistance.Checked = playerSettings.Distance;
            sldrAVPlayerPaintDistance.Value = playerSettings.PaintDistance;
            sldrAVPlayerTextDistance.Value = playerSettings.TextDistance;

            var looseLootSettings = aimviewSettings.ObjectSettings["LooseLoot"];
            swAVLooseLoot.Checked = looseLootSettings.Enabled;
            swAVLooseLootDistance.Checked = looseLootSettings.Distance;
            swAVLooseLootName.Checked = looseLootSettings.Name;
            swAVLooseLootValue.Checked = looseLootSettings.Value;
            sldrAVLooseLootPaintDistance.Value = looseLootSettings.PaintDistance;
            sldrAVLooseLootTextDistance.Value = looseLootSettings.TextDistance;

            var corpseSettings = aimviewSettings.ObjectSettings["Corpse"];
            swAVCorpses.Checked = corpseSettings.Enabled;
            swAVCorpseDistance.Checked = corpseSettings.Distance;
            swAVCorpseName.Checked = corpseSettings.Name;
            swAVCorpseValue.Checked = corpseSettings.Value;
            sldrAVCorpsePaintDistance.Value = corpseSettings.PaintDistance;
            sldrAVCorpseTextDistance.Value = corpseSettings.TextDistance;

            var questItemSettings = aimviewSettings.ObjectSettings["QuestItem"];
            swAVQuestItems.Checked = questItemSettings.Enabled;
            swAVQuestItemDistance.Checked = questItemSettings.Distance;
            swAVQuestItemName.Checked = questItemSettings.Name;
            sldrAVQuestItemPaintDistance.Value = questItemSettings.PaintDistance;
            sldrAVQuestItemTextDistance.Value = questItemSettings.TextDistance;

            var containerSettings = aimviewSettings.ObjectSettings["Container"];
            swAVContainers.Checked = containerSettings.Enabled;
            swAVContainerDistance.Checked = containerSettings.Distance;
            swAVContainerName.Checked = containerSettings.Name;
            sldrAVContainerPaintDistance.Value = containerSettings.PaintDistance;
            sldrAVContainerTextDistance.Value = containerSettings.TextDistance;

            var tripwireSettings = aimviewSettings.ObjectSettings["Tripwire"];
            swAVTripwire.Checked = tripwireSettings.Enabled;
            swAVTripwireDistance.Checked = tripwireSettings.Distance;
            sldrAVTripwirePaintDistance.Value = tripwireSettings.PaintDistance;
            sldrAVTripwireTextDistance.Value = tripwireSettings.TextDistance;

            var questZoneSettings = aimviewSettings.ObjectSettings["QuestZone"];
            swAVQuestZones.Checked = questZoneSettings.Enabled;
            swAVQuestZoneDistance.Checked = questZoneSettings.Distance;
            swAVQuestZoneName.Checked = questZoneSettings.Name;
            sldrAVQuestZoneTextDistance.Value = questZoneSettings.TextDistance;

            var exfilSettings = aimviewSettings.ObjectSettings["Exfil"];
            swAVExfils.Checked = exfilSettings.Enabled;
            swAVExfilDistance.Checked = exfilSettings.Distance;
            swAVExfilName.Checked = exfilSettings.Name;
            sldrAVExfilTextDistance.Value = exfilSettings.TextDistance;

            var transitSettings = aimviewSettings.ObjectSettings["Transit"];
            swAVTransits.Checked = transitSettings.Enabled;
            swAVTransitDistance.Checked = transitSettings.Distance;
            swAVTransitName.Checked = transitSettings.Name;
            sldrAVTransitTextDistance.Value = transitSettings.TextDistance;
            #endregion

            #region Memory Writing
            swMasterSwitch.Checked = this.config.MasterSwitch;

            // Global Features
            mcSettingsMemoryWritingGlobal.Enabled = this.config.MasterSwitch;
            swThirdperson.Checked = this.config.Thirdperson;
            swInfiniteStamina.Checked = this.config.InfiniteStamina;
            lblSettingsMemoryWritingTimeScaleFactor.Text = $"x{(this.config.TimeScaleFactor)}";
            lblSettingsMemoryWritingTimeScaleFactor.Enabled = this.config.TimeScale;
            swLootThroughWalls.Checked = this.config.LootThroughWalls;
            sldrLootThroughWallsDistance.Enabled = this.config.LootThroughWalls;
            swExtendedReach.Checked = this.config.ExtendedReach;
            sldrExtendedReachDistance.Enabled = this.config.ExtendedReach;
            swInventoryBlur.Checked = this.config.InventoryBlur;
            swMedPanel.Checked = this.config.MedInfoPanel;

            // Gear Features
            mcSettingsMemoryWritingGear.Enabled = this.config.MasterSwitch;
            swRecoil.Checked = this.config.Recoil;
            swWeaponSway.Checked = this.config.WeaponSway;
            sldrXFactor.Enabled = this.config.Recoil;
            sldrXFactor.Value = (int)Math.Round(this.config.RecoilXPercent * 100);
            sldrYFactor.Enabled = this.config.Recoil;
            sldrYFactor.Value = (int)Math.Round(this.config.RecoilYPercent * 100);
            sldrSwayFactor.Enabled = this.config.WeaponSway;
            sldrSwayFactor.Value = (int)Math.Round(this.config.WeaponSwayPercent * 100);
            swInstantADS.Checked = this.config.InstantADS;
            swNoVisor.Checked = this.config.NoVisor;
            swFrostBite.Checked = this.config.FrostBite;
            swThermalVision.Checked = this.config.ThermalVision;
            swOpticalThermal.Checked = this.config.OpticThermalVision;
            swNightVision.Checked = this.config.NightVision;
            swNoWeaponMalfunctions.Checked = this.config.NoWeaponMalfunctions;
            swJuggernaut.Checked = this.config.Juggernaut;

            // Max Skill Buff Management
            mcSettingsMemoryWritingSkillBuffs.Enabled = this.config.MasterSwitch;
            swMaxEndurance.Checked = this.config.MaxSkills["Endurance"];
            swMaxStrength.Checked = this.config.MaxSkills["Strength"];
            swMaxVitality.Checked = this.config.MaxSkills["Vitality"];
            swMaxHealth.Checked = this.config.MaxSkills["Health"];
            swMaxStressResistance.Checked = this.config.MaxSkills["Stress Resistance"];
            swMaxMetabolism.Checked = this.config.MaxSkills["Metabolism"];
            swMaxImmunity.Checked = this.config.MaxSkills["Immunity"];
            swMaxPerception.Checked = this.config.MaxSkills["Perception"];
            swMaxIntellect.Checked = this.config.MaxSkills["Intellect"];
            swMaxAttention.Checked = this.config.MaxSkills["Attention"];
            swMaxCovertMovement.Checked = this.config.MaxSkills["Covert Movement"];
            swMaxThrowables.Checked = this.config.MaxSkills["Throwables"];
            swMaxSurgery.Checked = this.config.MaxSkills["Surgery"];
            swMaxSearch.Checked = this.config.MaxSkills["Search"];
            swMaxMagDrills.Checked = this.config.MaxSkills["Mag Drills"];
            swMaxLightVests.Checked = this.config.MaxSkills["Light Vests"];
            swMaxHeavyVests.Checked = this.config.MaxSkills["Heavy Vests"];

            sldrMagDrillsSpeed.Enabled = this.config.MaxSkills["Mag Drills"];
            sldrMagDrillsSpeed.Value = this.config.MagDrillSpeed;
            sldrThrowStrength.Enabled = this.config.MaxSkills["Strength"];
            sldrThrowStrength.Value = this.config.ThrowPowerStrength;

            // Thermal Features
            mcSettingsMemoryWritingThermal.Enabled = this.config.MasterSwitch;
            mcSettingsMemoryWritingThermal.Enabled = (this.config.ThermalVision || this.config.OpticThermalVision);
            cboThermalType.SelectedIndex = this.config.ThermalVision ? 0 : (this.config.OpticThermalVision ? 1 : 0);
            cboThermalColorScheme.SelectedIndex = this.config.ThermalVision ? this.config.MainThermalSetting.ColorScheme : (this.config.OpticThermalVision ? this.config.OpticThermalSetting.ColorScheme : 0);
            sldrThermalColorCoefficient.Value = (int)(this.config.MainThermalSetting.ColorCoefficient * 100);
            sldrMinTemperature.Value = (int)((this.config.MainThermalSetting.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            sldrThermalRampShift.Value = (int)((this.config.MainThermalSetting.RampShift + 1.0f) * 100.0f);

            // Chams
            mcSettingsMemoryWritingChams.Enabled = this.config.MasterSwitch;
            var enabled = this.config.Chams["Enabled"];
            swChams.Checked = enabled;
            swChamsPMCs.Checked = this.config.Chams["PMCs"];
            swChamsPlayerScavs.Checked = this.config.Chams["PlayerScavs"];
            swChamsBosses.Checked = this.config.Chams["Bosses"];
            swChamsRogues.Checked = this.config.Chams["Rogues"];
            swChamsEvent.Checked = this.config.Chams["Event"];
            swChamsCultists.Checked = this.config.Chams["Cultists"];
            swChamsScavs.Checked = this.config.Chams["Scavs"];
            swChamsTeammates.Checked = this.config.Chams["Teammates"];
            swChamsCorpses.Checked = this.config.Chams["Corpses"];
            swChamsRevert.Checked = this.config.Chams["RevertOnClose"];

            // World
            mcSettingsMemoryWritingWorld.Enabled = this.config.MasterSwitch;
            swNoFog.Checked = this.config.WorldSettings.Fog;
            swNoRain.Checked = this.config.WorldSettings.Rain;
            swNoClouds.Checked = this.config.WorldSettings.Clouds;
            swNoShadows.Checked = this.config.WorldSettings.Shadows;
            swNoSun.Checked = this.config.WorldSettings.Sun;
            swNoMoon.Checked = this.config.WorldSettings.Moon;

            swSunIntensity.Checked = this.config.WorldSettings.SunLight;
            sldrSunIntensity.Enabled = this.config.WorldSettings.SunLight;
            sldrSunIntensity.Value = this.config.WorldSettings.SunLightIntensity;
            swMoonIntensity.Checked = this.config.WorldSettings.MoonLight;
            sldrMoonIntensity.Enabled = this.config.WorldSettings.MoonLight;
            sldrSunIntensity.Value = this.config.WorldSettings.SunLightIntensity;

            swFreezeTime.Checked = this.config.WorldSettings.FreezeTime;
            sldrTimeOfDay.Enabled = this.config.WorldSettings.FreezeTime;
            sldrTimeOfDay.Value = this.config.WorldSettings.TimeOfDay;
            swTimeScale.Checked = this.config.TimeScale;
            sldrTimeScaleFactor.Enabled = this.config.TimeScale;
            sldrTimeScaleFactor.Value = (int)(this.config.TimeScaleFactor * 10);

            this.ToggleChamsControls();
            #endregion

            #region Loot
            // General
            var processLootEnabled = this.config.ProcessLoot;
            swProcessLoot.Checked = processLootEnabled;
            this.UpdateLootControls();
            swFilteredOnly.Checked = this.config.ImportantLootOnly;
            swItemValue.Checked = this.config.LootValue;
            swLooseLoot.Checked = this.config.LooseLoot;
            swCorpses.Checked = this.config.LootCorpses;
            swSubItems.Checked = this.config.SubItems;
            swAutoLootRefresh.Checked = this.config.LootItemRefresh;

            // Quest Helper
            var questHelperEnabled = this.config.QuestHelper;
            swQuestHelper.Checked = questHelperEnabled;
            this.UpdateQuestControls();
            swUnknownQuestItems.Checked = this.config.UnknownQuestItems;
            swQuestItems.Checked = this.config.QuestItems;
            swQuestLootItems.Checked = this.config.QuestLootItems;
            swQuestLocations.Checked = this.config.QuestLocations;
            swAutoTaskRefresh.Checked = this.config.QuestTaskRefresh;
            sldrAutoTaskRefreshDelay.Value = this.config.QuestTaskRefreshDelay;

            // Minimum Loot Value
            sldrMinRegularLoot.Value = this.config.MinLootValue / 1000;
            sldrMinImportantLoot.Value = this.config.MinImportantLootValue / 1000;
            sldrMinCorpse.Value = this.config.MinCorpseValue / 1000;
            sldrMinSubItems.Value = this.config.MinSubItemValue / 1000;

            // Loot Ping
            sldrLootPingAnimationSpeed.Value = this.config.LootPing["AnimationSpeed"];
            sldrLootPingMaxRadius.Value = this.config.LootPing["Radius"];
            sldrLootPingRepetition.Value = this.config.LootPing["Repetition"];

            // Container settings
            swContainers.Checked = this.config.LootContainerSettings["Enabled"];
            lstContainers.Enabled = this.config.LootContainerSettings["Enabled"];
            sldrContainerDistance.Enabled = this.config.LootContainerSettings["Enabled"];
            #endregion
            #endregion

            this.InitiateContainerList();
            this.UpdatePlayerInformationSettings();
            this.UpdatePvEControls();
            this.UpdateAimviewSettings();
            this.InitializeAutoMapRefreshItems();
            this.InitializeFactions();
            this.InitializeLootFilter();
            this.InitializeWatchlist();
            this.InitializeColors();
            this.InitializeFonts();
            this.InitializeUIScaling();
            this.InitializeHotkeys();
            this.InitializeHotkeyActions();
        }

        private void InputCheckTimer_Tick(object sender, EventArgs e)
        {
            if (!this.InGame)
                return;

            this.CheckHotkeys();

            foreach (var hotkey in this.config.Hotkeys)
            {
                if (this.previouslyPressedKeys.Contains(hotkey.Key) && !InputManager.IsKeyDown(hotkey.Key))
                {
                    this.HandleKeyUp(hotkey.Key);
                    this.previouslyPressedKeys.Remove(hotkey.Key);
                }
            }
        }

        private void PlayerListUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (this.InGame)
            {
                this.BeginInvoke(new Action(() => {
                    this.UpdateWatchlistPlayers();
                }));
            }
        }
        #endregion

        #region General Event Handlers
        private async void frmMain_Shown(object sender, EventArgs e)
        {
            while (this.mapCanvas.GRContext is null)
                await Task.Delay(1);

            this.mapCanvas.GRContext.SetResourceCacheLimit(1500000000); // Fixes low FPS on big maps

            while (true)
            {
                if (config.VSync)
                {
                    await Task.Delay(1);
                    this.mapCanvas.Invalidate();
                }
                else
                {
                    await Task.Run(() => Thread.SpinWait(50000));
                    this.mapCanvas.Invalidate();
                }
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            this.LoadConfig();

            this.LoadMaps();

            this.mapChangeTimer.AutoReset = false;
            this.mapChangeTimer.Elapsed += this.MapChangeTimer_Elapsed;

            this.fpsWatch.Start();

            this.InitializeInputCheckTimer();
            this.InitializePlayerlistTimer();
            this.InitializeDoubleBuffering();
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

            lock (this.renderLock)
            {
                try
                {
                    this.selectedMap = this.maps[this.mapSelectionIndex]; // Swap map

                    if (this.loadedBitmaps is not null)
                    {
                        foreach (var bitmap in this.loadedBitmaps)
                            bitmap?.Dispose(); // Cleanup resources
                    }

                    this.loadedBitmaps = new SKBitmap[this.selectedMap.ConfigFile.MapLayers.Count];

                    for (int i = 0; i < this.loadedBitmaps.Length; i++)
                    {
                        using (
                            var stream = File.Open(
                                this.selectedMap.ConfigFile.MapLayers[i].Filename,
                                FileMode.Open,
                                FileAccess.Read))
                        {
                            this.loadedBitmaps[i] = SKBitmap.Decode(stream); // Load new bitmap(s)
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"ERROR loading {this.selectedMap.ConfigFile.MapLayers[0].Filename}: {ex}"
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
                this.GenerateCards(flpPlayerLoadoutsPlayers, x => x.IsHumanHostileActive, x => x.IsPMC, x => x.Value);
                this.GenerateCards(flpPlayerLoadoutsBosses, x => x.IsHostileActive && !x.IsHuman && x.IsBossRaider, x => x.Type == PlayerType.Boss, x => x.IsBossRaider, x => x.Value);
                this.GenerateCards(flpPlayerLoadoutsAI, x => x.IsHostileActive && !x.IsHuman && !x.IsBossRaider, x => x.Value);
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
                    foreach (var gear in player.Gear)
                    {
                        var gearItem = gear.Item;
                        var gearName = gearItem.Long;

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.AmmoType))
                        {
                            var ammoMsg = (player.isOfflinePlayer ? $"/{gearItem.GearInfo.AmmoCount}" : "");
                            //gearName += $" ({gearItem.GearInfo.AmmoType}/{gearItem.GearInfo.AmmoCount})";
                            gearName += $" ({gearItem.GearInfo.AmmoType}{ammoMsg})";
                        }

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.Thermal))
                            gearName += $" ({gearItem.GearInfo.Thermal})";

                        if (!string.IsNullOrEmpty(gearItem.GearInfo.NightVision))
                            gearName += $" ({gearItem.GearInfo.NightVision})";

                        var gearLabel = new MaterialLabel();
                        gearLabel.Text = $"{GearManager.GetGearSlotName(gear.Slot.Key)}: {gearName.Trim()}";
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
        private void InvalidateMapParams()
        {
            this.lastMapParamsUpdate = DateTime.MinValue;
        }

        private void ResetVariables()
        {
            this.selectedMap = null;

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
            lblRadarOtherValue.Text = "0";
            lblRadarOtherValue.UseAccent = false;
            lblRadarOtherValue.HighEmphasis = false;

            this.lastLootItemCount = 0;
            this.itemToPing = null;
            lstLootItems.Items.Clear();

            this.ClearAllRefs();
        }

        private void UpdateWindowTitle()
        {
            if (!this.InGame || this.LocalPlayer is null)
            {
                this.ResetVariables();
                return;
            }

            this.UpdateSelectedMap();

            if (this.fpsWatch.ElapsedMilliseconds >= 1000)
            {
                this.UpdateRadarStats();
                this.UpdateEnemyStats();
                this.fpsWatch.Restart();
                this.fps = 0;
            }
            else
            {
                this.fps++;
            }
        }

        private void UpdateRadarStats()
        {
            lblRadarFPSValue.Text = $"{this.fps}";
            lblRadarMemSValue.Text = $"{Memory.Ticks}";
            lblRadarLooseLootValue.Text = $"{this.Loot?.TotalLooseLoot ?? 0}";
            lblRadarContainersValue.Text = $"{this.Loot?.TotalContainers ?? 0}";
            lblRadarCorpsesValue.Text = $"{this.Loot?.TotalCorpses ?? 0}";
        }

        private void UpdateEnemyStats()
        {
            var playerCounts = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => x.IsAlive && x.IsActive)
                .GroupBy(x => x.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            this.UpdateEnemyStatLabel(lblRadarPMCsValue, playerCounts.GetValueOrDefault(PlayerType.USEC, 0) + playerCounts.GetValueOrDefault(PlayerType.BEAR, 0));
            this.UpdateEnemyStatLabel(lblRadarPlayerScavsValue, playerCounts.GetValueOrDefault(PlayerType.PlayerScav, 0));
            this.UpdateEnemyStatLabel(lblRadarAIScavsValue, playerCounts.GetValueOrDefault(PlayerType.Scav, 0));
            this.UpdateEnemyStatLabel(lblRadarRoguesValue, playerCounts.GetValueOrDefault(PlayerType.Raider) +
                                                      playerCounts.GetValueOrDefault(PlayerType.Rogue) +
                                                      playerCounts.GetValueOrDefault(PlayerType.BossFollower) +
                                                      playerCounts.GetValueOrDefault(PlayerType.BossGuard) +
                                                      playerCounts.GetValueOrDefault(PlayerType.Cultist));
            this.UpdateEnemyStatLabel(lblRadarBossesValue, playerCounts.GetValueOrDefault(PlayerType.Boss, 0));
            this.UpdateEnemyStatLabel(lblRadarOtherValue, playerCounts.GetValueOrDefault(PlayerType.FollowerOfMorana) +
                                                      playerCounts.GetValueOrDefault(PlayerType.Zombie));
        }

        private void UpdateEnemyStatLabel(MaterialLabel label, int count)
        {
            if (label.Text != count.ToString())
            {
                label.Text = $"{count}";
                label.UseAccent = count > 0;
                label.HighEmphasis = count > 0;
            }
        }

        private void UpdateSelectedMap()
        {
            var currentMap = this.MapName;
            var currentMapPrefix = currentMap.ToLower().Substring(0, Math.Min(4, currentMap.Length));

            if (this.selectedMap is null || !this.selectedMap.MapID.ToLower().StartsWith(currentMapPrefix))
            {
                this.selectedMap = this.maps.FirstOrDefault(x =>
                    x.MapID.ToLower().StartsWith(currentMapPrefix) ||
                    x.MapID.ToLower() == currentMap.ToLower());

                if (this.selectedMap is not null)
                {
                    this.CleanupLoadedBitmaps();
                    this.LoadMapBitmaps();

                    var selectedIndex = cboAutoRefreshMap.FindString(this.MapNameFormatted);
                    cboAutoRefreshMap.SelectedIndex = selectedIndex != -1 ? selectedIndex : 0;
                }
            }
        }

        private void CleanupLoadedBitmaps()
        {
            if (this.loadedBitmaps is not null)
            {
                Parallel.ForEach(this.loadedBitmaps, bitmap =>
                {
                    bitmap?.Dispose();
                });

                this.loadedBitmaps = null;
            }
        }

        private void LoadMapBitmaps()
        {
            var mapLayers = this.selectedMap.ConfigFile.MapLayers;
            this.loadedBitmaps = new SKBitmap[mapLayers.Count];

            Parallel.For(0, mapLayers.Count, i =>
            {
                try
                {
                    using (var stream = new FileStream(mapLayers[i].Filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        this.loadedBitmaps[i] = SKBitmap.Decode(stream);
                        this.loadedBitmaps[i].SetImmutable();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading bitmap for layer {i}: {ex.Message}");
                }
            });
        }

        private bool IsReadyToRender()
        {
            return this.Ready &&
                   this.InGame &&
                   !this.IsAtHideout &&
                   this.LocalPlayer is not null &&
                   this.selectedMap is not null;
        }

        private int GetMapLayerIndex(float playerHeight)
        {
            return this.loadedBitmaps
                .Select((bitmap, index) => new { Index = index, MinHeight = this.selectedMap.ConfigFile.MapLayers[index].MinHeight })
                .LastOrDefault(x => playerHeight > x.MinHeight)?.Index ?? 0;
        }

        private MapParameters GetMapParameters(MapPosition position)
        {
            var mapLayerIndex = this.GetMapLayerIndex(position.Height);
            var bitmap = this.loadedBitmaps[mapLayerIndex];
            var zoomFactor = this.currentZoom;
            var zoomWidth = bitmap.Width / zoomFactor;
            var zoomHeight = bitmap.Height / zoomFactor;

            var bounds = new SKRect(
                position.X - zoomWidth / 2,
                position.Y - zoomHeight / 2,
                position.X + zoomWidth / 2,
                position.Y + zoomHeight / 2
            ).AspectFill(this.mapCanvas.CanvasSize);

            return new MapParameters
            {
                UIScale = this.uiScale,
                MapLayerIndex = mapLayerIndex,
                Bounds = bounds,
                XScale = (float)this.mapCanvas.Width / bounds.Width,
                YScale = (float)this.mapCanvas.Height / bounds.Height
            };
        }

        private MapParameters GetMapLocation()
        {
            var now = DateTime.Now;
            if ((now - this.lastMapParamsUpdate).TotalMilliseconds < FRAME_DELAY)
                return this.cachedMapParams;

            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = localPlayer.Position;
                var localPlayerMapPos = localPlayerPos.ToMapPos(this.selectedMap);

                MapParameters calculatedParams;
                if (this.isFreeMapToggled)
                {
                    calculatedParams = this.GetMapParameters(new MapPosition()
                    {
                        X = this.currentPanPosition.X,
                        Y = this.currentPanPosition.Y,
                        Height = localPlayerMapPos.Height
                    });
                }
                else
                {
                    this.lastMousePosition = Point.Empty;
                    this.currentPanPosition = new SKPoint(localPlayerMapPos.X, localPlayerMapPos.Y);
                    this.targetPanPosition = this.currentPanPosition;
                    calculatedParams = this.GetMapParameters(localPlayerMapPos);
                }

                this.cachedMapParams = calculatedParams;
                this.lastMapParamsUpdate = now;
                return calculatedParams;
            }
            else
            {
                var calculatedParams = this.GetMapParameters(new MapPosition()
                {
                    X = this.currentPanPosition.X,
                    Y = this.currentPanPosition.Y,
                    Height = this.mapPanPosition.Height
                });

                this.cachedMapParams = calculatedParams;
                this.lastMapParamsUpdate = now;
                return calculatedParams;
            }
        }

        private static bool IsAggressorFacingTarget(SKPoint aggressor, float aggressorDegrees, SKPoint target, float distance)
        {
            double maxDiff = 31.3573 - 3.51726 * Math.Log(Math.Abs(0.626957 - 15.6948 * distance));
            if (maxDiff < 1f)
                maxDiff = 1f;

            var radians = Math.Atan2(target.Y - aggressor.Y, target.X - aggressor.X);
            var degs = radians.ToDegrees();

            if (degs < 0)
                degs += 360f;

            var diff = Math.Abs(degs - aggressorDegrees);
            return diff <= maxDiff;
        }

        private void DrawMap(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var localPlayerPos = localPlayer.Position;

            if (mcRadarMapSetup.Visible) // Print coordinates (to make it easy to setup JSON configs)
                lblRadarMapSetup.Text = $"Map Setup - Unity X,Y,Z: {localPlayerPos.X}, {localPlayerPos.Y}, {localPlayerPos.Z}";
            else if (lblRadarMapSetup.Text != "Map Setup" && !mcRadarMapSetup.Visible)
                lblRadarMapSetup.Text = "Map Setup";

            // Prepare to draw Game Map
            var mapParams = this.GetMapLocation();

            var mapCanvasBounds = new SKRect() // Drawing Destination
            {
                Left = this.mapCanvas.Left,
                Right = this.mapCanvas.Right,
                Top = this.mapCanvas.Top,
                Bottom = this.mapCanvas.Bottom
            };

            // Draw Game Map
            canvas.DrawBitmap(
                this.loadedBitmaps[mapParams.MapLayerIndex],
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
                var allPlayers = this.AllPlayers?
                        .Select(x => x.Value)
                        .Where(x => x.IsActive && x.IsAlive && !x.HasExfild);

                if (allPlayers.Any())
                {
                    var friendlies = allPlayers.Where(x => x.IsFriendlyActive).ToList();
                    var localPlayerPos = localPlayer.Position;
                    var localPlayerMapPos = localPlayerPos.ToMapPos(this.selectedMap);
                    var mouseOverGroup = this.mouseOverGroup;
                    var mapParams = this.GetMapLocation();

                    foreach (var player in allPlayers)
                    {
                        var playerPos = player.Position;
                        var playerMapPos = playerPos.ToMapPos(this.selectedMap);

                        if (!this.IsInViewport(playerMapPos, mapParams))
                            continue;

                        var playerZoomedPos = playerMapPos.ToZoomedPos(mapParams);

                        player.ZoomedPosition = new Vector2(playerZoomedPos.X, playerZoomedPos.Y);

                        var aimlineLength = 15;

                        if (player.Type != PlayerType.Teammate && !player.IsLocalPlayer)
                        {
                            foreach (var friendly in friendlies)
                            {
                                var friendlyPos = friendly.Position;
                                var friendlyDist = Vector3.Distance(playerPos, friendlyPos);

                                if (friendlyDist > this.config.MaxDistance)
                                    continue;

                                var friendlyMapPos = friendlyPos.ToMapPos(this.selectedMap);

                                if (IsAggressorFacingTarget(playerMapPos.GetPoint(), player.Rotation.X, friendlyMapPos.GetPoint(), friendlyDist))
                                {
                                    aimlineLength = 1000;
                                    break;
                                }
                            }
                        }

                        this.DrawPlayer(canvas, player, playerZoomedPos, aimlineLength, mouseOverGroup, localPlayerMapPos);
                    }
                }
            }
        }

        private void DrawPlayer(SKCanvas canvas, Player player, MapPosition playerZoomedPos, int aimlineLength, int? mouseOverGrp, MapPosition localPlayerMapPos)
        {
            if (!this.InGame || this.LocalPlayer is null)
                return;

            var type = player.Type.ToString();

            if (type.Equals("BEAR", StringComparison.OrdinalIgnoreCase) || type.Equals("USEC", StringComparison.OrdinalIgnoreCase))
                type = "PMC";

            var playerSettings = this.config.PlayerInformationSettings[type];
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
                aboveLines.Add(player.ErrorCount > 10 ? "ERROR" : player.Name);

            if (playerSettings.Height)
                leftLines.Add($"{Math.Round(height)}");

            if (playerSettings.Distance)
                belowLines.Add($"{Math.Round(dist)}");

            if (playerSettings.Flags)
            {
                if (playerSettings.Health && player.Health != -1)
                    rightLines.Add(player.HealthStatus);

                if (player.ItemInHands.Item is not null)
                {
                    if (playerSettings.ActiveWeapon && !string.IsNullOrEmpty(player.ItemInHands.Item.Short))
                        rightLines.Add(player.ItemInHands.Item.Short);

                    if (playerSettings.AmmoType && !string.IsNullOrEmpty(player.ItemInHands.Item.GearInfo.AmmoType))
                    {
                        var ammoMsg = (player.isOfflinePlayer ? $"/{player.ItemInHands.Item.GearInfo.AmmoCount}" : "");
                        //rightLines.Add($"{player.ItemInHands.Item.GearInfo.AmmoType}/{player.ItemInHands.Item.GearInfo.AmmoCount}");
                        rightLines.Add($"{player.ItemInHands.Item.GearInfo.AmmoType}{ammoMsg}");
                    }
                }

                if (playerSettings.Thermal && player.HasThermal)
                    rightLines.Add("T");

                if (playerSettings.NightVision && player.HasNVG)
                    rightLines.Add("NVG");

                if (playerSettings.Gear && player.HasRequiredGear)
                    rightLines.Add("GEAR");

                if (playerSettings.Value)
                    rightLines.Add($"${TarkovDevManager.FormatNumber(player.Value)}");

                if (playerSettings.Group && player.GroupID != -1)
                    rightLines.Add(player.GroupID.ToString());

                if (playerSettings.Tag && !string.IsNullOrEmpty(player.Tag))
                    rightLines.Add(player.Tag);
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

        private void DrawItemAnimations(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var mapParams = this.GetMapLocation();

                foreach (var animation in activeItemAnimations)
                {
                    var itemZoomedPos = animation.Item.Position
                                            .ToMapPos(this.selectedMap)
                                            .ToZoomedPos(mapParams);

                    var animationTime = animation.AnimationTime % animation.MaxAnimationTime;
                    var maxRadius = this.config.LootPing["Radius"];

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
                var loot = this.Loot;
                if (loot is not null)
                {
                    if (loot.Filter is null)
                        loot.ApplyFilter();

                    var filter = loot.Filter;

                    if (filter is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(this.selectedMap);
                        var mapParams = this.GetMapLocation();

                        if (loot.HasCachedItems && loot.Loot?.Count != this.lastLootItemCount)
                        {
                            this.lastLootItemCount = loot.Loot.Count;

                            if (lstLootItems.Items.Count != this.lastLootItemCount)
                                this.RefreshLootListItems();
                        }

                        var containersEnabled = this.config.LootContainerSettings["Enabled"];
                        var corpsesEnabled = this.config.LootCorpses;
                        var looseLootEnabled = this.config.LooseLoot;

                        foreach (var item in filter)
                        {
                            if (item is null || (this.config.ImportantLootOnly && !item.Important && !item.AlwaysShow))
                                continue;

                            if (item is LootContainer container && (!container.IsWithinDistance || !containersEnabled))
                                continue;

                            if (item is LootCorpse corpse && !corpsesEnabled)
                                continue;

                            if (item is LootItem lootItem && !looseLootEnabled)
                                continue;

                            var position = item.Position.Y - localPlayerMapPos.Height;
                            var itemMapPos = item.Position.ToMapPos(this.selectedMap);

                            if (!this.IsInViewport(itemMapPos, mapParams))
                                continue;

                            var itemZoomedPos = itemMapPos.ToZoomedPos(mapParams);
                            item.ZoomedPosition = new Vector2(itemZoomedPos.X, itemZoomedPos.Y);

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

        private void DrawQuestItems(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                if (this.config.QuestHelper && !Memory.IsScav) // Draw quest items (if enabled)
                {
                    if (this.QuestManager is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(this.selectedMap);
                        var mapParams = this.GetMapLocation();

                        if (this.config.QuestItems)
                        {
                            var questItems = this.QuestManager.QuestItems;

                            if (questItems is null)
                                return;

                            var items = this.config.UnknownQuestItems ?
                                questItems.Where(x => x?.Position.X != 0 && x?.Name == "????") :
                                questItems.Where(x => x?.Position.X != 0 && x?.Name != "????");

                            foreach (var item in items)
                            {
                                if (item is null || item.Complete)
                                    continue;

                                var position = item.Position.Y - localPlayerMapPos.Height;
                                var itemMapPos = item.Position.ToMapPos(this.selectedMap);

                                if (!this.IsInViewport(itemMapPos, mapParams))
                                    continue;

                                var itemZoomedPos = itemMapPos.ToZoomedPos(mapParams);

                                item.ZoomedPosition = new Vector2(itemZoomedPos.X, itemZoomedPos.Y);

                                itemZoomedPos.DrawQuestItem(
                                    canvas,
                                    item,
                                    position
                                );
                            }
                        }

                        if (this.config.QuestLocations)
                        {
                            var questZones = this.QuestManager.QuestZones;

                            if (questZones is null)
                                return;

                            foreach (var zone in questZones.Where(x => x.MapName.ToLower() == Memory.MapNameFormatted.ToLower() && !x.Complete))
                            {
                                var position = zone.Position.Y - localPlayerMapPos.Height;
                                var questZoneMapPos = zone.Position.ToMapPos(this.selectedMap);

                                if (!this.IsInViewport(questZoneMapPos, mapParams))
                                    continue;

                                var questZoneZoomedPos = questZoneMapPos.ToZoomedPos(mapParams);

                                zone.ZoomedPosition = new Vector2(questZoneZoomedPos.X, questZoneZoomedPos.Y);

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
                    var mapParams = this.GetMapLocation();

                    foreach (var grenade in grenades)
                    {
                        var grenadeZoomedPos = grenade
                            .Position
                            .ToMapPos(this.selectedMap)
                            .ToZoomedPos(mapParams);

                        grenadeZoomedPos.DrawGrenade(canvas, grenade);
                    }
                }
            }
        }

        private void DrawTripwires(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                var tripwires = this.Tripwires;
                if (tripwires is not null)
                {
                    var mapParams = this.GetMapLocation();

                    foreach (var tripwire in tripwires)
                    {
                        var tripwireMapPos = tripwire.FromPos.ToMapPos(this.selectedMap);

                        if (!this.IsInViewport(tripwireMapPos, mapParams))
                            continue;

                        var fromPosZoomedPos = tripwireMapPos.ToZoomedPos(mapParams);

                        var toPosZoomedPos = tripwire.ToPos
                            .ToMapPos(this.selectedMap)
                            .ToZoomedPos(mapParams);

                        var localPlayerMapPos = this.LocalPlayer.Position.
                            ToMapPos(this.selectedMap);

                        fromPosZoomedPos.DrawTripwire(
                            canvas,
                            toPosZoomedPos,
                            tripwire,
                            localPlayerMapPos.Height);
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
                    var mapParams = this.GetMapLocation();

                    foreach (var corpse in corpses)
                    {
                        var corpseMapPos = corpse.Position.ToMapPos(this.selectedMap);

                        if (!this.IsInViewport(corpseMapPos, mapParams))
                            continue;

                        var corpseZoomedPos = corpseMapPos.ToZoomedPos(mapParams);

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
                    var localPlayerMapPos = this.LocalPlayer.Position.ToMapPos(this.selectedMap);
                    var mapParams = this.GetMapLocation();

                    foreach (var exfil in exfils)
                    {
                        var exfilMapPos = exfil.Position.ToMapPos(this.selectedMap);

                        if (!this.IsInViewport(exfilMapPos, mapParams))
                            continue;

                        var exfilZoomedPos = exfilMapPos.ToZoomedPos(mapParams);

                        exfilZoomedPos.DrawExfil(
                            canvas,
                            exfil,
                            localPlayerMapPos.Height
                        );
                    }
                }
            }
        }

        private void DrawTransits(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            if (this.InGame && localPlayer is not null)
            {
                var transits = this.Transits;
                if (transits is not null)
                {
                    var localPlayerMapPos = this.LocalPlayer.Position.ToMapPos(this.selectedMap);
                    var mapParams = this.GetMapLocation();

                    foreach (var transit in transits)
                    {
                        var transitMapPos = transit.Position.ToMapPos(this.selectedMap);

                        if (!this.IsInViewport(transitMapPos, mapParams))
                            continue;

                        var transitZoomedPos = transitMapPos.ToZoomedPos(mapParams);

                        transitZoomedPos.DrawTransit(
                            canvas,
                            transit,
                            localPlayerMapPos.Height
                        );
                    }
                }
            }
        }

        private void DrawAimview(SKCanvas canvas)
        {
            if (!this.config.AimviewSettings.Enabled || this.AllPlayers is null)
                return;

            var aimviewPlayers = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => x.IsActive && x.IsAlive)
                .ToList();

            if (!aimviewPlayers.Any())
                return;

            //var primaryTeammateAimviewBounds = this.CalculateAimviewBounds(mcRadarStats.Visible || mcRadarEnemyStats.Visible, mcRadarStats);

            //var primaryTeammate = this.AllPlayers?
            //    .Select(x => x.Value)
            //    .FirstOrDefault(x => x.AccountID == txtTeammateID.Text);

            this.RenderAimview(canvas, this.LocalPlayer, aimviewPlayers);
            //this.RenderAimview(canvas, primaryTeammateAimviewBounds, primaryTeammate, aimviewPlayers);
        }

        private void DrawToolTips(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var mapParams = this.GetMapLocation();

            if (localPlayer is not null)
            {
                if (this.closestPlayerToMouse is not null)
                {
                    var playerZoomedPos = this.closestPlayerToMouse
                        .Position
                        .ToMapPos(this.selectedMap)
                        .ToZoomedPos(mapParams);

                    playerZoomedPos.DrawToolTip(canvas, this.closestPlayerToMouse);
                }
            }

            if (this.closestItemToMouse is not null)
            {
                var itemZoomedPos = this.closestItemToMouse
                    .Position
                    .ToMapPos(this.selectedMap)
                    .ToZoomedPos(mapParams);
                itemZoomedPos.DrawLootableObjectToolTip(canvas, this.closestItemToMouse);
            }

            if (this.closestTaskZoneToMouse is not null)
            {
                var taskZoneZoomedPos = this.closestTaskZoneToMouse
                    .Position
                    .ToMapPos(this.selectedMap)
                    .ToZoomedPos(mapParams);
                taskZoneZoomedPos.DrawToolTip(canvas, this.closestTaskZoneToMouse);
            }

            if (this.closestTaskItemToMouse is not null)
            {
                var taskItemZoomedPos = this.closestTaskItemToMouse
                    .Position
                    .ToMapPos(this.selectedMap)
                    .ToZoomedPos(mapParams);
                taskItemZoomedPos.DrawToolTip(canvas, this.closestTaskItemToMouse);
            }
        }

        private void DrawStatusText(SKCanvas canvas)
        {
            var isReady = this.Ready;
            var inGame = this.InGame;
            var isAtHideout = this.IsAtHideout;
            var localPlayer = this.LocalPlayer;
            var selectedMap = this.selectedMap;

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
                    this.ResetVariables();
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

            var centerX = this.mapCanvas.Width / 2;
            var centerY = this.mapCanvas.Height / 2;

            canvas.DrawText(statusText, centerX, centerY, SKPaints.TextRadarStatus);
        }

        private bool TryGetScreenPosition(Vector3 itemPos, SKRect drawingLocation, out Vector2 screenPos)
        {
            var isVisible = Extensions.WorldToScreen(itemPos, (int)drawingLocation.Width, (int)drawingLocation.Height, out screenPos);

            if (isVisible)
            {
                screenPos.X += drawingLocation.Left;
                screenPos.Y += drawingLocation.Top;
            }

            return isVisible;
        }

        private bool IsWithinDrawingBounds(Vector2 pos, SKRect bounds)
        {
            return pos.X >= bounds.Left &&
                   pos.X <= bounds.Right &&
                   pos.Y >= bounds.Top &&
                   pos.Y <= bounds.Bottom;
        }

        private void RenderAimview(SKCanvas canvas, Player sourcePlayer, IEnumerable<Player> aimviewPlayers)
        {
            if (sourcePlayer is null || !sourcePlayer.IsActive || !sourcePlayer.IsAlive)
                return;

            var aimviewBounds = this.GetAimviewBounds();
            canvas.DrawRect(aimviewBounds, SKPaints.PaintTransparentBacker);
            this.DrawCrosshair(canvas, aimviewBounds);

            if (aimviewPlayers is null || Memory.CameraManager?.ViewMatrix is null)
                return;

            var myPosition = sourcePlayer.Position;
            var aimviewSettings = this.config.AimviewSettings;

            // QUEST HELPER
            var questItemSettings = aimviewSettings.ObjectSettings["QuestItem"];
            var questZoneSettings = aimviewSettings.ObjectSettings["QuestZone"];
            if (this.config.QuestHelper && (questItemSettings.Enabled || questZoneSettings.Enabled))
            {
                if (questItemSettings.Enabled && this.config.QuestItems)
                {
                    var questItems = this.QuestManager?.QuestItems;

                    if (questItems is not null)
                    {
                        var items = this.config.UnknownQuestItems ?
                            questItems.Where(x => x?.Position.X != 0 && x?.Name == "????") :
                            questItems.Where(x => x?.Position.X != 0 && x?.Name != "????");

                        foreach (var item in items)
                        {
                            var itemPos = item.Position;
                            var dist = Vector3.Distance(myPosition, itemPos);

                            if (dist > questItemSettings.PaintDistance && dist > questItemSettings.TextDistance)
                                continue;

                            if (!this.TryGetScreenPosition(itemPos, aimviewBounds, out Vector2 screenPos))
                                continue;

                            if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                                this.DrawAimviewQuestItem(canvas, item, screenPos, dist, questItemSettings);
                        }
                    }
                }

                if (questZoneSettings.Enabled && this.config.QuestLocations)
                {
                    var questZones = this.QuestManager?.QuestZones;

                    if (questZones is not null)
                    {
                        var mapName = Memory.MapNameFormatted.ToLower();

                        foreach (var zone in questZones.Where(x => x.MapName.ToLower() == mapName && !x.Complete))
                        {
                            var zonePos = zone.Position;
                            var dist = Vector3.Distance(myPosition, zonePos);

                            if (dist > questZoneSettings.TextDistance)
                                continue;

                            if (!this.TryGetScreenPosition(zonePos, aimviewBounds, out Vector2 screenPos))
                                continue;

                            if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                                this.DrawAimviewQuestZone(canvas, zone, screenPos, dist, questZoneSettings);
                        }
                    }
                }
            }

            // TRIPWIRES
            var tripwireSettings = aimviewSettings.ObjectSettings["Tripwire"];
            if (tripwireSettings.Enabled)
            {
                var tripwires = this.Tripwires;
                if (tripwires is not null)
                {
                    foreach (var tripwire in tripwires)
                    {
                        var fromPos = tripwire.FromPos;
                        var dist = Vector3.Distance(myPosition, fromPos);

                        if (dist > tripwireSettings.PaintDistance && dist > tripwireSettings.TextDistance)
                            continue;

                        var toPos = tripwire.ToPos;

                        if (!this.TryGetScreenPosition(fromPos, aimviewBounds, out Vector2 fromScreenPos))
                            continue;
                        if (!this.TryGetScreenPosition(toPos, aimviewBounds, out Vector2 toScreenPos))
                            continue;

                        if (this.IsWithinDrawingBounds(fromScreenPos, aimviewBounds))
                            this.DrawAimviewTripwire(canvas, tripwire, fromScreenPos, toScreenPos, dist, tripwireSettings);
                    }
                }
            }

            // RENDER EXFIL
            var exfilSettings = aimviewSettings.ObjectSettings["Exfil"];
            if (exfilSettings.Enabled)
            {
                var exfils = this.Exfils;
                if (exfils is not null)
                {
                    foreach (var exfil in exfils)
                    {
                        var exfilPos = exfil.Position;
                        var dist = Vector3.Distance(myPosition, exfilPos);

                        if (dist > exfilSettings.TextDistance)
                            continue;

                        if (!this.TryGetScreenPosition(exfilPos, aimviewBounds, out Vector2 screenPos))
                            continue;

                        if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                            this.DrawAimviewExfil(canvas, exfil, screenPos, dist, exfilSettings);
                    }
                }
            }

            // RENDER TRANSIT
            var transitSettings = aimviewSettings.ObjectSettings["Transit"];
            if (transitSettings.Enabled)
            {
                var transits = this.Transits;
                if (transits is not null)
                {
                    foreach (var transit in transits)
                    {
                        var transitPos = transit.Position;
                        var dist = Vector3.Distance(myPosition, transitPos);

                        if (dist > transitSettings.TextDistance)
                            continue;

                        if (!this.TryGetScreenPosition(transitPos, aimviewBounds, out Vector2 screenPos))
                            continue;

                        if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                            this.DrawAimviewTransit(canvas, transit, screenPos, dist, transitSettings);
                    }
                }
            }

            // RENDER LOOT
            var looseLootSettings = aimviewSettings.ObjectSettings["LooseLoot"];
            var containerSettings = aimviewSettings.ObjectSettings["Container"];
            var corpseSettings = aimviewSettings.ObjectSettings["Corpse"];
            if (looseLootSettings.Enabled || containerSettings.Enabled || corpseSettings.Enabled)
            {
                if (this.config.ProcessLoot)
                {
                    var loot = this.Loot;
                    if (loot is not null)
                    {
                        if (loot.Filter is null)
                            loot.ApplyFilter();

                        var filter = loot.Filter;

                        if (filter is not null)
                        {
                            foreach (var item in filter)
                            {
                                if (item is null || (this.config.ImportantLootOnly && !item.Important && !item.AlwaysShow))
                                    continue;

                                var objectSettings = item switch
                                {
                                    LootItem => looseLootSettings,
                                    LootContainer => containerSettings,
                                    LootCorpse => corpseSettings
                                };

                                var itemPos = item.Position;
                                var dist = Vector3.Distance(myPosition, itemPos);

                                if (dist > objectSettings.PaintDistance && dist > objectSettings.TextDistance)
                                    continue;

                                if (!this.TryGetScreenPosition(itemPos, aimviewBounds, out Vector2 screenPos))
                                    continue;

                                if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                                    this.DrawLootableObject(canvas, item, screenPos, dist, objectSettings);
                            }
                        }
                    }
                }
            }

            // RENDER PLAYERS - head only dot
            var playerSettings = aimviewSettings.ObjectSettings["Player"];
            if (playerSettings.Enabled)
            {
                foreach (var player in aimviewPlayers)
                {
                    if (player == sourcePlayer)
                        continue;

                    var playerPos = player.Position;
                    var dist = Vector3.Distance(myPosition, playerPos);

                    if (dist > playerSettings.PaintDistance && dist > playerSettings.TextDistance)
                        continue;

                    if (!player.Bones.TryGetValue(PlayerBones.HumanHead, out var bone))
                        continue;

                    if (!this.TryGetScreenPosition(bone.Position, aimviewBounds, out Vector2 screenPos))
                        continue;

                    if (this.IsWithinDrawingBounds(screenPos, aimviewBounds))
                        this.DrawAimviewPlayer(canvas, player, screenPos, dist, playerSettings);
                }
            }
        }

        private SKRect GetAimviewBounds()
        {
            return new SKRect(
                this.config.AimviewSettings.X,
                this.config.AimviewSettings.Y,
                this.config.AimviewSettings.X + this.config.AimviewSettings.Width,
                this.config.AimviewSettings.Y + this.config.AimviewSettings.Height
            );
        }

        private void DrawCrosshair(SKCanvas canvas, SKRect drawingLocation)
        {
            canvas.DrawLine(
                drawingLocation.Left,
                drawingLocation.Bottom - (this.config.AimviewSettings.Height / 2),
                drawingLocation.Right,
                drawingLocation.Bottom - (this.config.AimviewSettings.Height / 2),
                SKPaints.PaintAimviewCrosshair
            );

            canvas.DrawLine(
                drawingLocation.Right - (this.config.AimviewSettings.Width / 2),
                drawingLocation.Top,
                drawingLocation.Right - (this.config.AimviewSettings.Width / 2),
                drawingLocation.Bottom,
                SKPaints.PaintAimviewCrosshair
            );
        }

        private void DrawBoneConnection(SKCanvas canvas, Dictionary<PlayerBones, Vector2> bones, PlayerBones bone1, PlayerBones bone2, SKPaint paint)
        {
            var start = bones[bone1];
            var end = bones[bone2];
            canvas.DrawLine(start.X, start.Y, end.X, end.Y, paint);
        }

        private void DrawLootableObject(SKCanvas canvas, LootableObject lootObject, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            switch (lootObject)
            {
                case LootItem item:
                    this.DrawAimviewLootItem(canvas, item, screenPos, distance, objectSettings);
                    break;
                case LootContainer container:
                    this.DrawAimviewLootContainer(canvas, container, screenPos, distance, objectSettings);
                    break;
                case LootCorpse corpse:
                    this.DrawAimviewLootCorpse(canvas, corpse, screenPos, distance, objectSettings);
                    break;
            }
        }

        private void DrawAimviewPlayer(SKCanvas canvas, Player player, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var objectSize = this.CalculateObjectSize(distance);
            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var paint = player.GetAimviewPaint();
                canvas.DrawCircle(screenPos.X, currentY, objectSize, paint);
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (objectSettings.Distance && distance < objectSettings.TextDistance)
            {
                var textPaint = player.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
            }
        }

        private void DrawAimviewTripwire(SKCanvas canvas, Tripwire tripwire, Vector2 fromScreenPos, Vector2 toScreenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var currentY = toScreenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var objectSize = this.CalculateObjectSize(distance);
                var paint = tripwire.GetAimviewPaint();
                var lineHeight = objectSize * (2.5f * this.uiScale);

                canvas.DrawLine(
                    fromScreenPos.X,
                    fromScreenPos.Y - lineHeight / 2,
                    fromScreenPos.X,
                    fromScreenPos.Y + lineHeight / 2,
                    paint
                );

                canvas.DrawLine(
                    fromScreenPos.X,
                    fromScreenPos.Y,
                    toScreenPos.X,
                    currentY,
                    paint
                );

                paint.Style = SKPaintStyle.Fill;
                canvas.DrawCircle(toScreenPos.X, currentY, objectSize, paint);

                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = tripwire.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", toScreenPos.X, currentY, textPaint);
            }
        }

        private void DrawAimviewQuestItem(SKCanvas canvas, QuestItem item, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var objectSize = this.CalculateObjectSize(distance);
                var itemPaint = item.GetAimviewPaint();
                canvas.DrawCircle(screenPos.X, currentY, objectSize, itemPaint);
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = item.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);

                if (objectSettings.Name)
                {
                    canvas.DrawText(item.Name, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
            }
        }

        private void DrawAimviewQuestZone(SKCanvas canvas, QuestZone zone, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var objectSize = this.CalculateFontSize(distance, true);
            var textPaint = zone.GetAimviewTextPaint();
            textPaint.TextSize = objectSize;

            var currentY = screenPos.Y;

            if (objectSettings.Name)
            {
                canvas.DrawText(zone.ObjectiveType, screenPos.X, currentY, textPaint);
                currentY += objectSize * this.uiScale;
            }

            if (objectSettings.Distance)
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
        }

        private void DrawAimviewExfil(SKCanvas canvas, Exfil exfil, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var objectSize = this.CalculateFontSize(distance);
            var textPaint = exfil.GetAimviewTextPaint();
            textPaint.TextSize = objectSize;

            var currentY = screenPos.Y;

            if (objectSettings.Name)
            {
                canvas.DrawText(exfil.Name, screenPos.X, currentY, textPaint);
                currentY += objectSize * this.uiScale;
            }

            if (objectSettings.Distance)
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
        }

        private void DrawAimviewTransit(SKCanvas canvas, Transit transit, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            var objectSize = this.CalculateFontSize(distance);
            var textPaint = transit.GetAimviewTextPaint();
            textPaint.TextSize = objectSize;

            var currentY = screenPos.Y;

            if (objectSettings.Name)
            {
                canvas.DrawText(transit.Name, screenPos.X, currentY, textPaint);
                currentY += objectSize * this.uiScale;
            }

            if (objectSettings.Distance)
                canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
        }

        private void DrawAimviewLootItem(SKCanvas canvas, LootItem item, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            if (!objectSettings.Enabled)
                return;

            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var objectSize = this.CalculateObjectSize(distance);
                var itemPaint = item.GetAimviewPaint();
                canvas.DrawCircle(screenPos.X, currentY, objectSize, itemPaint);
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = item.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);

                if (objectSettings.Name)
                {
                    canvas.DrawText(item.Item.shortName, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Value)
                {
                    canvas.DrawText(item.GetFormattedValue(), screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
            }
        }

        private void DrawAimviewLootContainer(SKCanvas canvas, LootContainer container, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            if (!objectSettings.Enabled)
                return;

            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var objectSize = this.CalculateObjectSize(distance);
                var containerPaint = container.GetAimviewPaint();
                canvas.DrawRect(
                    new SKRect(
                        screenPos.X - objectSize,
                        currentY - objectSize,
                        screenPos.X + objectSize,
                        currentY + objectSize
                    ),
                    containerPaint
                );
                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = container.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);

                if (objectSettings.Name)
                {
                    canvas.DrawText(container.Name, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
            }
        }

        private void DrawAimviewLootCorpse(SKCanvas canvas, LootCorpse corpse, Vector2 screenPos, float distance, AimviewObjectSettings objectSettings)
        {
            if (!objectSettings.Enabled)
                return;

            var currentY = screenPos.Y;

            if (distance < objectSettings.PaintDistance)
            {
                var objectSize = this.CalculateObjectSize(distance);
                var corpsePaint = corpse.GetAimviewPaint();

                canvas.DrawLine(
                    screenPos.X - objectSize, currentY - objectSize,
                    screenPos.X + objectSize, currentY + objectSize,
                    corpsePaint
                );
                canvas.DrawLine(
                    screenPos.X + objectSize, currentY - objectSize,
                    screenPos.X - objectSize, currentY + objectSize,
                    corpsePaint
                );

                currentY += (objectSize + AIMVIEW_OBJECT_SPACING) * this.uiScale;
            }

            if (distance < objectSettings.TextDistance)
            {
                var textPaint = corpse.GetAimviewTextPaint();
                textPaint.TextSize = this.CalculateFontSize(distance, true);

                if (objectSettings.Name)
                {
                    canvas.DrawText(corpse.Name, screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Value && corpse.Value > 0)
                {
                    canvas.DrawText(TarkovDevManager.FormatNumber(corpse.Value), screenPos.X, currentY, textPaint);
                    currentY += textPaint.TextSize * this.uiScale;
                }

                if (objectSettings.Distance)
                    canvas.DrawText($"{distance:F0}m", screenPos.X, currentY, textPaint);
            }
        }

        private float CalculateFontSize(float dist, bool smallerFont = false)
        {
            var baseSize = 16f;
            var scale = 1f - (dist / 150f);
            scale = Math.Max(0.4f, Math.Min(1f, scale));

            if (smallerFont)
            {
                baseSize *= 0.7f;
                return Math.Max(8f, baseSize * scale * this.uiScale);
            }
            else
            {
                baseSize *= 1f;
                return Math.Max(10f, baseSize * scale * this.uiScale);
            }
        }

        private float CalculateObjectSize(float dist, bool smallerObject = false)
        {
            var baseSize = 20f;
            var scale = 1f - (dist / 150f);
            scale = Math.Max(0.3f, Math.Min(1f, scale));

            if (smallerObject)
                baseSize *= 0.22f;
            else
                baseSize *= 0.3f;

            return baseSize * scale * this.uiScale;
        }

        private void ClearPlayerRefs()
        {
            this.closestPlayerToMouse = null;
            this.mouseOverGroup = null;
        }

        private void ClearItemRefs()
        {
            this.closestItemToMouse = null;
        }

        private void ClearTaskItemRefs()
        {
            this.closestTaskItemToMouse = null;
        }

        private void ClearTaskZoneRefs()
        {
            this.closestTaskZoneToMouse = null;
        }

        private T FindClosestObject<T>(IEnumerable<T> objects, Vector2 position, Func<T, Vector2> positionSelector, float threshold)
            where T : class
        {
            if (objects == null || !objects.Any())
                return null;

            return objects.AsParallel()
                .Select(obj => new { Object = obj, Distance = Vector2.Distance(positionSelector(obj), position) })
                .Where(x => x.Distance < threshold)
                .OrderBy(x => x.Distance)
                .Select(x => x.Object)
                .FirstOrDefault();
        }

        private bool ZoomIn(int amt, Point cursorPosition)
        {
            var zoomFactor = 1 + (this.config.ZoomSensitivity * amt / 1000f);
            this.targetZoom = Math.Clamp(this.targetZoom * zoomFactor, MAX_ZOOM, MIN_ZOOM);

            if (this.isFreeMapToggled)
                this.AdjustPanPositionForZoom(cursorPosition, zoomFactor);

            if (!this.isZooming)
            {
                this.isZooming = true;
                this.AnimateZoomAndPan();
            }

            return true;
        }

        private bool ZoomOut(int amt)
        {
            var zoomFactor = 1 + (this.config.ZoomSensitivity * amt / 1000f);
            this.targetZoom = Math.Clamp(this.targetZoom / zoomFactor, MAX_ZOOM, MIN_ZOOM);

            if (!this.isZooming)
            {
                this.isZooming = true;
                this.AnimateZoomAndPan();
            }

            return true;
        }

        private float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            var shouldStopAnimation = true;

            foreach (var animation in this.activeItemAnimations)
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
                this.itemPingAnimationTimer.Stop();
                this.itemPingAnimationTimer.Dispose();
                this.isItemPingAnimationRunning = false;
            }

            this.activeItemAnimations.RemoveAll(animation => animation.RepetitionCount >= animation.MaxRepetitions);

            this.mapCanvas.Invalidate();
        }

        private List<LootItem> GetLootAndContainerItems()
        {
            if (this.Loot?.Loot is null)
                return new List<LootItem>();

            var lootItems = new List<LootItem>(this.Loot.Loot.OfType<LootItem>());

            foreach (var corpse in this.Loot.Loot.OfType<LootCorpse>())
            {
                lootItems.AddRange(corpse.Items
                    .Select(gearItem => gearItem.Item.Item)
                    .Where(item => item?.Item is not null)
                    .Concat(corpse.Items.SelectMany(gearItem => gearItem.Item.Loot)));
            }

            return lootItems;
        }

        private void RefreshLootListItems()
        {
            if (this.isRefreshingLootItems)
                return;

            this.isRefreshingLootItems = true;

            try
            {
                if (!this.config.ProcessLoot || this.Loot?.Loot?.Count < 1 || !this.config.LootItemViewer)
                {
                    lstLootItems.Items.Clear();
                    return;
                }

                var lootItems = this.GetLootAndContainerItems();
                var itemToFind = txtLootItemFilter.Text.Trim();

                var mergedLootItems = lootItems
                    .GroupBy(lootItem => lootItem.ID)
                    .Select(group => new
                    {
                        LootItem = new LootItem
                        {
                            ID = group.First().ID,
                            Name = group.First().Name,
                            Position = group.First().Position,
                            Important = group.First().Important,
                            AlwaysShow = group.First().AlwaysShow,
                            Value = group.First().Value,
                            Color = group.First().Color
                        },
                        Quantity = group.Count()
                    })
                    .Where(item => string.IsNullOrEmpty(itemToFind) ||
                                   item.LootItem.Name.IndexOf(itemToFind, StringComparison.OrdinalIgnoreCase) != -1)
                    .OrderByDescending(x => x.LootItem.Value)
                    .ToList();

                lstLootItems.BeginUpdate();
                lstLootItems.Items.Clear();
                lstLootItems.Items.AddRange(mergedLootItems.Select(item => new ListViewItem
                {
                    Text = item.Quantity.ToString(),
                    Tag = item.LootItem,
                    SubItems = { item.LootItem.Name, TarkovDevManager.FormatNumber(item.LootItem.Value) }
                }).ToArray());
                lstLootItems.EndUpdate();

                if (this.itemToPing is not null)
                {
                    int itemIndex = lstLootItems.Items.Cast<ListViewItem>()
                        .ToList()
                        .FindIndex(item => item.SubItems[1].Text == this.itemToPing.Name);

                    if (itemIndex != -1)
                    {
                        lstLootItems.SelectedIndices.Clear();
                        lstLootItems.SelectedIndices.Add(itemIndex);
                    }
                }
            }
            finally
            {
                this.isRefreshingLootItems = false;
            }
        }

        private void HandleMapDragging(MouseEventArgs e)
        {
            this.InvalidateMapParams();

            if (!this.lastMousePosition.IsEmpty)
            {
                var dx = (e.X - this.lastMousePosition.X) * DRAG_SENSITIVITY;
                var dy = (e.Y - this.lastMousePosition.Y) * DRAG_SENSITIVITY;

                this.targetPanPosition = new SKPoint(
                    targetPanPosition.X - dx,
                    targetPanPosition.Y - dy
                );

                if (!this.isPanning)
                {
                    this.isPanning = true;
                    this.AnimateZoomAndPan();
                }
            }

            this.lastMousePosition = e.Location;
        }

        private void UpdateClosestObjects(Vector2 mouse, float threshold)
        {
            var allPlayers = this.AllPlayers?
                            .Select(x => x.Value)
                            .Where(x => x.IsActive && x.IsAlive && !x.HasExfild);

            this.closestPlayerToMouse = this.FindClosestObject(allPlayers, mouse, x => x.ZoomedPosition, threshold);

            this.mouseOverGroup = this.closestPlayerToMouse?.IsHumanHostile == true && this.closestPlayerToMouse.GroupID != -1
                ? this.closestPlayerToMouse.GroupID
                : -100;

            if (this.config.ProcessLoot)
            {
                var loot = this.Loot?.Filter?.ToList();
                this.closestItemToMouse = this.FindClosestObject(loot, mouse, x => x.ZoomedPosition, threshold);
            }
            else
            {
                this.ClearItemRefs();
            }

            var tasksItems = this.QuestManager?.QuestItems?.ToList();
            this.closestTaskItemToMouse = this.FindClosestObject(tasksItems, mouse, x => x.ZoomedPosition, threshold);

            var tasksZones = this.QuestManager?.QuestZones?.ToList();
            this.closestTaskZoneToMouse = this.FindClosestObject(tasksZones, mouse, x => x.ZoomedPosition, threshold);
        }

        private void ClearAllRefs()
        {
            this.ClearPlayerRefs();
            this.ClearItemRefs();
            this.ClearTaskItemRefs();
            this.ClearTaskZoneRefs();
        }
        #endregion

        #region Event Handlers
        private void btnToggleMapFree_Click(object sender, EventArgs e)
        {
            if (this.isFreeMapToggled)
            {
                btnToggleMapFree.Icon = Resources.tick;
                this.isFreeMapToggled = false;

                lock (this.renderLock)
                {
                    var localPlayer = this.LocalPlayer;
                    if (localPlayer is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(this.selectedMap);
                        this.mapPanPosition = new MapPosition()
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
                this.isFreeMapToggled = true;
            }

            this.InvalidateMapParams();
        }

        private void btnMapSetupApply_Click(object sender, EventArgs e)
        {
            if (float.TryParse(txtMapSetupX.Text, out float x)
                && float.TryParse(txtMapSetupY.Text, out float y)
                && float.TryParse(txtMapSetupScale.Text, out float scale))
            {
                lock (this.renderLock)
                {
                    if (this.selectedMap is not null)
                    {
                        this.selectedMap.ConfigFile.X = x;
                        this.selectedMap.ConfigFile.Y = y;
                        this.selectedMap.ConfigFile.Scale = scale;
                        this.selectedMap.ConfigFile.Save(this.selectedMap);
                    }
                }
            }
            else
                this.ShowErrorDialog("Invalid value(s) provided in the map setup textboxes.");
        }

        private void skMapCanvas_MouseMovePlayer(object sender, MouseEventArgs e)
        {
            if (!this.IsReadyToRender())
            {
                this.ClearAllRefs();
                return;
            }

            var mouse = new Vector2(e.X, e.Y);
            var threshold = 12 * this.uiScale;

            this.UpdateClosestObjects(mouse, threshold);

            if (this.isFreeMapToggled && this.isDraggingMap)
                this.HandleMapDragging(e);

            if (this.isDraggingAimview)
            {
                var deltaX = e.X - this.aimviewMouseDownPosition.X;
                var deltaY = e.Y - this.aimviewMouseDownPosition.Y;

                var newX = this.config.AimviewSettings.X + deltaX;
                var newY = this.config.AimviewSettings.Y + deltaY;

                newX = Math.Max(0, Math.Min(newX, this.mapCanvas.Width - this.config.AimviewSettings.Width));
                newY = Math.Max(0, Math.Min(newY, this.mapCanvas.Height - this.config.AimviewSettings.Height));

                this.config.AimviewSettings.X = newX;
                this.config.AimviewSettings.Y = newY;

                this.aimviewMouseDownPosition = e.Location;
                this.mapCanvas.Invalidate();
            }
            else if (this.isResizingAimview)
            {
                var deltaX = e.X - this.aimviewMouseDownPosition.X;
                var deltaY = e.Y - this.aimviewMouseDownPosition.Y;

                var newWidth = this.config.AimviewSettings.Width + deltaX;
                var newHeight = this.config.AimviewSettings.Height + deltaY;

                newWidth = Math.Max(MIN_AIMVIEW_SIZE, Math.Min(newWidth,
                    Math.Min(MAX_AIMVIEW_SIZE, this.mapCanvas.Width - this.config.AimviewSettings.X)));
                newHeight = Math.Max(MIN_AIMVIEW_SIZE, Math.Min(newHeight,
                    Math.Min(MAX_AIMVIEW_SIZE, this.mapCanvas.Height - this.config.AimviewSettings.Y)));

                this.config.AimviewSettings.Width = newWidth;
                this.config.AimviewSettings.Height = newHeight;

                //if (sldrAVWidth != null)
                //    sldrAVWidth.Value = newWidth;
                //if (sldrAVHeight != null)
                //    sldrAVHeight.Value = newHeight;

                this.aimviewMouseDownPosition = e.Location;
                this.mapCanvas.Invalidate();
            }
        }

        private void skMapCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (this.isFreeMapToggled)
                {
                    this.isDraggingMap = true;
                    this.lastMousePosition = e.Location;
                }

                if (this.config.AimviewSettings.Enabled)
                {
                    var aimviewBounds = this.GetAimviewBounds();
                    if (e.X >= aimviewBounds.Left && e.X <= aimviewBounds.Right &&
                        e.Y >= aimviewBounds.Top && e.Y <= aimviewBounds.Bottom)
                    {
                        if (ModifierKeys == Keys.Control)
                        {
                            this.isDraggingAimview = true;
                            this.aimviewMouseDownPosition = e.Location;
                        }
                        else if (ModifierKeys == Keys.Shift)
                        {
                            this.isResizingAimview = true;
                            this.aimviewMouseDownPosition = e.Location;
                        }
                    }
                }
            }
        }

        private void skMapCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.isDraggingMap)
            {
                this.isDraggingMap = false;
                this.lastMousePosition = Point.Empty;
            }

            if (this.isDraggingAimview)
                this.isDraggingAimview = false;

            if (this.isResizingAimview)
                this.isResizingAimview = false;
        }

        private void skMapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            this.UpdateWindowTitle();

            if (!this.IsReadyToRender())
            {
                this.DrawStatusText(canvas);
                return;
            }

            lock (this.renderLock)
            {
                this.DrawMap(canvas);

                if (this.config.ProcessLoot)
                {
                    if (this.config.LooseLoot ||
                        this.config.LootCorpses ||
                        this.config.LootContainerSettings["Enabled"] ||
                        (this.config.QuestHelper && this.config.QuestLootItems))
                    {
                        this.DrawItemAnimations(canvas);
                        this.DrawLoot(canvas);
                    }
                }
                else if (this.config.LootCorpses)
                {
                    this.DrawCorpses(canvas);
                }

                if (this.config.QuestHelper)
                    this.DrawQuestItems(canvas);

                this.DrawGrenades(canvas);
                this.DrawTripwires(canvas);
                this.DrawExfils(canvas);
                this.DrawTransits(canvas);
                this.DrawPlayers(canvas);

                if (this.config.AimviewSettings.Enabled)
                    this.DrawAimview(canvas);

                this.DrawToolTips(canvas);
            }

            canvas.Flush();
        }

        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            this.ToggleMap();
        }

        private void swRadarStats_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarStats.Checked;
            this.config.RadarStats = enabled;
            mcRadarStats.Visible = enabled;
        }

        private void btnPingSelectedItem_Click(object sender, EventArgs e)
        {
            if (this.itemToPing is null)
                return;

            var lootItems = this.GetLootAndContainerItems();
            var itemsToPing = lootItems.Where(item => item.ID == itemToPing.ID).ToList();

            this.activeItemAnimations.Clear();

            foreach (var item in itemsToPing)
            {
                this.activeItemAnimations.Add(new ItemAnimation(item)
                {
                    MaxAnimationTime = ((float)this.config.LootPing["AnimationSpeed"] / 1000),
                    MaxRepetitions = this.config.LootPing["Repetition"]
                });
            }

            this.isItemPingAnimationRunning = false;
            this.itemPingAnimationTimer?.Stop();
            this.itemPingAnimationTimer?.Dispose();

            this.itemPingAnimationTimer = new System.Timers.Timer(16);
            this.itemPingAnimationTimer.Elapsed += AnimationTimer_Tick;
            this.itemPingAnimationTimer.Start();
            this.isItemPingAnimationRunning = true;
        }

        private void txtLootItemFilter_TextChanged(object sender, EventArgs e)
        {
            this.RefreshLootListItems();
        }

        private void btnToggleLootItemViewer_Click(object sender, EventArgs e)
        {
            mcRadarLootItemViewer.Visible = this.config.LootItemViewer = !this.config.LootItemViewer;

            if (this.config.LootItemViewer)
                this.RefreshLootListItems();
        }

        private void lstLootItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.itemToPing = lstLootItems.SelectedItems.Count > 0 ? lstLootItems.SelectedItems[0]?.Tag as LootItem : null; ;
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
            var LTWDistance = (pveMode ? this.config.LootThroughWallsDistancePvE : this.config.LootThroughWallsDistance) * 10;
            var reachDistance = (pveMode ? this.config.ExtendedReachDistancePvE : this.config.ExtendedReachDistance) * 10;

            btnTriggerUnityCrash.Visible = pveMode;

            sldrLootThroughWallsDistance.RangeMax = maxLTWDistance;
            sldrLootThroughWallsDistance.ValueMax = maxLTWDistance;

            sldrExtendedReachDistance.RangeMax = maxReachDistance;
            sldrExtendedReachDistance.ValueMax = maxReachDistance;

            sldrLootThroughWallsDistance.Value = (int)LTWDistance;
            sldrExtendedReachDistance.Value = (int)reachDistance;

            lblSettingsMemoryWritingLootThroughWallsDistance.Enabled = this.config.LootThroughWalls;
            lblSettingsMemoryWritingLootThroughWallsDistance.Text = $"x{(LTWDistance / 10)}";

            lblSettingsMemoryWritingExtendedReachDistance.Enabled = this.config.ExtendedReach;
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
            playerText.TextSize = playerInfoSettings.FontSize * this.uiScale;

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
            playerText.TextSize = playerInfoSettings.FlagsFontSize * this.uiScale;

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

            foreach (var playerSetting in this.config.PlayerInformationSettings)
            {
                var settings = playerSetting.Value;
                var textPaint = SKPaints.TextBase.Clone();
                textPaint.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[settings.Font]);
                textPaint.TextSize = settings.FontSize * this.uiScale;

                var flagsTextPaint = SKPaints.TextBase.Clone();
                flagsTextPaint.Typeface = SKTypeface.FromFamilyName(FONTS_TO_USE[settings.FlagsFont]);
                flagsTextPaint.TextSize = settings.FlagsFontSize * this.uiScale;

                Extensions.PlayerTypeTextPaints.Add(playerSetting.Key, textPaint);
                Extensions.PlayerTypeFlagTextPaints.Add(playerSetting.Key, flagsTextPaint);
            }
        }

        private PlayerInformationSettings GetPlayerInfoSettings()
        {
            var playerType = this.GetActivePlayerType();
            return !string.IsNullOrEmpty(playerType) && this.config.PlayerInformationSettings.TryGetValue(playerType, out var settings) ? settings : null;
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
            swPlayerInfoGear.Checked = playerInfoSettings.Gear;
            swPlayerInfoAmmoType.Checked = playerInfoSettings.AmmoType;
            swPlayerInfoGroup.Checked = playerInfoSettings.Group;
            swPlayerInfoValue.Checked = playerInfoSettings.Value;
            swPlayerInfoHealth.Checked = playerInfoSettings.Health;
            swPlayerInfoTag.Checked = playerInfoSettings.Tag;

            swPlayerInfoActiveWeapon.Enabled = flagsChecked;
            swPlayerInfoThermal.Enabled = flagsChecked;
            swPlayerInfoNightVision.Enabled = flagsChecked;
            swPlayerInfoGear.Enabled = flagsChecked;
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

        private void UpdatePlayerAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Player"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVPlayers.Enabled = aimviewOn;
            swAVPlayerDistance.Enabled = enableObjectSettings;
            sldrAVPlayerPaintDistance.Enabled = enableObjectSettings;
            sldrAVPlayerTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateLooseLootAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["LooseLoot"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVLooseLoot.Enabled = aimviewOn;
            swAVLooseLootDistance.Enabled = enableObjectSettings;
            swAVLooseLootName.Enabled = enableObjectSettings;
            swAVLooseLootValue.Enabled = enableObjectSettings;
            sldrAVLooseLootPaintDistance.Enabled = enableObjectSettings;
            sldrAVLooseLootTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateCorpseAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Corpse"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVCorpses.Enabled = aimviewOn;
            swAVCorpseDistance.Enabled = enableObjectSettings;
            swAVCorpseName.Enabled = enableObjectSettings;
            swAVCorpseValue.Enabled = enableObjectSettings;
            sldrAVCorpsePaintDistance.Enabled = enableObjectSettings;
            sldrAVCorpseTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateQuestItemAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["QuestItem"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVQuestItems.Enabled = aimviewOn;
            swAVQuestItemDistance.Enabled = enableObjectSettings;
            swAVQuestItemName.Enabled = enableObjectSettings;
            sldrAVQuestItemPaintDistance.Enabled = enableObjectSettings;
            sldrAVQuestItemTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateContainerAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Container"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVContainers.Enabled = aimviewOn;
            swAVContainerDistance.Enabled = enableObjectSettings;
            swAVContainerName.Enabled = enableObjectSettings;
            sldrAVContainerPaintDistance.Enabled = enableObjectSettings;
            sldrAVContainerTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateTripwireAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Tripwire"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVTripwire.Enabled = aimviewOn;
            swAVTripwireDistance.Enabled = enableObjectSettings;
            sldrAVTripwirePaintDistance.Enabled = enableObjectSettings;
            sldrAVTripwireTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateQuestZoneAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["QuestZone"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVQuestZones.Enabled = aimviewOn;
            swAVQuestZoneDistance.Enabled = enableObjectSettings;
            swAVQuestZoneName.Enabled = enableObjectSettings;
            sldrAVQuestZoneTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateExfilAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Exfil"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVExfils.Enabled = aimviewOn;
            swAVExfilDistance.Enabled = enableObjectSettings;
            swAVExfilName.Enabled = enableObjectSettings;
            sldrAVExfilTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateTransitAimviewControls()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            var objectEnabled = aimviewSettings.ObjectSettings["Transit"].Enabled;
            var enableObjectSettings = (aimviewOn && objectEnabled);
            swAVTransits.Enabled = aimviewOn;
            swAVTransitDistance.Enabled = enableObjectSettings;
            swAVTransitName.Enabled = enableObjectSettings;
            sldrAVTransitTextDistance.Enabled = enableObjectSettings;
        }

        private void UpdateAimviewSettings()
        {
            var aimviewSettings = this.config.AimviewSettings;
            var aimviewOn = aimviewSettings.Enabled;
            swAimview.Checked = aimviewOn;
            sldrAVWidth.Enabled = aimviewOn;
            sldrAVHeight.Enabled = aimviewOn;
            txtTeammateID.Enabled = aimviewOn;

            this.UpdatePlayerAimviewControls();
            this.UpdateLooseLootAimviewControls();
            this.UpdateCorpseAimviewControls();
            this.UpdateQuestItemAimviewControls();
            this.UpdateContainerAimviewControls();
            this.UpdateTripwireAimviewControls();
            this.UpdateQuestZoneAimviewControls();
            this.UpdateExfilAimviewControls();
            this.UpdateTransitAimviewControls();
        }

        #endregion
        #region Event Handlers
        private void swMapHelper_CheckedChanged(object sender, EventArgs e)
        {
            if (swMapHelper.Checked)
            {
                mcRadarMapSetup.Visible = true;
                txtMapSetupX.Text = this.selectedMap?.ConfigFile.X.ToString() ?? "0";
                txtMapSetupY.Text = this.selectedMap?.ConfigFile.Y.ToString() ?? "0";
                txtMapSetupScale.Text = this.selectedMap?.ConfigFile.Scale.ToString() ?? "0";
            }
            else
                mcRadarMapSetup.Visible = false;
        }

        private void swExfilNames_CheckedChanged(object sender, EventArgs e)
        {
            this.config.ExfilNames = swExfilNames.Checked;
        }

        private void swHoverArmor_CheckedChanged(object sender, EventArgs e)
        {
            this.config.HoverArmor = swHoverArmor.Checked;
        }

        private void swTraderPrices_CheckedChanged(object sender, EventArgs e)
        {
            this.config.TraderPrices = swTraderPrices.Checked;
        }

        private void sldrUIScale_onValueChanged(object sender, int newValue)
        {
            this.config.UIScale = newValue;
            this.uiScale = (.01f * newValue);

            this.InitializeUIScaling();
        }

        private void btnRestartRadar_Click(object sender, EventArgs e)
        {
            Memory.Restart();
        }

        private void swRadarVsync_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarVsync.Checked;
            this.config.VSync = enabled;

            if (this.mapCanvas is not null)
                this.mapCanvas.VSync = enabled;
        }

        private void swPvEMode_CheckedChanged(object sender, EventArgs e)
        {
            this.config.PvEMode = swPvEMode.Checked;

            this.UpdatePvEControls();
        }

        private void swRadarEnemyCount_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRadarEnemyCount.Checked;

            this.config.EnemyCount = enabled;
            mcRadarEnemyStats.Visible = enabled;
        }

        private void cboFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.config.GlobalFont = cboGlobalFont.SelectedIndex;

            this.InitializeFonts();
        }

        private void sldrFontSize_onValueChanged(object sender, int newValue)
        {
            this.config.GlobalFontSize = newValue;

            this.InitializeFontSizes();
        }

        private void sldrZoomSensitivity_onValueChanged(object sender, int newValue)
        {
            this.config.ZoomSensitivity = newValue;
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
            swPlayerInfoGear.Enabled = flagsChecked;
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

        private void swPlayerInfoGear_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.TryGetPlayerInfoSettings(out var playerInfoSettings))
                return;

            playerInfoSettings.Gear = swPlayerInfoGear.Checked;
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
            if (Memory.InGame && Memory.LocalPlayer is not null)
                Memory.Chams.TriggerUnityCrash(Memory.LocalPlayer, 100UL);
        }

        // Aimview
        private void swAimview_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.Enabled = swAimview.Checked;
            this.UpdateAimviewSettings();
        }

        private void sldrAVWidth_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.Width = newValue;
        }

        private void sldrAVHeight_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.Height = newValue;
        }

        private void swAVPlayers_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Player"].Enabled = swAVPlayers.Checked;
            this.UpdatePlayerAimviewControls();
        }

        private void swAVPlayerDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Player"].Distance = swAVPlayerDistance.Checked;
        }

        private void sldrAVPlayerPaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Player"].PaintDistance = newValue;
        }

        private void sldrAVPlayerTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Player"].TextDistance = newValue;
        }

        private void swAVLooseLoot_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAVLooseLoot.Checked;
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].Enabled = enabled;
            this.UpdateLooseLootAimviewControls();

            if (enabled)
                this.Loot?.ApplyFilter();
        }

        private void swAVLooseLootDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].Distance = swAVLooseLootDistance.Checked;
        }

        private void swAVLooseLootName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].Name = swAVLooseLootName.Checked;
        }

        private void swAVLooseLootValue_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].Value = swAVLooseLootValue.Checked;
        }

        private void sldrAVLooseLootPaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].PaintDistance = newValue;
        }

        private void sldrAVLooseLootTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["LooseLoot"].TextDistance = newValue;
        }

        private void swAVCorpses_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].Enabled = swAVCorpses.Checked;
            this.UpdateCorpseAimviewControls();
        }

        private void swAVCorpseDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].Distance = swAVCorpseDistance.Checked;
        }

        private void swAVCorpseName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].Name = swAVCorpseName.Checked;
        }

        private void swAVCorpseValue_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].Value = swAVCorpseValue.Checked;
        }

        private void sldrAVCorpsePaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].PaintDistance = newValue;
        }

        private void sldrAVCorpseTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Corpse"].TextDistance = newValue;
        }

        private void swAVQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestItem"].Enabled = swAVQuestItems.Checked;
            this.UpdateQuestItemAimviewControls();
        }

        private void swAVQuestItemDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestItem"].Distance = swAVQuestItemDistance.Checked;
        }

        private void swAVQuestItemName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestItem"].Name = swAVQuestItemName.Checked;
        }

        private void sldrAVQuestItemPaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["QuestItem"].PaintDistance = newValue;
        }

        private void sldrAVQuestItemTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["QuestItem"].TextDistance = newValue;
        }

        private void swAVContainers_CheckedChanged(object sender, EventArgs e)
        {
            var aimviewContainersEnabled = swAVContainers.Checked;
            var containersEnabled = this.config.LootContainerSettings["Enabled"];

            lstContainers.Enabled = (containersEnabled || aimviewContainersEnabled);
            this.config.AimviewSettings.ObjectSettings["Container"].Enabled = aimviewContainersEnabled;

            this.UpdateContainerAimviewControls();

            this.Loot?.ApplyFilter();
        }

        private void swAVContainerDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Container"].Distance = swAVContainerDistance.Checked;
        }

        private void swAVContainerName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Container"].Name = swAVContainerName.Checked;
        }

        private void sldrAVContainerPaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Container"].PaintDistance = newValue;
        }

        private void sldrAVContainerTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Container"].TextDistance = newValue;
        }

        private void swAVTripwire_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Tripwire"].Enabled = swAVTripwire.Checked;
            this.UpdateTripwireAimviewControls();
        }

        private void swAVTripwireDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Tripwire"].Distance = swAVTripwireDistance.Checked;
        }

        private void sldrAVTripwirePaintDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Tripwire"].PaintDistance = newValue;
        }

        private void sldrAVTripwireTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Tripwire"].TextDistance = newValue;
        }

        private void swAVQuestZones_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestZone"].Enabled = swAVQuestZones.Checked;
            this.UpdateQuestZoneAimviewControls();
        }

        private void swAVQuestZoneDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestZone"].Distance = swAVQuestZoneDistance.Checked;
        }

        private void swAVQuestZoneName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["QuestZone"].Name = swAVQuestZoneName.Checked;
        }

        private void sldrAVQuestZoneTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["QuestZone"].TextDistance = newValue;
        }

        private void swAVExfils_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Exfil"].Enabled = swAVExfils.Checked;
            this.UpdateExfilAimviewControls();
        }

        private void swAVExfilDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Exfil"].Distance = swAVExfilDistance.Checked;
        }

        private void swAVExfilName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Exfil"].Name = swAVExfilName.Checked;
        }

        private void sldrAVExfilTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Exfil"].TextDistance = newValue;
        }

        private void swAVTransits_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Transit"].Enabled = swAVTransits.Checked;
            this.UpdateTransitAimviewControls();
        }

        private void swAVTransitDistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Transit"].Distance = swAVTransitDistance.Checked;
        }

        private void swAVTransitName_CheckedChanged(object sender, EventArgs e)
        {
            this.config.AimviewSettings.ObjectSettings["Transit"].Name = swAVTransitName.Checked;
        }

        private void sldrAVTransitTextDistance_onValueChanged(object sender, int newValue)
        {
            this.config.AimviewSettings.ObjectSettings["Transit"].TextDistance = newValue;
        }
        #endregion
        #endregion

        #region Hotkeys
        #region Helper Functions
        private void SetChams(bool enabled)
        {
            this.config.Chams["Enabled"] = enabled;
            swChams.Checked = enabled;
        }

        private void SetImportantLootOnly(bool enabled)
        {
            this.config.ImportantLootOnly = enabled;
            swFilteredOnly.Checked = enabled;
        }

        private void SetRecoil(bool enabled)
        {
            this.config.Recoil = enabled;
            swRecoil.Checked = enabled;
        }

        private void SetWeaponSway(bool enabled)
        {
            this.config.WeaponSway = enabled;
            swWeaponSway.Checked = enabled;
        }

        private void SetOpticalThermal(bool enabled)
        {
            this.config.OpticThermalVision = enabled;
            swOpticalThermal.Checked = enabled;
        }

        private void SetThermalVision(bool enabled)
        {
            this.config.ThermalVision = enabled;
            swThermalVision.Checked = enabled;
        }

        private void SetShowContainers(bool enabled)
        {
            this.config.LootContainerSettings["Enabled"] = enabled;
            swContainers.Checked = enabled;
        }

        private void SetShowCorpses(bool enabled)
        {
            this.config.LootCorpses = enabled;
            swCorpses.Checked = enabled;
        }

        private void SetShowLoot(bool enabled)
        {
            this.config.LooseLoot = enabled;
            swLooseLoot.Checked = enabled;
        }

        private void SetTimescale(bool enabled)
        {
            this.config.TimeScale = enabled;
            swTimeScale.Checked = enabled;
        }

        private void SetThirdperson(bool enabled)
        {
            this.config.Thirdperson = enabled;
            swThirdperson.Checked = enabled;
        }

        private void UpdateHotkeyEntryData()
        {
            this.hotkeyUpdating = true;

            if (this.lastHotkeyEntry?.Key is not null)
                cboHotkeyKey.SelectedItem = cboHotkeyKey.Items.Cast<HotkeyKey>().FirstOrDefault(k => k.Key == this.lastHotkeyEntry.Key);
            else
                cboHotkeyKey.SelectedIndex = -1;

            rdbToggleKey.Checked = this.lastHotkeyEntry?.Type == HotkeyType.Toggle;
            rdbOnKey.Checked = !rdbToggleKey.Checked;


            if (!string.IsNullOrEmpty(this.lastHotkeyEntry?.Action))
                cboHotkeyAction.SelectedItem = this.lastHotkeyEntry.Action;
            else
                cboHotkeyAction.SelectedIndex = -1;

            cboHotkeyKey.Refresh();
            cboHotkeyAction.Refresh();

            this.hotkeyUpdating = false;
        }

        private void UpdateHotkeyEntriesList()
        {
            lstHotkeys.Items.Clear();

            foreach (var hotkey in this.config.Hotkeys)
            {
                var item = new ListViewItem(new[] {
                    hotkey.Action,
                    this.GetDisplayNameForKey(hotkey.Key),
                    hotkey.Type.ToString()
                });
                item.Tag = hotkey;
                lstHotkeys.Items.Add(item);
            }
        }

        private bool HasUnsavedHotkeyChanges()
        {
            var selectedAction = cboHotkeyAction.SelectedItem;
            var selectedKey = cboHotkeyKey.SelectedItem as HotkeyKey;

            if (this.lastHotkeyEntry is null || selectedAction is null || selectedKey is null)
                return false;

            return selectedAction.ToString() != this.lastHotkeyEntry.Action ||
                    selectedKey.Key != this.lastHotkeyEntry.Key ||
                    (rdbOnKey.Checked ? HotkeyType.OnKey : HotkeyType.Toggle) != this.lastHotkeyEntry.Type;
        }

        private void SaveHotkeyEntry()
        {
            if (this.hotkeyUpdating)
                return;

            if (this.HasUnsavedHotkeyChanges())
            {
                if (cboHotkeyKey.SelectedItem is null ||
                    cboHotkeyAction.SelectedItem is null ||
                    string.IsNullOrEmpty(cboHotkeyAction.SelectedItem.ToString()))
                {
                    this.ShowErrorDialog("Select a valid key & action.");
                    return;
                }

                var selectedKey = (HotkeyKey)cboHotkeyKey.SelectedItem;

                var hotkey = new Hotkey
                {
                    Action = cboHotkeyAction.SelectedItem.ToString(),
                    Key = selectedKey.Key,
                    Type = rdbOnKey.Checked ? HotkeyType.OnKey : HotkeyType.Toggle
                };

                var exists = this.config.Hotkeys.Any(h =>
                                                        h.Action == hotkey.Action &&
                                                        h != this.lastHotkeyEntry);

                if (exists)
                {
                    this.ShowErrorDialog("Selected action is already in use");
                    return;
                }

                var index = this.config.Hotkeys.IndexOf(this.lastHotkeyEntry);

                if (index != -1)
                    this.config.Hotkeys[index] = hotkey;
                else
                    this.config.Hotkeys.Add(hotkey);

                this.lastHotkeyEntry = hotkey;

                this.UpdateHotkeyEntriesList();
            }
        }

        private void CheckHotkeys()
        {
            foreach (var hotkey in this.config.Hotkeys)
            {
                var onKeyDown = hotkey.Type == HotkeyType.OnKey;
                var isTriggered = onKeyDown
                    ? InputManager.IsKeyDown(hotkey.Key)
                    : InputManager.IsKeyPressed(hotkey.Key);

                if (isTriggered)
                {
                    if (onKeyDown)
                        this.previouslyPressedKeys.Add(hotkey.Key);

                    this.PerformAction(hotkey.Action, hotkey.Type);
                }
            }
        }

        private void PerformAction(string action, HotkeyType hotkeyType)
        {
            var actionWithoutSpaces = action.Replace(" ", "");

            if (Enum.TryParse(actionWithoutSpaces, out HotkeyAction parsedAction) && hotkeyActions.TryGetValue(parsedAction, out var actionHandler))
            {
                this.BeginInvoke(new Action(() =>
                {
                    var enabled = hotkeyType == HotkeyType.OnKey || !this.config.GetConfigValue(action);
                    actionHandler(enabled);
                }));
            }
            else
            {
                switch (action)
                {
                    case "Zoom In":
                        this.ZoomIn(2, Cursor.Position);
                        break;
                    case "Zoom Out":
                        this.ZoomOut(2);
                        break;
                }
            }
        }

        private void HandleKeyUp(Keys key)
        {
            var relevantHotkeys = this.config.Hotkeys.Where(h => h.Type == HotkeyType.OnKey && h.Key == key);
            foreach (var hotkey in relevantHotkeys)
            {
                var actionWithoutSpaces = hotkey.Action.Replace(" ", "");

                if (Enum.TryParse(actionWithoutSpaces, out HotkeyAction action) && hotkeyActions.TryGetValue(action, out var actionHandler))
                    this.BeginInvoke(new Action(() => actionHandler(false)));
            }
        }

        private string GetDisplayNameForKey(Keys key)
        {
            return this.keyDisplayNames.TryGetValue(key, out string displayName) ? displayName : key.ToString();
        }
        #endregion

        #region Event Handlers
        private void btnAddHotkey_Click(object sender, EventArgs e)
        {
            var blankHotkey = new Hotkey { Action = "", Key = Keys.None, Type = HotkeyType.OnKey };
            var exists = this.config.Hotkeys.Any(h => h.Action == blankHotkey.Action && h.Key == blankHotkey.Key && h.Type == blankHotkey.Type);

            if (exists)
            {
                this.ShowErrorDialog("A blank hotkey already exists.");
                return;
            }

            this.config.Hotkeys.Add(blankHotkey);
            this.UpdateHotkeyEntriesList();

            var index = this.config.Hotkeys.IndexOf(blankHotkey);

            lstHotkeys.Items[index].Selected = true;
        }

        private void btnRemoveHotkey_Click(object sender, EventArgs e)
        {
            if (lstHotkeys.SelectedItems.Count < 1)
                return;

            var hotkeyToRemove = (Hotkey)lstHotkeys.SelectedItems[0].Tag;
            this.config.Hotkeys.Remove(hotkeyToRemove);

            this.lastHotkeyEntry = null;

            this.UpdateHotkeyEntriesList();
        }

        private void lstHotkeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.lastHotkeyEntry = lstHotkeys.SelectedItems.Count > 0 ? (Hotkey)lstHotkeys.SelectedItems[0].Tag : null;
            this.UpdateHotkeyEntryData();
        }

        private void cboHotkeyKey_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.hotkeyUpdating)
                this.SaveHotkeyEntry();
        }

        private void cboHotkeyAction_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.hotkeyUpdating)
                this.SaveHotkeyEntry();
        }

        private void rdbOnKey_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.hotkeyUpdating && rdbOnKey.Checked)
                this.SaveHotkeyEntry();
        }

        private void rdbToggleKey_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.hotkeyUpdating && rdbToggleKey.Checked)
                this.SaveHotkeyEntry();
        }
        #endregion
        #endregion

        #region Memory Writing
        #region Helper Functions
        private ThermalSettings GetSelectedThermalSetting()
        {
            return cboThermalType.SelectedItem?.ToString() == "Main"
                ? this.config.MainThermalSetting
                : this.config.OpticThermalSetting;
        }

        private async Task RefreshChamsAsync()
        {
            await Task.Run(() =>
            {
                Memory.Chams?.ChamsDisable();
                Memory.Chams?.ChamsEnable();
            });
        }
        #endregion
        #region Event Handlers
        private void swThirdperson_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Thirdperson = swThirdperson.Checked;
        }

        private void swJuggernaut_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Juggernaut = swJuggernaut.Checked;
        }

        private void swLootThroughWalls_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swLootThroughWalls.Checked;
            this.config.LootThroughWalls = enabled;

            sldrLootThroughWallsDistance.Enabled = enabled;
            lblSettingsMemoryWritingLootThroughWallsDistance.Enabled = enabled;
        }

        private void sldrLootThroughWallsDistance_onValueChanged(object sender, int newValue)
        {
            var pveMode = this.config.PvEMode;
            var distance = (float)newValue / 10;

            if (pveMode)
                this.config.LootThroughWallsDistancePvE = distance;
            else
            {
                if (distance > 3)
                    distance = 3;

                this.config.LootThroughWallsDistance = distance;
            }

            lblSettingsMemoryWritingLootThroughWallsDistance.Text = $"x{distance}";

            if (Memory.LocalPlayer is not null)
                Memory.PlayerManager.UpdateLootThroughWallsDistance = true;
        }

        private void swExtendedReach_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swExtendedReach.Checked;
            this.config.ExtendedReach = enabled;

            sldrExtendedReachDistance.Enabled = enabled;
            lblSettingsMemoryWritingExtendedReachDistance.Enabled = enabled;
        }

        private void sldrExtendedReachDistance_onValueChanged(object sender, int newValue)
        {
            var pveMode = this.config.PvEMode;
            var distance = (float)newValue / 10;

            if (pveMode)
                this.config.ExtendedReachDistancePvE = distance;
            else
            {
                if (distance > 4f)
                    distance = 4f;

                this.config.ExtendedReachDistance = distance;
            }

            lblSettingsMemoryWritingExtendedReachDistance.Text = $"x{distance}";

            if (Memory.LocalPlayer is not null)
                Memory.Toolbox.UpdateExtendedReachDistance = true;
        }

        private void sldrFOV_onValueChanged(object sender, int newValue)
        {
            this.config.FOV = newValue;
        }

        private void swInventoryBlur_CheckedChanged(object sender, EventArgs e)
        {
            this.config.InventoryBlur = swInventoryBlur.Checked;
        }

        private void swMedPanel_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MedInfoPanel = swMedPanel.Checked;
        }

        private void swRecoil_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swRecoil.Checked;
            this.config.Recoil = enabled;
            sldrXFactor.Enabled = enabled;
            sldrYFactor.Enabled = enabled;
        }

        private void swWeaponSway_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swWeaponSway.Checked;
            this.config.WeaponSway = enabled;
            sldrSwayFactor.Enabled = enabled;
        }

        private void sldrXFactor_onValueChanged(object sender, int newValue)
        {
            var newPercent = (float)newValue / 100;
            this.config.RecoilXPercent = newPercent;
        }

        private void sldrYFactor_onValueChanged(object sender, int newValue)
        {
            var newPercent = (float)newValue / 100;
            this.config.RecoilYPercent = newPercent;
        }

        private void sldrWeaponSway_onValueChanged(object sender, int newValue)
        {
            var newPercent = (float)newValue / 100;
            this.config.WeaponSwayPercent = newPercent;
        }

        private void swInstantADS_CheckedChanged(object sender, EventArgs e)
        {
            this.config.InstantADS = swInstantADS.Checked;
        }

        private void swNoVisor_CheckedChanged(object sender, EventArgs e)
        {
            this.config.NoVisor = swNoVisor.Checked;
        }

        private void swFrostBite_CheckedChanged(object sender, EventArgs e)
        {
            this.config.FrostBite = swFrostBite.Checked;
        }

        private void swThermalVision_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swThermalVision.Checked;
            this.config.ThermalVision = enabled;

            mcSettingsMemoryWritingThermal.Enabled = enabled || this.config.OpticThermalVision;
        }

        private void swOpticalThermal_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swOpticalThermal.Checked;
            this.config.OpticThermalVision = enabled;

            mcSettingsMemoryWritingThermal.Enabled = enabled || this.config.ThermalVision;
        }

        private void swNightVision_CheckedChanged(object sender, EventArgs e)
        {
            this.config.NightVision = swNightVision.Checked;
        }

        private void swNoWeaponMalfunctions_CheckedChanged(object sender, EventArgs e)
        {
            this.config.NoWeaponMalfunctions = swNoWeaponMalfunctions.Checked;
        }

        private void sldrMagDrillsSpeed_onValueChanged(object sender, int newValue)
        {
            this.config.MagDrillSpeed = newValue;

            if (this.config.MaxSkills["Mag Drills"] && Memory.LocalPlayer is not null)
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
            this.config.InfiniteStamina = swInfiniteStamina.Checked;
        }

        private void sldrThrowStrength_onValueChanged(object sender, int newValue)
        {
            this.config.ThrowPowerStrength = newValue;

            if (this.config.MaxSkills["Strength"] && Memory.LocalPlayer is not null)
            {
                var throwDistanceSkill = Memory.PlayerManager.Skills["Strength"]["BuffThrowDistanceInc"];
                throwDistanceSkill.MaxValue = (float)newValue / 100;

                Memory.PlayerManager?.SetMaxSkill(throwDistanceSkill);
            }
        }

        private void cboThermalType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();

            var colorCoefficient = (int)(thermalSettings.ColorCoefficient * 100);
            var minTemperature = (int)((thermalSettings.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            var rampShift = (int)((thermalSettings.RampShift + 1.0f) * 100.0f);

            sldrThermalColorCoefficient.Value = colorCoefficient;
            sldrMinTemperature.Value = minTemperature;
            sldrThermalRampShift.Value = rampShift;
            cboThermalColorScheme.SelectedIndex = thermalSettings.ColorScheme;

            if (Memory.Toolbox is not null)
                Memory.Toolbox.UpdateThermalSettings = true;
        }

        private void cboThermalColorScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorScheme = cboThermalColorScheme.SelectedIndex;

            if (Memory.Toolbox is not null)
                Memory.Toolbox.UpdateThermalSettings = true;
        }

        private void sldrThermalColorCoefficient_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorCoefficient = (float)Math.Round(newValue / 100.0f, 4, MidpointRounding.AwayFromZero);

            if (Memory.Toolbox is not null)
                Memory.Toolbox.UpdateThermalSettings = true;
        }

        private void sldrMinTemperature_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.MinTemperature = (float)Math.Round((0.01f - 0.001f) * (newValue / 100.0f) + 0.001f, 4, MidpointRounding.AwayFromZero);

            if (Memory.Toolbox is not null)
                Memory.Toolbox.UpdateThermalSettings = true;
        }

        private void sldrThermalRampShift_onValueChanged(object sender, int newValue)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.RampShift = (float)Math.Round((newValue / 100.0f) - 1.0f, 4, MidpointRounding.AwayFromZero);

            if (Memory.Toolbox is not null)
                Memory.Toolbox.UpdateThermalSettings = true;
        }

        private void swMasterSwitch_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = swMasterSwitch.Checked;
            this.config.MasterSwitch = isChecked;

            mcSettingsMemoryWritingGlobal.Enabled = isChecked;
            mcSettingsMemoryWritingGear.Enabled = isChecked;
            mcSettingsMemoryWritingThermal.Enabled = isChecked;
            mcSettingsMemoryWritingSkillBuffs.Enabled = isChecked;
            mcSettingsMemoryWritingChams.Enabled = isChecked;
            mcSettingsMemoryWritingWorld.Enabled = isChecked;

            if (isChecked)
                Memory.Toolbox?.StartToolbox();
            else
                Memory.Toolbox?.StopToolbox();
        }

        private void swMaxEndurance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Endurance"] = swMaxEndurance.Checked;
        }

        private void swMaxStrength_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swMaxStrength.Checked;
            this.config.MaxSkills["Strength"] = enabled;

            sldrThrowStrength.Enabled = enabled;
        }

        private void swMaxVitality_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Vitality"] = swMaxVitality.Checked;
        }

        private void swMaxHealth_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Health"] = swMaxHealth.Checked;
        }

        private void swMaxStressResistance_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Stress Resistance"] = swMaxStressResistance.Checked;
        }

        private void swMaxMetabolism_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Metabolism"] = swMaxMetabolism.Checked;
        }

        private void swMaxImmunity_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Immunity"] = swMaxImmunity.Checked;
        }

        private void swMaxPerception_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Perception"] = swMaxPerception.Checked;
        }

        private void swMaxIntellect_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Intellect"] = swMaxIntellect.Checked;
        }

        private void swMaxAttention_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Attention"] = swMaxAttention.Checked;
        }

        private void swMaxCovertMovement_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Covert Movement"] = swMaxCovertMovement.Checked;
        }

        private void swMaxThrowables_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Throwables"] = swMaxThrowables.Checked;
        }

        private void swMaxSurgery_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Surgery"] = swMaxSurgery.Checked;
        }

        private void swMaxSearch_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Search"] = swMaxSearch.Checked;
        }

        private void swMaxMagDrills_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swMaxMagDrills.Checked;
            this.config.MaxSkills["Mag Drills"] = enabled;
            sldrMagDrillsSpeed.Enabled = enabled;
        }

        private void swMaxLightVests_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Light Vests"] = swMaxLightVests.Checked;
        }

        private void swMaxHeavyVests_CheckedChanged(object sender, EventArgs e)
        {
            this.config.MaxSkills["Heavy Vests"] = swMaxHeavyVests.Checked;
        }

        private void ToggleChamsControls()
        {
            var isChecked = swChams.Checked;

            swChamsPMCs.Enabled = isChecked;
            swChamsPlayerScavs.Enabled = isChecked;
            swChamsBosses.Enabled = isChecked;
            swChamsRogues.Enabled = isChecked;
            swChamsEvent.Enabled = isChecked;
            swChamsCultists.Enabled = isChecked;
            swChamsScavs.Enabled = isChecked;
            swChamsTeammates.Enabled = isChecked;
            swChamsCorpses.Enabled = isChecked;
            swChamsRevert.Enabled = isChecked;
        }

        private void swChams_CheckedChanged(object sender, EventArgs e)
        {
            var isChecked = swChams.Checked;
            this.config.Chams["Enabled"] = isChecked;

            this.ToggleChamsControls();
        }

        private void swChamsPlayers_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["PMCs"] = swChamsPMCs.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsPlayerScavs_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["PlayerScavs"] = swChamsPlayerScavs.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsBosses_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Bosses"] = swChamsBosses.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsRogues_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Rogues"] = swChamsRogues.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsEvent_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Event"] = swChamsEvent.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsCultists_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Cultists"] = swChamsCultists.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsScavs_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Scavs"] = swChamsScavs.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsTeammates_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Teammates"] = swChamsTeammates.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsCorpses_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["Corpses"] = swChamsCorpses.Checked;
            this.RefreshChamsAsync();
        }

        private void swChamsRevert_CheckedChanged(object sender, EventArgs e)
        {
            this.config.Chams["RevertOnClose"] = swChamsRevert.Checked;
        }

        private void swNoFog_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Fog = swNoFog.Checked;
        }

        private void swNoRain_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Rain = swNoRain.Checked;
        }

        private void swNoClouds_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Clouds = swNoClouds.Checked;
        }

        private void swNoShadows_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Shadows = swNoShadows.Checked;
        }

        private void swNoSun_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Sun = swNoSun.Checked;
        }

        private void swNoMoon_CheckedChanged(object sender, EventArgs e)
        {
            this.config.WorldSettings.Moon = swNoMoon.Checked;
        }

        private void swSunIntensity_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swSunIntensity.Checked;
            this.config.WorldSettings.SunLight = enabled;
            sldrSunIntensity.Enabled = enabled;
        }

        private void sldrSunIntensity_onValueChanged(object sender, int newValue)
        {
            this.config.WorldSettings.SunLightIntensity = newValue;
        }

        private void swMoonIntensity_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swMoonIntensity.Checked;
            this.config.WorldSettings.MoonLight = enabled;
            sldrMoonIntensity.Enabled = enabled;
        }

        private void sldrMoonIntensity_onValueChanged(object sender, int newValue)
        {
            this.config.WorldSettings.MoonLightIntensity = newValue;
        }

        private void swFreezeTime_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swFreezeTime.Checked;
            this.config.WorldSettings.FreezeTime = enabled;

            sldrTimeOfDay.Enabled = enabled;
        }

        private void sldrTimeOfDay_onValueChanged(object sender, int newValue)
        {
            this.config.WorldSettings.TimeOfDay = sldrTimeOfDay.Value;
        }

        private void swTimeScale_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swTimeScale.Checked;
            this.config.TimeScale = enabled;
            sldrTimeScaleFactor.Enabled = enabled;

            lblSettingsMemoryWritingTimeScaleFactor.Enabled = enabled;
        }

        private void sldrTimeScaleFactor_onValueChanged(object sender, int newValue)
        {
            if (newValue < 10)
                newValue = 10;
            else if (newValue > 18)
                newValue = 18;

            this.config.TimeScaleFactor = (float)newValue / 10;
            lblSettingsMemoryWritingTimeScaleFactor.Text = $"x{(this.config.TimeScaleFactor)}";
        }
        #endregion
        #endregion

        #region Loot
        #region Helper Functions
        private void InitiateContainerList()
        {
            var containers = TarkovDevManager.AllLootContainers
                .Values
                .Select(x => x.Name)
                .OrderByDescending(x => x)
                .Distinct()
                .ToList();

            foreach (var container in containers)
            {
                var checkbox = new MaterialCheckbox
                {
                    Text = container,
                    Checked = this.config.LootContainerSettings.TryGetValue(container, out bool value) && value
                };
                checkbox.CheckedChanged += this.ContainerCheckbox_CheckedChanged;
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
            swSubItems.Enabled = processLoot && swCorpses.Checked;
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
            this.config.ProcessLoot = processLoot;

            this.UpdateLootControls();
            this.UpdateQuestControls();

            if (!processLoot)
            {
                this.RefreshLootListItems();
                return;
            }

            if (this.config.LootItemRefresh)
                this.Loot?.StartAutoRefresh();
            else
                this.Loot?.RefreshLoot(true);
        }

        private void btnRefreshLoot_Click(object sender, EventArgs e)
        {
            lstLootItems.Items.Clear();

            this.Loot?.RefreshLoot(true);
        }

        private void swLooseLoot_CheckedChanged(object sender, EventArgs e)
        {
            var looseLoot = swLooseLoot.Checked;

            this.config.LooseLoot = looseLoot;

            this.Loot?.ApplyFilter();
        }

        private void swFilteredOnly_CheckedChanged(object sender, EventArgs e)
        {
            this.config.ImportantLootOnly = swFilteredOnly.Checked;
        }

        private void swSubItems_CheckedChanged(object sender, EventArgs e)
        {
            this.config.SubItems = swSubItems.Checked;
        }

        private void swItemValue_CheckedChanged(object sender, EventArgs e)
        {
            this.config.LootValue = swItemValue.Checked;
        }

        private void swCorpses_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swCorpses.Checked;

            this.config.LootCorpses = enabled;
            swSubItems.Enabled = enabled;

            this.Loot?.ApplyFilter();
        }

        private void swAutoLootRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAutoLootRefresh.Checked;
            var processLoot = swProcessLoot.Checked;
            this.config.LootItemRefresh = enabled;

            cboAutoRefreshMap.Enabled = (processLoot && enabled);
            sldrAutoLootRefreshDelay.Enabled = (processLoot && enabled);

            if (!processLoot)
                return;

            if (enabled)
                this.Loot?.StartAutoRefresh();
            else
                this.Loot?.StopAutoRefresh();
        }

        private void cboAutoRefreshMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !this.config.LootItemRefreshSettings.ContainsKey(mapName))
                return;

            sldrAutoLootRefreshDelay.Value = this.config.LootItemRefreshSettings[mapName];
        }

        private void sldrAutoRefreshDelay_onValueChanged(object sender, int newValue)
        {
            var mapName = cboAutoRefreshMap.SelectedItem.ToString();

            if (string.IsNullOrEmpty(mapName) || !this.config.LootItemRefreshSettings.ContainsKey(mapName))
                return;

            if (newValue != this.config.LootItemRefreshSettings[mapName])
                this.config.LootItemRefreshSettings[mapName] = newValue;
        }

        // Quest Helper
        private void swQuestHelper_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swQuestHelper.Checked;

            this.config.QuestHelper = enabled;

            this.UpdateQuestControls();
        }

        private void swQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            this.config.QuestItems = swQuestItems.Checked;
        }

        private void swQuestLootItems_CheckedChanged(object sender, EventArgs e)
        {
            this.config.QuestLootItems = swQuestLootItems.Checked;
            this.Loot?.ApplyFilter();
        }

        private void swQuestLocations_CheckedChanged(object sender, EventArgs e)
        {
            this.config.QuestLocations = swQuestLocations.Checked;
        }

        private void swAutoTaskRefresh_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = swAutoTaskRefresh.Checked;

            this.config.QuestTaskRefresh = enabled;
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

            this.config.QuestTaskRefreshDelay = newValue;
        }

        private void swUnknownQuestItems_CheckedChanged(object sender, EventArgs e)
        {
            this.config.UnknownQuestItems = swUnknownQuestItems.Checked;
        }

        private void btnRefreshTasks_Click(object sender, EventArgs e)
        {
            Memory.QuestManager?.RefreshQuests(true);
        }

        private void sldrMinRegularLoot_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 10)
            {
                var value = newValue * 1000;
                this.config.MinLootValue = value;

                this.Loot?.ApplyFilter();
            }
        }

        private void sldrMinImportantLoot_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 250)
            {
                var value = newValue * 1000;
                this.config.MinImportantLootValue = value;

                this.Loot?.ApplyFilter();
            }
        }

        private void sldrMinCorpse_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 10)
            {
                var value = newValue * 1000;
                this.config.MinCorpseValue = value;

                this.Loot?.ApplyFilter();
            }
        }

        private void sldrMinSubItems_onValueChanged(object sender, int newValue)
        {
            if (newValue >= 5)
            {
                var value = newValue * 1000;
                this.config.MinSubItemValue = value;

                this.Loot?.ApplyFilter();
            }

        }

        // Loot Ping
        private void sldrLootPingAnimationSpeed_onValueChanged(object sender, int newValue)
        {
            this.config.LootPing["AnimationSpeed"] = newValue;
        }

        private void sldrLootPingMaxRadius_onValueChanged(object sender, int newValue)
        {
            this.config.LootPing["Radius"] = newValue;
        }

        private void sldrLootPingRepetition_onValueChanged(object sender, int newValue)
        {
            if (newValue < 1)
                newValue = 1;

            this.config.LootPing["Repetition"] = newValue;
        }

        // Container Settings
        private void ContainerCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            var checkbox = (MaterialCheckbox)sender;

            this.config.LootContainerSettings[checkbox.Text] = checkbox.Checked;

            this.Loot?.ApplyFilter();
        }

        private void swContainers_CheckedChanged(object sender, EventArgs e)
        {
            var containersEnabled = swContainers.Checked;
            var aimviewContainersEnabled = this.config.AimviewSettings.ObjectSettings["Container"].Enabled;

            this.config.LootContainerSettings["Enabled"] = containersEnabled;

            lstContainers.Enabled = (containersEnabled || aimviewContainersEnabled);
            sldrContainerDistance.Enabled = containersEnabled;

            this.Loot?.ApplyFilter();
        }

        private void sldrContainerDistance_onValueChanged(object sender, int newValue)
        {
            this.config.LootContainerDistance = newValue;
            this.Loot?.ApplyFilter();
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
            var enemyAI = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => !x.IsHuman && faction.Names.Contains(x.Name))
                .ToList();

            Parallel.ForEach(enemyAI ?? Enumerable.Empty<Player>(), player =>
            {
                this.aiFactions.IsInFaction(player.Name, out var playerType);
                player.Type = playerType;
            });
        }

        private void RefreshPlayerTypeByName(string name)
        {
            var enemyAI = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => !x.IsHuman && x.Name == name)
                .ToList();

            enemyAI?.ForEach(Player =>
            {
                this.aiFactions.IsInFaction(Player.Name, out var playerType);
                Player.Type = playerType;
            });
        }

        private void UpdateFactions(int index = 0)
        {
            var factions = this.aiFactions.Factions;

            lstFactions.BeginUpdate();
            lstFactions.Items.Clear();
            lstFactions.Items.AddRange(factions.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());
            lstFactions.EndUpdate();

            if (lstFactions.Items.Count > 0)
            {
                lstFactions.Items[index].Selected = true;
                this.UpdateFactionData();
                this.UpdateFactionEntriesList();
            }
        }

        private void UpdateFactionData()
        {
            var selectedFaction = this.GetActiveFaction();
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
            txtFactionEntryName.Text = this.lastFactionEntry ?? "";
        }

        private void UpdateFactionEntriesList()
        {
            var selectedFaction = this.GetActiveFaction();
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
            var selectedFaction = this.GetActiveFaction();
            if (selectedFaction is null)
                return false;

            var selectedPlayerType = (PlayerType)cboFactionType.SelectedItem;
            return txtFactionName.Text != selectedFaction.Name || selectedPlayerType != selectedFaction.PlayerType;
        }

        private bool HasUnsavedFactionEntryChanges()
        {
            if (this.lastFactionEntry is null)
                return false;

            return txtFactionEntryName.Text != this.lastFactionEntry;
        }

        private void SaveFaction()
        {
            if (this.HasUnsavedFactionChanges())
            {
                if (string.IsNullOrEmpty(txtFactionName.Text))
                {
                    this.ShowErrorDialog("Add some text to the faction name textbox (minimum 1 character)");
                    return;
                }
                var selectedFaction = this.GetActiveFaction();
                var index = this.aiFactions.Factions.IndexOf(selectedFaction);

                selectedFaction.Name = txtFactionName.Text;
                selectedFaction.PlayerType = (PlayerType)cboFactionType.SelectedItem;

                this.aiFactions.UpdateFaction(selectedFaction, index);

                this.UpdateFactions(lstFactions.SelectedIndices[0]);
                this.RefreshPlayerTypeByFaction(selectedFaction);
            }
        }

        private void SaveFactionEntry()
        {
            if (this.HasUnsavedFactionEntryChanges())
            {
                if (string.IsNullOrEmpty(txtFactionEntryName.Text))
                {
                    this.ShowErrorDialog("Add some text to the entry name textbox (minimum 1 character)");
                    return;
                }

                var selectedFaction = this.GetActiveFaction();
                var entry = this.lastFactionEntry;
                var index = selectedFaction.Names.IndexOf(entry);

                entry = txtFactionEntryName.Text;

                this.aiFactions.UpdateEntry(selectedFaction, entry, index);

                this.lastFactionEntry = entry;

                this.UpdateFactionEntriesList();
                this.RefreshPlayerTypeByName(entry);
            }
        }

        private void RemoveFactionEntry(AIFactionManager.Faction selectedFaction, string name)
        {
            if (this.ShowConfirmationDialog("Are you sure you want to remove this entry?", "Are you sure?") == DialogResult.OK)
            {
                this.aiFactions.RemoveEntry(selectedFaction, name);
                this.lastFactionEntry = null;

                this.UpdateFactionEntriesList();
                this.UpdateFactionEntryData();
                this.RefreshPlayerTypeByName(name);
            }
        }
        #endregion

        #region Event Handlers
        private void txtFactionEntryName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var selectedFaction = this.GetActiveFaction();

                var newEntryName = txtFactionEntryName.Text;
                var existingEntry = selectedFaction.Names.FirstOrDefault(entry => entry == newEntryName);

                if (existingEntry is not null)
                {
                    this.ShowErrorDialog($"An entry with the name '{newEntryName}' already exists. Please edit or delete the existing entry.");
                    return;
                }

                this.SaveFactionEntry();
            }
        }

        private void txtFactionName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var selectedFaction = this.GetActiveFaction();

                var newFactionName = txtFactionName.Text;
                var existingEntry = this.aiFactions.Factions.FirstOrDefault(entry => entry.Name == newFactionName);

                if (existingEntry is not null)
                {
                    this.ShowErrorDialog($"A faction with the name '{newFactionName}' already exists. Please edit or delete the existing faction.");
                    return;
                }

                this.SaveFaction();
            }
        }

        private void btnAddFactionEntry_Click(object sender, EventArgs e)
        {
            var selectedFaction = this.GetActiveFaction();

            if (selectedFaction is null)
                return;

            var existingEntry = selectedFaction.Names.FirstOrDefault(entry => entry == "New Entry");

            if (existingEntry is not null)
            {
                this.ShowErrorDialog($"An entry with the name '{existingEntry}' already exists. Please edit or delete the existing entry.");
                return;
            }

            this.aiFactions.AddEmptyEntry(selectedFaction);
            this.UpdateFactionEntriesList();
        }

        private void btnAddFaction_Click(object sender, EventArgs e)
        {
            var existingFaction = this.aiFactions.Factions.FirstOrDefault(faction => faction.Name == "Default");

            if (existingFaction is not null)
            {
                this.ShowErrorDialog($"A faction with the name '{existingFaction.Name}' already exists. Please edit or delete the existing faction.");
                return;
            }

            this.aiFactions.AddEmptyFaction();
            this.UpdateFactions();
        }

        private void btnRemoveFactionEntry_Click(object sender, EventArgs e)
        {
            var selectedFaction = this.GetActiveFaction();
            var selectedEntry = this.lastFactionEntry;

            if (selectedFaction is not null)
                this.RemoveFactionEntry(selectedFaction, selectedEntry);
        }

        private void btnRemoveFaction_Click(object sender, EventArgs e)
        {
            var selectedFaction = this.GetActiveFaction();

            if (selectedFaction is null)
                return;

            var factions = this.aiFactions.Factions;

            if (factions.Count == 1)
            {
                if (this.ShowConfirmationDialog("Removing the last faction will automatically create a new one", "Warning") == DialogResult.OK)
                {
                    this.aiFactions.RemoveFaction(lstFactions.SelectedIndices[0]);
                    this.aiFactions.AddEmptyFaction();
                    this.RefreshPlayerTypeByFaction(selectedFaction);
                }
            }
            else
            {
                if (this.ShowConfirmationDialog("Are you sure you want to delete this faction?", "Warning") == DialogResult.OK)
                {
                    this.aiFactions.RemoveFaction(selectedFaction);
                    this.RefreshPlayerTypeByFaction(selectedFaction);
                }
            }

            this.UpdateFactions();
        }

        private void cboFactionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SaveFaction();
        }

        private void lstFactionEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SaveFactionEntry();
            this.lastFactionEntry = lstFactionEntries.FocusedItem?.Text ?? null;
            this.UpdateFactionEntryData();
        }

        private void lstFactions_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.lastFactionEntry = null;

            this.UpdateFactionData();
            this.UpdateFactionEntriesList();
            this.UpdateFactionEntryData();
        }
        #endregion
        #endregion

        #region Colors
        #region Helper Functions
        private void UpdateThemeColors()
        {
            var colorScheme = new ColorScheme(
                picOtherPrimary.BackColor,
                picOtherPrimaryDark.BackColor,
                picOtherPrimaryLight.BackColor,
                picOtherAccent.BackColor,
                TextShade.WHITE
            );

            MaterialSkinManager.Instance.ColorScheme = colorScheme;

            this.UpdatePaintColorControls();

            this.BeginInvoke(new Action(() =>
            {
                this.Invalidate();
                this.Refresh();
            }));
        }

        private Color DefaultPaintColorToColor(string name)
        {
            PaintColor.Colors color = Config.DefaultPaintColors[name];
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private void UpdatePaintColorControls()
        {
            var colors = this.config.PaintColors;

            Action<PictureBox, string> setColor = (pictureBox, name) =>
            {
                if (colors.TryGetValue(name, out var color))
                {
                    pictureBox.BackColor = Color.FromArgb(color.A, color.R, color.G, color.B);
                }
                else
                {
                    var defaultColor = Config.DefaultPaintColors[name];
                    colors[name] = defaultColor;

                    pictureBox.BackColor = Color.FromArgb(
                        defaultColor.A,
                        defaultColor.R,
                        defaultColor.G,
                        defaultColor.B
                    );
                }
            };

            // AI
            setColor(picAIBoss, "Boss");
            setColor(picAIBossGuard, "BossGuard");
            setColor(picAIBossFollower, "BossFollower");
            setColor(picAIRaider, "Raider");
            setColor(picAIRogue, "Rogue");
            setColor(picAICultist, "Cultist");
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

            // Transits
            setColor(picTransitText, "TransitText");
            setColor(picTransitIcon, "TransitIcon");

            // Loot/Quests
            setColor(picLootRegular, "RegularLoot");
            setColor(picLootImportant, "ImportantLoot");
            setColor(picQuestItem, "QuestItem");
            setColor(picQuestZone, "QuestZone");
            setColor(picRequiredQuestItem, "RequiredQuestItem");
            setColor(picLootPing, "LootPing");

            // Game World
            setColor(picGrenades, "Grenades");
            setColor(picTripwires, "Tripwires");
            setColor(picDeathMarker, "DeathMarker");

            // Other
            setColor(picOtherTextOutline, "TextOutline");
            setColor(picOtherChams, "Chams");
            setColor(picOtherPrimary, "Primary");
            setColor(picOtherPrimaryDark, "PrimaryDark");
            setColor(picOtherPrimaryLight, "PrimaryLight");
            setColor(picOtherAccent, "Accent");

            // Event/Temporary
            setColor(picEventFollowerOfMorana, "FollowerOfMorana");
            setColor(picEventZombie, "Zombie");
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

                if (this.config.PaintColors.ContainsKey(name))
                {
                    this.config.PaintColors[name] = paintColorToUse;

                    if (Extensions.SKColors.ContainsKey(name))
                        Extensions.SKColors[name] = new SKColor(col.R, col.G, col.B, col.A);
                }
                else
                {
                    this.config.PaintColors.Add(name, paintColorToUse);
                }
            }
        }
        #endregion

        #region Event Handlers
        // AI
        private void picAIBoss_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Boss", picAIBoss);
        }

        private void picAIBossGuard_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("BossGuard", picAIBossGuard);
        }

        private void picAIBossFollower_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("BossFollower", picAIBossFollower);
        }

        private void picAIRaider_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Raider", picAIRaider);
        }

        private void picAIRogue_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Rogue", picAIRogue);
        }

        private void picAICultist_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Cultist", picAICultist);
        }

        private void picAIScav_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Scav", picAIScav);
        }

        private void picAIOther_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Other", picAIOther);
        }

        // Players
        private void picPlayersUSEC_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("USEC", picPlayersUSEC);
        }

        private void picPlayersBEAR_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("BEAR", picPlayersBEAR);
        }

        private void picPlayersScav_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("PlayerScav", picPlayersScav);
        }

        private void picPlayersLocalPlayer_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("LocalPlayer", picPlayersLocalPlayer);
        }

        private void picPlayersTeammate_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Teammate", picPlayersTeammate);
        }

        private void picPlayersTeamHover_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("TeamHover", picPlayersTeamHover);
        }

        private void picPlayersSpecial_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Special", picPlayersSpecial);
        }

        // Exfiltration
        private void picExfilActiveText_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilActiveText", picExfilActiveText);
        }

        private void picExfilActiveIcon_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilActiveIcon", picExfilActiveIcon);
        }

        private void picExfilPendingText_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilPendingText", picExfilPendingText);
        }

        private void picExfilPendingIcon_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilPendingIcon", picExfilPendingIcon);
        }

        private void picExfilClosedText_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilClosedText", picExfilClosedText);
        }

        private void picExfilClosedIcon_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ExfilClosedIcon", picExfilClosedIcon);
        }

        // Transits
        private void picTransitText_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("TransitText", picTransitText);
        }

        private void picTransitIcon_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("TransitIcon", picTransitIcon);
        }

        // Loot / Quests
        private void picLootRegular_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("RegularLoot", picLootRegular);
        }

        private void picLootImportant_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("ImportantLoot", picLootImportant);
        }

        private void picQuestItem_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("QuestItem", picQuestItem);
        }

        private void picQuestZone_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("QuestZone", picQuestZone);
        }

        private void picRequiredQuestItem_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("RequiredQuestItem", picRequiredQuestItem);
        }

        private void picLootPing_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("LootPing", picLootPing);
        }

        // Game World
        private void picGrenades_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Grenades", picGrenades);
        }

        private void picTripwires_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Tripwires", picTripwires);
        }

        private void picDeathMarker_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("DeathMarker", picDeathMarker);
        }

        // Other
        private void picOtherTextOutline_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("TextOutline", picOtherTextOutline);
        }

        private void picOtherChams_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Chams", picOtherChams);
        }

        private void picOtherPrimary_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Primary", picOtherPrimary);
            this.UpdateThemeColors();
        }

        private void picOtherPrimaryDark_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("PrimaryDark", picOtherPrimaryDark);
            this.UpdateThemeColors();
        }

        private void picOtherPrimaryLight_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("PrimaryLight", picOtherPrimaryLight);
            this.UpdateThemeColors();
        }

        private void picOtherAccent_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Accent", picOtherAccent);
            this.UpdateThemeColors();
        }

        private void btnResetTheme_Click(object sender, EventArgs e)
        {
            this.config.PaintColors["Primary"] = Config.DefaultPaintColors["Primary"];
            this.config.PaintColors["PrimaryDark"] = Config.DefaultPaintColors["PrimaryDark"];
            this.config.PaintColors["PrimaryLight"] = Config.DefaultPaintColors["PrimaryLight"];
            this.config.PaintColors["Accent"] = Config.DefaultPaintColors["Accent"];

            picOtherPrimary.BackColor = this.DefaultPaintColorToColor("Primary");
            picOtherPrimaryDark.BackColor = this.DefaultPaintColorToColor("PrimaryDark");
            picOtherPrimaryLight.BackColor = this.DefaultPaintColorToColor("PrimaryLight");
            picOtherAccent.BackColor = this.DefaultPaintColorToColor("Accent");

            this.UpdateThemeColors();
        }

        // Event/Temporary
        private void picEventFollowerOfMorana_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("FollowerOfMorana", picEventFollowerOfMorana);
        }

        private void picEventZombie_Click(object sender, EventArgs e)
        {
            this.UpdatePaintColorByName("Zombie", picEventZombie);
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
            var enemyPlayers = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive && profile.Entries.Any(entry => entry.AccountID == x.AccountID))
                .ToList();

            enemyPlayers?.ForEach(player => player.RefreshWatchlistStatus());
        }

        private void RefreshWatchlistStatuses()
        {
            var enemyPlayers = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive)
                .ToList();

            enemyPlayers?.ForEach(player => player.RefreshWatchlistStatus());
        }

        private void RefreshWatchlistStatus(string accountID)
        {
            var enemyPlayer = this.AllPlayers?
                .Select(x => x.Value)
                .FirstOrDefault(x => x.IsHumanHostileActive && x.AccountID == accountID);

            enemyPlayer?.RefreshWatchlistStatus();
        }

        private void UpdateWatchlistProfiles(int index = 0)
        {
            var profiles = this.watchlist.Profiles;

            lstWatchlistProfiles.Items.Clear();
            lstWatchlistProfiles.Items.AddRange(profiles.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());

            if (lstWatchlistProfiles.Items.Count > 0)
            {
                lstWatchlistProfiles.Items[index].Selected = true;
                this.UpdateWatchlistEntriesList();
            }
        }

        private void UpdateWatchlistProfileData()
        {
            var selectedProfile = this.GetActiveWatchlistProfile();
            txtWatchlistProfileName.Text = selectedProfile?.Name ?? "";
        }

        private void UpdateWatchlistEntryData()
        {
            txtWatchlistAccountID.Text = this.lastWatchlistEntry?.AccountID ?? "";
            txtWatchlistTag.Text = this.lastWatchlistEntry?.Tag ?? "";
            txtWatchlistPlatformUsername.Text = this.lastWatchlistEntry?.PlatformUsername ?? "";
            swWatchlistIsStreamer.Checked = this.lastWatchlistEntry?.IsStreamer ?? false;
            rdbTwitch.Checked = this.lastWatchlistEntry?.Platform == 0;
            rdbYoutube.Checked = this.lastWatchlistEntry?.Platform == 1;
        }

        private void UpdateWatchlistEntriesList()
        {
            var selectedProfile = this.GetActiveWatchlistProfile();
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
            var selectedProfile = this.GetActiveWatchlistProfile();
            return (selectedProfile is not null && lstWatchlistProfiles.Text != selectedProfile.Name);
        }

        private bool HasUnsavedWatchlistEntryChanges()
        {
            if (this.lastWatchlistEntry is null)
                return false;

            return txtWatchlistAccountID.Text != this.lastWatchlistEntry.AccountID ||
                    txtWatchlistTag.Text != this.lastWatchlistEntry.Tag ||
                    txtWatchlistPlatformUsername.Text != this.lastWatchlistEntry.PlatformUsername ||
                    swWatchlistIsStreamer.Checked != this.lastWatchlistEntry.IsStreamer ||
                    (rdbTwitch.Checked ? 0 : 1) != this.lastWatchlistEntry.Platform;
        }

        private void SaveWatchlistProfile()
        {
            if (string.IsNullOrEmpty(txtWatchlistProfileName.Text))
            {
                this.ShowErrorDialog("Add some text to the profile name textbox (minimum 1 character)");
                return;
            }

            if (this.HasUnsavedWatchlistProfileChanges())
            {
                var selectedProfile = this.GetActiveWatchlistProfile();
                var index = this.watchlist.Profiles.IndexOf(selectedProfile);

                selectedProfile.Name = txtWatchlistProfileName.Text;

                this.watchlist.UpdateProfile(selectedProfile, index);

                this.UpdateWatchlistProfiles(lstWatchlistProfiles.SelectedIndices[0]);
                this.RefreshWatchlistStatusesByProfile(selectedProfile);
            }
        }

        private void SaveWatchlistEntry()
        {
            var selectedProfile = this.GetActiveWatchlistProfile();

            if (this.HasUnsavedWatchlistEntryChanges())
            {
                if (string.IsNullOrEmpty(txtWatchlistAccountID.Text) ||
                    string.IsNullOrEmpty(txtWatchlistTag.Text) ||
                    string.IsNullOrEmpty(txtWatchlistPlatformUsername.Text))
                {
                    this.ShowErrorDialog("Add some text to the account id / tag / platform username textboxes (minimum 1 character)");
                    return;
                }

                var entry = this.lastWatchlistEntry;
                var index = selectedProfile.Entries.IndexOf(entry);

                entry = new Watchlist.Entry()
                {
                    AccountID = txtWatchlistAccountID.Text,
                    Tag = txtWatchlistTag.Text,
                    IsStreamer = swWatchlistIsStreamer.Checked,
                    Platform = rdbTwitch.Checked ? 0 : 1,
                    PlatformUsername = txtWatchlistPlatformUsername.Text
                };

                this.watchlist.UpdateEntry(selectedProfile, entry, index);

                this.lastWatchlistEntry = entry;

                this.UpdateWatchlistEntriesList();
                this.RefreshWatchlistStatus(entry.AccountID);
            }
        }

        private void RemoveWatchlistEntry(Watchlist.Profile selectedProfile, Watchlist.Entry selectedEntry)
        {
            if (this.ShowConfirmationDialog("Are you sure you want to remove this entry?", "Are you sure?") == DialogResult.OK)
            {
                this.watchlist.RemoveEntry(selectedProfile, selectedEntry);

                this.lastWatchlistEntry = null;

                this.UpdateWatchlistEntriesList();
                this.RefreshWatchlistStatus(selectedEntry.AccountID);
                this.UpdateWatchlistEntryData();
            }
        }

        private void UpdateWatchlistPlayers(bool clearItems = false)
        {
            var enemyPlayers = this.AllPlayers?
                .Select(x => x.Value)
                .Where(x => x.IsHumanHostileActive)
                .ToList();

            if (clearItems)
                this.watchlistMatchPlayers.Clear();

            if (enemyPlayers != null)
            {
                var newPlayers = enemyPlayers
                    .Where(player => !this.watchlistMatchPlayers.Any(p => p.Name == player.Name))
                    .ToList();

                this.watchlistMatchPlayers.AddRange(newPlayers);
            }

            lstWatchlistPlayerList.BeginUpdate();
            lstWatchlistPlayerList.Items.Clear();
            lstWatchlistPlayerList.Items.AddRange(this.watchlistMatchPlayers
                .Select(entry => new ListViewItem
                {
                    Text = entry.Name,
                    Tag = entry,
                })
                .OrderBy(entry => entry.Text)
                .ToArray());
            lstWatchlistPlayerList.EndUpdate();
        }
        #endregion

        #region Event Handlers
        private void txtWatchlistAccountID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                this.SaveWatchlistEntry();
        }

        private void txtWatchlistTag_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                this.SaveWatchlistEntry();
        }

        private void txtWatchlistPlatformUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                this.SaveWatchlistEntry();
        }

        private void swWatchlistIsStreamer_CheckedChanged(object sender, EventArgs e)
        {
            this.SaveWatchlistEntry();
        }

        private void rdbTwitch_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbTwitch.Checked)
                this.SaveWatchlistEntry();
        }

        private void rdbYoutube_CheckedChanged(object sender, EventArgs e)
        {
            if (rdbYoutube.Checked)
                this.SaveWatchlistEntry();
        }

        private void txtWatchlistProfileName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                this.SaveWatchlistProfile();
        }

        private void btnAddWatchlistEntry_Click(object sender, EventArgs e)
        {
            var selectedProfile = this.GetActiveWatchlistProfile();
            var selectedPlayer = lstWatchlistPlayerList.SelectedItems.Count > 0 ? lstWatchlistPlayerList.SelectedItems[0].Tag as Player : null;

            if (selectedProfile is null)
                return;

            var existingEntry = selectedProfile.Entries.FirstOrDefault(entry => entry.AccountID == (selectedPlayer?.AccountID ?? "New Entry"));

            if (existingEntry is not null)
            {
                this.ShowErrorDialog($"An entry with the account id '{existingEntry.AccountID}' already exists. Please edit or delete the existing entry.");
                return;
            }

            if (selectedPlayer is not null)
            {
                this.watchlist.AddEntry(selectedProfile, selectedPlayer.AccountID, selectedPlayer.Name);
                this.RefreshWatchlistStatuses();
            }
            else
            {
                this.watchlist.AddEmptyEntry(selectedProfile);
            }

            this.UpdateWatchlistEntriesList();
        }

        private void btnAddWatchlistProfile_Click(object sender, EventArgs e)
        {
            var existingProfile = watchlist.Profiles.FirstOrDefault(profile => profile.Name == "Default");

            if (existingProfile is not null)
            {
                this.ShowErrorDialog($"A profile with the name '{existingProfile.Name}' already exists. Please edit or delete the existing profile.");
                return;
            }

            this.watchlist.AddEmptyProfile();
            this.UpdateWatchlistProfiles();
        }

        private void btnRemoveWatchlistProfile_Click(object sender, EventArgs e)
        {
            var selectedProfile = this.GetActiveWatchlistProfile();

            if (selectedProfile is null)
                return;

            var profiles = this.watchlist.Profiles;

            if (profiles.Count == 1)
            {
                if (this.ShowConfirmationDialog("Removing the last profile will automatically create a default one", "Warning") == DialogResult.OK)
                {
                    this.watchlist.RemoveProfile(lstWatchlistProfiles.SelectedIndices[0]);
                    this.watchlist.AddEmptyProfile();
                    this.RefreshWatchlistStatusesByProfile(selectedProfile);
                }
            }
            else
            {
                if (this.ShowConfirmationDialog("Are you sure you want to delete this profile?", "Warning") == DialogResult.OK)
                {
                    this.watchlist.RemoveProfile(selectedProfile);
                    this.RefreshWatchlistStatusesByProfile(selectedProfile);
                }
            }

            this.UpdateWatchlistProfiles();
        }

        private void btnRemoveWatchlistEntry_Click(object sender, EventArgs e)
        {
            var selectedWatchlist = this.GetActiveWatchlistProfile();
            var selectedEntry = this.lastWatchlistEntry;

            if (selectedWatchlist is not null)
                this.RemoveWatchlistEntry(selectedWatchlist, selectedEntry);
        }

        private void lstViewWatchlistEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SaveWatchlistEntry();
            this.lastWatchlistEntry = lstWatchlistEntries.SelectedItems.Count > 0 ? (Watchlist.Entry)lstWatchlistEntries.SelectedItems[0].Tag : null;
            this.UpdateWatchlistEntryData();
        }

        private void lstWatchlistProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.lastWatchlistEntry = null;

            this.UpdateWatchlistProfileData();
            this.UpdateWatchlistEntriesList();
            this.UpdateWatchlistEntryData();
        }

        private void btnResetPlayerlist_Click(object sender, EventArgs e)
        {
            this.UpdateWatchlistPlayers(true);
        }
        #endregion
        #endregion

        #region Loot Filter
        #region Helper Functions
        private LootFilterManager.Filter GetActiveLootFilter()
        {
            var itemCount = lstLootFilters.SelectedItems.Count;
            return itemCount > 0 ? (LootFilterManager.Filter)lstLootFilters.SelectedItems[0].Tag : null;
        }

        private bool HasUnsavedFilterChanges()
        {
            var selectedFilter = this.GetActiveLootFilter();

            if (selectedFilter is null)
                return false;

            return txtLootFilterName.Text != selectedFilter.Name ||
                   swLootFilterActive.Checked != selectedFilter.IsActive ||
                   picLootFilterColor.BackColor != Color.FromArgb(selectedFilter.Color.A, selectedFilter.Color.R, selectedFilter.Color.G, selectedFilter.Color.B);
        }

        private void UpdateLootFilters(int index = 0)
        {
            var lootFilters = this.config.Filters.OrderBy(lf => lf.Order).ToList();

            lstLootFilters.BeginUpdate();
            lstLootFilters.Items.Clear();
            lstLootFilters.Items.AddRange(lootFilters.Select(entry => new ListViewItem
            {
                Text = entry.Name,
                Tag = entry,
            }).ToArray());
            lstLootFilters.EndUpdate();

            if (lstLootFilters.Items.Count > 0)
            {
                lstLootFilters.Items[index].Selected = true;
                this.UpdateLootFilterData();
                this.UpdateLootFilterEntriesList();
            }

            this.Loot?.ApplyFilter();
        }

        private void UpdateLootFilterData()
        {
            var selectedFilter = this.GetActiveLootFilter();

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
                this.ShowErrorDialog("Add some text to the loot filter name textbox (minimum 1 character)");
                return;
            }

            var selectedFilter = this.GetActiveLootFilter();

            if (selectedFilter is null)
                return;

            if (this.HasUnsavedFilterChanges())
            {
                var index = this.config.Filters.IndexOf(selectedFilter);

                selectedFilter.Name = txtLootFilterName.Text;
                selectedFilter.IsActive = swLootFilterActive.Checked;
                selectedFilter.Color = new PaintColor.Colors
                {
                    R = picLootFilterColor.BackColor.R,
                    G = picLootFilterColor.BackColor.G,
                    B = picLootFilterColor.BackColor.B,
                    A = picLootFilterColor.BackColor.A
                };

                this.lootFilterManager.UpdateFilter(selectedFilter, index);
                this.UpdateLootFilters(lstLootFilters.SelectedIndices[0]);
            }
        }

        private void UpdateLootFilterOrders()
        {
            for (int i = 0; i < this.config.Filters.Count; i++)
            {
                this.config.Filters[i].Order = i + 1;
            }
        }

        private void UpdateLootFilterEntriesList()
        {
            var selectedFilter = this.GetActiveLootFilter();
            if (selectedFilter?.Items == null)
                return;

            var lootList = TarkovDevManager.AllItems;
            var matchingLoot = lootList.Values
                .Where(loot => selectedFilter.Items.Contains(loot.Item.id))
                .OrderBy(l => l.Item.name)
                .ToList();

            lstLootFilterEntries.BeginUpdate();
            lstLootFilterEntries.Items.Clear();
            lstLootFilterEntries.Items.AddRange(matchingLoot.Select(item => new ListViewItem
            {
                Text = item.Name,
                Tag = item,
                SubItems = { TarkovDevManager.FormatNumber(item.Value) }
            }).ToArray());
            lstLootFilterEntries.EndUpdate();
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

            var selectedFilter = this.GetActiveLootFilter();
            var selectedItem = (LootItem)cboLootFilterItemsToAdd.SelectedItem;

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
                LootFilterManager.SaveLootFilterManager(this.lootFilterManager);
                this.Loot?.ApplyFilter();
            }
        }

        private void btnRemoveLootFilterItem_Click(object sender, EventArgs e)
        {
            if (lstLootFilterEntries.SelectedItems.Count < 1)
                return;

            var selectedFilter = this.GetActiveLootFilter();
            var selectedItem = lstLootFilterEntries.SelectedItems[0];

            if (selectedItem?.Tag is LootItem lootItem)
            {
                selectedItem.Remove();
                this.lootFilterManager.RemoveFilterItem(selectedFilter, lootItem.Item.id);
                this.Loot?.ApplyFilter();
            }
        }

        private void btnFilterPriorityUp_Click(object sender, EventArgs e)
        {
            var selectedFilter = this.GetActiveLootFilter();

            if (selectedFilter is null || selectedFilter.Order == 1)
                return;

            var index = selectedFilter.Order - 1;
            var swapFilter = this.config.Filters.FirstOrDefault(f => f.Order == index);

            if (swapFilter is not null)
            {
                selectedFilter.Order = swapFilter.Order;
                swapFilter.Order = index + 1;
                LootFilterManager.SaveLootFilterManager(this.lootFilterManager);
                this.UpdateLootFilters(index - 1);
            }
        }

        private void btnFilterPriorityDown_Click(object sender, EventArgs e)
        {
            var selectedFilter = this.GetActiveLootFilter();

            if (selectedFilter is null || selectedFilter.Order == this.config.Filters.Count)
                return;

            var index = selectedFilter.Order;
            var swapFilter = this.config.Filters.FirstOrDefault(f => f.Order == index + 1);

            if (swapFilter is not null)
            {
                selectedFilter.Order = swapFilter.Order;
                swapFilter.Order = index;
                LootFilterManager.SaveLootFilterManager(this.lootFilterManager);
                this.UpdateLootFilters(index);
            }
        }

        private void btnAddFilter_Click(object sender, EventArgs e)
        {
            var existingFilter = this.config.Filters.FirstOrDefault(filter => filter.Name == "New Filter");

            if (existingFilter is not null)
            {
                this.ShowErrorDialog("A loot filter with the name 'New Filter' already exists. Please rename or delete the existing filter.");
                return;
            }

            this.lootFilterManager.AddEmptyProfile();
            this.UpdateLootFilters(this.config.Filters.Count - 1);
        }

        private void btnRemoveFilter_Click(object sender, EventArgs e)
        {
            var selectedFilter = this.GetActiveLootFilter();

            if (selectedFilter is null)
                return;

            if (this.config.Filters.Count == 1)
            {
                if (this.ShowConfirmationDialog("Removing the last filter will automatically create a blank one. Are you sure you want to proceed?", "Warning") == DialogResult.OK)
                {
                    this.lootFilterManager.RemoveFilter(lstLootFilters.SelectedIndices[0]);
                    this.lootFilterManager.AddEmptyProfile();
                    this.UpdateLootFilters();
                }
            }
            else
            {
                if (this.ShowConfirmationDialog("Are you sure you want to delete this filter?", "Warning") == DialogResult.OK)
                {
                    this.lootFilterManager.RemoveFilter(selectedFilter);
                    this.UpdateLootFilterOrders();
                    this.UpdateLootFilters();
                }
            }
        }

        private void txtLootFilterName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter)
            {
                this.SaveLootFilterChanges();
            }
        }

        private void picLootFilterColor_Click(object sender, EventArgs e)
        {
            if (colDialog.ShowDialog() == DialogResult.OK)
            {
                picLootFilterColor.BackColor = colDialog.Color;
                this.SaveLootFilterChanges();
            }
        }

        private void swLootFilterActive_CheckedChanged(object sender, EventArgs e)
        {
            this.SaveLootFilterChanges();
        }

        private void lstLootFilters_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.UpdateLootFilterData();
            this.UpdateLootFilterEntriesList();
        }
        #endregion
        #endregion
        #endregion
    }
}