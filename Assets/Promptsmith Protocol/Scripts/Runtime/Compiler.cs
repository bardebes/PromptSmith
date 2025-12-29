using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public static class Compiler
    {
        public static CompileResult Compile(GameContentSO content, DeterministicRng rng, RunState run, CombatState c, List<string> chosen3)
        {
            var res = new CompileResult();
            c.compileCount++;

            var prompt = new List<string>(chosen3);

            // Drift: second keyword duplicated
            if (HasDrift(c, DriftEffectKind.SecondKeywordDuplicated) && prompt.Count >= 2)
            {
                res.lines.Add("Drift: second keyword duplicated.");
                prompt.Insert(2, prompt[1]);
            }

            // Drift: first keyword ignored (unless player has ignore charges)
            if (HasDrift(c, DriftEffectKind.FirstKeywordIgnored) && run.player.ignoreDriftChargesThisFight <= 0)
            {
                res.lines.Add("Drift: first keyword ignored.");
                if (prompt.Count > 0) prompt[0] = "NULL";
            }

            // Cooldowns / once-per-fight enforcement
            for (var i = 0; i < prompt.Count; i++)
            {
                if (!TryConsumeKeywordUse(content, c, prompt[i], out var reason))
                {
                    res.lines.Add($"Rule: {reason} -> {prompt[i]} becomes NULL.");
                    prompt[i] = "NULL";
                }
            }

            // Apply operators L->R
            var opsNegated = HasDrift(c, DriftEffectKind.FirstOperatorNegated) && run.player.ignoreDriftChargesThisFight <= 0;
            var firstOpNegatedUsed = false;

            var working = new List<string>(prompt);
            working = working.Count > 4 ? working.GetRange(0, 4) : working;

            for (var i = 0; i < working.Count; i++)
            {
                var id = working[i];
                if (!content.TryGetKeyword(id, out var kw)) continue;
                if (kw.keywordType != KeywordType.Operator) continue;

                if (opsNegated && !firstOpNegatedUsed)
                {
                    res.lines.Add("Drift: first operator negated.");
                    firstOpNegatedUsed = true;
                    continue;
                }

                firstOpNegatedUsed = true;

                switch (kw.operatorKind)
                {
                    case OperatorKind.Splice:
                        if (i + 1 < working.Count && working.Count < 4)
                        {
                            working.Insert(i + 1, working[i + 1]);
                            res.lines.Add("Operator: Splice duplicated next keyword.");
                        }
                        break;

                    case OperatorKind.Shift:
                        if (i + 2 < working.Count)
                        {
                            (working[i + 1], working[i + 2]) = (working[i + 2], working[i + 1]);
                            res.lines.Add("Operator: Shift swapped next two keywords.");
                        }
                        break;

                    case OperatorKind.Invert:
                        if (i + 1 < working.Count)
                        {
                            working[i + 1] = Invert(content, working[i + 1]);
                            res.lines.Add("Operator: Invert flipped next keyword.");
                        }
                        break;

                    case OperatorKind.Mutate:
                        if (i + 1 < working.Count)
                        {
                            working[i + 1] = Mutate(content, rng, working[i + 1]);
                            res.lines.Add("Operator: Mutate rerolled next keyword.");
                        }
                        break;

                    case OperatorKind.Echo:
                        res.lines.Add("Operator: Echo will repeat last eligible action at 50%.");
                        break;

                    case OperatorKind.Compress:
                        res.lines.Add("Operator: Compress will merge first two eligible actions.");
                        break;
                }
            }

            if (working.Count > 4) working = working.GetRange(0, 4);
            res.promptAfterOps.AddRange(working);

            // Translate to actions
            var raw = new List<CompiledAction>();
            foreach (var id in working)
            {
                if (!content.TryGetKeyword(id, out var kw)) continue;
                if (kw.keywordType == KeywordType.Operator || kw.keywordType == KeywordType.Passive) continue;

                var a = Translate(content, rng, run, c, kw);
                if (a != null) raw.Add(a);
            }

            // Synergies
            ApplySynergies(content, run, c, working, raw, res.lines);

            // Post-pass: compress/echo
            ApplyCompressAndEcho(content, run, c, working, raw, res.lines);

            // Drift: 3 actions adds heat
            if (HasDrift(c, DriftEffectKind.ThreeActionsAddsHeat) && run.player.ignoreDriftChargesThisFight <= 0)
            {
                var actionsCount = CountActionKeywords(content, working);
                if (actionsCount >= 3)
                {
                    run.player.heat += 1;
                    res.lines.Add("Drift: compiling 3 actions added +1 Heat.");
                }
            }

            // Drift: operator gives enemy shield
            if (HasDrift(c, DriftEffectKind.OperatorGivesEnemyShield) && run.player.ignoreDriftChargesThisFight <= 0)
            {
                if (CountOperatorKeywords(content, working) > 0)
                {
                    var v = c.drifts.Find(d => d.kind == DriftEffectKind.OperatorGivesEnemyShield).value;
                    c.enemyShield += v;
                    res.lines.Add($"Drift: operator granted enemy shield +{v}.");
                }
            }

            // Cap to 4 actions
            if (raw.Count > 4)
            {
                res.lines.Add("Rule: action cap reached (4).");
                raw = raw.GetRange(0, 4);
            }

            res.actions = raw;
            run.player.Clamp();
            return res;
        }

        private static bool TryConsumeKeywordUse(GameContentSO content, CombatState c, string id, out string reason)
        {
            reason = "";
            if (!content.TryGetKeyword(id, out var kw)) return true; // allow NULL

            // once-per-fight
            if (kw.oncePerFight && c.oncePerFightUsed.Contains(id))
            {
                reason = "once-per-fight already used";
                return false;
            }

            // cooldown
            if (kw.cooldownTurns > 0 && c.cooldowns.TryGetValue(id, out var cd) && cd > 0)
            {
                reason = $"on cooldown ({cd})";
                return false;
            }

            // consume
            if (kw.oncePerFight) c.oncePerFightUsed.Add(id);
            if (kw.cooldownTurns > 0) c.cooldowns[id] = kw.cooldownTurns;

            return true;
        }

        private static string Mutate(GameContentSO content, DeterministicRng rng, string targetId)
        {
            if (!content.TryGetKeyword(targetId, out var target)) return targetId;
            var pool = new List<KeywordDefSO>();
            foreach (var k in content.keywords)
                if (k != null && k.rarity == target.rarity && MetaUnlocks.IsKeywordUnlocked(k))
                    pool.Add(k);

            if (pool.Count == 0) return targetId;
            return pool[rng.NextInt(0, pool.Count)].id;
        }

        private static string Invert(GameContentSO content, string id)
        {
            return id switch
            {
                "BURST" => "SHIELD",
                "SHIELD" => "BURST",
                "PATCH" => "DRAIN",
                "DRAIN" => "PATCH",
                _ => id
            };
        }

        private static void ApplySynergies(GameContentSO content, RunState run, CombatState c, List<string> prompt, List<CompiledAction> actions, List<string> lines)
        {
            // Example: Firewall + Shield => reflect
            if (prompt.Contains("FIREWALL") && prompt.Contains("SHIELD"))
            {
                run.player.reflectSelf = Math.Max(run.player.reflectSelf, 2);
                lines.Add("Synergy: Firewall+Shield -> Reflect 2 (this turn).");
            }

            // Double Burst => +20% damage and +1 heat
            var burstCount = 0;
            foreach (var id in prompt) if (id is "BURST" or "BURST_PLUS") burstCount++;
            if (burstCount >= 2)
            {
                foreach (var a in actions)
                    if (a.kind == ActionKind.Damage) a.power = Mathf.CeilToInt(a.power * 1.2f);

                run.player.heat += 1;
                lines.Add("Synergy: Double Burst -> +20% damage, Heat +1.");
            }

            // Trace + Tokenize -> +1 marked
            if (prompt.Contains("TRACE") && prompt.Contains("TOKENIZE"))
            {
                c.enemyMarked += 1;
                lines.Add("Synergy: Trace+Tokenize -> enemy Marked +1.");
            }
        }

        private static void ApplyCompressAndEcho(GameContentSO content, RunState run, CombatState c, List<string> prompt, List<CompiledAction> actions, List<string> lines)
        {
            var opPenalty = c.drifts.Find(d => d.kind == DriftEffectKind.OperatorPenaltyPct);
            var hasCompress = prompt.Contains("COMPRESS") || prompt.Contains("COMPRESS_LITE");
            var hasEcho = prompt.Contains("ECHO") || prompt.Contains("ECHO_LITE");

            if (hasCompress && actions.Count >= 2)
            {
                var pct = prompt.Contains("COMPRESS") ? 170 : 140;
                if (opPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
                    pct = Mathf.CeilToInt(pct * (1f - opPenalty.value / 100f));

                var a0 = actions[0];
                var a1 = actions[1];

                if (a0.kind == a1.kind && (a0.kind == ActionKind.Damage || a0.kind == ActionKind.Shield || a0.kind == ActionKind.Heal))
                {
                    var merged = Mathf.CeilToInt((a0.power + a1.power) * (pct / 100f));
                    a0.power = merged;
                    actions.RemoveAt(1);
                    lines.Add($"Operator: Compress merged first two actions at {pct}%.");
                }
            }

            if (hasEcho)
            {
                var echoPct = 50;
                if (opPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
                    echoPct = Mathf.CeilToInt(echoPct * (1f - opPenalty.value / 100f));

                // Find last eligible action (damage/shield/heal)
                for (var i = actions.Count - 1; i >= 0; i--)
                {
                    var last = actions[i];
                    if (last.kind is ActionKind.Damage or ActionKind.Shield or ActionKind.Heal)
                    {
                        actions.Add(new CompiledAction
                        {
                            kind = last.kind,
                            power = Mathf.CeilToInt(last.power * (echoPct / 100f)),
                            sourceId = last.sourceId
                        });
                        lines.Add($"Operator: Echo repeated last action at {echoPct}%.");
                        break;
                    }
                }
            }
        }

        private static CompiledAction Translate(GameContentSO content, DeterministicRng rng, RunState run, CombatState c, KeywordDefSO kw)
        {
            var act = new CompiledAction { kind = kw.actionKind, power = kw.basePower, sourceId = kw.id };

            // Drift: start with corrupted in hand => first turn random card -pct
            if (HasDrift(c, DriftEffectKind.StartWithCorruptedInHand) && run.player.turnsThisFight == 0 && run.player.ignoreDriftChargesThisFight <= 0)
            {
                if (c.firstTurnRandomPenaltyIndex < 0)
                    c.firstTurnRandomPenaltyIndex = rng.NextInt(0, 999999); // resolved later during action apply

                // We apply penalty at resolution if it matches the chosen slot, keeping it deterministic
            }

            // Drift: random hand keyword penalty pct
            var randomPenalty = c.drifts.Find(d => d.kind == DriftEffectKind.RandomHandKeywordPenaltyPct);
            var repeatPenalty = c.drifts.Find(d => d.kind == DriftEffectKind.RepeatKeywordCorruptsNextTurn);

            if (repeatPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
            {
                // If repeated keyword selected consecutively, apply penalty.
                if (!string.IsNullOrEmpty(c.repeatedKeywordId) && c.repeatedKeywordId == kw.id)
                {
                    var pct = repeatPenalty.value;
                    act.power = Mathf.CeilToInt(act.power * (1f - pct / 100f));
                }
            }

            switch (kw.id)
            {
                case "PING":
                    act.kind = ActionKind.Damage;
                    act.power = kw.basePower;
                    act.draw = 1;
                    break;

                case "PACKET":
                    act.kind = ActionKind.Draw;
                    act.draw = 1;
                    break;

                case "CACHE":
                    act.kind = ActionKind.Draw;
                    act.draw = 0;
                    run.player.extraDrawNextTurn += 1;
                    break;

                case "CACHEPLUS":
                    act.kind = ActionKind.Draw;
                    act.draw = 0;
                    run.player.extraDrawNextTurn += 2;
                    break;

                case "SEQUENCE":
                    act.kind = ActionKind.Draw;
                    act.draw = 2;
                    break;

                case "TOKENIZE":
                    act.kind = ActionKind.Damage;
                    act.multiHit = true;
                    act.hits = 2;
                    break;

                case "TRACE":
                    act.kind = ActionKind.ApplyMarked;
                    act.markedStacks = 1;
                    break;

                case "TRACEPLUS":
                    act.kind = ActionKind.ApplyMarked;
                    act.markedStacks = 2;
                    break;

                case "SNIFF":
                    act.kind = ActionKind.ApplyWeak;
                    act.weakTurns = 1;
                    break;

                case "JAM":
                    act.kind = ActionKind.ReduceEnemyDamageNextPct;
                    act.reduceEnemyDamagePct = 25;
                    break;

                case "JAMPLUS":
                    act.kind = ActionKind.ReduceEnemyDamageNextPct;
                    act.reduceEnemyDamagePct = 35;
                    act.weakTurns = 1;
                    break;

                case "FIREWALL":
                    act.kind = ActionKind.Shield;
                    act.power = 8;
                    act.reflectValue = 2;
                    break;

                case "DRAIN":
                    // handled in resolution: damage + heal
                    act.kind = ActionKind.Heal;
                    act.power = 6;
                    break;

                case "OVERLOAD":
                    act.kind = ActionKind.Damage;
                    act.power = 18;
                    act.heatDelta = +2;
                    break;

                case "THROTTLE":
                    act.kind = ActionKind.Shield;
                    act.power = 4;
                    act.heatDelta = -1;
                    break;

                case "STABILIZE":
                    act.kind = ActionKind.HeatDelta;
                    act.heatDelta = -2;
                    act.power = 2; // buffer bonus
                    break;

                case "OVERHEAD":
                    act.kind = ActionKind.GainCompute;
                    act.computeGain = 2;
                    break;

                case "PROBE":
                    act.kind = ActionKind.GainCompute;
                    act.computeGain = 1;
                    break;

                case "SANDBOX":
                    act.kind = ActionKind.IgnoreDriftCharges;
                    act.ignoreDriftCharges = 1;
                    break;

                case "JAILBREAK":
                    act.kind = ActionKind.IgnoreDriftCharges;
                    act.ignoreDriftCharges = 2;
                    act.heatDelta = +1;
                    break;

                case "ROLLBACK":
                    act.kind = ActionKind.Rollback;
                    act.rollback = true;
                    break;

                case "LATENT_BURST":
                    act.kind = ActionKind.Damage;
                    act.power = run.player.heat >= 5 ? 20 : 10;
                    break;

                case "BLACKBOX":
                    // implemented as +25% action power this turn but hide intent in UI layer if desired
                    act.kind = ActionKind.Damage;
                    act.power = 10;
                    break;

                case "HYPERTHREAD":
                    act.kind = ActionKind.HyperthreadTurns;
                    act.hyperthreadTurns = 2;
                    break;

                case "ORACLE":
                    act.kind = ActionKind.SetOracleIntent;
                    act.setOracle = true;
                    break;
            }

            // Drift: single-hit damage penalty
            var singleHitPenalty = c.drifts.Find(d => d.kind == DriftEffectKind.SingleHitDamagePenaltyPct);
            if (act.kind == ActionKind.Damage && !act.multiHit && singleHitPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
                act.power = Mathf.CeilToInt(act.power * (1f - singleHitPenalty.value / 100f));

            // Apply random hand penalty occasionally (deterministic via rng)
            if (randomPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
            {
                if (rng.NextDouble() < 0.25)
                    act.power = Mathf.CeilToInt(act.power * (1f - randomPenalty.value / 100f));
            }

            return act;
        }

        private static bool HasDrift(CombatState c, DriftEffectKind kind)
        {
            foreach (var d in c.drifts) if (d.kind == kind) return true;
            return false;
        }

        private static int CountActionKeywords(GameContentSO content, List<string> ids)
        {
            var n = 0;
            foreach (var id in ids)
            {
                if (!content.TryGetKeyword(id, out var kw)) continue;
                if (kw.keywordType == KeywordType.Action || kw.keywordType == KeywordType.Utility) n++;
            }
            return n;
        }

        private static int CountOperatorKeywords(GameContentSO content, List<string> ids)
        {
            var n = 0;
            foreach (var id in ids)
            {
                if (!content.TryGetKeyword(id, out var kw)) continue;
                if (kw.keywordType == KeywordType.Operator) n++;
            }
            return n;
        }
    }

    public static class GameContentExtensions
    {
        public static bool TryGetKeyword(this GameContentSO content, string id, out KeywordDefSO kw)
        {
            kw = null;
            if (content == null || string.IsNullOrEmpty(id)) return false;
            foreach (var k in content.keywords)
            {
                if (k != null && k.id == id) { kw = k; return true; }
            }
            return false;
        }
    }
}
