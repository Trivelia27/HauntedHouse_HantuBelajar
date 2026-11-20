using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Memori belajar hantu (model probabilistik).
    ///  - Jangka panjang (lintas rumah, disimpan): hitungan Dirichlet jenis tempat sembunyi & tipe ruangan favorit pemain.
    ///  - Jangka pendek (rumah ini): tiap tempat sembunyi = Beta(alfa, beta). Prior dari memori jangka panjang;
    ///    alfa naik saat pemain sembunyi di situ, beta naik saat hantu memeriksa dan (menurutnya) kosong.
    ///  - Pemilihan tempat yang diperiksa memakai Thompson sampling: sampel theta ~ Beta -> eksplorasi alami.
    ///  - Jalur favorit: heatmap posisi pemain dengan peluruhan (half-life +-2 menit) -> lokasi hadang (softmax).
    /// </summary>
    public static class GhostMemory
    {
        public const float PriorStrength = 3f;
        public static float[] kindCount = new float[2];
        public static float[] roomCount = new float[9];
        public static int totalHides, totalChecks, totalFinds, housesSeen;
        static readonly float[] pendingKind = new float[2];
        static readonly float[] pendingRoom = new float[9];
        static readonly Dictionary<int, Vector2> local = new Dictionary<int, Vector2>();   // x = tambahan alfa, y = tambahan beta
        public static float[] heat;
        public static readonly List<string> feed = new List<string>();
        static readonly System.Random rng = new System.Random(4242);
        static float decayT;

        [System.Serializable] class Save { public float[] k; public float[] r; public int h, c, f, n; }

        public static void Load()
        {
            string s = PlayerPrefs.GetString("HB_GhostMemory2", "");
            if (string.IsNullOrEmpty(s)) return;
            Save d = JsonUtility.FromJson<Save>(s);
            if (d == null || d.k == null || d.k.Length != 2 || d.r == null || d.r.Length != 9) return;
            kindCount = d.k; roomCount = d.r; totalHides = d.h; totalChecks = d.c; totalFinds = d.f; housesSeen = d.n;
        }
        public static void Persist()
        {
            PlayerPrefs.SetString("HB_GhostMemory2", JsonUtility.ToJson(new Save { k = kindCount, r = roomCount, h = totalHides, c = totalChecks, f = totalFinds, n = housesSeen }));
        }
        public static void Reset()
        {
            kindCount = new float[2]; roomCount = new float[9]; totalHides = totalChecks = totalFinds = housesSeen = 0;
            System.Array.Clear(pendingKind, 0, 2); System.Array.Clear(pendingRoom, 0, 9);
            local.Clear();
            if (heat != null) System.Array.Clear(heat, 0, heat.Length);
            Persist();
            Feed("Memori hantu dihapus - hantu mulai dari nol");
        }

        public static void BeginHouse(HouseLayout L)
        {
            CommitHouse();
            heat = new float[L.Count];
            local.Clear();
            housesSeen++;
            Persist();
        }

        /// <summary>Pindahkan kebiasaan rumah yang baru selesai ke prior lintas rumah.</summary>
        public static void CommitHouse()
        {
            for (int i = 0; i < 2; i++) { kindCount[i] += pendingKind[i]; pendingKind[i] = 0f; }
            for (int i = 0; i < 9; i++) { roomCount[i] += pendingRoom[i]; pendingRoom[i] = 0f; }
        }

        public static void Feed(string s) { feed.Add(s); if (feed.Count > 6) feed.RemoveAt(0); }

        // ------------------------------------------------------------------ jalur favorit
        public static void RecordPos(HouseLayout L, Vector3Int n, float dt)
        {
            if (heat == null || !L.In(n)) return;
            heat[L.Idx(n)] += dt;
            decayT += dt;
            if (decayT >= 1f)
            {
                decayT -= 1f;
                for (int i = 0; i < heat.Length; i++) heat[i] *= 0.995f;
            }
        }

        /// <summary>Pilih node hadang dengan softmax atas heatmap (temperature makin kecil = makin rakus).</summary>
        public static bool PickAmbush(HouseLayout L, float temperature, out Vector3Int node, out float share)
        {
            node = default; share = 0f;
            if (heat == null) return false;
            float max = 0f, total = 0f;
            for (int i = 0; i < heat.Length; i++) { max = Mathf.Max(max, heat[i]); total += heat[i]; }
            if (max < 3f) return false;
            List<int> cand = new List<int>(); List<float> w = new List<float>();
            float sum = 0f;
            for (int i = 0; i < heat.Length; i++)
            {
                if (heat[i] < max * 0.25f) continue;
                Vector3Int n = L.FromIdx(i);
                if (L.Blocked(n)) continue;
                float e = Mathf.Exp((heat[i] / max - 1f) / Mathf.Max(0.05f, temperature));
                cand.Add(i); w.Add(e); sum += e;
            }
            if (cand.Count == 0) return false;
            float r = (float)rng.NextDouble() * sum;
            for (int i = 0; i < cand.Count; i++)
            {
                r -= w[i];
                if (r <= 0f || i == cand.Count - 1) { node = L.FromIdx(cand[i]); share = heat[cand[i]] / Mathf.Max(1f, total); return true; }
            }
            return false;
        }

        public static float HeatAt(HouseLayout L, Vector3Int n) { return heat != null && L.In(n) ? heat[L.Idx(n)] : 0f; }

        // ------------------------------------------------------------------ tempat sembunyi (Beta-Bernoulli)
        public static float KindPref(int k) { float s = kindCount[0] + kindCount[1]; return (kindCount[k] + 1f) / (s + 2f); }
        public static float RoomPref(RoomType t) { float s = 0f; foreach (float v in roomCount) s += v; return (roomCount[(int)t] + 0.5f) / (s + 4.5f); }

        /// <summary>Rata-rata prior: seberapa mungkin pemain memakai tempat jenis ini (dari kebiasaan lintas rumah).</summary>
        public static float PriorMean(HidingSpot s)
        {
            float kp = KindPref((int)s.kind), rp = RoomPref(s.roomType);
            // netral (belum ada data): 0,33 + 0,35/9 = +-0,37; makin sering jenis/ruang ini dipakai, makin tinggi
            return Mathf.Clamp(0.33f + 0.45f * (kp - 0.5f) + 0.35f * rp, 0.05f, 0.9f);
        }

        public static void AlphaBeta(HidingSpot s, out float a, out float b)
        {
            float m = PriorMean(s);
            a = m * PriorStrength; b = (1f - m) * PriorStrength;
            if (local.TryGetValue(s.id, out Vector2 v)) { a += v.x; b += v.y; }
        }

        /// <summary>Rata-rata posterior E[theta] = alfa / (alfa + beta).</summary>
        public static float Mean(HidingSpot s) { AlphaBeta(s, out float a, out float b); return a / (a + b); }

        /// <summary>Thompson sampling: theta ~ Beta(alfa, beta).</summary>
        public static float Sample(HidingSpot s)
        {
            AlphaBeta(s, out float a, out float b);
            float x = Gamma(a), y = Gamma(b);
            return x / Mathf.Max(1e-6f, x + y);
        }

        public static int Uses(HidingSpot s) { return local.TryGetValue(s.id, out Vector2 v) ? Mathf.RoundToInt(v.x) : 0; }

        public static void RecordHide(HidingSpot s)
        {
            local.TryGetValue(s.id, out Vector2 v); v.x += 1f; local[s.id] = v;
            pendingKind[(int)s.kind] += 1f; pendingRoom[(int)s.roomType] += 1f; totalHides++;
            Persist();
            Feed("Mencatat: " + s.KindName + " (" + HouseGenerator.RoomName(s.roomType) + ")  ·  dipakai " + Uses(s) + "×  ·  E[θ] = " + Mean(s).ToString("0.00"));
        }

        public static void RecordCheck(HidingSpot s, bool found)
        {
            totalChecks++;
            if (found) totalFinds++;
            else { local.TryGetValue(s.id, out Vector2 v); v.y += 0.7f; local[s.id] = v; }
            Persist();
        }

        // Marsaglia-Tsang
        static float Gamma(float k)
        {
            if (k < 1f) return Gamma(k + 1f) * Mathf.Pow((float)rng.NextDouble() + 1e-9f, 1f / k);
            float d = k - 1f / 3f, c = 1f / Mathf.Sqrt(9f * d);
            while (true)
            {
                float x, v;
                do { x = Normal(); v = 1f + c * x; } while (v <= 0f);
                v = v * v * v;
                float u = (float)rng.NextDouble();
                if (u < 1f - 0.0331f * x * x * x * x) return d * v;
                if (Mathf.Log(u) < 0.5f * x * x + d * (1f - v + Mathf.Log(v))) return d * v;
            }
        }
        static float Normal()
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }
    }
}
