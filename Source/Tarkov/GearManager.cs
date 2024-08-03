using System.Collections.Concurrent;
using System.Numerics;

namespace eft_dma_radar
{
    public class GearManager
    {
        private static readonly ConcurrentBag<string> SLOTS_TO_SKIP = new ConcurrentBag<string> { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
        private static readonly ConcurrentBag<string> SLOTS_TO_SKIP_PVE = new ConcurrentBag<string> { "SecuredContainer", "Compass", "Eyewear", "ArmBand" };
        private static readonly ConcurrentBag<string> THERMAL_IDS = new ConcurrentBag<string> { "6478641c19d732620e045e17", "609bab8b455afd752b2e6138", "5c110624d174af029e69734c", "63fc44e2429a8a166c7f61e6", "5d1b5e94d7ad1a2b865a96b0", "606f2696f2cb2e02a42aceb1", "5a1eaa87fcdbcb001865f75e" };
        private static readonly ConcurrentBag<string> NVG_IDS = new ConcurrentBag<string> { "5b3b6e495acfc4330140bd88", "5a7c74b3e899ef0014332c29", "5c066e3a0db834001b7353f0", "5c0696830db834001d23f5da", "5c0558060db834001b735271", "57235b6f24597759bf5a30f1" };

        public static readonly Dictionary<string, string> GEAR_SLOT_NAMES = new(StringComparer.OrdinalIgnoreCase)
        {
            {"Headwear", "Head"},
            {"FaceCover", "Face"},
            {"ArmorVest", "Armor"},
            {"TacticalVest", "Vest"},
            {"Backpack", "Backpack"},
            {"FirstPrimaryWeapon", "Primary"},
            {"SecondPrimaryWeapon", "Secondary"},
            {"Holster", "Holster"},
            {"Scabbard", "Sheath"},
            {"Earpiece", "Earpiece"}
        };

        public static string GetGearSlotName(string key) => GEAR_SLOT_NAMES.TryGetValue(key, out var value) ? value : "n/a";

        public ConcurrentDictionary<string, GearItem> Gear { get; set; }

        public int Value { get; set; }
        public bool HasNVG { get; set; }
        public bool HasThermal { get; set; }

        private ConcurrentBag<string> _slotsToSkip;

        private ulong _slots { get; set; }

        public GearManager(ulong slots)
        {
            this._slots = slots;
            this.RefreshGear();
        }

        public void RefreshGear()
        {
            this._slotsToSkip = (Memory.IsPvEMode ? GearManager.SLOTS_TO_SKIP_PVE : GearManager.SLOTS_TO_SKIP);
            var gearItemMods = new List<LootItem>();
            var totalValue = 0;
            var slotDict = this.GetSlotDictionary(this._slots);
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
                var itemTemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var itemSlots = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Slots);
                var idPtr = round3.AddEntry<ulong>(i, 3, itemTemplate, null, Offsets.ItemTemplate.BsgId);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < slotDict.Count; i++)
            {
                try
                {
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                        continue;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var itemTemplate))
                        continue;
                    if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemSlots))
                        continue;
                    if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                        continue;

                    var id = Memory.ReadUnityString(idPtr);

                    if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                    {
                        string longName = lootItem.Item.name;
                        string shortName = lootItem.Item.shortName;
                        var tmpGearItemMods = new ConcurrentBag<LootItem>();
                        var totalGearValue = lootItem.Value;

                        var result = new PlayerGearInfo();
                        this.GetItemsInSlots(itemSlots, tmpGearItemMods, ref result);

                        totalGearValue += tmpGearItemMods.Sum(x => x.Value);
                        totalValue += tmpGearItemMods.Sum(x => x.Value);

                        var gear = new GearItem()
                        {
                            ID = id,
                            Long = longName,
                            Short = shortName,
                            Value = totalGearValue,
                            Item = lootItem,
                            Loot = tmpGearItemMods.ToList(),
                            GearInfo = result
                        };

                        gearDict.TryAdd(slotNames[i], gear);
                    }
                }
                catch { }
            }

            this.HasThermal = gearDict.Any(gear => gear.Value.Loot.Any(loot => GearManager.THERMAL_IDS.Contains(loot.ID)));
            this.HasNVG = gearDict.Any(gear => gear.Value.Loot.Any(loot => GearManager.NVG_IDS.Contains(loot.ID)));

            this.Value = totalValue;
            this.Gear = new(gearDict);
        }

        private void GetItemsInSlots(ulong itemSlots, ConcurrentBag<LootItem> loot, ref PlayerGearInfo result, int recurseDepth = 0)
        {
            if (itemSlots == 0 || recurseDepth > 3)
                return;

            var slotDict = this.GetSlotDictionary(itemSlots);

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
                var itemTemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var itemSlotsPtr = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Slots);
                var idPtr = round3.AddEntry<ulong>(i, 3, itemTemplate, null, Offsets.ItemTemplate.BsgId);

                if (slotNames[i] == "mod_magazine")
                {
                    var cartridges = round2.AddEntry<ulong>(i, 4, containedItem, null, Offsets.LootItemBase.Cartridges);
                    var cartridgeStack = round3.AddEntry<ulong>(i, 5, cartridges, null, Offsets.StackSlot.Items);
                    var cartridgeStackList = round4.AddEntry<ulong>(i, 6, cartridgeStack, null, Offsets.UnityList.Base);
                    var firstRoundItem = round5.AddEntry<ulong>(i, 7, cartridgeStackList, null, Offsets.UnityListBase.Start);
                    var firstRoundItemTemplate = round6.AddEntry<ulong>(i, 8, firstRoundItem, null, Offsets.LootItemBase.ItemTemplate);
                    var firstRoundIdPtr = round7.AddEntry<ulong>(i, 9, firstRoundItemTemplate, null, Offsets.ItemTemplate.BsgId);
                }
            }

            scatterReadMap.Execute();

            for (int i = 0; i < slotDict.Count; i++)
            {
                ProcessSlot(i, scatterReadMap, loot, ref result, recurseDepth, slotNames);
            }
        }

        private void ProcessSlot(int i, ScatterReadMap scatterReadMap, ConcurrentBag<LootItem> loot, ref PlayerGearInfo result, int recurseDepth, List<string> slotNames)
        {
            if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                return;
            if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemSlots))
                return;
            if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                return;

            var id = Memory.ReadUnityString(idPtr);

            if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
            {
                if (slotNames[i] == "mod_magazine")
                {
                    if (scatterReadMap.Results[i][9].TryGetResult<ulong>(out var firstRoundIdPtr))
                    {
                        var firstRoundId = Memory.ReadUnityString(firstRoundIdPtr);

                        if (TarkovDevManager.AllItems.TryGetValue(firstRoundId, out var firstRound))
                            result.AmmoType = firstRound.Item.shortName;
                    }
                }

                if (GearManager.THERMAL_IDS.Contains(id))
                    result.Thermal = lootItem.Item.shortName;

                if (GearManager.NVG_IDS.Contains(id))
                    result.NightVision = lootItem.Item.shortName;

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

            this.GetItemsInSlots(itemSlots, loot, ref result, recurseDepth + 1);
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

                for (int i = 0; i < size; i++)
                {
                    try
                    {
                        if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var slotPtr))
                            continue;
                        if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var namePtr))
                            continue;

                        var name = Memory.ReadUnityString(namePtr);

                        if (!this._slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                            slotDict[name] = slotPtr;
                    }
                    catch { continue; }
                }
            }
            catch { }

            return slotDict;
        }

        public string GetAmmoTypeFromWeapon(string weaponName)
        {
            var ammoType = FindAmmoTypeForWeapon(weaponName);

            if (string.IsNullOrEmpty(ammoType))
            {
                this.RefreshGear();
                ammoType = FindAmmoTypeForWeapon(weaponName);
            }

            return ammoType ?? "";
        }

        private string FindAmmoTypeForWeapon(string weaponName)
        {
            foreach (var gear in this.Gear.Values)
            {
                if (gear.Short.Contains(weaponName, StringComparison.OrdinalIgnoreCase))
                    return gear.GearInfo.AmmoType;
            }

            return null;
        }
    }
}
