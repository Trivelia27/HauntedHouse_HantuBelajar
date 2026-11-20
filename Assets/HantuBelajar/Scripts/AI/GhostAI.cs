using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    public enum GhostState { Patroli, Hadang, Selidiki, Kejar, Cari, Periksa, Muncul, Jaga }

    /// <summary>
    /// Hantu = Finite State Machine. Keputusan di tiap state memakai data belajar (GhostMemory) dan parameter DDA.
    /// Gerak memakai A* pada graf node rumah (termasuk tangga); hantu menembus pintu terkunci & perabot.
    /// </summary>
    public class GhostAI : MonoBehaviour
    {
        public GhostState state = GhostState.Patroli;
        public string decision = "-";
        public bool canSee, frozen, heardRecently;
        public float stateTime, unseenTime, lastCheckChance = -1f;
        public List<Vector3> path = new List<Vector3>();
        public HidingSpot inspecting;
        public string searchInfo = "";
        public int catches, checksMade;
        public bool holdSleep;          // panduan awal: hantu tetap tidur sampai pemain siap
        HidingSpot guardSpot; float guardT;

        HouseLayout L; HouseInstance H; PlayerController pl;
        int pathIdx;
        Vector3Int lastKnownNode, targetNode;
        Vector3 lastSeenPos, stuckPos;
        float lost, linger, repath, graceUntil, stuckT, fadeT, wanderT, heardT, catchT, windupT;
        readonly List<HidingSpot> queue = new List<HidingSpot>();
        readonly Dictionary<int, float> roomVisit = new Dictionary<int, float>();
        GhostVisual vis; AudioSource moveSrc, whisper;
        Vector3 lastFramePos;
        bool fadingOut, waking;

        public DdaMap.GhostParams D { get { return GameManager.I.dda.gp; } }
        public Vector3 FacePos { get { return transform.position + Vector3.up * 1.55f; } }
        public float DistanceTo(Vector3 p) { return Vector3.Distance(transform.position, p); }

        public static GhostAI Create()
        {
            GameObject g = new GameObject("Ghost");
            g.layer = 2;
            GhostAI ai = g.AddComponent<GhostAI>();
            ai.vis = g.AddComponent<GhostVisual>();
            ai.vis.Build();
            ai.moveSrc = g.AddComponent<AudioSource>();
            ai.moveSrc.spatialBlend = 1f; ai.moveSrc.loop = true; ai.moveSrc.minDistance = 1.5f; ai.moveSrc.maxDistance = 16f; ai.moveSrc.rolloffMode = AudioRolloffMode.Linear; ai.moveSrc.volume = 0f;
            ai.moveSrc.clip = AssetLibrary.Clip("ghost_move");
            ai.whisper = g.AddComponent<AudioSource>();
            ai.whisper.spatialBlend = 1f; ai.whisper.loop = true; ai.whisper.minDistance = 1f; ai.whisper.maxDistance = 11f; ai.whisper.rolloffMode = AudioRolloffMode.Linear; ai.whisper.volume = 0f;
            ai.whisper.clip = AudioDirector.WhisperClip();
            return ai;
        }

        public void Init(HouseLayout layout, HouseInstance house, PlayerController p)
        {
            L = layout; H = house; pl = p;
            transform.position = G.W(L.ghostStart);
            transform.rotation = Quaternion.identity;
            state = GhostState.Patroli; path.Clear(); queue.Clear(); inspecting = null; stateTime = 0f; unseenTime = 0f;
            roomVisit.Clear(); frozen = false; fadingOut = false; fadeT = 0f; vis.SetAlpha(1f);
            graceUntil = Time.time + D.awakeDelay; lastCheckChance = -1f; searchInfo = ""; checksMade = 0; catchT = 0f;
            lastFramePos = transform.position; stuckPos = transform.position; stuckT = 0f;
            if (moveSrc.clip != null && !moveSrc.isPlaying) moveSrc.Play();
            if (whisper.clip != null && !whisper.isPlaying) whisper.Play();
            vis.SetAlpha(0f); asleep = true; holdSleep = false; guardSpot = null;
            decision = "Tertidur... bangun dalam " + D.awakeDelay.ToString("0") + " dtk";
        }
        bool asleep;

        public static bool LogStates;
        void SetState(GhostState s, string why)
        {
            if (s != state)
            {
                if (LogStates && GameManager.I != null) Debug.Log("[Ghost] t=" + GameManager.I.houseTime.ToString("0.0") + " " + state + " -> " + s + " | " + why + " | jarak " + DistanceTo(pl.transform.position).ToString("0.0") + " m");
                state = s; stateTime = 0f;
            }
            decision = why;
        }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || L == null || pl == null) return;
            float dt = Time.deltaTime;
            float speed01 = 0f;
            Lamp.GhostActive = gm.Playing && !frozen;
            Lamp.GhostPos = transform.position;
            if (!gm.Playing || frozen)
            {
                vis.Tick(state, canSee, 0f, dt);
                moveSrc.volume = Mathf.MoveTowards(moveSrc.volume, 0f, dt);
                whisper.volume = Mathf.MoveTowards(whisper.volume, 0f, dt);
                return;
            }
            if (asleep)
            {
                // fase tidur: tidak bergerak, tidak melihat, tidak menangkap
                vis.SetAlpha(0f); vis.Tick(state, false, 0f, dt);
                moveSrc.volume = 0f; whisper.volume = 0f;
                if (holdSleep) graceUntil = Mathf.Max(graceUntil, Time.time + Mathf.Min(D.awakeDelay, 10f));
                if (Time.time >= graceUntil)
                {
                    asleep = false; unseenTime = 0f;
                    state = GhostState.Muncul; fadingOut = false; fadeT = 0f; waking = true;
                    decision = "Terbangun";
                    GameManager.I.Toast("Kamu merasa ada yang terbangun di rumah ini...", null, "ghost", UIKit.Danger);
                }
                return;
            }
            stateTime += dt;
            heardT -= dt; heardRecently = heardT > 0f;
            Perceive();
            if (canSee) { unseenTime = 0f; lastSeenPos = pl.transform.position; lastKnownNode = pl.node; }
            else unseenTime = pl.hidden ? Mathf.Min(unseenTime + dt, D.pressureTime * 0.5f) : unseenTime + dt;   // saat pemain bersembunyi hantu tidak "merasakan" apa pun

            switch (state)
            {
                case GhostState.Patroli:
                case GhostState.Hadang:
                    if (canSee) { StartChase(); break; }
                    if (unseenTime > D.pressureTime && !pl.hidden && PathDistToPlayer() > 9) { BeginAppear(); break; }
                    if (Follow(D.patrolSpeed))
                    {
                        linger -= dt;
                        LookAround(dt);
                        if (linger <= 0f) PickPatrol();
                    }
                    break;
                case GhostState.Selidiki:
                    if (canSee) { StartChase(); break; }
                    if (Follow(D.patrolSpeed * 1.3f))
                    {
                        linger -= dt;
                        LookAround(dt);
                        if (linger <= 0f) BeginSearch(lastKnownNode, "Tidak terlihat apa-apa, mulai menggeledah", false);
                    }
                    break;
                case GhostState.Kejar:
                    {
                        // pemain yang bersembunyi hanya "hilang dari pandangan": hantu tidak diberi tahu, ia menuju posisi terakhir pemain terlihat
                        if (windupT > 0f)
                        {
                            // jeda menjerit: menatap pemain, belum bergerak
                            windupT -= dt;
                            FaceTo(canSee ? pl.transform.position : lastSeenPos, dt * 2f);
                            break;
                        }
                        if (!canSee) lost += dt; else lost = 0f;
                        repath -= dt;
                        if (repath <= 0f) { repath = 0.35f; SetPathTo(canSee ? pl.node : lastKnownNode); }
                        float flat = G.Flat(pl.transform.position - transform.position).magnitude;
                        bool sameNode = !pl.hidden && L.NodeAt(transform.position) == pl.node;
                        if (flat < 3.4f && (canSee || sameNode)) MoveDirect(pl.transform.position, D.chaseSpeed);
                        else if (Follow(D.chaseSpeed) && !canSee) MoveDirect(lastSeenPos, D.chaseSpeed);
                        speed01 = 1f;
                        if (lost > D.loseTime) BeginSearch(lastKnownNode, "Kehilangan jejak - menggeledah sekitar posisi terakhir", false);
                        break;
                    }
                case GhostState.Cari:
                    if (canSee) { StartChase(); break; }
                    if (inspecting == null && queue.Count > 0)
                    {
                        inspecting = queue[0]; queue.RemoveAt(0);
                        SetPathTo(inspecting.frontNode);
                    }
                    if (inspecting != null)
                    {
                        if (Follow(D.patrolSpeed * 1.35f))
                        {
                            SetState(GhostState.Periksa, "Memeriksa " + inspecting.KindName + " (E[theta] = " + GhostMemory.Mean(inspecting).ToString("0.00") + ")");
                            linger = 1.7f;
                            inspecting.Inspect(1.7f);
                            AudioDirector.I.Creak(inspecting.transform.position);
                        }
                    }
                    else
                    {
                        wanderT -= dt;
                        if (Follow(D.patrolSpeed) || wanderT <= 0f)
                        {
                            if (stateTime > D.searchTime) { AfterSearch(); break; }
                            WanderNear(lastKnownNode, 2);
                        }
                    }
                    break;
                case GhostState.Periksa:
                    if (canSee && !pl.hidden) { inspecting = null; StartChase(); break; }
                    if (inspecting != null) FaceTo(inspecting.transform.position, dt);
                    linger -= dt;
                    if (linger <= 0f) FinishInspect();
                    break;
                case GhostState.Jaga:
                    if (canSee) { guardSpot = null; StartChase(); break; }
                    if (Follow(D.patrolSpeed))
                    {
                        guardT -= dt;
                        if (guardSpot != null)
                        {
                            Vector3 to = G.Flat(guardSpot.exitPos - transform.position);
                            if (to.sqrMagnitude > 0.01f) FaceTo(transform.position + Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.8f) * 35f, 0f) * to, dt);
                        }
                        else LookAround(dt);
                        if (guardT <= 0f) { guardSpot = null; PickPatrol(); }
                    }
                    break;
                case GhostState.Muncul:
                    fadeT += dt;
                    if (fadingOut)
                    {
                        vis.SetAlpha(1f - fadeT / 0.7f);
                        if (fadeT >= 0.7f) { Reappear(); fadingOut = false; fadeT = 0f; }
                    }
                    else
                    {
                        vis.SetAlpha(fadeT / 0.9f);
                        if (fadeT >= 0.9f) { vis.SetAlpha(1f); unseenTime = 0f; bool w = waking; waking = false; PickPatrol(!w); }
                    }
                    break;
            }
            if (state != GhostState.Kejar) speed01 = state == GhostState.Periksa || state == GhostState.Muncul ? 0f : 0.45f;

            // tangkap
            if (state != GhostState.Muncul && !pl.hidden && !pl.inputLocked)
            {
                Vector3 d = pl.transform.position - transform.position;
                bool touching = Mathf.Abs(d.y) < 1.3f && G.Flat(d).magnitude < 1.15f;
                catchT = touching ? catchT + dt : Mathf.Max(0f, catchT - dt * 2f);
                if (touching && catchT >= D.catchHold)
                {
                    if (LogStates) Debug.Log("[Ghost] t=" + gm.houseTime.ToString("0.0") + " TANGKAP saat state " + state + ", lihat " + canSee + ", stamina pemain " + pl.stamina.ToString("0") + ", lari " + pl.running);
                    gm.BeginCatch(this, "Hantu menangkapmu!"); return;
                }
            }

            // macet? (mis. jalur tidak valid) -> muncul di tempat lain
            stuckT += dt;
            if (stuckT > 4f)
            {
                bool shouldMove = state == GhostState.Kejar || (state != GhostState.Periksa && state != GhostState.Muncul && pathIdx < path.Count);
                if (shouldMove && (transform.position - stuckPos).magnitude < 0.4f) { SetPathTo(state == GhostState.Kejar ? lastKnownNode : targetNode); if (pathIdx >= path.Count) BeginAppear(); }
                stuckPos = transform.position; stuckT = 0f;
            }

            float spd = (transform.position - lastFramePos).magnitude / Mathf.Max(1e-4f, dt);
            lastFramePos = transform.position;
            vis.Tick(state, canSee, Mathf.Clamp01(spd / 5f), dt);
            float vol = state == GhostState.Kejar ? 0.75f : 0.32f;
            moveSrc.volume = Mathf.MoveTowards(moveSrc.volume, vol * Settings.Sfx * vis.Alpha, dt);
            whisper.volume = Mathf.MoveTowards(whisper.volume, (state == GhostState.Kejar ? 0.15f : 0.4f) * Settings.Sfx * vis.Alpha, dt * 0.5f);
            _ = speed01;
        }

        // ------------------------------------------------------------------ persepsi
        void Perceive()
        {
            canSee = false;
            if (pl.hidden || pl.inputLocked || Time.time < graceUntil || vis.Alpha < 0.5f) return;
            Vector3 eye = FacePos;
            Vector3 head = pl.HeadPos;
            Vector3 to = head - eye;
            float dist = to.magnitude;
            Room pr = L.RoomAt(pl.node);
            bool lightOn = pl.flashOn && pl.battery > 0f;
            bool dark = pr != null && !pr.lit && !lightOn;
            float range = D.visionRange * (lightOn ? 1.2f : dark ? 0.6f : 1f) * (pl.crouch ? 0.7f : 1f);
            if (dist > range) return;
            float ang = Vector3.Angle(transform.forward, G.Flat(to));
            bool inCone = ang < D.fov * 0.5f || dist < 2.0f;
            bool beam = lightOn && dist < 9f && ang < 140f && Vector3.Angle(pl.cam.transform.forward, eye - head) < pl.flash.spotAngle * 0.4f;
            if (!inCone && !beam) return;
            if (Physics.Linecast(eye, head, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            canSee = true;
        }

        public void HearNoise(Vector3 pos, float radius)
        {
            if (L == null || frozen || asleep || radius <= 0f || state == GhostState.Kejar || state == GhostState.Periksa || state == GhostState.Muncul) return;
            if (Time.time < graceUntil) return;
            float r = radius * D.hearingMult;
            float dist = Vector3.Distance(pos, transform.position);
            if (Mathf.Abs(pos.y - transform.position.y) > 1.5f) r *= 0.4f;
            if (dist > r) return;
            Vector3Int heard = L.NodeAt(pos);
            bool same = state == GhostState.Selidiki && heard == lastKnownNode;
            lastKnownNode = heard;
            heardT = 2f;
            if (!same)
            {
                SetPathTo(heard);
                linger = 2.0f;
                SetState(GhostState.Selidiki, "Mendengar langkah kaki (" + dist.ToString("0") + " m, radius " + r.ToString("0.0") + " m)");
                // pemberitahuan "hantu mendengar sesuatu" ditampilkan HUD sebagai indikator kewaspadaan (tidak berulang tiap langkah)
            }
        }

        public void OnPlayerHid(HidingSpot s)
        {
            // sengaja kosong: hantu TIDAK diberi tahu pemain bersembunyi (ia hanya kehilangan pandangan)
        }

        // ------------------------------------------------------------------ keputusan
        void StartChase()
        {
            if (state != GhostState.Kejar) { AudioDirector.I.Stinger(); GameManager.I.dda.OnChaseStart(); windupT = D.chaseWindup; }
            lastKnownNode = pl.node; lastSeenPos = pl.transform.position; lost = 0f; repath = 0f;
            SetState(GhostState.Kejar, "Melihat pemain -> KEJAR");
        }

        /// <summary>Pilih tempat sembunyi untuk diperiksa: Thompson sampling theta~Beta(alfa,beta), dikurangi jarak.</summary>
        public Vector3Int searchCenter;
        public HidingSpot NextToInspect { get { return inspecting != null ? inspecting : (queue.Count > 0 ? queue[0] : null); } }

        void BeginSearch(Vector3Int around, string why, bool sawEnter)
        {
            queue.Clear(); inspecting = null;
            searchCenter = around;
            int[] dist = L.Distances(around, true, true);
            List<(float, HidingSpot, float, float)> c = new List<(float, HidingSpot, float, float)>();
            foreach (HidingSpot s in H.spots)
            {
                int d = L.Dist(dist, s.frontNode);
                if (d < 0 || d > 6) continue;
                float theta = GhostMemory.Sample(s);
                float score = theta * (0.5f + D.learnWeight) - d * 0.07f;
                c.Add((score, s, theta, d));
            }
            c.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            int n = Mathf.Min(c.Count, 2 + Mathf.RoundToInt(D.learnWeight * 2f));
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                queue.Add(c[i].Item2);
                sb.Append(c[i].Item2.KindName + " #" + c[i].Item2.id + " theta=" + c[i].Item3.ToString("0.00") + (i < n - 1 ? ", " : ""));
            }
            searchInfo = n > 0 ? "Sampel Thompson -> " + sb : "Tidak ada tempat sembunyi dekat";
            lastKnownNode = around;
            wanderT = 0f;
            SetState(GhostState.Cari, why + " | periksa " + n + " tempat");
            if (queue.Count == 0) SetPathTo(around);
        }

        void FinishInspect()
        {
            HidingSpot s = inspecting; inspecting = null;
            checksMade++;
            if (s != null)
            {
                // hantu hanya memeriksa dari luar dan tidak pernah menemukan pemain di dalam; menurutnya tempat itu kosong
                lastCheckChance = GhostMemory.Mean(s);
                GhostMemory.RecordCheck(s, false);
            }
            SetState(GhostState.Cari, "Lanjut menggeledah");
            if (queue.Count == 0) wanderT = 0f;
        }

        /// <summary>
        /// Selesai menggeledah tanpa hasil. Dengan peluang = bobot belajar (DDA), hantu BERJAGA dekat tempat sembunyi yang paling
        /// dicurigai di sekitar lokasi terakhir pemain (Thompson sampling dari kebiasaan) - bukan karena tahu pemain ada di sana.
        /// </summary>
        void AfterSearch()
        {
            if (Random.value < D.learnWeight)
            {
                HidingSpot s = PickGuardSpot(searchCenter, 6);
                if (s != null) { BeginGuard(s, "Curiga kamu bersembunyi di sekitar sini -> berjaga dekat " + s.KindName + " #" + s.id + " (E[theta] = " + GhostMemory.Mean(s).ToString("0.00") + ")"); return; }
            }
            PickPatrol();
        }

        HidingSpot PickGuardSpot(Vector3Int around, int radius)
        {
            int[] dist = radius > 0 ? L.Distances(around, true, true) : null;
            HidingSpot best = null; float bs = float.MinValue;
            foreach (HidingSpot s in H.spots)
            {
                int d = dist != null ? L.Dist(dist, s.frontNode) : 0;
                if (dist != null && (d < 0 || d > radius)) continue;
                float sc = GhostMemory.Sample(s) - d * 0.03f;
                if (sc > bs) { bs = sc; best = s; }
            }
            return best;
        }

        void BeginGuard(HidingSpot s, string why)
        {
            guardSpot = s; guardT = D.guardTime;
            SetPathTo(GuardNode(s));
            SetState(GhostState.Jaga, why);
        }

        /// <summary>Posisi jaga: 1-2 langkah dari depan tempat sembunyi, di lantai yang sama.</summary>
        Vector3Int GuardNode(HidingSpot s)
        {
            int[] d = L.Distances(s.frontNode, true, true);
            Vector3Int best = s.frontNode; int bestScore = int.MinValue;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] < 1 || d[i] > 2) continue;
                Vector3Int n = L.FromIdx(i);
                if (L.Blocked(n) || n.z != s.floor) continue;
                int score = d[i] * 10 + Random.Range(0, 6);
                if (score > bestScore) { bestScore = score; best = n; }
            }
            return best;
        }

        /// <summary>Kecurigaan hantu pada tempat ini = E[theta] dari kebiasaan pemain (untuk panel presentasi; hantu tidak pernah menemukan pemain yang bersembunyi).</summary>
        public float CheckChance(HidingSpot s) { return GhostMemory.Mean(s); }

        void PickPatrol(bool towardPlayer = false)
        {
            float r = Random.value;
            if (!towardPlayer && r > D.explore && Random.value < D.learnWeight && GhostMemory.PickAmbush(L, Mathf.Lerp(0.6f, 0.15f, D.learnWeight), out Vector3Int amb, out float share))
            {
                SetPathTo(amb); linger = Random.Range(4f, 7f);
                SetState(GhostState.Hadang, "Menghadang di jalur favoritmu (" + L.FloorName(amb.z) + ", panas " + (share * 100f).ToString("0") + "%)");
                return;
            }
            if (!towardPlayer && Random.value < D.learnWeight * 0.3f)
            {
                // berjaga dekat tempat sembunyi favorit (kebiasaan lintas rumah + rumah ini)
                HidingSpot fav = PickGuardSpot(default, 0);
                if (fav != null && GhostMemory.Mean(fav) >= 0.45f) { BeginGuard(fav, "Berjaga dekat tempat sembunyi favoritmu: " + fav.KindName + " #" + fav.id + " (E[theta] = " + GhostMemory.Mean(fav).ToString("0.00") + ")"); return; }
            }
            if (towardPlayer && !pl.hidden)
            {
                WanderNear(pl.node, 3);
                linger = 2f;
                SetState(GhostState.Patroli, "Merasakan kehadiranmu di dekat sini");
                return;
            }
            // patroli: ruangan yang paling lama tidak dikunjungi (dengan sedikit acak)
            Room best = null; float bs = float.MaxValue;
            foreach (Room rm in L.rooms)
            {
                roomVisit.TryGetValue(rm.id, out float t);
                float s = t + Random.value * 25f;
                if (s < bs && rm.Area > 0) { bs = s; best = rm; }
            }
            if (best == null) best = L.rooms[0];
            roomVisit[best.id] = Time.time;
            Vector3Int target = RandomFree(best);
            SetPathTo(target); linger = Random.Range(1f, 2.5f);
            SetState(GhostState.Patroli, "Patroli ke " + HouseGenerator.RoomName(best.type) + " (" + L.FloorName(best.floor) + ")");
        }

        Vector3Int RandomFree(Room r)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2Int c = new Vector2Int(Random.Range(r.rect.xMin, r.rect.xMax), Random.Range(r.rect.yMin, r.rect.yMax));
                Vector3Int n = G.N(r.floor, c);
                if (!L.Blocked(n)) return n;
            }
            foreach (Vector2Int c in r.rect.allPositionsWithin) { Vector3Int n = G.N(r.floor, c); if (!L.Blocked(n)) return n; }
            return L.ghostStart;
        }

        void WanderNear(Vector3Int center, int radius)
        {
            int[] d = L.Distances(center, true, true);
            List<Vector3Int> c = new List<Vector3Int>();
            for (int i = 0; i < d.Length; i++) if (d[i] >= 0 && d[i] <= radius && !L.Blocked(L.FromIdx(i))) c.Add(L.FromIdx(i));
            if (c.Count == 0) c.Add(center);
            SetPathTo(c[Random.Range(0, c.Count)]);
            wanderT = 6f;
        }

        // ------------------------------------------------------------------ muncul (teleport)
        void BeginAppear()
        {
            fadingOut = true; fadeT = 0f;
            SetState(GhostState.Muncul, "Lama tidak bertemu pemain -> menghilang dan muncul lebih dekat");
            path.Clear(); pathIdx = 0;
        }

        void Reappear()
        {
            int[] d = L.Distances(pl.hidden ? lastKnownNode : pl.node, true, true);
            List<Vector3Int> c = new List<Vector3Int>();
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] < 5 || d[i] > 9) continue;
                Vector3Int n = L.FromIdx(i);
                if (L.Blocked(n)) continue;
                Vector3 p = G.W(n) + Vector3.up * 1.5f;
                if (!Physics.Linecast(pl.HeadPos, p, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;   // jangan muncul di depan mata
                c.Add(n);
            }
            Vector3Int pick = c.Count > 0 ? c[Random.Range(0, c.Count)] : L.ghostStart;
            transform.position = G.W(pick);
            lastFramePos = transform.position; stuckPos = transform.position;
            path.Clear(); pathIdx = 0;
        }

        /// <summary>Demo (F6): muncul di node sejauh +-n langkah dari pemain lalu langsung mengejar.</summary>
        public void SummonAndChase(int n)
        {
            int[] d = L.Distances(pl.node, true, true);
            Vector3Int best = L.ghostStart; int bd = int.MaxValue;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] < 0) continue;
                Vector3Int node = L.FromIdx(i);
                if (L.Blocked(node)) continue;
                int sc = Mathf.Abs(d[i] - n);
                if (sc < bd) { bd = sc; best = node; }
            }
            transform.position = G.W(best);
            lastFramePos = transform.position;
            Vector3 look = G.Flat(pl.transform.position - transform.position);
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            frozen = false; asleep = false; graceUntil = 0f; vis.SetAlpha(1f);
            StartChase();
            windupT = 0.6f;
        }

        public void Teleport(Vector3 p) { transform.position = p; lastFramePos = p; path.Clear(); pathIdx = 0; }
        public void ForceChase() { frozen = false; asleep = false; graceUntil = 0f; vis.SetAlpha(1f); StartChase(); }
        public void WakeForTest() { asleep = false; graceUntil = 0f; waking = false; fadingOut = false; vis.SetAlpha(1f); SetState(GhostState.Patroli, "Uji: bangun"); }
        public void GuardForTest(HidingSpot s) { BeginGuard(s, "Uji: berjaga dekat tempat sembunyi"); }
        public void InspectForTest(HidingSpot s) { queue.Clear(); inspecting = null; queue.Add(s); SetState(GhostState.Cari, "Uji: memeriksa tempat sembunyi tertentu"); }
        public bool Asleep { get { return asleep; } }

        // ------------------------------------------------------------------ gerak
        int PathDistToPlayer()
        {
            int[] d = L.Distances(L.NodeAt(transform.position), true, true);
            int v = L.Dist(d, pl.node);
            return v < 0 ? 99 : v;
        }

        void SetPathTo(Vector3Int target)
        {
            targetNode = target;
            Vector3Int from = L.NodeAt(transform.position);
            List<Vector3Int> nodes = L.Path(from, target, true, true);
            path = L.Waypoints(nodes);
            // titik pertama = tengah sel tempat berdiri -> dilewati agar tidak mundur
            pathIdx = path.Count > 1 ? 1 : 0;
            if (path.Count > 1)
            {
                Vector3 a = path[0], b = path[1];
                Vector3 me = transform.position;
                if (Vector3.Dot(b - a, me - a) < 0f && (me - a).magnitude > 0.3f) pathIdx = 0;
            }
        }

        /// <summary>true bila sudah sampai ujung jalur.</summary>
        bool Follow(float speed)
        {
            if (path == null || pathIdx >= path.Count) return true;
            if (MoveDirect(path[pathIdx], speed)) pathIdx++;
            return pathIdx >= path.Count;
        }

        bool MoveDirect(Vector3 tgt, float speed)
        {
            Vector3 d = tgt - transform.position;
            float step = speed * Time.deltaTime;
            if (d.magnitude <= step + 0.05f) { transform.position = tgt; return true; }
            transform.position += d.normalized * step;
            FaceTo(transform.position + d, Time.deltaTime);
            return false;
        }

        void FaceTo(Vector3 p, float dt)
        {
            Vector3 d = G.Flat(p - transform.position);
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 6f));
        }

        void LookAround(float dt) { transform.Rotate(0f, Mathf.Sin(Time.time * 0.9f) * 70f * dt, 0f); }

        // ------------------------------------------------------------------ adegan tertangkap
        public void PlayCatch(PlayerController p, float t)
        {
            frozen = true;
            Vector3 cam = p.cam.transform.position;
            Vector3 fwd = G.Flat(p.cam.transform.forward).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 target = cam + fwd * Mathf.Lerp(1.8f, 1.05f, Mathf.Clamp01(t / 0.3f)) - Vector3.up * 1.62f;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            transform.rotation = Quaternion.LookRotation(-fwd);
            vis.SetAlpha(1f);
            vis.Tick(GhostState.Kejar, true, 1f, Time.unscaledDeltaTime);
            vis.Lunge(t);
        }
    }
}
