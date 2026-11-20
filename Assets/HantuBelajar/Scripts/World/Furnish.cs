using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Isi rumah: tempat sembunyi, perabot pemblokir, pickup, dekorasi dinding, lampu, partikel.</summary>
    public static class Furnish
    {
        // putaran model paket agar sisi depannya menghadap +z sel (hasil pengecekan render studio)
        const float YawWardrobe = -90f, YawFridge = -90f, YawArmchair = 90f, YawChair = 90f, YawClock = -90f, YawBathtub = 90f;
        static float BackZ { get { return -G.T * 0.5f + 0.07f; } }

        public static void All(HouseLayout L, HouseInstance H, Transform root, System.Random rng, Dictionary<int, List<WallSlot>> slots)
        {
            foreach (Placed s in L.spots) Spot(L, H, root, s, rng);
            foreach (Placed p in L.props) Prop(L, H, root, p, rng);
            foreach (Placed r in L.relics)
            {
                GameObject g = new GameObject("Relik_" + r.id);
                g.transform.SetParent(root, false);
                g.transform.position = G.W(r.f, r.cell) + Jitter(rng, 0.5f);
                g.transform.rotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);
                RelicPickup rp = g.AddComponent<RelicPickup>();
                rp.Build(rng, r.kind);
                H.relics.Add(rp);
            }
            foreach (Placed b in L.batteries)
            {
                GameObject g = new GameObject("Baterai_" + b.id);
                g.transform.SetParent(root, false);
                g.transform.position = G.W(b.f, b.cell) + Jitter(rng, 0.9f);
                g.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                BatteryPickup bp = g.AddComponent<BatteryPickup>();
                bp.Build();
                H.batteries.Add(bp);
            }
            if (L.key != null)
            {
                GameObject g = new GameObject("Kunci");
                g.transform.SetParent(root, false);
                g.transform.position = G.W(L.key.f, L.key.cell) + Jitter(rng, 0.5f);
                H.key = g.AddComponent<KeyPickup>();
                H.key.Build(rng);
            }
            foreach (Room r in L.rooms) RoomDecor(L, H, root, r, rng, slots[r.id]);
        }

        static Vector3 Jitter(System.Random rng, float a) { return new Vector3(((float)rng.NextDouble() - 0.5f) * a, 0f, ((float)rng.NextDouble() - 0.5f) * a); }

        static Transform Frame(Transform root, string name, int f, Vector2Int cell, int dir)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.position = G.W(f, cell);
            g.transform.rotation = Quaternion.Euler(0f, G.Yaw(dir), 0f);
            return g.transform;
        }

        // ------------------------------------------------------------------ tempat sembunyi
        static void Spot(HouseLayout L, HouseInstance H, Transform root, Placed s, System.Random rng)
        {
            Room room = L.rooms[s.room];
            Transform fr = Frame(root, "Spot_" + s.id + "_" + HouseGenerator.SpotName(s.kind), s.f, s.cell, s.dir);
            HidingSpot hs = fr.gameObject.AddComponent<HidingSpot>();
            hs.Init(s, room.type);
            if ((SpotKind)s.kind == SpotKind.Lemari)
            {
                GameObject pf = AssetLibrary.Model("wardrobe");
                Vector3 inside;
                if (pf != null)
                {
                    GameObject w = Kit.AgainstWall(pf, fr, 0f, BackZ, YawWardrobe, G.S, false, 0f, true, "Wardrobe");
                    Bounds b = Kit.ScaledBounds(pf, G.S, YawWardrobe);
                    inside = new Vector3(0f, 1.5f, BackZ + b.size.z * 0.5f);
                    hs.SetShakeTarget(w.transform.GetChild(0));
                }
                else
                {
                    GameObject body = Kit.Prim(PrimitiveType.Cube, fr, new Vector3(0f, 1.15f, BackZ + 0.4f), new Vector3(1.5f, 2.3f, 0.8f), Look.DarkWood, "Wardrobe");
                    Kit.AddBox(body, new Bounds(Vector3.zero, Vector3.one));
                    inside = new Vector3(0f, 1.5f, BackZ + 0.4f);
                    hs.SetShakeTarget(body.transform);
                }
                hs.insidePos = fr.TransformPoint(inside);
                hs.insideRot = fr.rotation;
            }
            else
            {
                GameObject pf = AssetLibrary.Model("bed");
                float bedLen = 2.4f;
                if (pf != null)
                {
                    GameObject b = Kit.AgainstWall(pf, fr, 0f, BackZ, 0f, G.S, false, 0f, true, "Bed");
                    bedLen = Kit.ScaledBounds(pf, G.S, 0f).size.z;
                    hs.SetShakeTarget(b.transform.GetChild(0));
                    // meja samping ranjang + lilin
                    GameObject side = AssetLibrary.Model("bedside");
                    if (side != null)
                    {
                        float sx = Kit.ScaledBounds(pf, G.S, 0f).size.x * 0.5f + 0.45f;
                        if (sx + 0.4f < G.T * 0.5f)
                        {
                            GameObject bs = Kit.AgainstWall(side, fr, (rng.NextDouble() < 0.5 ? -1f : 1f) * sx, BackZ, 0f, G.S * 0.9f, false, 0f, true, "Bedside");
                            Candle(bs.transform, Kit.ScaledBounds(side, G.S * 0.9f, 0f), rng, room.lit ? 0.5 : 0.95);
                        }
                    }
                }
                else
                {
                    GameObject body = Kit.Prim(PrimitiveType.Cube, fr, new Vector3(0f, 0.55f, BackZ + 1.2f), new Vector3(1.7f, 0.5f, 2.4f), Look.Cloth(new Color(0.4f, 0.4f, 0.45f)), "Bed");
                    Kit.AddBox(body, new Bounds(Vector3.zero, Vector3.one));
                    hs.SetShakeTarget(body.transform);
                }
                hs.insidePos = fr.TransformPoint(new Vector3(0f, 0.22f, BackZ + bedLen * 0.62f));
                hs.insideRot = fr.rotation;
            }
            H.spots.Add(hs);
        }

        // ------------------------------------------------------------------ perabot pemblokir
        static void Prop(HouseLayout L, HouseInstance H, Transform root, Placed p, System.Random rng)
        {
            Room room = L.rooms[p.room];
            PropKind k = (PropKind)p.kind;
            Transform fr = Frame(root, "Prop_" + k, p.f, p.cell, p.dir);
            bool pack = AssetLibrary.HasPack;
            if (!pack) { FallbackProp(fr, k); return; }
            switch (k)
            {
                case PropKind.Dapur: Kitchen(fr, rng, H); break;
                case PropKind.Kulkas:
                    Kit.AgainstWall(AssetLibrary.Model("fridge"), fr, -0.9f, BackZ, YawFridge, G.S, false, 0f, true, "Fridge");
                    PlaceCabinetRun(fr, rng, 0.6f, 1);
                    break;
                case PropKind.MejaMakan: DiningSet(L, fr, p, room, rng, H); break;
                case PropKind.Perapian: Fireplace(fr, rng, H); break;
                case PropKind.Bak:
                    {
                        GameObject tub = AssetLibrary.Model("bathtub");
                        Kit.AgainstWall(tub, fr, 0f, BackZ, YawBathtub, G.S, false, 0f, true, "Bathtub");
                        GameObject steam = AssetLibrary.Model("fx_steam");
                        if (steam != null && rng.NextDouble() < 0.6) Kit.Spawn(steam, fr, fr.TransformPoint(new Vector3(0f, 0.7f, BackZ + 0.6f)), fr.eulerAngles.y, 0.7f, null, false, true, "Steam");
                        break;
                    }
                case PropKind.Wastafel:
                    {
                        GameObject sink = AssetLibrary.Model("sink");
                        Kit.AgainstWall(sink, fr, 0f, BackZ, 0f, G.S, false, 0.72f, true, "Sink");
                        GameObject mirror = AssetLibrary.Model("mirror");
                        if (mirror != null) Kit.AgainstWall(mirror, fr, 0f, BackZ, 0f, G.S * 1.15f, false, 1.25f, false, "Mirror");
                        GameObject pipe = AssetLibrary.Model("pipe");
                        if (pipe != null) Kit.AgainstWall(pipe, fr, 0f, BackZ, 0f, G.S * 0.55f, false, 0f, false, "Pipe");
                        GameObject rad = AssetLibrary.Model("radiator");
                        if (rad != null && rng.NextDouble() < 0.6) Kit.AgainstWall(rad, fr, 1.2f, BackZ, 0f, G.S, false, 0f, true, "Radiator");
                        break;
                    }
                case PropKind.Rak:
                    if (room.type == RoomType.Gudang || room.type == RoomType.Dapur) StorageShelf(fr, rng);
                    else Bookcase(fr, rng, room.type == RoomType.Perpustakaan ? 1.0f : 0.8f);
                    break;
                case PropKind.Peti: Crates(fr, rng); break;
                case PropKind.Kursi:
                    {
                        GameObject chair = AssetLibrary.Model("armchair");
                        Kit.AgainstWall(chair, fr, -0.45f, BackZ + 0.05f, YawArmchair + (float)(rng.NextDouble() * 24 - 12), G.S, false, 0f, true, "ArmChair");
                        GameObject st = AssetLibrary.Model("smalltable", rng);
                        if (st != null)
                        {
                            GameObject t = Kit.AgainstWall(st, fr, 0.65f, BackZ + 0.1f, 0f, G.S, false, 0f, true, "SideTable");
                            Candle(t.transform, Kit.ScaledBounds(st, G.S, 0f), rng, room.lit ? 0.45 : 0.9);
                        }
                        break;
                    }
                default: // Meja
                    if (room.type == RoomType.Perpustakaan || room.type == RoomType.Dapur || room.type == RoomType.RuangMakan) ReadingTable(fr, rng, room);
                    else
                    {
                        GameObject st = AssetLibrary.Model("smalltable", rng);
                        GameObject t = Kit.AgainstWall(st, fr, 0f, BackZ + 0.1f, 0f, G.S * 1.15f, false, 0f, true, "Table");
                        Candle(t.transform, Kit.ScaledBounds(st, G.S * 1.15f, 0f), rng, room.lit ? 0.5 : 0.95);
                        GameObject pic = AssetLibrary.Model("pictures", rng);
                        if (pic != null) Kit.AgainstWall(pic, fr, 0f, BackZ, 0f, G.S, false, 1.35f, false, "Picture");
                        GameObject chair = AssetLibrary.Model("chair", rng);
                        if (chair != null && rng.NextDouble() < 0.7) Kit.Centered(chair, fr, new Vector3(-0.9f, 0f, BackZ + 0.9f), YawChair + 30f, G.S, false, true, true, "Chair");
                    }
                    break;
            }
        }

        static void Kitchen(Transform fr, System.Random rng, HouseInstance H)
        {
            if (rng.NextDouble() < 0.6)
            {
                GameObject stove = AssetLibrary.Model("stove");
                Bounds sb = Kit.ScaledBounds(stove, G.S, 0f);
                float stoveX = -0.6f + sb.size.x * 0.5f + 0.03f;
                Kit.AgainstWall(stove, fr, stoveX, BackZ, 0f, G.S, false, 0f, true, "Stove");
                GameObject hood = AssetLibrary.Model("hood");
                if (hood != null) Kit.AgainstWall(hood, fr, stoveX, BackZ, 0f, G.S * 0.9f, false, 1.75f, false, "Hood");
                PlaceCabinetRun(fr, rng, -1.2f, 1);
                GameObject pot = AssetLibrary.Model(rng.NextDouble() < 0.5 ? "pot" : "pan");
                if (pot != null) Kit.Centered(pot, fr, new Vector3(stoveX - 0.3f, sb.size.y, BackZ + sb.size.z * 0.5f), 0f, G.S, false, false, true, "Pot");
            }
            else
            {
                PlaceCabinetRun(fr, rng, 0f, 3);
                GameObject sink = AssetLibrary.Model("sink_kitchen");
                if (sink != null) Kit.AgainstWall(sink, fr, 0f, BackZ + 0.05f, 0f, G.S, false, 1.0f, false, "KitchenSink");
                GameObject mw = AssetLibrary.Model("microwave");
                if (mw != null) Kit.AgainstWall(mw, fr, 1.2f, BackZ + 0.05f, 0f, G.S, false, 1.17f, false, "Microwave");
                GameObject top = AssetLibrary.Model("cabinet_top");
                if (top != null) { Kit.AgainstWall(top, fr, -1.2f, BackZ, 0f, G.S, false, 1.75f, false, "CabTop"); Kit.AgainstWall(top, fr, 0f, BackZ, 0f, G.S, false, 1.75f, false, "CabTop"); }
            }
        }

        /// <summary>Deretan lemari dapur (count buah, tiap 1,2 m) berpusat di x.</summary>
        static void PlaceCabinetRun(Transform fr, System.Random rng, float centerX, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject cab = AssetLibrary.Model("cabinet", rng);
                if (cab == null) return;
                float x = centerX + (i - (count - 1) * 0.5f) * G.M;
                GameObject c = Kit.AgainstWall(cab, fr, x, BackZ, 0f, G.S, false, 0f, true, "Cabinet");
                if (rng.NextDouble() < 0.5)
                {
                    GameObject it = AssetLibrary.Model(rng.NextDouble() < 0.5 ? "jar" : "bottle");
                    if (it != null) Kit.Centered(it, fr, new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.6f, Kit.ScaledBounds(cab, G.S, 0f).size.y, BackZ + 0.35f), (float)rng.NextDouble() * 360f, G.S, false, false, true, "Clutter");
                }
                _ = c;
            }
        }

        static void DiningSet(HouseLayout L, Transform fr, Placed p, Room room, System.Random rng, HouseInstance H)
        {
            GameObject table = AssetLibrary.Model("table_long");
            bool center = true;
            for (int k = 0; k < 4; k++) if (L.Wall(p.f, p.cell, k)) center = false;
            Bounds raw = Kit.RawBounds(table);
            float len = center ? 3.0f : 2.6f;
            float sx = len / (raw.size.x * G.S);
            float zc = center ? 0f : BackZ + raw.size.z * G.S * 0.5f + 0.15f;
            Vector3 pos = fr.TransformPoint(new Vector3(-raw.center.x * G.S * sx, -raw.min.y * G.S, zc - raw.center.z * G.S));
            GameObject t = Kit.Spawn(table, fr, pos, fr.eulerAngles.y, G.S, new Vector3(sx, 1f, 1f), false, true, "DiningTable");
            BoxCollider bc = t.AddComponent<BoxCollider>();
            bc.center = new Vector3(raw.center.x * G.S * sx, raw.center.y * G.S, raw.center.z * G.S);
            bc.size = new Vector3(raw.size.x * G.S * sx, raw.size.y * G.S, raw.size.z * G.S);
            float topY = raw.max.y * G.S, halfW = raw.size.z * G.S * 0.5f;
            GameObject chair = AssetLibrary.Model("chair", rng);
            if (chair != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    Kit.Centered(AssetLibrary.Model("chair", rng), fr, new Vector3(i * 0.95f, 0f, zc + halfW + 0.35f), YawChair + 180f, G.S, false, true, true, "Chair");
                    if (center) Kit.Centered(AssetLibrary.Model("chair", rng), fr, new Vector3(i * 0.95f, 0f, zc - halfW - 0.35f), YawChair, G.S, false, true, true, "Chair");
                }
            }
            GameObject cs = AssetLibrary.Model("candlestick");
            if (cs != null)
                for (int i = -1; i <= 1; i += 2)
                {
                    GameObject c = Kit.Centered(cs, fr, new Vector3(i * 0.8f, topY, zc), 0f, G.S, true, false, true, "Candlestick");
                    TuneCandle(c, H, room.lit);
                }
        }

        static void Fireplace(Transform fr, System.Random rng, HouseInstance H)
        {
            GameObject fp = AssetLibrary.Model("fireplace");
            Kit.AgainstWall(fp, fr, 0f, BackZ, 0f, G.S, false, 0f, true, "Fireplace");
            Bounds b = Kit.ScaledBounds(fp, G.S, 0f);
            // api: partikel lilin diperbesar + cahaya oranye berkedip + suara
            Vector3 firePos = new Vector3(0f, 0.22f, BackZ + 0.22f);
            GameObject flame = AssetLibrary.Model("fx_candle");
            for (int i = 0; i < 5 && flame != null; i++)
                Kit.Spawn(flame, fr, fr.TransformPoint(firePos + new Vector3((i - 2) * 0.14f, 0f, 0f)), fr.eulerAngles.y, 2.6f + (float)rng.NextDouble(), null, false, true, "Fire");
            GameObject lg = new GameObject("FireLight"); lg.transform.SetParent(fr, false); lg.transform.localPosition = firePos + new Vector3(0f, 0.35f, 0.45f);
            Light l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.18f); l.range = 8f; l.intensity = 6f; l.shadows = LightShadows.None;
            Lamp lamp = lg.AddComponent<Lamp>(); lamp.Init(l, Lamp.Kind.Fire, null);
            H.lamps.Add(lamp);
            AudioSource a = lg.AddComponent<AudioSource>();
            a.clip = AssetLibrary.Clip("candle"); a.loop = true; a.spatialBlend = 1f; a.minDistance = 1f; a.maxDistance = 9f; a.rolloffMode = AudioRolloffMode.Linear; a.volume = 0.6f;
            if (a.clip != null) { a.Play(); AudioDirector.Register(a, 0.6f, false); }
            // lilin di atas perapian
            GameObject cs = AssetLibrary.Model("candlestick");
            if (cs != null)
                for (int i = -1; i <= 1; i += 2)
                {
                    GameObject c = Kit.Centered(cs, fr, new Vector3(i * 0.75f, b.size.y, BackZ + 0.2f), 0f, G.S * 0.9f, true, false, true, "Candle");
                    TuneCandle(c, H, false);
                }
            GameObject pic = AssetLibrary.Model("pictures", rng);
            if (pic != null) Kit.AgainstWall(pic, fr, 0f, BackZ, 0f, G.S, false, b.size.y + 0.25f, false, "Portrait");
        }

        static void ReadingTable(Transform fr, System.Random rng, Room room)
        {
            GameObject table = AssetLibrary.Model("table_mid");
            if (table == null) return;
            Bounds raw = Kit.RawBounds(table);
            float sx = 2.2f / (raw.size.x * G.S);
            float zc = BackZ + raw.size.z * G.S * 0.5f + 0.1f;
            Vector3 pos = fr.TransformPoint(new Vector3(-raw.center.x * G.S * sx, -raw.min.y * G.S, zc - raw.center.z * G.S));
            GameObject t = Kit.Spawn(table, fr, pos, fr.eulerAngles.y, G.S, new Vector3(sx, 1f, 1f), false, true, "Table");
            BoxCollider bc = t.AddComponent<BoxCollider>();
            bc.center = new Vector3(raw.center.x * G.S * sx, raw.center.y * G.S, raw.center.z * G.S);
            bc.size = new Vector3(raw.size.x * G.S * sx, raw.size.y * G.S, raw.size.z * G.S);
            float topY = raw.max.y * G.S;
            for (int i = -1; i <= 1; i += 2)
                if (rng.NextDouble() < 0.8) Kit.Centered(AssetLibrary.Model("chair", rng), fr, new Vector3(i * 0.6f, 0f, zc + raw.size.z * G.S * 0.5f + 0.35f), YawChair + 180f + (float)(rng.NextDouble() * 30 - 15), G.S, false, true, true, "Chair");
            GameObject cs = AssetLibrary.Model("candlestick");
            if (cs != null && rng.NextDouble() < (room.lit ? 0.5 : 0.95))
            {
                GameObject c = Kit.Centered(cs, fr, new Vector3(0.5f, topY, zc), 0f, G.S, true, false, true, "Candle");
                TuneCandle(c, null, room.lit);
            }
            // buku di meja
            Material[] bm = { Look.Cloth(new Color(0.35f, 0.08f, 0.06f)), Look.Cloth(new Color(0.12f, 0.2f, 0.1f)), Look.Cloth(new Color(0.15f, 0.12f, 0.08f)) };
            for (int i = 0; i < 3; i++)
                Kit.Prim(PrimitiveType.Cube, fr, new Vector3(-0.5f + i * 0.05f, topY + 0.03f + i * 0.05f, zc + 0.05f), new Vector3(0.32f, 0.05f, 0.24f), bm[i], "Book", Quaternion.Euler(0f, rng.Next(-20, 20), 0f));
        }

        static void Bookcase(Transform fr, System.Random rng, float density)
        {
            GameObject g = new GameObject("Bookcase"); g.transform.SetParent(fr, false);
            g.transform.localPosition = new Vector3(0f, 0f, BackZ + 0.22f);
            Transform t = g.transform;
            Material wood = Look.DarkWood;
            float w = 2.2f, h = 2.3f, d = 0.42f;
            Kit.Prim(PrimitiveType.Cube, t, new Vector3(0f, h * 0.5f, -d * 0.5f + 0.02f), new Vector3(w, h, 0.04f), wood, "Back");
            for (int s = -1; s <= 1; s += 2) Kit.Prim(PrimitiveType.Cube, t, new Vector3(s * (w * 0.5f - 0.03f), h * 0.5f, 0f), new Vector3(0.06f, h, d), wood, "Side");
            Kit.Prim(PrimitiveType.Cube, t, new Vector3(0f, h - 0.04f, 0f), new Vector3(w + 0.1f, 0.08f, d + 0.06f), wood, "Top");
            for (int sh = 0; sh < 5; sh++)
            {
                float y = 0.08f + sh * 0.47f;
                Kit.Prim(PrimitiveType.Cube, t, new Vector3(0f, y, 0f), new Vector3(w - 0.06f, 0.035f, d), wood, "Shelf");
                if (sh == 4) break;
                float x = -w * 0.5f + 0.1f;
                while (x < w * 0.5f - 0.15f)
                {
                    float bw = 0.04f + (float)rng.NextDouble() * 0.05f, bh = 0.26f + (float)rng.NextDouble() * 0.14f;
                    if (rng.NextDouble() < density)
                    {
                        Color bc = Color.HSVToRGB((float)rng.NextDouble(), 0.35f + (float)rng.NextDouble() * 0.3f, 0.12f + (float)rng.NextDouble() * 0.22f);
                        float lean = rng.NextDouble() < 0.08 ? 12f : 0f;
                        Kit.Prim(PrimitiveType.Cube, t, new Vector3(x + bw * 0.5f, y + 0.02f + bh * 0.5f, 0.02f), new Vector3(bw, bh, d * 0.8f), Look.Cloth(bc), "Book", Quaternion.Euler(0f, 0f, lean));
                    }
                    x += bw + 0.005f;
                }
            }
            BoxCollider c = g.AddComponent<BoxCollider>(); c.center = new Vector3(0f, h * 0.5f, 0f); c.size = new Vector3(w, h, d);
        }

        static void StorageShelf(Transform fr, System.Random rng)
        {
            GameObject g = new GameObject("StorageShelf"); g.transform.SetParent(fr, false);
            g.transform.localPosition = new Vector3(0f, 0f, BackZ + 0.25f);
            Transform t = g.transform;
            Material wood = Look.Wood;
            float w = 2.4f, h = 2.0f, d = 0.5f;
            for (int s = -1; s <= 1; s += 2) for (int z = -1; z <= 1; z += 2)
                    Kit.Prim(PrimitiveType.Cube, t, new Vector3(s * (w * 0.5f - 0.03f), h * 0.5f, z * (d * 0.5f - 0.03f)), new Vector3(0.05f, h, 0.05f), wood, "Leg");
            string[] items = { "jar", "bottle", "pot", "plate", "jar", "bottle" };
            for (int sh = 0; sh < 4; sh++)
            {
                float y = 0.15f + sh * 0.6f;
                Kit.Prim(PrimitiveType.Cube, t, new Vector3(0f, y, 0f), new Vector3(w, 0.04f, d), wood, "Board");
                for (int i = 0; i < 4; i++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    GameObject it = AssetLibrary.Model(items[rng.Next(items.Length)]);
                    if (it == null) continue;
                    Kit.Centered(it, t, new Vector3(-w * 0.5f + 0.3f + i * 0.6f, y + 0.02f, 0f), (float)rng.NextDouble() * 360f, G.S, false, false, true, "Item");
                }
            }
            BoxCollider c = g.AddComponent<BoxCollider>(); c.center = new Vector3(0f, h * 0.5f, 0f); c.size = new Vector3(w, h, d);
        }

        static void Crates(Transform fr, System.Random rng)
        {
            Material wood = Look.Mat("crate", new Color(0.55f, 0.45f, 0.35f), 0.15f, Look.Planks(new Color(0.5f, 0.38f, 0.26f)), new Vector2(0.5f, 0.5f));
            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                float s = 0.7f + (float)rng.NextDouble() * 0.25f;
                Vector3 p = new Vector3(-0.9f + i * 0.85f, s * 0.5f, BackZ + 0.5f + (float)rng.NextDouble() * 0.2f);
                GameObject c = Kit.Prim(PrimitiveType.Cube, fr, p, Vector3.one * s, wood, "Crate", Quaternion.Euler(0f, rng.Next(-15, 15), 0f));
                c.AddComponent<BoxCollider>();
                if (rng.NextDouble() < 0.4)
                {
                    float s2 = s * 0.7f;
                    Kit.Prim(PrimitiveType.Cube, fr, p + Vector3.up * (s * 0.5f + s2 * 0.5f), Vector3.one * s2, wood, "Crate", Quaternion.Euler(0f, rng.Next(-30, 30), 0f));
                }
            }
            GameObject barrel = Kit.Prim(PrimitiveType.Cylinder, fr, new Vector3(1.2f, 0.5f, BackZ + 0.45f), new Vector3(0.75f, 0.5f, 0.75f), Look.Mat("barrel", Color.white, 0.2f, Look.Planks(new Color(0.42f, 0.28f, 0.16f)), new Vector2(2f, 1f)), "Barrel");
            barrel.AddComponent<CapsuleCollider>();
        }

        static void FallbackProp(Transform fr, PropKind k)
        {
            Vector3 size = k == PropKind.Rak ? new Vector3(2f, 2.2f, 0.45f) : k == PropKind.MejaMakan ? new Vector3(2.6f, 0.78f, 1.1f) : new Vector3(1.4f, 0.9f, 0.8f);
            GameObject g = Kit.Prim(PrimitiveType.Cube, fr, new Vector3(0f, size.y * 0.5f, BackZ + size.z * 0.5f), size, Look.Wood, "Prop_" + k);
            g.AddComponent<BoxCollider>();
        }

        // ------------------------------------------------------------------ lilin
        static void Candle(Transform surfaceWrap, Bounds surfaceLocal, System.Random rng, double chance)
        {
            if (rng.NextDouble() > chance) return;
            GameObject cs = AssetLibrary.Model("candlestick");
            if (cs == null) return;
            GameObject c = Kit.Spawn(cs, surfaceWrap, surfaceWrap.TransformPoint(new Vector3(surfaceLocal.center.x + 0.08f, surfaceLocal.max.y, surfaceLocal.center.z)), surfaceWrap.eulerAngles.y + rng.Next(360), G.S * 0.85f, null, true, true, "Candle");
            TuneCandle(c, null, false);
        }

        static void TuneCandle(GameObject c, HouseInstance H, bool roomLit)
        {
            foreach (Light l in c.GetComponentsInChildren<Light>(true))
            {
                l.enabled = true;
                l.range = 3.8f; l.intensity = roomLit ? 1.6f : 2.4f; l.shadows = LightShadows.None;
                Lamp lamp = l.gameObject.AddComponent<Lamp>(); lamp.Init(l, Lamp.Kind.Candle, null);
                if (H != null) H.lamps.Add(lamp);
            }
        }

        // ------------------------------------------------------------------ dekorasi & lampu ruangan
        static void RoomDecor(HouseLayout L, HouseInstance H, Transform root, Room r, System.Random rng, List<WallSlot> slots)
        {
            float baseY = r.floor * G.StoryH;
            Vector3 center = new Vector3((r.rect.x + r.rect.width * 0.5f) * G.T, baseY, (r.rect.y + r.rect.height * 0.5f) * G.T);
            // karpet
            string rugKey = r.type == RoomType.KamarTidur || r.type == RoomType.KamarAnak ? "rug_bed" : r.type == RoomType.RuangMakan ? "rug_dining" :
                            r.type == RoomType.Dapur ? "rug_kitchen" : r.type == RoomType.KamarMandi ? "rug_bath" : "rug_corr";
            GameObject rug = AssetLibrary.Model(rugKey);
            RectInt free0 = FreeRect(L, r);       // area ruangan tanpa sel tangga / lubang tangga
            if (rug != null && r.Area >= 4 && free0.width * free0.height >= 2)
            {
                Vector3 rugC = new Vector3((free0.x + free0.width * 0.5f) * G.T, baseY, (free0.y + free0.height * 0.5f) * G.T);
                float maxX = free0.width * G.T - 1.6f, maxZ = free0.height * G.T - 1.6f;
                Bounds rb = Kit.ScaledBounds(rug, 1f, 0f);
                bool rot = (rb.size.x > rb.size.z) != (maxX > maxZ);
                float yawR = rot ? 90f : 0f;
                Bounds rr = Kit.ScaledBounds(rug, 1f, yawR);
                float s = Mathf.Min(maxX / rr.size.x, maxZ / rr.size.z, G.S * 1.1f);
                if (s > 0.3f)
                {
                    Bounds sb = Kit.ScaledBounds(rug, s, yawR);
                    Kit.Spawn(rug, root, rugC + new Vector3(-sb.center.x, 0.005f - sb.min.y, -sb.center.z), yawR, s, null, false, true, "Rug");
                }
            }

            // balok plafon (ruangan besar) + sarang laba-laba di sudut atas
            if (r.Area >= 6 && rng.NextDouble() < 0.7)
            {
                bool alongX = r.rect.width < r.rect.height;     // balok melintang sisi pendek
                int count = Mathf.Max(r.rect.width, r.rect.height);
                for (int i = 0; i < count; i++)
                {
                    // balok yang akan melintasi lubang tangga (tanpa plafon) tidak dipasang
                    bool overWell = false;
                    int span = alongX ? r.rect.width : r.rect.height;
                    for (int j = 0; j < span; j++)
                    {
                        Vector2Int c = alongX ? new Vector2Int(r.rect.x + j, r.rect.y + i) : new Vector2Int(r.rect.x + i, r.rect.y + j);
                        if (r.floor + 1 < L.floors && L.hole[r.floor + 1, c.x, c.y]) overWell = true;
                    }
                    if (overWell) continue;
                    float off = (i + 0.5f - count * 0.5f) * G.T;
                    Vector3 bp = center + Vector3.up * (G.WallH - 0.13f) + (alongX ? new Vector3(0f, 0f, off) : new Vector3(off, 0f, 0f));
                    Vector3 size = alongX ? new Vector3(r.rect.width * G.T, 0.26f, 0.22f) : new Vector3(0.22f, 0.26f, r.rect.height * G.T);
                    Kit.Box(root, "CeilingBeam", bp, size, Look.DarkWood, false);
                }
            }
            int webs = rng.Next(0, 3);
            for (int i = 0; i < webs; i++)
            {
                float sx = rng.NextDouble() < 0.5 ? -1f : 1f, sz = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 corner = center + new Vector3(sx * (r.rect.width * G.T * 0.5f - 0.32f), G.WallH - 0.32f, sz * (r.rect.height * G.T * 0.5f - 0.32f));
                int spin = rng.Next(360); float webScale = 0.7f + (float)rng.NextDouble() * 0.5f;
                // sarang (lebar s/d 1,2 m) yang menjangkau sel anak tangga akan menembus tangga / melayang di lubang tangga
                bool nearStair = false;
                for (int q = 0; q < 4; q++)
                {
                    Stair ws = L.StairCell(r.floor, G.C(corner + new Vector3((q & 1) == 0 ? -0.65f : 0.65f, 0f, (q & 2) == 0 ? -0.65f : 0.65f)));
                    if (ws != null && ws.f == r.floor) nearStair = true;
                }
                if (nearStair) continue;
                GameObject web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(web.GetComponent<Collider>());
                web.name = "Cobweb";
                web.transform.SetParent(root, false);
                web.transform.position = corner;
                web.transform.rotation = Quaternion.LookRotation(new Vector3(-sx, 0.9f, -sz).normalized) * Quaternion.Euler(0f, 0f, spin);
                web.transform.localScale = Vector3.one * webScale;
                Renderer wr = web.GetComponent<Renderer>();
                wr.sharedMaterial = Look.Cobweb;
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // slot dinding polos milik ruangan, diacak
            List<WallSlot> free = slots.FindAll(s => !s.used);
            Shuffle(free, rng);

            // lampu dinding (lampu paket) di ruangan yang menyala
            if (r.lit)
            {
                int want = r.Area >= 6 ? 3 : 2;
                int shadowed = rng.NextDouble() < 0.5 ? 1 : 0;
                HashSet<int> usedWalls = new HashSet<int>();
                for (int i = 0; i < free.Count && want > 0; i++)
                {
                    WallSlot s = free[i];
                    int wallId = s.k * 1000 + (s.k % 2 == 0 ? s.cell.y : s.cell.x);
                    if (usedWalls.Contains(wallId) && free.Count > want * 2) continue;
                    usedWalls.Add(wallId);
                    s.used = true; want--;
                    Lamp lamp = Sconce(root, s, shadowed-- > 0);
                    if (lamp != null) H.lamps.Add(lamp);
                }
                // cahaya pantul lembut di tengah ruangan
                GameObject fg = new GameObject("RoomFill"); fg.transform.SetParent(root, false); fg.transform.position = center + Vector3.up * 2.3f;
                Light fl = fg.AddComponent<Light>(); fl.type = LightType.Point; fl.color = new Color(1f, 0.78f, 0.55f);
                fl.range = Mathf.Max(r.rect.width, r.rect.height) * G.T * 0.9f; fl.intensity = 3.2f; fl.shadows = LightShadows.None;
                Lamp fillLamp = fg.AddComponent<Lamp>(); fillLamp.Init(fl, Lamp.Kind.Fill, null); H.lamps.Add(fillLamp);
                // debu melayang
                GameObject dust = AssetLibrary.Model("fx_dust");
                if (dust != null && rng.NextDouble() < 0.7) Kit.Spawn(dust, root, center + Vector3.up * 1.4f, 0f, 1f, null, false, true, "Dust");
            }

            // lukisan
            int pics = r.type == RoomType.KamarMandi || r.type == RoomType.Dapur ? 0 : 1 + rng.Next(2);
            foreach (WallSlot s in free)
            {
                if (pics <= 0) break;
                if (s.used) continue;
                s.used = true; pics--;
                GameObject pic = AssetLibrary.Model("pictures", rng);
                if (pic == null) break;
                Bounds pb = Kit.ScaledBounds(pic, G.S, 0f);
                float tilt = rng.NextDouble() < 0.25 ? (float)(rng.NextDouble() * 10 - 5) : 0f;
                GameObject pg = Kit.Spawn(pic, root, s.pos + s.normal * (-pb.min.z + 0.02f) + Vector3.up * (1.65f - pb.center.y), Mathf.Atan2(s.normal.x, s.normal.z) * Mathf.Rad2Deg, G.S, null, false, true, "Picture");
                pg.transform.Rotate(0f, 0f, tilt, Space.Self);
            }

            // jam berdiri di pojok
            if ((r.type == RoomType.RuangTamu || r.type == RoomType.RuangKeluarga || r.type == RoomType.Perpustakaan) && rng.NextDouble() < 0.75)
            {
                GameObject clock = AssetLibrary.Model("clock");
                foreach (WallSlot s in free)
                {
                    if (s.used || clock == null) continue;
                    if (s.index != 0 && s.index != 2) continue;
                    Vector3Int node = G.N(s.f, s.cell);
                    if (L.Blocked(node)) continue;
                    s.used = true;
                    float yaw = Mathf.Atan2(s.normal.x, s.normal.z) * Mathf.Rad2Deg;
                    GameObject tmp = new GameObject("ClockFrame"); tmp.transform.SetParent(root, false);
                    tmp.transform.position = s.pos; tmp.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    Kit.AgainstWall(clock, tmp.transform, 0f, 0.02f, YawClock, G.S, false, 0f, true, "GrandfatherClock");
                    break;
                }
            }
        }

        /// <summary>Persegi panjang terbesar di dalam ruangan yang tidak memuat sel tangga / lubang tangga (dipakai untuk karpet).</summary>
        static RectInt FreeRect(HouseLayout L, Room r)
        {
            RectInt best = new RectInt(r.rect.x, r.rect.y, 0, 0);
            int bestA = 0, bestMin = 0;
            for (int x0 = r.rect.x; x0 < r.rect.xMax; x0++)
                for (int y0 = r.rect.y; y0 < r.rect.yMax; y0++)
                    for (int x1 = x0 + 1; x1 <= r.rect.xMax; x1++)
                        for (int y1 = y0 + 1; y1 <= r.rect.yMax; y1++)
                        {
                            bool ok = true;
                            for (int x = x0; x < x1 && ok; x++)
                                for (int y = y0; y < y1 && ok; y++)
                                    if (L.StairCell(r.floor, new Vector2Int(x, y)) != null) ok = false;
                            if (!ok) continue;
                            int a = (x1 - x0) * (y1 - y0), mn = Mathf.Min(x1 - x0, y1 - y0);
                            if (a > bestA || (a == bestA && mn > bestMin)) { bestA = a; bestMin = mn; best = new RectInt(x0, y0, x1 - x0, y1 - y0); }
                        }
            return best;
        }

        static Lamp Sconce(Transform root, WallSlot s, bool shadows)
        {
            GameObject sc = AssetLibrary.Model("sconce");
            float yaw = Mathf.Atan2(s.normal.x, s.normal.z) * Mathf.Rad2Deg;
            Light l;
            if (sc != null)
            {
                Bounds b = Kit.ScaledBounds(sc, G.S, 0f);
                GameObject g = Kit.Spawn(sc, root, s.pos + s.normal * (-b.min.z) + Vector3.up * (1.95f - b.min.y), yaw, G.S, null, true, true, "Sconce");
                l = g.GetComponentInChildren<Light>(true);
                if (l == null) return null;
            }
            else
            {
                GameObject g = new GameObject("Sconce"); g.transform.SetParent(root, false); g.transform.position = s.pos + s.normal * 0.2f + Vector3.up * 2.0f;
                Kit.Prim(PrimitiveType.Sphere, g.transform, Vector3.zero, Vector3.one * 0.18f, Look.Emissive("bulb", new Color(1f, 0.75f, 0.45f), 2f), "Bulb");
                l = g.AddComponent<Light>();
            }
            l.enabled = true;
            l.type = LightType.Spot;
            l.transform.rotation = Quaternion.LookRotation((s.normal * 0.55f + Vector3.down).normalized);
            l.spotAngle = 120f; l.innerSpotAngle = 70f;
            l.range = 9.5f; l.intensity = 6.5f;
            l.color = new Color(1f, 0.8f, 0.58f);
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            Lamp lamp = l.gameObject.AddComponent<Lamp>();
            lamp.Init(l, Lamp.Kind.Sconce, null);
            return lamp;
        }

        static void Shuffle<T>(List<T> l, System.Random rng)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; }
        }
    }
}
