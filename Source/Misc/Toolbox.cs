using System.DirectoryServices;

namespace eft_dma_radar
{
    public class Toolbox
    {
        private Thread autoRefreshThread;
        private CancellationTokenSource autoRefreshCancellationTokenSource;

        private bool extendedReachToggled = false;
        private bool freezeTime = false;
        private float timeOfDay = -1;
        private bool infiniteStaminaToggled = false;
        
        private bool thermalVisionToggled = false;
        private bool nightVisionToggled = false;

        private Dictionary<string, bool> Skills = new Dictionary<string, bool>
        {
            ["Endurance"] = false,
            ["Strength"] = false,
            ["Vitality"] = false,
            ["Health"] = false,
            ["Stress Resistance"] = false,
            ["Metabolism"] = false,
            ["Perception"] = false,
            ["Intellect"] = false,
            ["Attention"] = false,
            ["MagDrills"] = false,
            ["Immunity"] = false,
            ["Throwables"] = false,
            ["Covert Movement"] = false,
            ["Search"] = false,
            ["Surgery"] = false,
            ["Light Vests"] = false,
            ["Heavy Vests"] = false
        };

        private Config _config { get => Program.Config; }
        private CameraManager _cameraManager { get => Memory.CameraManager; }
        private PlayerManager _playerManager { get => Memory.PlayerManager; }
        private Chams _chams { get => Memory.Chams; }

        private ulong TOD_Sky_static;
        private ulong TOD_Sky_cached_ptr;
        private ulong TOD_Sky_inst_ptr;
        private ulong TOD_Components;
        private ulong TOD_Time;
        private ulong GameDateTime;
        private ulong Cycle;
        private ulong WeatherController;
        private ulong WeatherControllerDebug;
        private ulong GameWorld;
        private ulong HardSettings;

        private bool ToolboxMonoInitialized = false;
        private bool ShouldInitializeToolboxMono => !this.ToolboxMonoInitialized && Memory.InGame && Memory.LocalPlayer is not null;

        public Toolbox()
        {
            if (this._config.MasterSwitch)
            {
                Task.Run(() =>
                {
                    while (this.ShouldInitializeToolboxMono)
                    {
                        this.InitiateMonoAddresses();
                        Thread.Sleep(5000);
                    }
                });

                this.StartToolbox();
            }
        }

        public void StartToolbox()
        {
            if (this.autoRefreshThread is not null && this.autoRefreshThread.IsAlive)
            {
                return;
            }

            this.autoRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = this.autoRefreshCancellationTokenSource.Token;

            this.autoRefreshThread = new Thread(() => this.ToolboxWorkerThread(cancellationToken))
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            this.autoRefreshThread.Start();
        }

        public async void StopToolbox()
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

        private void ToolboxWorkerThread(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && this.IsSafeToWriteMemory())
            {
                if (this._config.MasterSwitch)
                {
                    Task.Run(() =>
                    {
                        this.ToolboxWorker();
                    });
                    Thread.Sleep(250);
                }
            }
            Program.Log("[ToolBox] Refresh thread stopped.");
        }

        private bool IsSafeToWriteMemory()
        {
            return Memory.InGame && Memory.LocalPlayer is not null && this._playerManager is not null;
        }

