using System;
using UnityEngine;
using System.Collections.Generic;
namespace PromptsmithProtocol
{
    [CreateAssetMenu(menuName = "Promptsmith/Drift", fileName = "DRIFT_")]
    public sealed class DriftDefSO : ScriptableObject
    {
        public string id;
        [TextArea] public string description;
        public DriftEffectKind kind;
        public int value;

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(id)) throw new Exception($"{name}: missing id");
            if (string.IsNullOrWhiteSpace(description)) throw new Exception($"{id}: missing description");
        }
    }
}
