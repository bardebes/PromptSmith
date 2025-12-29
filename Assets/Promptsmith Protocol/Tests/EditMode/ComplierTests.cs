#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace PromptsmithProtocol.Tests
{
    public sealed class CompilerTests
    {
        private GameContentSO _content;

        [SetUp]
        public void SetUp()
        {
            // Create minimal content in-memory (no assets).
            _content = ScriptableObject.CreateInstance<GameContentSO>();
            _content.keywords = new System.Collections.Generic.List<KeywordDefSO>();
            _content.drifts = new System.Collections.Generic.List<DriftDefSO>();
            _content.plugins = new System.Collections.Generic.List<PluginDefSO>();
            _content.enemies = new System.Collections.Generic.List<EnemyDefSO>();
            _content.events = new System.Collections.Generic.List<EventDefSO>();

            AddKW("BURST", KeywordType.Action, ActionKind.Damage, OperatorKind.None, 8);
            AddKW("SHIELD", KeywordType.Action, ActionKind.Shield, OperatorKind.None, 6);
            AddKW("PATCH", KeywordType.Action, ActionKind.Heal, OperatorKind.None, 5);
            AddKW("DRAIN", KeywordType.Action, ActionKind.Heal, OperatorKind.None, 6);
            AddKW("SPLICE", KeywordType.Operator, ActionKind.None, OperatorKind.Splice, 0);
            AddKW("SHIFT", KeywordType.Operator, ActionKind.None, OperatorKind.Shift, 0);
            AddKW("INVERT", KeywordType.Operator, ActionKind.None, OperatorKind.Invert, 0);
            AddKW("ECHO", KeywordType.Operator, ActionKind.None, OperatorKind.Echo, 50);
            AddKW("COMPRESS", KeywordType.Operator, ActionKind.None, OperatorKind.Compress, 170);
            AddKW("NULL", KeywordType.Action, ActionKind.Shield, OperatorKind.None, 2);

            AddDrift("D1", DriftEffectKind.FirstKeywordIgnored, 1);

            _content.RebuildIndexes();
        }

        [Test]
        public void Splice_DuplicatesNextKeyword()
        {
            var rng = new DeterministicRng(123);
            var run = new RunState { player = new PlayerState(), deck = new DeckState() };
            var c = new CombatState();

            var res = Compiler.Compile(_content, rng, run, c, new() { "SPLICE", "BURST", "SHIELD" });
            Assert.AreEqual(4, res.promptAfterOps.Count); // splice inserted
        }

        [Test]
        public void Shift_SwapsNextTwo()
        {
            var rng = new DeterministicRng(123);
            var run = new RunState { player = new PlayerState(), deck = new DeckState() };
            var c = new CombatState();

            var res = Compiler.Compile(_content, rng, run, c, new() { "SHIFT", "BURST", "SHIELD" });
            Assert.AreEqual("SHIELD", res.promptAfterOps[1]);
            Assert.AreEqual("BURST", res.promptAfterOps[2]);
        }

        [Test]
        public void Invert_FlipsBurstToShield()
        {
            var rng = new DeterministicRng(123);
            var run = new RunState { player = new PlayerState(), deck = new DeckState() };
            var c = new CombatState();

            var res = Compiler.Compile(_content, rng, run, c, new() { "INVERT", "BURST", "PATCH" });
            Assert.AreEqual("SHIELD", res.promptAfterOps[1]);
        }

        [Test]
        public void Drift_FirstKeywordIgnored_ReplacesWithNull()
        {
            var rng = new DeterministicRng(123);
            var run = new RunState { player = new PlayerState(), deck = new DeckState() };
            var c = new CombatState();
            c.drifts.Add(MakeDrift(DriftEffectKind.FirstKeywordIgnored, 1));

            var res = Compiler.Compile(_content, rng, run, c, new() { "BURST", "SHIELD", "PATCH" });
            Assert.Contains("NULL", res.promptAfterOps);
        }

        [Test]
        public void Compress_MergesFirstTwoEligibleActions()
        {
            var rng = new DeterministicRng(123);
            var run = new RunState { player = new PlayerState(), deck = new DeckState() };
            var c = new CombatState();

            var res = Compiler.Compile(_content, rng, run, c, new() { "COMPRESS", "BURST", "BURST" });
            Assert.LessOrEqual(res.actions.Count, 2);
        }

        [Test]
        public void Determinism_SameSeedSameMap()
        {
            var a = new DeterministicRng(999);
            var b = new DeterministicRng(999);

            var ma = MapGenerator.Generate(a, 12, 3);
            var mb = MapGenerator.Generate(b, 12, 3);

            Assert.AreEqual(ma.nodes.Count, mb.nodes.Count);
            for (var i = 0; i < ma.nodes.Count; i++)
            {
                Assert.AreEqual(ma.nodes[i].type, mb.nodes[i].type);
                Assert.AreEqual(ma.nodes[i].next.Count, mb.nodes[i].next.Count);
                for (var j = 0; j < ma.nodes[i].next.Count; j++)
                    Assert.AreEqual(ma.nodes[i].next[j], mb.nodes[i].next[j]);
            }
        }

        private void AddKW(string id, KeywordType type, ActionKind action, OperatorKind op, int power)
        {
            var kw = ScriptableObject.CreateInstance<KeywordDefSO>();
            kw.id = id;
            kw.displayName = id;
            kw.keywordType = type;
            kw.actionKind = action;
            kw.operatorKind = op;
            kw.basePower = power;
            kw.unlockCostInsight = 0;
            _content.keywords.Add(kw);
        }

        private void AddDrift(string id, DriftEffectKind kind, int value)
        {
            var d = ScriptableObject.CreateInstance<DriftDefSO>();
            d.id = id;
            d.description = id;
            d.kind = kind;
            d.value = value;
            _content.drifts.Add(d);
        }

        private DriftDefSO MakeDrift(DriftEffectKind kind, int value)
        {
            var d = ScriptableObject.CreateInstance<DriftDefSO>();
            d.id = "X";
            d.description = "X";
            d.kind = kind;
            d.value = value;
            return d;
        }
    }
}
#endif