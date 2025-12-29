#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PromptsmithProtocol.Editor
{
    public static class PromptsmithSetupWizard
    {
        private const string Root = "Assets/PromptsmithProtocol";
        private const string DataPath = Root + "/DataAssets";
        private const string ScenesPath = Root + "/Scenes";

        [MenuItem("Promptsmith/Setup Project")]
        public static void Setup()
        {
            EnsureFolder(Root);
            EnsureFolder(DataPath);
            EnsureFolder(ScenesPath);

            var content = CreateOrLoadContent();
            PopulateDefaultContent(content);

            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateScenes(content);

            UnityEditor.EditorBuildSettings.scenes = new[]
            {
                new UnityEditor.EditorBuildSettingsScene(ScenesPath + "/Promptsmith_MainMenu.unity", true),
                new UnityEditor.EditorBuildSettingsScene(ScenesPath + "/Promptsmith_Run.unity", true),
            };

            Debug.Log("Promptsmith setup complete. Open Promptsmith_MainMenu scene and press Play.");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static GameContentSO CreateOrLoadContent()
        {
            var assetPath = DataPath + "/GameContent.asset";
            var content = AssetDatabase.LoadAssetAtPath<GameContentSO>(assetPath);
            if (content != null) return content;

            content = ScriptableObject.CreateInstance<GameContentSO>();
            AssetDatabase.CreateAsset(content, assetPath);
            return content;
        }

        private static void PopulateDefaultContent(GameContentSO content)
        {
            content.keywords.Clear();
            content.drifts.Clear();
            content.plugins.Clear();
            content.enemies.Clear();
            content.events.Clear();

            // -------- Keywords (subset of bible; extend freely)
            AddKW(content, "BURST", "Burst", Rarity.Common, KeywordType.Action, ActionKind.Damage, OperatorKind.None, 8, KeywordTag.Attack, 0);
            AddKW(content, "PING", "Ping", Rarity.Common, KeywordType.Action, ActionKind.Damage, OperatorKind.None, 4, KeywordTag.Attack | KeywordTag.Utility, 0);
            AddKW(content, "SHIELD", "Shield", Rarity.Common, KeywordType.Action, ActionKind.Shield, OperatorKind.None, 6, KeywordTag.Defense, 0);
            AddKW(content, "PATCH", "Patch", Rarity.Common, KeywordType.Action, ActionKind.Heal, OperatorKind.None, 5, KeywordTag.Heal, 0);
            AddKW(content, "JAM", "Jam", Rarity.Common, KeywordType.Action, ActionKind.ReduceEnemyDamageNextPct, OperatorKind.None, 25, KeywordTag.Debuff, 0);
            AddKW(content, "TRACE", "Trace", Rarity.Common, KeywordType.Action, ActionKind.ApplyMarked, OperatorKind.None, 1, KeywordTag.Debuff, 0);
            AddKW(content, "CLEAN", "Clean", Rarity.Common, KeywordType.Action, ActionKind.CleanseSelf, OperatorKind.None, 1, KeywordTag.Utility, 0);
            AddKW(content, "CACHE", "Cache", Rarity.Common, KeywordType.Action, ActionKind.Draw, OperatorKind.None, 0, KeywordTag.Utility, 0);
            AddKW(content, "PACKET", "Packet", Rarity.Common, KeywordType.Action, ActionKind.Draw, OperatorKind.None, 1, KeywordTag.Utility, 0);
            AddKW(content, "TOKENIZE", "Tokenize", Rarity.Common, KeywordType.Action, ActionKind.Damage, OperatorKind.None, 3, KeywordTag.Attack, 0);
            AddKW(content, "NULL", "Null", Rarity.Common, KeywordType.Action, ActionKind.Shield, OperatorKind.None, 2, KeywordTag.Defense, 0);

            AddKW(content, "SPLICE", "Splice", Rarity.Common, KeywordType.Operator, ActionKind.None, OperatorKind.Splice, 0, KeywordTag.Operator, 0);
            AddKW(content, "SHIFT", "Shift", Rarity.Common, KeywordType.Operator, ActionKind.None, OperatorKind.Shift, 0, KeywordTag.Operator, 0);
            AddKW(content, "ECHO", "Echo", Rarity.Uncommon, KeywordType.Operator, ActionKind.None, OperatorKind.Echo, 50, KeywordTag.Operator, 30);
            AddKW(content, "INVERT", "Invert", Rarity.Uncommon, KeywordType.Operator, ActionKind.None, OperatorKind.Invert, 0, KeywordTag.Operator, 30);
            AddKW(content, "COMPRESS", "Compress", Rarity.Rare, KeywordType.Operator, ActionKind.None, OperatorKind.Compress, 170, KeywordTag.Operator, 60);

            AddKW(content, "FIREWALL", "Firewall", Rarity.Uncommon, KeywordType.Action, ActionKind.Shield, OperatorKind.None, 8, KeywordTag.Defense, 30);
            AddKW(content, "DRAIN", "Drain", Rarity.Uncommon, KeywordType.Action, ActionKind.Heal, OperatorKind.None, 6, KeywordTag.Attack | KeywordTag.Heal, 30);
            AddKW(content, "OVERLOAD", "Overload", Rarity.Uncommon, KeywordType.Action, ActionKind.Damage, OperatorKind.None, 18, KeywordTag.Attack, 30);
            AddKW(content, "STABILIZE", "Stabilize", Rarity.Common, KeywordType.Action, ActionKind.HeatDelta, OperatorKind.None, 2, KeywordTag.Utility, 0);
            AddKW(content, "JAILBREAK", "Jailbreak", Rarity.Rare, KeywordType.Action, ActionKind.IgnoreDriftCharges, OperatorKind.None, 2, KeywordTag.Utility, 60);
            AddKW(content, "SANDBOX", "Sandbox", Rarity.Uncommon, KeywordType.Action, ActionKind.IgnoreDriftCharges, OperatorKind.None, 1, KeywordTag.Utility, 30);

            // -------- Drifts (core set)
            AddDrift(content, "D1", "First keyword ignored.", DriftEffectKind.FirstKeywordIgnored, 1);
            AddDrift(content, "D2", "Healing -30%.", DriftEffectKind.HealingPenaltyPct, 30);
            AddDrift(content, "D3", "Shield -25%.", DriftEffectKind.ShieldPenaltyPct, 25);
            AddDrift(content, "D5", "Operators -20% potency.", DriftEffectKind.OperatorPenaltyPct, 20);
            AddDrift(content, "D8", "Enemy intent power +10%.", DriftEffectKind.EnemyIntentBoostPct, 10);
            AddDrift(content, "D10", "Your first action resolves last.", DriftEffectKind.FirstActionResolvesLast, 1);
            AddDrift(content, "D11", "Draw gives enemy +2 dmg next turn.", DriftEffectKind.DrawGivesEnemyBonusDamage, 2);
            AddDrift(content, "D14", "Compiling 3 actions adds +1 Heat.", DriftEffectKind.ThreeActionsAddsHeat, 1);
            AddDrift(content, "D15", "Compiling any operator gives enemy +2 shield.", DriftEffectKind.OperatorGivesEnemyShield, 2);
            AddDrift(content, "D16", "Your second keyword is duplicated.", DriftEffectKind.SecondKeywordDuplicated, 1);
            AddDrift(content, "D17", "End of your turn: Heat +1.", DriftEffectKind.EndTurnHeatPlusOne, 1);
            AddDrift(content, "D18", "Start fight with Weak(1).", DriftEffectKind.StartWithWeak, 1);
            AddDrift(content, "D19", "Start fight with Corrupted(1) in hand.", DriftEffectKind.StartWithCorruptedInHand, 1);
            AddDrift(content, "D20", "Enemy is immune to Weak.", DriftEffectKind.EnemyImmuneWeak, 1);
            AddDrift(content, "D21", "Single-hit attacks deal -20%.", DriftEffectKind.SingleHitDamagePenaltyPct, 20);
            AddDrift(content, "D22", "Random hand keyword -10% potency.", DriftEffectKind.RandomHandKeywordPenaltyPct, 10);
            AddDrift(content, "D23", "If you have 0 Buffer, take +2 dmg per hit.", DriftEffectKind.NoBufferExtraDamageFlat, 2);
            AddDrift(content, "D24", "Your first operator is negated.", DriftEffectKind.FirstOperatorNegated, 1);
            AddDrift(content, "D25", "Enemy below 30% HP gains Reflect 2.", DriftEffectKind.EnemyBelow30Reflect, 2);

            // -------- Plugins (core set)
            AddPlugin(content, "P1", "Vector Cache", Rarity.Common, PluginEffectKind.StartFightDrawPlus, 1);
            AddPlugin(content, "P2", "Rate Limiter", Rarity.Uncommon, PluginEffectKind.FirstEnemyHitReduceFlat, 3);
            AddPlugin(content, "P3", "Lossy Compression", Rarity.Common, PluginEffectKind.OperatorGrantsBuffer, 2);
            AddPlugin(content, "P4", "Adversarial Training", Rarity.Uncommon, PluginEffectKind.HeatGainGrantsBuffer, 2);
            AddPlugin(content, "P5", "Sandbox License", Rarity.Uncommon, PluginEffectKind.IgnoreFirstDriftEachFight, 1);
            AddPlugin(content, "P6", "Prompt Library", Rarity.Rare, PluginEffectKind.StartFightTempUncommonInHand, 1);
            AddPlugin(content, "P8", "Cold Start", Rarity.Common, PluginEffectKind.FirstTurnPowerBoostPct, 25);
            AddPlugin(content, "P9", "Hot Path", Rarity.Common, PluginEffectKind.HeatHighDamageBoostPct, 10);
            AddPlugin(content, "P14", "Speculative Exec", Rarity.Rare, PluginEffectKind.OncePerFightRecompile, 1);

            // -------- Enemies (core set + 1 boss)
            AddEnemy(content, "E1", "Tokenizer Swarm", false, 40, EnemyPassiveKind.None, 0,
                new EnemyIntentDef { id="swarm_hit", description="Swarm Hits", kind=EnemyIntentKind.Attack, basePower=6, multiHit=true, hits=2 },
                new EnemyIntentDef { id="swarm_mark", description="Tag You", kind=EnemyIntentKind.Debuff, basePower=1, appliesMarked=true },
                new EnemyIntentDef { id="swarm_shield", description="Scatter Shield", kind=EnemyIntentKind.Shield, basePower=4, grantsShield=true }
            );

            AddEnemy(content, "E4", "Firewall Drone", false, 60, EnemyPassiveKind.ReflectWhenShielded, 2,
                new EnemyIntentDef { id="fw_shield", description="Fortify", kind=EnemyIntentKind.Shield, basePower=10, grantsShield=true },
                new EnemyIntentDef { id="fw_attack", description="Policy Beam", kind=EnemyIntentKind.Attack, basePower=11 },
                new EnemyIntentDef { id="fw_mark", description="Scan", kind=EnemyIntentKind.Debuff, basePower=1, appliesMarked=true }
            );

            AddEnemy(content, "B1", "Safety Filter", true, 140, EnemyPassiveKind.PunishRepeats, 3,
                new EnemyIntentDef { id="sf_attack", description="Policy Strike", kind=EnemyIntentKind.Attack, basePower=18 },
                new EnemyIntentDef { id="sf_shield", description="Rule Shield", kind=EnemyIntentKind.Shield, basePower=14, grantsShield=true },
                new EnemyIntentDef { id="sf_mark", description="Flag", kind=EnemyIntentKind.Debuff, basePower=1, appliesMarked=true }
            );

            // Events omitted in this setup (your full bible events are already in earlier message);
            // you can generate them similarly as EventDefSO assets.

            content.RebuildIndexes();
        }

        private static void AddKW(GameContentSO content, string id, string name, Rarity rarity, KeywordType type, ActionKind action, OperatorKind op, int power, KeywordTag tags, int unlockCost)
        {
            var asset = ScriptableObject.CreateInstance<KeywordDefSO>();
            asset.id = id;
            asset.displayName = name;
            asset.rarity = rarity;
            asset.keywordType = type;
            asset.actionKind = action;
            asset.operatorKind = op;
            asset.basePower = power;
            asset.tags = tags;
            asset.unlockCostInsight = unlockCost;

            var path = $"{DataPath}/KW_{id}.asset";
            AssetDatabase.CreateAsset(asset, path);
            content.keywords.Add(asset);
        }

        private static void AddDrift(GameContentSO content, string id, string desc, DriftEffectKind kind, int value)
        {
            var asset = ScriptableObject.CreateInstance<DriftDefSO>();
            asset.id = id;
            asset.description = desc;
            asset.kind = kind;
            asset.value = value;

            var path = $"{DataPath}/DRIFT_{id}.asset";
            AssetDatabase.CreateAsset(asset, path);
            content.drifts.Add(asset);
        }

        private static void AddPlugin(GameContentSO content, string id, string name, Rarity rarity, PluginEffectKind kind, int value)
        {
            var asset = ScriptableObject.CreateInstance<PluginDefSO>();
            asset.id = id;
            asset.displayName = name;
            asset.rarity = rarity;
            asset.kind = kind;
            asset.value = value;

            var path = $"{DataPath}/PLUGIN_{id}.asset";
            AssetDatabase.CreateAsset(asset, path);
            content.plugins.Add(asset);
        }

        private static void AddEnemy(GameContentSO content, string id, string name, bool boss, int hp, EnemyPassiveKind passive, int val, params EnemyIntentDef[] intents)
        {
            var asset = ScriptableObject.CreateInstance<EnemyDefSO>();
            asset.id = id;
            asset.displayName = name;
            asset.isBoss = boss;
            asset.baseMaxHp = hp;
            asset.passiveKind = passive;
            asset.passiveValue = val;
            asset.intents = new List<EnemyIntentDef>(intents);

            var path = $"{DataPath}/ENEMY_{id}.asset";
            AssetDatabase.CreateAsset(asset, path);
            content.enemies.Add(asset);
        }

        private static void CreateScenes(GameContentSO content)
        {
            // Main Menu
            var main = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("GameApp").AddComponent<GameApp>();
            app.content = content;
            new GameObject("MainMenuController").AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(main, ScenesPath + "/Promptsmith_MainMenu.unity");

            // Run
            var run = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app2 = new GameObject("GameApp").AddComponent<GameApp>();
            app2.content = content;
            new GameObject("RunController").AddComponent<RunController>();
            EditorSceneManager.SaveScene(run, ScenesPath + "/Promptsmith_Run.unity");
        }
    }
}
#endif
