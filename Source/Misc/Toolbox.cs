namespace eft_dma_radar
{
    public class Toolbox
    {
        private Thread autoRefreshThread;
        private CancellationTokenSource autoRefreshCancellationTokenSource;

        private bool nightVisionToggled = false;
        private bool thermalVisionToggled = false;
        private bool doubleSearchToggled = false;
        private bool jumpPowerToggled = false;
        private bool throwPowerToggled = false;
        private bool juggernautToggled = false;
        private bool magDrillsToggled = false;
        private bool extendedReachToggled = false;
        private bool timeLocked = false;
        private float timeOfDay = -1;
        private bool searchSpeedToggled = false;
        private bool noSwayToggled = false;

        private Config _config
        {
            get => Program.Config;
        }
        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }
        private PlayerManager _playerManager
        {
            get => Memory.PlayerManager;
        }
        private Chams _chams
        {
            get => Memory.Chams;
        }

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

        public Toolbox()
        {
            if (this._config.MasterSwitchEnabled)
            {
                this.StartToolbox();
            }

            Task.Run(() =>
            {
                int num = 0;
                while (!this.ToolboxMonoInitialized)
                {
                    num++;

                    this.InitiateMonoAddresses();
                    Thread.Sleep(5000);
                }
            });
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
                if (this._config.MasterSwitchEnabled)
                {
                    Task.Run(() => { this.ToolboxWorker(); });
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
            if (!this.ToolboxMonoInitialized && Memory.InGame && Memory.LocalPlayer is not null)
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
                if (this._playerManager is not null)
                {
                    this._playerManager.isADS = Memory.ReadValue<bool>(this._playerManager.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.IsAiming);

                    // No Recoil / Sway
                    this._playerManager.SetNoRecoilSway(this._config.NoRecoilSwayEnabled);

                    // Instant ADS
                    this._playerManager.SetInstantADS(this._config.InstantADSEnabled);

                    // Double Search
                    if (this._config.DoubleSearchEnabled != this.doubleSearchToggled)
                    {
                        doubleSearchToggled = this._config.DoubleSearchEnabled;
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.SearchDouble, !this.doubleSearchToggled);
                    }

                    // Jump Power
                    if (this._config.JumpPowerEnabled != this.jumpPowerToggled)
                    {
                        this.jumpPowerToggled = this._config.JumpPowerEnabled;
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.JumpStrength, !this.jumpPowerToggled);
                    }

                    // Throw Power
                    if (this._config.ThrowPowerEnabled != this.throwPowerToggled)
                    {
                        this.throwPowerToggled = this._config.ThrowPowerEnabled;
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.ThrowStrength, !this.throwPowerToggled);
                    }

                    // Increase Max Weight
                    if (this._config.IncreaseMaxWeightEnabled != this.juggernautToggled)
                    {
                        this.juggernautToggled = this._config.IncreaseMaxWeightEnabled;
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.WeightStrength, !this.juggernautToggled);
                    }

                    // Mag Drills
                    if (this._config.MagDrillsEnabled != this.magDrillsToggled)
                    {
                        this.magDrillsToggled = this._config.MagDrillsEnabled;
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsLoad, !this.magDrillsToggled);
                        this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsUnload, !this.magDrillsToggled);
                    }

                    // Infinite Stamina
                    if (this._config.InfiniteStaminaEnabled)
                    {
                        this._playerManager.SetMovementState(true);
                        this._playerManager.SetMaxStamina();

                    }
                    else if (!this._config.InfiniteStaminaEnabled)
                    {
                        this._playerManager.SetMovementState(false);
                    }

                    if (this._config.SearchSpeedEnabled != this.searchSpeedToggled)
                    {
                        this.searchSpeedToggled = this._config.SearchSpeedEnabled;
                        //this._playerManager.SetMaxSkill(PlayerManager.Skills.SearchSpeed, !this.searchSpeedToggled);
                        //this._playerManager.SetMaxSkill(PlayerManager.Skills.AttentionSearchSpeed, !this.searchSpeedToggled);
                        //this._playerManager.SetMaxSkill(PlayerManager.Skills.AttentionExamine, !this.searchSpeedToggled);
                    }
                }

                // Mono stuff
                if (this.ToolboxMonoInitialized)
                {
                    // Extended Reach
                    if (this._config.ExtendedReachEnabled != this.extendedReachToggled)
                    {
                        this.extendedReachToggled = this._config.ExtendedReachEnabled;
                        this.SetInteractDistance(this.extendedReachToggled);
                    }

                    // Lock time of day + set time of day
                    if (this._config.LockTimeOfDay != this.timeLocked)
                    {
                        this.timeLocked = this._config.LockTimeOfDay;
                        this.LockTime(this.timeLocked);

                        if (!this.timeLocked && this.timeOfDay != -1)
                            this.timeOfDay = -1;
                    }

                    if (this._config.LockTimeOfDay && this.timeLocked && this._config.TimeOfDay != this.timeOfDay)
                    {
                        this.SetTimeOfDay(this._config.TimeOfDay);
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
                        this._cameraManager.VisorEffect(this._config.NoVisorEnabled);

                        // Smart Thermal Vision
                        if (this._playerManager is null || !this._playerManager.isADS)
                        {
                            if (this._config.ThermalVisionEnabled != thermalVisionToggled)
                            {
                                this.thermalVisionToggled = this._config.ThermalVisionEnabled;
                                this._cameraManager.ThermalVision(this.thermalVisionToggled);
                            }
                        }
                        else
                        {
                            if (this._config.OpticThermalVisionEnabled)
                            {
                                if (this.thermalVisionToggled)
                                {
                                    this.thermalVisionToggled = false;
                                    this._cameraManager.ThermalVision(false);
                                }

                                this._cameraManager.OpticThermalVision(true);
                            }
                            else
                            {
                                this._cameraManager.OpticThermalVision(false);
                            }
                        }

                        // Night Vision
                        if (this._config.NightVisionEnabled != this.nightVisionToggled)
                        {
                            this.nightVisionToggled = this._config.NightVisionEnabled;
                            this._cameraManager.NightVision(this.nightVisionToggled);
                        }

                        // Chams
                        if (this._config.ChamsEnabled)
                        {
                            this._chams.ChamsEnable();
                        }
                        else if (!this._config.ChamsEnabled && this._chams.PlayersWithChamsCount > 0)
                        {
                            this._chams.ChamsDisable();
                        }
                    }
                }
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
        private void SetInteractDistance(bool on)
        {
            var currentLootRaycastDistance = Memory.ReadValue<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE);

            if (on && currentLootRaycastDistance != 1.8f)
            {
                Memory.WriteValue<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.8f);
                Memory.WriteValue<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1.8f);
            }
            else if (!on && currentLootRaycastDistance == 1.8f)
            {
                Memory.WriteValue<float>(this.HardSettings + Offsets.EFTHardSettings.LOOT_RAYCAST_DISTANCE, 1.3f);
                Memory.WriteValue<float>(this.HardSettings + Offsets.EFTHardSettings.DOOR_RAYCAST_DISTANCE, 1f);
            }
        }

        /// <summary>
        /// Locks the time of day so it can be set manually
        /// </summary>
        private void LockTime(bool state)
        {
            if (state != !timeLocked)
            {
                timeLocked = state;
                Memory.WriteValue(this.TOD_Time + 0x68, timeLocked);
                Memory.WriteValue(this.GameDateTime, timeLocked);
            }
        }

        /// <summary>
        /// Manually sets the time of day
        /// </summary>
        /// <param name="time"></param>
        private void SetTimeOfDay(float time)
        {
            this.timeOfDay = time;
            Memory.WriteValue(this.Cycle + 0x10, this.timeOfDay);
        }
    }
}
