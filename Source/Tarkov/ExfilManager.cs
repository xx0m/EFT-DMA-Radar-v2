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

        public ReadOnlyCollection<Exfil> Exfils
        {
            get; set;
        }

        private readonly Stopwatch _swRefresh = new();

        public ExfilManager(ulong localGameWorld)
        {
            this.localGameWorld = localGameWorld;
            this.Exfils = new ReadOnlyCollection<Exfil>(new Exfil[0]);

            if (this.IsAtHideout)
            {
                Debug.WriteLine("In Hideout, not loading exfils.");
                return;
            }

            this.RefreshExfils();
            this._swRefresh.Start();
        }

        public void RefreshExfils()
        {
            if (this._swRefresh.ElapsedMilliseconds >= 5000 && this.Exfils.Count > 0)
            {
                this.UpdateExfils();
                this._swRefresh.Restart();
            }
            else if (this.Exfils.Count < 1 && Memory.GameStatus == Game.GameStatus.InGame && this._swRefresh.ElapsedMilliseconds >= 250)
            {
                this.GetExfils();
            }
        }

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
                var exfilAddr = round4.AddEntry<ulong>(i, 0, exfilPoints, null, Offsets.UnityListBase.Start + ((uint)i * 0x08));
                var localPlayer = round4.AddEntry<ulong>(i, 1, this.localGameWorld, null, Offsets.LocalGameWorld.MainPlayer);

                var localPlayerProfile = round5.AddEntry<ulong>(i, 2, localPlayer, null, Offsets.Player.Profile);
                var eligibleIds = round5.AddEntry<ulong>(i, 3, exfilAddr, null, Offsets.ExfiltrationPoint.EligibleIds);
                var eligibleEntryPoints = round5.AddEntry<ulong>(i, 4, exfilAddr, null, Offsets.ExfiltrationPoint.EligibleEntryPoints);

                var localPlayerInfo = round6.AddEntry<ulong>(i, 5, localPlayerProfile, null, Offsets.Profile.PlayerInfo);
                var eligibleIdsCount = round6.AddEntry<int>(i, 6, eligibleIds, null, Offsets.UnityList.Count);
                var eligibleEntryPointsCount = round6.AddEntry<int>(i, 7, eligibleEntryPoints, null, Offsets.UnityList.Count);

                var localPlayerEntryPoint = round7.AddEntry<ulong>(i, 8, localPlayerInfo, null, Offsets.PlayerInfo.EntryPoint);
            }

            scatterReadMap2.Execute();

            var list = new ConcurrentBag<Exfil>();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap2.Results[i][0].TryGetResult<ulong>(out var exfilAddr))
                    continue;
                if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var localPlayer))
                    continue;

                var exfil = new Exfil(exfilAddr);
                exfil.UpdateName();

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

            this.Exfils = new ReadOnlyCollection<Exfil>(list.ToList());
        }
    }

    #region Classes_Enums
    public class Exfil
    {
        public ulong BaseAddr { get; }
        public Vector3 Position { get; }
        public ExfilStatus Status { get; private set; } = ExfilStatus.Closed;
        public string Name { get; private set; } = "?";

        public Exfil(ulong baseAddr)
        {
            this.BaseAddr = baseAddr;
            var transform_internal = Memory.ReadPtrChain(baseAddr, Offsets.GameObject.To_TransformInternal);
            this.Position = new Transform(transform_internal, false).GetPosition();
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
    }

    public enum ExfilStatus
    {
        Open,
        Pending,
        Closed
    }
    #endregion
}
