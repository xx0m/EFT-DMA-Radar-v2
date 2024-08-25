namespace Offsets
{
    public struct UnityList
    {
        public const uint Base = 0x10; // to UnityListBase
        public const uint Count = 0x18; // int32
    }

    public struct UnityDictionary
    {
        public const uint Base = 0x18; // to Base
        public const uint Count = 0x40; // int32
    }

    public struct UnityListBase
    {
        public const uint Start = 0x20; // start of list +(i * 0x8)
    }

    public struct UnityString
    {
        public const uint Length = 0x10; // int32
        public const uint Value = 0x14; // string,unicode
    }

    public struct ModuleBase
    {
        public const uint GameObjectManager = 0x17FFD28; // to GameObjectManager
        public const uint CameraObjectManager = 0x0179F500; // to Camera
        public const uint TimeScale = 0x17FFAE0; // to TimeScale
    }

    public struct TimeScale
    {
        public const uint Value = 0xFC;
    }

    public struct GameObject
    {
        public static readonly uint[] To_TransformInternal = new uint[] { 0x10, 0x30, 0x30, 0x8, 0x28, 0x10 }; // to TransformInternal
        public const uint ObjectClass = 0x30;
        public const uint ObjectName = 0x60; // string,default (null terminated)
    }

    public struct GameWorld
    {
        public static readonly uint[] To_LocalGameWorld = new uint[] { GameObject.ObjectClass, 0x18, 0x28 };
    }

    public struct ExfilController // -.GClass11CD
    {
        public const uint ExfilCount = 0x18; // [18] Logger : -.GClass2A7B
        public const uint PMCExfilList = 0x20; // [20] exfiltrationPointArray_0x20 : EFT.Interactive.ExfiltrationPoint[]
        public const uint ScavExfilList = 0x28; // [28] scavExfiltrationPointArray_0x28 : EFT.Interactive.ScavExfiltrationPoint[]
    }

    public struct Exfil
    {
        public const uint Settings = 0x70; // [70] Settings : EFT.Interactive.ExitTriggerSettings
        public const uint Status = 0xC0; // [C0] _status : System.Byte
    }

    public struct ExitTriggerSettings
    {
        public const uint Name = 0x18; // [18] Name : String
    }

    public struct UnityClass
    {
        public static readonly uint[] Name = new uint[] { 0x0, 0x0, 0x48 }; // to ClassName
    }

    public struct Grenades // -.GClass0822<Int32, Throwable>
    {
        public const uint List = 0x18; // [18] list_0x18 : System.Collections.Generic.List<Var>
    }

    public struct Player // EFT.Player : MonoBehaviour, 
    {
        public static readonly uint[] To_TransformInternal = new uint[] { PlayerBody, 0x28, 0x28, 0x10, 0x20 + (0 * 0x8), 0x10 }; // to TransformInternal
        public const uint MovementContext = 0x50; // to MovementContext
        public const uint Corpse = 0x3F8; // EFT.Interactive.Corpse
        public const uint Profile = 0x620; // [620] <Profile>k__BackingField : EFT.Profile
        public const uint InventoryController = 0x678; // [678] _inventoryController : -.Player.PlayerInventoryController
        public const uint PlayerBody = 0xB8; // [B8] _playerBody : EFT.PlayerBody
        public const uint Location = 0x5E8; // [5E8] <Location>k__BackingField : String
        public const uint Physical = 0x630; // [630] Physical : -.GClass
        public const uint ProceduralWeaponAnimation = 0x1D8; // [1D8] <ProceduralWeaponAnimation>k__BackingField : EFT.Animations.ProceduralWeaponAnimation
        public const uint HandsController = 0x680; // [680] _handsController : -.Player.AbstractHandsController
    }

    public struct Profile // EFT.Profile
    {
        public const uint Id = 0x10; // [10] Id : String
        public const uint AccountId = 0x18; // [18] AccountId : String
        public const uint PlayerInfo = 0x28; // [28] Info : -.GClass17BE
        public const uint SkillManager = 0x60; //[60] Skills : EFT.SkillManager
        public const uint QuestsData = 0x78; // [78] QuestsData : System.Collections.Generic.List<GClass342B>
    }

    public struct ObservedPlayerView // [Class] EFT.NextObservedPlayer.ObservedPlayerView : MonoBehaviour
    {
        public const uint GroupID = 0x18; // [18] string_0x18 : String
        public const uint ID = 0x40; // [40] string_0x40 : String
        public const uint NickName = 0x48; // [48] string_0x48 : String
        public const uint AccountID = 0x50; // [50] string_0x50 : String
        public const uint PlayerBody = 0x60; // [60] playerBody_0x60 : EFT.PlayerBody
        public const uint ObservedPlayerController = 0x80; // [80] gClass22BE_0x80 : -.GClass22BE
        public const uint PlayerSide = 0xF8; // [F8] int32_0xF8 : System.Int32
        public const uint IsAI = 0x108; // [108] boolean_0x108 : Boolean
        public static readonly uint[] To_MovementContext = new uint[] { ObservedPlayerController, 0xC8, 0x10 }; // to MovementContext
        public static readonly uint[] To_TransformInternal = new uint[] { PlayerBody, 0x28, 0x28, 0x10, 0x20, 0x10 }; // to TransformInternal
        public static readonly uint[] To_InventoryController = new uint[] { ObservedPlayerController, 0x118 }; // to InventoryController
        public static readonly uint[] To_HealthController = new uint[] { ObservedPlayerController, 0xF0 }; // to HealthController
        public static readonly uint[] To_HandsController = new uint[] { ObservedPlayerController, 0xD8 }; // to HandsController
    }

    public struct HealthController
    {
        public const uint HealthStatus = 0xD8; // [D8] HealthStatus : System.Int32
    }

    public struct ObservedPlayerController
    {
        public const uint Profile = 0xE8; // [E8] gClass232C_0xE8 : -.GClass232C
    }

    public struct ObservedPlayerMovementContext
    {
        public const uint Rotation = 0x88; // to Vector2
    }

    public struct PlayerSettings
    {
        public const uint Role = 0x10; // [10] Role : System.Int32
    }

    public struct InventoryController // -.GClass1A98
    {
        public const uint Inventory = 0x118; // [118] <Inventory>k__BackingField : EFT.InventoryLogic.Inventory
        public const uint ObservedPlayerInventory = 0x138; // to Inventory
    }

    public struct Inventory
    {
        public const uint Equipment = 0x10; // to Equipment
    }

    public struct Equipment
    {
        public const uint Slots = 0x78; // to UnityList
    }

    public struct Slot
    {
        public const uint Name = 0x48; // [48] <ID>k__BackingField : String
        public const uint ContainedItem = 0x38; // [38] <ContainedItem>k__BackingField : EFT.InventoryLogic.Item
        public const uint ParentItem = 0x40; // [40] <ParentItem>k__BackingField : EFT.InventoryLogic.Item
        public const uint Size = 0x8;
    }

    public struct LootListItem
    {
        public const uint LootUnknownPtr = 0x10; // to LootUnknownPtr
    }

    public struct LootUnknownPtr
    {
        public const uint LootInteractiveClass = 0x28; // to LootInteractiveClass
    }

    public struct LootInteractiveClass
    {
        public const uint LootBaseObject = 0x10; // to LootBaseObject
        public const uint ItemOwner = 0x40; // to LootItemBase
        public const uint ContainerItemOwner = 0x120; // to ContainerItemOwner
    }

    public struct LootItemBase //EFT.InventoryLogic.Item
    {
        public const uint ItemTemplate = 0x40; // [40] <Template>k__BackingField : EFT.InventoryLogic.ItemTemplate
        public const uint Grids = 0x70; // to Grids
        public const uint Slots = 0x78; // to UnityList
        public const uint Cartridges = 0x98; // via -.GClass19FD : GClass19D6, IAmmoContainer , to StackSlot
    }

    public struct StackSlot // EFT.InventoryLogic.StackSlot : Object, IContainer
    {
        public const uint Items = 0x10; // [10] _items : System.Collections.Generic.List<Item>
    }

    public struct ItemTemplate //EFT.InventoryLogic.ItemTemplate
    {
        public const uint MongoID = 0x50; // [50] <_id>k__BackingField : EFT.MongoID
        public const uint IsQuestItem = 0xBC; // [BC] QuestItem : Boolean
    }

    public struct MongoID
    {
        public const uint ID = 0x10; // [10] _stringID : String
    }

    public struct LootBaseObject
    {
        public const uint GameObject = 0x30; // to GameObject
    }

    public struct LootGameObjectClass
    {
        public static readonly uint[] To_TransformInternal = new uint[] { 0x8, 0x28, 0x10 };
    }

    public struct Grids
    {
        public const uint GridsEnumerableClass = 0x40;
    }

    public struct TransformInternal
    {
        public const uint Hierarchy = 0x38; // to TransformHierarchy
        public const uint HierarchyIndex = 0x40; // int32
    }

    public struct TransformHierarchy
    {
        public const uint Vertices = 0x18; // List<Vector128<float>>
        public const uint Indices = 0x20; // List<int>
    }
    
    public struct ProceduralWeaponAnimation
    {
        public const uint HandsContainer = 0x18; // [18] HandsContainer : EFT.Animations.PlayerSpring
        public const uint Breath = 0x28; // [28] Breath : EFT.Animations.BreathEffector
        public const uint Walk = 0x30; // [30] Walk : -.WalkEffector
        public const uint MotionReact = 0x38; // [38] MotionReact : -.MotionEffector
        public const uint ForceReact = 0x40; // [40] ForceReact : -.ForceEffector
        public const uint Shooting = 0x48; // [48] Shootingg : -.ShotEffector
        public const uint FirearmContoller = 0xA8; // [A8] _firearmController : -.Player.FirearmController
        public const uint Mask = 0x138; // [138] Mask : System.Int32
        public const uint IsAiming = 0x1BD; // [1BD] _isAiming : Boolean
        public const uint AimingSpeed = 0x1DC; // [1DC] _aimingSpeed : Single
        public const uint AimSwayStrength = 0x2B0; // [2B0] _aimSwayStrength : Single
        public const uint FovCompensatoryDistance = 0x1F0; // [1F0] _fovCompensatoryDistance : Single
    }

    public struct HandsContainer
    {
        public const uint CameraOffset = 0xDC; // [DC] CameraOffset : UnityEngine.Vector3
    }

    public struct FirearmController
    {
        public const uint WeaponLn = 0x174; //[174] WeaponLn : Single
    }

    public struct HandsController
    {
        public const uint Item = 0x60; //[60] item_0x60 : EFT.InventoryLogic.Item
    }

    public struct ObservedHandsController
    {
        public const uint Item = 0x58; //[60] item_0x60 : EFT.InventoryLogic.Item
    }

    public struct BreathEffector
    {
        public const uint Intensity = 0xA4; //[A4] Intensity : Single
    }

    public struct WalkEffector
    {
        public const uint Intensity = 0x44; //[44] Intensity : Single
    }

    public struct MotionEffector
    {
        public const uint Intensity = 0xD0; //[D0] Intensity : Single
    }

    public struct ForceEffector
    {
        public const uint Intensity = 0x30; //[30] Intensity : Single
    }

    public struct ThermalVision
    {
        public const uint ThermalVisionUtilities = 0x18; //[18] ThermalVisionUtilities : -.ThermalVisionUtilities
        public const uint StuckFPSUtilities = 0x20; //[20] StuckFpsUtilities : -.StuckFPSUtilities
        public const uint MotionBlurUtilities = 0x28; //[28] MotionBlurUtilities : -.MotionBlurUtilities
        public const uint GlitchUtilities = 0x30; //[30] GlitchUtilities : -.GlitchUtilities
        public const uint PixelationUtilities = 0x38; //[38] PixelationUtilities : -.PixelationUtilities
        public const uint On = 0xE0; //[E0] On : Boolean
        public const uint IsNoisy = 0xE1;//[E1] IsNoisy : Boolean
        public const uint IsFpsStuck = 0xE2;//[E2] IsFpsStuck : Boolean
        public const uint IsMotionBlurred = 0xE3;//[E3] IsMotionBlurred : Boolean
        public const uint IsGlitched = 0xE4;//[E4] IsGlitched : Boolean
        public const uint IsPixelated = 0xE5;//[E5] IsPixelated : Boolean
        public const uint ChromaticAberrationThermalShift = 0xE8;//[E8] ChromaticAberrationThermalShift : Single
        public const uint UnsharpRadiusBlur = 0xEC;//[EC] UnsharpRadiusBlur : Single
        public const uint UnsharpBias = 0xF0;//[F0] UnsharpBias : Single
    }

    public struct ThermalVisionUtilities
    {
        public const uint ValuesCoefs = 0x18; //[18] ValuesCoefs : -.ValuesCoefs
        public const uint CurrentRampPalette = 0x30; //[30] CurrentRampPalette : System.Int32
        public const uint DepthFade = 0x34; //[34] DepthFade : Single
    }

    public struct ValuesCoefs
    {
        public const uint MainTexColorCoef = 0x10; //[10] MainTexColorCoef : Single
        public const uint MinimumTemperatureValue = 0x14; //[14] MinimumTemperatureValue : Single
        public const uint RampShift = 0x18; //[18] RampShift : Single
    }

    public struct NightVision
    {
        public const uint On = 0xEC; //[EC] On : Boolean
    }

    public struct VisorEffect
    {
        public const uint Intensity = 0xC0; //[C0] Intensity : Single
    }

    public struct PlayerInfo // [Class] -.GClass
    {
        public const uint Nickname = 0x10; // [10] Nickname : String
        public const uint GroupId = 0x20; // [20] GroupId : String
        public const uint EntryPoint = 0x30; // [30] EntryPoint : String
        public const uint GameVersion = 0x38; // [38] GameVersion : String
        public const uint Settings = 0x50; // [50] Settings : -.GClass179A
        public const uint PlayerSide = 0x70; // [70] Side : System.Int32
        public const uint RegistrationDate = 0x74; // [74] RegistrationDate : Int32
        public const uint MemberCategory = 0x90; // [90] MemberCategory : System.Int32
    }

    public struct ExfiltrationPoint
    {
        public const uint EligibleEntryPoints = 0x98; // [98] EligibleEntryPoints : System.String[]
        public const uint EligibleIds = 0xD8; // [D8] EligibleIds : System.Collections.Generic.List<String>
    }

    public struct LocalGameWorld // [Class] -.ClientLocalGameWorld : ClientGameWorld
    {
        public const uint MapName = 0x50; // [50] string_0x50 : String
        public const uint MainPlayer = 0x140; // [140] MainPlayer : EFT.Player
        public const uint ExfilController = 0x20; // [20] gClass11CD_0x20 : -.GClass11CD
        public const uint LootList = 0xC0; // [C0] LootList : System.Collections.Generic.List<GInterface1C43>
        public const uint RegisteredPlayers = 0xE8; // [E8] RegisteredPlayers : System.Collections.Generic.List<IPlayer>
        public const uint Grenades = 0x198; // [198] Grenades : -.GClass0822<Int32, Throwable>
        public const uint RaidStarted = 0x210; // [210] boolean_0x210 : Boolean
    }

    public struct EFTHardSettings
    {
        public const uint LOOT_RAYCAST_DISTANCE = 0x210; //[210] LOOT_RAYCAST_DISTANCE : Single
        public const uint DOOR_RAYCAST_DISTANCE = 0x214; //[214] DOOR_RAYCAST_DISTANCE : Single
    }

    public struct LootableContainer
    {
        public const uint ItemOwner = 0x118; // [118] ItemOwner : -.GClass27E2
        public const uint Template = 0x120; // [120] Template : String
    }

    public struct ObservedLootItem
    {
        public const uint Item = 0xB0; // [B0] item_0xB0 : EFT.InventoryLogic.Item
    }

    public struct Item
    {
        public const uint Template = 0x40; // [40] <Template>k__BackingField : EFT.InventoryLogic.ItemTemplate
    }

    public struct WeaponTemplate
    {
        public const uint Chambers = 0x188; // [188] Chambers : EFT.InventoryLogic.Slot[]
        public const uint AllowJam = 0x2DC; // [2DC] AllowJam : Boolean
        public const uint AllowFeed = 0x2DD; // [2DD] AllowFeed : Boolean
        public const uint AllowMisfire = 0x2DE; // [2DE] AllowMisfire : Boolean
        public const uint AllowSlide = 0x2DF; //  [2DF] AllowSlide : Boolean
    }

    public struct ItemOwner
    {
        public const uint Item = 0xB8; // [B8] item_0xB8 : EFT.InventoryLogic.Item
    }

    public struct MovementContext //EFT.MovementContext
    {
        public const uint Rotation = 0x408; // [408] _myRotation : UnityEngine.Vector2
        public const uint BaseMovementState = 0xE0; // [E0] <CurrentState>k__BackingField : EFT.BaseMovementState
    }

    public struct Physical
    {
        public const uint Stamina = 0x38; // [38] Stamina : -.GClass0792
        public const uint HandsStamina = 0x40; // [40] HandsStamina : -.GClass0792
        public const uint StaminaCapacity = 0xC0; // [C0] StaminaCapacity : Single
        public const uint HandsCapacity = 0xC8; // [C8] HandsCapacity : Single
    }

    public struct Stamina
    {
        public const uint Current = 0x48; //[48] Current : Single
        public const uint ForceMode = 0x5C; // [5C] boolean_0x5C : Boolean
    }

    public struct BaseMovementState
    {
        public const uint Name = 0x21; //[21] Name : System.Byte
    }

    public struct SkillManager
    {
        public const uint EnduranceBuffEnduranceInc = 0x20;
        public const uint EnduranceHands = 0x28;
        public const uint EnduranceBuffJumpCostRed = 0x30;
        public const uint EnduranceBuffBreathTimeInc = 0x38;
        public const uint EnduranceBuffRestoration = 0x40;
        public const uint EnduranceBreathElite = 0x48;
        public const uint StrengthBuffLiftWeightInc = 0x50;
        public const uint StrengthBuffSprintSpeedInc = 0x58;
        public const uint StrengthBuffJumpHeightInc = 0x60;
        public const uint StrengthBuffAimFatigue = 0x68;
        public const uint StrengthBuffThrowDistanceInc = 0x70;
        public const uint StrengthBuffMeleePowerInc = 0x78;
        public const uint StrengthBuffElite = 0x80;
        public const uint StrengthBuffMeleeCrits = 0x88;
        public const uint VitalityBuffBleedChanceRed = 0x90;
        public const uint VitalityBuffSurviobilityInc = 0x98;
        public const uint VitalityBuffRegeneration = 0xA0;
        public const uint VitalityBuffBleedStop = 0xA8;
        public const uint HealthBreakChanceRed = 0xB0;
        public const uint HealthEnergy = 0xC0;
        public const uint HealthHydration = 0xC8;
        public const uint HealthEliteAbsorbDamage = 0xD0;
        public const uint StressResistancePain = 0xE0;
        public const uint StressResistanceTremor = 0xE8;
        public const uint StressResistanceBerserk = 0xF0;
        public const uint MetabolismRatioPlus = 0xF8;
        public const uint MetabolismMiscDebuffTime = 0x108;
        public const uint MetabolismEliteBuffNoDyhydration = 0x110;
        public const uint PerceptionHearing = 0x118;
        public const uint PerceptionLootDot = 0x120;
        public const uint PerceptionEliteNoIdea = 0x128;
        public const uint IntellectLearningSpeed = 0x130;
        public const uint IntellectEliteNaturalLearner = 0x140;
        public const uint IntellectEliteAmmoCounter = 0x148;
        public const uint IntellectEliteContainerScope = 0x150;
        public const uint AttentionLootSpeed = 0x160;
        public const uint AttentionExamine = 0x168;
        public const uint AttentionEliteLuckySearch = 0x170;
        public const uint MagDrillsLoadSpeed = 0x180;
        public const uint MagDrillsUnloadSpeed = 0x188;
        public const uint MagDrillsInventoryCheckSpeed = 0x190;
        public const uint MagDrillsInventoryCheckAccuracy = 0x198;
        public const uint MagDrillsInstantCheck = 0x1A0;
        public const uint MagDrillsLoadProgression = 0x1A8;
        public const uint ImmunityMiscEffects = 0x220;
        public const uint ImmunityPoisonBuff = 0x228;
        public const uint ImmunityPainKiller = 0x230;
        public const uint ImmunityAvoidPoisonChance = 0x238;
        public const uint ImmunityAvoidMiscEffectsChance = 0x240;
        public const uint ThrowingStrengthBuff = 0x320;
        public const uint ThrowingEnergyExpenses = 0x328;
        public const uint ThrowingEliteBuff = 0x330;
        public const uint CovertMovementSoundVolume = 0x478;
        public const uint CovertMovementEquipment = 0x480;
        public const uint CovertMovementSpeed = 0x488;
        public const uint CovertMovementElite = 0x490;
        public const uint CovertMovementLoud = 0x498;
        public const uint ProneMovementSpeed = 0x4A0;
        public const uint ProneMovementVolume = 0x4A8;
        public const uint ProneMovementEliteSprint = 0x4B0;
        public const uint SearchBuffSpeed = 0x4B8;
        public const uint SearchDouble = 0x4C0;
        public const uint SurgeryReducePenalty = 0x4C8;
        public const uint SurgerySpeed = 0x4D0;
        public const uint LightVestMoveSpeedPenaltyReduction = 0x520;
        public const uint LightVestMeleeWeaponDamageReduction = 0x528;
        public const uint LightVestBleedingProtection = 0x538;
        public const uint HeavyVestMoveSpeedPenaltyReduction = 0x548;
        public const uint HeavyVestBluntThroughputDamageReduction = 0x550;
        public const uint HeavyVestNoBodyDamageDeflectChance = 0x560;
    }

    public struct SkillFloat
    {
        public const uint Value = 0x30; //[30] Value : Single
    }

    public struct SkillBool
    {
        public const uint Value = 0x30; //[30] Value : Boolean
    }

    public struct QuestData
    {
        public const uint ID = 0x10;  // [10] Id : String
        public const uint CompletedConditions = 0x20;  // [20] CompletedConditions : System.Collections.Generic.HashSet<String>
        public const uint Template = 0x28; // [28] Template : -.GClass342E
        public const uint Status = 0x34; // [34] Status : System.Int32
    }

    public struct Hashset
    {
        public const uint Size = 0x10;
        public const uint Base = 0x18;
        public const uint Start = 0x28;
        public const uint Count = 0x3C;
    }

    public struct TOD_SKY
    {
        public const uint CachedPtr = 0x10; // [10] m_CachedPtr : IntPtr
        public const uint Cycle = 0x18; // [18] Cycle : -.TOD_CycleParameters
        public const uint Instance = 0x20;
        public const uint TOD_Components = 0x78; // [78] tOD_Components_0x78 : -.TOD_Components
    }

    public struct TOD_Components
    {
        public const uint Time = 0x110; // [110] tOD_Time_0x110 : -.TOD_Time
    }

    public struct TOD_Time
    {
        public const uint GameDateTime = 0x18; // [18] GameDateTime : EFT.GameDateTime
    }
}