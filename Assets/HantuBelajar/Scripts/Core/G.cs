using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Konstanta dunia. Satu sel grid = 3 modul dinding paket Haunted House (1 m x skala 1,2) = 3,6 m.
    /// Node grid ditulis sebagai Vector3Int(x, y, lantai).
    /// </summary>
    public static class G
    {
        public const float S = 1.2f;          // skala semua model paket (karakter paket 1,43 m -> manusia ~1,75 m)
        public const float M = 1.2f;          // panjang satu modul dinding
        public const float T = 3.6f;          // ukuran sel
        public const float WallH = 2.76f;     // tinggi dinding (2,30 x 1,2)
        public const float StoryH = 3.0f;     // tinggi satu lantai (dinding + pelat)
        public const float DoorH = 2.25f;     // tinggi bukaan pintu

        // 0 = utara (+z), 1 = timur (+x), 2 = selatan (-z), 3 = barat (-x)
        public static readonly Vector2Int[] Dirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        public static Vector3 W(Vector3Int n) { return new Vector3(n.x * T + T * 0.5f, n.z * StoryH, n.y * T + T * 0.5f); }
        public static Vector3 W(int f, Vector2Int c) { return new Vector3(c.x * T + T * 0.5f, f * StoryH, c.y * T + T * 0.5f); }
        public static Vector3Int N(int f, Vector2Int c) { return new Vector3Int(c.x, c.y, f); }
        public static Vector2Int XY(Vector3Int n) { return new Vector2Int(n.x, n.y); }
        public static Vector2Int C(Vector3 p) { return new Vector2Int(Mathf.FloorToInt(p.x / T), Mathf.FloorToInt(p.z / T)); }
        /// <summary>Lantai dari ketinggian kaki.</summary>
        public static int F(float y, int floors) { return Mathf.Clamp(Mathf.FloorToInt((y + 0.9f) / StoryH), 0, Mathf.Max(0, floors - 1)); }
        public static Vector3 D3(int dir) { Vector2Int d = Dirs[dir]; return new Vector3(d.x, 0f, d.y); }
        public static float Yaw(int dir) { return dir * 90f; }
        public static int Opp(int dir) { return (dir + 2) & 3; }
        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
