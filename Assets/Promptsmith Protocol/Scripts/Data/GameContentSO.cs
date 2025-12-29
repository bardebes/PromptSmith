using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    [CreateAssetMenu(menuName = "Promptsmith/GameContent", fileName = "GameContent")]
    public sealed class GameContentSO : ScriptableObject
    {
        public List<KeywordDefSO> keywords = new();
        public List<DriftDefSO> drifts = new();
        public List<PluginDefSO> plugins = new();
        public List<EnemyDefSO> enemies = new();
        public List<EventDefSO> events = new();

        private Dictionary<string, KeywordDefSO> _kw;
        private Dictionary<string, DriftDefSO> _dr;
        private Dictionary<string, PluginDefSO> _pl;
        private Dictionary<string, EnemyDefSO> _en;
        private Dictionary<string, EventDefSO> _ev;

        public IReadOnlyList<KeywordDefSO> Keywords => keywords;
        public IReadOnlyList<DriftDefSO> Drifts => drifts;
        public IReadOnlyList<PluginDefSO> Plugins => plugins;
        public IReadOnlyList<EnemyDefSO> Enemies => enemies;
        public IReadOnlyList<EventDefSO> Events => events;

        public KeywordDefSO KW(string id) => _kw[id];
        public DriftDefSO DR(string id) => _dr[id];
        public PluginDefSO PL(string id) => _pl[id];
        public EnemyDefSO EN(string id) => _en[id];
        public EventDefSO EV(string id) => _ev[id];

        private void OnEnable() => RebuildIndexes();

        public void RebuildIndexes()
        {
            _kw = new Dictionary<string, KeywordDefSO>(StringComparer.Ordinal);
            _dr = new Dictionary<string, DriftDefSO>(StringComparer.Ordinal);
            _pl = new Dictionary<string, PluginDefSO>(StringComparer.Ordinal);
            _en = new Dictionary<string, EnemyDefSO>(StringComparer.Ordinal);
            _ev = new Dictionary<string, EventDefSO>(StringComparer.Ordinal);

            ValidateUniqueOrThrow(keywords, k => k.id, "Keyword");
            ValidateUniqueOrThrow(drifts, d => d.id, "Drift");
            ValidateUniqueOrThrow(plugins, p => p.id, "Plugin");
            ValidateUniqueOrThrow(enemies, e => e.id, "Enemy");
            ValidateUniqueOrThrow(events, e => e.id, "Event");

            foreach (var k in keywords) { k.ValidateOrThrow(); _kw[k.id] = k; }
            foreach (var d in drifts) { d.ValidateOrThrow(); _dr[d.id] = d; }
            foreach (var p in plugins) { p.ValidateOrThrow(); _pl[p.id] = p; }
            foreach (var e in enemies) { e.ValidateOrThrow(); _en[e.id] = e; }
            foreach (var e in events) { e.ValidateOrThrow(); _ev[e.id] = e; }
        }

        private static void ValidateUniqueOrThrow<T>(IEnumerable<T> items, Func<T, string> id, string label)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                var v = id(it);
                if (string.IsNullOrWhiteSpace(v)) throw new Exception($"{label}: missing id");
                if (!set.Add(v)) throw new Exception($"{label}: duplicate id '{v}'");
            }
        }
    }
}
