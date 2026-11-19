using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Titik masuk & alur game. Scene hanya berisi objek "Bootstrap" dengan komponen ini.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public enum State { Menu, Playing, Paused, Catching, Caught, Won }
        public State state = State.Menu;
        public bool Playing { get { return state == State.Playing; } }

        public HouseLayout layout; public HouseInstance house;
        public PlayerController player; public GhostAI ghost; public DDAManager dda; public GameUI ui; public Atmosphere atmo;
        public int relicsFound, houseNo, seed;
        public float houseTime;
        public string endReason = "";
        public int caughtThisHouse;

        /// <summary>Mode uji (harness/eksperimen): tanpa menu, tanpa sinematik, kursor tidak dikunci.</summary>
        public static bool TestMode;
        public static bool SkipMenu;
        float catchT, dAtStart;
        float menuSpin;
        bool autoplayArg, autoShots; float quitAfter = -1f, nextShot = 10f; int shotN;

        public int RelicsNeeded { get { return layout != null ? layout.relics.Count : 3; } }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 144;
            Settings.Load();
            GhostMemory.Load();
#if UNITY_EDITOR
            string testKind = UnityEditor.SessionState.GetString("HB_TEST_KIND", "");
            if (testKind.Length > 0) { TestMode = true; gameObject.AddComponent<TestRunner>().kind = testKind; }
