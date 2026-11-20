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
            // dinding di samping/ujung anak tangga (lantai bawah) tertutup tangga -> tanpa jendela, lukisan, lampu dinding
            foreach (Stair s in L.stairs) for (int k = 0; k < 4; k++) covered.Add(FaceKey(L, s.f, s.Ramp1, k));

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
        // Tata letak melintang (jarak dari garis tengah sel; setengah sel 1,80 m), hasil ukur modul dinding paket:
        //   muka datar dinding 1,64 (list atas/cornice 1,52)  |  balok tepi lubang 1,50..1,80 setinggi 2,70..3,00
        //   sisi terbuka : anak tangga s/d 1,44, papan samping 1,44..1,50, pegangan & jeruji di 1,47
        //   sisi dinding : anak tangga s/d 1,62, papan dinding 1,59..1,64
        public const float WellBand = 0.30f;      // kedalaman balok tepi lubang tangga dari garis sel
        const float WellBandBottom = 2.70f;       // tinggi bawah balok tepi, relatif lantai bawah
        const float WallFace = 0.16f;             // muka datar modul dinding dari garis sel (0,03 + 0,108 x 1,2)
        const float OpenEdge = 1.44f, WallEdge = 1.62f;

        static void BuildStairs(HouseLayout L, Transform root, Stair s)
        {
            GameObject sg = new GameObject("Stairs");
            sg.transform.SetParent(root, false);
            Transform st = sg.transform;
            Vector3 d = G.D3(s.dir);
            Vector3 side = new Vector3(d.z, 0f, -d.x);                  // +side = kanan (arah dir+1)
            Vector3 start = s.RampStart;                                // tepi sel bawah/ramp, di lantai bawah
            float run = G.T, rise = G.StoryH, half = G.T * 0.5f;
            const int N = 15;
            float tread = run / N, step = rise / N, pitch = rise / run;
            Quaternion rot = Quaternion.LookRotation(d);
            Material wood = Look.Wood, dark = Look.DarkWood;
            int fu = s.f + 1, left = (s.dir + 3) & 3, right = (s.dir + 1) & 3, back = G.Opp(s.dir);
            bool[] wallF = { L.Wall(s.f, s.Ramp1, left), L.Wall(s.f, s.Ramp1, right) };    // dinding di samping anak tangga (lantai bawah)
            bool[] wallU = { L.Wall(fu, s.Ramp1, left), L.Wall(fu, s.Ramp1, right) };      // dinding di samping lubang (lantai atas)
            bool wallBack = L.Wall(fu, s.Ramp1, back);
            float[] e = { wallF[0] ? WallEdge : OpenEdge, wallF[1] ? WallEdge : OpenEdge };  // jangkauan anak tangga kiri / kanan
            float width = e[0] + e[1];
            Vector3 cs = start + side * ((e[1] - e[0]) * 0.5f);                              // garis tengah anak tangga

            // anak tangga (balok padat dari lantai) + list depan (nosing) yang menonjol di atas tiap tegakan
            for (int i = 0; i < N; i++)
            {
                float hgt = step * (i + 1), a = tread * i;
                Kit.Box(st, "Step", cs + d * (a + tread * 0.5f) + Vector3.up * (hgt * 0.5f), new Vector3(width, hgt, tread + 0.002f), wood, false, rot);
                Kit.Box(st, "Nosing", cs + d * (a + 0.03f) + Vector3.up * (hgt - 0.015f), new Vector3(width, 0.038f, 0.12f), dark, false, rot);
            }

            // papan samping utuh sejajar tangga (satu mesh per sisi -> tanpa celah/garis antar anak tangga)
            float topOff = step + 0.06f;                                // tepi atas papan = garis nosing + 6 cm
            Vector2[] prof = { new Vector2(0f, 0f), new Vector2(0f, topOff), new Vector2((rise - topOff) / pitch, rise), new Vector2(run, rise), new Vector2(run, 0f) };
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f, t = wallF[k] ? 0.05f : 0.06f;
                float lat = wallF[k] ? half - WallFace - t * 0.5f : e[k] + t * 0.5f;
                Kit.Prism(st, "StairString", start + side * (lat * sd), rot, prof, t, dark, 90f);
            }

            // sisi terbuka: tiang awal + pegangan miring + jeruji; pegangan berakhir di bawah balok tepi lubang
            const float railH = 0.92f;
            for (int k = 0; k < 2; k++)
            {
                if (wallF[k]) continue;
                float sd = k == 0 ? -1f : 1f;
                Vector3 o = start + side * ((e[k] + 0.03f) * sd);
                float a0 = 0.07f, a1 = (WellBandBottom - railH - 0.035f) / pitch;
                Kit.Box(st, "Newel", o + d * a0 + Vector3.up * 0.6f, new Vector3(0.12f, 1.2f, 0.12f), dark, false);
                Kit.Box(st, "NewelCap", o + d * a0 + Vector3.up * 1.225f, new Vector3(0.16f, 0.05f, 0.16f), dark, false);
                Vector3 r0 = o + d * a0 + Vector3.up * (pitch * a0 + railH), r1 = o + d * a1 + Vector3.up * (pitch * a1 + railH);
                Kit.Box(st, "Handrail", (r0 + r1) * 0.5f, new Vector3(0.075f, 0.065f, (r1 - r0).magnitude), dark, false, Quaternion.LookRotation(r1 - r0));
                for (int i = 0; i < N * 2; i++)
                {
                    float a = tread * (i + 0.5f) * 0.5f;
                    if (a < a0 + 0.1f) continue;
                    float yb = pitch * a + topOff, yt = Mathf.Min(pitch * a + railH - 0.03f, WellBandBottom);
                    if (yt - yb < 0.05f) break;
                    Kit.Box(st, "Baluster", o + d * a + Vector3.up * ((yb + yt) * 0.5f), new Vector3(0.04f, yt - yb, 0.04f), dark, false);
                }
            }

            // balok tepi lubang lantai atas (belakang + kiri + kanan): menutup tepi pelat & menopang dinding lantai atas
            float bandH = rise - WellBandBottom;
            Vector3 bandBack = start + d * (WellBand * 0.5f) + Vector3.up * (WellBandBottom + bandH * 0.5f);
            Kit.Box(st, "WellTrim", bandBack, new Vector3(G.T, bandH, WellBand), dark, true, rot);
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f;
                Vector3 c = start + side * ((half - WellBand * 0.5f) * sd) + d * ((WellBand + run) * 0.5f) + Vector3.up * (WellBandBottom + bandH * 0.5f);
                Kit.Box(st, "WellTrim", c, new Vector3(WellBand, bandH, run - WellBand), dark, true, rot);
            }

            // pagar pengaman lantai atas di atas balok tepi, hanya di sisi tanpa dinding
            float gl = half - WellBand + 0.08f, gb = WellBand - 0.08f, wallStop = half - WallFace - 0.07f;
            Vector3 o1 = start + Vector3.up * rise;                     // tepi belakang lubang, di lantai atas
            if (!wallBack)
                Railing(st, o1 + d * gb - side * (wallU[0] ? wallStop : gl), o1 + d * gb + side * (wallU[1] ? wallStop : gl), true, true);
            for (int k = 0; k < 2; k++)
            {
                if (wallU[k]) continue;
                float sd = k == 0 ? -1f : 1f;
                Railing(st, o1 + side * (gl * sd) + d * (wallBack ? WallFace + 0.07f : gb), o1 + side * (gl * sd) + d * (run - 0.07f), wallBack, true);
            }

            // collider: permukaan ramp halus untuk CharacterController
            Vector3 ra = cs, rb = cs + d * run + Vector3.up * rise;
            Vector3 dir = (rb - ra).normalized;
            Vector3 up = Vector3.Cross(dir, side).normalized;
            if (up.y < 0) up = -up;
            GameObject ramp = new GameObject("RampCollider");
            ramp.transform.SetParent(st, false);
            ramp.transform.position = (ra + rb) * 0.5f - up * 0.13f;
            ramp.transform.rotation = Quaternion.LookRotation(dir, up);
            BoxCollider rc = ramp.AddComponent<BoxCollider>();
            rc.size = new Vector3(width, 0.3f, (rb - ra).magnitude + 0.2f);
            // penghalang samping (tak terlihat) dari lantai sampai 1 m di atas lantai atas
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? -1f : 1f;
                float lat = wallF[k] ? half - WallFace - 0.03f : e[k] + 0.03f;
                GameObject blk = new GameObject("StairSideBlock");
                blk.transform.SetParent(st, false);
                blk.transform.position = start + side * (lat * sd) + d * (run * 0.5f) + Vector3.up * ((rise + 1f) * 0.5f);
                blk.transform.rotation = rot;
                BoxCollider bc = blk.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.12f, rise + 1f, run);
            }
            // ujung atas di lantai bawah: penghalang + panel penutup (panel hanya bila tidak ada dinding di sana)
            float outL = wallF[0] ? half - WallFace : e[0] + 0.06f, outR = wallF[1] ? half - WallFace : e[1] + 0.06f;
            Vector3 endC = start + side * ((outR - outL) * 0.5f) + d * run;
            GameObject end = new GameObject("StairEndBlock");
            end.transform.SetParent(st, false);
            end.transform.position = endC + d * 0.06f + Vector3.up * ((rise - 0.15f) * 0.5f);
            end.transform.rotation = rot;
            BoxCollider ec = end.AddComponent<BoxCollider>();
            ec.size = new Vector3(outL + outR, rise - 0.15f, 0.12f);
            if (!L.Wall(s.f, s.Ramp1, s.dir))
                Kit.Box(st, "StairEndPanel", endC + d * 0.03f + Vector3.up * ((G.WallH + 0.04f) * 0.5f), new Vector3(outL + outR, G.WallH + 0.04f, 0.06f), dark, false, rot);
        }

        /// <summary>Pagar lurus p0 -> p1 (di ketinggian lantai). newel0/newel1 = buat tiang di ujung (false bila tiang sudut milik pagar lain).</summary>
        static void Railing(Transform parent, Vector3 p0, Vector3 p1, bool newel0, bool newel1)
        {
            Vector3 along = p1 - p0; float len = along.magnitude;
            if (len < 0.2f) return;
            along /= len;
            Quaternion rot = Quaternion.LookRotation(along);
            Vector3 mid = (p0 + p1) * 0.5f;
            Material m = Look.DarkWood;
            Kit.Box(parent, "RailTop", mid + Vector3.up * 1.0f, new Vector3(0.09f, 0.07f, len), m, false, rot);
            Kit.Box(parent, "RailBottom", mid + Vector3.up * 0.07f, new Vector3(0.07f, 0.06f, len), m, false, rot);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.15f));
            for (int i = 0; i <= n; i++)
            {
                bool endPost = i == 0 || i == n;
                if (endPost && !(i == 0 ? newel0 : newel1)) continue;
                Vector3 p = p0 + along * (len * i / n);
                if (endPost)
                {
                    Kit.Box(parent, "Newel", p + Vector3.up * 0.55f, new Vector3(0.13f, 1.1f, 0.13f), m, false);
                    Kit.Box(parent, "NewelCap", p + Vector3.up * 1.125f, new Vector3(0.17f, 0.05f, 0.17f), m, false);
                }
                else Kit.Box(parent, "Baluster", p + Vector3.up * 0.535f, new Vector3(0.04f, 0.87f, 0.04f), m, false);
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
