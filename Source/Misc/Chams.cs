using Offsets;
using OpenTK.Input;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace eft_dma_radar
{
    public class Chams
    {
        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }

        private ReadOnlyDictionary<string, Player> AllPlayers
        {
            get => Memory.Players;
        }

        private bool InGame
        {
            get => Memory.InGame;
        }

        private Config _config
        {
            get => Program.Config;
        }

        public int PlayersWithChamsCount
        {
            get => PlayersWithChams.Count;
        }

        public ulong NVGMaterial
        {
            get => _nvgMaterial;
        }

        public ulong ThermalMaterial
        {
            get => _thermalMaterial;
        }

        private Vector4 _color
        {
            get => Extensions.Vector4FromPaintColor("Chams");
        }

        private bool safeToWriteChams
        {
            get => (this.InGame && Memory.LocalPlayer is not null && Memory.LocalPlayer.IsActive && Memory.Players.Count > 1);
        }

        private static Dictionary<string, Player> PlayersWithChams = new Dictionary<string, Player>();
        private Dictionary<string, List<PointerBackup>> pointerBackups = new Dictionary<string, List<PointerBackup>>();

        private ulong _nvgMaterial;
        private ulong _thermalMaterial;

        private Vector4 lastColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        public void ChamsEnable()
        {
            if (!safeToWriteChams)
            {
                Program.Log("Chams -> not safe to write");
                return;
            }

            var nightVisionComponent = _cameraManager.NVGComponent;
            var fpsThermal = _cameraManager.ThermalComponent;

            if (nightVisionComponent == 0 || fpsThermal == 0)
            {
                Program.Log("Chams -> nvg or fps thermal component not found");
                Memory.CameraManager.UpdateCamera();
                return;
            }

            if (_nvgMaterial == 0UL)
                _nvgMaterial = Memory.ReadPtrChain(nightVisionComponent, new uint[] { 0x90, 0x10, 0x8 });

            if (_thermalMaterial == 0UL)
                _thermalMaterial = Memory.ReadPtrChain(fpsThermal, new uint[] { 0x90, 0x10, 0x8 });

            if (_nvgMaterial == 0UL || _thermalMaterial == 0UL)
            {
                Program.Log("Chams -> nvg or thermal material not found");
                return;
            }

            if (lastColor != _color)
            {
                lastColor = _color;
                Memory.WriteValue(nightVisionComponent + 0xD8, lastColor);
            }

            var players = this.AllPlayers
                             ?.Where(x =>
                                          !Chams.PlayersWithChams.ContainsKey(x.Value.Base.ToString()) &&
                                          x.Value.IsActive &&
                                          !x.Value.IsLocalPlayer &&
                                          x.Value.Type != PlayerType.LocalPlayer)
                             .Where(x =>
                                          _config.Chams["Corpses"] ? true : x.Value.IsAlive &&
                                          (_config.Chams["PMCs"] && x.Value.IsPMC && x.Value.Type != PlayerType.Teammate) ||
                                          (_config.Chams["Teammates"] && x.Value.IsPMC && x.Value.Type == PlayerType.Teammate) ||
                                          _config.Chams["PlayerScavs"] && x.Value.Type == PlayerType.PlayerScav ||
                                          _config.Chams["Bosses"] && x.Value.Type == PlayerType.Boss ||
                                          _config.Chams["Rogues"] && x.Value.IsRogueRaider ||
                                          _config.Chams["Cultists"] && x.Value.Type == PlayerType.Cultist ||
                                          _config.Chams["Scavs"] && x.Value.Type == PlayerType.Scav)
                             .Select(x => x.Value)
                             .ToList();

            if (players?.Count > 0)
            {
                foreach (var player in players)
                {
                    try
                    {
                        if (!safeToWriteChams)
                        {
                            Program.Log("Chams -> not safe to write");
                            break;
                        }

                        var materialTouse = (player.IsHuman || player.Type == PlayerType.Boss) && player.IsAlive ? _nvgMaterial : _thermalMaterial;

                        if (this.SetPlayerBodyChams(player, materialTouse))
                            Chams.PlayersWithChams.TryAdd(player.Base.ToString(), player);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR -> ChamsEnable -> {ex.Message}\nStackTrace:{ex.StackTrace}");
                    }
                }
            }
        }

        public bool SetPlayerBodyChams(Player player, ulong material)
        {
            var setAnyMaterial = false;

            try
            {
                var count = 1;
                var scatterReadMap = new ScatterReadMap(count);
                var map1Round1 = scatterReadMap.AddRound();
                var map1Round2 = scatterReadMap.AddRound();

                var bodySkinsPtr = map1Round1.AddEntry<ulong>(0, 0, player.PlayerBody, null, 0x40);
                var skinEntriesPtr = map1Round2.AddEntry<ulong>(0, 1, bodySkinsPtr, null, 0x18);
                var bodySkinsCountPtr = map1Round2.AddEntry<int>(0, 2, bodySkinsPtr, null, 0x40);

                scatterReadMap.Execute();

                if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var bodySkins))
                    return false;
                if (!scatterReadMap.Results[0][1].TryGetResult<ulong>(out var skinEntries))
                    return false;
                if (!scatterReadMap.Results[0][2].TryGetResult<int>(out var bodySkinsCount))
                    return false;

                var scatterReadMap2 = new ScatterReadMap(bodySkinsCount);
                var map2Round1 = scatterReadMap2.AddRound();
                var map2Round2 = scatterReadMap2.AddRound();
                var map2Round3 = scatterReadMap2.AddRound();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    var pBodySkinsPtr = map2Round1.AddEntry<ulong>(i, 0, skinEntries, null, 0x30 + (0x18 * (uint)i));
                    var pLodsArrayPtr = map2Round2.AddEntry<ulong>(i, 1, pBodySkinsPtr, null, 0x18);
                    var lodsCountPtr = map2Round3.AddEntry<int>(i, 2, pLodsArrayPtr, null, 0x18);
                }

                scatterReadMap2.Execute();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var pLodsArray))
                        continue;
                    if (!scatterReadMap2.Results[i][2].TryGetResult<int>(out var lodsCount))
                        continue;

                    var scatterReadMap3 = new ScatterReadMap(lodsCount);
                    var map3Round1 = scatterReadMap3.AddRound();
                    var map3Round2 = scatterReadMap3.AddRound();
                    var map3Round3 = scatterReadMap3.AddRound();
                    var map3Round4 = scatterReadMap3.AddRound();

                    if (lodsCount > 4)
                        continue;

                    for (int j = 0; j < lodsCount; j++)
                    {
                        var pLodEntryPtr = map3Round1.AddEntry<ulong>(j, 0, pLodsArray, null, 0x20 + (0x8 * (uint)j));

                        var skinnedMeshRendererPtr = map3Round2.AddEntry<ulong>(j, 1, pLodEntryPtr, null, 0x20);
                        var pMaterialDictionaryPtr = map3Round3.AddEntry<ulong>(j, 2, skinnedMeshRendererPtr, null, 0x10);

                        var materialCountPtr = map3Round4.AddEntry<int>(j, 3, pMaterialDictionaryPtr, null, 0x158);
                        var materialDictionaryBasePtr = map3Round4.AddEntry<ulong>(j, 4, pMaterialDictionaryPtr, null, 0x148);
                    }

                    scatterReadMap3.Execute();

                    for (int j = 0; j < lodsCount; j++)
                    {
                        if (!scatterReadMap3.Results[j][0].TryGetResult<ulong>(out var pLodEntry))
                            continue;
                        if (!scatterReadMap3.Results[j][2].TryGetResult<ulong>(out var pMaterialDictionary))
                            continue;
                        if (!scatterReadMap3.Results[j][3].TryGetResult<int>(out var materialCount))
                            continue;
                        if (!scatterReadMap3.Results[j][4].TryGetResult<ulong>(out var materialDictionaryBase))
                            continue;

                        if (j == 1)
                            pLodEntry = Memory.ReadPtr(pLodEntry + 0x20);

                        if (pLodEntry == 0)
                            continue;

                        if (materialCount > 0 && materialCount < 3)
                        {
                            var scatterReadMap4 = new ScatterReadMap(materialCount);
                            var map4Round1 = scatterReadMap4.AddRound();

                            for (int k = 0; k < materialCount; k++)
                            {
                                var pMaterialPtr = map4Round1.AddEntry<ulong>(k, 0, materialDictionaryBase, null, (0x50 * (uint)k));
                            }

                            scatterReadMap4.Execute();

                            for (int k = 0; k < materialCount; k++)
                            {
                                if (!scatterReadMap4.Results[k][0].TryGetResult<ulong>(out var pMaterial))
                                    continue;

                                if (pMaterial == 0 || material == 0)
                                    continue;

                                if (pMaterial == material)
                                {
                                    setAnyMaterial = true;
                                    break;
                                }

                                if (!safeToWriteChams)
                                    break;

                                if (!_config.Chams["AlternateMethod"])
                                    SavePointer(materialDictionaryBase + (0x50 * (uint)k), pMaterial, player);
                                else
                                    SavePointer(materialDictionaryBase + (sizeof(uint) * (uint)k), pMaterial, player);

                                Memory.WriteValue(materialDictionaryBase + (sizeof(uint) * (uint)k), material);
                                setAnyMaterial = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"SetPlayerBodyChams -> {ex.Message}\n{ex.StackTrace}");
            }

            return setAnyMaterial;
        }

        public bool TriggerUnityCrash(Player player, ulong material)
        {
            var setAnyMaterial = false;

            try
            {
                var count = 1;
                var scatterReadMap = new ScatterReadMap(count);
                var map1Round1 = scatterReadMap.AddRound();
                var map1Round2 = scatterReadMap.AddRound();

                var bodySkinsPtr = map1Round1.AddEntry<ulong>(0, 0, player.PlayerBody, null, 0x40);
                var skinEntriesPtr = map1Round2.AddEntry<ulong>(0, 1, bodySkinsPtr, null, 0x18);
                var bodySkinsCountPtr = map1Round2.AddEntry<int>(0, 2, bodySkinsPtr, null, 0x40);

                scatterReadMap.Execute();

                if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var bodySkins))
                    return false;
                if (!scatterReadMap.Results[0][1].TryGetResult<ulong>(out var skinEntries))
                    return false;
                if (!scatterReadMap.Results[0][2].TryGetResult<int>(out var bodySkinsCount))
                    return false;

                var scatterReadMap2 = new ScatterReadMap(bodySkinsCount);
                var map2Round1 = scatterReadMap2.AddRound();
                var map2Round2 = scatterReadMap2.AddRound();
                var map2Round3 = scatterReadMap2.AddRound();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    var pBodySkinsPtr = map2Round1.AddEntry<ulong>(i, 0, skinEntries, null, 0x30 + (0x18 * (uint)i));
                    var pLodsArrayPtr = map2Round2.AddEntry<ulong>(i, 1, pBodySkinsPtr, null, 0x18);
                    var lodsCountPtr = map2Round3.AddEntry<int>(i, 2, pLodsArrayPtr, null, 0x18);
                }

                scatterReadMap2.Execute();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var pLodsArray))
                        continue;
                    if (!scatterReadMap2.Results[i][2].TryGetResult<int>(out var lodsCount))
                        continue;

                    var scatterReadMap3 = new ScatterReadMap(lodsCount);
                    var map3Round1 = scatterReadMap3.AddRound();
                    var map3Round2 = scatterReadMap3.AddRound();
                    var map3Round3 = scatterReadMap3.AddRound();
                    var map3Round4 = scatterReadMap3.AddRound();

                    if (lodsCount > 4)
                        continue;

                    for (int j = 0; j < lodsCount; j++)
                    {
                        var pLodEntryPtr = map3Round1.AddEntry<ulong>(j, 0, pLodsArray, null, 0x20 + (0x8 * (uint)j));

                        var skinnedMeshRendererPtr = map3Round2.AddEntry<ulong>(j, 1, pLodEntryPtr, null, 0x20);
                        var pMaterialDictionaryPtr = map3Round3.AddEntry<ulong>(j, 2, skinnedMeshRendererPtr, null, 0x10);

                        var materialCountPtr = map3Round4.AddEntry<int>(j, 3, pMaterialDictionaryPtr, null, 0x158);
                        var materialDictionaryBasePtr = map3Round4.AddEntry<ulong>(j, 4, pMaterialDictionaryPtr, null, 0x148);
                    }

                    scatterReadMap3.Execute();

                    for (int j = 0; j < lodsCount; j++)
                    {
                        if (!scatterReadMap3.Results[j][0].TryGetResult<ulong>(out var pLodEntry))
                            continue;
                        if (!scatterReadMap3.Results[j][2].TryGetResult<ulong>(out var pMaterialDictionary))
                            continue;
                        if (!scatterReadMap3.Results[j][3].TryGetResult<int>(out var materialCount))
                            continue;
                        if (!scatterReadMap3.Results[j][4].TryGetResult<ulong>(out var materialDictionaryBase))
                            continue;

                        if (j == 1)
                            pLodEntry = Memory.ReadPtr(pLodEntry + 0x20);

                        if (pLodEntry == 0)
                            continue;

                        if (materialCount > 0 && materialCount < 3)
                        {
                            var scatterReadMap4 = new ScatterReadMap(materialCount);
                            var map4Round1 = scatterReadMap4.AddRound();

                            for (int k = 0; k < materialCount; k++)
                            {
                                var pMaterialPtr = map4Round1.AddEntry<ulong>(k, 0, materialDictionaryBase, null, (0x50 * (uint)k));
                            }

                            scatterReadMap4.Execute();

                            for (int k = 0; k < materialCount; k++)
                            {
                                if (!scatterReadMap4.Results[k][0].TryGetResult<ulong>(out var pMaterial))
                                    continue;

                                if (pMaterial == 0 || material == 0)
                                    continue;

                                if (pMaterial == material)
                                {
                                    setAnyMaterial = true;
                                    break;
                                }

                                if (!safeToWriteChams)
                                    break;

                                if (!_config.Chams["AlternateMethod"])
                                    SavePointer(materialDictionaryBase + (0x50 * (uint)k), pMaterial, player);
                                else
                                    SavePointer(materialDictionaryBase + (sizeof(uint) * (uint)k), pMaterial, player);

                                Memory.WriteValue(materialDictionaryBase + (sizeof(uint) * (uint)k), material);
                                setAnyMaterial = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"SetPlayerBodyChams -> {ex.Message}\n{ex.StackTrace}");
            }

            return setAnyMaterial;
        }

        public void ChamsDisable()
        {
            if (!safeToWriteChams)
                this.RemovePointers();
            else
                this.RestorePointers();
        }

        public void SoftChamsDisable()
        {
            try
            {
                if (PlayersWithChams.Count == 0)
                {
                    Program.Log("No players with chams enabled.");
                    return;
                }

                this.SoftRestorePointers();
            }
            catch { }
        }

        private void SavePointer(ulong address, ulong originalValue, Player player)
        {
            var key = player.Base.ToString();

            if (!pointerBackups.ContainsKey(key))
                pointerBackups[key] = new List<PointerBackup>();

            var existingBackup = pointerBackups[key].FirstOrDefault(b => b.Address == address);

            if (existingBackup.Address == 0)
                pointerBackups[key].Add(new PointerBackup { Address = address, OriginalValue = originalValue });
        }

        public void RestorePointers()
        {
            foreach (var backupList in pointerBackups.Values)
            {
                foreach (var backup in backupList)
                {
                    try
                    {
                        if (safeToWriteChams)
                            Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);
                    }
                    catch { continue; }
                }
            }

            RemovePointers();
        }

        public void SoftRestorePointers()
        {
            foreach (var backupList in pointerBackups.Values)
            {
                foreach (var backup in backupList)
                {
                    try
                    {
                        if (safeToWriteChams)
                            Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);

                    }
                    catch { continue; }
                }
            }

            PlayersWithChams.Clear();
        }

        public void RestorePointersForPlayer(Player player)
        {
            var key = player.Base.ToString();

            if (pointerBackups.ContainsKey(key))
            {
                foreach (var backup in pointerBackups[key])
                {
                    try
                    {
                        if (!player.IsActive)
                        {
                            this.RemovePointersForPlayer(player);
                            break;
                        }

                        if (safeToWriteChams)
                            Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);

                    }
                    catch { continue; }
                }
            }
        }

        public void RemovePointersForPlayer(Player player)
        {
            var key = player.Base.ToString();

            if (pointerBackups.Remove(key))
                Program.Log($"Cleaned pointer backups for {player.Name}");

            if (PlayersWithChams.Remove(key))
                Program.Log($"Removed {player.Name} from players w/ chams");

        }

        public void RemovePointers()
        {
            pointerBackups.Clear();
            PlayersWithChams.Clear();
        }
    }

    public struct PointerBackup
    {
        public ulong Address;
        public ulong OriginalValue;
    }
}