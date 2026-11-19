using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HantuBelajar
{
    /// <summary>Kabut, cahaya ambien, langit malam, halaman luar, dan post-processing (URP Volume) yang bereaksi pada bahaya.</summary>
    public class Atmosphere : MonoBehaviour
    {
        public static Atmosphere I;
        Volume volume; VolumeProfile profile;
        Vignette vignette; ColorAdjustments color; ChromaticAberration chroma; Bloom bloom; FilmGrain grain;
        float danger, flash, baseExposure = 0.6f, brightness = 0f;
        GameObject outside;

        void Awake() { I = this; }

        public void Setup()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.06f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.11f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.055f, 0.05f);
            RenderSettings.reflectionIntensity = 0.25f;
            Material sky = null;
            AssetLibrary lib = AssetLibrary.I;
            if (lib != null && lib.skyTexture != null)
            {
                bool cube = lib.skyTexture is Cubemap;
                Shader sh = Shader.Find(cube ? "Skybox/Cubemap" : "Skybox/Panoramic");
                if (sh != null)
                {
                    sky = new Material(sh) { name = "NightSky" };
                    sky.SetTexture(cube ? "_Tex" : "_MainTex", lib.skyTexture);
                    sky.SetFloat("_Exposure", 0.55f);
                    sky.SetColor("_Tint", new Color(0.55f, 0.55f, 0.65f));
                }
            }
            if (sky == null)
            {
                Shader sh = Shader.Find("Skybox/Procedural");
                if (sh != null)
                {
                    sky = new Material(sh) { name = "NightSkyProc" };
                    sky.SetFloat("_SunSize", 0f); sky.SetFloat("_AtmosphereThickness", 0.4f);
                    sky.SetColor("_SkyTint", new Color(0.15f, 0.15f, 0.3f)); sky.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.03f));
                    sky.SetFloat("_Exposure", 0.15f);
                }
            }
            RenderSettings.skybox = sky;
            RenderSettings.sun = null;

            if (Look.URP)
            {
                GameObject vg = new GameObject("PostProcess");
                vg.transform.SetParent(transform, false);
                volume = vg.AddComponent<Volume>();
                volume.isGlobal = true; volume.priority = 10f;
                VolumeProfile src = lib != null ? lib.volumeProfile as VolumeProfile : null;
                profile = src != null ? Instantiate(src) : ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = profile;
                Tonemapping tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
                bloom = Get<Bloom>(); bloom.threshold.Override(1.05f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.68f);
                vignette = Get<Vignette>(); vignette.intensity.Override(0.34f); vignette.smoothness.Override(0.5f); vignette.color.Override(Color.black); vignette.rounded.Override(false);
                color = Get<ColorAdjustments>(); color.postExposure.Override(baseExposure); color.contrast.Override(14f); color.saturation.Override(-18f); color.colorFilter.Override(new Color(0.96f, 0.97f, 1f));
                chroma = Get<ChromaticAberration>(); chroma.intensity.Override(0.05f);
                grain = Get<FilmGrain>(); grain.type.Override(FilmGrainLookup.Medium1); grain.intensity.Override(0.22f); grain.response.Override(0.75f);
                if (profile.TryGet(out LensDistortion ld)) ld.active = false;
                if (profile.TryGet(out DepthOfField dof)) dof.active = false;
                if (profile.TryGet(out MotionBlur mb)) mb.active = false;
            }
        }

        T Get<T>() where T : VolumeComponent
        {
            if (!profile.TryGet(out T c)) c = profile.Add<T>(true);
            c.active = true;
            return c;
        }

        /// <summary>Halaman luar (tanah + pepohonan) agar jendela tidak menghadap kekosongan.</summary>
        public void BuildOutside(HouseLayout L)
        {
            if (outside != null) Destroy(outside);
            outside = new GameObject("Outside");
            float W = L.w * G.T, Hh = L.h * G.T;
            Vector3 c = new Vector3(W * 0.5f, 0f, Hh * 0.5f);
            Material grass = Look.Mat("grassGround", Color.white, 0.05f, Look.Grass, new Vector2(40f, 40f));
            Kit.Box(outside.transform, "Ground", c + Vector3.down * 0.35f, new Vector3(W + 120f, 0.1f, Hh + 120f), grass, true);
            System.Random rng = new System.Random(L.usedSeed);
            Material bark = Look.Mat("bark", new Color(0.05f, 0.045f, 0.04f), 0.05f);
            for (int i = 0; i < 46; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Max(W, Hh) * 0.5f + 7f + (float)rng.NextDouble() * 22f;
                Vector3 p = c + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                float h = 6f + (float)rng.NextDouble() * 7f;
                GameObject trunk = Kit.Box(outside.transform, "Tree", p + Vector3.up * (h * 0.5f - 0.3f), new Vector3(0.45f, h, 0.45f), bark, false, Quaternion.Euler((float)rng.NextDouble() * 8f - 4f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 8f - 4f));
                for (int b = 0; b < 3; b++)
                {
                    float bh = h * (0.5f + 0.15f * b);
                    Kit.Box(trunk.transform.parent, "Branch", p + Vector3.up * bh, new Vector3(0.12f, 0.12f, 2.2f + (float)rng.NextDouble() * 1.5f), bark, false,
                        Quaternion.Euler(-25f - (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, 0f));
                }
            }
        }

        public void SetBrightness(float b) { brightness = b; }
        public void SetDanger(float d) { danger = Mathf.Clamp01(d); }
        public void Flash(float f) { flash = Mathf.Max(flash, f); }

        void Update()
        {
            flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.6f);
            if (vignette == null) return;
            vignette.intensity.value = 0.34f + danger * 0.22f + flash * 0.3f;
            vignette.color.value = Color.Lerp(Color.black, new Color(0.35f, 0f, 0f), Mathf.Max(danger * 0.8f, flash));
            chroma.intensity.value = 0.05f + danger * 0.45f + flash * 0.5f;
            color.saturation.value = -18f - danger * 28f;
            color.postExposure.value = baseExposure + brightness + flash * 1.2f;
            grain.intensity.value = 0.22f + danger * 0.3f;
        }
    }
}
