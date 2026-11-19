using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Memori hantu = "belajar" kebiasaan pemain.
    /// - heat[x,y]: jalur yang sering dilewati pemain (rumah saat ini)
    /// - spotUse: tempat sembunyi yang sering dipakai (rumah saat ini)
    /// - kindUse / roomHide: preferensi jenis tempat & tipe ruangan (lintas rumah, disimpan PlayerPrefs)
    /// </summary>
    public static class GhostMemory
    {
        public static float[,] heat;
        public static readonly Dictionary<int, int> spotUse = new Dictionary<int, int>();
        public static float[] kindUse = new float[2];
        public static float[] roomHide = new float[9];
        public static int totalHides;
        public static readonly List<string> feed = new List<string>();

        [System.Serializable] class Save { public float[] k; public float[] r; public int t; }

        public static void Load()
        {
            string s = PlayerPrefs.GetString("HB_GhostMemory", "");
            if (string.IsNullOrEmpty(s)) return;
            Save d = JsonUtility.FromJson<Save>(s);
            if (d == null || d.k == null || d.k.Length != 2 || d.r == null || d.r.Length != 9) return;
            kindUse = d.k; roomHide = d.r; totalHides = d.t;
        }
        static void Persist() { PlayerPrefs.SetString("HB_GhostMemory", JsonUtility.ToJson(new Save { k = kindUse, r = roomHide, t = totalHides })); }
        public static void Reset() { kindUse = new float[2]; roomHide = new float[9]; totalHides = 0; spotUse.Clear(); if (heat != null) System.Array.Clear(heat, 0, heat.Length); Persist(); Feed("Memori hantu direset"); }

        public static void BeginHouse(HouseLayout L) { heat = new float[L.w, L.h]; spotUse.Clear(); }

        public static void RecordPos(Vector2Int c, float dt)
        {
            if (heat == null || c.x < 0 || c.y < 0 || c.x >= heat.GetLength(0) || c.y >= heat.GetLength(1)) return;
            heat[c.x, c.y] += dt;
        }

        public static void RecordHide(HidingSpot s)
        {
            spotUse.TryGetValue(s.id, out int u); spotUse[s.id] = u + 1;
            kindUse[(int)s.kind] += 1f; roomHide[(int)s.roomType] += 1f; totalHides++;
            Persist();
            Feed("Hantu mencatat: kamu sembunyi di " + HouseGenerator.SpotName((int)s.kind) + " (" + HouseGenerator.RoomName(s.roomType) + ") - dipakai " + (u + 1) + "x");
        }

        public static void Feed(string s) { feed.Add(s); if (feed.Count > 6) feed.RemoveAt(0); }

        /// <summary>Skor 0..1 seberapa yakin hantu bahwa pemain suka sembunyi di sini.</summary>
        public static float SpotScore(HidingSpot s)
        {
            spotUse.TryGetValue(s.id, out int u);
            float local = u / (u + 1.5f);
            float ks = kindUse[0] + kindUse[1];
            float kindPref = ks > 0 ? kindUse[(int)s.kind] / ks : 0.5f;
            float roomPref = totalHides > 0 ? roomHide[(int)s.roomType] / totalHides : 0f;
            return Mathf.Clamp01(0.55f * local + 0.3f * kindPref + 0.15f * roomPref);
        }

        public static float KindPref(int k) { float ks = kindUse[0] + kindUse[1]; return ks > 0 ? kindUse[k] / ks : 0.5f; }

        /// <summary>Sel paling sering dilewati (untuk patroli/hadang).</summary>
        public static List<Vector2Int> HotCells(int n)
        {
            List<Vector2Int> res = new List<Vector2Int>();
            if (heat == null) return res;
            List<(float, Vector2Int)> all = new List<(float, Vector2Int)>();
            for (int x = 0; x < heat.GetLength(0); x++) for (int y = 0; y < heat.GetLength(1); y++) if (heat[x, y] > 1.5f) all.Add((heat[x, y], new Vector2Int(x, y)));
            all.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            for (int i = 0; i < all.Count && i < n; i++) res.Add(all[i].Item2);
            return res;
        }
    }

    public enum GhostState { Patroli, Hadang, Selidiki, Kejar, Cari, Periksa }

    /// <summary>
    /// Hantu: Finite State Machine (Patroli -> Selidiki -> Kejar -> Cari -> Periksa) + keputusan berbasis memori
    /// (Hadang di jalur favorit pemain, urutan memeriksa tempat sembunyi berdasarkan skor belajar).
    /// </summary>
    public class GhostAI : MonoBehaviour
    {
        public GhostState state = GhostState.Patroli;
        public string decision = "-";
        public bool canSee, frozen;
        public float stateTime;
        public List<Vector2Int> path = new List<Vector2Int>();
        public HidingSpot inspecting;
        public float lastCheckChance;
        HouseLayout L; HouseInstance H; PlayerController pl;
        int pathIdx; float repath, lost, linger, sawHideT = -99f;
        Vector2Int lastKnown; HidingSpot sawHideSpot; Vector3 lastSeenPos;
        readonly List<HidingSpot> searchQueue = new List<HidingSpot>();
        System.Random rng = new System.Random();
        GhostVisual vis; AudioSource voice;
        float graceUntil;

        public static GhostAI Create()
        {
            GameObject g = new GameObject("Ghost");
            g.layer = 2;
            GhostAI ai = g.AddComponent<GhostAI>();
            ai.vis = g.AddComponent<GhostVisual>();
            ai.vis.Build();
            ai.voice = g.AddComponent<AudioSource>();
            ai.voice.spatialBlend = 1f; ai.voice.loop = true; ai.voice.maxDistance = 18f; ai.voice.rolloffMode = AudioRolloffMode.Linear; ai.voice.volume = 0.8f;
            return ai;
        }

        public void Init(HouseLayout layout, HouseInstance house, PlayerController p)
        {
            L = layout; H = house; pl = p;
            transform.position = G.W(L.ghostStart);
            state = GhostState.Patroli; path.Clear(); searchQueue.Clear(); inspecting = null; stateTime = 0;
            graceUntil = Time.time + 6f;
            AudioClip w = AudioDirector.I.GhostLoop();
            if (w != null) { voice.clip = w; voice.Play(); }
            PickPatrol();
        }

        DDAManager D { get { return GameManager.I.dda; } }

        void SetState(GhostState s, string why)
        {
            if (s != state) { state = s; stateTime = 0f; }
            decision = why;
        }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || L == null || !gm.Playing || frozen) { vis.SetState(state, canSee); return; }
            float dt = Time.deltaTime;
            stateTime += dt;
            Perceive();

            switch (state)
            {
                case GhostState.Patroli:
                case GhostState.Hadang:
                    if (canSee && Time.time > graceUntil) { StartChase(); break; }
                    if (FollowPath(D.patrolSpeed))
                    {
                        linger -= dt;
                        if (linger <= 0f) PickPatrol();
                    }
                    break;
                case GhostState.Selidiki:
                    if (canSee) { StartChase(); break; }
                    if (FollowPath(D.patrolSpeed * 1.25f))
                    {
                        linger -= dt;
                        transform.Rotate(0, 90f * dt, 0);
                        if (linger <= 0f) BeginSearch(lastKnown, "Tidak ada apa-apa, mulai mencari sekitar");
                    }
                    break;
                case GhostState.Kejar:
                    if (pl.hidden)
                    {
                        BeginSearch(G.C(pl.spot.ExitPos), "Pemain hilang dari pandangan saat dikejar");
                        break;
                    }
                    if (canSee) { lost = 0f; lastKnown = pl.Cell; lastSeenPos = pl.transform.position; } else lost += dt;
                    repath -= dt;
                    if (repath <= 0f) { repath = 0.35f; SetPath(lastKnown); }
                    float dist = Flat(pl.transform.position - transform.position).magnitude;
                    Vector2Int myCell = G.C(transform.position);
                    if (dist < 3.2f && (canSee || myCell == pl.Cell)) MoveDirect(pl.transform.position, D.chaseSpeed);
                    else if (FollowPath(D.chaseSpeed)) MoveDirect(lastSeenPos, D.chaseSpeed);
                    if (dist < 1.25f && !pl.hidden) { GameManager.I.OnCaught("Hantu menangkapmu!"); return; }
                    if (lost > D.loseTime) BeginSearch(lastKnown, "Kehilangan jejak - cari di posisi terakhir");
                    break;
                case GhostState.Cari:
                    if (canSee) { StartChase(); break; }
                    if (stateTime > D.searchTime || searchQueue.Count == 0)
                    {
                        if (searchQueue.Count == 0 && stateTime < 1f) { PickPatrol(); break; }
                        if (stateTime > D.searchTime) { PickPatrol(); break; }
                    }
                    if (inspecting == null && searchQueue.Count > 0)
                    {
                        inspecting = searchQueue[0]; searchQueue.RemoveAt(0);
                        SetPath(inspecting.front);
                    }
                    if (inspecting != null && FollowPath(D.patrolSpeed * 1.2f))
                    {
                        SetState(GhostState.Periksa, "Memeriksa " + HouseGenerator.SpotName((int)inspecting.kind) + " (skor belajar " + GhostMemory.SpotScore(inspecting).ToString("0.00") + ")");
                        linger = 1.6f;
                        inspecting.Inspect(1.6f);
                        AudioDirector.I.Door();
                    }
                    break;
                case GhostState.Periksa:
                    if (inspecting != null) FaceTo(inspecting.transform.position, dt);
                    linger -= dt;
                    if (linger <= 0f)
                    {
                        HidingSpot s = inspecting; inspecting = null;
                        if (s != null && s.occupied && pl.hidden && pl.spot == s)
                        {
                            float ch = CheckChance(s);
                            lastCheckChance = ch;
                            if (Random.value < ch) { GameManager.I.OnCaught("Hantu sudah hafal tempat sembunyimu: " + HouseGenerator.SpotName((int)s.kind)); return; }
                            GhostMemory.Feed("Hantu memeriksa tempatmu tapi tidak melihatmu (peluang " + (ch * 100f).ToString("0") + "%)");
                        }
                        state = GhostState.Cari; decision = "Lanjut mencari";
                        if (searchQueue.Count == 0) PickPatrol();
                    }
                    break;
            }
            vis.SetState(state, canSee);
        }

        /// <summary>Peluang hantu menemukan pemain di tempat sembunyi = skor belajar x agresivitas DDA.</summary>
        public float CheckChance(HidingSpot s)
        {
            float baseC = 0.12f + 0.75f * GhostMemory.SpotScore(s);
            if (s == sawHideSpot && Time.time - sawHideT < 6f) baseC += 0.35f;
            return Mathf.Clamp(baseC * D.checkMul, 0.05f, 0.95f);
        }

        void Perceive()
        {
            canSee = false;
            if (pl == null || pl.hidden) return;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 to = pl.HeadPos - eye;
            float dist = to.magnitude;
            float range = D.visionRange * (pl.flashOn ? 1.3f : 1f) * (pl.crouch ? 0.7f : 1f);
            if (dist > range) return;
            float ang = Vector3.Angle(transform.forward, Flat(to));
            bool inCone = ang < D.fov * 0.5f || dist < 2.2f || (pl.flashOn && dist < range * 0.55f);
            if (!inCone) return;
            if (Physics.Linecast(eye, pl.HeadPos, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            canSee = true;
        }

        public void HearNoise(Vector3 pos, float radius)
        {
            if (L == null || state == GhostState.Kejar || state == GhostState.Periksa) return;
            float r = radius * D.hearingMult;
            if (Flat(pos - transform.position).magnitude > r) return;
            Vector2Int heard = G.C(pos);
            if (!L.In(heard)) return;
            bool same = state == GhostState.Selidiki && heard == lastKnown;
            lastKnown = heard;
            if (!same) SetPath(lastKnown);
            linger = 1.8f;
            SetState(GhostState.Selidiki, "Mendengar langkah kaki (radius " + r.ToString("0.0") + " m)");
            GameManager.I.Toast("Hantu mendengar sesuatu...");
        }

        /// <summary>Demo: hantu langsung melihat dan mengejar pemain.</summary>
        public void ForceChase()
        {
            graceUntil = 0f; frozen = false;
            StartChase();
        }
        public void Teleport(Vector3 p) { transform.position = new Vector3(p.x, 0, p.z); path.Clear(); }

        /// <summary>Demo (F6): hantu muncul di sel yang bisa dijangkau sekitar n langkah dari pemain lalu langsung mengejar.</summary>
        public void SummonAndChase(int n)
        {
            int[,] dist = L.Distances(pl.Cell);
            Vector2Int best = L.ghostStart; int bd = int.MaxValue;
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    int d = dist[x, y];
                    if (d < 0 || L.blocked[x, y]) continue;
                    int score = Mathf.Abs(d - n);
                    if (score < bd) { bd = score; best = new Vector2Int(x, y); }
                }
            Teleport(G.W(best));
            Vector3 look = pl.transform.position - transform.position; look.y = 0;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            ForceChase();
        }

        public void OnPlayerHid(HidingSpot s)
        {
            if (canSee || state == GhostState.Kejar) { sawHideSpot = s; sawHideT = Time.time; }
        }

        void StartChase()
        {
            if (state != GhostState.Kejar) { AudioDirector.I.Scare(); GameManager.I.dda.OnChaseStart(); }
            lastKnown = pl.Cell; lastSeenPos = pl.transform.position; lost = 0f; repath = 0f;
            SetState(GhostState.Kejar, "Melihat pemain -> KEJAR");
        }

        void BeginSearch(Vector2Int around, string why)
        {
            searchQueue.Clear(); inspecting = null;
            // tempat sembunyi dekat lokasi terakhir, diurutkan dari skor belajar + jarak
            List<(float, HidingSpot)> c = new List<(float, HidingSpot)>();
            foreach (HidingSpot s in H.spots)
            {
                int d = Mathf.Abs(s.front.x - around.x) + Mathf.Abs(s.front.y - around.y);
                if (d > 7) continue;
                float score = GhostMemory.SpotScore(s) * D.learnWeight * 3f - d * 0.15f + (s == sawHideSpot && Time.time - sawHideT < 6f ? 2f : 0f);
                c.Add((score, s));
            }
            c.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            for (int i = 0; i < c.Count && i < 3; i++) searchQueue.Add(c[i].Item2);
            SetState(GhostState.Cari, why + " | periksa " + searchQueue.Count + " tempat sembunyi (urut skor belajar)");
            if (searchQueue.Count == 0) { SetPath(around); }
        }

        void PickPatrol()
        {
            List<Vector2Int> hot = GhostMemory.HotCells(8);
            if (hot.Count > 0 && Random.value < D.learnWeight)
            {
                Vector2Int t = hot[Random.Range(0, hot.Count)];
                SetPath(t); linger = 4f;
                SetState(GhostState.Hadang, "Menghadang di jalur yang sering kamu lewati (sel " + t.x + "," + t.y + ")");
                return;
            }
            Room r = L.rooms[Random.Range(0, L.rooms.Count)];
            Vector2Int c = new Vector2Int(Random.Range(r.rect.xMin, r.rect.xMax), Random.Range(r.rect.yMin, r.rect.yMax));
            for (int i = 0; i < 10 && L.blocked[c.x, c.y]; i++) c = new Vector2Int(Random.Range(r.rect.xMin, r.rect.xMax), Random.Range(r.rect.yMin, r.rect.yMax));
            SetPath(c); linger = Random.Range(0.8f, 2f);
            SetState(GhostState.Patroli, "Patroli acak ke " + HouseGenerator.RoomName(r.type));
        }

        void SetPath(Vector2Int target)
        {
            Vector2Int from = G.C(transform.position);
            if (!L.In(from)) from = L.ghostStart;
            if (L.blocked[from.x, from.y])
            {
                for (int k = 0; k < 4; k++) { Vector2Int n = from + G.Dirs[k]; if (L.In(n) && !L.blocked[n.x, n.y]) { from = n; break; } }
            }
            path = L.Path(from, target);
            // sel pertama = sel tempat hantu berdiri; dilewati agar hantu tidak mundur ke tengah sel tiap kali jalur dihitung ulang
            pathIdx = path.Count > 1 ? 1 : 0;
        }

        /// <summary>true bila sudah sampai ujung jalur.</summary>
        bool FollowPath(float speed)
        {
            if (path == null || pathIdx >= path.Count) return true;
            Vector3 tgt = G.W(path[pathIdx]);
            if (MoveDirect(tgt, speed)) pathIdx++;
            return pathIdx >= path.Count;
        }

        bool MoveDirect(Vector3 tgt, float speed)
        {
            Vector3 d = Flat(tgt - transform.position);
            float step = speed * Time.deltaTime;
            if (d.magnitude <= step + 0.05f) { transform.position = new Vector3(tgt.x, 0, tgt.z); return true; }
            transform.position += d.normalized * step;
            FaceTo(transform.position + d, Time.deltaTime);
            return false;
        }

        void FaceTo(Vector3 p, float dt)
        {
            Vector3 d = Flat(p - transform.position);
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 6f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }

    /// <summary>Wujud hantu: model Kenney Graveyard Kit (character-ghost), melayang + mata menyala sesuai status.</summary>
    public class GhostVisual : MonoBehaviour
    {
        Transform body; Light aura; Material mat; Renderer[] eyes = new Renderer[2]; Material eyeMat;
        const float KS = 2.5f;

        public void Build()
        {
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            Texture2D tex = Resources.Load<Texture2D>("Kenney/ghost_colormap");
            mat = new Material(Look.Fade("ghostFade", new Color(0.95f, 0.97f, 1f, 0.85f), tex)) { name = "Ghost" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", new Color(0.25f, 0.3f, 0.4f));
            bool ok = Part("torso", new Vector3(0, 0.223f * KS + 0.15f, 0));
            Part("arm_left", new Vector3(-0.125f * KS, 0.373f * KS + 0.15f, 0.0025f * KS));
            Part("arm_right", new Vector3(0.125f * KS, 0.373f * KS + 0.15f, 0.0025f * KS));
            if (!ok)
            {
                GameObject c = HouseBuilder.LPrim(PrimitiveType.Capsule, body, new Vector3(0, 1.2f, 0), new Vector3(0.9f, 0.9f, 0.9f), mat, "Shroud");
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            eyeMat = new Material(Look.Emissive("ghostEye", Color.white, 3f));
            float ey = (0.223f + 0.36f) * KS + 0.15f, ez = 0.2f * KS;
            for (int s = 0; s < 2; s++)
            {
                GameObject e = HouseBuilder.LBox(body, new Vector3((s == 0 ? -0.07f : 0.07f) * KS, ey, ez), new Vector3(0.13f, 0.15f, 0.02f), eyeMat, "Eye");
                eyes[s] = e.GetComponent<Renderer>();
            }
            GameObject lg = new GameObject("Aura"); lg.transform.SetParent(body, false); lg.transform.localPosition = new Vector3(0, 1.4f, 0.6f);
            aura = lg.AddComponent<Light>(); aura.type = LightType.Point; aura.range = 5f; aura.intensity = 1.4f; aura.color = new Color(0.5f, 0.7f, 1f);
        }

        bool Part(string name, Vector3 pos)
        {
            GameObject src = Resources.Load<GameObject>("Kenney/ghost_" + name);
            if (src == null) return false;
            MeshFilter mf = src.GetComponentInChildren<MeshFilter>(true);
            if (mf == null) return false;
            GameObject g = new GameObject("Kenney_" + name);
            g.transform.SetParent(body, false); g.transform.localPosition = pos; g.transform.localScale = Vector3.one * KS;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        public void SetState(GhostState s, bool see)
        {
            Color c = s == GhostState.Kejar ? new Color(1f, 0.15f, 0.1f) : s == GhostState.Hadang ? new Color(0.8f, 0.3f, 1f) :
                      (s == GhostState.Cari || s == GhostState.Periksa || s == GhostState.Selidiki) ? new Color(1f, 0.7f, 0.15f) : new Color(0.55f, 0.75f, 1f);
            eyeMat.SetColor("_EmissionColor", c * 4f);
            aura.color = Color.Lerp(aura.color, c, Time.deltaTime * 4f);
            body.localPosition = new Vector3(0, Mathf.Sin(Time.time * 2f) * 0.12f + 0.1f, 0);
            body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f) * 3f, 0, Mathf.Sin(Time.time * 1.7f) * 4f);
        }
    }
}
