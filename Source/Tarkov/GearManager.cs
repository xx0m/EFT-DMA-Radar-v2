using System.Collections.Concurrent;
using System.Numerics;

namespace eft_dma_radar
{
    public class GearManager
    {
        private static readonly ConcurrentBag<string> slotsToSkip = new ConcurrentBag<string> { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
        private static readonly ConcurrentBag<string> slotsToSkipPvE = new ConcurrentBag<string> { "SecuredContainer", "Compass", "Eyewear", "ArmBand" };
        private static readonly ConcurrentBag<string> thermalIDs = new ConcurrentBag<string> { "5a1eaa87fcdbcb001865f75e", "5d1b5e94d7ad1a2b865a96b0", "63fc44e2429a8a166c7f61e6", "6478641c19d732620e045e17", "63fc44e2429a8a166c7f61e6" };

        public static string GetGearSlotName(string key) => key switch
        {
            "Headwear" => "Head",
            "FaceCover" => "Face",
            "ArmorVest" => "Armor",
            "TacticalVest" => "Vest",
            "Backpack" => "Backpack",
            "FirstPrimaryWeapon" => "Primary",
            "SecondPrimaryWeapon" => "Secondary",
            "Holster" => "Holster",
            "Scabbard" => "Sheath",
            "Earpiece" => "Earpiece",
            _ => "n/a"
        };

        public ConcurrentDictionary<string, GearItem> Gear { get; set; }

        public int Value { get; set; }

        public ConcurrentBag<LootItem> GearItemMods { get; set; }

        public GearManager(ulong slots)
        {
            var gearItemMods = new List<LootItem>();
            var totalValue = 0;
            var slotDict = this.GetSlotDictionary(slots);
            var gearDict = new ConcurrentDictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);

            var scatterReadMap = new ScatterReadMap(slotDict.Count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            var slotNames = slotDict.Keys.ToList();
            var slotPtrs = slotDict.Values.ToList();

            for (int i = 0; i < slotDict.Count; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, slotPtrs[i], null, Offsets.Slot.ContainedItem);
                var inventorytemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var parentSlot = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Slots);
                var idPtr = round3.AddEntry<ulong>(i, 3, inventorytemplate, null, Offsets.ItemTemplate.BsgId);
            }

            scatterReadMap.Execute();

