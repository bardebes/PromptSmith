using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public sealed class RunController : MonoBehaviour
    {
        private UiFactory _ui;
        private CombatState _combatState;

        private enum Mode { Map, Combat, End }
        private Mode _mode;

        private MapNode _currentNode;

        private string _enemyName;
        private int _enemyHp;
        private int _enemyMaxHp;
        private int _enemyAttack;
        private int _turn;

        private readonly List<string> _hand = new();
        private readonly List<int> _selectedHandIndices = new();

        private static readonly Dictionary<string, string> CardRules = new()
        {
            { "BURST", "Deal 10" },
            { "PING", "Deal 6" },
            { "SHIELD", "Gain +6 Buffer" },
            { "PATCH", "Heal +6" },
            { "JAM", "Enemy ATK -2" },
            { "TRACE", "Deal 4, +1 Compute" },
            { "CLEAN", "Heat -1" },
            { "CACHE", "+3 Compute" },
            { "SPLICE", "Deal 7, Heat +1" },
            { "SHIFT", "Deal 3, +3 Buffer" },
            { "PACKET", "Deal 5" },
            { "TOKENIZE", "Next turn +1 card" },
        };

        private void Start()
        {
            _ui = new UiFactory();
            _ui.EnsureUi();

            var app = GameApp.I;
            if (app == null) return;

            if (app.run == null || app.run.map == null || app.run.map.nodes == null || app.run.map.nodes.Count == 0)
                app.NewRun(unchecked((int)DateTime.UtcNow.Ticks));

            _mode = Mode.Map;
            Render();
        }

        private void Render()
{
_ui.ClearRoot();

var run = GameApp.I.run;
var p = run.player;

_ui.Title("RUN", 28);
_ui.Text($"HP {p.integrity}/{p.maxIntegrity} | Buffer {p.buffer} | Heat {p.heat} | Compute {p.compute}", 14);

if (_mode == Mode.Map) RenderMap();
else if (_mode == Mode.Combat) RenderCombat();
else RenderEnd();
}

        private void RenderMap()
        {
            var run = GameApp.I.run;
            var map = run.map;

            map.currentIndex = Mathf.Clamp(map.currentIndex, 0, map.nodes.Count - 1);
            _currentNode = map.nodes[map.currentIndex];

            EnsureOutgoingLinks(map, _currentNode);

            _ui.Text("Choose next node:", 14);
            _ui.Text($"Current: {_currentNode.type} (row {_currentNode.row + 1})", 14);
            _ui.Spacer(8);

            var list = _ui.Scroll(360);

            if (_currentNode.next == null || _currentNode.next.Count == 0)
            {
                _ui.Text(list, "No outgoing paths. Abandon Run and restart.", 14);
            }
            else
            {
                foreach (var nextIndex in _currentNode.next)
                {
                    if (nextIndex < 0 || nextIndex >= map.nodes.Count) continue;
                    var node = map.nodes[nextIndex];

                    _ui.Button(list, $"{node.type}  (row {node.row + 1}, lane {node.lane + 1})", () =>
                    {
                        map.currentIndex = node.index;
                        EnterNode(node);
                    }, 520);
                }
            }

            _ui.Spacer(8);

            var row = _ui.Row();
            _ui.Button(row, "Abandon Run", () =>
            {
                _mode = Mode.End;
                Render();
            }, 240);

            _ui.Button(row, "Back to Menu", () =>
            {
                GameApp.I.GoToMainMenu();
            }, 240);
        }

        private static void EnsureOutgoingLinks(MapState map, MapNode cur)
        {
            if (cur.next != null && cur.next.Count > 0) return;

            var targetRow = cur.row + 1;
            var candidates = new List<MapNode>(3);

            for (var i = 0; i < map.nodes.Count; i++)
            {
                var n = map.nodes[i];
                if (n.row != targetRow) continue;
                if (Math.Abs(n.lane - cur.lane) <= 1) candidates.Add(n);
            }

            if (candidates.Count == 0) return;

            cur.next ??= new List<int>(2);
            cur.next.Clear();

            candidates.Sort((a, b) => a.lane.CompareTo(b.lane));
            cur.next.Add(candidates[0].index);
            if (candidates.Count > 1) cur.next.Add(candidates[^1].index);
        }

        private void EnterNode(MapNode node)
        {
            if (node.type is NodeType.Fight or NodeType.Elite or NodeType.Boss)
            {
                StartCombat(node.type);
                return;
            }

            var run = GameApp.I.run;

            if (node.type == NodeType.Rest)
                run.player.integrity = Mathf.Min(run.player.maxIntegrity, run.player.integrity + 10);
            else if (node.type == NodeType.Shop)
                run.player.compute += 8;
            else if (node.type == NodeType.Event)
                run.player.heat = Mathf.Max(0, run.player.heat - 1);

            Render();
        }

        private void StartCombat(NodeType nodeType)
        {
            var app = GameApp.I;
            var run = app.run;
            var rng = app.rng;

            var enemy = new EnemyDefSO
            {
                displayName = nodeType.ToString(),
                baseMaxHp = nodeType == NodeType.Boss ? 70 : nodeType == NodeType.Elite ? 45 : 30,
                intents = new List<EnemyIntentDef>
                {
                    new EnemyIntentDef { description = "Attack", kind = EnemyIntentKind.Attack, basePower = nodeType == NodeType.Boss ? 10 : nodeType == NodeType.Elite ? 8 : 6 }
                }
            };

            var encounter = new Encounter
            {
                enemy = enemy,
                maxHp = enemy.baseMaxHp
            };

            var drifts = new List<DriftDefSO>(); // Populate with actual drifts if available
            var combatState = CombatSystem.StartCombat(app.content, ref rng, run, encounter, drifts);
            _combatState = combatState;

            _mode = Mode.Combat;
            RenderCombat();
        }
        private void DrawNewHand()
{
var run = GameApp.I.run;
var rng = GameApp.I.rng;

run.deck.EnsureFightReady(rng);

_hand.Clear();

var drawCount = 5 + Mathf.Max(0, run.player.extraDrawNextTurn);
run.player.extraDrawNextTurn = 0;

var cards = run.deck.DrawCards(rng, drawCount);

if (cards.Count == 0)
{
// Reshuffle discard into draw pile
run.deck.StartFight(rng);
cards = run.deck.DrawCards(rng, drawCount);
}

_hand.AddRange(cards);
}

        private void RenderCombat()
        {
            var run = GameApp.I.run;
            var p = run.player;

            if (_combatState == null)
            {
                _ui.Text("No active combat.", 14);
                return;
            }

            _ui.Title($"COMBAT: {_combatState.encounter.enemy.displayName}", 24);
            _ui.Text($"Enemy HP {_combatState.enemyHp}/{_combatState.encounter.maxHp} | Turn {run.player.turnsThisFight}", 14);
            _ui.Text($"Your Hand: select up to 3 cards, then COMPILE", 14);
            _ui.Text($"Deck {run.deck.All.Count} | Draw {run.deck.Draw.Count} | Discard {run.deck.Discard.Count}", 12);
            _ui.Spacer(8);

            var handBox = _ui.Scroll(300);

            if (_combatState.hand.Count == 0)
            {
                _ui.Text(handBox, "No cards drawn. Click DRAW HAND.", 14);
            }
            else
            {
                for (var i = 0; i < _combatState.hand.Count; i++)
                {
                    var idx = i;
                    var id = _combatState.hand[i];

                    var picked = _selectedHandIndices.Contains(idx);
                    var rule = CardRules.TryGetValue(id, out var r) ? r : "Effect TBD";
                    var label = (picked ? "[X] " : "[ ] ") + $"{id} — {rule}";

                    _ui.Button(handBox, label, () =>
                    {
                        if (_selectedHandIndices.Contains(idx)) _selectedHandIndices.Remove(idx);
                        else if (_selectedHandIndices.Count < 3) _selectedHandIndices.Add(idx);
                        RenderCombat();
                    }, 520);
                }
            }

            _ui.Spacer(8);

            var row = _ui.Row();

            _ui.Button(row, "DRAW HAND", () =>
            {
                _selectedHandIndices.Clear();
                DiscardHand();
                DrawNewHand();
                RenderCombat();
            }, 220);

            _ui.Button(row, "COMPILE", () =>
            {
                CombatSystem.PlayerCompile(GameApp.I.content, ref GameApp.I.rng, run, _combatState, _selectedHandIndices, out var compileResult);
                if (_combatState.isOver)
                {
                    if (_combatState.playerWon)
                    {
                        run.depthIndex++;
                        _combatState = null;
                        _mode = Mode.Map;
                        Render();
                    }
                    else
                    {
                        _combatState = null;
                        _mode = Mode.End;
                        Render();
                    }
                    return;
                }

                RenderCombat();
            }, 220);

            _ui.Button(row, "Flee (End)", () =>
            {
                _combatState = null;
                _mode = Mode.End;
                Render();
            }, 200);
        }
        private void DoPlayerTurn()
        {
            var run = GameApp.I.run;
            var p = run.player;

            var pickedCards = new List<string>();
            foreach (var idx in _selectedHandIndices)
            {
                if (idx < 0 || idx >= _hand.Count) continue;
                pickedCards.Add(_hand[idx]);
            }

            foreach (var card in pickedCards)
                ApplyCard(card, p);

            if (pickedCards.Count == 0)
                _enemyHp -= 2;

            _enemyHp = Mathf.Max(0, _enemyHp);
        }

        private void ApplyCard(string id, PlayerState p)
        {
            switch (id)
            {
                case "BURST": _enemyHp -= 10; break;
                case "PING": _enemyHp -= 6; break;
                case "SHIELD": p.buffer += 6; break;
                case "PATCH": p.integrity = Mathf.Min(p.maxIntegrity, p.integrity + 6); break;
                case "JAM": _enemyAttack = Mathf.Max(1, _enemyAttack - 2); break;
                case "TRACE": _enemyHp -= 4; p.compute += 1; break;
                case "CLEAN": p.heat = Mathf.Max(0, p.heat - 1); break;
                case "CACHE": p.compute += 3; break;
                case "SPLICE": _enemyHp -= 7; p.heat = Mathf.Min(10, p.heat + 1); break;
                case "SHIFT": p.buffer += 3; _enemyHp -= 3; break;
                case "PACKET": _enemyHp -= 5; break;
                case "TOKENIZE": p.extraDrawNextTurn += 1; break;
                default: _enemyHp -= 3; break;
            }
        }

        private void DoEnemyTurn()
        {
            var p = GameApp.I.run.player;

            var dmg = _enemyAttack;

            if (p.buffer > 0)
            {
                var absorbed = Mathf.Min(p.buffer, dmg);
                p.buffer -= absorbed;
                dmg -= absorbed;
            }

            if (dmg > 0)
                p.integrity -= dmg;
        }

        private void DiscardHand()
        {
            var run = GameApp.I.run;
            if (_hand.Count > 0)
                run.deck.DiscardMany(_hand);
            _hand.Clear();
        }

        private void RenderEnd()
        {
            var run = GameApp.I.run;

            _ui.Title("RUN END", 30);
            _ui.Text($"HP {run.player.integrity}/{run.player.maxIntegrity} | Depth {run.depthIndex}", 14);

            var row = _ui.Row();
            _ui.Button(row, "Main Menu", () => GameApp.I.GoToMainMenu(), 200);
            _ui.Button(row, "Restart (same seed)", () =>
            {
                GameApp.I.NewRun(run.seed);
                _mode = Mode.Map;
                Render();
            }, 260);
        }
    }
}
