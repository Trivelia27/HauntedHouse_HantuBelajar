using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>Ringkasan satu rumah untuk layar akhir.</summary>
    public struct EndInfo
    {
        public float time, dFrom, dTo;
        public int relics, relicsNeed, chases, hides;
        public string label, next;
    }

    /// <summary>Semua layar UI: menu utama, panduan bermain, pengaturan (tab), jeda + denah, layar akhir, kartu intro, fade.</summary>
    public class GameUI : MonoBehaviour
    {
        public HUD hud;
        public Canvas canvas;
        GameObject mainMenu, pauseMenu, settingsMenu, guideMenu, endMenu, introCard;
        Image fade; float fadeTarget, fadeSpeed = 2f;
        Button continueBtn; Text continueSub, memoryText; RectTransform[] mainButtons;
        Text pauseStatus; MapPanel pauseMap;
        System.Action back, guideDone;
        // panduan
        GameObject[] guidePages; int guidePage; bool guideFirst;
        Text guideTitle, guideStep, guideNextText; Image[] guideDots; Button guideNext, guidePrev, guideSkip;
        // pengaturan
        GameObject[] tabPages; UIHover[] tabs;
        // akhir
        Image endIcon, ddaFill; Text endTitle, endReason, endTrend, endNext, endPrimaryText; readonly Text[] statVal = new Text[4]; RectTransform ddaFrom, ddaTo;
        // intro
        Text introTitle; RectTransform introChips; CanvasGroup introCg; float introT, introAge;

        public bool GuideOpen { get { return guideMenu != null && guideMenu.activeSelf; } }
        /// <summary>Judul "RUMAH #N" masih tampil (notifikasi & petunjuk ditahan sampai judul memudar).</summary>
        public bool IntroActive { get { return introCard != null && introCard.activeSelf && introT > 0.7f; } }
        public bool AnyMenu { get { return mainMenu.activeSelf || pauseMenu.activeSelf || settingsMenu.activeSelf || guideMenu.activeSelf || endMenu.activeSelf; } }

        public static GameUI Create()
        {
            GameObject g = new GameObject("UI");
            GameUI ui = g.AddComponent<GameUI>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            CanvasScaler cs = gameObject.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920f, 1080f); cs.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            hud = gameObject.AddComponent<HUD>();
            hud.Build(transform);
            BuildMain(); BuildPause(); BuildSettings(); BuildGuide(); BuildEnd(); BuildIntro();
            hud.BuildOverlay(transform);       // notifikasi di atas menu (umpan balik tombol pengaturan)
            fade = UIKit.Fill(transform, "Fade", Color.black);
            fade.raycastTarget = false;
            HideAll();
        }

        static RectTransform MakeScreen(Transform parent, string name, Color bg)
        {
            RectTransform m = UIKit.Stretch(parent, name);
            m.gameObject.AddComponent<ScreenFade>();
            if (bg.a > 0f) { Image b = UIKit.Fill(m, "Bg", bg); b.raycastTarget = true; }
            return m;
        }

        // ------------------------------------------------------------------ menu utama
        void BuildMain()
        {
            RectTransform m = MakeScreen(transform, "MainMenu", Color.clear); mainMenu = m.gameObject;
            Image shade = UIKit.Fill(m, "Shade", new Color(0.015f, 0.01f, 0.012f, 1f)); shade.sprite = Icons.Gradient(true, 0.96f, 0.15f, "menuShade"); shade.raycastTarget = true;
            Image vig = UIKit.Fill(m, "Vignette", new Color(0f, 0f, 0f, 0.8f)); vig.sprite = Sprite.Create(Look.Radial("menuVig", 0.3f), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            Image bottom = UIKit.Panel(m, "Bottom", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 280f), new Color(0f, 0f, 0f, 0.85f), new Vector2(0.5f, 0f));
            bottom.sprite = Icons.Gradient(false, 1f, 0f, "fadeUp");
            UIKit.Label(m, "Over", "PROYEK EAS   ·   PCG   ·   DDA   ·   GAME AI", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(128f, -118f), new Vector2(900f, 26f), UIKit.Faint, FontKind.Head, new Vector2(0f, 1f));
            Text title = UIKit.Label(m, "Title", "HAUNTED HOUSE", 120, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(118f, -146f), new Vector2(1300f, 150f), UIKit.Ink, FontKind.Title, new Vector2(0f, 1f));
            Outline glow = title.gameObject.AddComponent<Outline>(); glow.effectColor = new Color(0.55f, 0.06f, 0.04f, 0.45f); glow.effectDistance = new Vector2(3f, -3f);
            UIKit.Label(m, "Subtitle", "Hantu yang Mempelajari Ketakutanmu", 36, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(126f, -296f), new Vector2(1100f, 48f), UIKit.Gold, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Panel(m, "Line", new Vector2(0f, 1f), new Vector2(128f, -358f), new Vector2(96f, 3f), UIKit.Accent, new Vector2(0f, 1f));
            UIKit.Label(m, "Blurb", "Rumah berubah setiap malam. Kumpulkan relik dan kabur - hantu di rumah ini belajar dari jalur dan tempat persembunyian favoritmu.", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(128f, -378f), new Vector2(740f, 70f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            Vector2 sz = new Vector2(660f, 64f), pv = new Vector2(0f, 1f), an = new Vector2(0f, 1f);
            continueBtn = UIKit.MakeButton(m, "Continue", "LANJUTKAN", an, new Vector2(112f, -480f), sz, () => GameManager.I.Continue(), 34, BtnStyle.Menu, null, pv);
            continueSub = UIKit.SubLabel(continueBtn, "", 20);
            Button nb = UIKit.MakeButton(m, "New", "MULAI BARU", an, Vector2.zero, sz, () => GameManager.I.StartNew(), 34, BtnStyle.Menu, null, pv);
            Button gb = UIKit.MakeButton(m, "HowTo", "PANDUAN BERMAIN", an, Vector2.zero, sz, () => ShowGuide(false, ShowMain), 34, BtnStyle.Menu, null, pv);
            Button sb = UIKit.MakeButton(m, "Settings", "PENGATURAN", an, Vector2.zero, sz, () => ShowSettings(ShowMain), 34, BtnStyle.Menu, null, pv);
            Button qb = UIKit.MakeButton(m, "Quit", "KELUAR", an, Vector2.zero, sz, () => GameManager.I.Quit(), 34, BtnStyle.Menu, null, pv);
            mainButtons = new[] { (RectTransform)continueBtn.transform, (RectTransform)nb.transform, (RectTransform)gb.transform, (RectTransform)sb.transform, (RectTransform)qb.transform };
            // kartu memori hantu
            RectTransform card = UIKit.Card(m, "Memory", new Vector2(1f, 0f), new Vector2(-72f, 84f), new Vector2(500f, 200f), new Vector2(1f, 0f));
            UIKit.Icon(card, "I", "ghost", new Vector2(0f, 1f), new Vector2(22f, -18f), 34f, UIKit.Gold, new Vector2(0f, 1f));
            UIKit.Label(card, "T", "MEMORI HANTU", 22, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(68f, -35f), new Vector2(380f, 30f), UIKit.Gold, FontKind.Head, new Vector2(0f, 0.5f));
            memoryText = UIKit.Label(card, "Body", "", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(24f, -68f), new Vector2(456f, 120f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            UIKit.Label(m, "Footer", "Aset: Stealth Game Haunted House (Unity)  ·  Graveyard Kit (Kenney)  ·  Unnerving Ambient Sounds (DHSFX)  ·  Horror Music Starter Kit (Fictium)", 16, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(128f, 30f), new Vector2(1300f, 26f), UIKit.Faint, FontKind.Body, new Vector2(0f, 0f));
        }

        static string MemoryText()
        {
            int hides = GhostMemory.totalHides;
            if (hides == 0) return "Belum ada data - hantu belum mengenalmu.\nSetiap kali kamu bersembunyi, ia mencatat kebiasaanmu.";
            float lem = GhostMemory.KindPref(0);
            int best = -1; float bv = 0f;
            for (int i = 0; i < GhostMemory.roomCount.Length; i++) if (GhostMemory.roomCount[i] > bv) { bv = GhostMemory.roomCount[i]; best = i; }
            return "Persembunyian tercatat: <color=#ece6da>" + hides + "</color>\n" +
                   "Favoritmu: " + (lem >= 0.5f ? "lemari " + Mathf.RoundToInt(lem * 100f) + "%" : "kolong ranjang " + Mathf.RoundToInt((1f - lem) * 100f) + "%") +
                   (best >= 0 ? "\nRuang favorit: " + HouseGenerator.RoomName((RoomType)best) : "");
        }

        // ------------------------------------------------------------------ jeda
        void BuildPause()
        {
            RectTransform m = MakeScreen(transform, "Pause", new Color(0f, 0f, 0f, 0.8f)); pauseMenu = m.gameObject;
            UIKit.Label(m, "Title", "JEDA", 96, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(120f, -110f), new Vector2(600f, 120f), UIKit.Ink, FontKind.Title, new Vector2(0f, 1f));
            pauseStatus = UIKit.Label(m, "Status", "", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(126f, -240f), new Vector2(560f, 130f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            Vector2 sz = new Vector2(540f, 62f), an = new Vector2(0f, 1f);
            UIKit.MakeButton(m, "Resume", "LANJUTKAN", an, new Vector2(112f, -400f), sz, () => GameManager.I.Resume(), 32, BtnStyle.Menu, null, an);
            UIKit.MakeButton(m, "Guide", "PANDUAN BERMAIN", an, new Vector2(112f, -470f), sz, () => ShowGuide(false, ShowPause), 32, BtnStyle.Menu, null, an);
            UIKit.MakeButton(m, "Settings", "PENGATURAN", an, new Vector2(112f, -540f), sz, () => ShowSettings(ShowPause), 32, BtnStyle.Menu, null, an);
            UIKit.MakeButton(m, "Menu", "MENU UTAMA", an, new Vector2(112f, -610f), sz, () => GameManager.I.ToMenu(), 32, BtnStyle.Menu, null, an);
            UIKit.Keycap(m, "Esc", new Vector2(0f, 0f), new Vector2(128f, 46f), 34f, new Vector2(0f, 0.5f));
            UIKit.Label(m, "EscLbl", "lanjutkan", 19, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(190f, 46f), new Vector2(200f, 30f), UIKit.Faint, FontKind.Head, new Vector2(0f, 0.5f));
            RectTransform card = UIKit.Card(m, "MapCard", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(1060f, 880f), new Vector2(1f, 0.5f));
            UIKit.Label(card, "T", "DENAH RUMAH", 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(600f, 40f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            pauseMap = new MapPanel(card, "PauseMap", new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(980f, 620f));
            MapUI.Legend(card, new Vector2(0.5f, 0f), new Vector2(0f, 52f), 980f, 17);
            hud.map.AttachPausePanel(pauseMap);
        }

        // ------------------------------------------------------------------ pengaturan
        void BuildSettings()
        {
            RectTransform m = MakeScreen(transform, "Settings", new Color(0.01f, 0.01f, 0.01f, 0.88f)); settingsMenu = m.gameObject;
            RectTransform card = UIKit.Card(m, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 880f));
            UIKit.Label(card, "Title", "PENGATURAN", 44, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(50f, -34f), new Vector2(700f, 58f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            string[] names = { "KONTROL", "GRAFIS", "AUDIO", "GAMEPLAY" };
            tabs = new UIHover[4]; tabPages = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                Button b = UIKit.MakeButton(card, "Tab" + i, names[i], new Vector2(0f, 1f), new Vector2(40f + i * 200f, -108f), new Vector2(190f, 54f), () => SelectTab(k), 22, BtnStyle.Tab, null, new Vector2(0f, 1f));
                tabs[i] = b.GetComponent<UIHover>();
                tabPages[i] = UIKit.Node(card, "Page" + i, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(1080f, 560f), new Vector2(0.5f, 1f)).gameObject;
            }
            UIKit.Divider(card, "Div", new Vector2(0.5f, 1f), new Vector2(0f, -166f), 1080f, new Vector2(0.5f, 1f));
            const float W = 1060f, step = -70f;
            // kontrol
            Transform p0 = tabPages[0].transform; float y = 0f;
            UIKit.MakeSlider(p0, "Sensitivitas mouse", 0.5f, 6f, Settings.Sensitivity, new Vector2(0f, y), W, v => Settings.Sensitivity = v, v => v.ToString("0.0")); y += step;
            UIKit.MakeToggle(p0, "Balik sumbu Y mouse", Settings.InvertY, new Vector2(0f, y), W, v => Settings.InvertY = v); y += step;
            UIKit.MakeSlider(p0, "Bidang pandang (FOV)", 60f, 95f, Settings.Fov, new Vector2(0f, y), W, v => { Settings.Fov = v; Settings.Apply(); }, v => v.ToString("0")); y += step;
            UIKit.MakeToggle(p0, "Guncangan kepala saat berjalan", Settings.HeadBob, new Vector2(0f, y), W, v => Settings.HeadBob = v); y += step;
            ControlsList(p0, y - 24f);
            // grafis
            Transform p1 = tabPages[1].transform; y = 0f;
            UIKit.MakeCycle(p1, "Kualitas grafis", new[] { "Rendah", "Sedang", "Tinggi" }, Settings.Quality, new Vector2(0f, y), W, v => { Settings.Quality = v; Settings.Apply(); }); y += step;
            UIKit.MakeSlider(p1, "Kecerahan", -0.6f, 0.9f, Settings.Brightness, new Vector2(0f, y), W, v => { Settings.Brightness = v; Settings.Apply(); }, v => (v >= 0 ? "+" : "") + v.ToString("0.0")); y += step;
            UIKit.MakeToggle(p1, "Layar penuh", Settings.Fullscreen, new Vector2(0f, y), W, v => { Settings.Fullscreen = v; Settings.Apply(); }); y += step;
            UIKit.Label(p1, "Note", "Kualitas memengaruhi resolusi render, jarak bayangan, dan anti-aliasing. Naikkan kecerahan bila ruangan terlalu gelap di layarmu.", 19, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-520f, y - 14f), new Vector2(1040f, 60f), UIKit.Faint, FontKind.Body, new Vector2(0f, 1f));
            // audio
            Transform p2 = tabPages[2].transform; y = 0f;
            UIKit.MakeSlider(p2, "Volume utama", 0f, 1f, Settings.Master, new Vector2(0f, y), W, v => Settings.Master = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.MakeSlider(p2, "Musik", 0f, 1f, Settings.Music, new Vector2(0f, y), W, v => Settings.Music = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.MakeSlider(p2, "Efek suara", 0f, 1f, Settings.Sfx, new Vector2(0f, y), W, v => Settings.Sfx = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.Label(p2, "Note", "Gunakan headphone: arah suara langkah dan bisikan hantu membantumu tahu dari mana ia datang.", 19, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-520f, y - 14f), new Vector2(1040f, 60f), UIKit.Faint, FontKind.Body, new Vector2(0f, 1f));
            // gameplay
            Transform p3 = tabPages[3].transform; y = 0f;
            UIKit.MakeToggle(p3, "Peta kecil (kanan atas)", Settings.Minimap, new Vector2(0f, y), W, v => Settings.Minimap = v); y += step;
            UIKit.MakeToggle(p3, "Kompas & penanda arah tujuan", Settings.Compass, new Vector2(0f, y), W, v => Settings.Compass = v); y += step;
            UIKit.MakeToggle(p3, "Petunjuk kontekstual", Settings.Hints, new Vector2(0f, y), W, v => Settings.Hints = v); y += step;
            UIKit.MakeButton(p3, "ResetTut", "ULANGI PANDUAN AWAL", new Vector2(0.5f, 1f), new Vector2(-268f, y - 20f), new Vector2(510f, 58f), () => { HUD.ResetHints(); hud.Toast("Panduan bermain & panduan awal akan muncul lagi", null, "check", UIKit.Good, true); }, 21, BtnStyle.Secondary, "info", new Vector2(0.5f, 1f));
            UIKit.MakeButton(p3, "ResetMem", "HAPUS MEMORI HANTU", new Vector2(0.5f, 1f), new Vector2(268f, y - 20f), new Vector2(510f, 58f), () => { GhostMemory.Reset(); hud.Toast("Memori hantu dihapus", "Hantu mulai dari nol lagi", "ghost", UIKit.Cold, true); }, 21, BtnStyle.Secondary, "ghost", new Vector2(0.5f, 1f));
            UIKit.Label(p3, "Note", "Hantu menyimpan kebiasaanmu (jenis & ruang tempat sembunyi favorit) di antara rumah dan sesi permainan.", 19, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-520f, y - 100f), new Vector2(1040f, 60f), UIKit.Faint, FontKind.Body, new Vector2(0f, 1f));
            // bawah
            UIKit.MakeButton(card, "Defaults", "BAWAAN", new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(220f, 58f), ResetDefaults, 22, BtnStyle.Secondary, null, new Vector2(0f, 0f));
            UIKit.MakeButton(card, "Back", "KEMBALI", new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(260f, 58f), () => { Settings.Save(); Settings.Apply(); back?.Invoke(); }, 24, BtnStyle.Primary, null, new Vector2(1f, 0f));
            SelectTab(0);
        }

        void ControlsList(Transform p, float y)
        {
            (string[], string)[] rows =
            {
                (new[] { "W", "A", "S", "D" }, "Bergerak"), (new[] { "Shift" }, "Lari"), (new[] { "C" }, "Jongkok"), (new[] { "F" }, "Senter nyala / mati"),
                (new[] { "Klik kanan" }, "Fokus senter"), (new[] { "E" }, "Ambil / buka / sembunyi"), (new[] { "M" }, "Peta rumah"), (new[] { "Esc" }, "Jeda"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = -520f + col * 540f, yy = y - row * 54f, kx = x;
                foreach (string k in rows[i].Item1) { RectTransform kc = UIKit.Keycap(p, k, new Vector2(0.5f, 1f), new Vector2(kx, yy), 38f, new Vector2(0f, 1f)); kx += kc.sizeDelta.x + 6f; }
                UIKit.Label(p, "L" + i, rows[i].Item2, 20, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(kx + 10f, yy - 19f), new Vector2(320f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 0.5f));
            }
        }

        public void SelectTab(int k)
        {
            for (int i = 0; i < tabPages.Length; i++) { tabPages[i].SetActive(i == k); tabs[i].selected = i == k; tabs[i].Refresh(); }
        }

        void ResetDefaults()
        {
            Settings.ResetDefaults(); Settings.Save(); Settings.Apply();
            int idx = settingsMenu.transform.GetSiblingIndex();
            System.Action ret = back;
            Destroy(settingsMenu);
            BuildSettings();
            settingsMenu.transform.SetSiblingIndex(idx);
            back = ret;
            settingsMenu.SetActive(true);
            hud.Toast("Pengaturan dikembalikan ke bawaan", null, "check", UIKit.Good, true);
        }

        // ------------------------------------------------------------------ panduan bermain
        static readonly string[] GuideTitles = { "Tujuanmu", "Kontrol", "Kenali hantunya", "Petunjuk arah & peta" };

        void BuildGuide()
        {
            RectTransform m = MakeScreen(transform, "Guide", new Color(0.01f, 0.008f, 0.01f, 0.93f)); guideMenu = m.gameObject;
            RectTransform card = UIKit.Card(m, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 870f));
            guideStep = UIKit.Label(card, "Step", "", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(56f, -36f), new Vector2(600f, 26f), UIKit.Faint, FontKind.Head, new Vector2(0f, 1f));
            guideTitle = UIKit.Label(card, "Title", "", 50, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(54f, -62f), new Vector2(1200f, 64f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Divider(card, "Div", new Vector2(0.5f, 1f), new Vector2(0f, -142f), 1330f, new Vector2(0.5f, 1f));
            guidePages = new GameObject[4];
            for (int i = 0; i < 4; i++) guidePages[i] = UIKit.Node(card, "Page" + i, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(1340f, 560f), new Vector2(0.5f, 1f)).gameObject;
            PageGoal(guidePages[0].transform); PageControls(guidePages[1].transform); PageGhost(guidePages[2].transform); PageHints(guidePages[3].transform);
            guideDots = new Image[4];
            for (int i = 0; i < 4; i++) guideDots[i] = UIKit.Icon(card, "Dot" + i, "disc", new Vector2(0.5f, 0f), new Vector2(-33f + i * 22f, 64f), 12f, UIKit.Faint, new Vector2(0.5f, 0.5f));
            guideSkip = UIKit.MakeButton(card, "GuideSkip", "LEWATI", new Vector2(0f, 0f), new Vector2(56f, 36f), new Vector2(200f, 58f), CloseGuide, 22, BtnStyle.Secondary, null, new Vector2(0f, 0f));
            guidePrev = UIKit.MakeButton(card, "GuidePrev", "KEMBALI", new Vector2(1f, 0f), new Vector2(-334f, 36f), new Vector2(220f, 58f), () => SetGuidePage(guidePage - 1), 22, BtnStyle.Secondary, null, new Vector2(1f, 0f));
            guideNext = UIKit.MakeButton(card, "GuideNext", "LANJUT", new Vector2(1f, 0f), new Vector2(-56f, 36f), new Vector2(264f, 58f), () => { if (guidePage < 3) SetGuidePage(guidePage + 1); else CloseGuide(); }, 23, BtnStyle.Primary, null, new Vector2(1f, 0f));
            guideNextText = guideNext.transform.Find("Text").GetComponent<Text>();
        }

        void PageGoal(Transform p)
        {
            (string, Color, string, string)[] tiles =
            {
                ("relic", UIKit.Gold, "1. Kumpulkan relik", "Relik bercahaya keemasan tersebar di rumah (1-2 lantai). Jumlahnya tampil di kiri atas layar."),
                ("key", new Color(1f, 0.42f, 0.36f), "2. Kunci & gembok", "Satu ruangan bisa digembok. Ambil KUNCI TUA (cahaya merah) untuk membukanya - biasanya ada relik di dalamnya."),
                ("exit", UIKit.Good, "3. Kabur!", "Setelah semua relik terkumpul, lampu di atas PINTU KELUAR (lantai 1) menyala hangat. Keluar untuk menang."),
            };
            for (int i = 0; i < 3; i++)
            {
                float x = -440f + i * 440f;
                Color c = tiles[i].Item2;
                RectTransform tile = UIKit.Card(p, "Tile" + i, new Vector2(0.5f, 1f), new Vector2(x, -10f), new Vector2(380f, 390f), new Vector2(0.5f, 1f), new Color(1f, 1f, 1f, 0.035f), 14, false);
                UIKit.Icon(tile, "Glow", "soft", new Vector2(0.5f, 1f), new Vector2(0f, -10f), 230f, new Color(c.r, c.g, c.b, 0.2f), new Vector2(0.5f, 1f));
                UIKit.Icon(tile, "Icon", tiles[i].Item1, new Vector2(0.5f, 1f), new Vector2(0f, -62f), 108f, c, new Vector2(0.5f, 1f));
                UIKit.Label(tile, "T", tiles[i].Item3, 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -198f), new Vector2(340f, 40f), UIKit.Ink, FontKind.Head, new Vector2(0.5f, 1f));
                UIKit.Label(tile, "B", tiles[i].Item4, 20, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -246f), new Vector2(330f, 140f), UIKit.Dim, FontKind.Body, new Vector2(0.5f, 1f));
                if (i < 2) UIKit.Icon(p, "Arrow" + i, "chevron", new Vector2(0.5f, 1f), new Vector2(x + 220f, -205f), 30f, UIKit.Faint, new Vector2(0.5f, 0.5f));
            }
            UIKit.Label(p, "Note", "Setiap rumah dibuat secara prosedural - tata letak, letak relik, dan tempat sembunyi selalu berbeda. Tingkat kesulitan menyesuaikan caramu bermain.", 21, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(1200f, 70f), UIKit.Faint, FontKind.Body, new Vector2(0.5f, 1f));
        }

        void PageControls(Transform p)
        {
            (string[], string, string, string)[] rows =
            {
                (new[] { "W", "A", "S", "D" }, null, "Bergerak", "Gerakkan mouse untuk melihat sekeliling"),
                (new[] { "Shift" }, null, "Lari", "Cepat, tapi berisik dan menguras stamina"),
                (new[] { "C" }, null, "Jongkok", "Pelan, senyap, dan sulit terlihat"),
                (new[] { "E" }, null, "Interaksi", "Ambil relik, buka gembok, bersembunyi"),
                (new[] { "F" }, null, "Senter", "Nyalakan / matikan - baterai terbatas"),
                (null, "mouseR", "Fokus senter", "Tahan klik kanan: sinar lebih terang & jauh"),
                (new[] { "M" }, null, "Peta rumah", "Denah kedua lantai + jalur ke tujuan"),
                (new[] { "Esc" }, null, "Jeda", "Pengaturan, panduan, dan denah rumah"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                int col = i % 2, row = i / 2;
                RectTransform r = UIKit.Card(p, "Row" + i, new Vector2(0.5f, 1f), new Vector2(-650f + col * 670f, -14f - row * 120f), new Vector2(630f, 102f), new Vector2(0f, 1f), new Color(1f, 1f, 1f, 0.03f), 12, false);
                float kx = 22f;
                if (rows[i].Item1 != null)
                    foreach (string k in rows[i].Item1) { RectTransform kc = UIKit.Keycap(r, k, new Vector2(0f, 0.5f), new Vector2(kx, 2f), 46f, new Vector2(0f, 0.5f)); kx += kc.sizeDelta.x + 6f; }
                else UIKit.Icon(r, "I", rows[i].Item2, new Vector2(0f, 0.5f), new Vector2(kx, 0f), 54f, UIKit.Ink, new Vector2(0f, 0.5f));
                const float tx = 254f;
                UIKit.Label(r, "T", rows[i].Item3, 25, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(tx, 17f), new Vector2(630f - tx - 16f, 32f), UIKit.Ink, FontKind.Head, new Vector2(0f, 0.5f));
                UIKit.Label(r, "B", rows[i].Item4, 19, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(tx, -17f), new Vector2(630f - tx - 16f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 0.5f));
            }
        }

        void PageGhost(Transform p)
        {
            (string, Color, string, string)[] tiles =
            {
                ("eye", UIKit.Danger, "Ia melihat", "Pandangannya berbentuk kerucut. Senter yang menyala membuatmu terlihat dari jauh; ruang gelap dan jongkok membuatmu samar."),
                ("sound", UIKit.Gold, "Ia mendengar", "Langkah lari terdengar jauh, berjalan sedang, jongkok hampir tak terdengar. Suara dari lantai lain lebih redup."),
                ("ghost", new Color(0.78f, 0.5f, 1f), "Ia belajar", "Hantu mencatat jalur dan tempat sembunyi favoritmu, lalu menghadang dan berjaga di sana. Variasikan kebiasaanmu!"),
                ("hide", UIKit.Cold, "Bersembunyi = aman", "Di dalam lemari / kolong ranjang hantu TIDAK bisa melihat atau menemukanmu. Intip dulu sebelum keluar - ia mungkin masih di dekatmu."),
            };
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                Color c = tiles[i].Item2;
                RectTransform r = UIKit.Card(p, "Tile" + i, new Vector2(0.5f, 1f), new Vector2(-650f + col * 670f, -10f - row * 206f), new Vector2(630f, 186f), new Vector2(0f, 1f), new Color(1f, 1f, 1f, 0.035f), 14, false);
                UIKit.Icon(r, "Glow", "soft", new Vector2(0f, 0.5f), new Vector2(-6f, 0f), 170f, new Color(c.r, c.g, c.b, 0.18f), new Vector2(0f, 0.5f));
                UIKit.Icon(r, "I", tiles[i].Item1, new Vector2(0f, 0.5f), new Vector2(36f, 0f), 84f, c, new Vector2(0f, 0.5f));
                UIKit.Label(r, "T", tiles[i].Item3, 27, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(152f, -22f), new Vector2(460f, 36f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
                UIKit.Label(r, "B", tiles[i].Item4, 19, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(152f, -62f), new Vector2(456f, 116f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            }
            UIKit.Label(p, "Note", "Lampu berkedip & napas memburu = hantu sudah dekat. Bila tertangkap, rumah berikutnya menyesuaikan kemampuanmu (DDA).", 20, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(1200f, 60f), UIKit.Faint, FontKind.Body, new Vector2(0.5f, 1f));
        }

        void PageHints(Transform p)
        {
            // ilustrasi kompas
            RectTransform comp = UIKit.Node(p, "MockCompass", new Vector2(0.5f, 1f), new Vector2(-330f, -24f), new Vector2(560f, 90f), new Vector2(0.5f, 1f));
            UIKit.Rounded(comp, "Bg", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(560f, 40f), new Color(1f, 1f, 1f, 0.07f), 12, new Vector2(0.5f, 1f));
            string[] letters = { "B", "BL", "U", "TL", "T" };
            for (int k = 0; k < letters.Length; k++)
                UIKit.Label(comp, "L" + k, letters[k], k % 2 == 0 ? 22 : 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(-220f + k * 110f, -20f), new Vector2(60f, 36f), k % 2 == 0 ? UIKit.Ink : UIKit.Dim, FontKind.Head);
            UIKit.Icon(comp, "Relic", "relic", new Vector2(0.5f, 1f), new Vector2(70f, -20f), 30f, UIKit.Gold, new Vector2(0.5f, 0.5f));
            UIKit.Label(comp, "Dist", "12 m", 18, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(70f, -40f), new Vector2(120f, 26f), UIKit.Gold, FontKind.Head, new Vector2(0.5f, 1f));
            UIKit.Icon(comp, "Notch", "arrow", new Vector2(0.5f, 1f), new Vector2(0f, -42f), 12f, UIKit.Gold, new Vector2(0.5f, 1f));
            // ilustrasi peta kecil
            RectTransform mm = UIKit.Node(p, "MockMini", new Vector2(0.5f, 1f), new Vector2(-330f, -140f), new Vector2(320f, 320f), new Vector2(0.5f, 1f));
            UIKit.Icon(mm, "Bg", "disc", new Vector2(0.5f, 0.5f), Vector2.zero, 320f, new Color(0.035f, 0.032f, 0.034f, 0.95f));
            Color room = new Color(0.25f, 0.235f, 0.22f), wall = new Color(0.87f, 0.81f, 0.71f);
            Vector2[] rp = { new Vector2(-50f, 50f), new Vector2(75f, 50f), new Vector2(10f, -65f) };
            Vector2[] rs = { new Vector2(150f, 110f), new Vector2(96f, 110f), new Vector2(220f, 110f) };
            for (int k = 0; k < 3; k++)
            {
                UIKit.Rounded(mm, "R" + k, new Vector2(0.5f, 0.5f), rp[k], rs[k], room, 4);
                UIKit.Rounded(mm, "W" + k, new Vector2(0.5f, 0.5f), rp[k], rs[k], wall, 4, null, 2);
            }
            for (int k = 0; k < 6; k++) UIKit.Icon(mm, "P" + k, "disc", new Vector2(0.5f, 0.5f), new Vector2(-10f + k * 14f, -10f - k * 12f), 7f, UIKit.Gold);
            UIKit.Icon(mm, "Relic", "relic", new Vector2(0.5f, 0.5f), new Vector2(80f, -82f), 26f, UIKit.Gold);
            UIKit.Icon(mm, "Hide", "hide", new Vector2(0.5f, 0.5f), new Vector2(-95f, 75f), 18f, new Color(0.6f, 0.66f, 0.78f));
            UIKit.Icon(mm, "Player", "player", new Vector2(0.5f, 0.5f), new Vector2(-14f, 4f), 26f, new Color(0.55f, 0.9f, 1f));
            UIKit.Icon(mm, "Ring", "ringthin", new Vector2(0.5f, 0.5f), Vector2.zero, 320f, new Color(1f, 0.93f, 0.82f, 0.3f));
            // teks
            string[] bullets =
            {
                "<b>Kompas</b> di atas layar menunjuk tujuan berikutnya dan mengikuti jalur rumah (lewat pintu & tangga). Angka = jarak. Panah kecil = naik / turun tangga.",
                "<b>Peta kecil</b> di kanan atas berputar mengikuti arah pandangmu. Titik-titik emas adalah jalur ke tujuan.",
                "Tekan <b>M</b> untuk <b>denah lengkap</b> kedua lantai: relik, kunci, pintu digembok, pintu keluar, baterai, dan tempat sembunyi.",
                "Di rumah pertama, <b>PANDUAN AWAL</b> di kiri layar menuntunmu langkah demi langkah. Hantu baru bangun setelah kamu siap.",
            };
            for (int k = 0; k < bullets.Length; k++)
            {
                UIKit.Icon(p, "Bul" + k, "disc", new Vector2(0.5f, 1f), new Vector2(40f, -40f - k * 118f), 10f, UIKit.Gold, new Vector2(0.5f, 0.5f));
                UIKit.Label(p, "Txt" + k, bullets[k], 21, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(64f, -26f - k * 118f), new Vector2(600f, 110f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            }
        }

        public void SetGuidePage(int i)
        {
            guidePage = Mathf.Clamp(i, 0, 3);
            for (int k = 0; k < 4; k++)
            {
                guidePages[k].SetActive(k == guidePage);
                guideDots[k].color = k == guidePage ? UIKit.Gold : UIKit.Faint;
                guideDots[k].rectTransform.sizeDelta = Vector2.one * (k == guidePage ? 14f : 10f);
            }
            guideTitle.text = GuideTitles[guidePage];
            guideStep.text = "PANDUAN BERMAIN   ·   " + (guidePage + 1) + " / 4";
            guidePrev.gameObject.SetActive(guidePage > 0);
            guideSkip.gameObject.SetActive(guidePage < 3);
            guideNextText.text = guidePage < 3 ? "LANJUT" : guideFirst ? "MULAI BERMAIN" : "TUTUP";
        }

        public void ShowGuide(bool firstTime, System.Action done)
        {
            guideFirst = firstTime; guideDone = done;
            HideAll(); guideMenu.SetActive(true);
            SetGuidePage(0);
        }
        public void ShowHowTo(System.Action ret) { ShowGuide(false, ret); }

        void CloseGuide()
        {
            System.Action d = guideDone; guideDone = null;
            guideMenu.SetActive(false);
            d?.Invoke();
        }

        // ------------------------------------------------------------------ layar akhir
        void BuildEnd()
        {
            RectTransform m = MakeScreen(transform, "End", new Color(0f, 0f, 0f, 0.86f)); endMenu = m.gameObject;
            endIcon = UIKit.Icon(m, "Icon", "skull", new Vector2(0.5f, 0.5f), new Vector2(0f, 372f), 84f, UIKit.Danger);
            endTitle = UIKit.Label(m, "Title", "", 112, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 262f), new Vector2(1600f, 140f), UIKit.Ink, FontKind.Title);
            endReason = UIKit.Label(m, "Reason", "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 40f), UIKit.Dim, FontKind.Body);
            string[] icons = { "clock", "relic", "ghost", "hide" }, labels = { "WAKTU", "RELIK", "DIKEJAR", "BERSEMBUNYI" };
            for (int i = 0; i < 4; i++)
            {
                RectTransform c = UIKit.Card(m, "Stat" + i, new Vector2(0.5f, 0.5f), new Vector2(-390f + i * 260f, 52f), new Vector2(240f, 146f));
                UIKit.Icon(c, "I", icons[i], new Vector2(0.5f, 1f), new Vector2(0f, -16f), 30f, UIKit.Gold, new Vector2(0.5f, 1f));
                statVal[i] = UIKit.Label(c, "V", "", 38, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(230f, 50f), UIKit.Ink, FontKind.Head);
                UIKit.Label(c, "L", labels[i], 16, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(230f, 24f), UIKit.Faint, FontKind.Head, new Vector2(0.5f, 0f));
            }
            RectTransform d = UIKit.Card(m, "Dda", new Vector2(0.5f, 0.5f), new Vector2(0f, -146f), new Vector2(1020f, 178f));
            UIKit.Label(d, "T", "KESULITAN (DDA)", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(400f, 26f), UIKit.Gold, FontKind.Head, new Vector2(0f, 1f));
            endTrend = UIKit.Label(d, "Trend", "", 22, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-30f, -16f), new Vector2(640f, 30f), UIKit.Ink, FontKind.Body, new Vector2(1f, 1f));
            RectTransform bar = UIKit.Bar(d, "Bar", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(960f, 10f), UIKit.Accent, out ddaFill, new Vector2(0.5f, 1f));
            ddaFrom = UIKit.Icon(bar, "From", "disc", new Vector2(0f, 0.5f), Vector2.zero, 16f, new Color(1f, 1f, 1f, 0.45f), new Vector2(0.5f, 0.5f)).rectTransform;
            ddaTo = UIKit.Icon(bar, "To", "disc", new Vector2(0f, 0.5f), Vector2.zero, 22f, UIKit.Gold, new Vector2(0.5f, 0.5f)).rectTransform;
            UIKit.Label(d, "Lo", "MUDAH", 15, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -88f), new Vector2(200f, 22f), UIKit.Faint, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Label(d, "Hi", "MIMPI BURUK", 15, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-30f, -88f), new Vector2(200f, 22f), UIKit.Faint, FontKind.Head, new Vector2(1f, 1f));
            endNext = UIKit.Label(d, "Next", "", 19, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(30f, 46f), new Vector2(960f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            Button pb = UIKit.MakeButton(m, "Primary", "RUMAH BERIKUTNYA", new Vector2(0.5f, 0.5f), new Vector2(-190f, -334f), new Vector2(420f, 70f), () => GameManager.I.NextHouse(), 26, BtnStyle.Primary);
            endPrimaryText = pb.transform.Find("Text").GetComponent<Text>();
            UIKit.MakeButton(m, "Menu", "MENU UTAMA", new Vector2(0.5f, 0.5f), new Vector2(200f, -334f), new Vector2(300f, 70f), () => GameManager.I.ToMenu(), 24, BtnStyle.Secondary);
        }

        // ------------------------------------------------------------------ kartu intro
        void BuildIntro()
        {
            RectTransform m = UIKit.Stretch(transform, "Intro"); introCard = m.gameObject;
            introCg = m.gameObject.AddComponent<CanvasGroup>(); introCg.blocksRaycasts = false; introCg.interactable = false;
            Image band = UIKit.Icon(m, "Band", "soft", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), 1f, new Color(0f, 0f, 0f, 0.75f));
            band.preserveAspect = false; band.rectTransform.sizeDelta = new Vector2(2200f, 520f);
            introTitle = UIKit.Label(m, "Title", "", 96, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 104f), new Vector2(1600f, 130f), UIKit.Ink, FontKind.Title);
            introChips = UIKit.Node(m, "Chips", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(10f, 44f));
        }

        // ------------------------------------------------------------------ tampil / sembunyi
        public void HideAll()
        {
            mainMenu.SetActive(false); pauseMenu.SetActive(false); settingsMenu.SetActive(false); guideMenu.SetActive(false); endMenu.SetActive(false); introCard.SetActive(false);
        }

        public void ShowMain()
        {
            HideAll(); mainMenu.SetActive(true); hud.SetVisible(false);
            bool has = GameManager.HasSave(out int house, out float d);
            continueBtn.gameObject.SetActive(has);
            float yy = -480f;
            foreach (RectTransform r in mainButtons)
            {
                if (!r.gameObject.activeSelf) continue;
                r.anchoredPosition = new Vector2(112f, yy); yy -= 72f;
            }
            if (has) continueSub.text = "Rumah #" + (house + 1) + "  ·  kesulitan " + Mathf.RoundToInt(d * 100) + "%";
            memoryText.text = MemoryText();
        }

        public void ShowPause()
        {
            HideAll(); pauseMenu.SetActive(true); hud.SetVisible(false);
            GameManager gm = GameManager.I;
            if (gm == null || gm.layout == null) return;
            int mm = (int)(gm.houseTime / 60f), ss = (int)(gm.houseTime % 60f);
            string key = gm.layout.lockedRoom >= 0 && gm.house.lockedDoor != null ? (gm.house.lockedDoor.locked ? (gm.player.hasKey ? "kunci: ada" : "kunci: belum ditemukan") : "gembok: terbuka") : "";
            pauseStatus.text = "Rumah #" + gm.houseNo + "   ·   " + gm.layout.FloorName(gm.player.floor) +
                               "\nRelik " + gm.relicsFound + " / " + gm.RelicsNeeded + (key.Length > 0 ? "   ·   " + key : "") +
                               "\nWaktu " + mm + ":" + ss.ToString("00") + "   ·   kesulitan " + gm.dda.Label.ToLower();
        }

        public void ShowSettings(System.Action ret) { back = ret; HideAll(); settingsMenu.SetActive(true); }
        public void ShowGame() { HideAll(); hud.SetVisible(true); }

        public void ShowEnd(bool won, string reason, EndInfo e)
        {
            HideAll(); endMenu.SetActive(true); hud.SetVisible(false);
            endIcon.sprite = Icons.Get(won ? "exit" : "skull"); endIcon.color = won ? UIKit.Gold : UIKit.Danger;
            endTitle.text = won ? "<color=#ffd27a>LOLOS!</color>" : "<color=#ff3a28>TERTANGKAP</color>";
            endReason.text = reason;
            int mm = (int)(e.time / 60f), ss = (int)(e.time % 60f);
            statVal[0].text = mm + ":" + ss.ToString("00");
            statVal[1].text = e.relics + " / " + e.relicsNeed;
            statVal[2].text = e.chases + "x";
            statVal[3].text = e.hides + "x";
            const float W = 960f;
            UIKit.SetBar(ddaFill, W, e.dTo);
            ddaFrom.anchoredPosition = new Vector2(e.dFrom * W, 0f);
            ddaTo.anchoredPosition = new Vector2(e.dTo * W, 0f);
            string trend = e.dTo > e.dFrom + 0.001f ? "<color=#ff8a70>naik</color>" : e.dTo < e.dFrom - 0.001f ? "<color=#8fe3a0>turun</color>" : "tetap";
            endTrend.text = trend + "   " + Mathf.RoundToInt(e.dFrom * 100) + "%  →  " + Mathf.RoundToInt(e.dTo * 100) + "%   ·   " + e.label;
            endNext.text = e.next ?? "";
            endPrimaryText.text = won ? "RUMAH BERIKUTNYA" : "COBA RUMAH BARU";
        }

        public void ShowIntro(string title, string[] chips, float seconds)
        {
            introCard.SetActive(true);
            introTitle.text = title;
            foreach (Transform c in introChips) Destroy(c.gameObject);
            List<RectTransform> list = new List<RectTransform>();
            if (chips != null) foreach (string s in chips) if (!string.IsNullOrEmpty(s)) list.Add(UIKit.Chip(introChips, s, new Vector2(0.5f, 0.5f), Vector2.zero, 22));
            UIKit.RowCenter(list.ToArray(), 0f, 14f);
            introT = seconds; introAge = 0f; introCg.alpha = 0f;
        }

        public void FadeTo(float a, float speed) { fadeTarget = a; fadeSpeed = speed; }
        public void SetFade(float a) { fadeTarget = a; Color c = fade.color; c.a = a; fade.color = c; }

        void Update()
        {
            Color c = fade.color;
            c.a = Mathf.MoveTowards(c.a, fadeTarget, Time.unscaledDeltaTime * fadeSpeed);
            fade.color = c;
            fade.raycastTarget = c.a > 0.95f;
            if (introCard.activeSelf)
            {
                introT -= Time.unscaledDeltaTime; introAge += Time.unscaledDeltaTime;
                introCg.alpha = Mathf.Min(Mathf.Clamp01(introAge * 2.5f), Mathf.Clamp01(introT / 0.9f));
                if (introT <= 0f) introCard.SetActive(false);
            }
        }
    }
}
