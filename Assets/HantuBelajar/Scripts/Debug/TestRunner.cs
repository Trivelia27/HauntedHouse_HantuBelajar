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
        readonly StringBuilder trace = new StringBuilder();
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
            GhostAI.LogStates = kind == "smoke" || kind == "calib";
            switch (kind)
            {
                case "shots": yield return Shots(); break;
                case "smoke": yield return Smoke(int.Parse(env("HB_HOUSES", "3")), float.Parse(env("HB_SCALE", "3"), CI), env("HB_PROFILE", "Sedang")); break;
                case "dda": yield return DdaStudy(int.Parse(env("HB_REPS", "4")), int.Parse(env("HB_HOUSES", "8")), float.Parse(env("HB_SCALE", "8"), CI), env("HB_PROFILES", "Pro,Sedang,Lemah"), false); break;
                case "calib": yield return DdaStudy(int.Parse(env("HB_REPS", "2")), int.Parse(env("HB_HOUSES", "6")), float.Parse(env("HB_SCALE", "8"), CI), env("HB_PROFILES", "Pro,Sedang,Lemah"), true); break;
                case "learning": yield return LearningStudy(int.Parse(env("HB_REPS", "10")), int.Parse(env("HB_HOUSES", "6")), float.Parse(env("HB_SCALE", "8"), CI)); break;
                case "reports": RebuildReports(int.Parse(env("HB_HOUSES", "8")), float.Parse(env("HB_SCALE", "16"), CI), int.Parse(env("HB_LEARN_HOUSES", "6"))); break;
                case "playtest": yield return PlaytestSim(int.Parse(env("HB_PARTICIPANTS", "3")), float.Parse(env("HB_SESSION", "1.5"), CI), float.Parse(env("HB_SCALE", "4"), CI)); break;
                case "chase": yield return Chase(); break;
                case "flow": yield return Flow(); break;
                case "hide": yield return HideTest(); break;
                case "stairs": yield return StairsTest(int.Parse(env("HB_HOUSES", "30")), int.Parse(env("HB_RENDER", "3"))); break;
                case "hud": yield return HudTest(); break;
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
        const float HouseLimit = 420f;   // detik permainan per rumah dalam eksperimen; lewat batas = gagal
        static string Env(string k, string d) { string v = System.Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }
        static int RepFrom(int reps) { return Mathf.Clamp(int.Parse(Env("HB_REP_FROM", "1")), 1, reps); }
        static int RepTo(int reps) { return Mathf.Clamp(int.Parse(Env("HB_REP_TO", reps.ToString())), 1, reps); }

        /// <summary>Folder data mentah per potongan; dikosongkan bila eksperimen dijalankan utuh (semua replikasi sekaligus).</summary>
        static string RowsDir(string name, int from, int to, int reps)
        {
            string dir = Path.Combine("TestOutput", name);
            if (from == 1 && to == reps && Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static int CountReps<T>(List<T> rows, System.Func<T, int> rep)
        {
            HashSet<int> s = new HashSet<int>();
            foreach (T x in rows) s.Add(rep(x));
            return s.Count;
        }
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

        // ------------------------------------------------------------------ eksperimen DDA (replikasi + statistik) & kalibrasi bot
        /// <summary>
        /// profil x kondisi (DDA / Tetap) x replikasi x rumah. calib = hanya kondisi Tetap (memeriksa urutan keterampilan bot).
        /// Seed sama untuk DDA & Tetap dalam satu replikasi. Laporan: Hasil_Eksperimen_DDA.md (+ grafik di Analisis/).
        /// </summary>
        IEnumerator DdaStudy(int reps, int houses, float scale, string profileList, bool calib)
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            AutoPilot bot = gm.player.gameObject.AddComponent<AutoPilot>();
            string[] profs = profileList.Split(',');
            bool[] conds = calib ? new[] { false } : new[] { true, false };
            List<DdaRow> rows = new List<DdaRow>();
            int rFrom = RepFrom(reps), rTo = RepTo(reps);
            int total = profs.Length * (rTo - rFrom + 1) * conds.Length * houses, done = 0; float t0 = Time.realtimeSinceStartup;
            log.AppendLine((calib ? "kalibrasi" : "eksperimen DDA") + ": " + profileList + " x " + (calib ? "Tetap" : "DDA/Tetap") + " x replikasi " + rFrom + "-" + rTo + " dari " + reps + " x " + houses + " rumah, skala x" + scale);
            foreach (string pf in profs)
            {
                AutoPilot.Profile prof = (AutoPilot.Profile)System.Enum.Parse(typeof(AutoPilot.Profile), pf.Trim());
                for (int r = rFrom; r <= rTo; r++)
                    foreach (bool adaptive in conds)
                    {
                        bot.profile = prof; bot.habitOn = false; bot.randomHide = false;
                        gm.dda.adaptive = adaptive; gm.dda.d = 0.4f; gm.dda.P = DDAManager.Target; gm.dda.Ps = DDAManager.Target; gm.dda.Apply();
                        GhostMemory.Reset(); GhostMemory.feed.Clear();
                        gm.StartStudyHouse(70000 + r * 1000 + (int)prof * 100);
                        yield return Frames(2);
                        for (int h = 1; h <= houses; h++)
                        {
                            float dStart = gm.dda.d, tStart = Time.time; int seedNow = gm.seed;
                            string result = "timeout";
                            float nextS = 0f; Vector3 lastPP = gm.player.transform.position; float lastPT = Time.time;
                            while (true)
                            {
                                Time.timeScale = scale;
                                if (calib && Time.time >= nextS)
                                {
                                    // jejak pengejaran (diagnosis bot): jarak, kecepatan nyata, lari/jongkok, mode bot
                                    Vector3 pp = gm.player.transform.position; float dtS = Mathf.Max(0.001f, Time.time - lastPT);
                                    if (gm.ghost.state == GhostState.Kejar || gm.state == GameManager.State.Catching)
                                        trace.AppendLine(prof + " seed " + seedNow + " t=" + gm.houseTime.ToString("0.00", CI) + " jarak " + gm.ghost.DistanceTo(pp).ToString("0.0", CI) + " v " + (G.Flat(pp - lastPP).magnitude / dtS).ToString("0.0", CI) +
                                                         (gm.player.running ? " LARI" : "") + (gm.player.crouch ? " JONGKOK" : "") + (gm.player.hidden ? " SEMBUNYI" : "") + " st " + gm.player.stamina.ToString("0", CI) + " mode " + bot.mode +
                                                         " p" + gm.player.node + " h" + gm.layout.NodeAt(gm.ghost.transform.position) + " lihat " + gm.ghost.canSee + " macet " + bot.stuckEvents);
                                    lastPP = pp; lastPT = Time.time; nextS = Time.time + 0.25f;
                                }
                                if (gm.state == GameManager.State.Won) { result = "lolos"; break; }
                                if (gm.state == GameManager.State.Caught) { result = "tertangkap"; break; }
                                if (Time.time - tStart > HouseLimit) break;
                                yield return null;
                            }
                            Time.timeScale = 1f;
                            if (result == "timeout" && gm.state == GameManager.State.Playing) gm.dda.OnCaught();    // lewat batas = gagal
                            rows.Add(new DdaRow
                            {
                                profile = prof.ToString(), cond = adaptive ? "DDA" : "Tetap", rep = r, house = h, seed = seedNow, result = result, time = gm.houseTime,
                                relFrac = gm.relicsFound / (double)Mathf.Max(1, gm.RelicsNeeded), chases = gm.dda.chasesHouse, hides = gm.dda.hidesHouse, near = gm.dda.nearMisses,
                                dStart = dStart, dEnd = gm.dda.d, meanP = gm.dda.nP > 0 ? gm.dda.sumP / gm.dda.nP : double.NaN, nP = gm.dda.nP,
                            });
                            done++;
                            log.AppendLine("  " + prof + (adaptive ? " DDA" : " Tetap") + " r" + r + " rumah " + h + " seed " + seedNow + ": " + result + " t=" + gm.houseTime.ToString("0", CI) + " relik " + gm.relicsFound + "/" + gm.RelicsNeeded +
                                           " dikejar " + gm.dda.chasesHouse + " sembunyi " + gm.dda.hidesHouse + " d " + dStart.ToString("0.00", CI) + "->" + gm.dda.d.ToString("0.00", CI) + (result != "lolos" ? " | mode " + bot.mode + " stamina " + gm.player.stamina.ToString("0", CI) + " hantu " + gm.ghost.state : ""));
                            if (done % 6 == 0 || done == total)
                            {
                                float el = Time.realtimeSinceStartup - t0;
                                Debug.Log("[Eksperimen] " + done + "/" + total + " rumah, " + el.ToString("0") + " dtk, perkiraan sisa " + (el / done * (total - done)).ToString("0") + " dtk");
                            }
                            yield return Frames(2);
                            gm.NextHouse();
                            yield return Frames(2);
                        }
                    }
            }
            Time.timeScale = 1f;
            log.AppendLine();
            foreach (string pf in profs)
                foreach (bool adaptive in conds)
                {
                    List<DdaRow> l = rows.FindAll(x => x.profile == pf.Trim() && x.cond == (adaptive ? "DDA" : "Tetap"));
                    int win = l.FindAll(x => x.result == "lolos").Count, to = l.FindAll(x => x.result == "timeout").Count;
                    double t = 0, ch = 0; foreach (DdaRow x in l) { t += x.time; ch += x.chases; }
                    log.AppendLine(pf.Trim() + " " + (adaptive ? "DDA" : "Tetap") + ": lolos " + win + "/" + l.Count + ", timeout " + to + ", waktu rata2 " + (t / Mathf.Max(1, l.Count)).ToString("0") + " dtk, dikejar rata2 " + (ch / Mathf.Max(1, l.Count)).ToString("0.0", CI) +
                                  (adaptive && l.Count > 0 ? ", d akhir rata2 " + AvgEnd(l, houses).ToString("0.00", CI) : ""));
                }
            log.AppendLine("waktu total " + (Time.realtimeSinceStartup - t0).ToString("0") + " dtk");
            if (calib) { Directory.CreateDirectory("TestOutput"); File.WriteAllText("TestOutput/kalibrasi_bot.txt", log.ToString()); File.WriteAllText("TestOutput/kalibrasi_kejar.txt", trace.ToString()); }
            else
            {
                string dir = RowsDir("dda_rows", rFrom, rTo, reps);
                File.WriteAllText(Path.Combine(dir, profileList.Replace(",", "-") + "_r" + rFrom + "-" + rTo + ".csv"), ResearchReport.DdaCsv(rows));
                List<DdaRow> all = ResearchReport.LoadDdaRows(dir);
                ResearchReport.WriteDda(".", all, CountReps(all, x => x.rep), houses, scale, ResearchReport.DdaBotNote);
                log.AppendLine("laporan: Hasil_Eksperimen_DDA.md (" + all.Count + " rumah dari semua potongan di " + dir + ")");
            }
        }

        /// <summary>Susun ulang kedua laporan eksperimen dari data mentah yang tersimpan (tanpa memainkan ulang).</summary>
        void RebuildReports(int houses, float scale, int learnHouses)
        {
            List<DdaRow> all = ResearchReport.LoadDdaRows(Path.Combine("TestOutput", "dda_rows"));
            if (all.Count > 0) { ResearchReport.WriteDda(".", all, CountReps(all, x => x.rep), houses, scale, ResearchReport.DdaBotNote); log.AppendLine("DDA: " + all.Count + " rumah"); }
            List<LearnHide> h = new List<LearnHide>(); List<LearnDec> e = new List<LearnDec>();
            ResearchReport.LoadLearnRows(Path.Combine("TestOutput", "learn_rows"), h, e);
            if (h.Count + e.Count > 0)
            {
                HashSet<int> reps = new HashSet<int>(); foreach (LearnHide x in h) reps.Add(x.rep); foreach (LearnDec x in e) reps.Add(x.rep);
                ResearchReport.WriteLearning(".", h, e, reps.Count, learnHouses, scale, ResearchReport.LearnD, ResearchReport.LearnConds, ResearchReport.LearnNotes);
                log.AppendLine("Belajar: " + h.Count + " sembunyi, " + e.Count + " keputusan");
            }
        }

        static double AvgEnd(List<DdaRow> l, int houses)
        {
            double s = 0; int n = 0;
            foreach (DdaRow x in l) if (x.house == houses) { s += x.dEnd; n++; }
            return n > 0 ? s / n : double.NaN;
        }

        // ------------------------------------------------------------------ eksperimen "hantu belajar"
        /// <summary>Bot berkebiasaan (85% memilih lemari) vs bot acak, d tetap 0,60; tiap keputusan hantu dibandingkan dengan pilihan acak (LearningProbe).</summary>
        IEnumerator LearningStudy(int reps, int houses, float scale)
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            AutoPilot bot = gm.player.gameObject.AddComponent<AutoPilot>();
            string[] conds = ResearchReport.LearnConds, notes = ResearchReport.LearnNotes;
            const float d = ResearchReport.LearnD;
            List<LearnHide> hides = new List<LearnHide>(); List<LearnDec> decs = new List<LearnDec>();
            int rFrom = RepFrom(reps), rTo = RepTo(reps);
            int total = (rTo - rFrom + 1) * conds.Length, done = 0; float t0 = Time.realtimeSinceStartup;
            for (int r = rFrom; r <= rTo; r++)
                for (int c = 0; c < conds.Length; c++)
                {
                    bot.profile = AutoPilot.Profile.Sedang; bot.habitOn = c == 0; bot.randomHide = c == 1; bot.habitKind = SpotKind.Lemari; bot.habitStrength = 0.85f;
                    gm.dda.adaptive = false; gm.dda.d = d; gm.dda.P = DDAManager.Target; gm.dda.Ps = DDAManager.Target; gm.dda.Apply();
                    GhostMemory.Reset(); GhostMemory.feed.Clear(); LearningProbe.Reset(1000 * r + c);
                    gm.StartStudyHouse(90000 + r * 1000);
                    yield return Frames(2);
                    for (int h = 1; h <= houses; h++)
                    {
                        float tStart = Time.time;
                        while (true)
                        {
                            Time.timeScale = scale;
                            if (gm.state == GameManager.State.Won || gm.state == GameManager.State.Caught) break;
                            if (Time.time - tStart > HouseLimit) break;
                            yield return null;
                        }
                        Time.timeScale = 1f;
                        LearningProbe.EndHouse();
                        yield return Frames(2);
                        gm.NextHouse();
                        yield return Frames(2);
                    }
                    LearningProbe.EndHouse();
                    foreach (LearningProbe.HideRec x in LearningProbe.hides) hides.Add(new LearnHide { cond = conds[c], rep = r, house = x.house, index = x.index, rank = x.rank, cand = x.cand, pModel = x.pModel, pUniform = x.pUniform });
                    foreach (LearningProbe.DecRec x in LearningProbe.decisions) decs.Add(new LearnDec { cond = conds[c], kind = x.kind, rep = r, house = x.house, hitM = x.hitModel, hitR = x.hitRandom });
                    done++;
                    float el = Time.realtimeSinceStartup - t0;
                    Debug.Log("[Eksperimen] belajar " + done + "/" + total + " blok, sembunyi " + LearningProbe.hides.Count + ", keputusan " + LearningProbe.decisions.Count + ", " + el.ToString("0") + " dtk, sisa ~" + (el / done * (total - done)).ToString("0") + " dtk");
                    log.AppendLine(conds[c] + " replikasi " + r + ": sembunyi " + LearningProbe.hides.Count + ", keputusan hadang/jaga " + LearningProbe.decisions.Count);
                }
            Time.timeScale = 1f;
            string dir = RowsDir("learn_rows", rFrom, rTo, reps);
            File.WriteAllText(Path.Combine(dir, "r" + rFrom + "-" + rTo + ".csv"), ResearchReport.LearnCsv(hides, decs));
            List<LearnHide> allH = new List<LearnHide>(); List<LearnDec> allD = new List<LearnDec>();
            ResearchReport.LoadLearnRows(dir, allH, allD);
            HashSet<int> repSet = new HashSet<int>(); foreach (LearnHide x in allH) repSet.Add(x.rep); foreach (LearnDec x in allD) repSet.Add(x.rep);
            ResearchReport.WriteLearning(".", allH, allD, repSet.Count, houses, scale, d, conds, notes);
            log.AppendLine("laporan: Hasil_Eksperimen_Belajar.md (" + allH.Count + " sembunyi, " + allD.Count + " keputusan dari semua potongan); waktu " + (Time.realtimeSinceStartup - t0).ToString("0") + " dtk");
        }

        // ------------------------------------------------------------------ uji alur uji coba pemain (peserta simulasi)
        /// <summary>
        /// Peserta bot menjalani alur uji coba sungguhan lewat tombol UI: pengaturan -> panduan -> latihan -> sesi A -> kuesioner -> sesi B -> kuesioner -> perbandingan.
        /// Jawaban kuesioner acak (berbenih). Data ditulis ke TestOutput/DataUjiCobaSim lalu dianalisis (laporan berlabel SIMULASI).
        /// </summary>
        IEnumerator PlaytestSim(int participants, float sessionMinutes, float scale)
        {
            string simRoot = Path.Combine("TestOutput", "DataUjiCobaSim");
            if (Directory.Exists(simRoot)) Directory.Delete(simRoot, true);
            Playtest.RootOverride = Path.GetFullPath(simRoot);
            PlayerPrefs.SetInt("HB_SaveHouse", 7); PlayerPrefs.SetString("HB_HintsShown", "goal,map"); PlayerPrefs.Save();
            string memBefore = PlayerPrefs.GetString("HB_GhostMemory2", "");
            AutoPilot bot = gm.player.gameObject.AddComponent<AutoPilot>(); bot.profile = AutoPilot.Profile.Sedang;
            System.Random ans = new System.Random(77);
            Directory.CreateDirectory("TestOutput/shots/ujicoba");
            for (int k = 1; k <= participants; k++)
            {
                gm.BackToMenu(); yield return Real(0.4f);
                Check("P" + k + ": tombol UJI COBA PEMAIN ada di menu utama", Click("Playtest")); yield return Real(0.4f);
                if (k == 1) Capture("ujicoba/u1_pengaturan", gm.player.cam, true);
                bool startOk = Click("PtStart"); yield return Real(0.4f);
                Check("P" + k + ": mulai uji coba (pencatatan aktif di folder P" + k.ToString("00") + ")", startOk && Telemetry.On && Telemetry.Participant == "P" + k.ToString("00"));
                Playtest.I.sessionMinutes = sessionMinutes; Playtest.I.practiceMinutes = Mathf.Max(0.5f, sessionMinutes * 0.5f);
                Check("P" + k + ": panduan bermain muncul dulu", gm.ui.GuideOpen);
                Click("GuideSkip");
                for (float w = 0f; w < 4f && !gm.Playing; w += 0.1f) yield return Real(0.1f);
                Check("P" + k + ": latihan berjalan dengan panduan awal", Playtest.I.phase == Playtest.Phase.Latihan && gm.Playing && gm.ui.hud.TutorialActive);
                // latihan, sesi A, sesi B (bot bermain; layar antar sesi diklik)
                int qDone = 0;
                float limit = Time.realtimeSinceStartup + 600f;
                while (Playtest.Active && Time.realtimeSinceStartup < limit)
                {
                    Time.timeScale = gm.Playing ? scale : 1f;
                    if (gm.state == GameManager.State.Won || gm.state == GameManager.State.Caught)
                    {
                        if (k == 1 && qDone == 0 && Playtest.I.phase == Playtest.Phase.SesiA) { Time.timeScale = 1f; yield return Real(0.4f); Capture("ujicoba/u2_akhir_rumah", gm.player.cam, true); }
                        Time.timeScale = 1f; yield return Real(0.2f);
                        Click("Primary");
                    }
                    else if (gm.ui.playtest.AnyOpen)
                    {
                        Time.timeScale = 1f;
                        yield return Real(0.3f);
                        if (Find("PtQuestNext"))
                        {
                            if (k == 1 && qDone == 0) Capture("ujicoba/u3_kuesioner_kosong", gm.player.cam, true);
                            for (int i = 0; i < 8; i++) gm.ui.playtest.AnswerQ(i, 1 + ans.Next(5));
                            yield return Real(0.2f);
                            if (k == 1 && qDone == 0) Capture("ujicoba/u4_kuesioner_terisi", gm.player.cam, true);
                            Click("PtQuestNext"); qDone++;
                        }
                        else if (Find("PtCompareNext"))
                        {
                            for (int i = 0; i < 3; i++) gm.ui.playtest.AnswerC(i, ans.Next(3));
                            yield return Real(0.2f);
                            if (k == 1) Capture("ujicoba/u5_perbandingan", gm.player.cam, true);
                            Click("PtCompareNext");
                        }
                        else if (Find("PtInfoNext"))
                        {
                            if (k == 1) Capture("ujicoba/u6_jeda_" + qDone, gm.player.cam, true);
                            Click("PtInfoNext");
                        }
                    }
                    yield return null;
                }
                Time.timeScale = 1f;
                yield return Real(0.4f);
                if (k == 1) Capture("ujicoba/u7_selesai", gm.player.cam, true);
                string dir = Path.Combine(simRoot, "P" + k.ToString("00"));
                Check("P" + k + ": alur selesai (kuesioner " + qDone + "x)", !Playtest.Active && qDone == 2 && Find("PtDoneMenu"));
                Check("P" + k + ": rumah.csv berisi sesi A & B", File.Exists(Path.Combine(dir, "rumah.csv")) && File.ReadAllText(Path.Combine(dir, "rumah.csv")).Contains("SESI A") && File.ReadAllText(Path.Combine(dir, "rumah.csv")).Contains("SESI B"));
                Check("P" + k + ": kuesioner.csv 2 baris + perbandingan.csv 1 baris", File.Exists(Path.Combine(dir, "kuesioner.csv")) && File.ReadAllLines(Path.Combine(dir, "kuesioner.csv")).Length == 3 &&
                      File.Exists(Path.Combine(dir, "perbandingan.csv")) && File.ReadAllLines(Path.Combine(dir, "perbandingan.csv")).Length == 2);
                Check("P" + k + ": urutan diseimbangkan (" + (k % 2 == 1 ? "DDA dulu" : "Tetap dulu") + ")", File.Exists(Path.Combine(dir, "info.txt")) && File.ReadAllText(Path.Combine(dir, "info.txt")).Contains(k % 2 == 1 ? "A=DDA" : "A=Tetap"));
                Check("P" + k + ": simpanan & memori pemilik game dikembalikan", PlayerPrefs.GetInt("HB_SaveHouse", 0) == 7 && PlayerPrefs.GetString("HB_GhostMemory2", "") == memBefore && PlayerPrefs.GetString("HB_HintsShown", "") == "goal,map");
                Click("PtDoneMenu"); yield return Real(0.3f);
            }
            string md = ResearchReport.AnalyzePlaytest(new[] { simRoot }, "TestOutput", "Hasil_Uji_Coba_SIMULASI.md", "sim_", true);
            Check("analisis: laporan uji coba tersusun untuk " + participants + " peserta", md.Contains("Peserta lengkap: **" + participants + "**") && File.Exists("TestOutput/Analisis/sim_kuesioner.svg"));
            Playtest.RootOverride = null;
            PlayerPrefs.DeleteKey("HB_SaveHouse"); PlayerPrefs.DeleteKey("HB_HintsShown"); PlayerPrefs.Save();
        }

        bool Find(string buttonName)
        {
            foreach (UnityEngine.UI.Button b in gm.ui.GetComponentsInChildren<UnityEngine.UI.Button>(false))
                if (b.name == buttonName && b.gameObject.activeInHierarchy) return true;
            return false;
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

        // ------------------------------------------------------------------ uji tangga: tidak ada benda yang menutupi tangga / ditembus kamera
        IEnumerator StairsTest(int houses, int renderHouses)
        {
            Directory.CreateDirectory("TestOutput/shots/stairs");
            WallProfile();
            int[] fixedSeeds = { 42019, 1234, 5678, 9012, 31337 };
            int stairsChecked = 0, intrusions = 0, camHits = 0, walked = 0, walkHouses = 10;
            List<string> walkFails = new List<string>();
            Dictionary<string, int> what = new Dictionary<string, int>();
            for (int i = 0; i < houses; i++)
            {
                string only = System.Environment.GetEnvironmentVariable("HB_SEEDS");
                int[] list = string.IsNullOrEmpty(only) ? null : System.Array.ConvertAll(only.Split(','), int.Parse);
                if (list != null && i >= list.Length) break;
                int seed = list != null ? list[i] : i < fixedSeeds.Length ? fixedSeeds[i] : 20000 + i * 7919;
                gm.NewHouse(seed); gm.ghost.frozen = true;
                yield return Frames(3);
                HouseLayout L = gm.layout;
                foreach (Stair s in L.stairs)
                {
                    stairsChecked++;
                    bool[] walls = StairWalls(L, s);
                    List<string> hits = StairIntrusions(L, s);
                    List<string> cam = StairCameraPath(L, s);
                    intrusions += hits.Count; camHits += cam.Count;
                    foreach (string h in hits) { string k = h.Split(' ')[0]; what[k] = (what.TryGetValue(k, out int c) ? c : 0) + 1; }
                    log.AppendLine("seed " + seed + " tangga " + s.bottom + " arah " + s.dir + " | dinding L1 kiri/kanan " + walls[0] + "/" + walls[1] + ", L2 kiri/kanan/belakang " + walls[2] + "/" + walls[3] + "/" + walls[4] +
                                   " | benda di lubang tangga: " + (hits.Count == 0 ? "-" : string.Join(", ", hits)) + " | kamera menembus: " + (cam.Count == 0 ? "-" : string.Join(", ", cam)));
                    if (i < renderHouses) yield return StairViews(L, s, "stairs/s" + i);
                    if (i < walkHouses) { walked++; yield return WalkStairs(s, walkFails); }
                }
            }
            log.AppendLine();
            foreach (var kv in what) log.AppendLine("  jenis benda pengganggu: " + kv.Key + " x" + kv.Value);
            foreach (string w in walkFails) log.AppendLine("  " + w);
            Check("tidak ada benda di ruang lubang tangga (" + stairsChecked + " tangga, " + houses + " rumah)", intrusions == 0);
            Check("kamera pemain tidak menembus benda saat naik tangga", camHits == 0);
            Check("pemain bisa naik & turun tangga di lajur tengah, kiri, kanan (" + walked + " tangga)", walkFails.Count == 0);
        }

        /// <summary>Pemain (autopilot) berjalan bawah -> pendaratan -> bawah di tiga lajur; gagal bila tidak sampai dalam 12 dtk.</summary>
        IEnumerator WalkStairs(Stair s, List<string> fails)
        {
            PlayerController p = gm.player;
            Vector3 d = G.D3(s.dir), side = new Vector3(d.z, 0f, -d.x);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Time.timeScale = 2f;
            foreach (float lane in new[] { 0f, -0.9f, 0.9f })
            {
                Vector3 off = side * lane;
                p.Spawn(G.W(s.BottomNode) + off + Vector3.up * 0.05f, yaw);
                yield return Frames(2);
                float tUp = -1f, tDown = -1f;
                yield return WalkRoute(p, new[] { s.RampStart + off, s.RampEnd + off, G.W(s.LandingNode) + off }, 12f, t => tUp = t);
                if (tUp < 0f || p.transform.position.y < s.RampEnd.y - 0.25f) fails.Add("tangga " + s.bottom + ": naik lajur " + lane + " gagal, berhenti di " + p.transform.position);
                else
                {
                    yield return WalkRoute(p, new[] { s.RampEnd + off, s.RampStart + off, G.W(s.BottomNode) + off }, 12f, t => tDown = t);
                    if (tDown < 0f || p.transform.position.y > s.RampStart.y + 0.3f) fails.Add("tangga " + s.bottom + ": turun lajur " + lane + " gagal, berhenti di " + p.transform.position);
                }
                log.AppendLine("     jalan lajur " + lane.ToString("+0.0;-0.0;0.0", CI) + ": naik " + (tUp < 0f ? "GAGAL" : tUp.ToString("0.0", CI) + " dtk") + ", turun " + (tDown < 0f ? "GAGAL" : tDown.ToString("0.0", CI) + " dtk"));
            }
            p.autopilot = false; p.autoDir = Vector3.zero;
            Time.timeScale = 1f;
        }

        IEnumerator WalkRoute(PlayerController p, Vector3[] pts, float timeout, System.Action<float> done)
        {
            p.autopilot = true; p.autoRun = false; p.autoLook = null;
            float t = 0f; int i = 0;
            while (t < timeout)
            {
                Vector3 dd = pts[i] - p.transform.position;
                if (G.Flat(dd).magnitude < 0.35f && Mathf.Abs(dd.y) < 1.4f)
                {
                    i++;
                    if (i >= pts.Length) { p.autoDir = Vector3.zero; done(t); yield break; }
                    continue;
                }
                p.autoDir = G.Flat(dd).normalized;
                t += Time.deltaTime;
                yield return null;
            }
            p.autoDir = Vector3.zero;
        }

        /// <summary>[0]=dinding L1 kiri, [1]=L1 kanan, [2]=L2 kiri, [3]=L2 kanan, [4]=L2 belakang (kiri = arah -side).</summary>
        static bool[] StairWalls(HouseLayout L, Stair s)
        {
            int left = (s.dir + 3) & 3, right = (s.dir + 1) & 3, back = G.Opp(s.dir);
            return new[] { L.Wall(s.f, s.Ramp1, left), L.Wall(s.f, s.Ramp1, right), L.Wall(s.f + 1, s.Ramp1, left), L.Wall(s.f + 1, s.Ramp1, right), L.Wall(s.f + 1, s.Ramp1, back) };
        }

        static string TopName(Transform t, Transform root)
        {
            while (t.parent != null && t.parent != root) t = t.parent;
            return t.name;
        }

        static bool Structural(string n)
        {
            return n == "Stairs" || n == "Floor" || n == "Ceiling" || n.StartsWith("Wall") || n == "Post" || n == "Header" || n == "HeaderTrim";
        }

        /// <summary>Benda (selain struktur) yang berada di ruang udara di atas anak tangga sampai setinggi orang + kepala.</summary>
        List<string> StairIntrusions(HouseLayout L, Stair s)
        {
            List<string> res = new List<string>();
            Vector3 o = s.RampStart, d = G.D3(s.dir), side = new Vector3(d.z, 0f, -d.x);
            float cap = (s.f + 1) * G.StoryH + 2.4f;
            Transform root = gm.house.transform;
            foreach (Renderer r in gm.house.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                string top = TopName(r.transform, root);
                // bagian tangga sendiri (pegangan, tiang, balok tepi, pagar) tidak boleh masuk lajur jalan (+-1,30 m)
                bool stairPart = top == "Stairs";
                if (stairPart) { if (r.name == "Step" || r.name == "Nosing") continue; }
                else if (Structural(top)) continue;
                float latMax = stairPart ? 1.30f : 1.45f;
                Bounds b = r.bounds;
                float a0 = float.MaxValue, a1 = float.MinValue, l0 = float.MaxValue, l1 = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) - o;
                    float a = Vector3.Dot(p, d), l = Vector3.Dot(p, side);
                    a0 = Mathf.Min(a0, a); a1 = Mathf.Max(a1, a); l0 = Mathf.Min(l0, l); l1 = Mathf.Max(l1, l);
                }
                float A0 = Mathf.Max(a0, 0.32f), A1 = Mathf.Min(a1, G.T - 0.05f);
                if (A0 > A1 || l1 < -latMax || l0 > latMax) continue;
                float y0 = b.min.y - o.y, y1 = b.max.y - o.y;
                float s0 = A0 / G.T * G.StoryH, s1 = A1 / G.T * G.StoryH;
                if (s0 > y1 - 0.03f || s1 < y0 - 2.4f || y0 + o.y > cap) continue;
                res.Add(top + "(" + r.name + " y" + b.min.y.ToString("0.00", CI) + "-" + b.max.y.ToString("0.00", CI) + " sel " + G.C(r.transform.position) + " a" + ((a0 + a1) * 0.5f).ToString("0.00", CI) + " l" + ((l0 + l1) * 0.5f).ToString("0.00", CI) + ")");
            }
            return res;
        }

        /// <summary>Titik kamera pemain di sepanjang tangga (3 lajur) yang berada di dalam batas renderer mana pun.</summary>
        List<string> StairCameraPath(HouseLayout L, Stair s)
        {
            HashSet<string> res = new HashSet<string>();
            Vector3 o = s.RampStart, d = G.D3(s.dir), side = new Vector3(d.z, 0f, -d.x);
            Transform root = gm.house.transform;
            Renderer[] rs = gm.house.GetComponentsInChildren<Renderer>();
            for (int li = -1; li <= 1; li++)
                for (int k = 0; k <= 25; k++)
                {
                    float a = k / 25f * G.T;
                    Vector3 eye = o + d * a + side * (li * 1.0f) + Vector3.up * (a / G.T * G.StoryH + 1.62f);
                    foreach (Renderer r in rs)
                    {
                        if (!r.enabled || r is ParticleSystemRenderer) continue;
                        Bounds b = r.bounds; b.Expand(0.12f);
                        if (b.Contains(eye)) res.Add(TopName(r.transform, root) + "/" + r.name + "@" + (a / G.T).ToString("0.00", CI));
                    }
                }
            return new List<string>(res);
        }

        IEnumerator StairViews(HouseLayout L, Stair s, string prefix)
        {
            Vector3 o = s.RampStart, d = G.D3(s.dir), side = new Vector3(d.z, 0f, -d.x);
            float Surf(float a) { return a / G.T * G.StoryH; }
            View(prefix + "_a_bawah", G.W(s.BottomNode) - d * 1.2f + Vector3.up * 1.62f, s.RampEnd + Vector3.up * 1.0f);
            View(prefix + "_a_bawah_dekat", G.W(s.BottomNode) + Vector3.up * 1.62f, s.RampEnd + Vector3.up * 1.4f);
            foreach (float t in new[] { 0.15f, 0.45f, 0.75f })
            {
                Vector3 eye = o + d * (t * G.T) + Vector3.up * (Surf(t * G.T) + 1.62f);
                View(prefix + "_b_naik" + Mathf.RoundToInt(t * 100), eye, eye + d * 2f + Vector3.up * 0.8f);
            }
            Vector3 mid = o + d * (0.6f * G.T) + Vector3.up * (Surf(0.6f * G.T) + 1.62f);
            View(prefix + "_c_turun", mid, mid - d * 3f + Vector3.down * 1.7f);
            View(prefix + "_d_atas", G.W(s.LandingNode) + Vector3.up * 1.62f, o + d * 1.2f + Vector3.up * 0.3f);
            View(prefix + "_d_atas_tepi", G.W(s.LandingNode) - d * 1.0f + side * 0.6f + Vector3.up * 1.62f, o + d * 0.6f + Vector3.up * 0.0f);
            // potongan samping (ortografis, bidang potong = garis tengah tangga) + denah lantai atas
            Camera c = ShotCam();
            Light flash = c.GetComponentInChildren<Light>(); if (flash) flash.enabled = false;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            GameObject sun = new GameObject("TestSun"); Light sl = sun.AddComponent<Light>(); sl.type = LightType.Directional; sl.intensity = 1.1f;
            Vector3 center = o + d * (G.T * 0.5f) + Vector3.up * 2.7f;
            c.orthographic = true; c.orthographicSize = 3.9f; c.nearClipPlane = 20f; c.farClipPlane = 60f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                c.transform.position = center + side * (20f * sd) - d * 0.0f;
                c.transform.rotation = Quaternion.LookRotation(-side * sd);
                sun.transform.rotation = Quaternion.LookRotation((-side * sd + Vector3.down * 0.6f + d * 0.3f).normalized);
                Capture(prefix + "_e_potong" + (sd < 0 ? "_kiri" : "_kanan"), c, false);
            }
            // denah: sembunyikan plafon & semua yang di atas lantai atas + 2,3 m
            List<Renderer> hidden = new List<Renderer>();
            float lim = (s.f + 1) * G.StoryH + 2.3f;
            foreach (Renderer r in gm.house.GetComponentsInChildren<Renderer>())
                if (r.enabled && (r.gameObject.layer == 8 || r.bounds.min.y > lim)) { r.enabled = false; hidden.Add(r); }
            c.orthographicSize = 5.2f; c.nearClipPlane = 0.3f;
            c.transform.position = G.W(s.f + 1, s.Ramp1) + Vector3.up * 30f; c.transform.rotation = Quaternion.LookRotation(Vector3.down, d);
            sun.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
            Capture(prefix + "_f_denah", c, false);
            foreach (Renderer r in hidden) r.enabled = true;
            Destroy(sun);
            c.orthographic = false; c.nearClipPlane = 0.05f; c.farClipPlane = 150f;
            RenderSettings.fog = fog;
            if (flash) flash.enabled = true;
            yield return null;
        }

        /// <summary>Profil kedalaman modul dinding paket (z lokal per ketinggian) - untuk menyelaraskan list tangga dengan muka dinding.</summary>
        void WallProfile()
        {
            // mesh paket tidak bisa dibaca -> ukur dengan raycast ke MeshCollider sementara (hanya di editor)
            foreach (string key in new[] { "wall", "window" })
            {
                GameObject[] set = AssetLibrary.Models(key);
                if (set.Length == 0) continue;
                GameObject pf = set[0];
                GameObject g = Instantiate(pf, new Vector3(0f, -300f, 0f), Quaternion.identity);
                foreach (Collider c0 in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c0);
                foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true)) { MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
                Physics.SyncTransforms();
                Bounds rb = Kit.RawBounds(pf);
                StringBuilder sb = new StringBuilder("profil " + pf.name + " (z lokal tak berskala; muka depan = +z, x " + rb.min.x.ToString("0.00", CI) + ".." + rb.max.x.ToString("0.00", CI) + "): ");
                for (float y = 0.025f; y < rb.max.y; y += 0.05f)
                {
                    float front = float.MinValue, back = float.MaxValue;
                    foreach (float fx in new[] { 0.2f, 0.5f, 0.8f })
                    {
                        float x = Mathf.Lerp(rb.min.x, rb.max.x, fx);
                        Vector3 o = new Vector3(x, -300f + y, 2f);
                        if (Physics.Raycast(o, Vector3.back, out RaycastHit h, 4f)) front = Mathf.Max(front, h.point.z);
                        o.z = -2f;
                        if (Physics.Raycast(o, Vector3.forward, out RaycastHit h2, 4f)) back = Mathf.Min(back, h2.point.z);
                    }
                    sb.Append(" [" + y.ToString("0.00", CI) + "] " + (back < float.MaxValue ? back.ToString("0.000", CI) : "-") + ".." + (front > float.MinValue ? front.ToString("0.000", CI) : "-"));
                }
                log.AppendLine(sb.ToString());
                DestroyImmediate(g);
            }
        }

        // ------------------------------------------------------------------ uji: informasi HUD rapi (tidak berulang, tidak saling menimpa / menutupi)
        IEnumerator HudTest()
        {
            HUD hud = gm.ui.hud; PlayerController p = gm.player; GhostAI g = gm.ghost; HouseLayout L = gm.layout;
            Directory.CreateDirectory("TestOutput/shots/hud");
            g.frozen = true;
            p.Spawn(G.W(L.start) + Vector3.up * 0.05f, 0f); yield return Frames(3);

            // (1) judul rumah tampil -> notifikasi & petunjuk ditahan, lalu muncul setelah judul memudar
            HUD.ResetHints();
            hud.BeginHouse(false, false);
            gm.ui.ShowIntro("RUMAH #1", new[] { "2 LANTAI", "3 RELIK", "RUANG DIGEMBOK" }, 4f);
            hud.Toast("Kamu merasa ada yang terbangun di rumah ini...", null, "ghost", UIKit.Danger);
            hud.Hint("uji_awal", "Petunjuk uji: muncul setelah judul rumah hilang.");
            yield return Real(1.5f);
            Check("(1) selama judul rumah tampil notifikasi & petunjuk ditahan", !hud.ToastShown && hud.HintNow == "");
            Capture("hud/h1_judul", p.cam, true);
            bool sawToast = false, sawHint = false, shot1 = false; float tEnd = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < tEnd && !(sawToast && sawHint && shot1))
            {
                if (hud.ToastShown) sawToast = true;
                if (hud.HintNow != "") sawHint = true;
                if (!shot1 && hud.ToastShown && hud.HintNow != "") { shot1 = true; Capture("hud/h1_setelah_judul", p.cam, true); }
                yield return null;
            }
            Check("(1) setelah judul hilang notifikasi & petunjuk muncul", sawToast && sawHint);
            log.AppendLine("     kanvas uji (batch) " + hud.root.rect.size + " - cek tumpang-tindih memakai layar virtual 16:9 (1920x1080) dan 16:10 (1821x1138)");
            hud.BeginHouse(false, false); yield return Real(0.3f);

            // (2) hantu mendengar langkah berkali-kali (node berbeda) -> satu indikator stabil, tanpa notifikasi berulang;
            //     relik + "semua relik" berurutan -> pesan relik tidak terpotong
            HidingSpot spot = gm.house.spots.Find(s => s.kind == SpotKind.Lemari) ?? gm.house.spots[0];
            Vector3Int gn = L.ghostStart;
            List<Vector3Int> nbs = new List<Vector3Int>(); L.Neighbors(gn, true, true, nbs);
            g.Teleport(G.W(gn)); g.WakeForTest(); g.frozen = false;
            float minA = 1f, relicShown = -1f, relicGone = -1f, t0 = Time.realtimeSinceStartup; int hears = 0; bool toastHear = false;
            gm.Toast("Relik 3 / 3  ·  Jam Kakek", "Jam saku yang berhenti tepat pukul 03.07.", "relic");
            gm.Toast("Semua relik terkumpul!", "Pintu keluar terbuka - ikuti kompas ke lantai 1", "exit", UIKit.Good);
            bool shot2 = false;
            while (Time.realtimeSinceStartup - t0 < 4.2f)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (hears < 7 && t > hears * 0.5f) { Vector3Int n = hears % 2 == 0 || nbs.Count == 0 ? gn : nbs[hears % nbs.Count]; g.HearNoise(G.W(n), 30f); hears++; }
                if (t > 0.5f) minA = Mathf.Min(minA, hud.AlertAlpha);
                if (hud.ToastQueuedContains("mendengar")) toastHear = true;
                if (relicShown < 0f && hud.ToastNow.StartsWith("Relik 3")) relicShown = t;
                if (relicShown >= 0f && relicGone < 0f && !hud.ToastNow.StartsWith("Relik 3")) relicGone = t;
                if (!shot2 && t > 1.2f) { shot2 = true; g.frozen = true; Capture("hud/h2_dengar_relik", p.cam, true); g.frozen = false; }
                yield return null;
            }
            g.frozen = true;
            Check("(2) " + hears + "x mendengar -> indikator tetap tampil tanpa berkedip (alpha min " + minA.ToString("0.00", CI) + ")", minA > 0.95f);
            Check("(2) tidak ada notifikasi 'mendengar' yang berulang", !toastHear);
            Check("(2) pesan relik tidak terpotong pesan berikutnya (tampil " + (relicGone - relicShown).ToString("0.0", CI) + " dtk)", relicShown >= 0f && relicGone - relicShown >= 2.5f);
            Check("(2) 'Semua relik terkumpul!' tampil sesudahnya", hud.ToastNow == "Semua relik terkumpul!");

            // (3) dikejar -> indikator merah; (4) lolos -> 'kehilangan jejak'
            g.Teleport(G.W(gn)); g.ForceChase(); yield return Real(0.45f); g.frozen = true;
            Check("(3) saat dikejar indikator 'HANTU MELIHATMU'", hud.AlertNow == "HANTU MELIHATMU");
            Capture("hud/h3_kejar", p.cam, true);
            HidingSpot nearGhost = spot; float nd = float.MaxValue;
            foreach (HidingSpot hs in gm.house.spots) { float dd = (hs.transform.position - G.W(gn)).sqrMagnitude; if (dd < nd) { nd = dd; nearGhost = hs; } }
            g.frozen = false; g.InspectForTest(nearGhost); yield return Real(0.5f); g.frozen = true;
            Check("(4) setelah lolos indikator 'HANTU KEHILANGAN JEJAKMU'", hud.AlertNow == "HANTU KEHILANGAN JEJAKMU");
            Capture("hud/h4_lolos", p.cam, true);
            g.frozen = false; yield return Real(3.4f); g.frozen = true;
            Check("(4) indikator hilang sendiri setelah beberapa detik", hud.AlertAlpha < 0.05f);

            // (5) bersembunyi: petunjuk + status sembunyi bertumpuk rapi
            p.Spawn(spot.exitPos + Vector3.up * 0.05f, 0f); yield return Frames(3);
            p.EnterHide(spot); yield return Real(1.2f);
            Check("(5) petunjuk sembunyi muncul", hud.HintNow == "hide");
            List<string> ov5 = Overlaps(hud);
            Check("(5) status sembunyi tidak menimpa petunjuk" + (ov5.Count > 0 ? " (" + string.Join(", ", ov5) + ")" : ""), ov5.Count == 0);
            Capture("hud/h5_sembunyi", p.cam, true);
            p.ExitHide(); yield return Real(0.8f);

            // (6) mode presentasi + petunjuk + notifikasi + indikator: tidak ada panel yang saling menutupi
            p.Spawn(G.W(L.start) + Vector3.up * 0.05f, 0f); yield return Frames(3);
            hud.present = true; hud.showDebug = true;
            gm.Toast("Kunci tua didapat", "Cari pintu yang digembok - ikon gembok ada di peta (M)", "key");
            hud.Hint("uji_presentasi", "HANTU! Lari (Shift) atau sembunyi di lemari / kolong ranjang (E). Di persembunyian ia tidak bisa melihatmu.", "ghost");
            GhostMemory.Feed("Mencatat: lemari (Dapur)  ·  dipakai 1×  ·  E[θ] = 0,53");
            yield return Real(1.0f);
            List<string> ov6 = Overlaps(hud);
            Check("(6) mode presentasi: panel tidak saling menutupi" + (ov6.Count > 0 ? " (" + string.Join(", ", ov6) + ")" : ""), ov6.Count == 0);
            Check("(6) keadaan hantu tampil di indikator", hud.AlertNow.StartsWith("HANTU: "));
            Capture("hud/h6_presentasi", p.cam, true);
            hud.present = false; hud.showDebug = false; yield return Real(0.6f);

            // (7) peta penuh dibuka: notifikasi ditahan, indikator pindah ke bawah
            hud.BeginHouse(false, false);
            hud.map.SetFull(true); yield return Real(0.3f);
            gm.Toast("Baterai senter +45%", null, "battery", UIKit.Good);
            g.Teleport(G.W(gn)); g.WakeForTest(); g.frozen = false; g.HearNoise(G.W(gn), 30f); yield return Real(0.6f); g.frozen = true;
            Check("(7) notifikasi ditahan selama peta penuh terbuka", !hud.ToastShown);
            Check("(7) indikator hantu tetap terlihat di bawah peta", hud.AlertNow == "HANTU MENDENGAR SESUATU" && hud.AlertRect.anchorMin.y < 0.5f);
            Capture("hud/h7_peta", p.cam, true);
            hud.map.SetFull(false); yield return Real(0.6f);
            Check("(7) setelah peta ditutup notifikasi muncul", hud.ToastNow == "Baterai senter +45%" && hud.ToastShown);
            yield return Real(3.5f);

            // (9) senter mati -> tangan turun keluar layar; senter menyala -> tangan naik & sinar menyala
            p.Spawn(G.W(L.start) + Vector3.up * 0.05f, 0f); yield return Frames(3);
            // lampu bantu kecil di kamera hanya untuk screenshot: model tangan & senter gelap sulit terlihat di ruangan gelap
            GameObject handLight = new GameObject("TestHandLight"); handLight.transform.SetParent(p.cam.transform, false); handLight.transform.localPosition = new Vector3(0.05f, 0.12f, 0.08f);
            Light hl = handLight.AddComponent<Light>(); hl.type = LightType.Point; hl.range = 1.1f; hl.intensity = 2.2f; hl.color = new Color(1f, 0.92f, 0.82f); hl.shadows = LightShadows.None;
            yield return Frames(2);
            Check("(9) awal: senter menyala & tangan terangkat", p.flashOn && p.HandVisible && p.HandRaise > 0.99f);
            Capture("hud/h9_a_terangkat", p.cam, true);
            p.ToggleFlash(); yield return Frames(2);
            Check("(9) klik mati: sinar langsung padam, tangan belum turun (jeda)", !p.flash.enabled && p.HandVisible);
            float tw9 = Time.realtimeSinceStartup + 2f;
            while (p.HandRaise > 0.8f && Time.realtimeSinceStartup < tw9) yield return null;
            Capture("hud/h9_b_turun", p.cam, true);
            yield return Real(1.0f);
            Check("(9) senter mati: tangan turun keluar layar (raise " + p.HandRaise.ToString("0.00", CI) + ")", !p.HandVisible && p.HandRaise < 0.02f);
            Capture("hud/h9_c_mati", p.cam, true);
            p.ToggleFlash();
            tw9 = Time.realtimeSinceStartup + 2f;
            while (p.HandRaise < 0.75f && Time.realtimeSinceStartup < tw9) yield return null;
            Check("(9) klik nyala: tangan naik kembali (raise " + p.HandRaise.ToString("0.00", CI) + ")", p.HandVisible && p.HandRaise >= 0.75f);
            Capture("hud/h9_d_naik", p.cam, true);
            yield return Real(0.6f);
            Check("(9) senter menyala: tangan terangkat & sinar penuh", p.HandVisible && p.HandRaise > 0.99f && p.flash.enabled && p.flash.intensity > 20f);
            Capture("hud/h9_e_nyala", p.cam, true);
            Destroy(handLight);

            // (8) umpan balik tombol menu terlihat di atas layar pengaturan
            gm.ui.ShowSettings(gm.ui.ShowGame); gm.ui.SelectTab(3);
            hud.Toast("Pengaturan dikembalikan ke bawaan", null, "check", UIKit.Good, true);
            yield return Real(0.6f);
            Check("(8) notifikasi menu terlihat di layar pengaturan", hud.ToastShown);
            Capture("hud/h8_menu", p.cam, true);
            gm.ui.ShowGame(); yield return Real(3.2f);
            g.frozen = true; g.Teleport(G.W(L.ghostStart));
        }

        /// <summary>Pasangan elemen informasi HUD yang tampil dan saling bertumpuk, pada layar 16:9 dan 16:10.</summary>
        List<string> Overlaps(HUD hud)
        {
            List<string> res = new List<string>();
            foreach (Vector2 cv in new[] { new Vector2(1920f, 1080f), new Vector2(1821f, 1138f) })
                foreach (string s in OverlapsAt(hud, cv)) if (!res.Contains(s)) res.Add(s + " @" + cv.x.ToString("0", CI));
            return res;
        }

        /// <summary>Persegi elemen (berjangkar satu titik pada kanvas / root HUD yang membentang penuh) pada ukuran kanvas virtual.</summary>
        static Rect RefRect(RectTransform r, Vector2 cv)
        {
            Vector2 size = r.sizeDelta;
            if (r.name == "Debug") size.y = Mathf.Min(size.y, HUD.DebugMaxH(cv.y));
            if (r.name == "Toast") size.x = Mathf.Min(size.x, Mathf.Max(300f, HUD.ToastMaxW(cv.x, r.parent.Find("HUD/Debug") != null && r.parent.Find("HUD/Debug").gameObject.activeInHierarchy)));
            Vector2 piv = new Vector2(r.anchorMin.x * cv.x, r.anchorMin.y * cv.y) + r.anchoredPosition;
            return new Rect(piv.x - r.pivot.x * size.x, piv.y - r.pivot.y * size.y, size.x, size.y);
        }

        List<string> OverlapsAt(HUD hud, Vector2 cv)
        {
            List<RectTransform> els = new List<RectTransform>();
            foreach (string n in new[] { "Objective", "Flash", "Minimap", "Compass", "Prompt", "Tutorial" })
            {
                Transform t = hud.root.Find(n);
                if (t != null && t.gameObject.activeInHierarchy) els.Add((RectTransform)t);
            }
            foreach (RectTransform r in new[] { hud.ToastRect, hud.AlertRect, hud.HintRect, hud.HiddenRect, hud.DebugRect, hud.FeedRect })
                if (r != null && r.gameObject.activeInHierarchy) els.Add(r);
            List<string> res = new List<string>();
            for (int i = 0; i < els.Count; i++)
                for (int j = i + 1; j < els.Count; j++)
                {
                    CanvasGroup ca = els[i].GetComponent<CanvasGroup>(), cb = els[j].GetComponent<CanvasGroup>();
                    if ((ca != null && ca.alpha < 0.05f) || (cb != null && cb.alpha < 0.05f)) continue;
                    if (RefRect(els[i], cv).Overlaps(RefRect(els[j], cv))) res.Add(els[i].name + "+" + els[j].name);
                }
            return res;
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
