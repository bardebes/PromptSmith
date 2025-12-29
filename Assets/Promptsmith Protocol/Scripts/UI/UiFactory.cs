using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PromptsmithProtocol
{
    public sealed class UiFactory
    {
        private const string CanvasName = "PromptsmithCanvas";
        private const string RootName = "PromptsmithRoot";

        public Canvas canvas;
        public RectTransform root;
        public Font font;

        public void EnsureUi()
{
EnsureSingleEventSystem();

font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

var canvasGo = GameObject.Find(CanvasName);
if (canvasGo == null)
{
canvasGo = new GameObject(CanvasName);
UnityEngine.Object.DontDestroyOnLoad(canvasGo);
}

canvas = canvasGo.GetComponent<Canvas>();
if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;

if (canvasGo.GetComponent<CanvasScaler>() == null)
{
var scaler = canvasGo.AddComponent<CanvasScaler>();
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1080, 1920);
}

if (canvasGo.GetComponent<GraphicRaycaster>() == null)
canvasGo.AddComponent<GraphicRaycaster>();

var existing = canvas.transform.Find(RootName) as RectTransform;
if (existing != null)
{
root = existing;
ClearRoot(); // Ensure old children are cleared before reusing the root
return;
}

var rgo = new GameObject(RootName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
root = rgo.GetComponent<RectTransform>();
root.SetParent(canvas.transform, false);

root.anchorMin = new Vector2(0.05f, 0.05f);
root.anchorMax = new Vector2(0.95f, 0.95f);
root.offsetMin = Vector2.zero;
root.offsetMax = Vector2.zero;

var v = rgo.GetComponent<VerticalLayoutGroup>();
v.spacing = 10;
v.padding = new RectOffset(18, 18, 18, 18);
v.childAlignment = TextAnchor.UpperCenter;
v.childControlWidth = true;
v.childControlHeight = true;
v.childForceExpandWidth = true;
v.childForceExpandHeight = false;

rgo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
}
        private static void EnsureSingleEventSystem()
{
var all = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
if (all.Length == 0)
{
var es = new GameObject("EventSystem");
UnityEngine.Object.DontDestroyOnLoad(es);
es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
                return;
            }

            for (var i = 1; i < all.Length; i++)
                SafeDestroy(all[i].gameObject);
        }

        
private bool _isClearingRoot = false;

public void ClearRoot()
{
    // Guard against re-entrancy to avoid StackOverflow if nested calls occur.
    if (_isClearingRoot) return;

    _isClearingRoot = true;
    try
    {
        // If we don't have a cached root, try to find it in the scene.
        if (root == null)
        {
            var existingGo = GameObject.Find(RootName);
            if (existingGo == null) return;
            root = existingGo.GetComponent<RectTransform>();
            if (root == null) return;
        }

        foreach (Transform child in root)
        {
            SafeDestroy(child.gameObject);
        }

        Canvas.ForceUpdateCanvases();
    }
    finally
    {
        _isClearingRoot = false;
    }
}

        public void Spacer(float height = 10)
        {
            EnsureUi();
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(root, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        public Text Title(string t, int size = 30) => Text(root, t, size, TextAnchor.MiddleCenter);

        public Text Text(string t, int size = 14) => Text(root, t, size, TextAnchor.MiddleLeft);

        public Text Text(Transform parent, string t, int size = 14, TextAnchor align = TextAnchor.MiddleLeft)
        {
            EnsureUi();

            var go = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = Mathf.Max(18, size + 10);
            le.preferredHeight = Mathf.Max(22, size + 12);

            var txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.text = t;
            txt.color = Color.white;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        public Transform Row(float minHeight = 52)
        {
            EnsureUi();

            var go = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(root, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;

            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            return go.transform;
        }

        public void Button(Transform parent, string label, Action onClick, float width = 200, float height = 50)
{
var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
buttonGo.transform.SetParent(parent, false);

var button = buttonGo.GetComponent<Button>();
button.onClick.AddListener(() => onClick?.Invoke());

var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
textGo.transform.SetParent(buttonGo.transform, false);

var text = textGo.GetComponent<Text>();
text.text = label;
text.font = font;
text.alignment = TextAnchor.MiddleCenter;

var layout = buttonGo.GetComponent<LayoutElement>();
layout.preferredWidth = width;
layout.preferredHeight = height;
}

        public InputField Input(Transform parent, string initial, float width = 220)
        {
            EnsureUi();

            var go = new GameObject("Input", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.minHeight = 44;
            le.preferredHeight = 44;

            go.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 0.95f);

            var input = go.GetComponent<InputField>();

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);

            var txt = tgo.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.fontSize = 18;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.supportRichText = false;

            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 8);
            trt.offsetMax = new Vector2(-10, -8);

            input.textComponent = txt;
            input.text = initial;

            return input;
        }

        public Transform Scroll(float height = 280)
        {
            EnsureUi();

            var go = new GameObject("ScrollView", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(root, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            go.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.75f);

            var scroll = go.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            var vprt = viewport.GetComponent<RectTransform>();
            vprt.anchorMin = Vector2.zero;
            vprt.anchorMax = Vector2.one;
            vprt.offsetMin = new Vector2(10, 10);
            vprt.offsetMax = new Vector2(-10, -10);

            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);

            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);

            var v = content.GetComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vprt;
            scroll.content = crt;

            return content.transform;
        }

        private static void SafeDestroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
