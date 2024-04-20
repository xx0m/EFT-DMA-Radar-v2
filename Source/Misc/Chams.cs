using System;
using System.Collections.Generic;
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

        public void ChamsEnable()
        {
            if (!this.InGame)
            {
                Console.WriteLine("Not in game");
                return;
            }
            else
            {
                ulong nvgMaterial = _cameraManager.NightVisionMaterial;

                if (nvgMaterial == 0)
                {
                    Console.WriteLine("[Chams] - Failed to get NVG material");
                    return;
                }

                var players = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.Type is not PlayerType.LocalPlayer && !x.HasExfild);

                if (players != null)
                {
                    foreach (var player in players)
                    {
                        if (player.Type == PlayerType.AIOfflineScav || player.Type == PlayerType.AIScav || player.Type == PlayerType.USEC || player.Type == PlayerType.BEAR)
                        {
                            var bodySkins = Memory.ReadPtrNullable(player.PlayerBody + 0x40);
                            if (bodySkins == 0)
                            {
                                continue;
                            }
                            var bodySkinsCount = Memory.ReadValue<int>(bodySkins + 0x40);
                            var skinEntries = Memory.ReadPtrNullable(bodySkins + 0x18);
                            if (skinEntries == 0)
                            {
                                continue;
                            }
                            for (int i = 0; i < bodySkinsCount; i++)
                            {
                                var pBodySkins = Memory.ReadPtrNullable(skinEntries + 0x30 + (0x18 * (uint)i));
                                if (pBodySkins == 0)
                                {
                                    continue;
                                }
                                var pLodsArray = Memory.ReadPtrNullable(pBodySkins + 0x18);
                                if (pLodsArray == 0)
                                {
                                    continue;
                                }
                                var lodsCount = Memory.ReadValue<int>(pLodsArray + 0x18);
                                for (int j = 0; j < lodsCount; j++)
                                {
                                    var pLodEntry = Memory.ReadPtrNullable(pLodsArray + 0x20 + (0x8 * (uint)j));
                                    if (j == 1)
                                    {
                                        pLodEntry = Memory.ReadPtrNullable(pLodEntry + 0x20);
                                    }
                                    if (pLodEntry == 0)
                                    {
                                        continue;
                                    }
                                    var SkinnedMeshRenderer = Memory.ReadPtrNullable(pLodEntry + 0x20);
                                    if (SkinnedMeshRenderer == 0)
                                    {
                                        continue;
                                    }
                                    var pMaterialDictionary = Memory.ReadPtrNullable(SkinnedMeshRenderer + 0x10);
                                    var MaterialCount = Memory.ReadValue<int>(pMaterialDictionary + 0x158);

                                    if (MaterialCount > 0 && MaterialCount < 5)
                                    {
                                        var MaterialDictionaryBase = Memory.ReadPtrNullable(pMaterialDictionary + 0x148);
                                        for (int k = 0; k < MaterialCount; k++)
                                        {
                                            try
                                            {
                                                //Memory.WriteValue(MaterialDictionaryBase + (0x50 * (uint)k), nvgMaterial);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }

                            //gear
                            var slotViews = Memory.ReadPtrNullable(player.PlayerBody + 0x58);
                            if (slotViews == 0)
                            {
                                continue;
                            }
                            var slotViewsList = Memory.ReadPtrNullable(slotViews + 0x18);
                            if (slotViewsList == 0)
                            {
                                continue;
                            }
                            var slotViewsBase = Memory.ReadPtrNullable(slotViewsList + 0x10);
                            var slotViewsListSize = Memory.ReadValue<int>(slotViewsList + 0x18);
                            for (int i = 0; i < slotViewsListSize; i++)
                            {
                                var slotEntry = Memory.ReadPtrNullable(slotViewsBase + 0x20 + (0x8 * (uint)i));
                                if (slotEntry == 0)
                                {
                                    continue;
                                }
                                try
                                {
                                    var dressesArray = Memory.ReadPtrNullable(slotEntry + 0x40);
                                    if (dressesArray == 0)
                                    {
                                        continue;
                                    }
                                    var dressesArraySize = Memory.ReadValue<int>(dressesArray + 0x18);
                                    for (int j = 0; j < dressesArraySize; j++)
                                    {
                                        var dressesEntry = Memory.ReadPtrNullable(dressesArray + 0x20 + (0x8 * (uint)j));
                                        if (dressesEntry == 0)
                                        {
                                            continue;
                                        }
                                        var rendererArray = Memory.ReadPtrNullable(dressesEntry + 0x28);
                                        if (rendererArray == 0)
                                        {
                                            continue;
                                        }
                                        var rendererArraySize = Memory.ReadValue<int>(rendererArray + 0x18);
                                        for (int k = 0; k < rendererArraySize; k++)
                                        {
                                            var rendererEntry = Memory.ReadPtrNullable(rendererArray + 0x20 + (0x8 * (uint)k));
                                            if (rendererEntry == 0)
                                            {
                                                continue;
                                            }
                                            var pMaterialDict = Memory.ReadPtrNullable(rendererEntry + 0x10);
                                            if (pMaterialDict == 0)
                                            {
                                                continue;
                                            }
                                            var gMaterialCount = Memory.ReadValue<int>(pMaterialDict + 0x158);
                                            if (gMaterialCount > 0 && gMaterialCount < 5)
                                            {
                                                var gMaterialDictBase = Memory.ReadPtrNullable(pMaterialDict + 0x148);
                                                for (int l = 0; l < gMaterialCount; l++)
                                                {
                                                    try
                                                    {
                                                        //Memory.WriteValue(gMaterialDictBase + (0x50 * (uint)l), nvgMaterial);
                                                    }
                                                    catch { continue; }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[Chams] - No players found");
                }
            }
        }

        //this should be not be callable if material cache is empty
        public void ChamsDisable()
        {

        }
    }
}