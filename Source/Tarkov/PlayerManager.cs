using OpenTK.Graphics.ES11;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eft_dma_radar.Source.Tarkov
{

    /// <summary>
    /// Class to manage local player write operations
    /// </summary>
    public class PlayerManager
    {
        private ulong playerBase { get; set; }
        private ulong playerProfile { get; set; }

        private ulong movementContext { get; set; }
        private ulong baseMovementState { get; set; }
        private ulong physical { get; set; }
        private ulong stamina { get; set; }
        private ulong handsStamina { get; set; }

        private ulong skillsManager { get; set; }
        private ulong magDrillsLoad { get; set; }
        private ulong magDrillsUnload { get; set; }
        private ulong jumpStrength { get; set; }
        private ulong weightStrength { get; set; }
        private ulong throwStrength { get; set; }
        private ulong searchDouble { get; set; }

        public ulong proceduralWeaponAnimation { get; set; }
        private ulong breathEffector { get; set; }
        private ulong walkEffector { get; set; }
        private ulong motionEffector { get; set; }
        private ulong forceEffector { get; set; }

        public bool isADS { get; set; }

        private Config _config { get => Program.Config; }
        public Dictionary<string, float> OriginalValues { get; }
        /// <summary>
        /// Stores the different skills that can be modified
        /// </summary>
        public enum Skills
        {
            MagDrillsLoad,
            MagDrillsUnload,
            JumpStrength,
            ThrowStrength,
            WeightStrength,
            SearchDouble,
            ADS
        }

        /// <summary>
        /// Creates new PlayerManager object
        /// </summary>
        public PlayerManager(ulong localGameWorld)
        {
            this.playerBase = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.MainPlayer);
            this.playerProfile = Memory.ReadPtr(playerBase + Offsets.Player.Profile);

            this.movementContext = Memory.ReadPtr(playerBase + Offsets.Player.MovementContext);
            this.baseMovementState = Memory.ReadPtr(movementContext + Offsets.MovementContext.BaseMovementState);

            this.physical = Memory.ReadPtr(playerBase + Offsets.Player.Physical);
            this.stamina = Memory.ReadPtr(physical + Offsets.Physical.Stamina);
            this.handsStamina = Memory.ReadPtr(this.physical + Offsets.Physical.HandsStamina);

            this.skillsManager = Memory.ReadPtr(playerProfile + Offsets.Profile.SkillManager);
            this.magDrillsLoad = Memory.ReadPtr(skillsManager + Offsets.SkillManager.MagDrillsLoadSpeed);
            this.magDrillsUnload = Memory.ReadPtr(skillsManager + Offsets.SkillManager.MagDrillsUnloadSpeed);
            this.jumpStrength = Memory.ReadPtr(skillsManager + Offsets.SkillManager.JumpStrength);
            this.weightStrength = Memory.ReadPtr(skillsManager + Offsets.SkillManager.WeightStrength);
            this.throwStrength = Memory.ReadPtr(skillsManager + Offsets.SkillManager.ThrowStrength);
            this.searchDouble = Memory.ReadPtr(skillsManager + Offsets.SkillManager.SearchDouble);

            this.proceduralWeaponAnimation = Memory.ReadPtr(playerBase + Offsets.Player.ProceduralWeaponAnimation);
            this.isADS = Memory.ReadValue<bool>(proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.IsAiming);
            this.breathEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Breath);
            this.walkEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Walk);
            this.motionEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.MotionReact);
            this.forceEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.ForceReact);

            this.OriginalValues = new Dictionary<string, float>()
            {
                ["MagDrillsLoad"] = -1,
                ["MagDrillsUnload"] = -1,
                ["JumpStrength"] = -1,
                ["WeightStrength"] = -1,
                ["ThrowStrength"] = -1,
                ["SearchDouble"] = -1,
                ["Mask"] = 125,
                ["AimingSpeed"] = 1,
                ["AimingSpeedSway"] = 0.2f,
                ["StaminaCapacity"] = -1,
                ["HandStaminaCapacity"] = -1,

                ["BreathEffectorIntensity"] = -1,
                ["WalkEffectorIntensity"] = -1,
                ["MotionEffectorIntensity"] = -1,
                ["ForceEffectorIntensity"] = -1,
            };
        }

        /// <summary>
        /// Enables / disables weapon recoil
        /// </summary>
        public void SetNoRecoilSway(bool on)
        {
            var mask = Memory.ReadValue<int>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask);

            if (on && mask != 0)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask, 0);
            }
            else if (!on && mask == 0)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask, (int)this.OriginalValues["Mask"]);
            }
        }

        /// <summary>
        /// Enables / disables weapon sway
        /// </summary>
        public void SetNoSway(bool on)
        {

            if (this.OriginalValues["BreathEffectorIntensity"] == -1)
            {
                this.OriginalValues["BreathEffectorIntensity"] = Memory.ReadValue<float>(this.breathEffector + Offsets.BreathEffector.Intensity);
                this.OriginalValues["WalkEffectorIntensity"] = Memory.ReadValue<float>(this.walkEffector + Offsets.WalkEffector.Intensity);
                this.OriginalValues["MotionEffectorIntensity"] = Memory.ReadValue<float>(this.motionEffector + Offsets.MotionEffector.Intensity);
                this.OriginalValues["ForceEffectorIntensity"] = Memory.ReadValue<float>(this.forceEffector + Offsets.ForceEffector.Intensity);
            }

            Memory.WriteValue<float>(this.breathEffector + Offsets.BreathEffector.Intensity, on ? 0f : this.OriginalValues["BreathEffectorIntensity"]);
            Memory.WriteValue<float>(this.walkEffector + Offsets.WalkEffector.Intensity, on ? 0f : this.OriginalValues["WalkEffectorIntensity"]);
            Memory.WriteValue<float>(this.motionEffector + Offsets.MotionEffector.Intensity, on ? 0f : this.OriginalValues["MotionEffectorIntensity"]);
            Memory.WriteValue<float>(this.forceEffector + Offsets.ForceEffector.Intensity, on ? 0f : this.OriginalValues["ForceEffectorIntensity"]);
        }

        /// <summary>
        /// Enables / disables instant ads, changes per weapon
        /// </summary>
        public void SetInstantADS(bool on)
        {
            var aimingSpeed = Memory.ReadValue<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed);

            if (on && aimingSpeed != 7)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, 7f);
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, 0f);
            } else if (!on && aimingSpeed != 1) {
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, (float)this.OriginalValues["AimingSpeed"]);
                Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, (float)this.OriginalValues["AimingSpeedSway"]);
            }
        }

        /// <summary>
        /// Modifies the players skill buffs
        /// </summary>
        public void SetMaxSkill(Skills skill, bool revert = false)
        {
            try
            {
                switch (skill)
                {
                    case Skills.MagDrillsLoad:
                        {
                            if (this.OriginalValues["MagDrillsLoad"] == -1)
                            {
                                this.OriginalValues["MagDrillsLoad"] = Memory.ReadValue<float>(this.magDrillsLoad + Offsets.SkillFloat.Value);
                            }
                            Memory.WriteValue<float>(this.magDrillsLoad + Offsets.SkillFloat.Value, (revert ? this.OriginalValues["MagDrillsLoad"] : this._config.MagDrillSpeed * 10));
                            break;
                        }
                    case Skills.MagDrillsUnload:
                        {
                            if (this.OriginalValues["MagDrillsUnload"] == -1)
                            {
                                this.OriginalValues["MagDrillsUnload"] = Memory.ReadValue<float>(this.magDrillsUnload + Offsets.SkillFloat.Value);
                            }
                            Memory.WriteValue<float>(this.magDrillsUnload + Offsets.SkillFloat.Value, (revert ? this.OriginalValues["MagDrillsUnload"] : this._config.MagDrillSpeed * 10));
                            break;
                        }
                    case Skills.JumpStrength:
                        {
                            if (this.OriginalValues["JumpStrength"] == -1)
                            {
                                this.OriginalValues["JumpStrength"] = Memory.ReadValue<float>(this.jumpStrength + Offsets.SkillFloat.Value);
                            }
                            Memory.WriteValue<float>(this.jumpStrength + Offsets.SkillFloat.Value, (revert ? this.OriginalValues["JumpStrength"] : this._config.JumpPowerStrength / 10));
                            break;
                        }
                    case Skills.WeightStrength:
                        {
                            if (this.OriginalValues["WeightStrength"] == -1)
                            {
                                this.OriginalValues["WeightStrength"] = Memory.ReadValue<float>(this.weightStrength + Offsets.SkillFloat.Value);
                            }

                            Memory.WriteValue<float>(this.weightStrength + Offsets.SkillFloat.Value, (revert ? this.OriginalValues["WeightStrength"] : 0.5f));
                            break;
                        }
                    case Skills.ThrowStrength:
                        {
                            if (this.OriginalValues["ThrowStrength"] == -1)
                            {
                                this.OriginalValues["ThrowStrength"] = Memory.ReadValue<float>(this.throwStrength + Offsets.SkillFloat.Value);
                            }

                            // value between 0.035f & 1f
                            Memory.WriteValue<float>(this.throwStrength + Offsets.SkillFloat.Value, (revert ? this.OriginalValues["ThrowStrength"] : this._config.ThrowPowerStrength / 10));
                            break;
                        }
                    case Skills.SearchDouble:
                        {
                            Memory.WriteValue<bool>(this.searchDouble + Offsets.SkillFloat.Value, this._config.DoubleSearchEnabled);
                            break;
                        }

                }
            }
            catch (Exception e)
            {
                throw new Exception($"ERROR Setting Max Skill: #{skill}");
            }
        }

        /// <summary>
        /// Changes movement state
        /// </summary>
        public void SetMovementState(bool on)
        {
            this.baseMovementState = Memory.ReadPtr(this.movementContext + Offsets.MovementContext.BaseMovementState);
            var animationState = Memory.ReadValue<byte>(this.baseMovementState + Offsets.BaseMovementState.Name);

            if (on && animationState == 5)
            {
                Memory.WriteValue<byte>(this.baseMovementState + Offsets.BaseMovementState.Name, 6);
            }
            else if (!on && animationState == 6)
            {
                Memory.WriteValue<byte>(this.baseMovementState + Offsets.BaseMovementState.Name, 5);
            }
        }

        /// <summary>
        /// Sets maximum stamina / hand stamina
        /// </summary>
        public void SetMaxStamina()
        {
            if (this.OriginalValues["StaminaCapacity"] == -1)
            {
                this.OriginalValues["StaminaCapacity"] = Memory.ReadValue<float>(this.physical + Offsets.Physical.StaminaCapacity);
                this.OriginalValues["HandStaminaCapacity"] = Memory.ReadValue<float>(this.physical + Offsets.Physical.HandsCapacity);
            }

            Memory.WriteValue<float>(this.stamina + Offsets.Stamina.Current, (float)this.OriginalValues["StaminaCapacity"]);
            Memory.WriteValue<float>(this.handsStamina + Offsets.Stamina.Current, (float)this.OriginalValues["HandStaminaCapacity"]);
        }
    }
}