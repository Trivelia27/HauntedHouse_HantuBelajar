using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Bot pemain untuk eksperimen DDA, uji otomatis, dan rekaman demo. Tiga tingkat keterampilan:
    /// Pro   : cepat sadar ancaman & langsung bereaksi, berlari saat hantu jauh (menyisakan stamina), mengendap saat hantu dekat tapi membelakangi,
    ///         tempat sembunyi aman yang jarang dicurigai.
    /// Sedang: sadar ancaman lebih dekat & bereaksi 0,45 dtk, kadang ragu / salah jalan, berlari saat hantu jauh, tempat sembunyi terdekat yang aman.
    /// Lemah : telat sadar & bereaksi 0,8 dtk, berjalan sambil ragu / kadang salah jalan, panik berlari (berisik) saat hantu dekat,
    ///         kembali ke tempat sembunyi favorit walau berisiko dan keluar terlalu cepat.
    /// Opsi uji belajar: habitOn (kebiasaan memilih jenis tempat sembunyi) atau randomHide (pilihan acak).
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public enum Profile { Pro, Sedang, Lemah }
        public Profile profile = Profile.Sedang;
        public string mode = "-";
        public int hides, flees, stuckEvents, uses;
        public bool allowHide = true;
        // uji "hantu belajar": kebiasaan memilih jenis tempat sembunyi, atau memilih acak sama sekali
        public bool habitOn, randomHide; public SpotKind habitKind = SpotKind.Lemari; public float habitStrength = 0.85f;

        struct Skill { public float chaseRange, seeRange, react, hideWait, safeDist, guardDist, maxHide, detour, pause; public bool runFar, crouchNear, panicRun, riskyHide; }

        static Skill SkillOf(Profile p)
        {
            switch (p)
            {
                case Profile.Pro: return new Skill { chaseRange = 18f, seeRange = 11f, react = 0f, hideWait = 2.5f, safeDist = 7f, guardDist = 10f, maxHide = 20f, runFar = true, crouchNear = true };
                case Profile.Lemah: return new Skill { chaseRange = 10f, seeRange = 6f, react = 0.8f, hideWait = 1.4f, safeDist = 4f, guardDist = 5f, maxHide = 10f, detour = 0.35f, pause = 0.12f, panicRun = true, riskyHide = true };
                default: return new Skill { chaseRange = 13f, seeRange = 8f, react = 0.45f, hideWait = 4f, safeDist = 6f, guardDist = 10f, maxHide = 22f, detour = 0.12f, pause = 0.05f, runFar = true };
            }
        }

        PlayerController p; GameManager gm;
        List<Vector3> route; int ri;
        Interactable target; Vector3Int targetNode;
        float repathT, stuckT, hideWait, nudgeT, reactT, fleeT, pauseT, detourT, hiddenT, blockedT, slideT;
        Vector3 lastPos, nudgeDir, stepPos, slideDir;
        HidingSpot favorite;
        int stuckCount;

        void Start() { p = GetComponent<PlayerController>(); gm = GameManager.I; p.autopilot = true; lastPos = transform.position; }
        void OnDestroy() { if (p != null) { p.autopilot = false; p.autoDir = Vector3.zero; p.autoRun = false; p.autoCrouch = false; } }

        void Update()
        {
            if (gm == null || !gm.Playing || gm.layout == null) { if (p != null) p.autoDir = Vector3.zero; return; }
            float dt = Time.deltaTime;
            GhostAI g = gm.ghost;
            float gd = g.DistanceTo(p.transform.position);
            Skill k = SkillOf(profile);
            bool threatNow = !g.frozen && ((g.state == GhostState.Kejar && gd < k.chaseRange) || (g.canSee && gd < k.seeRange));
            reactT = threatNow ? reactT + dt : 0f;
            bool threat = threatNow && reactT >= k.react;                      // waktu reaksi sesuai keterampilan

            if (p.hidden)
            {
                p.autoDir = Vector3.zero; mode = "Bersembunyi";
                hiddenT += dt;
                bool hunting = g.state == GhostState.Kejar || g.state == GhostState.Periksa;
                bool safe = !hunting && !((g.state == GhostState.Cari || g.state == GhostState.Jaga) && gd < k.guardDist) && gd > k.safeDist;
                // sudah lama bersembunyi: keluar diam-diam selama hantu tidak memburu dan tidak tepat di depan (mencegah menunggu tanpa akhir)
                bool restless = hiddenT > k.maxHide && !hunting && gd > 6f;
                hideWait = safe || restless ? hideWait + dt : 0f;
                if (hideWait > k.hideWait) { p.ExitHide(); hideWait = 0f; hiddenT = 0f; route = null; target = null; }
                return;
            }
            hiddenT = 0f;

            // ragu-ragu / melihat-lihat sebentar (pemain kurang terampil)
            pauseT -= dt;
            if (!threat && pauseT <= 0f && k.pause > 0f && Random.value < k.pause * dt) pauseT = Random.Range(1f, 2.2f);
            if (!threat && pauseT > 0f) { p.autoDir = Vector3.zero; p.autoRun = false; mode = "Ragu"; TryUse(); return; }

            fleeT -= dt;
            // pemain terampil terus menjauh selama hantu masih mengejar di dekatnya (tidak kembali ke tujuan di tengah pengejaran)
            if (!threat && k.react < 0.5f && g.state == GhostState.Kejar && gd < k.chaseRange + 8f && route != null && ri < route.Count && mode == "Kabur") threat = true;
            if (threat) { if (fleeT <= 0f || route == null || ri >= route.Count) { Flee(g, gd); fleeT = 0.6f; } }
            else if (target is HidingSpot) { target = null; route = null; }
            if (!threat || route == null)
            {
                if (!threat) Objective(g, gd);
            }
            Steer(dt);
            TryUse();
        }

        // ------------------------------------------------------------------ tujuan
        void Objective(GhostAI g, float gd)
        {
            Skill k = SkillOf(profile);
            if (detourT > 0f)
            {
                detourT -= Time.deltaTime;
                if (route != null && ri < route.Count) { Pace(k, g, gd); return; }   // masih salah jalan
                detourT = 0f; repathT = 0f;
            }
            repathT -= Time.deltaTime;
            if (target != null && repathT > 0f && route != null) return;
            repathT = 1.2f;
            HouseInstance H = gm.house; HouseLayout L = gm.layout;
            Interactable best = null; int bestD = int.MaxValue;
            int[] dist = L.Distances(p.node, false, false);
            // baterai bila hampir habis
            if (p.battery < 25f)
                foreach (BatteryPickup b in H.batteries)
                {
                    if (b.taken) continue;
                    int d = L.Dist(dist, L.NodeAt(b.transform.position));
                    if (d >= 0 && d < 10 && d < bestD) { bestD = d; best = b; }
                }
            if (best == null)
            {
                foreach (RelicPickup r in H.relics)
                {
                    if (r.taken) continue;
                    int d = L.Dist(dist, L.NodeAt(r.transform.position));
                    if (d >= 0 && d < bestD) { bestD = d; best = r; }
                }
            }
            if (best == null && H.lockedDoor != null && H.lockedDoor.locked)
            {
                if (p.hasKey) best = H.lockedDoor;
                else if (H.key != null && !H.key.taken) best = H.key;
            }
            if (best == null && gm.relicsFound >= gm.RelicsNeeded) best = H.exit;
            if (best == null) { mode = "Menjelajah"; WanderFar(); return; }
            bool fresh = best != target;
            target = best;
            if (fresh && k.detour > 0f && Random.value < k.detour)
            {
                // salah jalan dulu sebelum menuju tujuan sebenarnya
                Vector3Int dn = RandomNear(2, 4);
                if (dn != p.node) { RouteTo(dn); detourT = 8f; mode = "Salah jalan"; Pace(k, g, gd); return; }
            }
            Vector3Int tn = NodeFor(best);
            mode = best is RelicPickup ? "Ambil relik" : best is KeyPickup ? "Ambil kunci" : best is LockedDoor ? "Buka gembok" : best is BatteryPickup ? "Ambil baterai" : "Ke pintu keluar";
            RouteTo(tn);
            Pace(k, g, gd);
        }

        /// <summary>Lari / jalan / mengendap menurut keterampilan: lari saat hantu jauh (efisien), mengendap saat dekat, atau panik berlari (berisik).</summary>
        void Pace(Skill k, GhostAI g, float gd)
        {
            // mengendap hanya bila hantu dekat tetapi tidak menghadap ke arah pemain (bila menghadap, lebih baik menjauh dengan jalan biasa)
            Vector3 toMe = G.Flat(p.transform.position - g.transform.position);
            bool facing = toMe.sqrMagnitude > 0.01f && Vector3.Angle(G.Flat(g.transform.forward), toMe) < 70f;
            p.autoCrouch = k.crouchNear && gd < 9f && g.state != GhostState.Kejar && !facing;
            bool longRoute = route != null && route.Count > 6;
            // pemain terampil berlari hanya bila stamina masih banyak (cadangan untuk kabur); pemain lemah panik berlari saat hantu dekat
            p.autoRun = !p.autoCrouch && ((k.runFar && longRoute && gd > 14f && p.stamina > 70f) || (k.panicRun && gd < 12f));
        }

        Vector3Int RandomNear(int min, int max)
        {
            HouseLayout L = gm.layout;
            int[] d = L.Distances(p.node, false, p.hasKey);
            List<Vector3Int> c = new List<Vector3Int>();
            for (int i = 0; i < d.Length; i++) if (d[i] >= min && d[i] <= max) c.Add(L.FromIdx(i));
            return c.Count > 0 ? c[Random.Range(0, c.Count)] : p.node;
        }

        Vector3Int NodeFor(Interactable it)
        {
            HouseLayout L = gm.layout;
            if (it is LockedDoor)
            {
                // node di sisi pemain dari pintu
                Vector3 c = it.transform.position, f = it.transform.forward;
                Vector3Int a = L.NodeAt(c + f * 1.5f), b = L.NodeAt(c - f * 1.5f);
                int[] d = L.Distances(p.node, false, false);
                return L.Dist(d, a) >= 0 ? a : b;
            }
            return L.NodeAt(it.UsePoint);
        }

        void WanderFar()
        {
            HouseLayout L = gm.layout;
            int[] d = L.Distances(p.node, false, p.hasKey);
            List<Vector3Int> c = new List<Vector3Int>();
            for (int i = 0; i < d.Length; i++) if (d[i] >= 4) c.Add(L.FromIdx(i));
            if (c.Count > 0) RouteTo(c[Random.Range(0, c.Count)]);
        }

        // ------------------------------------------------------------------ kabur & sembunyi
        void Flee(GhostAI g, float gd)
        {
            HouseLayout L = gm.layout;
            Vector3Int gn = L.NodeAt(g.transform.position);
            int[] dp = L.Distances(p.node, false, p.hasKey), dg = L.Distances(gn, true, true);
            if (target is HidingSpot hs && !hs.occupied && route != null)
            {
                // tetap ke tempat sembunyi yang dipilih selama hantu belum memotong jalan ke sana
                int a0 = L.Dist(dp, hs.frontNode), b0 = L.Dist(dg, hs.frontNode);
                if (a0 >= 0 && (b0 < 0 || b0 > a0 || a0 == 0) && (a0 <= 1 || SafePath(L, hs.frontNode, dg))) { p.autoRun = true; p.autoCrouch = false; return; }
                target = null;
            }
            flees++;
            mode = "Kabur";
            HidingSpot pick = null; float best = float.MaxValue;
            if (allowHide)
            {
                Skill k = SkillOf(profile);
                bool useHabit = habitOn && Random.value < habitStrength;
                if (profile == Profile.Lemah && !habitOn && !randomHide && favorite != null && !favorite.occupied && L.Dist(dp, favorite.frontNode) >= 0 && L.Dist(dp, favorite.frontNode) <= 8) pick = favorite;
                else
                    foreach (HidingSpot s in gm.house.spots)
                    {
                        if (s.occupied) continue;
                        int a = L.Dist(dp, s.frontNode), b = L.Dist(dg, s.frontNode);
                        if (a < 0 || a > 7 || (b >= 0 && b <= a)) continue;
                        if (!k.riskyHide && !SafePath(L, s.frontNode, dg)) continue;
                        float score = a;
                        if (profile == Profile.Pro) score += GhostMemory.Mean(s) * 8f - Mathf.Min(b, 8) * 0.5f;
                        if (useHabit && s.kind == habitKind) score -= 6f;          // kebiasaan: jenis favorit walau sedikit lebih jauh
                        if (randomHide) score = Random.value * 10f;                 // tanpa kebiasaan sama sekali
                        if (score < best) { best = score; pick = s; }
                    }
            }
            if (pick != null)
            {
                target = pick; RouteTo(pick.frontNode);
                p.autoRun = true; p.autoCrouch = false;
                return;
            }
            FleeAway(g);
        }

        void FleeAway(GhostAI g)
        {
            HouseLayout L = gm.layout;
            Vector3Int gn = L.NodeAt(g.transform.position);
            int[] dp = L.Distances(p.node, false, p.hasKey), dg = L.Distances(gn, true, true);
            Vector3Int far = p.node; int fs = int.MinValue;
            Vector3Int alt = p.node; int fa = int.MinValue;
            int here = L.Dist(dg, p.node);
            for (int i = 0; i < dp.Length; i++)
            {
                if (dp[i] < 2 || dp[i] > 7 || dg[i] < 0) continue;
                int s = dg[i] - dp[i];
                if (s > fs && SafePath(L, L.FromIdx(i), dg)) { fs = s; far = L.FromIdx(i); }
                if (dg[i] > here && dg[i] > fa) { fa = dg[i]; alt = L.FromIdx(i); }   // cadangan: titik yang lebih jauh dari hantu
            }
            if (far == p.node) far = alt;
            target = null; RouteTo(far); mode = "Kabur";
            p.autoRun = true; p.autoCrouch = false;
        }

        /// <summary>Jalur ke node tujuan tidak melewati node yang bersebelahan dengan hantu.</summary>
        bool SafePath(HouseLayout L, Vector3Int to, int[] dg)
        {
            List<Vector3Int> nodes = L.Path(p.node, to, false, p.hasKey);
            for (int i = 1; i < nodes.Count; i++) { int d = L.Dist(dg, nodes[i]); if (d >= 0 && d <= 1) return false; }
            return nodes.Count > 0;
        }

        // ------------------------------------------------------------------ gerak
        void RouteTo(Vector3Int n)
        {
            HouseLayout L = gm.layout;
            targetNode = n;
            List<Vector3Int> nodes = L.Path(p.node, n, false, p.hasKey && (gm.house.lockedDoor == null || !gm.house.lockedDoor.locked));
            route = nodes.Count > 0 ? L.Waypoints(nodes) : null;
            ri = 0;
            if (route != null && route.Count > 1)
            {
                Vector3 a = route[0], b = route[1], me = transform.position;
                if (Vector3.Dot(G.Flat(b - a), G.Flat(me - a)) > 0f || G.Flat(me - a).magnitude < 0.4f) ri = 1;
            }
        }

        void Steer(float dt)
        {
            if (nudgeT > 0f) { nudgeT -= dt; p.autoDir = nudgeDir; return; }
            if (route == null || ri >= route.Count) { p.autoDir = Vector3.zero; return; }
            Vector3 w = route[ri];
            Vector3 d = w - transform.position;
            float flat = G.Flat(d).magnitude;
            bool last = ri == route.Count - 1;
            if (flat < (last ? 0.5f : 0.4f) && Mathf.Abs(d.y) < 1.4f) { ri++; if (ri >= route.Count) { p.autoDir = Vector3.zero; return; } w = route[ri]; d = w - transform.position; }
            // penghindar lokal hanya saat benar-benar tertahan (CharacterController sudah menggeser sendiri bila sekadar menyerempet)
            Vector3 want = G.Flat(d).normalized;
            float speedNow = G.Flat(transform.position - stepPos).magnitude / Mathf.Max(dt, 1e-4f);
            stepPos = transform.position;
            if (slideT > 0f) { slideT -= dt; want = slideDir; }
            else
            {
                blockedT = speedNow < 0.8f ? blockedT + dt : 0f;
                if (blockedT > 0.15f)
                {
                    blockedT = 0f;
                    Vector3 side = Avoid(want);
                    if (Vector3.Dot(side, want) < 0.95f) { slideDir = side; slideT = 0.45f; want = side; }
                }
            }
            p.autoDir = want;
            p.autoLook = null;
            // macet (saat kabur dicek jauh lebih cepat: satu detik tersangkut perabot = tertangkap)
            stuckT += dt;
            if (stuckT > (mode == "Kabur" ? 0.5f : 1.6f))
            {
                if ((transform.position - lastPos).magnitude < 0.25f)
                {
                    stuckCount++; stuckEvents++;
                    if (stuckCount >= 3) { nudgeDir = Quaternion.Euler(0f, Random.Range(60f, 300f), 0f) * p.autoDir; nudgeT = 0.6f; stuckCount = 0; }
                    RouteTo(targetNode);
                }
                else stuckCount = 0;
                lastPos = transform.position; stuckT = 0f;
            }
        }

        /// <summary>
        /// Penghindar lokal: graf node tidak tahu letak perabot, jadi bila garis lurus ke titik jalur berikutnya terhalang
        /// (ranjang, lemari, sudut dinding), bot menyusuri sisi penghalang yang paling searah dengan tujuan.
        /// </summary>
        Vector3 Avoid(Vector3 dir)
        {
            const float reach = 0.9f;
            if (dir.sqrMagnitude < 0.01f) return dir;
            Vector3 origin = transform.position + Vector3.up * 0.55f;
            if (!Physics.SphereCast(origin, 0.28f, dir, out RaycastHit hit, reach, ~(1 << 2), QueryTriggerInteraction.Ignore)) return dir;
            if (hit.normal.y > 0.5f) return dir;                                  // lantai / tangga: bukan penghalang
            Vector3 n = G.Flat(hit.normal);
            if (n.sqrMagnitude < 0.01f) return dir;
            n.Normalize();
            if (Vector3.Dot(n, dir) > -0.15f) return dir;                         // hanya menyerempet
            Vector3 t = Vector3.Cross(Vector3.up, n);
            if (Vector3.Dot(t, dir) < 0f) t = -t;
            return (t * 0.9f + n * 0.1f).normalized;
        }

        void TryUse()
        {
            if (target == null) return;
            Vector3 up = target.UsePoint;
            float flat = G.Flat(up - transform.position).magnitude;
            bool sameFloor = Mathf.Abs(up.y - transform.position.y) < 1.4f;
            float reach = target is LockedDoor ? 2.2f : target is HidingSpot ? 1.0f : 1.4f;
            if (!sameFloor || flat > reach) return;
            if (target is HidingSpot hs)
            {
                if (hs.occupied) { target = null; return; }
                // di persembunyian pemain langsung aman (hantu tidak pernah menemukan pemain yang bersembunyi),
                // jadi bot selalu masuk walau hantu melihat - kembali ke arah hantu justru berbahaya
                p.EnterHide(hs); hides++; if (favorite == null) favorite = hs; hideWait = 0f;
            }
            else
            {
                target.Use(p); uses++;
            }
            target = null; route = null; repathT = 0f;
        }
    }
}
