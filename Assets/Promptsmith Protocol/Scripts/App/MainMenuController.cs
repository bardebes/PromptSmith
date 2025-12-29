using System;
using UnityEngine;

namespace PromptsmithProtocol
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private static MainMenuController _inst;
        private UiFactory _ui;

        private void Awake()
        {
            if (_inst != null && _inst != this)
            {
                Destroy(gameObject);
                return;
            }
            _inst = this;
        }

        private void Start()
        {
            _ui = new UiFactory();
            _ui.EnsureUi();
            Render();
        }

        private void Render()
        {
            _ui.ClearRoot();

            _ui.Title("PROMPTSMITH PROTOCOL", 34);
            _ui.Spacer(10);

            var seedRow = _ui.Row();
            _ui.Text(seedRow, "Seed:", 14);
            var seedInput = _ui.Input(seedRow, "0", 220);

            _ui.Spacer(10);

            var insight = PlayerPrefs.GetInt(MetaKeys.Insight, 0);
            var artifacts = PlayerPrefs.GetInt(MetaKeys.Artifacts, 0);
            _ui.Text($"Meta: Insight {insight} | Artifacts {artifacts}", 14);

            _ui.Spacer(14);

            var buttonRow = _ui.Row();
            _ui.Button(buttonRow, "START RUN", () =>
            {
                if (!int.TryParse(seedInput.text, out var seed) || seed == 0)
                    seed = unchecked((int)DateTime.UtcNow.Ticks);

                GameApp.I.NewRun(seed);
                GameApp.I.GoToRun();
            }, 260);

            _ui.Button(buttonRow, "RESET META", () =>
            {
                PlayerPrefs.DeleteKey(MetaKeys.Insight);
                PlayerPrefs.DeleteKey(MetaKeys.Artifacts);
                PlayerPrefs.Save();
                Render();
            }, 220);

            _ui.Spacer(8);
            _ui.Text("Tip: If clicks don’t work, set Player > Active Input Handling = Both.", 12);
        }
    }
}