        private void InitiateMonoAddresses()
        {
            if (this.ShouldInitializeToolboxMono)
            {
                try
                {
                    Program.Log("Trying to initialize Mono addresses...");
                    this.TOD_Sky_static = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "TOD_Sky");
                    this.TOD_Sky_cached_ptr = Memory.ReadValue<ulong>(this.TOD_Sky_static + 0x10);
                    this.TOD_Sky_inst_ptr = Memory.ReadValue<ulong>(this.TOD_Sky_cached_ptr + 0x20);
                    this.TOD_Components = Memory.ReadValue<ulong>(this.TOD_Sky_inst_ptr + 0x80);
                    this.TOD_Time = Memory.ReadValue<ulong>(this.TOD_Components + 0x140);
                    this.GameDateTime = Memory.ReadValue<ulong>(this.TOD_Time + 0x18);
                    this.Cycle = Memory.ReadValue<ulong>(this.TOD_Sky_inst_ptr + 0x18);

                    //this.WeatherController = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "EFT.Weather.WeatherController");
                    //this.WeatherControllerDebug = Memory.ReadPtr(this.WeatherController + 0x80);

                    this.HardSettings = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "EFTHardSettings");

                    this.ToolboxMonoInitialized = true;
                    Program.Log("[ToolBox] Initialized Mono addresses correctly!");
                }
                catch (Exception ex)
                {
                    Program.Log($"[ToolBox] - InitiateMonoAddresses ({ex.Message})\n{ex.StackTrace}");
                }
            }
            else
            {
                Program.Log($"[ToolBox] - InitiateMonoAddresses failed");
            }
        }

        private void ToolboxWorker()
        {
            try
            {
                var entries = new List<IScatterWriteEntry>();

                if (this._playerManager is not null)
                {
                    this._playerManager.isADS = Memory.ReadValue<bool>(this._playerManager.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.IsAiming);

                    // No Recoil / Sway
                    this._playerManager.SetNoRecoilSway(this._config.NoRecoilSway, ref entries);

                    // Instant ADS
                    this._playerManager.SetInstantADS(this._config.InstantADS, ref entries);

                    #region Skill Buffs
                    if (this._config.MaxSkills["Endurance"] != this.Skills["Endurance"])
                    {
                        this.Skills["Endurance"] = this._config.MaxSkills["Endurance"];
                        this._playerManager.SetMaxSkillByCategory("Endurance", !this.Skills["Endurance"], ref entries);
                    }

                    if (this._config.MaxSkills["Strength"] != this.Skills["Strength"])
                    {
                        this.Skills["Strength"] = this._config.MaxSkills["Strength"];
                        this._playerManager.SetMaxSkillByCategory("Strength", !this.Skills["Strength"], ref entries);
                    }

                    if (this._config.MaxSkills["Vitality"] != this.Skills["Vitality"])
                    {
                        this.Skills["Vitality"] = this._config.MaxSkills["Vitality"];
                        this._playerManager.SetMaxSkillByCategory("Vitality", !this.Skills["Vitality"], ref entries);
                    }

                    if (this._config.MaxSkills["Health"] != this.Skills["Health"])
                    {
                        this.Skills["Health"] = this._config.MaxSkills["Health"];
                        this._playerManager.SetMaxSkillByCategory("Health", !this.Skills["Health"], ref entries);
                    }

                    if (this._config.MaxSkills["Stress Resistance"] != this.Skills["Stress Resistance"])
                    {
                        this.Skills["Stress Resistance"] = this._config.MaxSkills["Stress Resistance"];
                        this._playerManager.SetMaxSkillByCategory("Stress Resistance", !this.Skills["Stress Resistance"], ref entries);
                    }

                    if (this._config.MaxSkills["Metabolism"] != this.Skills["Metabolism"])
                    {
                        this.Skills["Metabolism"] = this._config.MaxSkills["Metabolism"];
                        this._playerManager.SetMaxSkillByCategory("Metabolism", !this.Skills["Metabolism"], ref entries);
                    }

                    if (this._config.MaxSkills["Perception"] != this.Skills["Perception"])
                    {
                        this.Skills["Perception"] = this._config.MaxSkills["Perception"];
                        this._playerManager.SetMaxSkillByCategory("Perception", !this.Skills["Perception"], ref entries);
                    }

                    if (this._config.MaxSkills["Intellect"] != this.Skills["Intellect"])
                    {
                        this.Skills["Intellect"] = this._config.MaxSkills["Intellect"];
                        this._playerManager.SetMaxSkillByCategory("Intellect", !this.Skills["Intellect"], ref entries);
                    }

                    if (this._config.MaxSkills["Attention"] != this.Skills["Attention"])
                    {
                        this.Skills["Attention"] = this._config.MaxSkills["Attention"];
                        this._playerManager.SetMaxSkillByCategory("Attention", !this.Skills["Attention"], ref entries);
                    }

                    if (this._config.MaxSkills["Mag Drills"] != this.Skills["MagDrills"])
                    {
                        this.Skills["MagDrills"] = this._config.MaxSkills["Mag Drills"];
                        this._playerManager.SetMaxSkillByCategory("MagDrills", !this.Skills["MagDrills"], ref entries);
                    }

                    if (this._config.MaxSkills["Immunity"] != this.Skills["Immunity"])
                    {
                        this.Skills["Immunity"] = this._config.MaxSkills["Immunity"];
                        this._playerManager.SetMaxSkillByCategory("Immunity", !this.Skills["Immunity"], ref entries);
                    }

                    if (this._config.MaxSkills["Throwables"] != this.Skills["Throwables"])
                    {
                        this.Skills["Throwables"] = this._config.MaxSkills["Throwables"];
                        this._playerManager.SetMaxSkillByCategory("Throwables", !this.Skills["Throwables"], ref entries);
                    }

                    if (this._config.MaxSkills["Covert Movement"] != this.Skills["Covert Movement"])
                    {
                        this.Skills["Covert Movement"] = this._config.MaxSkills["Covert Movement"];
                        this._playerManager.SetMaxSkillByCategory("Covert Movement", !this.Skills["Covert Movement"], ref entries);
                    }

                    if (this._config.MaxSkills["Search"] != this.Skills["Search"])
                    {
                        this.Skills["Search"] = this._config.MaxSkills["Search"];
                        this._playerManager.SetMaxSkillByCategory("Search", !this.Skills["Search"], ref entries);
                    }

                    if (this._config.MaxSkills["Surgery"] != this.Skills["Surgery"])
                    {
                        this.Skills["Surgery"] = this._config.MaxSkills["Surgery"];
                        this._playerManager.SetMaxSkillByCategory("Surgery", !this.Skills["Surgery"], ref entries);
                    }

                    if (this._config.MaxSkills["Light Vests"] != this.Skills["Light Vests"])
                    {
                        this.Skills["Light Vests"] = this._config.MaxSkills["Light Vests"];
                        this._playerManager.SetMaxSkillByCategory("Light Vests", !this.Skills["Light Vests"], ref entries);
                    }

                    if (this._config.MaxSkills["Heavy Vests"] != this.Skills["Heavy Vests"])
                    {
                        this.Skills["Heavy Vests"] = this._config.MaxSkills["Heavy Vests"];
                        this._playerManager.SetMaxSkillByCategory("Heavy Vests", !this.Skills["Heavy Vests"], ref entries);
                    }
                    #endregion

                    // Infinite Stamina
                    if (this._config.InfiniteStamina)
                    {
                        this._playerManager.SetMovementState(true, ref entries);
                        this._playerManager.SetMaxStamina(ref entries);

                    }
                    else if (!this._config.InfiniteStamina)
                    {
                        this._playerManager.SetMovementState(false, ref entries);
                    }
                }

                // Mono stuff
                if (this.ToolboxMonoInitialized)
                {
                    // Extended Reach
                    if (this._config.ExtendedReach != this.extendedReachToggled)
                    {
                        this.extendedReachToggled = this._config.ExtendedReach;
                        this.SetInteractDistance(this.extendedReachToggled, ref entries);
                    }

                    // Lock time of day + set time of day
                    if (this._config.FreezeTimeOfDay != this.freezeTime)
                    {
                        this.freezeTime = this._config.FreezeTimeOfDay;
                        this.FreezeTime(this.freezeTime, ref entries);

                        if (!this.freezeTime && this.timeOfDay != -1)
                            this.timeOfDay = -1;
                    }

                    if (this._config.FreezeTimeOfDay && this.freezeTime && this._config.TimeOfDay != this.timeOfDay)
                    {
                        this.SetTimeOfDay(this._config.TimeOfDay, ref entries);
                    }
                }

                // Camera Stuff
                if (this._cameraManager is not null)
                {
                    if (!this._cameraManager.IsReady)
                    {
                        this._cameraManager.UpdateCamera();
                    }
                    else
                    {
                        // No Visor
                        this._cameraManager.VisorEffect(this._config.NoVisor, ref entries);

                        // Smart Thermal Vision
                        if (this._playerManager is null || !this._playerManager.isADS)
                        {
                            if (this._config.ThermalVision != thermalVisionToggled)
                            {
                                this.thermalVisionToggled = this._config.ThermalVision;
                                this._cameraManager.ThermalVision(this.thermalVisionToggled, ref entries);
                            }
                        }
                        else
                        {
                            if (this._config.OpticThermalVision)
                            {
                                if (this.thermalVisionToggled)
                                {
                                    this.thermalVisionToggled = false;
                                    this._cameraManager.ThermalVision(false, ref entries);
                                }

                                this._cameraManager.OpticThermalVision(true, ref entries);
                            }
                            else
                            {
                                this._cameraManager.OpticThermalVision(false, ref entries);
                            }
                        }

                        // Night Vision
                        if (this._config.NightVision != this.nightVisionToggled)
                        {
                            this.nightVisionToggled = this._config.NightVision;
                            this._cameraManager.NightVision(this.nightVisionToggled, ref entries);
                        }

                        // Chams
                        if (this._config.ChamsEnabled)
                        {
                            this._chams.ChamsEnable();
                        }
                        else if (!this._config.ChamsEnabled && this._chams?.PlayersWithChamsCount > 0)
                        {
                            this._chams?.ChamsDisable();
                        }
                    }
                }

                if (entries.Any())
                    Memory.WriteScatter(entries);
            }
            catch (Exception ex)
            {
                Program.Log($"[ToolBox] ToolboxWorker ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sets the maximum loot/door interaction distance
        /// </summary>
        /// <param name="enabled"></param>
        private void SetInteractDistance(bool on, ref List<IScatterWriteEntry> entries)
        {
            var currentLootRaycastDistance = Memory.ReadValue<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE);

            if (on && currentLootRaycastDistance != 1.8f)
            {
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.8f));
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1.8f));
            }
            else if (!on && currentLootRaycastDistance == 1.8f)
            {
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.3f));
                entries.Add(new ScatterWriteDataEntry<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1f));
            }
        }

        /// <summary>
        /// Locks the time of day so it can be set manually
        /// </summary>
        private void FreezeTime(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (state != !freezeTime)
            {
                freezeTime = state;
                entries.Add(new ScatterWriteDataEntry<bool>(this.TOD_Time + 0x68, freezeTime));
                entries.Add(new ScatterWriteDataEntry<bool>(this.GameDateTime, freezeTime));
            }
        }

        /// <summary>
        /// Manually sets the time of day
        /// </summary>
        /// <param name="time"></param>
        private void SetTimeOfDay(float time, ref List<IScatterWriteEntry> entries)
        {
            this.timeOfDay = time;
            entries.Add(new ScatterWriteDataEntry<float>(this.Cycle + 0x10, this.timeOfDay));
        }
    }
}
