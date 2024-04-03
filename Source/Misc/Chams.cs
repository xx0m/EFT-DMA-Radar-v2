using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eft_dma_radar.Source.Tarkov;

namespace eft_dma_radar
{

    public class Chams
    {

        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }

        public Chams()
        {

        }

        public void ClothingChams(ulong playerBody)
        {

            if (playerBody == 0)
            {
                return;
            }
            var bodySkins = Memory.ReadPtr(playerBody + 0x40);
            if (bodySkins == 0)
            {
                return;
            }
            var bodySkinsCount = Memory.ReadValue<int>(bodySkins + 0x40);
            var skinEntries = Memory.ReadPtr(bodySkins + 0x18);
            if (skinEntries == 0)
            {
                return;
            }
            for (int i = 0; i < bodySkinsCount; i++)
            {
                var pBodySkins = Memory.ReadPtr(skinEntries + 0x30 + (0x18 * (uint)i));
                if (pBodySkins == 0)
                {
                    continue;
                }
                var pLodsArray = Memory.ReadPtr(pBodySkins + 0x18);
                if (pLodsArray == 0)
                {
                    continue;
                }
                var lodsCount = Memory.ReadValue<int>(pLodsArray + 0x18);

                for (int j = 0; j < lodsCount; j++)
                {
                    var pLodEntry = Memory.ReadPtr(pLodsArray + 0x20 + (0x8 * (uint)j));
                    if (j == 1)
                    {
                        pLodEntry = Memory.ReadPtr(pLodEntry + 0x20);
                    }
                    if (pLodEntry == 0)
                    {
                        continue;
                    }
                    var SkinnedMeshRenderer = Memory.ReadPtr(pLodEntry + 0x20);
                    if (SkinnedMeshRenderer == 0)
                    {
                        continue;
                    }
                    var pMaterialDictionary = Memory.ReadPtr(SkinnedMeshRenderer + 0x10);
                    var MaterialCount = Memory.ReadValue<int>(pMaterialDictionary + 0x158);

                    if (MaterialCount > 0 && MaterialCount < 5)
                    {
                        var MaterialDictionaryBase = Memory.ReadPtr(pMaterialDictionary + 0x148);
                        for (int k = 0; k < MaterialCount; k++)
                        {
                            try
                            {

                                var MaterialEntryPtr = Memory.ReadPtr(MaterialDictionaryBase + (0x50 * (uint)k));
                                //Console.WriteLine($"MaterialEntryPtr: {MaterialEntryPtr} {playerNick}");
                                SavePointer(MaterialDictionaryBase + (0x50 * (uint)k), MaterialEntryPtr);
                                //Memory.WriteValue<ulong>(MaterialDictionaryBase + (0x50 * (uint)k), 0);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        public void GearChams(ulong playerBody)
        {
            if (playerBody == 0)
            {
                return;
            }
            var SlotViews = Memory.ReadPtr(playerBody + 0x58);
            if (SlotViews == 0)
            {
                return;
            }
            var SlotViewsList = Memory.ReadPtr(SlotViews + 0x18);
            if (SlotViewsList == 0)
            {
                return;
            }
            var pList = Memory.ReadPtr(SlotViewsList + 0x10);
            var SlotViewsListSize = Memory.ReadValue<int>(SlotViewsList + 0x20);
            for (int i = 0; i < SlotViewsListSize; i++)
            {
                var pEntry = Memory.ReadPtr(pList + 0x20 + (0x8 * (uint)i));
                if (pEntry == 0)
                {
                    continue;
                }
                var DressesArray = Memory.ReadPtr(pEntry + 0x40);
                if (DressesArray == 0)
                {
                    continue;
                }
                var DressesArraySize = Memory.ReadValue<int>(DressesArray + 0x20);
                for (int j = 0; j < DressesArraySize; j++)
                {
                    var DressesEntry = Memory.ReadPtr(DressesArray + 0x20 + (0x8 * (uint)j));
                    if (DressesEntry == 0)
                    {
                        continue;
                    }
                    var RendererArray = Memory.ReadPtr(DressesEntry + 0x28);
                    if (RendererArray == 0)
                    {
                        continue;
                    }
                    var RendererArraySize = Memory.ReadValue<int>(RendererArray + 0x20);
                    for (int k = 0; k < RendererArraySize; k++)
                    {
                        var RendererEntry = Memory.ReadPtr(RendererArray + 0x20 + (0x8 * (uint)k));
                        if (RendererEntry == 0)
                        {
                            continue;
                        }
                        var pMaterialDict = Memory.ReadPtr(RendererEntry + 0x10);
                        if (pMaterialDict == 0)
                        {
                            continue;
                        }
                        var MaterialCount = Memory.ReadValue<int>(pMaterialDict + 0x158);
                        if (MaterialCount > 0 && MaterialCount < 6)
                        {
                            var MaterialDictionaryBase = Memory.ReadPtr(pMaterialDict + 0x148);
                            for (int l = 0; l < MaterialCount; l++)
                            {
                                var MaterialEntryPtr = Memory.ReadPtr(MaterialDictionaryBase + (0x50 * (uint)k));
                                //Console.WriteLine($"MaterialEntryPtr: {MaterialEntryPtr}");
                                SavePointer(MaterialDictionaryBase + (0x50 * (uint)k), MaterialEntryPtr);
                                //Memory.WriteValue<ulong>(MaterialDictionaryBase + (0x50 * (uint)k), 0);
                            }
                        }
                    }
                }
            }
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
                //Memory.WriteValue<ulong>(backup.Address, backup.OriginalValue);
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