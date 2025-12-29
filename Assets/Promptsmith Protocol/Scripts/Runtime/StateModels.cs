using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    [Serializable]
    public sealed class PlayerState
    {
        public int maxIntegrity = 60;
        public int integrity = 60;
        public int buffer;
        public int heat;
        public int compute;

        public int markedSelf;
        public int weakTurnsSelf;

        public int reflectSelf;
        public int extraDrawNextTurn;

        public bool ignoreFirstDriftThisFight;
        public int ignoreDriftChargesThisFight;
        public bool usedRollbackThisFight;
        public bool recompileAvailableThisFight;

        public int turnsThisFight;

        public void Clamp()
        {
            maxIntegrity = Mathf.Max(1, maxIntegrity);
            integrity = Mathf.Clamp(integrity, 0, maxIntegrity);
            buffer = Mathf.Max(0, buffer);
            heat = Mathf.Clamp(heat, 0, 10);
            compute = Mathf.Max(0, compute);
            markedSelf = Mathf.Max(0, markedSelf);
            weakTurnsSelf = Mathf.Max(0, weakTurnsSelf);
            reflectSelf = Mathf.Max(0, reflectSelf);
            extraDrawNextTurn = Mathf.Max(0, extraDrawNextTurn);
            ignoreDriftChargesThisFight = Mathf.Max(0, ignoreDriftChargesThisFight);
            turnsThisFight = Mathf.Max(0, turnsThisFight);
        }
    }

    [Serializable]
    public sealed class DeckState
    {
        [SerializeField] private List<string> all = new();
        [SerializeField] private List<string> draw = new();
        [SerializeField] private List<string> discard = new();
        [SerializeField] private List<string> exhaust = new();

        public IReadOnlyList<string> All => all;
        public IReadOnlyList<string> Draw => draw;
        public IReadOnlyList<string> Discard => discard;
        public IReadOnlyList<string> Exhaust => exhaust;

        public void Init(IEnumerable<string> cards)
        {
            all = new List<string>(cards);
            draw = new List<string>();
            discard = new List<string>();
            exhaust = new List<string>();
        }

        public void StartFight(DeterministicRng rng)
        {
            draw.Clear();
            discard.Clear();
            exhaust.Clear();
            draw.AddRange(all);
            rng.Shuffle(draw);
        }

        public List<string> DrawCards(DeterministicRng rng, int count)
        {
            var hand = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw.AddRange(discard);
                    discard.Clear();
                    rng.Shuffle(draw);
                }

                if (draw.Count == 0) break;
                var top = draw[^1];
                draw.RemoveAt(draw.Count - 1);
                hand.Add(top);
            }

            return hand;
        }

        public void DiscardMany(IEnumerable<string> ids) => discard.AddRange(ids);

        public void AddToDeck(string id)
        {
            all.Add(id);
            discard.Add(id);
        }

        public bool RemoveFromDeck(string id)
        {
            var ok = all.Remove(id);
            discard.Remove(id);
            draw.Remove(id);
            return ok;
        }

        public bool UpgradeFirst(string fromId, string toId)
        {
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == fromId)
                {
                    all[i] = toId;
                    for (var j = 0; j < draw.Count; j++) if (draw[j] == fromId) draw[j] = toId;
                    for (var j = 0; j < discard.Count; j++) if (discard[j] == fromId) discard[j] = toId;
                    return true;
                }
            }
            return false;
        }

        public void EnsureFightReady(DeterministicRng rng)
        {
            if (draw.Count > 0) return;
            StartFight(rng);
        }
    }

    [Serializable]
    public sealed class MapNode
    {
        public int index;
        public int row;
        public int lane;
        public NodeType type;
        public List<int> next = new();
    }

    [Serializable]
    public sealed class MapState
    {
        public List<MapNode> nodes = new();
        public int currentIndex;
    }

    [Serializable]
    public sealed class RunState
    {
        public int seed;
        public int depthIndex;
        public PlayerState player = new();
        public DeckState deck = new();
        public MapState map = new();
        public List<string> pluginIds = new();
    }
}
