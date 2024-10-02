using System.Text.RegularExpressions;
using System.Numerics;
using System.Collections.Concurrent;

namespace eft_dma_radar
{
    public class QuestManager
    {
        private ulong localGameWorld;
        private ulong questData;
        private ulong questDataBaseList;
        private int questCount;

        private bool refreshingQuests;
        private readonly Config config;

        private Thread autoRefreshThread;
        private CancellationTokenSource autoRefreshCancellationTokenSource;

        private ConcurrentBag<QuestInfo> savedQuestInfo;

        public ConcurrentBag<QuestItem> QuestItems
        {
            get;
            private set;
        }

        public ConcurrentBag<QuestZone> QuestZones
        {
            get;
            private set;
        }

        public HashSet<string> CompletedSubTasks
        {
            get;
            private set;
        }

        public static HashSet<string> RequiredItems { get; set; } = new HashSet<string>();

        public QuestManager(ulong localGameWorld)
        {
            this.config = Program.Config;
            this.localGameWorld = localGameWorld;
            this.savedQuestInfo = new ConcurrentBag<QuestInfo>();

            this.QuestItems = new ConcurrentBag<QuestItem>();
            this.QuestZones = new ConcurrentBag<QuestZone>();
            this.CompletedSubTasks = new HashSet<string>();
            
            if (Memory.IsScav)
            {
                if (RequiredItems.Count == 0)
                    RequiredItems = new HashSet<string>();

                return;
            }
           else
                RequiredItems = new HashSet<string>();

            this.GetQuests();

            if (this.config.QuestTaskRefresh)
                this.StartAutoRefresh();
            else if (this.config.QuestHelper)
                this.RefreshQuests(true);
        }

        public void StartAutoRefresh()
        {
            if (this.autoRefreshThread is not null && this.autoRefreshThread.IsAlive)
                return;

            this.refreshingQuests = false;
            this.autoRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = this.autoRefreshCancellationTokenSource.Token;

            this.autoRefreshThread = new Thread(() => this.QuestManagerWorkerThread(cancellationToken))
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

        private void QuestManagerWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && Memory.GameStatus == Game.GameStatus.InGame && this.config.QuestHelper && this.config.QuestTaskRefresh)
            {
                Task.Run(async () => { await this.RefreshQuests(); });
                var sleepFor = this.config.QuestTaskRefreshDelay * 1000;
                Thread.Sleep(sleepFor);
            }
            Program.Log("[QuestManager] Refresh thread stopped.");
        }

        public async Task RefreshQuests(bool forceRefresh = false)
        {
            if (this.refreshingQuests && !forceRefresh)
            {
                Program.Log("[QuestManager] Quest refresh is already in progress.");
                return;
            }

            this.refreshingQuests = true;

            if (forceRefresh)
            {
                await this.StopAutoRefresh();

                await Task.Run(() =>
                {
                    if (this.config.QuestHelper && this.config.QuestTaskRefresh && this.autoRefreshThread is null)
                        this.StartAutoRefresh();
                });

                if (this.autoRefreshThread is not null)
                    return;
            }

            this.CheckQuestItemsAndZones();

            this.refreshingQuests = false;
        }

        private void RefreshLootListAddresses()
        {
            var scatterReadMap = new ScatterReadMap(1);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();

            var mainPlayerPtr = round1.AddEntry<ulong>(0, 0, this.localGameWorld, null, Offsets.LocalGameWorld.MainPlayer);

            var playerProfilePtr = round2.AddEntry<ulong>(0, 1, mainPlayerPtr, null, Offsets.Player.Profile);

            var questDataPtr = round3.AddEntry<ulong>(0, 2, playerProfilePtr, null, Offsets.Profile.QuestsData);

            var questDataBaseListPtr = round4.AddEntry<ulong>(0, 3, questDataPtr, null, Offsets.UnityList.Base);
            var questCountPtr = round4.AddEntry<int>(0, 4, questDataPtr, null, Offsets.UnityList.Count);

            scatterReadMap.Execute();

            if (scatterReadMap.Results[0][2].TryGetResult<ulong>(out var questData))
                this.questData = questData;

            if (scatterReadMap.Results[0][3].TryGetResult<ulong>(out var questDataBaseList))
                this.questDataBaseList = questDataBaseList;

            if (scatterReadMap.Results[0][4].TryGetResult<int>(out var questCount))
                this.questCount = questCount;
        }

