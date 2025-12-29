using UnityEngine;
using System;

namespace PromptsmithProtocol
{
    [CreateAssetMenu(menuName = "Promptsmith/Plugin", fileName = "PLUGIN_")]
    public sealed class PluginDefSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public Rarity rarity;
        public PluginEffectKind kind;
        public int value;

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{name}: missing id");
            if (string.IsNullOrWhiteSpace(displayName)) throw new Exception($"{id}: missing displayName");
        }
    }
}