using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;
using eft_dma_radar.Source.Misc;
using eft_dma_radar.Source.Tarkov;
using Offsets;

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
        private readonly Stopwatch _sw = new();
        /// <summary>
        /// List of PMC Exfils in Local Game World and their position/status.
        /// </summary>
        public ReadOnlyCollection<Exfil> Exfils { get; }

        public ExfilManager(ulong localGameWorld)
        {
            //If we are in hideout, we don't need to do anything.
            if (this.IsAtHideout)
            {
                Debug.WriteLine("In Hideout, not loading exfils.");
                return;
            }
            try
            {
                var exfilController = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.ExfilController);
                var exfilPoints = (this.IsScav ? Memory.ReadPtr(exfilController + 0x28) : Memory.ReadPtr(exfilController + Offsets.ExfilController.ExfilList));
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
                    var exfilSettings = Memory.ReadPtr(exfilAddr + Offsets.Exfil.Settings);
                    var exfilName = Memory.ReadPtr(exfilSettings + Offsets.Exfil.Name);
                    var exfilUnityName = Memory.ReadUnityString(exfilName);
                    exfil.UpdateName(exfilUnityName);

                    if (this.IsScav)
                    {
                        list.Add(exfil);
                    }
                    else
                    {
                        var localPlayer = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.MainPlayer);
                        var localPlayerProfile = Memory.ReadPtr(localPlayer + Offsets.Player.Profile);
                        var localPlayerInfo = Memory.ReadPtr(localPlayerProfile + Offsets.Profile.PlayerInfo);
                        var localPlayerEntryPoint = Memory.ReadPtr(localPlayerInfo + 0x30); // causes low fps in raid wait
                        var localPlayerEntryPointString = Memory.ReadUnityString(localPlayerEntryPoint);

                        var eligibleEntryPoints = Memory.ReadPtr(exfilAddr + 0x80);
                        var eligibleEntryPointsCount = Memory.ReadValue<int>(eligibleEntryPoints + 0x18);
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
                this.UpdateExfils();
                this._sw.Start();
            } catch {}
        }

        /// <summary>
        /// Checks if Exfils are due for a refresh, and then refreshes them.
        /// </summary>
        public void Refresh()
        {
            if (_sw.ElapsedMilliseconds < 5000) return;
            UpdateExfils();
            _sw.Restart();
        }

        /// <summary>
        /// Updates exfil statuses.
        /// </summary>
        private void UpdateExfils()
        {
            try {
                var scatterMap = new ScatterReadMap(Exfils.Count);
                var round1 = scatterMap.AddRound();
                for (int i = 0; i < Exfils.Count; i++)
                {
                    round1.AddEntry<int>(i, 0, Exfils[i].BaseAddr + Offsets.Exfil.Status);
                }
                scatterMap.Execute();
                for (int i = 0; i < Exfils.Count; i++)
                {
                    try {
                        var status = scatterMap.Results[i][0].TryGetResult<int>(out var stat);
                        Exfils[i].UpdateStatus(stat);
                    }
                    catch{}

                }
            }
            catch{}
            
        }
    }

    #region Classes_Enums
    public class Exfil
    {
        public ulong BaseAddr { get; }
        public Vector3 Position { get; }
        public ExfilStatus Status { get; private set; } = ExfilStatus.Closed;
        public string Name { get; private set; } = "?";

        private Dictionary<string, string> streetsExfilNames = new Dictionary<string, string>
        {
            // pmc extracts
            ["E1"] = "Stylobate Building Elevator",
            ["E2"] = "Sewer River",
            ["E3"] = "Damaged House",
            ["E4"] = "Crash Site",
            ["E5"] = "Collapsed Crane",
            ["E7"] = "Expo Checkpoint",
            ["E7-car"] = "Primorsky Ave Taxi",
            ["E8-yard"] = "Courtyard",
            ["E9-sniper"] = "Klimov Street",
            ["Exit-E10-coop"] = "Pinewood Basement",

            //scav extracts
            ["scav-e1"] = "Basement Descent",
            ["scav-e2"] = "Entrance to Catacombs",
            ["scav-e3"] = "Ventilation Shaft",
            ["scav-e4"] = "Sewer Manhole",
            ["scav-e5"] = "Near Kamchatskaya Arch",
            ["scav-e7"] = "Cardinal Apartment Complex Parking",
            ["scav-e8"] = "Klimov Shopping Mall",
            ["Exit-E10-coop"] = "Pinewood Basement"
        };

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

        public void UpdateName(string name)
        {
            this.Name = name.Replace("_", "-");

            if (Memory.MapName == "TarkovStreets")
            {
                this.Name = streetsExfilNames[this.Name];
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
