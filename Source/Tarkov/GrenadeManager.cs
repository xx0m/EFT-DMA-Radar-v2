using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class GrenadeManager
    {
        private readonly Stopwatch _sw = new();
        private ulong _grenadeList;
        private ulong? _listBase = null;
        private int GrenadeCount
        {
            get
            {
                try
                {
                    var count = Memory.ReadValue<int>(this._grenadeList + Offsets.UnityList.Count);
                    if (count < 0 || count > 32)
                    {
                        return 0;
                    }
                    return count;
                }
                catch
                {
                    return 0;
                }
            }
        }
        /// <summary>
        /// List of "Hot" grenades in Local Game World.
        /// </summary>
        public ReadOnlyCollection<Grenade> Grenades { get; private set; }

        public GrenadeManager(ulong localGameWorld)
        {
            var grenadesPtr = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.Grenades);
            this._grenadeList = Memory.ReadPtr(grenadesPtr + Offsets.Grenades.List);
            this._sw.Start();
        }

        /// <summary>
        /// Check for "hot" grenades in LocalGameWorld if due.
        /// </summary>
        public void Refresh()
        {
            if (this._sw.ElapsedMilliseconds < 100) return;
            this._sw.Restart();

            try
            {
                var count = this.GrenadeCount;
                var grenades = new List<Grenade>();

                if (count > 0)
                {
                    if (this._listBase is null)
                    {
                        this._listBase = Memory.ReadPtr(this._grenadeList + Offsets.UnityList.Base);
                    }

                    var scatterReadMap = new ScatterReadMap(count);
                    var round1 = scatterReadMap.AddRound();

                    for (int i = 0; i < count; i++)
                    {
                        var grenadeAddr = round1.AddEntry<ulong>(i, 0, this._listBase, null, Offsets.UnityListBase.Start + ((uint)i * 0x08));
                    }

                    scatterReadMap.Execute();

                    for (int i = 0; i < count; i++)
                    {
                        if (scatterReadMap.Results[i][0].TryGetResult<ulong>(out var grenadeAddr))
                            grenades.Add(new Grenade(grenadeAddr));
                    };
                }
                else if (this._listBase is not null)
                {
                    this._listBase = null;
                }

                this.Grenades = new ReadOnlyCollection<Grenade>(grenades);
            }
            catch { }
        }
    }

    /// <summary>
    /// Represents a 'Hot' grenade in Local Game World.
    /// </summary>
    public class Grenade
    {
        /// <summary>
        /// Unity Position of grenade in Local Game World.
        /// </summary>
        public Vector3 Position { get; }
        public Grenade(ulong baseAddr)
        {
            try
            {
                var posAddr = Memory.ReadPtrChain(baseAddr, Offsets.GameObject.To_TransformInternal);
                this.Position = new Transform(posAddr, false).GetPosition();
            }
            catch (Exception ex)
            {
                Program.Log($"GrenadeManager - Failed to get grenade position => {ex.Message}\nStackTrace:{ex.StackTrace}");
            }
        }
    }
}
