using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    public class HUD : MonoBehaviour
    {
        public RectTransform root;
        Text objective, prompt, toast, hint, status, feed, debugTxt, mapTitle, cross;
        Image battFill, stamFill, battBg, stamBg, hideL, hideR, hideTop, edge, debugBg, mapBg;
        RawImage map; Texture2D mapTex;
        float toastT, hintT, mapT;
        public bool present, showDebug, showMap;
        readonly HashSet<string> shownHints = new HashSet<string>();
        string pendingHint;

        public void Build(Transform canvas)
        {
            root = UIKit.Stretch(canvas, "HUD");
            edge = UIKit.Fill(root, "DangerEdge", new Color(0.5f, 0f, 0f, 0f));
            edge.sprite = Sprite.Create(Look.Radial("edge", 0.45f), new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
            // overlay sembunyi: celah pintu lemari / kasur di atas kepala
            hideL = UIKit.Panel(root, "HideL", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900f, 1200f), new Color(0f, 0f, 0f, 0.93f), new Vector2(0f, 0.5f));
            hideR = UIKit.Panel(root, "HideR", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(900f, 1200f), new Color(0f, 0f, 0f, 0.93f), new Vector2(1f, 0.5f));
            hideTop = UIKit.Panel(root, "HideTop", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2400f, 640f), new Color(0.02f, 0.02f, 0.02f, 0.96f), new Vector2(0.5f, 1f));

            objective = UIKit.Label(root, "Objective", "", 27, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(760f, 140f));
            status = UIKit.Label(root, "GhostStatus", "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1000f, 44f));
            toast = UIKit.Label(root, "Toast", "", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(1300f, 50f), UIKit.Gold);
            cross = UIKit.Label(root, "Cross", "·", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(40f, 40f));
            prompt = UIKit.Label(root, "Prompt", "", 27, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(1000f, 44f));
            hint = UIKit.Label(root, "Hint", "", 25, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1300f, 70f), new Color(0.85f, 0.9f, 1f));

            UIKit.Label(root, "BattLbl", "SENTER", 19, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(36f, 74f), new Vector2(200f, 26f), UIKit.Dim, false, new Vector2(0f, 0f));
            battBg = UIKit.Bar(root, "Battery", new Vector2(36f, 62f), 300f, new Color(1f, 0.84f, 0.45f), out battFill);
            UIKit.Label(root, "StamLbl", "STAMINA", 19, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(200f, 26f), UIKit.Dim, false, new Vector2(0f, 0f));
            stamBg = UIKit.Bar(root, "Stamina", new Vector2(36f, 24f), 300f, new Color(0.6f, 0.82f, 1f), out stamFill);

            feed = UIKit.Label(root, "Feed", "", 20, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(820f, 220f), new Color(1f, 0.8f, 0.75f), false, new Vector2(1f, 0f));

            debugBg = UIKit.Panel(root, "DebugBg", new Vector2(0f, 0.5f), new Vector2(20f, -20f), new Vector2(700f, 800f), new Color(0f, 0f, 0f, 0.7f), new Vector2(0f, 0.5f));
            debugTxt = UIKit.Label(debugBg.transform, "Debug", "", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(676f, 780f), UIKit.Ink, false, new Vector2(0f, 1f));
            mapBg = UIKit.Panel(root, "MapBg", new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(470f, 420f), new Color(0f, 0f, 0f, 0.7f), new Vector2(1f, 1f));
            mapTitle = UIKit.Label(mapBg.transform, "MapTitle", "", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(440f, 30f), UIKit.Gold, false, new Vector2(0f, 1f));
            RectTransform mr = UIKit.Rect(mapBg.transform, "Map", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            mr.offsetMin = new Vector2(10f, 10f); mr.offsetMax = new Vector2(-10f, -40f);
            map = mr.gameObject.AddComponent<RawImage>();
            foreach (string id in PlayerPrefs.GetString("HB_Hints", "").Split(',')) if (id.Length > 0) shownHints.Add(id);
        }

        public void Toast(string s) { toast.text = s; toastT = 3.5f; }

        /// <summary>Petunjuk tutorial (sekali per pemain, bisa dimatikan di Pengaturan).</summary>
        public void Hint(string id, string text)
        {
            if (!Settings.Hints || shownHints.Contains(id)) return;
            shownHints.Add(id);
            PlayerPrefs.SetString("HB_Hints", string.Join(",", shownHints));
            hint.text = text; hintT = 6.5f;
        }
        public static void ResetHints() { PlayerPrefs.DeleteKey("HB_Hints"); if (GameManager.I != null && GameManager.I.ui != null) GameManager.I.ui.hud.shownHints.Clear(); }

        public void SetVisible(bool on) { root.gameObject.SetActive(on); }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.layout == null || gm.player == null) return;
            PlayerController p = gm.player; GhostAI gh = gm.ghost; DDAManager d = gm.dda;
            if (gm.Playing)
            {
                if (Input.GetKeyDown(KeyCode.F1)) { present = !present; showDebug = present; showMap = present; Toast(present ? "Mode presentasi: panel PCG/DDA/AI + peta" : "Mode presentasi dimatikan"); }
                if (Input.GetKeyDown(KeyCode.Tab)) showDebug = !showDebug;
                if (Input.GetKeyDown(KeyCode.M)) showMap = !showMap;
            }
            HouseLayout L = gm.layout;
            string keyLine = L.lockedRoom >= 0 ? (p.hasKey ? "   <color=#ff9080>KUNCI: ADA</color>" : (gm.house.lockedDoor != null && !gm.house.lockedDoor.locked ? "   <color=#8a8a8a>gembok terbuka</color>" : "   kunci: -")) : "";
            objective.text = "<b>RELIK " + gm.relicsFound + " / " + gm.RelicsNeeded + "</b>" + keyLine +
                             "\n<size=21><color=#b8b0a0>" + (gm.relicsFound >= gm.RelicsNeeded ? "Semua relik terkumpul - temukan PINTU KELUAR" : "Kumpulkan relik untuk membuka pintu keluar") + "</color></size>" +
                             "\n<size=20><color=#8f887c>" + L.FloorName(p.floor) + "  |  Rumah #" + gm.houseNo + (present ? "  |  DDA " + d.Label : "") + "</color></size>";
            battFill.rectTransform.sizeDelta = new Vector2(296f * p.battery / 100f, battFill.rectTransform.sizeDelta.y);
            battFill.color = p.battery < 20f ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.84f, 0.45f);
            stamFill.rectTransform.sizeDelta = new Vector2(296f * p.stamina / 100f, stamFill.rectTransform.sizeDelta.y);
            stamFill.color = p.exhausted ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.6f, 0.82f, 1f);
            prompt.text = gm.Playing ? p.focusPrompt : "";
            cross.color = p.focus != null ? UIKit.Gold : new Color(1f, 1f, 1f, 0.55f);
            cross.enabled = !p.hidden;
            toastT -= Time.deltaTime; toast.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, Mathf.Clamp01(toastT));
            hintT -= Time.deltaTime; hint.color = new Color(0.85f, 0.9f, 1f, Mathf.Clamp01(hintT));

            float dist = gh != null ? gh.DistanceTo(p.transform.position) : 99f;
            float danger = Mathf.Clamp01(1f - dist / 11f) * (gh != null && gh.state == GhostState.Kejar ? 1f : 0.55f);
            if (gh != null && gh.frozen) danger = 0f;
            edge.color = new Color(0.45f, 0f, 0f, Mathf.Lerp(edge.color.a, gm.Playing ? danger * 0.55f : 0f, Time.deltaTime * 4f));
            if (Atmosphere.I != null) Atmosphere.I.SetDanger(gm.Playing ? danger : 0f);

            bool wardrobe = p.hidden && p.spot != null && p.spot.kind == SpotKind.Lemari;
            bool bed = p.hidden && p.spot != null && p.spot.kind == SpotKind.KolongRanjang;
            hideL.enabled = hideR.enabled = wardrobe;
            if (wardrobe) { float gap = 70f + Mathf.Sin(Time.time * 0.7f) * 6f; hideL.rectTransform.sizeDelta = new Vector2(960f - gap, 1200f); hideR.rectTransform.sizeDelta = new Vector2(960f - gap, 1200f); }
            hideTop.enabled = bed;

            status.text = present && gh != null ? "Hantu: <color=" + StateColor(gh.state) + ">" + gh.state.ToString().ToUpper() + "</color>" + (gh.canSee ? "  <color=#ff4030>(MELIHATMU)</color>" : gh.heardRecently ? "  <color=#ffb030>(MENDENGAR)</color>" : "") + (gh.frozen ? "  <color=#8080ff>[BEKU]</color>" : "") : "";
            feed.text = present ? string.Join("\n", GhostMemory.feed) : "";

            debugBg.gameObject.SetActive(showDebug && gm.Playing);
            if (showDebug && gm.Playing) debugTxt.text = DebugText(gm);
            mapBg.gameObject.SetActive(showMap && gm.Playing);
            mapT -= Time.deltaTime;
            if (showMap && gm.Playing && mapT <= 0f) { mapT = 0.15f; DrawMap(gm); }

            // petunjuk kontekstual
            if (gm.Playing && gm.houseTime > 1.5f)
            {
                Hint("move", "WASD bergerak  |  Shift lari  |  C jongkok  |  F senter  |  E ambil / sembunyi");
                if (gm.houseTime > 9f) Hint("goal", "Kumpulkan semua RELIK (cahaya keemasan), lalu kabur lewat PINTU KELUAR.");
                if (p.focus is RelicPickup) Hint("relic", "Tekan E untuk mengambil relik.");
                if (gh != null && (gh.state == GhostState.Kejar || (gh.canSee && dist < 12f))) Hint("ghost", "HANTU! Lari (Shift), atau sembunyi di lemari / kolong ranjang dengan E.");
                if (p.hidden) Hint("hide", "Hantu MENGINGAT tempat sembunyimu. Tempat yang sama terus dipakai = makin mudah ketahuan.");
                if (p.battery < 30f) Hint("battery", "Senter hampir habis. Cari baterai (cahaya hijau kecil).");
                if (p.focus is LockedDoor) Hint("locked", "Pintu digembok. Cari KUNCI TUA (cahaya merah) di bagian lain rumah.");
                if (L.floors > 1 && L.stairs.Count > 0 && Vector3.Distance(p.transform.position, G.W(L.stairs[0].BottomNode)) < 4f) Hint("stairs", "Tangga ke lantai 2 - relik tersebar di dua lantai.");
                if (gm.relicsFound >= gm.RelicsNeeded) Hint("exit", "Semua relik terkumpul! Lampu di atas pintu keluar kini menyala hangat.");
                if (p.noiseLevel >= PlayerController.NoiseRun) Hint("noise", "Berlari itu berisik - hantu bisa mendengar langkahmu. Jongkok (C) paling senyap.");
            }
        }

        static string StateColor(GhostState s)
        {
            switch (s)
            {
                case GhostState.Kejar: return "#ff4030";
                case GhostState.Hadang: return "#d080ff";
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
            sb.AppendLine("<size=16>evaluasi tiap " + DDAManager.Window + " dtk (" + d.evalT.ToString("0") + ")  batas per rumah +" + DDAManager.MaxRise + " / -" + DDAManager.MaxDrop + "  tertangkap " + d.caughtTotal + "  lolos " + d.winsTotal + "</size>");
            sb.AppendLine("-> hantu: patroli " + d.gp.patrolSpeed.ToString("0.0") + "  kejar " + d.gp.chaseSpeed.ToString("0.0") + " m/s  lihat " + d.gp.visionRange.ToString("0") + " m/" + d.gp.fov.ToString("0") + " deg");
            sb.AppendLine("   dengar x" + d.gp.hearingMult.ToString("0.00") + "  cari " + d.gp.searchTime.ToString("0") + " dtk  belajar " + d.gp.learnWeight.ToString("0.00") + "  periksa x" + d.gp.checkMul.ToString("0.00") + "  eksplorasi " + d.gp.explore.ToString("0.00"));
            PcgParams nx = d.MakePcg(0);
            sb.AppendLine("-> rumah berikut: " + nx.floors + " lantai " + nx.width + "x" + nx.height + "  sembunyi " + nx.hidingSpots + "  baterai " + nx.batteries + "  relik " + nx.relicCount + "  jarak<=" + nx.relicSpotDist);
            foreach (string s in d.log) sb.AppendLine("<size=15><color=#b0b0b0>" + s + "</color></size>");
            sb.AppendLine();
            sb.AppendLine("<b><color=#ff8877>GAME AI</color></b>  FSM + A* + memori Bayesian (Thompson sampling)");
            sb.AppendLine("state <b>" + g.state + "</b>  lihat " + (g.canSee ? "YA" : "-") + "  dengar " + (g.heardRecently ? "YA" : "-") + "  tak bertemu " + g.unseenTime.ToString("0") + " dtk");
            sb.AppendLine("<size=16>" + g.decision + "</size>");
            if (g.searchInfo.Length > 0) sb.AppendLine("<size=15><color=#ffd090>" + g.searchInfo + "</color></size>");
            sb.AppendLine("preferensi: lemari " + (GhostMemory.KindPref(0) * 100f).ToString("0") + "%  kolong " + (GhostMemory.KindPref(1) * 100f).ToString("0") + "%  (sembunyi " + GhostMemory.totalHides + ", diperiksa " + GhostMemory.totalChecks + ", ketemu " + GhostMemory.totalFinds + ")");
            List<HidingSpot> top = new List<HidingSpot>(gm.house.spots);
            top.Sort((a, b) => GhostMemory.Mean(b).CompareTo(GhostMemory.Mean(a)));
            for (int i = 0; i < top.Count && i < 3; i++)
            {
                HidingSpot s = top[i];
                GhostMemory.AlphaBeta(s, out float a, out float b);
                sb.AppendLine("<size=16>  #" + s.id + " " + s.KindName + " Beta(" + a.ToString("0.0") + ", " + b.ToString("0.0") + ")  E=" + GhostMemory.Mean(s).ToString("0.00") + "  dipakai " + GhostMemory.Uses(s) + "x  -> ketahuan " + (g.CheckChance(s) * 100f).ToString("0") + "%</size>");
            }
            return sb.ToString();
        }

        void DrawMap(GameManager gm)
        {
            HouseLayout L = gm.layout;
            int f = gm.player.floor;
            int cs = 22, W = L.w * cs, Hh = L.h * cs;
            if (mapTex == null || mapTex.width != W || mapTex.height != Hh) { mapTex = new Texture2D(W, Hh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; map.texture = mapTex; }
            float maxHeat = 1f;
            if (GhostMemory.heat != null) foreach (float v in GhostMemory.heat) maxHeat = Mathf.Max(maxHeat, v);
            Color32[] px = new Color32[W * Hh];
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    Vector2Int c = new Vector2Int(x, y);
                    Room r = L.rooms[L.room[f, x, y]];
                    Color col = Color.HSVToRGB((r.id * 0.137f) % 1f, 0.3f, r.lit ? 0.33f : 0.2f);
                    if (L.blocked[f, x, y]) col *= 0.5f;
                    if (L.hole[f, x, y]) col = new Color(0.05f, 0.05f, 0.05f);
                    float h = present ? GhostMemory.HeatAt(L, G.N(f, c)) / maxHeat : 0f;
                    col = Color.Lerp(col, new Color(1f, 0.25f, 0.1f), h * 0.75f); col.a = 1f;
                    for (int i = 0; i < cs; i++)
                        for (int j = 0; j < cs; j++)
                        {
                            Color cc = col;
                            int k = i == cs - 1 ? 1 : i == 0 ? 3 : j == cs - 1 ? 0 : j == 0 ? 2 : -1;
                            if (k >= 0 && L.Wall(f, c, k))
                            {
                                bool door = L.IsDoor(f, c, k);
                                if (!door) cc = new Color(0.9f, 0.86f, 0.78f);
                                else if (L.IsLocked(f, c, k) && gm.house.lockedDoor != null && gm.house.lockedDoor.locked) cc = new Color(0.9f, 0.15f, 0.1f);
                            }
                            px[(y * cs + j) * W + x * cs + i] = cc;
                        }
                }
            foreach (Stair s in L.stairs) if (s.f == f || s.f + 1 == f) Dot(px, W, s.Ramp1, cs, new Color(0.6f, 0.6f, 0.6f), 5);
            int gf = G.F(gm.ghost.transform.position.y, L.floors);
            if (present)
            {
                // mode presentasi: tampilkan isi rumah + data belajar hantu
                foreach (HidingSpot s in gm.house.spots) if (s.floor == f) Dot(px, W, s.cell, cs, Color.Lerp(new Color(0.3f, 0.5f, 1f), new Color(1f, 0.1f, 0.9f), GhostMemory.Mean(s)), 6);
                foreach (RelicPickup r in gm.house.relics) if (!r.taken && G.F(r.transform.position.y, L.floors) == f) Dot(px, W, G.C(r.transform.position), cs, new Color(1f, 0.85f, 0.2f), 5);
                foreach (BatteryPickup b in gm.house.batteries) if (!b.taken && G.F(b.transform.position.y, L.floors) == f) Dot(px, W, G.C(b.transform.position), cs, new Color(0.3f, 1f, 0.4f), 3);
                if (gm.house.key != null && !gm.house.key.taken && G.F(gm.house.key.transform.position.y, L.floors) == f) Dot(px, W, G.C(gm.house.key.transform.position), cs, new Color(1f, 0.3f, 0.3f), 5);
                foreach (Vector3 w in gm.ghost.path) if (G.F(w.y, L.floors) == f) Dot(px, W, G.C(w), cs, new Color(1f, 0.4f, 0.4f), 2);
                Dot(px, W, G.C(gm.ghost.transform.position), cs, gf == f ? Color.red : new Color(0.5f, 0.15f, 0.15f), gf == f ? 7 : 4);
            }
            bool exitKnown = present || gm.relicsFound >= gm.RelicsNeeded;
            if (L.exitNode.z == f && exitKnown) Dot(px, W, G.XY(L.exitNode), cs, gm.relicsFound >= gm.RelicsNeeded ? Color.green : new Color(0.2f, 0.45f, 0.2f), 7);
            Dot(px, W, G.C(gm.player.transform.position), cs, Color.cyan, 6);
            mapTex.SetPixels32(px); mapTex.Apply();
            float aspect = (float)W / Hh;
            float mw = Mathf.Min(450f, 370f * aspect), mh = mw / aspect;
            mapBg.rectTransform.sizeDelta = new Vector2(mw + 20f, mh + 50f);
            mapTitle.text = "Peta - " + L.FloorName(f) + (present && gf != f ? "  <size=16>(hantu di " + L.FloorName(gf) + ")</size>" : "");
        }

        static void Dot(Color32[] px, int W, Vector2Int c, int cs, Color col, int r)
        {
            int cx = c.x * cs + cs / 2, cy = c.y * cs + cs / 2;
            for (int i = -r; i <= r; i++) for (int j = -r; j <= r; j++)
                {
                    if (i * i + j * j > r * r) continue;
                    int x = cx + i, y = cy + j, idx = y * W + x;
                    if (x < 0 || y < 0 || x >= W || idx >= px.Length || idx < 0) continue;
                    px[idx] = col;
                }
        }
    }
}
