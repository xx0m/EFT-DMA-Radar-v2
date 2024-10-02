using Offsets;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class TransitManager
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

        public List<Transit> Transits
        {
            get; set;
        }

        //public bool IsTransitMode
        //{
        //    get => this._IsTransitMode;
        //}

        //public bool IsExtracting
        //{
        //    get => this._isExtracting;
        //}

        //private bool _isExtracting = false;
        //private bool _IsTransitMode = false;

        //private ulong _transitPlayers;

        private readonly Stopwatch _swRefresh = new();
        private readonly Stopwatch _swExtractCheck = new();
        private readonly Stopwatch _swExtractTimer = new();
        private Transit currentTransit;

        public TransitManager(ulong localGameWorld)
        {
            this.localGameWorld = localGameWorld;

            this.Transits = new List<Transit>(new Transit[0]);

            if (this.IsAtHideout)
            {
                Debug.WriteLine("In Hideout, not loading transits.");
                return;
            }

            this.RefreshTransits();
            this._swRefresh.Start();
            this._swExtractCheck.Start();
        }

        // ghetto but works
        //private void RefreshExtractionCheck()
        //{
        //    var openTransits = this.Transits.Where(x => x.IsOpen);

        //    if (!openTransits.Any())
        //    {
        //        this.ResetExtractTimer();
        //        return;
        //    }

        //    var localPlayer = Memory.LocalPlayer;

        //    var closestTransit = openTransits.OrderBy(x => Vector3.Distance(localPlayer.Position, x.Position)).FirstOrDefault();

        //    if (closestTransit is null)
        //    {
        //        if (this._isExtracting)
        //            this._isExtracting = false;
        //        return;
        //    }

        //    var isPlayerClose = Vector3.Distance(localPlayer.Position, closestTransit.Position) <= 10;

        //    if (!isPlayerClose)
        //    {
        //        if (this._isExtracting)
        //            this._isExtracting = false;
        //        return;
        //    }

        //    this.UpdateQueuedPlayers(closestTransit);

        //    this._isExtracting = closestTransit.QueuedPlayers.Any(x => x == localPlayer.ProfileID);

        //    if (this._isExtracting)
        //    {
        //        var profileID = localPlayer.ProfileID;

        //        if (!closestTransit.QueuedPlayers.Contains(profileID))
        //            return;

        //        this.currentTransit = closestTransit;

        //        if (!this._swExtractTimer.IsRunning)
        //            this._swExtractTimer.Start();

        //        var timeToExtract = (this.currentTransit.ExfiltrationTime - 5f) * 1000;

        //        if (timeToExtract < 1)
        //            timeToExtract = (2f * 1000);

        //        if (this._swExtractTimer.ElapsedMilliseconds >= timeToExtract)
        //        {
        //            Memory.Chams.RestorePointers();
        //            this.ResetExtractTimer();
        //        }
        //    }
        //    else
        //    {
        //        this.ResetExtractTimer();
        //    }
        //}
        private void RefreshExtractionCheck()
        {
            var openTransits = this.Transits.Where(x => x.IsOpen);

            if (!openTransits.Any())
            {
                this.ResetExtractTimer();
                return;
            }

            var localPlayer = Memory.LocalPlayer;

            var closestTransit = openTransits.OrderBy(x => Vector3.Distance(localPlayer.Position, x.Position)).FirstOrDefault();

            if (closestTransit is null)
            {
                this.ResetExtractTimer();
                return;
            }

            var isPlayerClose = Vector3.Distance(localPlayer.Position, closestTransit.Position) <= 10;

            if (!isPlayerClose)
            {
                this.ResetExtractTimer();
                return;
            }

            this.currentTransit = closestTransit;

            if (!this._swExtractTimer.IsRunning)
                this._swExtractTimer.Start();

            var timeToExtract = (this.currentTransit.ExfiltrationTime - 5f) * 1000;

            if (timeToExtract < 1)
                timeToExtract = (2f * 1000);

            if (this._swExtractTimer.ElapsedMilliseconds >= timeToExtract)
            {
                Memory.Chams.RestorePointers();
                this.ResetExtractTimer();
            }
        }

        //private void UpdateQueuedPlayers(Transit transit)
        //{
        //    var count = 1;
        //    var scatterMap = new ScatterReadMap(count);
        //    var round1 = scatterMap.AddRound();
        //    var round2 = scatterMap.AddRound();

        //    if (transit is null)
        //        return;

        //    for (int i = 0; i < count; i++)
        //    {
        //        var queuedPlayers = round1.AddEntry<ulong>(0, 0, transit.BaseAddr, null, Offsets.TransitController.QueuedPlayers);

        //        round2.AddEntry<ulong>(0, 1, queuedPlayers, null, Offsets.UnityDictionary.Base);
        //        round2.AddEntry<int>(0, 2, queuedPlayers, null, Offsets.UnityDictionary.Count);
        //    }

        //    scatterMap.Execute();

        //    if (!scatterMap.Results[0][1].TryGetResult<ulong>(out var queuedPlayersList))
        //        return;
        //    if (!scatterMap.Results[0][2].TryGetResult<int>(out var queuedPlayersCount) || queuedPlayersCount < 1)
        //        return;

        //    var tmpQueuedHashSet = new HashSet<string>();

        //    var scatterMap2 = new ScatterReadMap(queuedPlayersCount);
        //    var round3 = scatterMap2.AddRound();

        //    for (int i = 0; i < count; i++)
        //    {
        //        var queuedPlayer = round3.AddEntry<ulong>(0, i, queuedPlayersList, null, 0x30 + (0x18 * (uint)i));
        //    }

        //    scatterMap2.Execute();

        //    for (int i = 0; i < queuedPlayersCount; i++)
        //    {
        //        try
        //        {
        //            if (!scatterMap2.Results[0][i].TryGetResult<ulong>(out var queuedPlayer) || queuedPlayer == 0)
        //                return;

        //            var playerID = Memory.ReadUnityString(queuedPlayer);
        //            tmpQueuedHashSet.Add(playerID);
        //        }
        //        catch { }
        //    }

        //    transit.UpdateQueuedPlayers(tmpQueuedHashSet);
        //}

        private void ResetExtractTimer()
        {
            this._swExtractTimer.Restart();
            this._swExtractTimer.Stop();
            this.currentTransit = null;
        }

        public void RefreshTransits()
        {
            //if (this._swExtractCheck.ElapsedMilliseconds >= 500 && Memory.LocalPlayer is not null && this._IsTransitMode)
            if (this._swExtractCheck.ElapsedMilliseconds >= 500 && Memory.LocalPlayer is not null)
            {
                this.RefreshExtractionCheck();
                this._swExtractCheck.Restart();
            }

            //if (this._swRefresh.ElapsedMilliseconds >= 5000 && this.Transits.Count > 0)
            //{
            //    this.CheckTransitPlayers();
            //    this.UpdateTransits();
            //    this._swRefresh.Restart();
            //}
            //else if (this.Transits.Count < 1 && Memory.GameStatus == Game.GameStatus.InGame && this._swRefresh.ElapsedMilliseconds >= 1000)
            if (this.Transits.Count < 1 && Memory.GameStatus == Game.GameStatus.InGame && this._swRefresh.ElapsedMilliseconds >= 1000)
            {
                this.GetTransits();
                this._swRefresh.Stop();
            }
        }

        //private void CheckTransitPlayers()
        //{
        //    if (this._IsTransitMode)
        //        return;

        //    var transitControllerPtr = Memory.ReadPtr(this.localGameWorld + Offsets.LocalGameWorld.TransitController);
        //    var transitsBasePtr = Memory.ReadPtr(transitControllerPtr + Offsets.TransitController.Transits);
        //    var transitPlayersPtr = Memory.ReadPtr(transitControllerPtr + 0x20);
        //    var transitPlayersCount = Memory.ReadValue<int>(transitPlayersPtr + Offsets.UnityDictionary.Count);

        //    if (transitPlayersCount > 0)
        //    {
        //        var scatterMap = new ScatterReadMap(transitPlayersCount);
        //        var round1 = scatterMap.AddRound();

        //        var transitPlayersBase = Memory.ReadPtr(this._transitPlayers + Offsets.UnityDictionary.Base);

        //        for (int i = 0; i < transitPlayersCount; i++)
        //        {
        //            var transitPlayer = round1.AddEntry<ulong>(i, 0, transitPlayersBase, null, 0x28 + ((uint)i * 0x18));
        //        }

        //        scatterMap.Execute();

        //        for (int i = 0; i < transitPlayersCount; i++)
        //        {
        //            if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var transitPlayerPtr))
        //                continue;

        //            try
        //            {
        //                var transitPlayer = Memory.ReadUnityString(transitPlayerPtr);

        //                if (transitPlayer == Memory.LocalPlayer.ProfileID)
        //                {
        //                    this._IsTransitMode = true;
        //                    break;
        //                }
        //            }
        //            catch { }
        //        }
        //    }
        //}

        //private void UpdateTransits()
        //{
        //    if (this.Transits.Any(x => x.Status == TransitStatus.Open) && this._IsTransitMode)
        //        return;

        //    var scatterMap = new ScatterReadMap(this.Transits.Count);
        //    var round1 = scatterMap.AddRound();

        //    for (int i = 0; i < this.Transits.Count; i++)
        //    {
        //        round1.AddEntry<int>(i, 0, this.Transits[i].TransitParametersAddr + Offsets.TransitPointParameters.ActivateAfterSec);
        //    }

        //    scatterMap.Execute();

        //    for (int i = 0; i < this.Transits.Count; i++)
        //    {
        //        if (!scatterMap.Results[i][0].TryGetResult<int>(out var activeAfter))
        //            continue;

        //        if (this._IsTransitMode)
        //            this.Transits[i].UpdateStatus(3);
        //        else if (activeAfter >= 1)
        //            this.Transits[i].UpdateStatus(1);
        //        else if (activeAfter == 0)
        //            this.Transits[i].UpdateStatus(2);
        //    }
        //}

        public void GetTransits()
        {
            var scatterReadMap = new ScatterReadMap(1);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();

            var transitControllerPtr = round1.AddEntry<ulong>(0, 0, this.localGameWorld, null, Offsets.LocalGameWorld.TransitController);
            var transitsBasePtr = round2.AddEntry<ulong>(0, 1, transitControllerPtr, null, Offsets.TransitController.Transits);
            //var transitPlayersPtr = round2.AddEntry<ulong>(0, 2, transitControllerPtr, null, Offsets.TransitController.TransitPlayers);

            var transitsPtr = round3.AddEntry<ulong>(0, 3, transitsBasePtr, null, Offsets.UnityDictionary.Base);
            var countPtr = round3.AddEntry<int>(0, 4, transitsBasePtr, null, Offsets.UnityDictionary.Count);

            scatterReadMap.Execute();

            if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var transitController))
                return;
            //if (!scatterReadMap.Results[0][2].TryGetResult<ulong>(out var transitPlayers))
            //    return;
            if (!scatterReadMap.Results[0][3].TryGetResult<ulong>(out var transits))
                return;
            if (!scatterReadMap.Results[0][4].TryGetResult<int>(out var count))
                return;

            if (count < 1 || count > 24)
                return;

            var scatterReadMap2 = new ScatterReadMap(count);
            var round4 = scatterReadMap2.AddRound();
            var round5 = scatterReadMap2.AddRound();
            var round6 = scatterReadMap2.AddRound();

            for (int i = 0; i < count; i++)
            {
                var transitAddr = round4.AddEntry<ulong>(i, 0, transits, null, 0x30 + ((uint)i * 0x18));
                var transitParameters = round5.AddEntry<ulong>(i, 1, transitAddr, null, Offsets.TransitPoint.Parameters);
                var id = round6.AddEntry<int>(i, 2, transitParameters, null, Offsets.TransitPointParameters.ID);
                var time = round6.AddEntry<int>(i, 3, transitParameters, null, Offsets.TransitPointParameters.Time);
            }

            scatterReadMap2.Execute();

            var list = new ConcurrentBag<Transit>();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap2.Results[i][0].TryGetResult<ulong>(out var transitAddr))
                    continue;
                if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var transitParameters))
                    continue;
                if (!scatterReadMap2.Results[i][2].TryGetResult<int>(out var ID))
                    continue;
                if (!scatterReadMap2.Results[i][3].TryGetResult<int>(out var transitTime))
                    continue;

                try
                {
                    var transit = new Transit(transitAddr, transitParameters, ID);
                    transit.UpdateName();
                    transit.UpdateTransitTime(transitTime);

                    if (this.IsScav && transit.Name.Contains("lab", StringComparison.OrdinalIgnoreCase))
                        continue;

                    list.Add(transit);
                }
                catch (Exception ex)
                {
                    Program.Log($"TransitManager -> {ex.Message}\n{ex.StackTrace}");
                    continue;
                }
            }

            //this._transitPlayers = transitPlayers;
            this.Transits = new List<Transit>(list);
        }
    }

    #region Classes_Enums
    public class Transit
    {
        public int ID { get; }
        public ulong BaseAddr { get; }
        public Vector3 Position { get; }
        public ulong TransitParametersAddr { get; }
        //public ulong TransitPlayersAddr { get; }
        public TransitStatus Status { get; private set; } = TransitStatus.Open;
        public string Name { get; private set; } = "?";
        public float ExfiltrationTime { get; private set; } = 30f;
        //public HashSet<string> QueuedPlayers { get; private set; } = new HashSet<string>();

        public bool IsOpen
        {
            get => this.Status == TransitStatus.Open;
        }

        public Transit(ulong baseAddr, ulong transitParameters, int ID)
        {
            this.ID = ID;
            this.BaseAddr = baseAddr;
            this.TransitParametersAddr = transitParameters;
            
            var transform_internal = Memory.ReadPtrChain(this.BaseAddr, Offsets.GameObject.To_TransformInternal);
            this.Position = new Transform(transform_internal).GetPosition();
        }

        public void UpdateTransitTime(float time)
        {
            this.ExfiltrationTime = time;
        }

        public void UpdateStatus(int status) => this.Status = status switch
        {
            1 => TransitStatus.Closed,
            2 => TransitStatus.Pending,
            3 => TransitStatus.Open,
            _ => TransitStatus.Closed
        };

        public void UpdateName()
        {
            var name = Memory.MapNameFormatted;

            if (TarkovDevManager.AllMaps.TryGetValue(name, out var map))
            {
                foreach (var transit in map.transits)
                {
                    if (this.ID.ToString() == transit.id || this.Position == transit.position || Vector3.Distance(transit.position, this.Position) <= 10)
                    {
                        this.Name = transit.description;
                        break;
                    }
                }
            }
        }

        //public void UpdateQueuedPlayers(HashSet<string> queuedPlayers)
        //{
        //    this.QueuedPlayers = queuedPlayers;
        //}
    }

    public enum TransitStatus
    {
        Open,
        Pending,
        Closed
    }
    #endregion
}
