using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UiDiagnosticsAuto
{
    static UiDiagnosticsAuto()
    {
        EditorApplication.delayCall += DumpRootOnce;
    }

    private static void DumpRootOnce()
    {
        // Try to invoke the diagnostics PrintRoot to get detailed info in the Editor.log
        try
        {
            PromptsmithProtocol.Editor.UiDiagnostics.PrintRoot();
            Debug.Log("[UiDiagnosticsAuto] Invoked UiDiagnostics.PrintRoot()");
        }
        catch (System.Exception ex)
        {
            Debug.Log($"[UiDiagnosticsAuto] Failed to call PrintRoot: {ex.Message}");
        }

        var rootGo = GameObject.Find("PromptsmithRoot");
        if (rootGo == null)
        {
            Debug.Log("[UiDiagnosticsAuto] PromptsmithRoot not found after reload.");
            return;
        }

        Debug.Log($"[UiDiagnosticsAuto] PromptsmithRoot found with {rootGo.transform.childCount} children (post-reload)");
    }
}
