using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    public static class ProjectBuilder
    {
        const string LibPath = "Assets/HantuBelajar/Resources/AssetLibrary.asset";
        const string ScenePath = "Assets/Scenes/HauntedHouse.unity";

        [MenuItem("Tools/Hantu Belajar/1. Import Paket Asset Store yang sudah di-download")]
        public static void ImportDownloaded()
        {
            string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Unity", "Asset Store-5.x");
            if (!Directory.Exists(dir)) { Debug.LogWarning("Folder cache Asset Store tidak ada: " + dir); return; }
            string[] keys = { "Haunted", "Unnerving", "Horror Music" };
            foreach (string f in Directory.GetFiles(dir, "*.unitypackage", SearchOption.AllDirectories))
            {
                if (!keys.Any(k => Path.GetFileName(f).IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                Debug.Log("[Import] " + f);
                AssetDatabase.ImportPackage(f, false);
            }
        }

        [MenuItem("Tools/Hantu Belajar/2. Pindai Aset + Bangun Scene")]
        public static void BuildAll()
        {
            ScanAssets();
            MakeScene();
        }

        public static void BuildAllBatch()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static bool Has(string n, params string[] k) { foreach (string s in k) if (n.Contains(s)) return true; return false; }

        public static void ScanAssets()
        {
            AssetLibrary lib = AssetDatabase.LoadAssetAtPath<AssetLibrary>(LibPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<AssetLibrary>(); Directory.CreateDirectory(Path.GetDirectoryName(LibPath)); AssetDatabase.CreateAsset(lib, LibPath); }
            var cat = new Dictionary<string, List<GameObject>>();
            string[] names = { "wardrobe", "bed", "table", "chair", "shelf", "crate", "bath", "piano", "lamp", "relic", "decor" };
            foreach (string n in names) cat[n] = new List<GameObject>();
            HashSet<string> seen = new HashSet<string>();
            StringBuilder rep = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/HantuBelajar") || !path.StartsWith("Assets/")) continue;
                string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (Has(n, "john", "lemon", "gargoyle", "ghost", "level", "wall", "floor", "ceiling", "character", "player", "enemy", "camera", "light", "door", "window", "stair", "corridor", "room", "_lod", "collider")) continue;
                if (seen.Contains(n)) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                string c = null;
                if (Has(n, "wardrobe", "closet", "cupboard", "cabinet", "armoire", "dresser")) c = "wardrobe";
                else if (Has(n, "bed")) c = "bed";
                else if (Has(n, "table", "desk")) c = "table";
                else if (Has(n, "chair", "bench", "stool", "sofa", "couch", "armchair")) c = "chair";
                else if (Has(n, "shelf", "shelves", "bookcase", "book")) c = "shelf";
                else if (Has(n, "crate", "box", "barrel", "chest", "trunk")) c = "crate";
                else if (Has(n, "bath", "toilet", "sink", "tub")) c = "bath";
                else if (Has(n, "piano", "organ")) c = "piano";
                else if (Has(n, "lamp", "candle", "chandelier", "lantern")) c = "lamp";
                else if (Has(n, "skull", "vase", "clock", "doll", "teddy", "frame", "picture", "painting", "pot", "plant", "statue")) c = Has(n, "skull", "doll", "teddy", "vase", "clock") ? "relic" : "decor";
                if (c == null) continue;
                seen.Add(n);
                cat[c].Add(go);
                rep.AppendLine(c + " <- " + path);
            }
            lib.wardrobe = cat["wardrobe"].ToArray(); lib.bed = cat["bed"].ToArray(); lib.table = cat["table"].ToArray(); lib.chair = cat["chair"].ToArray();
            lib.shelf = cat["shelf"].ToArray(); lib.crate = cat["crate"].ToArray(); lib.bath = cat["bath"].ToArray(); lib.piano = cat["piano"].ToArray();
            lib.lamp = cat["lamp"].ToArray(); lib.relic = cat["relic"].ToArray(); lib.decor = cat["decor"].ToArray();

            var au = new Dictionary<string, List<AudioClip>>();
            foreach (string n in new[] { "ambient", "music", "chase", "scare", "footstep", "door", "whisper", "heartbeat", "pickup" }) au[n] = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/HantuBelajar") || !path.StartsWith("Assets/")) continue;
                AudioClip a = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (a == null) continue;
                string n = path.ToLowerInvariant();
                string fn = Path.GetFileNameWithoutExtension(n);
                string c;
                if (Has(fn, "step", "foot")) c = "footstep";
                else if (Has(fn, "heart")) c = "heartbeat";
                else if (Has(fn, "door", "creak")) c = "door";
                else if (Has(fn, "whisper", "breath", "moan", "laugh", "voice", "ghost")) c = "whisper";
                else if (Has(fn, "pickup", "collect", "win", "item")) c = "pickup";
                else if (Has(fn, "chase", "pursuit", "tension", "run", "escape")) c = "chase";
                else if (Has(fn, "scream", "sting", "jump", "scare", "shock", "hit", "slam")) c = "scare";
                else if (Has(n, "music") || a.length > 60f) c = "music";
                else if (a.length > 12f) c = "ambient";
                else c = "scare";
                au[c].Add(a);
                rep.AppendLine("audio " + c + " <- " + path + " (" + a.length.ToString("0") + "s)");
            }
            // musik panjang juga cocok sebagai ambient bila ambient kosong
            if (au["ambient"].Count == 0) au["ambient"].AddRange(au["music"]);
            lib.ambient = au["ambient"].ToArray(); lib.music = au["music"].ToArray(); lib.chase = au["chase"].ToArray(); lib.scare = au["scare"].ToArray();
            lib.footstep = au["footstep"].ToArray(); lib.door = au["door"].ToArray(); lib.whisper = au["whisper"].ToArray(); lib.heartbeat = au["heartbeat"].ToArray(); lib.pickup = au["pickup"].ToArray();
            // material lantai/dinding/karpet dari paket Haunted House
            Material M(string name)
            {
                foreach (string g in AssetDatabase.FindAssets(name + " t:Material"))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
                }
                return null;
            }
            string[] fl = { "Floor_WoodPlanks_Light", "Floor_Bedroom", "Floor_TIlesGreen", "Floor_Bathroom", "Floor_WoodPlanks", "Floor_DinningRoom", "Floor_Bedroom", "Floor_WoodPlanks", "Floor_TilesRedish" };
            string[] wl = { "Wall_Corridor", "Wall_Bedroom", "Wall_Kitchen", "Wall_Bathroom", "Wall_Diningroom", "Wall_Diningroom", "Wall_Bedroom", "Wall_Corridor", "Wall_Corridor" };
            lib.floorByType = new Material[9]; lib.wallByType = new Material[9];
            for (int i = 0; i < 9; i++) { lib.floorByType[i] = M(fl[i]); lib.wallByType[i] = M(wl[i]); rep.AppendLine("mat " + (RoomType)i + " lantai=" + (lib.floorByType[i] ? lib.floorByType[i].name : "-") + " dinding=" + (lib.wallByType[i] ? lib.wallByType[i].name : "-")); }
            lib.rugs = new[] { M("Rugs_BluePurple"), M("Rugs_GreenYellow"), M("Rugs_RedBlue") }.Where(x => x != null).ToArray();
            lib.report = rep.ToString();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestOutput");
            File.WriteAllText("TestOutput/asset_scan.txt", rep.ToString());
            Debug.Log("[Scan] model+audio dipetakan:\n" + rep);
        }

        static void MakeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject b = new GameObject("Bootstrap");
            b.AddComponent<GameManager>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = "Haunted House - Hantu yang Mempelajari Ketakutanmu";
            AssetDatabase.SaveAssets();
        }
    }
}
