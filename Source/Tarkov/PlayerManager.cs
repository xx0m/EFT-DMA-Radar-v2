using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Net;
using System.Numerics;
using static eft_dma_radar.PlayerManager;

namespace eft_dma_radar
{
    /// <summary>
    /// Class to manage local player write operations
    /// </summary>
    public class PlayerManager
    {
        private ulong baseMovementState { get; set; }
        private ulong handsStamina { get; set; }
        public bool isADS { get; set; }
        private ulong movementContext { get; set; }
        private ulong physical { get; set; }
        private ulong playerBase { get; set; }
        private ulong playerProfile { get; set; }
        public ulong proceduralWeaponAnimation { get; set; }
        private ulong skillsManager { get; set; }
        private ulong stamina { get; set; }

        private Config _config { get => Program.Config; }
        public Dictionary<string, float> OriginalValues { get; }
        public Dictionary<string, Dictionary<string, Skill>> Skills;

        /// <summary>
        /// Creates new PlayerManager object
        /// </summary>
        public PlayerManager(ulong localGameWorld)
        {
            this.OriginalValues = new Dictionary<string, float>()
            {
                ["Mask"] = 125,
                ["AimingSpeed"] = 1,
                ["AimingSpeedSway"] = 0.2f,
                ["StaminaCapacity"] = -1,
                ["HandStaminaCapacity"] = -1,
            };

            this.Skills = new Dictionary<string, Dictionary<string, Skill>>
            {
                {
                    "Endurance", new Dictionary<string, Skill>
                    {
                        { "BuffEnduranceInc", new Skill(Offsets.SkillManager.EnduranceBuffEnduranceInc, 0.7f) },
                        { "Hands", new Skill(Offsets.SkillManager.EnduranceHands, 0.5f) },
                        { "BuffJumpCostRed", new Skill(Offsets.SkillManager.EnduranceBuffJumpCostRed, 0.3f) },
                        { "BuffBreathTimeInc", new Skill(Offsets.SkillManager.EnduranceBuffBreathTimeInc, 1f) },
                        { "BuffRestoration", new Skill(Offsets.SkillManager.EnduranceBuffRestoration, 0.5f) },
                        { "BreathElite", new Skill(Offsets.SkillManager.EnduranceBreathElite, 0f, true) }
                    }
                },
                {
                    "Strength", new Dictionary<string, Skill>
                    {
                        { "BuffLiftWeightInc", new Skill(Offsets.SkillManager.StrengthBuffLiftWeightInc, 0.3f) },
                        { "BuffSprintSpeedInc", new Skill(Offsets.SkillManager.StrengthBuffSprintSpeedInc, 0.2f) },
                        { "BuffJumpHeightInc", new Skill(Offsets.SkillManager.StrengthBuffJumpHeightInc, 0.2f + (_config.JumpPowerStrength / 100)) },
                        { "BuffAimFatigue", new Skill(Offsets.SkillManager.StrengthBuffAimFatigue, 0.2f) },
                        { "BuffThrowDistanceInc", new Skill(Offsets.SkillManager.StrengthBuffThrowDistanceInc, _config.ThrowPowerStrength / 100) },
                        { "BuffMeleePowerInc", new Skill(Offsets.SkillManager.StrengthBuffMeleePowerInc, 0.3f) },
                        { "BuffElite", new Skill(Offsets.SkillManager.StrengthBuffElite, 0f, true) },
                        { "BuffMeleeCrits", new Skill(Offsets.SkillManager.StrengthBuffMeleeCrits, 0f, true) }
                    }
                },
                {
                    "Vitality", new Dictionary<string, Skill>
                    {
                        { "BuffBleedChanceRed", new Skill(Offsets.SkillManager.VitalityBuffBleedChanceRed, 0.6f) },
                        { "BuffSurviobilityInc", new Skill(Offsets.SkillManager.VitalityBuffSurviobilityInc, 0.2f) },
                        { "BuffRegeneration", new Skill(Offsets.SkillManager.VitalityBuffRegeneration, 0f, true) },
                        { "BuffBleedStop", new Skill(Offsets.SkillManager.VitalityBuffBleedStop, 0f, true) }
                    }
                },
                {
                    "Health", new Dictionary<string, Skill>
                    {
                        { "BreakChanceRed", new Skill(Offsets.SkillManager.HealthBreakChanceRed, 0.6f) },
                        { "Energy", new Skill(Offsets.SkillManager.HealthEnergy, 0.3f) },
                        { "Hydration", new Skill(Offsets.SkillManager.HealthHydration, 0.3f) },
                        { "EliteAbsorbDamage", new Skill(Offsets.SkillManager.HealthEliteAbsorbDamage, 0f, true) }
                    }
                },
                {
                    "Stress Resistance", new Dictionary<string, Skill>
                    {
                        { "Pain", new Skill(Offsets.SkillManager.StressResistancePain, 0.5f) },
                        { "Tremor", new Skill(Offsets.SkillManager.StressResistanceTremor, 0.6f) },
                        { "Berserk", new Skill(Offsets.SkillManager.StressResistanceBerserk, 0f, true) }
                    }
                },
                {
                    "Metabolism", new Dictionary<string, Skill>
                    {
                        { "RatioPlus", new Skill(Offsets.SkillManager.MetabolismRatioPlus, 0.5f) },
                        { "MiscDebuffTime", new Skill(Offsets.SkillManager.MetabolismMiscDebuffTime, 0.5f) },
                        { "EliteBuffNoDyhydration", new Skill(Offsets.SkillManager.MetabolismEliteBuffNoDyhydration, 0f, true) }
                    }
                },
                {
                    "Perception", new Dictionary<string, Skill>
                    {
                        { "Hearing", new Skill(Offsets.SkillManager.PerceptionHearing, 0.15f) },
                        { "LootDot", new Skill(Offsets.SkillManager.PerceptionLootDot, 1f) },
                        { "EliteNoIdea", new Skill(Offsets.SkillManager.PerceptionEliteNoIdea, 0f, true) }
                    }
                },
                {
                    "Intellect", new Dictionary<string, Skill>
                    {
                        { "LearningSpeed", new Skill(Offsets.SkillManager.IntellectLearningSpeed, 1f) },
                        { "EliteNaturalLearner", new Skill(Offsets.SkillManager.IntellectEliteNaturalLearner, 0f, true) },
                        { "EliteAmmoCounter", new Skill(Offsets.SkillManager.IntellectEliteAmmoCounter, 0f, true) },
                        { "EliteContainerScope", new Skill(Offsets.SkillManager.IntellectEliteContainerScope, 0f, true) }
                    }
                },
                {
                    "Attention", new Dictionary<string, Skill>
                    {
                        { "LootSpeed", new Skill(Offsets.SkillManager.AttentionLootSpeed, 1f) },
                        { "Examine", new Skill(Offsets.SkillManager.AttentionExamine, 1f) },
                        { "EliteLuckySearch", new Skill(Offsets.SkillManager.AttentionEliteLuckySearch, 0f, true) }
                    }
                },
                {
                    "MagDrills", new Dictionary<string, Skill>
                    {
                        { "LoadSpeed", new Skill(Offsets.SkillManager.MagDrillsLoadSpeed, (float)_config.MagDrillSpeed) },
                        { "UnloadSpeed", new Skill(Offsets.SkillManager.MagDrillsUnloadSpeed, (float)_config.MagDrillSpeed) },
                        { "InventoryCheckSpeed", new Skill(Offsets.SkillManager.MagDrillsInventoryCheckSpeed, 40f) },
                        { "InventoryCheckAccuracy", new Skill(Offsets.SkillManager.MagDrillsInventoryCheckAccuracy, 100f) },
                        { "InstantCheck", new Skill(Offsets.SkillManager.MagDrillsInstantCheck, 0f, true) },
                        { "LoadProgression", new Skill(Offsets.SkillManager.MagDrillsLoadProgression, 0f, true) }
                    }
                },
                {
                    "Immunity", new Dictionary<string, Skill>
                    {
                        { "MiscEffects", new Skill(Offsets.SkillManager.ImmunityMiscEffects, 0.5f) },
                        { "PoisonBuff", new Skill(Offsets.SkillManager.ImmunityPoisonBuff, 0.5f) },
                        { "PainKiller", new Skill(Offsets.SkillManager.ImmunityPainKiller, 0.3f) },
                        { "AvoidPoisonChance", new Skill(Offsets.SkillManager.ImmunityAvoidPoisonChance, 1f) },
                        { "AvoidMiscEffectsChance", new Skill(Offsets.SkillManager.ImmunityAvoidMiscEffectsChance, 1f) }
                    }
                },
                {
                    "Throwables", new Dictionary<string, Skill>
                    {
                        { "StrengthBuff", new Skill(Offsets.SkillManager.ThrowingStrengthBuff, 0.25f) },
                        { "EnergyExpenses", new Skill(Offsets.SkillManager.ThrowingEnergyExpenses, 0.5f) },
                        { "EliteBuff", new Skill(Offsets.SkillManager.ThrowingEliteBuff, 0f, true) }
                    }
                },
                {
                    "Covert Movement", new Dictionary<string, Skill>
                    {
                        { "SoundVolume", new Skill(Offsets.SkillManager.CovertMovementSoundVolume, 0.6f) },
                        { "Equipment", new Skill(Offsets.SkillManager.CovertMovementEquipment, 0.6f) },
                        { "Speed", new Skill(Offsets.SkillManager.CovertMovementSpeed, 0.5f) },
                        { "Elite", new Skill(Offsets.SkillManager.CovertMovementElite, 0f, true) },
                        { "Loud", new Skill(Offsets.SkillManager.CovertMovementLoud, 0.6f) }
                    }
                },
                {
                    "Search", new Dictionary<string, Skill>
                    {
                        { "BuffSpeed", new Skill(Offsets.SkillManager.SearchBuffSpeed, 1.0f) },
                        { "Double", new Skill(Offsets.SkillManager.SearchDouble, 0f, true) },
                    }
                },
                {
                    "Surgery", new Dictionary<string, Skill>
                    {
                        { "ReducePenalty", new Skill(Offsets.SkillManager.SurgeryReducePenalty, 1f) },
                        { "Speed", new Skill(Offsets.SkillManager.SurgerySpeed, 0.4f) }
                    }
                },
                {
                    "Light Vests", new Dictionary<string, Skill>
                    {
                        { "MoveSpeedPenaltyReduction", new Skill(Offsets.SkillManager.LightVestMoveSpeedPenaltyReduction, 0.3f) },
                        { "MeleeWeaponDamageReduction", new Skill(Offsets.SkillManager.LightVestMeleeWeaponDamageReduction, 0.3f) },
                        { "BleedingProtection", new Skill(Offsets.SkillManager.LightVestBleedingProtection, 0f, true) },
                    }
                },
                {
                    "Heavy Vests", new Dictionary<string, Skill>
                    {
                        { "MoveSpeedPenaltyReduction", new Skill(Offsets.SkillManager.HeavyVestMoveSpeedPenaltyReduction, 0.25f) },
                        { "BluntThroughputDamageReduction", new Skill(Offsets.SkillManager.HeavyVestBluntThroughputDamageReduction, 0.2f) },
                        { "NoBodyDamageDeflectChance", new Skill(Offsets.SkillManager.HeavyVestNoBodyDamageDeflectChance, 0f, true) },
                    }
                },
            };

            var scatterMap = new ScatterReadMap(1);
            var round1 = scatterMap.AddRound();
            var round2 = scatterMap.AddRound();
            var round3 = scatterMap.AddRound();
            var round4 = scatterMap.AddRound();
            var round5 = scatterMap.AddRound();

            var playerBasePtr = round1.AddEntry<ulong>(0, 0, localGameWorld, null, Offsets.LocalGameWorld.MainPlayer);

            var playerProfilePtr = round2.AddEntry<ulong>(0, 1, playerBasePtr, null, Offsets.Player.Profile);
            var movementContextPtr = round2.AddEntry<ulong>(0, 2, playerBasePtr, null, Offsets.Player.MovementContext);
            var physicalPtr = round2.AddEntry<ulong>(0, 3, playerBasePtr, null, Offsets.Player.Physical);
            var proceduralWeaponAnimationPtr = round2.AddEntry<ulong>(0, 4, playerBasePtr, null, Offsets.Player.ProceduralWeaponAnimation);

            var skillsManagerPtr = round3.AddEntry<ulong>(0, 5, playerProfilePtr, null, Offsets.Profile.SkillManager);
            var baseMovementStatePtr = round3.AddEntry<ulong>(0, 6, movementContextPtr, null, Offsets.MovementContext.BaseMovementState);
            var isADSPtr = round3.AddEntry<ulong>(0, 7, proceduralWeaponAnimationPtr, null, Offsets.ProceduralWeaponAnimation.IsAiming);
            var staminaPtr = round3.AddEntry<ulong>(0, 8, physicalPtr, null, Offsets.Physical.Stamina);
            var handsStaminaPtr = round3.AddEntry<ulong>(0, 9, physicalPtr, null, Offsets.Physical.HandsStamina);

            var startingIndex = 10; // last scattermap index + 1

            SetupOriginalSkillValues(startingIndex, skillsManagerPtr, ref round4, ref round5);

            scatterMap.Execute();

            if (!scatterMap.Results[0][0].TryGetResult<ulong>(out var playerBase))
                return;
            if (!scatterMap.Results[0][1].TryGetResult<ulong>(out var playerProfile))
                return;
            if (!scatterMap.Results[0][2].TryGetResult<ulong>(out var movementContext))
                return;
            if (!scatterMap.Results[0][3].TryGetResult<ulong>(out var physical))
                return;
            if (!scatterMap.Results[0][4].TryGetResult<ulong>(out var proceduralWeaponAnimation))
                return;
            if (!scatterMap.Results[0][5].TryGetResult<ulong>(out var skillsManager))
                return;
            if (!scatterMap.Results[0][6].TryGetResult<ulong>(out var baseMovementState))
                return;
            if (!scatterMap.Results[0][8].TryGetResult<ulong>(out var stamina))
                return;
            if (!scatterMap.Results[0][9].TryGetResult<ulong>(out var handsStamina))
                return;

            scatterMap.Results[0][7].TryGetResult<bool>(out var isADS);

            this.playerBase = playerBase;
            this.playerProfile = playerProfile;
            this.movementContext = movementContext;
            this.baseMovementState = baseMovementState;
            this.physical = physical;
            this.stamina = stamina;
            this.handsStamina = handsStamina;
            this.skillsManager = skillsManager;
            this.proceduralWeaponAnimation = proceduralWeaponAnimation;
            this.isADS = isADS;

            ProcessOriginalSkillValues(startingIndex, ref scatterMap);
        }

