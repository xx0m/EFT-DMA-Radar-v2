using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class ExfilManager
    {
        private bool IsAtHideout
        {
            get => Memory.InHideout;
        }

        private bool IsScav
        {
            get => Memory.IsScav;
        }

        private ulong localGameWorld
        {
            get; set;
        }

        public List<Exfil> Exfils
        {
            get; set;
        }

        public bool IsExtracting
        {
            get => this._isExtracting;
        }

        private bool _isExtracting;
        //private bool _closeExfils;

        private readonly Stopwatch _swRefresh = new();
        private readonly Stopwatch _swExtractCheck = new();
        private readonly Stopwatch _swExtractTimer = new();
        private Exfil currentExfil;

        public ExfilManager(ulong localGameWorld)
        {
            this.localGameWorld = localGameWorld;
            
            this._isExtracting = false;
            //this._closeExfils = false;

            this.Exfils = new List<Exfil>(new Exfil[0]);

            if (this.IsAtHideout)
            {
                Debug.WriteLine("In Hideout, not loading exfils.");
                return;
            }

            this.RefreshExfils();
            this._swRefresh.Start();
            this._swExtractCheck.Start();
        }

        private void RefreshExtractionCheck()
        {
            var openExfils = this.Exfils.Where(x => x.IsOpen);

            if (!openExfils.Any())
            {
                this.ResetExtractTimer();
                return;
            }

            var localPlayer = Memory.LocalPlayer;

            this._isExtracting = Memory.ReadValue<bool>(localPlayer.Base + Offsets.Player.IsExtracting);

            if (this._isExtracting)
            {
                var closestExfil = openExfils.OrderBy(x => Vector3.Distance(localPlayer.Position, x.Position)).FirstOrDefault();
                
                if (closestExfil is null)
                    return;

                var isPlayerClose = Vector3.Distance(localPlayer.Position, closestExfil.Position) <= 100;

                if (!isPlayerClose)
                    return;

                var profileID = localPlayer.ProfileID;

                this.UpdatePlayersMetRequirements(closestExfil);

                if (!closestExfil.PlayersMetRequirements.Contains(profileID) ||
                    (closestExfil.Type == ExfilType.SharedTimer && !closestExfil.QueuedPlayers.Contains(profileID)))
                    return;

                this.currentExfil = closestExfil;

                if (!this._swExtractTimer.IsRunning)
                    this._swExtractTimer.Start();

                var timeToExtract = (this.currentExfil.IsVehicleExtract ? 4f : (this.currentExfil.ExfiltrationTime - 4f)) * 1000;

                if (timeToExtract < 1)
                    timeToExtract = (2f * 1000);

                if (this._swExtractTimer.ElapsedMilliseconds >= timeToExtract)
                {
                    Memory.Chams.RestorePointers();
                    this.ResetExtractTimer();
                }
            }
            else
            {
                this.ResetExtractTimer();
            }
        }

        private void UpdatePlayersMetRequirements(Exfil exfil)
        {
            var count = 1;
            var scatterMap = new ScatterReadMap(count);
            var round1 = scatterMap.AddRound();
            var round2 = scatterMap.AddRound();

            if (exfil is null)
                return;

            for (int i = 0; i < count; i++)
            {
                var playersMetReqs = round1.AddEntry<ulong>(0, 0, exfil.BaseAddr, null, Offsets.ExfiltrationPoint.PlayersMetAllRequirements);
                var queuedPlayers = round1.AddEntry<ulong>(0, 1, exfil.BaseAddr, null, Offsets.ExfiltrationPoint.QueuedPlayers);

                round2.AddEntry<ulong>(0, 2, playersMetReqs, null, Offsets.UnityList.Base);
                round2.AddEntry<int>(0, 3, playersMetReqs, null, Offsets.UnityList.Count);

                round2.AddEntry<ulong>(0, 4, queuedPlayers, null, Offsets.UnityList.Base);
                round2.AddEntry<int>(0, 5, queuedPlayers, null, Offsets.UnityList.Count);
            }

            scatterMap.Execute();

            if (!scatterMap.Results[0][2].TryGetResult<ulong>(out var playersMetReqsList))
                return;
            if (!scatterMap.Results[0][3].TryGetResult<int>(out var playersMetReqsCount))
                return;
            if (!scatterMap.Results[0][4].TryGetResult<ulong>(out var queuedPlayersList))
                return;
            if (!scatterMap.Results[0][5].TryGetResult<int>(out var queuedPlayersCount))
                return;

            var tmpReqHashSet = new HashSet<string>();
            var tmpQueuedHashSet = new HashSet<string>();

            var reqListBase = (playersMetReqsList + Offsets.UnityListBase.Start);
            var queuedListBase = (queuedPlayersList + Offsets.UnityListBase.Start);

            for (int i = 0; i < playersMetReqsCount; i++)
            {
                try
                {
                    var playerIDPtr = Memory.ReadPtr(reqListBase + ((uint)i * 0x8));
                    var playerID = Memory.ReadUnityString(playerIDPtr);
                    tmpReqHashSet.Add(playerID);
                }
                catch { }
            }

            for (int i = 0; i < queuedPlayersCount; i++)
            {
                try
                {
                    var playerIDPtr = Memory.ReadPtr(queuedListBase + ((uint)i * 0x8));
                    var playerID = Memory.ReadUnityString(playerIDPtr);
                    tmpQueuedHashSet.Add(playerID);
                }
                catch { }
            }

            exfil.UpdateQueuedPlayers(tmpQueuedHashSet);
            exfil.UpdatePlayersMetRequirements(tmpReqHashSet);
        }

        private void ResetExtractTimer()
        {
            this._swExtractTimer.Restart();
            this._swExtractTimer.Stop();
            this.currentExfil = null;
        }

        public void RefreshExfils()
        {
            //if (Memory.IsTransitMode && this._closeExfils)
            //    return;
            
            //if (Memory.IsTransitMode && !this._closeExfils)
            //{
            //    this.CloseExfils();
            //    this._closeExfils = true;
            //    return;
            //}

            if (this._swExtractCheck.ElapsedMilliseconds >= 500 && Memory.LocalPlayer is not null)
            {
                this.RefreshExtractionCheck();
                this._swExtractCheck.Restart();
            }

            if (this._swRefresh.ElapsedMilliseconds >= 5000 && this.Exfils.Count > 0)
            {
                this.UpdateExfils();
                this._swRefresh.Restart();
            }
            else if (this.Exfils.Count < 1 && Memory.GameStatus == Game.GameStatus.InGame && this._swRefresh.ElapsedMilliseconds >= 1000)
            {
                this.GetExfils();
            }
        }

        //private void CloseExfils()
        //{
        //    foreach(var exfil in this.Exfils)
        //    {
        //        exfil.UpdateStatus(1);
        //    }
        //}

        private void UpdateExfils()
        {
            var scatterMap = new ScatterReadMap(this.Exfils.Count);
            var round1 = scatterMap.AddRound();

            for (int i = 0; i < this.Exfils.Count; i++)
            {
                round1.AddEntry<int>(i, 0, this.Exfils[i].BaseAddr + Offsets.Exfil.Status);
            }

            scatterMap.Execute();

            for (int i = 0; i < this.Exfils.Count; i++)
            {
                if (!scatterMap.Results[i][0].TryGetResult<int>(out var stat))
                    continue;

                this.Exfils[i].UpdateStatus(stat);
            }
        }

        public void GetExfils()
        {
            var scatterReadMap = new ScatterReadMap(1);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            var exfilControllerPtr = round1.AddEntry<ulong>(0, 0, this.localGameWorld, null, Offsets.LocalGameWorld.ExfilController);
            var exfilPointsPtr = round2.AddEntry<ulong>(0, 1, exfilControllerPtr, null, (this.IsScav ? Offsets.ExfilController.ScavExfilList : Offsets.ExfilController.PMCExfilList));
            var countPtr = round3.AddEntry<int>(0, 2, exfilPointsPtr, null, Offsets.ExfilController.ExfilCount);

            scatterReadMap.Execute();

            if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var exfilController))
                return;
            if (!scatterReadMap.Results[0][1].TryGetResult<ulong>(out var exfilPoints))
                return;
            if (!scatterReadMap.Results[0][2].TryGetResult<int>(out var count))
                return;

            if (count < 1 || count > 24)
                return;

            var scatterReadMap2 = new ScatterReadMap(count);
            var round4 = scatterReadMap2.AddRound();
            var round5 = scatterReadMap2.AddRound();
            var round6 = scatterReadMap2.AddRound();
            var round7 = scatterReadMap2.AddRound();

            for (int i = 0; i < count; i++)
            {
                var exfilAddr = round4.AddEntry<ulong>(i, 0, exfilPoints, null, Offsets.UnityListBase.Start + ((uint)i * 0x8));
                var localPlayer = round4.AddEntry<ulong>(i, 1, this.localGameWorld, null, Offsets.LocalGameWorld.MainPlayer);

                var localPlayerProfile = round5.AddEntry<ulong>(i, 2, localPlayer, null, Offsets.Player.Profile);
                var eligibleIds = round5.AddEntry<ulong>(i, 3, exfilAddr, null, Offsets.ExfiltrationPoint.EligibleIds);
                var eligibleEntryPoints = round5.AddEntry<ulong>(i, 4, exfilAddr, null, Offsets.ExfiltrationPoint.EligibleEntryPoints);

                var localPlayerInfo = round6.AddEntry<ulong>(i, 5, localPlayerProfile, null, Offsets.Profile.PlayerInfo);
                var eligibleIdsCount = round6.AddEntry<int>(i, 6, eligibleIds, null, Offsets.UnityList.Count);
                var eligibleEntryPointsCount = round6.AddEntry<int>(i, 7, eligibleEntryPoints, null, Offsets.UnityList.Count);

                var localPlayerEntryPoint = round7.AddEntry<ulong>(i, 8, localPlayerInfo, null, Offsets.PlayerInfo.EntryPoint);

                var exfilSettings = round5.AddEntry<ulong>(i, 9, exfilAddr, null, Offsets.Exfil.Settings);
                var exfilRequirements = round5.AddEntry<ulong>(i, 10, exfilAddr, null, Offsets.Exfil.Requirements);

                var extractType = round6.AddEntry<int>(i, 11, exfilSettings, null, Offsets.ExfilSettings.Type);
                var exfiltrationTime = round6.AddEntry<float>(i, 12, exfilSettings, null, Offsets.ExfilSettings.Time);
                var requirementsCount = round6.AddEntry<int>(i, 13, exfilRequirements, null, Offsets.UnityList.Count);
            }

            scatterReadMap2.Execute();

            var list = new ConcurrentBag<Exfil>();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap2.Results[i][0].TryGetResult<ulong>(out var exfilAddr))
                    continue;
                if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var localPlayer))
                    continue;
                if (!scatterReadMap2.Results[i][9].TryGetResult<ulong>(out var exfilSettings))
                    continue;
                if (!scatterReadMap2.Results[i][10].TryGetResult<ulong>(out var exfilRequirements))
                    continue;
                if (!scatterReadMap2.Results[i][11].TryGetResult<int>(out var type))
                    continue;
                if (!scatterReadMap2.Results[i][12].TryGetResult<float>(out var exfilTime))
                    continue;
                if (!scatterReadMap2.Results[i][13].TryGetResult<int>(out var exfilReqCount))
                    continue;

                try
                {
                    var exfil = new Exfil(exfilAddr, exfilSettings);
                    exfil.UpdateName();
                    exfil.UpdateType(type);
                    exfil.UpdateExfilTime(exfilTime);

                    if (exfilReqCount > 0)
                    {
                        for (int j = 0; j < exfilReqCount; j++)
                        {
                            try
                            {
                                var requirementBase = Memory.ReadPtr(exfilRequirements + Offsets.UnityListBase.Start + ((uint)j * 0x8));
                                var requirement = Memory.ReadValue<int>(requirementBase + Offsets.ExfilRequirements.Requirement);
                                exfil.AddRequirement(requirement);
                            }
                            catch { }
                        }
                    }

                    if (this.IsScav)
                    {
                        scatterReadMap2.Results[i][3].TryGetResult<ulong>(out var eligibleIds);
                        scatterReadMap2.Results[i][6].TryGetResult<int>(out var eligibleIdsCount);

                        if (eligibleIdsCount != 0)
                        {
                            list.Add(exfil);
                            continue;
                        }
                    }
                    else
                    {
                        scatterReadMap2.Results[i][2].TryGetResult<ulong>(out var localPlayerProfile);
                        scatterReadMap2.Results[i][5].TryGetResult<ulong>(out var localPlayerInfo);
                        scatterReadMap2.Results[i][8].TryGetResult<ulong>(out var localPlayerEntryPoint);
                        scatterReadMap2.Results[i][4].TryGetResult<ulong>(out var eligibleEntryPoints);
                        scatterReadMap2.Results[i][7].TryGetResult<int>(out var eligibleEntryPointsCount);

                        var localPlayerEntryPointString = Memory.ReadUnityString(localPlayerEntryPoint);

                        for (uint j = 0; j < eligibleEntryPointsCount; j++)
                        {
                            var entryPoint = Memory.ReadPtr(eligibleEntryPoints + 0x20 + (j * 0x8));
                            var entryPointString = Memory.ReadUnityString(entryPoint);

                            if (entryPointString.ToLower() == localPlayerEntryPointString.ToLower())
                            {
                                list.Add(exfil);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.Log($"ExfilManager -> {ex.Message}\n{ex.StackTrace}");
                    continue;
                }
            }

            this.Exfils = new List<Exfil>(list);
        }
    }

    #region Classes_Enums
    public class Exfil
    {
        public ulong BaseAddr { get; }
        public ulong SettingsAddr { get; }
        public Vector3 Position { get; }
        public ExfilStatus Status { get; private set; } = ExfilStatus.Closed;
        public ExfilType Type { get; private set; } = ExfilType.Individual;
        public string Name { get; private set; } = "?";
        public float ExfiltrationTime { get; private set; } = 8f;

        public HashSet<string> PlayersMetRequirements { get; private set; } = new HashSet<string>();
        public HashSet<string> QueuedPlayers { get; private set; } = new HashSet<string>();

        public List<ExfilRequirement> Requirements { get; private set; } = new List<ExfilRequirement>();


        public bool IsVehicleExtract
        {
            get => this.Requirements.Any(x => x == ExfilRequirement.TransferItem);
        }

        public bool IsOpen
        {
            get => this.Status == ExfilStatus.Open;
        }

        public Exfil(ulong baseAddr, ulong settingsAddr)
        {
            this.BaseAddr = baseAddr;
            this.SettingsAddr = settingsAddr;

            var transform_internal = Memory.ReadPtrChain(baseAddr, Offsets.GameObject.To_TransformInternal);
            this.Position = new Transform(transform_internal).GetPosition();
        }

        public void UpdateStatus(int status) => this.Status = status switch
        {
            1 => ExfilStatus.Closed,
            2 => ExfilStatus.Pending,
            3 => ExfilStatus.Open,
            4 => ExfilStatus.Open,
            5 => ExfilStatus.Pending,
            6 => ExfilStatus.Pending,
            _ => ExfilStatus.Closed
        };

        private ExfilRequirement GetRequirementEnum(int requirement) => requirement switch
        {
            0 => ExfilRequirement.None,
            1 => ExfilRequirement.Empty,
            2 => ExfilRequirement.TransferItem,
            3 => ExfilRequirement.WorldEvent,
            4 => ExfilRequirement.NotEmpty,
            5 => ExfilRequirement.HasItem,
            6 => ExfilRequirement.WearsItem,
            7 => ExfilRequirement.EmptyOrSize,
            8 => ExfilRequirement.SkillLevel,
            9 => ExfilRequirement.Reference,
            10 => ExfilRequirement.ScavCooperation,
            11 => ExfilRequirement.Train,
            12 => ExfilRequirement.Timer,

            _ => ExfilRequirement.None
        };

        public void AddRequirement(int requirement)
        {
            var reqEnum = this.GetRequirementEnum(requirement);

            this.Requirements.Add(reqEnum);
        }

        public void UpdateType(int type) => this.Type = type switch
        {
            0 => ExfilType.Individual,
            1 => ExfilType.SharedTimer,
            2 => ExfilType.Manual,
            _ => ExfilType.Individual
        };

        public void UpdateExfilTime(float time)
        {
            this.ExfiltrationTime = time;
        }

        public void UpdateName()
        {
            var name = Memory.MapNameFormatted;

            if (TarkovDevManager.AllMaps.TryGetValue(name, out var map))
            {
                foreach (var extract in map.extracts)
                {
                    if (this.Position == extract.position || Vector3.Distance(extract.position, this.Position) <= 10)
                    {
                        this.Name = extract.name;
                        break;
                    }
                }
            }
        }

        public void UpdateQueuedPlayers(HashSet<string> queuedPlayers)
        {
            this.QueuedPlayers = queuedPlayers;
        }

        public void UpdatePlayersMetRequirements(HashSet<string> playersMetRequirements)
        {
            this.PlayersMetRequirements = playersMetRequirements;
        }
    }

    public enum ExfilStatus
    {
        Open,
        Pending,
        Closed
    }

    public enum ExfilType
    {
        Individual,
        SharedTimer,
        Manual
    }

    public enum ExfilRequirement
    {
        None,
        Empty,
        TransferItem,
        WorldEvent,
        NotEmpty,
        HasItem,
        WearsItem,
        EmptyOrSize,
        SkillLevel,
        Reference,
        ScavCooperation,
        Train,
        Timer
    }
    #endregion
}
