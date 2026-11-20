using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>
    /// HUD permainan: tujuan, kompas, peta mini, senter & stamina, prompt interaksi, panduan awal, panel presentasi.
    /// Informasi ditata dalam jalur tetap supaya tidak saling menimpa:
    ///   1. indikator kewaspadaan hantu (atas tengah, di bawah kompas) - satu elemen stabil yang hanya berubah saat keadaan hantu berubah;
    ///   2. notifikasi peristiwa (di bawahnya) - antre, tanpa duplikat, tidak memotong pesan sebelumnya;
    ///   3. petunjuk (bawah tengah) - antre, ditahan selama judul rumah / peta penuh tampil; status sembunyi menumpuk di atasnya.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public RectTransform root;
        public MapUI map;
        public bool present, showDebug;
        public bool TutorialActive { get; private set; }

        Image edge, hideL, hideR, hideTop, crossDot, crossRing, keySlot, battIcon, stamFill, hintIcon, promptBg;
        readonly Image[] relicSlots = new Image[6];
        readonly Image[] battSegs = new Image[10];
        Text objTitle, objText, objMeta, battPct, battMode, promptTxt, hintTxt, feedTxt, debugTxt;
        RectTransform hintRt, promptRt, promptKey, tutCard, debugCard, feedCard, hiddenPill;
        CanvasGroup hintCg, tutCg, stamCg;
        float hintT, hintAge, stamShow, debugT, hiddenY = 150f;
        string lastPrompt;
        readonly HashSet<string> shownHints = new HashSet<string>();

        // notifikasi peristiwa (antre)
        class Note { public string title, sub, icon; public Color tint; public bool menu; public float dur, minShow; }
        readonly List<Note> notes = new List<Note>();
        Note cur; float curT, curAge, toastY = -106f; bool curHeld, toastBottom;
        RectTransform toastRt; CanvasGroup toastCg; Image toastIcon, toastAccent; Text toastTitle, toastSub;

        // kewaspadaan hantu
        RectTransform alertRt; CanvasGroup alertCg; Image alertIcon, alertFill, alertBorder; Text alertTxt;
        string alertKey = ""; float alertPop, lostT, heardHold; bool alertOn; GhostState lastGhostState = GhostState.Patroli;

        // petunjuk (antre)
        readonly List<string[]> hintQueue = new List<string[]>();
        string[] curHint; bool hintHeld;

        // panduan awal (rumah pertama pemain baru)
        static readonly string[] TutSteps =
        {
            "Bergerak dengan W A S D",
            "Matikan / nyalakan senter (F)",
            "Buka peta rumah (M)",
            "Ikuti kompas, ambil relik pertama (E)",
            "Coba bersembunyi di lemari / kolong ranjang (E)",
        };
        readonly bool[] tutDone = new bool[5];
        readonly Text[] tutRows = new Text[5];
        readonly Image[] tutIcons = new Image[5];
        Vector3 tutStart; bool tutLastFlash, basicsAnnounced; int tutMapOpens; float tutEndT;

        public static bool TutDone { get { return PlayerPrefs.GetInt("HB_TutDone", 0) == 1; } set { PlayerPrefs.SetInt("HB_TutDone", value ? 1 : 0); PlayerPrefs.Save(); } }

        // tata letak kolom kiri (kartu tujuan 28..548, panel presentasi 28..28+DebugW) dan batas tinggi panel presentasi
        public const float DebugW = 580f;
        /// <summary>Tinggi maks panel presentasi: mulai di bawah kartu tujuan, berhenti di atas petunjuk & kartu senter.</summary>
        public static float DebugMaxH(float canvasH) { return Mathf.Clamp(canvasH - 172f - 290f, 220f, 618f); }
        /// <summary>Lebar maks notifikasi tengah agar tidak menyentuh kolom kiri (simetris, jadi juga aman dari peta mini).</summary>
        public static float ToastMaxW(float canvasW, bool debugPanel) { return canvasW - 2f * ((debugPanel ? 28f + DebugW : 548f) + 20f); }

        // keadaan untuk pengujian otomatis
        public string ToastNow { get { return cur != null ? cur.title : ""; } }
        public bool ToastShown { get { return cur != null && toastRt.gameObject.activeSelf && toastCg.alpha > 0.5f; } }
        public int ToastQueued { get { return notes.Count; } }
        public bool ToastQueuedContains(string s) { foreach (Note n in notes) if (n.title.Contains(s)) return true; return cur != null && cur.title.Contains(s); }
        public string AlertNow { get { return alertOn ? alertTxt.text : ""; } }
        public float AlertAlpha { get { return alertCg.alpha; } }
        public string HintNow { get { return curHint != null && hintRt.gameObject.activeSelf && hintCg.alpha > 0.05f ? curHint[0] : ""; } }
        public int HintQueued { get { return hintQueue.Count; } }
        public RectTransform ToastRect { get { return toastRt; } }
        public RectTransform AlertRect { get { return alertRt; } }
        public RectTransform HintRect { get { return hintRt; } }
        public RectTransform HiddenRect { get { return hiddenPill; } }
        public RectTransform DebugRect { get { return debugCard; } }
        public RectTransform FeedRect { get { return feedCard; } }

        // ------------------------------------------------------------------ bangun
        public void Build(Transform canvas)
        {
            root = UIKit.Stretch(canvas, "HUD");
            edge = UIKit.Fill(root, "DangerEdge", new Color(0.5f, 0f, 0f, 0f));
            edge.sprite = Sprite.Create(Look.Radial("edge", 0.45f), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            // overlay sembunyi: celah pintu lemari / kasur di atas kepala
            hideL = UIKit.Panel(root, "HideL", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900f, 1200f), new Color(0f, 0f, 0f, 0.93f), new Vector2(0f, 0.5f));
            hideR = UIKit.Panel(root, "HideR", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(900f, 1200f), new Color(0f, 0f, 0f, 0.93f), new Vector2(1f, 0.5f));
            hideTop = UIKit.Panel(root, "HideTop", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2400f, 640f), new Color(0.02f, 0.02f, 0.02f, 0.96f), new Vector2(0.5f, 1f));

            // tujuan (kiri atas)
            RectTransform obj = UIKit.Card(root, "Objective", new Vector2(0f, 1f), new Vector2(28f, -26f), new Vector2(520f, 128f), new Vector2(0f, 1f));
            for (int i = 0; i < relicSlots.Length; i++) relicSlots[i] = UIKit.Icon(obj, "Relic" + i, "relic", new Vector2(0f, 1f), new Vector2(20f + i * 34f, -16f), 30f, UIKit.Faint, new Vector2(0f, 1f));
            objTitle = UIKit.Label(obj, "Title", "", 24, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(130f, -31f), new Vector2(300f, 34f), UIKit.Ink, FontKind.Head, new Vector2(0f, 0.5f));
            keySlot = UIKit.Icon(obj, "Key", "key", new Vector2(1f, 1f), new Vector2(-20f, -16f), 30f, UIKit.Faint, new Vector2(1f, 1f));
            objText = UIKit.Label(obj, "Text", "", 21, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(22f, -58f), new Vector2(480f, 30f), UIKit.Ink, FontKind.Body, new Vector2(0f, 1f));
            objMeta = UIKit.Label(obj, "Meta", "", 17, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(22f, -94f), new Vector2(480f, 24f), UIKit.Dim, FontKind.Head, new Vector2(0f, 1f));

            // senter & stamina (kiri bawah)
            RectTransform fc = UIKit.Card(root, "Flash", new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(390f, 106f), new Vector2(0f, 0f));
            battIcon = UIKit.Icon(fc, "Icon", "flash", new Vector2(0f, 1f), new Vector2(16f, -12f), 42f, UIKit.Gold, new Vector2(0f, 1f));
            UIKit.Label(fc, "Lbl", "SENTER", 18, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(70f, -22f), new Vector2(110f, 24f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
            battMode = UIKit.Label(fc, "Mode", "", 17, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(156f, -22f), new Vector2(130f, 24f), UIKit.Gold, FontKind.Head, new Vector2(0f, 0.5f));
            battPct = UIKit.Label(fc, "Pct", "100%", 22, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-18f, -22f), new Vector2(90f, 28f), UIKit.Ink, FontKind.Head, new Vector2(1f, 0.5f));
            for (int i = 0; i < battSegs.Length; i++) battSegs[i] = UIKit.Rounded(fc, "Seg" + i, new Vector2(0f, 1f), new Vector2(70f + i * 30f, -42f), new Vector2(26f, 12f), UIKit.Gold, 3, new Vector2(0f, 1f));
            RectTransform st = UIKit.Node(fc, "Stamina", new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(354f, 22f), new Vector2(0f, 0f));
            stamCg = st.gameObject.AddComponent<CanvasGroup>();
            UIKit.Label(st, "Lbl", "STAMINA", 15, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(90f, 20f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
            UIKit.Bar(st, "Bar", new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(266f, 7f), UIKit.Cold, out stamFill, new Vector2(0f, 0.5f));

            // bidikan & prompt interaksi
            crossDot = UIKit.Icon(root, "CrossDot", "disc", new Vector2(0.5f, 0.5f), Vector2.zero, 6f, new Color(1f, 1f, 1f, 0.75f));
            crossRing = UIKit.Icon(root, "CrossRing", "ring", new Vector2(0.5f, 0.5f), Vector2.zero, 28f, UIKit.Gold);
            promptRt = UIKit.Node(root, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(600f, 54f));
            promptBg = UIKit.Rounded(promptRt, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 54f), new Color(0f, 0f, 0f, 0.6f), 12);
            promptKey = UIKit.Keycap(promptRt, "E", new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), 36f, new Vector2(0f, 0.5f));
            promptTxt = UIKit.Label(promptRt, "Text", "", 23, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 40f), UIKit.Ink, FontKind.Head, new Vector2(0f, 0.5f));

            // peta mini + kompas
            map = gameObject.AddComponent<MapUI>();
            map.Build(root);

            // petunjuk (bawah tengah)
            hintRt = UIKit.Card(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 72f), new Vector2(0.5f, 0f), new Color(0.05f, 0.06f, 0.08f, 0.93f), 14);
            hintCg = hintRt.gameObject.AddComponent<CanvasGroup>();
            hintIcon = UIKit.Icon(hintRt, "Icon", "info", new Vector2(0f, 0.5f), new Vector2(22f, 0f), 30f, UIKit.Cold, new Vector2(0f, 0.5f));
            hintTxt = UIKit.Label(hintRt, "Text", "", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(800f, 60f), new Color(0.88f, 0.92f, 1f), FontKind.Body, new Vector2(0f, 0.5f));

            // status bersembunyi (bawah tengah; naik ke atas petunjuk bila petunjuk sedang tampil)
            hiddenPill = UIKit.Card(root, "Hidden", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(560f, 52f), new Vector2(0.5f, 0f), new Color(0.03f, 0.045f, 0.06f, 0.88f), 12, false);
            UIKit.Icon(hiddenPill, "I", "eyeoff", new Vector2(0f, 0.5f), new Vector2(18f, 0f), 30f, UIKit.Cold, new Vector2(0f, 0.5f));
            Text ht = UIKit.Label(hiddenPill, "T", "TERSEMBUNYI  ·  hantu tidak bisa melihatmu", 20, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(520f, 40f), UIKit.Cold, FontKind.Head, new Vector2(0f, 0.5f));
            hiddenPill.sizeDelta = new Vector2(ht.preferredWidth + 84f, 52f);

            // panduan awal (kiri, di bawah kartu tujuan)
            tutCard = UIKit.Card(root, "Tutorial", new Vector2(0f, 1f), new Vector2(28f, -172f), new Vector2(520f, 256f), new Vector2(0f, 1f));
            tutCg = tutCard.gameObject.AddComponent<CanvasGroup>();
            UIKit.Icon(tutCard, "I", "info", new Vector2(0f, 1f), new Vector2(20f, -16f), 26f, UIKit.Gold, new Vector2(0f, 1f));
            UIKit.Label(tutCard, "Title", "PANDUAN AWAL", 20, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(56f, -29f), new Vector2(300f, 28f), UIKit.Gold, FontKind.Head, new Vector2(0f, 0.5f));
            for (int i = 0; i < 5; i++)
            {
                tutIcons[i] = UIKit.Icon(tutCard, "C" + i, "box", new Vector2(0f, 1f), new Vector2(22f, -62f - i * 37f), 22f, UIKit.Faint, new Vector2(0f, 1f));
                tutRows[i] = UIKit.Label(tutCard, "R" + i, TutSteps[i], 20, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(56f, -73f - i * 37f), new Vector2(450f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 0.5f));
            }
            tutCard.gameObject.SetActive(false);

            // mode presentasi: panel PCG/DDA/AI (kiri, di bawah kartu tujuan) + memori hantu (kanan, di bawah peta mini).
            // Tinggi panel mengikuti isi dan dibatasi agar tidak menutupi petunjuk, kartu senter, atau prompt.
            debugCard = UIKit.Card(root, "Debug", new Vector2(0f, 1f), new Vector2(28f, -172f), new Vector2(DebugW, 560f), new Vector2(0f, 1f), new Color(0.03f, 0.03f, 0.035f, 0.92f));
            debugTxt = UIKit.Label(debugCard, "Text", "", 16, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(DebugW - 38f, 530f), UIKit.Ink, FontKind.Body, new Vector2(0f, 1f), false);
            debugTxt.verticalOverflow = VerticalWrapMode.Truncate;
            feedCard = UIKit.Card(root, "Feed", new Vector2(1f, 1f), new Vector2(-28f, -356f), new Vector2(600f, 120f), new Vector2(1f, 1f), new Color(0.04f, 0.03f, 0.035f, 0.92f));
            UIKit.Icon(feedCard, "I", "ghost", new Vector2(0f, 1f), new Vector2(16f, -12f), 22f, UIKit.Gold, new Vector2(0f, 1f));
            UIKit.Label(feedCard, "Title", "MEMORI HANTU  ·  LANGSUNG", 16, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(46f, -23f), new Vector2(500f, 22f), UIKit.Gold, FontKind.Head, new Vector2(0f, 0.5f));
            feedTxt = UIKit.Label(feedCard, "Text", "", 16, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(18f, -46f), new Vector2(564f, 60f), new Color(1f, 0.86f, 0.8f), FontKind.Body, new Vector2(0f, 1f), false);
            feedTxt.verticalOverflow = VerticalWrapMode.Truncate;

            map.BuildFull(root);

            // indikator kewaspadaan hantu - dibangun setelah peta penuh agar tetap terlihat saat peta dibuka
            alertRt = UIKit.Card(root, "GhostAlert", new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(380f, 42f), new Vector2(0.5f, 1f), new Color(0.05f, 0.045f, 0.045f, 0.92f), 21);
            alertCg = alertRt.gameObject.AddComponent<CanvasGroup>(); alertCg.alpha = 0f; alertCg.blocksRaycasts = false;
            alertFill = alertRt.Find("Fill").GetComponent<Image>();
            alertBorder = alertRt.Find("Border").GetComponent<Image>();
            alertIcon = UIKit.Icon(alertRt, "Icon", "sound", new Vector2(0f, 0.5f), new Vector2(16f, 0f), 24f, UIKit.Gold, new Vector2(0f, 0.5f));
            alertTxt = UIKit.Label(alertRt, "Text", "", 19, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(520f, 30f), UIKit.Ink, FontKind.Head, new Vector2(0f, 0.5f));
            alertRt.gameObject.SetActive(false);

            foreach (string id in PlayerPrefs.GetString("HB_HintsShown", "").Split(',')) if (id.Length > 0) shownHints.Add(id);
            hintRt.gameObject.SetActive(false);
        }

        /// <summary>Lapisan notifikasi peristiwa, dibangun setelah layar menu agar umpan balik tombol menu juga terlihat.</summary>
        public void BuildOverlay(Transform canvas)
        {
            toastRt = UIKit.Card(canvas, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(420f, 56f), new Vector2(0.5f, 1f), new Color(0.06f, 0.055f, 0.055f, 0.94f), 14);
            toastCg = toastRt.gameObject.AddComponent<CanvasGroup>(); toastCg.blocksRaycasts = false; toastCg.interactable = false;
            toastAccent = UIKit.Rounded(toastRt, "Accent", new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(4f, 30f), UIKit.Gold, 2, new Vector2(0f, 0.5f));
            toastIcon = UIKit.Icon(toastRt, "Icon", "info", new Vector2(0f, 0.5f), new Vector2(26f, 0f), 30f, UIKit.Gold, new Vector2(0f, 0.5f));
            toastTitle = UIKit.Label(toastRt, "Title", "", 22, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(70f, -13f), new Vector2(800f, 30f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            toastSub = UIKit.Label(toastRt, "Sub", "", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(70f, -45f), new Vector2(800f, 26f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            toastRt.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API
        /// <summary>
        /// Notifikasi peristiwa (relik, kunci, baterai, ...). Pesan yang sama dengan yang sedang tampil hanya diperpanjang,
        /// pesan baru masuk antrean sehingga tidak memotong pesan sebelumnya. menu = boleh tampil di atas layar menu.
        /// </summary>
        public void Toast(string title, string sub = null, string icon = "info", Color? tint = null, bool menu = false)
        {
            if (string.IsNullOrEmpty(title)) return;
            sub = sub ?? "";
            if (cur != null && cur.title == title)
            {
                curT = Mathf.Max(curT, cur.dur * 0.6f);
                if (sub != cur.sub) { cur.sub = sub; LayoutToast(); }
                return;
            }
            foreach (Note q in notes) if (q.title == title) { q.sub = sub; return; }
            bool two = sub.Length > 0;
            notes.Add(new Note { title = title, sub = sub, icon = string.IsNullOrEmpty(icon) ? "info" : icon, tint = tint ?? UIKit.Gold, menu = menu, dur = two ? 4.6f : 3.0f, minShow = two ? 2.6f : 1.6f });
            while (notes.Count > 4) notes.RemoveAt(0);
        }

        /// <summary>Petunjuk kontekstual (sekali per pemain, bisa dimatikan di Pengaturan). Antre; petunjuk bahaya didahulukan.</summary>
        public void Hint(string id, string text, string icon = "info")
        {
            if (!Settings.Hints || shownHints.Contains(id)) return;
            shownHints.Add(id);
            PlayerPrefs.SetString("HB_HintsShown", string.Join(",", shownHints));
            string[] h = { id, text, icon };
            bool urgent = id == "ghost" || id == "hide" || id == "guard";
            if (urgent && curHint != null) { hintQueue.Insert(0, curHint); curHint = null; hintRt.gameObject.SetActive(false); }
            if (urgent) hintQueue.Insert(0, h); else hintQueue.Add(h);
        }

        public void ReloadShownHints()
        {
            shownHints.Clear();
            foreach (string id in PlayerPrefs.GetString("HB_HintsShown", "").Split(',')) if (id.Length > 0) shownHints.Add(id);
        }

        public static void ResetHints()
        {
            PlayerPrefs.DeleteKey("HB_HintsShown"); PlayerPrefs.DeleteKey("HB_TutDone"); PlayerPrefs.DeleteKey("HB_GuideSeen"); PlayerPrefs.Save();
            if (GameManager.I != null && GameManager.I.ui != null) GameManager.I.ui.hud.shownHints.Clear();
        }

        public void SetVisible(bool on) { root.gameObject.SetActive(on); }

        /// <summary>Dipanggil tiap rumah dimulai. tutorial = panduan awal aktif; resetSteps = mulai checklist dari awal.</summary>
        public void BeginHouse(bool tutorial, bool resetSteps)
        {
            GameManager gm = GameManager.I;
            TutorialActive = tutorial;
            if (resetSteps) for (int i = 0; i < 5; i++) tutDone[i] = false;
            tutStart = gm.player.transform.position;
            tutLastFlash = gm.player.flashOn; tutMapOpens = map.fullOpens; tutEndT = 0f; basicsAnnounced = tutDone[0] && tutDone[1] && tutDone[2];
            tutCard.gameObject.SetActive(tutorial); tutCg.alpha = 1f;
            // pesan rumah sebelumnya tidak dibawa ke rumah baru; petunjuk yang belum sempat tampil boleh muncul lagi nanti
            notes.Clear(); cur = null; if (toastRt != null) toastRt.gameObject.SetActive(false);
            foreach (string[] h in hintQueue) shownHints.Remove(h[0]);
            PlayerPrefs.SetString("HB_HintsShown", string.Join(",", shownHints));
            hintQueue.Clear(); curHint = null; hintT = 0f; hintRt.gameObject.SetActive(false);
            lostT = 0f; heardHold = 0f; alertKey = ""; alertOn = false; alertCg.alpha = 0f; alertRt.gameObject.SetActive(false); lastGhostState = GhostState.Patroli;
            map.SetFull(false);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.layout == null || gm.player == null) return;
            PlayerController p = gm.player; GhostAI gh = gm.ghost; HouseLayout L = gm.layout;
            float dt = Time.unscaledDeltaTime;
            if (gm.Playing)
            {
                if (Input.GetKeyDown(KeyCode.F1) && !Playtest.Active) { present = !present; showDebug = present; Toast(present ? "Mode presentasi" : "Mode presentasi dimatikan", present ? "Panel PCG / DDA / AI, keadaan hantu & heatmap jalur di peta" : null); }
                if (Input.GetKeyDown(KeyCode.Tab) && !Playtest.Active) showDebug = !showDebug;
                if (Input.GetKeyDown(KeyCode.M)) map.ToggleFull();
            }
            map.present = present;
            Guidance g = map.guide;

            // kartu tujuan
            int need = Mathf.Min(gm.RelicsNeeded, relicSlots.Length);
            for (int i = 0; i < relicSlots.Length; i++)
            {
                bool on = i < need;
                if (relicSlots[i].gameObject.activeSelf != on) relicSlots[i].gameObject.SetActive(on);
                if (on) relicSlots[i].color = i < gm.relicsFound ? UIKit.Gold : new Color(1f, 1f, 1f, 0.16f);
            }
            objTitle.rectTransform.anchoredPosition = new Vector2(20f + need * 34f + 8f, -31f);
            objTitle.text = "RELIK " + gm.relicsFound + " / " + gm.RelicsNeeded;
            bool hasLock = L.lockedRoom >= 0 && gm.house.lockedDoor != null;
            if (keySlot.gameObject.activeSelf != hasLock) keySlot.gameObject.SetActive(hasLock);
            if (hasLock) keySlot.color = !gm.house.lockedDoor.locked ? new Color(1f, 1f, 1f, 0.22f) : p.hasKey ? new Color(1f, 0.45f, 0.38f) : new Color(1f, 1f, 1f, 0.16f);
            string dist = g.kind != Guidance.Kind.None && Settings.Compass ? "   <color=#ffcc73>" + Mathf.RoundToInt(g.distance) + " m</color>" : "";
            objText.text = (g.text.Length > 0 ? g.text : "Jelajahi rumah") + dist;
            objMeta.text = Playtest.Active ? Playtest.I.TimerText() + "   ·   " + L.FloorName(p.floor).ToUpper() : L.FloorName(p.floor).ToUpper() + "   ·   RUMAH #" + gm.houseNo + (present ? "   ·   DDA " + gm.dda.Label + " " + Mathf.RoundToInt(gm.dda.d * 100f) + "%" : "");

            // senter & stamina
            bool lightOn = p.flashOn && p.battery > 0f && !p.hidden;
            int segs = Mathf.CeilToInt(p.battery / 10f - 0.001f);
            Color bc = p.battery < 20f ? UIKit.Danger : UIKit.Gold;
            for (int i = 0; i < battSegs.Length; i++) battSegs[i].color = i < segs ? (lightOn ? bc : new Color(bc.r, bc.g, bc.b, 0.4f)) : new Color(1f, 1f, 1f, 0.08f);
            battPct.text = Mathf.CeilToInt(p.battery) + "%";
            battIcon.color = lightOn ? bc : UIKit.Faint;
            battMode.text = p.battery <= 0f ? "HABIS" : !p.flashOn ? "MATI" : p.focusBeam ? "FOKUS" : "";
            UIKit.SetBar(stamFill, 266f, p.stamina / 100f);
            stamFill.color = p.exhausted ? new Color(0.5f, 0.5f, 0.55f) : UIKit.Cold;
            stamShow = Mathf.MoveTowards(stamShow, p.stamina < 99f ? 1f : 0.35f, dt * 2f);
            stamCg.alpha = stamShow;

            // bidikan & prompt
            bool aim = gm.Playing && !p.hidden;
            crossDot.enabled = aim;
            crossRing.enabled = aim && p.focus != null;
            if (crossRing.enabled) { float s = 26f + Mathf.Sin(Time.unscaledTime * 5f) * 2f; crossRing.rectTransform.sizeDelta = new Vector2(s, s); }
            SetPrompt(gm.Playing ? p.focusPrompt : "");

            // bahaya
            float gdist = gh != null ? gh.DistanceTo(p.transform.position) : 99f;
            float danger = Mathf.Clamp01(1f - gdist / 11f) * (gh != null && gh.state == GhostState.Kejar ? 1f : 0.55f);
            if (gh != null && (gh.frozen || gh.Asleep)) danger = 0f;
            edge.color = new Color(0.45f, 0f, 0f, Mathf.Lerp(edge.color.a, gm.Playing ? danger * 0.55f : 0f, dt * 4f));
            if (Atmosphere.I != null) Atmosphere.I.SetDanger(gm.Playing ? danger : 0f);

            // bersembunyi
            bool wardrobe = p.hidden && p.spot != null && p.spot.kind == SpotKind.Lemari;
            bool bed = p.hidden && p.spot != null && p.spot.kind == SpotKind.KolongRanjang;
            hideL.enabled = hideR.enabled = wardrobe;
            if (wardrobe) { float gap = 70f + Mathf.Sin(Time.time * 0.7f) * 6f; hideL.rectTransform.sizeDelta = new Vector2(960f - gap, 1200f); hideR.rectTransform.sizeDelta = new Vector2(960f - gap, 1200f); }
            hideTop.enabled = bed;

            // informasi: kewaspadaan hantu -> notifikasi peristiwa -> petunjuk -> status sembunyi (urutan penting untuk tata letak)
            UpdateAlert(gm, gh, dt);
            UpdateToast(gm, dt);
            UpdateHint(gm, dt);
            bool hp = p.hidden && gm.Playing;
            float hiddenTarget = hintRt.gameObject.activeSelf && hintCg.alpha > 0.02f ? 150f + hintRt.sizeDelta.y + 14f : 150f;
            if (hiddenPill.gameObject.activeSelf != hp) { hiddenPill.gameObject.SetActive(hp); hiddenY = hiddenTarget; }
            hiddenY = Mathf.Lerp(hiddenY, hiddenTarget, Mathf.Clamp01(dt * 10f));
            hiddenPill.anchoredPosition = new Vector2(0f, hiddenY);

            // presentasi
            bool fd = present && gm.Playing;
            if (feedCard.gameObject.activeSelf != fd) feedCard.gameObject.SetActive(fd);
            if (fd) UpdateFeed();
            bool dbg = showDebug && gm.Playing;
            if (debugCard.gameObject.activeSelf != dbg) { debugCard.gameObject.SetActive(dbg); debugT = 0f; }
            debugT -= dt;
            if (dbg && debugT <= 0f)
            {
                debugT = 0.2f;
                debugTxt.text = DebugText(gm);
                float h = Mathf.Min(debugTxt.preferredHeight + 34f, DebugMaxH(root.rect.height));
                debugCard.sizeDelta = new Vector2(DebugW, h);
                debugTxt.rectTransform.sizeDelta = new Vector2(DebugW - 38f, h - 28f);
            }

            UpdateTutorial(gm);

            // petunjuk kontekstual
            if (gm.Playing && gm.houseTime > 1.5f)
            {
                Room pr = L.RoomAt(p.node);
                if (TutorialActive) Hint("welcome", "Selamat datang. Ikuti PANDUAN AWAL di kiri layar - hantu belum bangun sampai kamu siap.");
                else if (gm.houseTime > 8f) Hint("goal", "Kumpulkan semua RELIK lalu kabur lewat PINTU KELUAR. Kompas di atas layar menunjuk tujuan berikutnya.", "relic");
                if (!TutorialActive && gm.houseTime > 30f) Hint("map", "Tekan M untuk melihat denah lengkap rumah beserta jalur ke tujuan.", "map");
                if (p.focus is RelicPickup) Hint("relic", "Tekan E untuk mengambil relik.", "relic");
                if (gh != null && !gh.Asleep && (gh.state == GhostState.Kejar || (gh.canSee && gdist < 12f))) Hint("ghost", "HANTU! Lari (Shift) atau sembunyi di lemari / kolong ranjang (E). Di persembunyian ia tidak bisa melihatmu.", "ghost");
                if (p.hidden) Hint("hide", "Di persembunyian hantu TIDAK bisa melihat atau menemukanmu. Ia hanya mengingat kebiasaanmu - intip dulu sebelum keluar.", "hide");
                if (p.hidden && gh != null && gh.state == GhostState.Jaga && gdist < 10f) Hint("guard", "Hantu berjaga di dekat sini karena hafal kebiasaanmu. Tunggu sampai ia pergi.", "eye");
                if (p.battery < 30f) Hint("battery", "Senter hampir habis. Ikon baterai hijau muncul di kompas dan peta.", "battery");
                if (lightOn && pr != null && !pr.lit && gm.houseTime > 20f) Hint("focus", "Tahan KLIK KANAN untuk memfokuskan senter - lebih terang dan jauh, tapi boros baterai.", "flash");
                if (p.focus is LockedDoor) Hint("locked", "Pintu digembok. Cari KUNCI TUA - ikon kunci merah ada di peta (M).", "lock");
                if (L.floors > 1 && L.stairs.Count > 0 && Vector3.Distance(p.transform.position, G.W(L.stairs[0].BottomNode)) < 4f) Hint("stairs", "Tangga ke lantai 2. Panah kecil di kompas menandai kapan kamu harus naik atau turun.", "stairs");
                if (gm.relicsFound >= gm.RelicsNeeded) Hint("exit", "Semua relik terkumpul! Ikuti kompas ke PINTU KELUAR di lantai 1.", "exit");
                if (p.noiseLevel >= PlayerController.NoiseRun) Hint("noise", "Berlari itu berisik - hantu bisa mendengar langkahmu. Jongkok (C) paling senyap.", "sound");
            }
        }

        // ------------------------------------------------------------------ kewaspadaan hantu (satu indikator stabil)
        void UpdateAlert(GameManager gm, GhostAI gh, float dt)
        {
            int level = 0; string text = "", icon = "ghost"; Color col = UIKit.Ink;
            bool live = gh != null && gm.Playing && !gh.frozen && !gh.Asleep;
            if (gh != null)
            {
                if (live && gh.state == GhostState.Selidiki && lastGhostState != GhostState.Selidiki) heardHold = 2.5f;     // tampil minimal 2,5 dtk
                if (live && lastGhostState == GhostState.Kejar && (gh.state == GhostState.Cari || gh.state == GhostState.Periksa)) lostT = 3.2f;
                if (!live || gh.state == GhostState.Kejar) lostT = 0f;
                lastGhostState = gh.state;
            }
            heardHold -= dt; lostT -= dt;
            if (live)
            {
                if (gh.state == GhostState.Kejar) { level = 3; text = "HANTU MELIHATMU"; icon = "eye"; col = UIKit.Danger; }
                else if (gh.state == GhostState.Selidiki || heardHold > 0f) { level = 2; text = "HANTU MENDENGAR SESUATU"; icon = "sound"; col = UIKit.Gold; }
                else if (lostT > 0f) { level = 1; text = "HANTU KEHILANGAN JEJAKMU"; icon = "eyeoff"; col = UIKit.Cold; }
            }
            if (present && gh != null && gm.Playing)
            {
                // mode presentasi: indikator selalu tampil dan menyebut state FSM hantu
                string flag = gh.frozen ? "  ·  BEKU" : gh.Asleep ? "  ·  TIDUR" : gh.canSee ? "  ·  MELIHATMU" : gh.heardRecently ? "  ·  MENDENGAR" : "";
                text = "HANTU: " + gh.state.ToString().ToUpper() + flag;
                col = StateCol(gh.state);
                if (level == 0) icon = "ghost";
                level = Mathf.Max(level, 1);
            }
            bool on = level > 0;
            string key = text + "|" + icon;
            if (on && key != alertKey)
            {
                alertKey = key;
                alertTxt.text = text;
                alertTxt.color = level >= 3 ? new Color(1f, 0.82f, 0.78f) : UIKit.Ink;
                alertIcon.sprite = Icons.Get(icon); alertIcon.color = col;
                alertFill.color = new Color(col.r * 0.16f + 0.03f, col.g * 0.12f + 0.03f, col.b * 0.12f + 0.03f, 0.93f);
                alertBorder.color = new Color(col.r, col.g, col.b, 0.5f);
                alertRt.sizeDelta = new Vector2(alertTxt.preferredWidth + 50f + 26f, 42f);
                alertPop = 1f;
            }
            if (on && !alertRt.gameObject.activeSelf) { alertRt.gameObject.SetActive(true); alertCg.alpha = 0f; }
            alertCg.alpha = Mathf.MoveTowards(alertCg.alpha, on ? 1f : 0f, dt * (on ? 6f : 2.5f));
            if (!on && alertCg.alpha <= 0f && alertRt.gameObject.activeSelf) { alertRt.gameObject.SetActive(false); alertKey = ""; }
            alertOn = alertRt.gameObject.activeSelf && (on || alertCg.alpha > 0.05f);
            alertPop = Mathf.MoveTowards(alertPop, 0f, dt * 4f);
            alertRt.localScale = Vector3.one * (1f + alertPop * 0.07f);
            alertIcon.rectTransform.localScale = Vector3.one * (1f + (level >= 3 ? Mathf.Sin(Time.unscaledTime * 9f) * 0.12f : 0f));
            // di bawah kompas; saat peta penuh dibuka pindah ke bawah layar agar tidak menutupi denah
            Vector2 anc = map.fullOpen ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            if (alertRt.anchorMin != anc) { alertRt.anchorMin = anc; alertRt.anchorMax = anc; alertRt.pivot = anc; }
            alertRt.anchoredPosition = new Vector2(0f, map.fullOpen ? 22f : -106f);
        }

        static Color StateCol(GhostState s)
        {
            switch (s)
            {
                case GhostState.Kejar: return UIKit.Danger;
                case GhostState.Hadang: case GhostState.Jaga: return new Color(0.82f, 0.55f, 1f);
                case GhostState.Patroli: return new Color(0.62f, 0.78f, 1f);
                case GhostState.Muncul: return new Color(0.7f, 0.7f, 0.7f);
                default: return UIKit.Gold;
            }
        }

        // ------------------------------------------------------------------ notifikasi peristiwa (antre)
        void UpdateToast(GameManager gm, float dt)
        {
            if (toastRt == null) return;
            bool menuOpen = gm.ui != null && gm.ui.AnyMenu;
            bool gameOk = gm.Playing && root.gameObject.activeSelf && !map.fullOpen && !(gm.ui != null && gm.ui.IntroActive);
            if (cur == null)
            {
                int idx = -1;
                for (int i = 0; i < notes.Count; i++) if (notes[i].menu ? (menuOpen || gameOk) : gameOk) { idx = i; break; }
                if (idx < 0) { if (toastRt.gameObject.activeSelf) toastRt.gameObject.SetActive(false); return; }
                cur = notes[idx]; notes.RemoveAt(idx);
                curT = cur.dur; curAge = 0f; curHeld = false;
                LayoutToast();
                toastRt.gameObject.SetActive(true);
            }
            bool ok = cur.menu ? (menuOpen || gameOk) : gameOk;
            if (!ok) { toastCg.alpha = 0f; curHeld = true; return; }           // ditahan (peta penuh, judul rumah, menu) lalu dilanjutkan
            if (curHeld) { curHeld = false; curAge = 0f; curT = Mathf.Max(curT, cur.minShow); }
            curAge += dt; curT -= dt;
            bool waiting = false;
            foreach (Note q in notes) if (q.menu ? (menuOpen || gameOk) : gameOk) { waiting = true; break; }
            if (waiting && curAge >= cur.minShow) curT = Mathf.Min(curT, 0.3f);     // ada pesan menunggu: tutup setelah waktu baca minimum
            float fin = Mathf.Clamp01(curAge / 0.2f);
            toastCg.alpha = Mathf.Min(fin, Mathf.Clamp01(curT / 0.3f));
            bool bottom = cur.menu && menuOpen;
            Vector2 anc = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            float target = bottom ? 34f : (alertOn ? -160f : -106f);
            if (bottom != toastBottom || toastRt.anchorMin != anc) { toastBottom = bottom; toastRt.anchorMin = anc; toastRt.anchorMax = anc; toastRt.pivot = anc; toastY = target; }
            if (curAge <= dt * 1.5f) toastY = target;                                   // pesan baru langsung di tempatnya
            toastY = Mathf.Lerp(toastY, target, Mathf.Clamp01(dt * 10f));
            toastRt.anchoredPosition = new Vector2(0f, toastY + (bottom ? -1f : 1f) * (1f - fin) * 10f);
            if (curT <= 0f) { cur = null; toastRt.gameObject.SetActive(false); }
        }

        void LayoutToast()
        {
            bool two = cur.sub.Length > 0;
            toastTitle.text = cur.title; toastSub.text = cur.sub;
            toastIcon.sprite = Icons.Get(cur.icon); toastIcon.color = cur.tint; toastAccent.color = cur.tint;
            float maxText = cur.menu ? 860f : Mathf.Clamp(ToastMaxW(root.rect.width, debugCard.gameObject.activeSelf) - 96f, 260f, 860f);
            float textW = Mathf.Min(Mathf.Max(toastTitle.preferredWidth, two ? toastSub.preferredWidth : 0f) + 8f, maxText);
            toastTitle.rectTransform.sizeDelta = new Vector2(textW, 30f);
            toastSub.rectTransform.sizeDelta = new Vector2(textW, 26f);
            float subH = two ? Mathf.Max(24f, toastSub.preferredHeight) : 0f;
            toastSub.rectTransform.sizeDelta = new Vector2(textW, subH);
            toastSub.gameObject.SetActive(two);
            float h = two ? 13f + 30f + 2f + subH + 13f : 56f;
            toastRt.sizeDelta = new Vector2(Mathf.Max(300f, textW + 70f + 26f), h);
            toastTitle.rectTransform.anchoredPosition = new Vector2(70f, -13f);
            toastSub.rectTransform.anchoredPosition = new Vector2(70f, -45f);
            toastAccent.rectTransform.sizeDelta = new Vector2(4f, h - 24f);
        }

        // ------------------------------------------------------------------ petunjuk (antre)
        void UpdateHint(GameManager gm, float dt)
        {
            bool ok = gm.Playing && root.gameObject.activeSelf && !map.fullOpen && !(gm.ui != null && gm.ui.IntroActive);
            if (curHint == null)
            {
                if (!ok || hintQueue.Count == 0) { if (hintRt.gameObject.activeSelf) hintRt.gameObject.SetActive(false); return; }
                curHint = hintQueue[0]; hintQueue.RemoveAt(0);
                LayoutHint(curHint[1], curHint[2]);
                hintT = 7f; hintAge = 0f; hintHeld = false;
                hintRt.gameObject.SetActive(true);
            }
            if (!ok) { hintCg.alpha = 0f; hintHeld = true; return; }
            if (hintHeld) { hintHeld = false; hintAge = 0f; hintT = Mathf.Max(hintT, 3f); }
            hintAge += dt; hintT -= dt;
            if (hintQueue.Count > 0 && hintAge > 4.5f) hintT = Mathf.Min(hintT, 0.5f);
            hintCg.alpha = Mathf.Min(Mathf.Clamp01(hintAge * 4f), Mathf.Clamp01(hintT / 0.5f));
            if (hintT <= 0f) { curHint = null; hintRt.gameObject.SetActive(false); }
        }

        void LayoutHint(string text, string icon)
        {
            hintTxt.text = text;
            hintIcon.sprite = Icons.Get(icon);
            hintTxt.rectTransform.sizeDelta = new Vector2(900f, 60f);
            float w = Mathf.Min(hintTxt.preferredWidth, 900f);
            hintTxt.rectTransform.sizeDelta = new Vector2(w + 6f, 60f);
            float h = Mathf.Max(64f, hintTxt.preferredHeight + 30f);
            hintRt.sizeDelta = new Vector2(w + 96f, h);
            hintTxt.rectTransform.sizeDelta = new Vector2(w + 6f, h - 10f);
        }

        void SetPrompt(string s)
        {
            if (s == lastPrompt) return;
            lastPrompt = s;
            bool on = !string.IsNullOrEmpty(s);
            if (promptRt.gameObject.activeSelf != on) promptRt.gameObject.SetActive(on);
            if (!on) return;
            bool key = s.StartsWith("[E] ");
            promptTxt.text = key ? s.Substring(4) : s;
            float tw = promptTxt.preferredWidth;
            float kw = key ? UIKit.KeycapWidth("E", 36f) + 14f : 0f;
            float total = kw + tw;
            promptKey.gameObject.SetActive(key);
            promptKey.anchoredPosition = new Vector2(-total * 0.5f, 2f);
            promptTxt.rectTransform.sizeDelta = new Vector2(tw + 10f, 40f);
            promptTxt.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + kw, 0f);
            promptBg.rectTransform.sizeDelta = new Vector2(total + 48f, 56f);
        }

        // ------------------------------------------------------------------ panduan awal
        void UpdateTutorial(GameManager gm)
        {
            if (!TutorialActive)
            {
                if (gm.ghost != null) gm.ghost.holdSleep = false;
                if (tutCard.gameObject.activeSelf)
                {
                    tutCg.alpha -= Time.unscaledDeltaTime * 1.5f;
                    if (tutCg.alpha <= 0f) tutCard.gameObject.SetActive(false);
                }
                return;
            }
            // mode presentasi memakai kolom kiri untuk panel PCG/DDA/AI
            if (tutCard.gameObject.activeSelf == showDebug) tutCard.gameObject.SetActive(!showDebug);
            PlayerController p = gm.player;
            if (gm.Playing)
            {
                if (!tutDone[0] && G.Flat(p.transform.position - tutStart).magnitude > 3f) Done(0);
                if (p.flashOn != tutLastFlash) { tutLastFlash = p.flashOn; if (!tutDone[1]) Done(1); }
                if (!tutDone[2] && map.fullOpens > tutMapOpens) Done(2);
                if (!tutDone[3] && gm.relicsFound > 0) Done(3);
                if (!tutDone[4] && p.hidden) Done(4);
            }
            bool basics = tutDone[0] && tutDone[1] && tutDone[2];
            if (gm.ghost != null) gm.ghost.holdSleep = !basics && gm.houseTime < 150f;
            if (basics && !basicsAnnounced) { basicsAnnounced = true; Toast("Hantu akan segera bangun...", "Kumpulkan relik sebelum ia menemukanmu", "ghost", UIKit.Danger); }
            int next = -1;
            for (int i = 0; i < 5; i++) if (!tutDone[i]) { next = i; break; }
            for (int i = 0; i < 5; i++)
            {
                tutIcons[i].sprite = Icons.Get(tutDone[i] ? "check" : "box");
                tutIcons[i].color = tutDone[i] ? UIKit.Good : i == next ? UIKit.Gold : UIKit.Faint;
                tutRows[i].color = tutDone[i] ? new Color(UIKit.Good.r, UIKit.Good.g, UIKit.Good.b, 0.85f) : i == next ? UIKit.Ink : UIKit.Dim;
            }
            if (next < 0)
            {
                tutEndT += Time.unscaledDeltaTime;
                if (tutEndT > 2.5f) { TutorialActive = false; TutDone = true; Toast("Panduan selesai", "Selamat bertahan. Hantu mulai mempelajari kebiasaanmu...", "check", UIKit.Good); }
            }
        }

        void Done(int i) { tutDone[i] = true; AudioDirector.I?.Click(); }

        // ------------------------------------------------------------------ panel presentasi
        void UpdateFeed()
        {
            List<string> f = GhostMemory.feed;
            StringBuilder sb = new StringBuilder();
            if (f.Count == 0) sb.Append("<color=#9a948a>Belum ada catatan. Hantu mencatat kebiasaanmu saat kamu keluar dari persembunyian.</color>");
            for (int i = Mathf.Max(0, f.Count - 4); i < f.Count; i++)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(i == f.Count - 1 ? "<color=#ffe0b0>" + f[i] + "</color>" : f[i]);
            }
            string s = sb.ToString();
            if (feedTxt.text != s)
            {
                feedTxt.text = s;
                feedTxt.rectTransform.sizeDelta = new Vector2(564f, 400f);
                float h = Mathf.Min(feedTxt.preferredHeight + 2f, 230f);
                feedTxt.rectTransform.sizeDelta = new Vector2(564f, h);
                feedCard.sizeDelta = new Vector2(600f, 46f + h + 16f);
            }
        }

        string DebugText(GameManager gm)
        {
            HouseLayout L = gm.layout; PcgParams pp = L.p; DDAManager d = gm.dda; GhostAI g = gm.ghost;
            const string dot = "  ·  ";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<size=18><b><color=#88ccff>PCG</color></b></size>   <color=#a8a296>BSP · Kruskal · tangga · lock-and-key · generate-and-test</color>");
            sb.AppendLine("seed " + pp.seed + dot + pp.width + "×" + pp.height + " sel × " + L.floors + " lantai" + dot + L.rooms.Count + " ruang" + dot + L.doors.Count + " pintu (loop " + L.loops + ")");
            sb.AppendLine("relik " + L.relics.Count + " (≤ " + pp.relicSpotDist + " sel ke tempat sembunyi, nyata " + L.relicSpotMax + ")" + dot + "sembunyi " + L.spots.Count + dot + "baterai " + L.batteries.Count);
            sb.AppendLine("ruang terkunci " + (L.lockedRoom >= 0 ? HouseGenerator.RoomName(L.rooms[L.lockedRoom].type) : "-") + dot + "lampu " + Mathf.RoundToInt(pp.litRatio * 100) + "%" + dot + "perabot " + L.props.Count);
            sb.AppendLine("validasi " + (L.valid ? "<color=#80ff80>LOLOS</color>" : "<color=#ff8080>" + L.failReason + "</color>") + dot + L.attempts + " percobaan" + dot + L.ms.ToString("0.0") + " ms" + dot + "hash " + L.hash);
            sb.AppendLine();
            sb.AppendLine("<size=18><b><color=#ffcc66>DDA</color></b></size>   d = <b>" + d.d.ToString("0.00") + "</b> " + d.Label + dot + "P " + d.P.ToString("0.00") + " (halus " + d.Ps.ToString("0.00") + ")" + dot + "target " + DDAManager.Target);
            sb.AppendLine("tempo " + d.pace.ToString("0.00") + dot + "aman " + d.safety.ToString("0.00") + dot + "baterai " + d.resource.ToString("0.00") + dot + "tenang " + d.calm.ToString("0.00") + dot + "nyaris " + d.nearMisses);
            sb.AppendLine("<color=#a8a296>evaluasi tiap " + DDAManager.Window + " dtk (" + d.evalT.ToString("0") + ")" + dot + "batas/rumah +" + DDAManager.MaxRise + " / -" + DDAManager.MaxDrop + dot + "tertangkap " + d.caughtTotal + dot + "lolos " + d.winsTotal + "</color>");
            sb.AppendLine("hantu: patroli " + d.gp.patrolSpeed.ToString("0.0") + " / kejar " + d.gp.chaseSpeed.ToString("0.0") + " m/s" + dot + "lihat " + d.gp.visionRange.ToString("0") + " m, " + d.gp.fov.ToString("0") + "°" + dot + "dengar ×" + d.gp.hearingMult.ToString("0.00"));
            sb.AppendLine("cari " + d.gp.searchTime.ToString("0") + " dtk" + dot + "belajar " + d.gp.learnWeight.ToString("0.00") + dot + "jaga " + d.gp.guardTime.ToString("0") + " dtk" + dot + "eksplorasi " + d.gp.explore.ToString("0.00"));
            PcgParams nx = d.MakePcg(0);
            sb.AppendLine("rumah berikut: " + nx.floors + " lantai " + nx.width + "×" + nx.height + dot + "sembunyi " + nx.hidingSpots + dot + "baterai " + nx.batteries + dot + "relik " + nx.relicCount);
            for (int i = Mathf.Max(0, d.log.Count - 2); i < d.log.Count; i++) sb.AppendLine("<color=#a8a296>" + d.log[i] + "</color>");
            sb.AppendLine();
            sb.AppendLine("<size=18><b><color=#ff8877>AI HANTU</color></b></size>   <color=#a8a296>FSM · A* · memori Bayesian (Thompson sampling)</color>");
            sb.AppendLine("state <b><color=#" + ColorUtility.ToHtmlStringRGB(StateCol(g.state)) + ">" + g.state.ToString().ToUpper() + "</color></b>" + dot + "melihat " + (g.canSee ? "<color=#ff6050>YA</color>" : "-") + dot + "mendengar " + (g.heardRecently ? "<color=#ffc060>YA</color>" : "-") + dot + "tak bertemu " + g.unseenTime.ToString("0") + " dtk");
            sb.AppendLine("<color=#d8d0c4>" + g.decision + "</color>");
            if (g.searchInfo.Length > 0) sb.AppendLine("<color=#ffd090>" + g.searchInfo + "</color>");
            sb.AppendLine("kebiasaan: lemari " + (GhostMemory.KindPref(0) * 100f).ToString("0") + "%" + dot + "kolong " + (GhostMemory.KindPref(1) * 100f).ToString("0") + "%" + dot + "sembunyi " + GhostMemory.totalHides + "×" + dot + "diperiksa " + GhostMemory.totalChecks + "×");
            List<HidingSpot> top = new List<HidingSpot>(gm.house.spots);
            top.Sort((a, b) => GhostMemory.Mean(b).CompareTo(GhostMemory.Mean(a)));
            for (int i = 0; i < top.Count && i < 3; i++)
            {
                HidingSpot s = top[i];
                GhostMemory.AlphaBeta(s, out float a, out float b);
                sb.AppendLine("   #" + s.id + " " + s.KindName + dot + "Beta(" + a.ToString("0.0") + ", " + b.ToString("0.0") + ")" + dot + "E = " + GhostMemory.Mean(s).ToString("0.00") + dot + "dipakai " + GhostMemory.Uses(s) + "×");
            }
            sb.Append("<color=#9fd8ff>Pemain yang bersembunyi tidak pernah ditemukan; kebiasaan hanya dipakai untuk menghadang & berjaga.</color>");
            return sb.ToString();
        }
    }
}
