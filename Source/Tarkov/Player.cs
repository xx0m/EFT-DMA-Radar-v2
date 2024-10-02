using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    /// <summary>
    /// Class containing Game Player Data.
    /// </summary>
    public class Player
    {
        private static Dictionary<string, int> _groups = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stopwatch _posRefreshSw = new();
        private readonly object _posLock = new(); // sync access to this.Position (non-atomic)
        private GearManager _gearManager;
        private Transform _transform;

        #region PlayerProperties
        /// <summary>
        /// Player is a PMC Operator.
        /// </summary>
        public bool IsPMC { get; set; }
        /// <summary>
        /// Player is a Local PMC Operator.
        /// </summary>
        public bool IsLocalPlayer { get; set; }
        /// <summary>
        /// Player is Alive/Not Dead.
        /// </summary>
        public volatile bool IsAlive = true;
        /// <summary>
        /// Player is Active (has not exfil'd).
        /// </summary>
        public volatile bool IsActive = true;
        /// <summary>
        /// Account UUID for Human Controlled Players.
        /// </summary>
        public string AccountID { get; set; }
        public string ProfileID { get; set; }
        /// <summary>
        /// Player name.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Player Level (Based on experience).
        /// </summary>
        public int Lvl { get; } = 0;
        /// <summary>
        /// Player's Kill/Death Average
        /// </summary>
        public float KDA { get; private set; } = -1f;
        /// <summary>
        /// Group that the player belongs to.
        /// </summary>
        public int GroupID { get; set; } = -1;
        /// <summary>
        /// Type of player unit.
        /// </summary>
        public PlayerType Type { get; set; }
        /// <summary>
        /// Player's current health (sum of all 7 body parts).
        /// </summary>
        public int Health { get; private set; } = -1;

        public ulong HealthController { get; set; }

        public ulong InventoryController { get; set; }

        public ulong InventorySlots { get; set; }

        public ulong PlayerBody { get; set; }

        private Vector3 _pos = new Vector3(0, 0, 0); // backing field

        /// <summary>
        /// Player's Unity Position in Local Game World.
        /// </summary>
        public Vector3 Position // 96 bits, cannot set atomically
        {
            get
            {
                lock (_posLock)
                    return _pos;
            }
            private set
            {
                lock (_posLock)
                    _pos = value;
            }
        }
        /// <summary>
        /// Cached 'Zoomed Position' on the Radar GUI. Used for mouseover events.
        /// </summary>
        public Vector2 ZoomedPosition { get; set; } = new();
        /// <summary>
        /// Player's Rotation (direction/pitch) in Local Game World.
        /// 90 degree offset ~already~ applied to account for 2D-Map orientation.
        /// </summary>
        public Vector2 Rotation { get; private set; } = new Vector2(0, 0); // 64 bits will be atomic
        /// <summary>
        /// Key = Slot Name, Value = Item 'Long Name' in Slot
        /// </summary>
        public List<GearManager.Gear> Gear
        {
            get => this._gearManager is not null ? this._gearManager.GearItems : null;
            set
            {
                this._gearManager.GearItems = value;
            }
        }

        public GearManager GearManager => this._gearManager;
        /// <summary>
        /// If 'true', Player object is no longer in the RegisteredPlayers list.
        /// Will be checked if dead/exfil'd on next loop.
        /// </summary>
        public bool LastUpdate { get; set; } = false;
        /// <summary>
        /// Consecutive number of errors that this Player object has 'errored out' while updating.
        /// </summary>
        public int ErrorCount { get; set; } = 0;
        public bool isOfflinePlayer { get; set; } = false;
        public int PlayerSide { get; set; }
        public int PlayerRole { get; set; }
        public bool HasRequiredGear { get; set; } = false;

        public List<ulong> BonePointers { get; } = new List<ulong>();
        public List<Vector3> BonePositions { get; } = new List<Vector3>();
        public List<Transform> BoneTransforms { get; } = new List<Transform>();
        #endregion

        #region Getters
        public List<PlayerBones> RequiredBones { get; } = new List<PlayerBones>
        {
            PlayerBones.HumanPelvis, PlayerBones.HumanHead, PlayerBones.HumanLForearm2,
            PlayerBones.HumanLPalm, PlayerBones.HumanRForearm2, PlayerBones.HumanRPalm,
            PlayerBones.HumanLThigh2, PlayerBones.HumanLFoot,
            PlayerBones.HumanRThigh2, PlayerBones.HumanRFoot
        };

        /// <summary>
        /// Contains 'Acct UUIDs' of tracked players for the Key, and the 'Reason' for the Value.
        /// </summary>
        private static Watchlist _watchlistManager
        {
            get => Program.Config.Watchlist;
        }
        /// <summary>
        /// Player is human-controlled.
        /// </summary>
        public bool IsHuman
        {
            get => (
                this.Type is PlayerType.LocalPlayer ||
                this.Type is PlayerType.Teammate ||
                this.Type is PlayerType.PMC ||
                this.Type is PlayerType.SpecialPlayer ||
                this.Type is PlayerType.PlayerScav ||
                this.Type is PlayerType.BEAR ||
                this.Type is PlayerType.USEC);
        }
        /// <summary>
        /// Player is human-controlled and Active/Alive.
        /// </summary>
        public bool IsHumanActive
        {
            get => (
                this.Type is PlayerType.LocalPlayer ||
                this.Type is PlayerType.Teammate ||
                this.Type is PlayerType.PMC ||
                this.Type is PlayerType.SpecialPlayer ||
                this.Type is PlayerType.PlayerScav ||
                this.Type is PlayerType.BEAR ||
                this.Type is PlayerType.USEC) && IsActive && IsAlive;
        }
        /// <summary>
        /// Player is human-controlled & Hostile.
        /// </summary>
        public bool IsHumanHostile
        {
            get => (
                this.Type is PlayerType.PMC ||
                this.Type is PlayerType.SpecialPlayer ||
                this.Type is PlayerType.PlayerScav ||
                this.Type is PlayerType.BEAR ||
                this.Type is PlayerType.USEC);
        }
        /// <summary>
        /// Player is human-controlled, hostile, and Active/Alive.
        /// </summary>
        public bool IsHumanHostileActive
        {
            get => (
                this.Type is PlayerType.BEAR ||
                this.Type is PlayerType.USEC ||
                this.Type is PlayerType.SpecialPlayer ||
                this.Type is PlayerType.PlayerScav) && this.IsActive && this.IsAlive;
        }
        /// <summary>
        /// Player is AI & boss, rogue, raider etc.
        /// </summary>
        public bool IsBossRaider
        {
            get => (
                this.Type is PlayerType.Raider ||
                this.Type is PlayerType.BossFollower ||
                this.Type is PlayerType.BossGuard ||
                this.Type is PlayerType.Rogue ||
                this.Type is PlayerType.Cultist ||
                this.Type is PlayerType.Boss);
        }

        /// <summary>
        /// Player is rogue, raider etc.
        /// </summary>
        public bool IsRogueRaider
        {
            get => (
                this.Type is PlayerType.Raider ||
                this.Type is PlayerType.BossFollower ||
                this.Type is PlayerType.BossGuard ||
                this.Type is PlayerType.Rogue ||
                this.Type is PlayerType.Cultist);
        }
        /// <summary>
        /// Player is AI/human-controlled and Active/Alive.
        /// </summary>
        public bool IsHostileActive
        {
            get => (
                this.Type is PlayerType.PMC ||
                this.Type is PlayerType.BEAR ||
                this.Type is PlayerType.USEC ||
                this.Type is PlayerType.SpecialPlayer ||
                this.Type is PlayerType.PlayerScav ||
                this.Type is PlayerType.Scav ||
                this.Type is PlayerType.Raider ||
                this.Type is PlayerType.BossFollower ||
                this.Type is PlayerType.BossGuard ||
                this.Type is PlayerType.Rogue ||
                this.Type is PlayerType.OfflineScav ||
                this.Type is PlayerType.Cultist ||
                this.Type is PlayerType.Boss) && this.IsActive && this.IsAlive;
        }
        /// <summary>
        /// Player is friendly to LocalPlayer (including LocalPlayer) and Active/Alive.
        /// </summary>
        public bool IsFriendlyActive
        {
            get => ((
                this.Type is PlayerType.LocalPlayer ||
                this.Type is PlayerType.Teammate) && this.IsActive && this.IsAlive);
        }
        /// <summary>
        /// Player has exfil'd/left the raid.
        /// </summary>
        public bool HasExfild
        {
            get => !this.IsActive && this.IsAlive;
        }
        /// <summary>
        /// Gets value of player.
        /// </summary>
        /// 
        public int Value
        {
            get => this._gearManager is not null ? this._gearManager.Value : 0;
        }
        /// <summary>
        /// EFT.Player Address
        /// </summary>
        public ulong Base { get; }
        /// <summary>
        /// EFT.Profile Address
        /// </summary>
        public ulong Profile { get; }
        /// <summary>
        /// PlayerInfo Address (GClass1044)
        /// </summary>
        public ulong Info { get; set; }
        public ulong TransformInternal { get; set; }
        public ulong VerticesAddr { get => this._transform?.VerticesAddr ?? 0x0; }
        public ulong IndicesAddr
        {
            get => this._transform?.IndicesAddr ?? 0x0;
        }
        /// <summary>
        /// Health Entries for each Body Part.
        /// </summary>
        public ulong[] HealthEntries { get; set; }
        public ulong MovementContext { get; set; }
        public ulong CorpsePtr
        {
            get => this.Base + Offsets.Player.Corpse;
        }
        /// <summary>
        /// IndicesAddress -> IndicesSize -> VerticesAddress -> VerticesSize
        /// </summary>
        public Tuple<ulong, int, ulong, int> TransformScatterReadParameters
        {
            get => this._transform?.GetScatterReadParameters() ?? new Tuple<ulong, int, ulong, int>(0, 0, 0, 0);
        }

        public int MarkedDeadCount { get; set; } = 0;
        public string Tag { get; set; } = string.Empty;

        public string HealthStatus => this.Health switch
        {
            100 => "Healthy",
            >= 50 => "Moderate",
            >= 10 => "Poor",
            >= 0 => "Critical",
            _ => "N/A"
        };

        public bool HasThermal => _gearManager.HasThermal;
        public bool HasNVG => _gearManager.HasNVG;

        public GearManager.Gear ItemInHands { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Player Constructor.
        /// </summary>
        public Player(ulong playerBase, ulong playerProfile, string profileID, Vector3? pos = null, string baseClassName = null)
        {
            if (string.IsNullOrEmpty(baseClassName))
                throw new Exception("BaseClass is not set!");

            var isOfflinePlayer = string.Equals(baseClassName, "ClientPlayer") || string.Equals(baseClassName, "LocalPlayer") || string.Equals(baseClassName, "HideoutPlayer");
            var isOnlinePlayer = string.Equals(baseClassName, "ObservedPlayerView");

            if (!isOfflinePlayer && !isOnlinePlayer)
                throw new Exception("Player is not of type OfflinePlayer or OnlinePlayer");

            Debug.WriteLine("Player Constructor: Initialization started.");

            this.Base = playerBase;
            this.Profile = playerProfile;
            this.ProfileID = profileID;

            if (pos is not null)
                this.Position = (Vector3)pos;

            var scatterReadMap = new ScatterReadMap(1);

            if (isOfflinePlayer)
            {
                this.SetupOfflineScatterReads(scatterReadMap);
                this.ProcessOfflinePlayerScatterReadResults(scatterReadMap);
            }
            else if (isOnlinePlayer)
            {
                this.Info = playerBase;
                this.SetupOnlineScatterReads(scatterReadMap);
                this.ProcessOnlinePlayerScatterReadResults(scatterReadMap);
            }
        }
        #endregion

        #region Setters
        /// <summary>
        /// Set player health.
        /// </summary>
        public bool SetHealth(int eTagStatus)
        {
            try
            {
                this.Health = eTagStatus switch
                {
                    1024 => 100,
                    2048 => 50,
                    4096 => 10,
                    8192 => 0,
                    _ => -1,
                };
                return true;
            }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting Player '{this.Name}' Health: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Set player rotation (Direction/Pitch)
        /// </summary>
        public bool SetRotation(object obj)
        {
            try
            {
                if (obj is not Vector2 rotation)
                    throw new ArgumentException("Rotation data must be of type Vector2.", nameof(obj));

                rotation.X = (rotation.X - 90 + 360) % 360;
                rotation.Y = (rotation.Y) % 360;

                this.Rotation = rotation;
                return true;
            }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting Player '{this.Name}' Rotation: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Set player position (Vector3 X,Y,Z)
        /// </summary>
        public bool SetPosition(object[] obj)
        {
            try
            {
                if (obj is null)
                    throw new NullReferenceException();

                this.Position = this._transform.GetPosition(obj);

                return true;
            }
            catch (Exception ex) // Attempt to re-allocate Transform on error
            {
                Program.Log($"ERROR getting Player '{this.Name}' Position: {ex}");

                if (!this._posRefreshSw.IsRunning)
                {
                    this._posRefreshSw.Start();
                }
                else if (this._posRefreshSw.ElapsedMilliseconds < 250)
                {
                    return false;
                }

                try
                {
                    Program.Log($"Attempting to get new Transform for Player '{this.Name}'...");
                    var transform = new Transform(this.TransformInternal, true);
                    this._transform = transform;
                    Program.Log($"Player '{this.Name}' obtained new Position Transform OK.");
                }
                catch (Exception ex2)
                {
                    Program.Log($"ERROR getting new Transform for Player '{this.Name}': {ex2}");
                }
                finally
                {
                    this._posRefreshSw.Restart();
                }

                return false;
            }
        }

        public void SetItemInHands(ulong pointer)
        {
            this.ItemInHands = this.GearManager.GearItems.FirstOrDefault(x => x.Pointer == pointer);
        }

        public void CheckForRequiredGear()
        {
            var found = false;
            var loot = Memory.Loot;
            var requiredQuestItems = QuestManager.RequiredItems;

            foreach (var gearItem in this.Gear)
            {
                var parentItem = gearItem.Item.ID;

                if (requiredQuestItems.Contains(parentItem) ||
                    gearItem.Item.Loot.Any(x => requiredQuestItems.Contains(x.ID)) ||
                    (loot is not null && loot.RequiredFilterItems is not null && (loot.RequiredFilterItems.ContainsKey(parentItem) ||
                                      gearItem.Item.Loot.Any(x => loot.RequiredFilterItems.ContainsKey(x.ID)))))
                {
                    found = true;
                    break;
                }
            }

            this.HasRequiredGear = found;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns PlayerType based on isAI & playuerSide
        /// </summary>
        private PlayerType GetOnlinePlayerType(bool isAI)
        {
            if (!isAI)
            {
                return this.PlayerSide switch
                {
                    1 => PlayerType.USEC,
                    2 => PlayerType.BEAR,
                    _ => PlayerType.PlayerScav,
                };
            }
            else
            {
                if (this.Name.Contains("(BTR)"))
                {
                    return PlayerType.Boss;
                }
                else
                {
                    var inFaction = Program.AIFactionManager.IsInFaction(this.Name, out var playerType);

                    if (!inFaction && Memory.IsPvEMode)
                    {
                        var dogtagSlot = this.Gear.FirstOrDefault(x => x.Slot.Key == "Dogtag");
                        
                        if (dogtagSlot.Item is not null)
                            playerType = (dogtagSlot.Item.Short == "BEAR" ? PlayerType.BEAR : PlayerType.USEC);
                    }

                    return playerType;
                }
            }
        }

        private PlayerType GetOfflinePlayerType(bool isAI)
        {
            if (!isAI)
            {
                return PlayerType.LocalPlayer;
            }
            else
            {
                if (this.Name.Contains("(BTR)"))
                {
                    return PlayerType.Boss;
                }
                else if (this.PlayerRole == 51 || this.PlayerRole == 52)
                {
                    return (this.PlayerRole == 51 ? PlayerType.BEAR : PlayerType.USEC);
                }
                else
                {
                    Program.AIFactionManager.IsInFaction(this.Name, out var playerType);

                    return playerType;
                }
            }
        }

        private void SetupOfflineScatterReads(ScatterReadMap scatterReadMap)
        {
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();
            var round6 = scatterReadMap.AddRound();

            var transIntPtr1 = round1.AddEntry<ulong>(0, 0, this.Base, null, Offsets.Player.To_TransformInternal[0]);
            var info = round1.AddEntry<ulong>(0, 1, this.Profile, null, Offsets.Profile.PlayerInfo);
            var inventoryController = round1.AddEntry<ulong>(0, 2, this.Base, null, Offsets.Player.InventoryController);
            var playerBody = round1.AddEntry<ulong>(0, 3, this.Base, null, Offsets.Player.PlayerBody);
            var movementContext = round1.AddEntry<ulong>(0, 4, this.Base, null, Offsets.Player.MovementContext);

            var transIntPtr2 = round2.AddEntry<ulong>(0, 5, transIntPtr1, null, Offsets.Player.To_TransformInternal[1]);
            var name = round2.AddEntry<ulong>(0, 6, info, null, Offsets.PlayerInfo.Nickname);
            var inventory = round2.AddEntry<ulong>(0, 7, inventoryController, null, Offsets.InventoryController.Inventory);
            var registrationDate = round2.AddEntry<int>(0, 8, info, null, Offsets.PlayerInfo.RegistrationDate);
            var groupID = round2.AddEntry<ulong>(0, 9, info, null, Offsets.PlayerInfo.GroupId);
            var botSettings = round2.AddEntry<ulong>(0, 10, info, null, Offsets.PlayerInfo.Settings);

            var transIntPtr3 = round3.AddEntry<ulong>(0, 11, transIntPtr2, null, Offsets.Player.To_TransformInternal[2]);
            var equipment = round3.AddEntry<ulong>(0, 12, inventory, null, Offsets.Inventory.Equipment);
            var role = round3.AddEntry<int>(0, 13, botSettings, null, Offsets.PlayerSettings.Role);

            var transIntPtr4 = round4.AddEntry<ulong>(0, 14, transIntPtr3, null, Offsets.Player.To_TransformInternal[3]);
            var inventorySlots = round4.AddEntry<ulong>(0, 15, equipment, null, Offsets.Equipment.Slots);

            var transIntPtr5 = round5.AddEntry<ulong>(0, 16, transIntPtr4, null, Offsets.Player.To_TransformInternal[4]);

            var transformInternal = round6.AddEntry<ulong>(0, 17, transIntPtr5, null, Offsets.Player.To_TransformInternal[5]);

            scatterReadMap.Execute();
        }

        private void ProcessOfflinePlayerScatterReadResults(ScatterReadMap scatterReadMap)
        {
            if (!scatterReadMap.Results[0][1].TryGetResult<ulong>(out var info))
                return;
            if (!scatterReadMap.Results[0][4].TryGetResult<ulong>(out var movementContext))
                return;
            if (!scatterReadMap.Results[0][2].TryGetResult<ulong>(out var inventoryController))
                return;
            if (!scatterReadMap.Results[0][3].TryGetResult<ulong>(out var playerBody))
                return;
            if (!scatterReadMap.Results[0][6].TryGetResult<ulong>(out var name))
                return;
            if (!scatterReadMap.Results[0][15].TryGetResult<ulong>(out var inventorySlots))
                return;
            if (!scatterReadMap.Results[0][17].TryGetResult<ulong>(out var transformInternal))
                return;
            if (!scatterReadMap.Results[0][9].TryGetResult<ulong>(out var groupID))
                return;
            if (!scatterReadMap.Results[0][13].TryGetResult<int>(out var role))
                return;

            this.Info = info;
            this.PlayerRole = role;
            this.InitializePlayerProperties(movementContext, inventoryController, inventorySlots, transformInternal, playerBody, name, groupID);

            if (scatterReadMap.Results[0][8].TryGetResult<int>(out var registrationDate))
            {
                var isAI = registrationDate == 0;

                this.IsLocalPlayer = !isAI;
                this.isOfflinePlayer = true;
                this.Type = this.GetOfflinePlayerType(isAI);
                this.IsPMC = (this.Type == PlayerType.BEAR || this.Type == PlayerType.USEC || !isAI);

                this.FinishAlloc();
            }
        }

        private void SetupOnlineScatterReads(ScatterReadMap scatterReadMap)
        {
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();
            var round6 = scatterReadMap.AddRound();

            var movementContextPtr1 = round1.AddEntry<ulong>(0, 0, this.Info, null, Offsets.ObservedPlayerView.To_MovementContext[0]);
            var transIntPtr1 = round1.AddEntry<ulong>(0, 1, this.Info, null, Offsets.ObservedPlayerView.To_TransformInternal[0]);
            var inventoryControllerPtr1 = round1.AddEntry<ulong>(0, 2, this.Info, null, Offsets.ObservedPlayerView.To_InventoryController[0]);
            var healthControllerPtr1 = round1.AddEntry<ulong>(0, 3, this.Info, null, Offsets.ObservedPlayerView.To_HealthController[0]);
            var name = round1.AddEntry<ulong>(0, 4, this.Info, null, Offsets.ObservedPlayerView.NickName);
            var accountID = round1.AddEntry<ulong>(0, 5, this.Info, null, Offsets.ObservedPlayerView.AccountID);
            var playerSide = round1.AddEntry<int>(0, 6, this.Info, null, Offsets.ObservedPlayerView.PlayerSide);
            var groupID = round1.AddEntry<ulong>(0, 7, this.Info, null, Offsets.ObservedPlayerView.GroupID);
            var playerBody = round1.AddEntry<ulong>(0, 8, this.Info, null, Offsets.ObservedPlayerView.PlayerBody);
            var memberCategory = round1.AddEntry<int>(0, 9, this.Info, null, Offsets.PlayerInfo.MemberCategory);

            var movementContextPtr2 = round2.AddEntry<ulong>(0, 10, movementContextPtr1, null, Offsets.ObservedPlayerView.To_MovementContext[1]);
            var transIntPtr2 = round2.AddEntry<ulong>(0, 11, transIntPtr1, null, Offsets.ObservedPlayerView.To_TransformInternal[1]);
            var inventoryController = round2.AddEntry<ulong>(0, 12, inventoryControllerPtr1, null, Offsets.ObservedPlayerView.To_InventoryController[1]);
            var healthController = round2.AddEntry<ulong>(0, 13, healthControllerPtr1, null, Offsets.ObservedPlayerView.To_HealthController[1]);

            var movementContext = round3.AddEntry<ulong>(0, 14, movementContextPtr2, null, Offsets.ObservedPlayerView.To_MovementContext[2]);
            var transIntPtr3 = round3.AddEntry<ulong>(0, 15, transIntPtr2, null, Offsets.ObservedPlayerView.To_TransformInternal[2]);
            var inventory = round3.AddEntry<ulong>(0, 16, inventoryController, null, Offsets.InventoryController.Inventory);

            var transIntPtr4 = round4.AddEntry<ulong>(0, 17, transIntPtr3, null, Offsets.ObservedPlayerView.To_TransformInternal[3]);
            var equipment = round4.AddEntry<ulong>(0, 18, inventory, null, Offsets.Inventory.Equipment);

            var transIntPtr5 = round5.AddEntry<ulong>(0, 19, transIntPtr4, null, Offsets.ObservedPlayerView.To_TransformInternal[4]);
            var inventorySlots = round5.AddEntry<ulong>(0, 20, equipment, null, Offsets.Equipment.Slots);

            var transformInternal = round6.AddEntry<ulong>(0, 21, transIntPtr5, null, Offsets.ObservedPlayerView.To_TransformInternal[5]);

            scatterReadMap.Execute();
        }

        private void ProcessOnlinePlayerScatterReadResults(ScatterReadMap scatterReadMap)
        {
            if (!scatterReadMap.Results[0][14].TryGetResult<ulong>(out var movementContext))
                return;
            if (!scatterReadMap.Results[0][12].TryGetResult<ulong>(out var inventoryController))
                return;
            if (!scatterReadMap.Results[0][20].TryGetResult<ulong>(out var inventorySlots))
                return;
            if (!scatterReadMap.Results[0][21].TryGetResult<ulong>(out var transformInternal))
                return;
            if (!scatterReadMap.Results[0][13].TryGetResult<ulong>(out var healthController))
                return;
            if (!scatterReadMap.Results[0][8].TryGetResult<ulong>(out var playerBody))
                return;
            if (!scatterReadMap.Results[0][6].TryGetResult<int>(out var playerSide))
                return;
            if (!scatterReadMap.Results[0][5].TryGetResult<ulong>(out var accountID))
                return;
            if (!scatterReadMap.Results[0][4].TryGetResult<ulong>(out var name))
                return;
            if (!scatterReadMap.Results[0][9].TryGetResult<int>(out var memberCategory))
                return;
            if (!scatterReadMap.Results[0][7].TryGetResult<ulong>(out var groupID))
                return;

            this.InitializePlayerProperties(movementContext, inventoryController, inventorySlots, transformInternal, playerBody, name, groupID, playerSide);

            this.IsLocalPlayer = false;
            this.HealthController = healthController;
            this.AccountID = Memory.ReadUnityString(accountID);

            this.Type = this.GetOnlinePlayerType(this.AccountID == "0");
            this.IsPMC = (this.Type == PlayerType.BEAR || this.Type == PlayerType.USEC);

            this.FinishAlloc();
        }

        private void InitializePlayerProperties(ulong movementContext, ulong inventoryController, ulong inventorySlots, ulong transformInternal, ulong playerBody, ulong name, ulong groupID, int playerSide = 0)
        {
            this.MovementContext = movementContext;
            this.InventoryController = inventoryController;
            this.InventorySlots = inventorySlots;
            this._gearManager = new GearManager(this.InventorySlots);
            this.TransformInternal = transformInternal;
            this._transform = new Transform(this.TransformInternal, true);
            this.PlayerBody = playerBody;
            this.Name = Memory.ReadUnityString(name);
            this.Name = Helpers.TransliterateCyrillic(this.Name);
            this.PlayerSide = playerSide;

            if (groupID != 0)
            {
                var group = Memory.ReadUnityString(groupID);
                _groups.TryAdd(group, _groups.Count);
                this.GroupID = _groups[group];
            }
            else
            {
                this.GroupID = -1;
            }

            this.SetupBones();
        }

        /// <summary>
        /// Gets the pointers/transforms of the required bones
        /// </summary>
        private void SetupBones()
        {
            //var boneMatrix = Memory.ReadPtrChain(this.PlayerBody, [0x28, 0x28, 0x10]);

            //foreach (var bone in RequiredBones)
            //{
            //    var boneIndex = (uint)bone;
            //    var pointer = Memory.ReadPtrChain(boneMatrix, [0x20 + (boneIndex * 0x8), 0x10]);

            //    this.BonePointers.Add(pointer);
            //    this.BoneTransforms.Add(new Transform(pointer, false));
            //    this.BonePositions.Add(new Vector3(0f, 0f, 0f));
            //}
        }

        /// <summary>
        /// Allocation wrap-up.
        /// </summary>
        private void FinishAlloc()
        {
            if (this.IsHumanHostile)
                this.RefreshWatchlistStatus();
        }

        public async void RefreshWatchlistStatus()
        {
            var isOnWatchlist = _watchlistManager.IsOnWatchlist(this.AccountID, out Watchlist.Entry entry);
            var isSpecialPlayer = this.Type == PlayerType.SpecialPlayer;

            if ((!isSpecialPlayer || isSpecialPlayer) && isOnWatchlist)
            {
                var isLive = false;

                if (entry.IsStreamer)
                {
                    isLive = await Watchlist.IsLive(entry);

                    if (isLive)
                        this.Name += " (LIVE)";
                }

                if (!isLive && this.Name.Contains("(LIVE)"))
                {
                    this.Name = this.Name.Substring(0, this.Name.IndexOf("(LIVE)") - 1);
                }

                if (!string.IsNullOrEmpty(entry.Tag))
                {
                    this.Tag = entry.Tag;
                    this.Type = PlayerType.SpecialPlayer;
                }
            }
            else if (isSpecialPlayer && !isOnWatchlist)
            {
                this.Tag = "";
                this.Type = this.isOfflinePlayer ? this.GetOfflinePlayerType(false) : this.GetOnlinePlayerType(false);
            }
        }

        public void RefreshGear()
        {
            //this._gearManager.RefreshGear();
        }

        /// <summary>
        /// Resets/Updates 'static' assets in preparation for a new game/raid instance.
        /// </summary>
        public static void Reset()
        {
            _groups.Clear();
        }
        #endregion
    }
}
