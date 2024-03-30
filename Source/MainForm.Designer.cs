using SkiaSharp.Views.Desktop;

namespace eft_dma_radar
{
    partial class frmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            tabLootFilter = new TabPage();
            chkLootFilterActive = new CheckBox();
            lblActiveFilter = new Label();
            cboFilters = new ComboBox();
            btnLootFilterRemoveItem = new Button();
            btnLootFilterAddItem = new Button();
            txtItemFilter = new TextBox();
            cboLootItems = new ComboBox();
            lstViewLootFilter = new ListView();
            colHeadBSGId = new ColumnHeader();
            colHeadFullName = new ColumnHeader();
            colHeadShortName = new ColumnHeader();
            colHeadValue = new ColumnHeader();
            tabPlayerHistory = new TabPage();
            lstViewPMCHistory = new ListView();
            columnHeader_Entry = new ColumnHeader();
            columnHeader_ID = new ColumnHeader();
            tabPlayerLoadouts = new TabPage();
            rchTxtPlayerInfo = new RichTextBox();
            tabSettings = new TabPage();
            grpConfig = new GroupBox();
            grpColors = new GroupBox();
            picDeathMarkerColor = new PictureBox();
            lblDeathMarkerColor = new Label();
            picTextOutlineColor = new PictureBox();
            lblTextOutlineColor = new Label();
            picExfilClosedIconColor = new PictureBox();
            lblExfilClosedIconColor = new Label();
            picExfilPendingIconColor = new PictureBox();
            lblExfilPendingIconColor = new Label();
            picExfilActiveIconColor = new PictureBox();
            lblExfilActiveIconColor = new Label();
            picExfilClosedTextColor = new PictureBox();
            lblExfilClosedTextColor = new Label();
            picExfilPendingTextColor = new PictureBox();
            lblExfilPendingTextColor = new Label();
            picExfilActiveTextColor = new PictureBox();
            lblExfilOpenTextColor = new Label();
            picQuestZonesColor = new PictureBox();
            lblQuestZonesColor = new Label();
            picQuestItemsColor = new PictureBox();
            lblQuestItemsColor = new Label();
            picImportantLootColor = new PictureBox();
            lblImportantLootColor = new Label();
            picUSECColor = new PictureBox();
            picRegularLootColor = new PictureBox();
            lblRegularLootColor = new Label();
            picTeamHoverColor = new PictureBox();
            lblTeamHoverColor = new Label();
            picTeammateColor = new PictureBox();
            lblTeammateColor = new Label();
            picLocalPlayerColor = new PictureBox();
            lblLocalPlayerColor = new Label();
            picBEARColor = new PictureBox();
            picBossColor = new PictureBox();
            picAIRaiderColor = new PictureBox();
            picPScavColor = new PictureBox();
            lblBEARColor = new Label();
            lblUSECColor = new Label();
            lblBossColor = new Label();
            lblAIRaiderColor = new Label();
            lblPScavColor = new Label();
            picAIScavColor = new PictureBox();
            lblAIScavColor = new Label();
            grpLootFilters = new GroupBox();
            lstEditLootFilters = new ListBox();
            btnCancelEditFilter = new Button();
            btnEditSaveFilter = new Button();
            chkLootFilterEditActive = new CheckBox();
            btnAddNewFilter = new Button();
            btnRemoveFilter = new Button();
            lblLootFilterColor = new Label();
            btnFilterPriorityDown = new Button();
            btnFilterPriorityUp = new Button();
            lblFilterEditName = new Label();
            txtLootFilterEditName = new TextBox();
            picLootFilterEditColor = new PictureBox();
            grpLoot = new GroupBox();
            chkShowSubItems = new CheckBox();
            chkShowCorpses = new CheckBox();
            grpLootValues = new GroupBox();
            lblSubItemDisplay = new Label();
            lblSubItemValueSlider = new Label();
            trkSubItemLootValue = new TrackBar();
            lblCorpseDisplay = new Label();
            lblCorpsValueSlider = new Label();
            trkCorpseLootValue = new TrackBar();
            lblImportantLootDisplay = new Label();
            lblRegularLootDisplay = new Label();
            lblImportantLootSlider = new Label();
            trkImportantLootValue = new TrackBar();
            lblRegularLoot = new Label();
            trkRegularLootValue = new TrackBar();
            btnRefreshLoot = new Button();
            chkHideLootValue = new CheckBox();
            chkImportantLootOnly = new CheckBox();
            grpUserInterface = new GroupBox();
            chkHideTextOutline = new CheckBox();
            chkHideExfilNames = new CheckBox();
            chkQuestHelper = new CheckBox();
            chkShowHoverArmor = new CheckBox();
            chkShowLoot = new CheckBox();
            trkAimLength = new TrackBar();
            lblAimline = new Label();
            txtTeammateID = new TextBox();
            lblPrimaryTeammate = new Label();
            trkZoom = new TrackBar();
            lblUIScale = new Label();
            lblZoom = new Label();
            trkUIScale = new TrackBar();
            chkShowAimview = new CheckBox();
            chkHideNames = new CheckBox();
            grpMemoryWriting = new GroupBox();
            chkMasterSwitch = new CheckBox();
            grpGlobalFeatures = new GroupBox();
            chkExtendedReach = new CheckBox();
            chkChams = new CheckBox();
            chkDoubleSearch = new CheckBox();
            grpGearFeatures = new GroupBox();
            chkNoSway = new CheckBox();
            chkInstantADS = new CheckBox();
            trkMagDrills = new TrackBar();
            chkNoRecoil = new CheckBox();
            chkMagDrills = new CheckBox();
            chkNoVisor = new CheckBox();
            chkNightVision = new CheckBox();
            chkOpticThermalVision = new CheckBox();
            chkThermalVision = new CheckBox();
            grpPhysicalFeatures = new GroupBox();
            chkInfiniteStamina = new CheckBox();
            chkIncreaseMaxWeight = new CheckBox();
            trkThrowPower = new TrackBar();
            trkJumpPower = new TrackBar();
            chkThrowPower = new CheckBox();
            chkJumpPower = new CheckBox();
            grpRadar = new GroupBox();
            btnRestartRadar = new Button();
            chkShowMapSetup = new CheckBox();
            btnToggleMap = new Button();
            tabRadar = new TabPage();
            grpMapSetup = new GroupBox();
            chkMapFree = new CheckBox();
            txtMapSetupScale = new TextBox();
            lblMapScale = new Label();
            txtMapSetupY = new TextBox();
            lblMapXY = new Label();
            txtMapSetupX = new TextBox();
            lblMapCoords = new Label();
            tabControl = new TabControl();
            colDialog = new ColorDialog();
            toolTip = new ToolTip(components);
            chkAutoLootRefresh = new CheckBox();
            tabLootFilter.SuspendLayout();
            tabPlayerHistory.SuspendLayout();
            tabPlayerLoadouts.SuspendLayout();
            tabSettings.SuspendLayout();
            grpConfig.SuspendLayout();
            grpColors.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picDeathMarkerColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picTextOutlineColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilClosedIconColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilPendingIconColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilActiveIconColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilClosedTextColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilPendingTextColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picExfilActiveTextColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picQuestZonesColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picQuestItemsColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picImportantLootColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picUSECColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picRegularLootColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picTeamHoverColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picTeammateColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picLocalPlayerColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picBEARColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picBossColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picAIRaiderColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picPScavColor).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picAIScavColor).BeginInit();
            grpLootFilters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picLootFilterEditColor).BeginInit();
            grpLoot.SuspendLayout();
            grpLootValues.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trkSubItemLootValue).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkCorpseLootValue).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkImportantLootValue).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkRegularLootValue).BeginInit();
            grpUserInterface.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trkAimLength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkZoom).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkUIScale).BeginInit();
            grpMemoryWriting.SuspendLayout();
            grpGlobalFeatures.SuspendLayout();
            grpGearFeatures.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trkMagDrills).BeginInit();
            grpPhysicalFeatures.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)trkThrowPower).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trkJumpPower).BeginInit();
            grpRadar.SuspendLayout();
            tabRadar.SuspendLayout();
            grpMapSetup.SuspendLayout();
            tabControl.SuspendLayout();
            SuspendLayout();
            // 
            // tabLootFilter
            // 
            tabLootFilter.Controls.Add(chkLootFilterActive);
            tabLootFilter.Controls.Add(lblActiveFilter);
            tabLootFilter.Controls.Add(cboFilters);
            tabLootFilter.Controls.Add(btnLootFilterRemoveItem);
            tabLootFilter.Controls.Add(btnLootFilterAddItem);
            tabLootFilter.Controls.Add(txtItemFilter);
            tabLootFilter.Controls.Add(cboLootItems);
            tabLootFilter.Controls.Add(lstViewLootFilter);
            tabLootFilter.Location = new Point(4, 24);
            tabLootFilter.Name = "tabLootFilter";
            tabLootFilter.Padding = new Padding(3);
            tabLootFilter.Size = new Size(1168, 638);
            tabLootFilter.TabIndex = 4;
            tabLootFilter.Text = "Loot Filter";
            tabLootFilter.UseVisualStyleBackColor = true;
            // 
            // chkLootFilterActive
            // 
            chkLootFilterActive.AutoSize = true;
            chkLootFilterActive.Location = new Point(1011, 8);
            chkLootFilterActive.Name = "chkLootFilterActive";
            chkLootFilterActive.Size = new Size(59, 19);
            chkLootFilterActive.TabIndex = 7;
            chkLootFilterActive.Text = "Active";
            toolTip.SetToolTip(chkLootFilterActive, "Enables/disables the loot filter");
            chkLootFilterActive.UseVisualStyleBackColor = true;
            chkLootFilterActive.CheckedChanged += chkLootFilterActive_CheckedChanged;
            // 
            // lblActiveFilter
            // 
            lblActiveFilter.AutoSize = true;
            lblActiveFilter.Location = new Point(780, 10);
            lblActiveFilter.Name = "lblActiveFilter";
            lblActiveFilter.Size = new Size(44, 15);
            lblActiveFilter.TabIndex = 6;
            lblActiveFilter.Text = "Profile:";
            // 
            // cboFilters
            // 
            cboFilters.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilters.FormattingEnabled = true;
            cboFilters.Location = new Point(830, 6);
            cboFilters.Name = "cboFilters";
            cboFilters.Size = new Size(175, 23);
            cboFilters.TabIndex = 5;
            toolTip.SetToolTip(cboFilters, "The loot filter to view/modify");
            cboFilters.SelectedValueChanged += cboFilters_SelectedValueChanged;
            // 
            // btnLootFilterRemoveItem
            // 
            btnLootFilterRemoveItem.Location = new Point(689, 6);
            btnLootFilterRemoveItem.Name = "btnLootFilterRemoveItem";
            btnLootFilterRemoveItem.Size = new Size(75, 23);
            btnLootFilterRemoveItem.TabIndex = 4;
            btnLootFilterRemoveItem.Text = "Remove";
            toolTip.SetToolTip(btnLootFilterRemoveItem, "Removes the selected item from the active filter");
            btnLootFilterRemoveItem.UseVisualStyleBackColor = true;
            btnLootFilterRemoveItem.Click += btnLootFilterRemoveItem_Click;
            // 
            // btnLootFilterAddItem
            // 
            btnLootFilterAddItem.Location = new Point(608, 6);
            btnLootFilterAddItem.Name = "btnLootFilterAddItem";
            btnLootFilterAddItem.Size = new Size(75, 23);
            btnLootFilterAddItem.TabIndex = 3;
            btnLootFilterAddItem.Text = "Add";
            toolTip.SetToolTip(btnLootFilterAddItem, "Adds the item from the drop down list into the filter");
            btnLootFilterAddItem.UseVisualStyleBackColor = true;
            btnLootFilterAddItem.Click += btnLootFilterAddItem_Click;
            // 
            // txtItemFilter
            // 
            txtItemFilter.Location = new Point(374, 6);
            txtItemFilter.Name = "txtItemFilter";
            txtItemFilter.Size = new Size(228, 23);
            txtItemFilter.TabIndex = 2;
            toolTip.SetToolTip(txtItemFilter, "The item to search for");
            txtItemFilter.KeyDown += txtItemFilter_KeyDown;
            // 
            // cboLootItems
            // 
            cboLootItems.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLootItems.FormattingEnabled = true;
            cboLootItems.Location = new Point(8, 6);
            cboLootItems.Name = "cboLootItems";
            cboLootItems.Size = new Size(360, 23);
            cboLootItems.TabIndex = 1;
            // 
            // lstViewLootFilter
            // 
            lstViewLootFilter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstViewLootFilter.AutoArrange = false;
            lstViewLootFilter.Columns.AddRange(new ColumnHeader[] { colHeadBSGId, colHeadFullName, colHeadShortName, colHeadValue });
            lstViewLootFilter.FullRowSelect = true;
            lstViewLootFilter.GridLines = true;
            lstViewLootFilter.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstViewLootFilter.Location = new Point(3, 35);
            lstViewLootFilter.MultiSelect = false;
            lstViewLootFilter.Name = "lstViewLootFilter";
            lstViewLootFilter.Size = new Size(1162, 600);
            lstViewLootFilter.TabIndex = 0;
            lstViewLootFilter.UseCompatibleStateImageBehavior = false;
            lstViewLootFilter.View = View.Details;
            // 
            // colHeadBSGId
            // 
            colHeadBSGId.Text = "Item ID";
            colHeadBSGId.Width = 200;
            // 
            // colHeadFullName
            // 
            colHeadFullName.Text = "Name";
            colHeadFullName.Width = 400;
            // 
            // colHeadShortName
            // 
            colHeadShortName.Text = "Short Name";
            colHeadShortName.Width = 200;
            // 
            // colHeadValue
            // 
            colHeadValue.Text = "Value";
            colHeadValue.Width = 90;
            // 
            // tabPlayerHistory
            // 
            tabPlayerHistory.Controls.Add(lstViewPMCHistory);
            tabPlayerHistory.Location = new Point(4, 24);
            tabPlayerHistory.Name = "tabPlayerHistory";
            tabPlayerHistory.Size = new Size(1168, 638);
            tabPlayerHistory.TabIndex = 3;
            tabPlayerHistory.Text = "Player History";
            tabPlayerHistory.UseVisualStyleBackColor = true;
            // 
            // lstViewPMCHistory
            // 
            lstViewPMCHistory.AutoArrange = false;
            lstViewPMCHistory.Columns.AddRange(new ColumnHeader[] { columnHeader_Entry, columnHeader_ID });
            lstViewPMCHistory.Dock = DockStyle.Fill;
            lstViewPMCHistory.Font = new Font("Arial", 9F, FontStyle.Regular, GraphicsUnit.Point);
            lstViewPMCHistory.FullRowSelect = true;
            lstViewPMCHistory.GridLines = true;
            lstViewPMCHistory.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lstViewPMCHistory.Location = new Point(0, 0);
            lstViewPMCHistory.MultiSelect = false;
            lstViewPMCHistory.Name = "lstViewPMCHistory";
            lstViewPMCHistory.Size = new Size(1168, 638);
            lstViewPMCHistory.TabIndex = 0;
            lstViewPMCHistory.UseCompatibleStateImageBehavior = false;
            lstViewPMCHistory.View = View.Details;
            // 
            // columnHeader_Entry
            // 
            columnHeader_Entry.Text = "Entry";
            columnHeader_Entry.Width = 200;
            // 
            // columnHeader_ID
            // 
            columnHeader_ID.Text = "ID";
            columnHeader_ID.Width = 50;
            // 
            // tabPlayerLoadouts
            // 
            tabPlayerLoadouts.Controls.Add(rchTxtPlayerInfo);
            tabPlayerLoadouts.Location = new Point(4, 24);
            tabPlayerLoadouts.Name = "tabPlayerLoadouts";
            tabPlayerLoadouts.Size = new Size(1168, 638);
            tabPlayerLoadouts.TabIndex = 2;
            tabPlayerLoadouts.Text = "Player Loadouts";
            tabPlayerLoadouts.UseVisualStyleBackColor = true;
            // 
            // rchTxtPlayerInfo
            // 
            rchTxtPlayerInfo.Dock = DockStyle.Fill;
            rchTxtPlayerInfo.Font = new Font("Arial", 9F, FontStyle.Regular, GraphicsUnit.Point);
            rchTxtPlayerInfo.Location = new Point(0, 0);
            rchTxtPlayerInfo.Name = "rchTxtPlayerInfo";
            rchTxtPlayerInfo.ReadOnly = true;
            rchTxtPlayerInfo.Size = new Size(1168, 638);
            rchTxtPlayerInfo.TabIndex = 0;
            rchTxtPlayerInfo.Text = "";
            // 
            // tabSettings
            // 
            tabSettings.Controls.Add(grpConfig);
            tabSettings.Location = new Point(4, 24);
            tabSettings.Name = "tabSettings";
            tabSettings.Padding = new Padding(3);
            tabSettings.Size = new Size(1168, 638);
            tabSettings.TabIndex = 1;
            tabSettings.Text = "Settings";
            tabSettings.UseVisualStyleBackColor = true;
            // 
            // grpConfig
            // 
            grpConfig.Controls.Add(grpColors);
            grpConfig.Controls.Add(grpLootFilters);
            grpConfig.Controls.Add(grpLoot);
            grpConfig.Controls.Add(grpUserInterface);
            grpConfig.Controls.Add(grpMemoryWriting);
            grpConfig.Controls.Add(grpRadar);
            grpConfig.Dock = DockStyle.Fill;
            grpConfig.Location = new Point(3, 3);
            grpConfig.Margin = new Padding(4, 3, 4, 3);
            grpConfig.Name = "grpConfig";
            grpConfig.Padding = new Padding(4, 3, 4, 3);
            grpConfig.Size = new Size(1162, 632);
            grpConfig.TabIndex = 8;
            grpConfig.TabStop = false;
            grpConfig.Text = "Radar Config";
            // 
            // grpColors
            // 
            grpColors.Controls.Add(picDeathMarkerColor);
            grpColors.Controls.Add(lblDeathMarkerColor);
            grpColors.Controls.Add(picTextOutlineColor);
            grpColors.Controls.Add(lblTextOutlineColor);
            grpColors.Controls.Add(picExfilClosedIconColor);
            grpColors.Controls.Add(lblExfilClosedIconColor);
            grpColors.Controls.Add(picExfilPendingIconColor);
            grpColors.Controls.Add(lblExfilPendingIconColor);
            grpColors.Controls.Add(picExfilActiveIconColor);
            grpColors.Controls.Add(lblExfilActiveIconColor);
            grpColors.Controls.Add(picExfilClosedTextColor);
            grpColors.Controls.Add(lblExfilClosedTextColor);
            grpColors.Controls.Add(picExfilPendingTextColor);
            grpColors.Controls.Add(lblExfilPendingTextColor);
            grpColors.Controls.Add(picExfilActiveTextColor);
            grpColors.Controls.Add(lblExfilOpenTextColor);
            grpColors.Controls.Add(picQuestZonesColor);
            grpColors.Controls.Add(lblQuestZonesColor);
            grpColors.Controls.Add(picQuestItemsColor);
            grpColors.Controls.Add(lblQuestItemsColor);
            grpColors.Controls.Add(picImportantLootColor);
            grpColors.Controls.Add(lblImportantLootColor);
            grpColors.Controls.Add(picUSECColor);
            grpColors.Controls.Add(picRegularLootColor);
            grpColors.Controls.Add(lblRegularLootColor);
            grpColors.Controls.Add(picTeamHoverColor);
            grpColors.Controls.Add(lblTeamHoverColor);
            grpColors.Controls.Add(picTeammateColor);
            grpColors.Controls.Add(lblTeammateColor);
            grpColors.Controls.Add(picLocalPlayerColor);
            grpColors.Controls.Add(lblLocalPlayerColor);
            grpColors.Controls.Add(picBEARColor);
            grpColors.Controls.Add(picBossColor);
            grpColors.Controls.Add(picAIRaiderColor);
            grpColors.Controls.Add(picPScavColor);
            grpColors.Controls.Add(lblBEARColor);
            grpColors.Controls.Add(lblUSECColor);
            grpColors.Controls.Add(lblBossColor);
            grpColors.Controls.Add(lblAIRaiderColor);
            grpColors.Controls.Add(lblPScavColor);
            grpColors.Controls.Add(picAIScavColor);
            grpColors.Controls.Add(lblAIScavColor);
            grpColors.Location = new Point(888, 22);
            grpColors.Name = "grpColors";
            grpColors.Size = new Size(162, 522);
            grpColors.TabIndex = 28;
            grpColors.TabStop = false;
            grpColors.Text = "Colors";
            // 
            // picDeathMarkerColor
            // 
            picDeathMarkerColor.BackColor = Color.Transparent;
            picDeathMarkerColor.Location = new Point(108, 492);
            picDeathMarkerColor.Name = "picDeathMarkerColor";
            picDeathMarkerColor.Size = new Size(47, 18);
            picDeathMarkerColor.TabIndex = 55;
            picDeathMarkerColor.TabStop = false;
            picDeathMarkerColor.Click += picDeathMarkerColor_Click;
            // 
            // lblDeathMarkerColor
            // 
            lblDeathMarkerColor.AutoSize = true;
            lblDeathMarkerColor.Location = new Point(21, 492);
            lblDeathMarkerColor.Name = "lblDeathMarkerColor";
            lblDeathMarkerColor.Size = new Size(81, 15);
            lblDeathMarkerColor.TabIndex = 54;
            lblDeathMarkerColor.Text = "Death Marker:";
            // 
            // picTextOutlineColor
            // 
            picTextOutlineColor.BackColor = Color.Transparent;
            picTextOutlineColor.Location = new Point(108, 468);
            picTextOutlineColor.Name = "picTextOutlineColor";
            picTextOutlineColor.Size = new Size(47, 18);
            picTextOutlineColor.TabIndex = 53;
            picTextOutlineColor.TabStop = false;
            picTextOutlineColor.Click += picTextOutlineColor_Click;
            // 
            // lblTextOutlineColor
            // 
            lblTextOutlineColor.AutoSize = true;
            lblTextOutlineColor.Location = new Point(29, 466);
            lblTextOutlineColor.Name = "lblTextOutlineColor";
            lblTextOutlineColor.Size = new Size(73, 15);
            lblTextOutlineColor.TabIndex = 52;
            lblTextOutlineColor.Text = "Text Outline:";
            // 
            // picExfilClosedIconColor
            // 
            picExfilClosedIconColor.BackColor = Color.Transparent;
            picExfilClosedIconColor.Location = new Point(109, 444);
            picExfilClosedIconColor.Name = "picExfilClosedIconColor";
            picExfilClosedIconColor.Size = new Size(47, 18);
            picExfilClosedIconColor.TabIndex = 51;
            picExfilClosedIconColor.TabStop = false;
            picExfilClosedIconColor.Click += picExfilClosedIconColor_Click;
            // 
            // lblExfilClosedIconColor
            // 
            lblExfilClosedIconColor.AutoSize = true;
            lblExfilClosedIconColor.Location = new Point(8, 444);
            lblExfilClosedIconColor.Name = "lblExfilClosedIconColor";
            lblExfilClosedIconColor.Size = new Size(97, 15);
            lblExfilClosedIconColor.TabIndex = 50;
            lblExfilClosedIconColor.Text = "Exfil Closed Icon:";
            // 
            // picExfilPendingIconColor
            // 
            picExfilPendingIconColor.BackColor = Color.Transparent;
            picExfilPendingIconColor.Location = new Point(108, 396);
            picExfilPendingIconColor.Name = "picExfilPendingIconColor";
            picExfilPendingIconColor.Size = new Size(47, 18);
            picExfilPendingIconColor.TabIndex = 49;
            picExfilPendingIconColor.TabStop = false;
            picExfilPendingIconColor.Click += picExfilPendingIconColor_Click;
            // 
            // lblExfilPendingIconColor
            // 
            lblExfilPendingIconColor.AutoSize = true;
            lblExfilPendingIconColor.Location = new Point(-1, 396);
            lblExfilPendingIconColor.Name = "lblExfilPendingIconColor";
            lblExfilPendingIconColor.Size = new Size(105, 15);
            lblExfilPendingIconColor.TabIndex = 48;
            lblExfilPendingIconColor.Text = "Exfil Pending Icon:";
            // 
            // picExfilActiveIconColor
            // 
            picExfilActiveIconColor.BackColor = Color.Transparent;
            picExfilActiveIconColor.Location = new Point(108, 348);
            picExfilActiveIconColor.Name = "picExfilActiveIconColor";
            picExfilActiveIconColor.Size = new Size(47, 18);
            picExfilActiveIconColor.TabIndex = 47;
            picExfilActiveIconColor.TabStop = false;
            picExfilActiveIconColor.Click += picExfilActiveIconColor_Click;
            // 
            // lblExfilActiveIconColor
            // 
            lblExfilActiveIconColor.AutoSize = true;
            lblExfilActiveIconColor.Location = new Point(10, 348);
            lblExfilActiveIconColor.Name = "lblExfilActiveIconColor";
            lblExfilActiveIconColor.Size = new Size(94, 15);
            lblExfilActiveIconColor.TabIndex = 46;
            lblExfilActiveIconColor.Text = "Exfil Active Icon:";
            // 
            // picExfilClosedTextColor
            // 
            picExfilClosedTextColor.BackColor = Color.Transparent;
            picExfilClosedTextColor.Location = new Point(108, 420);
            picExfilClosedTextColor.Name = "picExfilClosedTextColor";
            picExfilClosedTextColor.Size = new Size(47, 18);
            picExfilClosedTextColor.TabIndex = 45;
            picExfilClosedTextColor.TabStop = false;
            picExfilClosedTextColor.Click += picExfilClosedColor_Click;
            // 
            // lblExfilClosedTextColor
            // 
            lblExfilClosedTextColor.AutoSize = true;
            lblExfilClosedTextColor.Location = new Point(7, 420);
            lblExfilClosedTextColor.Name = "lblExfilClosedTextColor";
            lblExfilClosedTextColor.Size = new Size(95, 15);
            lblExfilClosedTextColor.TabIndex = 44;
            lblExfilClosedTextColor.Text = "Exfil Closed Text:";
            // 
            // picExfilPendingTextColor
            // 
            picExfilPendingTextColor.BackColor = Color.Transparent;
            picExfilPendingTextColor.Location = new Point(108, 372);
            picExfilPendingTextColor.Name = "picExfilPendingTextColor";
            picExfilPendingTextColor.Size = new Size(47, 18);
            picExfilPendingTextColor.TabIndex = 43;
            picExfilPendingTextColor.TabStop = false;
            picExfilPendingTextColor.Click += picExfilPendingColor_Click;
            // 
            // lblExfilPendingTextColor
            // 
            lblExfilPendingTextColor.AutoSize = true;
            lblExfilPendingTextColor.Location = new Point(-1, 372);
            lblExfilPendingTextColor.Name = "lblExfilPendingTextColor";
            lblExfilPendingTextColor.Size = new Size(103, 15);
            lblExfilPendingTextColor.TabIndex = 42;
            lblExfilPendingTextColor.Text = "Exfil Pending Text:";
            // 
            // picExfilActiveTextColor
            // 
            picExfilActiveTextColor.BackColor = Color.Transparent;
            picExfilActiveTextColor.Location = new Point(108, 324);
            picExfilActiveTextColor.Name = "picExfilActiveTextColor";
            picExfilActiveTextColor.Size = new Size(47, 18);
            picExfilActiveTextColor.TabIndex = 41;
            picExfilActiveTextColor.TabStop = false;
            picExfilActiveTextColor.Click += picExfilActiveColor_Click;
            // 
            // lblExfilOpenTextColor
            // 
            lblExfilOpenTextColor.AutoSize = true;
            lblExfilOpenTextColor.Location = new Point(10, 324);
            lblExfilOpenTextColor.Name = "lblExfilOpenTextColor";
            lblExfilOpenTextColor.Size = new Size(92, 15);
            lblExfilOpenTextColor.TabIndex = 40;
            lblExfilOpenTextColor.Text = "Exfil Active Text:";
            // 
            // picQuestZonesColor
            // 
            picQuestZonesColor.BackColor = Color.Transparent;
            picQuestZonesColor.Location = new Point(108, 300);
            picQuestZonesColor.Name = "picQuestZonesColor";
            picQuestZonesColor.Size = new Size(47, 18);
            picQuestZonesColor.TabIndex = 39;
            picQuestZonesColor.TabStop = false;
            picQuestZonesColor.Click += picQuestZonesColor_Click;
            // 
            // lblQuestZonesColor
            // 
            lblQuestZonesColor.AutoSize = true;
            lblQuestZonesColor.Location = new Point(26, 300);
            lblQuestZonesColor.Name = "lblQuestZonesColor";
            lblQuestZonesColor.Size = new Size(76, 15);
            lblQuestZonesColor.TabIndex = 38;
            lblQuestZonesColor.Text = "Quest Zones:";
            // 
            // picQuestItemsColor
            // 
            picQuestItemsColor.BackColor = Color.Transparent;
            picQuestItemsColor.Location = new Point(108, 276);
            picQuestItemsColor.Name = "picQuestItemsColor";
            picQuestItemsColor.Size = new Size(47, 18);
            picQuestItemsColor.TabIndex = 37;
            picQuestItemsColor.TabStop = false;
            picQuestItemsColor.Click += picQuestItemsColor_Click;
            // 
            // lblQuestItemsColor
            // 
            lblQuestItemsColor.AutoSize = true;
            lblQuestItemsColor.Location = new Point(29, 276);
            lblQuestItemsColor.Name = "lblQuestItemsColor";
            lblQuestItemsColor.Size = new Size(73, 15);
            lblQuestItemsColor.TabIndex = 36;
            lblQuestItemsColor.Text = "Quest Items:";
            // 
            // picImportantLootColor
            // 
            picImportantLootColor.BackColor = Color.Transparent;
            picImportantLootColor.Location = new Point(108, 252);
            picImportantLootColor.Name = "picImportantLootColor";
            picImportantLootColor.Size = new Size(47, 18);
            picImportantLootColor.TabIndex = 35;
            picImportantLootColor.TabStop = false;
            picImportantLootColor.Click += picImportantLootColor_Click;
            // 
            // lblImportantLootColor
            // 
            lblImportantLootColor.AutoSize = true;
            lblImportantLootColor.Location = new Point(12, 252);
            lblImportantLootColor.Name = "lblImportantLootColor";
            lblImportantLootColor.Size = new Size(90, 15);
            lblImportantLootColor.TabIndex = 34;
            lblImportantLootColor.Text = "Important Loot:";
            // 
            // picUSECColor
            // 
            picUSECColor.BackColor = Color.Transparent;
            picUSECColor.Location = new Point(108, 108);
            picUSECColor.Name = "picUSECColor";
            picUSECColor.Size = new Size(47, 18);
            picUSECColor.TabIndex = 26;
            picUSECColor.TabStop = false;
            picUSECColor.Click += picUSECColor_Click;
            // 
            // picRegularLootColor
            // 
            picRegularLootColor.BackColor = Color.Transparent;
            picRegularLootColor.Location = new Point(108, 228);
            picRegularLootColor.Name = "picRegularLootColor";
            picRegularLootColor.Size = new Size(47, 18);
            picRegularLootColor.TabIndex = 33;
            picRegularLootColor.TabStop = false;
            picRegularLootColor.Click += picRegularLootColor_Click;
            // 
            // lblRegularLootColor
            // 
            lblRegularLootColor.AutoSize = true;
            lblRegularLootColor.Location = new Point(25, 228);
            lblRegularLootColor.Name = "lblRegularLootColor";
            lblRegularLootColor.Size = new Size(77, 15);
            lblRegularLootColor.TabIndex = 32;
            lblRegularLootColor.Text = "Regular Loot:";
            // 
            // picTeamHoverColor
            // 
            picTeamHoverColor.BackColor = Color.Transparent;
            picTeamHoverColor.Location = new Point(108, 204);
            picTeamHoverColor.Name = "picTeamHoverColor";
            picTeamHoverColor.Size = new Size(47, 18);
            picTeamHoverColor.TabIndex = 31;
            picTeamHoverColor.TabStop = false;
            picTeamHoverColor.Click += picTeamHoverColor_Click;
            // 
            // lblTeamHoverColor
            // 
            lblTeamHoverColor.AutoSize = true;
            lblTeamHoverColor.Location = new Point(29, 204);
            lblTeamHoverColor.Name = "lblTeamHoverColor";
            lblTeamHoverColor.Size = new Size(73, 15);
            lblTeamHoverColor.TabIndex = 30;
            lblTeamHoverColor.Text = "Team Hover:";
            // 
            // picTeammateColor
            // 
            picTeammateColor.BackColor = Color.Transparent;
            picTeammateColor.Location = new Point(108, 180);
            picTeammateColor.Name = "picTeammateColor";
            picTeammateColor.Size = new Size(47, 18);
            picTeammateColor.TabIndex = 29;
            picTeammateColor.TabStop = false;
            picTeammateColor.Click += picTeammateColor_Click;
            // 
            // lblTeammateColor
            // 
            lblTeammateColor.AutoSize = true;
            lblTeammateColor.Location = new Point(37, 180);
            lblTeammateColor.Name = "lblTeammateColor";
            lblTeammateColor.Size = new Size(65, 15);
            lblTeammateColor.TabIndex = 28;
            lblTeammateColor.Text = "Teammate:";
            // 
            // picLocalPlayerColor
            // 
            picLocalPlayerColor.BackColor = Color.Transparent;
            picLocalPlayerColor.Location = new Point(108, 156);
            picLocalPlayerColor.Name = "picLocalPlayerColor";
            picLocalPlayerColor.Size = new Size(47, 18);
            picLocalPlayerColor.TabIndex = 27;
            picLocalPlayerColor.TabStop = false;
            picLocalPlayerColor.Click += picLocalPlayerColor_Click;
            // 
            // lblLocalPlayerColor
            // 
            lblLocalPlayerColor.AutoSize = true;
            lblLocalPlayerColor.Location = new Point(32, 156);
            lblLocalPlayerColor.Name = "lblLocalPlayerColor";
            lblLocalPlayerColor.Size = new Size(70, 15);
            lblLocalPlayerColor.TabIndex = 26;
            lblLocalPlayerColor.Text = "LocalPlayer:";
            // 
            // picBEARColor
            // 
            picBEARColor.BackColor = Color.Transparent;
            picBEARColor.Location = new Point(108, 132);
            picBEARColor.Name = "picBEARColor";
            picBEARColor.Size = new Size(47, 18);
            picBEARColor.TabIndex = 25;
            picBEARColor.TabStop = false;
            picBEARColor.Click += picBEARColor_Click;
            // 
            // picBossColor
            // 
            picBossColor.BackColor = Color.Transparent;
            picBossColor.Location = new Point(108, 84);
            picBossColor.Name = "picBossColor";
            picBossColor.Size = new Size(47, 18);
            picBossColor.TabIndex = 24;
            picBossColor.TabStop = false;
            picBossColor.Click += picBossColor_Click;
            // 
            // picAIRaiderColor
            // 
            picAIRaiderColor.BackColor = Color.Transparent;
            picAIRaiderColor.Location = new Point(108, 60);
            picAIRaiderColor.Name = "picAIRaiderColor";
            picAIRaiderColor.Size = new Size(47, 18);
            picAIRaiderColor.TabIndex = 23;
            picAIRaiderColor.TabStop = false;
            picAIRaiderColor.Click += picAIRaiderColor_Click;
            // 
            // picPScavColor
            // 
            picPScavColor.BackColor = Color.Transparent;
            picPScavColor.Location = new Point(108, 36);
            picPScavColor.Name = "picPScavColor";
            picPScavColor.Size = new Size(47, 18);
            picPScavColor.TabIndex = 22;
            picPScavColor.TabStop = false;
            picPScavColor.Click += picPScavColor_Click;
            // 
            // lblBEARColor
            // 
            lblBEARColor.AutoSize = true;
            lblBEARColor.Location = new Point(64, 132);
            lblBEARColor.Name = "lblBEARColor";
            lblBEARColor.Size = new Size(38, 15);
            lblBEARColor.TabIndex = 21;
            lblBEARColor.Text = "BEAR:";
            // 
            // lblUSECColor
            // 
            lblUSECColor.AutoSize = true;
            lblUSECColor.Location = new Point(64, 108);
            lblUSECColor.Name = "lblUSECColor";
            lblUSECColor.Size = new Size(38, 15);
            lblUSECColor.TabIndex = 20;
            lblUSECColor.Text = "USEC:";
            // 
            // lblBossColor
            // 
            lblBossColor.AutoSize = true;
            lblBossColor.Location = new Point(68, 84);
            lblBossColor.Name = "lblBossColor";
            lblBossColor.Size = new Size(34, 15);
            lblBossColor.TabIndex = 19;
            lblBossColor.Text = "Boss:";
            // 
            // lblAIRaiderColor
            // 
            lblAIRaiderColor.AutoSize = true;
            lblAIRaiderColor.Location = new Point(20, 60);
            lblAIRaiderColor.Name = "lblAIRaiderColor";
            lblAIRaiderColor.Size = new Size(82, 15);
            lblAIRaiderColor.TabIndex = 18;
            lblAIRaiderColor.Text = "Raider/Rogue:";
            // 
            // lblPScavColor
            // 
            lblPScavColor.AutoSize = true;
            lblPScavColor.Location = new Point(33, 34);
            lblPScavColor.Name = "lblPScavColor";
            lblPScavColor.Size = new Size(69, 15);
            lblPScavColor.TabIndex = 17;
            lblPScavColor.Text = "Player Scav:";
            // 
            // picAIScavColor
            // 
            picAIScavColor.BackColor = Color.Transparent;
            picAIScavColor.Location = new Point(108, 12);
            picAIScavColor.Name = "picAIScavColor";
            picAIScavColor.Size = new Size(47, 18);
            picAIScavColor.TabIndex = 16;
            picAIScavColor.TabStop = false;
            picAIScavColor.Click += picAIScavColor_Click;
            // 
            // lblAIScavColor
            // 
            lblAIScavColor.AutoSize = true;
            lblAIScavColor.Location = new Point(54, 12);
            lblAIScavColor.Name = "lblAIScavColor";
            lblAIScavColor.Size = new Size(48, 15);
            lblAIScavColor.TabIndex = 15;
            lblAIScavColor.Text = "AI Scav:";
            // 
            // grpLootFilters
            // 
            grpLootFilters.Controls.Add(lstEditLootFilters);
            grpLootFilters.Controls.Add(btnCancelEditFilter);
            grpLootFilters.Controls.Add(btnEditSaveFilter);
            grpLootFilters.Controls.Add(chkLootFilterEditActive);
            grpLootFilters.Controls.Add(btnAddNewFilter);
            grpLootFilters.Controls.Add(btnRemoveFilter);
            grpLootFilters.Controls.Add(lblLootFilterColor);
            grpLootFilters.Controls.Add(btnFilterPriorityDown);
            grpLootFilters.Controls.Add(btnFilterPriorityUp);
            grpLootFilters.Controls.Add(lblFilterEditName);
            grpLootFilters.Controls.Add(txtLootFilterEditName);
            grpLootFilters.Controls.Add(picLootFilterEditColor);
            grpLootFilters.Location = new Point(474, 274);
            grpLootFilters.Name = "grpLootFilters";
            grpLootFilters.Size = new Size(408, 352);
            grpLootFilters.TabIndex = 27;
            grpLootFilters.TabStop = false;
            grpLootFilters.Text = "Loot Filters/Profiles";
            // 
            // lstEditLootFilters
            // 
            lstEditLootFilters.DisplayMember = "Name";
            lstEditLootFilters.FormattingEnabled = true;
            lstEditLootFilters.ItemHeight = 15;
            lstEditLootFilters.Location = new Point(6, 22);
            lstEditLootFilters.Name = "lstEditLootFilters";
            lstEditLootFilters.Size = new Size(168, 259);
            lstEditLootFilters.TabIndex = 14;
            lstEditLootFilters.SelectedIndexChanged += lstEditLootFilters_SelectedIndexChanged;
            // 
            // btnCancelEditFilter
            // 
            btnCancelEditFilter.Location = new Point(331, 103);
            btnCancelEditFilter.Name = "btnCancelEditFilter";
            btnCancelEditFilter.Size = new Size(71, 23);
            btnCancelEditFilter.TabIndex = 13;
            btnCancelEditFilter.Text = "Cancel";
            toolTip.SetToolTip(btnCancelEditFilter, "Cancels changes to the current loot filter");
            btnCancelEditFilter.UseVisualStyleBackColor = true;
            btnCancelEditFilter.Visible = false;
            btnCancelEditFilter.Click += btnCancelEditFilter_Click;
            // 
            // btnEditSaveFilter
            // 
            btnEditSaveFilter.Location = new Point(269, 103);
            btnEditSaveFilter.Name = "btnEditSaveFilter";
            btnEditSaveFilter.Size = new Size(56, 23);
            btnEditSaveFilter.TabIndex = 12;
            btnEditSaveFilter.Text = "Edit";
            toolTip.SetToolTip(btnEditSaveFilter, "Edits / Saves the current loot filter");
            btnEditSaveFilter.UseVisualStyleBackColor = true;
            btnEditSaveFilter.Click += btnEditSaveFilter_Click;
            // 
            // chkLootFilterEditActive
            // 
            chkLootFilterEditActive.AutoSize = true;
            chkLootFilterEditActive.Enabled = false;
            chkLootFilterEditActive.Location = new Point(269, 78);
            chkLootFilterEditActive.Name = "chkLootFilterEditActive";
            chkLootFilterEditActive.Size = new Size(70, 19);
            chkLootFilterEditActive.TabIndex = 11;
            chkLootFilterEditActive.Text = "Is Active";
            toolTip.SetToolTip(chkLootFilterEditActive, "Turns the current loot filter on/off");
            chkLootFilterEditActive.UseVisualStyleBackColor = true;
            // 
            // btnAddNewFilter
            // 
            btnAddNewFilter.Location = new Point(180, 229);
            btnAddNewFilter.Name = "btnAddNewFilter";
            btnAddNewFilter.Size = new Size(22, 23);
            btnAddNewFilter.TabIndex = 9;
            btnAddNewFilter.Text = "✚";
            btnAddNewFilter.UseVisualStyleBackColor = true;
            btnAddNewFilter.Click += btnAddNewFilter_Click;
            // 
            // btnRemoveFilter
            // 
            btnRemoveFilter.Location = new Point(180, 258);
            btnRemoveFilter.Name = "btnRemoveFilter";
            btnRemoveFilter.Size = new Size(22, 23);
            btnRemoveFilter.TabIndex = 8;
            btnRemoveFilter.Text = "✖";
            btnRemoveFilter.UseVisualStyleBackColor = true;
            btnRemoveFilter.Click += btnRemoveFilter_Click;
            // 
            // lblLootFilterColor
            // 
            lblLootFilterColor.AutoSize = true;
            lblLootFilterColor.Location = new Point(224, 53);
            lblLootFilterColor.Name = "lblLootFilterColor";
            lblLootFilterColor.Size = new Size(39, 15);
            lblLootFilterColor.TabIndex = 6;
            lblLootFilterColor.Text = "Color:";
            // 
            // btnFilterPriorityDown
            // 
            btnFilterPriorityDown.Location = new Point(180, 51);
            btnFilterPriorityDown.Name = "btnFilterPriorityDown";
            btnFilterPriorityDown.Size = new Size(22, 23);
            btnFilterPriorityDown.TabIndex = 5;
            btnFilterPriorityDown.Text = "▼";
            btnFilterPriorityDown.UseVisualStyleBackColor = true;
            btnFilterPriorityDown.Click += btnFilterPriorityDown_Click;
            // 
            // btnFilterPriorityUp
            // 
            btnFilterPriorityUp.Location = new Point(180, 22);
            btnFilterPriorityUp.Name = "btnFilterPriorityUp";
            btnFilterPriorityUp.Size = new Size(22, 23);
            btnFilterPriorityUp.TabIndex = 4;
            btnFilterPriorityUp.Text = "▲";
            btnFilterPriorityUp.UseVisualStyleBackColor = true;
            btnFilterPriorityUp.Click += btnFilterPriorityUp_Click;
            // 
            // lblFilterEditName
            // 
            lblFilterEditName.AutoSize = true;
            lblFilterEditName.Location = new Point(221, 24);
            lblFilterEditName.Name = "lblFilterEditName";
            lblFilterEditName.Size = new Size(42, 15);
            lblFilterEditName.TabIndex = 2;
            lblFilterEditName.Text = "Name:";
            // 
            // txtLootFilterEditName
            // 
            txtLootFilterEditName.Enabled = false;
            txtLootFilterEditName.Location = new Point(269, 21);
            txtLootFilterEditName.Name = "txtLootFilterEditName";
            txtLootFilterEditName.Size = new Size(133, 23);
            txtLootFilterEditName.TabIndex = 1;
            // 
            // picLootFilterEditColor
            // 
            picLootFilterEditColor.BackColor = Color.Transparent;
            picLootFilterEditColor.Enabled = false;
            picLootFilterEditColor.Location = new Point(269, 53);
            picLootFilterEditColor.Name = "picLootFilterEditColor";
            picLootFilterEditColor.Size = new Size(47, 18);
            picLootFilterEditColor.TabIndex = 7;
            picLootFilterEditColor.TabStop = false;
            picLootFilterEditColor.Click += picLootFilterPreview_Click;
            // 
            // grpLoot
            // 
            grpLoot.Controls.Add(chkAutoLootRefresh);
            grpLoot.Controls.Add(chkShowSubItems);
            grpLoot.Controls.Add(chkShowCorpses);
            grpLoot.Controls.Add(grpLootValues);
            grpLoot.Controls.Add(btnRefreshLoot);
            grpLoot.Controls.Add(chkHideLootValue);
            grpLoot.Controls.Add(chkImportantLootOnly);
            grpLoot.Location = new Point(474, 22);
            grpLoot.Name = "grpLoot";
            grpLoot.Size = new Size(408, 246);
            grpLoot.TabIndex = 13;
            grpLoot.TabStop = false;
            grpLoot.Text = "Loot";
            // 
            // chkShowSubItems
            // 
            chkShowSubItems.AutoSize = true;
            chkShowSubItems.Location = new Point(112, 225);
            chkShowSubItems.Name = "chkShowSubItems";
            chkShowSubItems.Size = new Size(112, 19);
            chkShowSubItems.TabIndex = 34;
            chkShowSubItems.Text = "Show Sub-Items";
            toolTip.SetToolTip(chkShowSubItems, "Hides item value");
            chkShowSubItems.UseVisualStyleBackColor = true;
            chkShowSubItems.CheckedChanged += chkShowSubItems_CheckedChanged;
            // 
            // chkShowCorpses
            // 
            chkShowCorpses.AutoSize = true;
            chkShowCorpses.Location = new Point(6, 225);
            chkShowCorpses.Name = "chkShowCorpses";
            chkShowCorpses.Size = new Size(100, 19);
            chkShowCorpses.TabIndex = 33;
            chkShowCorpses.Text = "Show Corpses";
            toolTip.SetToolTip(chkShowCorpses, "Hides item value");
            chkShowCorpses.UseVisualStyleBackColor = true;
            chkShowCorpses.CheckedChanged += chkShowCorpses_CheckedChanged;
            // 
            // grpLootValues
            // 
            grpLootValues.Controls.Add(lblSubItemDisplay);
            grpLootValues.Controls.Add(lblSubItemValueSlider);
            grpLootValues.Controls.Add(trkSubItemLootValue);
            grpLootValues.Controls.Add(lblCorpseDisplay);
            grpLootValues.Controls.Add(lblCorpsValueSlider);
            grpLootValues.Controls.Add(trkCorpseLootValue);
            grpLootValues.Controls.Add(lblImportantLootDisplay);
            grpLootValues.Controls.Add(lblRegularLootDisplay);
            grpLootValues.Controls.Add(lblImportantLootSlider);
            grpLootValues.Controls.Add(trkImportantLootValue);
            grpLootValues.Controls.Add(lblRegularLoot);
            grpLootValues.Controls.Add(trkRegularLootValue);
            grpLootValues.Location = new Point(6, 18);
            grpLootValues.Name = "grpLootValues";
            grpLootValues.Size = new Size(306, 176);
            grpLootValues.TabIndex = 32;
            grpLootValues.TabStop = false;
            grpLootValues.Text = "Minimum Ruble Value";
            // 
            // lblSubItemDisplay
            // 
            lblSubItemDisplay.AutoSize = true;
            lblSubItemDisplay.Location = new Point(68, 133);
            lblSubItemDisplay.Name = "lblSubItemDisplay";
            lblSubItemDisplay.Size = new Size(25, 15);
            lblSubItemDisplay.TabIndex = 40;
            lblSubItemDisplay.Text = "xxx";
            // 
            // lblSubItemValueSlider
            // 
            lblSubItemValueSlider.AutoSize = true;
            lblSubItemValueSlider.Location = new Point(32, 117);
            lblSubItemValueSlider.Name = "lblSubItemValueSlider";
            lblSubItemValueSlider.Size = new Size(61, 15);
            lblSubItemValueSlider.TabIndex = 38;
            lblSubItemValueSlider.Text = "Sub-items";
            // 
            // trkSubItemLootValue
            // 
            trkSubItemLootValue.BackColor = SystemColors.Control;
            trkSubItemLootValue.LargeChange = 10;
            trkSubItemLootValue.Location = new Point(99, 120);
            trkSubItemLootValue.Maximum = 200;
            trkSubItemLootValue.Minimum = 1;
            trkSubItemLootValue.Name = "trkSubItemLootValue";
            trkSubItemLootValue.Size = new Size(207, 45);
            trkSubItemLootValue.SmallChange = 10;
            trkSubItemLootValue.TabIndex = 39;
            trkSubItemLootValue.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkSubItemLootValue, "The minimum value for sub-items to be shown (eg mods on a weapon)");
            trkSubItemLootValue.Value = 35;
            trkSubItemLootValue.Scroll += trkSubItemLootValue_Scroll;
            // 
            // lblCorpseDisplay
            // 
            lblCorpseDisplay.AutoSize = true;
            lblCorpseDisplay.Location = new Point(68, 99);
            lblCorpseDisplay.Name = "lblCorpseDisplay";
            lblCorpseDisplay.Size = new Size(25, 15);
            lblCorpseDisplay.TabIndex = 37;
            lblCorpseDisplay.Text = "xxx";
            // 
            // lblCorpsValueSlider
            // 
            lblCorpsValueSlider.AutoSize = true;
            lblCorpsValueSlider.Location = new Point(44, 84);
            lblCorpsValueSlider.Name = "lblCorpsValueSlider";
            lblCorpsValueSlider.Size = new Size(49, 15);
            lblCorpsValueSlider.TabIndex = 35;
            lblCorpsValueSlider.Text = "Corpses";
            // 
            // trkCorpseLootValue
            // 
            trkCorpseLootValue.BackColor = SystemColors.Control;
            trkCorpseLootValue.LargeChange = 10;
            trkCorpseLootValue.Location = new Point(99, 85);
            trkCorpseLootValue.Maximum = 800;
            trkCorpseLootValue.Minimum = 10;
            trkCorpseLootValue.Name = "trkCorpseLootValue";
            trkCorpseLootValue.Size = new Size(207, 45);
            trkCorpseLootValue.SmallChange = 10;
            trkCorpseLootValue.TabIndex = 36;
            trkCorpseLootValue.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkCorpseLootValue, "The minimum value for a corpse to be shown");
            trkCorpseLootValue.Value = 250;
            trkCorpseLootValue.Scroll += trkCorpseLootValue_Scroll;
            // 
            // lblImportantLootDisplay
            // 
            lblImportantLootDisplay.AutoSize = true;
            lblImportantLootDisplay.Location = new Point(68, 69);
            lblImportantLootDisplay.Name = "lblImportantLootDisplay";
            lblImportantLootDisplay.Size = new Size(25, 15);
            lblImportantLootDisplay.TabIndex = 34;
            lblImportantLootDisplay.Text = "xxx";
            // 
            // lblRegularLootDisplay
            // 
            lblRegularLootDisplay.AutoSize = true;
            lblRegularLootDisplay.Location = new Point(68, 37);
            lblRegularLootDisplay.Name = "lblRegularLootDisplay";
            lblRegularLootDisplay.Size = new Size(25, 15);
            lblRegularLootDisplay.TabIndex = 27;
            lblRegularLootDisplay.Text = "xxx";
            // 
            // lblImportantLootSlider
            // 
            lblImportantLootSlider.AutoSize = true;
            lblImportantLootSlider.Location = new Point(6, 54);
            lblImportantLootSlider.Name = "lblImportantLootSlider";
            lblImportantLootSlider.Size = new Size(87, 15);
            lblImportantLootSlider.TabIndex = 32;
            lblImportantLootSlider.Text = "Important Loot";
            // 
            // trkImportantLootValue
            // 
            trkImportantLootValue.BackColor = SystemColors.Control;
            trkImportantLootValue.LargeChange = 10;
            trkImportantLootValue.Location = new Point(99, 50);
            trkImportantLootValue.Maximum = 500;
            trkImportantLootValue.Minimum = 250;
            trkImportantLootValue.Name = "trkImportantLootValue";
            trkImportantLootValue.Size = new Size(207, 45);
            trkImportantLootValue.SmallChange = 10;
            trkImportantLootValue.TabIndex = 33;
            trkImportantLootValue.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkImportantLootValue, "The minimum value for loot to be considered 'important'");
            trkImportantLootValue.Value = 300;
            trkImportantLootValue.Scroll += trkImportantLootValue_Scroll;
            // 
            // lblRegularLoot
            // 
            lblRegularLoot.AutoSize = true;
            lblRegularLoot.Location = new Point(19, 22);
            lblRegularLoot.Name = "lblRegularLoot";
            lblRegularLoot.Size = new Size(74, 15);
            lblRegularLoot.TabIndex = 0;
            lblRegularLoot.Text = "Regular Loot";
            // 
            // trkRegularLootValue
            // 
            trkRegularLootValue.BackColor = SystemColors.Control;
            trkRegularLootValue.LargeChange = 10;
            trkRegularLootValue.Location = new Point(99, 20);
            trkRegularLootValue.Maximum = 249;
            trkRegularLootValue.Minimum = 10;
            trkRegularLootValue.Name = "trkRegularLootValue";
            trkRegularLootValue.Size = new Size(207, 45);
            trkRegularLootValue.SmallChange = 10;
            trkRegularLootValue.TabIndex = 31;
            trkRegularLootValue.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkRegularLootValue, "The minimum value for loot to be displayed");
            trkRegularLootValue.Value = 85;
            trkRegularLootValue.Scroll += trkRegularLootValue_Scroll;
            // 
            // btnRefreshLoot
            // 
            btnRefreshLoot.Location = new Point(318, 27);
            btnRefreshLoot.Name = "btnRefreshLoot";
            btnRefreshLoot.Size = new Size(85, 41);
            btnRefreshLoot.TabIndex = 21;
            btnRefreshLoot.Text = "Refresh Loot";
            toolTip.SetToolTip(btnRefreshLoot, "Manually triggers loot refresh");
            btnRefreshLoot.UseVisualStyleBackColor = true;
            btnRefreshLoot.Click += btnRefreshLoot_Click;
            // 
            // chkHideLootValue
            // 
            chkHideLootValue.AutoSize = true;
            chkHideLootValue.Location = new Point(290, 201);
            chkHideLootValue.Name = "chkHideLootValue";
            chkHideLootValue.Size = new Size(82, 19);
            chkHideLootValue.TabIndex = 29;
            chkHideLootValue.Text = "Hide Value";
            toolTip.SetToolTip(chkHideLootValue, "Hides item value");
            chkHideLootValue.UseVisualStyleBackColor = true;
            chkHideLootValue.CheckedChanged += chkHideLootValue_CheckedChanged;
            // 
            // chkImportantLootOnly
            // 
            chkImportantLootOnly.AutoSize = true;
            chkImportantLootOnly.Location = new Point(133, 201);
            chkImportantLootOnly.Name = "chkImportantLootOnly";
            chkImportantLootOnly.Size = new Size(151, 19);
            chkImportantLootOnly.TabIndex = 22;
            chkImportantLootOnly.Text = "Important/Filtered Only";
            toolTip.SetToolTip(chkImportantLootOnly, "Only shows items considered 'important' or ones in a filter");
            chkImportantLootOnly.UseVisualStyleBackColor = true;
            chkImportantLootOnly.CheckedChanged += chkImportantLootOnly_CheckedChanged;
            // 
            // grpUserInterface
            // 
            grpUserInterface.Controls.Add(chkHideTextOutline);
            grpUserInterface.Controls.Add(chkHideExfilNames);
            grpUserInterface.Controls.Add(chkQuestHelper);
            grpUserInterface.Controls.Add(chkShowHoverArmor);
            grpUserInterface.Controls.Add(chkShowLoot);
            grpUserInterface.Controls.Add(trkAimLength);
            grpUserInterface.Controls.Add(lblAimline);
            grpUserInterface.Controls.Add(txtTeammateID);
            grpUserInterface.Controls.Add(lblPrimaryTeammate);
            grpUserInterface.Controls.Add(trkZoom);
            grpUserInterface.Controls.Add(lblUIScale);
            grpUserInterface.Controls.Add(lblZoom);
            grpUserInterface.Controls.Add(trkUIScale);
            grpUserInterface.Controls.Add(chkShowAimview);
            grpUserInterface.Controls.Add(chkHideNames);
            grpUserInterface.Location = new Point(5, 121);
            grpUserInterface.Name = "grpUserInterface";
            grpUserInterface.Size = new Size(463, 203);
            grpUserInterface.TabIndex = 26;
            grpUserInterface.TabStop = false;
            grpUserInterface.Text = "UI";
            // 
            // chkHideTextOutline
            // 
            chkHideTextOutline.AutoSize = true;
            chkHideTextOutline.Location = new Point(250, 47);
            chkHideTextOutline.Name = "chkHideTextOutline";
            chkHideTextOutline.Size = new Size(117, 19);
            chkHideTextOutline.TabIndex = 32;
            chkHideTextOutline.Text = "Hide Text Outline";
            toolTip.SetToolTip(chkHideTextOutline, "Removes the outline from text on the radar");
            chkHideTextOutline.UseVisualStyleBackColor = true;
            chkHideTextOutline.CheckedChanged += chkHideTextOutline_CheckedChanged;
            // 
            // chkHideExfilNames
            // 
            chkHideExfilNames.AutoSize = true;
            chkHideExfilNames.Location = new Point(117, 47);
            chkHideExfilNames.Name = "chkHideExfilNames";
            chkHideExfilNames.Size = new Size(116, 19);
            chkHideExfilNames.TabIndex = 31;
            chkHideExfilNames.Text = "Hide Exfil Names";
            toolTip.SetToolTip(chkHideExfilNames, "Removes names from exfiltration points");
            chkHideExfilNames.UseVisualStyleBackColor = true;
            chkHideExfilNames.CheckedChanged += chkHideExfilNames_CheckedChanged;
            // 
            // chkQuestHelper
            // 
            chkQuestHelper.AutoSize = true;
            chkQuestHelper.Location = new Point(6, 47);
            chkQuestHelper.Name = "chkQuestHelper";
            chkQuestHelper.Size = new Size(95, 19);
            chkQuestHelper.TabIndex = 30;
            chkQuestHelper.Text = "Quest Helper";
            toolTip.SetToolTip(chkQuestHelper, "Displays all active quest tasks/items on the map. Must use 'Show Loot' to display quest items.");
            chkQuestHelper.UseVisualStyleBackColor = true;
            chkQuestHelper.CheckedChanged += chkQuestHelper_CheckedChanged;
            // 
            // chkShowHoverArmor
            // 
            chkShowHoverArmor.AutoSize = true;
            chkShowHoverArmor.Location = new Point(368, 22);
            chkShowHoverArmor.Name = "chkShowHoverArmor";
            chkShowHoverArmor.Size = new Size(95, 19);
            chkShowHoverArmor.TabIndex = 29;
            chkShowHoverArmor.Text = "Hover Armor";
            toolTip.SetToolTip(chkShowHoverArmor, "Show current gear pieces when hovering over a player");
            chkShowHoverArmor.UseVisualStyleBackColor = true;
            chkShowHoverArmor.CheckedChanged += chkShowHoverArmor_CheckedChanged;
            // 
            // chkShowLoot
            // 
            chkShowLoot.AutoSize = true;
            chkShowLoot.Location = new Point(6, 22);
            chkShowLoot.Name = "chkShowLoot";
            chkShowLoot.Size = new Size(105, 19);
            chkShowLoot.TabIndex = 17;
            chkShowLoot.Text = "Show Loot (F3)";
            toolTip.SetToolTip(chkShowLoot, "Displays loose loot & lootable items on corpses/cointainers");
            chkShowLoot.UseVisualStyleBackColor = true;
            chkShowLoot.CheckedChanged += chkShowLoot_CheckedChanged;
            // 
            // trkAimLength
            // 
            trkAimLength.LargeChange = 50;
            trkAimLength.Location = new Point(100, 99);
            trkAimLength.Margin = new Padding(4, 3, 4, 3);
            trkAimLength.Maximum = 1000;
            trkAimLength.Minimum = 10;
            trkAimLength.Name = "trkAimLength";
            trkAimLength.Size = new Size(114, 45);
            trkAimLength.SmallChange = 5;
            trkAimLength.TabIndex = 11;
            trkAimLength.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkAimLength, "Length of the 'bar' or 'aim line' on the localplayer");
            trkAimLength.Value = 500;
            // 
            // lblAimline
            // 
            lblAimline.AutoSize = true;
            lblAimline.Location = new Point(4, 102);
            lblAimline.Margin = new Padding(4, 0, 4, 0);
            lblAimline.Name = "lblAimline";
            lblAimline.Size = new Size(88, 15);
            lblAimline.TabIndex = 13;
            lblAimline.Text = "Aimline Length";
            lblAimline.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtTeammateID
            // 
            txtTeammateID.Location = new Point(249, 172);
            txtTeammateID.MaxLength = 12;
            txtTeammateID.Name = "txtTeammateID";
            txtTeammateID.Size = new Size(147, 23);
            txtTeammateID.TabIndex = 25;
            toolTip.SetToolTip(txtTeammateID, "Primary teammate ID for friendly aimview");
            // 
            // lblPrimaryTeammate
            // 
            lblPrimaryTeammate.AutoSize = true;
            lblPrimaryTeammate.Location = new Point(249, 150);
            lblPrimaryTeammate.Name = "lblPrimaryTeammate";
            lblPrimaryTeammate.Size = new Size(76, 15);
            lblPrimaryTeammate.TabIndex = 22;
            lblPrimaryTeammate.Text = "Teammate ID";
            // 
            // trkZoom
            // 
            trkZoom.LargeChange = 1;
            trkZoom.Location = new Point(335, 102);
            trkZoom.Maximum = 200;
            trkZoom.Name = "trkZoom";
            trkZoom.Size = new Size(118, 45);
            trkZoom.TabIndex = 15;
            trkZoom.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkZoom, "The map zoom distance (also controlled with mouse scrolling)");
            trkZoom.Value = 100;
            // 
            // lblUIScale
            // 
            lblUIScale.AutoSize = true;
            lblUIScale.Location = new Point(44, 150);
            lblUIScale.Name = "lblUIScale";
            lblUIScale.Size = new Size(48, 15);
            lblUIScale.TabIndex = 28;
            lblUIScale.Text = "UI Scale";
            lblUIScale.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblZoom
            // 
            lblZoom.AutoSize = true;
            lblZoom.Location = new Point(241, 102);
            lblZoom.Margin = new Padding(4, 0, 4, 0);
            lblZoom.Name = "lblZoom";
            lblZoom.Size = new Size(87, 15);
            lblZoom.TabIndex = 16;
            lblZoom.Text = "Zoom Distance";
            lblZoom.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // trkUIScale
            // 
            trkUIScale.LargeChange = 10;
            trkUIScale.Location = new Point(100, 150);
            trkUIScale.Maximum = 200;
            trkUIScale.Minimum = 50;
            trkUIScale.Name = "trkUIScale";
            trkUIScale.Size = new Size(116, 45);
            trkUIScale.TabIndex = 27;
            trkUIScale.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkUIScale, "Scales the UI fonts etc, useful for larger screen resolutions");
            trkUIScale.Value = 100;
            // 
            // chkShowAimview
            // 
            chkShowAimview.AutoSize = true;
            chkShowAimview.Location = new Point(117, 22);
            chkShowAimview.Name = "chkShowAimview";
            chkShowAimview.Size = new Size(127, 19);
            chkShowAimview.TabIndex = 19;
            chkShowAimview.Text = "Show Aimview (F4)";
            toolTip.SetToolTip(chkShowAimview, "Displays the 3D aimview");
            chkShowAimview.UseVisualStyleBackColor = true;
            // 
            // chkHideNames
            // 
            chkHideNames.AutoSize = true;
            chkHideNames.Location = new Point(250, 22);
            chkHideNames.Name = "chkHideNames";
            chkHideNames.Size = new Size(114, 19);
            chkHideNames.TabIndex = 26;
            chkHideNames.Text = "Hide Names (F6)";
            toolTip.SetToolTip(chkHideNames, "Removes player names");
            chkHideNames.UseVisualStyleBackColor = true;
            // 
            // grpMemoryWriting
            // 
            grpMemoryWriting.Controls.Add(chkMasterSwitch);
            grpMemoryWriting.Controls.Add(grpGlobalFeatures);
            grpMemoryWriting.Controls.Add(grpGearFeatures);
            grpMemoryWriting.Controls.Add(grpPhysicalFeatures);
            grpMemoryWriting.Location = new Point(6, 330);
            grpMemoryWriting.Name = "grpMemoryWriting";
            grpMemoryWriting.Size = new Size(462, 296);
            grpMemoryWriting.TabIndex = 9;
            grpMemoryWriting.TabStop = false;
            grpMemoryWriting.Text = "Memory Writing [RISKY]";
            // 
            // chkMasterSwitch
            // 
            chkMasterSwitch.AutoSize = true;
            chkMasterSwitch.Location = new Point(362, 11);
            chkMasterSwitch.Name = "chkMasterSwitch";
            chkMasterSwitch.Size = new Size(100, 19);
            chkMasterSwitch.TabIndex = 35;
            chkMasterSwitch.Text = "Master Switch";
            chkMasterSwitch.UseVisualStyleBackColor = true;
            chkMasterSwitch.CheckedChanged += chkMasterSwitch_CheckedChanged;
            // 
            // grpGlobalFeatures
            // 
            grpGlobalFeatures.Controls.Add(chkExtendedReach);
            grpGlobalFeatures.Controls.Add(chkChams);
            grpGlobalFeatures.Controls.Add(chkDoubleSearch);
            grpGlobalFeatures.Location = new Point(6, 22);
            grpGlobalFeatures.Name = "grpGlobalFeatures";
            grpGlobalFeatures.Size = new Size(450, 82);
            grpGlobalFeatures.TabIndex = 34;
            grpGlobalFeatures.TabStop = false;
            grpGlobalFeatures.Text = "Global Features";
            // 
            // chkExtendedReach
            // 
            chkExtendedReach.AutoSize = true;
            chkExtendedReach.Location = new Point(143, 47);
            chkExtendedReach.Name = "chkExtendedReach";
            chkExtendedReach.Size = new Size(110, 19);
            chkExtendedReach.TabIndex = 23;
            chkExtendedReach.Text = "Extended Reach";
            toolTip.SetToolTip(chkExtendedReach, "Increases maximum loot/door interaction distance");
            chkExtendedReach.UseVisualStyleBackColor = true;
            chkExtendedReach.CheckedChanged += chkExtendedReach_CheckedChanged;
            // 
            // chkChams
            // 
            chkChams.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkChams.AutoSize = true;
            chkChams.Location = new Point(6, 22);
            chkChams.Name = "chkChams";
            chkChams.Size = new Size(142, 19);
            chkChams.TabIndex = 22;
            chkChams.Text = "Chams (Offline - WIP)";
            toolTip.SetToolTip(chkChams, "Enables chams on players [dont use lol]");
            chkChams.UseVisualStyleBackColor = true;
            chkChams.CheckedChanged += chkChams_CheckedChanged;
            // 
            // chkDoubleSearch
            // 
            chkDoubleSearch.AutoSize = true;
            chkDoubleSearch.Location = new Point(6, 47);
            chkDoubleSearch.Name = "chkDoubleSearch";
            chkDoubleSearch.Size = new Size(102, 19);
            chkDoubleSearch.TabIndex = 2;
            chkDoubleSearch.Text = "Double Search";
            toolTip.SetToolTip(chkDoubleSearch, "Enables searching x2 slots at once");
            chkDoubleSearch.UseVisualStyleBackColor = true;
            chkDoubleSearch.CheckedChanged += chkDoubleSearch_CheckedChanged;
            // 
            // grpGearFeatures
            // 
            grpGearFeatures.Controls.Add(chkNoSway);
            grpGearFeatures.Controls.Add(chkInstantADS);
            grpGearFeatures.Controls.Add(trkMagDrills);
            grpGearFeatures.Controls.Add(chkNoRecoil);
            grpGearFeatures.Controls.Add(chkMagDrills);
            grpGearFeatures.Controls.Add(chkNoVisor);
            grpGearFeatures.Controls.Add(chkNightVision);
            grpGearFeatures.Controls.Add(chkOpticThermalVision);
            grpGearFeatures.Controls.Add(chkThermalVision);
            grpGearFeatures.Location = new Point(6, 110);
            grpGearFeatures.Name = "grpGearFeatures";
            grpGearFeatures.Size = new Size(207, 180);
            grpGearFeatures.TabIndex = 34;
            grpGearFeatures.TabStop = false;
            grpGearFeatures.Text = "Gear Features";
            // 
            // chkNoSway
            // 
            chkNoSway.AutoSize = true;
            chkNoSway.Location = new Point(118, 22);
            chkNoSway.Name = "chkNoSway";
            chkNoSway.Size = new Size(72, 19);
            chkNoSway.TabIndex = 35;
            chkNoSway.Text = "No Sway";
            toolTip.SetToolTip(chkNoSway, "Removes weapon sway");
            chkNoSway.UseVisualStyleBackColor = true;
            chkNoSway.CheckedChanged += chkNoSway_CheckedChanged;
            // 
            // chkInstantADS
            // 
            chkInstantADS.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkInstantADS.AutoSize = true;
            chkInstantADS.Location = new Point(118, 47);
            chkInstantADS.Name = "chkInstantADS";
            chkInstantADS.Size = new Size(87, 19);
            chkInstantADS.TabIndex = 34;
            chkInstantADS.Text = "Instant ADS";
            toolTip.SetToolTip(chkInstantADS, "Increases ADS speed to be near instantaneous");
            chkInstantADS.UseVisualStyleBackColor = true;
            chkInstantADS.CheckedChanged += chkInstantADS_CheckedChanged;
            // 
            // trkMagDrills
            // 
            trkMagDrills.LargeChange = 10;
            trkMagDrills.Location = new Point(85, 147);
            trkMagDrills.Maximum = 7;
            trkMagDrills.Minimum = 1;
            trkMagDrills.Name = "trkMagDrills";
            trkMagDrills.Size = new Size(116, 45);
            trkMagDrills.TabIndex = 33;
            trkMagDrills.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkMagDrills, "Speed of un/packing ammunition from a magazine");
            trkMagDrills.Value = 5;
            trkMagDrills.Visible = false;
            trkMagDrills.Scroll += trkMagDrills_Scroll;
            // 
            // chkNoRecoil
            // 
            chkNoRecoil.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkNoRecoil.AutoSize = true;
            chkNoRecoil.Location = new Point(6, 22);
            chkNoRecoil.Name = "chkNoRecoil";
            chkNoRecoil.Size = new Size(77, 19);
            chkNoRecoil.TabIndex = 25;
            chkNoRecoil.Text = "No Recoil";
            toolTip.SetToolTip(chkNoRecoil, "Removes weapon recoil");
            chkNoRecoil.UseVisualStyleBackColor = true;
            chkNoRecoil.CheckedChanged += chkNoRecoil_CheckedChanged;
            // 
            // chkMagDrills
            // 
            chkMagDrills.AutoSize = true;
            chkMagDrills.Location = new Point(6, 147);
            chkMagDrills.Name = "chkMagDrills";
            chkMagDrills.Size = new Size(79, 19);
            chkMagDrills.TabIndex = 32;
            chkMagDrills.Text = "Mag Drills";
            toolTip.SetToolTip(chkMagDrills, "Increases ammunition un/loading in magazines");
            chkMagDrills.UseVisualStyleBackColor = true;
            chkMagDrills.CheckedChanged += chkMagDrills_CheckedChanged;
            // 
            // chkNoVisor
            // 
            chkNoVisor.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkNoVisor.AutoSize = true;
            chkNoVisor.Location = new Point(6, 122);
            chkNoVisor.Name = "chkNoVisor";
            chkNoVisor.Size = new Size(71, 19);
            chkNoVisor.TabIndex = 21;
            chkNoVisor.Text = "No Visor";
            toolTip.SetToolTip(chkNoVisor, "Removes visual visor effect");
            chkNoVisor.UseVisualStyleBackColor = true;
            chkNoVisor.CheckedChanged += chkNoVisor_CheckedChanged;
            // 
            // chkNightVision
            // 
            chkNightVision.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkNightVision.AutoSize = true;
            chkNightVision.Location = new Point(6, 72);
            chkNightVision.Name = "chkNightVision";
            chkNightVision.Size = new Size(144, 19);
            chkNightVision.TabIndex = 18;
            chkNightVision.Text = "Night Vision (Ctrl + N)";
            toolTip.SetToolTip(chkNightVision, "Enables 'bug eye' night vision");
            chkNightVision.UseVisualStyleBackColor = true;
            chkNightVision.CheckedChanged += chkNightVision_CheckedChanged;
            // 
            // chkOpticThermalVision
            // 
            chkOpticThermalVision.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkOpticThermalVision.AutoSize = true;
            chkOpticThermalVision.Location = new Point(6, 47);
            chkOpticThermalVision.Name = "chkOpticThermalVision";
            chkOpticThermalVision.Size = new Size(101, 19);
            chkOpticThermalVision.TabIndex = 20;
            chkOpticThermalVision.Text = "Optic Thermal";
            toolTip.SetToolTip(chkOpticThermalVision, "Turns optics into thermals");
            chkOpticThermalVision.UseVisualStyleBackColor = true;
            chkOpticThermalVision.CheckedChanged += chkOpticThermalVision_CheckedChanged;
            // 
            // chkThermalVision
            // 
            chkThermalVision.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chkThermalVision.AutoSize = true;
            chkThermalVision.Location = new Point(6, 97);
            chkThermalVision.Name = "chkThermalVision";
            chkThermalVision.Size = new Size(154, 19);
            chkThermalVision.TabIndex = 19;
            chkThermalVision.Text = "Thermal Vision (Ctrl + T)";
            toolTip.SetToolTip(chkThermalVision, "Enables T-7 thermal vision");
            chkThermalVision.UseVisualStyleBackColor = true;
            chkThermalVision.CheckedChanged += chkThermalVision_CheckedChanged;
            // 
            // grpPhysicalFeatures
            // 
            grpPhysicalFeatures.Controls.Add(chkInfiniteStamina);
            grpPhysicalFeatures.Controls.Add(chkIncreaseMaxWeight);
            grpPhysicalFeatures.Controls.Add(trkThrowPower);
            grpPhysicalFeatures.Controls.Add(trkJumpPower);
            grpPhysicalFeatures.Controls.Add(chkThrowPower);
            grpPhysicalFeatures.Controls.Add(chkJumpPower);
            grpPhysicalFeatures.Location = new Point(226, 110);
            grpPhysicalFeatures.Name = "grpPhysicalFeatures";
            grpPhysicalFeatures.Size = new Size(230, 180);
            grpPhysicalFeatures.TabIndex = 26;
            grpPhysicalFeatures.TabStop = false;
            grpPhysicalFeatures.Text = "Physical Features";
            // 
            // chkInfiniteStamina
            // 
            chkInfiniteStamina.AutoSize = true;
            chkInfiniteStamina.Location = new Point(6, 97);
            chkInfiniteStamina.Name = "chkInfiniteStamina";
            chkInfiniteStamina.Size = new Size(109, 19);
            chkInfiniteStamina.TabIndex = 32;
            chkInfiniteStamina.Text = "Infinite Stamina";
            toolTip.SetToolTip(chkInfiniteStamina, "Allows you to run forever");
            chkInfiniteStamina.UseVisualStyleBackColor = true;
            chkInfiniteStamina.CheckedChanged += chkInfiniteStamina_CheckedChanged;
            // 
            // chkIncreaseMaxWeight
            // 
            chkIncreaseMaxWeight.AutoSize = true;
            chkIncreaseMaxWeight.Location = new Point(6, 22);
            chkIncreaseMaxWeight.Name = "chkIncreaseMaxWeight";
            chkIncreaseMaxWeight.Size = new Size(136, 19);
            chkIncreaseMaxWeight.TabIndex = 15;
            chkIncreaseMaxWeight.Text = "Increase Max Weight";
            toolTip.SetToolTip(chkIncreaseMaxWeight, "Increases maximum weight capacity");
            chkIncreaseMaxWeight.UseVisualStyleBackColor = true;
            chkIncreaseMaxWeight.CheckedChanged += chkIncreaseMaxWeight_CheckedChanged;
            // 
            // trkThrowPower
            // 
            trkThrowPower.LargeChange = 10;
            trkThrowPower.Location = new Point(108, 72);
            trkThrowPower.Minimum = 1;
            trkThrowPower.Name = "trkThrowPower";
            trkThrowPower.Size = new Size(116, 45);
            trkThrowPower.TabIndex = 31;
            trkThrowPower.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkThrowPower, "The 'strength' of the throw");
            trkThrowPower.Value = 5;
            trkThrowPower.Visible = false;
            trkThrowPower.Scroll += trkThrowPower_Scroll;
            // 
            // trkJumpPower
            // 
            trkJumpPower.LargeChange = 10;
            trkJumpPower.Location = new Point(108, 47);
            trkJumpPower.Minimum = 1;
            trkJumpPower.Name = "trkJumpPower";
            trkJumpPower.Size = new Size(116, 45);
            trkJumpPower.TabIndex = 30;
            trkJumpPower.TickStyle = TickStyle.None;
            toolTip.SetToolTip(trkJumpPower, "The 'strength' of the jump");
            trkJumpPower.Value = 5;
            trkJumpPower.Visible = false;
            trkJumpPower.Scroll += trkJumpPower_Scroll;
            // 
            // chkThrowPower
            // 
            chkThrowPower.AutoSize = true;
            chkThrowPower.Location = new Point(6, 72);
            chkThrowPower.Name = "chkThrowPower";
            chkThrowPower.Size = new Size(95, 19);
            chkThrowPower.TabIndex = 1;
            chkThrowPower.Text = "Throw Power";
            toolTip.SetToolTip(chkThrowPower, "Increases maximum throw power");
            chkThrowPower.UseVisualStyleBackColor = true;
            chkThrowPower.CheckedChanged += chkThrowPower_CheckedChanged;
            // 
            // chkJumpPower
            // 
            chkJumpPower.AutoSize = true;
            chkJumpPower.Location = new Point(6, 47);
            chkJumpPower.Name = "chkJumpPower";
            chkJumpPower.Size = new Size(91, 19);
            chkJumpPower.TabIndex = 0;
            chkJumpPower.Text = "Jump Power";
            toolTip.SetToolTip(chkJumpPower, "Increases maximum jump power");
            chkJumpPower.UseVisualStyleBackColor = true;
            chkJumpPower.CheckedChanged += chkJumpPower_CheckedChanged;
            // 
            // grpRadar
            // 
            grpRadar.Controls.Add(btnRestartRadar);
            grpRadar.Controls.Add(chkShowMapSetup);
            grpRadar.Controls.Add(btnToggleMap);
            grpRadar.Location = new Point(5, 22);
            grpRadar.Name = "grpRadar";
            grpRadar.Size = new Size(463, 99);
            grpRadar.TabIndex = 26;
            grpRadar.TabStop = false;
            grpRadar.Text = "Radar";
            // 
            // btnRestartRadar
            // 
            btnRestartRadar.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            btnRestartRadar.Location = new Point(350, 22);
            btnRestartRadar.Name = "btnRestartRadar";
            btnRestartRadar.Size = new Size(107, 27);
            btnRestartRadar.TabIndex = 18;
            btnRestartRadar.Text = "Restart Radar";
            toolTip.SetToolTip(btnRestartRadar, "Manually triggers radar restart");
            btnRestartRadar.UseVisualStyleBackColor = true;
            btnRestartRadar.Click += btnRestartRadar_Click;
            // 
            // chkShowMapSetup
            // 
            chkShowMapSetup.AutoSize = true;
            chkShowMapSetup.Location = new Point(6, 28);
            chkShowMapSetup.Name = "chkShowMapSetup";
            chkShowMapSetup.Size = new Size(153, 19);
            chkShowMapSetup.TabIndex = 9;
            chkShowMapSetup.Text = "Show Map Setup Helper";
            toolTip.SetToolTip(chkShowMapSetup, "Shows the 'Map Setup' panel");
            chkShowMapSetup.UseVisualStyleBackColor = true;
            chkShowMapSetup.CheckedChanged += chkShowMapSetup_CheckedChanged;
            // 
            // btnToggleMap
            // 
            btnToggleMap.Location = new Point(236, 22);
            btnToggleMap.Margin = new Padding(4, 3, 4, 3);
            btnToggleMap.Name = "btnToggleMap";
            btnToggleMap.Size = new Size(107, 27);
            btnToggleMap.TabIndex = 7;
            btnToggleMap.Text = "Toggle Map (F5)";
            toolTip.SetToolTip(btnToggleMap, "Manually toggles active map");
            btnToggleMap.UseVisualStyleBackColor = true;
            btnToggleMap.Click += btnToggleMap_Click;
            // 
            // tabRadar
            // 
            tabRadar.Controls.Add(grpMapSetup);
            tabRadar.Location = new Point(4, 24);
            tabRadar.Name = "tabRadar";
            tabRadar.Padding = new Padding(3);
            tabRadar.Size = new Size(1168, 638);
            tabRadar.TabIndex = 0;
            tabRadar.Text = "Radar";
            tabRadar.UseVisualStyleBackColor = true;
            // 
            // grpMapSetup
            // 
            grpMapSetup.Controls.Add(chkMapFree);
            grpMapSetup.Controls.Add(txtMapSetupScale);
            grpMapSetup.Controls.Add(lblMapScale);
            grpMapSetup.Controls.Add(txtMapSetupY);
            grpMapSetup.Controls.Add(lblMapXY);
            grpMapSetup.Controls.Add(txtMapSetupX);
            grpMapSetup.Controls.Add(lblMapCoords);
            grpMapSetup.Location = new Point(8, 11);
            grpMapSetup.Name = "grpMapSetup";
            grpMapSetup.Size = new Size(327, 175);
            grpMapSetup.TabIndex = 11;
            grpMapSetup.TabStop = false;
            grpMapSetup.Text = "Map Setup";
            grpMapSetup.Visible = false;
            // 
            // chkMapFree
            // 
            chkMapFree.Appearance = Appearance.Button;
            chkMapFree.AutoSize = true;
            chkMapFree.Location = new Point(0, 0);
            chkMapFree.Name = "chkMapFree";
            chkMapFree.Size = new Size(66, 25);
            chkMapFree.TabIndex = 17;
            chkMapFree.Text = "Map Free";
            chkMapFree.UseVisualStyleBackColor = true;
            chkMapFree.CheckedChanged += chkMapFree_CheckedChanged;
            // 
            // txtMapSetupScale
            // 
            txtMapSetupScale.Location = new Point(46, 101);
            txtMapSetupScale.Name = "txtMapSetupScale";
            txtMapSetupScale.Size = new Size(50, 23);
            txtMapSetupScale.TabIndex = 15;
            // 
            // lblMapScale
            // 
            lblMapScale.AutoSize = true;
            lblMapScale.Location = new Point(6, 104);
            lblMapScale.Name = "lblMapScale";
            lblMapScale.Size = new Size(34, 15);
            lblMapScale.TabIndex = 14;
            lblMapScale.Text = "Scale";
            // 
            // txtMapSetupY
            // 
            txtMapSetupY.Location = new Point(102, 67);
            txtMapSetupY.Name = "txtMapSetupY";
            txtMapSetupY.Size = new Size(50, 23);
            txtMapSetupY.TabIndex = 13;
            // 
            // lblMapXY
            // 
            lblMapXY.AutoSize = true;
            lblMapXY.Location = new Point(6, 70);
            lblMapXY.Name = "lblMapXY";
            lblMapXY.Size = new Size(24, 15);
            lblMapXY.TabIndex = 12;
            lblMapXY.Text = "X,Y";
            // 
            // txtMapSetupX
            // 
            txtMapSetupX.Location = new Point(46, 67);
            txtMapSetupX.Name = "txtMapSetupX";
            txtMapSetupX.Size = new Size(50, 23);
            txtMapSetupX.TabIndex = 11;
            // 
            // lblMapCoords
            // 
            lblMapCoords.AutoSize = true;
            lblMapCoords.Location = new Point(7, 19);
            lblMapCoords.Margin = new Padding(4, 0, 4, 0);
            lblMapCoords.Name = "lblMapCoords";
            lblMapCoords.Size = new Size(43, 15);
            lblMapCoords.TabIndex = 10;
            lblMapCoords.Text = "coords";
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabRadar);
            tabControl.Controls.Add(tabSettings);
            tabControl.Controls.Add(tabPlayerLoadouts);
            tabControl.Controls.Add(tabPlayerHistory);
            tabControl.Controls.Add(tabLootFilter);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1176, 666);
            tabControl.TabIndex = 8;
            // 
            // colDialog
            // 
            colDialog.FullOpen = true;
            // 
            // chkAutoLootRefresh
            // 
            chkAutoLootRefresh.AutoSize = true;
            chkAutoLootRefresh.Location = new Point(6, 200);
            chkAutoLootRefresh.Name = "chkAutoLootRefresh";
            chkAutoLootRefresh.Size = new Size(121, 19);
            chkAutoLootRefresh.TabIndex = 35;
            chkAutoLootRefresh.Text = "Auto Loot Refresh";
            chkAutoLootRefresh.UseVisualStyleBackColor = true;
            chkAutoLootRefresh.CheckedChanged += chkAutoLootRefresh_CheckedChanged;
            // 
            // frmMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1176, 666);
            Controls.Add(tabControl);
            Margin = new Padding(4, 3, 4, 3);
            Name = "frmMain";
            Text = "EFT Radar";
            tabLootFilter.ResumeLayout(false);
            tabLootFilter.PerformLayout();
            tabPlayerHistory.ResumeLayout(false);
            tabPlayerLoadouts.ResumeLayout(false);
            tabSettings.ResumeLayout(false);
            grpConfig.ResumeLayout(false);
            grpColors.ResumeLayout(false);
            grpColors.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picDeathMarkerColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picTextOutlineColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilClosedIconColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilPendingIconColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilActiveIconColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilClosedTextColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilPendingTextColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picExfilActiveTextColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picQuestZonesColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picQuestItemsColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picImportantLootColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picUSECColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picRegularLootColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picTeamHoverColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picTeammateColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picLocalPlayerColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picBEARColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picBossColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picAIRaiderColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picPScavColor).EndInit();
            ((System.ComponentModel.ISupportInitialize)picAIScavColor).EndInit();
            grpLootFilters.ResumeLayout(false);
            grpLootFilters.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picLootFilterEditColor).EndInit();
            grpLoot.ResumeLayout(false);
            grpLoot.PerformLayout();
            grpLootValues.ResumeLayout(false);
            grpLootValues.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trkSubItemLootValue).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkCorpseLootValue).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkImportantLootValue).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkRegularLootValue).EndInit();
            grpUserInterface.ResumeLayout(false);
            grpUserInterface.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trkAimLength).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkZoom).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkUIScale).EndInit();
            grpMemoryWriting.ResumeLayout(false);
            grpMemoryWriting.PerformLayout();
            grpGlobalFeatures.ResumeLayout(false);
            grpGlobalFeatures.PerformLayout();
            grpGearFeatures.ResumeLayout(false);
            grpGearFeatures.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trkMagDrills).EndInit();
            grpPhysicalFeatures.ResumeLayout(false);
            grpPhysicalFeatures.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)trkThrowPower).EndInit();
            ((System.ComponentModel.ISupportInitialize)trkJumpPower).EndInit();
            grpRadar.ResumeLayout(false);
            grpRadar.PerformLayout();
            tabRadar.ResumeLayout(false);
            grpMapSetup.ResumeLayout(false);
            grpMapSetup.PerformLayout();
            tabControl.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TabPage tabLootFilter;
        private CheckBox chkLootFilterActive;
        private Label lblActiveFilter;
        private ComboBox cboFilters;
        private Button btnLootFilterRemoveItem;
        private Button btnLootFilterAddItem;
        private TextBox txtItemFilter;
        private ComboBox cboLootItems;
        private ListView lstViewLootFilter;
        private ColumnHeader colHeadBSGId;
        private ColumnHeader colHeadFullName;
        private ColumnHeader colHeadShortName;
        private ColumnHeader colHeadValue;
        private TabPage tabPlayerHistory;
        private ListView lstViewPMCHistory;
        private ColumnHeader columnHeader_Entry;
        private ColumnHeader columnHeader_ID;
        private TabPage tabPlayerLoadouts;
        private RichTextBox rchTxtPlayerInfo;
        private TabPage tabSettings;
        private GroupBox grpConfig;
        private GroupBox grpLoot;
        private TrackBar trkRegularLootValue;
        private Button btnRefreshLoot;
        private CheckBox chkHideLootValue;
        private CheckBox chkImportantLootOnly;
        private Label lblRegularLoot;
        private GroupBox grpUserInterface;
        private CheckBox chkShowLoot;
        private TrackBar trkAimLength;
        private Label lblAimline;
        private TextBox txtTeammateID;
        private Label lblPrimaryTeammate;
        private TrackBar trkZoom;
        private Label lblUIScale;
        private Label lblZoom;
        private TrackBar trkUIScale;
        private CheckBox chkShowAimview;
        private CheckBox chkHideNames;
        private GroupBox grpMemoryWriting;
        private CheckBox chkNoVisor;
        private CheckBox chkOpticThermalVision;
        private CheckBox chkNightVision;
        private CheckBox chkThermalVision;
        private GroupBox grpRadar;
        private Button btnRestartRadar;
        private CheckBox chkShowMapSetup;
        private Button btnToggleMap;
        private TabPage tabRadar;
        private GroupBox grpMapSetup;
        private CheckBox chkMapFree;
        private TextBox txtMapSetupScale;
        private Label lblMapScale;
        private TextBox txtMapSetupY;
        private Label lblMapXY;
        private TextBox txtMapSetupX;
        private Label lblMapCoords;
        private TabControl tabControl;
        private GroupBox grpLootValues;
        private Label lblImportantLootSlider;
        private TrackBar trkImportantLootValue;
        private Label lblRegularLootDisplay;
        private Label lblImportantLootDisplay;
        private GroupBox grpLootFilters;
        private TextBox txtLootFilterEditName;
        private Label lblFilterEditName;
        private Button btnFilterPriorityDown;
        private Button btnFilterPriorityUp;
        private Label lblLootFilterColor;
        private ColorDialog colDialog;
        private PictureBox picLootFilterEditColor;
        private Button btnAddNewFilter;
        private Button btnRemoveFilter;
        private CheckBox chkLootFilterEditActive;
        private Button btnEditSaveFilter;
        private Button btnCancelEditFilter;
        private ListBox lstEditLootFilters;
        private GroupBox grpColors;
        private PictureBox picAIScavColor;
        private Label lblAIScavColor;
        private Label lblBEARColor;
        private Label lblUSECColor;
        private Label lblBossColor;
        private Label lblAIRaiderColor;
        private Label lblPScavColor;
        private PictureBox picUSECColor;
        private PictureBox picBEARColor;
        private PictureBox picBossColor;
        private PictureBox picAIRaiderColor;
        private PictureBox picPScavColor;
        private PictureBox picTeamHoverColor;
        private Label lblTeamHoverColor;
        private PictureBox picTeammateColor;
        private Label lblTeammateColor;
        private PictureBox picLocalPlayerColor;
        private Label lblLocalPlayerColor;
        private PictureBox picRegularLootColor;
        private Label lblRegularLootColor;
        private PictureBox picImportantLootColor;
        private Label lblImportantLootColor;
        private CheckBox chkChams;
        private CheckBox chkNoRecoil;
        private CheckBox chkShowHoverArmor;
        private GroupBox grpPhysicalFeatures;
        private CheckBox chkThrowPower;
        private CheckBox chkJumpPower;
        private CheckBox chkDoubleSearch;
        private TrackBar trkJumpPower;
        private TrackBar trkThrowPower;
        private CheckBox chkMagDrills;
        private TrackBar trkMagDrills;
        private GroupBox grpGearFeatures;
        private GroupBox grpGlobalFeatures;
        private CheckBox chkIncreaseMaxWeight;
        private CheckBox chkQuestHelper;
        private PictureBox picQuestItemsColor;
        private Label lblQuestItemsColor;
        private PictureBox picQuestZonesColor;
        private Label lblQuestZonesColor;
        private ToolTip toolTip;
        private CheckBox chkHideExfilNames;
        private PictureBox picExfilClosedTextColor;
        private Label lblExfilClosedTextColor;
        private PictureBox picExfilPendingTextColor;
        private Label lblExfilPendingTextColor;
        private PictureBox picExfilActiveTextColor;
        private Label lblExfilOpenTextColor;
        private PictureBox picExfilActiveIconColor;
        private Label lblExfilActiveIconColor;
        private PictureBox picExfilPendingIconColor;
        private Label lblExfilPendingIconColor;
        private PictureBox picExfilClosedIconColor;
        private Label lblExfilClosedIconColor;
        private CheckBox chkInstantADS;
        private PictureBox picTextOutlineColor;
        private Label lblTextOutlineColor;
        private CheckBox chkHideTextOutline;
        private CheckBox chkNoSway;
        private CheckBox chkMasterSwitch;
        private CheckBox chkInfiniteStamina;
        private CheckBox chkExtendedReach;
        private PictureBox picDeathMarkerColor;
        private Label lblDeathMarkerColor;
        private CheckBox chkShowCorpses;
        private Label lblCorpseDisplay;
        private Label lblCorpsValueSlider;
        private TrackBar trkCorpseLootValue;
        private Label lblSubItemDisplay;
        private Label lblSubItemValueSlider;
        private TrackBar trkSubItemLootValue;
        private CheckBox chkShowSubItems;
        private CheckBox chkAutoLootRefresh;
    }
}

