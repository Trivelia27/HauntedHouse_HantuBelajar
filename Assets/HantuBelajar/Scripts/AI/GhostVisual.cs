using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Wujud hantu (model Kenney Graveyard Kit "character-ghost") dengan animasi prosedural.</summary>
    public class GhostVisual : MonoBehaviour
    {
        const float KS = 2.45f, Lift = 0.18f;
        Transform body, armL, armR, eyesRoot;
        Material mat, eyeMat;
        Light aura;
        ParticleSystem wisps;
        float alpha = 1f, lean, armReach, lunge, t0;
        Color eyeCol = new Color(0.55f, 0.75f, 1f);
        public float Alpha { get { return alpha; } }

        public void Build()
        {
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            Texture2D tex = Resources.Load<Texture2D>("Kenney/ghost_colormap");
            mat = new Material(Look.Fade("ghostBase", new Color(0.52f, 0.56f, 0.64f, 0.62f), tex)) { name = "GhostMat" };
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.065f, 0.09f));
            bool ok = Part("torso", body, new Vector3(0f, 0.223f * KS + Lift, 0f));
            GameObject sl = new GameObject("ShoulderL"); sl.transform.SetParent(body, false); sl.transform.localPosition = new Vector3(-0.125f * KS, 0.373f * KS + Lift, 0.0025f * KS); armL = sl.transform;
            GameObject sr = new GameObject("ShoulderR"); sr.transform.SetParent(body, false); sr.transform.localPosition = new Vector3(0.125f * KS, 0.373f * KS + Lift, 0.0025f * KS); armR = sr.transform;
            Part("arm_left", armL, Vector3.zero);
            Part("arm_right", armR, Vector3.zero);
            if (!ok)
            {
                GameObject c = Kit.Prim(PrimitiveType.Capsule, body, new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 0.95f, 0.9f), mat, "Shroud");
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            eyeMat = new Material(Look.Emissive("ghostEyeBase", Color.white, 3f)) { name = "GhostEyes" };
            eyesRoot = new GameObject("Eyes").transform; eyesRoot.SetParent(body, false);
            float ey = (0.223f + 0.36f) * KS + Lift, ez = 0.2f * KS;
            Material socket = Look.UnlitColor("ghostSocket", new Color(0.01f, 0.01f, 0.015f));
            for (int s = -1; s <= 1; s += 2)
            {
                // rongga mata gelap + pupil kecil menyala
                GameObject so = Kit.Prim(PrimitiveType.Cube, eyesRoot, new Vector3(s * 0.07f * KS, ey, ez), new Vector3(0.13f, 0.16f, 0.015f), socket, "Socket");
                so.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObject e = Kit.Prim(PrimitiveType.Sphere, eyesRoot, new Vector3(s * 0.07f * KS, ey - 0.01f, ez + 0.012f), new Vector3(0.045f, 0.05f, 0.02f), eyeMat, "Eye");
                e.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            GameObject lg = new GameObject("Aura"); lg.transform.SetParent(body, false); lg.transform.localPosition = new Vector3(0f, 1.3f, 1.1f);
            aura = lg.AddComponent<Light>(); aura.type = LightType.Point; aura.range = 4f; aura.intensity = 0.8f; aura.color = eyeCol; aura.shadows = LightShadows.None;
            wisps = Fx.Wisps(body, new Color(0.8f, 0.85f, 1f, 0.35f));
            t0 = Random.value * 10f;
        }

        bool Part(string name, Transform parent, Vector3 pos)
        {
            GameObject src = Resources.Load<GameObject>("Kenney/ghost_" + name);
            if (src == null) return false;
            MeshFilter mf = src.GetComponentInChildren<MeshFilter>(true);
            if (mf == null) return false;
            GameObject g = new GameObject("Kenney_" + name);
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = Vector3.one * KS;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        public void SetAlpha(float a) { alpha = Mathf.Clamp01(a); }
        public void Lunge(float t) { lunge = Mathf.Clamp01(t / 0.3f); }

        public void Tick(GhostState s, bool see, float speed01, float dt)
        {
            float t = Time.time + t0;
            Color target = s == GhostState.Kejar ? new Color(1f, 0.12f, 0.08f) : (s == GhostState.Hadang || s == GhostState.Jaga) ? new Color(0.75f, 0.3f, 1f) :
                           (s == GhostState.Cari || s == GhostState.Periksa || s == GhostState.Selidiki) ? new Color(1f, 0.65f, 0.15f) : new Color(0.55f, 0.75f, 1f);
            eyeCol = Color.Lerp(eyeCol, target, 1f - Mathf.Exp(-dt * 5f));
            eyeMat.SetColor("_EmissionColor", eyeCol * (6f + lunge * 6f));
            aura.color = eyeCol;
            aura.intensity = (0.6f + (s == GhostState.Kejar ? 0.9f : 0f) + Mathf.PerlinNoise(t * 3f, 0.2f) * 0.3f) * alpha;
            // melayang & condong
            lean = Mathf.Lerp(lean, speed01 * 16f, 1f - Mathf.Exp(-dt * 4f));
            body.localPosition = new Vector3(0f, Mathf.Sin(t * 1.9f) * 0.09f + 0.05f, 0f);
            body.localRotation = Quaternion.Euler(lean + Mathf.Sin(t * 1.3f) * 2.5f, 0f, Mathf.Sin(t * 1.7f) * 3.5f);
            // tangan menjangkau saat mengejar / menerjang
            float reach = s == GhostState.Kejar || lunge > 0f ? 1f : s == GhostState.Periksa ? 0.6f : 0f;
            armReach = Mathf.Lerp(armReach, reach, 1f - Mathf.Exp(-dt * 5f));
            // lengan model mengarah ke samping (x): terkulai = putar di sumbu z, menjangkau ke depan = putar di sumbu y
            float swing = Mathf.Sin(t * 2.4f) * 8f * (1f - armReach);
            float hang = 58f * (1f - armReach), reachYaw = 78f * armReach;
            if (lunge > 0f) { reachYaw = Mathf.Lerp(reachYaw, 30f, lunge); hang = Mathf.Lerp(hang, -35f, lunge); swing = 0f; }   // menerkam: tangan terangkat ke samping
            if (armL) armL.localRotation = Quaternion.Euler(0f, reachYaw, 0f) * Quaternion.Euler(0f, 0f, hang + swing);
            if (armR) armR.localRotation = Quaternion.Euler(0f, -reachYaw, 0f) * Quaternion.Euler(0f, 0f, -hang + swing);
            // transparansi berdenyut + skala saat menerjang
            float a = alpha * (0.5f + Mathf.PerlinNoise(t * 2.2f, 1.7f) * 0.2f + (s == GhostState.Kejar ? 0.15f : 0f));
            Color c = mat.color; c.a = a; mat.color = c;
            mat.SetColor("_EmissionColor", new Color(0.07f, 0.09f, 0.13f) * alpha);
            body.localScale = Vector3.one * (1f + lunge * 0.12f);
            eyesRoot.gameObject.SetActive(alpha > 0.15f);
            var em = wisps.emission; em.rateOverTime = 14f * alpha;
        }
    }
}
