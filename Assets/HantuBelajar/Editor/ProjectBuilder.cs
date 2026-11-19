using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>
    /// Tools > Hantu Belajar > Bangun Proyek: memetakan aset paket Asset Store ke AssetLibrary (eksplisit per nama),
    /// mengatur import audio (streaming + Vorbis), membuat material penjaga varian shader, dan membuat scene.
    /// Batch: Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.ProjectBuilder.BuildAllBatch -quit
    /// </summary>
    public static class ProjectBuilder
    {
        const string LibPath = "Assets/HantuBelajar/Resources/AssetLibrary.asset";
        const string ScenePath = "Assets/Scenes/HauntedHouse.unity";
        const string Pack = "Assets/_3DStealthGame/";
        const string Env = Pack + "Prefabs/Environment/";
        const string Unnerving = "Assets/Unnerving Ambient Sounds - Horror Game Sound Effect Pack/";
        const string Music = "Assets/Horror_Music_Free_Starter_Kit/";

        [MenuItem("Tools/Hantu Belajar/Bangun Proyek (aset + scene)")]
        public static void BuildAll()
        {
            ConfigureAudio();
            BuildLibrary();
            MakeShaderKeep();
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] selesai");
        }

        public static void BuildAllBatch()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static GameObject[] P(params string[] paths)
        {
            List<GameObject> l = new List<GameObject>();
            foreach (string p in paths)
            {
                if (p.Contains("*"))
                {
                    string dir = Path.GetDirectoryName(p).Replace('\\', '/');
                    string pat = Path.GetFileName(p).Replace("*", "");
                    if (!AssetDatabase.IsValidFolder(dir)) continue;
                    foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
                    {
                        string ap = AssetDatabase.GUIDToAssetPath(g);
                        if (Path.GetFileNameWithoutExtension(ap).StartsWith(pat) && Path.GetDirectoryName(ap).Replace('\\', '/') == dir)
                            l.Add(AssetDatabase.LoadAssetAtPath<GameObject>(ap));
                    }
                    continue;
                }
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p + ".prefab");
                if (go != null) l.Add(go);
                else Debug.LogWarning("[Build] prefab tidak ditemukan: " + p);
            }
            return l.ToArray();
        }

        static AudioClip[] Clips(string folder, params string[] prefixes)
        {
            List<AudioClip> l = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/'))) return l.ToArray();
            foreach (string g in AssetDatabase.FindAssets("t:AudioClip", new[] { folder.TrimEnd('/') }))
            {
                string ap = AssetDatabase.GUIDToAssetPath(g);
                string n = Path.GetFileNameWithoutExtension(ap);
                if (prefixes.Length == 0 || prefixes.Any(x => n.StartsWith(x))) l.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(ap));
            }
            l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return l.ToArray();
        }

        static void BuildLibrary()
        {
            AssetLibrary lib = AssetDatabase.LoadAssetAtPath<AssetLibrary>(LibPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibPath));
                lib = ScriptableObject.CreateInstance<AssetLibrary>();
                AssetDatabase.CreateAsset(lib, LibPath);
            }
            string D = Env + "Decorations/";
            var models = new List<AssetLibrary.ModelEntry>();
            void M(string key, params string[] paths) { models.Add(new AssetLibrary.ModelEntry { key = key, items = P(paths) }); }
            M("wall", Env + "Walls/Wall_Straight_A", Env + "Walls/Wall_Straight_B", Env + "Walls/Wall_Straight_C", Env + "Walls/Wall_Straight_D");
            M("window", Env + "Walls/Wall_Window_A", Env + "Walls/Wall_Window_B");
            M("window3", Env + "Walls/Wall_Window_Tripple");
            M("post", Env + "Walls/Post");
            M("door", D + "Corridors/Door");
            M("key", Pack + "Tutorial_Demo/Demo_Prefabs/Key&Doors/Key");
            M("wardrobe", D + "Bedroom/Wardrobe"); M("bed", D + "Bedroom/Bed"); M("bedside", D + "Bedroom/Bedside_Table");
            M("rug_bed", D + "Bedroom/Rug_Bedroom"); M("rug_bath", D + "Bathroom/Rug_Bathroom"); M("rug_corr", D + "Corridors/Rug_Corridor");
            M("rug_dining", D + "Diningroom/Rug_Diningroom"); M("rug_kitchen", D + "Kitchen/RugKitchen");
            M("bathtub", D + "Bathroom/Bathtub"); M("sink", D + "Bathroom/Sink"); M("mirror", D + "Bathroom/Mirror"); M("radiator", D + "Bathroom/Radiator"); M("pipe", D + "Bathroom/Pipe");
            M("candlestick", D + "Diningroom/Candlestick"); M("chair", D + "Diningroom/Chair_A", D + "Diningroom/Chair_B");
            M("table_long", D + "Diningroom/Table_Long"); M("table_mid", D + "Diningroom/Table_Middle");
            M("armchair", D + "General/ArmChair"); M("clock", D + "General/Clock"); M("fireplace", D + "General/Fireplace"); M("sconce", D + "General/Light");
            M("pictures", D + "General/PictureFrame_*"); M("smalltable", D + "General/SmallTable_A", D + "General/SmallTable_B");
            M("cabinet", D + "Kitchen/Cabinate_A", D + "Kitchen/Cabinate_B", D + "Kitchen/Cabinate_C"); M("cabinet_top", D + "Kitchen/CabinateTop");
            M("fridge", D + "Kitchen/Fridge"); M("stove", D + "Kitchen/KitchenStove"); M("hood", D + "Kitchen/KitchenHood"); M("microwave", D + "Kitchen/Microwave");
            M("shelf", D + "Kitchen/Shelf"); M("sink_kitchen", D + "Kitchen/SinkKitchen");
            M("bottle", D + "Kitchen/Bottle"); M("jar", D + "Kitchen/Jar"); M("pot", D + "Kitchen/Pot"); M("pan", D + "Kitchen/Pan"); M("plate", D + "Kitchen/Plate");
            M("fx_candle", Pack + "Prefabs/VFX/CandleFlame"); M("fx_dust", Pack + "Prefabs/VFX/DustMotesEffect"); M("fx_steam", Pack + "Prefabs/VFX/RisingSteam");
            lib.models = models.ToArray();

            var mats = new List<AssetLibrary.MatEntry>();
            void T(string key, string path) { Material m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) mats.Add(new AssetLibrary.MatEntry { key = key, item = m }); }
            string F = Pack + "Art/Materials/Floors/", W = Pack + "Art/Materials/Wallls/";
            T("floor_wood", F + "Floor_WoodPlanks.mat"); T("floor_wood_light", F + "Floor_WoodPlanks_Light.mat"); T("floor_bedroom", F + "Floor_Bedroom.mat");
            T("floor_bath", F + "Floor_Bathroom.mat"); T("floor_dining", F + "Floor_DinningRoom.mat"); T("floor_tiles_green", F + "Floor_TIlesGreen.mat"); T("floor_tiles_red", F + "Floor_TilesRedish.mat");
            T("wall_corridor", W + "Wall_Corridor.mat"); T("wall_bedroom", W + "Wall_Bedroom.mat"); T("wall_kitchen", W + "Wall_Kitchen.mat");
            T("wall_bath", W + "Wall_Bathroom.mat"); T("wall_dining", W + "Wall_Diningroom.mat");
            lib.materials = mats.ToArray();

            var clips = new List<AssetLibrary.ClipEntry>();
            void C(string key, AudioClip[] items) { clips.Add(new AssetLibrary.ClipEntry { key = key, items = items }); }
            AudioClip A(string path) { return AssetDatabase.LoadAssetAtPath<AudioClip>(path); }
            string SA = Pack + "Audio/";
            C("amb_house", new[] { A(SA + "SFXHouseAmbience.wav") });
            C("ghost_move", new[] { A(SA + "SFXGhostMove.wav") });
            C("footsteps", new[] { A(SA + "SFXFootstepsLooping.wav") });
            C("lose", new[] { A(SA + "SFXLose.wav") }); C("win", new[] { A(SA + "SFXWin.wav") });
            C("buzz", new[] { A(SA + "SFXBuzzingLight.wav") }); C("candle", new[] { A(SA + "SFXCandle.wav") });
            C("amb_bed", Clips(Unnerving, "Alone at Twilight", "Exploring Nightmare", "Paranormal Phenomena", "The Unknown"));
            C("ghost_near", Clips(Unnerving, "Close Encounter", "The Creature"));
            C("music_explore", Clips(Music, "WAV_AMBIENCE_LOOP", "WAV_ATMOSPHERIC_LOOP"));
            C("music_chase", Clips(Music, "WAV_HIGH_TENSION_LOOP", "WAV_LIGHT_TENSION_LOOP_Run"));
            C("music_menu", Clips(Music, "WAV_MENU_FULL"));
            lib.clips = clips.ToArray();

            lib.volumeProfile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Pack + "Art/PostProcessing/Scary Volume Profile.asset");
            lib.skyTexture = AssetDatabase.LoadAssetAtPath<Texture>(Pack + "Art/Textures/Sky/SkyNight.png");
            lib.titleFont = AssetDatabase.LoadAssetAtPath<Font>(Pack + "Art/Fonts/IrishGrover-Regular.ttf");
            lib.bodyFont = null;

            StringBuilder rep = new StringBuilder();
            foreach (var e in lib.models) rep.AppendLine("model " + e.key + ": " + (e.items != null ? e.items.Length : 0));
            foreach (var e in lib.materials) rep.AppendLine("mat " + e.key + ": " + (e.item != null ? e.item.name : "-"));
            foreach (var e in lib.clips) rep.AppendLine("audio " + e.key + ": " + (e.items != null ? e.items.Count(x => x != null) : 0));
            rep.AppendLine("volume " + (lib.volumeProfile != null) + ", sky " + (lib.skyTexture != null) + ", font " + (lib.titleFont != null));
            lib.report = rep.ToString();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/asset_library.txt", rep.ToString());
            Debug.Log("[Build] AssetLibrary:\n" + rep);
        }

        /// <summary>Klip panjang di-stream (Vorbis) agar memori & ukuran build kecil.</summary>
        static void ConfigureAudio()
        {
            int changed = 0;
            foreach (string folder in new[] { Unnerving.TrimEnd('/'), Music.TrimEnd('/'), Pack + "Audio" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string g in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    string ap = AssetDatabase.GUIDToAssetPath(g);
                    AudioImporter imp = AssetImporter.GetAtPath(ap) as AudioImporter;
                    if (imp == null) continue;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ap);
                    bool longClip = clip != null && clip.length > 20f;
                    AudioImporterSampleSettings s = imp.defaultSampleSettings;
                    AudioClipLoadType lt = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
                    float q = longClip ? 0.45f : 0.7f;
                    if (s.loadType == lt && s.compressionFormat == AudioCompressionFormat.Vorbis && Mathf.Abs(s.quality - q) < 0.01f) continue;
                    s.loadType = lt; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = q;
                    s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    imp.defaultSampleSettings = s;
                    imp.forceToMono = false;
                    imp.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log("[Build] audio diatur ulang: " + changed);
        }

        /// <summary>Material penjaga agar varian shader yang dibuat lewat kode ikut ke build.</summary>
        static void MakeShaderKeep()
        {
            string dir = "Assets/HantuBelajar/Resources/ShaderKeep";
            Directory.CreateDirectory(dir);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader part = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            void Keep(string name, Shader sh, System.Action<Material> setup)
            {
                if (sh == null) return;
                string path = dir + "/" + name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
                m.shader = sh;
                setup?.Invoke(m);
                EditorUtility.SetDirty(m);
            }
            Keep("LitOpaque", lit, m => { });
            Keep("LitTex", lit, m => { m.mainTexture = Texture2D.whiteTexture; });
            Keep("LitEmission", lit, m => { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
            Keep("LitFade", lit, m => { HantuBelajar.Look.MakeFade(m); });
            Keep("LitFadeEmission", lit, m => { HantuBelajar.Look.MakeFade(m); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); });
            Keep("Unlit", unlit, m => { });
            Keep("ParticleFade", part, m =>
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            });
            Shader pano = Shader.Find("Skybox/Panoramic");
            Keep("SkyPanoramic", pano, m => { });
            Shader proc = Shader.Find("Skybox/Procedural");
            Keep("SkyProcedural", proc, m => { });
            Shader cubeSky = Shader.Find("Skybox/Cubemap");
            Keep("SkyCubemap", cubeSky, m => { });
        }

        static void MakeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject b = new GameObject("Bootstrap");
            b.AddComponent<GameManager>();
            // kabut disimpan di scene agar varian shader kabut tidak dibuang saat build (stripping "Automatic")
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.06f);
            // dan paksa semua mode kabut dipertahankan
            Object[] gsa = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gsa != null && gsa.Length > 0)
            {
                SerializedObject so = new SerializedObject(gsa[0]);
                SerializedProperty strip = so.FindProperty("m_FogStripping");
                if (strip != null)
                {
                    strip.intValue = 1;   // Custom
                    foreach (string k in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" }) { SerializedProperty sp = so.FindProperty(k); if (sp != null) sp.boolValue = true; }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Haunted House - Hantu yang Mempelajari Ketakutanmu";
            PlayerSettings.companyName = "Kelompok EAS";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
        }
    }
}
