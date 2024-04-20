namespace eft_dma_radar
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
        private ulong searchDouble { get; set; }

        private ulong magDrillsInventoryCheckAccuracy { get; set; } // 0x198
        private ulong magDrillsInventoryCheckSpeed { get; set; } // 0x190
        private ulong magDrillsInstantCheck { get; set; } // 0x1A0
        private ulong magDrillsLoadProgression { get; set; } // 0x1A8
        private ulong enduranceBreathElite { get; set; } // 0x48
        private ulong enduranceBuffBreathTimeInc { get; set; } // 0x38
        private ulong enduranceBuffEnduranceInc { get; set; } // 0x20
        private ulong enduranceBuffJumpCostRed { get; set; } // 0x30
        private ulong enduranceBuffRestoration { get; set; } // 0x40 //limit unknown
        private ulong enduranceHands { get; set; } // 0x28
        private ulong strengthBuffAimFatigue { get; set; } // 0x68
        private ulong strengthBuffElite { get; set; } // 0x80
        private ulong strengthBuffJumpHeightInc { get; set; } // 0x60
        private ulong strengthBuffLiftWeightInc { get; set; } // 0x50
        private ulong strengthBuffMeleeCrits { get; set; } // 0x88
        private ulong strengthBuffMeleePowerInc { get; set; } // 0x78
        private ulong strengthBuffSprintSpeedInc { get; set; } // 0x58
        private ulong strengthBuffThrowDistanceInc { get; set; } // 0x70
        private ulong throwingStrengthBuff { get; set; } // 0x320
        private ulong throwingEliteBuff { get; set; } // 0x330
        private ulong vitalityBuffBleedStop { get; set; } // 0xA8
        private ulong vitalityBuffRegeneration { get; set; } // 0xA0
        private ulong vitalityBuffSurviobilityInc { get; set; } // 0x98
        private ulong searchBuffSpeed { get; set; } // 0x4B8
        private ulong metabolismMiscDebuffTime { get; set; } // 0x108
        private ulong metabolismEliteBuffNoDyhydration { get; set; } // 0x110
        private ulong attentionEliteLuckySearch { get; set; } // 0x170
        private ulong healthBreakChanceRed { get; set; } // 0xB0
        private ulong healthEliteAbsorbDamage { get; set; } // 0xD0
        private ulong healthEnergy { get; set; } // 0xC0
        private ulong surgerySpeed { get; set; } // 0x4D0
        private ulong stressBerserk { get; set; } // 0xF0
        private ulong stressPain { get; set; } // 0xE0
        private ulong drawElite { get; set; } // 0x348
        private ulong drawSpeed { get; set; } // 0x338
        private ulong drawSound { get; set; } // 0x340
        private ulong covertMovementSpeed { get; set; } // 0x488
        private ulong covertMovementSoundVolume { get; set; } // 0x478
        private ulong covertMovementLoud { get; set; } // 0x498
        private ulong covertMovementEquipment { get; set; } // 0x480
        private ulong covertMovementElite { get; set; } // 0x490
        private ulong perceptionHearing { get; set; } // 0x118
        private ulong perceptionLootDot { get; set; } // 0x120

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
            ADS,
            MagDrillsInventoryCheckAccuracy,
            MagDrillsInventoryCheckSpeed,
            MagDrillsInstantCheck,
            MagDrillsLoadProgression,
            EnduranceBreathElite,
            EnduranceBuffBreathTimeInc,
            EnduranceBuffEnduranceInc,
            EnduranceBuffJumpCostRed,
            EnduranceBuffRestoration,
            EnduranceHands,
            StrengthBuffAimFatigue,
            StrengthBuffElite,
            StrengthBuffJumpHeightInc,
            StrengthBuffLiftWeightInc,
            StrengthBuffMeleeCrits,
            StrengthBuffMeleePowerInc,
            StrengthBuffSprintSpeedInc,
            StrengthBuffThrowDistanceInc,
            ThrowingStrengthBuff,
            ThrowingEliteBuff,
            VitalityBuffBleedStop,
            VitalityBuffRegeneration,
            VitalityBuffSurviobilityInc,
            SearchBuffSpeed,
            MetabolismMiscDebuffTime,
            MetabolismEliteBuffNoDyhydration,
            AttentionEliteLuckySearch,
            HealthBreakChanceRed,
            HealthEliteAbsorbDamage,
            HealthEnergy,
            SurgerySpeed,
            StressBerserk,
            StressPain,
            DrawElite,
            DrawSpeed,
            DrawSound,
            CovertMovementSpeed,
            CovertMovementSoundVolume,
            CovertMovementLoud,
            CovertMovementEquipment,
            CovertMovementElite,
            PerceptionHearing,
            PerceptionLootDot
        }

        /// <summary>
        /// Creates new PlayerManager object
        /// </summary>
        public PlayerManager(ulong localGameWorld)
        {
            this.playerBase = Memory.ReadPtr(localGameWorld + Offsets.LocalGameWorld.MainPlayer);
            this.playerProfile = Memory.ReadPtr(this.playerBase + Offsets.Player.Profile);

            this.movementContext = Memory.ReadPtr(this.playerBase + Offsets.Player.MovementContext);
            this.baseMovementState = Memory.ReadPtr(this.movementContext + Offsets.MovementContext.BaseMovementState);

            this.physical = Memory.ReadPtr(this.playerBase + Offsets.Player.Physical);
            this.stamina = Memory.ReadPtr(this.physical + Offsets.Physical.Stamina);
            this.handsStamina = Memory.ReadPtr(this.physical + Offsets.Physical.HandsStamina);

            this.skillsManager = Memory.ReadPtr(this.playerProfile + Offsets.Profile.SkillManager);
            this.magDrillsLoad = Memory.ReadPtr(this.skillsManager + Offsets.SkillManager.MagDrillsLoadSpeed);
            this.magDrillsUnload = Memory.ReadPtr(this.skillsManager + Offsets.SkillManager.MagDrillsUnloadSpeed);
            this.searchDouble = Memory.ReadPtr(this.skillsManager + 0x4C0);
            this.magDrillsInventoryCheckAccuracy = Memory.ReadPtr(this.skillsManager + 0x198);
            this.magDrillsInventoryCheckSpeed = Memory.ReadPtr(this.skillsManager + 0x190);
            this.magDrillsInstantCheck = Memory.ReadPtr(this.skillsManager + 0x1A0);
            this.magDrillsLoadProgression = Memory.ReadPtr(this.skillsManager + 0x1A8);
            this.enduranceBreathElite = Memory.ReadPtr(this.skillsManager + 0x48);
            this.enduranceBuffBreathTimeInc = Memory.ReadPtr(this.skillsManager + 0x38);
            this.enduranceBuffEnduranceInc = Memory.ReadPtr(this.skillsManager + 0x20);
            this.enduranceBuffJumpCostRed = Memory.ReadPtr(this.skillsManager + 0x30);
            this.enduranceBuffRestoration = Memory.ReadPtr(this.skillsManager + 0x40);
            this.enduranceHands = Memory.ReadPtr(this.skillsManager + 0x28);
            this.strengthBuffAimFatigue = Memory.ReadPtr(this.skillsManager + 0x68);
            this.strengthBuffElite = Memory.ReadPtr(this.skillsManager + 0x80);
            this.strengthBuffJumpHeightInc = Memory.ReadPtr(this.skillsManager + 0x60);
            this.strengthBuffLiftWeightInc = Memory.ReadPtr(this.skillsManager + 0x50);
            this.strengthBuffMeleeCrits = Memory.ReadPtr(this.skillsManager + 0x88);
            this.strengthBuffMeleePowerInc = Memory.ReadPtr(this.skillsManager + 0x78);
            this.strengthBuffSprintSpeedInc = Memory.ReadPtr(this.skillsManager + 0x58);
            this.strengthBuffThrowDistanceInc = Memory.ReadPtr(this.skillsManager + 0x70);
            this.throwingStrengthBuff = Memory.ReadPtr(this.skillsManager + 0x320);
            this.throwingEliteBuff = Memory.ReadPtr(this.skillsManager + 0x330);
            this.vitalityBuffBleedStop = Memory.ReadPtr(this.skillsManager + 0xA8);
            this.vitalityBuffRegeneration = Memory.ReadPtr(this.skillsManager + 0xA0);
            this.vitalityBuffSurviobilityInc = Memory.ReadPtr(this.skillsManager + 0x98);
            this.searchBuffSpeed = Memory.ReadPtr(this.skillsManager + 0x4B8);
            this.metabolismMiscDebuffTime = Memory.ReadPtr(this.skillsManager + 0x108);
            this.metabolismEliteBuffNoDyhydration = Memory.ReadPtr(this.skillsManager + 0x110);
            this.attentionEliteLuckySearch = Memory.ReadPtr(this.skillsManager + 0x170);
            this.healthBreakChanceRed = Memory.ReadPtr(this.skillsManager + 0xB0);
            this.healthEliteAbsorbDamage = Memory.ReadPtr(this.skillsManager + 0xD0);
            this.healthEnergy = Memory.ReadPtr(this.skillsManager + 0xC0);
            this.surgerySpeed = Memory.ReadPtr(this.skillsManager + 0x4D0);
            this.stressBerserk = Memory.ReadPtr(this.skillsManager + 0xF0);
            this.stressPain = Memory.ReadPtr(this.skillsManager + 0xE0);
            this.drawElite = Memory.ReadPtr(this.skillsManager + 0x348);
            this.drawSpeed = Memory.ReadPtr(this.skillsManager + 0x338);
            this.drawSound = Memory.ReadPtr(this.skillsManager + 0x340);
            this.covertMovementSpeed = Memory.ReadPtr(this.skillsManager + 0x488);
            this.covertMovementSoundVolume = Memory.ReadPtr(this.skillsManager + 0x478);
            this.covertMovementLoud = Memory.ReadPtr(this.skillsManager + 0x498);
            this.covertMovementEquipment = Memory.ReadPtr(this.skillsManager + 0x480);
            this.covertMovementElite = Memory.ReadPtr(this.skillsManager + 0x490);
            this.perceptionHearing = Memory.ReadPtr(this.skillsManager + 0x118);
            this.perceptionLootDot = Memory.ReadPtr(this.skillsManager + 0x120);

            this.proceduralWeaponAnimation = Memory.ReadPtr(this.playerBase + 0x1C0);
            this.isADS = Memory.ReadValue<bool>(this.proceduralWeaponAnimation + 0x1BD);
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

                ["MagDrillsInventoryCheckAccuracy"] = -1,
                ["MagDrillsInventoryCheckSpeed"] = -1,
                ["MagDrillsInstantCheck"] = -1,
                ["MagDrillsLoadProgression"] = -1,
                ["EnduranceBreathElite"] = -1,
                ["EnduranceBuffBreathTimeInc"] = -1,
                ["EnduranceBuffEnduranceInc"] = -1,
                ["EnduranceBuffJumpCostRed"] = -1,
                ["EnduranceBuffRestoration"] = -1,
                ["EnduranceHands"] = -1,
                ["StrengthBuffAimFatigue"] = -1,
                ["StrengthBuffElite"] = -1,
                ["StrengthBuffJumpHeightInc"] = -1,
                ["StrengthBuffLiftWeightInc"] = -1,
                ["StrengthBuffMeleeCrits"] = -1,
                ["StrengthBuffMeleePowerInc"] = -1,
                ["StrengthBuffSprintSpeedInc"] = -1,
                ["StrengthBuffThrowDistanceInc"] = -1,
                ["ThrowingStrengthBuff"] = -1,
                ["ThrowingEliteBuff"] = -1,
                ["VitalityBuffBleedStop"] = -1,
                ["VitalityBuffRegeneration"] = -1,
                ["VitalityBuffSurviobilityInc"] = -1,
                ["SearchBuffSpeed"] = -1,
                ["MetabolismMiscDebuffTime"] = -1,
                ["MetabolismEliteBuffNoDyhydration"] = -1,
                ["AttentionEliteLuckySearch"] = -1,
                ["HealthBreakChanceRed"] = -1,
                ["HealthEliteAbsorbDamage"] = -1,
                ["HealthEnergy"] = -1,
                ["SurgerySpeed"] = -1,
                ["StressBerserk"] = -1,
                ["StressPain"] = -1,
                ["DrawElite"] = -1,
                ["DrawSpeed"] = -1,
                ["DrawSound"] = -1,
                ["CovertMovementSpeed"] = -1,
                ["CovertMovementSoundVolume"] = -1,
                ["CovertMovementLoud"] = -1,
                ["CovertMovementEquipment"] = -1,
                ["CovertMovementElite"] = -1,
                ["PerceptionHearing"] = -1,
                ["PerceptionLootDot"] = -1
            };
        }

        /// <summary>
        /// Enables / disables weapon recoil
        /// </summary>
        public void SetNoRecoilSway(bool on)
        {
            try
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
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetNoRecoilSway ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Enables / disables weapon recoil
        /// </summary>
        // public void SetNoRecoil(bool on)
        // {
        //     var mask = Memory.ReadValue<int>(this.proceduralWeaponAnimation + 0x138);

        //     if (on && mask != 1)
        //     {
        //         Memory.WriteValue(this.proceduralWeaponAnimation + 0x138, 1);
        //     }
        //     else if (!on && mask == 1)
        //     {
        //         Memory.WriteValue(this.proceduralWeaponAnimation + 0x138, (int)this.OriginalValues["Mask"]);
        //     }
        // }

        /// <summary>
        /// Enables / disables weapon sway
        /// </summary>
        // public void SetNoSway(bool on)
        // {

        //     if (this.OriginalValues["BreathEffectorIntensity"] == -1)
        //     {
        //         this.OriginalValues["BreathEffectorIntensity"] = Memory.ReadValue<float>(this.breathEffector + 0xA4);
        //         this.OriginalValues["WalkEffectorIntensity"] = Memory.ReadValue<float>(this.walkEffector + 0x44);
        //         this.OriginalValues["MotionEffectorIntensity"] = Memory.ReadValue<float>(this.motionEffector + 0xD0);
        //         this.OriginalValues["ForceEffectorIntensity"] = Memory.ReadValue<float>(this.forceEffector + 0x30);
        //     }

        //     Memory.WriteValue<float>(this.breathEffector + 0xA4, on ? 0f : this.OriginalValues["BreathEffectorIntensity"]);
        //     Memory.WriteValue<float>(this.walkEffector + 0x44, on ? 0f : this.OriginalValues["WalkEffectorIntensity"]);
        //     Memory.WriteValue<float>(this.motionEffector + 0xD0, on ? 0f : this.OriginalValues["MotionEffectorIntensity"]);
        //     Memory.WriteValue<float>(this.forceEffector + 0x30, on ? 0f : this.OriginalValues["ForceEffectorIntensity"]);
        // }

        /// <summary>
        /// Enables / disables instant ads, changes per weapon
        /// </summary>
        public void SetInstantADS(bool on)
        {
            try
            {
                var aimingSpeed = Memory.ReadValue<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed);

                if (on && aimingSpeed != 7)
                {
                    Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, 7f);
                    Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, 0f);
                }
                else if (!on && aimingSpeed != 1)
                {
                    Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, (float)this.OriginalValues["AimingSpeed"]);
                    Memory.WriteValue(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, (float)this.OriginalValues["AimingSpeedSway"]);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetInstantADS ({ex.Message})\n{ex.StackTrace}");
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
                            Memory.WriteValue<float>(this.magDrillsLoad + 0x30, revert ? this.OriginalValues["MagDrillsLoad"] : 30f);
                            break;
                        }
                    case Skills.MagDrillsUnload:
                        {
                            if (this.OriginalValues["MagDrillsUnload"] == -1)
                            {
                                this.OriginalValues["MagDrillsUnload"] = Memory.ReadValue<float>(this.magDrillsUnload + 0x30);
                            }
                            Memory.WriteValue<float>(this.magDrillsUnload + 0x30, revert ? this.OriginalValues["MagDrillsUnload"] : 30f);
                            break;
                        }

                    case Skills.SearchDouble:
                        {
                            Memory.WriteValue<bool>(this.searchDouble + 0x30, _config.DoubleSearchEnabled);
                            break;
                        }
                    case Skills.MagDrillsInventoryCheckAccuracy:
                        {
                            if (this.OriginalValues["MagDrillsInventoryCheckAccuracy"] == -1)
                            {
                                this.OriginalValues["MagDrillsInventoryCheckAccuracy"] = Memory.ReadValue<float>(this.magDrillsInventoryCheckAccuracy + 0x30);
                            }
                            Memory.WriteValue<float>(this.magDrillsInventoryCheckAccuracy + 0x30, revert ? this.OriginalValues["MagDrillsInventoryCheckAccuracy"] : 2f);
                            break;
                        }
                    case Skills.MagDrillsInventoryCheckSpeed:
                        {
                            if (this.OriginalValues["MagDrillsInventoryCheckSpeed"] == -1)
                            {
                                this.OriginalValues["MagDrillsInventoryCheckSpeed"] = Memory.ReadValue<float>(this.magDrillsInventoryCheckSpeed + 0x30);
                            }
                            Memory.WriteValue<float>(this.magDrillsInventoryCheckSpeed + 0x30, revert ? this.OriginalValues["MagDrillsInventoryCheckSpeed"] : 40f);
                            break;
                        }
                    case Skills.MagDrillsInstantCheck:
                        {

                            Memory.WriteValue<bool>(this.magDrillsInstantCheck + 0x30, true);
                            break;
                        }
                    case Skills.MagDrillsLoadProgression:
                        {
                            if (this.OriginalValues["MagDrillsLoadProgression"] == -1)
                            {
                                this.OriginalValues["MagDrillsLoadProgression"] = Memory.ReadValue<float>(this.magDrillsLoadProgression + 0x30);
                            }
                            //Memory.WriteValue<float>(this.magDrillsLoadProgression + 0x30, revert ? this.OriginalValues["MagDrillsLoadProgression"] : 30f);
                            break;
                        }
                    case Skills.EnduranceBreathElite:
                        {
                            if (this.OriginalValues["EnduranceBreathElite"] == -1)
                            {
                                this.OriginalValues["EnduranceBreathElite"] = Memory.ReadValue<float>(this.enduranceBreathElite + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceBreathElite + 0x30, revert ? this.OriginalValues["EnduranceBreathElite"] : 1f);
                            break;
                        }
                    case Skills.EnduranceBuffBreathTimeInc:
                        {
                            if (this.OriginalValues["EnduranceBuffBreathTimeInc"] == -1)
                            {
                                this.OriginalValues["EnduranceBuffBreathTimeInc"] = Memory.ReadValue<float>(this.enduranceBuffBreathTimeInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceBuffBreathTimeInc + 0x30, revert ? this.OriginalValues["EnduranceBuffBreathTimeInc"] : 1f);
                            break;
                        }
                    case Skills.EnduranceBuffEnduranceInc:
                        {
                            if (this.OriginalValues["EnduranceBuffEnduranceInc"] == -1)
                            {
                                this.OriginalValues["EnduranceBuffEnduranceInc"] = Memory.ReadValue<float>(this.enduranceBuffEnduranceInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceBuffEnduranceInc + 0x30, revert ? this.OriginalValues["EnduranceBuffEnduranceInc"] : 0.7f);
                            break;
                        }
                    case Skills.EnduranceBuffJumpCostRed:
                        {
                            if (this.OriginalValues["EnduranceBuffJumpCostRed"] == -1)
                            {
                                this.OriginalValues["EnduranceBuffJumpCostRed"] = Memory.ReadValue<float>(this.enduranceBuffJumpCostRed + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceBuffJumpCostRed + 0x30, revert ? this.OriginalValues["EnduranceBuffJumpCostRed"] : 0.3f);
                            break;
                        }
                    case Skills.EnduranceBuffRestoration:
                        {
                            if (this.OriginalValues["EnduranceBuffRestoration"] == -1)
                            {
                                this.OriginalValues["EnduranceBuffRestoration"] = Memory.ReadValue<float>(this.enduranceBuffRestoration + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceBuffRestoration + 0x30, revert ? this.OriginalValues["EnduranceBuffRestoration"] : 0.75f);
                            break;
                        }
                    case Skills.EnduranceHands:
                        {
                            if (this.OriginalValues["EnduranceHands"] == -1)
                            {
                                this.OriginalValues["EnduranceHands"] = Memory.ReadValue<float>(this.enduranceHands + 0x30);
                            }
                            Memory.WriteValue<float>(this.enduranceHands + 0x30, revert ? this.OriginalValues["EnduranceHands"] : 0.5f);
                            break;
                        }
                    case Skills.StrengthBuffAimFatigue:
                        {
                            if (this.OriginalValues["StrengthBuffAimFatigue"] == -1)
                            {
                                this.OriginalValues["StrengthBuffAimFatigue"] = Memory.ReadValue<float>(this.strengthBuffAimFatigue + 0x30);
                            }
                            //Memory.WriteValue<float>(this.strengthBuffAimFatigue + 0x30, revert ? this.OriginalValues["StrengthBuffAimFatigue"] : 0.5f);
                            break;
                        }
                    case Skills.StrengthBuffElite:
                        {

                            Memory.WriteValue<bool>(this.strengthBuffElite + 0x30, true);
                            break;
                        }
                    case Skills.StrengthBuffJumpHeightInc:
                        {
                            if (this.OriginalValues["StrengthBuffJumpHeightInc"] == -1)
                            {
                                this.OriginalValues["StrengthBuffJumpHeightInc"] = Memory.ReadValue<float>(this.strengthBuffJumpHeightInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffJumpHeightInc + 0x30, revert ? this.OriginalValues["StrengthBuffJumpHeightInc"] : 0.2f);
                            break;
                        }
                    case Skills.StrengthBuffLiftWeightInc:
                        {
                            if (this.OriginalValues["StrengthBuffLiftWeightInc"] == -1)
                            {
                                this.OriginalValues["StrengthBuffLiftWeightInc"] = Memory.ReadValue<float>(this.strengthBuffLiftWeightInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffLiftWeightInc + 0x30, revert ? this.OriginalValues["StrengthBuffLiftWeightInc"] : 0.3f);
                            break;
                        }
                    case Skills.StrengthBuffMeleeCrits:
                        {
                            if (this.OriginalValues["StrengthBuffMeleeCrits"] == -1)
                            {
                                this.OriginalValues["StrengthBuffMeleeCrits"] = Memory.ReadValue<float>(this.strengthBuffMeleeCrits + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffMeleeCrits + 0x30, revert ? this.OriginalValues["StrengthBuffMeleeCrits"] : 0.5f);
                            break;
                        }
                    case Skills.StrengthBuffMeleePowerInc:
                        {
                            if (this.OriginalValues["StrengthBuffMeleePowerInc"] == -1)
                            {
                                this.OriginalValues["StrengthBuffMeleePowerInc"] = Memory.ReadValue<float>(this.strengthBuffMeleePowerInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffMeleePowerInc + 0x30, revert ? this.OriginalValues["StrengthBuffMeleePowerInc"] : 0.3f);
                            break;
                        }
                    case Skills.StrengthBuffSprintSpeedInc:
                        {
                            if (this.OriginalValues["StrengthBuffSprintSpeedInc"] == -1)
                            {
                                this.OriginalValues["StrengthBuffSprintSpeedInc"] = Memory.ReadValue<float>(this.strengthBuffSprintSpeedInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffSprintSpeedInc + 0x30, revert ? this.OriginalValues["StrengthBuffSprintSpeedInc"] : 0.2f);
                            break;
                        }
                    case Skills.StrengthBuffThrowDistanceInc:
                        {
                            if (this.OriginalValues["StrengthBuffThrowDistanceInc"] == -1)
                            {
                                this.OriginalValues["StrengthBuffThrowDistanceInc"] = Memory.ReadValue<float>(this.strengthBuffThrowDistanceInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.strengthBuffThrowDistanceInc + 0x30, revert ? this.OriginalValues["StrengthBuffThrowDistanceInc"] : 0.2f);
                            break;
                        }
                    case Skills.ThrowingStrengthBuff:
                        {

                            //Memory.WriteValue<bool>(this.throwingStrengthBuff + 0x30, true);
                            break;
                        }
                    case Skills.ThrowingEliteBuff:
                        {

                            //Memory.WriteValue<bool>(this.throwingEliteBuff + 0x30, true);
                            break;
                        }
                    case Skills.VitalityBuffBleedStop:
                        {

                            Memory.WriteValue<bool>(this.vitalityBuffBleedStop + 0x30, true);
                            break;
                        }
                    case Skills.VitalityBuffRegeneration:
                        {

                            Memory.WriteValue<bool>(this.vitalityBuffRegeneration + 0x30, true);
                            break;
                        }
                    case Skills.VitalityBuffSurviobilityInc:
                        {
                            if (this.OriginalValues["VitalityBuffSurviobilityInc"] == -1)
                            {
                                this.OriginalValues["VitalityBuffSurviobilityInc"] = Memory.ReadValue<float>(this.vitalityBuffSurviobilityInc + 0x30);
                            }
                            Memory.WriteValue<float>(this.vitalityBuffSurviobilityInc + 0x30, revert ? this.OriginalValues["VitalityBuffSurviobilityInc"] : 0.2f);
                            break;
                        }
                    case Skills.SearchBuffSpeed:
                        {
                            if (this.OriginalValues["SearchBuffSpeed"] == -1)
                            {
                                this.OriginalValues["SearchBuffSpeed"] = Memory.ReadValue<float>(this.searchBuffSpeed + 0x30);
                            }
                            Memory.WriteValue<float>(this.searchBuffSpeed + 0x30, revert ? this.OriginalValues["SearchBuffSpeed"] : 0.5f);
                            break;
                        }
                    case Skills.MetabolismMiscDebuffTime:
                        {
                            if (this.OriginalValues["MetabolismMiscDebuffTime"] == -1)
                            {
                                this.OriginalValues["MetabolismMiscDebuffTime"] = Memory.ReadValue<float>(this.metabolismMiscDebuffTime + 0x30);
                            }
                            Memory.WriteValue<float>(this.metabolismMiscDebuffTime + 0x30, revert ? this.OriginalValues["MetabolismMiscDebuffTime"] : 0.5f);
                            break;
                        }
                    case Skills.MetabolismEliteBuffNoDyhydration:
                        {

                            Memory.WriteValue<bool>(this.metabolismEliteBuffNoDyhydration + 0x30, true);
                            break;
                        }
                    case Skills.AttentionEliteLuckySearch:
                        {

                            //Memory.WriteValue<bool>(this.attentionEliteLuckySearch + 0x30, true);
                            break;
                        }
                    case Skills.HealthBreakChanceRed:
                        {
                            if (this.OriginalValues["HealthBreakChanceRed"] == -1)
                            {
                                this.OriginalValues["HealthBreakChanceRed"] = Memory.ReadValue<float>(this.healthBreakChanceRed + 0x30);
                            }
                            Memory.WriteValue<float>(this.healthBreakChanceRed + 0x30, revert ? this.OriginalValues["HealthBreakChanceRed"] : 0.6f);
                            break;
                        }
                    case Skills.HealthEliteAbsorbDamage:
                        {

                            Memory.WriteValue<bool>(this.healthEliteAbsorbDamage + 0x30, true);
                            break;
                        }
                    case Skills.HealthEnergy:
                        {
                            if (this.OriginalValues["HealthEnergy"] == -1)
                            {
                                this.OriginalValues["HealthEnergy"] = Memory.ReadValue<float>(this.healthEnergy + 0x30);
                            }
                            Memory.WriteValue<float>(this.healthEnergy + 0x30, revert ? this.OriginalValues["HealthEnergy"] : 0.3f);
                            break;
                        }
                    case Skills.SurgerySpeed:
                        {
                            if (this.OriginalValues["SurgerySpeed"] == -1)
                            {
                                this.OriginalValues["SurgerySpeed"] = Memory.ReadValue<float>(this.surgerySpeed + 0x30);
                            }
                            Memory.WriteValue<float>(this.surgerySpeed + 0x30, revert ? this.OriginalValues["SurgerySpeed"] : 0.4f);
                            break;
                        }
                    case Skills.StressBerserk:
                        {
                            Memory.WriteValue<bool>(this.stressBerserk + 0x30, true);
                            break;
                        }
                    case Skills.StressPain:
                        {
                            if (this.OriginalValues["StressPain"] == -1)
                            {
                                this.OriginalValues["StressPain"] = Memory.ReadValue<float>(this.stressPain + 0x30);
                            }
                            Memory.WriteValue<float>(this.stressPain + 0x30, revert ? this.OriginalValues["StressPain"] : 0.5f);
                            break;
                        }
                    case Skills.DrawElite:
                        {
                            Memory.WriteValue<bool>(this.drawElite + 0x30, true);
                            break;
                        }
                    case Skills.DrawSpeed:
                        {
                            if (this.OriginalValues["DrawSpeed"] == -1)
                            {
                                this.OriginalValues["DrawSpeed"] = Memory.ReadValue<float>(this.drawSpeed + 0x30);
                            }
                            Memory.WriteValue<float>(this.drawSpeed + 0x30, revert ? this.OriginalValues["DrawSpeed"] : 0.5f);
                            break;
                        }
                    case Skills.DrawSound:
                        {
                            if (this.OriginalValues["DrawSound"] == -1)
                            {
                                this.OriginalValues["DrawSound"] = Memory.ReadValue<float>(this.drawSound + 0x30);
                            }
                            Memory.WriteValue<float>(this.drawSound + 0x30, revert ? this.OriginalValues["DrawSound"] : 0.5f);
                            break;
                        }
                    case Skills.CovertMovementSpeed:
                        {
                            if (this.OriginalValues["CovertMovementSpeed"] == -1)
                            {
                                this.OriginalValues["CovertMovementSpeed"] = Memory.ReadValue<float>(this.covertMovementSpeed + 0x30);
                            }
                            Memory.WriteValue<float>(this.covertMovementSpeed + 0x30, revert ? this.OriginalValues["CovertMovementSpeed"] : 1.5f);
                            break;
                        }
                    case Skills.CovertMovementSoundVolume:
                        {
                            if (this.OriginalValues["CovertMovementSoundVolume"] == -1)
                            {
                                this.OriginalValues["CovertMovementSoundVolume"] = Memory.ReadValue<float>(this.covertMovementSoundVolume + 0x30);
                            }
                            Memory.WriteValue<float>(this.covertMovementSoundVolume + 0x30, revert ? this.OriginalValues["CovertMovementSoundVolume"] : 0.6f);
                            break;
                        }
                    case Skills.CovertMovementLoud:
                        {
                            if (this.OriginalValues["CovertMovementLoud"] == -1)
                            {
                                this.OriginalValues["CovertMovementLoud"] = Memory.ReadValue<float>(this.covertMovementLoud + 0x30);
                            }
                            Memory.WriteValue<float>(this.covertMovementLoud + 0x30, revert ? this.OriginalValues["CovertMovementLoud"] : 0.6f);
                            break;
                        }
                    case Skills.CovertMovementEquipment:
                        {
                            if (this.OriginalValues["CovertMovementEquipment"] == -1)
                            {
                                this.OriginalValues["CovertMovementEquipment"] = Memory.ReadValue<float>(this.covertMovementEquipment + 0x30);
                            }
                            Memory.WriteValue<float>(this.covertMovementEquipment + 0x30, revert ? this.OriginalValues["CovertMovementEquipment"] : 0.6f);
                            break;
                        }
                    case Skills.CovertMovementElite:
                        {
                            Memory.WriteValue<bool>(this.covertMovementElite + 0x30, true);
                            break;
                        }
                    case Skills.PerceptionHearing:
                        {
                            if (this.OriginalValues["PerceptionHearing"] == -1)
                            {
                                this.OriginalValues["PerceptionHearing"] = Memory.ReadValue<float>(this.perceptionHearing + 0x30);
                            }
                            Memory.WriteValue<float>(this.perceptionHearing + 0x30, revert ? this.OriginalValues["PerceptionHearing"] : 0.15f);
                            break;
                        }
                    case Skills.PerceptionLootDot:
                        {
                            if (this.OriginalValues["PerceptionLootDot"] == -1)
                            {
                                this.OriginalValues["PerceptionLootDot"] = Memory.ReadValue<float>(this.perceptionLootDot + 0x30);
                            }
                            Memory.WriteValue<float>(this.perceptionLootDot + 0x30, revert ? this.OriginalValues["PerceptionLootDot"] : 1f);
                            break;
                        }

                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetSkillValue ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Changes movement state
        /// </summary>
        public void SetMovementState(bool on)
        {
            try
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
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetMovementState ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sets maximum stamina / hand stamina
        /// </summary>
        public void SetMaxStamina()
        {
            try
            {
                if (this.OriginalValues["StaminaCapacity"] == -1)
                {
                    this.OriginalValues["StaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC0);
                    this.OriginalValues["HandStaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC8);
                }

                Memory.WriteValue<float>(this.stamina + 0x48, (float)this.OriginalValues["StaminaCapacity"]);
                Memory.WriteValue<float>(this.handsStamina + 0x48, (float)this.OriginalValues["HandStaminaCapacity"]);
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetMaxStamina ({ex.Message})\n{ex.StackTrace}");
            }
        }
    }
}
