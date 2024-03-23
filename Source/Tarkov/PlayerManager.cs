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
            this.playerProfile = Memory.ReadPtr(playerBase + 0x588);

            this.movementContext = Memory.ReadPtr(playerBase + Offsets.Player.MovementContext);
            this.baseMovementState = Memory.ReadPtr(movementContext + 0xD0);

            this.physical = Memory.ReadPtr(playerBase + 0x598);
            this.stamina = Memory.ReadPtr(physical + 0x38);
            this.handsStamina = Memory.ReadPtr(this.physical + 0x40);

            this.skillsManager = Memory.ReadPtr(playerProfile + 0x60);
            this.magDrillsLoad = Memory.ReadPtr(skillsManager + 0x180);
            this.magDrillsUnload = Memory.ReadPtr(skillsManager + 0x188);
            this.jumpStrength = Memory.ReadPtr(skillsManager + 0x60);
            this.weightStrength = Memory.ReadPtr(skillsManager + 0x50);
            this.throwStrength = Memory.ReadPtr(skillsManager + 0x70);
            this.searchDouble = Memory.ReadPtr(skillsManager + 0x4C0);

            this.proceduralWeaponAnimation = Memory.ReadPtr(playerBase + 0x1A0);
            this.isADS = Memory.ReadValue<bool>(proceduralWeaponAnimation + 0x1BD);
            this.breathEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + 0x28);
            this.walkEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + 0x30);
            this.motionEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + 0x38);
            this.forceEffector = Memory.ReadPtr(this.proceduralWeaponAnimation + 0x40);

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
        public void SetNoRecoil(bool on)
        {
            var mask = Memory.ReadValue<int>(this.proceduralWeaponAnimation + 0x138);

            if (on && mask != 1)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x138, 1);
            }
            else if (!on && mask == 1)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x138, (int)this.OriginalValues["Mask"]);
            }
        }

        /// <summary>
        /// Enables / disables weapon sway
        /// </summary>
        public void SetNoSway(bool on)
        {

            if (this.OriginalValues["BreathEffectorIntensity"] == -1)
            {
                this.OriginalValues["BreathEffectorIntensity"] = Memory.ReadValue<float>(this.breathEffector + 0xA4);
                this.OriginalValues["WalkEffectorIntensity"] = Memory.ReadValue<float>(this.walkEffector + 0x44);
                this.OriginalValues["MotionEffectorIntensity"] = Memory.ReadValue<float>(this.motionEffector + 0xD0);
                this.OriginalValues["ForceEffectorIntensity"] = Memory.ReadValue<float>(this.forceEffector + 0x30);
            }

            Memory.WriteValue<float>(this.breathEffector + 0xA4, on ? 0f : this.OriginalValues["BreathEffectorIntensity"]);
            Memory.WriteValue<float>(this.walkEffector + 0x44, on ? 0f : this.OriginalValues["WalkEffectorIntensity"]);
            Memory.WriteValue<float>(this.motionEffector + 0xD0, on ? 0f : this.OriginalValues["MotionEffectorIntensity"]);
            Memory.WriteValue<float>(this.forceEffector + 0x30, on ? 0f : this.OriginalValues["ForceEffectorIntensity"]);
        }

        /// <summary>
        /// Enables / disables instant ads, changes per weapon
        /// </summary>
        public void SetInstantADS(bool on)
        {
            var aimingSpeed = Memory.ReadValue<float>(this.proceduralWeaponAnimation + 0x1DC);

            if (on && aimingSpeed != 7)
            {
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x1DC, 7f);
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x2A4, 0f);
            } else if (!on && aimingSpeed != 1) {
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x1DC, (float)this.OriginalValues["AimingSpeed"]);
                Memory.WriteValue(this.proceduralWeaponAnimation + 0x2A4, (float)this.OriginalValues["AimingSpeedSway"]);
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
                                this.OriginalValues["MagDrillsLoad"] = Memory.ReadValue<float>(this.magDrillsLoad + 0x30);
                            }
                            Memory.WriteValue<float>(this.magDrillsLoad + 0x30, (revert ? this.OriginalValues["MagDrillsLoad"] : _config.MagDrillSpeed * 10));
                            break;
                        }
                    case Skills.MagDrillsUnload:
                        {
                            if (this.OriginalValues["MagDrillsUnload"] == -1)
                            {
                                this.OriginalValues["MagDrillsUnload"] = Memory.ReadValue<float>(this.magDrillsUnload + 0x30);
                            }
                            Memory.WriteValue<float>(this.magDrillsUnload + 0x30, (revert ? this.OriginalValues["MagDrillsUnload"] : _config.MagDrillSpeed * 10));
                            break;
                        }
                    case Skills.JumpStrength:
                        {
                            if (this.OriginalValues["JumpStrength"] == -1)
                            {
                                this.OriginalValues["JumpStrength"] = Memory.ReadValue<float>(this.jumpStrength + 0x30);
                            }
                            Memory.WriteValue<float>(this.jumpStrength + 0x30, (revert ? this.OriginalValues["JumpStrength"] : _config.JumpPowerStrength / 10));
                            break;
                        }
                    case Skills.WeightStrength:
                        {
                            if (this.OriginalValues["WeightStrength"] == -1)
                            {
                                this.OriginalValues["WeightStrength"] = Memory.ReadValue<float>(this.weightStrength + 0x30);
                            }

                            Memory.WriteValue<float>(this.weightStrength + 0x30, (revert ? this.OriginalValues["WeightStrength"] : 0.5f));
                            break;
                        }
                    case Skills.ThrowStrength:
                        {
                            if (this.OriginalValues["ThrowStrength"] == -1)
                            {
                                this.OriginalValues["ThrowStrength"] = Memory.ReadValue<float>(this.throwStrength + 0x30);
                            }

                            // value between 0.035f & 1f
                            Memory.WriteValue<float>(this.throwStrength + 0x30, (revert ? this.OriginalValues["ThrowStrength"] : _config.ThrowPowerStrength / 10));
                            break;
                        }
                    case Skills.SearchDouble:
                        {
                            Memory.WriteValue<bool>(this.searchDouble + 0x30, _config.DoubleSearchEnabled);
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
            var animationState = Memory.ReadValue<byte>(this.baseMovementState + 0x21);

            if (on && animationState == 5)
            {
                Memory.WriteValue<byte>(this.baseMovementState + 0x21, 6);
            }
            else if (!on && animationState == 6)
            {
                Memory.WriteValue<byte>(this.baseMovementState + 0x21, 5);
            }
        }

        /// <summary>
        /// Sets maximum stamina / hand stamina
        /// </summary>
        public void SetMaxStamina()
        {
            if (this.OriginalValues["StaminaCapacity"] == -1)
            {
                this.OriginalValues["StaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC0);
                this.OriginalValues["HandStaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC8);
            }

            Memory.WriteValue<float>(this.stamina + 0x48, (float)this.OriginalValues["StaminaCapacity"]);
            Memory.WriteValue<float>(this.handsStamina + 0x48, (float)this.OriginalValues["HandStaminaCapacity"]);
        }
    }
}