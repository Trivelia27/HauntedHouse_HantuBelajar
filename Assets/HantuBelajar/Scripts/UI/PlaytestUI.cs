using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>
    /// Layar mode uji coba pemain: pengaturan (oleh peneliti), jeda antarsesi, kuesioner skala 1-5, perbandingan sesi, dan terima kasih.
    /// Butir kuesioner disusun dari dimensi Game Experience Questionnaire (kesenangan, ketegangan, tantangan, kompetensi, afek negatif)
    /// ditambah persepsi kesulitan dan persepsi adaptasi hantu.
    /// </summary>
    public class PlaytestUI : MonoBehaviour
    {
        public static readonly string[] Items =
        {
            "Permainan di sesi ini menyenangkan.",
            "Saya merasa tegang atau takut selama bermain.",
            "Tingkat kesulitannya pas dengan kemampuan saya.",
            "Menurut saya, permainan di sesi ini...",
            "Saya merasa frustrasi.",
            "Saya merasa mampu menguasai permainan.",
            "Hantu terasa menyesuaikan diri dengan cara saya bermain.",
            "Saya ingin memainkan sesi ini lagi.",
        };
        public static readonly string[] ItemShort = { "Seru", "Tegang", "Kesulitan pas", "Kesulitan", "Frustrasi", "Mampu", "Hantu adaptif", "Main lagi" };
        public static readonly string[] CompareItems = { "Sesi mana yang lebih seru?", "Sesi mana yang tingkat kesulitannya lebih pas?", "Di sesi mana hantu terasa lebih pintar?" };

        GameObject setup, info, quest, compare, done;
        Text setupNum, setupMin, setupPrac, setupPath;
        int num = 1; float minutes = 8f, practice = 3f;
        Text infoTitle, infoBody, infoBtnText; System.Action infoNext;
        Text questTitle, questCount; Button questNext; CanvasGroup questNextCg;
        readonly int[] qAns = new int[8]; readonly UIHover[,] qBtn = new UIHover[8, 5]; System.Action<int[]> questDone;
        readonly int[] cAns = new int[3]; readonly UIHover[,] cBtn = new UIHover[3, 3]; Button compareNext; CanvasGroup compareNextCg; System.Action<int[]> compareDone;
        Text doneBody, donePath; string doneFolder;

        public bool AnyOpen { get { return setup.activeSelf || info.activeSelf || quest.activeSelf || compare.activeSelf || done.activeSelf; } }

        // ------------------------------------------------------------------ bangun
        static RectTransform MakePanel(Transform parent, string name, Vector2 cardSize, out RectTransform card)
        {
            RectTransform m = UIKit.Stretch(parent, name);
            m.gameObject.AddComponent<ScreenFade>();
            Image bg = UIKit.Fill(m, "Bg", new Color(0.015f, 0.012f, 0.014f, 0.97f)); bg.raycastTarget = true;
            card = UIKit.Card(m, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
            return m;
        }

        public void Build(Transform canvas)
        {
            // pengaturan (peneliti)
            setup = MakePanel(canvas, "PlaytestSetup", new Vector2(1120f, 780f), out RectTransform c).gameObject;
            UIKit.Label(c, "Title", "UJI COBA PEMAIN", 46, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(900f, 60f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Label(c, "Sub", "Mode penelitian: membandingkan DDA dengan kesulitan tetap pada pemain sungguhan.", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(62f, -116f), new Vector2(1000f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            setupNum = StepperRow(c, "Nomor peserta", -190f, () => { num = Mathf.Max(1, num - 1); RefreshSetup(); }, () => { num = Mathf.Min(99, num + 1); RefreshSetup(); });
            setupMin = StepperRow(c, "Durasi tiap sesi", -262f, () => { minutes = Mathf.Max(2f, minutes - 1f); RefreshSetup(); }, () => { minutes = Mathf.Min(20f, minutes + 1f); RefreshSetup(); });
            setupPrac = StepperRow(c, "Durasi latihan", -334f, () => { practice = Mathf.Max(1f, practice - 1f); RefreshSetup(); }, () => { practice = Mathf.Min(6f, practice + 1f); RefreshSetup(); });
            UIKit.Label(c, "Flow", "Alur: panduan bermain  →  latihan (panduan awal)  →  Sesi A  →  kuesioner  →  Sesi B  →  kuesioner  →  perbandingan.\n" +
                        "Satu sesi memakai DDA, sesi lain kesulitan tetap. Urutannya diatur otomatis (nomor ganjil DDA dulu, genap Tetap dulu) dan " +
                        "tidak ditampilkan ke peserta. Hantu, rumah, dan kesulitan awal disamakan untuk semua peserta.", 21, TextAnchor.UpperLeft,
                        new Vector2(0f, 1f), new Vector2(62f, -420f), new Vector2(1000f, 130f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            setupPath = UIKit.Label(c, "Path", "", 17, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(62f, -600f), new Vector2(1000f, 50f), UIKit.Faint, FontKind.Body, new Vector2(0f, 1f));
            UIKit.MakeButton(c, "PtBack", "KEMBALI", new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(260f, 64f), () => { HideAll(); GameManager.I.ui.ShowMain(); }, 24, BtnStyle.Secondary, null, new Vector2(0f, 0f));
            UIKit.MakeButton(c, "PtStart", "MULAI UJI COBA", new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(380f, 64f), StartStudy, 25, BtnStyle.Primary, null, new Vector2(1f, 0f));

            // jeda / informasi antarsesi
            info = MakePanel(canvas, "PlaytestInfo", new Vector2(1060f, 560f), out c).gameObject;
            infoTitle = UIKit.Label(c, "Title", "", 46, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(980f, 60f), UIKit.Ink, FontKind.Head, new Vector2(0.5f, 1f));
            infoBody = UIKit.Label(c, "Body", "", 25, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 220f), UIKit.Dim, FontKind.Body, new Vector2(0.5f, 1f));
            Button ib = UIKit.MakeButton(c, "PtInfoNext", "", new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(420f, 70f), () => { System.Action n = infoNext; infoNext = null; HideAll(); n?.Invoke(); }, 26, BtnStyle.Primary, null, new Vector2(0.5f, 0f));
            infoBtnText = ib.transform.Find("Text").GetComponent<Text>();

            // kuesioner
            quest = MakePanel(canvas, "PlaytestQuest", new Vector2(1560f, 1000f), out c).gameObject;
            questTitle = UIKit.Label(c, "Title", "", 40, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(56f, -40f), new Vector2(1100f, 52f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Label(c, "Sub", "Pilih angka 1-5 untuk tiap pernyataan tentang sesi yang baru saja kamu mainkan. Tidak ada jawaban benar atau salah.", 21, TextAnchor.UpperLeft,
                        new Vector2(0f, 1f), new Vector2(58f, -98f), new Vector2(1400f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            for (int i = 0; i < Items.Length; i++)
            {
                float y = -150f - i * 90f;
                if (i % 2 == 0) UIKit.Rounded(c, "Band" + i, new Vector2(0.5f, 1f), new Vector2(0f, y + 8f), new Vector2(1480f, 90f), new Color(1f, 1f, 1f, 0.025f), 10, new Vector2(0.5f, 1f));
                UIKit.Label(c, "Q" + i, (i + 1) + ".  " + Items[i], 24, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(64f, y - 30f), new Vector2(820f, 64f), UIKit.Ink, FontKind.Body, new Vector2(0f, 0.5f));
                for (int k = 0; k < 5; k++)
                {
                    int qi = i, v = k + 1;
                    Button b = UIKit.MakeButton(c, "Q" + i + "_" + v, v.ToString(), new Vector2(0f, 1f), new Vector2(940f + k * 92f, y - 6f), new Vector2(76f, 50f), () => AnswerQ(qi, v), 24, BtnStyle.Secondary, null, new Vector2(0f, 1f));
                    qBtn[i, k] = b.GetComponent<UIHover>();
                }
                string lo = i == 3 ? "terlalu mudah" : "sangat tidak setuju", hi = i == 3 ? "terlalu sulit" : "sangat setuju";
                UIKit.Label(c, "Lo" + i, lo, 14, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(940f, y - 60f), new Vector2(200f, 20f), UIKit.Faint, FontKind.Body, new Vector2(0f, 1f));
                UIKit.Label(c, "Hi" + i, hi, 14, TextAnchor.UpperRight, new Vector2(0f, 1f), new Vector2(1384f, y - 60f), new Vector2(200f, 20f), UIKit.Faint, FontKind.Body, new Vector2(1f, 1f));
                if (i == 3) UIKit.Label(c, "Mid" + i, "pas", 14, TextAnchor.UpperCenter, new Vector2(0f, 1f), new Vector2(1162f, y - 60f), new Vector2(80f, 20f), UIKit.Faint, FontKind.Body, new Vector2(0.5f, 1f));
            }
            questCount = UIKit.Label(c, "Count", "", 21, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(60f, 76f), new Vector2(500f, 40f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
            questNext = UIKit.MakeButton(c, "PtQuestNext", "LANJUT", new Vector2(1f, 0f), new Vector2(-56f, 44f), new Vector2(300f, 64f), SubmitQuest, 25, BtnStyle.Primary, null, new Vector2(1f, 0f));
            questNextCg = questNext.gameObject.AddComponent<CanvasGroup>();

            // perbandingan
            compare = MakePanel(canvas, "PlaytestCompare", new Vector2(1320f, 660f), out c).gameObject;
            UIKit.Label(c, "Title", "PERBANDINGAN SESI", 42, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(1100f, 56f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            UIKit.Label(c, "Sub", "Bandingkan Sesi A dan Sesi B yang baru saja kamu mainkan.", 21, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(58f, -104f), new Vector2(1100f, 30f), UIKit.Dim, FontKind.Body, new Vector2(0f, 1f));
            string[] opt = { "SESI A", "SAMA SAJA", "SESI B" };
            for (int i = 0; i < CompareItems.Length; i++)
            {
                float y = -176f - i * 110f;
                UIKit.Label(c, "C" + i, CompareItems[i], 25, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(64f, y - 30f), new Vector2(620f, 60f), UIKit.Ink, FontKind.Body, new Vector2(0f, 0.5f));
                for (int k = 0; k < 3; k++)
                {
                    int ci = i, v = k;
                    Button b = UIKit.MakeButton(c, "C" + i + "_" + k, opt[k], new Vector2(0f, 1f), new Vector2(690f + k * 196f, y - 4f), new Vector2(180f, 54f), () => AnswerC(ci, v), 21, BtnStyle.Secondary, null, new Vector2(0f, 1f));
                    cBtn[i, k] = b.GetComponent<UIHover>();
                }
            }
            compareNext = UIKit.MakeButton(c, "PtCompareNext", "SELESAI", new Vector2(1f, 0f), new Vector2(-56f, 44f), new Vector2(300f, 64f), SubmitCompare, 25, BtnStyle.Primary, null, new Vector2(1f, 0f));
            compareNextCg = compareNext.gameObject.AddComponent<CanvasGroup>();

            // selesai
            done = MakePanel(canvas, "PlaytestDone", new Vector2(1100f, 560f), out c).gameObject;
            UIKit.Icon(c, "Icon", "check", new Vector2(0.5f, 1f), new Vector2(0f, -50f), 64f, UIKit.Good, new Vector2(0.5f, 1f));
            UIKit.Label(c, "Title", "TERIMA KASIH!", 50, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1000f, 64f), UIKit.Ink, FontKind.Head, new Vector2(0.5f, 1f));
            doneBody = UIKit.Label(c, "Body", "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(960f, 70f), UIKit.Dim, FontKind.Body, new Vector2(0.5f, 1f));
            donePath = UIKit.Label(c, "Path", "", 17, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(980f, 50f), UIKit.Faint, FontKind.Body, new Vector2(0.5f, 1f));
            UIKit.MakeButton(c, "PtOpenFolder", "BUKA FOLDER DATA", new Vector2(0.5f, 0f), new Vector2(-200f, 52f), new Vector2(360f, 64f), () => Application.OpenURL("file:///" + doneFolder.Replace('\\', '/')), 22, BtnStyle.Secondary, null, new Vector2(0.5f, 0f));
            UIKit.MakeButton(c, "PtDoneMenu", "MENU UTAMA", new Vector2(0.5f, 0f), new Vector2(200f, 52f), new Vector2(360f, 64f), () => { HideAll(); GameManager.I.BackToMenu(); }, 24, BtnStyle.Primary, null, new Vector2(0.5f, 0f));

            HideAll();
        }

        Text StepperRow(RectTransform card, string label, float y, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            RectTransform row = UIKit.Node(card, "Row_" + label, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, 60f), new Vector2(0.5f, 1f));
            UIKit.Rounded(row, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 60f), new Color(1f, 1f, 1f, 0.03f), 8);
            UIKit.Label(row, "Label", label, 24, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(500f, 50f), UIKit.Ink, FontKind.Body, new Vector2(0f, 0.5f));
            UIKit.MakeButton(row, "Minus_" + label, "-", new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(52f, 44f), minus, 26, BtnStyle.Secondary, null, new Vector2(1f, 0.5f));
            Text v = UIKit.Label(row, "Value", "", 25, TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(-136f, 0f), new Vector2(170f, 50f), UIKit.Gold, FontKind.Head, new Vector2(0.5f, 0.5f));
            UIKit.MakeButton(row, "Plus_" + label, "+", new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(52f, 44f), plus, 26, BtnStyle.Secondary, null, new Vector2(1f, 0.5f));
            return v;
        }

        // ------------------------------------------------------------------ tampil
        public void HideAll()
        {
            if (setup == null) return;
            setup.SetActive(false); info.SetActive(false); quest.SetActive(false); compare.SetActive(false); done.SetActive(false);
        }

        void Open(GameObject g)
        {
            GameManager.I.ui.HideAll();
            GameManager.I.ui.hud.SetVisible(false);
            g.SetActive(true);
        }

        public void ShowSetup()
        {
            num = Playtest.NextParticipant(); minutes = 8f; practice = 3f;
            RefreshSetup();
            Open(setup);
        }

        void RefreshSetup()
        {
            setupNum.text = "P" + num.ToString("00");
            setupMin.text = Mathf.RoundToInt(minutes) + " menit";
            setupPrac.text = Mathf.RoundToInt(practice) + " menit";
            setupPath.text = "Data disimpan di: " + System.IO.Path.Combine(Playtest.DataRoot, "P" + num.ToString("00"));
        }

        void StartStudy() { HideAll(); Playtest.Ensure().Begin(num, minutes, practice); }

        public void ShowBreak(string title, string body, string button, System.Action next)
        {
            infoTitle.text = title; infoBody.text = body; infoBtnText.text = button; infoNext = next;
            Open(info);
        }

        public void ShowQuestionnaire(string session, System.Action<int[]> onDone)
        {
            for (int i = 0; i < qAns.Length; i++) { qAns[i] = 0; for (int k = 0; k < 5; k++) Mark(qBtn[i, k], false); }
            questTitle.text = "KUESIONER  ·  " + session;
            questDone = onDone;
            RefreshQuest();
            Open(quest);
        }

        public void ShowCompare(System.Action<int[]> onDone)
        {
            for (int i = 0; i < cAns.Length; i++) { cAns[i] = -1; for (int k = 0; k < 3; k++) Mark(cBtn[i, k], false); }
            compareDone = onDone;
            RefreshCompare();
            Open(compare);
        }

        public void ShowDone(string code, string folder)
        {
            doneFolder = folder;
            doneBody.text = "Data peserta " + code + " sudah tersimpan. Silakan panggil peneliti.";
            donePath.text = folder;
            Open(done);
        }

        // ------------------------------------------------------------------ jawaban
        public void AnswerQ(int i, int v)
        {
            qAns[i] = v;
            for (int k = 0; k < 5; k++) Mark(qBtn[i, k], k + 1 == v);
            RefreshQuest();
        }

        public void AnswerC(int i, int v)
        {
            cAns[i] = v;
            for (int k = 0; k < 3; k++) Mark(cBtn[i, k], k == v);
            RefreshCompare();
        }

        void RefreshQuest()
        {
            int n = 0; foreach (int a in qAns) if (a > 0) n++;
            questCount.text = n + " / " + qAns.Length + " terjawab";
            bool ok = n == qAns.Length;
            questNext.interactable = ok; questNextCg.alpha = ok ? 1f : 0.35f;
        }

        void RefreshCompare()
        {
            bool ok = true; foreach (int a in cAns) if (a < 0) ok = false;
            compareNext.interactable = ok; compareNextCg.alpha = ok ? 1f : 0.35f;
        }

        void SubmitQuest()
        {
            foreach (int a in qAns) if (a <= 0) return;
            int[] copy = (int[])qAns.Clone();
            System.Action<int[]> d = questDone; questDone = null;
            HideAll();
            d?.Invoke(copy);
        }

        void SubmitCompare()
        {
            foreach (int a in cAns) if (a < 0) return;
            int[] copy = (int[])cAns.Clone();
            System.Action<int[]> d = compareDone; compareDone = null;
            HideAll();
            d?.Invoke(copy);
        }

        static void Mark(UIHover h, bool on)
        {
            if (h == null) return;
            h.selected = on;
            Color g = UIKit.Gold, dark = new Color(0.1f, 0.07f, 0.04f);
            h.bgN = on ? new Color(g.r, g.g, g.b, 0.92f) : new Color(1f, 1f, 1f, 0.04f);
            h.bgH = on ? new Color(g.r, g.g, g.b, 1f) : new Color(1f, 1f, 1f, 0.11f);
            h.borderN = on ? new Color(g.r, g.g, g.b, 1f) : new Color(1f, 0.93f, 0.82f, 0.18f);
            h.textN = on ? dark : UIKit.Ink; h.textH = on ? dark : UIKit.Gold;
            h.Refresh();
        }
    }
}
