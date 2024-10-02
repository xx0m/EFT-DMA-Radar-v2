using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class TripwireManager
    {
        private readonly Stopwatch _sw = new();
        private ulong _tripwireList;
        private ulong? _listBase = null;
        private int TripwireCount
        {
            get
            {
                try
                {
                    var count = Memory.ReadValue<int>(this._tripwireList + Offsets.UnityList.Count);

                    if (count < 0 || count > 32)
                        return 0;

                    return count;
                }
                catch
                {
                    return 0;
                }
            }
        }
        /// <summary>
        /// List of tripwires in Local Game World.
        /// </summary>
        public List<Tripwire> Tripwires { get; private set; }

        public TripwireManager(ulong localGameWorld)
        {
            var tripwireManager = Memory.ReadPtrChain(localGameWorld, [Offsets.LocalGameWorld.ToTripwireManager, Offsets.ToTripwireManager.TripwireManager]);
            this._tripwireList = Memory.ReadPtr(tripwireManager + Offsets.Tripwires.List);
            this._sw.Start();
        }

        /// <summary>
        /// Check for tripwires in LocalGameWorld.
        /// </summary>
        public void Refresh()
        {
            if (this._sw.ElapsedMilliseconds < 5000)
                return;

            this._sw.Restart();

            try
            {
                var count = this.TripwireCount;
                var tripwires = new List<Tripwire>();

                if (count > 0)
                {
                    if (this._listBase is null)
                        this._listBase = Memory.ReadPtr(this._tripwireList + Offsets.UnityList.Base);

                    var scatterReadMap = new ScatterReadMap(count);
                    var round1 = scatterReadMap.AddRound();
                    var round2 = scatterReadMap.AddRound();

                    for (int i = 0; i < count; i++)
                    {
                        var tripwireAddr = round1.AddEntry<ulong>(i, 0, this._listBase, null, Offsets.UnityListBase.Start + ((uint)i * 0x8));

                        var state = round2.AddEntry<TripwireState>(i, 1, tripwireAddr, null, Offsets.TripwireSynchronizableObject.TripwireState);
                        var fromPos = round2.AddEntry<Vector3>(i, 2, tripwireAddr, null, Offsets.TripwireSynchronizableObject.FromPosition);
                        var toPos = round2.AddEntry<Vector3>(i, 3, tripwireAddr, null, Offsets.TripwireSynchronizableObject.ToPosition);
                    }

                    scatterReadMap.Execute();

                    for (int i = 0; i < count; i++)
                    {
                        if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var tripwireAddr))
                            continue;
                        if (!scatterReadMap.Results[i][1].TryGetResult<TripwireState>(out var state))
                            continue;
                        if (!(state == TripwireState.Wait || state == TripwireState.Active))
                            continue;
                        if (!scatterReadMap.Results[i][2].TryGetResult<Vector3>(out var fromPos))
                            continue;
                        if (!scatterReadMap.Results[i][3].TryGetResult<Vector3>(out var toPos))
                            continue;

                        tripwires.Add(new Tripwire(fromPos, toPos));
                    };
                }
                else if (this._listBase is not null)
                {
                    this._listBase = null;
                }

                this.Tripwires = new List<Tripwire>(tripwires);
            }
            catch { }
        }
    }

    /// <summary>
    /// Represents a tripwire in Local Game World.
    /// </summary>
    public readonly struct Tripwire
    {
        public Vector3 FromPos { get; }
        public Vector3 ToPos { get; }

        public Tripwire(Vector3 fromPos, Vector3 toPos)
        {
            FromPos = new Vector3(fromPos.X, fromPos.Z, fromPos.Y);
            ToPos = new Vector3(toPos.X, toPos.Z, toPos.Y);
        }
    }

    public enum TripwireState
    {
        None,
        Wait,
        Active,
        Exploding,
        Exploded,
        Inert
    }
}
