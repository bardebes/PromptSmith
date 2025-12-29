using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PromptsmithProtocol
{
    public sealed class GameApp : MonoBehaviour
    {
        public const string MainMenuScene = "Promptsmith_MainMenu";
        public const string RunScene = "Promptsmith_Run";

        public GameContentSO content;
        public RunState run;
        public CombatState combat;
        public DeterministicRng rng;

        private static GameApp _inst;

        public static GameApp I
        {
            get
            {
                if (_inst != null) return _inst;
                _inst = FindFirstObjectByType<GameApp>();
                return _inst;
            }
        }

        private void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            DontDestroyOnLoad(gameObject);

            EnsureOneCamera();

            // Register safe JSON settings so unknown enum values don't throw during deserialization
            Newtonsoft.Json.JsonConvert.DefaultSettings = () => new Newtonsoft.Json.JsonSerializerSettings
            {
                Converters = new List<Newtonsoft.Json.JsonConverter> { new SafeStringEnumConverter() },
                MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore
            };

            if (content == null)
                throw new Exception("GameApp.content is not set. Run Promptsmith/Setup Project or assign GameContent asset.");
        }

        private static void EnsureOneCamera()
        {
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera keep = null;

            if (cams.Length > 0) keep = cams[0];

            if (keep == null)
            {
                var go = new GameObject("Main Camera");
                keep = go.AddComponent<Camera>();
                keep.orthographic = true;
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
                DontDestroyOnLoad(go);
            }

            // Kill other cameras (prevents "no cameras rendering" + extra listeners).
            for (var i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i] != keep)
                    Destroy(cams[i].gameObject);
            }

            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].gameObject != keep.gameObject)
                    Destroy(listeners[i]);
            }
        }

        public void NewRun(int seed)
        {
            rng = new DeterministicRng(seed);

            run = new RunState { seed = seed, depthIndex = 0, player = new PlayerState() };
            run.deck.Init(new[]
            {
                "BURST","BURST","PING","SHIELD","SHIELD","PATCH",
                "JAM","TRACE","CLEAN","CACHE","SPLICE","SHIFT","PACKET","TOKENIZE"
            });

            run.map = MapGenerator.Generate(rng, rows: 12, lanes: 3);
            run.pluginIds = new List<string>();

            combat = null;
        }

        public void GoToMainMenu() => SceneManager.LoadScene(MainMenuScene);
        public void GoToRun() => SceneManager.LoadScene(RunScene);
    }
}