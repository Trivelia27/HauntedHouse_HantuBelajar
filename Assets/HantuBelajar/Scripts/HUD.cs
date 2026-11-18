using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    public class HUD : MonoBehaviour
    {
        Font font;
        Text caption, captionSub; Image captionBg;
        Text objective, prompt, toast, debugTxt, feedTxt, overlayTitle, overlayBody, stateTag;
        Image batteryFill, staminaFill, vignette, overlayBg, hideMask, mapFrame, debugBg;
        RawImage map; Texture2D mapTex;
        GameObject overlay;
        float toastT, mapT;
        public bool showDebug = true, showMap = true;

        public static HUD Create()
        {
            GameObject g = new GameObject("HUD");
            Canvas c = g.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 10;
            CanvasScaler cs = g.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
            HUD h = g.AddComponent<HUD>();
            h.Build();
            return h;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vignette = Img("Vignette", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.5f, 0f, 0f, 0f));
            vignette.sprite = Sprite.Create(Look.Radial("vig", 0.35f), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            hideMask = Img("HideMask", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.75f));
            hideMask.sprite = Sprite.Create(Look.Radial("hide", 0.15f), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);

            objective = Txt("Objective", new Vector2(0, 1), new Vector2(30, -25), new Vector2(700, 90), 30, TextAnchor.UpperLeft);
            stateTag = Txt("GhostTag", new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(900, 50), 28, TextAnchor.UpperCenter);
            toast = Txt("Toast", new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 50), 30, TextAnchor.UpperCenter);
            prompt = Txt("Prompt", new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(900, 50), 28, TextAnchor.MiddleCenter);
            Txt("Cross", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40), 26, TextAnchor.MiddleCenter).text = "+";

            Txt("BatLbl", new Vector2(0, 0), new Vector2(30, 95), new Vector2(200, 34), 24, TextAnchor.LowerLeft).text = "SENTER";
            batteryFill = Bar(new Vector2(150, 100), new Color(1f, 0.85f, 0.4f));
            Txt("StaLbl", new Vector2(0, 0), new Vector2(30, 50), new Vector2(200, 34), 24, TextAnchor.LowerLeft).text = "STAMINA";
            staminaFill = Bar(new Vector2(150, 55), new Color(0.5f, 0.85f, 1f));

            debugBg = Img("DebugBg", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, -10), new Vector2(640, 760), new Color(0, 0, 0, 0.62f));
            debugBg.rectTransform.pivot = new Vector2(0, 0.5f);
            debugTxt = Txt("Debug", new Vector2(0, 0.5f), new Vector2(34, -10), new Vector2(615, 745), 19, TextAnchor.UpperLeft);
            debugTxt.rectTransform.pivot = new Vector2(0, 0.5f);
            debugTxt.supportRichText = true;

            mapFrame = Img("MapBg", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(470, 370), new Color(0, 0, 0, 0.6f));
            mapFrame.rectTransform.pivot = new Vector2(1, 1);
            GameObject mg = new GameObject("Map", typeof(RectTransform)); mg.transform.SetParent(mapFrame.transform, false);
            map = mg.AddComponent<RawImage>();
            RectTransform mr = map.rectTransform; mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = new Vector2(8, 8); mr.offsetMax = new Vector2(-8, -8);

            feedTxt = Txt("Feed", new Vector2(1, 0), new Vector2(-30, 40), new Vector2(760, 210), 21, TextAnchor.LowerRight);
            feedTxt.rectTransform.pivot = new Vector2(1, 0);

            captionBg = Img("CaptionBg", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1250, 120), new Color(0.05f, 0, 0, 0.78f));
            captionBg.rectTransform.pivot = new Vector2(0.5f, 0);
            caption = Txt("Caption", new Vector2(0.5f, 0), new Vector2(0, 222), new Vector2(1220, 50), 38, TextAnchor.MiddleCenter);
            captionSub = Txt("CaptionSub", new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1220, 60), 24, TextAnchor.MiddleCenter);
            caption.color = new Color(1f, 0.8f, 0.45f);
            Caption("", "");
            overlayBg = Img("Overlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.82f));
            overlay = overlayBg.gameObject;
            overlayTitle = Txt("OvTitle", new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1600, 140), 84, TextAnchor.MiddleCenter);
            overlayTitle.transform.SetParent(overlay.transform, true);
            overlayBody = Txt("OvBody", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(1400, 520), 28, TextAnchor.UpperCenter);
            overlayBody.transform.SetParent(overlay.transform, true);
        }

        Text Txt(string n, Vector2 anchor, Vector2 pos, Vector2 size, int fs, TextAnchor al)
        {
            GameObject g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(transform, false);
            Text t = g.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = al; t.color = new Color(0.93f, 0.9f, 0.85f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            Outline o = g.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(1.5f, -1.5f);
            RectTransform r = t.rectTransform; r.anchorMin = r.anchorMax = anchor; r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size;
            return t;
        }
        Image Img(string n, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size, Color c)
        {
            GameObject g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(transform, false);
            Image i = g.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            RectTransform r = i.rectTransform; r.anchorMin = amin; r.anchorMax = amax; r.anchoredPosition = pos;
            if (amin == amax) r.sizeDelta = size; else { r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }
            return i;
        }
        Image Bar(Vector2 pos, Color c)
        {
            Image bg = Img("BarBg", Vector2.zero, Vector2.zero, pos, new Vector2(300, 18), new Color(0, 0, 0, 0.6f));
            bg.rectTransform.pivot = Vector2.zero;
            Image f = Img("Fill", Vector2.zero, Vector2.zero, pos + new Vector2(3, 3), new Vector2(294, 12), c);
            f.rectTransform.pivot = Vector2.zero;
            return f;
        }

        public void Caption(string title, string sub)
        {
            bool on = !string.IsNullOrEmpty(title);
            caption.text = title; captionSub.text = sub;
            caption.enabled = captionSub.enabled = captionBg.enabled = on;
            if (on) { captionBg.transform.SetAsLastSibling(); caption.transform.SetAsLastSibling(); captionSub.transform.SetAsLastSibling(); }
        }

        public void Toast(string s) { toast.text = s; toastT = 3.5f; }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.layout == null) return;
            PlayerController p = gm.player; GhostAI gh = gm.ghost; DDAManager d = gm.dda;
            if (Input.GetKeyDown(KeyCode.Tab)) showDebug = !showDebug;
            if (Input.GetKeyDown(KeyCode.M)) showMap = !showMap;

            objective.text = "Relik: " + gm.relicsFound + " / " + gm.RelicsNeeded + (gm.relicsFound >= gm.RelicsNeeded ? "   ->  Cari PINTU KELUAR" : "   Kumpulkan relik untuk membuka pintu keluar") + "\nRumah #" + gm.houseNo + "  |  Kesulitan: " + d.Label;
            batteryFill.rectTransform.sizeDelta = new Vector2(294 * p.battery / 100f, 12);
            staminaFill.rectTransform.sizeDelta = new Vector2(294 * p.stamina / 100f, 12);
            prompt.text = p.focusPrompt;
            toastT -= Time.deltaTime; toast.color = new Color(1f, 0.9f, 0.7f, Mathf.Clamp01(toastT));
            string col = gh.state == GhostState.Kejar ? "#ff4030" : gh.state == GhostState.Patroli ? "#9cc4ff" : gh.state == GhostState.Hadang ? "#d080ff" : "#ffb030";
            stateTag.supportRichText = true;
            stateTag.text = "Hantu: <color=" + col + ">" + gh.state.ToString().ToUpper() + "</color>" + (gh.canSee ? "  <color=#ff4030>(MELIHATMU)</color>" : "");
            float dist = Vector3.Distance(gh.transform.position, p.transform.position);
            float danger = Mathf.Clamp01(1f - dist / 10f) * (gh.state == GhostState.Kejar ? 1f : 0.6f);
            vignette.color = new Color(0.45f, 0f, 0f, Mathf.Lerp(vignette.color.a, danger * 0.85f, Time.deltaTime * 4f));
            hideMask.enabled = p.hidden;

            feedTxt.text = string.Join("\n", GhostMemory.feed);

            debugBg.enabled = showDebug; debugTxt.enabled = showDebug;
            if (showDebug) debugTxt.text = DebugText(gm);
            mapFrame.gameObject.SetActive(showMap);
            mapT -= Time.deltaTime;
            if (showMap && mapT <= 0f) { mapT = 0.15f; DrawMap(gm); }

            overlay.SetActive(gm.state != GameManager.State.Playing);
            switch (gm.state)
            {
                case GameManager.State.Title:
                    overlayTitle.text = "HAUNTED HOUSE";
                    overlayBody.text = "<size=40><color=#ffcc88>Hantu yang Mempelajari Ketakutanmu</color></size>\n\nKumpulkan relik, buka pintu keluar, jangan tertangkap.\nHantu mengingat tempat sembunyi dan jalur favoritmu.\n\nWASD jalan  |  Shift lari  |  C jongkok  |  F senter  |  E ambil / sembunyi\nTab panel PCG-DDA-AI  |  M peta  |  G rumah baru  |  F2/F3 DDA -/+  |  F5 bekukan hantu  |  F6 panggil hantu  |  F9 reset memori\n\n<color=#ffffff><size=36>Klik untuk mulai</size></color>";
                    break;
                case GameManager.State.Paused:
                    overlayTitle.text = "JEDA"; overlayBody.text = "Klik untuk lanjut"; break;
                case GameManager.State.Caught:
                    overlayTitle.text = "<color=#ff3020>TERTANGKAP</color>";
                    overlayBody.text = gm.endReason + "\n\nDDA menurunkan kesulitan -> " + d.Label + " (d = " + d.d.ToString("0.00") + ")\nRumah berikutnya: lebih banyak tempat sembunyi & baterai, hantu lebih lambat.\n\nTekan R untuk rumah baru";
                    break;
                case GameManager.State.Won:
                    overlayTitle.text = "<color=#60ff90>LOLOS!</color>";
                    overlayBody.text = gm.endReason + "\n\nDDA menaikkan kesulitan -> " + d.Label + " (d = " + d.d.ToString("0.00") + ")\nRumah berikutnya lebih besar, tempat sembunyi lebih sedikit, hantu lebih peka.\n\nTekan R untuk rumah berikutnya";
                    break;
            }
            overlayTitle.supportRichText = true; overlayBody.supportRichText = true;
        }

        string DebugText(GameManager gm)
        {
            HouseLayout L = gm.layout; PcgParams pp = L.p; DDAManager d = gm.dda; GhostAI g = gm.ghost;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b><color=#88ccff>PCG - Denah Rumah (BSP + Kruskal)</color></b>");
            sb.AppendLine("seed " + pp.seed + "  ukuran " + pp.width + "x" + pp.height + " sel  ruang " + L.rooms.Count + "  pintu " + L.doors.Count + " (loop " + L.loops + ")");
            sb.AppendLine("relik " + L.relics.Count + "  sembunyi " + L.spots.Count + "/" + pp.hidingSpots + "  baterai " + L.batteries.Count + "  perabot " + L.props.Count);
            sb.AppendLine("validasi " + (L.valid ? "<color=#80ff80>LOLOS</color>" : "<color=#ff8080>GAGAL</color>") + "  percobaan " + L.attempts + "  tolak " + L.rejectedPlacements + "  " + L.ms.ToString("0.0") + " ms  hash " + L.hash);
            sb.AppendLine();
            sb.AppendLine("<b><color=#ffcc66>DDA - Kesulitan Dinamis</color></b>");
            sb.AppendLine("d = <b>" + d.d.ToString("0.00") + "</b> (" + d.Label + ")   P = " + d.P.ToString("0.00") + "  target " + DDAManager.Target);
            sb.AppendLine("indikator: tempo " + d.pace.ToString("0.00") + "  aman " + d.safety.ToString("0.00") + "  baterai " + d.resource.ToString("0.00") + "  tenang " + d.calm.ToString("0.00"));
            sb.AppendLine("tertangkap " + d.caughtTotal + "  lolos " + d.winsTotal + "  evaluasi tiap " + DDAManager.Window + " dtk (" + d.evalT.ToString("0") + ")");
            sb.AppendLine("-> AI: patroli " + d.patrolSpeed.ToString("0.0") + "  kejar " + d.chaseSpeed.ToString("0.0") + " m/s  lihat " + d.visionRange.ToString("0") + " m / " + d.fov.ToString("0") + " deg");
            sb.AppendLine("       dengar x" + d.hearingMult.ToString("0.00") + "  cari " + d.searchTime.ToString("0") + " dtk  bobot belajar " + d.learnWeight.ToString("0.00") + "  periksa x" + d.checkMul.ToString("0.00"));
            PcgParams nx = d.MakePcg(0);
            sb.AppendLine("-> PCG berikut: " + nx.width + "x" + nx.height + "  sembunyi " + nx.hidingSpots + "  baterai " + nx.batteries + "  relik " + nx.relicCount + "  | baterai -" + d.batteryDrain.ToString("0.0") + "%/dtk");
            foreach (string s in d.log) sb.AppendLine("<size=16><color=#bbbbbb>" + s + "</color></size>");
            sb.AppendLine();
            sb.AppendLine("<b><color=#ff8877>GAME AI - Hantu (FSM + memori belajar)</color></b>");
            sb.AppendLine("state <b>" + g.state + "</b>  melihat " + (g.canSee ? "YA" : "tidak") + "  jalur " + g.path.Count + " sel");
            sb.AppendLine("<size=17>keputusan: " + g.decision + "</size>");
            sb.AppendLine("belajar: Lemari " + (GhostMemory.KindPref(0) * 100f).ToString("0") + "%  Kolong Ranjang " + (GhostMemory.KindPref(1) * 100f).ToString("0") + "%  (total sembunyi " + GhostMemory.totalHides + ")");
            int shown = 0;
            foreach (HidingSpot s in gm.house.spots)
            {
                float sc = GhostMemory.SpotScore(s);
                if (sc < 0.2f || shown >= 3) continue;
                shown++;
                sb.AppendLine("<size=17>  tempat #" + s.id + " " + HouseGenerator.SpotName((int)s.kind) + " skor " + sc.ToString("0.00") + " -> peluang ketahuan " + (g.CheckChance(s) * 100f).ToString("0") + "%</size>");
            }
            return sb.ToString();
        }

        void DrawMap(GameManager gm)
        {
            HouseLayout L = gm.layout;
            int cs = 20, W = L.w * cs, Hh = L.h * cs;
            if (mapTex == null || mapTex.width != W || mapTex.height != Hh) { mapTex = new Texture2D(W, Hh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; map.texture = mapTex; }
            float maxHeat = 1f;
            if (GhostMemory.heat != null) foreach (float v in GhostMemory.heat) maxHeat = Mathf.Max(maxHeat, v);
            Color32[] px = new Color32[W * Hh];
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    Room r = L.rooms[L.room[x, y]];
                    Color c = Color.HSVToRGB((r.id * 0.13f) % 1f, 0.35f, 0.32f);
                    if (L.blocked[x, y]) c *= 0.45f;
                    float h = GhostMemory.heat != null && x < GhostMemory.heat.GetLength(0) && y < GhostMemory.heat.GetLength(1) ? GhostMemory.heat[x, y] / maxHeat : 0f;
                    c = Color.Lerp(c, new Color(1f, 0.25f, 0.1f), h * 0.75f);
                    c.a = 1f;
                    for (int i = 0; i < cs; i++)
                        for (int j = 0; j < cs; j++)
                        {
                            Color cc = c;
                            if ((i == cs - 1 && L.Wall(new Vector2Int(x, y), 1)) || (i == 0 && L.Wall(new Vector2Int(x, y), 3)) ||
                                (j == cs - 1 && L.Wall(new Vector2Int(x, y), 0)) || (j == 0 && L.Wall(new Vector2Int(x, y), 2))) cc = new Color(0.9f, 0.88f, 0.8f);
                            px[(y * cs + j) * W + x * cs + i] = cc;
                        }
                }
            foreach (HidingSpot s in gm.house.spots) Dot(px, W, s.cell, cs, Color.Lerp(new Color(0.3f, 0.5f, 1f), new Color(1f, 0.1f, 0.9f), GhostMemory.SpotScore(s)), 6);
            foreach (RelicPickup r in gm.house.relics) if (!r.taken) Dot(px, W, G.C(r.transform.position), cs, new Color(1f, 0.85f, 0.2f), 5);
            Dot(px, W, L.exitCell, cs, gm.relicsFound >= gm.RelicsNeeded ? Color.green : new Color(0.2f, 0.5f, 0.2f), 7);
            foreach (Vector2Int c in gm.ghost.path) Dot(px, W, c, cs, new Color(1f, 0.4f, 0.4f), 2);
            Dot(px, W, G.C(gm.ghost.transform.position), cs, Color.red, 7);
            Dot(px, W, G.C(gm.player.transform.position), cs, Color.cyan, 6);
            mapTex.SetPixels32(px); mapTex.Apply();
            float aspect = (float)W / Hh;
            mapFrame.rectTransform.sizeDelta = new Vector2(Mathf.Min(470, 370 * aspect), Mathf.Min(370, 470 / aspect));
        }

        static void Dot(Color32[] px, int W, Vector2Int c, int cs, Color col, int r)
        {
            int cx = c.x * cs + cs / 2, cy = c.y * cs + cs / 2;
            for (int i = -r; i <= r; i++) for (int j = -r; j <= r; j++)
                {
                    if (i * i + j * j > r * r) continue;
                    int x = cx + i, y = cy + j, idx = y * W + x;
                    if (x < 0 || y < 0 || x >= W || idx >= px.Length) continue;
                    px[idx] = col;
                }
        }
    }
}
