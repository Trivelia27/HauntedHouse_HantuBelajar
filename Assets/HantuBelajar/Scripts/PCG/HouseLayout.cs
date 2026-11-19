using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    public enum RoomType { RuangTamu, KamarTidur, Dapur, KamarMandi, Perpustakaan, RuangMakan, KamarAnak, Gudang, RuangKeluarga }
    public enum SpotKind { Lemari, KolongRanjang }
    public enum PropKind { Meja, MejaMakan, Kursi, Rak, Peti, Bak, Dapur, Kulkas, Perapian, Wastafel }

    /// <summary>Parameter utama PCG (sebagian diatur DDA untuk rumah berikutnya).</summary>
    [System.Serializable]
    public class PcgParams
    {
        public int seed = 12345;
        public int width = 7, height = 6;      // ukuran denah per lantai (sel 3,6 m)
        public int floors = 2;                 // jumlah lantai (1-2), dihubungkan tangga
        public int minRoom = 2, maxRoom = 3;   // batas ukuran ruangan BSP (sel)
        public float loopChance = 0.3f;        // peluang pintu tambahan (jalur alternatif)
        public int relicCount = 3;             // relik yang harus dikumpulkan
        public int hidingSpots = 7;            // lemari + kolong ranjang
        public int batteries = 3;              // baterai senter
        public float propDensity = 0.35f;      // kepadatan perabot
        public int lockedRooms = 1;            // ruang terkunci (butuh kunci)
        public int relicSpotDist = 3;          // jarak maks relik ke tempat sembunyi terdekat (sel); 0 = bebas
        public float litRatio = 0.7f;          // proporsi ruangan yang lampunya menyala
        public PcgParams Clone() { return (PcgParams)MemberwiseClone(); }
    }

    public class Room
    {
        public int id, floor, depth = -1;
        public RectInt rect;
        public RoomType type;
        public bool lit = true, locked, hasStairs;
        public List<int> links = new List<int>();
        public int Area { get { return rect.width * rect.height; } }
    }

    public struct DoorEdge { public int f; public Vector2Int a, b; public bool loop, locked; }

    public class Placed
    {
        public int id, f, dir, kind, room;
        public Vector2Int cell;
        public Vector3Int Node { get { return new Vector3Int(cell.x, cell.y, f); } }
        public Vector3Int FrontNode { get { Vector2Int c = cell + G.Dirs[dir]; return new Vector3Int(c.x, c.y, f); } }
    }

    /// <summary>Tangga lurus: sel bawah -> 1 sel tangga (naik 3 m sepanjang 3,6 m, +-40 derajat) -> sel pendaratan di lantai atas.</summary>
    public class Stair
    {
        public int f, dir;
        public Vector2Int bottom;
        public Vector2Int Ramp1 { get { return bottom + G.Dirs[dir]; } }
        public Vector2Int Landing { get { return bottom + G.Dirs[dir] * 2; } }
        public Vector3Int BottomNode { get { return new Vector3Int(bottom.x, bottom.y, f); } }
        public Vector3Int LandingNode { get { Vector2Int l = Landing; return new Vector3Int(l.x, l.y, f + 1); } }
        /// <summary>Titik awal & akhir permukaan ramp (dunia).</summary>
        public Vector3 RampStart { get { return G.W(f, bottom) + G.D3(dir) * (G.T * 0.5f); } }
        public Vector3 RampEnd { get { return G.W(f, bottom) + G.D3(dir) * (G.T * 1.5f) + Vector3.up * G.StoryH; } }
    }

    /// <summary>Hasil PCG: data murni (tanpa GameObject), bisa diuji tanpa Unity scene.</summary>
    public class HouseLayout
    {
        public PcgParams p;
        public int w, h, floors, usedSeed, attempts;
        public int[,,] room;          // [f,x,y] id ruangan
        public bool[,,] blocked;      // sel tidak bisa dilewati (perabot, ramp, lubang tangga)
        public bool[,,] hole;         // lantai atas yang berlubang untuk tangga
        public List<Room> rooms = new List<Room>();
        public List<DoorEdge> doors = new List<DoorEdge>();
        public HashSet<long> doorKeys = new HashSet<long>();
        public HashSet<long> lockedKeys = new HashSet<long>();
        public List<Stair> stairs = new List<Stair>();
        public Vector3Int start, exitNode, ghostStart;
        public int exitDir, lockedRoom = -1;
        public Placed key;
        public List<Placed> spots = new List<Placed>(), relics = new List<Placed>(), batteries = new List<Placed>(), props = new List<Placed>();
        public bool valid;
        public int loops, rejectedPlacements, reachable, freeCells, relicSpotMax, relicSpotRelax;
        public string hash = "-", failReason = "";
        public float ms;

        public bool In(int f, Vector2Int c) { return f >= 0 && f < floors && c.x >= 0 && c.y >= 0 && c.x < w && c.y < h; }
        public bool In(Vector3Int n) { return In(n.z, new Vector2Int(n.x, n.y)); }
        public int Idx(Vector3Int n) { return (n.z * h + n.y) * w + n.x; }
        public Vector3Int FromIdx(int i) { int x = i % w, r = i / w; return new Vector3Int(x, r % h, r / h); }
        public int Count { get { return w * h * floors; } }
        public bool Blocked(Vector3Int n) { return blocked[n.z, n.x, n.y]; }
        public Room RoomAt(Vector3Int n) { return In(n) ? rooms[room[n.z, n.x, n.y]] : null; }

        public long Key(int f, Vector2Int a, Vector2Int b)
        {
            int ia = (f * h + a.y) * w + a.x, ib = (f * h + b.y) * w + b.x;
            return (long)Mathf.Min(ia, ib) * 1000003L + Mathf.Max(ia, ib);
        }
        /// <summary>true bila ada dinding (atau pintu terkunci dianggap dinding untuk pemain) di sisi dir sel c.</summary>
        public bool Wall(int f, Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            if (!In(f, b)) return true;
            if (room[f, c.x, c.y] == room[f, b.x, b.y]) return false;
            return !doorKeys.Contains(Key(f, c, b));
        }
        public bool IsDoor(int f, Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            return In(f, b) && room[f, c.x, c.y] != room[f, b.x, b.y] && doorKeys.Contains(Key(f, c, b));
        }
        public bool IsLocked(int f, Vector2Int c, int dir)
        {
            Vector2Int b = c + G.Dirs[dir];
            return In(f, b) && lockedKeys.Contains(Key(f, c, b));
        }
        public Stair StairAt(Vector3Int n)
        {
            foreach (Stair s in stairs) if (s.BottomNode == n || s.LandingNode == n) return s;
            return null;
        }
        /// <summary>Apakah (f,c) adalah sel ramp/lubang tangga.</summary>
        public Stair StairCell(int f, Vector2Int c)
        {
            foreach (Stair s in stairs)
                if ((f == s.f || f == s.f + 1) && c == s.Ramp1) return s;
            return null;
        }

        /// <summary>Tetangga node pada graf. ghost = hantu bisa menembus pintu terkunci; hasKey = pemain punya kunci.</summary>
        public void Neighbors(Vector3Int n, bool ghost, bool hasKey, List<Vector3Int> res)
        {
            res.Clear();
            Vector2Int c = new Vector2Int(n.x, n.y);
            int f = n.z;
            for (int k = 0; k < 4; k++)
            {
                Vector2Int b = c + G.Dirs[k];
                if (!In(f, b) || Wall(f, c, k) || blocked[f, b.x, b.y]) continue;
                if (!ghost && !hasKey && IsLocked(f, c, k)) continue;
                res.Add(new Vector3Int(b.x, b.y, f));
            }
            foreach (Stair s in stairs)
            {
                if (n == s.BottomNode) res.Add(s.LandingNode);
                else if (n == s.LandingNode) res.Add(s.BottomNode);
            }
        }

        readonly List<Vector3Int> nb = new List<Vector3Int>();

        /// <summary>Jarak terpendek (Dijkstra, tangga = 2 langkah) dari node; -1 = tak terjangkau.</summary>
        public int[] Distances(Vector3Int from, bool ghost = false, bool hasKey = true)
        {
            int n = Count;
            int[] d = new int[n];
            bool[] done = new bool[n];
            for (int i = 0; i < n; i++) d[i] = int.MaxValue;
            if (In(from))
            {
                d[Idx(from)] = 0;
                List<Vector3Int> local = new List<Vector3Int>();
                while (true)
                {
                    int cur = -1, best = int.MaxValue;
                    for (int i = 0; i < n; i++) if (!done[i] && d[i] < best) { best = d[i]; cur = i; }
                    if (cur < 0) break;
                    done[cur] = true;
                    Vector3Int c = FromIdx(cur);
                    Neighbors(c, ghost, hasKey, local);
                    foreach (Vector3Int m in local)
                    {
                        int mi = Idx(m);
                        int nd = best + (m.z != c.z ? 2 : 1);
                        if (nd < d[mi]) d[mi] = nd;
                    }
                }
            }
            for (int i = 0; i < n; i++) if (d[i] == int.MaxValue) d[i] = -1;
            return d;
        }

        /// <summary>Cek cepat keterjangkauan (BFS) - dipakai saat penempatan generate-and-test.</summary>
        public bool[] Reach(Vector3Int from, bool ghost, bool hasKey)
        {
            bool[] r = new bool[Count];
            if (!In(from) || Blocked(from)) return r;
            Queue<Vector3Int> q = new Queue<Vector3Int>();
            r[Idx(from)] = true; q.Enqueue(from);
            List<Vector3Int> local = new List<Vector3Int>();
            while (q.Count > 0)
            {
                Vector3Int c = q.Dequeue();
                Neighbors(c, ghost, hasKey, local);
                foreach (Vector3Int m in local) { int mi = Idx(m); if (!r[mi]) { r[mi] = true; q.Enqueue(m); } }
            }
            return r;
        }
        public int Dist(int[] d, Vector3Int n) { return In(n) ? d[Idx(n)] : -1; }

        /// <summary>A* antar node (termasuk tangga). Jalur kosong bila tak terjangkau.</summary>
        public List<Vector3Int> Path(Vector3Int a, Vector3Int b, bool ghost, bool hasKey)
        {
            List<Vector3Int> res = new List<Vector3Int>();
            if (!In(a) || !In(b) || Blocked(b)) return res;
            int n = Count;
            int[] g = new int[n]; int[] par = new int[n]; bool[] closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = int.MaxValue; par[i] = -1; }
            List<int> open = new List<int>();
            int s = Idx(a), t = Idx(b);
            g[s] = 0; open.Add(s);
            while (open.Count > 0)
            {
                int bi = 0, bf = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    Vector3Int v = FromIdx(open[i]);
                    int f = g[open[i]] + Mathf.Abs(v.x - b.x) + Mathf.Abs(v.y - b.y) + Mathf.Abs(v.z - b.z) * 2;
                    if (f < bf) { bf = f; bi = i; }
                }
                int cur = open[bi]; open.RemoveAt(bi);
                if (cur == t) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                Vector3Int cv = FromIdx(cur);
                Neighbors(cv, ghost, hasKey, nb);
                foreach (Vector3Int m in nb)
                {
                    int mi = Idx(m);
                    if (closed[mi]) continue;
                    int ng = g[cur] + (m.z != cv.z ? 2 : 1);
                    if (ng < g[mi]) { g[mi] = ng; par[mi] = cur; open.Add(mi); }
                }
            }
            if (par[t] < 0 && s != t) return res;
            for (int c = t; c != -1; c = par[c]) { res.Add(FromIdx(c)); if (c == s) break; }
            res.Reverse();
            return res;
        }

        /// <summary>Node graf terdekat untuk posisi dunia (dipakai pemain/hantu, termasuk saat di tangga).</summary>
        public Vector3Int NodeAt(Vector3 p)
        {
            Vector2Int c = G.C(p);
            c.x = Mathf.Clamp(c.x, 0, w - 1); c.y = Mathf.Clamp(c.y, 0, h - 1);
            foreach (Stair s in stairs)
            {
                if (c == s.Ramp1)
                {
                    float rel = (p.y - s.f * G.StoryH) / G.StoryH;
                    if (rel > -0.4f && rel < 1.4f) return rel < 0.5f ? s.BottomNode : s.LandingNode;
                }
            }
            int f = G.F(p.y, floors);
            Vector3Int n = new Vector3Int(c.x, c.y, f);
            if (!blocked[f, c.x, c.y]) return n;
            // sel terblokir (berdiri di samping perabot): pilih tetangga bebas terdekat yang terhubung
            Vector3Int best = n; float bd = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                Vector2Int b = c + G.Dirs[k];
                if (!In(f, b) || blocked[f, b.x, b.y] || Wall(f, c, k)) continue;
                float dd = (G.W(f, b) - p).sqrMagnitude;
                if (dd < bd) { bd = dd; best = new Vector3Int(b.x, b.y, f); }
            }
            return best;
        }

        /// <summary>Ubah jalur node menjadi titik dunia (menyisipkan titik ramp tangga).</summary>
        public List<Vector3> Waypoints(List<Vector3Int> path)
        {
            List<Vector3> pts = new List<Vector3>();
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0 && path[i].z != path[i - 1].z)
                {
                    Stair s = StairAt(path[i - 1]) ?? StairAt(path[i]);
                    if (s != null)
                    {
                        bool up = path[i].z > path[i - 1].z;
                        pts.Add(up ? s.RampStart : s.RampEnd);
                        pts.Add(up ? s.RampEnd : s.RampStart);
                    }
                }
                pts.Add(G.W(path[i]));
            }
            return pts;
        }

        public string FloorName(int f) { return f == 0 ? "Lantai 1" : "Lantai 2"; }
    }
}
