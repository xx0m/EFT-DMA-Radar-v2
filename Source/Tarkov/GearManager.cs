namespace eft_dma_radar
{
    public class GearManager
    {
        private static readonly List<string> _skipSlots = new List<string> { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
        private static readonly List<string> _thermalScopes = new List<string> { "5a1eaa87fcdbcb001865f75e", "5d1b5e94d7ad1a2b865a96b0", "63fc44e2429a8a166c7f61e6", "6478641c19d732620e045e17", "63fc44e2429a8a166c7f61e6" };
        /// <summary>
        /// List of equipped items in PMC Inventory Slots.
        /// </summary>
        public Dictionary<string, GearItem> Gear { get; set; }

        /// <summary>
        /// Total value of all equipped items.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// All gear items and mods.
        /// </summary>
        public List<LootItem> GearItemMods { get; set; }

        public GearManager(ulong slots)
        {
            var gearItemMods = new List<LootItem>();
            var totalValue = 0;
            var slotDict = this.GetSlotDictionary(slots);
            var gearDict = new Dictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);

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

            for (int i = 0; i < slotDict.Count; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var parentSlot))
                    continue;
                if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var idPtr))
                    continue;

                var id = Memory.ReadUnityString(idPtr);

                if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                {
                    string longName = lootItem.Item.name;
                    string shortName = lootItem.Item.shortName;
                    var tmpGearItemMods = new List<LootItem>();
                    var totalGearValue = TarkovDevManager.GetItemValue(lootItem.Item);

                    var result = new PlayerWeaponInfo();
                    this.GetItemsInSlots(parentSlot, tmpGearItemMods, ref result);

                    totalGearValue += tmpGearItemMods.Sum(x => TarkovDevManager.GetItemValue(x.Item));
                    totalValue += tmpGearItemMods.Sum(x => TarkovDevManager.GetItemValue(x.Item));
                    gearItemMods.AddRange(tmpGearItemMods);

                    var extraSlotInfo = result.ToString();
                    var hasThermal = result.ThermalScope != null;

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
                        HasThermal = hasThermal
                    };

                    gearDict.TryAdd(slotNames[i], gear);
                }
            }

            this.Value = totalValue;
            this.GearItemMods = new(gearItemMods);
            this.Gear = new(gearDict);
        }

        private void GetItemsInSlots(ulong slotItemBase, List<LootItem> loot, ref PlayerWeaponInfo result, int recurseDepth = 0)
        {
            if (slotItemBase == 0 || recurseDepth > 3)
                return;

            var slotDict = this.GetSlotDictionary(slotItemBase);

            if (slotDict == null || slotDict.Count == 0)
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
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var idPtr))
                    continue;

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

                var id = Memory.ReadUnityString(idPtr);

                if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                {
                    if (GearManager._thermalScopes.Contains(id))
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
                        Value = TarkovDevManager.GetItemValue(lootItem.Item)
                    };

                    loot.Add(newLootItem);
                }

                this.GetItemsInSlots(containedItem, loot, ref result, recurseDepth + 1);
            }
        }

        private Dictionary<string, ulong> GetSlotDictionary(ulong slotItemBase)
        {
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);

                if (size < 1 || size > 25)
                {
                    size = Math.Clamp(size, 0, 25);
                }

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
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var slotPtr))
                        continue;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var namePtr))
                        continue;

                    var name = Memory.ReadUnityString(namePtr);

                    if (!GearManager._skipSlots.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        slotDict[name] = slotPtr;
                    }
                }
            }
            catch { }

            return slotDict;
        }
    }
}
