using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
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
using OpenTK.Graphics.OpenGL;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;
using static System.Reflection.Metadata.BlobBuilder;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Menu;
using static eft_dma_radar.ObjectiveZones;

namespace eft_dma_radar
{
    public class LootManager
    {
        public ulong lootlistPtr;
        public ulong lootListEntity;
        public int countLootListObjects;
        public ulong localGameWorld;

        private List<(bool Valid, int Index, ulong Pointer)> validLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
        private List<(bool Valid, int Index, ulong Pointer)> invalidLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
        private ConcurrentBag<ContainerInfo> savedLootContainersInfo = new ConcurrentBag<ContainerInfo>();
        private ConcurrentBag<CorpseInfo> savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>();
        private ConcurrentBag<LootItemInfo> savedLootItemsInfo = new ConcurrentBag<LootItemInfo>();
        private static ConcurrentDictionary<ulong, GridState> gridCache = new ConcurrentDictionary<ulong, GridState>();

        /// <summary>
        /// list of slots to skip over
        /// </summary>
        private static readonly IReadOnlyCollection<string> gridSlotsToSearch = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Headwear", "TacticalVest", "Backpack", "Pockets" };
        private static readonly IReadOnlyCollection<string> slotsToSearch = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FirstPrimaryWeapon", "SecondPrimaryWeapon", "ArmorVest", "Holster", "Headwear" };
        private static readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };

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
        private Collection<QuestItem> QuestItems { get => Memory.QuestManager is not null ? Memory.QuestManager.QuestItems : null; }

