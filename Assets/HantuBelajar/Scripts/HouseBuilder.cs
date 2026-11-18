using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Objek yang bisa dipakai pemain dengan tombol E.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt(PlayerController p);
        public abstract void Use(PlayerController p);
    }

    public class HouseInstance : MonoBehaviour
    {
        public HouseLayout L;
        public List<HidingSpot> spots = new List<HidingSpot>();
        public List<RelicPickup> relics = new List<RelicPickup>();
        public ExitDoor exit;
        public List<Light> lamps = new List<Light>();
    }

    /// <summary>Mengubah HouseLayout (data PCG) menjadi GameObject 3D.</summary>
    public static class HouseBuilder
    {
        const float Thick = 0.2f;
        const float DoorW = 1.3f, DoorH = 2.3f;

        public static HouseInstance Build(HouseLayout L)
        {
            GameObject root = new GameObject("House_" + L.p.seed);
            HouseInstance H = root.AddComponent<HouseInstance>();
            H.L = L;
            System.Random rng = new System.Random(L.usedSeed ^ 0x5bd1e995);
            float T = G.T;

            // lantai per ruangan + plafon
            foreach (Room r in L.rooms)
            {
                Vector3 c = new Vector3((r.rect.x + r.rect.width * 0.5f) * T, 0f, (r.rect.y + r.rect.height * 0.5f) * T);
                Vector3 size = new Vector3(r.rect.width * T, 0.1f, r.rect.height * T);
                Box(root.transform, "Floor_" + r.id, c + Vector3.down * 0.05f, size, Look.Floor(r.type, new Vector2(r.rect.width, r.rect.height)));
                Box(root.transform, "Ceil_" + r.id, c + Vector3.up * (G.WallH + 0.05f), size, Look.Mat("ceiling", new Color(0.32f, 0.3f, 0.28f), 0.05f, Look.Noise(new Color(0.8f, 0.78f, 0.74f), 0.3f, "ceilN")), true).layer = 8;
                // lampu ruangan
                Vector3 lp = c + Vector3.up * (G.WallH - 0.45f);
                GameObject lampGo = new GameObject("Lamp_" + r.id);
                lampGo.transform.SetParent(root.transform, false);
                lampGo.transform.position = lp;
                Light li = lampGo.AddComponent<Light>();
                li.type = LightType.Point;
                li.color = new Color(1f, 0.72f, 0.45f);
                li.range = Mathf.Max(r.rect.width, r.rect.height) * T * 0.95f;
                li.intensity = r.lit ? 1.25f : 0.0f;
                li.shadows = LightShadows.None;
                li.enabled = r.lit;
                H.lamps.Add(li);
                GameObject bulb = Box(lampGo.transform, "Bulb", lp, new Vector3(0.22f, 0.3f, 0.22f), r.lit ? Look.Emissive("bulbOn", new Color(1f, 0.75f, 0.4f), 2.5f) : Look.Mat("bulbOff", new Color(0.2f, 0.18f, 0.15f)), true);
                bulb.layer = 8;
                Box(lampGo.transform, "Cord", lp + Vector3.up * 0.3f, new Vector3(0.03f, 0.45f, 0.03f), Look.Mat("iron", new Color(0.08f, 0.08f, 0.08f), 0.3f), true);
                if (r.lit && rng.NextDouble() < 0.35) lampGo.AddComponent<LightFlicker>().bulb = bulb.GetComponent<Renderer>();
            }

            // dinding: sisi timur & utara tiap sel + batas barat/selatan
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    Vector2Int c = new Vector2Int(x, y);
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2Int n = c + G.Dirs[k];
                        bool outer = !L.In(n);
                        if (!outer && (k == 2 || k == 3)) continue;   // diproses dari tetangga
                        if (!outer && L.room[x, y] == L.room[n.x, n.y]) continue;
                        bool exitHere = outer && c == L.exitCell && k == L.exitDir;
                        bool door = !outer && L.IsDoor(c, k);
                        RoomType rt = L.rooms[L.room[x, y]].type;
                        WallSegment(root.transform, c, k, door || exitHere, rt, outer);
                    }
                }

            // pintu keluar
            {
                Vector3 p = G.W(L.exitCell) + G.D3(L.exitDir) * (T * 0.5f);
                GameObject ex = new GameObject("ExitDoor");
                ex.transform.SetParent(root.transform, false);
                ex.transform.position = p;
                ex.transform.rotation = Quaternion.Euler(0f, G.Yaw(L.exitDir), 0f);
                H.exit = ex.AddComponent<ExitDoor>();
                H.exit.Build();
            }

            // relik
            foreach (Placed r in L.relics)
            {
                GameObject g = new GameObject("Relik_" + r.id);
                g.transform.SetParent(root.transform, false);
                g.transform.position = G.W(r.cell);
                RelicPickup rp = g.AddComponent<RelicPickup>();
                rp.index = r.id;
                rp.Build(rng);
                H.relics.Add(rp);
            }

            // baterai
            foreach (Placed b in L.batteries)
            {
                GameObject g = new GameObject("Baterai_" + b.id);
                g.transform.SetParent(root.transform, false);
                g.transform.position = G.W(b.cell) + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f);
                g.AddComponent<BatteryPickup>().Build();
            }

            // tempat sembunyi
            foreach (Placed s in L.spots)
            {
                GameObject g = new GameObject("Spot_" + s.id + "_" + HouseGenerator.SpotName(s.kind));
                g.transform.SetParent(root.transform, false);
                g.transform.position = G.W(s.cell);
                g.transform.rotation = Quaternion.Euler(0f, G.Yaw(s.dir), 0f);
                HidingSpot hs = g.AddComponent<HidingSpot>();
                hs.Init(s, L.rooms[s.room].type, rng);
                H.spots.Add(hs);
            }

            // perabot
            foreach (Placed pr in L.props)
            {
                GameObject g = new GameObject("Prop_" + (PropKind)pr.kind);
                g.transform.SetParent(root.transform, false);
                g.transform.position = G.W(pr.cell);
                g.transform.rotation = Quaternion.Euler(0f, G.Yaw(pr.dir), 0f);
                Props.Build((PropKind)pr.kind, g.transform, rng);
                BoxCollider bc = g.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, 1f, 0f);
                bc.size = new Vector3(T * 0.8f, 2f, T * 0.8f);
            }

            // karpet di ruangan besar (dekor, tidak memblokir)
            foreach (Room r in L.rooms)
            {
                if (r.rect.width < 3 || r.rect.height < 3 || rng.NextDouble() < 0.4) continue;
                Vector3 c = new Vector3((r.rect.x + r.rect.width * 0.5f) * T, 0.012f, (r.rect.y + r.rect.height * 0.5f) * T);
                Color rc = Color.HSVToRGB((float)rng.NextDouble() * 0.1f + 0.95f * (rng.NextDouble() < 0.5 ? 1f : 0.6f), 0.55f, 0.32f);
                Material rm = AssetLibrary.I != null && AssetLibrary.I.rugs != null && AssetLibrary.I.rugs.Length > 0 ? AssetLibrary.I.rugs[rng.Next(AssetLibrary.I.rugs.Length)] : Look.Mat("rug" + r.id, rc, 0.02f, Look.Noise(Color.white, 0.4f, "rugN"));
                GameObject rug = Box(root.transform, "Rug", c, new Vector3(r.rect.width * T * 0.5f, 0.02f, r.rect.height * T * 0.45f), rm, true);
                rug.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return H;
        }

        static void WallSegment(Transform parent, Vector2Int c, int k, bool opening, RoomType rt, bool outer)
        {
            float T = G.T, H = G.WallH;
            Vector3 center = G.W(c) + G.D3(k) * (T * 0.5f);
            Vector3 along = G.D3((k + 1) % 4);
            bool alongX = Mathf.Abs(along.x) > 0.5f;
            Material wm = outer ? Look.Mat("outer", new Color(0.22f, 0.2f, 0.19f), 0.05f, Look.Noise(new Color(0.7f, 0.66f, 0.6f), 0.4f, "outerN")) : Look.Wall(rt);
            if (!opening)
            {
                Box(parent, "Wall", center + Vector3.up * H * 0.5f, alongX ? new Vector3(T + Thick, H, Thick) : new Vector3(Thick, H, T + Thick), wm);
                // list lantai
                Box(parent, "Skirt", center + Vector3.up * 0.08f, alongX ? new Vector3(T + Thick, 0.16f, Thick + 0.04f) : new Vector3(Thick + 0.04f, 0.16f, T + Thick), Look.Mat("skirt", new Color(0.16f, 0.1f, 0.07f), 0.3f), true);
                return;
            }
            float side = (T + Thick - DoorW) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pc = center + along * s * (DoorW * 0.5f + side * 0.5f) + Vector3.up * H * 0.5f;
                Box(parent, "WallSide", pc, alongX ? new Vector3(side, H, Thick) : new Vector3(Thick, H, side), wm);
                Vector3 post = center + along * s * (DoorW * 0.5f) + Vector3.up * DoorH * 0.5f;
                Box(parent, "Frame", post, alongX ? new Vector3(0.12f, DoorH, Thick + 0.08f) : new Vector3(Thick + 0.08f, DoorH, 0.12f), Look.Mat("frame", new Color(0.2f, 0.12f, 0.07f), 0.35f), true);
            }
            Box(parent, "Lintel", center + Vector3.up * (DoorH + (H - DoorH) * 0.5f), alongX ? new Vector3(DoorW, H - DoorH, Thick) : new Vector3(Thick, H - DoorH, DoorW), wm);
            Box(parent, "FrameTop", center + Vector3.up * (DoorH + 0.05f), alongX ? new Vector3(DoorW + 0.2f, 0.12f, Thick + 0.08f) : new Vector3(Thick + 0.08f, 0.12f, DoorW + 0.2f), Look.Mat("frame", new Color(0.2f, 0.12f, 0.07f), 0.35f), true);
        }

        public static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material m, bool noCollider = false)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = worldPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (noCollider) Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        /// <summary>Kubus dalam ruang lokal parent (dipakai perabot prosedural).</summary>
        public static GameObject LBox(Transform parent, Vector3 localPos, Vector3 size, Material m, string name = "Part")
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
        public static GameObject LPrim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 size, Material m, string name = "Part")
        {
            GameObject g = GameObject.CreatePrimitive(t);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
    }

    /// <summary>Perabot: pakai model Asset Store bila ada, kalau tidak model kotak sederhana.</summary>
    public static class Props
    {
        static Material Wood { get { return Look.Mat("wood", Color.white, 0.25f, Look.Planks(new Color(0.36f, 0.22f, 0.13f)), new Vector2(0.5f, 0.5f)); } }
        static Material Dark { get { return Look.Mat("darkwood", new Color(0.4f, 0.32f, 0.27f), 0.3f, Look.Planks(new Color(0.3f, 0.2f, 0.14f))); } }
        static Material Cloth(Color c) { return Look.Mat("cloth" + c, c, 0.05f, Look.Noise(Color.white, 0.25f, "clothN")); }
        static Material Porcelain { get { return Look.Mat("porcelain", new Color(0.78f, 0.78f, 0.74f), 0.7f); } }

        public static void Build(PropKind k, Transform t, System.Random rng)
        {
            float back = -G.T * 0.5f + 0.1f;     // menempel dinding di belakang (lokal -z)
            GameObject model = null;
            switch (k)
            {
                case PropKind.Meja: model = AssetLibrary.Model(l => l.table, rng); break;
                case PropKind.Kursi: model = AssetLibrary.Model(l => l.chair, rng); break;
                case PropKind.Rak: model = AssetLibrary.Model(l => l.shelf, rng); break;
                case PropKind.Peti: model = AssetLibrary.Model(l => l.crate, rng); break;
                case PropKind.Bak: model = AssetLibrary.Model(l => l.bath, rng); break;
                case PropKind.Piano: model = AssetLibrary.Model(l => l.piano, rng); break;
                case PropKind.Lampu: model = AssetLibrary.Model(l => l.lamp, rng) ?? AssetLibrary.Model(l => l.decor, rng); break;
            }
            if (model != null)
            {
                Vector3 size = k == PropKind.Rak ? new Vector3(1.8f, 2.3f, 0.7f) : k == PropKind.Kursi ? new Vector3(0.8f, 1.1f, 0.8f) :
                               k == PropKind.Lampu ? new Vector3(0.8f, 1.8f, 0.8f) : k == PropKind.Piano ? new Vector3(1.8f, 1.4f, 1f) : new Vector3(1.8f, 1.1f, 1.1f);
                GameObject w = AssetLibrary.Fit(model, t, size);
                w.transform.localPosition = new Vector3(0f, 0f, back + size.z * 0.5f);
                return;
            }
            switch (k)
            {
                case PropKind.Meja:
                    HouseBuilder.LBox(t, new Vector3(0, 0.78f, back + 0.6f), new Vector3(1.7f, 0.08f, 1f), Wood);
                    for (int i = 0; i < 4; i++) HouseBuilder.LBox(t, new Vector3((i % 2 == 0 ? -0.75f : 0.75f), 0.39f, back + 0.6f + (i < 2 ? -0.4f : 0.4f)), new Vector3(0.08f, 0.78f, 0.08f), Dark);
                    HouseBuilder.LPrim(PrimitiveType.Cylinder, t, new Vector3(0.4f, 0.9f, back + 0.5f), new Vector3(0.1f, 0.12f, 0.1f), Look.Emissive("candle", new Color(1f, 0.85f, 0.6f), 0.6f));
                    break;
                case PropKind.Kursi:
                    HouseBuilder.LBox(t, new Vector3(0, 0.45f, back + 0.5f), new Vector3(0.6f, 0.08f, 0.6f), Wood);
                    HouseBuilder.LBox(t, new Vector3(0, 0.95f, back + 0.22f), new Vector3(0.6f, 0.9f, 0.07f), Wood);
                    for (int i = 0; i < 4; i++) HouseBuilder.LBox(t, new Vector3((i % 2 == 0 ? -0.26f : 0.26f), 0.22f, back + 0.5f + (i < 2 ? -0.26f : 0.26f)), new Vector3(0.06f, 0.45f, 0.06f), Dark);
                    break;
                case PropKind.Rak:
                    HouseBuilder.LBox(t, new Vector3(0, 1.15f, back + 0.25f), new Vector3(1.8f, 2.3f, 0.45f), Dark);
                    for (int s = 0; s < 4; s++)
                        for (int b = 0; b < 7; b++)
                        {
                            if (rng.NextDouble() < 0.25) continue;
                            Color bc = Color.HSVToRGB((float)rng.NextDouble(), 0.4f, 0.25f + (float)rng.NextDouble() * 0.2f);
                            float hgt = 0.3f + (float)rng.NextDouble() * 0.12f;
                            HouseBuilder.LBox(t, new Vector3(-0.75f + b * 0.24f, 0.25f + s * 0.52f + hgt * 0.5f, back + 0.5f), new Vector3(0.18f, hgt, 0.3f), Look.Mat("book" + (int)(bc.r * 10) + (int)(bc.g * 10), bc, 0.2f));
                        }
                    break;
                case PropKind.Peti:
                    for (int i = 0; i < 2; i++)
                        HouseBuilder.LBox(t, new Vector3(-0.4f + i * 0.85f, 0.4f + (i == 1 && rng.NextDouble() < 0.5 ? 0f : 0f), back + 0.5f), new Vector3(0.8f, 0.8f, 0.8f), Wood).transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 20f, 0);
                    break;
                case PropKind.Bak:
                    HouseBuilder.LBox(t, new Vector3(0, 0.3f, back + 0.5f), new Vector3(1.7f, 0.6f, 0.8f), Porcelain);
                    HouseBuilder.LBox(t, new Vector3(0, 0.58f, back + 0.5f), new Vector3(1.5f, 0.05f, 0.6f), Look.Mat("water", new Color(0.1f, 0.14f, 0.12f), 0.95f));
                    break;
                case PropKind.Piano:
                    HouseBuilder.LBox(t, new Vector3(0, 0.65f, back + 0.4f), new Vector3(1.6f, 1.3f, 0.6f), Look.Mat("piano", new Color(0.04f, 0.03f, 0.03f), 0.75f));
                    HouseBuilder.LBox(t, new Vector3(0, 0.75f, back + 0.78f), new Vector3(1.4f, 0.05f, 0.2f), Porcelain);
                    break;
                default:
                    HouseBuilder.LPrim(PrimitiveType.Cylinder, t, new Vector3(0, 0.8f, back + 0.4f), new Vector3(0.08f, 0.8f, 0.08f), Look.Mat("iron", new Color(0.08f, 0.08f, 0.08f), 0.3f));
                    HouseBuilder.LPrim(PrimitiveType.Cylinder, t, new Vector3(0, 1.7f, back + 0.4f), new Vector3(0.5f, 0.2f, 0.5f), Cloth(new Color(0.5f, 0.35f, 0.25f)));
                    break;
            }
        }

        public static Material ClothMat(Color c) { return Cloth(c); }
        public static Material WoodMat { get { return Wood; } }
        public static Material DarkMat { get { return Dark; } }
    }

    public class LightFlicker : MonoBehaviour
    {
        public Renderer bulb;
        Light l; float baseI, next, seed;
        void Start() { l = GetComponent<Light>(); baseI = l.intensity; seed = Random.value * 100f; }
        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            float k = n < 0.28f ? 0.1f : 1f;
            if (Time.time > next) { next = Time.time + Random.Range(0.05f, 0.3f); }
            l.intensity = baseI * k;
        }
    }
}
