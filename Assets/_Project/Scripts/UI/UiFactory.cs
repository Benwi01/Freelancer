using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// Builds simple uGUI elements from code so screens don't depend on hand-made prefabs.
    /// Flat colours, rounded corners and the built-in font.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        public static readonly Color ButtonColor = new Color(0.98f, 0.55f, 0.2f);
        public static readonly Color TextColor = Color.white;

        /// <summary>Quiet see-through button style (secondary actions).</summary>
        public static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.07f);

        static Font s_Font;
        static Sprite s_Rounded;
        const int RoundedSize = 64, RoundedRadius = 24;
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
            Round(img, 12f);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            var text = CreateText("Label", img.transform, label, 34);
            text.color = Color.black;
            Stretch(text.rectTransform);
            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var bg = CreatePanel(name, parent, Color.white);
            Round(bg, 12f);
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

        /// <summary>Restyle a button as a quiet see-through one with white text.</summary>
        public static Button Ghost(Button button)
        {
            button.GetComponent<Image>().color = GhostColor;
            var colors = button.colors;
            // The multiplier lets hover go brighter than the base colour.
            colors.normalColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
            colors.colorMultiplier = 2.5f;
            button.colors = colors;
            foreach (var t in button.GetComponentsInChildren<Text>()) t.color = TextColor;
            return button;
        }

        /// <summary>Give an image rounded corners (radius in canvas pixels).</summary>
        public static Image Round(Image img, float radius = 16f)
        {
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundedRadius / Mathf.Max(1f, radius);
            return img;
        }

        static Sprite RoundedSprite
        {
            get
            {
                if (s_Rounded != null) return s_Rounded;
                var tex = new Texture2D(RoundedSize, RoundedSize, TextureFormat.RGBA32, false)
                    { name = "RoundedRect", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[RoundedSize * RoundedSize];
                float r = RoundedRadius;
                for (int y = 0; y < RoundedSize; y++)
                for (int x = 0; x < RoundedSize; x++)
                {
                    // Distance outside the rounded corner, anti-aliased over one pixel.
                    float cx = Mathf.Clamp(x + 0.5f, r, RoundedSize - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, RoundedSize - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - r;
                    byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    px[y * RoundedSize + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                s_Rounded = Sprite.Create(tex, new Rect(0, 0, RoundedSize, RoundedSize), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return s_Rounded;
            }
        }

        /// <summary>A full-screen vertical gradient (bottom to top).</summary>
        public static RawImage CreateGradient(string name, Transform parent, Color bottom, Color top)
        {
            var img = CreateRect(name, parent).gameObject.AddComponent<RawImage>();
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A soft round glow (colour fades out to the edges).</summary>
        public static RawImage CreateGlow(string name, Transform parent, Color color)
        {
            var img = CreateRect(name, parent).gameObject.AddComponent<RawImage>();
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static void SetPreferredHeight(Component c, float height)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
    }
}
