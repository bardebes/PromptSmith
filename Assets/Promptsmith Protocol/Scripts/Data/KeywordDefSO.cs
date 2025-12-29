using UnityEngine;
using System;

namespace PromptsmithProtocol
{
    [CreateAssetMenu(menuName = "Promptsmith/Keyword", fileName = "KW_")]
    public sealed class KeywordDefSO : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Classification")]
        public Rarity rarity;
        public KeywordType keywordType;
        public KeywordTag tags;

        [Header("Action / Operator")]
        public ActionKind actionKind;
        public OperatorKind operatorKind;

        [Header("Tuning")]
        public int basePower;
        public int cooldownTurns;
        public bool oncePerFight;

        [Header("Meta Unlock")]
        public int unlockCostInsight; // 0 => available by default

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{name}: missing id");
            if (string.IsNullOrWhiteSpace(displayName)) throw new Exception($"{id}: missing displayName");
        }
    }
}
