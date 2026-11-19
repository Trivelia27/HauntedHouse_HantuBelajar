using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Referensi aset paket Asset Store (diisi otomatis oleh Tools > Hantu Belajar > Bangun Proyek).
    /// Setiap entri punya kunci; bila paket belum di-import, entri kosong dan game memakai model/suara buatan sendiri.
    /// </summary>
    public class AssetLibrary : ScriptableObject
    {
        [System.Serializable] public class ModelEntry { public string key; public GameObject[] items; }
        [System.Serializable] public class ClipEntry { public string key; public AudioClip[] items; }
        [System.Serializable] public class MatEntry { public string key; public Material item; }

        public ModelEntry[] models = new ModelEntry[0];
        public ClipEntry[] clips = new ClipEntry[0];
        public MatEntry[] materials = new MatEntry[0];
        public ScriptableObject volumeProfile;          // UnityEngine.Rendering.VolumeProfile "Scary Volume Profile"
        public Texture skyTexture;                      // SkyNight.png (2D atau cubemap)
        public Font titleFont, bodyFont;
        [TextArea(3, 30)] public string report;

        static AssetLibrary inst; static bool loaded;
        Dictionary<string, GameObject[]> mm; Dictionary<string, AudioClip[]> cm; Dictionary<string, Material> tm;

        public static AssetLibrary I
        {
            get
            {
                if (!loaded) { inst = Resources.Load<AssetLibrary>("AssetLibrary"); loaded = true; }
                return inst;
            }
        }

        void Build()
        {
            if (mm != null) return;
            mm = new Dictionary<string, GameObject[]>(); cm = new Dictionary<string, AudioClip[]>(); tm = new Dictionary<string, Material>();
            foreach (ModelEntry e in models) if (e != null && e.key != null) mm[e.key] = Clean(e.items);
            foreach (ClipEntry e in clips) if (e != null && e.key != null) cm[e.key] = Clean(e.items);
            foreach (MatEntry e in materials) if (e != null && e.key != null && e.item != null) tm[e.key] = e.item;
        }

        static T[] Clean<T>(T[] arr) where T : Object
        {
            List<T> l = new List<T>();
            if (arr != null) foreach (T t in arr) if (t != null) l.Add(t);
            return l.ToArray();
        }

        public static GameObject[] Models(string key)
        {
            if (I == null) return new GameObject[0];
            I.Build();
            return I.mm.TryGetValue(key, out GameObject[] a) ? a : new GameObject[0];
        }
        public static GameObject Model(string key, System.Random r = null)
        {
            GameObject[] a = Models(key);
            if (a.Length == 0) return null;
            return a[r != null ? r.Next(a.Length) : 0];
        }
        public static AudioClip[] Clips(string key)
        {
            if (I == null) return new AudioClip[0];
            I.Build();
            return I.cm.TryGetValue(key, out AudioClip[] a) ? a : new AudioClip[0];
        }
        public static AudioClip Clip(string key, System.Random r = null)
        {
            AudioClip[] a = Clips(key);
            if (a.Length == 0) return null;
            return a[r != null ? r.Next(a.Length) : Random.Range(0, a.Length)];
        }
        public static Material Mat(string key)
        {
            if (I == null) return null;
            I.Build();
            return I.tm.TryGetValue(key, out Material m) ? m : null;
        }
        public static bool HasPack { get { return Models("wall").Length > 0; } }
    }
}
