using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public enum EventEffectKind
    {
        None,
        GainCompute,
        Heal,
        HeatDelta,
        AddKeywordById,
        AddRandomKeywordByRarity,
        AddPluginRandom,
        RemoveRandomNonStarterKeyword,
        UpgradeRandomStarterKeyword,
        MetaGainInsight
    }

    [Serializable]
    public sealed class EventChoiceDef
    {
        public string label;
        public EventEffectKind effectKind;
        public int intValue;
        public string stringValue;
        public Rarity rarityValue;
    }

    [CreateAssetMenu(menuName = "Promptsmith/Event", fileName = "EVENT_")]
    public sealed class EventDefSO : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea] public string body;
        public List<EventChoiceDef> choices = new();

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{name}: missing id");
            if (string.IsNullOrWhiteSpace(title)) throw new Exception($"{id}: missing title");
            if (choices == null || choices.Count == 0) throw new Exception($"{id}: choices required");
            foreach (var c in choices)
                if (string.IsNullOrWhiteSpace(c.label)) throw new Exception($"{id}: choice label missing");
        }
    }
}
