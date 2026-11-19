using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>Pembuat elemen uGUI lewat kode (gaya gelap-horor yang konsisten).</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.92f, 0.88f, 0.8f);
        public static readonly Color Dim = new Color(0.62f, 0.58f, 0.52f);
        public static readonly Color Accent = new Color(0.86f, 0.25f, 0.18f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.45f);
        public static readonly Color PanelCol = new Color(0.035f, 0.03f, 0.03f, 0.88f);
        static Font body, title;

        public static Font Body
        {
            get
            {
                if (body == null)
                {
                    Font f = AssetLibrary.I != null ? AssetLibrary.I.bodyFont : null;
                    body = f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return body;
            }
        }
        public static Font Title
        {
            get
            {
                if (title == null)
                {
                    Font f = AssetLibrary.I != null ? AssetLibrary.I.titleFont : null;
                    title = f != null ? f : Body;
                }
                return title;
            }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)g.transform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }
        public static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform r = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return r;
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Vector2? pivot = null)
        {
            RectTransform r = Rect(parent, name, anchor, anchor, pivot ?? anchor, pos, size);
            Image i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }
        public static Image Fill(Transform parent, string name, Color c)
        {
            RectTransform r = Stretch(parent, name);
            Image i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color? col = null, bool titleFont = false, Vector2? pivot = null)
        {
            RectTransform r = Rect(parent, name, anchor, anchor, pivot ?? anchor, pos, box);
            Text t = r.gameObject.AddComponent<Text>();
            t.font = titleFont ? Title : Body; t.fontSize = size; t.alignment = align; t.color = col ?? Ink; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true; t.raycastTarget = false;
            Shadow s = r.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.85f); s.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static UnityEngine.UI.Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 30)
        {
            RectTransform r = Rect(parent, name, anchor, anchor, anchor, pos, size);
            Image bg = r.gameObject.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.0f);
            UnityEngine.UI.Button b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioDirector.I?.Click(); onClick?.Invoke(); });
            Image bar = Panel(r, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(5f, size.y * 0.7f), Accent, new Vector2(0f, 0.5f));
            Text t = Label(r, "Text", text, fontSize, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(size.x - 24f, size.y), Ink, true, new Vector2(0f, 0.5f));
            UIHover h = r.gameObject.AddComponent<UIHover>(); h.text = t; h.bar = bar; h.bg = bg;
            return b;
        }

        public static UnityEngine.UI.Slider MakeSlider(Transform parent, string label, float min, float max, float value, Vector2 pos, UnityAction<float> onChange, System.Func<float, string> fmt)
        {
            RectTransform row = Rect(parent, "Row_" + label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(820f, 52f));
            Label(row, "Label", label, 24, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(300f, 50f), Ink, false, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", fmt(value), 22, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(110f, 50f), Gold, false, new Vector2(1f, 0.5f));
            RectTransform sr = Rect(row, "Slider", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(310f, 0f), new Vector2(380f, 22f));
            Image bg = sr.gameObject.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.12f);
            RectTransform fillArea = Rect(sr, "FillArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fi = fill.gameObject.AddComponent<Image>(); fi.color = new Color(Accent.r, Accent.g, Accent.b, 0.85f);
            RectTransform handleArea = Rect(sr, "HandleArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, 0f));
            RectTransform handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 12f));
            Image hi = handle.gameObject.AddComponent<Image>(); hi.color = Ink;
            UnityEngine.UI.Slider s = sr.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = hi; s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => { val.text = fmt(v); onChange(v); });
            return s;
        }

        public static UnityEngine.UI.Button MakeToggle(Transform parent, string label, bool value, Vector2 pos, UnityAction<bool> onChange)
        {
            RectTransform row = Rect(parent, "Row_" + label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(820f, 52f));
            Label(row, "Label", label, 24, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 50f), Ink, false, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", value ? "YA" : "TIDAK", 24, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(200f, 50f), Gold, true, new Vector2(1f, 0.5f));
            Image bg = row.gameObject.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.03f);
            UnityEngine.UI.Button b = row.gameObject.AddComponent<UnityEngine.UI.Button>(); b.transition = Selectable.Transition.None;
            bool state = value;
            b.onClick.AddListener(() => { state = !state; val.text = state ? "YA" : "TIDAK"; AudioDirector.I?.Click(); onChange(state); });
            UIHover h = row.gameObject.AddComponent<UIHover>(); h.text = val; h.bg = bg;
            return b;
        }

        public static UnityEngine.UI.Button MakeCycle(Transform parent, string label, string[] options, int value, Vector2 pos, UnityAction<int> onChange)
        {
            RectTransform row = Rect(parent, "Row_" + label, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(820f, 52f));
            Label(row, "Label", label, 24, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 50f), Ink, false, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", options[value], 24, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(240f, 50f), Gold, true, new Vector2(1f, 0.5f));
            Image bg = row.gameObject.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.03f);
            UnityEngine.UI.Button b = row.gameObject.AddComponent<UnityEngine.UI.Button>(); b.transition = Selectable.Transition.None;
            int st = value;
            b.onClick.AddListener(() => { st = (st + 1) % options.Length; val.text = options[st]; AudioDirector.I?.Click(); onChange(st); });
            UIHover h = row.gameObject.AddComponent<UIHover>(); h.text = val; h.bg = bg;
            return b;
        }

        public static Image Bar(Transform parent, string name, Vector2 pos, float width, Color c, out Image fill)
        {
            Image bg = Panel(parent, name, new Vector2(0f, 0f), pos, new Vector2(width, 10f), new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 0f));
            RectTransform fr = Rect(bg.transform, "Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(width - 4f, -4f));
            fill = fr.gameObject.AddComponent<Image>(); fill.color = c; fill.raycastTarget = false;
            return bg;
        }
    }

    /// <summary>Efek hover tombol: teks menyala, garis aksen memanjang, suara pelan.</summary>
    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Text text; public Image bar, bg;
        bool over; float t;
        public void OnPointerEnter(PointerEventData e) { over = true; AudioDirector.I?.UiHover(); }
        public void OnPointerExit(PointerEventData e) { over = false; }
        public void OnSelect(BaseEventData e) { over = true; }
        public void OnDeselect(BaseEventData e) { over = false; }
        void OnDisable() { over = false; t = 0f; Refresh(); }
        void Update()
        {
            t = Mathf.MoveTowards(t, over ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            Refresh();
        }
        void Refresh()
        {
            if (text) text.color = Color.Lerp(UIKit.Ink * 0.85f, UIKit.Gold, t);
            if (bar) { RectTransform r = bar.rectTransform; r.sizeDelta = new Vector2(Mathf.Lerp(4f, 12f, t), r.sizeDelta.y); }
            if (bg) bg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.0f, 0.06f, t));
        }
    }
}
