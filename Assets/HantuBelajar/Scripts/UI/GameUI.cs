using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>Semua UI: HUD, menu utama, pengaturan, cara bermain, jeda, layar akhir, kartu intro, fade.</summary>
    public class GameUI : MonoBehaviour
    {
        public HUD hud;
        public Canvas canvas;
        GameObject mainMenu, pauseMenu, settingsMenu, howMenu, endMenu, introCard;
        Image fade; float fadeTarget, fadeSpeed = 2f;
        Text endTitle, endBody, introText, continueLabel;
        Button continueBtn, endPrimary;
        RectTransform[] mainButtons;
        Text endPrimaryText;
        System.Action back;

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
            BuildMain(); BuildPause(); BuildSettings(); BuildHowTo(); BuildEnd(); BuildIntro();
            fade = UIKit.Fill(transform, "Fade", Color.black);
            fade.raycastTarget = false;
            HideAll();
        }

        // ------------------------------------------------------------------ menu utama
        void BuildMain()
        {
            mainMenu = UIKit.Stretch(transform, "MainMenu").gameObject;
            Image shade = UIKit.Fill(mainMenu.transform, "Shade", Color.white);
            shade.sprite = Sprite.Create(Gradient(), new Rect(0, 0, 256, 4), new Vector2(0.5f, 0.5f));
            shade.raycastTarget = true;
            Transform t = mainMenu.transform;
            UIKit.Label(t, "Title", "HAUNTED HOUSE", 104, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(120f, -120f), new Vector2(1100f, 130f), UIKit.Ink, true, new Vector2(0f, 1f));
            UIKit.Label(t, "Subtitle", "Hantu yang Mempelajari Ketakutanmu", 38, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(126f, -250f), new Vector2(1100f, 50f), UIKit.Gold, false, new Vector2(0f, 1f));
            UIKit.Label(t, "Blurb", "Kumpulkan relik yang tersebar di rumah yang berubah setiap malam. Hantu di rumah ini belajar dari setiap tempat sembunyi dan jalur favoritmu.", 23, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(128f, -312f), new Vector2(760f, 90f), UIKit.Dim, false, new Vector2(0f, 1f));
            float y = -460f;
            continueBtn = UIKit.MakeButton(t, "Continue", "LANJUTKAN", new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(760f, 60f), () => GameManager.I.Continue(), 40);
            continueLabel = continueBtn.GetComponentInChildren<Text>();
            Button nb = UIKit.MakeButton(t, "New", "MULAI BARU", new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(520f, 60f), () => GameManager.I.StartNew(), 40);
            Button sb = UIKit.MakeButton(t, "Settings", "PENGATURAN", new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(520f, 60f), () => ShowSettings(ShowMain), 40);
            Button hb = UIKit.MakeButton(t, "HowTo", "CARA BERMAIN", new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(520f, 60f), () => ShowHowTo(ShowMain), 40);
            Button qb = UIKit.MakeButton(t, "Quit", "KELUAR", new Vector2(0f, 1f), new Vector2(120f, y), new Vector2(520f, 60f), () => GameManager.I.Quit(), 40);
            mainButtons = new[] { (RectTransform)continueBtn.transform, (RectTransform)nb.transform, (RectTransform)sb.transform, (RectTransform)hb.transform, (RectTransform)qb.transform };
            UIKit.Label(t, "Footer", "Proyek EAS  -  PCG, DDA & Game AI   |   Aset: Stealth Game Haunted House (Unity), Graveyard Kit (Kenney), Unnerving Ambient Sounds (DHSFX), Horror Music Starter Kit (Fictium Sound Design)", 17, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(120f, 34f), new Vector2(1700f, 30f), UIKit.Dim * 0.9f, false, new Vector2(0f, 0f));
        }

        static Texture2D Gradient()
        {
            Texture2D t = new Texture2D(256, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float a = Mathf.Lerp(0.92f, 0.15f, Mathf.SmoothStep(0f, 1f, x / 255f * 1.4f));
                for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(0.01f, 0.008f, 0.008f, a));
            }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ jeda
        void BuildPause()
        {
            pauseMenu = UIKit.Stretch(transform, "Pause").gameObject;
            Image bg = UIKit.Fill(pauseMenu.transform, "Bg", new Color(0f, 0f, 0f, 0.72f)); bg.raycastTarget = true;
            Transform t = pauseMenu.transform;
            UIKit.Label(t, "Title", "JEDA", 84, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(160f, 220f), new Vector2(800f, 110f), UIKit.Ink, true, new Vector2(0f, 0.5f));
            UIKit.MakeButton(t, "Resume", "LANJUTKAN", new Vector2(0f, 0.5f), new Vector2(160f, 90f), new Vector2(520f, 60f), () => GameManager.I.Resume(), 38);
            UIKit.MakeButton(t, "Settings", "PENGATURAN", new Vector2(0f, 0.5f), new Vector2(160f, 20f), new Vector2(520f, 60f), () => ShowSettings(ShowPause), 38);
            UIKit.MakeButton(t, "HowTo", "CARA BERMAIN", new Vector2(0f, 0.5f), new Vector2(160f, -50f), new Vector2(520f, 60f), () => ShowHowTo(ShowPause), 38);
            UIKit.MakeButton(t, "Menu", "MENU UTAMA", new Vector2(0f, 0.5f), new Vector2(160f, -120f), new Vector2(520f, 60f), () => GameManager.I.ToMenu(), 38);
        }

        // ------------------------------------------------------------------ pengaturan
        void BuildSettings()
        {
            settingsMenu = UIKit.Stretch(transform, "Settings").gameObject;
            Image bg = UIKit.Fill(settingsMenu.transform, "Bg", new Color(0.01f, 0.01f, 0.01f, 0.9f)); bg.raycastTarget = true;
            Transform t = settingsMenu.transform;
            UIKit.Label(t, "Title", "PENGATURAN", 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 90f), UIKit.Ink, true);
            float y = -160f, step = -56f;
            UIKit.MakeSlider(t, "Sensitivitas mouse", 0.5f, 6f, Settings.Sensitivity, new Vector2(0f, y), v => Settings.Sensitivity = v, v => v.ToString("0.0")); y += step;
            UIKit.MakeToggle(t, "Balik sumbu Y mouse", Settings.InvertY, new Vector2(0f, y), v => Settings.InvertY = v); y += step;
            UIKit.MakeSlider(t, "Bidang pandang (FOV)", 60f, 95f, Settings.Fov, new Vector2(0f, y), v => { Settings.Fov = v; Settings.Apply(); }, v => v.ToString("0")); y += step;
            UIKit.MakeSlider(t, "Kecerahan", -0.6f, 0.9f, Settings.Brightness, new Vector2(0f, y), v => { Settings.Brightness = v; Settings.Apply(); }, v => (v >= 0 ? "+" : "") + v.ToString("0.0")); y += step;
            UIKit.MakeSlider(t, "Volume utama", 0f, 1f, Settings.Master, new Vector2(0f, y), v => Settings.Master = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.MakeSlider(t, "Musik", 0f, 1f, Settings.Music, new Vector2(0f, y), v => Settings.Music = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.MakeSlider(t, "Efek suara", 0f, 1f, Settings.Sfx, new Vector2(0f, y), v => Settings.Sfx = v, v => Mathf.RoundToInt(v * 100) + "%"); y += step;
            UIKit.MakeToggle(t, "Guncangan kepala saat berjalan", Settings.HeadBob, new Vector2(0f, y), v => Settings.HeadBob = v); y += step;
            UIKit.MakeCycle(t, "Kualitas grafis", new[] { "Rendah", "Sedang", "Tinggi" }, Settings.Quality, new Vector2(0f, y), v => { Settings.Quality = v; Settings.Apply(); }); y += step;
            UIKit.MakeToggle(t, "Layar penuh", Settings.Fullscreen, new Vector2(0f, y), v => { Settings.Fullscreen = v; Settings.Apply(); }); y += step;
            UIKit.MakeToggle(t, "Petunjuk tutorial", Settings.Hints, new Vector2(0f, y), v => Settings.Hints = v); y += step;
            UIKit.MakeButton(t, "ResetTut", "Ulangi tutorial", new Vector2(0.5f, 1f), new Vector2(-400f, y - 14f), new Vector2(380f, 50f), () => { HUD.ResetHints(); hud.Toast("Petunjuk tutorial akan muncul lagi"); }, 26);
            UIKit.MakeButton(t, "ResetMem", "Hapus memori hantu", new Vector2(0.5f, 1f), new Vector2(40f, y - 14f), new Vector2(420f, 50f), () => { GhostMemory.Reset(); hud.Toast("Memori hantu dihapus"); }, 26);
            UIKit.MakeButton(t, "Back", "KEMBALI", new Vector2(0.5f, 0f), new Vector2(-120f, 60f), new Vector2(300f, 60f), () => { Settings.Save(); Settings.Apply(); back?.Invoke(); }, 38);
        }

        // ------------------------------------------------------------------ cara bermain
        void BuildHowTo()
        {
            howMenu = UIKit.Stretch(transform, "HowTo").gameObject;
            Image bg = UIKit.Fill(howMenu.transform, "Bg", new Color(0.01f, 0.01f, 0.01f, 0.92f)); bg.raycastTarget = true;
            Transform t = howMenu.transform;
            UIKit.Label(t, "Title", "CARA BERMAIN", 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 90f), UIKit.Ink, true);
            string txt =
                "<b><color=#ffcc73>TUJUAN</color></b>\nKumpulkan semua <b>relik</b> (cahaya keemasan), lalu kabur lewat <b>pintu keluar</b> di lantai 1. Satu relik biasanya ada di <b>ruang yang digembok</b> - cari <b>kunci tua</b> (cahaya merah) dulu.\n\n" +
                "<b><color=#ffcc73>KONTROL</color></b>\nWASD bergerak   Mouse melihat   Shift lari   C / Ctrl jongkok   F senter   E ambil / buka / sembunyi   Esc jeda\n\n" +
                "<b><color=#ffcc73>HANTU</color></b>\nHantu melihat (senter yang menyala membuatmu lebih terlihat, gelap dan jongkok membuatmu sulit terlihat) dan mendengar langkahmu (lari paling berisik). Bila dikejar, lari atau sembunyi di <b>lemari</b> / <b>kolong ranjang</b>.\n" +
                "Hantu <b>belajar</b>: tempat sembunyi dan jalur yang sering kamu pakai akan diperiksa dan dihadang lebih dulu. Variasikan kebiasaanmu! Lampu yang berkedip berarti hantu sudah dekat.\n\n" +
                "<b><color=#ffcc73>RUMAH YANG BERUBAH</color></b>\nSetiap rumah dibuat secara prosedural (1-2 lantai). Tingkat kesulitan menyesuaikan caramu bermain: bila sering tertangkap, rumah berikutnya lebih ramah; bila mudah lolos, hantu makin peka.\n\n" +
                "<b><color=#ffcc73>MODE PRESENTASI</color></b>\nF1 panel PCG/DDA/AI + peta   Tab panel   M peta   G rumah baru   F2/F3 kesulitan -/+   F5 bekukan hantu   F6 panggil hantu   F9 hapus memori hantu";
            UIKit.Label(t, "Body", txt, 24, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1400f, 760f), UIKit.Ink);
            UIKit.MakeButton(t, "Back", "KEMBALI", new Vector2(0.5f, 0f), new Vector2(-120f, 60f), new Vector2(300f, 60f), () => back?.Invoke(), 38);
        }

        // ------------------------------------------------------------------ layar akhir
        void BuildEnd()
        {
            endMenu = UIKit.Stretch(transform, "End").gameObject;
            Image bg = UIKit.Fill(endMenu.transform, "Bg", new Color(0f, 0f, 0f, 0.86f)); bg.raycastTarget = true;
            Transform t = endMenu.transform;
            endTitle = UIKit.Label(t, "Title", "", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(1600f, 140f), UIKit.Ink, true);
            endBody = UIKit.Label(t, "Body", "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 215f), new Vector2(1300f, 460f), UIKit.Ink, false, new Vector2(0.5f, 1f));
            endPrimary = UIKit.MakeButton(t, "Primary", "RUMAH BERIKUTNYA", new Vector2(0.5f, 0f), new Vector2(-480f, 120f), new Vector2(560f, 64f), () => GameManager.I.NextHouse(), 40);
            endPrimaryText = endPrimary.GetComponentInChildren<Text>();
            UIKit.MakeButton(t, "Menu", "MENU UTAMA", new Vector2(0.5f, 0f), new Vector2(160f, 120f), new Vector2(420f, 64f), () => GameManager.I.ToMenu(), 40);
        }

        void BuildIntro()
        {
            introCard = UIKit.Stretch(transform, "Intro").gameObject;
            introText = UIKit.Label(introCard.transform, "Text", "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 300f), UIKit.Ink, true);
        }

        // ------------------------------------------------------------------ tampil / sembunyi
        public void HideAll()
        {
            mainMenu.SetActive(false); pauseMenu.SetActive(false); settingsMenu.SetActive(false); howMenu.SetActive(false); endMenu.SetActive(false); introCard.SetActive(false);
        }
        public bool AnyMenu { get { return mainMenu.activeSelf || pauseMenu.activeSelf || settingsMenu.activeSelf || howMenu.activeSelf || endMenu.activeSelf; } }

        public void ShowMain()
        {
            HideAll(); mainMenu.SetActive(true); hud.SetVisible(false);
            bool has = GameManager.HasSave(out int house, out float d);
            continueBtn.gameObject.SetActive(has);
            float yy = -460f;
            foreach (RectTransform r in mainButtons)
            {
                if (!r.gameObject.activeSelf) continue;
                r.anchoredPosition = new Vector2(120f, yy); yy -= 70f;
            }
            if (has) continueLabel.text = "LANJUTKAN  <size=26><color=#9a9284>Rumah #" + (house + 1) + "  -  kesulitan " + Mathf.RoundToInt(d * 100) + "%</color></size>";
        }
        public void ShowPause() { HideAll(); pauseMenu.SetActive(true); }
        public void ShowSettings(System.Action ret) { back = ret; HideAll(); settingsMenu.SetActive(true); }
        public void ShowHowTo(System.Action ret) { back = ret; HideAll(); howMenu.SetActive(true); }
        public void ShowGame() { HideAll(); hud.SetVisible(true); }

        public void ShowEnd(bool won, string reason, string stats)
        {
            HideAll(); endMenu.SetActive(true);
            endTitle.text = won ? "<color=#ffd27a>LOLOS!</color>" : "<color=#ff3a28>TERTANGKAP</color>";
            endBody.text = reason + "\n\n" + stats;
            endPrimaryText.text = won ? "RUMAH BERIKUTNYA" : "COBA RUMAH BARU";
        }

        public void ShowIntro(string text, float seconds)
        {
            introCard.SetActive(true);
            introText.text = text;
            introT = seconds;
        }
        float introT;

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
                introT -= Time.unscaledDeltaTime;
                introText.color = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, Mathf.Clamp01(introT));
                if (introT <= 0f) introCard.SetActive(false);
            }
        }
    }
}
