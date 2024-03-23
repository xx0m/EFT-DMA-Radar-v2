using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Xml.Linq;
using eft_dma_radar.Source.Misc;
using Offsets;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

namespace eft_dma_radar
{
    public class LootManager
    {

        public ulong lootlistPtr;
        public ulong lootListEntity;
        public int countLootListObjects;
        public ulong localGameWorld;

        private readonly Config _config;
        /// <summary>
        /// Filtered loot ready for display by GUI.
        /// </summary>
        public ReadOnlyCollection<LootItem> Filter { get; private set; }
        /// <summary>
        /// All tracked loot/corpses in Local Game World.
        /// </summary>
        public ReadOnlyCollection<LootItem> Loot { get; set; }
        /// <summary>
        /// all quest items
        /// </summary>
        private Collection<QuestItem> QuestItems { get => Memory.QuestManager.QuestItems; }
        /// <summary>
        /// key,value pair of filtered item ids (key) and their filtered color (value)
        /// </summary>
        public Dictionary<string, LootFilter.Colors> LootFilterColors { get; private set; }
        /// <summary>
        /// list of slots to skip over
        /// </summary>
        private readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Scabbard", "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="LootManager"/> class.
        /// </summary>
        public LootManager(ulong localGameWorld)
        {
            this._config = Program.Config;
            this.localGameWorld = localGameWorld;
            this.RefreshLootListAddresses();

            new Thread((ThreadStart)delegate
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Console.WriteLine("[LootManager] Refresh thread started.");
                while (Memory.GameStatus == Game.GameStatus.InGame)
                {
                    stopwatch.Restart();
                    Console.WriteLine("[LootManager] Refreshing loot...");
                    this.GetLoot();
                    this.ApplyFilter();
                    DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(34, 1);
                    defaultInterpolatedStringHandler.AppendLiteral("[LootManager] Refreshed loot in ");
                    defaultInterpolatedStringHandler.AppendFormatted<long>(stopwatch.ElapsedMilliseconds);
                    defaultInterpolatedStringHandler.AppendLiteral("ms");
                    Console.WriteLine(defaultInterpolatedStringHandler.ToStringAndClear());
                    Thread.Sleep(10000);
                }
                Console.WriteLine("[LootManager] Refresh thread stopped.");
            })
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            }.Start();
        }
        #endregion

        #region Methods
        public void GetLoot()
        {
            this.RefreshLootListAddresses();

            if (this.countLootListObjects < 0 || this.countLootListObjects > 4096)
                throw new ArgumentOutOfRangeException("countLootListObjects"); // Loot list sanity check

            var loot = new List<LootItem>(countLootListObjects);

            var scatterReadMap = new ScatterReadMap(countLootListObjects);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();
            var round6 = scatterReadMap.AddRound();
            var round7 = scatterReadMap.AddRound();
            var round8 = scatterReadMap.AddRound();
            var round9 = scatterReadMap.AddRound();

            for (int i = 0; i < countLootListObjects; i++)
            {
                var lootObjectBase = round1.AddEntry<ulong>(i, 0, lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8));

                var lootUnknownPtr = round2.AddEntry<ulong>(i, 1, lootObjectBase, null, Offsets.LootListItem.LootUnknownPtr);

                var interactiveClass = round3.AddEntry<ulong>(i, 2, lootUnknownPtr, null, Offsets.LootUnknownPtr.LootInteractiveClass);

                var lootBaseObject = round4.AddEntry<ulong>(i, 3, interactiveClass, null, Offsets.LootInteractiveClass.LootBaseObject);

                var gameObject = round5.AddEntry<ulong>(i, 4, lootBaseObject, null, Offsets.LootBaseObject.GameObject);

                var objectName = round6.AddEntry<ulong>(i, 5, gameObject, null, Offsets.GameObject.ObjectName);
                var entry7 = round6.AddEntry<ulong>(i, 7, interactiveClass, null, 0x0);
                var objectClass = round6.AddEntry<ulong>(i, 11, gameObject, null, Offsets.GameObject.ObjectClass);

                var transformOne = round7.AddEntry<ulong>(i, 12, objectClass, null, LootGameObjectClass.To_TransformInternal[0]);
                var containerName = round7.AddEntry<string>(i, 6, objectName, 64);
                var entry9 = round7.AddEntry<ulong>(i, 8, entry7, null, 0x0);

                var transformTwo = round8.AddEntry<ulong>(i, 13, transformOne, null, LootGameObjectClass.To_TransformInternal[1]);
                var entry10 = round8.AddEntry<ulong>(i, 9, entry9, null, 0x48);

                var position = round9.AddEntry<ulong>(i, 14, transformTwo, null, LootGameObjectClass.To_TransformInternal[2]);
                var className = round9.AddEntry<string>(i, 10, entry10, 64);
            }

            scatterReadMap.Execute();

            List<Player> players = new List<Player>();
            ReadOnlyDictionary<string, Player> playersDict = Memory.Players;

            if (playersDict is not null)
            {
                var enumerable = from x in playersDict select x.Value into x where x.CorpsePtr > 0x000 select x;
                players = enumerable.ToList<Player>();
            }

            Parallel.For(0, this.countLootListObjects, i =>
            {
                try
                {
                    var result1 = scatterReadMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);
                    var result2 = scatterReadMap.Results[i][1].TryGetResult<ulong>(out var unknownPtr);
                    if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var interactiveClass))
                        return;
                    var result4 = scatterReadMap.Results[i][3].TryGetResult<ulong>(out var baseObject);
                    if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var gameObject))
                        return;
                    var result6 = scatterReadMap.Results[i][10].TryGetResult<string>(out var className);
                    var result7 = scatterReadMap.Results[i][6].TryGetResult<string>(out var containerName);
                    var result8 = scatterReadMap.Results[i][11].TryGetResult<ulong>(out var objectClass);
                    var result9 = scatterReadMap.Results[i][14].TryGetResult<ulong>(out var pos);

                    bool isCorpse = className.Contains("Corpse", StringComparison.OrdinalIgnoreCase);
                    bool isLooseLoot = className.Equals("ObservedLootItem", StringComparison.OrdinalIgnoreCase);
                    bool isContainer = className.Equals("LootableContainer", StringComparison.OrdinalIgnoreCase);

                    if (!containerName.Contains("script", StringComparison.OrdinalIgnoreCase))
                    {
                        Vector3 position = new Transform(pos, false).GetPosition(null);

                        if (isContainer)
                        {
                            var containerIDPtr = Memory.ReadPtr(interactiveClass + 0x128);
                            var containerID = Memory.ReadUnityString(containerIDPtr);
                            TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container);
                            if (container != null)
                            {
                                try
                                {
                                    var itemOwner = Memory.ReadPtr(interactiveClass + Offsets.LootInteractiveClass.ContainerItemOwner);
                                    var itemBase = Memory.ReadPtr(itemOwner + 0xC0); //Offsets.ContainerItemOwner.LootItemBase);
                                    var grids = Memory.ReadPtr(itemBase + Offsets.LootItemBase.Grids);
                                    GetItemsInGrid(grids, containerName, position, loot, true, container.Name, containerName);
                                }
                                catch { }
                            }
                            else
                            {
                                Program.Log($"Container: {containerName} {containerID} is not in the list");
                            }
                        }
                        else
                        {
                            if (isCorpse)
                            {
                                Player player = players.FirstOrDefault(x => x.CorpsePtr == interactiveClass);

                                if (interactiveClass == 0x0)
                                    return;

                                var itemOwner = Memory.ReadPtr(interactiveClass + 0x40);
                                var rootItem = Memory.ReadPtr(itemOwner + 0xC0);
                                var slots = Memory.ReadPtr(rootItem + 0x78);
                                var slotsArray = new MemArray(slots);

                                foreach (var slot in slotsArray.Data)
                                {
                                    try
                                    {
                                        var namePtr = Memory.ReadPtr(slot + Offsets.Slot.Name);
                                        var slotName = Memory.ReadUnityString(namePtr);
                                        
                                        if (slotsToSkip.Contains(slotName))
                                            continue;

                                        var containedItem = Memory.ReadPtrNullable(slot + 0x40);

                                        if (containedItem == 0x0)
                                            continue;

                                        var itemTemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate); //EFT.InventoryLogic.ItemTemplate
                                        var BSGIdPtr = Memory.ReadPtr(itemTemplate + Offsets.ItemTemplate.BsgId);
                                        var id = Memory.ReadUnityString(BSGIdPtr);
                                        var grids = Memory.ReadPtr(containedItem + Offsets.LootItemBase.Grids);

                                        containerName = "Corpse" + (player is not null ? $" [{player.Name}]" : "");

                                        if (grids == 0x0)
                                        {
                                            if (TarkovDevManager.AllItems.TryGetValue(id, out var lootItem))
                                            {
                                                loot.Add(new LootItem
                                                {
                                                    Label = lootItem.Label,
                                                    AlwaysShow = lootItem.AlwaysShow,
                                                    Important = lootItem.Important,
                                                    Position = position,
                                                    Item = lootItem.Item,
                                                    Container = true,
                                                    ContainerName = containerName,
                                                    Value = TarkovDevManager.GetItemValue(lootItem.Item)
                                                });
                                            }
                                        };
                                        GetItemsInGrid(grids, id, position, loot, true, containerName);
                                        continue;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Corpse Error: " + ex.Message);
                                        continue;
                                    }
                                }
                            }
                            else if (isLooseLoot)
                            {
                                var item = Memory.ReadPtr(interactiveClass + 0xB0);
                                var itemTemplate = Memory.ReadPtr(item + Offsets.LootItemBase.ItemTemplate);
                                var BSGIdPtr = Memory.ReadPtr(itemTemplate + Offsets.ItemTemplate.BsgId);
                                var id = Memory.ReadUnityString(BSGIdPtr);
                                bool questItem = Memory.ReadValue<bool>(itemTemplate + Offsets.ItemTemplate.IsQuestItem);

                                if (id == null)
                                    return;

                                if (!questItem)
                                {
                                    if (TarkovDevManager.AllItems.TryGetValue(id, out var lootItem))
                                    {
                                        loot.Add(new LootItem
                                        {
                                            Label = lootItem.Label,
                                            AlwaysShow = lootItem.AlwaysShow,
                                            Important = lootItem.Important,
                                            Position = position,
                                            Item = lootItem.Item,
                                            Value = TarkovDevManager.GetItemValue(lootItem.Item)
                                        });
                                    }
                                }
                                else
                                {
                                    var questItemTest = this.QuestItems.Where(x => x.Id == id).FirstOrDefault();
                                    if (questItemTest != null)
                                    {
                                        questItemTest.Position = position;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with LootManager.GetLoot(): {ex.Message}");
                }
            });

            this.Loot = new(loot);
        }

        /// <summary>
        /// Applies loot filter
        /// </summary>
        public void ApplyFilter()
        {
            var loot = this.Loot;
            if (loot is not null)
            {
                var activeFilters = this._config.Filters.Where(f => f.IsActive).ToList();
                var minValueLootItems = loot.Where(x => x.AlwaysShow || x.Value > this._config.MinLootValue).ToList();

                var itemsWithData = activeFilters.SelectMany(f => f.Items)
                    .Distinct()
                    .Select(item => new {
                        ItemId = item,
                        Filter = activeFilters
                            .Where(f => f.Items.Contains(item))
                            .OrderBy(f => f.Order)
                            .First()
                    });

                var orderedItems = itemsWithData
                   .OrderBy(x => x.Filter.Order)
                   .Select(x => new {
                       x.ItemId,
                       x.Filter.Color
                   })
                   .ToList();

                var orderedIds = orderedItems.Select(x => x.ItemId).ToList();

                //ghetto way to prevent overriding LootItems in the original loot list
                var lootCopy = loot.Select(l => new LootItem
                {
                    Label = l.Label,
                    Important = l.Important,
                    Position = l.Position,
                    AlwaysShow = l.AlwaysShow,
                    BsgId = l.BsgId,
                    ContainerName = l.ContainerName,
                    Container = l.Container,
                    Item = l.Item
                }).ToList();

                var filteredLoot = from l in lootCopy
                                   join id in orderedItems on l.Item.id equals id.ItemId
                                   select l;

                // ghetto quickfix lmao
                filteredLoot = filteredLoot.ToList();

                foreach (var lootItem in filteredLoot)
                {
                    lootItem.Important = true;
                }

                foreach (var lootItem in minValueLootItems)
                {
                    if (lootItem.Value >= this._config.MinImportantLootValue)
                    {
                        lootItem.Important = true;
                    }
                }

                filteredLoot = filteredLoot.Union(minValueLootItems)
                    .GroupBy(x => x.Position)
                    .Select(g => g.OrderBy(x => {
                        var match = orderedItems.FirstOrDefault(oi => oi.ItemId == x.Item.id);
                        return match == null ? int.MaxValue : orderedItems.IndexOf(match);
                    })
                    .First())
                    .OrderBy(x => {
                        var match = orderedItems.FirstOrDefault(oi => oi.ItemId == x.Item.id);
                        return match == null ? int.MaxValue : orderedItems.IndexOf(match);
                    });

                this.LootFilterColors = orderedItems.ToDictionary(item => item.ItemId, item => item.Color);
                this.Filter = new ReadOnlyCollection<LootItem>(filteredLoot.ToList());
            }
        }

        /// <summary>
        /// Removes an item from the loot filter list
        /// </summary>
        /// <param name="itemToRemove">The item to remove</param>
        public void RemoveFilterItem(LootItem itemToRemove)
        {
            var filter = this.Filter.ToList();
            filter.Remove(itemToRemove);

            this.Filter = new ReadOnlyCollection<LootItem>(new List<LootItem>(filter));
            this.ApplyFilter();
        }

        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        public void RefreshLootListAddresses()
        {
            this.lootlistPtr = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.LootList);
            this.lootListEntity = Memory.ReadPtr(lootlistPtr + Offsets.UnityList.Base);
            this.countLootListObjects = Memory.ReadValue<int>(lootListEntity + Offsets.UnityList.Count);
        }

        /// <summary>
        /// Recursively searches items within a grid
        /// </summary>
        private void GetItemsInGrid(ulong gridsArrayPtr, string id, Vector3 pos, List<LootItem> loot, bool isContainer = false, string containerName = "", string realContainerName = "")
        {
            if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
            {
                loot.Add(new LootItem
                {
                    Label = lootItem.Label,
                    AlwaysShow = lootItem.AlwaysShow,
                    Important = lootItem.Important,
                    Position = pos,
                    Item = lootItem.Item,
                    Container = isContainer,
                    ContainerName = containerName,
                    Value = TarkovDevManager.GetItemValue(lootItem.Item)
                });
            }

            if (gridsArrayPtr == 0x0)
            {
                return;
            }

            var gridsArray = new MemArray(gridsArrayPtr);

            try
            {
                foreach (var grid in gridsArray.Data)
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

                            GetItemsInGrid(childGridsArrayPtr, childItemId, pos, loot, true, containerName, realContainerName);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {

            }
        }
        #endregion
    }

    #region Classes
    //Helper class or struct
    public class MemArray
    {
        public ulong Address
        {
            get;
        }
        public int Count
        {
            get;
        }
        public ulong[] Data
        {
            get;
        }

        public MemArray(ulong address)
        {
            var type = typeof(ulong);

            Address = address;
            Count = Memory.ReadValue<int>(address + Offsets.UnityList.Count);
            var arrayBase = address + Offsets.UnityListBase.Start;
            var tSize = (uint)Marshal.SizeOf(type);

            // Rudimentary sanity check
            if (Count > 4096 || Count < 0)
                Count = 0;

            var retArray = new ulong[Count];
            var buf = Memory.ReadBuffer(arrayBase, Count * (int)tSize);

            for (uint i = 0; i < Count; i++)
            {
                var index = i * tSize;
                var t = MemoryMarshal.Read<ulong>(buf.Slice((int)index, (int)tSize));
                if (t == 0x0) throw new NullPtrException();
                retArray[i] = t;
            }

            Data = retArray;
        }
    }

    //Helper class or struct
    public class MemList
    {
        public ulong Address
        {
            get;
        }

        public int Count
        {
            get;
        }

        public List<ulong> Data
        {
            get;
        }

        public MemList(ulong address)
        {
            var type = typeof(ulong);

            Address = address;
            Count = Memory.ReadValue<int>(address + Offsets.UnityList.Count);

            if (Count > 4096 || Count < 0)
                Count = 0;

            var arrayBase = Memory.ReadPtr(address + Offsets.UnityList.Base) + Offsets.UnityListBase.Start;
            var tSize = (uint)Marshal.SizeOf(type);
            var retList = new List<ulong>(Count);
            var buf = Memory.ReadBuffer(arrayBase, Count * (int)tSize);

            for (uint i = 0; i < Count; i++)
            {
                var index = i * tSize;
                var t = MemoryMarshal.Read<ulong>(buf.Slice((int)index, (int)tSize));
                if (t == 0x0) throw new NullPtrException();
                retList.Add(t);
            }

            Data = retList;
        }
    }

    public class LootItem
    {
        public string Label
        {
            get;
            init;
        }
        public bool Important
        {
            get;
            set;
        } = false;
        public Vector3 Position
        {
            get;
            init;
        }
        public bool AlwaysShow
        {
            get;
            init;
        } = false;
        public string BsgId
        {
            get;
            init;
        }
        public bool Container
        {
            get;
            init;
        } = false;
        public string ContainerName
        {
            get;
            init;
        }
        public TarkovItem Item
        {
            get;
            init;
        } = new();
        public int Value
        {
            get;
            init;
        }

        public Player PlayerCorpse { get; set; }

        /// <summary>
        /// Cached 'Zoomed Position' on the Radar GUI. Used for mouseover events.
        /// </summary>
        public Vector2 ZoomedPosition { get; set; } = new();

        /// <summary>
        /// Gets the formatted the items value
        /// </summary>
        public string GetFormattedValue()
        {
            return TarkovDevManager.FormatNumber(this.Value);
        }

        /// <summary>
        /// Gets the formatted item value + name
        /// </summary>
        public string GetFormattedValueName()
        {
            return (this.AlwaysShow || this.Item.shortName is not null) ? $"[{this.GetFormattedValue()}] {this.Item.name}" : "null";
        }

        /// <summary>
        /// Gets the formatted item value + name
        /// </summary>
        public string GetFormattedValueShortName()
        {
            return (this.AlwaysShow || this.Item.shortName is not null) ? $"[{this.GetFormattedValue()}] {this.Item.shortName}" : "null";
        }
    }

    public class LootContainers
    {
        public string Name
        {
            get;
            init;
        }
        public string ID
        {
            get;
            init;
        }
        public string NormalizedName
        {
            get;
            init;
        }
    }

    /// <summary>
    /// Class to help handle filter lists/profiles for the loot filter
    /// </summary>
    public class LootFilter
    {
        public List<string>? Items { get; set; }
        public Colors Color { get; set; }
        public bool IsActive { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }

        public struct Colors
        {
            public byte A { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }
    }
    #endregion
}