using System.Collections.Concurrent;

namespace eft_dma_radar
{
    public class GearManager
    {
        private HashSet<string> _slotsToSkip;
        private static readonly HashSet<string> SLOTS_TO_SKIP = new HashSet<string> { "SecuredContainer", "Dogtag", "Compass", "ArmBand"};
        private static readonly HashSet<string> SLOTS_TO_SKIP_PVE = new HashSet<string> { "SecuredContainer", "Compass", "ArmBand" };
        private static readonly HashSet<string> THERMAL_IDS = new HashSet<string> { "6478641c19d732620e045e17", "609bab8b455afd752b2e6138", "5c110624d174af029e69734c", "63fc44e2429a8a166c7f61e6", "5d1b5e94d7ad1a2b865a96b0", "606f2696f2cb2e02a42aceb1", "5a1eaa87fcdbcb001865f75e" };
        private static readonly HashSet<string> NVG_IDS = new HashSet<string> { "5b3b6e495acfc4330140bd88", "5a7c74b3e899ef0014332c29", "5c066e3a0db834001b7353f0", "5c0696830db834001d23f5da", "5c0558060db834001b735271", "57235b6f24597759bf5a30f1" };

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
        
        private List<GearSlot> GearSlots { get; set; }
        private ulong _slots { get; set; }

        public List<Gear> GearItems { get; set; }

        public Gear ActiveWeapon { get; set; }

        private int _tempValue { get; set; }
        public int Value { get; set; }
        public bool HasNVG { get; set; }
        public bool HasThermal { get; set; }

        public GearManager(ulong slots)
        {
            this._slots = slots;
            this.GearItems = new List<Gear>();

            this.GetGearSlots();
            this.CheckGearSlots();
        }

        private void GetGearSlots()
        {
            this._slotsToSkip = (Memory.IsPvEMode ? GearManager.SLOTS_TO_SKIP_PVE : GearManager.SLOTS_TO_SKIP);

            this.GearSlots = this.GetSlotDictionary(this._slots);
        }

