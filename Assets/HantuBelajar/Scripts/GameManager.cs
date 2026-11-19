using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Titik masuk game. Scene hanya berisi objek "Bootstrap" dengan komponen ini.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public enum State { Title, Playing, Paused, Caught, Won }
        public State state = State.Title;
        public bool Playing { get { return state == State.Playing; } }

        public HouseLayout layout; public HouseInstance house;
        public PlayerController player; public GhostAI ghost; public DDAManager dda; public HUD hud;
        public int relicsFound, houseNo, seed = 12345;
        public string endReason = "";
        public static bool AutoStart;     // dipakai harness uji
        public int RelicsNeeded { get { return layout != null ? layout.relics.Count : 3; } }

        void Awake()
        {
            I = this;
            GhostMemory.Load();
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadowDistance = 25f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.09f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.045f; RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
            RenderSettings.skybox = null;
            new GameObject("Audio").AddComponent<AudioDirector>();
            dda = gameObject.AddComponent<DDAManager>();
            player = PlayerController.Create();
            ghost = GhostAI.Create();
            hud = HUD.Create();
        }

        void Start()
        {
            NewHouse(seed);
            state = AutoStart ? State.Playing : State.Title;
        }

        public void NewHouse(int s)
        {
            seed = s;
            if (house != null) Destroy(house.gameObject);
            PcgParams p = dda.MakePcg(seed);
            layout = HouseGenerator.Generate(p);
            house = HouseBuilder.Build(layout);
            relicsFound = 0; houseNo++;
            Vector2Int st = layout.start;
            // hadap ke arah ruangan yang terbuka
            player.Spawn(G.W(st) + Vector3.up * 0.05f, 0f);
            ghost.Init(layout, house, player);
            GhostMemory.BeginHouse(layout);
            dda.BeginHouse();
            Debug.Log("[PCG] seed " + seed + " ukuran " + p.width + "x" + p.height + " ruang " + layout.rooms.Count + " valid " + layout.valid + " percobaan " + layout.attempts + " hash " + layout.hash + " " + layout.ms.ToString("0.0") + " ms");
        }

        void Update()
        {
            if (Playing && player != null) GhostMemory.RecordPos(player.Cell, Time.deltaTime);

            switch (state)
            {
                case State.Title:
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) { state = State.Playing; Lock(true); }
                    break;
                case State.Playing:
                    if (Input.GetKeyDown(KeyCode.Escape)) { state = State.Paused; Lock(false); }
                    if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) Lock(true);
                    break;
                case State.Paused:
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape)) { state = State.Playing; Lock(true); }
                    break;
                case State.Caught:
                case State.Won:
                    if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)) { NewHouse(seed + 1); state = State.Playing; Lock(true); }
                    break;
            }

            // tombol demo / debug untuk video progress
            if (Input.GetKeyDown(KeyCode.G)) { NewHouse(seed + 1); Toast("PCG: rumah baru seed " + seed); }
            if (Input.GetKeyDown(KeyCode.F2)) { dda.Force(-0.15f); Toast("DDA: kesulitan turun -> " + dda.Label); }
            if (Input.GetKeyDown(KeyCode.F3)) { dda.Force(+0.15f); Toast("DDA: kesulitan naik -> " + dda.Label); }
            if (Input.GetKeyDown(KeyCode.F5)) { ghost.frozen = !ghost.frozen; Toast(ghost.frozen ? "Hantu dibekukan (demo)" : "Hantu aktif lagi"); }
            if (Input.GetKeyDown(KeyCode.F6) && Playing && !player.hidden) { ghost.SummonAndChase(3); Toast("Demo: hantu dipanggil dan mengejar!"); }
            if (Input.GetKeyDown(KeyCode.F9)) { GhostMemory.Reset(); Toast("Memori hantu direset"); }
        }

        public void Lock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        public void OnRelic(RelicPickup r)
        {
            relicsFound++;
            dda.OnRelic();
            AudioDirector.I.Pickup();
            Toast("Relik didapat: " + RelicPickup.Names[r.index % RelicPickup.Names.Length] + " (" + relicsFound + "/" + RelicsNeeded + ")");
            if (relicsFound >= RelicsNeeded) Toast("Semua relik terkumpul! Cari pintu keluar (hijau)");
        }

        public void OnCaught(string why)
        {
            if (!Playing) return;
            endReason = why;
            dda.OnCaught();
            state = State.Caught; Lock(false);
            AudioDirector.I.Scare();
        }

        public void OnWin()
        {
            if (!Playing) return;
            endReason = "Kamu lolos dari rumah berhantu!";
            dda.OnWin();
            state = State.Won; Lock(false);
        }

        public void Toast(string s) { if (hud != null) hud.Toast(s); }
    }
}
