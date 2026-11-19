using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HantuBelajar
{
    public enum FontKind { Body, Head, Title }
    public enum BtnStyle { Menu, Primary, Secondary, Tab }

    /// <summary>Pembuat elemen uGUI lewat kode: gaya gelap-horor yang konsisten (kartu membulat, tombol beranimasi, keycap, ikon).</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.94f, 0.91f, 0.85f);
        public static readonly Color Dim = new Color(0.68f, 0.64f, 0.58f);
        public static readonly Color Faint = new Color(0.48f, 0.45f, 0.42f);
        public static readonly Color Accent = new Color(0.86f, 0.2f, 0.15f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.45f);
        public static readonly Color Good = new Color(0.45f, 0.92f, 0.58f);
        public static readonly Color Cold = new Color(0.58f, 0.8f, 1f);
        public static readonly Color Danger = new Color(1f, 0.34f, 0.27f);
        public static readonly Color CardCol = new Color(0.07f, 0.064f, 0.066f, 0.9f);
        public static readonly Color LineCol = new Color(1f, 0.93f, 0.82f, 0.1f);

        static Font body, head, title;

        static Font OsFont(string[] names)
        {
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                foreach (string n in names)
                    if (System.Array.IndexOf(installed, n) >= 0)
                    {
                        Font f = Font.CreateDynamicFontFromOSFont(n, 32);
                        if (f != null) return f;
                    }
            }
            catch (System.Exception) { }
            return null;
        }

        public static Font Body
        {
            get
            {
                if (body == null)
                {
                    Font f = AssetLibrary.I != null ? AssetLibrary.I.bodyFont : null;
                    if (f == null) f = OsFont(new[] { "Segoe UI", "Calibri", "Arial" });
                    body = f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return body;
            }
        }
        public static Font Head
        {
            get
            {
                if (head == null)
                {
                    Font f = OsFont(new[] { "Bahnschrift", "Segoe UI Semibold", "Segoe UI", "Arial" });
                    head = f != null ? f : Body;
                }
                return head;
            }
        }
        public static Font Title
        {
            get
            {
                if (title == null)
                {
                    Font f = AssetLibrary.I != null ? AssetLibrary.I.titleFont : null;
                    title = f != null ? f : Head;
                }
                return title;
            }
        }
        public static Font FontOf(FontKind k) { return k == FontKind.Title ? Title : k == FontKind.Head ? Head : Body; }

        // ------------------------------------------------------------------ dasar
        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)g.transform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }
        public static RectTransform Node(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            return Rect(parent, name, anchor, anchor, pivot ?? anchor, pos, size);
        }
        public static RectTransform Stretch(Transform parent, string name)
        {
            return Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, Vector2? pivot = null)
        {
            RectTransform r = Node(parent, name, anchor, pos, size, pivot);
            Image i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }
        public static Image Rounded(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, int radius = 12, Vector2? pivot = null, int border = 0)
        {
            Image i = Panel(parent, name, anchor, pos, size, c, pivot);
            i.sprite = Icons.Rounded(radius, border); i.type = Image.Type.Sliced;
            return i;
        }
        public static Image Fill(Transform parent, string name, Color c)
        {
            RectTransform r = Stretch(parent, name);
            Image i = r.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }
        static Image StretchImage(Transform parent, string name, Color c, Sprite s)
        {
            Image i = Fill(parent, name, c);
            i.sprite = s; i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>Kartu: bayangan lembut + isi membulat + garis tepi tipis. Konten ditambahkan sebagai anak (tampil di atasnya).</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null, Color? fill = null, int radius = 14, bool shadow = true)
        {
            RectTransform r = Node(parent, name, anchor, pos, size, pivot);
            if (shadow)
            {
                Image sh = StretchImage(r, "Shadow", new Color(0f, 0f, 0f, 0.5f), Icons.Shadow());
                sh.rectTransform.offsetMin = new Vector2(-24f, -30f); sh.rectTransform.offsetMax = new Vector2(24f, 16f);
            }
            StretchImage(r, "Fill", fill ?? CardCol, Icons.Rounded(radius));
            StretchImage(r, "Border", LineCol, Icons.Rounded(radius, 2));
            return r;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color? col = null, FontKind font = FontKind.Body, Vector2? pivot = null, bool shadow = true)
        {
            RectTransform r = Node(parent, name, anchor, pos, box, pivot);
            Text t = r.gameObject.AddComponent<Text>();
            t.font = FontOf(font); t.fontSize = size; t.alignment = align; t.color = col ?? Ink; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true; t.raycastTarget = false;
            if (font == FontKind.Body) t.lineSpacing = 1.1f;
            if (shadow) { Shadow s = r.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0f, 0f, 0.8f); s.effectDistance = new Vector2(1.5f, -1.5f); }
            return t;
        }

        public static Image Icon(Transform parent, string name, string icon, Vector2 anchor, Vector2 pos, float size, Color col, Vector2? pivot = null)
        {
            RectTransform r = Node(parent, name, anchor, pos, new Vector2(size, size), pivot);
            Image i = r.gameObject.AddComponent<Image>(); i.sprite = Icons.Get(icon); i.color = col; i.raycastTarget = false; i.preserveAspect = true;
            return i;
        }

        public static Image Divider(Transform parent, string name, Vector2 anchor, Vector2 pos, float width, Vector2? pivot = null)
        {
            return Panel(parent, name, anchor, pos, new Vector2(width, 1f), LineCol, pivot);
        }

        // ------------------------------------------------------------------ tombol
        public static Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, int fontSize = 30, BtnStyle style = BtnStyle.Secondary, string icon = null, Vector2? pivot = null)
        {
            RectTransform r = Node(parent, name, anchor, pos, size, pivot);
            Image bg = r.gameObject.AddComponent<Image>(); bg.raycastTarget = true; bg.color = Color.clear;
            UIHover h = r.gameObject.AddComponent<UIHover>();
            Button b = r.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = bg;
            Navigation nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
            b.onClick.AddListener(() => { AudioDirector.I?.Click(); onClick?.Invoke(); });
            float textX = 0f; TextAnchor align = TextAnchor.MiddleCenter;
            switch (style)
            {
                case BtnStyle.Menu:
                    bg.sprite = Icons.Gradient(true, 0.2f, 0f, "btnGrad"); bg.type = Image.Type.Simple;
                    h.bg = bg; h.bgN = new Color(1f, 0.82f, 0.66f, 0f); h.bgH = new Color(1f, 0.82f, 0.66f, 1f);
                    h.bar = Panel(r, "Bar", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, size.y * 0.6f), Accent, new Vector2(0f, 0.5f));
                    h.barN = 3f; h.barH = 9f;
                    h.textN = new Color(Ink.r * 0.9f, Ink.g * 0.9f, Ink.b * 0.9f, 1f); h.textH = Gold; h.slide = 10f;
                    textX = 28f; align = TextAnchor.MiddleLeft;
                    break;
                case BtnStyle.Primary:
                    bg.sprite = Icons.Rounded(10); bg.type = Image.Type.Sliced;
                    h.bg = bg; h.bgN = new Color(0.72f, 0.16f, 0.12f, 1f); h.bgH = new Color(0.92f, 0.26f, 0.18f, 1f);
                    h.textN = Color.white; h.textH = Color.white; h.scaleH = 1.03f;
                    break;
                case BtnStyle.Secondary:
                    bg.sprite = Icons.Rounded(10); bg.type = Image.Type.Sliced;
                    h.bg = bg; h.bgN = new Color(1f, 1f, 1f, 0.04f); h.bgH = new Color(1f, 1f, 1f, 0.11f);
                    h.border = StretchImage(r, "Border", LineCol, Icons.Rounded(10, 2));
                    h.borderN = new Color(1f, 0.93f, 0.82f, 0.18f); h.borderH = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
                    h.textN = Ink; h.textH = Gold; h.scaleH = 1.02f;
                    break;
                case BtnStyle.Tab:
                    h.underline = Panel(r, "Underline", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size.x * 0.72f, 3f), Accent, new Vector2(0.5f, 0f));
                    h.textN = Dim; h.textH = Ink;
                    break;
            }
            if (icon != null)
            {
                float isz = size.y * 0.46f;
                Image ic = Icon(r, "Icon", icon, align == TextAnchor.MiddleLeft ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f), Vector2.zero, isz, Ink, new Vector2(0f, 0.5f));
                h.icon = ic;
                if (align == TextAnchor.MiddleLeft) { ic.rectTransform.anchoredPosition = new Vector2(textX, 0f); textX += isz + 14f; }
            }
            Text t;
            if (align == TextAnchor.MiddleLeft)
                t = Label(r, "Text", text, fontSize, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(textX, 0f), new Vector2(size.x - textX - 10f, size.y), Ink, FontKind.Head, new Vector2(0f, 0.5f));
            else
            {
                t = Label(r, "Text", text, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size, Ink, FontKind.Head);
                if (h.icon != null)
                {
                    // ikon + teks di tengah
                    float tw = Mathf.Min(t.preferredWidth, size.x - 40f), isz = h.icon.rectTransform.sizeDelta.x;
                    float total = isz + 12f + tw;
                    h.icon.rectTransform.anchoredPosition = new Vector2(-total * 0.5f, 0f);
                    t.rectTransform.anchoredPosition = new Vector2((isz + 12f) * 0.5f, 0f);
                }
            }
            h.text = t;
            h.Refresh();
            return b;
        }

        /// <summary>Tulisan kecil di bawah/samping label tombol (mis. info simpanan).</summary>
        public static Text SubLabel(Button b, string text, int size = 20)
        {
            Text main = b.transform.Find("Text").GetComponent<Text>();
            Text s = Label(b.transform, "Sub", text, size, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(420f, 40f), Dim, FontKind.Body, new Vector2(0f, 0.5f));
            s.rectTransform.anchoredPosition = new Vector2(main.rectTransform.anchoredPosition.x + main.preferredWidth + 22f, -1f);
            return s;
        }

        // ------------------------------------------------------------------ baris pengaturan
        static Image RowBg(RectTransform row)
        {
            Image bg = row.gameObject.AddComponent<Image>(); bg.sprite = Icons.Rounded(8); bg.type = Image.Type.Sliced; bg.color = new Color(1f, 1f, 1f, 0.03f);
            UIHover h = row.gameObject.AddComponent<UIHover>(); h.bg = bg; h.bgN = new Color(1f, 1f, 1f, 0.03f); h.bgH = new Color(1f, 1f, 1f, 0.07f);
            h.Refresh();
            return bg;
        }

        public static Slider MakeSlider(Transform parent, string label, float min, float max, float value, Vector2 pos, float width, UnityAction<float> onChange, System.Func<float, string> fmt)
        {
            RectTransform row = Node(parent, "Row_" + label, new Vector2(0.5f, 1f), pos, new Vector2(width, 58f), new Vector2(0.5f, 1f));
            RowBg(row);
            Label(row, "Label", label, 23, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(width * 0.45f, 50f), Ink, FontKind.Body, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", fmt(value), 23, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(100f, 50f), Gold, FontKind.Head, new Vector2(1f, 0.5f));
            float sw = width * 0.4f;
            RectTransform sr = Node(row, "Slider", new Vector2(1f, 0.5f), new Vector2(-140f, 0f), new Vector2(sw, 30f), new Vector2(1f, 0.5f));
            Image hit = sr.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            Rounded(sr, "Track", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sw, 8f), new Color(1f, 1f, 1f, 0.14f), 4);
            RectTransform fillArea = Rect(sr, "FillArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
            RectTransform fill = Rect(fillArea, "Fill", Vector2.zero, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fi = fill.gameObject.AddComponent<Image>(); fi.sprite = Icons.Rounded(4); fi.type = Image.Type.Sliced; fi.color = Accent; fi.raycastTarget = false;
            RectTransform handleArea = Rect(sr, "HandleArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, 0f));
            RectTransform handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, -6f));
            Image hi = handle.gameObject.AddComponent<Image>(); hi.sprite = Icons.Get("disc"); hi.color = Ink; hi.preserveAspect = true;
            Slider s = sr.gameObject.AddComponent<Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = hi; s.direction = Slider.Direction.LeftToRight; s.transition = Selectable.Transition.None;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => { val.text = fmt(v); onChange(v); });
            return s;
        }

        public static Button MakeToggle(Transform parent, string label, bool value, Vector2 pos, float width, UnityAction<bool> onChange)
        {
            RectTransform row = Node(parent, "Row_" + label, new Vector2(0.5f, 1f), pos, new Vector2(width, 58f), new Vector2(0.5f, 1f));
            Image bg = RowBg(row);
            Label(row, "Label", label, 23, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(width * 0.65f, 50f), Ink, FontKind.Body, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", value ? "AKTIF" : "MATI", 19, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-100f, 0f), new Vector2(120f, 40f), Dim, FontKind.Head, new Vector2(1f, 0.5f));
            RectTransform sw = Node(row, "Switch", new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(62f, 32f), new Vector2(1f, 0.5f));
            Image track = Rounded(sw, "Track", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 32f), new Color(1f, 1f, 1f, 0.14f), 16);
            Image knob = Icon(sw, "Knob", "disc", new Vector2(0f, 0.5f), new Vector2(4f, 0f), 24f, Ink, new Vector2(0f, 0.5f));
            SwitchAnim a = sw.gameObject.AddComponent<SwitchAnim>(); a.track = track; a.knob = knob.rectTransform; a.on = value; a.Snap();
            Button b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = bg;
            bool state = value;
            b.onClick.AddListener(() => { state = !state; a.on = state; val.text = state ? "AKTIF" : "MATI"; AudioDirector.I?.Click(); onChange(state); });
            return b;
        }

        public static Button MakeCycle(Transform parent, string label, string[] options, int value, Vector2 pos, float width, UnityAction<int> onChange)
        {
            RectTransform row = Node(parent, "Row_" + label, new Vector2(0.5f, 1f), pos, new Vector2(width, 58f), new Vector2(0.5f, 1f));
            Image bg = RowBg(row);
            Label(row, "Label", label, 23, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(width * 0.5f, 50f), Ink, FontKind.Body, new Vector2(0f, 0.5f));
            Text val = Label(row, "Value", options[value], 23, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(-56f, 0f), new Vector2(190f, 50f), Gold, FontKind.Head, new Vector2(1f, 0.5f));
            int st = value;
            Image left = Icon(row, "Prev", "chevron", new Vector2(1f, 0.5f), new Vector2(-262f, 0f), 22f, Dim, new Vector2(0.5f, 0.5f));
            left.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f); left.raycastTarget = true;
            Button lb = left.gameObject.AddComponent<Button>(); lb.transition = Selectable.Transition.None;
            Icon(row, "Next", "chevron", new Vector2(1f, 0.5f), new Vector2(-32f, 0f), 22f, Dim, new Vector2(0.5f, 0.5f));
            Button b = row.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = bg;
            b.onClick.AddListener(() => { st = (st + 1) % options.Length; val.text = options[st]; AudioDirector.I?.Click(); onChange(st); });
            lb.onClick.AddListener(() => { st = (st + options.Length - 1) % options.Length; val.text = options[st]; AudioDirector.I?.Click(); onChange(st); });
            return b;
        }

        // ------------------------------------------------------------------ keycap, bar, chip
        public static float KeycapWidth(string key, float h)
        {
            int fs = Mathf.RoundToInt(h * 0.46f);
            return Mathf.Max(h, key.Length * fs * 0.6f + h * 0.6f);
        }

        public static RectTransform Keycap(Transform parent, string key, Vector2 anchor, Vector2 pos, float h = 40f, Vector2? pivot = null)
        {
            int fs = Mathf.RoundToInt(h * 0.46f);
            float w = KeycapWidth(key, h);
            RectTransform r = Node(parent, "Key_" + key, anchor, pos, new Vector2(w, h), pivot);
            Image depth = StretchImage(r, "Depth", new Color(0.01f, 0.01f, 0.01f, 0.95f), Icons.Rounded(8));
            depth.rectTransform.offsetMin = new Vector2(0f, -4f);
            StretchImage(r, "Face", new Color(0.21f, 0.2f, 0.19f, 0.98f), Icons.Rounded(8));
            StretchImage(r, "Edge", new Color(1f, 1f, 1f, 0.16f), Icons.Rounded(8, 2));
            Label(r, "T", key, fs, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(w + 20f, h), Ink, FontKind.Head, null, false);
            return r;
        }

        public static RectTransform Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fillCol, out Image fill, Vector2? pivot = null)
        {
            RectTransform r = Node(parent, name, anchor, pos, size, pivot);
            StretchImage(r, "Bg", new Color(0f, 0f, 0f, 0.55f), Icons.Rounded(3));
            RectTransform fr = Rect(r, "Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size.x, 0f));
            fill = fr.gameObject.AddComponent<Image>(); fill.sprite = Icons.Rounded(3); fill.type = Image.Type.Sliced; fill.color = fillCol; fill.raycastTarget = false;
            return r;
        }
        public static void SetBar(Image fill, float width, float v)
        {
            v = Mathf.Clamp01(v);
            fill.rectTransform.sizeDelta = new Vector2(width * v, 0f);
            fill.enabled = v > 0.01f;
        }

        public static RectTransform Chip(Transform parent, string text, Vector2 anchor, Vector2 pos, int fontSize = 22, Color? col = null, string icon = null, Vector2? pivot = null)
        {
            RectTransform r = Node(parent, "Chip", anchor, pos, new Vector2(100f, fontSize * 1.9f), pivot);
            Image bg = StretchImage(r, "Bg", new Color(1f, 1f, 1f, 0.06f), Icons.Rounded(Mathf.RoundToInt(fontSize * 0.9f)));
            StretchImage(r, "Edge", new Color(1f, 0.93f, 0.82f, 0.16f), Icons.Rounded(Mathf.RoundToInt(fontSize * 0.9f), 2));
            Text t = Label(r, "T", text, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, fontSize * 1.6f), col ?? Ink, FontKind.Head, null, false);
            float tw = t.preferredWidth;
            float iw = icon != null ? fontSize * 1.1f + 8f : 0f;
            r.sizeDelta = new Vector2(tw + iw + fontSize * 1.4f, fontSize * 1.9f);
            t.rectTransform.sizeDelta = new Vector2(tw + 4f, fontSize * 1.6f);
            t.rectTransform.anchoredPosition = new Vector2(iw * 0.5f, 0f);
            if (icon != null) Icon(r, "I", icon, new Vector2(0.5f, 0.5f), new Vector2(-(tw * 0.5f) - 4f, 0f), fontSize * 1.1f, col ?? Ink, new Vector2(0.5f, 0.5f));
            _ = bg;
            return r;
        }

        /// <summary>Susun anak-anak horizontal di tengah (untuk deretan chip).</summary>
        public static void RowCenter(RectTransform[] items, float y, float gap)
        {
            float total = -gap;
            foreach (RectTransform r in items) total += r.sizeDelta.x + gap;
            float x = -total * 0.5f;
            foreach (RectTransform r in items)
            {
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(x, y);
                x += r.sizeDelta.x + gap;
            }
        }
    }

    /// <summary>Efek hover tombol: warna, garis aksen, geser teks, skala (berjalan juga saat waktu dijeda).</summary>
    public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Text text; public Image bg, border, bar, icon, underline;
        public Color textN = UIKit.Ink, textH = UIKit.Gold, bgN = Color.clear, bgH = Color.clear, borderN = Color.clear, borderH = Color.clear;
        public float barN = 4f, barH = 10f, scaleH = 1f, slide = 0f;
        public bool selected;
        bool over, down, baseSet; float t; Vector2 textBase;

        public void OnPointerEnter(PointerEventData e) { over = true; AudioDirector.I?.UiHover(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnSelect(BaseEventData e) { over = true; }
        public void OnDeselect(BaseEventData e) { over = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
        void OnDisable() { over = false; down = false; t = 0f; Refresh(); }
        void Update()
        {
            t = Mathf.MoveTowards(t, over ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            Refresh();
        }
        public void Refresh()
        {
            float e = t * t * (3f - 2f * t);
            if (text != null)
            {
                text.color = Color.Lerp(textN, textH, selected ? 1f : e);
                if (slide != 0f)
                {
                    if (!baseSet) { textBase = text.rectTransform.anchoredPosition; baseSet = true; }
                    text.rectTransform.anchoredPosition = textBase + new Vector2(slide * e, 0f);
                }
            }
            if (icon != null) icon.color = text != null ? text.color : Color.Lerp(textN, textH, e);
            if (bg != null) bg.color = Color.Lerp(bgN, bgH, e);
            if (border != null) border.color = Color.Lerp(borderN, borderH, e);
            if (bar != null) { RectTransform r = bar.rectTransform; r.sizeDelta = new Vector2(Mathf.Lerp(barN, barH, e), r.sizeDelta.y); }
            if (underline != null) underline.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, selected ? 1f : e * 0.45f);
            float s = Mathf.Lerp(1f, scaleH, e) * (down ? 0.97f : 1f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Animasi saklar (toggle) pengaturan.</summary>
    public class SwitchAnim : MonoBehaviour
    {
        public Image track; public RectTransform knob; public bool on;
        float t;
        public void Snap() { t = on ? 1f : 0f; Apply(); }
        void Update() { t = Mathf.MoveTowards(t, on ? 1f : 0f, Time.unscaledDeltaTime * 8f); Apply(); }
        void Apply()
        {
            float e = t * t * (3f - 2f * t);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(4f, 34f, e), 0f);
            if (track != null) track.color = Color.Lerp(new Color(1f, 1f, 1f, 0.14f), UIKit.Accent, e);
        }
    }

    /// <summary>Layar muncul dengan transisi halus (alpha) - berjalan juga saat waktu dijeda.</summary>
    public class ScreenFade : MonoBehaviour
    {
        CanvasGroup cg; float t;
        public float speed = 5f;
        void Awake() { cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); }
        void OnEnable() { t = 0f; if (cg != null) cg.alpha = 0f; }
        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.MoveTowards(t, 1f, Time.unscaledDeltaTime * speed);
            cg.alpha = t * t * (3f - 2f * t);
        }
    }
}
