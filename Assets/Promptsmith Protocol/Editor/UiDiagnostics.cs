using UnityEditor;
using UnityEngine;

namespace PromptsmithProtocol.Editor
{
    public static class UiDiagnostics
    {
        [MenuItem("Promptsmith/Diagnostics/Print PromptsmithRoot Hierarchy")]
        public static void PrintRoot()
        {
            var rootGo = GameObject.Find("PromptsmithRoot");
            if (rootGo == null)
            {
                Debug.Log("PromptsmithRoot not found in scene.");
                return;
            }

            Debug.Log($"PromptsmithRoot found with {rootGo.transform.childCount} children:");
            for (int i = 0; i < rootGo.transform.childCount; i++)
            {
                var child = rootGo.transform.GetChild(i);
                Debug.Log($"  [{i}] {child.name} (active={child.gameObject.activeInHierarchy})");
                var txt = child.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) Debug.Log($"    Text: '{txt.text}' Font={(txt.font==null?"<null>":txt.font.name)} FontSize={txt.fontSize}");
                var btn = child.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) Debug.Log("    Has Button component");
            }
        }

        [MenuItem("Promptsmith/Diagnostics/Recreate UI (EnsureUi)")]
        public static void RecreateUi()
        {
            var app = UnityEngine.Object.FindObjectOfType<PromptsmithProtocol.MainMenuController>();
            if (app != null)
            {
                var uiFactoryField = typeof(PromptsmithProtocol.MainMenuController).GetField("_ui", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var uiFactory = uiFactoryField?.GetValue(app) as PromptsmithProtocol.UiFactory;
                if (uiFactory == null)
                {
                    Debug.Log("_ui field null; creating new UiFactory and calling EnsureUi and Render on MainMenuController.");
                    var mf = new PromptsmithProtocol.UiFactory();
                    mf.EnsureUi();
                    // Try to call Render if available
                    var renderMethod = typeof(PromptsmithProtocol.MainMenuController).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (renderMethod != null) renderMethod.Invoke(app, null);
                    return;
                }

                uiFactory.EnsureUi();
                Debug.Log("Called EnsureUi() on existing MainMenuController._ui instance.");
            }
            else
            {
                Debug.Log("No MainMenuController found in scene. Creating transient UiFactory and calling EnsureUi().");
                var mf = new PromptsmithProtocol.UiFactory();
                mf.EnsureUi();
            }
        }
    }
}
