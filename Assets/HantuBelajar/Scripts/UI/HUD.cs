using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>HUD permainan: tujuan, kompas, peta mini, senter & stamina, prompt interaksi, petunjuk, panduan awal, panel presentasi.</summary>
    public class HUD : MonoBehaviour
    {
        public RectTransform root;
        public MapUI map;
        public bool present, showDebug;
        public bool TutorialActive { get; private set; }

        Image edge, hideL, hideR, hideTop, crossDot, crossRing, keySlot, battIcon, stamFill, hintIcon, promptBg;
        readonly Image[] relicSlots = new Image[6];
        readonly Image[] battSegs = new Image[10];
        Text objTitle, objText, objMeta, battPct, battMode, promptTxt, toastTitle, toastSub, hintTxt, statusTxt, feedTxt, debugTxt;
        RectTransform toastRt, hintRt, promptRt, promptKey, tutCard, debugCard, feedCard, hiddenPill;
        CanvasGroup toastCg, hintCg, tutCg, stamCg;
        float toastT, toastAge, hintT, hintAge, stamShow, debugT;
        string lastPrompt;
        readonly HashSet<string> shownHints = new HashSet<string>();

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

            // notifikasi
            toastRt = UIKit.Card(root, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(700f, 78f), new Vector2(0.5f, 1f), new Color(0.06f, 0.055f, 0.055f, 0.93f), 14);
            toastCg = toastRt.gameObject.AddComponent<CanvasGroup>();
            toastTitle = UIKit.Label(toastRt, "Title", "", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1200f, 32f), UIKit.Gold, FontKind.Head);
            toastSub = UIKit.Label(toastRt, "Sub", "", 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1200f, 28f), UIKit.Dim, FontKind.Body);

            // petunjuk (bawah tengah)
            hintRt = UIKit.Card(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 72f), new Vector2(0.5f, 0f), new Color(0.05f, 0.06f, 0.08f, 0.93f), 14);
            hintCg = hintRt.gameObject.AddComponent<CanvasGroup>();
            hintIcon = UIKit.Icon(hintRt, "Icon", "info", new Vector2(0f, 0.5f), new Vector2(22f, 0f), 30f, UIKit.Cold, new Vector2(0f, 0.5f));
            hintTxt = UIKit.Label(hintRt, "Text", "", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(800f, 60f), new Color(0.88f, 0.92f, 1f), FontKind.Body, new Vector2(0f, 0.5f));

            // status bersembunyi
            hiddenPill = UIKit.Card(root, "Hidden", new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(560f, 52f), new Vector2(0.5f, 0f), new Color(0.03f, 0.045f, 0.06f, 0.88f), 12, false);
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

            // mode presentasi
            statusTxt = UIKit.Label(root, "GhostStatus", "", 22, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(1000f, 34f), UIKit.Ink, FontKind.Head);
            debugCard = UIKit.Card(root, "Debug", new Vector2(0f, 0.5f), new Vector2(28f, -70f), new Vector2(780f, 690f), new Vector2(0f, 0.5f), new Color(0.03f, 0.03f, 0.035f, 0.9f));
            debugTxt = UIKit.Label(debugCard, "Text", "", 17, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(744f, 670f), UIKit.Ink, FontKind.Body, new Vector2(0f, 1f), false);
            feedCard = UIKit.Card(root, "Feed", new Vector2(1f, 0f), new Vector2(-28f, 26f), new Vector2(780f, 196f), new Vector2(1f, 0f), new Color(0.04f, 0.03f, 0.035f, 0.9f));
            UIKit.Label(feedCard, "Title", "MEMORI HANTU (LANGSUNG)", 16, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(500f, 22f), UIKit.Gold, FontKind.Head, new Vector2(0f, 1f));
            feedTxt = UIKit.Label(feedCard, "Text", "", 17, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(748f, 150f), new Color(1f, 0.84f, 0.78f), FontKind.Body, new Vector2(0f, 0f), false);

            map.BuildFull(root);
            foreach (string id in PlayerPrefs.GetString("HB_HintsShown", "").Split(',')) if (id.Length > 0) shownHints.Add(id);
            toastRt.gameObject.SetActive(false); hintRt.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API
        public void Toast(string title, string sub = null)
        {
            toastTitle.text = title;
            toastSub.text = sub ?? "";
            bool two = !string.IsNullOrEmpty(sub);
            float w = Mathf.Max(toastTitle.preferredWidth, two ? toastSub.preferredWidth : 0f);
            toastRt.sizeDelta = new Vector2(Mathf.Clamp(w + 80f, 320f, 1300f), two ? 82f : 52f);
            toastTitle.rectTransform.anchoredPosition = new Vector2(0f, two ? -25f : -26f);
            toastT = two ? 4.5f : 3.2f; toastAge = 0f;
            toastRt.gameObject.SetActive(true);
        }

        /// <summary>Petunjuk kontekstual (sekali per pemain, bisa dimatikan di Pengaturan).</summary>
        public void Hint(string id, string text, string icon = "info")
        {
            if (!Settings.Hints || shownHints.Contains(id)) return;
            shownHints.Add(id);
            PlayerPrefs.SetString("HB_HintsShown", string.Join(",", shownHints));
            hintTxt.text = text;
            hintIcon.sprite = Icons.Get(icon);
            hintTxt.rectTransform.sizeDelta = new Vector2(980f, 60f);
            float w = Mathf.Min(hintTxt.preferredWidth, 980f);
            hintTxt.rectTransform.sizeDelta = new Vector2(w + 6f, 60f);
            float h = Mathf.Max(64f, hintTxt.preferredHeight + 30f);
            hintRt.sizeDelta = new Vector2(w + 96f, h);
            hintTxt.rectTransform.sizeDelta = new Vector2(w + 6f, h - 10f);
            hintT = 7f; hintAge = 0f;
            hintRt.gameObject.SetActive(true);
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
            toastT = 0f; hintT = 0f; toastRt.gameObject.SetActive(false); hintRt.gameObject.SetActive(false);
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
                if (Input.GetKeyDown(KeyCode.F1)) { present = !present; showDebug = present; Toast(present ? "Mode presentasi" : "Mode presentasi dimatikan", present ? "Panel PCG / DDA / AI, hantu & heatmap jalur di peta" : null); }
                if (Input.GetKeyDown(KeyCode.Tab)) showDebug = !showDebug;
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
            objMeta.text = L.FloorName(p.floor).ToUpper() + "   ·   RUMAH #" + gm.houseNo + (present ? "   ·   DDA " + gm.dda.Label + " " + Mathf.RoundToInt(gm.dda.d * 100f) + "%" : "");

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

            // notifikasi & petunjuk
            if (toastT > 0f)
            {
                toastT -= dt; toastAge += dt;
                toastCg.alpha = Mathf.Min(Mathf.Clamp01(toastAge * 5f), Mathf.Clamp01(toastT / 0.5f));
                toastRt.anchoredPosition = new Vector2(0f, -116f + (1f - Mathf.Clamp01(toastAge * 5f)) * 14f);
                if (toastT <= 0f) toastRt.gameObject.SetActive(false);
            }
            if (hintT > 0f)
            {
                hintT -= dt; hintAge += dt;
                hintCg.alpha = Mathf.Min(Mathf.Clamp01(hintAge * 4f), Mathf.Clamp01(hintT / 0.6f));
                if (hintT <= 0f) hintRt.gameObject.SetActive(false);
            }

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
            bool hp = p.hidden && gm.Playing;
            if (hiddenPill.gameObject.activeSelf != hp) hiddenPill.gameObject.SetActive(hp);

            // presentasi
            statusTxt.text = present && gh != null ? "Hantu: <color=" + StateColor(gh.state) + ">" + gh.state.ToString().ToUpper() + "</color>" + (gh.canSee ? "  <color=#ff4030>(MELIHATMU)</color>" : gh.heardRecently ? "  <color=#ffb030>(MENDENGAR)</color>" : "") + (gh.frozen ? "  <color=#8080ff>[BEKU]</color>" : gh.Asleep ? "  <color=#8080ff>[TIDUR]</color>" : "") : "";
            bool fd = present && gm.Playing;
            if (feedCard.gameObject.activeSelf != fd) feedCard.gameObject.SetActive(fd);
            if (fd) feedTxt.text = string.Join("\n", GhostMemory.feed);
            bool dbg = showDebug && gm.Playing;
            if (debugCard.gameObject.activeSelf != dbg) debugCard.gameObject.SetActive(dbg);
            debugT -= dt;
            if (dbg && debugT <= 0f) { debugT = 0.2f; debugTxt.text = DebugText(gm); }

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
            if (basics && !basicsAnnounced) { basicsAnnounced = true; Toast("Hantu akan segera bangun...", "Kumpulkan relik sebelum ia menemukanmu"); }
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
                if (tutEndT > 2.5f) { TutorialActive = false; TutDone = true; Toast("Panduan selesai", "Selamat bertahan. Hantu mulai mempelajari kebiasaanmu..."); }
            }
        }

        void Done(int i) { tutDone[i] = true; AudioDirector.I?.Click(); }

        // ------------------------------------------------------------------ panel presentasi
        static string StateColor(GhostState s)
        {
            switch (s)
            {
                case GhostState.Kejar: return "#ff4030";
                case GhostState.Hadang: case GhostState.Jaga: return "#d080ff";
                case GhostState.Patroli: return "#9cc4ff";
                case GhostState.Muncul: return "#a0a0a0";
                default: return "#ffb030";
            }
        }

        string DebugText(GameManager gm)
        {
            HouseLayout L = gm.layout; PcgParams pp = L.p; DDAManager d = gm.dda; GhostAI g = gm.ghost;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b><color=#88ccff>PCG</color></b>  BSP + Kruskal + tangga + lock-and-key + generate-and-test");
            sb.AppendLine("seed " + pp.seed + "  " + pp.width + "x" + pp.height + " sel x " + L.floors + " lantai  ruang " + L.rooms.Count + "  pintu " + L.doors.Count + " (loop " + L.loops + ")");
            sb.AppendLine("relik " + L.relics.Count + " (jarak ke tempat sembunyi <= " + pp.relicSpotDist + ", nyata " + L.relicSpotMax + ")  sembunyi " + L.spots.Count + "  baterai " + L.batteries.Count);
            sb.AppendLine("ruang terkunci " + (L.lockedRoom >= 0 ? "ya (" + HouseGenerator.RoomName(L.rooms[L.lockedRoom].type) + ")" : "tidak") + "  lampu " + Mathf.RoundToInt(pp.litRatio * 100) + "%  perabot " + L.props.Count);
            sb.AppendLine("validasi " + (L.valid ? "<color=#80ff80>LOLOS</color>" : "<color=#ff8080>" + L.failReason + "</color>") + "  percobaan " + L.attempts + "  " + L.ms.ToString("0.0") + " ms  hash " + L.hash);
            sb.AppendLine();
            sb.AppendLine("<b><color=#ffcc66>DDA</color></b>  d = <b>" + d.d.ToString("0.00") + "</b> " + d.Label + "   P " + d.P.ToString("0.00") + " (halus " + d.Ps.ToString("0.00") + ")  target " + DDAManager.Target);
            sb.AppendLine("tempo " + d.pace.ToString("0.00") + "  aman " + d.safety.ToString("0.00") + "  baterai " + d.resource.ToString("0.00") + "  tenang " + d.calm.ToString("0.00") + "  nyaris " + d.nearMisses);
            sb.AppendLine("<size=15>evaluasi tiap " + DDAManager.Window + " dtk (" + d.evalT.ToString("0") + ")  batas per rumah +" + DDAManager.MaxRise + " / -" + DDAManager.MaxDrop + "  tertangkap " + d.caughtTotal + "  lolos " + d.winsTotal + "</size>");
            sb.AppendLine("-> hantu: patroli " + d.gp.patrolSpeed.ToString("0.0") + "  kejar " + d.gp.chaseSpeed.ToString("0.0") + " m/s  lihat " + d.gp.visionRange.ToString("0") + " m/" + d.gp.fov.ToString("0") + " deg");
            sb.AppendLine("   dengar x" + d.gp.hearingMult.ToString("0.00") + "  cari " + d.gp.searchTime.ToString("0") + " dtk  belajar " + d.gp.learnWeight.ToString("0.00") + "  jaga " + d.gp.guardTime.ToString("0") + " dtk  eksplorasi " + d.gp.explore.ToString("0.00"));
            PcgParams nx = d.MakePcg(0);
            sb.AppendLine("-> rumah berikut: " + nx.floors + " lantai " + nx.width + "x" + nx.height + "  sembunyi " + nx.hidingSpots + "  baterai " + nx.batteries + "  relik " + nx.relicCount + "  jarak<=" + nx.relicSpotDist);
            foreach (string s in d.log) sb.AppendLine("<size=14><color=#b0b0b0>" + s + "</color></size>");
            sb.AppendLine();
            sb.AppendLine("<b><color=#ff8877>GAME AI</color></b>  FSM + A* + memori Bayesian (Thompson sampling)");
            sb.AppendLine("state <b>" + g.state + "</b>  lihat " + (g.canSee ? "YA" : "-") + "  dengar " + (g.heardRecently ? "YA" : "-") + "  tak bertemu " + g.unseenTime.ToString("0") + " dtk");
            sb.AppendLine("<size=15>" + g.decision + "</size>");
            if (g.searchInfo.Length > 0) sb.AppendLine("<size=14><color=#ffd090>" + g.searchInfo + "</color></size>");
            sb.AppendLine("preferensi: lemari " + (GhostMemory.KindPref(0) * 100f).ToString("0") + "%  kolong " + (GhostMemory.KindPref(1) * 100f).ToString("0") + "%  (sembunyi " + GhostMemory.totalHides + ", diperiksa " + GhostMemory.totalChecks + ")");
            sb.AppendLine("<size=14><color=#9fd8ff>Pemain yang bersembunyi tidak pernah ditemukan; kebiasaan hanya dipakai untuk menghadang & berjaga.</color></size>");
            List<HidingSpot> top = new List<HidingSpot>(gm.house.spots);
            top.Sort((a, b) => GhostMemory.Mean(b).CompareTo(GhostMemory.Mean(a)));
            for (int i = 0; i < top.Count && i < 3; i++)
            {
                HidingSpot s = top[i];
                GhostMemory.AlphaBeta(s, out float a, out float b);
                sb.AppendLine("<size=15>  #" + s.id + " " + s.KindName + " Beta(" + a.ToString("0.0") + ", " + b.ToString("0.0") + ")  E=" + GhostMemory.Mean(s).ToString("0.00") + "  dipakai " + GhostMemory.Uses(s) + "x</size>");
            }
            return sb.ToString();
        }
    }
}
