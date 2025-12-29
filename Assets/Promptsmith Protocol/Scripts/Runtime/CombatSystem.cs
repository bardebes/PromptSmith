using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public static class CombatSystem
    {
        public static CombatState StartCombat(GameContentSO content, ref DeterministicRng rng, RunState run, Encounter enc, List<DriftDefSO> drifts)
        {
            var c = new CombatState
            {
                encounter = enc,
                drifts = drifts ?? new List<DriftDefSO>(),
                enemyHp = enc.maxHp,
                enemyShield = 0,
                enemyMarked = 0,
                enemyWeakTurns = 0,
                enemyDamageDownTurns = 0,
                enemyReflect = 0,
                intent = RollIntent(enc, ref rng),
            };

            run.player.turnsThisFight = 0;
            run.player.reflectSelf = 0;
            run.player.extraDrawNextTurn = 0;
            run.player.markedSelf = 0;
            run.player.weakTurnsSelf = 0;
            run.player.ignoreDriftChargesThisFight = 0;
            run.player.usedRollbackThisFight = false;

            // Plugins - start of fight
            ApplyStartFightPlugins(content, ref rng, run, c);

            // Drifts - start of fight
            foreach (var d in c.drifts)
            {
                if (d.kind == DriftEffectKind.StartWithWeak && run.player.ignoreDriftChargesThisFight <= 0)
                    run.player.weakTurnsSelf = Math.Max(run.player.weakTurnsSelf, d.value);
            }

            run.deck.StartFight(rng);

            // Draw initial hand
            var draw = 6 + run.player.extraDrawNextTurn;
            run.player.extraDrawNextTurn = 0;
            c.hand.AddRange(run.deck.DrawCards(rng, draw));

            // Boss reflect drift is applied dynamically in EndTurn
            c.log.Add($"Encounter: {enc.enemy.displayName} (HP {c.enemyHp})");
            if (c.drifts.Count > 0) c.log.Add($"Drift: {string.Join(" | ", c.drifts.ConvertAll(x => x.description))}");

            return c;
        }

        private static void ApplyStartFightPlugins(GameContentSO content, ref DeterministicRng rng, RunState run, CombatState c)
        {
            foreach (var pid in run.pluginIds)
            {
                var pl = FindPlugin(content, pid);
                if (pl == null) continue;

                switch (pl.kind)
                {
                    case PluginEffectKind.StartFightDrawPlus:
                        run.player.extraDrawNextTurn += pl.value;
                        break;
                    case PluginEffectKind.StartFightBufferFlat:
                        run.player.buffer += pl.value;
                        break;
                    case PluginEffectKind.EnemyStartsMarked:
                        c.enemyMarked += pl.value;
                        break;
                    case PluginEffectKind.IgnoreFirstDriftEachFight:
                        run.player.ignoreFirstDriftThisFight = true;
                        break;
                    case PluginEffectKind.StartFightTempUncommonInHand:
                        {
                            var pool = new List<KeywordDefSO>();
                            foreach (var k in content.keywords)
                                if (k != null && k.rarity == Rarity.Uncommon && MetaUnlocks.IsKeywordUnlocked(k))
                                    pool.Add(k);

                            if (pool.Count > 0)
                            {
                                var kw = pool[rng.NextInt(0, pool.Count)];
                                c.hand.Add(kw.id);
                                c.log.Add($"Plugin: Prompt Library added temporary {kw.displayName}.");
                            }
                        }
                        break;
                    case PluginEffectKind.OncePerFightRecompile:
                        run.player.recompileAvailableThisFight = true;
                        break;
                }
            }

            run.player.Clamp();
        }

        public static void PlayerCompile(GameContentSO content, ref DeterministicRng rng, RunState run, CombatState c, List<int> handIndicesChosen, out CompileResult compile)
        {
            compile = null;
            if (c.isOver) return;

            var chosenIds = new List<string>(3);
            var sorted = new List<int>(handIndicesChosen);
            sorted.Sort();

            foreach (var idx in sorted)
            {
                if (idx < 0 || idx >= c.hand.Count) continue;
                chosenIds.Add(c.hand[idx]);
            }

            if (chosenIds.Count != 3) throw new Exception("Must choose exactly 3 keywords.");

            // Track repeated keyword drift
            var repeatDrift = c.drifts.Find(d => d.kind == DriftEffectKind.RepeatKeywordCorruptsNextTurn);
            if (repeatDrift != null && run.player.ignoreDriftChargesThisFight <= 0)
            {
                // If any keyword repeats from last compile, mark it (simple: first in chosen)
                c.repeatedKeywordId = chosenIds[0];
            }

            compile = Compiler.Compile(content, rng, run, c, chosenIds);

            foreach (var line in compile.lines) c.log.Add(line);

            // Move chosen cards to discard (deckbuilder feel)
            var toDiscard = new List<string>();
            for (var i = sorted.Count - 1; i >= 0; i--)
            {
                var idx = sorted[i];
                if (idx < 0 || idx >= c.hand.Count) continue;
                toDiscard.Add(c.hand[idx]);
                c.hand.RemoveAt(idx);
            }
            run.deck.DiscardMany(toDiscard);

            // Resolve player actions
            ResolvePlayerActions(content, ref rng, run, c, compile.actions);

            if (!c.isOver)
            {
                // Enemy resolves
                ResolveEnemyTurn(content, ref rng, run, c);
            }

            if (!c.isOver)
            {
                EndTurn(content, ref rng, run, c);
            }
        }

        private static void ResolvePlayerActions(GameContentSO content, ref DeterministicRng rng, RunState run, CombatState c, List<CompiledAction> actions)
        {
            var heatBefore = run.player.heat;

            // Plugin: First turn power boost
            var firstTurnBoost = GetPluginValue(content, run, PluginEffectKind.FirstTurnPowerBoostPct);
            var dmgBoostHeat = run.player.heat >= 5 ? GetPluginValue(content, run, PluginEffectKind.HeatHighDamageBoostPct) : 0;

            for (var i = 0; i < actions.Count; i++)
            {
                var a = actions[i];

                // Drift: first action resolves last
                if (c.drifts.Exists(d => d.kind == DriftEffectKind.FirstActionResolvesLast) && run.player.ignoreDriftChargesThisFight <= 0)
                {
                    if (i == 0 && actions.Count > 1)
                    {
                        // postpone by swapping with last
                        var last = actions[^1];
                        actions[^1] = a;
                        actions[0] = last;
                        c.log.Add("Drift: first action resolved last.");
                    }
                }

                var power = a.power;

                if (run.player.turnsThisFight == 0 && firstTurnBoost > 0)
                    power = Mathf.CeilToInt(power * (1f + firstTurnBoost / 100f));

                if (a.kind == ActionKind.Damage && dmgBoostHeat > 0)
                    power = Mathf.CeilToInt(power * (1f + dmgBoostHeat / 100f));

                // Drift: start with corrupted in hand => apply -pct to one deterministic chosen slot on turn 0
                var corrupt = c.drifts.Find(d => d.kind == DriftEffectKind.StartWithCorruptedInHand);
                if (corrupt != null && run.player.turnsThisFight == 0 && run.player.ignoreDriftChargesThisFight <= 0)
                {
                    // deterministic: penalize action if (hash(sourceId) % 3 == 0)
                    if ((StableHash(a.sourceId) % 3) == 0)
                        power = Mathf.CeilToInt(power * 0.9f);
                }

                // Apply Heat delta early (so follow-up actions can reference)
                if (a.heatDelta != 0) run.player.heat += a.heatDelta;

                switch (a.kind)
                {
                    case ActionKind.Damage:
                        DealDamageToEnemy(run, c, power, a.multiHit ? a.hits : 1);
                        c.log.Add($"You dealt {power}{(a.multiHit ? $"x{a.hits}" : "")}.");
                        break;

                    case ActionKind.Shield:
                        power = ApplyShieldPenalty(c, run, power);
                        run.player.buffer += power;
                        c.log.Add($"You gained {power} Buffer.");
                        if (a.reflectValue > 0) run.player.reflectSelf = Math.Max(run.player.reflectSelf, a.reflectValue);
                        break;

                    case ActionKind.Heal:
                        power = ApplyHealingPenalty(c, run, power);
                        if (a.sourceId == "DRAIN")
                        {
                            DealDamageToEnemy(run, c, power, 1);
                            c.log.Add($"Drain dealt {power}.");
                        }
                        run.player.integrity = Mathf.Min(run.player.maxIntegrity, run.player.integrity + power);
                        c.log.Add($"You healed {power}.");
                        ApplyHealAlsoHealsEnemy(c, run);
                        break;

                    case ActionKind.ApplyMarked:
                        c.enemyMarked += Math.Max(1, a.markedStacks);
                        c.log.Add($"Enemy Marked +{Math.Max(1, a.markedStacks)}.");
                        break;

                    case ActionKind.ApplyWeak:
                        if (c.drifts.Exists(d => d.kind == DriftEffectKind.EnemyImmuneWeak) && run.player.ignoreDriftChargesThisFight <= 0)
                            c.log.Add("Enemy resisted Weak.");
                        else
                        {
                            c.enemyWeakTurns = Math.Max(c.enemyWeakTurns, Math.Max(1, a.weakTurns));
                            c.log.Add("Enemy Weak (1 turn).");
                        }
                        break;

                    case ActionKind.ReduceEnemyDamageNextPct:
                        c.enemyDamageDownTurns = Math.Max(c.enemyDamageDownTurns, 1);
                        c.log.Add($"Enemy damage -{a.reduceEnemyDamagePct}% next turn.");
                        break;

                    case ActionKind.CleanseSelf:
                        run.player.weakTurnsSelf = 0;
                        run.player.markedSelf = 0;
                        c.log.Add("You cleansed debuffs.");
                        break;

                    case ActionKind.Draw:
                        {
                            // draw now (not next turn)
                            var extra = 0;
                            if (a.sourceId is "CACHE" or "CACHEPLUS")
                            {
                                // plugin: cache first time extra draw
                                if (GetPluginValue(content, run, PluginEffectKind.CacheFirstTimeExtraDraw) > 0)
                                    extra += 1;
                            }

                            var drawn = run.deck.DrawCards(rng, a.draw + extra);
                            c.hand.AddRange(drawn);
                            c.log.Add($"You drew {drawn.Count}.");

                            // Drift: draw gives enemy bonus damage
                            var drawPenalty = c.drifts.Find(d => d.kind == DriftEffectKind.DrawGivesEnemyBonusDamage);
                            if (drawPenalty != null && run.player.ignoreDriftChargesThisFight <= 0)
                                c.intent.basePower += drawPenalty.value;
                        }
                        break;

                    case ActionKind.GainCompute:
                        run.player.compute += Math.Max(0, a.computeGain);
                        c.log.Add($"Compute +{Math.Max(0, a.computeGain)}.");
                        break;

                    case ActionKind.HeatDelta:
                        run.player.heat += a.heatDelta;
                        if (a.power > 0) run.player.buffer += ApplyShieldPenalty(c, run, a.power);
                        c.log.Add($"Heat {a.heatDelta}.");
                        break;

                    case ActionKind.IgnoreDriftCharges:
                        run.player.ignoreDriftChargesThisFight += Math.Max(0, a.ignoreDriftCharges);
                        c.log.Add($"Ignore drift charges +{Math.Max(0, a.ignoreDriftCharges)}.");
                        break;

                    case ActionKind.Rollback:
                        if (!run.player.usedRollbackThisFight)
                        {
                            // Safe approximation: restore 10 integrity (tracks last damage in full version)
                            run.player.integrity = Mathf.Min(run.player.maxIntegrity, run.player.integrity + 10);
                            run.player.usedRollbackThisFight = true;
                            c.log.Add("Rollback restored 10 integrity.");
                        }
                        break;

                    case ActionKind.HyperthreadTurns:
                        // Not fully implemented in UI in this sample; store as extraDrawNextTurn for feel
                        run.player.extraDrawNextTurn += 1;
                        c.log.Add("Hyperthread: +1 extra draw next turn (UI simplification).");
                        break;

                    case ActionKind.SetOracleIntent:
                        // UI flow should allow selection; here we simply reroll to a favorable non-debuff if possible
                        c.intent = RollIntentPreferNonDebuff(c.encounter, ref rng);
                        c.log.Add("Oracle: intent influenced.");
                        break;
                }

                // Plugin: apply debuff gives compute
                if (a.kind is ActionKind.ApplyMarked or ActionKind.ApplyWeak)
                {
                    var tele = GetPluginValue(content, run, PluginEffectKind.ApplyDebuffGivesCompute);
                    if (tele > 0) run.player.compute += tele;
                }

                // Enemy reflect drift (below 30%) handled in EndTurn; enemy reflect passive handled on enemy side if you want later
            }

            // Plugin: Heat gain grants buffer
            var heatDelta = run.player.heat - heatBefore;
            if (heatDelta > 0)
            {
                var v = GetPluginValue(content, run, PluginEffectKind.HeatGainGrantsBuffer);
                if (v > 0) run.player.buffer += v * heatDelta;
            }

            // Check win
            if (c.enemyHp <= 0)
            {
                c.isOver = true;
                c.playerWon = true;
                c.log.Add("Enemy defeated.");
            }

            run.player.Clamp();
        }

        private static void ResolveEnemyTurn(GameContentSO content, ref DeterministicRng rng, RunState run, CombatState c)
        {
            if (c.isOver) return;

            var intent = c.intent;
            var power = intent.basePower;

            // Drift: enemy intent boost
            var boost = c.drifts.Find(d => d.kind == DriftEffectKind.EnemyIntentBoostPct);
            if (boost != null && run.player.ignoreDriftChargesThisFight <= 0)
                power = Mathf.CeilToInt(power * (1f + boost.value / 100f));

            // Enemy damage down
            if (c.enemyDamageDownTurns > 0)
            {
                power = Mathf.CeilToInt(power * 0.75f);
                c.enemyDamageDownTurns--;
            }

            // Enemy weak
            if (c.enemyWeakTurns > 0)
            {
                power = Mathf.CeilToInt(power * 0.80f);
                c.enemyWeakTurns--;
            }

            // Plugin: buffer high reduces enemy damage
            var bufDebuff = GetPluginValue(content, run, PluginEffectKind.BufferHighEnemyDamagePenaltyPct);
            if (bufDebuff > 0 && run.player.buffer > 10)
                power = Mathf.CeilToInt(power * (1f - bufDebuff / 100f));

            switch (intent.kind)
            {
                case EnemyIntentKind.Attack:
                case EnemyIntentKind.Mixed:
                    DealDamageToPlayer(run, c, power, intent.multiHit ? intent.hits : 1);
                    c.log.Add($"Enemy used {intent.description} for {power}{(intent.multiHit ? $"x{intent.hits}" : "")}.");
                    break;

                case EnemyIntentKind.Shield:
                    c.enemyShield += power;
                    c.log.Add($"Enemy gained shield +{power}.");
                    break;

                case EnemyIntentKind.Heal:
                    c.enemyHp = Mathf.Min(c.encounter.maxHp, c.enemyHp + power);
                    c.log.Add($"Enemy healed {power}.");
                    break;

                case EnemyIntentKind.Debuff:
                    if (intent.appliesWeak) run.player.weakTurnsSelf = Math.Max(run.player.weakTurnsSelf, 1);
                    if (intent.appliesMarked) run.player.markedSelf += 1;
                    c.log.Add($"Enemy debuff: {(intent.appliesWeak ? "Weak " : "")}{(intent.appliesMarked ? "Marked " : "")}");
                    break;
            }

            // Player reflect on enemy attack
            if (run.player.reflectSelf > 0 && (intent.kind == EnemyIntentKind.Attack || intent.kind == EnemyIntentKind.Mixed))
            {
                c.enemyHp -= run.player.reflectSelf;
                c.log.Add($"Reflect dealt {run.player.reflectSelf} to enemy.");
                if (c.enemyHp <= 0)
                {
                    c.isOver = true;
                    c.playerWon = true;
                    c.log.Add("Enemy defeated (reflect).");
                }
            }

            if (run.player.integrity <= 0)
            {
                c.isOver = true;
                c.playerWon = false;
                c.log.Add("You were defeated.");
            }

            run.player.Clamp();
        }

        public static void EndTurn(GameContentSO content, ref DeterministicRng rng, RunState run, CombatState c)
        {
            if (c.isOver) return;

            run.player.turnsThisFight++;

            // Drift: end turn heat +1
            if (c.drifts.Exists(d => d.kind == DriftEffectKind.EndTurnHeatPlusOne) && run.player.ignoreDriftChargesThisFight <= 0)
                run.player.heat += 1;

            // Reduce player debuffs
            if (run.player.weakTurnsSelf > 0) run.player.weakTurnsSelf--;

            // Plugin: every 3 turns cleanse
            var gc = GetPluginValue(content, run, PluginEffectKind.EveryThreeTurnsCleanse);
            if (gc > 0 && (run.player.turnsThisFight % 3) == 0)
            {
                run.player.weakTurnsSelf = 0;
                run.player.markedSelf = 0;
                c.log.Add("Garbage Collector cleansed debuffs.");
            }

            // Drift: enemy reflect below 30%
            var dref = c.drifts.Find(d => d.kind == DriftEffectKind.EnemyBelow30Reflect);
            if (dref != null && run.player.ignoreDriftChargesThisFight <= 0 && c.enemyHp <= Mathf.CeilToInt(c.encounter.maxHp * 0.30f))
                c.enemyReflect = dref.value;
            else
                c.enemyReflect = 0;

            // Tick cooldowns
            var keys = new List<string>(c.cooldowns.Keys);
            foreach (var k in keys)
            {
                c.cooldowns[k] = Math.Max(0, c.cooldowns[k] - 1);
                if (c.cooldowns[k] <= 0) c.cooldowns.Remove(k);
            }

            // Draw up to hand size
            var targetHand = 6;
            var draw = Math.Max(0, targetHand - c.hand.Count) + run.player.extraDrawNextTurn;
            run.player.extraDrawNextTurn = 0;

            if (draw > 0)
                c.hand.AddRange(run.deck.DrawCards(rng, draw));

            // Roll next intent
            c.intent = RollIntent(c.encounter, ref rng);

            // Consume ignore drift charges gradually (prevents infinite)
            if (run.player.ignoreDriftChargesThisFight > 0) run.player.ignoreDriftChargesThisFight = Math.Max(0, run.player.ignoreDriftChargesThisFight - 1);

            run.player.Clamp();
        }

        private static void DealDamageToEnemy(RunState run, CombatState c, int dmg, int hits)
        {
            for (var i = 0; i < Math.Max(1, hits); i++)
            {
                var amount = dmg;

                if (c.enemyMarked > 0)
                {
                    amount = Mathf.CeilToInt(amount * 1.25f);
                    c.enemyMarked = Math.Max(0, c.enemyMarked - 1);
                }

                if (c.enemyShield > 0)
                {
                    var absorbed = Math.Min(c.enemyShield, amount);
                    c.enemyShield -= absorbed;
                    amount -= absorbed;
                }

                if (amount > 0) c.enemyHp -= amount;

                if (c.enemyReflect > 0)
                {
                    DealDamageToPlayer(run, c, c.enemyReflect, 1);
                    c.log.Add($"Enemy reflect hit you for {c.enemyReflect}.");
                }
            }
        }

        private static void DealDamageToPlayer(RunState run, CombatState c, int dmg, int hits)
        {
            for (var i = 0; i < Math.Max(1, hits); i++)
            {
                var amount = dmg;

                // Drift: no buffer extra damage
                var noBuf = c.drifts.Find(d => d.kind == DriftEffectKind.NoBufferExtraDamageFlat);
                if (noBuf != null && run.player.ignoreDriftChargesThisFight <= 0 && run.player.buffer <= 0)
                    amount += noBuf.value;

                // Drift: marked affects both sides
                if (c.drifts.Exists(d => d.kind == DriftEffectKind.MarkedAffectsBothSides) && run.player.ignoreDriftChargesThisFight <= 0 && run.player.markedSelf > 0)
                {
                    amount = Mathf.CeilToInt(amount * 1.25f);
                    run.player.markedSelf = Math.Max(0, run.player.markedSelf - 1);
                }

                // Absorb buffer
                if (run.player.buffer > 0)
                {
                    var absorbed = Math.Min(run.player.buffer, amount);
                    run.player.buffer -= absorbed;
                    amount -= absorbed;
                }

                if (amount > 0) run.player.integrity -= amount;
            }

            run.player.Clamp();
        }

        private static EnemyIntentDef RollIntent(Encounter enc, ref DeterministicRng rng)
        {
            var e = enc.enemy;
            var pick = e.intents[rng.NextInt(0, e.intents.Count)];

            if (e.passiveKind == EnemyPassiveKind.LiesSometimes && rng.NextDouble() < 0.25)
            {
                return new EnemyIntentDef
                {
                    id = pick.id,
                    description = "?? (hallucinated intent)",
                    kind = pick.kind,
                    basePower = pick.basePower,
                    appliesJam = pick.appliesJam,
                    appliesMarked = pick.appliesMarked,
                    appliesWeak = pick.appliesWeak,
                    grantsShield = pick.grantsShield,
                    healsSelf = pick.healsSelf,
                    multiHit = pick.multiHit,
                    hits = pick.hits
                };
            }

            return CloneIntent(pick);
        }

        private static EnemyIntentDef RollIntentPreferNonDebuff(Encounter enc, ref DeterministicRng rng)
        {
            var list = new List<EnemyIntentDef>();
            foreach (var it in enc.enemy.intents)
                if (it.kind != EnemyIntentKind.Debuff) list.Add(it);

            if (list.Count == 0) return RollIntent(enc, ref rng);
            return CloneIntent(list[rng.NextInt(0, list.Count)]);
        }

        private static EnemyIntentDef CloneIntent(EnemyIntentDef it)
        {
            return new EnemyIntentDef
            {
                id = it.id,
                description = it.description,
                kind = it.kind,
                basePower = it.basePower,
                appliesJam = it.appliesJam,
                appliesMarked = it.appliesMarked,
                appliesWeak = it.appliesWeak,
                grantsShield = it.grantsShield,
                healsSelf = it.healsSelf,
                multiHit = it.multiHit,
                hits = it.hits
            };
        }

        private static int ApplyHealingPenalty(CombatState c, RunState run, int heal)
        {
            var d = c.drifts.Find(x => x.kind == DriftEffectKind.HealingPenaltyPct);
            if (d == null || run.player.ignoreDriftChargesThisFight > 0) return heal;
            return Mathf.CeilToInt(heal * (1f - d.value / 100f));
        }

        private static int ApplyShieldPenalty(CombatState c, RunState run, int shield)
        {
            var d = c.drifts.Find(x => x.kind == DriftEffectKind.ShieldPenaltyPct);
            if (d == null || run.player.ignoreDriftChargesThisFight > 0) return shield;
            return Mathf.CeilToInt(shield * (1f - d.value / 100f));
        }

        private static void ApplyHealAlsoHealsEnemy(CombatState c, RunState run)
        {
            var d = c.drifts.Find(x => x.kind == DriftEffectKind.HealAlsoHealsEnemyFlat);
            if (d == null || run.player.ignoreDriftChargesThisFight > 0) return;
            c.enemyHp = Mathf.Min(c.encounter.maxHp, c.enemyHp + d.value);
            c.log.Add("Drift: enemy healed due to your heal.");
        }

        private static PluginDefSO FindPlugin(GameContentSO content, string id)
        {
            foreach (var p in content.plugins)
                if (p != null && p.id == id) return p;
            return null;
        }

        private static int GetPluginValue(GameContentSO content, RunState run, PluginEffectKind kind)
        {
            var sum = 0;
            foreach (var pid in run.pluginIds)
            {
                var p = FindPlugin(content, pid);
                if (p != null && p.kind == kind) sum += p.value;
            }
            return sum;
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                var h = 23;
                for (var i = 0; i < s.Length; i++) h = h * 31 + s[i];
                return h;
            }
        }
    }
}
