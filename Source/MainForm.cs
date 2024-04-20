using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using static eft_dma_radar.LootFilter;

namespace eft_dma_radar
{
    public partial class frmMain : Form
    {
        private readonly Config _config;
        private readonly SKGLControl _mapCanvas;
        private readonly Stopwatch _fpsWatch = new();
        private readonly object _renderLock = new();
        private readonly object _loadMapBitmapsLock = new();
        private readonly System.Timers.Timer _mapChangeTimer = new(900);
        private readonly List<Map> _maps = new(); // Contains all maps from \\Maps folder

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
        private string CurrentMapName
        {
            get => Memory.MapName;
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
        /// Radar is in the process of loading loot. Radar may be paused during this operation.
        /// </summary>
        private bool LoadingLoot
        {
            get => Memory.LoadingLoot;
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
        #endregion

        #region Constructor
        /// <summary>
        /// GUI Constructor.
        /// </summary>
        public frmMain()
        {
            _config = Program.Config;

            InitializeComponent();

            _mapCanvas = new SKGLControl()
            {
                Size = new Size(50, 50),
                Dock = DockStyle.Fill,
                VSync = _config.Vsync
            };
            tabRadar.Controls.Add(_mapCanvas);
            chkMapFree.Parent = _mapCanvas;
            trkUIScale.ValueChanged += trkUIScale_ValueChanged;

            LoadConfig();
            LoadMaps();

            _mapChangeTimer.AutoReset = false;
            _mapChangeTimer.Elapsed += MapChangeTimer_Elapsed;

            this.DoubleBuffered = true;
            this.Shown += frmMain_Shown;

            _mapCanvas.PaintSurface += MapCanvas_PaintSurface;
            _mapCanvas.MouseMove += MapCanvas_MouseMovePlayer;
            _mapCanvas.MouseDown += MapCanvas_MouseDown;
            _mapCanvas.MouseUp += MapCanvas_MouseUp;

            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            _fpsWatch.Start();

        }
        #endregion

        #region Events
        /// <summary>
        /// Event fires when MainForm becomes visible. Loops endlessly but is asynchronously non-blocking.
        /// </summary>
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

        /// <summary>
        /// Event fires when switching Tab Pages.
        /// </summary>
        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == 2) // Player Loadouts Tab
            {
                rchTxtPlayerInfo.Clear();
                var enemyPlayers = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.IsHumanHostileActive)
                    .ToList()
                    .OrderBy(x => x.GroupID)
                    .ThenBy(x => x.Name);
                if (this.InGame && enemyPlayers is not null)
                {
                    var sb = new StringBuilder();
                    sb.Append(@"{\rtf1\ansi");

                    foreach (var player in enemyPlayers)
                    {
                        string title = $"*** {player.Name} ({player.Type})  L:{player.Lvl}";

                        if (player.GroupID != -1)
                            title += $" G:{player.GroupID}";
                        if (player.KDA != -1f)
                            title += $" KD{player.KDA.ToString("n1")}";

                        sb.Append(@$"\b {title} \b0 ");
                        sb.Append(@" \line ");

                        var gear = player.Gear;

                        if (gear is not null)
                            foreach (var slot in gear)
                            {
                                sb.Append(@$"\b {slot.Key}: \b0 ");
                                sb.Append(slot.Value.Long);
                                sb.Append(@" \line ");
                            }
                        else
                            sb.Append(@" ERROR retrieving gear \line");
                        sb.Append(@" \line ");
                    }
                    sb.Append(@"}");
                    rchTxtPlayerInfo.Rtf = sb.ToString();
                }
            }
        }

        /// <summary>
        /// Fired when 'Is Active' checkbox for a filter is changed, saves config & then applies the lootfilter
        /// </summary>
        private void chkLootFilterActive_CheckedChanged(object sender, EventArgs e)
        {
            LootFilter selectedFilter = (LootFilter)cboFilters.SelectedItem;
            selectedFilter.IsActive = chkLootFilterActive.Checked;

            Config.SaveConfig(_config);
            this.Loot?.ApplyFilter();
        }

        /// <summary>
        /// Fired when NightVision checkbox has been adjusted
        /// </summary>
        private void chkNightVision_CheckedChanged(object sender, EventArgs e)
        {
            _config.NightVisionEnabled = chkNightVision.Checked;
        }

        /// <summary>
        /// Fired when ThermalVision checkbox has been adjusted
        /// </summary>
        private void chkThermalVision_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = chkThermalVision.Checked;

