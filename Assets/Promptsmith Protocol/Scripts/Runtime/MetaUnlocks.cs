using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public static class MetaKeys
    {
        public const string Insight = "PP_INSIGHT";
        public const string Artifacts = "PP_ARTIFACTS";
        public const string UnlockedKeywords = "PP_UNLOCK_KW";
    }

    public static class MetaUnlocks
    {
        public static bool IsKeywordUnlocked(KeywordDefSO kw)
        {
            if (kw.unlockCostInsight <= 0) return true;

            // Always-on starter ids
            if (kw.id is "BURST" or "PING" or "SHIELD" or "PATCH" or "JAM" or "TRACE" or "CLEAN" or "CACHE" or "SPLICE" or "SHIFT" or "PACKET" or "TOKENIZE")
                return true;

            var blob = PlayerPrefs.GetString(MetaKeys.UnlockedKeywords, "");
            foreach (var part in blob.Split('|'))
                if (part == kw.id) return true;

            return false;
        }

        public static void UnlockKeyword(string id)
        {
            var blob = PlayerPrefs.GetString(MetaKeys.UnlockedKeywords, "");
            var set = new HashSet<string>(blob.Split('|'), StringComparer.Ordinal);
            set.Add(id);
            PlayerPrefs.SetString(MetaKeys.UnlockedKeywords, string.Join("|", set));
            PlayerPrefs.Save();
        }
    }
}
