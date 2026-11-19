using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Uji otomatis di play mode (dipakai dari Editor lewat HarnessLauncher; tidak aktif di build biasa).
    /// kind: shots (screenshot visual), smoke (bot bermain beberapa rumah + cek error), dda (eksperimen DDA vs tetap), chase (uji kejar).
    /// </summary>
    public class TestRunner : MonoBehaviour
    {
        public string kind = "shots";
        GameManager gm;
        readonly StringBuilder log = new StringBuilder();
        readonly List<string> errors = new List<string>();
        RenderTexture rt; Texture2D read;
        const int W = 1280, H = 720;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string stack, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
            {
                if (msg.Contains("[TestRunner]")) return;
                errors.Add(t + ": " + msg + (stack.Length > 0 ? " @ " + stack.Split('\n')[0] : ""));
            }
        }

        IEnumerator Start()
        {
            gm = GameManager.I;
            rt = new RenderTexture(W, H, 24); read = new Texture2D(W, H, TextureFormat.RGB24, false);
            for (int i = 0; i < 10; i++) yield return null;
            string env(string k, string d) { string v = System.Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }
            GhostAI.LogStates = kind == "smoke" || kind == "dda";
            switch (kind)
            {
                case "shots": yield return Shots(); break;
                case "smoke": yield return Smoke(int.Parse(env("HB_HOUSES", "3")), float.Parse(env("HB_SCALE", "3"), CI), env("HB_PROFILE", "Sedang")); break;
                case "dda": yield return DdaExperiment(int.Parse(env("HB_HOUSES", "4")), float.Parse(env("HB_SCALE", "4"), CI)); break;
                case "chase": yield return Chase(); break;
                case "flow": yield return Flow(); break;
                case "hide": yield return HideTest(); break;
            }
            Finish();
        }

        void Finish()
        {
            log.AppendLine();
            log.AppendLine("ERROR/EXCEPTION: " + errors.Count);
            foreach (string e in errors.GetRange(0, Mathf.Min(errors.Count, 30))) log.AppendLine("  " + e);
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/report_" + kind + ".txt", log.ToString());
            Debug.Log("[TestRunner] selesai " + kind + ", error " + errors.Count);
            Time.timeScale = 1f;
            // jangan tinggalkan jejak uji di PlayerPrefs pemain (memori hantu, petunjuk tutorial)
            GhostMemory.Reset(); GhostMemory.feed.Clear();
            PlayerPrefs.DeleteKey("HB_HintsShown"); PlayerPrefs.DeleteKey("HB_GuideSeen"); PlayerPrefs.DeleteKey("HB_TutDone"); PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.SessionState.SetString("HB_TEST_KIND", "");
            UnityEditor.EditorApplication.Exit(errors.Count > 0 ? 3 : 0);
#endif
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        IEnumerator Real(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        // ------------------------------------------------------------------ capture
        void Capture(string name, Camera cam, bool withUI)
        {
            Directory.CreateDirectory("TestOutput/shots");
            Canvas c = gm.ui.canvas;
            RenderMode old = c.renderMode;
            if (withUI) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.06f; Canvas.ForceUpdateCanvases(); }
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(); RenderTexture.active = null;
            File.WriteAllBytes("TestOutput/shots/" + name + ".png", read.EncodeToPNG());
            if (withUI) { c.renderMode = old; c.worldCamera = null; }
            log.AppendLine("shot " + name);
        }

        Camera ShotCam()
        {
            GameObject g = GameObject.Find("ShotCam");
            if (g != null) return g.GetComponent<Camera>();
            g = new GameObject("ShotCam");
            Camera c = g.AddComponent<Camera>(); c.enabled = false; c.fieldOfView = 72f; c.nearClipPlane = 0.05f; c.farClipPlane = 150f; c.clearFlags = CameraClearFlags.Skybox;
            if (Look.URP) { var d = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(c); d.renderPostProcessing = true; }
            GameObject fl = new GameObject("ShotFlash"); fl.transform.SetParent(g.transform, false); fl.transform.localPosition = new Vector3(0.2f, -0.2f, 0.3f);
            Light l = fl.AddComponent<Light>(); l.type = LightType.Spot; l.range = 22f; l.spotAngle = 62f; l.innerSpotAngle = 24f; l.intensity = 16f; l.color = new Color(1f, 0.94f, 0.82f); l.cookie = Look.FlashCookie; l.shadows = LightShadows.Soft;
            return c;
        }

        void Overview(int f, string name)
        {
            HouseLayout L = gm.layout;
            Camera c = ShotCam();
            List<Renderer> hidden = new List<Renderer>();
            foreach (Renderer r in gm.house.GetComponentsInChildren<Renderer>())
                if (r.enabled && (r.gameObject.layer == 8 || r.bounds.min.y > f * G.StoryH + 2.3f)) { r.enabled = false; hidden.Add(r); }
            float cx = L.w * G.T * 0.5f, cz = L.h * G.T * 0.5f;
            Light shotFlash = c.GetComponentInChildren<Light>(); if (shotFlash) shotFlash.enabled = false;
            c.orthographic = true; c.orthographicSize = Mathf.Max(L.h * G.T * 0.55f, L.w * G.T * 0.55f * 9f / 16f);
            c.transform.position = new Vector3(cx, f * G.StoryH + 40f, cz - 6f);
            c.transform.rotation = Quaternion.Euler(80f, 0f, 0f);
            GameObject sun = new GameObject("TestSun"); Light l = sun.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.2f; sun.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            Capture(name, c, false);
            Destroy(sun);
            c.orthographic = false;
            if (shotFlash) shotFlash.enabled = true;
            foreach (Renderer r in hidden) r.enabled = true;
        }

        void View(string name, Vector3 eye, Vector3 look)
        {
            Camera c = ShotCam();
            c.transform.position = eye;
            c.transform.rotation = Quaternion.LookRotation(look - eye);
            Capture(name, c, false);
        }

        // ------------------------------------------------------------------ shots
        IEnumerator Shots()
        {
            gm.ghost.frozen = true;
            HouseLayout L = gm.layout;
            log.AppendLine("rumah seed " + gm.seed + " " + L.w + "x" + L.h + "x" + L.floors + " ruang " + L.rooms.Count + " valid " + L.valid + " jendela " + gm.house.windowCount + " lampu " + Lamp.All.Count);
            yield return Frames(30);
            for (int f = 0; f < L.floors; f++) Overview(f, "00_overview_lantai" + (f + 1));
            yield return Frames(5);
            int n = 0;
            foreach (Room r in L.rooms)
            {
                if (n >= 12) break;
                Vector3 c = new Vector3((r.rect.x + r.rect.width * 0.5f) * G.T, r.floor * G.StoryH + 1.62f, (r.rect.y + r.rect.height * 0.5f) * G.T);
                Vector3Int node = L.NodeAt(c - Vector3.up * 1.6f);
                if (L.Blocked(node)) continue;
                Vector3 eye = G.W(node) + Vector3.up * 1.62f;
                Vector3 dir = r.rect.width >= r.rect.height ? Vector3.right : Vector3.forward;
                View("10_room" + n + "_" + r.type + "_a", eye - dir * 1.2f, eye + dir * 5f + Vector3.down * 0.3f);
                View("10_room" + n + "_" + r.type + "_b", eye + dir * 1.2f, eye - dir * 5f + Vector3.down * 0.3f);
                n++;
                yield return null;
            }
            foreach (Stair s in L.stairs)
            {
                Vector3 b = G.W(s.BottomNode) - G.D3(s.dir) * 1.2f + Vector3.up * 1.62f;
                View("20_stairs_bottom", b, s.RampEnd + Vector3.up * 1.0f);
                Vector3 top = G.W(s.LandingNode) + G.D3(s.dir) * 1.0f + Vector3.up * 1.62f;
                View("21_stairs_top", top, s.RampStart + Vector3.up * 0.5f);
            }
            if (gm.house.lockedDoor != null)
            {
                Transform d = gm.house.lockedDoor.transform;
                View("22_locked_door", d.position + d.forward * 3.2f + Vector3.up * 1.6f, d.position + Vector3.up * 1.1f);
                View("22_locked_door_b", d.position - d.forward * 3.2f + Vector3.up * 1.6f, d.position + Vector3.up * 1.1f);
            }
            if (gm.house.exit != null) { Transform e = gm.house.exit.transform; View("23_exit", e.position + e.forward * 3.4f + Vector3.up * 1.6f, e.position + Vector3.up * 1.2f); }
            foreach (RelicPickup rp in gm.house.relics) { Vector3 p = rp.transform.position; View("24_relic", p + new Vector3(1.6f, 1.5f, 1.2f), p + Vector3.up * 0.8f); break; }
            if (gm.house.key != null) { Vector3 p = gm.house.key.transform.position; View("25_key", p + new Vector3(1.4f, 1.5f, 1.1f), p + Vector3.up * 0.8f); }
            foreach (HidingSpot s in gm.house.spots)
            {
                Vector3 fp = s.exitPos + Vector3.up * 1.62f;
                View("26_spot_" + s.kind + "_" + s.id, fp + (s.exitPos - s.transform.position).normalized * 0.6f, s.transform.position + Vector3.up * 0.9f);
            }
            // pemain bersembunyi
            PlayerController p0 = gm.player;
            HidingSpot war = gm.house.spots.Find(s => s.kind == SpotKind.Lemari), bed = gm.house.spots.Find(s => s.kind == SpotKind.KolongRanjang);
            if (war != null)
            {
                p0.Spawn(war.exitPos, 0f); yield return Frames(3);
                p0.EnterHide(war); yield return Real(1.0f);
                Capture("30_hide_wardrobe", p0.cam, true);
                p0.ExitHide(); yield return Real(0.8f);
            }
            if (bed != null)
            {
                p0.Spawn(bed.exitPos, 0f); yield return Frames(3);
                p0.EnterHide(bed); yield return Real(1.0f);
                Capture("31_hide_bed", p0.cam, true);
                p0.ExitHide(); yield return Real(0.8f);
            }
            // hantu dari dekat
            p0.Spawn(G.W(L.start) + Vector3.up * 0.05f, 0f); yield return Frames(3);
            Vector3 gp = p0.transform.position + p0.transform.forward * 3.2f;
            gm.ghost.Teleport(new Vector3(gp.x, gp.y, gp.z));
            gm.ghost.transform.rotation = Quaternion.LookRotation(-p0.transform.forward);
            gm.ghost.frozen = false; gm.ghost.ForceChase(); yield return Frames(2); gm.ghost.frozen = true;
            yield return Frames(10);
            Capture("40_ghost_close", p0.cam, true);
            gm.ghost.Teleport(G.W(L.ghostStart));
            // HUD mode presentasi
            gm.ui.hud.present = true; gm.ui.hud.showDebug = true;
            yield return Real(0.6f);
            Capture("50_hud_presentation", p0.cam, true);
            gm.ui.hud.present = false; gm.ui.hud.showDebug = false;
            gm.ui.hud.BeginHouse(true, true); yield return Real(0.6f);
            Capture("51_hud_normal", p0.cam, true);
            gm.ui.hud.map.SetFull(true); yield return Real(0.6f);
            Capture("52_map_full", p0.cam, true);
            gm.ui.hud.map.SetFull(false); gm.ui.hud.BeginHouse(false, false); yield return Frames(3);
            // adegan tertangkap (jumpscare) dengan sinematik
            p0.Spawn(G.W(L.start) + Vector3.up * 0.05f, 0f); yield return Frames(3);
            gm.ghost.Teleport(p0.transform.position + p0.transform.forward * 2.5f);
            GameManager.TestMode = false;
            gm.BeginCatch(gm.ghost, "Uji adegan tertangkap");
            yield return Real(0.45f); Capture("45_jumpscare", p0.cam, true);
            yield return Real(1.6f); Capture("46_after_catch", p0.cam, true);
            log.AppendLine("state setelah tertangkap: " + gm.state);
            GameManager.TestMode = true;
            gm.NextHouse(); yield return Frames(10);
            // menu & layar
            gm.ui.ShowMain(); yield return Real(0.5f); Capture("60_menu_main", p0.cam, true);
            gm.ui.ShowSettings(gm.ui.ShowMain);
            for (int tb = 0; tb < 4; tb++) { gm.ui.SelectTab(tb); yield return Real(0.4f); Capture("61_settings_tab" + tb, p0.cam, true); }
            gm.ui.ShowGuide(true, null);
            for (int pg = 0; pg < 4; pg++) { gm.ui.SetGuidePage(pg); yield return Real(0.4f); Capture("62_guide_" + pg, p0.cam, true); }
            gm.ui.ShowGame(); gm.ui.ShowPause(); yield return Real(0.6f); Capture("63_menu_pause", p0.cam, true);
            gm.ui.ShowEnd(false, "Hantu menangkapmu!", new EndInfo { time = 192f, relics = 2, relicsNeed = 3, chases = 3, hides = 2, dFrom = 0.52f, dTo = 0.37f, label = "SEDANG", next = "Rumah berikutnya: 2 lantai  ·  8 tempat sembunyi  ·  3 baterai  ·  3 relik   -   hantu lebih lambat dan kurang peka" });
            yield return Real(0.5f); Capture("64_end_caught", p0.cam, true);
            gm.ui.ShowEnd(true, "Kamu berhasil kabur dari rumah #1.", new EndInfo { time = 245f, relics = 3, relicsNeed = 3, chases = 2, hides = 1, dFrom = 0.4f, dTo = 0.55f, label = "SULIT", next = "Rumah berikutnya: 2 lantai  ·  7 tempat sembunyi  ·  2 baterai  ·  3 relik   -   hantu makin peka dan cepat" });
            yield return Real(0.5f); Capture("65_end_won", p0.cam, true);
            gm.ui.ShowGame();
            gm.ui.ShowIntro("RUMAH #1", new[] { "2 LANTAI", "3 RELIK", "RUANG DIGEMBOK", "KESULITAN SEDANG" }, 4f); yield return Real(0.7f); Capture("66_intro", p0.cam, true);
        }

        // ------------------------------------------------------------------ smoke (bot)
        IEnumerator Smoke(int houses, float scale, string profile)
        {
            GhostMemory.Reset();
            AutoPilot bot = gm.player.gameObject.AddComponent<AutoPilot>();
            bot.profile = (AutoPilot.Profile)System.Enum.Parse(typeof(AutoPilot.Profile), profile);
            log.AppendLine("smoke: " + houses + " rumah, skala waktu " + scale + ", profil " + profile);
            int done = 0, shot = 0; float nextShot = 5f;
            float houseStart = Time.time; int maxFloorErr = 0; float lastMoveT = Time.time; Vector3 lastPos = gm.player.transform.position; float longestIdle = 0f;
            while (done < houses)
            {
                Time.timeScale = scale;
                if (gm.state == GameManager.State.Playing)
                {
                    Vector3 pp = gm.player.transform.position, gp = gm.ghost.transform.position;
                    if (float.IsNaN(pp.x) || float.IsNaN(gp.x)) errors.Add("NaN posisi");
                    if (pp.y < -1.5f || pp.y > gm.layout.floors * G.StoryH + 1f) maxFloorErr++;
                    if ((pp - lastPos).magnitude > 0.5f || gm.player.hidden) { lastPos = pp; lastMoveT = Time.time; }
                    longestIdle = Mathf.Max(longestIdle, Time.time - lastMoveT);
                    if (Time.time > nextShot) { nextShot = Time.time + 25f; Capture("smoke_" + (shot++).ToString("00"), gm.player.cam, true); }
                    if (Time.time - houseStart > 420f) { log.AppendLine("  rumah #" + gm.houseNo + " TIMEOUT (bot " + bot.mode + ", relik " + gm.relicsFound + "/" + gm.RelicsNeeded + ")"); done++; gm.NextHouse(); houseStart = Time.time; }
                }
                else if (gm.state == GameManager.State.Caught || gm.state == GameManager.State.Won)
                {
                    log.AppendLine("  rumah #" + gm.houseNo + " " + (gm.state == GameManager.State.Won ? "LOLOS" : "TERTANGKAP") + " t=" + gm.houseTime.ToString("0") + "s relik " + gm.relicsFound + "/" + gm.RelicsNeeded +
                                   " dikejar " + gm.dda.chasesHouse + " sembunyi " + gm.dda.hidesHouse + " d=" + gm.dda.d.ToString("0.00") + " lantai " + gm.layout.floors + " kunci " + (gm.layout.lockedRoom >= 0) +
                                   " | bot macet " + bot.stuckEvents + " | " + gm.endReason);
                    done++;
                    yield return Frames(3);
                    if (done < houses) { gm.NextHouse(); houseStart = Time.time; }
                }
                yield return null;
            }
            Time.timeScale = 1f;
            log.AppendLine("posisi di luar batas: " + maxFloorErr + " frame, diam terlama " + longestIdle.ToString("0") + " dtk (waktu game)");
        }

        // ------------------------------------------------------------------ eksperimen DDA
        IEnumerator DdaExperiment(int housesPerBlock, float scale)
        {
            AutoPilot bot = gm.player.gameObject.AddComponent<AutoPilot>();
            StringBuilder csv = new StringBuilder("profil,kondisi,rumah,hasil,waktu,relik,relikTotal,dikejar,sembunyi,nyaris,dAwal,dAkhir,lantai,sembunyiTempat,baterai\n");
            Dictionary<string, List<float[]>> agg = new Dictionary<string, List<float[]>>();
            foreach (AutoPilot.Profile prof in new[] { AutoPilot.Profile.Pro, AutoPilot.Profile.Sedang, AutoPilot.Profile.Lemah })
                foreach (bool adaptive in new[] { true, false })
                {
                    string key = prof + "|" + (adaptive ? "DDA" : "Tetap");
                    agg[key] = new List<float[]>();
                    bot.profile = prof;
                    gm.dda.adaptive = adaptive; gm.dda.d = 0.4f; gm.dda.Ps = DDAManager.Target; gm.dda.Apply();
                    GhostMemory.Reset();
                    gm.seed = 5000 + (int)prof * 100;   // seed sama untuk kondisi DDA & tetap -> perbandingan adil
                    gm.houseNo = 0;
                    gm.NextHouse();
                    for (int h = 0; h < housesPerBlock; h++)
                    {
                        float dStart = gm.dda.d; float t0 = Time.time;
                        int floors = gm.layout.floors, spots = gm.layout.spots.Count, batts = gm.layout.batteries.Count;
                        string result = "TIMEOUT";
                        while (true)
                        {
                            Time.timeScale = scale;
                            if (gm.state == GameManager.State.Won) { result = "LOLOS"; break; }
                            if (gm.state == GameManager.State.Caught) { result = "TERTANGKAP"; break; }
                            if (Time.time - t0 > 360f) { result = "TIMEOUT"; break; }
                            yield return null;
                        }
                        float tt = gm.houseTime;
                        float[] row = { result == "LOLOS" ? 1 : 0, tt, gm.relicsFound, gm.RelicsNeeded, gm.dda.chasesHouse, gm.dda.hidesHouse, gm.dda.nearMisses, dStart, gm.dda.d, result == "TIMEOUT" ? 1 : 0 };
                        agg[key].Add(row);
                        csv.AppendLine(prof + "," + (adaptive ? "DDA" : "Tetap") + "," + (h + 1) + "," + result + "," + tt.ToString("0", CI) + "," + gm.relicsFound + "," + gm.RelicsNeeded + "," + gm.dda.chasesHouse + "," + gm.dda.hidesHouse + "," + gm.dda.nearMisses + "," + dStart.ToString("0.00", CI) + "," + gm.dda.d.ToString("0.00", CI) + "," + floors + "," + spots + "," + batts);
                        log.AppendLine(key + " rumah " + (h + 1) + ": " + result + " t=" + tt.ToString("0") + " relik " + gm.relicsFound + "/" + gm.RelicsNeeded + " d " + dStart.ToString("0.00") + "->" + gm.dda.d.ToString("0.00"));
                        if (result == "TIMEOUT" && gm.state == GameManager.State.Playing) gm.dda.OnCaught();   // dianggap gagal
                        yield return Frames(2);
                        gm.NextHouse();
                        yield return Frames(2);
                    }
                }
            Time.timeScale = 1f;
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/dda_experiment.csv", csv.ToString()); File.WriteAllText("Hasil_Eksperimen_DDA.csv", csv.ToString());
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Hasil Eksperimen DDA - Haunted House: Hantu yang Mempelajari Ketakutanmu");
            md.AppendLine();
            md.AppendLine("Dibuat otomatis oleh `Tools > Hantu Belajar > Uji > Eksperimen DDA` (" + housesPerBlock + " rumah per blok, bot AutoPilot memainkan game sungguhan, skala waktu x" + scale + "). Tiap blok mulai dari d = 0,40, memori hantu dikosongkan, dan seed rumah sama untuk kondisi **DDA** maupun **Tetap** (d = 0,40 sepanjang blok).");
            md.AppendLine();
            md.AppendLine("Profil bot: **Pro** (tenang, jongkok saat hantu dekat, memilih tempat sembunyi yang jarang dipakai), **Sedang** (tempat sembunyi terdekat), **Lemah** (sering berlari, reaksi lambat, selalu kembali ke tempat sembunyi favorit).");
            md.AppendLine();
            md.AppendLine("| Profil | Kondisi | Lolos | Waktu rata2 (dtk) | Relik rata2 | Dikejar rata2 | Sembunyi rata2 | d akhir | Timeout |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var kv in agg)
            {
                List<float[]> rows = kv.Value; int n = rows.Count; if (n == 0) continue;
                float win = 0, t = 0, rel = 0, ch = 0, hd = 0, dEnd = 0, to = 0;
                foreach (float[] r in rows) { win += r[0]; t += r[1]; rel += r[2] / Mathf.Max(1f, r[3]); ch += r[4]; hd += r[5]; to += r[9]; }
                dEnd = rows[n - 1][8];
                string[] parts = kv.Key.Split('|');
                md.AppendLine("| " + parts[0] + " | " + parts[1] + " | " + win + "/" + n + " (" + Mathf.RoundToInt(win * 100f / n) + "%) | " + (t / n).ToString("0", CI) + " | " + Mathf.RoundToInt(rel * 100f / n) + "% | " + (ch / n).ToString("0.0", CI) + " | " + (hd / n).ToString("0.0", CI) + " | " + dEnd.ToString("0.00", CI) + " | " + to + " |");
            }
            md.AppendLine();
            md.AppendLine("## Ringkasan");
            foreach (string prof in new[] { "Pro", "Sedang", "Lemah" })
            {
                if (!agg.ContainsKey(prof + "|DDA") || !agg.ContainsKey(prof + "|Tetap")) continue;
                List<float[]> a1 = agg[prof + "|DDA"], a2 = agg[prof + "|Tetap"];
                float w1 = 0, w2 = 0, dm = 0; foreach (float[] r in a1) { w1 += r[0]; dm += r[8]; } foreach (float[] r in a2) w2 += r[0];
                dm /= Mathf.Max(1, a1.Count);
                float dEnd = a1.Count > 0 ? a1[a1.Count - 1][8] : 0.4f;
                md.AppendLine("- **" + prof + "**: dengan DDA kesulitan bergerak dari 0.40 ke " + dEnd.ToString("0.00", CI) + " (rata-rata " + dm.ToString("0.00", CI) + "); lolos " + w1 + "/" + a1.Count + " vs " + w2 + "/" + a2.Count + " tanpa DDA.");
            }
            md.AppendLine();
            md.AppendLine("Interpretasi: setelah tertangkap DDA menurunkan kesulitan (hantu lebih lambat & kurang peka, rumah berikutnya lebih kecil dengan lebih banyak tempat sembunyi & baterai) dan setelah lolos menaikkannya. Dengan " + housesPerBlock + " rumah per blok, selisih tingkat lolos antara kondisi DDA dan Tetap masih dalam variasi acak; tabel ini menunjukkan arah adaptasi kesulitan, belum bukti statistik. Tambah jumlah rumah per blok (HB_HOUSES) dan lakukan playtest manusia untuk laporan final.");
            md.AppendLine();
            md.AppendLine("Data per rumah: `Hasil_Eksperimen_DDA.csv`.");
            File.WriteAllText("Hasil_Eksperimen_DDA.md", md.ToString());
            log.AppendLine(md.ToString());
        }

        // ------------------------------------------------------------------ alur pemain (menu -> main -> jeda -> menang -> menu)
        void Check(string name, bool ok) { log.AppendLine((ok ? "[OK]   " : "[GAGAL] ") + name); if (!ok) errors.Add("cek gagal: " + name); }

        bool Click(string buttonName)
        {
            foreach (UnityEngine.UI.Button b in gm.ui.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                if (b.name == buttonName && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return true; }
            return false;
        }

        IEnumerator Flow()
        {
            GameManager.TestMode = false;
            PlayerPrefs.DeleteKey("HB_GuideSeen"); PlayerPrefs.DeleteKey("HB_TutDone");
            gm.ToMenu(); yield return Real(1.2f);
            Check("menu utama tampil", gm.state == GameManager.State.Menu);
            Capture("flow_00_menu", gm.player.cam, true);
            Check("tombol MULAI BARU ditekan", Click("New")); yield return Real(0.6f);
            Check("panduan bermain muncul saat pertama kali main", gm.ui.GuideOpen && gm.state == GameManager.State.Menu);
            for (int i = 0; i < 4; i++)
            {
                Capture("flow_guide_" + i, gm.player.cam, true);
                Check("panduan halaman " + (i + 1) + ": tombol lanjut", Click("GuideNext")); yield return Real(0.5f);
            }
            yield return Real(1.0f);
            Check("permainan dimulai (rumah #1) setelah panduan", gm.state == GameManager.State.Playing && gm.houseNo == 1 && !gm.ui.GuideOpen);
            Check("panduan awal (checklist) aktif", gm.ui.hud.TutorialActive);
            Check("hantu tertidur & menunggu pemain siap", gm.ghost.Asleep && gm.ghost.holdSleep);
            Capture("flow_01_intro", gm.player.cam, true);
            {
                // langkah dasar panduan awal: bergerak, senter, peta
                HouseLayout L0 = gm.layout;
                int[] d0 = L0.Distances(L0.start, false, false);
                Vector3Int mv = L0.start;
                for (int i = 0; i < d0.Length; i++) if (d0[i] == 2 && !L0.Blocked(L0.FromIdx(i))) { mv = L0.FromIdx(i); break; }
                gm.player.Spawn(G.W(mv) + Vector3.up * 0.05f, 0f); yield return Frames(3);
                gm.player.ToggleFlash(); yield return Frames(3);
                gm.ui.hud.map.ToggleFull(); yield return Real(0.6f);
                Check("peta penuh terbuka (M)", gm.ui.hud.map.fullOpen);
                Capture("flow_map_full", gm.player.cam, true);
                gm.ui.hud.map.ToggleFull(); gm.player.ToggleFlash(); yield return Real(0.5f);
                Check("langkah dasar selesai -> hantu boleh bangun", !gm.ghost.holdSleep);
                Guidance gd = gm.ui.hud.map.guide;
                Check("kompas menunjuk tujuan (" + gd.kind + ", " + gd.distance.ToString("0") + " m, " + gd.path.Count + " titik)", gd.kind != Guidance.Kind.None && gd.path.Count >= 2 && gd.distance > 0.5f);
                Capture("flow_hud_tutorial", gm.player.cam, true);
            }
            gm.Pause(); yield return Real(0.5f);
            Check("jeda: waktu berhenti", gm.state == GameManager.State.Paused && Time.timeScale == 0f);
            Capture("flow_02_pause", gm.player.cam, true);
            Check("tombol PENGATURAN dari jeda", Click("Settings")); yield return Real(0.4f);
            Check("tombol KEMBALI dari pengaturan", Click("Back")); yield return Real(0.4f);
            Check("tombol LANJUTKAN", Click("Resume")); yield return Real(0.5f);
            Check("lanjut bermain", gm.state == GameManager.State.Playing && Time.timeScale == 1f);
            gm.ghost.frozen = true;
            // kunci + gembok (cari rumah yang punya ruang terkunci)
            for (int k = 0; k < 12 && gm.house.lockedDoor == null; k++) { gm.NewHouse(gm.seed + 1); gm.ghost.frozen = true; yield return Frames(3); }
            Check("rumah dengan ruang terkunci tersedia", gm.house.lockedDoor != null && gm.house.key != null);
            if (gm.house.key != null && gm.house.lockedDoor != null)
            {
                gm.house.lockedDoor.Use(gm.player);
                Check("gembok tidak terbuka tanpa kunci", gm.house.lockedDoor.locked);
                gm.house.key.Use(gm.player);
                Check("kunci diambil", gm.player.hasKey);
                gm.house.lockedDoor.Use(gm.player); yield return Real(0.5f);
                Check("gembok terbuka & kunci terpakai", !gm.house.lockedDoor.locked && !gm.player.hasKey && gm.layout.lockedKeys.Count == 0);
            }
            // pintu keluar sebelum relik lengkap -> tetap terkunci
            gm.house.exit.Use(gm.player); yield return Real(0.2f);
            Check("pintu keluar terkunci sebelum relik lengkap", gm.state == GameManager.State.Playing);
            foreach (RelicPickup r in gm.house.relics) r.Use(gm.player);
            Check("semua relik terkumpul", gm.relicsFound == gm.RelicsNeeded);
            BatteryPickup bat = gm.house.batteries.Count > 0 ? gm.house.batteries[0] : null;
            if (bat != null) { gm.player.battery = 20f; bat.Use(gm.player); Check("baterai menambah daya", gm.player.battery > 60f); }
            float dPreWin = gm.dda.d;
            gm.house.exit.Use(gm.player); yield return Real(0.8f);
            Check("menang setelah relik lengkap", gm.state == GameManager.State.Won);
            Check("DDA naik setelah menang (" + dPreWin.ToString("0.00") + " -> " + gm.dda.d.ToString("0.00") + ")", gm.dda.d > dPreWin);
            Capture("flow_03_won", gm.player.cam, true);
            int houseBefore = gm.houseNo;
            Check("tombol RUMAH BERIKUTNYA", Click("Primary")); yield return Real(1.5f);
            Check("rumah berikutnya dibuat", gm.state == GameManager.State.Playing && gm.houseNo == houseBefore + 1);
            gm.ghost.frozen = true;
            Capture("flow_04_next_house", gm.player.cam, true);
            gm.Pause(); yield return Real(0.3f);
            Check("tombol MENU UTAMA dari jeda", Click("Menu")); yield return Real(1.0f);
            Check("kembali ke menu", gm.state == GameManager.State.Menu && Time.timeScale == 1f);
            Check("simpanan progres ada", GameManager.HasSave(out int hs, out float dd) && hs >= 2);
            Check("tombol LANJUTKAN muncul di menu", Click("Continue")); yield return Real(1.5f);
            Check("lanjutkan memulai rumah berikutnya", gm.state == GameManager.State.Playing && gm.houseNo >= 3);
            GameManager.TestMode = true;
            PlayerPrefs.DeleteKey("HB_SaveHouse"); PlayerPrefs.DeleteKey("HB_SaveD"); PlayerPrefs.DeleteKey("HB_SaveSeed");
        }

        // ------------------------------------------------------------------ uji: apakah hantu tahu pemain bersembunyi?
        IEnumerator HideTest()
        {
            HouseLayout L = gm.layout;
            PlayerController p = gm.player; GhostAI g = gm.ghost;
            HidingSpot spot = gm.house.spots.Find(s => s.kind == SpotKind.Lemari) ?? gm.house.spots[0];
            Vector3Int front = spot.frontNode;
            // (b) dikejar, lalu lari ke tempat sembunyi di luar pandangan hantu
            int[] dist = L.Distances(front, false, true);
            Vector3Int startNode = front;
            for (int i = 0; i < dist.Length; i++) if (dist[i] >= 3 && dist[i] <= 5 && !L.Blocked(L.FromIdx(i))) { startNode = L.FromIdx(i); break; }
            p.Spawn(G.W(startNode) + Vector3.up * 0.05f, 0f); yield return Frames(3);
            g.ForceChase(); yield return Frames(2);           // hantu "melihat" pemain di startNode (posisi terakhir terlihat)
            Vector3Int far = L.ghostStart; int fd = -1; int[] dg = L.Distances(front, true, true);
            for (int i = 0; i < dg.Length; i++) if (dg[i] > fd && !L.Blocked(L.FromIdx(i))) { fd = dg[i]; far = L.FromIdx(i); }
            g.Teleport(G.W(far)); g.frozen = true;           // hantu jauh, tidak ada garis pandang
            p.Spawn(spot.exitPos + Vector3.up * 0.05f, 0f); yield return Frames(3);
            p.EnterHide(spot); yield return Real(0.7f);
            g.frozen = false; Time.timeScale = 4f; yield return Real(1.5f); Time.timeScale = 1f; g.frozen = true;
            Check("(a) hantu tidak bisa melihat pemain yang bersembunyi", !g.canSee);
            Check("(b) hantu mencari di posisi terakhir pemain terlihat (bukan di lemari)", (g.state == GhostState.Cari || g.state == GhostState.Periksa) && g.searchCenter == startNode && g.searchCenter != front);
            log.AppendLine("     pusat cari " + g.searchCenter + ", depan lemari " + front + ", posisi terakhir terlihat " + startNode);
            // (d) heatmap tidak bertambah selama bersembunyi
            float before = 0f; foreach (float v in GhostMemory.heat) before += v;
            yield return Real(1.0f);
            float after = 0f; foreach (float v in GhostMemory.heat) after += v;
            Check("(d) heatmap jalur tidak bertambah selama bersembunyi", Mathf.Abs(after - before) < 0.05f);
            p.ExitHide(); yield return Real(0.8f);
            // (c) hantu melihat pemain masuk lemari -> lemari itu diperiksa pertama
            gm.NextHouse(); yield return Frames(5);
            L = gm.layout; p = gm.player; g = gm.ghost;
            spot = gm.house.spots.Find(s => s.kind == SpotKind.Lemari) ?? gm.house.spots[0];
            p.Spawn(spot.exitPos + Vector3.up * 0.05f, 0f); yield return Frames(3);
            Vector3 toward = G.Flat(spot.exitPos - spot.transform.position).normalized;
            g.Teleport(spot.exitPos + toward * 1.4f);
            g.transform.rotation = Quaternion.LookRotation(-toward);
            g.ForceChase(); yield return Frames(3);
            bool sawBefore = g.canSee;
            p.EnterHide(spot); yield return Frames(3);
            Check("hantu melihat pemain sebelum masuk (" + sawBefore + ")", sawBefore);
            Check("(c) walau melihat pemain masuk, hantu tidak langsung menuju lemari itu", g.NextToInspect != spot);
            Time.timeScale = 4f; yield return Real(5f); Time.timeScale = 1f;
            Check("(e) pemain yang bersembunyi tidak tertangkap selama 20 dtk hantu bebas bergerak (state " + g.state + ")", gm.state == GameManager.State.Playing && gm.caughtThisHouse == 0);
            int c0 = g.checksMade;
            g.InspectForTest(spot); Time.timeScale = 4f; yield return Real(5f); Time.timeScale = 1f;
            Check("(f) hantu memeriksa lemari yang ditempati tetapi tidak menemukan pemain", g.checksMade > c0 && gm.state == GameManager.State.Playing && p.hidden);
            // (g) berjaga dekat tempat sembunyi (dari kebiasaan) lalu pergi sendiri - tetap tidak menemukan pemain
            g.GuardForTest(spot); Time.timeScale = 4f; yield return Real(0.6f); Time.timeScale = 1f;
            Check("(g) hantu berjaga dekat tempat sembunyi (state " + g.state + ")", g.state == GhostState.Jaga);
            Time.timeScale = 4f; yield return Real(4.5f); Time.timeScale = 1f;
            Check("(g) setelah waktu jaga habis hantu pergi, pemain tetap aman (state " + g.state + ")", g.state != GhostState.Jaga && gm.state == GameManager.State.Playing && p.hidden);
            p.ForceUnhide();
        }

        // ------------------------------------------------------------------ uji kejar
        IEnumerator Chase()
        {
            int caught = 0, trials = 6;
            for (int i = 0; i < trials; i++)
            {
                gm.player.autopilot = true; gm.player.autoDir = Vector3.zero;
                gm.ghost.SummonAndChase(3);
                float t = 0f; int reversals = 0; Vector3 last = gm.ghost.transform.position, lastDir = Vector3.zero;
                while (t < 15f && gm.state == GameManager.State.Playing)
                {
                    t += Time.deltaTime;
                    Vector3 step = G.Flat(gm.ghost.transform.position - last);
                    if (step.magnitude > 0.01f) { Vector3 dd = step.normalized; if (lastDir != Vector3.zero && Vector3.Dot(dd, lastDir) < -0.5f) reversals++; lastDir = dd; }
                    last = gm.ghost.transform.position;
                    yield return null;
                }
                bool c = gm.state == GameManager.State.Caught;
                if (c) caught++;
                log.AppendLine("percobaan " + (i + 1) + ": " + (c ? "TERTANGKAP dalam " + t.ToString("0.0") + " dtk" : "tidak tertangkap (" + gm.ghost.state + ")") + ", berbalik arah " + reversals + "x");
                gm.NextHouse();
                yield return Frames(3);
            }
            log.AppendLine("HASIL: tertangkap " + caught + "/" + trials);
        }
    }
}
