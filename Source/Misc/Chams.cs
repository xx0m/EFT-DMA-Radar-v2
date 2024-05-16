using System.Collections.ObjectModel;
using System.Drawing;
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

        private static Dictionary<string, Player> PlayersWithChams = new Dictionary<string, Player>();

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

        private ulong _nvgMaterial;
        private ulong _thermalMaterial;

        private Vector4 lastColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        public void ChamsEnable()
        {
            if (!this.InGame)
            {
                Console.WriteLine("Not in game");
                return;
            }

            var nightVisionComponent = _cameraManager.NVGComponent;
            var fpsThermal = _cameraManager.ThermalComponent;

            if (nightVisionComponent == 0 || fpsThermal == 0)
            {
                Console.WriteLine("nvg or fps thermal component not found");
                Memory.CameraManager.UpdateCamera();
                return;
            }

            if (_nvgMaterial == 0UL)
            {
                _nvgMaterial = Memory.ReadPtrChain(nightVisionComponent, new uint[] { 0x90, 0x10, 0x8 });
            }

            if (_thermalMaterial == 0UL)
            {
                _thermalMaterial = Memory.ReadPtrChain(fpsThermal, new uint[] { 0x90, 0x10, 0x8 });
            }

            if (_nvgMaterial == 0UL || _thermalMaterial == 0UL)
            {
                Console.WriteLine("nvg or thermal material not found");
                return;
            }

            if (lastColor != _color)
            {
                lastColor = _color;
                Memory.WriteValue(nightVisionComponent + 0xD8, lastColor);
            }

            var players = this.AllPlayers
                          ?.Where(x => x.Value.IsAlive && x.Value.Type is not PlayerType.LocalPlayer && !Chams.PlayersWithChams.ContainsKey(x.Value.Base.ToString()))
                          .Select(x => x.Value)
                          .ToList();

            if (players is not null && players.Count > 0)
            {
                foreach (var player in players)
                {
                    try
                    {
                        string key = player.Base.ToString();

                        var materialTouse = player.IsHuman ? _nvgMaterial : _thermalMaterial;

                        this.SetPlayerBodyChams(player, materialTouse);
                        Chams.PlayersWithChams.TryAdd(key, player);
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
            var count = 1;
            var setAnyMaterial = false;
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

                    if (materialCount > 0 && materialCount < 5)
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

                            try
                            {
                                if (pMaterial == 0 || material == 0)
                                    continue;
                                
                                SavePointer(materialDictionaryBase + (0x50 * (uint)k), pMaterial, player);
                                Memory.WriteValue(materialDictionaryBase + (0x50 * (uint)k), material);
                                setAnyMaterial = true;
                            } catch
                            { continue; }
                        }
                    }
                }
            }

            return setAnyMaterial;
        }

        public void ChamsDisable()
        {
            RestorePointers();
        }

        private Dictionary<string, List<PointerBackup>> pointerBackups = new Dictionary<string, List<PointerBackup>>();

        private void SavePointer(ulong address, ulong originalValue, Player player)
        {
            var key = player.Base.ToString();

            if (!pointerBackups.ContainsKey(key))
            {
                pointerBackups[key] = new List<PointerBackup>();
            }

            pointerBackups[key].Add(new PointerBackup { Address = address, OriginalValue = originalValue });
        }

        public void RestorePointers()
        {
            foreach (var backups in pointerBackups.Values)
            {
                foreach (var backup in backups)
                {
                    Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);
                }
            }

            pointerBackups.Clear();
            PlayersWithChams.Clear();
        }

        public async Task RestorePointersForPlayerAsync(Player player)
        {
            var key = player.Base.ToString();

            if (pointerBackups.ContainsKey(key))
            {
                foreach (var backup in pointerBackups[key])
                {
                    Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);
                }

                pointerBackups.Remove(key);
                PlayersWithChams.Remove(key);
            }
        }
    }

    public struct PointerBackup
    {
        public ulong Address;
        public ulong OriginalValue;
    }
}