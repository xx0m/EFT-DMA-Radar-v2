using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;

namespace eft_dma_radar.Source.Tarkov
{
    public class Toolbox
    {
        private Thread _workerThread;
        private bool nightVisionToggled = false;
        private bool thermalVisionToggled = false;
        private bool doubleSearchToggled = false;
        private bool jumpPowerToggled = false;
        private bool throwPowerToggled = false;
        private bool juggernautToggled = false;
        private bool magDrillsToggled = false;
        private bool extendedReachToggled = false;
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

        public Toolbox()
        {
            if (this._config.MasterSwitchEnabled)
            {
                this.StartToolbox();
            }
        }

        public void StartToolbox()
        {
            if (this._workerThread != null && this._workerThread.IsAlive)
            {
                return;
            }

            this._workerThread = new Thread(this.ToolboxWorkerThread)
            {
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            this._workerThread.Start();
        }

        public void StopToolbox()
        {
            this._workerThread?.Join(); // Wait for the thread to finish
            this._workerThread = null;
        }

        private void ToolboxWorkerThread()
        {
            while (this.IsSafeToWriteMemory())
            {
                if (this._config.MasterSwitchEnabled)
                {
                    this.ToolboxWorker();
                }
                Thread.Sleep(250);
            }
        }

        private bool IsSafeToWriteMemory()
        {
            return Memory.InGame && Memory.LocalPlayer != null && this._playerManager != null;
        }

        private void ToolboxWorker()
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

            // Extended Reach
            if (this._config.ExtendedReachEnabled != this.extendedReachToggled)
            {
                this.extendedReachToggled = this._config.ExtendedReachEnabled;
                Game.SetInteractDistance(this.extendedReachToggled);
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
                }
            }
        }
    }
}