        public List<GearSlot> CheckGearSlots()
        {
            var size = this.GearSlots.Count;
            var slotsToRefresh = new List<GearSlot>();
            var tmpGearItems = new List<GearItem>();

            var scatterReadMap = new ScatterReadMap(size);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();

            for (int i = 0; i < size; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, this.GearSlots[i].Pointer, null, Offsets.Slot.ContainedItem);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < size; i++)
            {
                var slot = this.GearSlots[i];

                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem) ||
                    containedItem == 0 ||
                    !this.GearItems.Any(x => x.Pointer == containedItem) ||
                    this.GearItems.Where(x => x.Pointer == containedItem && x.Slot.Key == slot.Key).Count() == 0)
                {
                    var itemToRemove = this.GearItems.FirstOrDefault(x => x.Slot.Key == slot.Key);
                    this.GearItems.Remove(itemToRemove);

                    slotsToRefresh.Add(new GearSlot { Key = slot.Key, Pointer = slot.Pointer }); // update these gear slots
                }
            }

            if (slotsToRefresh.Count > 0)
            {
                for (int i = 0; i < slotsToRefresh.Count; i++)
                {
                    this.RefreshSlot(slotsToRefresh[i]);
                }
            }

            this.HasThermal = this.GearItems.Any(gear => gear.Item.Loot.Any(loot => GearManager.THERMAL_IDS.Contains(loot.ID)));
            this.HasNVG = this.GearItems.Any(gear => gear.Item.Loot.Any(loot => GearManager.NVG_IDS.Contains(loot.ID)));

            this.Value = this.GearItems.Sum(gear => gear.Item.TotalValue + (gear.Item.Loot.Sum(loot => loot.Value)));
            return slotsToRefresh;
        }

        private void RefreshSlot(GearSlot slot)
        {
            var count = 1;
            var scatterReadMap = new ScatterReadMap(count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, slot.Pointer, null, Offsets.Slot.ContainedItem);

                var itemTemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var itemSlots = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Slots);

                var bsgIDPtr = round3.AddEntry<ulong>(i, 3, itemTemplate, null, Offsets.ItemTemplate.MongoID + Offsets.MongoID.ID);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem) || containedItem == 0)
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var itemTemplate))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemSlots))
                    continue;
                if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                    continue;

                try
                {
                    var id = Memory.ReadUnityString(idPtr);

                    if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                    {
                        string longName = lootItem.Item.name;
                        string shortName = lootItem.Item.shortName;
                        var tmpGearItemMods = new ConcurrentBag<LootItem>();
                        var totalGearValue = lootItem.Value;

                        var result = new PlayerGearInfo();

                        if (slot.Key != "Scabbard")
                            this.GetItemsInSlots(itemSlots, tmpGearItemMods, ref result);

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

                        var gearItemRef = this.GearItems.FirstOrDefault(x => x.Slot.Key == slot.Key);

                        if (gearItemRef.Item is not null)
                        {
                            gearItemRef.Item = gear;
                        }
                        else
                        {
                            this.GearItems.Add(new Gear
                            {
                                Slot = slot,
                                Item = gear,
                                Pointer = containedItem
                            });
                        }
                    }
                }
                catch {}
            }
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
            var slotNames = slotDict.Select(x => x.Key).ToList();

            for (int i = 0; i < slotDict.Count; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, slotDict[i].Pointer, null, Offsets.Slot.ContainedItem);

                var itemTemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var itemSlotsPtr = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Slots);

                var bsgIDPtr = round3.AddEntry<ulong>(i, 3, itemTemplate, null, Offsets.ItemTemplate.MongoID + Offsets.MongoID.ID);

                if (slotNames[i] == "mod_magazine")
                {
                    var cartridges = round2.AddEntry<ulong>(i, 4, containedItem, null, Offsets.LootItemBase.Cartridges);
                    var cartridgeStack = round3.AddEntry<ulong>(i, 5, cartridges, null, Offsets.StackSlot.Items);
                    var cartridgeStackCount = round4.AddEntry<int>(i, 6, cartridgeStack, null, Offsets.UnityList.Count);
                    var cartridgeStackList = round4.AddEntry<ulong>(i, 7, cartridgeStack, null, Offsets.UnityList.Base);
                }
            }

            scatterReadMap.Execute();

            for (int i = 0; i < slotDict.Count; i++)
            {
                this.ProcessSlot(i, scatterReadMap, loot, ref result, recurseDepth, slotNames);
            }
        }

        private void ProcessSlot(int i, ScatterReadMap scatterReadMap, ConcurrentBag<LootItem> loot, ref PlayerGearInfo result, int recurseDepth, List<string> slotNames)
        {
            if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemSlots))
                return;
            if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                return;

            var id = Memory.ReadUnityString(idPtr);

            if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
            {
                if (slotNames[i] == "mod_magazine")
                {
                    if (!scatterReadMap.Results[i][6].TryGetResult<int>(out var cartridgeStackCount))
                        return;
                    if (!scatterReadMap.Results[i][7].TryGetResult<ulong>(out var cartridgeStackList))
                        return;
                    if (cartridgeStackCount < 1)
                        return;

                    var ammoCount = 0;
                    var ammoType = "";
                    var cartridgeStackBase = (cartridgeStackList + Offsets.UnityListBase.Start);

                    var ammoReadMap = new ScatterReadMap(cartridgeStackCount);
                    var round1 = ammoReadMap.AddRound();
                    var round2 = ammoReadMap.AddRound();
                    var round3 = ammoReadMap.AddRound();

                    for (int j = 0; j < cartridgeStackCount; j++)
                    {
                        var cartridgeBase = round1.AddEntry<ulong>(j, 0, cartridgeStackBase, null, ((uint)j * 0x8));

                        var cartridgeCount = round2.AddEntry<int>(j, 1, cartridgeBase, null, Offsets.Item.StackObjectsCount);
                        var cartridgeTemplate = round2.AddEntry<ulong>(j, 2, cartridgeBase, null, Offsets.LootItemBase.ItemTemplate);

                        var cartridgeBSGID = round3.AddEntry<ulong>(j, 3, cartridgeTemplate, null, Offsets.ItemTemplate.MongoID + Offsets.MongoID.ID);
                    }

                    ammoReadMap.Execute();

                    for (int j = 0; j < cartridgeStackCount; j++)
                    {
                        try
                        {
                            if (!ammoReadMap.Results[j][1].TryGetResult<int>(out var cartridgeCount))
                                return;

                            ammoCount += cartridgeCount;

                            if (!ammoReadMap.Results[j][3].TryGetResult<ulong>(out var cartridgeBSGID) || cartridgeBSGID == 0)
                                return;

                            var ammoID = Memory.ReadUnityString(cartridgeBSGID);

                            if (TarkovDevManager.AllItems.TryGetValue(ammoID, out var firstRound))
                                ammoType = firstRound.Item.shortName;
                        }
                        catch { }
                    }

                    result.AmmoType = ammoType;
                    result.AmmoCount = ammoCount;
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

        private List<GearSlot> GetSlotDictionary(ulong slotItemBase)
        {
            var slots = new List<GearSlot>();

            try
            {
                var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);

                if (size < 1 || size > 25)
                    return slots;

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
                            slots.Add(new GearSlot { Key = name, Pointer = slotPtr });
                    }
                    catch { continue; }
                }
            }
            catch { }

            return slots;
        }

        public void RefreshActiveWeaponAmmoInfo(ulong pointer)
        {
            if (this.GearItems is null)
                return;

            this.ActiveWeapon = this.GearItems.FirstOrDefault(x => x.Pointer == pointer);

            if (this.ActiveWeapon.Item is null)
                return;

            var count = 1;

            var scatterReadMap = new ScatterReadMap(count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();

            for (int i = 0; i < count; i ++)
            {
                var magSlotCache = round1.AddEntry<ulong>(i, 0, pointer, null, Offsets.WeaponItem.MagSlotCache);

                var containedItem = round2.AddEntry<ulong>(i, 1, magSlotCache, null, Offsets.Slot.ContainedItem);

                var cartridges = round3.AddEntry<ulong>(i, 2, containedItem, null, Offsets.LootItemBase.Cartridges);

                var cartridgeStack = round4.AddEntry<ulong>(i, 3, cartridges, null, Offsets.StackSlot.Items);

                var cartridgeStackList = round5.AddEntry<ulong>(i, 4, cartridgeStack, null, Offsets.UnityList.Base);
                var cartridgeStackCount = round5.AddEntry<int>(i, 5, cartridgeStack, null, Offsets.UnityList.Count);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var cartridgeStack))
                    return;
                if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var cartridgeStackList))
                    return;
                if (!scatterReadMap.Results[i][5].TryGetResult<int>(out var cartridgeStackCount))
                    return;

                var ammoCount = 0;
                var ammoType = "";

                if (cartridgeStackCount > 0)
                {
                    var cartridgeStackBase = (cartridgeStackList + Offsets.UnityListBase.Start);

                    var ammoReadMap = new ScatterReadMap(cartridgeStackCount);
                    var ammoRound1 = ammoReadMap.AddRound();
                    var ammoRound2 = ammoReadMap.AddRound();
                    var ammoRound3 = ammoReadMap.AddRound();

                    for (int j = 0; j < cartridgeStackCount; j++)
                    {
                        var cartridgeBase = ammoRound1.AddEntry<ulong>(j, 0, cartridgeStackBase, null, ((uint)j * 0x8));

                        var cartridgeCount = ammoRound2.AddEntry<int>(j, 1, cartridgeBase, null, Offsets.Item.StackObjectsCount);
                        var cartridgeTemplate = ammoRound2.AddEntry<ulong>(j, 2, cartridgeBase, null, Offsets.LootItemBase.ItemTemplate);

                        var cartridgeBSGID = ammoRound3.AddEntry<ulong>(j, 3, cartridgeTemplate, null, Offsets.ItemTemplate.MongoID + Offsets.MongoID.ID);
                    }

                    ammoReadMap.Execute();

                    for (int j = 0; j < cartridgeStackCount; j++)
                    {
                        try
                        {
                            if (!ammoReadMap.Results[j][1].TryGetResult<int>(out var cartridgeCount))
                                return;

                            ammoCount += cartridgeCount;

                            if (!ammoReadMap.Results[j][3].TryGetResult<ulong>(out var cartridgeBSGID) || cartridgeBSGID == 0)
                                return;

                            var ammoID = Memory.ReadUnityString(cartridgeBSGID);

                            if (TarkovDevManager.AllItems.TryGetValue(ammoID, out var firstRound))
                                ammoType = firstRound.Item.shortName;
                        }
                        catch { }
                    }
                }

                var newGearInfo = new PlayerGearInfo
                {
                    Thermal = this.ActiveWeapon.Item.GearInfo.Thermal,
                    NightVision = this.ActiveWeapon.Item.GearInfo.NightVision,
                    AmmoType = ammoType,
                    AmmoCount = ammoCount
                };

                this.ActiveWeapon.Item.GearInfo = newGearInfo;
            }
        }

        public struct PlayerGearInfo
        {
            public string Thermal;
            public string NightVision;
            public string AmmoType;
            public int AmmoCount;
        }

        public struct GearSlot
        {
            public string Key;
            public ulong Pointer;
            public GearItem GearItem;
        }

        public struct Gear
        {
            public GearSlot Slot;
            public GearItem Item;
            public ulong Pointer;
        }
    }
}
