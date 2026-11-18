using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HantuBelajar
{
    /// <summary>Material + tekstur prosedural (Built-in Standard shader).</summary>
    public static class Look
    {
        static Shader std;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();

        public static bool URP { get { return GraphicsSettings.currentRenderPipeline != null; } }
        static Shader Std
        {
            get
            {
                if (std == null) std = URP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                if (std == null) std = Shader.Find("Standard");
                return std;
            }
        }

        public static Material Mat(string key, Color c, float smooth = 0.15f, Texture2D t = null, Vector2? tiling = null)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Std) { name = key, color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (t != null) { m.mainTexture = t; if (tiling.HasValue) m.mainTextureScale = tiling.Value; }
            cache[key] = m;
            return m;
        }

        public static Material Emissive(string key, Color c, float intensity)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Std) { name = key, color = c * 0.3f };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            cache[key] = m;
            return m;
        }

        public static Material Fade(string key, Color c, Texture2D t = null)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Std) { name = key, color = c };
            if (t != null) m.mainTexture = t;
            MakeFade(m);
            cache[key] = m;
            return m;
        }

        public static void MakeFade(Material m)
        {
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                return;
            }
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        // ---------- tekstur prosedural ----------
        public static Texture2D Planks(Color baseC)
        {
            string key = "planks" + baseC;
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 256; t = NewTex(N);
            int planks = 6, pw = N / planks;
            System.Random r = new System.Random(7);
            float[] tone = new float[planks * 4];
            for (int i = 0; i < tone.Length; i++) tone[i] = 0.82f + (float)r.NextDouble() * 0.3f;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    int pi = x / pw; int seg = ((y + pi * 61) % N) / (N / 2);
                    float grain = Mathf.PerlinNoise(x * 0.08f + pi * 13f, y * 0.012f) * 0.35f + Mathf.PerlinNoise(x * 0.5f, y * 0.04f) * 0.12f;
                    float edge = (x % pw == 0 || (y + pi * 61) % (N / 2) == 0) ? 0.45f : 1f;
                    float v = tone[pi * 2 + seg] * (0.78f + grain) * edge;
                    t.SetPixel(x, y, new Color(baseC.r * v, baseC.g * v, baseC.b * v));
                }
            t.Apply(true); tex[key] = t; return t;
        }

        public static Texture2D Wallpaper(Color a, Color b)
        {
            string key = "wp" + a + b;
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 256; t = NewTex(N);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    float stripe = Mathf.Abs(Mathf.Sin(x / (float)N * Mathf.PI * 8f));
                    float u = (x % 32) / 32f - 0.5f, v = (y % 48) / 48f - 0.5f;
                    float motif = Mathf.Clamp01(1f - (Mathf.Abs(u) * 2.2f + Mathf.Abs(v) * 1.6f)) > 0.5f ? 1f : 0f;
                    float stain = Mathf.PerlinNoise(x * 0.015f, y * 0.015f);
                    Color c = Color.Lerp(a, b, stripe > 0.92f ? 1f : motif * 0.5f);
                    c *= 0.75f + stain * 0.4f;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply(true); tex[key] = t; return t;
        }

        public static Texture2D Tiles(Color a, Color b)
        {
            string key = "tiles" + a + b;
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 256; t = NewTex(N);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    bool grout = x % 64 < 3 || y % 64 < 3;
                    bool chk = ((x / 64) + (y / 64)) % 2 == 0;
                    float n = 0.85f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.25f;
                    Color c = grout ? new Color(0.18f, 0.17f, 0.15f) : (chk ? a : b) * n;
                    c.a = 1f; t.SetPixel(x, y, c);
                }
            t.Apply(true); tex[key] = t; return t;
        }

        public static Texture2D Noise(Color baseC, float amt, string key)
        {
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 128; t = NewTex(N);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    float n = 1f - amt + Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * amt * 1.6f;
                    Color c = baseC * n; c.a = 1f; t.SetPixel(x, y, c);
                }
            t.Apply(true); tex[key] = t; return t;
        }

        /// <summary>Gradasi radial (vignette / halo UI).</summary>
        public static Texture2D Radial(string key, float inner)
        {
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 128; t = NewTex(N); t.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                    float a = Mathf.Clamp01((d - inner) / (1f - inner));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply(); tex[key] = t; return t;
        }

        static Texture2D NewTex(int n)
        {
            return new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        }

        // ---------- palet per ruangan ----------
        public static Material Floor(RoomType t, Vector2 tiling)
        {
            AssetLibrary lib = AssetLibrary.I;
            string k = "floor_" + t + tiling;
            if (cache.TryGetValue(k, out Material m) && m != null) return m;
            if (lib != null && lib.floorByType != null && lib.floorByType.Length > (int)t && lib.floorByType[(int)t] != null)
            {
                m = new Material(lib.floorByType[(int)t]) { name = k };
                m.mainTextureScale = tiling * 0.75f;
            }
            else if (lib != null && lib.floorMat != null && t != RoomType.KamarMandi && t != RoomType.Dapur)
            {
                m = new Material(lib.floorMat) { name = k };
                m.mainTextureScale = tiling * 0.5f;
            }
            else if (t == RoomType.KamarMandi || t == RoomType.Dapur)
                m = Mat(k, Color.white, 0.35f, Tiles(new Color(0.62f, 0.6f, 0.55f), new Color(0.25f, 0.27f, 0.26f)), tiling * 0.75f);
            else
                m = Mat(k, Color.white, 0.22f, Planks(t == RoomType.Gudang ? new Color(0.36f, 0.3f, 0.24f) : new Color(0.42f, 0.29f, 0.19f)), tiling * 0.5f);
            cache[k] = m;
            return m;
        }

        public static Material Wall(RoomType t)
        {
            AssetLibrary lib = AssetLibrary.I;
            string k = "wall_" + t;
            if (cache.TryGetValue(k, out Material m) && m != null) return m;
            if (lib != null && lib.wallByType != null && lib.wallByType.Length > (int)t && lib.wallByType[(int)t] != null) { m = new Material(lib.wallByType[(int)t]) { name = k }; m.mainTextureScale = new Vector2(1.5f, 1f); cache[k] = m; return m; }
            if (lib != null && lib.wallMat != null) { m = new Material(lib.wallMat) { name = k }; cache[k] = m; return m; }
            Color a, b;
            switch (t)
            {
                case RoomType.KamarTidur: a = new Color(0.34f, 0.22f, 0.24f); b = new Color(0.45f, 0.33f, 0.3f); break;
                case RoomType.KamarAnak: a = new Color(0.3f, 0.34f, 0.36f); b = new Color(0.45f, 0.44f, 0.4f); break;
                case RoomType.Perpustakaan: a = new Color(0.2f, 0.27f, 0.22f); b = new Color(0.32f, 0.33f, 0.25f); break;
                case RoomType.KamarMandi: case RoomType.Dapur: a = new Color(0.5f, 0.5f, 0.46f); b = new Color(0.44f, 0.45f, 0.42f); break;
                case RoomType.Gudang: a = new Color(0.3f, 0.28f, 0.25f); b = new Color(0.33f, 0.3f, 0.26f); break;
                default: a = new Color(0.36f, 0.3f, 0.22f); b = new Color(0.46f, 0.38f, 0.26f); break;
            }
            m = Mat(k, Color.white, 0.08f, Wallpaper(a, b), new Vector2(1.5f, 1f));
            return m;
        }
    }
}
