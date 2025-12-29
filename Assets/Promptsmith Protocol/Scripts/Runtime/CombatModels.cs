using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public sealed class Encounter
    {
        public EnemyDefSO enemy;
        public int maxHp;
        public EnemyPassiveKind passiveKind;
        public int passiveValue;
    }

    public sealed class CombatState
    {
        public Encounter encounter;

        public List<DriftDefSO> drifts = new();
        public List<string> hand = new();
        public List<string> log = new();

        public int enemyHp;
        public int enemyShield;
        public int enemyMarked;
        public int enemyWeakTurns;
        public int enemyDamageDownTurns;
        public int enemyReflect;

        public EnemyIntentDef intent;

        public int compileCount;
        public bool isOver;
        public bool playerWon;

        public HashSet<int> selectedHandIndices = new();
        public Dictionary<string, int> cooldowns = new(StringComparer.Ordinal);
        public HashSet<string> oncePerFightUsed = new(StringComparer.Ordinal);

        public int firstTurnRandomPenaltyIndex = -1; // StartWithCorruptedInHand drift
        public int repeatedKeywordPenaltyPct = 0; // RepeatKeywordCorruptsNextTurn drift
        public string repeatedKeywordId = null;

        public void ClearSelections() => selectedHandIndices.Clear();
    }

    public sealed class CompiledAction
    {
        public ActionKind kind;
        public int power;
        public string sourceId;

        public bool multiHit;
        public int hits = 1;

        public int markedStacks;
        public int weakTurns;

        public int reduceEnemyDamagePct;

        public int draw;
        public int computeGain;
        public int heatDelta;

        public int ignoreDriftCharges;
        public bool setOracle;
        public bool fork;
        public bool rollback;
        public int hyperthreadTurns;
        public int reflectValue;
    }

    public sealed class CompileResult
    {
        public List<string> promptAfterOps = new();
        public List<string> lines = new();
        public List<CompiledAction> actions = new();
    }
}
