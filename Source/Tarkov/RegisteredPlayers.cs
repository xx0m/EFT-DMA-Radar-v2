using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;

namespace eft_dma_radar
{
    public class RegisteredPlayers
    {
        private readonly ulong _base;
        private readonly ulong _listBase;
        private readonly Stopwatch _regSw = new();
        private readonly Stopwatch _healthSw = new();
        private readonly Stopwatch _posSw = new();
        private readonly Stopwatch _weaponSw = new();

        private readonly ConcurrentDictionary<string, Player> _players = new(StringComparer.OrdinalIgnoreCase);

        private int _localPlayerGroup = -100;
        private readonly Vector3 DEFAULT_POSITION = new Vector3(0, 0, -9999);

        #region Getters
        public ReadOnlyDictionary<string, Player> Players { get; }

        private bool IsAtHideout
        {
            get => Memory.InHideout;
        }

        public int PlayerCount
        {
            get
            {
                const int maxAttempts = 5;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        var count = Memory.ReadValue<int>(this._base + Offsets.UnityList.Count);

                        if (count < 1 || count > 1024)
                        {
                            this._players.Clear();
                            return -1;
                        }

                        return count;
                    }
                    catch (DMAShutdown)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < maxAttempts - 1)
                    {
                        Program.Log($"ERROR - PlayerCount attempt {attempt + 1} failed: {ex}");
                        Thread.Sleep(1000);
                    }
                }
                return -1;
            }
        }
        #endregion

        /// <summary>
        /// RegisteredPlayers List Constructor.
        /// </summary>
        public RegisteredPlayers(ulong baseAddr)
        {
            this._base = baseAddr;
            this.Players = new(this._players);
            this._listBase = Memory.ReadPtr(this._base + 0x0010);
            this._regSw.Start();
            this._healthSw.Start();
            this._posSw.Start();
            this._weaponSw.Start();
        }

        #region Update List/Player Functions
        /// <summary>
        /// Updates the ConcurrentDictionary of 'Players'
        /// </summary>
        public void UpdateList()
        {
            if (this._regSw.ElapsedMilliseconds < 500)
                return;

            try
            {
                var count = this.PlayerCount;

                if (count < 1 || count > 1024)
                    throw new RaidEnded();

                var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var scatterMap = new ScatterReadMap(count);
                var round1 = scatterMap.AddRound();
                var round2 = scatterMap.AddRound();
                var round3 = scatterMap.AddRound();
                var round4 = scatterMap.AddRound();
                var round5 = scatterMap.AddRound();

                for (int i = 0; i < count; i++)
                {
                    var p1 = round1.AddEntry<ulong>(i, 0, _listBase + Offsets.UnityListBase.Start + (uint)(i * 0x8));
                    var p2 = round2.AddEntry<ulong>(i, 1, p1, null, 0x0);
                    var p3 = round3.AddEntry<ulong>(i, 2, p2, null, 0x0);
                    var p4 = round4.AddEntry<ulong>(i, 3, p3, null, 0x48);
                    var p5 = round5.AddEntry<string>(i, 4, p4, 64);

                    var p6 = round2.AddEntry<ulong>(i, 5, p1, null, Offsets.Player.Profile);
                    var p7 = round3.AddEntry<ulong>(i, 6, p6, null, Offsets.Profile.Id);
                }

                scatterMap.Execute();

                var scatterMap2 = new ScatterReadMap(count);
                var round6 = scatterMap2.AddRound();
                var round7 = scatterMap2.AddRound();
                var round8 = scatterMap2.AddRound();

                for (int i = 0; i < count; i++)
                {
                    if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var playerBase))
                        continue;
                    if (!scatterMap.Results[i][4].TryGetResult<string>(out var className))
                        continue;

                    ScatterReadEntry<ulong> p2;

                    if (className == "ClientPlayer" || className == "LocalPlayer" || className == "HideoutPlayer")
                    {
                        p2 = round7.AddEntry<ulong>(i, 1, playerBase, null, Offsets.Player.Profile);
                    }
                    else
                    {
                        var p1 = round6.AddEntry<ulong>(i, 0, playerBase, null, Offsets.ObservedPlayerView.ObservedPlayerController);
                        p2 = round7.AddEntry<ulong>(i, 1, p1, null, Offsets.ObservedPlayerController.Profile);
                    }

                    var playerID = round8.AddEntry<ulong>(i, 2, p2, null, Offsets.Profile.Id);
                }

                scatterMap2.Execute();

                for (int i = 0; i < count; i++)
                {
                    if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var playerBase))
                        continue;
                    if (!scatterMap.Results[i][4].TryGetResult<string>(out var className))
                        continue;
                    if (!scatterMap2.Results[i][1].TryGetResult<ulong>(out var profilePtr))
                        continue;
                    if (!scatterMap2.Results[i][2].TryGetResult<ulong>(out var playerID))
                        continue;

                    var profileID = Memory.ReadUnityString(playerID);

                    if (string.IsNullOrEmpty(profileID) || profileID.Length != 24 && profileID.Length != 36 || className.Length < 0)
                    {
                        Program.Log($"Invalid ProfileID: {profileID} - {className}");
                        continue;
                    }

                    registered.Add(profileID);

                    if (this._players.TryGetValue(profileID, out var player))
                    {
                        if (player.ErrorCount > 50)
                        {
                            Program.Log($"Existing player '{player.Name}' being reallocated due to excessive errors...");
                            reallocatePlayer(profileID, playerBase, profilePtr);
                        }
                        else if (player.Base != playerBase)
                        {
                            Program.Log($"Existing player '{player.Name}' being reallocated due to new base address...");
                            reallocatePlayer(profileID, playerBase, profilePtr);
                        }
                        else
                        {
                            player.IsActive = true;

                            if (player.MarkedDeadCount < 2)
                                player.IsAlive = true;
                        }
                    }
                    else
                    {
                        try
                        {
                            var newPlayer = new Player(playerBase, profilePtr, profileID, null, className);

                            if (string.IsNullOrEmpty(newPlayer.Name))
                                throw new Exception($"Error setting name for profile '{newPlayer.Profile}' ({newPlayer.Name})");

                            if (newPlayer.Type == PlayerType.LocalPlayer)
                                if (this._players.Values.Any(x => x.Type == PlayerType.LocalPlayer))
                                    continue; // Don't allocate more than one LocalPlayer on accident

                            if (this._players.TryAdd(profileID, newPlayer))
                                Program.Log($"Player '{newPlayer.Name}' allocated.");
                        }
                        catch
                        {
                            Program.Log($"ERROR - Failed to read player data for '{profileID}'");
                        }
                    }
                }

                foreach (var player in this._players)
                {
                    if (registered.Count == 0)
                        break;

                    if (!registered.Contains(player.Key))
                    {
                        if (player.Value.IsActive)
                        {
                            player.Value.LastUpdate = true;
                        }
                        else
                        {
                            var dupeCount = registered.Count(x => x == player.Key);

                            if (dupeCount > 1)
                                Program.Log($"WARNING - Player '{player.Value.Name} {player.Key}' registered {count} times.");

                            player.Value.IsActive = false;
                        }
                    }
                }
            }
            catch (DMAShutdown)
            {
                throw;
            }
            catch (RaidEnded)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Log($"CRITICAL ERROR - RegisteredPlayers Loop FAILED: {ex}");
            }
            finally
            {
                this._regSw.Restart();
            }

            void reallocatePlayer(string profileID, ulong playerBase, ulong profilePtr)
            {
                try
                {
                    this._players[profileID] = new Player(playerBase, profilePtr, profileID, this._players[profileID].Position);
                    Program.Log($"Player '{this._players[profileID].Name}' Re-Allocated successfully.");
                }
                catch (Exception ex)
                {
                    throw new Exception($"ERROR re-allocating player: ", ex);
                }
            }
        }

        /// <summary>
        /// Updates all 'Player' values (Position,health,direction,etc.)
        /// </summary>
        public void UpdateAllPlayers()
        {
            if (this.IsAtHideout)
                return;

            try
            {
                var players = this._players
                    .Select(x => x.Value)
                    .Where(x => x.IsActive && x.IsAlive)
                    .ToArray();

                if (players.Length == 0)
                    return;

                if (this._localPlayerGroup == -100)
                {
                    var localPlayer = this._players.FirstOrDefault(x => x.Value.Type is PlayerType.LocalPlayer).Value;

                    if (localPlayer is not null)
                        this._localPlayerGroup = localPlayer.GroupID;
                }

                var checkHealth = this._healthSw.ElapsedMilliseconds > 500;
                var checkWeaponInfo = this._weaponSw.ElapsedMilliseconds > 2500;
                var checkPos = this._posSw.ElapsedMilliseconds > 10000 && players.Any(x => x.IsHumanActive);

                var scatterMap = new ScatterReadMap(players.Length);
                var round1 = scatterMap.AddRound();
                var round2 = scatterMap.AddRound();
                var round3 = scatterMap.AddRound();
                var round4 = scatterMap.AddRound();
                var round5 = scatterMap.AddRound();
                var round6 = scatterMap.AddRound();

                for (int i = 0; i < players.Length; i++)
                {
                    var player = players[i];

                    if (player.LastUpdate)
                    {
                        var corpse = round1.AddEntry<ulong>(i, 6, player.CorpsePtr);
                    }
                    else
                    {
                        var rotation = round1.AddEntry<Vector2>(i, 0, (player.isOfflinePlayer ? player.MovementContext + Offsets.MovementContext.Rotation : player.MovementContext + Offsets.ObservedPlayerMovementContext.Rotation));
                        var posAddr = player.TransformScatterReadParameters;
                        var indices = round1.AddEntry<List<int>>(i, 1, posAddr.Item1, posAddr.Item2 * 4);
                        var vertices = round1.AddEntry<List<Vector128<float>>>(i, 2, posAddr.Item3, posAddr.Item4 * 16);

                        if (checkPos)
                        {
                            var hierarchy = round1.AddEntry<ulong>(i, 3, player.TransformInternal, null, Offsets.TransformInternal.Hierarchy);
                            var indicesAddr = round2.AddEntry<ulong>(i, 4, hierarchy, null, Offsets.TransformHierarchy.Indices);
                            var verticesAddr = round2.AddEntry<ulong>(i, 5, hierarchy, null, Offsets.TransformHierarchy.Vertices);
                        }

                        if (checkHealth && player.IsActive)
                        {
                            var health = round1.AddEntry<int>(i, 6, player.HealthController, null, Offsets.HealthController.HealthStatus);
                        }

                        if (checkWeaponInfo && !player.IsZombie)
                        {
                            ScatterReadEntry<ulong> handsController, currentItem;

                            if (player.isOfflinePlayer)
                            {
                                handsController = round1.AddEntry<ulong>(i, 8, player.Base, null, Offsets.Player.HandsController);
                                currentItem = round3.AddEntry<ulong>(i, 9, handsController, null, Offsets.HandsController.Item);
                            }
                            else
                            {
                                handsController = round1.AddEntry<ulong>(i, 7, player.Base, null, Offsets.ObservedPlayerView.To_HandsController[0]);
                                handsController = round2.AddEntry<ulong>(i, 8, handsController, null, Offsets.ObservedPlayerView.To_HandsController[1]);
                                currentItem = round3.AddEntry<ulong>(i, 9, handsController, null, Offsets.ObservedHandsController.Item);
                            }
                        }
                    }
                }

                scatterMap.Execute();

                for (int i = 0; i < players.Length; i++)
                {
                    var player = players[i];

                    if (player.Type is PlayerType.Default)
                        continue;

                    if (this._localPlayerGroup != -100 && player.GroupID != -1 && player.IsHumanHostile && player.GroupID == this._localPlayerGroup)
                        player.Type = PlayerType.Teammate;

                    if (player.LastUpdate) // player may be dead/exfil'd
                    {
                        var doChams = Program.Config.MasterSwitch && Program.Config.Chams["Enabled"];

                        if (player.Position == DEFAULT_POSITION)
                        {

                            player.IsActive = false;
                            Program.Log($"{player.Name} exfiltrated");

                            if (doChams)
                                Memory.Chams.RemovePointersForPlayer(player);
                        }
                        else
                        {
                            if (player.Position == DEFAULT_POSITION)
                            {

                                player.IsActive = false;
                                Program.Log($"{player.Name} exfiltrated");

                                Memory.Chams.RemovePointersForPlayer(player);
                            }
                            else
                            {
                                if (doChams)
                                {
                                    if (player.IsLocalPlayer)
                                    {
                                        Memory.Chams.RestorePointers();
                                        Program.Log("LocalPlayer has died!");
                                    }
                                    else
                                    {
                                        if (Program.Config.Chams["Corpses"])
                                            Memory.Chams.SetPlayerBodyChams(player, Memory.Chams.ThermalMaterial);
                                        else
                                            Memory.Chams.RestorePointersForPlayer(player);
                                    }
                                }

                                player.IsAlive = false;
                                Program.Log($"{player.Name} died");
                            }
                        }

                        player.LastUpdate = false;
                    }
                    else
                    {
                        bool posOK = true;

                        if (checkPos && player.IsHumanActive)
                        {
                            if (!scatterMap.Results[i][2].TryGetResult<ulong>(out var i4))
                                continue;
                            if (!scatterMap.Results[i][3].TryGetResult<ulong>(out var i5))
                                continue;

                            if (i4 != 0 && i5 != 0)
                            {
                                var indicesAddr = i4;
                                var verticesAddr = i5;

                                if (player.IndicesAddr != indicesAddr || player.VerticesAddr != verticesAddr)
                                {
                                    Program.Log($"WARNING - Transform has changed for Player '{player.Name}'");
                                    player.SetPosition(null);
                                    posOK = false;
                                }
                            }
                        }

                        var rotation = scatterMap.Results[i][0].TryGetResult<Vector2>(out var rot);
                        bool p2 = player.SetRotation(rot);

                        var indices = scatterMap.Results[i][1].TryGetResult<List<int>>(out var ind);
                        var vertices = scatterMap.Results[i][2].TryGetResult<List<Vector128<float>>>(out var vert);

                        var posBufs = new object[2]
                        {
                            ind,
                            vert
                        };

                        bool p3 = true;

                        if (posOK)
                            p3 = player.SetPosition(posBufs);

                        if (checkHealth && !player.IsLocalPlayer)
                            if (scatterMap.Results[i][6].TryGetResult<int>(out var hp))
                                player.SetHealth(hp);

                        if (checkWeaponInfo && !player.IsZombie)
                        {
                            try
                            {
                                var slotsRefreshed = player.GearManager.CheckGearSlots();

                                scatterMap.Results[i][9].TryGetResult<ulong>(out var activeWeaponPtr);

                                if (activeWeaponPtr != 0 && !slotsRefreshed.Any(x => x.Pointer == activeWeaponPtr))
                                    player.GearManager.RefreshActiveWeaponAmmoInfo(activeWeaponPtr);

                                if (player.ItemInHands.Pointer != activeWeaponPtr)
                                    player.SetItemInHands(activeWeaponPtr);

                                player.CheckForRequiredGear();
                            }
                            catch { }
                        }

                        if (p2 && p3)
                            player.ErrorCount = 0;
                        else
                            player.ErrorCount++;
                    }
                };

                if (checkHealth)
                    this._healthSw.Restart();

                if (checkPos)
                    this._posSw.Restart();

                if (checkWeaponInfo)
                    this._weaponSw.Restart();
            }

            catch (Exception ex)
            {
                Program.Log($"CRITICAL ERROR - UpdateAllPlayers Loop FAILED: {ex}");
            }
        }
        #endregion
    }
}