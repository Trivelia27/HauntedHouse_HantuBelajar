using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Bot pemain untuk eksperimen DDA, uji otomatis, dan rekaman demo.
    /// Pro: tenang, jongkok saat hantu dekat, memilih tempat sembunyi yang JARANG dipakai.
    /// Sedang: tempat sembunyi terdekat. Lemah: sering berlari (berisik), selalu kembali ke tempat sembunyi favorit.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        public enum Profile { Pro, Sedang, Lemah }
        public Profile profile = Profile.Sedang;
        public string mode = "-";
        public int hides, flees, stuckEvents, uses;
        public bool allowHide = true;

        PlayerController p; GameManager gm;
        List<Vector3> route; int ri;
        Interactable target; Vector3Int targetNode;
        float repathT, stuckT, hideWait, nudgeT, reactT, fleeT;
        Vector3 lastPos, nudgeDir;
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
            reactT -= dt;
            bool threat = !g.frozen && ((g.state == GhostState.Kejar && gd < 18f) || (g.canSee && gd < 10f));
            if (profile == Profile.Lemah && threat && reactT > 0f) threat = false;     // reaksi lambat
            if (!threat && profile == Profile.Lemah) reactT = 0.6f;

            if (p.hidden)
            {
                p.autoDir = Vector3.zero; mode = "Bersembunyi";
                bool safe = g.state != GhostState.Kejar && g.state != GhostState.Periksa && !(g.state == GhostState.Cari && gd < 10f) && gd > 6f;
                hideWait = safe ? hideWait + dt : 0f;
                if (hideWait > (profile == Profile.Pro ? 2.5f : 4f)) { p.ExitHide(); hideWait = 0f; route = null; target = null; }
                return;
            }

            fleeT -= dt;
            if (threat) { if (fleeT <= 0f || route == null) { Flee(g, gd); fleeT = 0.6f; } }
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
            target = best;
            Vector3Int tn = NodeFor(best);
            mode = best is RelicPickup ? "Ambil relik" : best is KeyPickup ? "Ambil kunci" : best is LockedDoor ? "Buka gembok" : best is BatteryPickup ? "Ambil baterai" : "Ke pintu keluar";
            RouteTo(tn);
            p.autoCrouch = profile == Profile.Pro && gd < 10f && g.state != GhostState.Kejar;
            p.autoRun = profile == Profile.Lemah ? (route != null && route.Count > 3) : profile == Profile.Sedang ? (route != null && route.Count > 6 && gd > 14f) : false;
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
            if (target is HidingSpot hs && !hs.occupied && route != null) { p.autoRun = true; p.autoCrouch = false; return; }
            flees++;
            mode = "Kabur";
            Vector3Int gn = L.NodeAt(g.transform.position);
            int[] dp = L.Distances(p.node, false, p.hasKey), dg = L.Distances(gn, true, true);
            HidingSpot pick = null; float best = float.MaxValue;
            if (allowHide)
            {
                if (profile == Profile.Lemah && favorite != null && !favorite.occupied && L.Dist(dp, favorite.frontNode) >= 0 && L.Dist(dp, favorite.frontNode) <= 8) pick = favorite;
                else
                    foreach (HidingSpot s in gm.house.spots)
                    {
                        if (s.occupied) continue;
                        int a = L.Dist(dp, s.frontNode), b = L.Dist(dg, s.frontNode);
                        if (a < 0 || a > 7 || (b >= 0 && b <= a)) continue;
                        if (!SafePath(L, s.frontNode, dg)) continue;
                        float score = a;
                        if (profile == Profile.Pro) score += GhostMemory.Mean(s) * 8f - Mathf.Min(b, 8) * 0.5f;
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
            Vector3Int far = p.node; int fs = -1;
            for (int i = 0; i < dp.Length; i++)
            {
                if (dp[i] < 2 || dp[i] > 7) continue;
                int s = dg[i] - dp[i];
                if (s > fs && SafePath(L, L.FromIdx(i), dg)) { fs = s; far = L.FromIdx(i); }
            }
            target = null; RouteTo(far);
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
            p.autoDir = G.Flat(d).normalized;
            p.autoLook = null;
            // macet
            stuckT += dt;
            if (stuckT > 1.6f)
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
                GhostAI g = gm.ghost;
                bool watched = g.canSee && g.DistanceTo(transform.position) < 9f;
                if (watched && profile != Profile.Lemah && p.stamina > 8f)
                {
                    // hantu masih melihat: lari dulu menjauh lalu coba lagi
                    target = null; route = null; repathT = 0f; FleeAway(g);
                    return;
                }
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