#endif
            QualitySettings.vSyncCount = 1;
            atmo = gameObject.AddComponent<Atmosphere>();
            atmo.Setup();
            new GameObject("Audio").AddComponent<AudioDirector>();
            dda = gameObject.AddComponent<DDAManager>();
            player = PlayerController.Create();
            ghost = GhostAI.Create();
            ui = GameUI.Create();
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoplay") autoplayArg = true;
                if (args[i] == "-autoshots") autoShots = true;
                if (args[i] == "-quitafter" && i + 1 < args.Length) float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out quitAfter);
            }
        }

        void Start()
        {
            Settings.Apply();
            if (autoplayArg) { TestMode = true; StartNew(); player.gameObject.AddComponent<AutoPilot>().profile = AutoPilot.Profile.Sedang; return; }
            if (TestMode || SkipMenu) { StartNew(); return; }
            seed = Random.Range(1000, 99999);
            NewHouse(seed);
            EnterMenu();
        }

        // ------------------------------------------------------------------ alur
        void EnterMenu()
        {
            state = State.Menu;
            Time.timeScale = 1f; AudioListener.pause = false;
            ghost.gameObject.SetActive(false);
            ui.ShowMain(); ui.SetFade(1f); ui.FadeTo(0f, 0.8f);
            AudioDirector.I.SetMode(AudioDirector.Mode.Menu);
            Lock(false);
        }

        public void StartNew()
        {
            // pertama kali bermain: tampilkan panduan bermain dulu
            if (!TestMode && !GuideSeen) { ui.ShowGuide(true, () => { GuideSeen = true; StartNewInternal(); }); return; }
            StartNewInternal();
        }

        void StartNewInternal()
        {
            houseNo = 0; dda.d = 0.4f; dda.Ps = 0.5f; dda.Apply();
            seed = Random.Range(1000, 99999);
            NextHouseInternal();
        }

        public static bool GuideSeen { get { return PlayerPrefs.GetInt("HB_GuideSeen", 0) == 1; } set { PlayerPrefs.SetInt("HB_GuideSeen", value ? 1 : 0); PlayerPrefs.Save(); } }

        public void Continue()
        {
            if (!HasSave(out int h, out float d)) { StartNew(); return; }
            houseNo = h; dda.d = d; dda.Apply();
            seed = PlayerPrefs.GetInt("HB_SaveSeed", Random.Range(1000, 99999)) + 1;
            NextHouseInternal();
        }

        public void NextHouse() { seed++; NextHouseInternal(); }

        void NextHouseInternal()
        {
            houseNo++;
            NewHouse(seed);
            BeginPlay();
            SaveProgress();
        }

        public void NewHouse(int s)
        {
            seed = s;
            if (house != null) Destroy(house.gameObject);
            Lamp.All.Clear();
            player.ForceUnhide();
            PcgParams p = dda.MakePcg(seed);
            layout = HouseGenerator.Generate(p);
            house = HouseBuilder.Build(layout);
            atmo.BuildOutside(layout);
            relicsFound = 0; houseTime = 0f; caughtThisHouse = 0;
            player.Spawn(G.W(layout.start) + Vector3.up * 0.05f, StartYaw());
            GhostMemory.BeginHouse(layout);
            ghost.gameObject.SetActive(true);
            ghost.Init(layout, house, player);
            dda.BeginHouse(layout);
            dAtStart = dda.d;
            if (house.exit != null) house.exit.SetUnlocked(false);
            Debug.Log("[PCG] rumah #" + houseNo + " seed " + seed + " " + p.width + "x" + p.height + "x" + layout.floors + " ruang " + layout.rooms.Count + " valid " + layout.valid + " (" + layout.failReason + ") percobaan " + layout.attempts + " hash " + layout.hash + " " + layout.ms.ToString("0.0") + " ms, d=" + dda.d.ToString("0.00"));
        }

        float StartYaw()
        {
            foreach (DoorEdge d in layout.doors)
            {
                if (d.f != 0) continue;
                Room ra = layout.rooms[layout.room[0, d.a.x, d.a.y]];
                Room rb = layout.rooms[layout.room[0, d.b.x, d.b.y]];
                Room sr = layout.RoomAt(layout.start);
                if (ra != sr && rb != sr) continue;
                Vector3 doorPos = (G.W(0, d.a) + G.W(0, d.b)) * 0.5f;
                Vector3 dir = G.Flat(doorPos - G.W(layout.start));
                if (dir.sqrMagnitude > 0.1f) return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
            return 0f;
        }

        void BeginPlay()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            ui.ShowGame();
            state = State.Playing;
            AudioDirector.I.SetMode(AudioDirector.Mode.Game);
            Lock(true);
            if (!TestMode)
            {
                ui.SetFade(1f); ui.FadeTo(0f, 0.9f);
                ui.ShowIntro("RUMAH #" + houseNo, new[] { layout.floors > 1 ? "2 LANTAI" : "1 LANTAI", RelicsNeeded + " RELIK", layout.lockedRoom >= 0 ? "RUANG DIGEMBOK" : null, "KESULITAN " + dda.Label }, 4.2f);
            }
            ui.hud.BeginHouse(!TestMode && !HUD.TutDone, houseNo == 1);
        }

        public void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused;
            Time.timeScale = 0f; AudioListener.pause = true;
            ui.hud.map.SetFull(false);
            ui.ShowPause();
            Lock(false);
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            Time.timeScale = 1f; AudioListener.pause = false;
            ui.ShowGame();
            state = State.Playing;
            Lock(true);
        }

        public void ToMenu()
        {
            SaveProgress();
            player.ForceUnhide();
            EnterMenu();
        }

        public void Quit()
        {
            SaveProgress();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            if (autoShots && Time.realtimeSinceStartup > nextShot) { nextShot += 15f; AutoShot(); }
            if (quitAfter > 0f && Time.realtimeSinceStartup > quitAfter) { Debug.Log("[Autoplay] selesai, rumah " + houseNo + " tertangkap " + dda.caughtTotal + " lolos " + dda.winsTotal); Application.Quit(); quitAfter = -1f; }
            switch (state)
            {
                case State.Menu:
                    menuSpin += udt * 6f;
                    player.Yaw = StartYaw() + Mathf.Sin(menuSpin * 0.03f) * 40f;
                    player.transform.rotation = Quaternion.Euler(0f, player.Yaw, 0f);
                    break;
                case State.Playing:
                    houseTime += Time.deltaTime;
                    if (!player.hidden) GhostMemory.RecordPos(layout, player.node, Time.deltaTime);   // jalur yang dilalui, bukan tempat diam bersembunyi
                    if (Input.GetKeyDown(KeyCode.Escape) && !TestMode) { Pause(); break; }
                    if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !TestMode) Lock(true);
                    DevKeys();
                    break;
                case State.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape)) Resume();
                    break;
                case State.Catching:
                    catchT += udt;
                    ghost.PlayCatch(player, catchT);
                    player.LookAtPoint(ghost.FacePos, 20f);
                    player.cam.transform.localPosition = Random.insideUnitSphere * 0.03f * Mathf.Clamp01(1.2f - catchT);
                    if (catchT > 1.6f) FinishCatch();
                    break;
            }
        }

        void DevKeys()
        {
            if (Input.GetKeyDown(KeyCode.G)) { NewHouse(seed + 1); Toast("PCG: rumah baru seed " + seed + " (" + layout.rooms.Count + " ruang, " + layout.floors + " lantai)"); }
            if (Input.GetKeyDown(KeyCode.F2)) { dda.Force(-0.15f); Toast("DDA: kesulitan turun -> " + dda.Label + " (d=" + dda.d.ToString("0.00") + ")"); }
            if (Input.GetKeyDown(KeyCode.F3)) { dda.Force(+0.15f); Toast("DDA: kesulitan naik -> " + dda.Label + " (d=" + dda.d.ToString("0.00") + ")"); }
            if (Input.GetKeyDown(KeyCode.F5)) { ghost.frozen = !ghost.frozen; Toast(ghost.frozen ? "Hantu dibekukan (demo)" : "Hantu aktif lagi"); }
            if (Input.GetKeyDown(KeyCode.F6) && !player.hidden) { ghost.SummonAndChase(3); Toast("Demo: hantu dipanggil dan mengejar!"); }
            if (Input.GetKeyDown(KeyCode.F9)) { GhostMemory.Reset(); Toast("Memori hantu dihapus"); }
        }

        public void Lock(bool on)
        {
            if (TestMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // ------------------------------------------------------------------ event game
        public void OnRelic(RelicPickup r)
        {
            relicsFound++;
            dda.OnRelic();
            AudioDirector.I.Pickup();
            Toast("Relik " + relicsFound + " / " + RelicsNeeded + "  ·  " + RelicPickup.Names[r.index % RelicPickup.Names.Length], RelicPickup.Lore[r.index % RelicPickup.Lore.Length]);
            if (relicsFound >= RelicsNeeded)
            {
                if (house.exit != null) house.exit.SetUnlocked(true);
                Toast("Semua relik terkumpul!", "Pintu keluar terbuka - ikuti kompas ke lantai 1");
            }
        }
        public void OnKey() { dda.OnProgress(); Toast("Kunci tua didapat", "Cari pintu yang digembok - ikon gembok ada di peta (M)"); }
        public void OnUnlock()
        {
            layout.lockedKeys.Clear();     // graf jalur pemain kini melewati pintu yang sudah terbuka
            dda.OnProgress();
            Toast("Gembok terbuka");
        }

        public void OnWin()
        {
            if (state != State.Playing) return;
            player.ForceUnhide();
            state = State.Won;
            dda.OnWin();
            AudioDirector.I.Win();
            endReason = "Kamu berhasil kabur dari rumah #" + houseNo + ".";
            ui.ShowEnd(true, endReason, Stats(true));
            Lock(false);
            SaveProgress();
        }

        public void BeginCatch(GhostAI g, string reason)
        {
            if (state != State.Playing) return;
            endReason = reason;
            caughtThisHouse++;
            player.inputLocked = true;
            if (TestMode) { FinishCatch(); return; }
            state = State.Catching; catchT = 0f;
            AudioDirector.I.Scream();
            if (atmo != null) atmo.Flash(1f);
        }

        void FinishCatch()
        {
            dda.OnCaught();
            state = State.Caught;
            player.cam.transform.localPosition = Vector3.zero;
            player.ForceUnhide();
            ghost.frozen = true;
            ui.ShowEnd(false, endReason, Stats(false));
            Lock(false);
            SaveProgress();
        }

        EndInfo Stats(bool won)
        {
            PcgParams nx = dda.MakePcg(0);
            return new EndInfo
            {
                time = houseTime, relics = relicsFound, relicsNeed = RelicsNeeded, chases = dda.chasesHouse, hides = dda.hidesHouse,
                dFrom = dAtStart, dTo = dda.d, label = dda.Label,
                next = "Rumah berikutnya: " + nx.floors + " lantai  ·  " + nx.hidingSpots + " tempat sembunyi  ·  " + nx.batteries + " baterai  ·  " + nx.relicCount + " relik" +
                       (dda.d > dAtStart + 0.005f ? "   -   hantu makin peka dan cepat" : dda.d < dAtStart - 0.005f ? "   -   hantu lebih lambat dan kurang peka" : "   -   tingkat kesulitan tetap"),
            };
        }

        public void Toast(string s, string sub = null) { if (ui != null) ui.hud.Toast(s, sub); }

        /// <summary>Uji build (-autoshots): render kamera + UI ke PNG di samping exe.</summary>
        void AutoShot()
        {
            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(1280, 720, 24);
                Canvas c = ui.canvas;
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = player.cam; c.planeDistance = 0.11f;
                Canvas.ForceUpdateCanvases();
                player.cam.targetTexture = rt; player.cam.Render(); player.cam.targetTexture = null;
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply();
                RenderTexture.active = null;
                c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null;
                string dir = System.IO.Path.GetDirectoryName(Application.dataPath);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "autoshot_" + (shotN++).ToString("00") + ".png"), t.EncodeToPNG());
                Destroy(t); RenderTexture.ReleaseTemporary(rt);
            }
            catch (System.Exception e) { Debug.LogWarning("[AutoShot] " + e.Message); }
        }

        // ------------------------------------------------------------------ simpan
        void SaveProgress()
        {
            if (TestMode) return;
            PlayerPrefs.SetInt("HB_SaveHouse", houseNo);
            PlayerPrefs.SetFloat("HB_SaveD", dda.d);
            PlayerPrefs.SetInt("HB_SaveSeed", seed);
            PlayerPrefs.Save();
        }
        public static bool HasSave(out int house, out float d)
        {
            house = PlayerPrefs.GetInt("HB_SaveHouse", 0);
            d = PlayerPrefs.GetFloat("HB_SaveD", 0.4f);
            return house > 0;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && state == State.Playing && !TestMode && !Application.isBatchMode) Pause();
        }
    }
}
