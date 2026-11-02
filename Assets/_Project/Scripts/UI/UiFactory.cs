using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// Builds simple uGUI elements from code so screens don't depend on hand-made prefabs.
    /// Placeholder look: flat colours and the built-in font.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        public static readonly Color ButtonColor = new Color(0.98f, 0.55f, 0.2f);
        public static readonly Color TextColor = Color.white;

        static Font s_Font;
        public static Font Font => s_Font != null ? s_Font : (s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor a rect to a point of its parent (0..1) with a pixel size and offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string text, int size,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TextColor;
            t.raycastTarget = false; // labels must never block clicks on buttons underneath
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button CreateButton(string name, Transform parent, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var img = CreatePanel(name, parent, ButtonColor);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);
            var text = CreateText("Label", img.transform, label, 34);
            text.color = Color.black;
            Stretch(text.rectTransform);
            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var bg = CreatePanel(name, parent, Color.white);
            var field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;

            var text = CreateText("Text", bg.transform, "", 34);
            text.color = Color.black;
            text.supportRichText = false;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 0);
            text.rectTransform.offsetMax = new Vector2(-16, 0);

            var ph = CreateText("Placeholder", bg.transform, placeholder, 34);
            ph.color = new Color(0, 0, 0, 0.4f);
            ph.fontStyle = FontStyle.Italic;
            Stretch(ph.rectTransform);
            ph.rectTransform.offsetMin = new Vector2(16, 0);
            ph.rectTransform.offsetMax = new Vector2(-16, 0);

            field.textComponent = text;
            field.placeholder = ph;
            return field;
        }

        /// <summary>A vertical stack (centred) for menus.</summary>
        public static RectTransform CreateColumn(string name, Transform parent, float spacing)
        {
            var rt = CreateRect(name, parent);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        public static void SetPreferredHeight(Component c, float height)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
