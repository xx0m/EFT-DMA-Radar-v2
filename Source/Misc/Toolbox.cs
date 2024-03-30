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
        private readonly Thread _workerThread;
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

        public Toolbox(ulong localGameWorld)
        {
            this._workerThread = new Thread((ThreadStart)delegate
            {
                while (Memory.LocalPlayer is null)
                {
                    Thread.Sleep(100);
                }

                while (IsSafeToWriteMemory())
                {
                    if (this._config.MasterSwitchEnabled)
                    {
                        this.ToolboxWorker();
                    }
                    Thread.Sleep(500);
                }

                Program.Log("LocalPlayer found, initializing toolbox");
            });

            this._workerThread.Priority = ThreadPriority.BelowNormal;
            this._workerThread.IsBackground = true;
            this._workerThread.Start();
        }

        private bool IsSafeToWriteMemory()
        {
            return Memory.InGame && Memory.LocalPlayer is not null;
        }

        private void ToolboxWorker()
        {
            this._playerManager.isADS = Memory.ReadValue<bool>(this._playerManager.proceduralWeaponAnimation + 0x1BD);

            // No Recoil
            this._playerManager.SetNoRecoil(this._config.NoRecoilEnabled);

            // Instant ADS
            this._playerManager.SetInstantADS(this._config.InstantADSEnabled);

            // No Sway
            if (this._config.NoSwayEnabled && !this.noSwayToggled)
            {
                this.noSwayToggled = true;
                this._playerManager.SetNoSway(true);
            }
            else if (!this._config.NoSwayEnabled && this.noSwayToggled)
            {
                this.noSwayToggled = false;
                this._playerManager.SetNoSway(false);
            }

            // Double Search
            if (this._config.DoubleSearchEnabled && !this.doubleSearchToggled)
            {
                this.doubleSearchToggled = true;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.SearchDouble);
            }
            else if (!this._config.DoubleSearchEnabled && this.doubleSearchToggled)
            {
                this.doubleSearchToggled = false;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.SearchDouble, true);
            }

            // Jump Power
            if (this._config.JumpPowerEnabled && !this.jumpPowerToggled)
            {
                this.jumpPowerToggled = true;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.JumpStrength);
            }
            else if (!this._config.JumpPowerEnabled && this.jumpPowerToggled)
            {
                this.jumpPowerToggled = false;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.JumpStrength, true);
            }

            // Throw Power
            if (this._config.ThrowPowerEnabled && !this.throwPowerToggled)
            {
                this.throwPowerToggled = true;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.ThrowStrength);
            }
            else if (!_config.ThrowPowerEnabled && this.throwPowerToggled)
            {
                this.throwPowerToggled = false;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.ThrowStrength, true);
            }

            // Increase Max Weight
            if (this._config.IncreaseMaxWeightEnabled && !this.juggernautToggled)
            {
                this.juggernautToggled = true;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.WeightStrength);
            }
            else if (!this._config.IncreaseMaxWeightEnabled && this.juggernautToggled)
            {
                this.juggernautToggled = false;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.WeightStrength, true);
            }

            // Mag Drills
            if (this._config.MagDrillsEnabled && !this.magDrillsToggled)
            {
                this.magDrillsToggled = true;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsLoad);
                this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsUnload);
            }
            else if (!this._config.MagDrillsEnabled && this.magDrillsToggled)
            {
                this.magDrillsToggled = false;
                this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsLoad, true);
                this._playerManager.SetMaxSkill(PlayerManager.Skills.MagDrillsUnload, true);
            }

            // Extended Reach
            if (this._config.ExtendedReachEnabled && !this.extendedReachToggled)
            {
                this.extendedReachToggled = true;
                Game.SetInteractDistance(true);
            }
            else if (!this._config.ExtendedReachEnabled && this.extendedReachToggled)
            {
                this.extendedReachToggled = false;
                Game.SetInteractDistance(false);
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
                    if (this._playerManager is not null && this._playerManager.isADS)
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
                    }
                    else
                    {
                        if (this._config.ThermalVisionEnabled && !this.thermalVisionToggled)
                        {
                            this.thermalVisionToggled = true;
                            this._cameraManager.ThermalVision(true);
                        }
                        else if (!this._config.ThermalVisionEnabled && this.thermalVisionToggled)
                        {
                            this.thermalVisionToggled = false;
                            this._cameraManager.ThermalVision(false);
                        }
                    }

                    // Night Vision
                    if (this._config.NightVisionEnabled && !this.nightVisionToggled)
                    {
                        this.nightVisionToggled = true;
                        this._cameraManager.NightVision(true);
                    }
                    else if (!this._config.NightVisionEnabled && this.nightVisionToggled)
                    {
                        this.nightVisionToggled = false;
                        this._cameraManager.NightVision(false);
                    }
                }
            }
        }
    }
}
