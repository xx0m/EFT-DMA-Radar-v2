using System.Collections.ObjectModel;

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

        public void ChamsEnable(bool updateAllPlayers = false)
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
                return;
            }

            var nvgMaterial = Memory.ReadPtrChain(nightVisionComponent, new uint[] { 0x90, 0x10, 0x8 });
            var thermalMaterial = Memory.ReadPtrChain(fpsThermal, new uint[] { 0x90, 0x10, 0x8 });

            //var colorValuePMC = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
            //Memory.WriteValue<Vector4>(nvgMaterial + 0xD8, colorValuePMC);

            //var colorValuePMC = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
            //Memory.WriteValue<Vector4>(thermalMaterial + 0xD8, colorValuePMC);


            var players = this.AllPlayers
                ?.Where(x => x.Value.Type is not PlayerType.LocalPlayer)
                .Select(x => x.Value);

            if (!updateAllPlayers) // if we're not updating all players, only update the ones that don't have chams
                players = players?.Where(x => !Chams.PlayersWithChams.ContainsKey(x.Base.ToString()));

            if (players != null)
            {
                foreach (var player in players)
                {
                    try
                    {
                        string key = player.Base.ToString();
                        var material = (player.IsPMC ? nvgMaterial : thermalMaterial);

                        // only night vision can have rgba set
                        if (material == nvgMaterial)
                        {
                            var color = Extensions.Vector4FromPlayerPaintColor(player);
                            Memory.WriteValue(nightVisionComponent + 0xD8, color);
                        }

                        this.SetPlayerBodyChams(player, material);

                        if (updateAllPlayers)
                        {
                            if (Chams.PlayersWithChams.ContainsKey(key))
                            {
                                Chams.PlayersWithChams[key] = player;
                            }
                            else
                            {
                                Chams.PlayersWithChams.Add(key, player);
                            }
                        }
                        else
                        {
                            Chams.PlayersWithChams.TryAdd(key, player);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Chams - PlayerBodyChams -> {ex.Message}\nStackTrace:{ex.StackTrace}");
                    }
                }
            }
            else
            {
                Console.WriteLine("[Chams] - No players found");
            }
        }

        private bool SetPlayerBodyChams(Player player, ulong material)
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

                            Chams.SavePointer(materialDictionaryBase + (0x50 * (uint)k), pMaterial);

                            Memory.WriteValue(materialDictionaryBase + (0x50 * (uint)k), material);
                            setAnyMaterial = true;
                        }
                    }
                }
            }

            return setAnyMaterial;
        }

        private void PlayerGearChams(Player player, ulong material)
        {
            var slotViews = Memory.ReadPtr(player.PlayerBody + 0x58);
            if (slotViews == 0)
            {
                return;
            }
            var slotViewsList = Memory.ReadPtr(slotViews + 0x18);
            if (slotViewsList == 0)
            {
                return;
            }
            var slotViewsBase = Memory.ReadPtr(slotViewsList + 0x10);
            var slotViewsListSize = Memory.ReadValue<int>(slotViewsList + 0x18);
            if (slotViewsListSize > 0 && slotViewsListSize < 11)
            {
                for (int i = 0; i < slotViewsListSize; i++)
                {
                    var slotEntry = Memory.ReadPtr(slotViewsBase + 0x20 + (0x8 * (uint)i));
                    if (slotEntry == 0)
                    {
                        continue;
                    }
                    try
                    {
                        var dressesArray = Memory.ReadPtr(slotEntry + 0x40);
                        if (dressesArray == 0)
                        {
                            continue;
                        }
                        var dressesArraySize = Memory.ReadValue<int>(dressesArray + 0x18);
                        for (int j = 0; j < dressesArraySize; j++)
                        {
                            var dressesEntry = Memory.ReadPtr(dressesArray + 0x20 + (0x8 * (uint)j));
                            if (dressesEntry == 0)
                            {
                                continue;
                            }
                            var rendererArray = Memory.ReadPtr(dressesEntry + 0x28);
                            if (rendererArray == 0)
                            {
                                continue;
                            }
                            var rendererArraySize = Memory.ReadValue<int>(rendererArray + 0x18);
                            for (int k = 0; k < rendererArraySize; k++)
                            {
                                var rendererEntry = Memory.ReadPtr(rendererArray + 0x20 + (0x8 * (uint)k));
                                if (rendererEntry == 0)
                                {
                                    continue;
                                }
                                var gMaterials = Memory.ReadPtr(rendererEntry + 0x10);
                                var gMaterialCount = Memory.ReadValue<int>(gMaterials + 0x158);
                                if (gMaterialCount > 0 && gMaterialCount < 10)
                                {
                                    var gMaterialDictionaryBase = Memory.ReadPtr(gMaterials + 0x148);
                                    for (int l = 0; l < gMaterialCount; l++)
                                    {
                                        try
                                        {
                                            var gMaterial = Memory.ReadPtr(gMaterialDictionaryBase + (0x50 * (uint)l));
                                            Chams.SavePointer(gMaterialDictionaryBase + (0x50 * (uint)l), gMaterial);
                                            Memory.WriteValue(gMaterialDictionaryBase + (0x50 * (uint)l), material);
                                            continue;
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }

        //this should be not be callable if material cache is empty
        public void ChamsDisable()
        {
            RestorePointers(); // works for 0 pointers but not for other materials
        }

        private static List<PointerBackup> pointerBackups = new List<PointerBackup>();

        private static void SavePointer(ulong address, ulong originalValue)
        {
            pointerBackups.Add(new PointerBackup { Address = address, OriginalValue = originalValue });
        }

        public static void RestorePointers()
        {
            foreach (var backup in pointerBackups)
            {
                Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);
            }
            pointerBackups.Clear();
        }
    }

    public struct PointerBackup
    {
        public ulong Address;
        public ulong OriginalValue;
    }
}