            var task = Task.Run(() =>
            {
                Parallel.For(0, slotDict.Count, Program.Config.ParallelOptions, i =>
                {
                    try
                    {
                        if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                            return;
                        if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                            return;
                        if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var parentSlot))
                            return;
                        if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                            return;

                        var id = Memory.ReadUnityString(idPtr);

                        if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                        {
                            string longName = lootItem.Item.name;
                            string shortName = lootItem.Item.shortName;
                            var tmpGearItemMods = new ConcurrentBag<LootItem>();
                            var totalGearValue = lootItem.Value;

                            var result = new PlayerWeaponInfo();
                            this.GetItemsInSlots(parentSlot, tmpGearItemMods, ref result);

                            totalGearValue += tmpGearItemMods.Sum(x => x.Value);
                            totalValue += tmpGearItemMods.Sum(x => x.Value);
                            gearItemMods.AddRange(tmpGearItemMods);

                            var extraSlotInfo = result.ToString();
                            var hasThermal = result.ThermalScope is not null;

                            if (extraSlotInfo is not null)
                            {
                                longName += $" ({extraSlotInfo})";
                                shortName += $" ({extraSlotInfo})";
                            }

                            var gear = new GearItem()
                            {
                                ID = id,
                                Long = longName,
                                Short = shortName,
                                Value = totalGearValue,
                                HasThermal = hasThermal,
                                Item = lootItem
                            };

                            gearDict.TryAdd(slotNames[i], gear);
                        }
                    } catch { return; }
                });
            });

            task.Wait();

            this.Value = totalValue;
            this.GearItemMods = new(gearItemMods);
            this.Gear = new(gearDict);
        }

        private void GetItemsInSlots(ulong slotItemBase, ConcurrentBag<LootItem> loot, ref PlayerWeaponInfo result, int recurseDepth = 0)
        {
            if (slotItemBase == 0 || recurseDepth > 3)
                return;

            var slotDict = this.GetSlotDictionary(slotItemBase);

            if (slotDict is null || slotDict.Count == 0)
                return;

            var scatterReadMap = new ScatterReadMap(slotDict.Count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();
            var round6 = scatterReadMap.AddRound();
            var round7 = scatterReadMap.AddRound();

            var slotNames = slotDict.Keys.ToList();
            var slotPtrs = slotDict.Values.ToList();

            for (int i = 0; i < slotDict.Count; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, slotPtrs[i], null, Offsets.Slot.ContainedItem);
                var inventorytemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var idPtr = round3.AddEntry<ulong>(i, 2, inventorytemplate, null, Offsets.ItemTemplate.BsgId);

                if (slotNames[i] == "mod_magazine")
                {
                    var cartridges = round2.AddEntry<ulong>(i, 3, containedItem, null, Offsets.LootItemBase.Cartridges);
                    var cartridgeStack = round3.AddEntry<ulong>(i, 4, cartridges, null, Offsets.StackSlot.Items);
                    var cartridgeStackList = round4.AddEntry<ulong>(i, 5, cartridgeStack, null, Offsets.UnityList.Base);
                    var firstRoundItem = round5.AddEntry<ulong>(i, 6, cartridgeStackList, null, Offsets.UnityListBase.Start + 0);
                    var firstRoundItemTemplate = round6.AddEntry<ulong>(i, 7, firstRoundItem, null, Offsets.LootItemBase.ItemTemplate);
                    var firstRoundIdPtr = round7.AddEntry<ulong>(i, 8, firstRoundItemTemplate, null, Offsets.ItemTemplate.BsgId);
                }
            }

            scatterReadMap.Execute();

            for (int i = 0; i < slotDict.Count; i++)
            {
                ProcessSlot(i, scatterReadMap, loot, ref result, recurseDepth, slotNames);
            }
        }

        private void ProcessSlot(int i, ScatterReadMap scatterReadMap, ConcurrentBag<LootItem> loot, ref PlayerWeaponInfo result, int recurseDepth, List<string> slotNames)
        {
            if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                return;
            if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                return;
            if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var idPtr))
                return;

            var id = Memory.ReadUnityString(idPtr);

            if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
            {
                if (slotNames[i] == "mod_magazine")
                {
                    if (scatterReadMap.Results[i][8].TryGetResult<ulong>(out var firstRoundIdPtr))
                    {
                        var firstRoundId = Memory.ReadUnityString(firstRoundIdPtr);

                        if (TarkovDevManager.AllItems.TryGetValue(firstRoundId, out var firstRound))
                        {
                            result.AmmoType = firstRound.Item.shortName;
                        }
                    }
                }

                if (GearManager.thermalIDs.Contains(id))
                {
                    result.ThermalScope = lootItem.Item.shortName;
                }

                var newLootItem = new LootItem
                {
                    ID = id,
                    Name = lootItem.Item.name,
                    AlwaysShow = lootItem.AlwaysShow,
                    Important = lootItem.Important,
                    Item = lootItem.Item,
                    Value = lootItem.Value
                };

                loot.Add(newLootItem);
            }

            this.GetItemsInSlots(containedItem, loot, ref result, recurseDepth + 1);
        }

        private ConcurrentDictionary<string, ulong> GetSlotDictionary(ulong slotItemBase)
        {
            var slotDict = new ConcurrentDictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);

                if (size < 1 || size > 25)
                    size = Math.Clamp(size, 0, 25);

                var scatterReadMap = new ScatterReadMap(size);
                var round1 = scatterReadMap.AddRound();
                var round2 = scatterReadMap.AddRound();

                var slotItemBaseStart = slotItemBase + Offsets.UnityListBase.Start;

                for (int i = 0; i < size; i++)
                {
                    var slotPtr = round1.AddEntry<ulong>(i, 0, slotItemBaseStart, null, (uint)i * Offsets.Slot.Size);
                    var namePtr = round2.AddEntry<ulong>(i, 1, slotPtr, null, Offsets.Slot.Name);
                }

                scatterReadMap.Execute();

                var task = Task.Run(() =>
                {
                    Parallel.For(0, size, Program.Config.ParallelOptions, i =>
                    {
                        try
                        {
                            if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var slotPtr))
                                return;
                            if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var namePtr))
                                return;

                            var name = Memory.ReadUnityString(namePtr);

                            var slotsToSkip = (Memory.IsPvEMode ? GearManager.slotsToSkipPvE : GearManager.slotsToSkip);

                            if (!slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                                slotDict[name] = slotPtr;
                        }
                        catch { return; }
                    });
                });

                task.Wait();
            }
            catch { }

            return slotDict;
        }
    }
}
