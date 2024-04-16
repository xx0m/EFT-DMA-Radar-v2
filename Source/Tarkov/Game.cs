using System;
using System.Collections.ObjectModel;
using eft_dma_radar.Source.MonoSharp;
using eft_dma_radar.Source.Tarkov;
using Offsets;

namespace eft_dma_radar
{

    /// <summary>
    /// Class containing Game (Raid) instance.
    /// </summary>
    public class Game
    { 
        private GameObjectManager _gom;
        private LootManager _lootManager;
        private RegisteredPlayers _rgtPlayers;
        private GrenadeManager _grenadeManager;
        private ExfilManager _exfilManager;
        private PlayerManager _playerManager;
        private Config _config;
        private CameraManager _cameraManager;
        private QuestManager _questManager;
        private Toolbox _toolbox;
        private ulong _localGameWorld;
        private readonly ulong _unityBase;
        private bool _inHideout = false;
        private volatile bool _inGame = false;
        private volatile bool _loadingLoot = false;
        private volatile bool _refreshLoot = false;
        private volatile string _mapName = string.Empty;
        private volatile bool _isScav = false;

        public enum GameStatus
        {
            NotFound,
            Found,
            Menu,
            LoadingLoot,
            Matching,
            InGame,
            Error
        }

        #region Getters
        public bool InGame
        {
            get => _inGame;
        }
        // in InHideout means local game world not false and registered players is 1
        public bool InHideout
        {
            get => _inHideout;
        }
        public bool IsScav
        {
            get => _isScav;
        }
        public string MapName
        {
            get => _mapName;
        }
        public int PlayerSide
        {
            get => 0;
        }
        public bool LoadingLoot
        {
            get => _loadingLoot;
        }
        public ReadOnlyDictionary<string, Player> Players
        {
            get => _rgtPlayers?.Players;
        }
        public LootManager Loot
        {
            get => _lootManager;
        }
        public ReadOnlyCollection<Grenade> Grenades
        {
            get => _grenadeManager?.Grenades;
        }
        public ReadOnlyCollection<Exfil> Exfils
        {
            get => _exfilManager?.Exfils;
        }
        public CameraManager CameraManager {
            get => _cameraManager;
        }
        public PlayerManager PlayerManager
        {
            get => _playerManager;
        }
        public Toolbox Toolbox
        {
            get => _toolbox;
        }
        public QuestManager QuestManager {
        
            get => _questManager;
        }
        #endregion

        /// <summary>
        /// Game Constructor.
        /// </summary>
        public Game(ulong unityBase)
        {
            _unityBase = unityBase;
        }

