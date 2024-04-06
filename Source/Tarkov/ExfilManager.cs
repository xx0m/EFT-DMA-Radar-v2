using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using eft_dma_radar.Source.Misc;
using eft_dma_radar.Source.Tarkov;
using Offsets;
using OpenTK.Graphics.ES20;

namespace eft_dma_radar
{
    public class ExfilManager
    {
        private bool IsAtHideout
        {
            get => Memory.InHideout;
        }

        private bool IsScav { get => Memory.IsScav; }
        private readonly Stopwatch _swStatus = new();
        private readonly Stopwatch _swExfils = new();
        private ulong localGameWorld { get; set; }
        /// <summary>
        /// List of PMC Exfils in Local Game World and their position/status.
        /// </summary>
        public ReadOnlyCollection<Exfil> Exfils { get; set; }

        public ExfilManager(ulong localGameWorld)
        {
            this.localGameWorld = localGameWorld;
            this.Exfils = new ReadOnlyCollection<Exfil>(new Exfil[0]);

            //If we are in hideout, we don't need to do anything.
            if (this.IsAtHideout)
            {
                Debug.WriteLine("In Hideout, not loading exfils.");
                return;
            }

            this.RefreshExfils();

            this._swExfils.Start();
            this._swStatus.Start();
        }

        /// <summary>
        /// Checks if Exfils are due for a refresh, and then refreshes them.
        /// </summary>
        public void RefreshExfils()
        {
            bool shouldUpdateExfils = false;

            if (this._swStatus.ElapsedMilliseconds >= 5000)
            {
                shouldUpdateExfils = true;
                this._swStatus.Restart();
            }

            if (this._swExfils.ElapsedMilliseconds >= 250 && this.Exfils.Count < 1)
            {
                try
                {
                    this.GetExfils();
                    shouldUpdateExfils = true;
                    this._swExfils.Stop();
                }
                catch { }
            }

            if (shouldUpdateExfils)
            {
                this.UpdateExfils();

                this._swExfils.Restart();
            }
        }

        /// <summary>
        /// Updates exfil statuses.
        /// </summary>
        private void UpdateExfils()
        {
            try {
                var scatterMap = new ScatterReadMap(this.Exfils.Count);
                var round1 = scatterMap.AddRound();
                for (int i = 0; i < this.Exfils.Count; i++)
                {
                    round1.AddEntry<int>(i, 0, this.Exfils[i].BaseAddr + Offsets.Exfil.Status);
                }
                scatterMap.Execute();
                for (int i = 0; i < this.Exfils.Count; i++)
                {
                    try {
                        var status = scatterMap.Results[i][0].TryGetResult<int>(out var stat);
                        this.Exfils[i].UpdateStatus(stat);
                    }
                    catch{}

                }
            }
            catch{}
            
        }

        public void GetExfils()
        {

            try
            {
                var exfilController = Memory.ReadPtr(this.localGameWorld + Offsets.LocalGameWorld.ExfilController);
                var exfilPoints = this.IsScav ? Memory.ReadPtr(exfilController + Offsets.ExfilController.ScavExfilList) : Memory.ReadPtr(exfilController + Offsets.ExfilController.PMCExfilList);
                var count = Memory.ReadValue<int>(exfilPoints + Offsets.ExfilController.ExfilCount);

                if (count < 1 || count > 24)
                {
                    throw new ArgumentOutOfRangeException();
                }

                var list = new List<Exfil>();

                for (uint i = 0; i < count; i++)
                {
                    var exfilAddr = Memory.ReadPtr(exfilPoints + Offsets.UnityListBase.Start + (i * 0x08));

                    Exfil exfil = new Exfil(exfilAddr);
                    exfil.UpdateName();

                    var localPlayer = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.MainPlayer);
                    var localPlayerProfile = Memory.ReadPtr(localPlayer + Offsets.Player.Profile); // to EFT.Profile
                    var localPlayerInfo = Memory.ReadPtr(localPlayerProfile + Offsets.Profile.PlayerInfo); // to EFT.Profile.Info

                    if (this.IsScav)
                    {
                        var eligibleIds = Memory.ReadPtr(exfilAddr + 0xC0);
                        var eligibleIdsCount = Memory.ReadValue<int>(eligibleIds + Offsets.UnityList.Count);
                        if (eligibleIdsCount != 0)
                        {
                            list.Add(exfil);
                            continue;
                        }
                    }
                    else
                    {
                        var localPlayerEntryPoint = Memory.ReadPtr(localPlayerInfo + Offsets.PlayerInfo.EntryPoint);
                        var localPlayerEntryPointString = Memory.ReadUnityString(localPlayerEntryPoint);

                        var eligibleEntryPoints = Memory.ReadPtr(exfilAddr + Offsets.ExfiltrationPoint.EligibleEntryPoints);
                        var eligibleEntryPointsCount = Memory.ReadValue<int>(eligibleEntryPoints + Offsets.UnityList.Count);
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

                this.Exfils = new ReadOnlyCollection<Exfil>(list);
            }
            catch { }
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
            this.Position = new Transform(transform_internal).GetPosition();
        }

        /// <summary>
        /// Update status of exfil.
        /// </summary>
        public void UpdateStatus(int status)
        {
            switch (status)
            {
                case 1: // NotOpen
                    this.Status = ExfilStatus.Closed;
                    break;
                case 2: // IncompleteRequirement
                    this.Status = ExfilStatus.Pending;
                    break;
                case 3: // Countdown
                    this.Status = ExfilStatus.Open;
                    break;
                case 4: // Open
                    this.Status = ExfilStatus.Open;
                    break;
                case 5: // Pending
                    this.Status = ExfilStatus.Pending;
                    break;
                case 6: // AwaitActivation
                    this.Status = ExfilStatus.Pending;
                    break;
                default:
                    break;
            }
        }

        public void UpdateName()
        {
            var name = TarkovDevManager.GetMapName(Memory.MapName);

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
