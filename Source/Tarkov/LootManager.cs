using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
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

        private ConcurrentDictionary<ulong, GridCacheEntry> gridCache;
        private ConcurrentDictionary<ulong, SlotCacheEntry> slotCache;
        private List<(bool Valid, int Index, ulong Pointer)> validLootEntities;
        private List<(bool Valid, int Index, ulong Pointer)> invalidLootEntities;
        private ConcurrentBag<ContainerInfo> savedLootContainersInfo;
        private ConcurrentBag<CorpseInfo> savedLootCorpsesInfo;
        private ConcurrentBag<LootItemInfo> savedLootItemsInfo;
        private static readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };

        private Thread autoRefreshThread;

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

            this.gridCache = new ConcurrentDictionary<ulong, GridCacheEntry>();
            this.slotCache = new ConcurrentDictionary<ulong, SlotCacheEntry>();
            this.validLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
            this.invalidLootEntities = new List<(bool Valid, int Index, ulong Pointer)>();
            this.savedLootContainersInfo = new ConcurrentBag<ContainerInfo>();
            this.savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>();
            this.savedLootItemsInfo = new ConcurrentBag<LootItemInfo>();

            this.RefreshLootListAddresses();

            if (this._config.AutoLootRefreshEnabled)
            {
                this.StartAutoRefresh();
            }
            else
            {
                this.RefreshLoot();
            }
        }
        #endregion

        #region Methods
        public void StartAutoRefresh()
        {
            if (this.autoRefreshThread != null && this.autoRefreshThread.IsAlive)
            {
                return;
            }

            this.autoRefreshThread = new Thread(this.LootManagerWorkerThread)
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            this.autoRefreshThread.Start();
        }

        public void StopAutoRefresh()
        {
            this.autoRefreshThread?.Join();
            this.autoRefreshThread = null;
        }

        private void LootManagerWorkerThread()
        {
            while (Memory.GameStatus == Game.GameStatus.InGame && this._config.AutoLootRefreshEnabled && this._config.LootEnabled)
            {
                this.RefreshLoot();
                Thread.Sleep(this.CurrentMapName == "TarkovStreets" ? 30000 : 10000);
            }
            Console.WriteLine("[LootManager] Refresh thread stopped.");
        }

        private void RefreshLoot()
        {
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start(); // Start timing
            //Console.WriteLine("[LootManager] Refreshing Loot...");
            this.GetLootList();
            this.GetLoot();
            this.FillLoot();
            this.ApplyFilter();
        //    stopwatch.Stop(); // Stop timing
        //    TimeSpan ts = stopwatch.Elapsed;
        //    string elapsedTime = String.Format("RunTime {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        //    Console.WriteLine("[LootManager] RunTime " + elapsedTime);
        //    Console.WriteLine($"[LootManager] Total loot items processed: {this.savedLootItemsInfo.Count + this.savedLootContainersInfo.Count + this.savedLootCorpsesInfo.Count}");
        //    Console.WriteLine($"[LootManager] Found {this.savedLootItemsInfo.Count} loose loot items");
        //    Console.WriteLine($"[LootManager] Found {this.savedLootContainersInfo.Count} lootable containers");
        //    Console.WriteLine($"[LootManager] Found {this.savedLootCorpsesInfo.Count} lootable corpses");
        }

        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        private void RefreshLootListAddresses()
        {
            var count = 3;

            var scatterReadMap = new ScatterReadMap(count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var lootlistPtr = round1.AddEntry<ulong>(i, 0, this.localGameWorld, null, Offsets.LocalGameWorld.LootList);
                var lootListEntity = round2.AddEntry<ulong>(i, 1, lootlistPtr, null, Offsets.UnityList.Base);
                var countLootListObjects = round3.AddEntry<int>(i, 2, lootListEntity, null, Offsets.UnityList.Count);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < count; i++)
            {
                if (scatterReadMap.Results[i][0].TryGetResult<ulong>(out var lootlistPtr))
                    this.lootlistPtr = lootlistPtr;

                if (scatterReadMap.Results[i][1].TryGetResult<ulong>(out var lootListEntity))
                    this.lootListEntity = lootListEntity;

                if (scatterReadMap.Results[i][2].TryGetResult<int>(out var countLootListObjects))
                    this.countLootListObjects = countLootListObjects;

            }
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
            };

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
                if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity) || lootObjectsEntity == 0)
                    continue;

                this.validLootEntities.Add((true, this.invalidLootEntities[i].Index, lootObjectsEntity));
                this.invalidLootEntities.RemoveAt(i);
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
                if (!validScatterCheckMap.Results[i][0].TryGetResult<ulong>(out var lootUnknownPtr) || lootUnknownPtr != 0)
                    continue;

                this.invalidLootEntities.Add((true, this.validLootEntities[i].Index, this.validLootEntities[i].Pointer));
                this.validLootEntities.RemoveAt(i);
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

            for (int i = 0; i < this.validLootEntities.Count; i++)
            //Parallel.For(0, this.validLootEntities.Count, i =>
            {
                try
                {
                    if (!validScatterMap.Results[i][1].TryGetResult<ulong>(out var interactiveClass))
                        continue;
                    if (!validScatterMap.Results[i][2].TryGetResult<ulong>(out var lootBaseObject))
                        continue;
                    if (!validScatterMap.Results[i][24].TryGetResult<ulong>(out var posToTransform))
                        continue;
                    if (!validScatterMap.Results[i][20].TryGetResult<string>(out var containerName))
                        continue;
                    if (!validScatterMap.Results[i][19].TryGetResult<string>(out var className))
                        continue;

                    if (containerName.Contains("script", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    else
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
                                            continue;

                                        var playerNameSplit = containerName.Split('(', ')');
                                        var playerName = playerNameSplit.Count() > 1 ? playerNameSplit[1] : playerNameSplit[0];

                                        this.savedLootCorpsesInfo.Add(new CorpseInfo { InteractiveClass = interactiveClass, Position = position, Slots = slots, PlayerName = playerName });
                                    }
                                }
                                else if (isContainer)
                                {
                                    if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                                    {
                                        if (!validScatterMap.Results[i][5].TryGetResult<ulong>(out var containerIDPtr))
                                            continue;

                                        if (!validScatterMap.Results[i][18].TryGetResult<ulong>(out var grids))
                                            continue;

                                        var containerID = Memory.ReadUnityString(containerIDPtr);

                                        TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container);

                                        this.savedLootContainersInfo.Add(new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = container?.Name ?? containerName, Grids = grids });
                                    }
                                }
                                else if (isLooseLoot) // handle loose weapons / gear
                                {
                                    if (!this.savedLootItemsInfo.Any(x => x.InteractiveClass == interactiveClass))
                                    {
                                        if (!validScatterMap.Results[i][15].TryGetResult<bool>(out var questItem))
                                            continue;
                                        if (!validScatterMap.Results[i][16].TryGetResult<ulong>(out var BSGIDPtr))
                                            continue;

                                        var id = Memory.ReadUnityString(BSGIDPtr);

                                        if (id == null)
                                            continue;

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
                                                catch { continue; }
                                            }
                                        }
                                        else
                                        {
                                            this.savedLootItemsInfo.Add(new LootItemInfo { InteractiveClass = interactiveClass, QuestItem = questItem, Position = position, ItemID = id });
                                        }
                                    }
                                }
                            }
                            catch { continue; }
                        }
                    }
                }
                catch { continue; }
            //});
            }
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
                        Name = "Corpse" + (savedLootCorpse.PlayerName != null ? $" {savedLootCorpse.PlayerName}" : ""),
                        Position = savedLootCorpse.Position,
                        InteractiveClass = savedLootCorpse.InteractiveClass,
                        Slots = savedLootCorpse.Slots,
                        Items = gearItems
                    };

                    if (corpse.Slots != 0)
                    {
                        this.GetItemsInSlots(corpse.Slots, corpse.Position, corpse.Items);
                    }

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
                        mergedContainer.Name = "AIRDROP";
                        mergedContainer.AlwaysShow = true;
                    }

                    if (firstContainer.Slots != 0)
                    {
                        this.GetItemsInSlots(firstContainer.Slots, mergedContainer.Position, mergedContainer.Items);
                    }

                    if (firstContainer.Grids != 0)
                    {
                        this.GetItemsInGrid(mergedContainer.Grids, mergedContainer.Position, mergedContainer.Items);
                    }

                    mergedContainer.Items = this.MergeDupelicateLootItems(mergedContainer.Items);
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
        private void GetItemsInGrid(ulong gridsArrayPtr, Vector3 position, List<LootItem> containerLoot, int recurseDepth = 0)
        {
            if (gridsArrayPtr == 0 || recurseDepth > 3)
                return;

            try
            {
                int currentChildrenCount = CalculateChildrenCount(gridsArrayPtr);

                if (this.gridCache.TryGetValue(gridsArrayPtr, out var cacheEntry))
                {
                    if (currentChildrenCount == cacheEntry.ChildrenCount)
                    {
                        containerLoot.AddRange(cacheEntry.CachedLootItems);
                        return;
                    }
                }

                var newCachedLootItems = new List<LootItem>();
                ProcessGrid(gridsArrayPtr, position, newCachedLootItems, recurseDepth);

                this.gridCache[gridsArrayPtr] = new GridCacheEntry
                {
                    ChildrenCount = currentChildrenCount,
                    CachedLootItems = newCachedLootItems
                };

                containerLoot.AddRange(newCachedLootItems);
            }
            catch { }
        }

        private void ProcessGrid(ulong gridsArrayPtr, Vector3 position, List<LootItem> cachedLootItems, int recurseDepth)
        {
            var gridsArrayCount = Memory.ReadValue<int>(gridsArrayPtr + Offsets.UnityList.Count);

            if (gridsArrayCount < 0 || gridsArrayCount > 4096)
                return;

            var scatterReadMap = new ScatterReadMap(gridsArrayCount);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();

            var gridItemBaseStart = gridsArrayPtr + Offsets.UnityListBase.Start;

            for (int i = 0; i < gridsArrayCount; i++)
            {
                var grid = round1.AddEntry<ulong>(i, 0, gridItemBaseStart, null, (uint)i * Offsets.Slot.Size);
                var gridEnumerableClass = round2.AddEntry<ulong>(i, 1, grid, null, Offsets.Grids.GridsEnumerableClass);
                var itemListPtr = round3.AddEntry<ulong>(i, 2, gridEnumerableClass, null, Offsets.UnityList.Count);
                var itemListCount = round4.AddEntry<int>(i, 3, itemListPtr, null, Offsets.UnityList.Count);
                var arrayBase = round4.AddEntry<ulong>(i, 4, itemListPtr, null, Offsets.UnityList.Base);
            }

            scatterReadMap.Execute();

            //Parallel.For(0, gridsArrayCount, i =>
            for (int i = 0; i < gridsArrayCount; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var grid))
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var gridEnumerableClass))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemListPtr))
                    continue;
                if (!scatterReadMap.Results[i][3].TryGetResult<int>(out var itemListCount))
                    continue;
                if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var arrayBase))
                    continue;

                var innerScatterReadMap = new ScatterReadMap(itemListCount);
                var innerRound1 = innerScatterReadMap.AddRound();
                var innerRound2 = innerScatterReadMap.AddRound();
                var innerRound3 = innerScatterReadMap.AddRound();

                for (int j = 0; j < itemListCount; j++)
                {
                    var childItem = innerRound1.AddEntry<ulong>(j, 0, arrayBase, null, Offsets.UnityListBase.Start + ((uint)j * 0x08));
                    var childItemTemplate = innerRound2.AddEntry<ulong>(j, 1, childItem, null, Offsets.LootItemBase.ItemTemplate);
                    var childGridsArrayPtr = innerRound2.AddEntry<ulong>(j, 2, childItem, null, Offsets.LootItemBase.Grids);
                    var childItemIdPtr = innerRound3.AddEntry<ulong>(j, 3, childItemTemplate, null, Offsets.ItemTemplate.BsgId);
                }

                innerScatterReadMap.Execute();

                //Parallel.For(0, itemListCount, j =>
                for (int j = 0; j < itemListCount; j++)
                {
                    if (!innerScatterReadMap.Results[j][0].TryGetResult<ulong>(out var childItem))
                        continue;
                    if (!innerScatterReadMap.Results[j][1].TryGetResult<ulong>(out var childItemTemplate))
                        continue;
                    if (!innerScatterReadMap.Results[j][3].TryGetResult<ulong>(out var childItemIdPtr))
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

                    if (!innerScatterReadMap.Results[j][2].TryGetResult<ulong>(out var childGridsArrayPtr))
                        continue;

                    this.GetItemsInGrid(childGridsArrayPtr, position, cachedLootItems, recurseDepth + 1);
                //});
                }
            //});
            }
        }

        private void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<GearItem> gearItems)
        {
            if (slotItemBase == 0)
                return;

            var slotDict = this.GetSlotDictionary(slotItemBase);

            if (slotDict == null || slotDict.Count == 0)
                return;

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
                var idPtr = round3.AddEntry<ulong>(i, 2, inventorytemplate, null, Offsets.ItemTemplate.BsgId);
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

                var id = Memory.ReadUnityString(idPtr);
                var slotName = slotNames[i];

                SlotCacheEntry cacheEntry = this.slotCache.GetOrAdd(slotItemBase, _ => new SlotCacheEntry());

                if (cacheEntry.CachedGearItems.TryGetValue(slotName, out var cachedGearItem))
                {
                    gearItems.Add(cachedGearItem);
                }
                else
                {
                    var isPocket = (slotName == "Pockets");

                    if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem) || isPocket)
                    {
                        var longName = isPocket ? "Pocket" : lootItem?.Item.name ?? "Unknown";
                        var shortName = isPocket ? "Pocket" : lootItem?.Item.shortName ?? "Unknown";
                        var value = isPocket || lootItem == null ? 0 : TarkovDevManager.GetItemValue(lootItem.Item);

                        var newGearItem = new GearItem
                        {
                            ID = id,
                            Long = longName,
                            Short = shortName,
                            Value = value,
                            HasThermal = false,
                            Loot = new List<LootItem>()
                        };

                        this.ProcessNestedItems(newGearItem.Loot, containedItem, position, isPocket ? 0 : 0);

                        gearItems.Add(newGearItem);
                        cacheEntry.CachedGearItems[slotName] = newGearItem;
                    }
                }
            }
        }

        private void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<LootItem> loot, int recurseDepth = 0)
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

            var slotNames = slotDict.Keys.ToList();
            var slotPtrs = slotDict.Values.ToList();

            for (int i = 0; i < slotDict.Count; i++)
            {
                var containedItem = round1.AddEntry<ulong>(i, 0, slotPtrs[i], null, Offsets.Slot.ContainedItem);
                var inventorytemplate = round2.AddEntry<ulong>(i, 1, containedItem, null, Offsets.LootItemBase.ItemTemplate);
                var idPtr = round3.AddEntry<ulong>(i, 2, inventorytemplate, null, Offsets.ItemTemplate.BsgId);
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

                var id = Memory.ReadUnityString(idPtr);
                var slotName = slotNames[i];

                SlotCacheEntry cacheEntry = this.slotCache.GetOrAdd(slotItemBase, _ => new SlotCacheEntry());

                if (cacheEntry.CachedLootItems.TryGetValue(slotName, out var cachedLootItem))
                {
                    loot.Add(cachedLootItem);
                }
                else
                {
                    if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem))
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
                }

                this.ProcessNestedItems(loot, containedItem, position, recurseDepth + 1);
            }
        }

        private void ProcessNestedItems(List<LootItem> loot, ulong containedItem, Vector3 position, int recursiveDepth = 0)
        {
            var slotsPtr = Memory.ReadPtrNullable(containedItem + Offsets.Equipment.Slots);
            if (slotsPtr != 0)
            {
                this.GetItemsInSlots(slotsPtr, position, loot, recursiveDepth);
            }

            var gridsPtr = Memory.ReadPtrNullable(containedItem + Offsets.LootItemBase.Grids);
            if (gridsPtr != 0)
            {
                this.GetItemsInGrid(gridsPtr, position, loot);
            }
        }

        private int CalculateChildrenCount(ulong gridsArrayPtr)
        {
            int totalChildrenCount = 0;
            var gridsArrayCount = Memory.ReadValue<int>(gridsArrayPtr + Offsets.UnityList.Count);

            if (gridsArrayCount < 0 || gridsArrayCount > 4096)
                return 0;

            var scatterReadMap = new ScatterReadMap(gridsArrayCount);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();

            var gridItemBaseStart = gridsArrayPtr + Offsets.UnityListBase.Start;

            for (int i = 0; i < gridsArrayCount; i++)
            {
                var grid = round1.AddEntry<ulong>(i, 0, gridItemBaseStart, null, (uint)i * Offsets.Slot.Size);
                var gridEnumerableClass = round2.AddEntry<ulong>(i, 1, grid, null, Offsets.Grids.GridsEnumerableClass);
                var itemListPtr = round3.AddEntry<ulong>(i, 2, gridEnumerableClass, null, Offsets.UnityList.Count);
                var itemListCount = round4.AddEntry<int>(i, 3, itemListPtr, null, Offsets.UnityList.Count);
            }

            scatterReadMap.Execute();

            //Parallel.For(0, gridsArrayCount, i =>
            for (int i = 0; i < gridsArrayCount; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var grid))
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var gridEnumerableClass))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemListPtr))
                    continue;
                if (!scatterReadMap.Results[i][3].TryGetResult<int>(out var itemListCount))
                    continue;

                totalChildrenCount += itemListCount;
            //});
            }

            return totalChildrenCount;
        }

        private Dictionary<string, ulong> GetSlotDictionary(ulong slotItemBase)
        {
            var slotDict = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var size = Memory.ReadValue<int>(slotItemBase + Offsets.UnityList.Count);

                if (size <= 0 || size > 25)
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

                    if (!LootManager.slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        slotDict[name] = slotPtr;
                    }
                }
            }
            catch { }

            return slotDict;
        }

        private List<LootItem> MergeDupelicateLootItems(List<LootItem> lootItems)
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
        public string PlayerName;
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