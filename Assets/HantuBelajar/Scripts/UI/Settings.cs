using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HantuBelajar
{
    /// <summary>Pengaturan pemain (PlayerPrefs).</summary>
    public static class Settings
    {
        public static float Sensitivity = 2f, Master = 0.9f, Music = 0.75f, Sfx = 1f, Fov = 72f, Brightness = 0f;
        public static bool InvertY, HeadBob = true, Fullscreen = true, Hints = true;
        public static int Quality = 2;
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Sensitivity = PlayerPrefs.GetFloat("HB_Sens", 2f);
            Master = PlayerPrefs.GetFloat("HB_Master", 0.9f);
            Music = PlayerPrefs.GetFloat("HB_Music", 0.75f);
            Sfx = PlayerPrefs.GetFloat("HB_Sfx", 1f);
            Fov = PlayerPrefs.GetFloat("HB_Fov", 72f);
            Brightness = PlayerPrefs.GetFloat("HB_Bright", 0f);
            InvertY = PlayerPrefs.GetInt("HB_Invert", 0) == 1;
            HeadBob = PlayerPrefs.GetInt("HB_Bob", 1) == 1;
            Fullscreen = PlayerPrefs.GetInt("HB_Full", 1) == 1;
            Hints = PlayerPrefs.GetInt("HB_Hints", 1) == 1;
            Quality = PlayerPrefs.GetInt("HB_Quality", 2);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("HB_Sens", Sensitivity); PlayerPrefs.SetFloat("HB_Master", Master); PlayerPrefs.SetFloat("HB_Music", Music);
            PlayerPrefs.SetFloat("HB_Sfx", Sfx); PlayerPrefs.SetFloat("HB_Fov", Fov); PlayerPrefs.SetFloat("HB_Bright", Brightness);
            PlayerPrefs.SetInt("HB_Invert", InvertY ? 1 : 0); PlayerPrefs.SetInt("HB_Bob", HeadBob ? 1 : 0); PlayerPrefs.SetInt("HB_Full", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("HB_Hints", Hints ? 1 : 0); PlayerPrefs.SetInt("HB_Quality", Quality);
            PlayerPrefs.Save();
        }

        /// <summary>Terapkan pengaturan tampilan. Aset URP hanya diubah di build (agar file proyek tidak ikut berubah di Editor).</summary>
        public static void Apply()
        {
            if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
            if (Atmosphere.I != null) Atmosphere.I.SetBrightness(Brightness);
            if (GameManager.I != null && GameManager.I.player != null) GameManager.I.player.ApplySettings();
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Quality == 0 ? 0.75f : Quality == 1 ? 0.9f : 1f;
                urp.shadowDistance = Quality == 0 ? 14f : Quality == 1 ? 22f : 30f;
                urp.msaaSampleCount = Quality == 2 ? 2 : 1;
            }
        }

        public static string QualityName(int q) { return q == 0 ? "Rendah" : q == 1 ? "Sedang" : "Tinggi"; }
    }
}
