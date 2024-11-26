using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace eft_dma_radar
{
    public class Chams
    {
        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }

        private ReadOnlyDictionary<string, Player> AllPlayers
        {
            get => Memory.Players;
        }

        private bool InGame
        {
            get => Memory.InGame;
        }

        private Config _config
        {
            get => Program.Config;
        }

        public int PlayersWithChamsCount
        {
            get => PlayersWithChams.Count;
        }

        public ulong NVGMaterial
        {
            get => _nvgMaterial;
        }

        public ulong ThermalMaterial
        {
            get => _thermalMaterial;
        }

        private Vector4 _color
        {
            get => Extensions.Vector4FromPaintColor("Chams");
        }

        private bool safeToWriteChams
        {
            get => (this.InGame && Memory.LocalPlayer is not null && Memory.LocalPlayer.IsActive && Memory.Players.Count > 1);
        }

        private Dictionary<string, Player> PlayersWithChams = new Dictionary<string, Player>();
        private Dictionary<string, List<PointerBackup>> pointerBackups = new Dictionary<string, List<PointerBackup>>();

        private ulong _nvgMaterial;
        private ulong _thermalMaterial;

        private Vector4 lastColor = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

        public void ChamsEnable()
        {
            if (!this.safeToWriteChams || Memory.IsExtracting)
                return;

            var nightVisionComponent = this._cameraManager.NVGComponent;
            var fpsThermal = this._cameraManager.ThermalComponent;

            if (nightVisionComponent == 0 || fpsThermal == 0)
            {
                Program.Log("Chams -> nvg or fps thermal component not found");
                Memory.CameraManager.UpdateCamera();
                return;
            }

            if (this._nvgMaterial == 0UL)
                this._nvgMaterial = Memory.ReadPtrChain(nightVisionComponent, new uint[] { 0x90, 0x10, 0x8 });

            if (this._thermalMaterial == 0UL)
                this._thermalMaterial = Memory.ReadPtrChain(fpsThermal, new uint[] { 0x90, 0x10, 0x8 });

            if (this._nvgMaterial == 0UL || this._thermalMaterial == 0UL)
            {
                Program.Log("Chams -> nvg or thermal material not found");
                return;
            }

            if (this.lastColor != this._color)
            {
                this.lastColor = this._color;
                Memory.WriteValue(nightVisionComponent + 0xD8, this.lastColor);
            }

            var players = this.AllPlayers
                            .Select(x => x.Value)
                            ?.Where(x =>
                                        !this.PlayersWithChams.ContainsKey(x.ProfileID) &&
                                        x.IsActive &&
                                        x.ErrorCount == 0 &&
                                        !x.IsLocalPlayer)
                            .Where(x =>
                                    (this._config.Chams["Corpses"] && !x.IsAlive) ||
                                    (this._config.Chams["PMCs"] && x.IsPMC && x.Type != PlayerType.Teammate && x.IsAlive) ||
                                    (this._config.Chams["Teammates"] && x.IsPMC && x.Type == PlayerType.Teammate && x.IsAlive) ||
                                    (this._config.Chams["PlayerScavs"] && x.Type == PlayerType.PlayerScav && x.IsAlive) ||
                                    (this._config.Chams["Bosses"] && x.Type == PlayerType.Boss && x.IsAlive) ||
                                    (this._config.Chams["Rogues"] && x.IsRogueRaider && x.IsAlive) ||
                                    (this._config.Chams["Event"] && x.IsEventAI && x.IsAlive) ||
                                    (this._config.Chams["Cultists"] && x.Type == PlayerType.Cultist && x.IsAlive) ||
                                    (this._config.Chams["Scavs"] && x.Type == PlayerType.Scav && x.IsAlive))
                            .ToList();

            if (players?.Count > 0 && Memory.LocalPlayer.IsAlive)
            {
                foreach (var player in players)
                {
                    try
                    {
                        if (!this.safeToWriteChams)
                        {
                            Program.Log("Chams -> not safe to write");
                            break;
                        }

                        var materialTouse = (player.IsHuman || player.Type == PlayerType.Boss) && player.IsAlive ? _nvgMaterial : _thermalMaterial;

                        if (this.PlayersWithChams.TryAdd(player.ProfileID, player))
                            this.SetPlayerBodyChams(player, materialTouse);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR -> ChamsEnable -> {ex.Message}\nStackTrace:{ex.StackTrace}");
                    }
                }
            }
        }

        public bool SetPlayerBodyChams(Player player, ulong material)
        {
            var setAnyMaterial = false;
            var entries = new List<IScatterWriteEntry>();

            if (Memory.IsExtracting)
                return false;

            // temp
            //if (player.Name != "Tagilla")
            //    return false;

            try
            {
                var BodySkins = Memory.ReadPtr(player.PlayerBody + 0x40);
                var BodySkinEntries = Memory.ReadPtr(BodySkins + 0x18);
                var bodySkinsCount = Memory.ReadValue<int>(BodySkins + 0x40);

                //if (bodySkinsCount < 1)
                //    return false;

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    try
                    {
                        var bodySkin = Memory.ReadPtr(BodySkinEntries + 0x30 + (0x18 * (uint)i));
                        var lodsArray = Memory.ReadPtr(bodySkin + 0x18);
                        var lodsCount = Memory.ReadValue<int>(lodsArray + 0x18);

                        //if (lodsCount < 1)
                        //    continue;

                        // temporary
                        //if (i != 1)
                        //    continue;

                        for (int j = 0; j < lodsCount; j++)
                        {
                            try
                            {
                                var lodEntry = Memory.ReadPtr(lodsArray + 0x20 + (0x8 * (uint)j));
                                var skinnedMeshRender = Memory.ReadPtr(lodEntry + 0x20);

                                try //  Diz.Skinning.Skin
                                {
                                    var _rootBonePath = Memory.ReadPtr(lodEntry + 0x30);
                                    var rootbonePath = Memory.ReadUnityString(_rootBonePath);
                                }
                                catch // EFT.Visual.TorsoSkin or EFT.Visual.CustomSkin`1
                                {
                                    try
                                    {
                                        skinnedMeshRender = Memory.ReadPtr(skinnedMeshRender + 0x20);
                                    }
                                    catch {
                                        continue;
                                    }
                                }

                                var materialDictionary = Memory.ReadPtr(skinnedMeshRender + 0x10);
                                var materialCount = Memory.ReadValue<int>(materialDictionary + 0x158);

                                if (materialCount < 1 && materialCount > 5)
                                    continue;

                                var materialDictionaryBase = Memory.ReadPtr(materialDictionary + 0x148);

                                for (int k = 0; k < materialCount; k++)
                                {
                                    try
                                    {
                                        var materialPtr = Memory.ReadPtr(materialDictionaryBase + (0x4 * (uint)k));

                                        if (materialPtr == material)
                                        {
                                            setAnyMaterial = true;
                                            continue;
                                        }

                                        if (material > 0)
                                        {
                                            if (!_config.Chams["AlternateMethod"])
                                                this.SavePointer(materialDictionaryBase + (0x4 * (uint)k), materialPtr, player);
                                            else
                                                this.SavePointer(materialDictionaryBase + (sizeof(uint) * (uint)k), materialPtr, player);

                                            entries.Add(new ScatterWriteDataEntry<ulong>(materialDictionaryBase + (0x4 * (uint)k), material));
                                            setAnyMaterial = true;
                                        }
                                    }
                                    catch { Console.WriteLine("Failed to read material"); }
                                }
                            }
                            catch {}
                        }
                    }
                    catch {}
                }
            }
            catch {}

            if (!setAnyMaterial)
                this.PlayersWithChams.Remove(player.ProfileID);
            else
            {
                if (entries.Any())
                    Memory.WriteScatter(entries);
            }

            return setAnyMaterial;
        }

        public bool TriggerUnityCrash(Player player, ulong material)
        {
            var setAnyMaterial = false;

            try
            {
                var count = 1;
                var scatterReadMap = new ScatterReadMap(count);
                var map1Round1 = scatterReadMap.AddRound();
                var map1Round2 = scatterReadMap.AddRound();

                var bodySkinsPtr = map1Round1.AddEntry<ulong>(0, 0, player.PlayerBody, null, 0x40);
                var skinEntriesPtr = map1Round2.AddEntry<ulong>(0, 1, bodySkinsPtr, null, 0x18);
                var bodySkinsCountPtr = map1Round2.AddEntry<int>(0, 2, bodySkinsPtr, null, 0x40);

                scatterReadMap.Execute();

                if (!scatterReadMap.Results[0][0].TryGetResult<ulong>(out var bodySkins))
                    return false;
                if (!scatterReadMap.Results[0][1].TryGetResult<ulong>(out var skinEntries))
                    return false;
                if (!scatterReadMap.Results[0][2].TryGetResult<int>(out var bodySkinsCount))
                    return false;

                var scatterReadMap2 = new ScatterReadMap(bodySkinsCount);
                var map2Round1 = scatterReadMap2.AddRound();
                var map2Round2 = scatterReadMap2.AddRound();
                var map2Round3 = scatterReadMap2.AddRound();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    var pBodySkinsPtr = map2Round1.AddEntry<ulong>(i, 0, skinEntries, null, 0x30 + (0x18 * (uint)i));
                    var pLodsArrayPtr = map2Round2.AddEntry<ulong>(i, 1, pBodySkinsPtr, null, 0x18);
                    var lodsCountPtr = map2Round3.AddEntry<int>(i, 2, pLodsArrayPtr, null, 0x18);
                }

                scatterReadMap2.Execute();

                for (int i = 0; i < bodySkinsCount; i++)
                {
                    if (!scatterReadMap2.Results[i][1].TryGetResult<ulong>(out var pLodsArray))
                        continue;
                    if (!scatterReadMap2.Results[i][2].TryGetResult<int>(out var lodsCount))
                        continue;

                    var scatterReadMap3 = new ScatterReadMap(lodsCount);
                    var map3Round1 = scatterReadMap3.AddRound();
                    var map3Round2 = scatterReadMap3.AddRound();
                    var map3Round3 = scatterReadMap3.AddRound();
                    var map3Round4 = scatterReadMap3.AddRound();

                    if (lodsCount > 4)
                        continue;

                    for (int j = 0; j < lodsCount; j++)
                    {
                        var pLodEntryPtr = map3Round1.AddEntry<ulong>(j, 0, pLodsArray, null, 0x20 + (0x8 * (uint)j));

                        var skinnedMeshRendererPtr = map3Round2.AddEntry<ulong>(j, 1, pLodEntryPtr, null, 0x20);
                        var pMaterialDictionaryPtr = map3Round3.AddEntry<ulong>(j, 2, skinnedMeshRendererPtr, null, 0x10);

                        var materialCountPtr = map3Round4.AddEntry<int>(j, 3, pMaterialDictionaryPtr, null, 0x158);
                        var materialDictionaryBasePtr = map3Round4.AddEntry<ulong>(j, 4, pMaterialDictionaryPtr, null, 0x148);
                    }

                    scatterReadMap3.Execute();

                    for (int j = 0; j < lodsCount; j++)
                    {
                        if (!scatterReadMap3.Results[j][0].TryGetResult<ulong>(out var pLodEntry))
                            continue;
                        if (!scatterReadMap3.Results[j][2].TryGetResult<ulong>(out var pMaterialDictionary))
                            continue;
                        if (!scatterReadMap3.Results[j][3].TryGetResult<int>(out var materialCount))
                            continue;
                        if (!scatterReadMap3.Results[j][4].TryGetResult<ulong>(out var materialDictionaryBase))
                            continue;

                        if (j == 1)
                            pLodEntry = Memory.ReadPtr(pLodEntry + 0x20);

                        if (pLodEntry == 0)
                            continue;

                        if (materialCount > 0 && materialCount < 3)
                        {
                            var scatterReadMap4 = new ScatterReadMap(materialCount);
                            var map4Round1 = scatterReadMap4.AddRound();

                            for (int k = 0; k < materialCount; k++)
                            {
                                var pMaterialPtr = map4Round1.AddEntry<ulong>(k, 0, materialDictionaryBase, null, (0x50 * (uint)k));
                            }

                            scatterReadMap4.Execute();

                            for (int k = 0; k < materialCount; k++)
                            {
                                if (!scatterReadMap4.Results[k][0].TryGetResult<ulong>(out var pMaterial))
                                    continue;

                                if (pMaterial == 0 || material == 0)
                                    continue;

                                if (pMaterial == material)
                                {
                                    setAnyMaterial = true;
                                    break;
                                }

                                if (!safeToWriteChams)
                                    break;

                                if (!_config.Chams["AlternateMethod"])
                                    SavePointer(materialDictionaryBase + (0x50 * (uint)k), pMaterial, player);
                                else
                                    SavePointer(materialDictionaryBase + (sizeof(uint) * (uint)k), pMaterial, player);

                                Memory.WriteValue(materialDictionaryBase + (sizeof(uint) * (uint)k), material);
                                setAnyMaterial = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"SetPlayerBodyChams -> {ex.Message}\n{ex.StackTrace}");
            }

            return setAnyMaterial;
        }

        public void ChamsDisable()
        {
            if (!this.safeToWriteChams)
                this.RemovePointers();
            else
                this.RestorePointers();
        }

        public void SoftChamsDisable()
        {
            if (this.PlayersWithChams.Count == 0)
            {
                Program.Log("No players with chams enabled.");
                return;
            }

            this.SoftRestorePointers();
        }

        private void SavePointer(ulong address, ulong originalValue, Player player)
        {
            var key = player.ProfileID;

            if (!this.pointerBackups.ContainsKey(key))
                this.pointerBackups[key] = new List<PointerBackup>();

            var existingBackup = this.pointerBackups[key].FirstOrDefault(b => b.Address == address);

            if (existingBackup.Address == 0)
                this.pointerBackups[key].Add(new PointerBackup { Address = address, OriginalValue = originalValue });
        }

        public void RestorePointers()
        {
            if (this.pointerBackups.Count < 1)
                return;

            var entries = new List<IScatterWriteEntry>();

            foreach (var pointerBackup in this.pointerBackups.Values)
            {
                foreach (var backup in pointerBackup)
                {
                    if (this.safeToWriteChams)
                        entries.Add(new ScatterWriteDataEntry<ulong>(backup.Address, backup.OriginalValue));
                };
            }

            if (entries.Any())
                Memory.WriteScatter(entries);

            this.RemovePointers();
        }

        public void SoftRestorePointers()
        {
            var entries = new List<IScatterWriteEntry>();

            foreach (var pointerBackup in this.pointerBackups.Values)
            {
                foreach (var backup in pointerBackup)
                {
                    if (this.safeToWriteChams)
                        entries.Add(new ScatterWriteDataEntry<ulong>(backup.Address, backup.OriginalValue));
                }
            }

            if (entries.Any())
                Memory.WriteScatter(entries);

            this.PlayersWithChams.Clear();
        }

        public void RestorePointersForPlayer(Player player)
        {
            var key = player.ProfileID;

            if (!this.pointerBackups.ContainsKey(key))
                return;
            
            var entries = new List<IScatterWriteEntry>();

            foreach (var pointerBackup in this.pointerBackups[key])
            {
                if (this.safeToWriteChams && player.IsActive)
                    entries.Add(new ScatterWriteDataEntry<ulong>(pointerBackup.Address, pointerBackup.OriginalValue));
            };

            if (entries.Any())
                Memory.WriteScatter(entries);
        }

        public void RemovePointersForPlayer(Player player)
        {
            var key = player.ProfileID;

            if (this.pointerBackups.Remove(key))
                Program.Log($"Cleaned pointer backups for {player.Name}");

            if (this.PlayersWithChams.Remove(key))
                Program.Log($"Removed {player.Name} from players w/ chams");

        }

        public void RemovePointers()
        {
            this.pointerBackups.Clear();
            this.PlayersWithChams.Clear();
        }
    }

    public struct PointerBackup
    {
        public ulong Address;
        public ulong OriginalValue;
    }
}