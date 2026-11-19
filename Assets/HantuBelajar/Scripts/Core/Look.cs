using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HantuBelajar
{
    /// <summary>Material runtime (URP Lit, fallback Standard) + tekstur prosedural pelengkap aset paket.</summary>
    public static class Look
    {
        static Shader lit, unlit, particle;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();

        public static bool URP { get { return GraphicsSettings.currentRenderPipeline != null; } }
        static Shader Lit
        {
            get
            {
                if (lit == null) lit = URP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                if (lit == null) lit = Shader.Find("Standard");
                return lit;
            }
        }
        static Shader Unlit
        {
            get
            {
                if (unlit == null) unlit = URP ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                return unlit;
            }
        }
        static Shader Particle
        {
            get
            {
                if (particle == null) particle = URP ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Particles/Standard Unlit");
                return particle;
            }
        }

        public static Material Mat(string key, Color c, float smooth = 0.15f, Texture2D t = null, Vector2? tiling = null, float metal = 0f)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Lit) { name = key, color = c };
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (t != null) { m.mainTexture = t; if (tiling.HasValue) m.mainTextureScale = tiling.Value; }
            cache[key] = m;
            return m;
        }

        public static Material Emissive(string key, Color c, float intensity)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Lit) { name = key, color = c * 0.25f };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            cache[key] = m;
            return m;
        }

        public static Material UnlitColor(string key, Color c)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Unlit) { name = key, color = c };
            cache[key] = m;
            return m;
        }

        public static Material Fade(string key, Color c, Texture2D t = null)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Lit) { name = key, color = c };
            if (t != null) m.mainTexture = t;
            MakeFade(m);
            cache[key] = m;
            return m;
        }

        public static Material ParticleMat(string key, Color c, Texture2D t)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Particle) { name = key };
            m.SetColor("_BaseColor", c); m.color = c;
            if (t != null) { m.SetTexture("_BaseMap", t); m.mainTexture = t; }
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            m.renderQueue = 3000;
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
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }

        // ------------------------------------------------------------------ material tematik
        public static Material Wood { get { return Mat("wood", Color.white, 0.3f, Planks(new Color(0.34f, 0.21f, 0.12f)), new Vector2(0.6f, 0.6f)); } }
        public static Material DarkWood { get { return Mat("darkwood", Color.white, 0.35f, Planks(new Color(0.2f, 0.12f, 0.07f)), new Vector2(0.6f, 0.6f)); } }
        public static Material Plaster { get { return Mat("plaster", new Color(0.62f, 0.6f, 0.56f), 0.05f, Noise(new Color(0.85f, 0.83f, 0.79f), 0.18f, "plasterN"), new Vector2(0.5f, 0.5f)); } }
        public static Material Iron { get { return Mat("iron", new Color(0.1f, 0.1f, 0.1f), 0.45f, null, null, 0.8f); } }
        public static Material Brass { get { return Mat("brass", new Color(0.55f, 0.42f, 0.2f), 0.65f, null, null, 0.9f); } }
        public static Material Cloth(Color c) { return Mat("cloth" + ColorKey(c), c, 0.05f, Noise(Color.white, 0.25f, "clothN")); }
        static string ColorKey(Color c) { return ((int)(c.r * 20)) + "_" + ((int)(c.g * 20)) + "_" + ((int)(c.b * 20)); }

        public static Material Floor(RoomType t)
        {
            Material m = AssetLibrary.Mat(FloorKey(t));
            if (m != null) return m;
            if (t == RoomType.KamarMandi || t == RoomType.Dapur)
                return Mat("floorTiles", Color.white, 0.4f, Tiles(new Color(0.62f, 0.6f, 0.55f), new Color(0.25f, 0.27f, 0.26f)), new Vector2(2f, 2f));
            return Mat("floorWood" + (int)t, Color.white, 0.25f, Planks(t == RoomType.Gudang ? new Color(0.36f, 0.3f, 0.24f) : new Color(0.42f, 0.29f, 0.19f)), new Vector2(1.2f, 1.2f));
        }

        public static string FloorKey(RoomType t)
        {
            switch (t)
            {
                case RoomType.KamarTidur: return "floor_bedroom";
                case RoomType.KamarAnak: return "floor_bedroom";
                case RoomType.KamarMandi: return "floor_bath";
                case RoomType.Dapur: return "floor_tiles_green";
                case RoomType.RuangMakan: return "floor_dining";
                case RoomType.Gudang: return "floor_wood";
                case RoomType.Perpustakaan: return "floor_wood";
                case RoomType.RuangKeluarga: return "floor_tiles_red";
                default: return "floor_wood_light";
            }
        }

        public static string WallKey(RoomType t)
        {
            switch (t)
            {
                case RoomType.KamarTidur: case RoomType.KamarAnak: return "wall_bedroom";
                case RoomType.KamarMandi: return "wall_bath";
                case RoomType.Dapur: return "wall_kitchen";
                case RoomType.RuangMakan: case RoomType.RuangKeluarga: return "wall_dining";
                default: return "wall_corridor";
            }
        }

        /// <summary>Material dinding fallback bila paket tidak ada.</summary>
        public static Material WallFallback(RoomType t)
        {
            Color a, b;
            switch (t)
            {
                case RoomType.KamarTidur: case RoomType.KamarAnak: a = new Color(0.34f, 0.22f, 0.24f); b = new Color(0.45f, 0.33f, 0.3f); break;
                case RoomType.KamarMandi: case RoomType.Dapur: a = new Color(0.5f, 0.5f, 0.46f); b = new Color(0.44f, 0.45f, 0.42f); break;
                case RoomType.Perpustakaan: a = new Color(0.2f, 0.27f, 0.22f); b = new Color(0.32f, 0.33f, 0.25f); break;
                default: a = new Color(0.2f, 0.33f, 0.33f); b = new Color(0.28f, 0.4f, 0.4f); break;
            }
            return Mat("wallFb" + (int)t, Color.white, 0.08f, Wallpaper(a, b), new Vector2(1f, 1f));
        }

        // ------------------------------------------------------------------ tekstur prosedural
        public static Texture2D Planks(Color baseC)
        {
            string key = "planks" + ColorKey(baseC);
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
            string key = "wp" + ColorKey(a) + ColorKey(b);
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 256; t = NewTex(N);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    float stripe = Mathf.Abs(Mathf.Sin(x / (float)N * Mathf.PI * 8f));
                    float u = (x % 32) / 32f - 0.5f, v = (y % 48) / 48f - 0.5f;
                    float motif = Mathf.Clamp01(1f - (Mathf.Abs(u) * 2.2f + Mathf.Abs(v) * 1.6f)) > 0.5f ? 1f : 0f;
                    float stain = Mathf.PerlinNoise(x * 0.015f, y * 0.015f);
                    Color c = Color.Lerp(a, b, stripe > 0.92f ? 1f : motif * 0.5f) * (0.75f + stain * 0.4f);
                    c.a = 1f; t.SetPixel(x, y, c);
                }
            t.Apply(true); tex[key] = t; return t;
        }

        public static Texture2D Tiles(Color a, Color b)
        {
            string key = "tiles" + ColorKey(a) + ColorKey(b);
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
                    float n = 1f - amt + (Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.7f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.3f) * amt * 1.6f;
                    Color c = baseC * n; c.a = 1f; t.SetPixel(x, y, c);
                }
            t.Apply(true); tex[key] = t; return t;
        }

        /// <summary>Gradasi radial (alpha naik ke tepi) untuk vignette/overlay UI.</summary>
        public static Texture2D Radial(string key, float inner, bool invert = false)
        {
            if (tex.TryGetValue(key, out Texture2D t)) return t;
            const int N = 128; t = NewTex(N); t.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                    float a = Mathf.Clamp01((d - inner) / (1f - inner));
                    a = a * a * (3f - 2f * a);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, invert ? 1f - a : a));
                }
            t.Apply(); tex[key] = t; return t;
        }

        /// <summary>Sarang laba-laba (tekstur prosedural, dua sisi, transparan).</summary>
        public static Material Cobweb
        {
            get
            {
                if (cache.TryGetValue("cobweb", out Material m) && m != null) return m;
                const int N = 256;
                Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                Color[] px = new Color[N * N];
                Vector2 o = new Vector2(N * 0.08f, N * 0.08f);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        Vector2 d = new Vector2(x, y) - o;
                        float r = d.magnitude / N, ang = Mathf.Atan2(d.y, d.x);
                        float a = 0f;
                        // jari-jari
                        float spoke = Mathf.Abs(Mathf.Sin(ang * 9f));
                        if (spoke < 0.03f && r < 1.05f) a = 0.75f;
                        // lingkaran spiral bergelombang
                        float ring = Mathf.Abs(Mathf.Sin((r * 26f + Mathf.Sin(ang * 9f) * 0.25f) * Mathf.PI));
                        if (ring < 0.05f && r < 0.95f && ang > -0.2f && ang < 1.8f) a = Mathf.Max(a, 0.55f);
                        a *= Mathf.Clamp01(1.15f - r) * (0.6f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f);
                        px[y * N + x] = new Color(0.85f, 0.85f, 0.82f, a);
                    }
                t.SetPixels(px); t.Apply(true);
                m = ParticleMat("cobweb", new Color(1f, 1f, 1f, 0.7f), t);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                cache["cobweb"] = m;
                return m;
            }
        }

        /// <summary>Partikel bulat lembut.</summary>
        public static Texture2D SoftDot { get { return Radial("softdot", 0f, true); } }

        /// <summary>Cookie senter: terang di tengah, cincin halus, bintik lensa.</summary>
        public static Texture2D FlashCookie
        {
            get
            {
                if (tex.TryGetValue("cookie", out Texture2D t)) return t;
                const int N = 256;
                t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < N; x++)
                    for (int y = 0; y < N; y++)
                    {
                        float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                        // pola senter sungguhan: titik panas terang, korona lembut, cincin reflektor tipis, tepi halus (tanpa bintik)
                        float hot = Mathf.Exp(-(d * d) / (2f * 0.2f * 0.2f));
                        float corona = Mathf.Pow(Mathf.Clamp01(1f - d), 0.8f);
                        float ring = Mathf.Exp(-Mathf.Pow((d - 0.3f) / 0.035f, 2f)) * 0.08f + Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.05f, 2f)) * 0.05f;
                        float e = Mathf.Clamp01((d - 0.85f) / 0.15f); float edge = 1f - e * e * (3f - 2f * e);
                        float v = Mathf.Clamp01((0.55f * hot + 0.48f * corona + ring) * edge);
                        t.SetPixel(x, y, new Color(v, v, v, v));
                    }
                t.Apply(); tex["cookie"] = t; return t;
            }
        }

        public static Texture2D Grass
        {
            get
            {
                if (tex.TryGetValue("grass", out Texture2D t)) return t;
                const int N = 256; t = NewTex(N);
                for (int x = 0; x < N; x++)
                    for (int y = 0; y < N; y++)
                    {
                        float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.4f;
                        Color c = Color.Lerp(new Color(0.05f, 0.07f, 0.04f), new Color(0.12f, 0.15f, 0.08f), n); c.a = 1f;
                        t.SetPixel(x, y, c);
                    }
                t.Apply(true); tex["grass"] = t; return t;
            }
        }

        static Texture2D NewTex(int n)
        {
            return new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        }
    }
}
