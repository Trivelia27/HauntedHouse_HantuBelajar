using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Merekam video demo progress secara otomatis (frame JPG 30 fps -> ffmpeg).
    /// Alur mengikuti naskah: konsep -> progress -> PCG -> Game AI -> DDA -> integrasi -> berikutnya.
    /// </summary>
    public class DemoRecorder : MonoBehaviour
    {
        public static string OutDir = "TestOutput/frames";
        public const int W = 1280, H = 720, FPS = 24;
        GameManager gm; RenderTexture rt; Texture2D read; Camera over; int frame;
        public static bool Done; public static string Report = "";
        Canvas canvas;
        // durasi tiap bagian (detik), bisa ditimpa dari TestOutput/timing.txt agar cocok dengan voice
        public static float[] Dur = { 50, 50, 80, 100, 70, 40, 40 };
        float secEnd;
        float Now { get { return frame / (float)FPS; } }
        void Section(int i) { float st = 0; for (int k = 0; k < i; k++) st += Dur[k]; secEnd = st + Dur[i]; }
        IEnumerator PadUntilEnd(bool walk)
        {
            while (Now < secEnd - 0.05f)
            {
                if (walk && gm.state == GameManager.State.Playing && !gm.player.hidden)
                {
                    Vector2Int c = Far(gm.player.Cell, Random.Range(3, 7));
                    yield return WalkTo(c, false, Mathf.Min(8f, secEnd - Now), 0.5f);
                    yield return LookAround(Mathf.Min(1.5f, Mathf.Max(0f, secEnd - Now)));
                }
                else yield return null;
            }
        }

        IEnumerator Start()
        {
            Done = false;
            try
            {
                string tf = "TestOutput/timing.txt";
                if (File.Exists(tf))
                {
                    string[] parts = File.ReadAllText(tf).Split(new[] { ',', ' ', (char)10, (char)13 }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length && i < Dur.Length; i++) Dur[i] = float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch (System.Exception e) { Debug.LogWarning(e); }
            Directory.CreateDirectory(OutDir);
            foreach (string f in Directory.GetFiles(OutDir, "*.jpg")) File.Delete(f);
            Time.captureFramerate = FPS;
            UnityEngine.Random.InitState(7);
            yield return null;
            gm = GameManager.I;
            rt = new RenderTexture(W, H, 24); read = new Texture2D(W, H, TextureFormat.RGB24, false);
            canvas = gm.hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = 0.1f;
            GameObject oc = new GameObject("OverCam"); over = oc.AddComponent<Camera>(); over.enabled = false;
            over.cullingMask = ~(1 << 8); over.clearFlags = CameraClearFlags.SolidColor; over.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            GhostMemory.Reset(); GhostMemory.feed.Clear();
            gm.ghost.frozen = true;
            gm.hud.showMap = true; gm.hud.showDebug = false;
            capturing = true;

            // 1. konsep (layar judul)
            gm.state = GameManager.State.Title;
            Section(0);
            Cap("1. Konsep Game", "3D First-Person Survival Horror  |  PCG + DDA + Game AI (hantu yang belajar)");
            yield return Wait(Mathf.Min(8f, Dur[0] * 0.4f));
            gm.state = GameManager.State.Playing;
            gm.player.autopilot = true;
            yield return PadUntilEnd(true);

            // 2. progress implementasi
            Section(1);
            Cap("2. Progress Implementasi", "Kontrol FPS, senter berbaterai, stamina, relik, baterai, pintu keluar, menang/kalah");
            gm.player.autopilot = true;
            yield return LookAround(2.5f);
            RelicPickup rel = Nearest(gm.house.relics);
            if (rel != null) { yield return WalkTo(G.C(rel.transform.position), false, 25f, 1.6f); gm.player.autoDir = Vector3.zero; yield return Face(rel.transform.position, 0.8f); rel.Use(gm.player); }
            yield return Wait(2.5f);
            gm.player.flashOn = false; yield return Wait(0.8f); gm.player.flashOn = true; yield return Wait(1f);
            yield return PadUntilEnd(true);

            // 3. PCG
            Section(2);
            Cap("3. Procedural Content Generation", "BSP -> graf ruang -> Kruskal + loop -> BFS start/exit -> generate-and-test penempatan -> validasi");
            gm.hud.showDebug = true;
            over.enabled = true;
            GameObject sun = new GameObject("DemoSun"); Light sl = sun.AddComponent<Light>(); sl.type = LightType.Directional; sl.intensity = 0.9f; sun.transform.rotation = Quaternion.Euler(70, 30, 0);
            for (int i = 0; Now < secEnd - 4f; i++)
            {
                if (i > 0) { gm.NewHouse(gm.seed + 1); gm.ghost.frozen = true; gm.hud.Toast("PCG: rumah baru seed " + gm.seed + " (" + gm.layout.rooms.Count + " ruang, " + gm.layout.doors.Count + " pintu)"); }
                PlaceOver();
                yield return Wait(i == 0 ? 6f : 5f);
            }
            Destroy(sun); over.enabled = false;

            // 4. Game AI
            Section(3);
            Cap("4. Game AI - Hantu (FSM + memori belajar)", "Patroli / Hadang / Selidiki / Kejar / Cari / Periksa  |  A* di grid  |  penglihatan + pendengaran");
            gm.ghost.frozen = false;
            HidingSpot fav = null;
            for (int round = 0; Now < secEnd - 22f; round++)
            {
                if (gm.state != GameManager.State.Playing) { yield return Wait(3f); gm.NewHouse(gm.seed + 1); gm.state = GameManager.State.Playing; fav = null; }
                fav = fav ?? NearestSpot();
                if (fav == null) break;
                Vector3 spawn = G.W(Far(gm.player.Cell, 4));
                gm.ghost.Teleport(spawn);
                yield return Wait(1f);
                gm.ghost.ForceChase();
                gm.hud.Toast("Hantu melihatmu! Lari dan sembunyi");
                yield return WalkTo(fav.front, true, 18f, 0.3f);
                if (gm.state != GameManager.State.Playing) continue;
                yield return Face(fav.transform.position, 0.5f);
                if (!fav.occupied) gm.player.EnterHide(fav);
                Cap("4. Game AI - belajar tempat sembunyi", "Sembunyi ke-" + (round + 1) + " di " + HouseGenerator.SpotName((int)fav.kind) + " yang sama -> skor belajar & peluang ketahuan naik");
                float t = 0f;
                while (t < 11f && gm.state == GameManager.State.Playing) { t += Time.deltaTime; yield return null; }
                if (gm.state == GameManager.State.Playing && gm.player.hidden) { gm.player.ExitHide(); yield return Wait(0.5f); }
                if (gm.state == GameManager.State.Playing) { gm.ghost.Teleport(G.W(Far(gm.player.Cell, 99))); yield return WalkTo(Far(gm.player.Cell, 3), false, 6f, 0.5f); }
            }
            if (gm.state != GameManager.State.Playing) { yield return Wait(3.5f); gm.NewHouse(gm.seed + 1); gm.state = GameManager.State.Playing; }
            gm.ghost.frozen = false;
            yield return PadUntilEnd(true);

            // 5. DDA
            Section(4);
            Cap("5. Dynamic Difficulty Adjustment", "Indikator: tempo relik, waktu dikejar, baterai, waktu sembunyi, nyaris tertangkap -> P -> d");
            gm.ghost.frozen = true;
            gm.ghost.Teleport(G.W(Far(gm.player.Cell, 99)));
            yield return Wait(2.5f);
            gm.dda.Force(+0.15f); gm.hud.Toast("DDA naik -> " + gm.dda.Label + " : hantu lebih cepat, pandang lebih jauh"); yield return Wait(2.5f);
            gm.dda.Force(+0.15f); gm.hud.Toast("DDA naik -> " + gm.dda.Label); yield return Wait(2.5f);
            gm.dda.Force(-0.3f); gm.hud.Toast("DDA turun -> " + gm.dda.Label + " : hantu melambat"); yield return Wait(2.5f);
            Cap("5. DDA - pemain tertangkap", "Event tertangkap: d -0.15 -> rumah berikutnya lebih ramah (lebih banyak tempat sembunyi & baterai)");
            gm.ghost.Teleport(gm.player.transform.position + gm.player.transform.forward * 5f);
            gm.ghost.ForceChase();
            float tc = 0f;
            while (gm.state == GameManager.State.Playing && tc < 10f) { tc += Time.deltaTime; gm.player.autoDir = Vector3.zero; yield return null; }
            yield return Wait(4.5f);
            gm.NewHouse(gm.seed + 1); gm.state = GameManager.State.Playing; gm.ghost.frozen = true;
            gm.hud.Toast("Rumah baru dari parameter DDA: " + gm.layout.p.width + "x" + gm.layout.p.height + ", sembunyi " + gm.layout.spots.Count + ", baterai " + gm.layout.batteries.Count);
            yield return Wait(4f);
            yield return PadUntilEnd(true);

            // 6. integrasi
            Section(5);
            Cap("6. Integrasi Sistem", "Player Performance -> DDA -> Parameter PCG & Game AI -> Perubahan Gameplay  (+ memori belajar hantu)");
            RelicPickup r2 = Nearest(gm.house.relics);
            if (r2 != null) yield return WalkTo(G.C(r2.transform.position), false, 9f, 1.6f);
            yield return Wait(1f);
            yield return PadUntilEnd(true);

            // 7. berikutnya
            gm.hud.showDebug = false;
            Section(6);
            Cap("7. Progress Berikutnya", "Aset modular Haunted House, animasi hantu, model belajar probabilistik, kalibrasi DDA + eksperimen, menu & build");
            yield return PadUntilEnd(true);
            Cap("", "");
            gm.state = GameManager.State.Title;
            yield return Wait(2f);
            Report = "frames " + frame;
            Done = true;
        }

        void Cap(string a, string b) { gm.hud.Caption(a, b); }

        IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.deltaTime; yield return null; } }

        bool capturing;
        void LateUpdate()
        {
            if (!capturing || Done || gm == null) return;
            Camera cam = over.enabled ? over : gm.player.cam;
            canvas.worldCamera = cam;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, W, H), 0, 0); read.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, "f" + frame.ToString("00000") + ".jpg"), read.EncodeToJPG(85));
            frame++;
        }

        void PlaceOver()
        {
            HouseLayout L = gm.layout;
            float cx = L.w * G.T * 0.5f, cz = L.h * G.T * 0.5f;
            float size = Mathf.Max(L.w, L.h * 16f / 9f) * G.T;
            over.transform.position = new Vector3(cx - 4f, size * 0.85f, cz - size * 0.42f);
            over.transform.LookAt(new Vector3(cx - 4f, 0, cz));
            over.fieldOfView = 55f;
        }

        IEnumerator LookAround(float s)
        {
            float t = 0f; PlayerController p = gm.player;
            while (t < s) { t += Time.deltaTime; p.autoDir = Vector3.zero; p.Yaw += Mathf.Sin(t * 1.5f) * 50f * Time.deltaTime; yield return null; }
        }

        IEnumerator Face(Vector3 p, float s)
        {
            Vector3 d = p - gm.player.transform.position; d.y = 0;
            gm.player.autoDir = Vector3.zero;
            float ty = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float t = 0f;
            while (t < s) { t += Time.deltaTime; gm.player.Yaw = Mathf.MoveTowardsAngle(gm.player.Yaw, ty, 240f * Time.deltaTime); yield return null; }
        }

        IEnumerator WalkTo(Vector2Int target, bool run, float maxT, float stopDist)
        {
            PlayerController p = gm.player;
            HouseLayout L = gm.layout;
            List<Vector2Int> path = L.Path(p.Cell, target);
            p.autoRun = run;
            float t = 0f; int i = 1;
            while (i < path.Count && t < maxT && gm.state == GameManager.State.Playing)
            {
                Vector3 w = G.W(path[i]);
                Vector3 d = w - p.transform.position; d.y = 0;
                bool last = i == path.Count - 1;
                if (d.magnitude < (last ? stopDist : 0.45f)) { i++; continue; }
                p.autoDir = d.normalized;
                t += Time.deltaTime;
                yield return null;
            }
            p.autoDir = Vector3.zero; p.autoRun = false;
        }

        RelicPickup Nearest(List<RelicPickup> l)
        {
            RelicPickup best = null; int bd = int.MaxValue;
            int[,] dist = gm.layout.Distances(gm.player.Cell);
            foreach (RelicPickup r in l) { if (r.taken) continue; Vector2Int c = G.C(r.transform.position); int d = dist[c.x, c.y]; if (d >= 0 && d < bd) { bd = d; best = r; } }
            return best;
        }

        HidingSpot NearestSpot()
        {
            HidingSpot best = null; int bd = int.MaxValue;
            int[,] dist = gm.layout.Distances(gm.player.Cell);
            foreach (HidingSpot s in gm.house.spots) { int d = dist[s.front.x, s.front.y]; if (d >= 2 && d < bd && s.kind == SpotKind.Lemari) { bd = d; best = s; } }
            if (best == null) foreach (HidingSpot s in gm.house.spots) { int d = dist[s.front.x, s.front.y]; if (d >= 0 && d < bd) { bd = d; best = s; } }
            return best;
        }

        /// <summary>Sel yang berjarak kira-kira n langkah (n besar = terjauh).</summary>
        Vector2Int Far(Vector2Int from, int n)
        {
            int[,] dist = gm.layout.Distances(from);
            Vector2Int best = from; int bd = -1;
            for (int x = 0; x < gm.layout.w; x++) for (int y = 0; y < gm.layout.h; y++)
                {
                    int d = dist[x, y]; if (d < 0) continue;
                    int score = n >= 99 ? d : -Mathf.Abs(d - n);
                    if (bd < 0 || score > bd) { bd = score; best = new Vector2Int(x, y); }
                }
            return best;
        }
    }
}
