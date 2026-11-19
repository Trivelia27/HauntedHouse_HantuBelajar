using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Rumah yang sudah dibangun (GameObject) + referensi objek penting.</summary>
    public class HouseInstance : MonoBehaviour
    {
        public HouseLayout L;
        public List<HidingSpot> spots = new List<HidingSpot>();
        public List<RelicPickup> relics = new List<RelicPickup>();
        public List<BatteryPickup> batteries = new List<BatteryPickup>();
        public KeyPickup key;
        public LockedDoor lockedDoor;
        public ExitDoor exit;
        public List<Lamp> lamps = new List<Lamp>();
        public int windowCount, moonLights, shadowLights;
    }

    /// <summary>Slot dinding polos (untuk lukisan, lampu dinding, cermin).</summary>
    public class WallSlot
    {
        public int f, room, k, index;
        public Vector2Int cell;
        public Vector3 pos;      // titik tengah modul di permukaan dinding (y = lantai)
        public Vector3 normal;   // menghadap ke dalam ruangan
        public bool used;
    }

    /// <summary>Mengubah HouseLayout (data PCG) menjadi rumah 3D dari modul paket Haunted House.</summary>
    public static class HouseBuilder
    {
        const float WallOff = 0.03f;     // jarak muka dinding dari garis tepi sel (dua sisi tidak z-fight)
        const float GapHalf = 1.2f;      // setengah lebar bukaan pintu (2 modul)

        public static HouseInstance Build(HouseLayout L)
        {
            GameObject rootGo = new GameObject("House_" + L.p.seed);
            Transform root = rootGo.transform;
            HouseInstance H = rootGo.AddComponent<HouseInstance>();
            H.L = L;
            System.Random rng = new System.Random(L.usedSeed ^ 0x5bd1e995);
            Dictionary<int, List<WallSlot>> slots = new Dictionary<int, List<WallSlot>>();
            foreach (Room r in L.rooms) slots[r.id] = new List<WallSlot>();

            // sisi dinding yang ditutup perabot (punggung perabot) -> tidak diberi jendela/lukisan
            HashSet<long> covered = new HashSet<long>();
            foreach (Placed p in L.props) covered.Add(FaceKey(L, p.f, p.cell, G.Opp(p.dir)));
            foreach (Placed p in L.spots) covered.Add(FaceKey(L, p.f, p.cell, G.Opp(p.dir)));

            BuildFloorsCeilings(L, root);
            int shadowBudget = 6;
            for (int f = 0; f < L.floors; f++)
                for (int x = 0; x < L.w; x++)
                    for (int y = 0; y < L.h; y++)
                    {
                        Vector2Int c = new Vector2Int(x, y);
                        for (int k = 0; k < 4; k++)
                        {
                            Vector2Int n = c + G.Dirs[k];
                            bool outer = !L.In(f, n);
                            bool isDoor = !outer && L.IsDoor(f, c, k);
                            bool wall = outer || (L.room[f, c.x, c.y] != L.room[f, n.x, n.y]);
                            if (!wall) continue;
                            bool isExit = outer && f == L.exitNode.z && c == G.XY(L.exitNode) && k == L.exitDir;
                            Room room = L.rooms[L.room[f, c.x, c.y]];
                            BuildFace(L, H, root, f, c, k, room, outer, isDoor, isExit, covered.Contains(FaceKey(L, f, c, k)), rng, slots[room.id], ref shadowBudget);
                            // struktur bersama (tiang, balok, pintu) dibuat sekali per tepi
                            if (outer || (n.x + n.y * L.w > c.x + c.y * L.w))
                            {
                                if (isDoor) BuildDoorway(L, H, root, f, c, k, L.IsLocked(f, c, k));
                            }
                        }
                    }
            BuildPosts(L, root);
            foreach (Stair s in L.stairs) BuildStairs(L, root, s);

            Furnish.All(L, H, root, rng, slots);
            return H;
        }

        public static long FaceKey(HouseLayout L, int f, Vector2Int c, int k) { return ((long)L.Idx(G.N(f, c))) * 4 + k; }

        // ------------------------------------------------------------------ lantai & plafon
        static void BuildFloorsCeilings(HouseLayout L, Transform root)
        {
            Material ceil = Look.Plaster;
            for (int f = 0; f < L.floors; f++)
                for (int x = 0; x < L.w; x++)
                    for (int y = 0; y < L.h; y++)
                    {
                        Vector2Int c = new Vector2Int(x, y);
                        Vector3 center = G.W(f, c);
                        Room r = L.rooms[L.room[f, x, y]];
                        if (!L.hole[f, x, y])
                        {
                            float th = f == 0 ? 0.3f : 0.12f;
                            Kit.Box(root, "Floor", center + Vector3.down * (th * 0.5f), new Vector3(G.T, th, G.T), Look.Floor(r.type));
                        }
                        bool openAbove = f + 1 < L.floors && L.hole[f + 1, x, y];
                        if (!openAbove)
                        {
                            float top = f + 1 < L.floors ? (f + 1) * G.StoryH - 0.12f : f * G.StoryH + G.WallH + 0.3f;
                            float bot = f * G.StoryH + G.WallH;
                            GameObject cg = Kit.Box(root, "Ceiling", new Vector3(center.x, (top + bot) * 0.5f, center.z), new Vector3(G.T, top - bot, G.T), ceil, false);
                            cg.layer = 8;
                        }
                    }
        }

        // ------------------------------------------------------------------ dinding
        static void BuildFace(HouseLayout L, HouseInstance H, Transform root, int f, Vector2Int c, int k, Room room, bool outer, bool isDoor, bool isExit,
                              bool covered, System.Random rng, List<WallSlot> slots, ref int shadowBudget)
        {
            Vector3 nrm = -G.D3(k);
            Vector3 right = new Vector3(nrm.z, 0f, -nrm.x);
            Vector3 mid = G.W(f, c) + G.D3(k) * (G.T * 0.5f);
            Vector3 s0 = mid - right * (G.T * 0.5f);
            float yaw = Mathf.Atan2(nrm.x, nrm.z) * Mathf.Rad2Deg;
            Material wm = AssetLibrary.Mat(Look.WallKey(room.type));
            GameObject[] walls = AssetLibrary.Models("wall");

            if (isDoor)
            {
                Module(walls, rng, root, s0, right, nrm, yaw, f, 0f, 0.6f, wm, room.type, 1f);
                Module(walls, rng, root, s0, right, nrm, yaw, f, 3.0f, 0.6f, wm, room.type, 1f);
                return;
            }
            if (isExit)
            {
                Module(walls, rng, root, s0, right, nrm, yaw, f, 0f, G.M, wm, room.type, 1f);
                Module(walls, rng, root, s0, right, nrm, yaw, f, 2f * G.M, G.M, wm, room.type, 1f);
                // dinding di atas pintu
                float doorTop = 2.22f;
                Module(walls, rng, root, s0, right, nrm, yaw, f, G.M, G.M, wm, room.type, (G.WallH - doorTop) / G.WallH, doorTop);
                Vector3 doorPos = s0 + right * (G.M * 1.5f) + nrm * WallOff;
                GameObject eg = new GameObject("ExitDoor");
                eg.transform.SetParent(root, false);
                eg.transform.position = doorPos;
                eg.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                H.exit = eg.AddComponent<ExitDoor>();
                H.exit.Build(doorTop);
                return;
            }

            // pola modul: jendela hanya di dinding luar yang tidak tertutup perabot
            int pattern = 0;   // 0 = dinding polos, 1 = jendela tengah, 2 = jendela tiga
            if (outer && !covered)
            {
                double v = rng.NextDouble();
                if (v < 0.42) pattern = 1;
                else if (v < 0.52 && AssetLibrary.Models("window3").Length > 0) pattern = 2;
            }
            if (pattern == 2)
            {
                Module(AssetLibrary.Models("window3"), rng, root, s0, right, nrm, yaw, f, 0f, G.T, wm, room.type, 1f);
                H.windowCount++;
                MoonLight(H, root, s0 + right * (G.T * 0.5f), nrm, f, ref shadowBudget, rng);
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                bool win = pattern == 1 && i == 1;
                GameObject[] set = win && AssetLibrary.Models("window").Length > 0 ? AssetLibrary.Models("window") : walls;
                Module(set, rng, root, s0, right, nrm, yaw, f, G.M * i, G.M, wm, room.type, 1f);
                if (win) { H.windowCount++; MoonLight(H, root, s0 + right * (G.M * 1.5f), nrm, f, ref shadowBudget, rng); }
                else if (!covered)
                    slots.Add(new WallSlot { f = f, room = room.id, cell = c, k = k, index = i, pos = s0 + right * (G.M * (i + 0.5f)) + nrm * WallOff, normal = nrm });
            }
        }

        /// <summary>Satu modul dinding paket (atau kotak fallback) menempati [start, start+len] sepanjang 'right'.</summary>
        static void Module(GameObject[] set, System.Random rng, Transform root, Vector3 s0, Vector3 right, Vector3 nrm, float yaw, int f,
                           float start, float len, Material wallMat, RoomType rt, float yScale, float yBase = 0f)
        {
            Vector3 basePos = s0 + right * start + nrm * WallOff + Vector3.up * yBase;   // s0 sudah berada di ketinggian lantai f
            if (set == null || set.Length == 0)
            {
                // fallback: kotak tipis
                float h = G.WallH * yScale;
                Kit.Box(root, "WallFb", basePos + right * (len * 0.5f) + nrm * 0.03f + Vector3.up * (h * 0.5f), new Vector3(len, h, 0.06f), Look.WallFallback(rt), true, Quaternion.LookRotation(nrm));
                return;
            }
            GameObject pf = set[rng.Next(set.Length)];
            Bounds b = Kit.RawBounds(pf);
            float sx = len / Mathf.Max(0.01f, b.size.x * G.S);
            Vector3 pos = basePos - right * (b.min.x * G.S * sx) - nrm * (Mathf.Max(0f, b.min.z) * G.S);
            GameObject w = Kit.Spawn(pf, root, pos, yaw, G.S, new Vector3(sx, yScale, 1f));
            if (wallMat != null) Kit.SwapMaterial(w, "Wall_Corridor", wallMat);
            // collider dinding: kotak sederhana setebal 0,12 m (lebih andal dari mesh)
            BoxCollider bc = w.AddComponent<BoxCollider>();
            bc.center = new Vector3(len * 0.5f, G.WallH * yScale * 0.5f, 0.06f);
            bc.size = new Vector3(len, G.WallH * yScale, 0.12f);
            // modul 'right' lokal = sumbu x pembungkus; kompensasi offset b.min.x dalam koordinat pembungkus
            bc.center += new Vector3(b.min.x * G.S * sx, 0f, Mathf.Max(0f, b.min.z) * G.S);
        }

        static void MoonLight(HouseInstance H, Transform root, Vector3 windowCenterOnEdge, Vector3 nrm, int f, ref int shadowBudget, System.Random rng)
        {
            if (H.moonLights >= 14 || rng.NextDouble() < 0.35) return;
            GameObject g = new GameObject("MoonLight");
            g.transform.SetParent(root, false);
            Vector3 p = windowCenterOnEdge - nrm * 1.6f + Vector3.up * 2.6f;
            g.transform.position = p;
            g.transform.rotation = Quaternion.LookRotation((nrm * 1.6f + Vector3.down * 1.1f).normalized);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.55f, 0.66f, 1f);
            l.intensity = 5f;
            l.range = 10f;
            l.spotAngle = 62f; l.innerSpotAngle = 30f;
            if (shadowBudget > 0) { l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f; shadowBudget--; H.shadowLights++; }
            else l.shadows = LightShadows.None;
            H.moonLights++;
        }

        // ------------------------------------------------------------------ pintu & tiang
        static void BuildDoorway(HouseLayout L, HouseInstance H, Transform root, int f, Vector2Int c, int k, bool locked)
        {
            Vector3 mid = G.W(f, c) + G.D3(k) * (G.T * 0.5f);
            Vector3 along = new Vector3(G.D3(k).z, 0f, -G.D3(k).x);
            GameObject post = AssetLibrary.Model("post");
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = mid + along * (GapHalf * s);
                if (post != null)
                {
                    GameObject pg = Kit.Spawn(post, root, p, Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg);
                    BoxCollider bc = pg.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 1.35f, 0f); bc.size = new Vector3(0.45f, 2.7f, 0.5f);
                }
                else Kit.Box(root, "Post", p + Vector3.up * (G.WallH * 0.5f), new Vector3(0.4f, G.WallH, 0.4f), Look.DarkWood);
            }
            // balok kusen di atas bukaan
            float h = G.WallH - G.DoorH;
            Quaternion rot = Quaternion.LookRotation(G.D3(k));
            Kit.Box(root, "Header", mid + Vector3.up * (G.DoorH + h * 0.5f), new Vector3(GapHalf * 2f + 0.3f, h, 0.32f), Look.DarkWood, false, rot);
            Kit.Box(root, "HeaderTrim", mid + Vector3.up * (G.DoorH - 0.04f), new Vector3(GapHalf * 2f + 0.1f, 0.08f, 0.38f), Look.Wood, false, rot);
            if (locked)
            {
                GameObject dg = new GameObject("LockedDoor");
                dg.transform.SetParent(root, false);
                dg.transform.position = mid;
                dg.transform.rotation = Quaternion.LookRotation(G.D3(k));
                H.lockedDoor = dg.AddComponent<LockedDoor>();
                H.lockedDoor.Build(GapHalf - 0.22f, G.DoorH - 0.05f);
            }
        }

        static void BuildPosts(HouseLayout L, Transform root)
        {
            GameObject post = AssetLibrary.Model("post");
            for (int f = 0; f < L.floors; f++)
            {
                HashSet<Vector2Int> verts = new HashSet<Vector2Int>();
                for (int x = 0; x < L.w; x++)
                    for (int y = 0; y < L.h; y++)
                    {
                        Vector2Int c = new Vector2Int(x, y);
                        for (int k = 0; k < 4; k++)
                        {
                            Vector2Int n = c + G.Dirs[k];
                            bool wall = !L.In(f, n) || L.room[f, c.x, c.y] != L.room[f, n.x, n.y];
                            if (!wall) continue;
                            // titik sudut tepi k dari sel c
                            Vector2Int a, b;
                            switch (k)
                            {
                                case 0: a = new Vector2Int(x, y + 1); b = new Vector2Int(x + 1, y + 1); break;
                                case 1: a = new Vector2Int(x + 1, y); b = new Vector2Int(x + 1, y + 1); break;
                                case 2: a = new Vector2Int(x, y); b = new Vector2Int(x + 1, y); break;
                                default: a = new Vector2Int(x, y); b = new Vector2Int(x, y + 1); break;
                            }
                            verts.Add(a); verts.Add(b);
                        }
                    }
                foreach (Vector2Int v in verts)
                {
                    Vector3 p = new Vector3(v.x * G.T, f * G.StoryH, v.y * G.T);
                    if (post != null) Kit.Spawn(post, root, p, 0f);
                    else Kit.Box(root, "Post", p + Vector3.up * (G.WallH * 0.5f), new Vector3(0.36f, G.WallH, 0.36f), Look.DarkWood, false);
                }
            }
        }

        // ------------------------------------------------------------------ tangga
        static void BuildStairs(HouseLayout L, Transform root, Stair s)
        {
            GameObject sg = new GameObject("Stairs");
            sg.transform.SetParent(root, false);
            Vector3 d = G.D3(s.dir);
            Vector3 side = new Vector3(d.z, 0f, -d.x);
            Vector3 start = s.RampStart;
            float run = G.T, rise = G.StoryH, width = G.T - 0.5f;
            const int N = 15;
            float tread = run / N, step = rise / N;
            Quaternion rot = Quaternion.LookRotation(d);
            Material treadM = Look.DarkWood, riserM = Look.Wood;
            for (int i = 0; i < N; i++)
            {
                float hgt = step * (i + 1);
                Vector3 c = start + d * (tread * (i + 0.5f)) + Vector3.up * (hgt * 0.5f);
                Kit.Box(sg.transform, "Step", c, new Vector3(width, hgt, tread + 0.01f), i % 2 == 0 ? treadM : riserM, false, rot);
                Kit.Box(sg.transform, "Nosing", start + d * (tread * (i + 1) - 0.02f) + Vector3.up * (hgt + 0.015f), new Vector3(width + 0.04f, 0.03f, tread + 0.06f), treadM, false, rot);
            }
            // permukaan ramp (collider halus untuk CharacterController)
            Vector3 a = start, b = s.RampEnd;
            Vector3 dir = (b - a).normalized;
            Vector3 up = Vector3.Cross(dir, side).normalized;
            if (up.y < 0) up = -up;
            GameObject ramp = new GameObject("RampCollider");
            ramp.transform.SetParent(sg.transform, false);
            ramp.transform.position = (a + b) * 0.5f - up * 0.15f + up * 0.02f;
            ramp.transform.rotation = Quaternion.LookRotation(dir, up);
            BoxCollider rc = ramp.AddComponent<BoxCollider>();
            rc.size = new Vector3(width, 0.3f, (b - a).magnitude + 0.2f);
            // pagar samping: tiang + pegangan miring + dinding penghalang (collider)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 o = side * (width * 0.5f + 0.06f) * sd;
                // papan sisi (stringer) menutup samping tangga
                for (int i = 0; i < N; i++)
                {
                    float hgt = step * (i + 1);
                    Kit.Box(sg.transform, "Stringer", start + o + d * (tread * (i + 0.5f)) + Vector3.up * (hgt * 0.5f), new Vector3(0.1f, hgt, tread + 0.01f), riserM, false, rot);
                }
                // pegangan tangga
                Vector3 r0 = a + o + Vector3.up * 0.95f, r1 = b + o + Vector3.up * 0.95f;
                Kit.Box(sg.transform, "Handrail", (r0 + r1) * 0.5f, new Vector3(0.08f, 0.08f, (r1 - r0).magnitude), treadM, false, Quaternion.LookRotation(r1 - r0));
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    Vector3 basePt = Vector3.Lerp(a, b, t) + o;
                    Kit.Box(sg.transform, "Baluster", basePt + Vector3.up * 0.47f, new Vector3(0.05f, 0.95f, 0.05f), treadM, false);
                }
                // penghalang samping (tidak terlihat) dari lantai sampai atas pegangan
                GameObject blk = new GameObject("StairSideBlock");
                blk.transform.SetParent(sg.transform, false);
                blk.transform.position = start + o + d * (run * 0.5f) + Vector3.up * ((rise + 1f) * 0.5f);
                blk.transform.rotation = rot;
                BoxCollider bc = blk.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.12f, rise + 1f, run);
            }
            // penghalang ujung atas di lantai bawah (bagian bawah pendaratan)
            GameObject end = new GameObject("StairEndBlock");
            end.transform.SetParent(sg.transform, false);
            end.transform.position = start + d * (run + 0.06f) + Vector3.up * ((rise - 0.15f) * 0.5f);
            end.transform.rotation = rot;
            BoxCollider ec = end.AddComponent<BoxCollider>();
            ec.size = new Vector3(width + 0.3f, rise - 0.15f, 0.12f);
            Kit.Box(sg.transform, "StairEndPanel", end.transform.position, new Vector3(width + 0.3f, rise - 0.15f, 0.1f), riserM, false, rot);

            // pagar pengaman di sekitar lubang lantai atas
            int fu = s.f + 1;
            Vector2Int hole = s.Ramp1;
            for (int k = 0; k < 4; k++)
            {
                if (k == s.dir) continue;                         // sisi pendaratan terbuka
                Vector2Int n = hole + G.Dirs[k];
                if (!L.In(fu, n) || L.Wall(fu, hole, k)) continue;   // sudah ada dinding
                Vector3 mid = G.W(fu, hole) + G.D3(k) * (G.T * 0.5f - 0.08f);
                Vector3 along = new Vector3(G.D3(k).z, 0f, -G.D3(k).x);
                Railing(sg.transform, mid, along, G.T - 0.2f);
            }
        }

        static void Railing(Transform parent, Vector3 mid, Vector3 along, float len)
        {
            Quaternion rot = Quaternion.LookRotation(along);
            Material m = Look.DarkWood;
            Kit.Box(parent, "RailTop", mid + Vector3.up * 1.0f, new Vector3(0.09f, 0.08f, len), m, false, rot);
            Kit.Box(parent, "RailBottom", mid + Vector3.up * 0.08f, new Vector3(0.09f, 0.08f, len), m, false, rot);
            int n = Mathf.RoundToInt(len / 0.3f);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = mid + along * (-len * 0.5f + len * i / n);
                bool newel = i == 0 || i == n;
                Kit.Box(parent, newel ? "Newel" : "Baluster", p + Vector3.up * 0.52f, newel ? new Vector3(0.14f, 1.1f, 0.14f) : new Vector3(0.05f, 0.92f, 0.05f), m, false);
            }
            GameObject blk = new GameObject("RailBlock");
            blk.transform.SetParent(parent, false);
            blk.transform.position = mid + Vector3.up * 0.75f;
            blk.transform.rotation = rot;
            BoxCollider bc = blk.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.12f, 1.5f, len);
        }
    }
}
