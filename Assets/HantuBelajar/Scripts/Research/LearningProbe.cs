using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Pengukur "apakah hantu benar-benar belajar" tanpa mengubah perilaku game.
    /// Tiap keputusan yang memakai memori hantu dibandingkan dengan pilihan ACAK pada lintasan pemain yang sama (evaluasi kontrafaktual):
    ///  - Hadang : pemain lewat dalam 1 sel dari titik hadang dalam 25 dtk? (titik hasil heatmap vs node acak)
    ///  - Jaga   : pemain sedang/akan bersembunyi (20 dtk) di tempat yang dijaga? (Thompson sampling vs tempat acak dari kandidat sama)
    ///  - Sembunyi: peluang model untuk tempat yang benar-benar dipilih pemain vs peluang acak 1/kandidat (kandidat = tempat
    ///              sembunyi &lt;= 6 langkah dari tempat terpilih), dihitung SEBELUM kebiasaan ini dicatat.
    /// Generator acak sendiri agar tidak mengubah urutan acak game.
    /// </summary>
    public static class LearningProbe
    {
        public struct HideRec { public int house, index; public float pModel, pUniform; public int rank, cand; public string kind, room; }
        public struct DecRec { public string kind; public int house; public bool hitModel, hitRandom; }

        class Pend { public string kind; public Vector3Int nodeM, nodeR; public HidingSpot spotM, spotR; public float t0, window; public bool hitM, hitR; }

        public static readonly List<HideRec> hides = new List<HideRec>();
        public static readonly List<DecRec> decisions = new List<DecRec>();
        static readonly List<Pend> pending = new List<Pend>();
        static readonly List<int> walkable = new List<int>();
        static HouseLayout walkFor;
        static System.Random rng = new System.Random(20261120);
        static int house;

        public static void Reset(int seed = 20261120)
        {
            hides.Clear(); decisions.Clear(); pending.Clear(); walkFor = null; house = 0;
            rng = new System.Random(seed);
        }

        public static void BeginHouse(int houseIndex) { FinishAll(); house = houseIndex; }

        /// <summary>Akhir rumah: keputusan yang belum terbukti dihitung meleset.</summary>
        public static void EndHouse() { FinishAll(); }

        static void FinishAll()
        {
            foreach (Pend e in pending) Finish(e);
            pending.Clear();
        }

        // ------------------------------------------------------------------ hook dari GhostAI / PlayerController
        public static void OnAmbush(HouseLayout L, Vector3Int chosen)
        {
            if (L == null) return;
            if (walkFor != L)
            {
                walkFor = L; walkable.Clear();
                for (int i = 0; i < L.Count; i++) if (!L.Blocked(L.FromIdx(i))) walkable.Add(i);
            }
            if (walkable.Count == 0) return;
            Vector3Int rnd = L.FromIdx(walkable[rng.Next(walkable.Count)]);
            pending.Add(new Pend { kind = "hadang", nodeM = chosen, nodeR = rnd, t0 = Time.time, window = 25f });
        }

        public static void OnGuard(HouseLayout L, HouseInstance H, HidingSpot chosen, Vector3Int around, int radius, PlayerController p)
        {
            if (L == null || H == null || chosen == null) return;
            List<HidingSpot> cand = new List<HidingSpot>();
            int[] dist = radius > 0 ? L.Distances(around, true, true) : null;
            foreach (HidingSpot s in H.spots)
            {
                if (dist != null) { int d = L.Dist(dist, s.frontNode); if (d < 0 || d > radius) continue; }
                cand.Add(s);
            }
            if (!cand.Contains(chosen)) cand.Add(chosen);
            HidingSpot rnd = cand[rng.Next(cand.Count)];
            Pend e = new Pend { kind = "jaga", spotM = chosen, spotR = rnd, t0 = Time.time, window = 20f };
            if (p != null && p.hidden && p.spot != null) { e.hitM = p.spot == chosen; e.hitR = p.spot == rnd; }
            pending.Add(e);
        }

        public static void OnHide(HouseLayout L, HouseInstance H, HidingSpot s)
        {
            if (L == null || H == null || s == null) return;
            foreach (Pend e in pending)
            {
                if (e.kind != "jaga") continue;
                if (e.spotM == s) e.hitM = true;
                if (e.spotR == s) e.hitR = true;
            }
            int[] dist = L.Distances(s.frontNode, true, true);
            List<HidingSpot> cand = new List<HidingSpot>();
            foreach (HidingSpot c in H.spots) { int d = L.Dist(dist, c.frontNode); if (d >= 0 && d <= 6) cand.Add(c); }
            if (!cand.Contains(s)) cand.Add(s);
            float sum = 0f, ms = GhostMemory.Mean(s); int rank = 1;
            foreach (HidingSpot c in cand) { float m = GhostMemory.Mean(c); sum += m; if (c != s && m > ms + 1e-5f) rank++; }
            HideRec r = new HideRec
            {
                house = house, index = hides.Count + 1, pModel = sum > 0f ? ms / sum : 0f, pUniform = 1f / cand.Count, rank = rank, cand = cand.Count,
                kind = s.KindName, room = HouseGenerator.RoomName(s.roomType),
            };
            hides.Add(r);
            Telemetry.Learn("sembunyi", r.pModel, r.pUniform, rank, cand.Count, -1, -1, s.KindName + " / " + r.room);
        }

        /// <summary>Dipanggil tiap frame saat bermain: cek apakah pemain melewati titik hadang (model maupun acak).</summary>
        public static void Tick(PlayerController p)
        {
            if (pending.Count == 0 || p == null) return;
            float now = Time.time;
            Vector3Int pn = p.node;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Pend e = pending[i];
                if (e.kind == "hadang" && !p.hidden)
                {
                    if (Near(pn, e.nodeM)) e.hitM = true;
                    if (Near(pn, e.nodeR)) e.hitR = true;
                }
                if (now - e.t0 >= e.window) { Finish(e); pending.RemoveAt(i); }
            }
        }

        static bool Near(Vector3Int a, Vector3Int b) { return a.z == b.z && Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) <= 1; }

        static void Finish(Pend e)
        {
            decisions.Add(new DecRec { kind = e.kind, house = house, hitModel = e.hitM, hitRandom = e.hitR });
            Telemetry.Learn(e.kind, -1f, -1f, -1, -1, e.hitM ? 1 : 0, e.hitR ? 1 : 0, "");
        }
    }
}