        public void GetQuests()
        {
            this.RefreshLootListAddresses();

            var questScatterMap = new ScatterReadMap(this.questCount);
            var qRound1 = questScatterMap.AddRound();
            var qRound2 = questScatterMap.AddRound();
            var qRound3 = questScatterMap.AddRound();

            var questBase = this.questDataBaseList + 0x20;

            for (int i = 0; i < this.questCount; i++)
            {
                var questEntry = qRound1.AddEntry<ulong>(i, 0, questBase, null, (uint)(i * 0x8));

                var questTemplate = qRound2.AddEntry<ulong>(i, 1, questEntry, null, Offsets.QuestData.Template);
                var questStatus = qRound2.AddEntry<int>(i, 2, questEntry, null, Offsets.QuestData.Status);
                var completedSubTasks = qRound2.AddEntry<ulong>(i, 3, questEntry, null, Offsets.QuestData.CompletedConditions);

                var questID = qRound3.AddEntry<ulong>(i, 4, questTemplate, null, Offsets.QuestData.ID);
            }

            questScatterMap.Execute();

            for (int i = 0; i <  questCount; i++)
            {
                try
                {
                    if (!questScatterMap.Results[i][0].TryGetResult<ulong>(out var questEntry))
                        continue;
                    if (!questScatterMap.Results[i][2].TryGetResult<int>(out var questStatus))
                        continue;
                    if (!questScatterMap.Results[i][3].TryGetResult<ulong>(out var completedSubTasks))
                        continue;
                    if (!questScatterMap.Results[i][4].TryGetResult<ulong>(out var questIDPtr))
                        continue;
                    if (questStatus != 2)
                        continue;

                    var questID = Memory.ReadUnityString(questIDPtr);

                    if (!TarkovDevManager.AllTasks.TryGetValue(questID, out var task) || task is null)
                        continue;

                    if (!this.savedQuestInfo.Any(x => x.ID == questID))
                        this.savedQuestInfo.Add(new QuestInfo { ID = questID, CompletedSubTasks = completedSubTasks, Task = task });
                }
                catch { }
            }
        }