        #region GameLoop
        /// <summary>
        /// Main Game Loop executed by Memory Worker Thread.
        /// It manages the updating of player list and game environment elements like loot, grenades, and exfils.
        /// </summary>
        public void GameLoop()
        {
            try
            {
                this._rgtPlayers.UpdateList();
                this._rgtPlayers.UpdateAllPlayers();
                this.UpdateMisc();
            }
            catch (DMAShutdown)
            {
                HandleDMAShutdown();
            }
            catch (RaidEnded e)
            {
                HandleRaidEnded(e);
            }
            catch (Exception ex)
            {
                HandleUnexpectedException(ex);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Method to get map name using local game world
        /// </summary>
        /// <returns></returns>
        private void GetMapName()
        {
            if (this._inHideout)
            {
                this._mapName = string.Empty;
                return;
            }

            try
            {
                var mapNamePtr = Memory.ReadPtrChain(this._localGameWorld, new uint[] { Offsets.LocalGameWorld.MainPlayer, Offsets.Player.Location });
                this._mapName = Memory.ReadUnityString(mapNamePtr);
            }
            catch
            {
                try
                {
                    var mapNamePtr = Memory.ReadPtr(this._localGameWorld + Offsets.LocalGameWorld.MapName);
                    this._mapName = Memory.ReadUnityString(mapNamePtr);
                }
                catch
                {
                    Program.Log("Couldn't find map name!!!");
                    this._mapName = "bigmap";
                }
            }
        }

        /// <summary>
        /// Handles the scenario when DMA shutdown occurs.
        /// </summary>
        private void HandleDMAShutdown()
        {
            this._inGame = false;
        }

        /// <summary>
        /// Handles the scenario when the raid ends.
        /// </summary>
        /// <param name="e">The RaidEnded exception instance containing details about the raid end.</param>
        private void HandleRaidEnded(RaidEnded e) {
            Program.Log("Raid has ended!");

            //this._inGame = false;
            //Memory.GameStatus = Game.GameStatus.Menu;
            Memory.Restart();
        }

        /// <summary>
        /// Handles unexpected exceptions that occur during the game loop.
        /// </summary>
        /// <param name="ex">The exception instance that was thrown.</param>
        private void HandleUnexpectedException(Exception ex)
        {
            Program.Log($"CRITICAL ERROR - Raid ended due to unhandled exception: {ex}");
            this._inGame = false;
        }

        /// <summary>
        /// Waits until Raid has started before returning to caller.
        /// </summary>
        /// 
        public void WaitForGame()
        {
			while (!this.GetGOM() || !this.GetLGW())
			{
				Thread.Sleep(1500);
			}
			Thread.Sleep(1000);
			Program.Log("Match found!");
			this._inGame = true;
			Thread.Sleep(1500);
        }

        /// <summary>
        /// Helper method to locate Game World object.
        /// </summary>
        private ulong GetObjectFromList(ulong activeObjectsPtr, ulong lastObjectPtr, string objectName)
        {
            var activeObject = Memory.ReadValue<BaseObject>(Memory.ReadPtr(activeObjectsPtr));
            var lastObject = Memory.ReadValue<BaseObject>(Memory.ReadPtr(lastObjectPtr));
            if (activeObject.obj != 0x0 && lastObject.obj == 0x0)
            {
                // Add wait for lastObject to be populated
                Program.Log("Waiting for lastObject to be populated...");
                while (lastObject.obj == 0x0)
                {
                    lastObject = Memory.ReadValue<BaseObject>(Memory.ReadPtr(lastObjectPtr));
                    Thread.Sleep(1000);
                }
            }

            if (activeObject.obj != 0x0)
            {
                while (activeObject.obj != 0x0 && activeObject.obj != lastObject.obj)
                {
                    var objectNamePtr = Memory.ReadPtr(activeObject.obj + Offsets.GameObject.ObjectName);
                    var objectNameStr = Memory.ReadString(objectNamePtr, 64);
                    if (objectNameStr.Contains(objectName, StringComparison.OrdinalIgnoreCase))
                    {
                        Program.Log($"Found object {objectNameStr}");
                        return activeObject.obj;
                    }

                    activeObject = Memory.ReadValue<BaseObject>(activeObject.nextObjectLink); // Read next object
                }
            }
            if (lastObject.obj != 0x0)
            {
                var objectNamePtr = Memory.ReadPtr(lastObject.obj + Offsets.GameObject.ObjectName);
                var objectNameStr = Memory.ReadString(objectNamePtr, 64);
                if (objectNameStr.Contains(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    Program.Log($"Found object {objectNameStr}");
                    return lastObject.obj;
                }
            }
            Program.Log($"Couldn't find object {objectName}");
            return 0;
        }

        /// <summary>
        /// Gets Game Object Manager structure.
        /// </summary>
        private bool GetGOM()
        {
            try
            {
                var addr = Memory.ReadPtr(_unityBase + Offsets.ModuleBase.GameObjectManager);
                _gom = Memory.ReadValue<GameObjectManager>(addr);
                Program.Log($"Found Game Object Manager at 0x{addr.ToString("X")}");
                return true;
            }
            catch (DMAShutdown) { throw; }
            catch (Exception ex)
            {
                throw new GameNotRunningException($"ERROR getting Game Object Manager, game may not be running: {ex}");
            }
        }

        /// <summary>
        /// Gets Local Game World address.
        /// </summary>
        private bool GetLGW()
        {
            var found = false;
            try
            {
                ulong gameWorld;
                ulong activeNodes;
                ulong lastActiveNode;
                try
                {
                    activeNodes = Memory.ReadPtr(_gom.ActiveNodes);
                    lastActiveNode = Memory.ReadPtr(_gom.LastActiveNode);
                    gameWorld = this.GetObjectFromList(activeNodes, lastActiveNode, "GameWorld");
                }
                catch
                {
                    this.GetGOM();
                    return found;
                }
                if (gameWorld == 0)
                {
                    Program.Log("Unable to find GameWorld Object, likely not in raid.");
                }
                else
                {
                    try
                    {
                        this._localGameWorld = Memory.ReadPtrChain(gameWorld, Offsets.GameWorld.To_LocalGameWorld);
                        Program.Log($"Found LocalGameWorld at 0x{this._localGameWorld.ToString("X")}");
                    }
                    catch
                    {
                        Program.Log("Couldnt find LocalGameWorld pointer");
                        Memory.GameStatus = Game.GameStatus.Menu;
                    }

                    if (this._localGameWorld == 0)
                    {
                        Program.Log("LocalGameWorld found but is 0");
                    }
                    else
                    {
                        Memory.GameStatus = Game.GameStatus.Matching;

                        if (!Memory.ReadValue<bool>(this._localGameWorld + Offsets.LocalGameWorld.RaidStarted))
                        {
                            Program.Log("Raid hasn't started!");
                        }
                        else
                        {
                            RegisteredPlayers registeredPlayers = new RegisteredPlayers(Memory.ReadPtr(this._localGameWorld + Offsets.LocalGameWorld.RegisteredPlayers));
                            if (registeredPlayers.PlayerCount > 0)
                            {
                                var localPlayer = Memory.ReadPtr(this._localGameWorld + Offsets.LocalGameWorld.MainPlayer);
                                var playerInfoPtr = Memory.ReadPtrChain(localPlayer, new uint[] { Offsets.Player.Profile, Offsets.Profile.PlayerInfo });
                                var localPlayerSide = Memory.ReadValue<int>(playerInfoPtr + Offsets.PlayerInfo.PlayerSide);
                                this._isScav = (localPlayerSide == 4);

                                this._rgtPlayers = registeredPlayers;
                                Memory.GameStatus = Game.GameStatus.InGame;
                                found = true;

                                Program.Log("Raid has started!!");
                            }
                        }
                    }
                }
            }
            catch (DMAShutdown)
            {
                throw; // Propagate the DMAShutdown exception upwards
            }
            catch (Exception ex)
            {
                Program.Log($"ERROR getting Local Game World: {ex}. Retrying...");
            }

            return found;
        }

        /// <summary>
        /// Loot, grenades, exfils,etc.
        /// </summary>
        private void UpdateMisc()
        {
            this._config = Program.Config;

            if (this._mapName == string.Empty)
            {
                this.GetMapName();
            }
            else
            {
                if (this._config.LootEnabled && (this._lootManager == null|| this._refreshLoot))
                {
                    this._loadingLoot = true;
                    try
                    {
                        this._lootManager = new LootManager(this._localGameWorld);
                        this._refreshLoot = false;
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"ERROR loading LootEngine: {ex}");
                    }
                    this._loadingLoot = false;
                }

                if (this._config.MasterSwitchEnabled)
                {
                    if (this._cameraManager is null)
                    {
                        try
                        {
                            this._cameraManager = new CameraManager(this._unityBase);
                        }
                        catch (Exception ex)
                        {
                            Program.Log($"ERROR loading CameraManager: {ex}");
                        }
                    }

                    if (this._playerManager is null)
                    {
                        try
                        {
                            this._playerManager = new PlayerManager(this._localGameWorld);
                        }
                        catch (Exception ex)
                        {
                            Program.Log($"ERROR loading PlayerManager: {ex}");
                        }
                    }

                    if (this._questManager is null)
                    {
                        try
                        {
                            this._questManager = new QuestManager(this._localGameWorld);
                        }
                        catch (Exception ex)
                        {
                            Program.Log($"ERROR loading QuestManager: {ex}");
                        }
                    }

                    if (this._toolbox is null)
                    {
                        try
                        {
                            this._toolbox = new Toolbox();
                        }
                        catch (Exception ex)
                        {
                            Program.Log($"ERROR loading Toolbox: {ex}");
                        }
                    }
                }

                if (this._exfilManager is null)
                {
                    try
                    {
                        this._exfilManager = new ExfilManager(this._localGameWorld);
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"ERROR loading ExfilController: {ex}");
                    }
                }
                else this._exfilManager.RefreshExfils();

                if (this._grenadeManager is null)
                {
                    try
                    {
                        this._grenadeManager = new GrenadeManager(this._localGameWorld);
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"ERROR loading GrenadeManager: {ex}");
                    }
                }
                else this._grenadeManager.Refresh();

                if (this._config.QuestHelperEnabled && this._questManager is null)
                {
                    try
                    {
                        this._questManager = new QuestManager(this._localGameWorld);
                    }
                    catch (Exception ex)
                    {
                        Program.Log($"ERROR loading QuestManager: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Triggers loot refresh
        /// </summary>
        public void RefreshLoot()
        {
            if (this._inGame)
            {
                this._refreshLoot = true;
            }
        }

        /// <summary>
        /// Sets the maximum loot/door interaction distance
        /// </summary>
        /// <param name="enabled"></param>
        public static void SetInteractDistance(bool on)
        {
            var hardSettings = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "EFTHardSettings");
            var currentLootRaycastDistance = Memory.ReadValue<float>(hardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE);

            if (on && currentLootRaycastDistance != 1.8f)
            {
                Memory.WriteValue<float>(hardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.8f);
                Memory.WriteValue<float>(hardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1.8f);
            }
            else if (!on && currentLootRaycastDistance == 1.8f)
            {
                Memory.WriteValue<float>(hardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.3f);
                Memory.WriteValue<float>(hardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1f);
            }
        }
        #endregion
    }

    #region Exceptions
    public class GameNotRunningException : Exception
    {
        public GameNotRunningException()
        {
        }

        public GameNotRunningException(string message)
            : base(message)
        {
        }

        public GameNotRunningException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class RaidEnded : Exception
    {
        public RaidEnded()
        {

        }

        public RaidEnded(string message)
            : base(message)
        {
        }

        public RaidEnded(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    #endregion
}
