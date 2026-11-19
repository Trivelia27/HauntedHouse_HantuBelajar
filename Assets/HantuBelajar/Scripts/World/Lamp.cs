using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Lampu rumah: api & lilin bergoyang, lampu dinding berkedip/mati sesaat ketika hantu mendekat.</summary>
    public class Lamp : MonoBehaviour
    {
        public enum Kind { Sconce, Fill, Fire, Candle }
        public static readonly List<Lamp> All = new List<Lamp>();
        public static Vector3 GhostPos;
        public static bool GhostActive;
        public static float GhostRadius = 9f;

        public Light lightRef; public Kind kind; public float baseIntensity;
        float seed, nextBlink; bool blinkOff;
        AudioSource buzz;

        public void Init(Light l, Kind k, Renderer emissive)
        {
            lightRef = l; kind = k; baseIntensity = l.intensity; seed = Random.value * 100f;
            if (!All.Contains(this)) All.Add(this);
            if (k == Kind.Sconce && Random.value < 0.35f)
            {
                AudioClip c = AssetLibrary.Clip("buzz");
                if (c != null)
                {
                    buzz = gameObject.AddComponent<AudioSource>();
                    buzz.clip = c; buzz.loop = true; buzz.spatialBlend = 1f; buzz.minDistance = 0.6f; buzz.maxDistance = 6f; buzz.rolloffMode = AudioRolloffMode.Linear; buzz.volume = 0.22f;
                    buzz.time = Random.value * c.length * 0.9f;
                    buzz.Play();
                }
            }
        }

        void OnDestroy() { All.Remove(this); }

        void Update()
        {
            if (lightRef == null) return;
            float t = Time.time, k = 1f;
            switch (kind)
            {
                case Kind.Fire: k = 0.7f + Mathf.PerlinNoise(seed, t * 3.2f) * 0.45f + Mathf.PerlinNoise(seed + 9f, t * 11f) * 0.15f; break;
                case Kind.Candle: k = 0.82f + Mathf.PerlinNoise(seed, t * 4.5f) * 0.3f; break;
            }
            if (GhostActive && (kind == Kind.Sconce || kind == Kind.Fill || kind == Kind.Candle))
            {
                float d = Vector3.Distance(transform.position, GhostPos);
                if (d < GhostRadius)
                {
                    float p = 1f - d / GhostRadius;
                    if (t > nextBlink)
                    {
                        blinkOff = Random.value < p * (kind == Kind.Candle ? 0.25f : 0.65f);
                        nextBlink = t + Random.Range(0.04f, 0.22f) + (1f - p) * 0.3f;
                    }
                    k *= blinkOff ? 0.06f : 1f - p * 0.35f;
                }
                else blinkOff = false;
            }
            lightRef.intensity = baseIntensity * k;
            if (buzz != null) buzz.volume = (blinkOff ? 0.5f : 0.22f) * Settings.Sfx;
        }
    }

    /// <summary>Partikel sederhana dibuat lewat kode.</summary>
    public static class Fx
    {
        public static ParticleSystem Sparkles(Transform parent, Color c, float radius)
        {
            GameObject g = new GameObject("Sparkles");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = c;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = 9f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Look.ParticleMat("sparkle", Color.white, Look.SoftDot);
            ps.Play();
            return ps;
        }

        /// <summary>Kabut tipis di sekitar hantu.</summary>
        public static ParticleSystem Wisps(Transform parent, Color c)
        {
            GameObject g = new GameObject("Wisps");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startColor = c;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.45f;
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Look.ParticleMat("wisp", Color.white, Look.SoftDot);
            ps.Play();
            return ps;
        }
    }
}
