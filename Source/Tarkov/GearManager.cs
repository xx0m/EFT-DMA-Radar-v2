using Offsets;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;
using static System.Reflection.Metadata.BlobBuilder;

namespace eft_dma_radar
{
    public class GearManager
    {
        private static readonly List<string> _weaponSlots = new List<string> { "FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster" };
        private static readonly List<string> _skipSlots = new List<string> { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
        private static readonly List<string> _skipSlotsPmc = new List<string> { "Scabbard", "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
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
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            int size;
            size = Memory.ReadValue<int>(slots + Offsets.UnityList.Count);
            if (size == 0 || slots == 0) return;
            for (int slotID = 0; slotID < size; slotID++)
            {
                var slotPtr = Memory.ReadPtr(slots + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                var name = Memory.ReadUnityString(namePtr);
                slotDict.TryAdd(name, slotPtr);
            }
            var gearDict = new Dictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var slotName in slotDict.Keys)
            {
                if (GearManager._skipSlots.Contains(slotName, StringComparer.OrdinalIgnoreCase))
                    continue;

                try
                {
                    if (slotDict.TryGetValue(slotName, out var slot))
                    {
                        var containedItem = Memory.ReadPtr(slot + Offsets.Slot.ContainedItem);
                        var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                        var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                        var id = Memory.ReadUnityString(idPtr);

                        if (TarkovDevManager.AllItems.TryGetValue(id, out var lootItem))
                        {
                            string longName = lootItem.Item.name;
                            string shortName = lootItem.Item.shortName;
                            bool hasThermal = false;
                            string extraSlotInfo = null;
                            var tmpGearItemMods = new List<LootItem>();
                            var totalGearValue = TarkovDevManager.GetItemValue(lootItem.Item);

                            if (GearManager._weaponSlots.Contains(slotName, StringComparer.OrdinalIgnoreCase)) // Only interested in weapons
                            {
                                try
                                {
                                    var result = new PlayerWeaponInfo();
                                    this.RecurseSlotsForThermalsAmmo(containedItem, ref result); // Check weapon ammo type, and if it contains a thermal scope
                                    extraSlotInfo = result.ToString();
                                    hasThermal = result.ThermalScope != null;
                                }
                                catch { }
                            }

                            GearManager.GetItemsInSlots(containedItem, id, tmpGearItemMods);

                            totalGearValue += tmpGearItemMods.Sum(x => TarkovDevManager.GetItemValue(x.Item));
                            totalValue += tmpGearItemMods.Sum(x => TarkovDevManager.GetItemValue(x.Item));
                            gearItemMods.AddRange(tmpGearItemMods);

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

                            gearDict.TryAdd(slotName, gear);
                        }
                        else
                        {
                            Debug.WriteLine($"GearManager: ID: {id} not found in TarkovDevManager.AllItems");
                        }
                    }
                }
                catch { }
            }

            this.Value = totalValue;
            this.GearItemMods = new(gearItemMods);
            this.Gear = new(gearDict);
        }

        /// <summary>
        /// Checks a 'Primary' weapon for Ammo Type, and Thermal Scope.
        /// </summary>
        private void RecurseSlotsForThermalsAmmo(ulong lootItemBase, ref PlayerWeaponInfo result)
        {
            //Debug.WriteLine($"GearManager Scope: Starting...");
            try
            {
                var parentSlots = Memory.ReadPtr(lootItemBase + Offsets.LootItemBase.Slots);
                var size = Memory.ReadValue<int>(parentSlots + Offsets.UnityList.Count);
                var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

                for (int slotID = 0; slotID < size; slotID++)
                {
                    var slotPtr = Memory.ReadPtr(parentSlots + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                    var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                    var name = Memory.ReadUnityString(namePtr);
                    if (_skipSlots.Contains(name, StringComparer.OrdinalIgnoreCase))
                        continue;
                    slotDict.TryAdd(name, slotPtr);
                }
                foreach (var slotName in slotDict.Keys)
                {
                    try
                    {
                        if (slotDict.TryGetValue(slotName, out var slot))
                        {
                            var containedItem = Memory.ReadPtr(slot + Offsets.Slot.ContainedItem);
                            if (slotName == "mod_magazine") // Magazine slot - Check for ammo!
                            {
                                var cartridge = Memory.ReadPtr(containedItem + Offsets.LootItemBase.Cartridges);
                                var cartridgeStack = Memory.ReadPtr(cartridge + Offsets.StackSlot.Items);
                                var cartridgeStackList = Memory.ReadPtr(cartridgeStack + Offsets.UnityList.Base);
                                var firstRoundItem = Memory.ReadPtr(cartridgeStackList + Offsets.UnityListBase.Start + 0); // Get first round in magazine
                                var firstRoundItemTemplate = Memory.ReadPtr(firstRoundItem + Offsets.LootItemBase.ItemTemplate);
                                var firstRoundIdPtr = Memory.ReadPtr(firstRoundItemTemplate + Offsets.ItemTemplate.BsgId);
                                var firstRoundId = Memory.ReadUnityString(firstRoundIdPtr);
                                if (TarkovDevManager.AllItems.TryGetValue(firstRoundId, out var firstRound)) // Lookup ammo type
                                {
                                    result.AmmoType = firstRound.Item.shortName;
                                }
                            }
                            else // Not a magazine, keep recursing for a scope
                            {
                                var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                                var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                                var id = Memory.ReadUnityString(idPtr);
                                if (_thermalScopes.Contains(id))
                                {
                                    if (TarkovDevManager.AllItems.TryGetValue(id, out var entry))
                                    {
                                        result.ThermalScope = entry.Item.shortName;
                                    }
                                }
                                RecurseSlotsForThermalsAmmo(containedItem, ref result);
                            }
                        }
                    }
                    catch { } // Skip over empty slots
                }
            }
            catch
            {
            }
        }

        private static void GetItemsInSlots(ulong slotItemBase, string id, List<LootItem> loot)
        {
            var parentSlots = Memory.ReadPtr(slotItemBase + Offsets.LootItemBase.Slots);
            var size = Memory.ReadValue<int>(parentSlots + Offsets.UnityList.Count);
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            for (int slotID = 0; slotID < size; slotID++)
            {
                var slotPtr = Memory.ReadPtr(parentSlots + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                var name = Memory.ReadUnityString(namePtr);
                if (_skipSlots.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;
                slotDict.TryAdd(name, slotPtr);
            }

            if (size == 0 || parentSlots == 0)
                return;

            foreach (var slotName in slotDict.Keys)
            {
                try
                {
                    if (slotDict.TryGetValue(slotName, out var slot))
                    {
                        var containedItem = Memory.ReadPtr(slot + Offsets.Slot.ContainedItem);
                        var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                        var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                        var newID = Memory.ReadUnityString(idPtr);

                        if (TarkovDevManager.AllItems.TryGetValue(newID, out LootItem lootItem))
                        {
                            loot.Add(new LootItem
                            {
                                ID = newID,
                                Name = lootItem.Name,
                                AlwaysShow = lootItem.AlwaysShow,
                                Important = lootItem.Important,
                                Item = lootItem.Item,
                                Value = TarkovDevManager.GetItemValue(lootItem.Item)
                            });
                        }

                        GearManager.GetItemsInSlots(containedItem, newID, loot);
                    }
                } catch {}
            }
        }
    }
}
