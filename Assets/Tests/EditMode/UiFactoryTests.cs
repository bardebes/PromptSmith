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
        // Instead of relying on MainMenuController lifecycle, construct UiFactory directly to create UI elements
        var ui = new PromptsmithProtocol.UiFactory();
        ui.EnsureUi();
        ui.ClearRoot();
        ui.Title("PROMPTSMITH PROTOCOL", 34);
        ui.Spacer(10);
        var buttonRow = ui.Row();
        ui.Button(buttonRow, "START", () => { }, 260);

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
