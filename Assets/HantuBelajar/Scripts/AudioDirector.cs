using UnityEngine;

namespace HantuBelajar
{
    /// <summary>Audio: pakai klip Asset Store (AssetLibrary) bila ada, kalau tidak suara sintetis.</summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I;
        AudioSource amb, music, chase, heart, sfx;
        AudioClip synStep, synScare, synHeart, synDrone, synPick, synDoor, synWhisper, synClick;
        System.Random r = new System.Random();
        float heartT;

        void Awake()
        {
            I = this;
            synStep = Synth("step", 0.12f, (t, i) => Noise(i) * Mathf.Exp(-t * 40f) * 0.5f);
            synScare = Synth("scare", 1.4f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * (900f - t * 500f)) * 0.4f + Noise(i) * 0.35f) * Mathf.Exp(-t * 2f));
            synHeart = Synth("heart", 0.9f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * 55f) * (Mathf.Exp(-t * 18f) + (t > 0.25f ? Mathf.Exp(-(t - 0.25f) * 18f) * 0.7f : 0f)));
            synDrone = Synth("drone", 8f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * 46f) * 0.25f + Mathf.Sin(t * 2f * Mathf.PI * 69.5f + Mathf.Sin(t * 0.7f) * 2f) * 0.15f + Noise(i) * 0.02f) * (0.7f + 0.3f * Mathf.Sin(t * Mathf.PI / 4f)), true);
            synPick = Synth("pick", 0.7f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * 880f) + Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.5f) * Mathf.Exp(-t * 5f) * 0.3f);
            synDoor = Synth("door", 0.6f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * (180f + Mathf.Sin(t * 30f) * 40f)) * 0.25f * Mathf.Exp(-t * 3f) + Noise(i) * 0.05f * Mathf.Exp(-t * 6f));
            synWhisper = Synth("whisper", 4f, (t, i) => Noise(i) * 0.12f * (0.5f + 0.5f * Mathf.Sin(t * 5f)) * (0.5f + 0.5f * Mathf.Sin(t * 1.3f)), true);
            synClick = Synth("click", 0.05f, (t, i) => Noise(i) * Mathf.Exp(-t * 200f) * 0.4f);

            amb = Src(true, 0.55f); music = Src(true, 0.3f); chase = Src(true, 0f); heart = Src(false, 0.8f); sfx = Src(false, 0.9f);
            amb.clip = AssetLibrary.Clip(l => l.ambient, r) ?? synDrone; amb.Play();
            AudioClip m = AssetLibrary.Clip(l => l.music, r);
            if (m != null) { music.clip = m; music.Play(); }
            AudioClip c = AssetLibrary.Clip(l => l.chase, r) ?? AssetLibrary.Clip(l => l.music, r);
            if (c != null) { chase.clip = c; chase.Play(); }
        }

        AudioSource Src(bool loop, float vol) { AudioSource s = gameObject.AddComponent<AudioSource>(); s.loop = loop; s.volume = vol; s.playOnAwake = false; return s; }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.ghost == null || gm.player == null) return;
            float dist = Vector3.Distance(gm.ghost.transform.position, gm.player.transform.position);
            bool chasing = gm.ghost.state == GhostState.Kejar && gm.Playing;
            chase.volume = Mathf.MoveTowards(chase.volume, chasing ? 0.75f : 0f, Time.deltaTime * 0.8f);
            music.volume = Mathf.MoveTowards(music.volume, chasing ? 0.05f : 0.3f, Time.deltaTime * 0.5f);
            float danger = Mathf.Clamp01(1f - dist / 12f);
            heartT -= Time.deltaTime;
            if (gm.Playing && danger > 0.05f && heartT <= 0f)
            {
                heartT = Mathf.Lerp(1.2f, 0.42f, danger);
                AudioClip hc = AssetLibrary.Clip(l => l.heartbeat, r) ?? synHeart;
                heart.PlayOneShot(hc, danger);
            }
        }

        public void Step(Vector3 p, float v) { sfx.PlayOneShot(AssetLibrary.Clip(l => l.footstep, r) ?? synStep, v * 0.6f); }
        public void Scare() { sfx.PlayOneShot(AssetLibrary.Clip(l => l.scare, r) ?? synScare, 0.9f); }
        public void Pickup() { sfx.PlayOneShot(AssetLibrary.Clip(l => l.pickup, r) ?? synPick, 0.8f); }
        public void Door() { sfx.PlayOneShot(AssetLibrary.Clip(l => l.door, r) ?? synDoor, 0.7f); }
        public void Click() { sfx.PlayOneShot(synClick, 0.6f); }
        public AudioClip GhostLoop() { return AssetLibrary.Clip(l => l.whisper, r) ?? synWhisper; }

        static uint seed = 1;
        static float Noise(int i) { seed = seed * 1664525u + 1013904223u; return ((seed >> 9) / 4194304f) * 2f - 1f; }
        static AudioClip Synth(string name, float len, System.Func<float, int, float> f, bool loop = false)
        {
            int sr = 22050, n = (int)(sr * len);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float v = f(t, i);
                if (loop) { float e = Mathf.Min(1f, Mathf.Min(t, len - t) / 0.3f); v *= Mathf.Lerp(0.6f, 1f, e); }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            AudioClip c = AudioClip.Create(name, n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