            _config.ThermalVisionEnabled = enabled;
            grpThermalSettings.Enabled = enabled;
        }

        /// <summary>
        /// Fired when OpticThermalVision checkbox has been adjusted
        /// </summary>
        private void chkOpticThermalVision_CheckedChanged(object sender, EventArgs e)
        {
            var enabled = chkOpticThermalVision.Checked;
            _config.OpticThermalVisionEnabled = enabled;
            grpThermalSettings.Enabled = enabled;
        }

        /// <summary>
        /// Fired when NoVisor checkbox has been adjusted
        /// </summary>
        private void chkNoVisor_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoVisorEnabled = chkNoVisor.Checked;
        }

        /// <summary>
        /// Fired when No Recoil checkbox has been adjusted
        /// </summary>
        private void chkNoRecoilSway_CheckedChanged(object sender, EventArgs e)
        {
            _config.NoRecoilSwayEnabled = chkNoRecoilSway.Checked;
        }

        private void chkJumpPower_CheckedChanged(object sender, EventArgs e)
        {
            _config.JumpPowerEnabled = chkJumpPower.Checked;
            trkJumpPower.Visible = chkJumpPower.Checked;
        }

        private void chkThrowPower_CheckedChanged(object sender, EventArgs e)
        {
            _config.ThrowPowerEnabled = chkThrowPower.Checked;
            trkThrowPower.Visible = chkThrowPower.Checked;
        }

        private void chkMagDrills_CheckedChanged(object sender, EventArgs e)
        {
            _config.MagDrillsEnabled = chkMagDrills.Checked;
            trkMagDrills.Visible = chkMagDrills.Checked;
        }

        private void trkJumpPower_Scroll(object sender, EventArgs e)
        {
            _config.JumpPowerStrength = trkJumpPower.Value;

            if (chkJumpPower.Checked && Memory.LocalPlayer is not null)
            {
                Memory.PlayerManager.SetMaxSkill(PlayerManager.Skills.JumpStrength);
            }
        }

        private void trkThrowPower_Scroll(object sender, EventArgs e)
        {
            _config.ThrowPowerStrength = trkThrowPower.Value;

            if (chkThrowPower.Checked && Memory.LocalPlayer is not null)
            {
                Memory.PlayerManager.SetMaxSkill(PlayerManager.Skills.ThrowStrength);
            }
        }

        private void trkMagDrills_Scroll(object sender, EventArgs e)
        {
            _config.MagDrillSpeed = trkMagDrills.Value;

            if (chkMagDrills.Checked && Memory.LocalPlayer is not null)
            {
                Memory.PlayerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsLoad);
                Memory.PlayerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsUnload);
            }
        }

        private void chkIncreaseMaxWeight_CheckedChanged(object sender, EventArgs e)
        {
            _config.IncreaseMaxWeightEnabled = chkIncreaseMaxWeight.Checked;
        }

        private void chkDoubleSearch_CheckedChanged(object sender, EventArgs e)
        {
            _config.DoubleSearchEnabled = chkDoubleSearch.Checked;
        }

        private void chkShowLoot_CheckedChanged(object sender, EventArgs e)
        {
            _config.LootEnabled = chkShowLoot.Checked;
        }

        private void chkQuestHelper_CheckedChanged(object sender, EventArgs e)
        {
            _config.QuestHelperEnabled = chkQuestHelper.Checked;
        }

        private void picQuestItemsColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("QuestItem", picQuestItemsColor);
        }

        private void picQuestZonesColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("QuestZone", picQuestZonesColor);
        }

        private void chkHideExfilNames_CheckedChanged(object sender, EventArgs e)
        {
            _config.HideExfilNames = chkHideExfilNames.Checked;
        }

        private void picExfilActiveColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilActiveText", picExfilActiveTextColor);
        }

        private void picExfilActiveIconColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilActiveIcon", picExfilActiveIconColor);
        }

        private void picExfilPendingColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilPendingText", picExfilPendingTextColor);
        }

        private void picExfilPendingIconColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilPendingIcon", picExfilPendingIconColor);
        }

        private void picExfilClosedColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilClosedText", picExfilClosedTextColor);
        }

        private void picExfilClosedIconColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ExfilClosedIcon", picExfilClosedIconColor);
        }

        private void chkHideTextOutline_CheckedChanged(object sender, EventArgs e)
        {
            _config.HideTextOutline = chkHideTextOutline.Checked;
        }

        private void chkMasterSwitch_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = chkMasterSwitch.Checked;
            _config.MasterSwitchEnabled = isChecked;
            grpGlobalFeatures.Enabled = isChecked;
            grpGearFeatures.Enabled = isChecked;
            grpPhysicalFeatures.Enabled = isChecked;

            if (Memory.Toolbox is not null && Memory.InGame)
            {
                if (chkMasterSwitch.Checked)
                {
                    Memory.Toolbox.StartToolbox();
                }
                else
                {
                    Memory.Toolbox.StopToolbox();
                }
            }
        }

        private void chkInfiniteStamina_CheckedChanged(object sender, EventArgs e)
        {
            _config.InfiniteStaminaEnabled = chkInfiniteStamina.Checked;
        }

        private void chkExtendedReach_CheckedChanged(object sender, EventArgs e)
        {
            _config.ExtendedReachEnabled = chkExtendedReach.Checked;
        }

        private void chkShowCorpses_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowCorpsesEnabled = chkShowCorpses.Checked;
        }

        private void chkImportantLootOnly_CheckedChanged(object sender, EventArgs e)
        {
            _config.ImportantLootOnly = chkImportantLootOnly.Checked;
        }

        private void chkAutoLootRefresh_CheckedChanged(object sender, EventArgs e)
        {
            _config.AutoLootRefreshEnabled = chkAutoLootRefresh.Checked;
            if (Memory.Loot is not null && Memory.InGame)
            {
                if (chkAutoLootRefresh.Checked)
                {
                    Memory.Loot.StartAutoRefresh();
                }
                else
                {
                    Memory.Loot.StopAutoRefresh();
                }
            }
        }

        private void picDeathMarkerColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("DeathMarker", picDeathMarkerColor);
        }

        private void trkCorpseLootValue_Scroll(object sender, EventArgs e)
        {
            int value = trkCorpseLootValue.Value * 1000;
            lblCorpseDisplay.Text = TarkovDevManager.FormatNumber(value);
            _config.MinCorpseValue = value;

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        private void trkSubItemLootValue_Scroll(object sender, EventArgs e)
        {
            int value = trkSubItemLootValue.Value * 1000;
            lblSubItemDisplay.Text = TarkovDevManager.FormatNumber(value);
            _config.MinSubItemValue = value;

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        private void chkShowSubItems_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowSubItemsEnabled = chkShowSubItems.Checked;

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        private ThermalSettings GetSelectedThermalSetting()
        {
            return cboThermalType.SelectedItem?.ToString() == "Main" ? _config.MainThermalSetting : _config.OpticThermalSetting;
        }

        private void trkThermalColorCoefficient_Scroll(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorCoefficient = (float)Math.Round(trkThermalColorCoefficient.Value / 100.0f, 4, MidpointRounding.AwayFromZero);
        }

        private void trkThermalMinTemperature_Scroll(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.MinTemperature = (float)Math.Round((0.01f - 0.001f) * (trkThermalMinTemperature.Value / 100.0f) + 0.001f, 4, MidpointRounding.AwayFromZero);
        }

        private void trkThermalShift_Scroll(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.RampShift = (float)Math.Round((trkThermalShift.Value / 100.0f) - 1.0f, 4, MidpointRounding.AwayFromZero);
        }

        private void cboThermalColorScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();
            thermalSettings.ColorScheme = cboThermalColorScheme.SelectedIndex;
        }

        private void cboThermalType_SelectedIndexChanged(object sender, EventArgs e)
        {
            var thermalSettings = this.GetSelectedThermalSetting();

            var colorCoefficient = (int)(thermalSettings.ColorCoefficient * 100);
            var minTemperature = (int)((thermalSettings.MinTemperature - 0.001f) / (0.01f - 0.001f) * 100.0f);
            var rampShift = (int)((thermalSettings.RampShift + 1.0f) * 100.0f);

            trkThermalColorCoefficient.Value = colorCoefficient;
            trkThermalMinTemperature.Value = minTemperature;
            trkThermalShift.Value = rampShift;
            cboThermalColorScheme.SelectedIndex = thermalSettings.ColorScheme;
        }

        private void btnApplyMapScale_Click(object sender, EventArgs e)
        {
            if (
                float.TryParse(txtMapSetupX.Text, out float x)
                && float.TryParse(txtMapSetupY.Text, out float y)
                && float.TryParse(txtMapSetupScale.Text, out float scale)
            )
            {
                lock (_renderLock)
                {
                    _selectedMap.ConfigFile.X = x;
                    _selectedMap.ConfigFile.Y = y;
                    _selectedMap.ConfigFile.Scale = scale;
                    _selectedMap.ConfigFile.Save(_selectedMap);
                }
            }
            else
            {
                throw new Exception("INVALID float values in Map Setup.");
            }
        }

        /// <summary>
        /// Fired when Chams checkbox has been adjusted
        /// </summary>
        private void chkChams_CheckedChanged(object sender, EventArgs e)
        {
            //_config.ChamsEnabled = chkChams.Checked;
            Memory.Chams.ChamsEnable();
        }

        /// <summary>
        /// Fired when item is removed from a filter & saves config
        /// </summary>
        private void btnLootFilterRemoveItem_Click(object sender, EventArgs e)
        {
            if (lstViewLootFilter.SelectedItems.Count > 0)
            {
                LootFilter selectedFilter = (LootFilter)cboFilters.SelectedItem;
                ListViewItem selectedItem = lstViewLootFilter.SelectedItems[0];

                if (selectedItem is not null)
                {
                    LootItem lootItem = (LootItem)selectedItem.Tag;

                    selectedItem.Remove();
                    selectedFilter.Items.Remove(lootItem.Item.id);

                    Config.SaveConfig(_config);
                    this.Loot?.ApplyFilter();
                }
            }
        }

        /// <summary>
        /// Fired when item is added to a filter & saves config
        /// </summary>
        private void btnLootFilterAddItem_Click(object sender, EventArgs e)
        {
            if (cboLootItems.SelectedIndex != -1)
            {
                LootFilter selectedFilter = (LootFilter)cboFilters.SelectedItem;
                LootItem selectedItem = (LootItem)cboLootItems.SelectedItem;

                if (selectedFilter is not null)
                {
                    if (selectedFilter.Items.Contains(selectedItem.ID))
                    {
                        return;
                    }
                    else
                    {
                        ListViewItem listItem = new ListViewItem();
                        listItem.Text = selectedItem.Item.id;
                        listItem.SubItems.Add(selectedItem.Item.name);
                        listItem.SubItems.Add(selectedItem.Item.shortName);
                        listItem.SubItems.Add(TarkovDevManager.FormatNumber(selectedItem.Value));
                        listItem.Tag = selectedItem;

                        lstViewLootFilter.Items.Add(listItem);
                        selectedFilter.Items.Add(selectedItem.Item.id);

                        Config.SaveConfig(_config);
                        this.Loot?.ApplyFilter();
                    }
                }
            }
        }

        /// <summary>
        /// Fired when an item to search for is being typed
        /// </summary>
        private void txtItemFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter)
            {
                var itemToSearch = txtItemFilter.Text;
                List<LootItem> lootList = TarkovDevManager.AllItems
                    .Select(x => x.Value)
                    .Where(x => x.Name.Contains(itemToSearch.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Name)
                    .Take(25)
                    .ToList();

                cboLootItems.DataSource = lootList;
                cboLootItems.DisplayMember = "Label";
            }
        }

        /// <summary>
        /// Fired when the current filter is changed
        /// </summary>
        private void cboFilters_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateLootFilterList();
        }

        /// <summary>
        /// Fired when UI Scale Trackbar is Adjusted
        /// </summary>
        private void trkUIScale_ValueChanged(object sender, EventArgs e)
        {
            _uiScale = (.01f * trkUIScale.Value);
            lblUIScale.Text = $"UI Scale {_uiScale.ToString("n2")}";
            #region UpdatePaints
            SKPaints.TextMouseoverGroup.TextSize = 12 * _uiScale;
            SKPaints.TextBase.TextSize = 12 * _uiScale;
            SKPaints.LootText.TextSize = 13 * _uiScale;
            SKPaints.TextBaseOutline.TextSize = 13 * _uiScale;
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
        }

        /// <summary>
        /// Event fires when the "Map Free" or "Map Follow" checkbox (button) is clicked on the Main Window.
        /// </summary>
        private void chkMapFree_CheckedChanged(object sender, EventArgs e)
        {
            if (chkMapFree.Checked)
            {
                chkMapFree.Text = "Map Follow";
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
                chkMapFree.Text = "Map Free";
        }

        /// <summary>
        /// Handles mouse movement on Map Canvas, specifically checks if mouse moves close to a 'Player' position.
        /// </summary>
        private void MapCanvas_MouseMovePlayer(object sender, MouseEventArgs e)
        {
            if (this.InGame && this.LocalPlayer is not null) // Must be in-game
            {
                var players = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.Type is not PlayerType.LocalPlayer && !x.HasExfild); // Get all players except LocalPlayer & Exfil'd Players

                var loot = this.Loot?.Filter?.Select(x => x);
                var tasksItems = this.QuestManager?.QuestItems?.Select(x => x);
                var tasksZones = this.QuestManager?.QuestZones?.Select(x => x);

                if ((players is not null && players.Any()) || (loot is not null && loot.Any()) || (tasksItems is not null && tasksItems.Any()) || (tasksZones is not null && tasksZones.Any()))
                {
                    var mouse = new Vector2(e.X, e.Y); // Get current mouse position in control

                    if (players is not null && players.Any())
                    {
                        var closestPlayer = players.Aggregate(
                            (x1, x2) =>
                                Vector2.Distance(x1.ZoomedPosition, mouse)
                                < Vector2.Distance(x2.ZoomedPosition, mouse)
                                    ? x1
                                    : x2
                        ); // Get player object 'closest' to mouse position

                        if (closestPlayer is not null)
                        {
                            var dist = Vector2.Distance(closestPlayer.ZoomedPosition, mouse);
                            if (dist < 12) // See if 'closest object' is close enough.
                            {
                                _closestPlayerToMouse = closestPlayer; // Save ref to closest player object
                                if (closestPlayer.IsHumanHostile && closestPlayer.GroupID != -1)
                                    _mouseOverGroup = closestPlayer.GroupID; // Set group ID for closest player(s)
                                else
                                    _mouseOverGroup = null; // Clear Group ID
                            }
                            else
                                ClearPlayerRefs();
                        }
                        else
                            ClearPlayerRefs();
                    }
                    else
                        ClearPlayerRefs();

                    if (_config.LootEnabled)
                    {
                        if (loot is not null && loot.Any())
                        {
                            var closestItem = loot.Aggregate(
                                (x1, x2) =>
                                    Vector2.Distance(x1.ZoomedPosition, mouse)
                                    < Vector2.Distance(x2.ZoomedPosition, mouse)
                                        ? x1
                                        : x2
                            ); // Get loot object 'closest' to mouse position

                            if (closestItem is not null)
                            {
                                var dist = Vector2.Distance(closestItem.ZoomedPosition, mouse);
                                if (dist < 12) // See if 'closest object' is close enough.
                                {
                                    _closestItemToMouse = closestItem; // Save ref to closest item object
                                }
                                else
                                    ClearItemRefs();
                            }
                            else
                                ClearItemRefs();
                        }
                        else
                            ClearItemRefs();
                    }
                    else
                    {
                        ClearItemRefs();
                    }

                    if (tasksItems is not null && tasksItems.Any())
                    {
                        var closestTaskItem = tasksItems.Aggregate(
                            (x1, x2) =>
                                Vector2.Distance(x1.ZoomedPosition, mouse)
                                < Vector2.Distance(x2.ZoomedPosition, mouse)
                                    ? x1
                                    : x2
                        ); // Get quest item object 'closest' to mouse position

                        if (closestTaskItem is not null)
                        {
                            var dist = Vector2.Distance(closestTaskItem.ZoomedPosition, mouse);
                            if (dist < 12) // See if 'closest object' is close enough.
                            {
                                _closestTaskItemToMouse = closestTaskItem; // Save ref to closest quest item object
                            }
                            else
                                ClearTaskItemRefs();
                        }
                        else
                            ClearTaskItemRefs();
                    }
                    else
                        ClearTaskItemRefs();

                    if (tasksZones is not null && tasksZones.Any())
                    {
                        var closestTaskZone = tasksZones.Aggregate(
                            (x1, x2) =>
                                Vector2.Distance(x1.ZoomedPosition, mouse)
                                < Vector2.Distance(x2.ZoomedPosition, mouse)
                                    ? x1
                                    : x2
                        ); // Get task zone 'closest' to mouse position

                        if (closestTaskZone is not null)
                        {
                            var dist = Vector2.Distance(closestTaskZone.ZoomedPosition, mouse);
                            if (dist < 12) // See if 'closest zone' is close enough.
                            {
                                _closestTaskZoneToMouse = closestTaskZone; // Save ref to closest zone object
                            }
                            else
                                ClearTaskZoneRefs();
                        }
                        else
                            ClearTaskZoneRefs();
                    }
                    else
                        ClearTaskZoneRefs();
                }
                else
                {
                    ClearPlayerRefs();
                    ClearItemRefs();
                    ClearTaskItemRefs();
                    ClearTaskZoneRefs();
                }
            }
            else if (this.InGame && Memory.LocalPlayer is null)
            {
                ClearPlayerRefs();
                ClearItemRefs();
                ClearTaskItemRefs();
                ClearTaskZoneRefs();
            }

            if (this._isDragging && chkMapFree.Checked)
            {
                if (!this._lastMousePosition.IsEmpty) // if this isn't the first MouseMove event
                {
                    lock (_renderLock)
                    {
                        // calculate the difference in position
                        int dx = e.X - this._lastMousePosition.X;
                        int dy = e.Y - this._lastMousePosition.Y;

                        this._mapPanPosition = new MapPosition() // Pan based on difference in position
                        {
                            X = this._mapPanPosition.X - dx, // Negate the difference in X
                            Y = this._mapPanPosition.Y - dy  // Negate the difference in Y
                        };
                    }
                }

                // store the current mouse position for the next MouseMove event
                this._lastMousePosition = e.Location;
            }
        }

        private void MapCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this._isDragging = true;
                this._lastMousePosition = e.Location;
            }
        }

        private void MapCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this._isDragging = false;
                this._lastMousePosition = Point.Empty;
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

        /// <summary>
        /// Event fires when Map Setup box is checked/unchecked.
        /// </summary>
        private void chkShowMapSetup_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowMapSetup.Checked)
            {
                grpMapSetup.Visible = true;
                txtMapSetupX.Text = _selectedMap.ConfigFile.X.ToString();
                txtMapSetupY.Text = _selectedMap.ConfigFile.Y.ToString();
                txtMapSetupScale.Text = _selectedMap.ConfigFile.Scale.ToString();
            }
            else
                grpMapSetup.Visible = false;
        }

        /// <summary>
        /// Event fires when Restart Game button is clicked in Settings.
        /// </summary>
        private void btnRestartRadar_Click(object sender, EventArgs e)
        {
            Memory.Restart();
        }

        /// <summary>
        /// Event fires when Apply button is clicked in the "Map Setup Groupbox".
        /// </summary>
        private void btnMapSetupApply_Click(object sender, EventArgs e)
        {
            if (float.TryParse(txtMapSetupX.Text, out float x)
                && float.TryParse(txtMapSetupY.Text, out float y)
                && float.TryParse(txtMapSetupScale.Text, out float scale))
            {
                lock (_renderLock)
                {
                    _selectedMap.ConfigFile.X = x;
                    _selectedMap.ConfigFile.Y = y;
                    _selectedMap.ConfigFile.Scale = scale;
                    _selectedMap.ConfigFile.Save(_selectedMap);
                }
            }
            else
            {
                throw new Exception("INVALID float values in Map Setup.");
            }
        }

        /// <summary>
        /// Allows panning the map when in "Free" mode.
        /// </summary>
        private void MapCanvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (chkMapFree.Checked)
            {
                var center = new SKPoint(_mapCanvas.Width / 2, _mapCanvas.Height / 2); // Get center of canvas

                lock (_renderLock)
                {
                    _mapPanPosition = new MapPosition() // Pan based on distance/direction from center
                    {
                        X = _mapPanPosition.X + (e.X - center.X),
                        Y = _mapPanPosition.Y + (e.Y - center.Y)
                    };
                }
            }
        }

        /// <summary>
        /// Executes map change after a short delay, in case switching through maps quickly to reduce UI lag.
        /// </summary>
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

        /// <summary>
        /// Event fires when the Map button is clicked in Settings.
        /// </summary>
        private void btnToggleMap_Click(object sender, EventArgs e)
        {
            ToggleMap();
        }

        /// <summary>
        /// Adjusts min regular loot based on slider value
        /// </summary>
        private void trkRegularLootValue_Scroll(object sender, EventArgs e)
        {
            int value = trkRegularLootValue.Value * 1000;
            lblRegularLootDisplay.Text = TarkovDevManager.FormatNumber(value);
            _config.MinLootValue = value;

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        /// <summary>
        /// Adjusts min important loot based on slider value
        /// </summary>
        private void trkImportantLootValue_Scroll(object sender, EventArgs e)
        {
            int value = trkImportantLootValue.Value * 1000;
            lblImportantLootDisplay.Text = TarkovDevManager.FormatNumber(value);
            _config.MinImportantLootValue = value;

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        /// <summary>
        /// Refreshes the loot in the match
        /// </summary>
        private void btnRefreshLoot_Click(object sender, EventArgs e)
        {
            Memory.Loot.RefreshLoot(true);
        }

        /// <summary>
        /// Handles color modification for loot filters
        /// </summary>
        private void picLootFilterPreview_Click(object sender, EventArgs e)
        {
            if (colDialog.ShowDialog() == DialogResult.OK)
            {
                picLootFilterEditColor.BackColor = colDialog.Color;
            }
        }

        /// <summary>
        /// Handles the edit/save button for modifying filters
        /// </summary>
        private void btnEditSaveFilter_Click(object sender, EventArgs e)
        {
            string btnText = btnEditSaveFilter.Text;
            if (btnText == "Edit")
            { /// show edit button
                txtLootFilterEditName.Enabled = true;
                picLootFilterEditColor.Enabled = true;
                chkLootFilterEditActive.Enabled = true;
                btnEditSaveFilter.Text = "Save";
                btnCancelEditFilter.Visible = true;
            }
            else if (btnText == "Save")
            { // save functionality
                if (HasUnsavedFilterEditChanges() && MessageBox.Show("Are you sure you want to save changes?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (txtLootFilterEditName.Text.Length > 0)
                    {
                        btnCancelEditFilter.Visible = false;
                        btnEditSaveFilter.Text = "Edit";

                        txtLootFilterEditName.Enabled = false;
                        picLootFilterEditColor.Enabled = false;
                        chkLootFilterEditActive.Enabled = false;

                        LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;
                        int index = _config.Filters.IndexOf(selectedFilter);

                        Color cols = picLootFilterEditColor.BackColor;
                        selectedFilter.Name = txtLootFilterEditName.Text;
                        selectedFilter.IsActive = chkLootFilterEditActive.Checked;
                        selectedFilter.Color = new Colors
                        {
                            R = cols.R,
                            G = cols.G,
                            B = cols.B,
                            A = cols.A
                        };

                        _config.Filters.RemoveAt(index);
                        _config.Filters.Insert(index, selectedFilter);

                        Config.SaveConfig(_config);
                        UpdateEditFilterListBox(lstEditLootFilters.SelectedIndex);
                    }
                    else
                    {
                        MessageBox.Show("Add some text to the textbox (minimum 1 character)", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Handles cancel button when modifying filters
        /// </summary>
        private void btnCancelEditFilter_Click(object sender, EventArgs e)
        {
            if (HasUnsavedFilterEditChanges())
            {
                if (MessageBox.Show("Are you sure you want to cancel changes?", "Are you sure?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    btnCancelEditFilter.Visible = false;
                    btnEditSaveFilter.Text = "Edit";

                    txtLootFilterEditName.Enabled = false;
                    picLootFilterEditColor.Enabled = false;
                    chkLootFilterEditActive.Enabled = false;

                    UpdateEditFilterListBox(lstEditLootFilters.SelectedIndex);
                }
            }
            else
            {
                btnCancelEditFilter.Visible = false;
                btnEditSaveFilter.Text = "Edit";

                txtLootFilterEditName.Enabled = false;
                picLootFilterEditColor.Enabled = false;
                chkLootFilterEditActive.Enabled = false;
            }
        }

        /// <summary>
        /// Updates the ListBox with filters & applys the loot filter if in-game
        /// </summary>
        /// <param name="index">Optional argument to set the selected index manually for the ListBox containing filters</param>
        private void UpdateEditFilterListBox(int index = 0)
        {
            lstEditLootFilters.Items.Clear();

            List<LootFilter> lootFilters = _config.Filters.OrderBy(lf => lf.Order).ToList();

            foreach (LootFilter filter in lootFilters)
            {
                lstEditLootFilters.Items.Add(filter);
            }

            if (lootFilters.Count > 0)
            {
                lstEditLootFilters.SetSelected(index, true);
                UpdateLootFilterComboBoxes();
            }

            if (Loot is not null)
            {
                Loot.ApplyFilter();
            }
        }

        /// <summary>
        /// Updates the information displayed about a filter
        /// </summary>
        private void lstEditLootFilters_SelectedIndexChanged(object sender, EventArgs e)
        {
            LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;

            if (selectedFilter is not null)
            {
                Colors col = selectedFilter.Color;

                txtLootFilterEditName.Text = selectedFilter.Name;
                picLootFilterEditColor.BackColor = Color.FromArgb(col.A, col.R, col.G, col.B);
                chkLootFilterEditActive.Checked = selectedFilter.IsActive;
            }
        }

        /// <summary>
        /// Shifts the order (aka priority) of the filter up
        /// </summary>
        private void btnFilterPriorityUp_Click(object sender, EventArgs e)
        {
            LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;

            if (selectedFilter is not null)
            {
                if (selectedFilter.Order != 1)
                { // make sure we dont out of bounds ourself
                    int tmpOrder = selectedFilter.Order;
                    int index = selectedFilter.Order - 1;

                    LootFilter swapFilter = _config.Filters.FirstOrDefault(f => f.Order == selectedFilter.Order - 1);

                    selectedFilter.Order = swapFilter.Order;
                    swapFilter.Order = tmpOrder;

                    Config.SaveConfig(_config);
                    UpdateEditFilterListBox(index - 1);
                }
            }
        }

        /// <summary>
        /// Shifts the order (aka priority) of the filter down
        /// </summary>
        private void btnFilterPriorityDown_Click(object sender, EventArgs e)
        {
            LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;

            if (selectedFilter is not null)
            {
                if (selectedFilter.Order != _config.Filters.Count)
                { // make sure we dont out of bounds ourself
                    int tmpOrder = selectedFilter.Order;
                    int index = selectedFilter.Order - 1;

                    LootFilter swapFilter = _config.Filters.FirstOrDefault(f => f.Order == index + 2);

                    selectedFilter.Order = swapFilter.Order;
                    swapFilter.Order = tmpOrder;

                    Config.SaveConfig(_config);
                    UpdateEditFilterListBox(index + 1);
                }
            }
        }

        /// <summary>
        /// Creates a new filter
        /// </summary>
        private void btnAddNewFilter_Click(object sender, EventArgs e)
        {
            LootFilter newFilter = new LootFilter()
            {
                Order = _config.Filters.Count + 1,
                IsActive = true,
                Name = "New Filter",
                Items = new List<String>(),
                Color = new Colors()
                {
                    R = 255,
                    G = 255,
                    B = 255,
                    A = 255
                }
            };

            _config.Filters.Add(newFilter);
            Config.SaveConfig(_config);
            UpdateEditFilterListBox();
        }

        /// <summary>
        /// Nukes a filter & removes it then updates the filter lists
        /// </summary>
        private void btnRemoveFilter_Click(object sender, EventArgs e)
        {
            LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;

            if (selectedFilter is not null)
            {
                if (_config.Filters.Count == 1)
                {
                    if (MessageBox.Show("Removing the last filter will automatically create a blank one", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
                    {
                        _config.Filters.RemoveAt(selectedFilter.Order - 1);

                        LootFilter newFilter = new LootFilter()
                        {
                            Order = 1,
                            IsActive = true,
                            Name = "New Filter",
                            Items = new List<String>(),
                            Color = new Colors()
                            {
                                R = 255,
                                G = 255,
                                B = 255,
                                A = 255
                            }
                        };

                        _config.Filters.Add(newFilter);
                        Config.SaveConfig(_config);
                        UpdateEditFilterListBox();
                    }
                }
                else
                {
                    if (MessageBox.Show("Are you sure you want to delete this filter?", "Are you sure?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        _config.Filters.Remove(selectedFilter);

                        var lootFiltersToUpdate = _config.Filters.Select(lf => lf).Where(lf => lf.Order > selectedFilter.Order);

                        foreach (LootFilter filterToUpdate in lootFiltersToUpdate)
                        {
                            filterToUpdate.Order -= 1;
                        }

                        Config.SaveConfig(_config);
                        UpdateEditFilterListBox();
                    }
                }
            }
        }

        private void picAIScavColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("AIScav", picAIScavColor);
        }

        private void picPScavColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("PScav", picPScavColor);
        }

        private void picAIRaiderColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("AIRaider", picAIRaiderColor);
        }

        private void picBossColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("Boss", picBossColor);
        }

        private void picBEARColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("BEAR", picBEARColor);
        }

        private void picUSECColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("USEC", picUSECColor);
        }

        private void picLocalPlayerColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("LocalPlayer", picLocalPlayerColor);
        }

        private void picTeammateColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("AIScav", picTeammateColor);
        }

        private void picTeamHoverColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("TeamHover", picTeamHoverColor);
        }

        private void picRegularLootColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("RegularLoot", picRegularLootColor);
        }

        private void picImportantLootColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("ImportantLoot", picImportantLootColor);
        }

        private void chkHideLootValue_CheckedChanged(object sender, EventArgs e)
        {
            _config.HideLootValue = chkHideLootValue.Checked;
        }

        private void chkShowHoverArmor_CheckedChanged(object sender, EventArgs e)
        {
            _config.ShowHoverArmor = chkShowHoverArmor.Checked;
        }

        private void chkInstantADS_CheckedChanged(object sender, EventArgs e)
        {
            _config.InstantADSEnabled = chkInstantADS.Checked;
        }

        private void picTextOutlineColor_Click(object sender, EventArgs e)
        {
            UpdatePaintColorByName("TextOutline", picTextOutlineColor);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Load previously set GUI Config values. Run at startup.
        /// </summary>
        private void LoadConfig()
        {
            chkMasterSwitch.Checked = _config.MasterSwitchEnabled;
            grpGlobalFeatures.Enabled = _config.MasterSwitchEnabled;
            grpGearFeatures.Enabled = _config.MasterSwitchEnabled;
            grpPhysicalFeatures.Enabled = _config.MasterSwitchEnabled;

            trkAimLength.Value = _config.PlayerAimLineLength;
            chkShowLoot.Checked = _config.LootEnabled;
            chkQuestHelper.Checked = _config.QuestHelperEnabled;
            chkShowAimview.Checked = _config.AimviewEnabled;
            chkHideNames.Checked = _config.HideNames;
            chkImportantLootOnly.Checked = _config.ImportantLootOnly;
            chkHideLootValue.Checked = _config.HideLootValue;
            chkShowHoverArmor.Checked = _config.ShowHoverArmor;
            trkZoom.Value = _config.DefaultZoom;
            trkUIScale.Value = _config.UIScale;
            txtTeammateID.Text = _config.PrimaryTeammateId;
            trkRegularLootValue.Value = _config.MinLootValue / 1000;
            trkImportantLootValue.Value = _config.MinImportantLootValue / 1000;
            trkCorpseLootValue.Value = _config.MinCorpseValue / 1000;
            trkSubItemLootValue.Value = _config.MinSubItemValue / 1000;
            lblRegularLootDisplay.Text = TarkovDevManager.FormatNumber(_config.MinLootValue);
            lblImportantLootDisplay.Text = TarkovDevManager.FormatNumber(_config.MinImportantLootValue);
            lblCorpseDisplay.Text = TarkovDevManager.FormatNumber(_config.MinCorpseValue);
            lblSubItemDisplay.Text = TarkovDevManager.FormatNumber(_config.MinSubItemValue);
            chkNoRecoilSway.Checked = _config.NoRecoilSwayEnabled;
            chkNightVision.Checked = _config.NightVisionEnabled;
            chkThermalVision.Checked = _config.ThermalVisionEnabled;
            chkOpticThermalVision.Checked = _config.OpticThermalVisionEnabled;
            chkNoVisor.Checked = _config.NoVisorEnabled;
            chkIncreaseMaxWeight.Checked = _config.IncreaseMaxWeightEnabled;
            chkInstantADS.Checked = _config.InstantADSEnabled;
            chkDoubleSearch.Checked = _config.DoubleSearchEnabled;
            chkJumpPower.Checked = _config.JumpPowerEnabled;
            trkJumpPower.Value = _config.JumpPowerStrength;
            chkThrowPower.Checked = _config.ThrowPowerEnabled;
            trkThrowPower.Value = _config.ThrowPowerStrength;
            chkInfiniteStamina.Checked = _config.InfiniteStaminaEnabled;
            chkMagDrills.Checked = _config.MagDrillsEnabled;
            trkMagDrills.Value = _config.MagDrillSpeed;
            chkShowCorpses.Checked = _config.ShowCorpsesEnabled;
            chkShowSubItems.Checked = _config.ShowSubItemsEnabled;
            chkAutoLootRefresh.Checked = _config.AutoLootRefreshEnabled;
            btnLockTimeOfDay.Checked = _config.LockTimeOfDay;
            numTimeOfDay.Value = (decimal)_config.TimeOfDay;
            chkExtendedReach.Checked = _config.ExtendedReachEnabled;
            chkChams.Checked = _config.ChamsEnabled;

            grpThermalSettings.Enabled = _config.ThermalVisionEnabled || _config.OpticThermalVisionEnabled;

            cboThermalType.SelectedIndex = 0;

            if (_config.Filters.Count == 0)
            {
                LootFilter newFilter = new LootFilter()
                {
                    Order = 1,
                    IsActive = true,
                    Name = "Default",
                    Items = new List<String>(),
                    Color = new Colors()
                    {
                        R = 255,
                        G = 255,
                        B = 255,
                        A = 255
                    }
                };

                _config.Filters.Add(newFilter);
                Config.SaveConfig(_config);
            }

            if (cboLootItems.Items.Count == 0)
            {
                List<LootItem> lootList = TarkovDevManager.AllItems.Select(x => x.Value).OrderBy(x => x.Name).Take(25).ToList();

                cboLootItems.DataSource = lootList;
                cboLootItems.DisplayMember = "Name";
            }

            if (cboRefreshMap.Items.Count == 0)
            {
                foreach (var key in _config.AutoRefreshSettings)
                {
                    cboRefreshMap.Items.Add(key.Key);
                }

                int selectedIndex = 0;

                if (_selectedMap is not null)
                {
                    selectedIndex = cboRefreshMap.FindString(_selectedMap.Name);
                }

                cboRefreshMap.SelectedIndex = selectedIndex;
            }

            UpdateLootFilterComboBoxes();
            UpdateLootFilterList();
            UpdateEditFilterListBox();
            UpdatePaintColorControls();
        }

        /// <summary>
        /// Load map files (.PNG) and Configs (.JSON) from \\Maps folder. Run at startup.
        /// </summary>
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

        /// <summary>
        /// Zooms the bitmap 'in'.
        /// </summary>
        private void ZoomIn(int amt)
        {
            trkZoom.Value = (trkZoom.Value - amt >= 1) ? trkZoom.Value -= amt : 1;
        }

        /// <summary>
        /// Zooms the bitmap 'out'.
        /// </summary>
        private void ZoomOut(int amt)
        {
            trkZoom.Value = (trkZoom.Value + amt <= 200) ? trkZoom.Value += amt : 200;
        }

        /// <summary>
        /// Provides miscellaneous map parameters used throughout the entire render.
        /// </summary>
        private MapParameters GetMapParameters(MapPosition localPlayerPos)
        {
            int mapLayerIndex = GetMapLayerIndex(localPlayerPos.Height);

            var bitmap = _loadedBitmaps[mapLayerIndex];
            float zoomFactor = 0.01f * trkZoom.Value;
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

        /// <summary>
        /// Determines if an aggressor player is facing a friendly player.
        /// </summary>
        private static bool IsAggressorFacingTarget(
            SKPoint aggressor,
            float aggressorDegrees,
            SKPoint target,
            float distance)
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

        /// <summary>
        /// Toggles currently selected map.
        /// </summary>
        private void ToggleMap()
        {
            if (!btnToggleMap.Enabled)
                return;
            if (_mapSelectionIndex == _maps.Count - 1)
                _mapSelectionIndex = 0; // Start over when end of maps reached
            else
                _mapSelectionIndex++; // Move onto next map
            tabRadar.Text = $"Radar ({_maps[_mapSelectionIndex].Name})";
            _mapChangeTimer.Restart(); // Start delay
        }

        /// <summary>
        /// Adds filters into the combo box for selection
        /// </summary>
        private void UpdateLootFilterComboBoxes()
        {
            cboFilters.DataSource = null;
            cboFilters.Items.Clear();

            List<LootFilter> lootFilters = _config.Filters.OrderBy(lf => lf.Order).ToList();

            foreach (LootFilter filter in lootFilters)
            {
                cboFilters.Items.Add(filter);
            }

            cboFilters.DisplayMember = "Name";

            if (lootFilters.Count > 0)
            {
                cboFilters.SelectedItem = lootFilters[0];
            }
        }

        /// <summary>
        /// Adds items from the loot filter into the list view
        /// </summary>
        private void UpdateLootFilterList()
        {
            lstViewLootFilter.Items.Clear();

            LootFilter selectedFilter = (LootFilter)cboFilters.SelectedItem;
            List<LootItem> lootList = TarkovDevManager.AllItems.Select(x => x.Value).ToList();
            if (selectedFilter is not null)
            {
                List<LootItem> matchingLoot = lootList.Where(loot => selectedFilter.Items.Any(id => id == loot.Item.id)).OrderBy(l => l.Item.name).ToList();

                foreach (LootItem item in matchingLoot)
                {
                    ListViewItem listItem = new ListViewItem()
                    {
                        Text = item.Item.id,
                        Tag = item
                    };

                    listItem.SubItems.Add(item.Item.name);
                    listItem.SubItems.Add(item.Item.shortName);
                    listItem.SubItems.Add(TarkovDevManager.FormatNumber(TarkovDevManager.GetItemValue(item.Item)));

                    lstViewLootFilter.Items.Add(listItem);
                }

                chkLootFilterActive.Checked = selectedFilter.IsActive;
            }
        }

        /// <summary>
        /// Checks if the text, IsActive or color is different
        /// </summary>
        /// <returns>A bool</returns>
        private bool HasUnsavedFilterEditChanges()
        {
            LootFilter selectedFilter = (LootFilter)lstEditLootFilters.SelectedItem;

            if (selectedFilter is not null)
            {
                Colors col = selectedFilter.Color;

                return txtLootFilterEditName.Text != selectedFilter.Name ||
                        chkLootFilterEditActive.Checked != selectedFilter.IsActive ||
                        picLootFilterEditColor.BackColor.ToArgb() != Color.FromArgb(col.A, col.R, col.G, col.B).ToArgb();
            }
            else
            {
                return false;
            }
        }

        private void UpdatePaintColorControls()
        {
            var colors = _config.PaintColors;

            Action<PictureBox, string> setColor = (pictureBox, name) =>
            {
                if (colors.ContainsKey(name))
                {
                    PaintColor.Colors color = colors[name];
                    pictureBox.BackColor = Color.FromArgb(color.A, color.R, color.G, color.B);
                }
                else
                {
                    pictureBox.BackColor = Color.FromArgb(255, 255, 255, 255);
                }
            };

            setColor(picAIScavColor, "AIScav");
            setColor(picPScavColor, "PScav");
            setColor(picAIRaiderColor, "AIRaider");
            setColor(picBossColor, "Boss");
            setColor(picUSECColor, "USEC");
            setColor(picBEARColor, "BEAR");
            setColor(picLocalPlayerColor, "LocalPlayer");
            setColor(picTeammateColor, "Teammate");
            setColor(picTeamHoverColor, "TeamHover");
            setColor(picRegularLootColor, "RegularLoot");
            setColor(picImportantLootColor, "ImportantLoot");
            setColor(picQuestItemsColor, "QuestItem");
            setColor(picQuestZonesColor, "QuestZone");
            setColor(picExfilActiveTextColor, "ExfilActiveText");
            setColor(picExfilActiveIconColor, "ExfilActiveIcon");
            setColor(picExfilPendingTextColor, "ExfilPendingText");
            setColor(picExfilPendingIconColor, "ExfilPendingIcon");
            setColor(picExfilClosedTextColor, "ExfilClosedText");
            setColor(picExfilClosedIconColor, "ExfilClosedIcon");
            setColor(picTextOutlineColor, "TextOutline");
            setColor(picDeathMarkerColor, "DeathMarker");
        }

        /// <summary>
        /// Updates the Color of a PaintColor object in the PaintColors dictionary by name
        /// </summary>
        private void UpdatePaintColorByName(string name, PictureBox pictureBox)
        {
            if (colDialog.ShowDialog() == DialogResult.OK)
            {
                Color col = colDialog.Color;
                pictureBox.BackColor = col;

                _config.PaintColors[name] = new PaintColor.Colors
                {
                    A = col.A,
                    R = col.R,
                    G = col.G,
                    B = col.B
                };
            }
        }
        #endregion

        #region Render
        private void MapCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear();

            UpdateWindowTitle();

            try
            {
                if (IsReadyToRender())
                {
                    lock (_renderLock)
                    {
                        DrawMap(canvas);
                        DrawPlayers(canvas);
                        DrawLoot(canvas);
                        DrawQuestItems(canvas);
                        DrawGrenades(canvas);
                        DrawExfils(canvas);
                        DrawAimview(canvas);
                        DrawToolTips(canvas);
                    }
                }
                else
                {
                    DrawStatusText(canvas);
                }
            }
            catch (Exception ex) { }

            canvas.Flush();
        }

        private void UpdateWindowTitle()
        {
            bool inGame = this.InGame;
            var localPlayer = this.LocalPlayer;

            if (inGame && localPlayer is not null)
            {
                // Check if map changed
                UpdateSelectedMap();

                if (_fpsWatch.ElapsedMilliseconds >= 1000)
                {
                    // RE-ENABLE & EXPLORE WHAT THIS DOES
                    //_mapCanvas.GRContext.PurgeResources(); // Seems to fix mem leak issue on increasing resource cache
                    string title = "EFT Radar";

                    title += $" ({_fps} fps) ({Memory.Ticks} mem/s)";

                    if (this.LoadingLoot)
                        title += " - LOADING LOOT";

                    this.Text = title; // Set window title
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
            string currentMap = this.CurrentMapName;
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
                    tabRadar.Text = $"Radar ({_selectedMap.Name})";

                    int selectedIndex = cboRefreshMap.FindString(_selectedMap.Name);
                    cboRefreshMap.SelectedIndex = selectedIndex != 0 ? selectedIndex : 0;
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
            {
                return false; // Map not loaded
            }

            return true; // Ready to render
        }

        private MapParameters GetMapLocation()
        {
            var localPlayer = this.LocalPlayer;
            if (localPlayer is not null)
            {
                var localPlayerPos = localPlayer.Position;
                var localPlayerMapPos = localPlayerPos.ToMapPos(_selectedMap);

                if (chkMapFree.Checked)
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

        private void DrawMap(SKCanvas canvas)
        {
            var localPlayer = this.LocalPlayer;
            var localPlayerPos = localPlayer.Position;

            if (grpMapSetup.Visible) // Print coordinates (to make it easy to setup JSON configs)
            {
                lblMapCoords.Text = $"Unity X,Y,Z: {localPlayerPos.X},{localPlayerPos.Y},{localPlayerPos.Z}";
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
                    {
                        var localPlayerZoomedPos = localPlayerMapPos.ToZoomedPos(mapParams);
                        localPlayerZoomedPos.DrawPlayerMarker(
                            canvas,
                            localPlayer,
                            trkAimLength.Value,
                            null
                        );
                    }

                    foreach (var player in allPlayers) // Draw PMCs
                    {
                        if (player.Type == PlayerType.LocalPlayer)
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

                        if (!player.IsAlive)
                        {
                            // Draw 'X' death marker
                            //playerZoomedPos.DrawDeathMarker(canvas);
                            continue;
                        }
                        else if (player.Type is not PlayerType.Teammate)
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
                            aimlineLength = trkAimLength.Value; // Allies use player's aim length
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

                if (!chkHideNames.Checked) // show full names & info
                {
                    lines = new string[2]
                    {
                        string.Empty,
                        $"{(int)Math.Round(height)}, {(int)Math.Round(dist)}"
                    };

                    string name = player.Name;

                    if (player.ErrorCount > 10)
                        name = "ERROR"; // In case POS stops updating, let us know!

                    if (player.IsHuman || player.IsBossRaider)
                        lines[0] += $"{name} ({player.Health})";
                    else
                        lines[0] += $"{name}";
                }
                else // just height & hp (for humans)
                {
                    lines = new string[1] { $"{(int)Math.Round(height)}, {(int)Math.Round(dist)}" };

                    if (player.IsHuman || player.IsBossRaider)
                        lines[0] += $" ({player.Health})";
                    if (player.ErrorCount > 10)
                        lines[0] = "ERROR"; // In case POS stops updating, let us know!
                }

                if (!string.IsNullOrEmpty(player.Category))
                    lines[0] += $" [{player.Category}]";

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
                if (chkShowLoot.Checked) // Draw loot (if enabled)
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
                                if (item is null || (this._config.ImportantLootOnly && !item.Important && !item.AlwaysShow) || (item is LootCorpse && !this._config.ShowCorpsesEnabled))
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
                if (chkQuestHelper.Checked && !Memory.IsScav) // Draw quest items (if enabled)
                {
                    if (this.QuestManager is not null)
                    {
                        var localPlayerMapPos = localPlayer.Position.ToMapPos(_selectedMap);
                        var mapParams = GetMapLocation();

                        var questItems = this.QuestManager.QuestItems;
                        if (questItems is not null)
                        {
                            foreach (var item in questItems.Where(x => x.Position.X != 0))
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
                    var localPlayerMapPos = this.LocalPlayer.Position.ToMapPos(_selectedMap);
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
            if (chkShowAimview.Checked)
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
                    this.tabRadar.Text = "Radar";
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

        #endregion

        #region Overrides
        /// <summary>
        /// Form closing event.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true; // Cancel shutdown
            this.Enabled = false; // Lock window
            _config.PlayerAimLineLength = trkAimLength.Value;
            _config.LootEnabled = chkShowLoot.Checked;
            _config.QuestHelperEnabled = chkQuestHelper.Checked;
            _config.AimviewEnabled = chkShowAimview.Checked;
            _config.HideNames = chkHideNames.Checked;
            _config.ImportantLootOnly = chkImportantLootOnly.Checked;
            _config.HideLootValue = chkHideLootValue.Checked;
            _config.DefaultZoom = trkZoom.Value;
            _config.UIScale = trkUIScale.Value;
            _config.PrimaryTeammateId = txtTeammateID.Text;
            _config.NoRecoilSwayEnabled = chkNoRecoilSway.Checked;
            _config.ThermalVisionEnabled = chkThermalVision.Checked;
            _config.NightVisionEnabled = chkNightVision.Checked;
            _config.NoVisorEnabled = chkNoVisor.Checked;
            _config.OpticThermalVisionEnabled = chkOpticThermalVision.Checked;
            _config.QuestHelperEnabled = chkQuestHelper.Checked;
            _config.DoubleSearchEnabled = chkDoubleSearch.Checked;
            _config.MagDrillsEnabled = chkMagDrills.Checked;
            _config.MagDrillSpeed = trkMagDrills.Value;
            _config.InstantADSEnabled = chkInstantADS.Checked;
            _config.IncreaseMaxWeightEnabled = chkIncreaseMaxWeight.Checked;
            _config.JumpPowerEnabled = chkJumpPower.Checked;
            _config.JumpPowerStrength = trkJumpPower.Value;
            _config.ThrowPowerEnabled = chkThrowPower.Checked;
            _config.ThrowPowerStrength = trkThrowPower.Value;
            _config.HideExfilNames = chkHideExfilNames.Checked;

            Config.SaveConfig(_config); // Save Config to Config.json
            Memory.Shutdown(); // Wait for Memory Thread to gracefully exit
            e.Cancel = false; // Ready to close
            base.OnFormClosing(e); // Proceed with closing
        }

        /// <summary>
        /// Process hotkey presses.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1:
                    ZoomIn(5);
                    return true;
                case Keys.F2:
                    ZoomOut(5);
                    return true;
                case Keys.F3:
                    this.chkShowLoot.Checked = !this.chkShowLoot.Checked; // Toggle loot
                    return true;
                case Keys.F4:
                    this.chkShowAimview.Checked = !this.chkShowAimview.Checked; // Toggle aimview
                    return true;
                case Keys.F5:
                    ToggleMap(); // Toggle to next map
                    return true;
                case Keys.F6:
                    chkHideNames.Checked = !chkHideNames.Checked; // Toggle Hide Names
                    return true;
                // Night Vision Ctrl + N
                case Keys.Control | Keys.N:
                    chkNightVision.Checked = !chkNightVision.Checked;
                    return true;
                // Thermal Vision Ctrl + T
                case Keys.Control | Keys.T:
                    chkThermalVision.Checked = !chkThermalVision.Checked;
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        /// <summary>
        /// Process mousewheel events.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (tabControl.SelectedIndex == 0) // Main Radar Tab should be open
            {
                bool increment = e.Delta > 0;
                int amt = (e.Delta / (increment ? SystemInformation.MouseWheelScrollDelta : -SystemInformation.MouseWheelScrollDelta)) * 5; // Calculate zoom amount based on number of deltas

                if (increment)
                {
                    ZoomIn(amt);
                }
                else
                {
                    ZoomOut(amt);
                }

                return;
            }

            base.OnMouseWheel(e);
        }

        private void numRefreshDelay_ValueChanged(object sender, EventArgs e)
        {
            var mapName = cboRefreshMap.SelectedItem.ToString();
            var value = (int)numRefreshDelay.Value;

            if (value != _config.AutoRefreshSettings[mapName])
            {
                _config.AutoRefreshSettings[mapName] = value;
            }
        }

        private void cboRefreshMap_SelectedIndexChanged(object sender, EventArgs e)
        {
            var mapName = cboRefreshMap.SelectedItem.ToString();
            numRefreshDelay.Value = _config.AutoRefreshSettings[mapName];
        }

        private void numTimeOfDay_ValueChanged(object sender, EventArgs e)
        {
            _config.TimeOfDay = (float)numTimeOfDay.Value;
        }

        private void btnFreezeTime_CheckedChanged(object sender, EventArgs e)
        {
            _config.LockTimeOfDay = btnLockTimeOfDay.Checked;
        }

        private void chkSearchSpeed_CheckedChanged(object sender, EventArgs e)
        {
            _config.SearchSpeedEnabled = chkSearchSpeed.Checked;
        }

        private void numThreadSpinDelay_ValueChanged(object sender, EventArgs e)
        {
            _config.ThreadSpinDelay = (int)numThreadSpinDelay.Value;
        }
    }
    #endregion
}