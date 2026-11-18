using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Konstanta grid rumah: 1 sel = 3 x 3 meter.</summary>
    public static class G
    {
        public const float T = 3f;
        public const float WallH = 3f;
        // 0 = utara (+z), 1 = timur (+x), 2 = selatan (-z), 3 = barat (-x)
        public static readonly Vector2Int[] Dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
        public static Vector3 W(Vector2Int c) { return new Vector3(c.x * T + T * 0.5f, 0f, c.y * T + T * 0.5f); }
        public static Vector2Int C(Vector3 p) { return new Vector2Int(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(p.z / T)); }
        public static Vector3 D3(int dir) { Vector2Int d = Dirs[dir]; return new Vector3(d.x, 0f, d.y); }
        public static float Yaw(int dir) { return dir * 90f; }
    }

    public enum RoomType { RuangTamu, KamarTidur, Dapur, KamarMandi, Perpustakaan, RuangMakan, KamarAnak, Gudang, RuangKeluarga }
    public enum SpotKind { Lemari, KolongRanjang }
    public enum PropKind { Meja, Kursi, Rak, Peti, Bak, Piano, Lampu }

    /// <summary>Parameter utama PCG (sebagian diatur oleh DDA untuk rumah berikutnya).</summary>
    [System.Serializable]
    public class PcgParams
    {
        public int seed = 12345;
        public int width = 14, height = 10;     // ukuran rumah dalam sel
        public int minRoom = 3, maxRoom = 5;    // batas ukuran ruangan BSP
        public float loopChance = 0.25f;        // peluang pintu tambahan (jalur alternatif / loop)
        public int relicCount = 3;              // jumlah relik yang harus dikumpulkan
        public int hidingSpots = 7;             // jumlah tempat sembunyi
        public int batteries = 3;               // jumlah baterai senter
        public float propDensity = 0.35f;       // kepadatan perabot
        public PcgParams Clone() { return (PcgParams)MemberwiseClone(); }
    }

    public class Room { public int id; public RectInt rect; public RoomType type; public int depth = -1; public bool lit = true; public List<int> links = new List<int>(); }
    public struct DoorEdge { public Vector2Int a, b; public bool loop; }
    public class Placed { public int id; public Vector2Int cell; public int dir; public int kind; public int room; }

    public class HouseLayout
    {
        public PcgParams p;
        public int w, h, usedSeed, attempts;
        public int[,] room;
        public bool[,] blocked;
        public List<Room> rooms = new List<Room>();
        public List<DoorEdge> doors = new List<DoorEdge>();
        public HashSet<long> doorKeys = new HashSet<long>();
        public Vector2Int start, exitCell, ghostStart;
        public int exitDir;
        public List<Placed> spots = new List<Placed>(), relics = new List<Placed>(), batteries = new List<Placed>(), props = new List<Placed>();
        public bool valid;
        public int loops, rejectedPlacements, reachable, freeCells;
        public string hash = "-";
        public float ms;

        public bool In(Vector2Int c) { return c.x >= 0 && c.y >= 0 && c.x < w && c.y < h; }
        public long Key(Vector2Int a, Vector2Int b)
        {
            int ia = a.y * w + a.x, ib = b.y * w + b.x;
            return (long)Mathf.Min(ia, ib) * 100000L + Mathf.Max(ia, ib);
        }
        /// <summary>true bila ada dinding di sisi dir dari sel c.</summary>
        public bool Wall(Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            if (!In(b)) return true;
            if (room[c.x, c.y] == room[b.x, b.y]) return false;
            return !doorKeys.Contains(Key(c, b));
        }
        public bool IsDoor(Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            return In(b) && room[c.x, c.y] != room[b.x, b.y] && doorKeys.Contains(Key(c, b));
        }
        public bool CanMove(Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            return In(b) && !Wall(c, dir) && !blocked[b.x, b.y];
        }
        public Room RoomAt(Vector2Int c) { return In(c) ? rooms[room[c.x, c.y]] : null; }

        /// <summary>BFS jarak sel dari titik awal (sel terblokir = -1).</summary>
        public int[,] Distances(Vector2Int from)
        {
            int[,] d = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) d[x, y] = -1;
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            d[from.x, from.y] = 0; q.Enqueue(from);
            while (q.Count > 0)
            {
                Vector2Int c = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    if (!CanMove(c, k)) continue;
                    Vector2Int n = c + G.Dirs[k];
                    if (d[n.x, n.y] >= 0) continue;
                    d[n.x, n.y] = d[c.x, c.y] + 1; q.Enqueue(n);
                }
            }
            return d;
        }

        /// <summary>A* pada grid (dipakai hantu).</summary>
        public List<Vector2Int> Path(Vector2Int a, Vector2Int b)
        {
            List<Vector2Int> res = new List<Vector2Int>();
            if (!In(a) || !In(b)) return res;
            if (blocked[b.x, b.y]) return res;
            int n = w * h;
            int[] g = new int[n]; int[] par = new int[n]; bool[] closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = int.MaxValue; par[i] = -1; }
            List<int> open = new List<int>();
            int s = a.y * w + a.x, t = b.y * w + b.x;
            g[s] = 0; open.Add(s);
            while (open.Count > 0)
            {
                int bi = 0, bf = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int id = open[i]; int f = g[id] + Mathf.Abs(id % w - b.x) + Mathf.Abs(id / w - b.y);
                    if (f < bf) { bf = f; bi = i; }
                }
                int cur = open[bi]; open.RemoveAt(bi);
                if (cur == t) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                Vector2Int c = new Vector2Int(cur % w, cur / w);
                for (int k = 0; k < 4; k++)
                {
                    Vector2Int nb = c + G.Dirs[k];
                    if (!In(nb) || Wall(c, k)) continue;
                    if (blocked[nb.x, nb.y] && nb != a) continue;
                    int ni = nb.y * w + nb.x;
                    if (closed[ni]) continue;
                    int ng = g[cur] + 1;
                    if (ng < g[ni]) { g[ni] = ng; par[ni] = cur; open.Add(ni); }
                }
            }
            if (par[t] < 0 && s != t) return res;
            for (int c = t; c != -1; c = par[c]) { res.Add(new Vector2Int(c % w, c / w)); if (c == s) break; }
            res.Reverse();
            return res;
        }
    }

    /// <summary>
    /// PCG rumah berhantu: BSP membagi denah menjadi ruangan -> graf ketetanggaan ->
    /// spanning tree acak (Kruskal) + pintu loop -> start/exit dari kedalaman BFS ->
    /// relik, tempat sembunyi, baterai, perabot ditempatkan dengan generate-and-test (cek konektivitas BFS).
    /// </summary>
    public static class HouseGenerator
    {
        public const int MaxAttempts = 12;

        public static HouseLayout Generate(PcgParams p)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HouseLayout last = null;
            for (int a = 0; a < MaxAttempts; a++)
            {
                HouseLayout L = TryGenerate(p, p.seed + a * 7919);
                L.attempts = a + 1;
                last = L;
                if (L.valid) break;
            }
            last.ms = (float)sw.Elapsed.TotalMilliseconds;
            return last;
        }

        static HouseLayout TryGenerate(PcgParams p, int seed)
        {
            System.Random rng = new System.Random(seed);
            HouseLayout L = new HouseLayout { p = p, w = p.width, h = p.height, usedSeed = seed };
            L.room = new int[L.w, L.h];
            L.blocked = new bool[L.w, L.h];

            // 1. BSP
            List<RectInt> leaves = new List<RectInt>();
            Split(new RectInt(0, 0, L.w, L.h), rng, p, leaves, 0);
            for (int i = 0; i < leaves.Count; i++)
            {
                L.rooms.Add(new Room { id = i, rect = leaves[i] });
                foreach (Vector2Int c in leaves[i].allPositionsWithin) L.room[c.x, c.y] = i;
            }

            // 2. graf ketetanggaan antar-ruangan
            Dictionary<long, List<DoorEdge>> shared = new Dictionary<long, List<DoorEdge>>();
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2Int a = new Vector2Int(x, y), b = a + G.Dirs[k];
                        if (!L.In(b)) continue;
                        int ra = L.room[a.x, a.y], rb = L.room[b.x, b.y];
                        if (ra == rb) continue;
                        long pk = (long)Mathf.Min(ra, rb) * 1000 + Mathf.Max(ra, rb);
                        if (!shared.TryGetValue(pk, out List<DoorEdge> list)) shared[pk] = list = new List<DoorEdge>();
                        list.Add(new DoorEdge { a = a, b = b });
                    }
            List<long> pairs = new List<long>(shared.Keys);
            pairs.Sort();
            Shuffle(pairs, rng);

            // 3. spanning tree (Kruskal + union-find) lalu pintu loop
            int[] uf = new int[L.rooms.Count];
            for (int i = 0; i < uf.Length; i++) uf[i] = i;
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

            // 4. start, kedalaman, exit
            List<Room> boundaryRooms = L.rooms.FindAll(r => r.rect.xMin == 0 || r.rect.yMin == 0 || r.rect.xMax == L.w || r.rect.yMax == L.h);
            Room startRoom = boundaryRooms.Count > 0 ? boundaryRooms[rng.Next(boundaryRooms.Count)] : L.rooms[0];
            Queue<Room> q = new Queue<Room>();
            startRoom.depth = 0; q.Enqueue(startRoom);
            while (q.Count > 0)
            {
                Room r = q.Dequeue();
                foreach (int li in r.links) if (L.rooms[li].depth < 0) { L.rooms[li].depth = r.depth + 1; q.Enqueue(L.rooms[li]); }
            }
            L.start = Center(startRoom.rect);

            Room exitRoom = null;
            foreach (Room r in boundaryRooms)
                if (r != startRoom && (exitRoom == null || r.depth > exitRoom.depth || (r.depth == exitRoom.depth && rng.NextDouble() < 0.5))) exitRoom = r;
            if (exitRoom == null) return L;
            List<(Vector2Int, int)> outer = new List<(Vector2Int, int)>();
            foreach (Vector2Int c in exitRoom.rect.allPositionsWithin)
                for (int k = 0; k < 4; k++) if (!L.In(c + G.Dirs[k])) outer.Add((c, k));
            var ex = outer[rng.Next(outer.Count)];
            L.exitCell = ex.Item1; L.exitDir = ex.Item2;

            // 5. tipe ruangan
            RoomType[] pool = { RoomType.KamarTidur, RoomType.Dapur, RoomType.KamarMandi, RoomType.Perpustakaan, RoomType.RuangMakan, RoomType.KamarAnak, RoomType.Gudang, RoomType.KamarTidur };
            foreach (Room r in L.rooms)
            {
                int area = r.rect.width * r.rect.height;
                if (r == startRoom) r.type = RoomType.RuangTamu;
                else if (area >= 20 && rng.NextDouble() < 0.5) r.type = rng.NextDouble() < 0.5 ? RoomType.RuangKeluarga : RoomType.RuangMakan;
                else if (area <= 9 && rng.NextDouble() < 0.5) r.type = rng.NextDouble() < 0.5 ? RoomType.KamarMandi : RoomType.Gudang;
                else r.type = pool[rng.Next(pool.Length)];
                r.lit = r == startRoom || rng.NextDouble() > 0.3;
            }

            // 6. sel yang harus tetap kosong
            HashSet<Vector2Int> reserved = new HashSet<Vector2Int> { L.start, L.exitCell };
            foreach (DoorEdge d in L.doors) { reserved.Add(d.a); reserved.Add(d.b); }

            // 7. relik di ruangan paling dalam (bukan ruang awal)
            List<Room> cand = L.rooms.FindAll(r => r != startRoom);
            Shuffle(cand, rng);
            cand.Sort((x, y) => y.depth.CompareTo(x.depth));
            if (cand.Count < p.relicCount + 1) return L;
            // ambil acak dari 2/3 teratas supaya tersebar
            int topN = Mathf.Max(p.relicCount, Mathf.CeilToInt(cand.Count * 0.7f));
            List<Room> top = cand.GetRange(0, topN);
            Shuffle(top, rng);
            for (int i = 0; i < p.relicCount; i++)
            {
                Vector2Int c = FreeCell(L, top[i].rect, reserved, rng, true);
                if (c.x < 0) return L;
                reserved.Add(c);
                L.relics.Add(new Placed { id = i, cell = c, room = top[i].id, kind = i });
            }

            // 8. tempat sembunyi (lemari / kolong ranjang) menempel dinding
            List<Room> spotRooms = L.rooms.FindAll(r => r != startRoom);
            int[] perRoom = new int[L.rooms.Count];
            for (int i = 0; i < p.hidingSpots; i++)
            {
                spotRooms.Sort((x, y) => perRoom[x.id] != perRoom[y.id] ? perRoom[x.id].CompareTo(perRoom[y.id]) : 0);
                int pickRange = 1;
                while (pickRange < spotRooms.Count && perRoom[spotRooms[pickRange].id] == perRoom[spotRooms[0].id]) pickRange++;
                Room r = spotRooms[rng.Next(pickRange)];
                bool bedroom = r.type == RoomType.KamarTidur || r.type == RoomType.KamarAnak;
                int kind = bedroom && rng.NextDouble() < 0.6 ? (int)SpotKind.KolongRanjang : (int)SpotKind.Lemari;
                if (TryPlaceWall(L, r, reserved, rng, out Placed pl))
                {
                    pl.id = L.spots.Count; pl.kind = kind;
                    L.spots.Add(pl);
                }
                perRoom[r.id]++;
            }

            // 9. baterai
            for (int i = 0; i < p.batteries; i++)
            {
                Room r = cand[rng.Next(cand.Count)];
                Vector2Int c = FreeCell(L, r.rect, reserved, rng, false);
                if (c.x < 0) continue;
                reserved.Add(c);
                L.batteries.Add(new Placed { id = i, cell = c, room = r.id });
            }

            // 10. perabot dekorasi (memblokir sel, tetap dicek konektivitasnya)
            foreach (Room r in L.rooms)
            {
                int perim = 2 * (r.rect.width + r.rect.height);
                int n = Mathf.RoundToInt(perim * p.propDensity * 0.35f);
                for (int i = 0; i < n; i++)
                {
                    if (!TryPlaceWall(L, r, reserved, rng, out Placed pl)) continue;
                    pl.id = L.props.Count; pl.kind = (int)PropFor(r.type, rng);
                    L.props.Add(pl);
                }
            }

            // 11. hantu mulai jauh dari pemain
            int[,] dist = L.Distances(L.start);
            Vector2Int best = L.start; int bestD = -1;
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    int dd = dist[x, y];
                    if (dd < 0 || L.blocked[x, y]) continue;
                    int score = dd * 10 + rng.Next(10);
                    if (score > bestD) { bestD = score; best = new Vector2Int(x, y); }
                }
            L.ghostStart = best;

            // 12. validasi: semua sel kosong terhubung, semua relik + exit + baterai terjangkau
            L.freeCells = 0; L.reachable = 0;
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    if (L.blocked[x, y]) continue;
                    L.freeCells++;
                    if (dist[x, y] >= 0) L.reachable++;
                }
            bool ok = L.reachable == L.freeCells && dist[L.exitCell.x, L.exitCell.y] >= 0;
            foreach (Placed r in L.relics) ok &= dist[r.cell.x, r.cell.y] >= 0;
            foreach (Placed b in L.batteries) ok &= dist[b.cell.x, b.cell.y] >= 0;
            foreach (Placed s in L.spots) { Vector2Int f = s.cell + G.Dirs[s.dir]; ok &= L.In(f) && dist[f.x, f.y] >= 0; }
            ok &= L.rooms.Count >= p.relicCount + 2 && L.spots.Count >= Mathf.Min(2, p.hidingSpots);
            L.valid = ok;
            L.hash = Hash(L);
            return L;
        }

        static void Split(RectInt r, System.Random rng, PcgParams p, List<RectInt> outList, int depth)
        {
            bool canV = r.width >= p.minRoom * 2, canH = r.height >= p.minRoom * 2;
            bool small = r.width <= p.maxRoom && r.height <= p.maxRoom;
            if ((!canV && !canH) || (small && depth > 1 && rng.NextDouble() < 0.4)) { outList.Add(r); return; }
            bool vertical = canV && (!canH || (r.width >= r.height ? rng.NextDouble() < 0.8 : rng.NextDouble() < 0.2));
            if (vertical)
            {
                int cut = rng.Next(p.minRoom, r.width - p.minRoom + 1);
                Split(new RectInt(r.x, r.y, cut, r.height), rng, p, outList, depth + 1);
                Split(new RectInt(r.x + cut, r.y, r.width - cut, r.height), rng, p, outList, depth + 1);
            }
            else
            {
                int cut = rng.Next(p.minRoom, r.height - p.minRoom + 1);
                Split(new RectInt(r.x, r.y, r.width, cut), rng, p, outList, depth + 1);
                Split(new RectInt(r.x, r.y + cut, r.width, r.height - cut), rng, p, outList, depth + 1);
            }
        }

        static void AddDoor(HouseLayout L, List<DoorEdge> edges, System.Random rng, bool loop, int ra, int rb)
        {
            int mid = edges.Count / 2, spread = Mathf.Max(0, edges.Count / 3);
            int i = Mathf.Clamp(mid + rng.Next(-spread, spread + 1), 0, edges.Count - 1);
            DoorEdge e = edges[i]; e.loop = loop;
            L.doors.Add(e);
            L.doorKeys.Add(L.Key(e.a, e.b));
            L.rooms[ra].links.Add(rb); L.rooms[rb].links.Add(ra);
        }

        static Vector2Int FreeCell(HouseLayout L, RectInt r, HashSet<Vector2Int> reserved, System.Random rng, bool preferInner)
        {
            List<Vector2Int> c = new List<Vector2Int>(), inner = new List<Vector2Int>();
            foreach (Vector2Int v in r.allPositionsWithin)
            {
                if (reserved.Contains(v) || L.blocked[v.x, v.y]) continue;
                c.Add(v);
                if (v.x > r.xMin && v.x < r.xMax - 1 && v.y > r.yMin && v.y < r.yMax - 1) inner.Add(v);
            }
            List<Vector2Int> src = preferInner && inner.Count > 0 ? inner : c;
            return src.Count > 0 ? src[rng.Next(src.Count)] : new Vector2Int(-1, -1);
        }

        /// <summary>Tempatkan objek pemblokir menempel dinding; dibatalkan bila memutus konektivitas.</summary>
        static bool TryPlaceWall(HouseLayout L, Room r, HashSet<Vector2Int> reserved, System.Random rng, out Placed placed)
        {
            placed = null;
            List<(Vector2Int, int)> opts = new List<(Vector2Int, int)>();
            foreach (Vector2Int c in r.rect.allPositionsWithin)
            {
                if (reserved.Contains(c) || L.blocked[c.x, c.y]) continue;
                for (int k = 0; k < 4; k++)
                {
                    if (!L.Wall(c, k) || L.IsDoor(c, k)) continue;
                    int face = (k + 2) % 4;
                    Vector2Int f = c + G.Dirs[face];
                    if (!L.In(f) || L.room[f.x, f.y] != r.id || L.blocked[f.x, f.y]) continue;
                    opts.Add((c, face));
                }
            }
            for (int tries = 0; tries < 4 && opts.Count > 0; tries++)
            {
                int i = rng.Next(opts.Count);
                var o = opts[i]; opts.RemoveAt(i);
                L.blocked[o.Item1.x, o.Item1.y] = true;
                if (AllConnected(L))
                {
                    reserved.Add(o.Item1 + G.Dirs[o.Item2]);
                    placed = new Placed { cell = o.Item1, dir = o.Item2, room = r.id };
                    return true;
                }
                L.blocked[o.Item1.x, o.Item1.y] = false;
                L.rejectedPlacements++;
            }
            return false;
        }

        static bool AllConnected(HouseLayout L)
        {
            int[,] d = L.Distances(L.start);
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                    if (!L.blocked[x, y] && d[x, y] < 0) return false;
            return true;
        }

        static PropKind PropFor(RoomType t, System.Random rng)
        {
            switch (t)
            {
                case RoomType.Perpustakaan: return rng.NextDouble() < 0.7 ? PropKind.Rak : PropKind.Meja;
                case RoomType.Dapur: case RoomType.RuangMakan: return rng.NextDouble() < 0.5 ? PropKind.Meja : PropKind.Kursi;
                case RoomType.KamarMandi: return PropKind.Bak;
                case RoomType.Gudang: return PropKind.Peti;
                case RoomType.RuangKeluarga: return rng.NextDouble() < 0.25 ? PropKind.Piano : (rng.NextDouble() < 0.5 ? PropKind.Kursi : PropKind.Rak);
                default:
                    double v = rng.NextDouble();
                    return v < 0.3 ? PropKind.Meja : v < 0.55 ? PropKind.Kursi : v < 0.8 ? PropKind.Rak : PropKind.Lampu;
            }
        }

        static Vector2Int Center(RectInt r) { return new Vector2Int(r.x + r.width / 2, r.y + r.height / 2); }
        static int Find(int[] uf, int i) { while (uf[i] != i) { uf[i] = uf[uf[i]]; i = uf[i]; } return i; }
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
                for (int x = 0; x < L.w; x++) for (int y = 0; y < L.h; y++) { Add(L.room[x, y]); Add(L.blocked[x, y] ? 1 : 0); }
                foreach (DoorEdge d in L.doors) { Add(d.a.x); Add(d.a.y); Add(d.b.x); Add(d.b.y); }
                foreach (Placed r in L.relics) { Add(r.cell.x); Add(r.cell.y); }
                Add(L.exitCell.x); Add(L.exitCell.y);
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
