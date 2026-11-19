using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Ikon & bentuk UI prosedural (putih, diwarnai lewat Image.color), digambar dengan signed distance (tepi halus).
    /// </summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const int N = 96;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out Sprite s) && s != null) return s;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);   // -1..1
                    float a = Shape(name, p);
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            t.SetPixels(px); t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            cache[name] = s;
            return s;
        }

        // cakupan piksel dari jarak bertanda (negatif = di dalam)
        static float Fill(float sd) { return Mathf.Clamp01(0.5f - sd * N * 0.5f); }
        static float Circle(Vector2 p, Vector2 c, float r) { return (p - c).magnitude - r; }
        static float Box(Vector2 p, Vector2 c, Vector2 h, float r = 0f)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + new Vector2(r, r);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
        }
        static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }
        static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // jarak ke segitiga (kira-kira, cukup untuk ikon)
            float d = Mathf.Min(Mathf.Min(Seg(p, a, b, 0f), Seg(p, b, c, 0f)), Seg(p, c, a, 0f));
            bool inside = Side(p, a, b) >= 0 == (Side(p, b, c) >= 0) && (Side(p, b, c) >= 0) == (Side(p, c, a) >= 0);
            return inside ? -d : d;
        }
        static float Side(Vector2 p, Vector2 a, Vector2 b) { return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x); }
        static float Arc(Vector2 p, Vector2 c, float r, float th, float halfAngle)
        {
            Vector2 d = p - c;
            if (Mathf.Abs(Mathf.Atan2(d.y, d.x)) <= halfAngle) return Mathf.Abs(d.magnitude - r) - th;
            Vector2 e1 = c + new Vector2(Mathf.Cos(halfAngle), Mathf.Sin(halfAngle)) * r, e2 = c + new Vector2(Mathf.Cos(halfAngle), -Mathf.Sin(halfAngle)) * r;
            return Mathf.Min((p - e1).magnitude, (p - e2).magnitude) - th;
        }
        static float Union(float a, float b) { return Mathf.Min(a, b); }
        static float Sub(float a, float b) { return Mathf.Max(a, -b); }

        static float Shape(string name, Vector2 p)
        {
            switch (name)
            {
                case "dot": return Fill(Circle(p, Vector2.zero, 0.8f));
                case "ring": return Fill(Mathf.Abs(Circle(p, Vector2.zero, 0.72f)) - 0.12f);
                case "soft": { float d = p.magnitude; return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); }
                case "relic":
                    {
                        // permata: belah ketupat dengan faset
                        float d = (Mathf.Abs(p.x) / 0.62f + Mathf.Abs(p.y + 0.02f) / 0.86f) - 1f;
                        float a = Fill(d * 0.6f);
                        float facet = Fill(Mathf.Abs(p.y - 0.18f) - 0.035f) * (Mathf.Abs(p.x) < 0.5f ? 1f : 0f);
                        return a * (1f - facet * 0.55f);
                    }
                case "key":
                    {
                        float bow = Mathf.Abs(Circle(p, new Vector2(0f, 0.45f), 0.3f)) - 0.1f;
                        float shaft = Seg(p, new Vector2(0f, 0.15f), new Vector2(0f, -0.85f), 0.09f);
                        float t1 = Box(p, new Vector2(0.17f, -0.72f), new Vector2(0.15f, 0.07f));
                        float t2 = Box(p, new Vector2(0.14f, -0.48f), new Vector2(0.12f, 0.06f));
                        return Fill(Union(Union(bow, shaft), Union(t1, t2)));
                    }
                case "battery":
                    {
                        float outer = Box(p, new Vector2(0f, -0.08f), new Vector2(0.42f, 0.78f), 0.12f);
                        float inner = Box(p, new Vector2(0f, -0.08f), new Vector2(0.29f, 0.65f), 0.06f);
                        float cap = Box(p, new Vector2(0f, 0.8f), new Vector2(0.18f, 0.1f), 0.04f);
                        float charge = Box(p, new Vector2(0f, -0.3f), new Vector2(0.2f, 0.36f), 0.03f);
                        return Fill(Union(Union(Sub(outer, inner), cap), charge));
                    }
                case "exit":
                    {
                        float frame = Sub(Box(p, new Vector2(-0.12f, 0f), new Vector2(0.5f, 0.86f), 0.06f), Box(p, new Vector2(-0.12f, 0f), new Vector2(0.38f, 0.74f)));
                        float door = Tri(p, new Vector2(-0.5f, 0.74f), new Vector2(0.18f, 0.55f), new Vector2(-0.5f, -0.74f));
                        float door2 = Tri(p, new Vector2(0.18f, 0.55f), new Vector2(0.18f, -0.62f), new Vector2(-0.5f, -0.74f));
                        float arrow = Union(Seg(p, new Vector2(0.35f, 0f), new Vector2(0.92f, 0f), 0.07f), Tri(p, new Vector2(0.98f, 0f), new Vector2(0.7f, 0.24f), new Vector2(0.7f, -0.24f)));
                        return Fill(Union(Union(frame, Union(door, door2)), arrow));
                    }
                case "lock":
                    {
                        float shackle = Sub(Mathf.Abs(Circle(p, new Vector2(0f, 0.25f), 0.38f)) - 0.1f, Box(p, new Vector2(0f, -0.3f), new Vector2(1f, 0.55f)));
                        float legs = Union(Box(p, new Vector2(-0.38f, 0.12f), new Vector2(0.1f, 0.16f)), Box(p, new Vector2(0.38f, 0.12f), new Vector2(0.1f, 0.16f)));
                        float body = Box(p, new Vector2(0f, -0.35f), new Vector2(0.62f, 0.45f), 0.1f);
                        float hole = Union(Circle(p, new Vector2(0f, -0.28f), 0.1f), Box(p, new Vector2(0f, -0.48f), new Vector2(0.045f, 0.14f)));
                        return Fill(Sub(Union(Union(shackle, legs), body), hole));
                    }
                case "stairs":
                    {
                        float d = 1f;
                        for (int i = 0; i < 4; i++) d = Union(d, Box(p, new Vector2(-0.6f + i * 0.4f, -0.8f + (i + 1) * 0.2f), new Vector2(0.2f, (i + 1) * 0.2f)));
                        return Fill(d);
                    }
                case "ghost":
                    {
                        float head = Circle(p, new Vector2(0f, 0.2f), 0.6f);
                        float body = Box(p, new Vector2(0f, -0.3f), new Vector2(0.6f, 0.5f));
                        float waves = 1f;
                        for (int i = 0; i < 3; i++) waves = Union(waves, Circle(p, new Vector2(-0.4f + i * 0.4f, -0.8f), 0.2f));
                        float shape = Union(head, Sub(body, waves));
                        float eyes = Union(Box(p, new Vector2(-0.22f, 0.22f), new Vector2(0.1f, 0.14f), 0.03f), Box(p, new Vector2(0.22f, 0.22f), new Vector2(0.1f, 0.14f), 0.03f));
                        return Fill(Sub(shape, eyes));
                    }
                case "eye":
                    {
                        float outer = Union(Circle(p, new Vector2(0f, -0.62f), 0.95f), 0f);
                        outer = Mathf.Max(Circle(p, new Vector2(0f, -0.62f), 0.95f), Circle(p, new Vector2(0f, 0.62f), 0.95f));
                        float iris = Circle(p, Vector2.zero, 0.22f);
                        return Fill(Union(Sub(outer, Circle(p, Vector2.zero, 0.34f)), iris));
                    }
                case "hide":
                    {
                        float body = Sub(Box(p, Vector2.zero, new Vector2(0.62f, 0.86f), 0.08f), Box(p, Vector2.zero, new Vector2(0.5f, 0.74f)));
                        float split = Box(p, Vector2.zero, new Vector2(0.04f, 0.74f));
                        float knobs = Union(Circle(p, new Vector2(-0.12f, 0f), 0.06f), Circle(p, new Vector2(0.12f, 0f), 0.06f));
                        return Fill(Union(Union(body, split), knobs));
                    }
                case "arrow": return Fill(Tri(p, new Vector2(0f, 0.9f), new Vector2(0.7f, -0.6f), new Vector2(-0.7f, -0.6f)) - 0.02f);
                case "player":
                    {
                        // panah arah pemain (lancip di depan)
                        float t = Tri(p, new Vector2(0f, 0.92f), new Vector2(0.62f, -0.7f), new Vector2(-0.62f, -0.7f));
                        float notch = Tri(p, new Vector2(0f, -0.3f), new Vector2(0.7f, -0.95f), new Vector2(-0.7f, -0.95f));
                        return Fill(Sub(t, notch));
                    }
                case "check":
                    return Fill(Union(Seg(p, new Vector2(-0.62f, 0.02f), new Vector2(-0.18f, -0.45f), 0.13f), Seg(p, new Vector2(-0.18f, -0.45f), new Vector2(0.66f, 0.55f), 0.13f)));
                case "box": return Fill(Mathf.Abs(Box(p, Vector2.zero, new Vector2(0.72f, 0.72f), 0.16f)) - 0.08f);
                case "flash":
                    {
                        float body = Box(p, new Vector2(-0.35f, 0f), new Vector2(0.45f, 0.18f), 0.05f);
                        float head = Tri(p, new Vector2(0.1f, 0.18f), new Vector2(0.45f, 0.4f), new Vector2(0.45f, -0.4f));
                        head = Union(head, Tri(p, new Vector2(0.1f, -0.18f), new Vector2(0.1f, 0.18f), new Vector2(0.45f, -0.4f)));
                        float beam = Mathf.Abs(p.y) < (p.x - 0.5f) * 0.6f + 0.18f && p.x > 0.52f ? -1f : 1f;
                        return Mathf.Max(Fill(Union(body, head)), beam < 0 ? 0.35f * Mathf.Clamp01(1f - (p.x - 0.5f) * 2f) : 0f);
                    }
                case "disc": return Fill(Circle(p, Vector2.zero, 0.97f));
                case "ringthin": return Fill(Mathf.Abs(Circle(p, Vector2.zero, 0.94f)) - 0.035f);
                case "sound":
                    {
                        float body = Box(p, new Vector2(-0.6f, 0f), new Vector2(0.16f, 0.22f), 0.03f);
                        float cone = Union(Tri(p, new Vector2(-0.46f, 0.22f), new Vector2(-0.1f, 0.56f), new Vector2(-0.1f, -0.56f)), Tri(p, new Vector2(-0.46f, 0.22f), new Vector2(-0.1f, -0.56f), new Vector2(-0.46f, -0.22f)));
                        float w1 = Arc(p, new Vector2(-0.1f, 0f), 0.36f, 0.07f, 0.85f);
                        float w2 = Arc(p, new Vector2(-0.1f, 0f), 0.68f, 0.07f, 0.85f);
                        return Fill(Union(Union(body, cone), Union(w1, w2)));
                    }
                case "map":
                    {
                        float m1 = Box(p, new Vector2(-0.55f, 0.05f), new Vector2(0.24f, 0.62f), 0.03f);
                        float m2 = Box(p, new Vector2(0f, -0.05f), new Vector2(0.24f, 0.62f), 0.03f);
                        float m3 = Box(p, new Vector2(0.55f, 0.05f), new Vector2(0.24f, 0.62f), 0.03f);
                        float route = Seg(p, new Vector2(-0.6f, -0.3f), new Vector2(0.55f, 0.3f), 0.05f);
                        return Fill(Sub(Union(Union(m1, m2), m3), route));
                    }
                case "eyeoff":
                    {
                        float outer = Mathf.Max(Circle(p, new Vector2(0f, -0.62f), 0.95f), Circle(p, new Vector2(0f, 0.62f), 0.95f));
                        float eye = Union(Sub(outer, Circle(p, Vector2.zero, 0.34f)), Circle(p, Vector2.zero, 0.22f));
                        float cut = Seg(p, new Vector2(-0.78f, -0.78f), new Vector2(0.78f, 0.78f), 0.17f);
                        float slash = Seg(p, new Vector2(-0.78f, -0.78f), new Vector2(0.78f, 0.78f), 0.07f);
                        return Fill(Union(Sub(eye, cut), slash));
                    }
                case "clock":
                    return Fill(Union(Mathf.Abs(Circle(p, Vector2.zero, 0.76f)) - 0.09f, Union(Seg(p, Vector2.zero, new Vector2(0f, 0.46f), 0.08f), Seg(p, Vector2.zero, new Vector2(0.32f, -0.16f), 0.08f))));
                case "skull":
                    {
                        float head = Union(Circle(p, new Vector2(0f, 0.14f), 0.66f), Box(p, new Vector2(0f, -0.46f), new Vector2(0.36f, 0.24f), 0.1f));
                        float eyes = Union(Circle(p, new Vector2(-0.25f, 0.1f), 0.17f), Circle(p, new Vector2(0.25f, 0.1f), 0.17f));
                        float nose = Tri(p, new Vector2(0f, -0.1f), new Vector2(-0.08f, -0.27f), new Vector2(0.08f, -0.27f));
                        float teeth = Union(Box(p, new Vector2(-0.12f, -0.6f), new Vector2(0.025f, 0.12f)), Box(p, new Vector2(0.12f, -0.6f), new Vector2(0.025f, 0.12f)));
                        return Fill(Sub(Sub(Sub(head, eyes), nose), teeth));
                    }
                case "info":
                    return Fill(Union(Mathf.Abs(Circle(p, Vector2.zero, 0.8f)) - 0.08f, Union(Box(p, new Vector2(0f, -0.14f), new Vector2(0.08f, 0.3f), 0.03f), Circle(p, new Vector2(0f, 0.38f), 0.1f))));
                case "chevron":
                    return Fill(Union(Seg(p, new Vector2(-0.25f, 0.55f), new Vector2(0.28f, 0f), 0.12f), Seg(p, new Vector2(0.28f, 0f), new Vector2(-0.25f, -0.55f), 0.12f)));
                case "mouse":
                case "mouseL":
                case "mouseR":
                    {
                        float bodyO = Box(p, Vector2.zero, new Vector2(0.5f, 0.8f), 0.46f);
                        float d = Union(Mathf.Abs(bodyO) - 0.06f, Union(Seg(p, new Vector2(0f, 0.8f), new Vector2(0f, 0.22f), 0.05f), Seg(p, new Vector2(-0.5f, 0.22f), new Vector2(0.5f, 0.22f), 0.05f)));
                        if (name != "mouse")
                        {
                            float side = name == "mouseL" ? Box(p, new Vector2(-0.25f, 0.52f), new Vector2(0.25f, 0.3f)) : Box(p, new Vector2(0.25f, 0.52f), new Vector2(0.25f, 0.3f));
                            d = Union(d, Mathf.Max(bodyO, side));
                        }
                        return Fill(d);
                    }
                default: return Fill(Circle(p, Vector2.zero, 0.8f));
            }
        }

        /// <summary>Persegi panjang membulat 9-slice (untuk panel, tombol, keycap).</summary>
        public static Sprite Rounded(int radius = 12, int border = 0)
        {
            string key = "rr" + radius + "_" + border;
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            int size = radius * 2 + 4;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
                    Vector2 h = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);
                    Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - h + new Vector2(radius, radius);
                    float sd = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - sd);
                    if (border > 0) a *= Mathf.Clamp01(sd + border + 0.5f);   // hanya garis tepi
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            cache[key] = s;
            return s;
        }

        /// <summary>Bayangan lembut 9-slice untuk kartu.</summary>
        public static Sprite Shadow()
        {
            const string key = "shadow";
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            const int size = 64, inner = 22;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(inner - x, x - (size - 1 - inner)));
                    float dy = Mathf.Max(0f, Mathf.Max(inner - y, y - (size - 1 - inner)));
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / inner);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(inner + 1, inner + 1, inner + 1, inner + 1));
            cache[key] = s;
            return s;
        }

        /// <summary>Gradasi horizontal / vertikal (alpha) untuk latar menu dan bayangan.</summary>
        public static Sprite Gradient(bool horizontal, float a0, float a1, string key)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            int w = horizontal ? 256 : 4, h = horizontal ? 4 : 256;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = horizontal ? x / (float)(w - 1) : y / (float)(h - 1);
                    float a = Mathf.Lerp(a0, a1, u * u * (3f - 2f * u));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            cache[key] = s;
            return s;
        }
    }
}
