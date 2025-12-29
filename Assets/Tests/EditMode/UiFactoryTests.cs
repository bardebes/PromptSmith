using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class UiFactoryTests
{
    [SetUp]
    public void Setup()
    {
        // Ensure a fresh empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
    }

    [Test]
    public void MainMenuController_CreatesUiElements()
    {
        var go = new GameObject("MainMenuController");
        var mm = go.AddComponent<PromptsmithProtocol.MainMenuController>();

        // Simulate Unity lifecycle by invoking private Awake/Start via reflection
        var awake = typeof(PromptsmithProtocol.MainMenuController).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var start = typeof(PromptsmithProtocol.MainMenuController).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        awake?.Invoke(mm, null);
        start?.Invoke(mm, null);

        var root = GameObject.Find("PromptsmithRoot");
        Assert.IsNotNull(root, "PromptsmithRoot should be created by MainMenuController Start/UiFactory.EnsureUi");
        Assert.Greater(root.transform.childCount, 0, "PromptsmithRoot should have children after Render()");

        // Look for the button text
        bool foundStart = false;
        foreach (Transform child in root.transform)
        {
            var txt = child.GetComponentInChildren<UnityEngine.UI.Text>();
            if (txt != null && txt.text != null && txt.text.Contains("START"))
            {
                foundStart = true;
                break;
            }
        }

        Assert.IsTrue(foundStart, "Start button text should be present in the UI hierarchy.");
    }
}
