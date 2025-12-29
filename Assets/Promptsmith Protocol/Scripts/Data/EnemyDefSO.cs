using UnityEngine;
using System;
using System.Collections.Generic;

namespace PromptsmithProtocol
{
    [Serializable]
    public sealed class EnemyIntentDef
    {
        public string id;
        public string description;
        public EnemyIntentKind kind;
        public int basePower;

        public bool appliesWeak;
        public bool appliesMarked;
        public bool appliesJam;
        public bool grantsShield;
        public bool healsSelf;

        public bool multiHit;
        public int hits = 1;

        public void ValidateOrThrow(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{ownerId}: intent missing id");
            if (string.IsNullOrWhiteSpace(description)) throw new Exception($"{ownerId}:{id}: intent missing description");
            hits = Mathf.Max(1, hits);
        }
    }

    [CreateAssetMenu(menuName = "Promptsmith/Enemy", fileName = "ENEMY_")]
    public sealed class EnemyDefSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool isBoss;
        public int baseMaxHp;

        public EnemyPassiveKind passiveKind;
        public int passiveValue;

        public List<EnemyIntentDef> intents = new();

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{name}: missing id");
            if (string.IsNullOrWhiteSpace(displayName)) throw new Exception($"{id}: missing displayName");
            if (baseMaxHp <= 0) throw new Exception($"{id}: baseMaxHp must be > 0");
            if (intents == null || intents.Count == 0) throw new Exception($"{id}: intents required");
            foreach (var it in intents) it.ValidateOrThrow(id);
        }
    }
}