using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class LootManager
    {
        public ulong lootlistPtr;
        public ulong lootListEntity;
        public int countLootListObjects;
        public ulong localGameWorld;

        private bool hasCachedItems;
        private bool refreshingItems;

        private const int BATCH_LOOSE_LOOT = 40;
        private const int BATCH_CORPSES = 10;
        private const int BATCH_CONTAINERS = 10;

        private ConcurrentDictionary<ulong, GridCacheEntry> gridCache;
        private ConcurrentDictionary<ulong, SlotCacheEntry> slotCache;
        private ConcurrentBag<(bool Valid, int Index, ulong Pointer)> validLootEntities;
        private ConcurrentBag<(bool Valid, int Index, ulong Pointer)> invalidLootEntities;
        private ConcurrentBag<ContainerInfo> savedLootContainersInfo;
        private ConcurrentBag<CorpseInfo> savedLootCorpsesInfo;
        private ConcurrentBag<LootItemInfo> savedLootItemsInfo;
        private static readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SecuredContainer",
            "Dogtag",
            "Compass",
            "Eyewear",
            "ArmBand"
        };

        private Thread autoRefreshThread;
        private CancellationTokenSource autoRefreshCancellationTokenSource;

        private readonly Config _config;
        /// <summary>
        /// Filtered loot ready for display by GUI.
        /// </summary>
        public ConcurrentBag<LootableObject> Filter { get; private set; }
        /// <summary>
        /// All tracked loot/corpses in Local Game World.
        /// </summary>
        public ConcurrentBag<LootableObject> Loot { get; set; }

        public bool HasCachedItems
        {
            get => hasCachedItems;
        }

        public int TotalLooseLoot
        {
            get => savedLootItemsInfo.Count;
        }

        public int TotalContainers
        {
           get => savedLootContainersInfo.Count;
        }

        public int TotalCorpses
        {
            get => savedLootCorpsesInfo.Count;
        }
        /// <summary>
        /// all quest items
        /// </summary>
        private ConcurrentBag<QuestItem> QuestItems {
            get => Memory.QuestManager?.QuestItems ?? null;
        }

        private HashSet<string> RequiredQuestItems
        {
            get => QuestManager.RequiredItems ?? null;
        }

        public Dictionary<string, PaintColor.Colors> RequiredFilterItems { get; set; }

        private string CurrentMapName;
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
            this.validLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>();
            this.invalidLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>();
            this.savedLootContainersInfo = new ConcurrentBag<ContainerInfo>();
            this.savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>();
            this.savedLootItemsInfo = new ConcurrentBag<LootItemInfo>();
            
            this.hasCachedItems = false;
            this.refreshingItems = false;

            this.CurrentMapName = Memory.MapNameFormatted;

            if (this._config.LootItemRefresh)
                this.StartAutoRefresh();
            else if (this._config.ProcessLoot)
                this.RefreshLoot(true);
        }
        #endregion

        #region Methods
        public void StartAutoRefresh()
        {
            if (this.autoRefreshThread is not null && this.autoRefreshThread.IsAlive)
                return;

            this.refreshingItems = false;
            this.autoRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = this.autoRefreshCancellationTokenSource.Token;

            this.autoRefreshThread = new Thread(() => this.LootManagerWorkerThread(cancellationToken))
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            this.autoRefreshThread.Start();
        }

        public async Task StopAutoRefresh()
        {
            await Task.Run(() =>
            {
                if (this.autoRefreshCancellationTokenSource is not null)
                {
                    this.autoRefreshCancellationTokenSource.Cancel();
                    this.autoRefreshCancellationTokenSource.Dispose();
                    this.autoRefreshCancellationTokenSource = null;
                }

                if (this.autoRefreshThread is not null)
                {
                    this.autoRefreshThread.Join();
                    this.autoRefreshThread = null;
                }
            });
        }

        private void LootManagerWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && Memory.GameStatus == Game.GameStatus.InGame && this._config.ProcessLoot && this._config.LootItemRefresh)
            {
                Task.Run(async () => { await this.RefreshLoot(); });
                var sleepFor = this._config.LootItemRefreshSettings[this.CurrentMapName] * 1000;
                Thread.Sleep(sleepFor);
            }
            Program.Log("[LootManager] Refresh thread stopped.");
        }

        public async Task RefreshLoot(bool forceRefresh = false)
        {
            if (this.refreshingItems && !forceRefresh)
            {
                Program.Log("[LootManager] Loot refresh is already in progress.");
                return;
            }

            this.refreshingItems = true;

            if (forceRefresh)
            {
                await this.StopAutoRefresh();

                await Task.Run(() =>
                {
                    if (this._config.ProcessLoot && this._config.LootItemRefresh && this.autoRefreshThread is null)
                        this.StartAutoRefresh();
                });

                if (this.autoRefreshThread is not null)
                    return;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Program.Log("[LootManager] Refreshing Loot...");

            var sw = new Stopwatch();
            var swTotal = new Stopwatch();
            sw.Start();
            swTotal.Start();
            await Task.Run(async() => { this.GetLootList(); });
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("[LootManager] Finished GetLootList {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Restart();

            await Task.Run(async () => { this.GetLoot(); });
            ts = sw.Elapsed;
            elapsedTime = String.Format("[LootManager] Finished GetLoot {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Restart();

            await Task.Run(async () => { this.FillLoot(); });
            ts = sw.Elapsed;
            elapsedTime = String.Format("[LootManager] Finished FillLoot {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Stop();
            swTotal.Stop();
            ts = swTotal.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log("[LootManager] RunTime " + elapsedTime);
            Program.Log($"[LootManager] Found {savedLootItemsInfo.Count} loose loot items");
            Program.Log($"[LootManager] Found {savedLootContainersInfo.Count} lootable containers");
            Program.Log($"[LootManager] Found {savedLootCorpsesInfo.Count} lootable corpses");
            Program.Log($"[LootManager] Total loot items processed: {savedLootItemsInfo.Count + savedLootContainersInfo.Count + savedLootCorpsesInfo.Count}");
            Program.Log($"---------------------------------");

            if (!this.hasCachedItems)
                this.hasCachedItems = true;

            this.refreshingItems = false;
        }

        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        private void RefreshLootListAddresses()
        {
            var scatterReadMap = new ScatterReadMap(1);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            var lootlistPtr = round1.AddEntry<ulong>(0, 0, this.localGameWorld, null, Offsets.LocalGameWorld.LootList);
            var lootListEntity = round2.AddEntry<ulong>(0, 1, lootlistPtr, null, Offsets.UnityList.Base);
            var countLootListObjects = round3.AddEntry<int>(0, 2, lootlistPtr, null, Offsets.UnityList.Count);

            scatterReadMap.Execute();

            if (scatterReadMap.Results[0][0].TryGetResult<ulong>(out var lootlistPtrRslt))
                this.lootlistPtr = lootlistPtrRslt;

            if (scatterReadMap.Results[0][1].TryGetResult<ulong>(out var lootListEntityRslt))
                this.lootListEntity = lootListEntityRslt;

            if (scatterReadMap.Results[0][2].TryGetResult<int>(out var countLootListObjectsRslt))
                this.countLootListObjects = countLootListObjectsRslt;
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

            var lootEntitiesWithIndex = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>();

            var basePtrStart = this.lootListEntity + Offsets.UnityListBase.Start;

            for (int i = 0; i < this.countLootListObjects; i++)
            {
                var p1 = round1.AddEntry<ulong>(i, 0, basePtrStart + (uint)(i * 0x8));
            }

            scatterMap.Execute();

            for (int i = 0; i < this.countLootListObjects; i++)
            {
                scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity);

                if (lootObjectsEntity != 0)
                    lootEntitiesWithIndex.Add((true, i, lootObjectsEntity));
                else
                    lootEntitiesWithIndex.Add((false, i, this.lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8)));
            };

            var lootEntitiesLookup = lootEntitiesWithIndex.ToLookup(x => x.Valid);
            this.validLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>(lootEntitiesLookup[true]);
            this.invalidLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>(lootEntitiesLookup[false]);
        }

        /// <summary>
        /// Creates saved loot items from valid loot entities
        /// </summary>
        private void GetLoot()
        {
            var scatterMap = new ScatterReadMap(this.invalidLootEntities.Count);
            var round1 = scatterMap.AddRound();

            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                var p1 = round1.AddEntry<ulong>(i, 0, this.invalidLootEntities.ElementAt(i).Pointer);
            }

            scatterMap.Execute();

            for (int i = 0; i < this.invalidLootEntities.Count; i++)
            {
                if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var lootObjectsEntity) || lootObjectsEntity == 0)
                    continue;

                var itemToRemove = this.invalidLootEntities.ElementAt(i);
                this.validLootEntities.Add((true, itemToRemove.Index, lootObjectsEntity));
                this.invalidLootEntities.TryTake(out itemToRemove);
            };

            var validScatterCheckMap = new ScatterReadMap(this.validLootEntities.Count);
            var validCheckRound1 = validScatterCheckMap.AddRound();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                var lootUnknownPtr = validCheckRound1.AddEntry<ulong>(i, 0, this.validLootEntities.ElementAt(i).Pointer, null);
            }

            validScatterCheckMap.Execute();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                if (!validScatterCheckMap.Results[i][0].TryGetResult<ulong>(out var lootUnknownPtr) || lootUnknownPtr != 0)
                    continue;

                var itemToRemove = this.invalidLootEntities.ElementAt(i);
                this.invalidLootEntities.Add((true, itemToRemove.Index, itemToRemove.Pointer));
                this.validLootEntities.TryTake(out itemToRemove);
            };

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
                var lootUnknownPtr = vRound1.AddEntry<ulong>(i, 0, this.validLootEntities.ElementAt(i).Pointer, null, Offsets.LootListItem.LootUnknownPtr);

                var interactiveClass = vRound2.AddEntry<ulong>(i, 1, lootUnknownPtr, null, Offsets.LootUnknownPtr.LootInteractiveClass);

                var lootBaseObject = vRound3.AddEntry<ulong>(i, 2, interactiveClass, null, Offsets.LootInteractiveClass.LootBaseObject);
                var entry7 = vRound3.AddEntry<ulong>(i, 3, interactiveClass, null, 0x0);
                var item = vRound3.AddEntry<ulong>(i, 4, interactiveClass, null, Offsets.ObservedLootItem.Item);
                var containerIDPtr = vRound3.AddEntry<ulong>(i, 5, interactiveClass, null, Offsets.LootableContainer.Template);
                //var itemOwner = vRound3.AddEntry<ulong>(i, 6, interactiveClass, null, Offsets.LootInteractiveClass.ItemOwner);
                //var containerItemOwner = vRound3.AddEntry<ulong>(i, 7, interactiveClass, null, Offsets.LootInteractiveClass.ContainerItemOwner);

                var gameObject = vRound4.AddEntry<ulong>(i, 8, lootBaseObject, null, Offsets.LootBaseObject.GameObject);
                var entry9 = vRound4.AddEntry<ulong>(i, 9, entry7, null, 0x0);
                var itemTemplate = vRound4.AddEntry<ulong>(i, 10, item, null, Offsets.LootItemBase.ItemTemplate);
                //var containerItemBase = vRound4.AddEntry<ulong>(i, 11, containerItemOwner, null, Offsets.ItemOwner.Item);

                var objectName = vRound5.AddEntry<ulong>(i, 12, gameObject, null, Offsets.GameObject.ObjectName);
                var objectClass = vRound5.AddEntry<ulong>(i, 13, gameObject, null, Offsets.GameObject.ObjectClass);
                var entry10 = vRound5.AddEntry<ulong>(i, 14, entry9, null, 0x48);
                var isQuestItem = vRound5.AddEntry<bool>(i, 15, itemTemplate, null, Offsets.ItemTemplate.IsQuestItem);
                var BSGIdPtr = vRound5.AddEntry<ulong>(i, 16, itemTemplate, null, Offsets.ItemTemplate.MongoID + Offsets.MongoID.ID);
                //var rootItem = vRound5.AddEntry<ulong>(i, 17, itemOwner, null, Offsets.ItemOwner.Item);
                //var containerGrids = vRound5.AddEntry<ulong>(i, 18, containerItemBase, null, Offsets.LootItemBase.Grids);

                var className = vRound6.AddEntry<string>(i, 19, entry10, 64);
                var containerName = vRound6.AddEntry<string>(i, 20, objectName, 64);
                var transformOne = vRound6.AddEntry<ulong>(i, 21, objectClass, null, Offsets.LootGameObjectClass.To_TransformInternal[0]);
                //var slots = vRound6.AddEntry<ulong>(i, 22, rootItem, null, Offsets.LootItemBase.Slots);

                var transformTwo = vRound7.AddEntry<ulong>(i, 23, transformOne, null, Offsets.LootGameObjectClass.To_TransformInternal[1]);

                var position = vRound8.AddEntry<ulong>(i, 24, transformTwo, null, Offsets.LootGameObjectClass.To_TransformInternal[2]);

                var corpsePlayerProfileIDPtr = vRound3.AddEntry<ulong>(i, 25, interactiveClass, null, Offsets.LootInteractiveClass.PlayerProfileID);
            }

            validScatterMap.Execute();

            for (int i = 0; i < this.validLootEntities.Count; i++)
            {
                if (!validScatterMap.Results[i][1].TryGetResult<ulong>(out var interactiveClass))
                    continue;
                if (!validScatterMap.Results[i][2].TryGetResult<ulong>(out var lootBaseObject))
                    continue;
                if (!validScatterMap.Results[i][19].TryGetResult<string>(out var className))
                    continue;
                if (!validScatterMap.Results[i][20].TryGetResult<string>(out var containerName))
                    continue;
                if (!validScatterMap.Results[i][24].TryGetResult<ulong>(out var posToTransform) || posToTransform == 0)
                    continue;
                if (containerName.Contains("script", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (className.Contains("Corpse", StringComparison.OrdinalIgnoreCase))
                {                   
                    if (!this.savedLootCorpsesInfo.Any(x => x.InteractiveClass == interactiveClass))
                    {
                        //if (!validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                        //    return;
                        if (!validScatterMap.Results[i][25].TryGetResult<ulong>(out var corpsePlayerProfileIDPtr))
                            continue;

                        try
                        {
                            Vector3 position = new Transform(posToTransform).GetPosition();
                            var corpsePlayerProfileID = Memory.ReadUnityString(corpsePlayerProfileIDPtr);

                            //this.savedLootCorpsesInfo.Add(new CorpseInfo { InteractiveClass = interactiveClass, Position = position, Slots = slots, PlayerName = playerName });
                            this.savedLootCorpsesInfo.Add(new CorpseInfo { InteractiveClass = interactiveClass, Position = position, Slots = 0, ProfileID = corpsePlayerProfileID, Name = containerName });
                        }
                        catch { }
                    }
                }
                else if (className.Equals("LootableContainer", StringComparison.OrdinalIgnoreCase))
                {
                    if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                    {
                        if (!validScatterMap.Results[i][5].TryGetResult<ulong>(out var containerIDPtr) || containerIDPtr == 0)
                            continue;
                        //if (!validScatterMap.Results[i][18].TryGetResult<ulong>(out var grids) || grids == 0)
                        //    return;
                        try
                        {
                            Vector3 position = new Transform(posToTransform).GetPosition();

                            var containerID = Memory.ReadUnityString(containerIDPtr);
                            var containerExists = TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container) && container is not null;

                            //this.savedLootContainersInfo.Add(new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = containerExists ? container.Name : containerName, Grids = grids });
                            this.savedLootContainersInfo.Add(new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = containerExists ? container.Name : containerName, Grids = 0 });
                        }
                        catch { }
                    }
                }
                else if (className.Equals("ObservedLootItem", StringComparison.OrdinalIgnoreCase)) // handle loose weapons / gear
                {
                    var savedItemExists = this.savedLootItemsInfo.Any(x => x.InteractiveClass == interactiveClass);
                    //var savedSearchableExists = this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass);

                    //if (!savedItemExists || !savedSearchableExists)
                    if (!savedItemExists)
                    {
                        if (!validScatterMap.Results[i][15].TryGetResult<bool>(out var isQuestItem))
                            continue;
                        if (!validScatterMap.Results[i][16].TryGetResult<ulong>(out var BSGIDPtr) || BSGIDPtr == 0)
                            continue;

                        try
                        {
                            var id = Memory.ReadUnityString(BSGIDPtr);

                            //var itemExists = TarkovDevManager.AllItems.TryGetValue(id, out var lootItem) && lootItem is not null;
                            //var isSearchableItem = lootItem?.Item.categories.FirstOrDefault(x => x.name == "Weapon" || x.name == "Searchable item") is not null;

                            //if (isSearchableItem && !savedSearchableExists)
                            //{
                            //    Vector3 position = new Transform(posToTransform).GetPosition();
                            //    var container = new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = lootItem.Item.shortName ?? containerName };

                            //    if (validScatterMap.Results[i][23].TryGetResult<ulong>(out var slots))
                            //        container.Slots = slots;

                            //    if (validScatterMap.Results[i][18].TryGetResult<ulong>(out var rootItem))
                            //    {
                            //        //var itemGrids = Memory.ReadPtr(rootItem + Offsets.LootItemBase.Grids);
                            //        container.Grids = rootItem;
                            //    }

                            //    this.savedLootContainersInfo.Add(container);
                            //}
                            //else if (!savedItemExists)
                            //{
                                Vector3 position = new Transform(posToTransform, false).GetPosition();
                                this.savedLootItemsInfo.Add(new LootItemInfo { InteractiveClass = interactiveClass, QuestItem = isQuestItem, Position = position, ItemID = id });
                            //}
                        }
                        catch { }
                    }
                }
            };
        }

        private void FillLoot()
        {
            var loot = new ConcurrentBag<LootableObject>();

            var savedLootItemsBatches = this.savedLootItemsInfo
                .Select((item, index) => new { Item = item, Index = index })
                .GroupBy(x => x.Index / BATCH_LOOSE_LOOT)
                .Select(g => g.Select(x => x.Item).ToList())
                .ToList();

            var savedLootCorpsesBatches = this.savedLootCorpsesInfo
                .Select((item, index) => new { Item = item, Index = index })
                .GroupBy(x => x.Index / BATCH_CORPSES)
                .Select(g => g.Select(x => x.Item).ToList())
                .ToList();

            var groupedContainers = this.savedLootContainersInfo
                .GroupBy(container => (container.Position, container.Name))
                .ToList();

            var groupedContainersBatches = groupedContainers
                .Select((item, index) => new { Item = item, Index = index })
                .GroupBy(x => x.Index / BATCH_CONTAINERS)
                .Select(g => g.Select(x => x.Item).ToList())
                .ToList();

            Parallel.ForEach(this.savedLootItemsInfo, Program.Config.ParallelOptions, (savedLootItem) =>
            {
                if (this.validLootEntities.Any(x => x.Pointer == savedLootItem.InteractiveClass))
                {
                    if (!savedLootItem.QuestItem)
                    {
                        if (TarkovDevManager.AllItems.TryGetValue(savedLootItem.ItemID, out var lootItem))
                            loot.Add(CreateLootableItem(lootItem, savedLootItem.Position));
                    }
                    else
                    {
                        if (this.QuestItems is not null)
                        {
                            var questItem = this.QuestItems.Where(x => x?.Id == savedLootItem.ItemID).FirstOrDefault();

                            if (questItem is not null)
                                questItem.Position = savedLootItem.Position;
                            else
                            {
                                if (!TarkovDevManager.AllQuestItems.TryGetValue(savedLootItem.ItemID, out var newQuestItem))
                                {
                                    this.QuestItems.Add(new QuestItem()
                                    {
                                        Id = savedLootItem.ItemID,
                                        Name = "????",
                                        ShortName = "????",
                                        NormalizedName = "????",
                                        TaskName = "Unknown task",
                                        Description = "Unknown task",
                                        Position = savedLootItem.Position
                                    });
                                }
                            }
                        }
                    }
                }
                else
                {
                    this.savedLootItemsInfo = new ConcurrentBag<LootItemInfo>(this.savedLootItemsInfo.Where(x => x.InteractiveClass != savedLootItem.InteractiveClass));
                }

                if (!hasCachedItems)
                {
                    this.Loot = new(loot);
                    this.ApplyFilter();
                }
            });

            Parallel.ForEach(this.savedLootCorpsesInfo, Program.Config.ParallelOptions, (savedLootCorpse) =>
            {
                if (this.validLootEntities.Any(x => x.Pointer == savedLootCorpse.InteractiveClass))
                    loot.Add(CreateLootableCorpse(savedLootCorpse.ProfileID, savedLootCorpse.Name, savedLootCorpse.InteractiveClass, savedLootCorpse.Position, savedLootCorpse.Slots));
                else
                    this.savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>(this.savedLootCorpsesInfo.Where(x => x.InteractiveClass != savedLootCorpse.InteractiveClass));

                if (!hasCachedItems)
                {
                    this.Loot = new(loot);
                    this.ApplyFilter();
                }
            });

            Parallel.ForEach(groupedContainers, Program.Config.ParallelOptions, (savedContainerItem) =>
            {
                var firstContainer = savedContainerItem.First();

                if (this.validLootEntities.Any(x => x.Pointer == firstContainer.InteractiveClass))
                    loot.Add(CreateLootableContainer(firstContainer.Name, firstContainer.Position));
                else
                    this.savedLootContainersInfo = new ConcurrentBag<ContainerInfo>(this.savedLootContainersInfo.Where(x => x.InteractiveClass != firstContainer.InteractiveClass));

                if (!hasCachedItems)
                {
                    this.Loot = new(loot);
                    this.ApplyFilter();
                }
            });

            if (hasCachedItems)
            {
                this.Loot = new(loot);
                this.ApplyFilter();
            }
        }

        private LootCorpse CreateLootableCorpse(string profileID, string name, ulong interactiveClass, Vector3 position, ulong slots)
        {
            var player = Memory.Players.FirstOrDefault(x => x.Value.ProfileID == profileID).Value;

            var playerName = "";

            var itemList = new List<GearManager.Gear>();

            if (player is not null)
                itemList = new List<GearManager.Gear>(player.Gear);

            var corpse = new LootCorpse
            {
                Name = "Corpse" + (!string.IsNullOrEmpty(name) && !name.Contains("Clone") ? $" {name}" : ""),
                Position = position,
                InteractiveClass = interactiveClass,
                Slots = slots,
                Items = itemList
            };

            if (player is not null)
            {
                corpse.Name = player.Name;
                corpse.Player = player;
            }
            else
            {
                var playerNameSplit = name.Split('(', ')');
                playerName = playerNameSplit.Count() > 1 ? playerNameSplit[1] : playerNameSplit[0];
                corpse.Name = playerName = Helpers.TransliterateCyrillic(playerName);
            }

            corpse.Items = corpse.Items.OrderBy(x => x.Item.TotalValue).ToList();
            corpse.UpdateValue();

            return corpse;
        }

        private LootContainer CreateLootableContainer(string name, Vector3 position)
        {
            var container = new LootContainer
            {
                Name = name,
                Position = position
            };

            if (container.Name.Contains("COLLIDER"))
            {
                container.Name = "AIRDROP";
                container.AlwaysShow = true;
            }

            return container;
        }

        private LootItem CreateLootableItem(LootItem lootItem, Vector3 position)
        {
            return new LootItem
            {
                ID = lootItem.ID,
                Name = lootItem.Name,
                Position = position,
                Item = lootItem.Item,
                Important = lootItem.Important,
                AlwaysShow = lootItem.AlwaysShow,
                Value = lootItem.Value
            };
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

            this.RequiredFilterItems = orderedActiveFilters
                .SelectMany(filter => filter.Items.Select(itemId => new { ItemId = itemId, Color = filter.Color }))
                .GroupBy(pair => pair.ItemId)
                .ToDictionary(group => group.Key, group => group.FirstOrDefault().Color);

            var filteredItems = new ConcurrentBag<LootableObject>();

            // Add loose loot
            foreach (var lootItem in loot.OfType<LootItem>())
            {
                var isValuable = lootItem.Value > _config.MinLootValue;
                var isImportant = lootItem.Value > _config.MinImportantLootValue;
                var isFiltered = this.RequiredFilterItems.ContainsKey(lootItem.ID);
                var isRequired = (this._config.QuestHelper && this._config.QuestLootItems && this.RequiredQuestItems.Contains(lootItem.ID));

                var tmpLootItem = new LootItem(lootItem);

                if (isFiltered || isImportant || isRequired)
                {
                    tmpLootItem.Important = true;

                    if (isRequired)
                        tmpLootItem.RequiredByQuest = true;
                    else if (isFiltered)
                        tmpLootItem.Color = this.RequiredFilterItems[lootItem.ID];
                }

                if (isRequired || (_config.LooseLoot && (isFiltered || isValuable || lootItem.AlwaysShow)))
                    filteredItems.Add(tmpLootItem);
            }

            // Add containers
            if (_config.LootContainerSettings["Enabled"])
            {
                foreach (var container in loot.OfType<LootContainer>())
                {
                    var tmpContainer = new LootContainer(container);
                    tmpContainer.AlwaysShow = true;

                    if (_config.LootContainerSettings.ContainsKey(tmpContainer.Name))
                        if (_config.LootContainerSettings[tmpContainer.Name])
                            filteredItems.Add(tmpContainer);
                }
            }

            // Add corpses
            foreach (var corpse in loot.OfType<LootCorpse>())
            {
                var addedCorpse = false;
                var tmpCorpse = new LootCorpse(corpse);
                LootItem lowestOrderLootItem = null;
                GearItem lowestOrderGearItem = null;

                foreach (var gearItem in tmpCorpse.Items)
                {
                    var isGearImportant = gearItem.Item.TotalValue > _config.MinImportantLootValue;
                    var isGearFiltered = this.RequiredFilterItems.ContainsKey(gearItem.Item.ID);

                    if (isGearImportant || isGearFiltered)
                    {
                        gearItem.Item.Important = true;
                        tmpCorpse.Important = true;

                        if (isGearFiltered)
                        {
                            gearItem.Item.Color = this.RequiredFilterItems[gearItem.Item.ID];

                            var gearItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(gearItem.Item.ID));
                            if (gearItemFilter is not null && (lowestOrderGearItem is null || gearItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
                                lowestOrderGearItem = gearItem.Item;
                        }

                        foreach (var lootItem in gearItem.Item.Loot)
                        {
                            var isLootImportant = lootItem.Value > _config.MinImportantLootValue;
                            var isLootFiltered = this.RequiredFilterItems.ContainsKey(lootItem.ID);

                            if (isLootImportant || isLootFiltered)
                            {
                                lootItem.Important = true;
                                gearItem.Item.Important = true;
                                tmpCorpse.Important = true;

                                if (isLootFiltered)
                                {
                                    lootItem.Color = this.RequiredFilterItems[lootItem.ID];

                                    var lootItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(lootItem.ID));
                                    if (lootItemFilter is not null && (lowestOrderLootItem is null || lootItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order))
                                        lowestOrderLootItem = lootItem;
                                }
                            }
                        }

                        if (lowestOrderLootItem is not null)
                            gearItem.Item.Color = lowestOrderLootItem.Color;
                    }

                    if (lowestOrderLootItem is not null && (lowestOrderGearItem is null ||
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order <
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
                    {
                        tmpCorpse.Color = lowestOrderLootItem.Color;
                    }
                    else if (lowestOrderGearItem is not null)
                    {
                        tmpCorpse.Color = lowestOrderGearItem.Color;
                    }

                    if (tmpCorpse.Value > _config.MinCorpseValue || tmpCorpse.Important)
                    {
                        addedCorpse = true;
                        filteredItems.Add(tmpCorpse);
                    }
                }

                if (!addedCorpse && (_config.LooseLoot && _config.LootCorpses || !_config.LooseLoot && _config.LootCorpses))
                    filteredItems.Add(tmpCorpse);
            }

            this.Filter = new(filteredItems.OrderByDescending(x => x.Important)
                                            .ThenByDescending(x => x.Value));
        }
        /// <summary>
        /// Removes an item from the loot filter list
        /// </summary>
        /// <param name="itemToRemove">The item to remove</param>
        public void RemoveFilterItem(LootItem itemToRemove)
        {
            var filter = this.Filter.ToList();
            filter.Remove(itemToRemove);

            this.Filter = new ConcurrentBag<LootableObject>(new ConcurrentBag<LootableObject>(filter));
            this.ApplyFilter();
        }

        public static List<LootItem> MergeDupelicateLootItems(List<LootItem> lootItems)
        {
            return
            lootItems
            .GroupBy(lootItem => lootItem?.ID)
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
            public ConcurrentDictionary<string, GearItem> CachedGearItems { get; set; } = new ConcurrentDictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);
            public ConcurrentDictionary<string, LootItem> CachedLootItems { get; set; } = new ConcurrentDictionary<string, LootItem>(StringComparer.OrdinalIgnoreCase);
        }
        #endregion
    }

    #region Classes
    public abstract class LootableObject
    {
        public string Name { get; set; }
        public bool Important { get; set; }
        public bool AlwaysShow { get; set; }
        public bool RequiredByQuest { get; set; }
        public int Value { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
        public PaintColor.Colors Color { get; set; }
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
        public bool IsWithinDistance =>
            Memory.LocalPlayer is not null &&
            Vector3.Distance(this.Position, Memory.LocalPlayer?.Position ?? Vector3.Zero) <= Program.Config.LootContainerDistance;

        public LootContainer() { }

        // for deep copying
        public LootContainer(LootContainer other)
        {
            base.Name = other.Name;
            base.Important = other.Important;
            base.AlwaysShow = other.AlwaysShow;
            base.Position = other.Position;

            this.InteractiveClass = other.InteractiveClass;
        }
    }

    public class LootCorpse : LootableObject
    {
        public ulong InteractiveClass { get; set; }
        public ulong Slots { get; set; }
        public List<GearManager.Gear> Items { get; set; }
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
            this.Items = other.Items.Select(item => new GearManager.Gear
            {
                Slot = item.Slot,
                Item = new GearItem(item.Item),
                Pointer = item.Pointer
            }).ToList();
            this.Player = other.Player;
        }

        public void UpdateValue() => this.Value = this.Items.Sum(item => item.Item.TotalValue);
    }

    public class GearItem : LootableObject
    {
        public string ID { get; set; }
        public string Long { get; set; }
        public string Short { get; set; }
        public int LootValue { get => this.Loot.Sum(x => x.Value); }
        public int TotalValue { get => base.Value + this.LootValue; }
        public List<LootItem> Loot { get; set; }
        public LootItem Item { get; set; }
        public GearManager.PlayerGearInfo GearInfo { get; set; }

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
            this.Item = other.Item is not null ? new LootItem(other.Item) : null;
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
        public string ProfileID;
        public string Name;
    }
    #endregion
}