        public void CheckQuestItemsAndZones()
        {
            var tempRequiredLooseLoot = new HashSet<string>();

            foreach (var quest in this.savedQuestInfo)
            {
                try
                {
                    var scatterReadMap = new ScatterReadMap(1);
                    var round1 = scatterReadMap.AddRound();

                    var completedSubTasksListPtr = round1.AddEntry<ulong>(0, 0, quest.CompletedSubTasks, null, Offsets.Hashset.Base);
                    var completedSubTasksCountPtr = round1.AddEntry<int>(0, 1, quest.CompletedSubTasks, null, Offsets.Hashset.Count);

                    scatterReadMap.Execute();

                    if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var completedSubTaskList))
                        continue;
                    if (!scatterReadMap.Results[0][1].TryGetResult<int>(out var completedSubTaskCount))
                        continue;

                    if (completedSubTaskCount > 0)
                    {
                        var scatterReadMapConditions = new ScatterReadMap(completedSubTaskCount);
                        var cRound1 = scatterReadMapConditions.AddRound();
                        var hashsetStart = completedSubTaskList + Offsets.Hashset.Start;


                        for (int j = 0; j < completedSubTaskCount; j++)
                        {
                            var conditionID = cRound1.AddEntry<ulong>(j, 0, hashsetStart, null, (uint)j * Offsets.Hashset.Size);
                        }

                        scatterReadMapConditions.Execute();

                        for (int j = 0; j < completedSubTaskCount; j++)
                        {
                            try
                            {
                                if (!scatterReadMapConditions.Results[j][0].TryGetResult<ulong>(out var conditionIDPtr))
                                    continue;

                                var conditionID = Memory.ReadUnityString(conditionIDPtr);

                                if (!this.CompletedSubTasks.Contains(conditionID))
                                    this.CompletedSubTasks.Add(conditionID);
                            }
                            catch { }
                        }
                    }

                    var objectives = quest.Task.Objectives;

                    foreach (var objective in objectives)
                    {
                        if (objective is null ||
                            (objective.Type != "visit" &&
                            objective.Type != "mark" &&
                            objective.Type != "findQuestItem" &&
                            objective.Type != "plantItem" &&
                            objective.Type != "plantQuestItem" &&
                            objective.Type != "findItem" &&
                            objective.Type != "giveItem"))
                            continue;

                        var zones = objective.Zones;
                        var objectiveType = Regex.Replace(objective.Type, "(\\B[A-Z])", " $1");
                        objectiveType = objectiveType[0].ToString().ToUpper() + objectiveType.Substring(1);
                        var objectiveComplete = this.CompletedSubTasks.Contains(objective.ID);

                        if (zones is not null)
                        {
                            foreach (var zone in zones)
                            {
                                var zoneID = zone.id;
                                var existingQuestZone = this.QuestZones.FirstOrDefault(qz => qz.ID == zoneID);

                                if (existingQuestZone is null)
                                {
                                    this.QuestZones.Add(new QuestZone
                                    {
                                        ID = zoneID,
                                        MapName = zone.map.name,
                                        Position = new Vector3((float)zone.position.x, (float)zone.position.y, (float)zone.position.z),
                                        ObjectiveType = objectiveType,
                                        Description = objective.Description,
                                        TaskName = quest.Task.Name,
                                        Complete = objectiveComplete
                                    });
                                }
                                else
                                {
                                    existingQuestZone.Complete = objectiveComplete;
                                }
                            }
                        }

                        if ((objective.Type == "giveItem" || objective.Type == "findItem") && !objectiveComplete)
                        {
                            foreach (var item in objective.Items)
                            {
                                tempRequiredLooseLoot.Add(item.Id);
                            }
                        }

                        if (objective.Type == "findQuestItem")
                        {
                            var questItemID = objective.QuestItem.Id;
                            var existingQuestItem = this.QuestItems.FirstOrDefault(qi => qi.Id == questItemID);

                            if (existingQuestItem is null)
                            {
                                this.QuestItems.Add(new QuestItem
                                {
                                    Id = questItemID,
                                    Name = objective.QuestItem.Name,
                                    ShortName = objective.QuestItem.ShortName,
                                    NormalizedName = objective.QuestItem.NormalizedName,
                                    TaskName = quest.Task.Name,
                                    Description = objective.QuestItem.Description,
                                    Complete = objectiveComplete
                                });
                            }
                            else
                            {
                                existingQuestItem.Complete = objectiveComplete;
                            }
                        }
                    }
                }
                catch { }
            }

            RequiredItems = tempRequiredLooseLoot;
        }

        private struct QuestInfo
        {
            public string ID { get; set; }
            public Tasks Task { get; set; }
            public ulong CompletedSubTasks { get; set; }
        }
    }

    public class QuestItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string NormalizedName { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
        public bool Complete { get; set; }
    }

    public class QuestZone
    {
        public string ID { get; set; }
        public string MapName { get; set; }
        public string Description { get; set; }
        public string TaskName { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 ZoomedPosition { get; set; } = new();
        public string ObjectiveType { get; internal set; }
        public bool Complete { get; set; }
    }
}