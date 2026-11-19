using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// PCG rumah berhantu (data murni):
    ///  1. BSP membagi denah tiap lantai menjadi ruangan
    ///  2. graf ketetanggaan -> spanning tree Kruskal (union-find) + pintu loop
    ///  3. tangga lurus menghubungkan lantai 1 dan 2 (dicek konektivitasnya)
    ///  4. start (ruang tamu) & pintu keluar dari kedalaman BFS graf ruangan
    ///  5. ruang terkunci (daun graf) + kunci di tempat yang terjangkau tanpa kunci (lock-and-key)
    ///  6. tempat sembunyi, relik (dibatasi jarak ke tempat sembunyi), baterai, perabot: generate-and-test
    ///  7. validasi keterjangkauan; gagal -> ulang dengan seed turunan, lalu parameter dilonggarkan
    /// </summary>
    public static class HouseGenerator
    {
        public const int MaxAttempts = 16;

        public static HouseLayout Generate(PcgParams p)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HouseLayout last = null;
            int total = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                PcgParams q = p;
                if (pass == 1)
                {
                    // fallback: parameter dilonggarkan agar rumah tetap bisa dimainkan
                    q = p.Clone(); q.relicSpotDist = 0; q.lockedRooms = 0; q.loopChance = Mathf.Max(q.loopChance, 0.4f);
                }
                for (int a = 0; a < MaxAttempts; a++)
                {
                    HouseLayout L = TryGenerate(q, p.seed + a * 7919 + pass * 104729);
                    total++;
                    L.attempts = total;
                    last = L;
                    if (L.valid) { L.ms = (float)sw.Elapsed.TotalMilliseconds; return L; }
                }
            }
            last.ms = (float)sw.Elapsed.TotalMilliseconds;
            return last;
        }

        static HouseLayout TryGenerate(PcgParams p, int seed)
        {
            System.Random rng = new System.Random(seed);
            int floors = Mathf.Clamp(p.floors, 1, 2);
            HouseLayout L = new HouseLayout { p = p, w = Mathf.Max(4, p.width), h = Mathf.Max(4, p.height), floors = floors, usedSeed = seed };
            L.room = new int[floors, L.w, L.h];
            L.blocked = new bool[floors, L.w, L.h];
            L.hole = new bool[floors, L.w, L.h];

            // 1. BSP tiap lantai
            for (int f = 0; f < floors; f++)
            {
                List<RectInt> leaves = new List<RectInt>();
                Split(new RectInt(0, 0, L.w, L.h), rng, p, leaves, 0);
                foreach (RectInt r in leaves)
                {
                    Room room = new Room { id = L.rooms.Count, floor = f, rect = r };
                    L.rooms.Add(room);
                    foreach (Vector2Int c in r.allPositionsWithin) L.room[f, c.x, c.y] = room.id;
                }
            }

            // 2. pintu: spanning tree Kruskal + loop, per lantai
            for (int f = 0; f < floors; f++) MakeDoors(L, f, rng, p);

            // 3. ruang awal (lantai 1, menempel batas luar)
            List<Room> boundary0 = L.rooms.FindAll(r => r.floor == 0 && Touches(L, r));
            if (boundary0.Count == 0) { L.failReason = "tidak ada ruang batas"; return L; }
            Room startRoom = boundary0[rng.Next(boundary0.Count)];
            Vector2Int sc = Center(startRoom.rect);
            L.start = new Vector3Int(sc.x, sc.y, 0);

            // 4. tangga
            if (floors > 1 && !PlaceStairs(L, rng, startRoom)) { L.failReason = "tangga tidak bisa ditempatkan"; return L; }

            // 5. kedalaman graf ruangan (pintu + tangga)
            foreach (Stair s in L.stairs)
            {
                int a = L.room[s.f, s.bottom.x, s.bottom.y], b = L.room[s.f + 1, s.Landing.x, s.Landing.y];
                L.rooms[a].links.Add(b); L.rooms[b].links.Add(a);
                L.rooms[a].hasStairs = true; L.rooms[b].hasStairs = true;
                L.rooms[L.room[s.f, s.Ramp1.x, s.Ramp1.y]].hasStairs = true;
                L.rooms[L.room[s.f + 1, s.Ramp1.x, s.Ramp1.y]].hasStairs = true;
            }
            Queue<Room> q = new Queue<Room>();
            startRoom.depth = 0; q.Enqueue(startRoom);
            while (q.Count > 0)
            {
                Room r = q.Dequeue();
                foreach (int li in r.links) if (L.rooms[li].depth < 0) { L.rooms[li].depth = r.depth + 1; q.Enqueue(L.rooms[li]); }
            }

            // 6. pintu keluar: ruang batas terdalam di lantai 1
            Room exitRoom = null;
            foreach (Room r in boundary0)
                if (r != startRoom && (exitRoom == null || r.depth > exitRoom.depth || (r.depth == exitRoom.depth && rng.NextDouble() < 0.5))) exitRoom = r;
            if (exitRoom == null) { L.failReason = "tidak ada ruang untuk pintu keluar"; return L; }
            List<(Vector2Int, int)> outer = new List<(Vector2Int, int)>();
            foreach (Vector2Int c in exitRoom.rect.allPositionsWithin)
            {
                if (L.blocked[0, c.x, c.y]) continue;
                for (int k = 0; k < 4; k++) if (!L.In(0, c + G.Dirs[k])) outer.Add((c, k));
            }
            if (outer.Count == 0) { L.failReason = "dinding luar ruang keluar tertutup"; return L; }
            var ex = outer[rng.Next(outer.Count)];
            L.exitNode = new Vector3Int(ex.Item1.x, ex.Item1.y, 0); L.exitDir = ex.Item2;

            // 7. tipe ruangan + lampu
            AssignTypes(L, rng, startRoom, p);

            // 8. sel yang harus tetap kosong
            HashSet<Vector3Int> reserved = new HashSet<Vector3Int> { L.start, L.exitNode };
            foreach (DoorEdge d in L.doors) { reserved.Add(new Vector3Int(d.a.x, d.a.y, d.f)); reserved.Add(new Vector3Int(d.b.x, d.b.y, d.f)); }
            foreach (Stair s in L.stairs) { reserved.Add(s.BottomNode); reserved.Add(s.LandingNode); }

            // 9. ruang terkunci (daun graf pintu) -> pintunya dikunci
            if (p.lockedRooms > 0) LockRoom(L, rng, startRoom, exitRoom);

            // 10. tempat sembunyi
            PlaceSpots(L, rng, reserved, startRoom, p);

            // 11. relik (jarak ke tempat sembunyi dibatasi), 12. kunci, 13. baterai
            if (!PlaceRelics(L, rng, reserved, startRoom, p)) { L.failReason = "relik tidak cukup"; return L; }
            if (L.lockedRoom >= 0 && !PlaceKey(L, rng, reserved, startRoom)) { L.failReason = "kunci tidak bisa ditempatkan"; return L; }
            PlaceBatteries(L, rng, reserved, startRoom, p);

            // 14. perabot pemblokir
            PlaceProps(L, rng, reserved, p);

            // 15. hantu mulai sejauh mungkin dari pemain
            int[] dist = L.Distances(L.start, true, true);
            int bestScore = -1;
            for (int i = 0; i < dist.Length; i++)
            {
                if (dist[i] < 0) continue;
                Vector3Int n = L.FromIdx(i);
                if (L.Blocked(n)) continue;
                int score = dist[i] * 10 + rng.Next(10) + (n.z != L.start.z ? 60 : 0);
                if (score > bestScore) { bestScore = score; L.ghostStart = n; }
            }

            // 16. validasi
            Validate(L, p);
            L.hash = Hash(L);
            return L;
        }

        // ------------------------------------------------------------------ BSP
        static void Split(RectInt r, System.Random rng, PcgParams p, List<RectInt> outList, int depth)
        {
            int minR = Mathf.Max(2, p.minRoom), maxR = Mathf.Max(minR, p.maxRoom);
            bool canV = r.width >= minR * 2, canH = r.height >= minR * 2;
            bool small = r.width <= maxR && r.height <= maxR;
            if ((!canV && !canH) || (small && depth > 1 && rng.NextDouble() < 0.45)) { outList.Add(r); return; }
            bool vertical = canV && (!canH || (r.width >= r.height ? rng.NextDouble() < 0.8 : rng.NextDouble() < 0.2));
            if (vertical)
            {
                int cut = rng.Next(minR, r.width - minR + 1);
                Split(new RectInt(r.x, r.y, cut, r.height), rng, p, outList, depth + 1);
                Split(new RectInt(r.x + cut, r.y, r.width - cut, r.height), rng, p, outList, depth + 1);
            }
            else
            {
                int cut = rng.Next(minR, r.height - minR + 1);
                Split(new RectInt(r.x, r.y, r.width, cut), rng, p, outList, depth + 1);
                Split(new RectInt(r.x, r.y + cut, r.width, r.height - cut), rng, p, outList, depth + 1);
            }
        }

        // ------------------------------------------------------------------ pintu
        static void MakeDoors(HouseLayout L, int f, System.Random rng, PcgParams p)
        {
            Dictionary<long, List<DoorEdge>> shared = new Dictionary<long, List<DoorEdge>>();
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2Int a = new Vector2Int(x, y), b = a + G.Dirs[k];
                        if (!L.In(f, b)) continue;
                        int ra = L.room[f, a.x, a.y], rb = L.room[f, b.x, b.y];
                        if (ra == rb) continue;
                        long pk = (long)Mathf.Min(ra, rb) * 1000 + Mathf.Max(ra, rb);
                        if (!shared.TryGetValue(pk, out List<DoorEdge> list)) shared[pk] = list = new List<DoorEdge>();
                        list.Add(new DoorEdge { f = f, a = a, b = b });
                    }
            List<long> pairs = new List<long>(shared.Keys);
            pairs.Sort();
            Shuffle(pairs, rng);
            Dictionary<int, int> uf = new Dictionary<int, int>();
            foreach (Room r in L.rooms) if (r.floor == f) uf[r.id] = r.id;
            List<long> extra = new List<long>();
            foreach (long pk in pairs)
            {
                int ra = (int)(pk / 1000), rb = (int)(pk % 1000);
                int fa = Find(uf, ra), fb = Find(uf, rb);
                if (fa != fb) { uf[fa] = fb; AddDoor(L, shared[pk], rng, false, ra, rb); }
                else extra.Add(pk);
            }
            foreach (long pk in extra)
                if (rng.NextDouble() < p.loopChance) { AddDoor(L, shared[pk], rng, true, (int)(pk / 1000), (int)(pk % 1000)); L.loops++; }
        }

        static void AddDoor(HouseLayout L, List<DoorEdge> edges, System.Random rng, bool loop, int ra, int rb)
        {
            int mid = edges.Count / 2, spread = Mathf.Max(0, edges.Count / 3);
            int i = Mathf.Clamp(mid + rng.Next(-spread, spread + 1), 0, edges.Count - 1);
            DoorEdge e = edges[i]; e.loop = loop;
            L.doors.Add(e);
            L.doorKeys.Add(L.Key(e.f, e.a, e.b));
            L.rooms[ra].links.Add(rb); L.rooms[rb].links.Add(ra);
        }

        // ------------------------------------------------------------------ tangga
        static bool PlaceStairs(HouseLayout L, System.Random rng, Room startRoom)
        {
            List<Stair> cand = new List<Stair>();
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                    for (int k = 0; k < 4; k++)
                    {
                        Stair s = new Stair { f = 0, bottom = new Vector2Int(x, y), dir = k };
                        Vector2Int r1 = s.Ramp1, ld = s.Landing;
                        if (!L.In(0, ld)) continue;
                        int r0 = L.room[0, x, y];
                        if (L.room[0, r1.x, r1.y] != r0) continue;
                        int u = L.room[1, r1.x, r1.y];
                        if (L.room[1, ld.x, ld.y] != u) continue;
                        if (G.N(0, r1) == L.start || s.BottomNode == L.start) continue;
                        bool doorTouch = false;
                        for (int d = 0; d < 4; d++)
                            if (L.IsDoor(0, r1, d) || L.IsDoor(1, r1, d)) doorTouch = true;
                        if (doorTouch) continue;
                        cand.Add(s);
                    }
            Shuffle(cand, rng);
            int tries = 0;
            foreach (Stair s in cand)
            {
                if (++tries > 24) break;
                SetStairCells(L, s, true);
                L.stairs.Add(s);
                bool[] r = L.Reach(L.start, true, true);
                bool ok = true;
                for (int i = 0; i < r.Length && ok; i++) if (!r[i] && !L.Blocked(L.FromIdx(i))) ok = false;
                if (ok) return true;
                L.stairs.Remove(s);
                SetStairCells(L, s, false);
                L.rejectedPlacements++;
            }
            return false;
        }

        static void SetStairCells(HouseLayout L, Stair s, bool on)
        {
            Vector2Int c = s.Ramp1;
            L.blocked[s.f, c.x, c.y] = on;
            L.blocked[s.f + 1, c.x, c.y] = on;
            L.hole[s.f + 1, c.x, c.y] = on;
        }

        // ------------------------------------------------------------------ tipe ruangan
        static void AssignTypes(HouseLayout L, System.Random rng, Room startRoom, PcgParams p)
        {
            for (int f = 0; f < L.floors; f++)
            {
                List<Room> rs = L.rooms.FindAll(r => r.floor == f && r != startRoom);
                rs.Sort((a, b) => b.Area.CompareTo(a.Area));
                List<RoomType> big, small;
                if (L.floors == 1)
                {
                    big = new List<RoomType> { RoomType.RuangMakan, RoomType.KamarTidur, RoomType.Dapur, RoomType.RuangKeluarga, RoomType.Perpustakaan, RoomType.KamarAnak, RoomType.KamarTidur, RoomType.Dapur };
                    small = new List<RoomType> { RoomType.KamarMandi, RoomType.Gudang };
                }
                else if (f == 0)
                {
                    big = new List<RoomType> { RoomType.RuangMakan, RoomType.Dapur, RoomType.RuangKeluarga, RoomType.Perpustakaan, RoomType.RuangKeluarga, RoomType.Dapur };
                    small = new List<RoomType> { RoomType.KamarMandi, RoomType.Gudang };
                }
                else
                {
                    big = new List<RoomType> { RoomType.KamarTidur, RoomType.KamarAnak, RoomType.KamarTidur, RoomType.Perpustakaan, RoomType.KamarTidur, RoomType.RuangKeluarga };
                    small = new List<RoomType> { RoomType.KamarMandi, RoomType.Gudang };
                }
                // sedikit variasi urutan agar rumah tidak seragam
                for (int i = 0; i + 1 < big.Count; i += 2) if (rng.NextDouble() < 0.4) { RoomType t = big[i]; big[i] = big[i + 1]; big[i + 1] = t; }
                // ruangan terkecil (tanpa tangga) menjadi kamar mandi / gudang, maksimal satu masing-masing
                List<Room> smallRooms = rs.FindAll(r => r.Area <= 6 && !r.hasStairs);
                smallRooms.Sort((a, b) => a.Area.CompareTo(b.Area));
                int si = 0;
                foreach (Room r in smallRooms)
                {
                    if (si >= small.Count || rs.Count - si <= 2) break;
                    r.type = small[si++];
                    rs.Remove(r);
                }
                int bi = 0;
                foreach (Room r in rs) r.type = big[bi++ % big.Count];
            }
            startRoom.type = RoomType.RuangTamu;
            foreach (Room r in L.rooms) r.lit = r == startRoom || rng.NextDouble() < p.litRatio;
        }

        // ------------------------------------------------------------------ ruang terkunci
        static void LockRoom(HouseLayout L, System.Random rng, Room startRoom, Room exitRoom)
        {
            List<Room> cand = new List<Room>();
            foreach (Room r in L.rooms)
            {
                if (r == startRoom || r == exitRoom || r.hasStairs || r.depth < 1) continue;
                int deg = 0;
                foreach (DoorEdge d in L.doors)
                    if (d.f == r.floor && (L.room[d.f, d.a.x, d.a.y] == r.id || L.room[d.f, d.b.x, d.b.y] == r.id)) deg++;
                if (deg == 1) cand.Add(r);
            }
            if (cand.Count == 0) return;
            cand.Sort((a, b) => b.depth.CompareTo(a.depth));
            Room pick = cand[rng.Next(Mathf.Min(2, cand.Count))];
            for (int i = 0; i < L.doors.Count; i++)
            {
                DoorEdge d = L.doors[i];
                if (d.f == pick.floor && (L.room[d.f, d.a.x, d.a.y] == pick.id || L.room[d.f, d.b.x, d.b.y] == pick.id))
                {
                    d.locked = true; L.doors[i] = d;
                    L.lockedKeys.Add(L.Key(d.f, d.a, d.b));
                }
            }
            pick.locked = true;
            L.lockedRoom = pick.id;
        }

        // ------------------------------------------------------------------ tempat sembunyi
        static void PlaceSpots(HouseLayout L, System.Random rng, HashSet<Vector3Int> reserved, Room startRoom, PcgParams p)
        {
            List<Room> eligible = L.rooms.FindAll(r => r != startRoom);
            if (eligible.Count == 0) eligible.Add(startRoom);
            int[] count = new int[L.rooms.Count];
            bool[] hasBed = new bool[L.rooms.Count];
            for (int i = 0; i < p.hidingSpots; i++)
            {
                // ruangan dengan tempat sembunyi paling sedikit; kamar tidur diprioritaskan
                Room best = null; float bs = float.MaxValue;
                foreach (Room r in eligible)
                {
                    bool bedroom = r.type == RoomType.KamarTidur || r.type == RoomType.KamarAnak;
                    float s = count[r.id] * (bedroom ? 0.6f : 1f) + (float)rng.NextDouble() * 0.5f + (r.Area <= 4 && count[r.id] > 0 ? 1.5f : 0f);
                    if (s < bs) { bs = s; best = r; }
                }
                bool bedRoom = best.type == RoomType.KamarTidur || best.type == RoomType.KamarAnak;
                int kind = bedRoom && !hasBed[best.id] ? (int)SpotKind.KolongRanjang : (int)SpotKind.Lemari;
                if (TryPlaceWall(L, best, reserved, rng, out Placed pl))
                {
                    pl.id = L.spots.Count; pl.kind = kind;
                    L.spots.Add(pl);
                    if (kind == (int)SpotKind.KolongRanjang) hasBed[best.id] = true;
                }
                count[best.id]++;
            }
        }

        // ------------------------------------------------------------------ relik
        static bool PlaceRelics(HouseLayout L, System.Random rng, HashSet<Vector3Int> reserved, Room startRoom, PcgParams p)
        {
            // jarak setiap sel ke depan tempat sembunyi terdekat (BFS multi-sumber)
            int[] sd = SpotDistances(L);
            int maxD = p.relicSpotDist > 0 ? p.relicSpotDist : 999;
            for (int relax = 0; relax < 12; relax++)
            {
                List<Placed> chosen = new List<Placed>();
                HashSet<int> usedRooms = new HashSet<int>();
                List<Room> order = L.rooms.FindAll(r => r != startRoom);
                Shuffle(order, rng);
                order.Sort((a, b) => b.depth.CompareTo(a.depth));
                // relik pertama di ruang terkunci (hadiah membuka pintu)
                if (L.lockedRoom >= 0)
                {
                    Room lr = L.rooms[L.lockedRoom];
                    Vector2Int c = FreeCell(L, lr, reserved, rng, null, 999);
                    if (c.x >= 0) { chosen.Add(new Placed { f = lr.floor, cell = c, room = lr.id }); usedRooms.Add(lr.id); }
                }
                // pastikan tiap lantai punya relik
                for (int f = 0; f < L.floors && chosen.Count < p.relicCount; f++)
                {
                    if (chosen.Exists(c => c.f == f)) continue;
                    foreach (Room r in order)
                    {
                        if (r.floor != f || usedRooms.Contains(r.id)) continue;
                        Vector2Int c = FreeCell(L, r, reserved, rng, sd, maxD);
                        if (c.x < 0) continue;
                        chosen.Add(new Placed { f = f, cell = c, room = r.id }); usedRooms.Add(r.id);
                        break;
                    }
                }
                // sisanya: ruangan terdalam (2/3 teratas diacak agar tersebar)
                int top = Mathf.Max(p.relicCount, Mathf.CeilToInt(order.Count * 0.7f));
                List<Room> pool = order.GetRange(0, Mathf.Min(top, order.Count));
                Shuffle(pool, rng);
                pool.AddRange(order);
                foreach (Room r in pool)
                {
                    if (chosen.Count >= p.relicCount) break;
                    if (usedRooms.Contains(r.id)) continue;
                    Vector2Int c = FreeCell(L, r, reserved, rng, sd, maxD);
                    if (c.x < 0) continue;
                    chosen.Add(new Placed { f = r.floor, cell = c, room = r.id }); usedRooms.Add(r.id);
                }
                if (chosen.Count >= p.relicCount)
                {
                    L.relicSpotRelax = relax;
                    L.relicSpotMax = 0;
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        chosen[i].id = i; chosen[i].kind = i;
                        L.relics.Add(chosen[i]);
                        reserved.Add(chosen[i].Node);
                        int d = sd[L.Idx(chosen[i].Node)];
                        if (L.rooms[chosen[i].room].locked) continue;
                        L.relicSpotMax = Mathf.Max(L.relicSpotMax, d < 0 ? 99 : d);
                    }
                    return true;
                }
                maxD += 1;
            }
            return false;
        }

        static int[] SpotDistances(HouseLayout L)
        {
            int[] d = new int[L.Count];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            Queue<Vector3Int> q = new Queue<Vector3Int>();
            foreach (Placed s in L.spots)
            {
                Vector3Int fn = s.FrontNode;
                if (!L.In(fn)) continue;
                if (d[L.Idx(fn)] < 0) { d[L.Idx(fn)] = 0; q.Enqueue(fn); }
            }
            List<Vector3Int> nb = new List<Vector3Int>();
            while (q.Count > 0)
            {
                Vector3Int c = q.Dequeue();
                L.Neighbors(c, false, true, nb);
                foreach (Vector3Int m in nb) { int mi = L.Idx(m); if (d[mi] < 0) { d[mi] = d[L.Idx(c)] + 1; q.Enqueue(m); } }
            }
            return d;
        }

        static bool PlaceKey(HouseLayout L, System.Random rng, HashSet<Vector3Int> reserved, Room startRoom)
        {
            bool[] reach = L.Reach(L.start, false, false);
            int[] dist = L.Distances(L.start, false, false);
            Room locked = L.rooms[L.lockedRoom];
            List<(int, Vector3Int, int)> cand = new List<(int, Vector3Int, int)>();
            foreach (Room r in L.rooms)
            {
                if (r == startRoom || r.locked) continue;
                foreach (Vector2Int c in r.rect.allPositionsWithin)
                {
                    Vector3Int n = G.N(r.floor, c);
                    if (L.Blocked(n) || reserved.Contains(n) || !reach[L.Idx(n)]) continue;
                    int score = dist[L.Idx(n)] * 4 + (r.floor != locked.floor ? 6 : 0) + rng.Next(8);
                    cand.Add((score, n, r.id));
                }
            }
            if (cand.Count == 0) return false;
            cand.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            var pick = cand[rng.Next(Mathf.Max(1, cand.Count / 3))];
            L.key = new Placed { f = pick.Item2.z, cell = new Vector2Int(pick.Item2.x, pick.Item2.y), room = pick.Item3 };
            reserved.Add(pick.Item2);
            return true;
        }

        static void PlaceBatteries(HouseLayout L, System.Random rng, HashSet<Vector3Int> reserved, Room startRoom, PcgParams p)
        {
            List<Room> rs = L.rooms.FindAll(r => r != startRoom && !r.locked);
            if (rs.Count == 0) return;
            for (int i = 0; i < p.batteries; i++)
            {
                Room r = rs[rng.Next(rs.Count)];
                Vector2Int c = FreeCell(L, r, reserved, rng, null, 999);
                if (c.x < 0) continue;
                Placed b = new Placed { id = L.batteries.Count, f = r.floor, cell = c, room = r.id };
                L.batteries.Add(b);
                reserved.Add(b.Node);
            }
        }

        // ------------------------------------------------------------------ perabot
        static void PlaceProps(HouseLayout L, System.Random rng, HashSet<Vector3Int> reserved, PcgParams p)
        {
            foreach (Room r in L.rooms)
            {
                List<PropKind> wanted = new List<PropKind>();
                switch (r.type)
                {
                    case RoomType.Dapur: wanted.AddRange(new[] { PropKind.Dapur, PropKind.Kulkas, PropKind.Dapur, PropKind.Meja }); break;
                    case RoomType.RuangMakan: wanted.AddRange(new[] { PropKind.MejaMakan, PropKind.Meja, PropKind.Rak }); break;
                    case RoomType.RuangKeluarga: wanted.AddRange(new[] { PropKind.Perapian, PropKind.Kursi, PropKind.Rak, PropKind.Kursi }); break;
                    case RoomType.Perpustakaan: wanted.AddRange(new[] { PropKind.Rak, PropKind.Rak, PropKind.Meja, PropKind.Rak, PropKind.Kursi }); break;
                    case RoomType.KamarMandi: wanted.AddRange(new[] { PropKind.Bak, PropKind.Wastafel }); break;
                    case RoomType.Gudang: wanted.AddRange(new[] { PropKind.Peti, PropKind.Rak, PropKind.Peti }); break;
                    case RoomType.RuangTamu: wanted.AddRange(new[] { PropKind.Kursi, PropKind.Meja }); break;
                    default: wanted.AddRange(new[] { PropKind.Meja, PropKind.Rak, PropKind.Kursi }); break;
                }
                int perim = 2 * (r.rect.width + r.rect.height);
                int n = Mathf.Clamp(Mathf.RoundToInt(perim * p.propDensity * 0.3f), 1, wanted.Count);
                if (r.Area <= 4) n = Mathf.Min(n, 2);
                for (int i = 0; i < n; i++)
                {
                    PropKind k = wanted[i];
                    Placed pl;
                    bool ok = k == PropKind.MejaMakan ? (TryPlaceCenter(L, r, reserved, rng, out pl) || TryPlaceWall(L, r, reserved, rng, out pl))
                                                       : TryPlaceWall(L, r, reserved, rng, out pl);
                    if (!ok) continue;
                    pl.id = L.props.Count; pl.kind = (int)k;
                    L.props.Add(pl);
                }
            }
        }

        // ------------------------------------------------------------------ helper penempatan
        static Vector2Int FreeCell(HouseLayout L, Room r, HashSet<Vector3Int> reserved, System.Random rng, int[] spotDist, int maxD)
        {
            List<Vector2Int> c = new List<Vector2Int>(), inner = new List<Vector2Int>();
            foreach (Vector2Int v in r.rect.allPositionsWithin)
            {
                Vector3Int n = G.N(r.floor, v);
                if (L.Blocked(n) || reserved.Contains(n)) continue;
                if (spotDist != null)
                {
                    int d = spotDist[L.Idx(n)];
                    if (d < 0 || d > maxD) continue;
                }
                c.Add(v);
                if (v.x > r.rect.xMin && v.x < r.rect.xMax - 1 && v.y > r.rect.yMin && v.y < r.rect.yMax - 1) inner.Add(v);
            }
            List<Vector2Int> src = inner.Count > 0 && rng.NextDouble() < 0.7 ? inner : c;
            return src.Count > 0 ? src[rng.Next(src.Count)] : new Vector2Int(-1, -1);
        }

        /// <summary>Tempatkan objek pemblokir menempel dinding; dibatalkan bila memutus konektivitas.</summary>
        static bool TryPlaceWall(HouseLayout L, Room r, HashSet<Vector3Int> reserved, System.Random rng, out Placed placed)
        {
            placed = null;
            int f = r.floor;
            List<(Vector2Int, int)> opts = new List<(Vector2Int, int)>();
            foreach (Vector2Int c in r.rect.allPositionsWithin)
            {
                Vector3Int n = G.N(f, c);
                if (reserved.Contains(n) || L.Blocked(n)) continue;
                for (int k = 0; k < 4; k++)
                {
                    if (!L.Wall(f, c, k) || L.IsDoor(f, c, k)) continue;
                    int face = G.Opp(k);
                    Vector2Int fr = c + G.Dirs[face];
                    if (!L.In(f, fr) || L.room[f, fr.x, fr.y] != r.id || L.blocked[f, fr.x, fr.y]) continue;
                    if (f == L.exitNode.z && c == G.XY(L.exitNode)) continue;
                    opts.Add((c, face));
                }
            }
            for (int tries = 0; tries < 5 && opts.Count > 0; tries++)
            {
                int i = rng.Next(opts.Count);
                var o = opts[i]; opts.RemoveAt(i);
                if (L.blocked[f, o.Item1.x, o.Item1.y]) continue;
                L.blocked[f, o.Item1.x, o.Item1.y] = true;
                if (AllConnected(L))
                {
                    reserved.Add(G.N(f, o.Item1 + G.Dirs[o.Item2]));
                    placed = new Placed { f = f, cell = o.Item1, dir = o.Item2, room = r.id };
                    return true;
                }
                L.blocked[f, o.Item1.x, o.Item1.y] = false;
                L.rejectedPlacements++;
            }
            return false;
        }

        /// <summary>Tempatkan objek di tengah ruangan (semua sisi ruang yang sama).</summary>
        static bool TryPlaceCenter(HouseLayout L, Room r, HashSet<Vector3Int> reserved, System.Random rng, out Placed placed)
        {
            placed = null;
            int f = r.floor;
            List<Vector2Int> opts = new List<Vector2Int>();
            foreach (Vector2Int c in r.rect.allPositionsWithin)
            {
                Vector3Int n = G.N(f, c);
                if (reserved.Contains(n) || L.Blocked(n)) continue;
                bool inner = true;
                for (int k = 0; k < 4; k++) if (L.Wall(f, c, k) || L.IsDoor(f, c, k)) inner = false;
                if (inner) opts.Add(c);
            }
            Shuffle(opts, rng);
            foreach (Vector2Int c in opts)
            {
                L.blocked[f, c.x, c.y] = true;
                if (AllConnected(L)) { placed = new Placed { f = f, cell = c, dir = rng.Next(4), room = r.id }; return true; }
                L.blocked[f, c.x, c.y] = false;
                L.rejectedPlacements++;
            }
            return false;
        }

        static bool AllConnected(HouseLayout L)
        {
            bool[] r = L.Reach(L.start, true, true);
            for (int i = 0; i < r.Length; i++) if (!r[i] && !L.Blocked(L.FromIdx(i))) return false;
            return true;
        }

        // ------------------------------------------------------------------ validasi
        static void Validate(HouseLayout L, PcgParams p)
        {
            bool[] all = L.Reach(L.start, false, true);
            bool[] noKey = L.Reach(L.start, false, false);
            L.freeCells = 0; L.reachable = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (L.Blocked(L.FromIdx(i))) continue;
                L.freeCells++;
                if (all[i]) L.reachable++;
            }
            List<string> why = new List<string>();
            if (L.reachable != L.freeCells) why.Add("ada sel terputus");
            if (!noKey[L.Idx(L.exitNode)]) why.Add("pintu keluar butuh kunci");
            foreach (Placed r in L.relics) if (!all[L.Idx(r.Node)]) why.Add("relik tak terjangkau");
            foreach (Placed s in L.spots) if (!L.In(s.FrontNode) || !all[L.Idx(s.FrontNode)]) why.Add("tempat sembunyi tertutup");
            if (L.relics.Count != p.relicCount) why.Add("jumlah relik kurang");
            if (L.spots.Count < Mathf.Min(2, p.hidingSpots)) why.Add("tempat sembunyi kurang");
            if (L.floors > 1 && L.stairs.Count == 0) why.Add("tangga tidak ada");
            if (L.lockedRoom >= 0)
            {
                if (L.key == null || !noKey[L.Idx(L.key.Node)]) why.Add("kunci tak terjangkau tanpa kunci");
            }
            if (L.floors > 1)
                for (int f = 0; f < L.floors; f++)
                    if (!L.relics.Exists(r => r.f == f)) why.Add("lantai " + (f + 1) + " tanpa relik");
            L.valid = why.Count == 0;
            L.failReason = string.Join(", ", why);
        }

        // ------------------------------------------------------------------ util
        static bool Touches(HouseLayout L, Room r) { return r.rect.xMin == 0 || r.rect.yMin == 0 || r.rect.xMax == L.w || r.rect.yMax == L.h; }
        static Vector2Int Center(RectInt r) { return new Vector2Int(r.x + r.width / 2, r.y + r.height / 2); }
        static int Find(Dictionary<int, int> uf, int i) { while (uf[i] != i) { uf[i] = uf[uf[i]]; i = uf[i]; } return i; }
        static void Shuffle<T>(List<T> l, System.Random rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        static string Hash(HouseLayout L)
        {
            unchecked
            {
                uint hsh = 2166136261;
                void Add(int v) { hsh = (hsh ^ (uint)v) * 16777619; }
                for (int f = 0; f < L.floors; f++)
                    for (int x = 0; x < L.w; x++)
                        for (int y = 0; y < L.h; y++) { Add(L.room[f, x, y]); Add(L.blocked[f, x, y] ? 1 : 0); }
                foreach (DoorEdge d in L.doors) { Add(d.f); Add(d.a.x); Add(d.a.y); Add(d.b.x); Add(d.b.y); Add(d.locked ? 1 : 0); }
                foreach (Stair s in L.stairs) { Add(s.bottom.x); Add(s.bottom.y); Add(s.dir); }
                foreach (Placed r in L.relics) { Add(r.f); Add(r.cell.x); Add(r.cell.y); }
                if (L.key != null) { Add(L.key.f); Add(L.key.cell.x); Add(L.key.cell.y); }
                Add(L.exitNode.x); Add(L.exitNode.y);
                return hsh.ToString("X8");
            }
        }

        public static string RoomName(RoomType t)
        {
            switch (t)
            {
                case RoomType.RuangTamu: return "Ruang Tamu";
                case RoomType.KamarTidur: return "Kamar Tidur";
                case RoomType.Dapur: return "Dapur";
                case RoomType.KamarMandi: return "Kamar Mandi";
                case RoomType.Perpustakaan: return "Perpustakaan";
                case RoomType.RuangMakan: return "Ruang Makan";
                case RoomType.KamarAnak: return "Kamar Anak";
                case RoomType.Gudang: return "Gudang";
                default: return "Ruang Keluarga";
            }
        }
        public static string SpotName(int k) { return k == (int)SpotKind.Lemari ? "Lemari" : "Kolong Ranjang"; }
    }
}
