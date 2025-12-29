using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    [Flags]
    public enum KeywordTag
    {
        None = 0,
        Attack = 1 << 0,
        Defense = 1 << 1,
        Heal = 1 << 2,
        Debuff = 1 << 3,
        Operator = 1 << 4,
        Utility = 1 << 5,
    }

    public enum Rarity { Common, Uncommon, Rare, Mythic }
    public enum KeywordType { Action, Operator, Passive, Utility }

    public enum ActionKind
    {
        None,
        Damage,
        Shield,
        Heal,
        ApplyMarked,
        ApplyWeak,
        ReduceEnemyDamageNextPct,
        CleanseSelf,
        Draw,
        GainCompute,
        HeatDelta,
        IgnoreDriftCharges,
        SetOracleIntent,
        Fork,
        Rollback,
        HyperthreadTurns
    }

    public enum OperatorKind
    {
        None,
        Splice,
        Shift,
        Echo,
        Invert,
        Compress,
        Mutate,
        SeedBias
    }

    public enum NodeType { Start, Fight, Elite, Event, Shop, Rest, Boss, End }

    public enum DriftEffectKind
    {
        None,
        FirstKeywordIgnored,
        HealingPenaltyPct,
        ShieldPenaltyPct,
        EveryThirdCompileCounterDamage,
        OperatorPenaltyPct,
        RepeatKeywordCorruptsNextTurn, // implemented as a one-turn -pct on repeated keyword
        FirstDebuffCleansed,
        EnemyIntentBoostPct,
        MarkedAffectsBothSides,
        FirstActionResolvesLast,
        DrawGivesEnemyBonusDamage,
        BufferHighEnemyPiercePct,
        HealAlsoHealsEnemyFlat,
        ThreeActionsAddsHeat,
        OperatorGivesEnemyShield,
        SecondKeywordDuplicated,
        EndTurnHeatPlusOne,
        StartWithWeak,
        StartWithCorruptedInHand, // implemented as random hand card -pct on first turn
        EnemyImmuneWeak,
        SingleHitDamagePenaltyPct,
        RandomHandKeywordPenaltyPct,
        NoBufferExtraDamageFlat,
        FirstOperatorNegated,
        EnemyBelow30Reflect
    }

    public enum PluginEffectKind
    {
        None,
        StartFightDrawPlus,
        StartFightBufferFlat,
        EnemyStartsMarked,
        FirstEnemyHitReduceFlat,
        OperatorGrantsBuffer,
        HeatGainGrantsBuffer,
        IgnoreFirstDriftEachFight,
        StartFightTempUncommonInHand,
        FirstDebuffRemovedImmediately,
        FirstTurnPowerBoostPct,
        HeatHighDamageBoostPct,
        LowHpHealingBoostPct,
        EveryThreeTurnsCleanse,
        ApplyDebuffGivesCompute,
        BufferHighEnemyDamagePenaltyPct,
        OncePerFightRecompile,
        CacheFirstTimeExtraDraw,
        OperatorPowerBoostPct,
        AfterBossHealFlat,
        AfterEliteHeatMinus,
        CompressBonusPct,
        EndTurnHeatUnchangedCompute,
        RewardDraftOptionsPlus,
    }

    public enum EnemyIntentKind { Attack, Shield, Heal, Debuff, Mixed }
    public enum EnemyPassiveKind
    {
        None,
        ReflectWhenShielded,
        PunishRepeats,
        PunishDraw,
        CleanseFirstDebuff,
        CorruptRandomKeywordNextTurn,
        LiesSometimes,
        DelayFirstAction,
        CopiesLastActionWeakly
    }
}