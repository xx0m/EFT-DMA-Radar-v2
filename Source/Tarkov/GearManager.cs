using Offsets;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;

namespace eft_dma_radar
{
    public class GearManager
    {
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
        public int TotalValue { get; set; }

        /// <summary>
        /// All gear items and mods.
        /// </summary>
        public List<LootItem> GearItemsAndMods { get; set; }

        public GearManager(ulong playerBase, bool isPMC, bool isLocal)
        {
            this.GearItemsAndMods = new List<LootItem>();
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            var inventorycontroller = 0UL;

            if (isLocal)
            {
                inventorycontroller = Memory.ReadPtr(playerBase + Offsets.Player.InventoryController);
            }
            else
            {
                var observedPlayerController = Memory.ReadPtr(playerBase + Offsets.ObservedPlayerView.ObservedPlayerController);
                inventorycontroller = Memory.ReadPtr(observedPlayerController + Offsets.ObservedPlayerController.InventoryController);
            }

            var inventory = Memory.ReadPtr(inventorycontroller + Offsets.InventoryController.ObservedPlayerInventory);
            var equipment = Memory.ReadPtr(inventory + Offsets.Inventory.Equipment);
            var slots = Memory.ReadPtr(equipment + Offsets.Equipment.Slots);
            var size = Memory.ReadValue<int>(slots + Offsets.UnityList.Count);

            if (size == 0 || slots == 0)
                return;

            for (int slotID = 0; slotID < size; slotID++)
            {
                var slotPtr = Memory.ReadPtr(slots + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                var name = Memory.ReadUnityString(namePtr);
                if (_skipSlots.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;

                slotDict.TryAdd(name, slotPtr);
            }

            var gearDict = new Dictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var slotName in slotDict.Keys)
            {
                if (_skipSlots.Contains(slotName, StringComparer.OrdinalIgnoreCase))
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
                            List<LootItem> gearMods = new List<LootItem>();
                            var totalGearValue = TarkovDevManager.GetItemValue(lootItem.Item);
                            if (isPMC)
                            {
                                if (slotName == "FirstPrimaryWeapon" || slotName == "SecondPrimaryWeapon" || slotName == "Holster") // Only interested in weapons
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
                            }

                            totalGearValue += gearMods.Sum(x => x.Value);
                            this.TotalValue += totalGearValue;
                            this.GearItemsAndMods.AddRange(gearMods);

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
                        } else {
                            Debug.WriteLine($"GearManager: ID: {id} not found in TarkovDevManager.AllItems");
                        }
                    }
                }
                catch { }
            }
            
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

        private static void GetItemsInGrid(ulong gridsArrayPtr, string id, List<LootItem> loot)
        {
            if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
            {
                loot.Add(new LootItem
                {
                    Label = lootItem.Label,
                    AlwaysShow = lootItem.AlwaysShow,
                    Important = lootItem.Important,
                    Item = lootItem.Item,
                    Value = TarkovDevManager.GetItemValue(lootItem.Item)
                });
            }

            if (gridsArrayPtr == 0x0)
            {
                return;
            }

            var gridsArray = new MemArray(gridsArrayPtr);
            foreach (var grid in gridsArray.Data)
            {
                try
                {
                    var gridEnumerableClass = Memory.ReadPtr(grid + Offsets.Grids.GridsEnumerableClass);
                    var itemListPtr = Memory.ReadPtr(gridEnumerableClass + 0x18);
                    var itemList = new MemList(itemListPtr);
                    foreach (var childItem in itemList.Data)
                    {
                        try
                        {
                            var childItemTemplate = Memory.ReadPtr(childItem + Offsets.LootItemBase.ItemTemplate);
                            var childItemIdPtr = Memory.ReadPtr(childItemTemplate + Offsets.ItemTemplate.BsgId);
                            var childItemId = Memory.ReadUnityString(childItemIdPtr).Replace("\\0", "");
                            var childGridsArrayPtr = Memory.ReadPtrNullable(childItem + Offsets.LootItemBase.Grids);
                            GearManager.GetItemsInGrid(childGridsArrayPtr, childItemId, loot);
                        }
                        catch {}
                    }
                } catch {}
            }
        }
    }
}
