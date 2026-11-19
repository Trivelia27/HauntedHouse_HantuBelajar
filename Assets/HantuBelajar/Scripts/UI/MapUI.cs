using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HantuBelajar
{
    /// <summary>Tujuan aktif pemain + jalur A* (lewat pintu & tangga) untuk kompas, peta, dan teks tujuan.</summary>
    public class Guidance
    {
        public enum Kind { None, Relic, Key, Lock, Exit }
        public Kind kind = Kind.None;
        public Interactable target;
        public Vector3 targetPos, nextPoint;
        public int targetFloor, floorDelta;
        public float distance;
        public readonly List<Vector3> path = new List<Vector3>();
        public BatteryPickup battery;
        public string text = "";

        public string IconName
        {
            get
            {
                switch (kind)
                {
                    case Kind.Relic: return "relic";
                    case Kind.Key: return "key";
                    case Kind.Lock: return "lock";
                    case Kind.Exit: return "exit";
                    default: return "disc";
                }
            }
        }
        public Color IconColor
        {
            get
            {
                switch (kind)
                {
                    case Kind.Key: return new Color(1f, 0.42f, 0.36f);
                    case Kind.Lock: return UIKit.Danger;
                    case Kind.Exit: return UIKit.Good;
                    default: return UIKit.Gold;
                }
            }
        }

        public void Compute(GameManager gm)
        {
            HouseLayout L = gm.layout; HouseInstance H = gm.house; PlayerController p = gm.player;
            kind = Kind.None; target = null; path.Clear(); distance = 0f; floorDelta = 0; battery = null; text = "";
            if (L == null || H == null || p == null) return;
            Vector3Int from = p.node;
            int[] dist = L.Distances(from, false, false);
            if (gm.relicsFound >= gm.RelicsNeeded && H.exit != null) { kind = Kind.Exit; target = H.exit; }
            else
            {
                RelicPickup best = null; int bd = int.MaxValue;
                foreach (RelicPickup r in H.relics)
                {
                    if (r == null || r.taken) continue;
                    int d = L.Dist(dist, L.NodeAt(r.transform.position));
                    if (d >= 0 && d < bd) { bd = d; best = r; }
                }
                if (best != null) { kind = Kind.Relic; target = best; }
                else if (H.lockedDoor != null && H.lockedDoor.locked)
                {
                    // relik tersisa ada di balik gembok
                    if (p.hasKey) { kind = Kind.Lock; target = H.lockedDoor; }
                    else if (H.key != null && !H.key.taken) { kind = Kind.Key; target = H.key; }
                }
            }
            if (target == null) return;
            Vector3Int tn;
            if (target is LockedDoor)
            {
                Vector3 c = target.transform.position, f = target.transform.forward;
                Vector3Int a = L.NodeAt(c + f * 1.5f), b = L.NodeAt(c - f * 1.5f);
                tn = L.Dist(dist, a) >= 0 ? a : b;
                targetPos = c;
            }
            else
            {
                tn = L.NodeAt(target.UsePoint);
                targetPos = target.transform.position;
            }
            targetFloor = tn.z;
            Vector3 me = p.transform.position;
            path.Add(me);
            List<Vector3Int> nodes = L.Path(from, tn, false, false);
            if (nodes.Count > 0)
            {
                List<Vector3> w = L.Waypoints(nodes);
                for (int i = 1; i < w.Count; i++) path.Add(w[i]);   // titik pertama = tengah sel pemain (dilewati)
            }
            path.Add(new Vector3(targetPos.x, G.W(tn).y, targetPos.z));
            for (int i = 1; i < path.Count; i++) distance += Vector3.Distance(path[i - 1], path[i]);
            nextPoint = path[path.Count - 1];
            for (int i = 1; i < path.Count; i++) if (G.Flat(path[i] - me).magnitude > 1.4f) { nextPoint = path[i]; break; }
            floorDelta = Mathf.Clamp(targetFloor - p.floor, -1, 1);
            if (p.battery < 30f)
            {
                int bb = 13;
                foreach (BatteryPickup b in H.batteries)
                {
                    if (b == null || b.taken) continue;
                    int d = L.Dist(dist, L.NodeAt(b.transform.position));
                    if (d >= 0 && d < bb) { bb = d; battery = b; }
                }
            }
            string fl = floorDelta > 0 ? "  (naik tangga)" : floorDelta < 0 ? "  (turun tangga)" : "";
            switch (kind)
            {
                case Kind.Relic: text = "Ambil relik: " + RelicPickup.Names[((RelicPickup)target).index % RelicPickup.Names.Length] + fl; break;
                case Kind.Key: text = "Relik terakhir di ruang tergembok - cari KUNCI TUA" + fl; break;
                case Kind.Lock: text = "Buka pintu yang digembok dengan kunci" + fl; break;
                case Kind.Exit: text = "Kabur lewat PINTU KELUAR" + fl; break;
            }
        }
    }

    /// <summary>Tekstur denah satu lantai (lantai ruangan, dinding, pintu, tangga, gembok, pintu keluar).</summary>
    public static class MapTex
    {
        public const int CS = 36, M = 8;   // piksel per sel, margin
        public static Vector2 ToPx(Vector3 w) { return new Vector2(M + w.x / G.T * CS, M + w.z / G.T * CS); }

        static Color RoomTint(RoomType t)
        {
            switch (t)
            {
                case RoomType.KamarMandi: return new Color(0.3f, 0.42f, 0.55f);
                case RoomType.Dapur: return new Color(0.36f, 0.48f, 0.32f);
                case RoomType.KamarTidur: return new Color(0.55f, 0.36f, 0.32f);
                case RoomType.Perpustakaan: return new Color(0.5f, 0.38f, 0.22f);
                case RoomType.RuangMakan: return new Color(0.55f, 0.3f, 0.26f);
                case RoomType.KamarAnak: return new Color(0.48f, 0.36f, 0.5f);
                case RoomType.Gudang: return new Color(0.32f, 0.32f, 0.32f);
                case RoomType.RuangKeluarga: return new Color(0.46f, 0.38f, 0.28f);
                default: return new Color(0.44f, 0.36f, 0.27f);
            }
        }

        public static Texture2D Build(HouseLayout L, HouseInstance H, int f, bool[] visited, float[] heat, bool exitOpen, Texture2D reuse)
        {
            int W = L.w * CS + 2 * M, Ht = L.h * CS + 2 * M;
            Texture2D t = reuse != null && reuse.width == W && reuse.height == Ht ? reuse
                : new Texture2D(W, Ht, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Map" + f };
            Color32[] px = new Color32[W * Ht];
            float maxHeat = 1f;
            if (heat != null) foreach (float v in heat) maxHeat = Mathf.Max(maxHeat, v);
            Color wallC = new Color(0.87f, 0.81f, 0.71f, 1f);
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    Room r = L.rooms[L.room[f, x, y]];
                    bool vis = visited == null || (r.id >= 0 && r.id < visited.Length && visited[r.id]);
                    Color c = vis ? new Color(0.25f, 0.235f, 0.22f) : new Color(0.13f, 0.123f, 0.118f);
                    c = Color.Lerp(c, RoomTint(r.type) * (vis ? 1f : 0.55f), 0.16f);
                    if (L.blocked[f, x, y]) c *= 0.72f;
                    if (heat != null && heat.Length == L.Count)
                    {
                        float h = heat[L.Idx(new Vector3Int(x, y, f))] / maxHeat;
                        c = Color.Lerp(c, new Color(0.95f, 0.25f, 0.1f), Mathf.Clamp01(h) * 0.75f);
                    }
                    c.a = 0.97f;
                    Stair st = L.StairCell(f, new Vector2Int(x, y));
                    Color stepC = Color.Lerp(c, wallC, 0.42f); stepC.a = 0.97f;
                    Color32 c32 = c, s32 = stepC;
                    int ox = M + x * CS, oy = M + y * CS;
                    for (int j = 0; j < CS; j++)
                        for (int i = 0; i < CS; i++)
                        {
                            Color32 cc = c32;
                            if (st != null) { int along = (st.dir == 0 || st.dir == 2) ? j : i; if (along % 6 < 2) cc = s32; }
                            px[(oy + j) * W + ox + i] = cc;
                        }
                }
            const int th = 4;
            bool lockedNow = H != null && H.lockedDoor != null && H.lockedDoor.locked;
            Color lockC = new Color(0.95f, 0.22f, 0.16f, 1f), exitC = exitOpen ? new Color(0.45f, 0.95f, 0.55f, 1f) : lockC;
            int g0 = (int)(CS * 0.22f), g1 = (int)(CS * 0.78f);
            for (int x = 0; x < L.w; x++)
                for (int y = 0; y < L.h; y++)
                {
                    Vector2Int c = new Vector2Int(x, y);
                    for (int k = 0; k < 4; k++)
                    {
                        bool exit = f == L.exitNode.z && c == G.XY(L.exitNode) && k == L.exitDir;
                        bool door = L.IsDoor(f, c, k);
                        bool wall = L.Wall(f, c, k);
                        if (!exit && !door && !wall) continue;
                        int x0 = M + x * CS, y0 = M + y * CS;
                        int ax, ay;
                        bool horiz = k == 0 || k == 2;
                        if (k == 0) { ax = x0; ay = y0 + CS; }
                        else if (k == 2) { ax = x0; ay = y0; }
                        else if (k == 1) { ax = x0 + CS; ay = y0; }
                        else { ax = x0; ay = y0; }
                        if (exit)
                        {
                            Seg(px, W, Ht, ax, ay, horiz, 0, g0, th, wallC);
                            Seg(px, W, Ht, ax, ay, horiz, g1, CS, th, wallC);
                            Seg(px, W, Ht, ax, ay, horiz, g0, g1, th + 2, exitC);
                        }
                        else if (door)
                        {
                            Seg(px, W, Ht, ax, ay, horiz, 0, g0, th, wallC);
                            Seg(px, W, Ht, ax, ay, horiz, g1, CS, th, wallC);
                            if (lockedNow && L.IsLocked(f, c, k)) Seg(px, W, Ht, ax, ay, horiz, g0, g1, th, lockC);
                        }
                        else Seg(px, W, Ht, ax, ay, horiz, 0, CS, th, wallC);
                    }
                }
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        static void Seg(Color32[] px, int W, int Ht, int ax, int ay, bool horiz, int s0, int s1, int th, Color c)
        {
            Color32 cc = c;
            int e0 = s0 == 0 ? -th / 2 : s0, e1 = s1 == CS ? CS + th / 2 : s1;
            for (int s = e0; s < e1; s++)
                for (int o = -th / 2; o < th - th / 2; o++)
                {
                    int x = horiz ? ax + s : ax + o, y = horiz ? ay + o : ay + s;
                    if (x < 0 || y < 0 || x >= W || y >= Ht) continue;
                    px[y * W + x] = cc;
                }
        }
    }

    /// <summary>Satu tampilan denah (RawImage + ikon + titik jalur + penanda pemain).</summary>
    public class MapView
    {
        public RectTransform root;
        public RawImage img;
        public float scale = 1f;
        public bool counter;
        readonly RectTransform dotLayer, iconLayer;
        readonly List<Image> icons = new List<Image>(), dots = new List<Image>();
        readonly Image player;
        int ni, nd; float angle;

        public MapView(Transform parent, string name)
        {
            root = UIKit.Node(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            img = root.gameObject.AddComponent<RawImage>(); img.raycastTarget = false;
            dotLayer = UIKit.Stretch(root, "Path");
            iconLayer = UIKit.Stretch(root, "Icons");
            player = UIKit.Icon(root, "Player", "player", Vector2.zero, Vector2.zero, 26f, new Color(0.55f, 0.9f, 1f), new Vector2(0.5f, 0.5f));
            Shadow sh = player.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.9f); sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public void SetTexture(Texture2D t, float s)
        {
            img.texture = t; scale = s;
            root.sizeDelta = t != null ? new Vector2(t.width, t.height) * s : Vector2.zero;
        }
        public Vector2 Local(Vector3 w) { return MapTex.ToPx(w) * scale; }

        public void Begin() { ni = 0; nd = 0; }
        public void AddIcon(string icon, Vector3 w, float size, Color col)
        {
            Image i;
            if (ni < icons.Count) i = icons[ni];
            else
            {
                i = UIKit.Icon(iconLayer, "I", icon, Vector2.zero, Vector2.zero, size, col, new Vector2(0.5f, 0.5f));
                Shadow sh = i.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.85f); sh.effectDistance = new Vector2(1.5f, -1.5f);
                icons.Add(i);
            }
            ni++;
            if (!i.gameObject.activeSelf) i.gameObject.SetActive(true);
            i.sprite = Icons.Get(icon); i.color = col;
            i.rectTransform.sizeDelta = new Vector2(size, size);
            i.rectTransform.anchoredPosition = Local(w);
            i.rectTransform.localEulerAngles = new Vector3(0f, 0f, counter ? -angle : 0f);
        }
        public void AddDot(Vector3 w, float size, Color col)
        {
            Image i;
            if (nd < dots.Count) i = dots[nd];
            else { i = UIKit.Icon(dotLayer, "D", "disc", Vector2.zero, Vector2.zero, size, col, new Vector2(0.5f, 0.5f)); dots.Add(i); }
            nd++;
            if (!i.gameObject.activeSelf) i.gameObject.SetActive(true);
            i.color = col; i.rectTransform.sizeDelta = new Vector2(size, size);
            i.rectTransform.anchoredPosition = Local(w);
        }
        public void End()
        {
            for (int i = ni; i < icons.Count; i++) if (icons[i].gameObject.activeSelf) icons[i].gameObject.SetActive(false);
            for (int i = nd; i < dots.Count; i++) if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
        }
        public void SetPlayer(Vector3 w, float yaw, bool on)
        {
            if (player.gameObject.activeSelf != on) player.gameObject.SetActive(on);
            player.rectTransform.anchoredPosition = Local(w);
            player.rectTransform.localEulerAngles = new Vector3(0f, 0f, -yaw);
            player.transform.SetAsLastSibling();
        }
        /// <summary>Peta diputar (minimap): ikon diputar balik agar tetap tegak.</summary>
        public void Counter(float a)
        {
            angle = a;
            for (int i = 0; i < ni; i++) icons[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -a);
        }
    }

    /// <summary>Denah 1-2 lantai berdampingan (dipakai di peta penuh & layar jeda).</summary>
    public class MapPanel
    {
        public RectTransform root;
        public readonly MapView[] views = new MapView[2];
        readonly Text[] labels = new Text[2];
        readonly RectTransform[] frames = new RectTransform[2];
        readonly Vector2 area;
        public int floors = 1;

        public MapPanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 area)
        {
            this.area = area;
            root = UIKit.Node(parent, name, anchor, pos, area);
            for (int f = 0; f < 2; f++)
            {
                frames[f] = UIKit.Node(root, "Floor" + (f + 1), new Vector2(0.5f, 0.5f), Vector2.zero, area);
                views[f] = new MapView(frames[f], "View");
                labels[f] = UIKit.Label(frames[f], "Label", "", 21, TextAnchor.LowerCenter, new Vector2(0.5f, 1f), new Vector2(0f, 8f), new Vector2(700f, 32f), UIKit.Dim, FontKind.Head, new Vector2(0.5f, 0f));
            }
        }

        public void Layout(HouseLayout L, Texture2D[] tex)
        {
            floors = L.floors;
            const float gap = 80f, labelH = 46f;
            float wEach = floors > 1 ? (area.x - gap) * 0.5f : area.x, hAvail = area.y - labelH;
            for (int f = 0; f < 2; f++)
            {
                bool on = f < floors && tex[f] != null;
                frames[f].gameObject.SetActive(on);
                if (!on) continue;
                float s = Mathf.Min(wEach / tex[f].width, hAvail / tex[f].height);
                views[f].SetTexture(tex[f], s);
                Vector2 sz = views[f].root.sizeDelta;
                frames[f].sizeDelta = sz;
                float x = floors > 1 ? (f == 0 ? -1f : 1f) * (wEach + gap) * 0.5f : 0f;
                frames[f].anchoredPosition = new Vector2(x, -labelH * 0.5f);
                views[f].root.anchoredPosition = Vector2.zero;
            }
        }
        public void SetLabel(int f, string s) { labels[f].text = s; }
    }

    /// <summary>Kompas di atas layar: arah mata angin + penanda tujuan berikutnya (mengikuti jalur) + jarak.</summary>
    public class CompassBar
    {
        public RectTransform root;
        readonly Text[] dirs = new Text[8];
        readonly Image[] ticks = new Image[16];
        readonly RectTransform markerRt;
        readonly Image marker, floorArrow, battMarker;
        readonly Text dist;
        const float Width = 620f, PxPerDeg = 3.3f;
        static readonly string[] Names = { "U", "TL", "T", "TG", "S", "BD", "B", "BL" };

        public CompassBar(Transform parent)
        {
            root = UIKit.Node(parent, "Compass", new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(Width + 40f, 80f), new Vector2(0.5f, 1f));
            UIKit.Rounded(root, "Bg", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width + 40f, 40f), new Color(0f, 0f, 0f, 0.4f), 12, new Vector2(0.5f, 1f));
            RectTransform strip = UIKit.Node(root, "Strip", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, 40f), new Vector2(0.5f, 1f));
            RectMask2D m = strip.gameObject.AddComponent<RectMask2D>(); m.softness = new Vector2Int(80, 0);
            for (int i = 0; i < 8; i++)
                dirs[i] = UIKit.Label(strip, "D" + i, Names[i], i % 2 == 0 ? 22 : 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 36f), i % 2 == 0 ? UIKit.Ink : UIKit.Dim, FontKind.Head);
            for (int j = 0; j < ticks.Length; j++) ticks[j] = UIKit.Panel(strip, "T" + j, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 9f), new Color(1f, 0.93f, 0.82f, 0.35f));
            UIKit.Icon(root, "Notch", "arrow", new Vector2(0.5f, 1f), new Vector2(0f, -41f), 12f, UIKit.Gold, new Vector2(0.5f, 1f));
            markerRt = UIKit.Node(root, "Marker", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));
            marker = markerRt.gameObject.AddComponent<Image>(); marker.preserveAspect = true; marker.raycastTarget = false;
            Shadow sh = markerRt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.9f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            floorArrow = UIKit.Icon(markerRt, "Floor", "arrow", new Vector2(1f, 1f), new Vector2(8f, -2f), 14f, UIKit.Gold, new Vector2(0.5f, 0.5f));
            dist = UIKit.Label(markerRt, "Dist", "", 18, TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(160f, 26f), UIKit.Gold, FontKind.Head, new Vector2(0.5f, 1f));
            battMarker = UIKit.Icon(root, "Batt", "battery", new Vector2(0.5f, 1f), new Vector2(0f, -20f), 22f, UIKit.Good, new Vector2(0.5f, 0.5f));
        }

        public void Tick(float yaw, Guidance g, Vector3 me, bool on)
        {
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;
            for (int i = 0; i < 8; i++) Place(dirs[i].rectTransform, yaw, i * 45f);
            int j = 0;
            for (int b = 0; b < 24 && j < ticks.Length; b++) { if (b % 3 == 0) continue; Place(ticks[j].rectTransform, yaw, b * 15f); j++; }
            bool has = g.kind != Guidance.Kind.None;
            if (markerRt.gameObject.activeSelf != has) markerRt.gameObject.SetActive(has);
            if (has)
            {
                float delta = Bearing(yaw, me, g.nextPoint);
                float x = delta * PxPerDeg, lim = Width * 0.5f - 16f;
                bool clamped = Mathf.Abs(x) > lim;
                markerRt.anchoredPosition = new Vector2(Mathf.Clamp(x, -lim, lim), -20f);
                marker.sprite = Icons.Get(g.IconName);
                Color c = g.IconColor; if (clamped) c.a = 0.5f;
                marker.color = c;
                dist.text = Mathf.RoundToInt(g.distance) + " m";
                bool fl = g.floorDelta != 0;
                if (floorArrow.gameObject.activeSelf != fl) floorArrow.gameObject.SetActive(fl);
                floorArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, g.floorDelta > 0 ? 0f : 180f);
            }
            bool bat = g.battery != null;
            if (battMarker.gameObject.activeSelf != bat) battMarker.gameObject.SetActive(bat);
            if (bat)
            {
                float lim = Width * 0.5f - 16f;
                float x = Mathf.Clamp(Bearing(yaw, me, g.battery.transform.position) * PxPerDeg, -lim, lim);
                battMarker.rectTransform.anchoredPosition = new Vector2(x, -20f);
            }
        }

        static float Bearing(float yaw, Vector3 me, Vector3 to)
        {
            Vector3 d = to - me;
            return Mathf.DeltaAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }

        static void Place(RectTransform r, float yaw, float bearing)
        {
            float x = Mathf.DeltaAngle(yaw, bearing) * PxPerDeg;
            bool vis = Mathf.Abs(x) < Width * 0.5f + 20f;
            if (r.gameObject.activeSelf != vis) r.gameObject.SetActive(vis);
            r.anchoredPosition = new Vector2(x, 0f);
        }
    }

    /// <summary>Peta mini (berputar mengikuti arah pandang), peta penuh (M), denah di layar jeda, dan kompas.</summary>
    public class MapUI : MonoBehaviour
    {
        public readonly Guidance guide = new Guidance();
        public bool fullOpen, present;
        public int fullOpens;
        GameManager gm;
        HouseLayout bound;
        readonly Texture2D[] tex = new Texture2D[2];
        bool[] visited;
        bool dirty, lastLock, lastExit, lastPresent;
        float heatT, guideT, popMini, popFull, popPause;
        int miniFloor = -1;
        RectTransform hudRoot, miniRoot, north, fullRoot;
        MapView mini;
        Text miniFloorTxt, fullTitle, fullSub;
        MapPanel full, pausePanel;
        CompassBar compass;
        const float MiniScale = 0.9f;

        // ------------------------------------------------------------------ bangun
        public void Build(RectTransform hud)
        {
            hudRoot = hud;
            compass = new CompassBar(hud);
            miniRoot = UIKit.Node(hud, "Minimap", new Vector2(1f, 1f), new Vector2(-34f, -30f), new Vector2(262f, 262f), new Vector2(1f, 1f));
            UIKit.Icon(miniRoot, "Shadow", "soft", new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), 340f, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Icon(miniRoot, "Bg", "disc", new Vector2(0.5f, 0.5f), Vector2.zero, 262f, new Color(0.035f, 0.032f, 0.034f, 0.9f));
            RectTransform mask = UIKit.Node(miniRoot, "Mask", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(248f, 248f));
            Image mi = mask.gameObject.AddComponent<Image>(); mi.sprite = Icons.Get("disc"); mi.raycastTarget = false;
            Mask mk = mask.gameObject.AddComponent<Mask>(); mk.showMaskGraphic = false;
            mini = new MapView(mask, "MiniView") { counter = true };
            UIKit.Icon(miniRoot, "Ring", "ringthin", new Vector2(0.5f, 0.5f), Vector2.zero, 262f, new Color(1f, 0.93f, 0.82f, 0.3f));
            north = UIKit.Label(miniRoot, "North", "U", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f), UIKit.Gold, FontKind.Head).rectTransform;
            miniFloorTxt = UIKit.Label(miniRoot, "Floor", "LANTAI 1", 19, TextAnchor.UpperRight, new Vector2(1f, 0f), new Vector2(-6f, -8f), new Vector2(200f, 26f), UIKit.Dim, FontKind.Head, new Vector2(1f, 1f));
            UIKit.Keycap(miniRoot, "M", new Vector2(0f, 0f), new Vector2(8f, -6f), 28f, new Vector2(0f, 1f));
            UIKit.Label(miniRoot, "MapLbl", "Peta", 19, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(44f, -20f), new Vector2(120f, 26f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
        }

        /// <summary>Peta penuh dibangun terakhir agar tampil di atas elemen HUD lain.</summary>
        public void BuildFull(RectTransform hud)
        {
            fullRoot = UIKit.Stretch(hud, "FullMap");
            UIKit.Fill(fullRoot, "Dim", new Color(0f, 0f, 0f, 0.62f));
            RectTransform card = UIKit.Card(fullRoot, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 930f));
            fullTitle = UIKit.Label(card, "Title", "DENAH RUMAH", 38, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(44f, -30f), new Vector2(900f, 50f), UIKit.Ink, FontKind.Head, new Vector2(0f, 1f));
            fullSub = UIKit.Label(card, "Sub", "", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(46f, -84f), new Vector2(1200f, 30f), UIKit.Gold, FontKind.Body, new Vector2(0f, 1f));
            UIKit.Keycap(card, "M", new Vector2(1f, 1f), new Vector2(-170f, -34f), 36f, new Vector2(1f, 1f));
            UIKit.Label(card, "Close", "Tutup peta", 21, TextAnchor.MiddleLeft, new Vector2(1f, 1f), new Vector2(-158f, -52f), new Vector2(140f, 30f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
            full = new MapPanel(card, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(1540f, 640f));
            Legend(card, new Vector2(0.5f, 0f), new Vector2(0f, 50f), 1560f, 19);
            fullRoot.gameObject.SetActive(false);
        }

        public static void Legend(Transform parent, Vector2 anchor, Vector2 pos, float maxWidth, int fs)
        {
            (string, Color, string)[] items =
            {
                ("player", new Color(0.55f, 0.9f, 1f), "Kamu"), ("relic", UIKit.Gold, "Relik"), ("key", new Color(1f, 0.42f, 0.36f), "Kunci tua"),
                ("lock", UIKit.Danger, "Pintu digembok"), ("exit", UIKit.Good, "Pintu keluar"), ("battery", UIKit.Good, "Baterai"),
                ("hide", new Color(0.6f, 0.66f, 0.78f), "Tempat sembunyi"), ("stairs", UIKit.Ink, "Tangga"), ("disc", UIKit.Gold, "Jalur ke tujuan"),
            };
            RectTransform box = UIKit.Node(parent, "Legend", anchor, pos, new Vector2(maxWidth, 30f));
            List<List<RectTransform>> rows = new List<List<RectTransform>> { new List<RectTransform>() };
            float rowW = 0f, gap = 30f;
            foreach (var it in items)
            {
                RectTransform r = UIKit.Node(box, "L", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 30f));
                float isz = it.Item1 == "disc" ? 10f : fs * 1.25f;
                UIKit.Icon(r, "I", it.Item1, new Vector2(0f, 0.5f), new Vector2(fs * 0.62f, 0f), isz, it.Item2, new Vector2(0.5f, 0.5f));
                Text t = UIKit.Label(r, "T", it.Item3, fs, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(fs * 1.25f + 8f, 0f), new Vector2(400f, 30f), UIKit.Dim, FontKind.Head, new Vector2(0f, 0.5f));
                float w = fs * 1.25f + 8f + t.preferredWidth;
                t.rectTransform.sizeDelta = new Vector2(t.preferredWidth + 4f, 30f);
                r.sizeDelta = new Vector2(w, 30f);
                if (rowW > 0f && rowW + gap + w > maxWidth) { rows.Add(new List<RectTransform>()); rowW = 0f; }
                rowW += (rowW > 0f ? gap : 0f) + w;
                rows[rows.Count - 1].Add(r);
            }
            for (int i = 0; i < rows.Count; i++) UIKit.RowCenter(rows[i].ToArray(), (rows.Count - 1 - i) * 36f, gap);
        }

        public void AttachPausePanel(MapPanel p) { pausePanel = p; bound = null; }

        public void ToggleFull() { SetFull(!fullOpen); }
        public void SetFull(bool on)
        {
            if (on == fullOpen) return;
            fullOpen = on;
            if (on) { fullOpens++; popFull = 0f; AudioDirector.I?.Click(); }
        }

        // ------------------------------------------------------------------ data
        void Bind(HouseLayout L)
        {
            bound = L;
            visited = new bool[L.rooms.Count];
            Room start = L.RoomAt(L.start);
            if (start != null && start.id < visited.Length) visited[start.id] = true;
            for (int f = 0; f < 2; f++) if (tex[f] != null) { Destroy(tex[f]); tex[f] = null; }
            miniFloor = -1; fullOpen = false; popMini = popFull = popPause = 0f; guideT = 0f;
            lastLock = gm.house.lockedDoor != null && gm.house.lockedDoor.locked;
            lastExit = gm.relicsFound >= gm.RelicsNeeded;
            Rebuild();
            full.Layout(L, tex);
            if (pausePanel != null) pausePanel.Layout(L, tex);
        }

        void Rebuild()
        {
            HouseLayout L = gm.layout;
            for (int f = 0; f < L.floors && f < 2; f++)
                tex[f] = MapTex.Build(L, gm.house, f, visited, present ? GhostMemory.heat : null, gm.relicsFound >= gm.RelicsNeeded, tex[f]);
        }

        static int FloorOf(HouseLayout L, Vector3 w) { return G.F(w.y, L.floors); }

        void Populate(MapView v, int f, bool mini)
        {
            HouseLayout L = gm.layout; HouseInstance H = gm.house; PlayerController p = gm.player;
            float k = mini ? 1f : 1.3f;
            v.Begin();
            foreach (HidingSpot s in H.spots) if (s != null && s.floor == f) v.AddIcon("hide", s.transform.position, 14f * k, new Color(0.6f, 0.66f, 0.78f, 0.8f));
            foreach (Stair st in L.stairs) if (st.f == f || st.f + 1 == f) v.AddIcon("stairs", G.W(f, st.Ramp1), 20f * k, new Color(0.9f, 0.86f, 0.78f, 0.95f));
            if (Settings.Compass && guide.path.Count > 1)
            {
                Color dc = new Color(1f, 0.82f, 0.45f, 0.9f);
                for (int i = 1; i < guide.path.Count; i++)
                {
                    Vector3 a = guide.path[i - 1], b = guide.path[i];
                    int n = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(a, b) / 1.1f));
                    for (int j = 0; j < n; j++)
                    {
                        Vector3 q = Vector3.Lerp(a, b, (j + 0.5f) / n);
                        if (FloorOf(L, q) == f) v.AddDot(q, 6f * k, dc);
                    }
                }
            }
            foreach (BatteryPickup b in H.batteries) if (b != null && !b.taken && FloorOf(L, b.transform.position) == f) v.AddIcon("battery", b.transform.position, 15f * k, UIKit.Good);
            if (H.key != null && !H.key.taken && FloorOf(L, H.key.transform.position) == f) v.AddIcon("key", H.key.transform.position, 21f * k, new Color(1f, 0.42f, 0.36f));
            if (H.lockedDoor != null && H.lockedDoor.locked && FloorOf(L, H.lockedDoor.transform.position) == f) v.AddIcon("lock", H.lockedDoor.transform.position, 21f * k, UIKit.Danger);
            foreach (RelicPickup r in H.relics) if (r != null && !r.taken && FloorOf(L, r.transform.position) == f) v.AddIcon("relic", r.transform.position, 23f * k, UIKit.Gold);
            if (H.exit != null && L.exitNode.z == f) v.AddIcon("exit", H.exit.transform.position, 25f * k, gm.relicsFound >= gm.RelicsNeeded ? UIKit.Good : new Color(1f, 0.45f, 0.38f));
            if (present && gm.ghost != null && gm.ghost.gameObject.activeSelf && FloorOf(L, gm.ghost.transform.position) == f)
                v.AddIcon("ghost", gm.ghost.transform.position, 22f * k, new Color(1f, 0.3f, 0.25f));
            v.SetPlayer(p.transform.position, p.Yaw, p.floor == f);
            v.End();
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            gm = GameManager.I;
            if (gm == null || gm.layout == null || gm.house == null || gm.player == null || full == null) return;
            HouseLayout L = gm.layout; PlayerController p = gm.player;
            float dt = Time.unscaledDeltaTime;
            if (L != bound) Bind(L);
            Room cur = L.RoomAt(p.node);
            if (cur != null && cur.id < visited.Length && !visited[cur.id]) { visited[cur.id] = true; dirty = true; }
            bool lk = gm.house.lockedDoor != null && gm.house.lockedDoor.locked, ex = gm.relicsFound >= gm.RelicsNeeded;
            if (lk != lastLock || ex != lastExit) { lastLock = lk; lastExit = ex; dirty = true; }
            heatT -= dt;
            if (present && heatT <= 0f) { heatT = 1f; dirty = true; }
            if (present != lastPresent) { lastPresent = present; dirty = true; }
            if (dirty) { Rebuild(); dirty = false; }
            guideT -= dt;
            if (guideT <= 0f) { guideT = 0.3f; guide.Compute(gm); }

            bool playing = gm.Playing;
            float yaw = p.Yaw;
            compass.Tick(yaw, guide, p.transform.position, Settings.Compass && playing);

            // peta mini
            bool showMini = Settings.Minimap && playing;
            if (miniRoot.gameObject.activeSelf != showMini) miniRoot.gameObject.SetActive(showMini);
            if (showMini)
            {
                int f = Mathf.Clamp(p.floor, 0, L.floors - 1);
                if (f != miniFloor && tex[f] != null) { mini.SetTexture(tex[f], MiniScale); miniFloor = f; popMini = 0f; miniFloorTxt.text = L.FloorName(f).ToUpper(); }
                Vector2 pp = mini.Local(p.transform.position), sz = mini.root.sizeDelta;
                if (sz.x > 1f && sz.y > 1f) mini.root.pivot = new Vector2(pp.x / sz.x, pp.y / sz.y);
                mini.root.anchoredPosition = Vector2.zero;
                mini.root.localEulerAngles = new Vector3(0f, 0f, yaw);
                popMini -= dt;
                if (popMini <= 0f) { popMini = 0.2f; Populate(mini, f, true); }
                mini.Counter(yaw);
                mini.SetPlayer(p.transform.position, yaw, true);
                float a = yaw * Mathf.Deg2Rad;
                north.anchoredPosition = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 117f;
            }

            // peta penuh
            bool showFull = fullOpen && playing;
            if (fullRoot.gameObject.activeSelf != showFull) fullRoot.gameObject.SetActive(showFull);
            if (showFull)
            {
                popFull -= dt;
                if (popFull <= 0f)
                {
                    popFull = 0.2f;
                    for (int f = 0; f < L.floors && f < 2; f++) { Populate(full.views[f], f, false); full.SetLabel(f, FloorLabel(L, f)); }
                    fullTitle.text = "DENAH RUMAH #" + gm.houseNo;
                    fullSub.text = guide.text.Length > 0 ? "Tujuan: " + guide.text + "   ·   " + Mathf.RoundToInt(guide.distance) + " m" : "";
                }
            }

            // denah di layar jeda
            if (pausePanel != null && pausePanel.root.gameObject.activeInHierarchy)
            {
                popPause -= dt;
                if (popPause <= 0f)
                {
                    popPause = 0.25f;
                    for (int f = 0; f < L.floors && f < 2; f++) { Populate(pausePanel.views[f], f, false); pausePanel.SetLabel(f, FloorLabel(L, f)); }
                }
            }
        }

        string FloorLabel(HouseLayout L, int f)
        {
            return L.FloorName(f).ToUpper() + (gm.player.floor == f ? "   <color=#8fe6ff>·  KAMU DI SINI</color>" : "");
        }
    }
}
