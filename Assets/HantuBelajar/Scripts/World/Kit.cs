using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Penempatan model paket: ukuran dihitung dari mesh (tanpa instansiasi), model diskalakan G.S,
    /// komponen skrip demo paket dibuang, collider diganti kotak yang pas.
    /// </summary>
    public static class Kit
    {
        static readonly Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();

        /// <summary>Batas model dalam ruang lokal "pembungkus" (rotasi root prefab ikut), belum diskalakan.</summary>
        public static Bounds RawBounds(GameObject prefab)
        {
            if (prefab == null) return new Bounds(Vector3.zero, Vector3.one);
            if (boundsCache.TryGetValue(prefab, out Bounds b)) return b;
            bool any = false;
            Matrix4x4 rootInv = Matrix4x4.TRS(prefab.transform.localPosition, Quaternion.identity, Vector3.one).inverse;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                Encapsulate(ref b, ref any, mf.sharedMesh.bounds, rootInv * mf.transform.localToWorldMatrix);
            }
            foreach (SkinnedMeshRenderer sr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sr.sharedMesh == null) continue;
                Encapsulate(ref b, ref any, sr.sharedMesh.bounds, rootInv * sr.transform.localToWorldMatrix);
            }
            if (!any) b = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            // root berada di titik asal pembungkus (posisi root diabaikan, rotasi & skala root ikut)
            boundsCache[prefab] = b;
            return b;
        }

        static void Encapsulate(ref Bounds b, ref bool any, Bounds mb, Matrix4x4 m)
        {
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }

        /// <summary>Batas setelah skala s dan putaran yaw (derajat) di sekitar titik asal.</summary>
        public static Bounds ScaledBounds(GameObject prefab, float s, float yaw)
        {
            Bounds b = RawBounds(prefab);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Bounds r = new Bounds(); bool any = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
                p = q * (p * s);
                if (!any) { r = new Bounds(p, Vector3.zero); any = true; } else r.Encapsulate(p);
            }
            return r;
        }

        /// <summary>
        /// Instansiasi prefab di dalam pembungkus baru. scale = skala seragam (default G.S), stretch = skala tambahan per sumbu lokal.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 worldPos, float worldYaw, float scale = G.S, Vector3? stretch = null,
                                       bool keepLights = false, bool keepParticles = true, string name = null)
        {
            GameObject wrap = new GameObject(name ?? (prefab != null ? prefab.name : "Model"));
            wrap.transform.SetParent(parent, false);
            wrap.transform.position = worldPos;
            wrap.transform.rotation = Quaternion.Euler(0f, worldYaw, 0f);
            if (prefab == null) return wrap;
            GameObject g = Object.Instantiate(prefab, wrap.transform, false);
            g.name = prefab.name;
            Vector3 st = stretch ?? Vector3.one;
            Transform t = g.transform;
            t.localPosition = Vector3.zero;     // konsisten dengan RawBounds (posisi root diabaikan)
            t.localScale = Vector3.Scale(t.localScale * scale, st);
            Strip(g, keepLights, keepParticles);
            return wrap;
        }

        /// <summary>Buang skrip demo paket, collider bawaan, animator, audio; lampu/partikel opsional.</summary>
        public static void Strip(GameObject g, bool keepLights, bool keepParticles)
        {
            foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string asm = mb.GetType().Assembly.GetName().Name;
                if (asm == "Assembly-CSharp" || asm.StartsWith("Assembly-CSharp")) Object.Destroy(mb);
            }
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb);
            foreach (Animator an in g.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            foreach (AudioSource a in g.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(a);
            if (!keepLights) foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
            if (!keepParticles) foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        /// <summary>
        /// Tempatkan model "menempel dinding": sisi belakang (z minimum setelah yawOffset) di backZ lokal sel,
        /// dipusatkan di x lokal. frame = transform sel (z+ = menghadap ke ruangan).
        /// </summary>
        public static GameObject AgainstWall(GameObject prefab, Transform frame, float localX, float backZ, float yawOffset, float scale = G.S,
                                             bool keepLights = false, float y = 0f, bool collider = true, string name = null)
        {
            Bounds b = ScaledBounds(prefab, scale, yawOffset);
            Vector3 local = new Vector3(localX - b.center.x, y - b.min.y, backZ - b.min.z);
            GameObject w = Spawn(prefab, frame, frame.TransformPoint(local), frame.eulerAngles.y + yawOffset, scale, null, keepLights, true, name);
            if (collider) AddBox(w, ScaledBounds(prefab, scale, 0f));
            return w;
        }

        /// <summary>Tempatkan model berpusat di posisi lokal sel (alas di lantai).</summary>
        public static GameObject Centered(GameObject prefab, Transform frame, Vector3 localCenter, float yawOffset, float scale = G.S,
                                          bool keepLights = false, bool collider = true, bool onFloor = true, string name = null)
        {
            Bounds b = ScaledBounds(prefab, scale, yawOffset);
            Vector3 local = new Vector3(localCenter.x - b.center.x, onFloor ? localCenter.y - b.min.y : localCenter.y - b.center.y, localCenter.z - b.center.z);
            GameObject w = Spawn(prefab, frame, frame.TransformPoint(local), frame.eulerAngles.y + yawOffset, scale, null, keepLights, true, name);
            if (collider) AddBox(w, ScaledBounds(prefab, scale, 0f));
            return w;
        }

        /// <summary>Skala agar model muat di kotak (lebar x, tinggi y, dalam z) - skala seragam.</summary>
        public static float FitScale(GameObject prefab, float yawOffset, Vector3 box)
        {
            Bounds b = ScaledBounds(prefab, 1f, yawOffset);
            float k = float.MaxValue;
            if (box.x > 0f && b.size.x > 1e-3f) k = Mathf.Min(k, box.x / b.size.x);
            if (box.y > 0f && b.size.y > 1e-3f) k = Mathf.Min(k, box.y / b.size.y);
            if (box.z > 0f && b.size.z > 1e-3f) k = Mathf.Min(k, box.z / b.size.z);
            return k == float.MaxValue ? G.S : k;
        }

        /// <summary>BoxCollider seukuran batas model (bounds dalam ruang lokal pembungkus).</summary>
        public static BoxCollider AddBox(GameObject wrap, Bounds localBounds, float shrink = 0.04f)
        {
            BoxCollider bc = wrap.AddComponent<BoxCollider>();
            bc.center = localBounds.center;
            bc.size = new Vector3(Mathf.Max(0.05f, localBounds.size.x - shrink), Mathf.Max(0.05f, localBounds.size.y), Mathf.Max(0.05f, localBounds.size.z - shrink));
            return bc;
        }

        /// <summary>Kubus primitif (dunia) dengan atau tanpa collider.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 worldPos, Vector3 size, Material m, bool collider = true, Quaternion? rot = null)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = worldPos;
            g.transform.rotation = rot ?? Quaternion.identity;
            g.transform.localScale = size;
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return g;
        }

        /// <summary>Primitif dalam ruang lokal parent, tanpa collider.</summary>
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 size, Material m, string name = "Part", Quaternion? localRot = null)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot ?? Quaternion.identity;
            g.transform.localScale = size;
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return g;
        }

        /// <summary>Ganti material bernama 'from' pada semua renderer turunan.</summary>
        public static void SwapMaterial(GameObject g, string from, Material to)
        {
            if (to == null) return;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                Material[] ms = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith(from)) { ms[i] = to; changed = true; }
                if (changed) r.sharedMaterials = ms;
            }
        }
    }
}
