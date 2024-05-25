using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class CorpseManager
    {
        private readonly Stopwatch _sw = new();
        private ulong _corpseDictionary;
        private ulong? _DictionaryBase = null;
        public ulong localGameWorld;
        public ReadOnlyCollection<PlayerCorpse> Corpses { get; private set; }
        private ReadOnlyDictionary<string, Player> AllPlayers
        {
            get => Memory.Players;
        }
        public CorpseManager(ulong localGameWorld)
        {
            this.localGameWorld = localGameWorld;
            this._corpseDictionary = Memory.ReadPtr(this.localGameWorld + 0xE0);
            this._sw.Start();
        }

        public void Refresh()
        {
            if (this._sw.ElapsedMilliseconds < 500) return;
            this._sw.Restart();

            try
            {
                var count = this.CorpseCount;
                var corpses = new List<PlayerCorpse>();
                //Console.WriteLine($"[CorpseManager] - CorpseCount: {count}");
                if (count > 0)
                {
                    this._DictionaryBase ??= Memory.ReadPtr(this._corpseDictionary + Offsets.UnityDictionary.Base);

                    var scatterReadMap = new ScatterReadMap(count);
                    var round1 = scatterReadMap.AddRound();

                    for (int i = 0; i < count; i++)
                    {
                        var corpseBase = round1.AddEntry<ulong>(i, 0, this._DictionaryBase + 0x30 + (0x18 * (uint)i), null);
                    }

                    scatterReadMap.Execute();

                    Parallel.For(0, count, Program.Config.ParallelOptions, i =>
                    {
                        if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var corpseBase))
                        {
                            return;
                        }
                        //Console.WriteLine($"[CorpseManager] - CorpseBase: {corpseBase}");
                        //[40] ItemOwner : -.GClass257D
                        //[60] ItemId : String
                        //[68] TemplateId : String
                        //[70] ValidProfiles : System.String[]
                        //[B0] item_0xB0 : EFT.InventoryLogic.Item
                        //[128] PlayerBody : EFT.PlayerBody
                        //[130] ItemInHands : -.GClass31BE<Item>
                        //[140] PlayerProfileID : String
                        //var corpseItemOwnerPtr = Memory.ReadPtr(corpseBase + 0x40);
                        //var corpseItemIdPtr = Memory.ReadPtr(corpseBase + 0x60);
                        //var corpseTemplateIdPtr = Memory.ReadPtr(corpseBase + 0x68);
                        //var corpseItemPtr = Memory.ReadPtr(corpseBase + 0xB0);
                        //var corpsePlayerBodyPtr = Memory.ReadPtr(corpseBase + 0x128);
                        //var corpseItemInHandsPtr = Memory.ReadPtr(corpseBase + 0x130);
                        try
                        {
                            var corpsePlayerProfileIDPtr = Memory.ReadPtr(corpseBase + 0x140);
                            var corpsePlayerProfileID = Memory.ReadUnityString(corpsePlayerProfileIDPtr);
                            var posAddr = Memory.ReadPtrChain(corpseBase, Offsets.GameObject.To_TransformInternal);
                            var position = new Transform(posAddr, false).GetPosition();
                            //Console.WriteLine($"[CorpseManager] - CorpsePosition: {position}");
                            corpses.Add(new PlayerCorpse
                            {
                                ProfileID = corpsePlayerProfileID,
                                Position = position
                            });

                            var players = this.AllPlayers
                                ?.Select(x => x.Value)
                                .Where(x => x.Type is not PlayerType.LocalPlayer && x.IsAlive && !x.HasExfild);
                            if (players != null)
                            {
                                foreach (var player in players)
                                {
                                    if (player.ProfileID == corpsePlayerProfileID)
                                    {
                                        //Console.WriteLine($"[CorpseManager] - Found Player: {player.Name}");
                                        //Console.WriteLine($"[CorpseManager] - If you see this message, it means that the player is dead. but it's still in active list");
                                        //remove player from active list
                                        player.IsAlive = false;

                                        if (Program.Config.ChamsEnabled)
                                        {
                                            
                                           Task.Run(async () =>
                                           {
                                                await Memory.Chams.RestorePointersForPlayerAsync(player);

                                                Memory.Chams.SetPlayerBodyChams(player, Memory.Chams.ThermalMaterial);
                                           });                                            
                                        }

                                        continue;
                                    }
                                }
                            }
                        }
                        catch { }

                    });
                }
                this.Corpses = new ReadOnlyCollection<PlayerCorpse>(corpses);
            }
            catch { }
        }

        private int CorpseCount
        {
            get
            {
                try
                {
                    var count = Memory.ReadValue<int>(this._corpseDictionary + Offsets.UnityDictionary.Count);
                    if (count < 0 || count > 128)
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
    }

    public class PlayerCorpse
    {
        public string ProfileID { get; set; }
        public Vector3 Position { get; set; }
    }
}