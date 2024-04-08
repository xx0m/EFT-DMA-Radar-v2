using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace eft_dma_radar
{
    public class LootManager
    {
        public ulong lootlistPtr;
        public ulong lootListEntity;
        public int countLootListObjects;
        public ulong localGameWorld;

        private static ConcurrentDictionary<ulong, GridCacheEntry> gridCache = new ConcurrentDictionary<ulong, GridCacheEntry>();
        private static ConcurrentDictionary<ulong, SlotCacheEntry> slotCache = new ConcurrentDictionary<ulong, SlotCacheEntry>();
        private List<(bool Valid, int Index, ulong Pointer)> validLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
        private List<(bool Valid, int Index, ulong Pointer)> invalidLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
        private ConcurrentBag<ContainerInfo> savedLootContainersInfo = new ConcurrentBag<ContainerInfo>();
        private ConcurrentBag<CorpseInfo> savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>();
        private ConcurrentBag<LootItemInfo> savedLootItemsInfo = new ConcurrentBag<LootItemInfo>();
        private static readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };

        private readonly Config _config;
        /// <summary>
        /// Filtered loot ready for display by GUI.
        /// </summary>
        public ReadOnlyCollection<LootableObject> Filter { get; private set; }
        /// <summary>
        /// All tracked loot/corpses in Local Game World.
        /// </summary>
        public ReadOnlyCollection<LootableObject> Loot { get; set; }
        /// <summary>
        /// all quest items
        /// </summary>
        private Collection<QuestItem> QuestItems { get => Memory.QuestManager is not null ? Memory.QuestManager.QuestItems : null; }

        private string CurrentMapName { get => Memory.MapName; }
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="LootManager"/> class.
        /// </summary>
        public LootManager(ulong localGameWorld)
        {
            this._config = Program.Config;
            this.localGameWorld = localGameWorld;
            this.RefreshLootListAddresses();

            // Perform an initial loot refresh if auto-refresh is disabled
            if (!this._config.AutoLootRefreshEnabled)
            {
                this.RefreshLoot();
            }

            new Thread((ThreadStart)delegate
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                while (Memory.GameStatus == Game.GameStatus.InGame && this._config.AutoLootRefreshEnabled && this._config.LootEnabled)
                {
                    stopwatch.Restart();
                    this.RefreshLoot();
                    Thread.Sleep(this.CurrentMapName == "TarkovStreets" ? 30000 : 10000);
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
        private void RefreshLoot()
        {
            this.GetLootList();
            this.GetLoot();
            this.FillLoot();
            this.ApplyFilter();
        }

        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        private void RefreshLootListAddresses()
        {
            this.lootlistPtr = Memory.ReadPtr(this.localGameWorld + Offsets.LocalGameWorld.LootList);
            this.lootListEntity = Memory.ReadPtr(this.lootlistPtr + Offsets.UnityList.Base);
            this.countLootListObjects = Memory.ReadValue<int>(this.lootListEntity + Offsets.UnityList.Count);
        }
        /// <summary>
        /// Gets the invalid & valid loot entities
        /// </summary>
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
                scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);

                if (lootObjectsEntity == 0)
                {
                    lootEntitiesWithIndex.Add((false, i, this.lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8)));
                }
                else
                {
                    lootEntitiesWithIndex.Add((true, i, lootObjectsEntity));
                }
            }

            lootEntitiesWithIndex = lootEntitiesWithIndex.OrderBy(x => x.Index).ToList();
            this.validLootEntities = lootEntitiesWithIndex.Where(x => x.Valid).ToList();
            this.invalidLootEntities = lootEntitiesWithIndex.Where(x => !x.Valid).ToList();
        }
        /// <summary>
        /// Creates saved loot items from valid loot entities
        /// </summary>
        public void GetLoot()
        {
            var scatterMap = new ScatterReadMap(this.invalidLootEntities.Count);
            var round1 = scatterMap.AddRound();
            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                var p1 = round1.AddEntry<ulong>(i, 0, this.invalidLootEntities[i].Pointer);
            }
            scatterMap.Execute();

            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);
                if (lootObjectsEntity != 0)
                {
                    this.validLootEntities.Add((true, this.invalidLootEntities[i].Index, lootObjectsEntity));
                    this.invalidLootEntities.RemoveAt(i);
                }
            }

            var validScatterCheckMap = new ScatterReadMap(this.validLootEntities.Count);
            var validCheckRound1 = validScatterCheckMap.AddRound();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                var lootUnknownPtr = validCheckRound1.AddEntry<ulong>(i, 0, this.validLootEntities[i].Pointer, null);
            }

            validScatterCheckMap.Execute();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                validScatterCheckMap.Results[i][0].TryGetResult<ulong>(out var lootUnknownPtr);
                if (lootUnknownPtr == 0)
                {
                    this.invalidLootEntities.Add((true, this.validLootEntities[i].Index, this.validLootEntities[i].Pointer));
                    this.validLootEntities.RemoveAt(i);
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

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                var lootUnknownPtr = vRound1.AddEntry<ulong>(i, 0, validLootEntities[i].Pointer, null, Offsets.LootListItem.LootUnknownPtr);

                var interactiveClass = vRound2.AddEntry<ulong>(i, 1, lootUnknownPtr, null, Offsets.LootUnknownPtr.LootInteractiveClass);

                var lootBaseObject = vRound3.AddEntry<ulong>(i, 2, interactiveClass, null, Offsets.LootInteractiveClass.LootBaseObject);
                var entry7 = vRound3.AddEntry<ulong>(i, 3, interactiveClass, null, 0x0);
                var item = vRound3.AddEntry<ulong>(i, 4, interactiveClass, null, Offsets.ObservedLootItem.Item);
                var containerIDPtr = vRound3.AddEntry<ulong>(i, 5, interactiveClass, null, Offsets.LootableContainer.Template);
                var itemOwner = vRound3.AddEntry<ulong>(i, 6, interactiveClass, null, Offsets.LootInteractiveClass.ItemOwner);
                var containerItemOwner = vRound3.AddEntry<ulong>(i, 7, interactiveClass, null, Offsets.LootInteractiveClass.ContainerItemOwner);

                var gameObject = vRound4.AddEntry<ulong>(i, 8, lootBaseObject, null, Offsets.LootBaseObject.GameObject);
                var entry9 = vRound4.AddEntry<ulong>(i, 9, entry7, null, 0x0);
                var itemTemplate = vRound4.AddEntry<ulong>(i, 10, item, null, Offsets.LootItemBase.ItemTemplate);
                var containerItemBase = vRound4.AddEntry<ulong>(i, 11, containerItemOwner, null, Offsets.ContainerItemOwner.Item);

                var objectName = vRound5.AddEntry<ulong>(i, 12, gameObject, null, Offsets.GameObject.ObjectName);
                var objectClass = vRound5.AddEntry<ulong>(i, 13, gameObject, null, Offsets.GameObject.ObjectClass);
                var entry10 = vRound5.AddEntry<ulong>(i, 14, entry9, null, 0x48);
                var isQuestItem = vRound5.AddEntry<bool>(i, 15, itemTemplate, null, Offsets.ItemTemplate.IsQuestItem);
                var BSGIdPtr = vRound5.AddEntry<ulong>(i, 16, itemTemplate, null, Offsets.ItemTemplate.BsgId);
                var rootItem = vRound5.AddEntry<ulong>(i, 17, itemOwner, null, Offsets.ItemOwner.Item);
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
                .Where(x => x.Value.CorpsePtr > 0)
                .Select(x => x.Value)
                .ToList() ?? new List<Player>();

            Parallel.For(0, this.validLootEntities.Count, i =>
            {
                if (validScatterMap.Results[i][1].TryGetResult<ulong>(out var interactiveClass))
                {
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
                            try
                            {
                                Vector3 position = new Transform(posToTransform, false).GetPosition(null);

                                if (isCorpse)
                                {
                                    if (!this.savedLootCorpsesInfo.Any(x => x.InteractiveClass == interactiveClass))
                                    {
                                        if (!validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                                            return;

                                        //Player player = players.FirstOrDefault(x => x.CorpsePtr == interactiveClass);
                                        var playerName = containerName.Split('(', ')');
                                        var test = playerName.Count() > 1 ? playerName[1] : playerName[0];
                                        Player player = players.FirstOrDefault(x => x.Name == test);

                                        this.savedLootCorpsesInfo.Add(new CorpseInfo { InteractiveClass = interactiveClass, Position = position, Slots = slots, Player = player });
                                    }
                                }
                                else if (isContainer)
                                {
                                    if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                                    {
                                        if (!validScatterMap.Results[i][5].TryGetResult<ulong>(out var containerIDPtr))
                                            return;

                                        var containerID = Memory.ReadUnityString(containerIDPtr);
                                        if (!validScatterMap.Results[i][18].TryGetResult<ulong>(out var grids))
                                            return;

                                        TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container);

                                        this.savedLootContainersInfo.Add(new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = container?.Name ?? containerName, Grids = grids });
                                    }
                                }
                                else if (isLooseLoot) // handle loose weapons / gear
                                {
                                    if (!this.savedLootItemsInfo.Any(x => x.InteractiveClass == interactiveClass))
                                    {
                                        if (!validScatterMap.Results[i][15].TryGetResult<bool>(out var questItem))
                                            return;
                                        if (!validScatterMap.Results[i][16].TryGetResult<ulong>(out var BSGIDPtr))
                                            return;

                                        var id = Memory.ReadUnityString(BSGIDPtr);

                                        if (id == null)
                                            return;

                                        var searchableItem = TarkovDevManager.AllItems.Values.FirstOrDefault(x => x.Item.id == id && x.Item.categories.FirstOrDefault(x => x.name == "Weapon" || x.name == "Searchable item") != null);

                                        if (searchableItem != null)
                                        {
                                            if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                                            {
                                                try
                                                {
                                                    var isWeapon = searchableItem.Item.categories.FirstOrDefault(x => x.name == "Weapon") != null;
                                                    var container = new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = searchableItem.Item.shortName ?? containerName };

                                                    if (validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                                                        container.Slots = slots;

                                                    if (validScatterMap.Results[i][17].TryGetResult<ulong>(out var rootItem))
                                                    {
                                                        var itemGrids = Memory.ReadPtr(rootItem + 0x70);
                                                        container.Grids = itemGrids;
                                                    }

                                                    this.savedLootContainersInfo.Add(container);
                                                }
                                                catch { }
                                            }
                                        }
                                        else
                                        {
                                            this.savedLootItemsInfo.Add(new LootItemInfo { InteractiveClass = interactiveClass, QuestItem = questItem, Position = position, ItemID = id });
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            });
        }
        /// <summary>
        /// Creates LootableObjects from saved loot info
        /// </summary>
        public void FillLoot()
        {
            var loot = new List<LootableObject>();

            foreach (var savedLootItem in this.savedLootItemsInfo)
            {
                if (this.validLootEntities.Any(x => x.Pointer == savedLootItem.InteractiveClass))
                {
                    if (!savedLootItem.QuestItem)
                    {
                        if (TarkovDevManager.AllItems.TryGetValue(savedLootItem.ItemID, out var lootItem))
                        {
                            loot.Add(new LootItem
                            {
                                ID = savedLootItem.ItemID,
                                Name = lootItem.Name,
                                Position = savedLootItem.Position,
                                Item = lootItem.Item,
                                Important = lootItem.Important,
                                AlwaysShow = lootItem.AlwaysShow,
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
                        if (this.QuestItems != null)
                        {
                            var questItem = this.QuestItems.Where(x => x.Id == savedLootItem.ItemID).FirstOrDefault();
                            if (questItem != null)
                            {
                                questItem.Position = savedLootItem.Position;
                            }
                        }
                    }
                }
                else
                {
                    this.savedLootItemsInfo = new ConcurrentBag<LootItemInfo>(this.savedLootItemsInfo.Where(x => x.InteractiveClass != savedLootItem.InteractiveClass));
                }
            }

            // create Corpse objects
            foreach (var savedLootCorpse in this.savedLootCorpsesInfo)
            {
                if (this.validLootEntities.Any(x => x.Pointer == savedLootCorpse.InteractiveClass))
                {
                    var gearItems = new List<GearItem>();

                    var corpse = new LootCorpse
                    {
                        Name = "Corpse" + (savedLootCorpse.Player != null ? $" {savedLootCorpse.Player.Name}" : ""),
                        Position = savedLootCorpse.Position,
                        InteractiveClass = savedLootCorpse.InteractiveClass,
                        Slots = savedLootCorpse.Slots,
                        Items = gearItems
                    };

                    if (savedLootCorpse.Player is not null)
                    {
                        corpse.Player = savedLootCorpse.Player;
                    }

                    LootManager.GetItemsInSlots(savedLootCorpse.Slots, savedLootCorpse.Position, corpse.Items);

                    corpse.Items = corpse.Items.Where(item => item.TotalValue > 0).ToList();

                    foreach (var gearItem in corpse.Items)
                    {
                        int index = gearItem.Loot.FindIndex(lootItem => lootItem.ID == gearItem.ID);
                        if (index != -1)
                        {
                            gearItem.Loot.RemoveAt(index);
                        }

                        gearItem.Loot = MergeDupelicateLootItems(gearItem.Loot);
                    }

                    corpse.Items = corpse.Items.OrderBy(x => x.TotalValue).ToList();
                    corpse.UpdateValue();

                    loot.Add(corpse);
                }
                else
                {
                    this.savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>(this.savedLootCorpsesInfo.Where(x => x.InteractiveClass != savedLootCorpse.InteractiveClass));
                }
            }

            // create Container objects, merge dupe entries based on position + name
            // (helps deal with multiple entries for the same container)
            var groupedContainers = this.savedLootContainersInfo.GroupBy(container => (container.Position, container.Name)).ToList();
            foreach (var savedContainerItem in groupedContainers)
            {
                var firstContainer = savedContainerItem.First();

                if (this.validLootEntities.Any(x => x.Pointer == firstContainer.InteractiveClass))
                {
                    var lootItems = new List<LootItem>();

                    var mergedContainer = new LootContainer
                    {
                        Name = firstContainer.Name,
                        Position = firstContainer.Position,
                        InteractiveClass = firstContainer.InteractiveClass,
                        Grids = firstContainer.Grids,
                        Items = lootItems,
                    };

                    if (mergedContainer.Name.Contains("COLLIDER(1)"))
                    {
                        mergedContainer.Name = "Airdrop";
                        mergedContainer.AlwaysShow = true;
                    }

                    if (firstContainer.Name == "M4A1")
                        Console.WriteLine("test");

                    if (firstContainer.Slots != 0)
                    {
                        LootManager.GetItemsInSlots(firstContainer.Slots, mergedContainer.Position, mergedContainer.Items);
                    }

                    if (firstContainer.Grids != 0)
                    {
                        LootManager.GetItemsInGrid(mergedContainer.Grids, mergedContainer.Position, mergedContainer.Items);
                    }

                    mergedContainer.Items = LootManager.MergeDupelicateLootItems(mergedContainer.Items);
                    mergedContainer.UpdateValue();

                    loot.Add(mergedContainer);
                }
                else
                {
                    this.savedLootContainersInfo = new ConcurrentBag<ContainerInfo>(this.savedLootContainersInfo.Where(x => x.InteractiveClass != firstContainer.InteractiveClass));
                }
            }

            this.Loot = new(loot);
        }
        /// <summary>
        /// Applies loot filter
        /// </summary>
        public void ApplyFilter()
        {
            var loot = this.Loot;
            if (loot is null)
                return;

            var orderedActiveFilters = _config.Filters
                .Where(filter => filter.IsActive)
                .OrderBy(filter => filter.Order)
                .ToList();

            var itemIdColorPairs = orderedActiveFilters
                .SelectMany(filter => filter.Items.Select(itemId => new { ItemId = itemId, Color = filter.Color }))
                .ToDictionary(pair => pair.ItemId, pair => pair.Color);

            var filteredItems = new List<LootableObject>();

            // Add loose loot
            foreach (var lootItem in loot.OfType<LootItem>())
            {
                var isValuable = lootItem.Value > _config.MinLootValue;
                var isImportant = lootItem.Value > _config.MinImportantLootValue;
                var isFiltered = itemIdColorPairs.ContainsKey(lootItem.ID);

                if (isFiltered || isImportant)
                {
                    lootItem.Important = true;

                    if (isFiltered)
                        lootItem.Color = itemIdColorPairs[lootItem.ID];
                }

                if (isFiltered || isValuable || lootItem.AlwaysShow)
                    filteredItems.Add(lootItem);
            }

            // Add containers
            foreach (var container in loot.OfType<LootContainer>())
            {
                var tempContainer = new LootContainer(container);
                var hasImportantOrFilteredItems = false;

                if (tempContainer.Name == "M4A1")
                {
                    Console.WriteLine("test");
                }

                foreach (var item in tempContainer.Items)
                {
                    var isImportant = item.Value > _config.MinImportantLootValue;
                    var isFiltered = itemIdColorPairs.ContainsKey(item.ID);

                    if (isFiltered || isImportant)
                    {
                        item.Important = true;
                        tempContainer.Important = true;
                        hasImportantOrFilteredItems = true;

                        if (isFiltered)
                            item.Color = itemIdColorPairs[item.ID];
                    }
                }

                if (hasImportantOrFilteredItems)
                {
                    var firstMatchingItem = tempContainer.Items
                        .FirstOrDefault(item => itemIdColorPairs.ContainsKey(item.ID));

                    if (firstMatchingItem is not null)
                        tempContainer.Color = firstMatchingItem.Color;
                }

                if (tempContainer.Items.Any(item => item.Value > _config.MinLootValue) || tempContainer.Important || tempContainer.AlwaysShow)
                    filteredItems.Add(tempContainer);
            }

            // Add corpses
            foreach (var corpse in loot.OfType<LootCorpse>())
            {
                var tempCorpse = new LootCorpse(corpse);
                var hasImportantOrFilteredItems = false;
                LootItem lowestOrderLootItem = null;
                GearItem lowestOrderGearItem = null;

                foreach (var gearItem in tempCorpse.Items)
                {
                    var isGearImportant = gearItem.TotalValue > _config.MinImportantLootValue;
                    var isGearFiltered = itemIdColorPairs.ContainsKey(gearItem.ID);

                    if (isGearImportant || isGearFiltered)
                    {
                        gearItem.Important = true;
                        tempCorpse.Important = true;
                        hasImportantOrFilteredItems = true;

                        if (isGearFiltered)
                        {
                            gearItem.Color = itemIdColorPairs[gearItem.ID];

                            var gearItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(gearItem.ID));
                            if (gearItemFilter != null && (lowestOrderGearItem == null || gearItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
                            {
                                lowestOrderGearItem = gearItem;
                            }
                        }

                        foreach (var lootItem in gearItem.Loot)
                        {
                            var isLootImportant = lootItem.Value > _config.MinImportantLootValue;
                            var isLootFiltered = itemIdColorPairs.ContainsKey(lootItem.ID);

                            if (isLootImportant || isLootFiltered)
                            {
                                lootItem.Important = true;
                                gearItem.Important = true;
                                tempCorpse.Important = true;
                                hasImportantOrFilteredItems = true;

                                if (isLootFiltered)
                                {
                                    lootItem.Color = itemIdColorPairs[lootItem.ID];

                                    var lootItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(lootItem.ID));
                                    if (lootItemFilter != null && (lowestOrderLootItem == null || lootItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order))
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
            }

            this.Filter = new ReadOnlyCollection<LootableObject>(filteredItems.ToList());
        }
        /// <summary>
        /// Removes an item from the loot filter list
        /// </summary>
        /// <param name="itemToRemove">The item to remove</param>
        public void RemoveFilterItem(LootItem itemToRemove)
        {
            var filter = this.Filter.ToList();
            filter.Remove(itemToRemove);

            this.Filter = new ReadOnlyCollection<LootableObject>(new List<LootableObject>(filter));
            this.ApplyFilter();
        }

        /// <summary>
        /// Recursively searches items within a grid
        /// </summary>
        private static void GetItemsInGrid(ulong gridsArrayPtr, Vector3 position, List<LootItem> containerLoot, int recurseDepth = 0)
        {
            if (gridsArrayPtr == 0 || recurseDepth > 3)
                return;

            try
            {
                int currentChildrenCount = CalculateChildrenCount(gridsArrayPtr);

                if (LootManager.gridCache.TryGetValue(gridsArrayPtr, out var cacheEntry))
                {
                    if (currentChildrenCount == cacheEntry.ChildrenCount)
                    {
                        containerLoot.AddRange(cacheEntry.CachedLootItems);
                        return;
                    }
                }

                var newCachedLootItems = new List<LootItem>();
                ProcessGrid(gridsArrayPtr, position, newCachedLootItems, recurseDepth);

                LootManager.gridCache[gridsArrayPtr] = new GridCacheEntry
                {
                    ChildrenCount = currentChildrenCount,
                    CachedLootItems = newCachedLootItems
                };

                containerLoot.AddRange(newCachedLootItems);
            }
            catch { }
        }

        private static void ProcessGrid(ulong gridsArrayPtr, Vector3 position, List<LootItem> cachedLootItems, int recurseDepth)
        {
            var gridsArray = new MemArray(gridsArrayPtr);
            foreach (var grid in gridsArray.Data)
            {
                var gridEnumerableClass = Memory.ReadPtr(grid + Offsets.Grids.GridsEnumerableClass);
                var itemListPtr = Memory.ReadPtr(gridEnumerableClass + Offsets.UnityList.Count);
                var itemList = new MemList(itemListPtr);

                foreach (var childItem in itemList.Data)
                {
                    var childItemTemplate = Memory.ReadPtr(childItem + Offsets.LootItemBase.ItemTemplate);
                    if (childItemTemplate == 0)
                        continue;

                    var childItemIdPtr = Memory.ReadPtr(childItemTemplate + Offsets.ItemTemplate.BsgId);
                    if (childItemIdPtr == 0)
                        continue;

                    var childItemId = Memory.ReadUnityString(childItemIdPtr).Replace("\\0", "");
                    if (TarkovDevManager.AllItems.TryGetValue(childItemId, out var childLootItem))
                    {
                        var newItem = new LootItem
                        {
                            Name = childLootItem.Name,
                            ID = childItemId,
                            AlwaysShow = childLootItem.AlwaysShow,
                            Important = childLootItem.Important,
                            Position = position,
                            Item = childLootItem.Item,
                            Value = TarkovDevManager.GetItemValue(childLootItem.Item)
                        };

                        cachedLootItems.Add(newItem);
                    }

                    var childGridsArrayPtr = Memory.ReadPtrNullable(childItem + Offsets.LootItemBase.Grids);
                    if (childGridsArrayPtr != 0)
                    {
                        LootManager.GetItemsInGrid(childGridsArrayPtr, position, cachedLootItems, recurseDepth + 1);
                    }
                }
            }
        }

        private static void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<GearItem> gearItems)
        {
            var slotDict = GetSlotDictionary(slotItemBase);

            if (slotDict.Count == 0 || slotItemBase == 0)
                return;

            foreach (var kvp in slotDict)
            {
                var slotName = kvp.Key;
                var slot = kvp.Value;
                var containedItem = Memory.ReadPtrNullable(slot + Offsets.Slot.ContainedItem);

                if (containedItem == 0x0)
                {
                    continue;
                }

                var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                var id = Memory.ReadUnityString(idPtr);
                var isPocket = slotName.Contains("Pocket", StringComparison.OrdinalIgnoreCase);

                SlotCacheEntry cacheEntry = LootManager.slotCache.GetOrAdd(slotItemBase, _ => new SlotCacheEntry());
                GearItem cachedGearItem = null;

                if (!cacheEntry.CachedGearItems.TryGetValue(slotName, out cachedGearItem) || cachedGearItem.ID != id)
                {
                    LootItem lootItem = null;
                    if (TarkovDevManager.AllItems.TryGetValue(id, out lootItem) || isPocket)
                    {
                        var longName = isPocket ? "Pocket" : lootItem?.Item.name ?? "Unknown";
                        var shortName = isPocket ? "Pocket" : lootItem?.Item.shortName ?? "Unknown";
                        var value = isPocket || lootItem == null ? 0 : TarkovDevManager.GetItemValue(lootItem.Item);
                        var lootItems = new List<LootItem>();

                        LootManager.ProcessNestedItems(lootItems, containedItem, position);

                        cachedGearItem = new GearItem
                        {
                            ID = id,
                            Long = longName,
                            Short = shortName,
                            Value = value,
                            HasThermal = false,
                            Loot = lootItems
                        };

                        cacheEntry.CachedGearItems[slotName] = cachedGearItem;
                    }
                }

                gearItems.Add(cachedGearItem);
            }
        }

        private static void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<LootItem> loot)
        {
            var slotDict = LootManager.GetSlotDictionary(slotItemBase);

            if (slotDict.Count == 0 || slotItemBase == 0)
                return;

            foreach (var kvp in slotDict)
            {
                var slotName = kvp.Key;
                var slot = kvp.Value;
                var containedItem = Memory.ReadPtrNullable(slot + Offsets.Slot.ContainedItem);

                if (containedItem == 0x0)
                {
                    continue;
                }

                var inventorytemplate = Memory.ReadPtr(containedItem + Offsets.LootItemBase.ItemTemplate);
                var idPtr = Memory.ReadPtr(inventorytemplate + Offsets.ItemTemplate.BsgId);
                var id = Memory.ReadUnityString(idPtr);

                if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
                {
                    SlotCacheEntry cacheEntry = LootManager.slotCache.GetOrAdd(slotItemBase, _ => new SlotCacheEntry());

                    if (cacheEntry.CachedLootItems.TryGetValue(slotName, out var cachedLootItem))
                    {
                        loot.Add(cachedLootItem);
                    }
                    else
                    {
                        var newLootItem = new LootItem
                        {
                            ID = id,
                            Name = lootItem.Item.name,
                            AlwaysShow = lootItem.AlwaysShow,
                            Important = lootItem.Important,
                            Position = position,
                            Item = lootItem.Item,
                            Value = TarkovDevManager.GetItemValue(lootItem.Item)
                        };

                        loot.Add(newLootItem);
                        cacheEntry.CachedLootItems[slotName] = newLootItem;
                    }

                    LootManager.ProcessNestedItems(loot, containedItem, position);
                }
            }
        }

        private static void ProcessNestedItems(List<LootItem> loot, ulong containedItem, Vector3 position)
        {
            var slotsPtr = Memory.ReadPtrNullable(containedItem + Offsets.Equipment.Slots);
            if (slotsPtr != 0)
            {
                LootManager.GetItemsInSlots(slotsPtr, position, loot);
            }

            var gridsPtr = Memory.ReadPtrNullable(containedItem + Offsets.LootItemBase.Grids);
            if (gridsPtr != 0)
            {
                LootManager.GetItemsInGrid(gridsPtr, position, loot);
            }
        }

        private static int CalculateChildrenCount(ulong gridsArrayPtr)
        {
            int totalChildrenCount = 0;
            var gridsArray = new MemArray(gridsArrayPtr);
            foreach (var grid in gridsArray.Data)
            {
                var gridEnumerableClass = Memory.ReadPtr(grid + Offsets.Grids.GridsEnumerableClass);
                var itemListPtr = Memory.ReadPtr(gridEnumerableClass + Offsets.UnityList.Count);
                var itemList = new MemList(itemListPtr);
                totalChildrenCount += itemList.Count; // Sum up the count of items in each grid
            }
            return totalChildrenCount;
        }

        private static Dictionary<string, ulong> GetSlotDictionary(ulong slotItemBase)
        {
            try
            {
                var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);
                var slotDict = new Dictionary<string, ulong>(size, StringComparer.OrdinalIgnoreCase);
                var slotItemBaseStart = slotItemBase + Offsets.UnityListBase.Start;

                for (int slotID = 0; slotID < size; slotID++)
                {
                    var slotPtr = Memory.ReadPtr(slotItemBaseStart + (uint)slotID * Offsets.Slot.Size);
                    var namePtr = Memory.ReadPtr(slotPtr + Offsets.Slot.Name);
                    var name = Memory.ReadUnityString(namePtr);

                    if (!LootManager.slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        slotDict[name] = slotPtr;
                    }
                }

                return slotDict;
            }
            catch { return new Dictionary<string, ulong>(0, StringComparer.OrdinalIgnoreCase); }
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
                    Value = firstItem.Value * count,
                    Color = firstItem.Color
                };

                return mergedItem;
            })
            .OrderBy(lootItem => lootItem.Value)
            .ToList();
        }

        internal class GridCacheEntry
        {
            public int ChildrenCount { get; set; }
            public List<LootItem> CachedLootItems { get; set; } = new List<LootItem>();
        }

        internal class SlotCacheEntry
        {
            public Dictionary<string, GearItem> CachedGearItems { get; set; } = new Dictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, LootItem> CachedLootItems { get; set; } = new Dictionary<string, LootItem>(StringComparer.OrdinalIgnoreCase);
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

    public abstract class LootableObject
    {
        public string Name { get; set; }
        public bool Important { get; set; }
        public bool AlwaysShow { get; set; }
        public int Value { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
        public LootFilter.Colors Color { get; set; }
    }

    public class LootItem : LootableObject
    {
        public string ID { get; set; }
        public TarkovItem Item { get; set; }

        public LootItem() { }

        // for deep copying
        public LootItem(LootItem other)
        {
            base.Name = other.Name;
            base.Important = other.Important;
            base.AlwaysShow = other.AlwaysShow;
            base.Position = other.Position;
            base.Value = other.Value;

            this.Item = other.Item;
            this.ID = other.ID;

            base.Value = other.Value;
        }

        public string GetFormattedValue() => TarkovDevManager.FormatNumber(this.Value);
        public string GetFormattedValueName() => this.Value > 0 ? $"[{this.GetFormattedValue()}] {base.Name}" : base.Name;
        public string GetFormattedValueShortName() => this.Value > 0 ? $"[{this.GetFormattedValue()}] {this.Item.shortName}" : this.Item.shortName;

    }

    public class LootContainer : LootableObject
    {
        public ulong InteractiveClass { get; set; }
        public ulong Grids;
        public List<LootItem> Items { get; set; }

        public LootContainer() { }

        // for deep copying
        public LootContainer(LootContainer other)
        {
            base.Name = other.Name;
            base.Important = other.Important;
            base.AlwaysShow = other.AlwaysShow;
            base.Position = other.Position;

            this.InteractiveClass = other.InteractiveClass;
            this.Grids = other.Grids;
            this.Items = other.Items.Select(item => new LootItem(item)).ToList();
        }

        public void UpdateValue() => this.Value = this.Items.Sum(item => item.Value);

    }

    public class LootCorpse : LootableObject
    {
        public ulong InteractiveClass { get; set; }
        public ulong Slots { get; set; }
        public List<GearItem> Items { get; set; }
        public Player Player { get; set; }

        public LootCorpse() { }

        // for deep copying
        public LootCorpse(LootCorpse other)
        {
            base.Name = other.Name;
            base.Important = other.Important;
            base.AlwaysShow = other.AlwaysShow;
            base.Position = other.Position;
            base.Value = other.Value;

            this.InteractiveClass = other.InteractiveClass;
            this.Slots = other.Slots;
            this.Items = other.Items.Select(item => new GearItem(item)).ToList();
        }

        public void UpdateValue() => this.Value = this.Items.Sum(item => item.TotalValue);
    }

    public class GearItem : LootableObject
    {
        public string ID { get; set; }
        public string Long { get; set; }
        public string Short { get; set; }
        public int LootValue { get => this.Loot.Sum(x => x.Value); }
        public int TotalValue { get => base.Value + this.LootValue; }
        public List<LootItem> Loot { get; set; }
        public bool HasThermal { get; set; }

        public GearItem() { }

        // for deep copying
        public GearItem(GearItem other)
        {
            base.Important = other.Important;
            base.AlwaysShow = other.AlwaysShow;
            base.Position = other.Position;
            base.Value = other.Value;

            this.ID = other.ID;
            this.Long = other.Long;
            this.Short = other.Short;
            this.Loot = other.Loot.Select(item => new LootItem(item)).ToList();
            this.HasThermal = other.HasThermal;
        }

        public string GetFormattedValue() => TarkovDevManager.FormatNumber(base.Value);
        public string GetFormattedLootValue() => TarkovDevManager.FormatNumber(this.LootValue);
        public string GetFormattedTotalValue() => TarkovDevManager.FormatNumber(this.TotalValue);

        public string GetFormattedValueName() => base.Value > 0 ? $"[{this.GetFormattedValue()}] {this.Long}" : this.Long;
        public string GetFormattedValueShortName() => base.Value > 0 ? $"[{this.GetFormattedValue()}] {this.Short}" : this.Short;
        public string GetFormattedTotalValueName() => this.TotalValue > 0 ? $"[{this.GetFormattedTotalValue()}] {this.Long}" : this.Long;
    }

    struct ContainerInfo
    {
        public ulong InteractiveClass;
        public Vector3 Position;
        public string Name;
        public ulong Grids;
        public ulong Slots;
        public bool IsCorpse;
    }

    struct LootItemInfo
    {
        public ulong InteractiveClass;
        public bool QuestItem;
        public Vector3 Position;
        public string ItemID;
    }

    struct CorpseInfo
    {
        public ulong InteractiveClass;
        public ulong Slots;
        public Vector3 Position;
        public Player Player;
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