        /// <summary>
        /// Enables / disables weapon recoil
        /// </summary>
        public void SetNoRecoilSway(bool on, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                var mask = Memory.ReadValue<int>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask);

                if (on && mask != 0)
                {
                    entries.Add(new ScatterWriteDataEntry<int>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask, 0));
                }
                else if (!on && mask == 0)
                {
                    entries.Add(new ScatterWriteDataEntry<int>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.Mask, (int)this.OriginalValues["Mask"]));
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetNoRecoilSway ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Enables / disables instant ads, changes per weapon
        /// </summary>
        public void SetInstantADS(bool on, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                var aimingSpeed = Memory.ReadValue<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed);

                if (on && aimingSpeed != 7)
                {
                    entries.Add(new ScatterWriteDataEntry<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, 7f));
                    entries.Add(new ScatterWriteDataEntry<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, 0f));
                }
                else if (!on && aimingSpeed != 1)
                {
                    entries.Add(new ScatterWriteDataEntry<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimingSpeed, this.OriginalValues["AimingSpeed"]));
                    entries.Add(new ScatterWriteDataEntry<float>(this.proceduralWeaponAnimation + Offsets.ProceduralWeaponAnimation.AimSwayStrength, this.OriginalValues["AimingSpeedSway"]));
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetInstantADS ({ex.Message})\n{ex.StackTrace}");
            }
        }

        private void ProcessOriginalSkillValues(int index, ref ScatterReadMap scatterMap)
        {
            try
            {
                foreach (var category in Skills)
                {
                    foreach (var skill in category.Value.Values)
                    {
                        scatterMap.Results[0][index].TryGetResult<ulong>(out var pointer);
                        skill.Pointer = pointer;
                        
                        index++;

                        if (skill.IsEliteSkill)
                        {
                            scatterMap.Results[0][index].TryGetResult<bool>(out var value);
                            skill.EliteToggled = value;
                        }
                        else
                        {
                            scatterMap.Results[0][index].TryGetResult<float>(out var value);
                            skill.DefaultValue = value;
                        }

                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - UpdateOriginalSkillValues ({ex.Message})\n{ex.StackTrace}");
            }
        }

        private void SetupOriginalSkillValues(int index, ScatterReadEntry<ulong> skillsManagerPtr, ref ScatterReadRound round4, ref ScatterReadRound round5)
        {
            try
            {
                foreach (var category in Skills)
                {
                    foreach (var skill in category.Value.Values)
                    {
                        var skillPtr = round4.AddEntry<ulong>(0, index, skillsManagerPtr, null, skill.Offset);

                        index++;

                        if (skill.EliteToggled)
                        {
                            round5.AddEntry<bool>(0, index, skillPtr, null, Offsets.SkillBool.Value);
                        }
                        else
                        {
                            round5.AddEntry<float>(0, index, skillPtr, null, Offsets.SkillFloat.Value);
                        }

                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - UpdateOriginalSkillValues ({ex.Message})\n{ex.StackTrace}");
            }
        }

        public void SetMaxSkillByCategory(string category, bool revert, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                foreach (var skill in this.Skills[category])
                {
                    var skillValue = skill.Value;

                    if (skillValue.IsEliteSkill)
                    {
                        skillValue.EliteToggled = !revert;
                        entries.Add(new ScatterWriteDataEntry<bool>(skillValue.Pointer + Offsets.SkillFloat.Value, skillValue.EliteToggled));
                    }
                    else
                    {
                        entries.Add(new ScatterWriteDataEntry<float>(skillValue.Pointer + Offsets.SkillFloat.Value, revert ? skillValue.DefaultValue : skillValue.MaxValue));
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetMaxSkillByCategory ({ex.Message})\n{ex.StackTrace}");
            }
        }

        public void SetMaxSkill(Skill skill, bool revert = false)
        {
            try
            {
                Memory.WriteValue<float>(skill.Pointer + Offsets.SkillFloat.Value, revert ? skill.DefaultValue : skill.MaxValue);
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetMaxSkill ({ex.Message})\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Changes movement state
        /// </summary>
        public void SetMovementState(bool on, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                this.baseMovementState = Memory.ReadPtr(this.movementContext + Offsets.MovementContext.BaseMovementState);
                var animationState = Memory.ReadValue<byte>(this.baseMovementState + Offsets.BaseMovementState.Name);

                if (on && animationState == 5)
                {
                    entries.Add(new ScatterWriteDataEntry<byte>(this.baseMovementState + Offsets.BaseMovementState.Name, 6));
                }
                else if (!on && animationState == 6)
                {
                    entries.Add(new ScatterWriteDataEntry<byte>(this.baseMovementState + Offsets.BaseMovementState.Name, 5));
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
        public void SetMaxStamina(ref List<IScatterWriteEntry> entries)
        {
            try
            {
                if (this.OriginalValues["StaminaCapacity"] == -1)
                {
                    this.OriginalValues["StaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC0);
                    this.OriginalValues["HandStaminaCapacity"] = Memory.ReadValue<float>(this.physical + 0xC8);
                }

                entries.Add(new ScatterWriteDataEntry<float>(this.stamina + 0x48, this.OriginalValues["StaminaCapacity"]));
                entries.Add(new ScatterWriteDataEntry<float>(this.handsStamina + 0x48, this.OriginalValues["HandStaminaCapacity"]));
            }
            catch (Exception ex)
            {
                Program.Log($"[PlayerManager] - SetMaxStamina ({ex.Message})\n{ex.StackTrace}");
            }
        }

        public class Skill
        {
            public uint Offset { get; set; }
            public ulong Pointer { get; set; }
            public float DefaultValue { get; set; }
            public float MaxValue { get; set; }
            public bool IsEliteSkill { get; set; }
            public bool EliteToggled { get; set; }

            public Skill(uint offset, float maxValue, bool isEliteSkill = false, bool eliteToggled = false)
            {
                Offset = offset;
                MaxValue = maxValue;
                IsEliteSkill = isEliteSkill;
                EliteToggled = eliteToggled;
                DefaultValue = -1f;
            }
        }
    }
}