        private string CurrentMapName { get => Memory.MapName; }
        /// <summary>
        /// key,value pair of filtered item ids (key) and their filtered color (value)
        /// </summary>
        public Dictionary<string, LootFilter.Colors> LootFilterColors { get; private set; }
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
                if (_config.AutoLootRefreshEnabled)
                {
                    while (this._config.LootEnabled && Memory.GameStatus == Game.GameStatus.InGame)
                    {
                        stopwatch.Restart();
                        this.GetLootList();
                        this.GetLoot();
                        this.FillLoot();
                        this.ApplyFilter();
                        if (this.CurrentMapName == "TarkovStreets")
                        {
                            Thread.Sleep(30000);
                        }
                        else
                        {
                            Thread.Sleep(10000);
                        }
                    }
                }
                else
                {
                    this.GetLootList();
                    this.GetLoot();
                    this.FillLoot();
                    this.ApplyFilter();
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
        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        private void RefreshLootListAddresses()
        {
            this.lootlistPtr = Memory.ReadPtr(this.localGameWorld + Offsets.LocalGameWorld.LootList);
            this.lootListEntity = Memory.ReadPtr(this.lootlistPtr + Offsets.UnityList.Base);
            this.countLootListObjects = Memory.ReadValue<int>(this.lootListEntity + Offsets.UnityList.Count);
        }

        private void GetLootList()
        {
            this.RefreshLootListAddresses();

            if (this.countLootListObjects < 0 || this.countLootListObjects > 4096)
                throw new ArgumentOutOfRangeException("countLootListObjects"); // Loot list sanity check

            var scatterMap = new ScatterReadMap(this.countLootListObjects);
            var round1 = scatterMap.AddRound();

            var lootEntitiesWithIndex = new List<(bool Valid, int Index, ulong Pointer)>();

            for (int i = 0; i < this.countLootListObjects; i++)
            {
                var p1 = round1.AddEntry<ulong>(i, 0, this.lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8));
            }
            scatterMap.Execute();
            for (int i = 0; i < this.countLootListObjects; i++)
            {
                try
                {
                    var result1 = scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);
                    if (lootObjectsEntity == 0x0)
                    {
                        lootEntitiesWithIndex.Add((false, i, this.lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8)));
                    }
                    else
                    {
                        lootEntitiesWithIndex.Add((true, i, lootObjectsEntity));
                    }

                }
                catch
                {
                    Program.Log($"Error reading loot item {i}");
                }
            }

            lootEntitiesWithIndex = lootEntitiesWithIndex.OrderBy(x => x.Index).ToList();
            this.validLootEntities = lootEntitiesWithIndex.Where(x => x.Valid).ToList();
            this.invalidLootEntities = lootEntitiesWithIndex.Where(x => !x.Valid).ToList();
        }

        public void GetLoot()
        {
            //test first 50 failed loot entities
            var scatterMap = new ScatterReadMap(this.invalidLootEntities.Count);
            var round1 = scatterMap.AddRound();
            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                var p1 = round1.AddEntry<ulong>(i, 0, this.invalidLootEntities[i].Pointer);
            }
            scatterMap.Execute();
            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                try
                {
                    var result1 = scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);
                    if (lootObjectsEntity != 0x0)
                    {
                        //Remove from invalid list
                        this.validLootEntities.Add((true, this.invalidLootEntities[i].Index, lootObjectsEntity));
                        this.invalidLootEntities.RemoveAt(i);
                    }
                }
                catch
                {
                    Program.Log($"Error reading loot item {i}");
                }
            }
            //check all valid loot entities
            var validScatterCheckMap = new ScatterReadMap(this.validLootEntities.Count);
            var validCheckRound1 = validScatterCheckMap.AddRound();
            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                var lootUnknownPtr = validCheckRound1.AddEntry<ulong>(i, 0, this.validLootEntities[i].Pointer, null);
            }
            validScatterCheckMap.Execute();
            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                try
                {
                    var result1 = validScatterCheckMap.Results[i][0].TryGetResult<ulong>(out var lootUnknownPtr);
                    if (lootUnknownPtr == 0x0)
                    {
                        this.invalidLootEntities.Add((true, this.validLootEntities[i].Index, this.validLootEntities[i].Pointer));
                        //Console.WriteLine($"Invalid loot entity {validLootEntities[i].Index}");
                        this.validLootEntities.RemoveAt(i);
                    }
                }
                catch
                {
                    Program.Log($"Error reading loot item {i}");
                }
            }

            var validScatterMap = new ScatterReadMap(this.validLootEntities.Count);
            var vRound1 = validScatterMap.AddRound();
            var vRound2 = validScatterMap.AddRound();
            var vRound3 = validScatterMap.AddRound();
            var vRound4 = validScatterMap.AddRound();
            var vRound5 = validScatterMap.AddRound();
            var vRound6 = validScatterMap.AddRound();
            var vRound7 = validScatterMap.AddRound();
            var vRound8 = validScatterMap.AddRound();
            var vRound9 = validScatterMap.AddRound();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                var lootUnknownPtr = vRound1.AddEntry<ulong>(i, 0, validLootEntities[i].Pointer, null, Offsets.LootListItem.LootUnknownPtr);

                var interactiveClass = vRound2.AddEntry<ulong>(i, 1, lootUnknownPtr, null, Offsets.LootUnknownPtr.LootInteractiveClass);

                var lootBaseObject = vRound3.AddEntry<ulong>(i, 2, interactiveClass, null, Offsets.LootInteractiveClass.LootBaseObject);
                var entry7 = vRound3.AddEntry<ulong>(i, 3, interactiveClass, null, 0x0);
                var item = vRound3.AddEntry<ulong>(i, 4, interactiveClass, null, 0xB0);
                var containerIDPtr = vRound3.AddEntry<ulong>(i, 5, interactiveClass, null, 0x128);
                var itemOwner = vRound3.AddEntry<ulong>(i, 6, interactiveClass, null, 0x40);
                var containerItemOwner = vRound3.AddEntry<ulong>(i, 7, interactiveClass, null, Offsets.LootInteractiveClass.ContainerItemOwner);

                var gameObject = vRound4.AddEntry<ulong>(i, 8, lootBaseObject, null, Offsets.LootBaseObject.GameObject);
                var entry9 = vRound4.AddEntry<ulong>(i, 9, entry7, null, 0x0);
                var itemTemplate = vRound4.AddEntry<ulong>(i, 10, item, null, Offsets.LootItemBase.ItemTemplate);
                var containerItemBase = vRound4.AddEntry<ulong>(i, 11, containerItemOwner, null, 0xC0);

                var objectName = vRound5.AddEntry<ulong>(i, 12, gameObject, null, Offsets.GameObject.ObjectName);
                var objectClass = vRound5.AddEntry<ulong>(i, 13, gameObject, null, Offsets.GameObject.ObjectClass);
                var entry10 = vRound5.AddEntry<ulong>(i, 14, entry9, null, 0x48);
                var isQuestItem = vRound5.AddEntry<bool>(i, 15, itemTemplate, null, Offsets.ItemTemplate.IsQuestItem);
                var BSGIdPtr = vRound5.AddEntry<ulong>(i, 16, itemTemplate, null, Offsets.ItemTemplate.BsgId);
                var rootItem = vRound5.AddEntry<ulong>(i, 17, itemOwner, null, 0xC0);
                var containerGrids = vRound5.AddEntry<ulong>(i, 18, containerItemBase, null, Offsets.LootItemBase.Grids);

                var className = vRound6.AddEntry<string>(i, 19, entry10, 64);
                var containerName = vRound6.AddEntry<string>(i, 20, objectName, 64);
                var transformOne = vRound6.AddEntry<ulong>(i, 21, objectClass, null, Offsets.LootGameObjectClass.To_TransformInternal[0]);
                var slots = vRound6.AddEntry<ulong>(i, 22, rootItem, null, 0x78);

                var transformTwo = vRound7.AddEntry<ulong>(i, 23, transformOne, null, Offsets.LootGameObjectClass.To_TransformInternal[1]);

                var position = vRound8.AddEntry<ulong>(i, 24, transformTwo, null, Offsets.LootGameObjectClass.To_TransformInternal[2]);
            }

            validScatterMap.Execute();
           
            List<Player> players = Memory.Players?
                .Where(x => x.Value.CorpsePtr > 0x00)
                .Select(x => x.Value)
                .ToList() ?? new List<Player>();

            //for (int i = 0; i < this.validLootEntities.Count; i++)
            Parallel.For(0, this.validLootEntities.Count, i =>
            {
                try
                {
                    if (!validScatterMap.Results[i][1].TryGetResult<ulong>(out var interactiveClass))
                        return;
                    if (!validScatterMap.Results[i][2].TryGetResult<ulong>(out var lootBaseObject))
                        return;
                    validScatterMap.Results[i][8].TryGetResult<ulong>(out var gameObject);
                    validScatterMap.Results[i][12].TryGetResult<ulong>(out var objectName);
                    validScatterMap.Results[i][13].TryGetResult<ulong>(out var objectClass);
                    validScatterMap.Results[i][20].TryGetResult<string>(out var containerName);
                    validScatterMap.Results[i][19].TryGetResult<string>(out var className);
                    if (!validScatterMap.Results[i][24].TryGetResult<ulong>(out var posToTransform))
                        return;

                    if (!containerName.Contains("script", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isCorpse = className.Contains("Corpse", StringComparison.OrdinalIgnoreCase);
                        bool isLooseLoot = className.Equals("ObservedLootItem", StringComparison.OrdinalIgnoreCase);
                        bool isContainer = className.Equals("LootableContainer", StringComparison.OrdinalIgnoreCase);
                        if (isCorpse || isContainer || isLooseLoot)
                        {
                            Vector3 position = new Transform(posToTransform, false).GetPosition(null);

                            if (isCorpse)
                            {
                                if (!this.savedLootCorpsesInfo.Any(x => x.InteractiveClass == interactiveClass))
                                {
                                    if (!validScatterMap.Results[i][6].TryGetResult<ulong>(out var itemOwner))
                                        return;
                                    if (!validScatterMap.Results[i][17].TryGetResult<ulong>(out var rootItem))
                                        return;
                                    if (!validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                                        return;

                                    Player player = players.FirstOrDefault(x => x.CorpsePtr == interactiveClass);
                                    //var playerName = containerName.Split('(', ')')[1];
                                    //Player player = players.FirstOrDefault(x => x.Name == playerName);

                                    this.savedLootCorpsesInfo.Add(new CorpseInfo {InteractiveClass = interactiveClass, Position = position, Slots = slots, Player = player});
                                }
                            }
                            else if (isContainer)
                            {
                                if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                                {
                                    if (!validScatterMap.Results[i][5].TryGetResult<ulong>(out var containerIDPtr))
                                        return;

                                    var containerID = Memory.ReadUnityString(containerIDPtr);
                                    validScatterMap.Results[i][18].TryGetResult<ulong>(out var grids);

                                    TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container);
                                    this.savedLootContainersInfo.Add(new ContainerInfo {InteractiveClass = interactiveClass, Position = position, Name = container?.Name ?? containerName, Grids = grids});
                                }
                            }
                            else if (isLooseLoot) // handle loose weapons / gear
                            {
                                if (!this.savedLootItemsInfo.Any(x => x.InteractiveClass == interactiveClass))
                                {
                                    if (!validScatterMap.Results[i][4].TryGetResult<ulong>(out var item))
                                        return;
                                    if (!validScatterMap.Results[i][10].TryGetResult<ulong>(out var itemTemplate))
                                        return;
                                    if (!validScatterMap.Results[i][15].TryGetResult<bool>(out var questItem))
                                        return;
                                    if (!validScatterMap.Results[i][16].TryGetResult<ulong>(out var BSGIDPtr))
                                        return;

                                    var id = Memory.ReadUnityString(BSGIDPtr);

                                    if (id == null)
                                        return;

                                    this.savedLootItemsInfo.Add(new LootItemInfo(interactiveClass, questItem, position, id));
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with LootManager.GetLoot(): {ex.Message}");
                }
            });
        }

        public void FillLoot()
        {
            var loot = new List<LootItem>();

            foreach (var savedLootItem in this.savedLootItemsInfo)
            {
                if (validLootEntities.Any(x => x.Pointer == savedLootItem.InteractiveClass))
                {
                    if (!savedLootItem.QuestItem)
                    {
                        if (TarkovDevManager.AllItems.TryGetValue(savedLootItem.ItemID, out var lootItem))
                        {
                            loot.Add(new LootItem
                            {
                                Name = lootItem.Name,
                                ID = savedLootItem.ItemID,
                                AlwaysShow = lootItem.AlwaysShow,
                                Important = lootItem.Important,
                                Position = savedLootItem.Position,
                                Item = lootItem.Item,
                                Value = TarkovDevManager.GetItemValue(lootItem.Item)
                            });
                        }
                        else
                        {
                            Console.WriteLine($"[LootManager] Item {savedLootItem.ItemID} not found in API.");
                        }
                    }
                    else
                    {
                        var questItem = this.QuestItems.Where(x => x.Id == savedLootItem.ItemID).FirstOrDefault();
                        if (questItem != null)
                        {
                            questItem.Position = savedLootItem.Position;
                        }
                    }
                }
                else
                {
                    savedLootItemsInfo = new ConcurrentBag<LootItemInfo>(savedLootItemsInfo.Where(x => x.InteractiveClass != savedLootItem.InteractiveClass));
                }
            }

            // create Corpse objects
            foreach (var savedLootCorpse in this.savedLootCorpsesInfo)
            {
                var gearItems = new List<GearItem>();
                var name = "Corpse" + (savedLootCorpse.Player != null ? $" {savedLootCorpse.Player.Name}" : "");

                var corpse = new LootCorpse(
                    savedLootCorpse.InteractiveClass,
                    savedLootCorpse.Slots,
                    savedLootCorpse.Position,
                    name
                );

                if (savedLootCorpse.Player is not null)
                {
                    corpse.Player = savedLootCorpse.Player;
                }

                LootManager.GetItemsInSlots(savedLootCorpse.Slots, savedLootCorpse.Position, gearItems);

                gearItems = gearItems.Where(item => item.TotalValue > 0).ToList();

                foreach (var gearItem in gearItems)
                {
                    int index = gearItem.Loot.FindIndex(lootItem => lootItem.ID == gearItem.ID);
                    if (index != -1)
                    {
                        gearItem.Loot.RemoveAt(index);
                    }

                    gearItem.Loot = MergeDupelicateLootItems(gearItem.Loot);
                }

                corpse.Items = gearItems.OrderBy(x => x.TotalValue).ToList();
                corpse.UpdateValue();

                loot.Add(corpse);
            }

            // create Container objects, merge dupe entries based on position + name
            // (helps deal with multiple entries for the same container)
            var groupedContainers = this.savedLootContainersInfo.GroupBy(container => (container.Position, container.Name)).ToList();
            foreach (var savedContainerItem in groupedContainers)
            {
                var firstContainer = savedContainerItem.First();

                var mergedContainer = new LootContainer(
                    firstContainer.InteractiveClass,
                    firstContainer.Position,
                    firstContainer.Name,
                    firstContainer.Grids
                );

                var tmpLootList = new List<LootItem>();

                LootManager.GetItemsInGrid(mergedContainer.Grids, mergedContainer.Name, mergedContainer.Position, tmpLootList);

                mergedContainer.Items = MergeDupelicateLootItems(tmpLootList);

                mergedContainer.UpdateValue();

                loot.Add(mergedContainer);
            }

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
                var orderedActiveFilters = _config.Filters
                    .Where(filter => filter.IsActive)
                    .OrderBy(filter => filter.Order)
                    .ToList();

                Dictionary<string, LootFilter.Colors> itemIdColorPairs = new Dictionary<string, LootFilter.Colors>();

                foreach (var filter in orderedActiveFilters)
                {
                    foreach (var itemId in filter.Items)
                    {
                        if (!itemIdColorPairs.ContainsKey(itemId))
                        {
                            itemIdColorPairs.Add(itemId, filter.Color);
                        }
                    }
                }

                var filteredItems = loot
                    .Where(item => !item.IsCorpse && !item.Container && (itemIdColorPairs.ContainsKey(item.ID) || item.Value > _config.MinLootValue))
                    .Select(item =>
                    {
                        var copiedItem = new LootItem(item);

                        var isImportant = item.Value > _config.MinImportantLootValue;
                        var isFiltered = itemIdColorPairs.ContainsKey(copiedItem.ID);

                        copiedItem.Important = (isImportant || isFiltered);

                        if (isFiltered)
                        {
                            copiedItem.Color = itemIdColorPairs[copiedItem.ID];
                        }

                        return copiedItem;
                    })
                    .ToList();

                foreach (var container in loot.OfType<LootContainer>())
                {
                    if (container.Items.Any(item => item.Value > _config.MinLootValue) || container.AlwaysShow)
                    {
                        var tempContainer = new LootContainer(container);

                        foreach (var item in tempContainer.Items)
                        {
                            var isImportant = item.Value > _config.MinImportantLootValue;
                            var isFiltered = itemIdColorPairs.ContainsKey(item.ID);
                            
                            item.Important = isImportant;

                            if (isFiltered || isImportant)
                            {
                                tempContainer.Important = (isImportant || isFiltered);

                                if (isFiltered)
                                {
                                    item.Color = itemIdColorPairs[item.ID];
                                }
                            }
                        }

                        var itemsWithFilters = tempContainer.Items
                            .Select(item => new { Item = item, Filter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(item.ID)) })
                            .Where(x => x.Filter != null);

                        var firstMatchingItem = itemsWithFilters.Any()
                            ? itemsWithFilters.Aggregate((a, b) => a.Filter.Order < b.Filter.Order ? a : b)?.Item
                            : null;

                        if (firstMatchingItem is not null)
                        {
                            tempContainer.Color = firstMatchingItem.Color;
                        }

                        filteredItems.Add(tempContainer);
                    }
                }

                foreach (var corpse in loot.OfType<LootCorpse>())
                {
                    var tempCorpse = new LootCorpse(corpse);

                    LootItem lowestOrderLootItem = null;
                    GearItem lowestOrderGearItem = null;

                    var lowestFilterOrder = 999;

                    foreach (var gearItem in tempCorpse.Items)
                    {
                        var isGearImportant = gearItem.TotalValue > _config.MinImportantLootValue;
                        var isGearFiltered = itemIdColorPairs.ContainsKey(gearItem.ID);

                        if (isGearImportant || isGearFiltered)
                        {
                            gearItem.Important = (isGearImportant || isGearFiltered);
                            tempCorpse.Important = (isGearImportant || isGearFiltered);

                            if (isGearFiltered)
                            {
                                gearItem.Color = itemIdColorPairs[gearItem.ID];

                                var gearItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(gearItem.ID));
                                if (gearItemFilter != null && gearItemFilter.Order < lowestFilterOrder)
                                {
                                    lowestOrderGearItem = gearItem;
                                    lowestFilterOrder = gearItemFilter.Order;
                                }
                            }
                        }

                        foreach (var lootItem in gearItem.Loot)
                        {
                            var isLootImportant = lootItem.Value > _config.MinImportantLootValue;
                            var isLootFiltered = itemIdColorPairs.ContainsKey(lootItem.ID);

                            if (isLootImportant || isLootFiltered)
                            {
                                lootItem.Important = (isLootImportant || isLootFiltered);
                                gearItem.Important = (isLootImportant || isLootFiltered);
                                tempCorpse.Important = (isLootImportant || isLootFiltered);

                                if (isLootFiltered)
                                {
                                    lootItem.Color = itemIdColorPairs[lootItem.ID];

                                    var lootItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(lootItem.ID));
                                    if (lootItemFilter != null && (lowestOrderLootItem == null || lootItemFilter.Order < lowestFilterOrder))
                                    {
                                        lowestOrderLootItem = lootItem;
                                    }
                                }
                            }
                        }

                        if (lowestOrderLootItem != null)
                        {
                            gearItem.Color = lowestOrderLootItem.Color;
                        }
                    }

                    if (lowestOrderLootItem != null && (lowestOrderGearItem == null ||
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order <
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
                    {
                        tempCorpse.Color = lowestOrderLootItem.Color;
                    }
                    else if (lowestOrderGearItem != null)
                    {
                        tempCorpse.Color = lowestOrderGearItem.Color;
                    }

                    if (tempCorpse.Value > _config.MinCorpseValue || tempCorpse.Important)
                    {
                        filteredItems.Add(tempCorpse);
                    }
                }

                this.Filter = new ReadOnlyCollection<LootItem>(filteredItems.ToList());
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
        /// Recursively searches items within a grid
        /// </summary>
        private static void GetItemsInGrid(ulong gridsArrayPtr, string id, Vector3 position, List<LootItem> containerLoot, int recurseDepth = 0)
        {
            if (TarkovDevManager.AllItems.TryGetValue(id, out var lootItem))
            {
                containerLoot.Add(new LootItem
                {
                    Name = lootItem.Name,
                    ID = id,
                    AlwaysShow = lootItem.AlwaysShow,
                    Important = lootItem.Important,
                    Position = position,
                    Item = lootItem.Item,
                    Value = TarkovDevManager.GetItemValue(lootItem.Item)
                });
            }

            if (gridsArrayPtr == 0x0)
            {
                return;
            }

            if (recurseDepth++ > 3)
            {
                return;
            }

            var gridsArray = new MemArray(gridsArrayPtr);

            if (gridsArray.Count == 0)
            {
                return;
            }

            try
            {
                HashSet<string> currentItemsIdentifiers = new HashSet<string>();

                bool shouldProcessGrid = true;
                if (gridCache.TryGetValue(gridsArrayPtr, out var cachedState))
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
                                currentItemsIdentifiers.Add(childItemId);
                            }
                            catch { }
                        }
                    }
                    shouldProcessGrid = !currentItemsIdentifiers.SetEquals(cachedState.ItemIdentifiers);
                }

                if (!shouldProcessGrid)
                {
                    //This shoule be fixed soon as possible!!! this is fully unnecessary and should be removed but currently filling the loot list with items
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
                                var childItemIdStr = Memory.ReadUnityString(childItemIdPtr).Replace("\\0", "");
                                if (TarkovDevManager.AllItems.TryGetValue(childItemIdStr, out lootItem))
                                {
                                    containerLoot.Add(new LootItem
                                    {
                                        Name = lootItem.Name,
                                        ID = lootItem.Item.id,
                                        AlwaysShow = lootItem.AlwaysShow,
                                        Important = lootItem.Important,
                                        Position = position,
                                        Item = lootItem.Item,
                                        Value = TarkovDevManager.GetItemValue(lootItem.Item)

                                    });
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    return;
                }

                if (shouldProcessGrid)
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

                                GetItemsInGrid(childGridsArrayPtr, childItemId, position, containerLoot);
                            }
                            catch { }
                        }
                    }
                }

                gridCache[gridsArrayPtr] = new GridState
                {
                    ItemCount = gridsArray.Count,
                    GridPointer = gridsArrayPtr,
                    ItemIdentifiers = currentItemsIdentifiers
                };
            }
            catch {}
        }

        private static void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<GearItem> gearItems)
        {
            var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            for (int slotID = 0; slotID < size; slotID++)
            {
                var slotPtr = Memory.ReadPtr(slotItemBase + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                var name = Memory.ReadUnityString(namePtr);
                if (LootManager.slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;
                slotDict.TryAdd(name, slotPtr);
            }

            if (size == 0 || slotItemBase == 0)
                return;

            foreach (var slotName in slotDict.Keys)
            {
                try
                {
                    if (slotDict.TryGetValue(slotName, out var slot))
                    {
                        var containedItem = Memory.ReadPtrNullable(slot + Offsets.Slot.ContainedItem);

                        if (containedItem == 0x0)
                        {
                            continue;
                        }

                        var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                        var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                        var id = Memory.ReadUnityString(idPtr);
                        var isPocket = slotName.Contains("Pocket", StringComparison.OrdinalIgnoreCase);

                        if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem) || isPocket)
                        {
                            var lootItems = new List<LootItem>();
                            var longName = isPocket ? "Pocket" : lootItem.Item.name;
                            var shortName = isPocket ? "Pocket" : lootItem.Item.shortName;
                            var value = isPocket ? 0 : TarkovDevManager.GetItemValue(lootItem.Item);

                            try
                            {
                                if (LootManager.slotsToSearch.Contains(slotName))
                                {
                                    var slotsPtr = Memory.ReadPtr(containedItem + 0x78);
                                    LootManager.GetItemsInSlots(slotsPtr, position, lootItems);
                                }
                                else if (LootManager.gridSlotsToSearch.Contains(slotName))
                                {
                                    var grids = Memory.ReadPtr(containedItem + Offsets.LootItemBase.Grids);
                                    LootManager.GetItemsInGrid(grids, id, position, lootItems);
                                }
                            }
                            catch { }
                            finally
                            {
                                gearItems.Add(new GearItem
                                {
                                    ID = id,
                                    Long = longName,
                                    Short = shortName,
                                    Value = value,
                                    HasThermal = false,
                                    Loot = lootItems,
                                });
                            }
                        }

                    }
                }
                catch { }
            }
        }

        private static void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<LootItem> loot)
        {
            var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            for (int slotID = 0; slotID < size; slotID++)
            {
                var slotPtr = Memory.ReadPtr(slotItemBase + Offsets.UnityListBase.Start + (uint)slotID * 0x8);
                var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                var name = Memory.ReadUnityString(namePtr);
                if (LootManager.slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;
                slotDict.TryAdd(name, slotPtr);
            }

            if (size == 0 || slotItemBase == 0)
                return;

            foreach (var slotName in slotDict.Keys)
            {
                try
                {
                    if (slotDict.TryGetValue(slotName, out var slot))
                    {
                        var containedItem = Memory.ReadPtrNullable(slot + Offsets.Slot.ContainedItem);

                        if (containedItem == 0x0)
                        {
                            continue;
                        }

                        var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                        var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                        var id = Memory.ReadUnityString(idPtr);

                        var lootItems = new List<LootItem>();

                        if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                        {
                            try
                            {
                                if (LootManager.slotsToSearch.Contains(slotName))
                                {
                                    var slotsPtr = Memory.ReadPtr(containedItem + 0x78);
                                    LootManager.GetItemsInSlots(slotsPtr, position, lootItems);
                                }
                                else if (LootManager.gridSlotsToSearch.Contains(slotName))
                                {
                                    var grids = Memory.ReadPtr(containedItem + Offsets.LootItemBase.Grids);
                                    LootManager.GetItemsInGrid(grids, id, position, lootItems);
                                }
                            }
                            catch { }
                            finally
                            {
                                loot.Add(new LootItem
                                {
                                    ID = id,
                                    Name = lootItem.Name,
                                    AlwaysShow = lootItem.AlwaysShow,
                                    Important = lootItem.Important,
                                    Item = lootItem.Item,
                                    Value = TarkovDevManager.GetItemValue(lootItem.Item)
                                });
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static List<LootItem> MergeDupelicateLootItems(List<LootItem> lootItems)
        {
            return
            lootItems
            .GroupBy(lootItem => lootItem.ID)
            .Select(group =>
            {
                var count = group.Count();
                var firstItem = group.First();
                var mergedItem = new LootItem
                {
                    ID = firstItem.ID,
                    Name = (count > 1) ? $"x{count} {firstItem.Name}" : firstItem.Name,
                    Position = firstItem.Position,
                    Item = firstItem.Item,
                    Important = firstItem.Important,
                    AlwaysShow = firstItem.AlwaysShow,
                    Color = firstItem.Color,
                    Value = firstItem.Value * count
                };
                return mergedItem;
            })
            .OrderBy(lootItem => lootItem.Value)
            .ToList();
        }
        #endregion

        internal class GridState
        {
            public int ItemCount { get; set; }
            public ulong GridPointer { get; set; }
            public HashSet<string> ItemIdentifiers { get; set; } = new HashSet<string>();
        }
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
        public string Name { get; init; }
        public string ID { get; init; }
        public bool Important { get; set; } = false;
        public bool AlwaysShow { get; set; } = false;
        public Vector3 Position { get; init; }
        public TarkovItem Item { get; init; } = new();
        public int Value { get; set; }
        public bool Container { get; set; }
        public string ContainerName { get; set; }
        public bool IsCorpse { get; set; }
        public LootFilter.Colors Color { get; set; }

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
            return this.Value > 0 ? $"[{this.GetFormattedValue()}] {this.Name}" : this.Name;
        }

        /// <summary>
        /// Gets the formatted item value + name
        /// </summary>
        public string GetFormattedValueShortName()
        {
            return this.Value > 0 ? $"[{this.GetFormattedValue()}] {this.Item.shortName}" : this.Item.shortName;
        }

        // ghetto way for deep copy
        public LootItem() { }

        public LootItem(LootItem other)
        {
            this.Name = other.Name;
            this.ID = other.ID;
            this.Important = other.Important;
            this.AlwaysShow = other.AlwaysShow;
            this.Position = other.Position;
            this.Item = other.Item;
            this.Value = other.Value;
        }
    }

    public class LootContainer : LootItem
    {
        public string Name;
        public ulong InteractiveClass;
        public ulong Grids;
        public List<LootItem> Items;

        public LootContainer(ulong InteractiveClass, Vector3 position, string containerName, ulong grids)
        {
            this.InteractiveClass = InteractiveClass;
            base.Position = position;
            this.Name = containerName;
            this.Items = new List<LootItem>();

            base.Container = true;
            base.ContainerName = containerName;
            base.IsCorpse = false;
            this.Grids = grids;
        }

        public LootContainer(LootContainer other) : base(other)
        {
            this.InteractiveClass = other.InteractiveClass;
            this.Position = other.Position;
            this.Name = other.Name;
            this.Items = other.Items.Select(item => new LootItem(item)).ToList();

            base.Container = other.Container;
            base.ContainerName = other.ContainerName;
            base.IsCorpse = other.IsCorpse;

            this.Grids = other.Grids;

            this.Value = other.Value;
        }

        public void UpdateValue()
        {
            this.Value = this.Items.Sum(item => item.Value);
        }
    }

    public class LootCorpse : LootItem
    {
        public string Name;
        public ulong InteractiveClass;
        public ulong Slots;
        public new List<GearItem> Items;
        public int Value;
        public Player Player;

        public LootCorpse(ulong lootInteractiveClass, ulong slots, Vector3 position, string containerName)
        {
            this.InteractiveClass = lootInteractiveClass;
            this.Slots = slots;
            base.Position = position;
            this.Name = containerName;
            this.Items = new List<GearItem>();

            base.IsCorpse = true;
            base.Container = true;
            base.ContainerName = containerName;
        }

        public LootCorpse(LootCorpse other) : base(other)
        {
            this.InteractiveClass = other.InteractiveClass;
            this.Slots = other.Slots;
            base.Position = other.Position;
            this.Name = other.Name;
            this.Items = other.Items.Select(item => new GearItem
            {
                ID = item.ID,
                Long = item.Long,
                Short = item.Short,
                Value = item.Value,
                Important = item.Important,
                Loot = item.Loot.Select(loot => new LootItem(loot)).ToList(),
                HasThermal = item.HasThermal
            }).ToList();

            base.Container = other.Container;
            base.ContainerName = other.ContainerName;
            base.IsCorpse = other.IsCorpse;
            this.Value = other.Value;

            this.Player = other.Player;
        }

        public void UpdateValue()
        {
            this.Value = this.Items.Sum(item => item.TotalValue);
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

    struct ContainerInfo
    {
        public ulong InteractiveClass;
        public Vector3 Position;
        public string Name;
        public ulong Grids;
        public bool IsCorpse;
        public bool IsGear;

        public ContainerInfo(ulong interactiveClass, Vector3 position, string containerName, ulong grids, bool isCorpse = false, bool isGear = false)
        {
            InteractiveClass = interactiveClass;
            Position = position;
            Name = containerName;
            Grids = grids;
            IsCorpse = isCorpse;
            IsGear = isGear;
        }
    }

    struct LootItemInfo
    {
        public ulong InteractiveClass;
        public bool QuestItem;
        public Vector3 Position;
        public string ItemID;

        public LootItemInfo(ulong interactiveClass, bool questItem, Vector3 position, string itemID)
        {
            InteractiveClass = interactiveClass;
            QuestItem = questItem;
            Position = position;
            ItemID = itemID;
        }
    }

    struct CorpseInfo
    {
        public ulong InteractiveClass;
        public ulong Slots;
        public Vector3 Position;
        public Player Player;

        public CorpseInfo(ulong interactiveClass, ulong slots, Vector3 position, Player player = null)
        {
            InteractiveClass = interactiveClass;
            Slots = slots;
            Position = position;
            Player = player;
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