using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Daftar aset pihak ketiga (Asset Store) yang dipindai otomatis oleh editor (Tools > Hantu Belajar > Pindai Aset).
    /// Bila suatu kategori kosong, game memakai model/suara buatan sendiri (fallback), jadi game selalu bisa jalan.
    /// </summary>
    public class AssetLibrary : ScriptableObject
    {
        [Header("Model (Stealth Game - Haunted House 3D)")]
        public GameObject[] wardrobe, bed, table, chair, shelf, crate, bath, piano, lamp, relic, decor;
        public Material floorMat, wallMat;
        public Material[] floorByType = new Material[9], wallByType = new Material[9], rugs;
        [Header("Audio (Unnerving Ambient + Horror Music Starter Kit)")]
        public AudioClip[] ambient, music, chase, scare, footstep, door, whisper, heartbeat, pickup;
        [TextArea(4, 20)] public string report;

        static AssetLibrary inst; static bool loaded;
        public static AssetLibrary I
        {
            get
            {
                if (!loaded) { inst = Resources.Load<AssetLibrary>("AssetLibrary"); loaded = true; }
                return inst;
            }
        }

        public static T Pick<T>(T[] arr, System.Random r) where T : Object
        {
            if (arr == null || arr.Length == 0) return null;
            int n = 0;
            foreach (T t in arr) if (t != null) n++;
            if (n == 0) return null;
            int k = r.Next(n);
            foreach (T t in arr) if (t != null && k-- == 0) return t;
            return null;
        }
        public static AudioClip Clip(System.Func<AssetLibrary, AudioClip[]> sel, System.Random r)
        {
            return I != null ? Pick(sel(I), r) : null;
        }
        public static GameObject Model(System.Func<AssetLibrary, GameObject[]> sel, System.Random r)
        {
            return I != null ? Pick(sel(I), r) : null;
        }

        /// <summary>Instansiasi model lalu skalakan agar pas dalam kotak size (meter), alas di y=0, pusat di (0,0).</summary>
        public static GameObject Fit(GameObject prefab, Transform parent, Vector3 size)
        {
            GameObject g = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            g.name = prefab.name;
            foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (Animator an in g.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (AudioSource a in g.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            GameObject wrap = new GameObject(prefab.name + "_fit");
            Bounds b;
            if (Measure(g, out b))
            {
                if ((b.size.x > b.size.z) != (size.x > size.z) && Mathf.Abs(size.x - size.z) > 0.2f)
                {
                    g.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    Measure(g, out b);
                }
                float k = Mathf.Min(size.x / Mathf.Max(b.size.x, 1e-3f), Mathf.Min(size.y / Mathf.Max(b.size.y, 1e-3f), size.z / Mathf.Max(b.size.z, 1e-3f)));
                g.transform.localScale = g.transform.localScale * k;
                Measure(g, out b);
                g.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            }
            g.transform.SetParent(wrap.transform, true);
            wrap.transform.SetParent(parent, false);
            return wrap;
        }

        static bool Measure(GameObject g, out Bounds b)
        {
            b = new Bounds(); bool any = false;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any && b.size.sqrMagnitude > 1e-6f;
        }
    }
}
