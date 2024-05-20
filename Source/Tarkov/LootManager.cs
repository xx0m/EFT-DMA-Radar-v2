using Offsets;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace eft_dma_radar
{
    public class LootManager
    {
        public ulong lootlistPtr;
        public ulong lootListEntity;
        public int countLootListObjects;
        public ulong localGameWorld;

        private bool hasCachedItems;

        private ConcurrentDictionary<ulong, GridCacheEntry> gridCache;
        private ConcurrentDictionary<ulong, SlotCacheEntry> slotCache;
        private ConcurrentBag<(bool Valid, int Index, ulong Pointer)> validLootEntities;
        private ConcurrentBag<(bool Valid, int Index, ulong Pointer)> invalidLootEntities;
        private ConcurrentBag<ContainerInfo> savedLootContainersInfo;
        private ConcurrentBag<CorpseInfo> savedLootCorpsesInfo;
        private ConcurrentBag<LootItemInfo> savedLootItemsInfo;
        private static readonly IReadOnlyCollection<string> slotsToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SecuredContainer", "Dogtag", "Compass", "Eyewear", "ArmBand" };

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

        public int TotalLooseLoot { get => savedLootItemsInfo.Count; }
        public int TotalContainers { get => savedLootContainersInfo.Count; }
        public int TotalCorpses { get => savedLootCorpsesInfo.Count; }
        /// <summary>
        /// all quest items
        /// </summary>
        private Collection<QuestItem> QuestItems { get => Memory.QuestManager is not null ? Memory.QuestManager.QuestItems : null; }

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

            this.CurrentMapName = Memory.MapNameFormatted;

            if (this._config.AutoLootRefresh)
            {
                this.StartAutoRefresh();
            }
            else if (this._config.ProcessLoot)
            {
                this.RefreshLoot();
            }
        }
        #endregion

        #region Methods
        public void StartAutoRefresh()
        {
            if (this.autoRefreshThread is not null && this.autoRefreshThread.IsAlive)
            {
                return;
            }

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
            while (!cancellationToken.IsCancellationRequested && Memory.GameStatus == Game.GameStatus.InGame && this._config.ProcessLoot && this._config.AutoLootRefresh)
            {
                Task.Run(async () => { await this.RefreshLoot(); });
                var sleepFor = this._config.AutoRefreshSettings[this.CurrentMapName] * 1000;
                Thread.Sleep(sleepFor);
            }
            Program.Log("[LootManager] Refresh thread stopped.");
        }

        public async Task RefreshLoot(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                await Task.Run(async () => { await this.StopAutoRefresh(); });

                await Task.Run(() =>
                {
                    if (this._config.ProcessLoot && this._config.AutoLootRefresh)
                    {
                        this.StartAutoRefresh();
                    }
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
            await Task.Run(async() => { await this.GetLootList(); });
            var ts = sw.Elapsed;
            var elapsedTime = String.Format("[LootManager] Finished GetLootList {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Restart();

            await Task.Run(async () => { await this.GetLoot(); });
            ts = sw.Elapsed;
            elapsedTime = String.Format("[LootManager] Finished GetLoot {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Restart();

            await Task.Run(async () => { await this.FillLoot(); });
            ts = sw.Elapsed;
            elapsedTime = String.Format("[LootManager] Finished FillLoot {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log(elapsedTime);
            sw.Stop();
            swTotal.Stop();
            ts = swTotal.Elapsed;
            elapsedTime = String.Format("RunTime {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            Program.Log("[LootManager] RunTime " + elapsedTime);
            Program.Log($"[LootManager] Found {savedLootItemsInfo.Count} loose loot items");
            Program.Log($"[LootManager] Found {savedLootContainersInfo.Count} lootable containers");
            Program.Log($"[LootManager] Found {savedLootCorpsesInfo.Count} lootable corpses");
            Program.Log($"[LootManager] Total loot items processed: {savedLootItemsInfo.Count + savedLootContainersInfo.Count + savedLootCorpsesInfo.Count}");
            Program.Log($"---------------------------------");

            this.hasCachedItems = true;
        }

        /// <summary>
        /// Refresh loot list pointers
        /// </summary>
        private async Task RefreshLootListAddresses()
        {
            await Task.Run(() =>
            {
                var scatterReadMap = new ScatterReadMap(1);
                var round1 = scatterReadMap.AddRound();
                var round2 = scatterReadMap.AddRound();
                var round3 = scatterReadMap.AddRound();

                var lootlistPtr = round1.AddEntry<ulong>(0, 0, this.localGameWorld, null, Offsets.LocalGameWorld.LootList);
                var lootListEntity = round2.AddEntry<ulong>(0, 1, lootlistPtr, null, Offsets.UnityList.Base);
                var countLootListObjects = round3.AddEntry<int>(0, 2, lootListEntity, null, Offsets.UnityList.Count);

                scatterReadMap.Execute();

                if (scatterReadMap.Results[0][0].TryGetResult<ulong>(out var lootlistPtrRslt))
                    this.lootlistPtr = lootlistPtrRslt;

                if (scatterReadMap.Results[0][1].TryGetResult<ulong>(out var lootListEntityRslt))
                    this.lootListEntity = lootListEntityRslt;

                if (scatterReadMap.Results[0][2].TryGetResult<int>(out var countLootListObjectsRslt))
                    this.countLootListObjects = countLootListObjectsRslt;
            });
        }
        /// <summary>
        /// Gets the invalid & valid loot entities
        /// </summary>
        private async Task GetLootList()
        {
            await this.RefreshLootListAddresses();

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
                {
                    lootEntitiesWithIndex.Add((true, i, lootObjectsEntity));
                }
                else
                {
                    lootEntitiesWithIndex.Add((false, i, this.lootListEntity + Offsets.UnityListBase.Start + (uint)(i * 0x8)));
                }
            };

            var lootEntitiesLookup = lootEntitiesWithIndex.ToLookup(x => x.Valid);
            this.validLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>(lootEntitiesLookup[true]);
            this.invalidLootEntities = new ConcurrentBag<(bool Valid, int Index, ulong Pointer)>(lootEntitiesLookup[false]);
        }
        /// <summary>
        /// Creates saved loot items from valid loot entities
        /// </summary>
        public async Task GetLoot()
        {
            await Task.Run(() =>
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
                        return;

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
                        return;

                    var itemToRemove = this.invalidLootEntities.ElementAt(i);
                    this.invalidLootEntities.Add((true, itemToRemove.Index, itemToRemove.Pointer));
                    this.validLootEntities.TryTake(out itemToRemove);
                };
            });

            var validScatterMap = new ScatterReadMap(this.validLootEntities.Count);
            var vRound1 = validScatterMap.AddRound();
            var vRound2 = validScatterMap.AddRound();
            var vRound3 = validScatterMap.AddRound();
            var vRound4 = validScatterMap.AddRound();
            var vRound5 = validScatterMap.AddRound();
            var vRound6 = validScatterMap.AddRound();
            var vRound7 = validScatterMap.AddRound();
            var vRound8 = validScatterMap.AddRound();

            Parallel.For(0, this.validLootEntities.Count, Program.Config.ParallelOptions, i =>
            {
                var lootUnknownPtr = vRound1.AddEntry<ulong>(i, 0, this.validLootEntities.ElementAt(i).Pointer, null, Offsets.LootListItem.LootUnknownPtr);

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
            });

            validScatterMap.Execute();

            Parallel.For(0, this.validLootEntities.Count, Program.Config.ParallelOptions, i =>
            {
                if (!validScatterMap.Results[i][1].TryGetResult<ulong>(out var interactiveClass))
                    return;
                if (!validScatterMap.Results[i][2].TryGetResult<ulong>(out var lootBaseObject))
                    return;
                if (!validScatterMap.Results[i][24].TryGetResult<ulong>(out var posToTransform))
                    return;
                if (!validScatterMap.Results[i][20].TryGetResult<string>(out var containerName))
                    return;
                if (!validScatterMap.Results[i][19].TryGetResult<string>(out var className))
                    return;

                if (containerName.Contains("script", StringComparison.OrdinalIgnoreCase))
                    return;

                try
                {
                    if (className.Contains("Corpse", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!this.savedLootCorpsesInfo.Any(x => x.InteractiveClass == interactiveClass))
                        {
                            if (!validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                                return;

                            Vector3 position = new Transform(posToTransform, false).GetPosition(null);

                            var playerNameSplit = containerName.Split('(', ')');
                            var playerName = playerNameSplit.Count() > 1 ? playerNameSplit[1] : playerNameSplit[0];
                            playerName = Helpers.TransliterateCyrillic(playerName);

                            this.savedLootCorpsesInfo.Add(new CorpseInfo { InteractiveClass = interactiveClass, Position = position, Slots = slots, PlayerName = playerName });
                        }
                    }
                    else if (className.Equals("LootableContainer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass))
                        {
                            if (!validScatterMap.Results[i][5].TryGetResult<ulong>(out var containerIDPtr))
                                return;

                            if (!validScatterMap.Results[i][18].TryGetResult<ulong>(out var grids))
                                return;

                            Vector3 position = new Transform(posToTransform, false).GetPosition(null);

                            var containerID = Memory.ReadUnityString(containerIDPtr);
                            var containerExists = TarkovDevManager.AllLootContainers.TryGetValue(containerID, out var container) && container is not null;

                            this.savedLootContainersInfo.Add(new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = containerExists ? container.Name : containerName, Grids = grids });
                        }
                    }
                    else if (className.Equals("ObservedLootItem", StringComparison.OrdinalIgnoreCase)) // handle loose weapons / gear
                    {
                        var savedItemExists = this.savedLootItemsInfo.Any(x => x.InteractiveClass == interactiveClass);
                        var savedSearchableExists = this.savedLootContainersInfo.Any(x => x.InteractiveClass == interactiveClass);

                        if (!savedItemExists || !savedSearchableExists)
                        {
                            if (!validScatterMap.Results[i][15].TryGetResult<bool>(out var isQuestItem))
                                return;
                            if (!validScatterMap.Results[i][16].TryGetResult<ulong>(out var BSGIDPtr))
                                return;

                            var id = Memory.ReadUnityString(BSGIDPtr);

                            if (id is null)
                                return;

                            var itemExists = TarkovDevManager.AllItems.TryGetValue(id, out var lootItem) && lootItem is not null;
                            var isSearchableItem = lootItem?.Item.categories.FirstOrDefault(x => x.name == "Weapon" || x.name == "Searchable item") is not null;

                            if (isSearchableItem)
                            {
                                if (!savedSearchableExists)
                                {
                                    Vector3 position = new Transform(posToTransform, false).GetPosition(null);
                                    var container = new ContainerInfo { InteractiveClass = interactiveClass, Position = position, Name = lootItem.Item.shortName ?? containerName };

                                    if (validScatterMap.Results[i][22].TryGetResult<ulong>(out var slots))
                                        container.Slots = slots;

                                    if (validScatterMap.Results[i][17].TryGetResult<ulong>(out var rootItem))
                                    {
                                        var itemGrids = Memory.ReadPtr(rootItem + 0x70);
                                        container.Grids = itemGrids;
                                    }

                                    this.savedLootContainersInfo.Add(container);
                                }
                            }
                            else
                            {
                                if (!savedItemExists)
                                {
                                    Vector3 position = new Transform(posToTransform, false).GetPosition(null);
                                    this.savedLootItemsInfo.Add(new LootItemInfo { InteractiveClass = interactiveClass, QuestItem = isQuestItem, Position = position, ItemID = id });
                                }
                            }
                        }
                    }
                }
                catch { }
                
            });
        }

        public async Task FillLoot()
        {
            var loot = new ConcurrentBag<LootableObject>();

            // create Loot items
            await Task.Run(() =>
            {
                Parallel.ForEach(this.savedLootItemsInfo, Program.Config.ParallelOptions, (savedLootItem) =>
                {
                    if (this.validLootEntities.Any(x => x.Pointer == savedLootItem.InteractiveClass))
                    {
                        if (!savedLootItem.QuestItem)
                        {
                            if (TarkovDevManager.AllItems.TryGetValue(savedLootItem.ItemID, out var lootItem))
                            {
                                loot.Add(CreateLootableItem(lootItem, savedLootItem.Position));
                            }
                        }
                        else
                        {
                            if (this.QuestItems is not null)
                            {
                                var questItem = this.QuestItems.Where(x => x.Id == savedLootItem.ItemID).FirstOrDefault();
                                if (questItem is not null)
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
                });
            });

            // create Corpse objects
            await Task.Run(() =>
            {
                Parallel.ForEach(this.savedLootCorpsesInfo, Program.Config.ParallelOptions, (savedLootCorpse) =>
                {
                    if (this.validLootEntities.Any(x => x.Pointer == savedLootCorpse.InteractiveClass))
                    {
                        loot.Add(CreateLootableCorpse(savedLootCorpse.PlayerName, savedLootCorpse.InteractiveClass, savedLootCorpse.Position, savedLootCorpse.Slots));
                    }
                    else
                    {
                        this.savedLootCorpsesInfo = new ConcurrentBag<CorpseInfo>(this.savedLootCorpsesInfo.Where(x => x.InteractiveClass != savedLootCorpse.InteractiveClass));
                    }
                });
            });

            // create Container objects, merge dupe entries based on position + name
            // (helps deal with multiple entries for the same container)
            var groupedContainers = this.savedLootContainersInfo.GroupBy(container => (container.Position, container.Name)).ToList();
            await Task.Run(() =>
            {
                Parallel.ForEach(groupedContainers, Program.Config.ParallelOptions, (savedContainerItem) =>
                {
                    var firstContainer = savedContainerItem.First();

                    if (this.validLootEntities.Any(x => x.Pointer == firstContainer.InteractiveClass))
                    {
                        loot.Add(CreateLootableContainer(firstContainer.Name, firstContainer.Position, firstContainer.Grids, firstContainer.Slots));
                    }
                    else
                    {
                        this.savedLootContainersInfo = new ConcurrentBag<ContainerInfo>(this.savedLootContainersInfo.Where(x => x.InteractiveClass != firstContainer.InteractiveClass));
                    }
                });
            });

            this.Loot = new(loot);

            this.ApplyFilter();
        }

        private LootCorpse CreateLootableCorpse(string name, ulong interactiveClass, Vector3 position, ulong slots)
        {
            var corpse = new LootCorpse
            {
                Name = "Corpse" + (!string.IsNullOrEmpty(name) ? $" {name}" : ""),
                Position = position,
                InteractiveClass = interactiveClass,
                Slots = slots,
                Items = new List<GearItem>()
            };

            if (corpse.Slots != 0)
            {
                this.GetItemsInSlots(corpse.Slots, corpse.Position, corpse.Items);
            }

            corpse.Items = corpse.Items.Where(item => item?.TotalValue > 0).ToList();

            Parallel.ForEach(corpse.Items, Program.Config.ParallelOptions, (gearItem) =>
            {
                int index = gearItem.Loot.FindIndex(lootItem => lootItem.ID == gearItem.ID);
                if (index != -1)
                {
                    gearItem.Loot.RemoveAt(index);
                }

                gearItem.Loot = MergeDupelicateLootItems(gearItem.Loot);
            });

            corpse.Items = corpse.Items.OrderBy(x => x.TotalValue).ToList();
            corpse.UpdateValue();

            return corpse;
        }

        private LootContainer CreateLootableContainer(string name, Vector3 position, ulong grids, ulong slots = 0)
        {
            var container = new LootContainer
            {
                Name = name,
                Position = position,
                Grids = grids,
                Items = new List<LootItem>()
            };

            if (container.Name.Contains("COLLIDER(1)"))
            {
                container.Name = "AIRDROP";
                container.AlwaysShow = true;
            }

            if (slots != 0)
            {
                this.GetItemsInSlots(slots, container.Position, container.Items);
            }

            if (grids != 0)
            {
                this.GetItemsInGrid(grids, container.Position, container.Items);
            }

            container.Items = this.MergeDupelicateLootItems(container.Items);
            container.UpdateValue();

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

            var itemIdColorPairs = orderedActiveFilters
                .SelectMany(filter => filter.Items.Select(itemId => new { ItemId = itemId, Color = filter.Color }))
                .GroupBy(pair => pair.ItemId)
                .ToDictionary(group => group.Key, group => group.FirstOrDefault().Color);


            var filteredItems = new ConcurrentBag<LootableObject>();

            // Add loose loot
            Parallel.ForEach(loot.OfType<LootItem>(), Program.Config.ParallelOptions, (lootItem) =>
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
            });

            // Add containers
            Parallel.ForEach(loot.OfType<LootContainer>(), Program.Config.ParallelOptions, (container) =>
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
            });

            // Add corpses
            Parallel.ForEach(loot.OfType<LootCorpse>(), Program.Config.ParallelOptions, (corpse) =>
            {
                var tempCorpse = new LootCorpse(corpse);
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

                        if (isGearFiltered)
                        {
                            gearItem.Color = itemIdColorPairs[gearItem.ID];

                            var gearItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(gearItem.ID));
                            if (gearItemFilter is not null && (lowestOrderGearItem is null || gearItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
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

                                if (isLootFiltered)
                                {
                                    lootItem.Color = itemIdColorPairs[lootItem.ID];

                                    var lootItemFilter = orderedActiveFilters.FirstOrDefault(filter => filter.Items.Contains(lootItem.ID));
                                    if (lootItemFilter is not null && (lowestOrderLootItem is null || lootItemFilter.Order < orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order))
                                    {
                                        lowestOrderLootItem = lootItem;
                                    }
                                }
                            }
                        }

                        if (lowestOrderLootItem is not null)
                        {
                            gearItem.Color = lowestOrderLootItem.Color;
                        }
                    }

                    if (lowestOrderLootItem is not null && (lowestOrderGearItem is null ||
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderLootItem.ID)).Order <
                        orderedActiveFilters.First(filter => filter.Items.Contains(lowestOrderGearItem.ID)).Order))
                    {
                        tempCorpse.Color = lowestOrderLootItem.Color;
                    }
                    else if (lowestOrderGearItem is not null)
                    {
                        tempCorpse.Color = lowestOrderGearItem.Color;
                    }

                    if (tempCorpse.Value > _config.MinCorpseValue || tempCorpse.Important)
                    {
                        filteredItems.Add(tempCorpse);
                    }
                }
            });

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

        /// <summary>
        /// Recursively searches items within a grid
        /// </summary>
        private void GetItemsInGrid(ulong gridsArrayPtr, Vector3 position, List<LootItem> containerLoot, int recurseDepth = 0)
        {
            if (gridsArrayPtr == 0 || recurseDepth > 3)
                return;

            try
            {
                int currentChildrenCount = this.CalculateChildrenCount(gridsArrayPtr);

                if (this.gridCache.TryGetValue(gridsArrayPtr, out var cacheEntry))
                {
                    if (currentChildrenCount == cacheEntry.ChildrenCount)
                    {
                        containerLoot.AddRange(cacheEntry.CachedLootItems);
                        return;
                    }
                }

                var newCachedLootItems = new List<LootItem>();

                this.ProcessGrid(gridsArrayPtr, position, newCachedLootItems, recurseDepth);

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

            Parallel.For(0, gridsArrayCount, i =>
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var grid))
                    return;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var gridEnumerableClass))
                    return;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemListPtr))
                    return;
                if (!scatterReadMap.Results[i][3].TryGetResult<int>(out var itemListCount))
                    return;
                if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var arrayBase))
                    return;

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

                Parallel.For(0, itemListCount, j =>
                {
                    try
                    {
                        if (!innerScatterReadMap.Results[j][0].TryGetResult<ulong>(out var childItem))
                            return;
                        if (!innerScatterReadMap.Results[j][1].TryGetResult<ulong>(out var childItemTemplate))
                            return;
                        if (!innerScatterReadMap.Results[j][3].TryGetResult<ulong>(out var childItemIdPtr))
                            return;
                        var childItemId = Memory.ReadUnityString(childItemIdPtr);

                        if (childItemId is null)
                            return;

                        childItemId = childItemId.Replace("\\0", "");

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
                                Value = childLootItem.Value
                            };

                            cachedLootItems.Add(newItem);
                        }

                        if (!innerScatterReadMap.Results[j][2].TryGetResult<ulong>(out var childGridsArrayPtr))
                            return;

                        this.GetItemsInGrid(childGridsArrayPtr, position, cachedLootItems, recurseDepth + 1);
                    }
                    catch { return; }
                });
            });
        }

        private void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<GearItem> gearItems)
        {
            if (slotItemBase == 0)
                return;

            var slotDict = this.GetSlotDictionary(slotItemBase);

            if (slotDict is null || slotDict.Count == 0)
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
                var slots = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.Equipment.Slots);
                var grids = round2.AddEntry<ulong>(i, 3, containedItem, null, Offsets.LootItemBase.Grids);

                var idPtr = round3.AddEntry<ulong>(i, 4, inventorytemplate, null, Offsets.ItemTemplate.BsgId);
            }

            scatterReadMap.Execute();

            Parallel.For(0, slotDict.Count, i =>
            {
                try
                {
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                    return;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                        return;
                    if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var idPtr))
                        return;
                    var id = Memory.ReadUnityString(idPtr);
                    var slotName = slotNames[i];

                    SlotCacheEntry cacheEntry = this.slotCache.GetOrAdd(slotItemBase, _ => new SlotCacheEntry());

                    if (cacheEntry.CachedGearItems.TryGetValue(slotName, out var cachedGearItem))
                    {
                        if (cachedGearItem.Loot.Count > 0)
                        {
                            cachedGearItem.Loot.Clear();

                            if (scatterReadMap.Results[i][2].TryGetResult<ulong>(out var slots))
                                this.GetItemsInSlots(slots, position, cachedGearItem.Loot);

                            if (scatterReadMap.Results[i][3].TryGetResult<ulong>(out var grids))
                                this.GetItemsInGrid(grids, position, cachedGearItem.Loot);
                        }

                        gearItems.Add(cachedGearItem);
                    }
                    else
                    {
                        var isPocket = (slotName == "Pockets");

                        if (TarkovDevManager.AllItems.TryGetValue(id, out LootItem lootItem) || isPocket)
                        {
                            var longName = isPocket ? "Pocket" : lootItem?.Item.name ?? "Unknown";
                            var shortName = isPocket ? "Pocket" : lootItem?.Item.shortName ?? "Unknown";
                            var value = isPocket || lootItem is null ? 0 : lootItem.Value;

                            var newGearItem = new GearItem
                            {
                                ID = id,
                                Long = longName,
                                Short = shortName,
                                Value = value,
                                HasThermal = false,
                                Loot = new List<LootItem>()
                            };

                            if (scatterReadMap.Results[i][2].TryGetResult<ulong>(out var slots))
                                this.GetItemsInSlots(slots, position, newGearItem.Loot);

                            if (scatterReadMap.Results[i][3].TryGetResult<ulong>(out var grids))
                                this.GetItemsInGrid(grids, position, newGearItem.Loot);

                            gearItems.Add(newGearItem);
                            cacheEntry.CachedGearItems[slotName] = newGearItem;
                        }
                    }
                } catch { return; }
            });
        }

        private void GetItemsInSlots(ulong slotItemBase, Vector3 position, List<LootItem> loot, int recurseDepth = 0)
        {
            if (slotItemBase == 0 || recurseDepth > 3)
                return;

            var slotDict = this.GetSlotDictionary(slotItemBase);

            if (slotDict is null || slotDict.Count == 0)
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
                var slots = round2.AddEntry<ulong>(i, 2, containedItem, null, Offsets.Equipment.Slots);
                var grids = round2.AddEntry<ulong>(i, 3, containedItem, null, Offsets.LootItemBase.Grids);

                var idPtr = round3.AddEntry<ulong>(i, 4, inventorytemplate, null, Offsets.ItemTemplate.BsgId);
            }

            scatterReadMap.Execute();

            Parallel.For(0, slotDict.Count, i =>
            {
                try
                {
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var containedItem))
                    return;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var inventorytemplate))
                        return;
                    if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var idPtr))
                        return;

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
                                Value = lootItem.Value
                            };

                            loot.Add(newLootItem);
                            cacheEntry.CachedLootItems[slotName] = newLootItem;
                        }
                    }

                    if (scatterReadMap.Results[i][2].TryGetResult<ulong>(out var slots))
                        this.GetItemsInSlots(slots, position, loot, recurseDepth + 1);

                    if (scatterReadMap.Results[i][3].TryGetResult<ulong>(out var grids))
                        this.GetItemsInGrid(grids, position, loot);
                }
                catch { return; }
            });
        }

        private int CalculateChildrenCount(ulong gridsArrayPtr)
        {
            try
            {
                var gridsArrayCount = Memory.ReadValue<int>(gridsArrayPtr + Offsets.UnityList.Count);

                if (gridsArrayCount < 0 || gridsArrayCount > 4096)
                    return 0;

                int totalChildrenCount = 0;

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

                Parallel.For(0, gridsArrayCount, i =>
                {
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var grid))
                        return;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var gridEnumerableClass))
                        return;
                    if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var itemListPtr))
                        return;
                    if (!scatterReadMap.Results[i][3].TryGetResult<int>(out var itemListCount))
                        return;

                    totalChildrenCount += itemListCount;
                });

                return totalChildrenCount;
            }
            catch { return 0; }
        }

        private ConcurrentDictionary<string, ulong> GetSlotDictionary(ulong slotItemBase)
        {
            var slotDict = new ConcurrentDictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

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

                var task = Task.Run(() =>
                {
                    Parallel.For(0, size, i =>
                    {
                        try
                        {
                            if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var slotPtr))
                                return;
                            if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var namePtr))
                                return;

                            var name = Memory.ReadUnityString(namePtr);

                            if (!LootManager.slotsToSkip.Contains(name, StringComparer.OrdinalIgnoreCase))
                            {
                                slotDict[name] = slotPtr;
                            }
                        }
                        catch { return; }
                    });
                });

                task.Wait();
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
            public ConcurrentDictionary<string, GearItem> CachedGearItems { get; set; } = new ConcurrentDictionary<string, GearItem>(StringComparer.OrdinalIgnoreCase);
            public ConcurrentDictionary<string, LootItem> CachedLootItems { get; set; } = new ConcurrentDictionary<string, LootItem>(StringComparer.OrdinalIgnoreCase);
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
        public LootFilterManager.Filter.Colors Color { get; set; }
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
    #endregion
}