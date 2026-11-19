using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Audio berlapis: ambience rumah + latar Unnerving Ambient, musik eksplorasi / kejar (Horror Music Starter Kit),
    /// lapisan "hantu dekat", detak jantung, efek pendek. Volume mengikuti Pengaturan.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I;
        public enum Mode { Menu, Game }
        Mode mode = Mode.Menu;
        AudioSource amb, bed, music, chase, near, menu, heart, sfx;
        float heartT, bedTimer, musicTimer;
        AudioClip synStep, synStinger, synHeart, synDrone, synPick, synCreak, synRattle, synClick, synScream, synOpen;
        static AudioClip breath, whisperClip;
        static readonly List<(AudioSource, float, bool)> registered = new List<(AudioSource, float, bool)>();
        System.Random r = new System.Random();

        void Awake()
        {
            I = this;
            synStinger = Synth("stinger", 1.6f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * (980f - t * 520f)) * 0.3f + Mathf.Sin(t * 2f * Mathf.PI * (1470f - t * 700f)) * 0.15f + Noise() * 0.3f) * Mathf.Exp(-t * 1.8f) + Mathf.Sin(t * 2f * Mathf.PI * 48f) * Mathf.Exp(-t * 4f) * 0.6f);
            synHeart = Synth("heart", 0.9f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * 52f) * (Mathf.Exp(-t * 16f) + (t > 0.26f ? Mathf.Exp(-(t - 0.26f) * 16f) * 0.7f : 0f)) * 0.9f);
            synDrone = Synth("drone", 8f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * 46f) * 0.22f + Mathf.Sin(t * 2f * Mathf.PI * 69.5f + Mathf.Sin(t * 0.7f) * 2f) * 0.12f + Noise() * 0.02f) * (0.7f + 0.3f * Mathf.Sin(t * Mathf.PI / 4f)), true);
            synPick = Synth("pick", 1.1f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * 660f) + Mathf.Sin(t * 2f * Mathf.PI * 990f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.25f) * Mathf.Exp(-t * 3.5f) * 0.25f);
            synCreak = Synth("creak", 0.9f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * (140f + Mathf.Sin(t * 23f) * 45f + t * 60f)) * 0.22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f)) + Noise() * 0.05f * Mathf.Exp(-t * 5f));
            synOpen = Synth("open", 1.8f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * (110f + Mathf.Sin(t * 13f) * 30f + t * 40f)) * 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.8f)) + Noise() * 0.04f);
            synRattle = Synth("rattle", 0.5f, (t, i) => Noise() * 0.5f * Mathf.Abs(Mathf.Sin(t * 60f)) * Mathf.Exp(-t * 5f));
            synClick = Synth("click", 0.05f, (t, i) => Noise() * Mathf.Exp(-t * 200f) * 0.4f);
            synScream = Synth("scream", 1.6f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * (620f + Mathf.Sin(t * 40f) * 90f)) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * (931f + Mathf.Sin(t * 33f) * 120f)) * 0.2f + Noise() * 0.35f) * Mathf.Min(1f, t * 20f) * Mathf.Exp(-t * 1.3f));
            amb = Src(true, 0f, true); bed = Src(true, 0f, true); music = Src(true, 0f, true); chase = Src(true, 0f, true); near = Src(true, 0f, true);
            menu = Src(true, 0f, true); heart = Src(false, 1f, false); sfx = Src(false, 1f, false);
            menu.ignoreListenerPause = true; sfx.ignoreListenerPause = true;
            amb.clip = AssetLibrary.Clip("amb_house") ?? synDrone;
            near.clip = AssetLibrary.Clip("ghost_near");
            menu.clip = AssetLibrary.Clip("music_menu");
            chase.clip = AssetLibrary.Clip("music_chase");
            foreach (AudioSource s in new[] { amb, near, menu, chase }) if (s.clip != null) s.Play();
            NextBed(); NextMusic();
        }

        AudioSource Src(bool loop, float vol, bool music)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.loop = loop; s.volume = vol; s.playOnAwake = false; s.spatialBlend = 0f;
            return s;
        }

        public void SetMode(Mode m) { mode = m; }
        public static void Register(AudioSource s, float baseVol, bool music) { registered.Add((s, baseVol, music)); }

        void NextBed()
        {
            AudioClip c = AssetLibrary.Clip("amb_bed", r);
            if (c == null) return;
            bed.clip = c; bed.time = 0f; bed.Play(); bedTimer = c.length - 1f;
        }
        void NextMusic()
        {
            AudioClip c = AssetLibrary.Clip("music_explore", r);
            if (c == null) return;
            music.clip = c; music.time = 0f; music.Play(); musicTimer = c.length - 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AudioListener.volume = Settings.Master;
            float mus = Settings.Music, sx = Settings.Sfx;
            GameManager gm = GameManager.I;
            bool game = mode == Mode.Game && gm != null && gm.player != null;
            bool chasing = game && gm.ghost != null && gm.ghost.state == GhostState.Kejar && gm.Playing;
            float dist = game && gm.ghost != null ? gm.ghost.DistanceTo(gm.player.transform.position) : 99f;
            float danger = Mathf.Clamp01(1f - dist / 14f);
            Fade(menu, mode == Mode.Menu ? 0.55f * mus : 0f, dt * 0.5f);
            Fade(amb, game ? 0.45f * sx : 0f, dt * 0.4f);
            Fade(bed, game ? (chasing ? 0.08f : 0.32f) * mus : 0f, dt * 0.3f);
            Fade(music, game ? (chasing ? 0.03f : 0.22f) * mus : 0f, dt * 0.35f);
            Fade(chase, chasing ? 0.7f * mus : 0f, chasing ? dt * 1.5f : dt * 0.35f);
            Fade(near, game && !chasing ? danger * danger * 0.55f * sx : 0f, dt * 0.6f);
            bedTimer -= Time.deltaTime; musicTimer -= Time.deltaTime;
            if (bed.clip != null && bedTimer <= 0f) NextBed();
            if (music.clip != null && musicTimer <= 0f) NextMusic();
            foreach (var e in registered) if (e.Item1 != null) e.Item1.volume = e.Item2 * (e.Item3 ? mus : sx);
            registered.RemoveAll(e => e.Item1 == null);

            heartT -= Time.deltaTime;
            if (game && gm.Playing && danger > 0.15f && heartT <= 0f)
            {
                heartT = Mathf.Lerp(1.1f, 0.38f, danger);
                heart.PlayOneShot(synHeart, danger * 0.8f * sx);
            }
        }

        static void Fade(AudioSource s, float target, float rate) { if (s != null) s.volume = Mathf.MoveTowards(s.volume, target, rate); }

        void Play(AudioClip c, float v) { if (c != null) sfx.PlayOneShot(c, v * Settings.Sfx); }
        void PlayAt(AudioClip c, Vector3 p, float v)
        {
            if (c == null) return;
            GameObject g = new GameObject("Sfx3D"); g.transform.position = p;
            AudioSource s = g.AddComponent<AudioSource>(); s.clip = c; s.spatialBlend = 1f; s.minDistance = 1f; s.maxDistance = 18f; s.rolloffMode = AudioRolloffMode.Linear; s.volume = v * Settings.Sfx;
            s.Play(); Destroy(g, c.length + 0.1f);
        }

        public void Pickup() { Play(synPick, 0.9f); }
        public void Click() { Play(synClick, 0.6f); }
        public void Creak() { Play(synCreak, 0.7f); }
        public void Creak(Vector3 p) { PlayAt(synCreak, p, 1f); }
        public void Rattle() { Play(synRattle, 0.8f); }
        public void DoorOpen() { Play(synOpen, 0.8f); }
        public void Stinger() { Play(synStinger, 0.85f); }
        public void Scream() { Play(synScream, 1f); Play(AssetLibrary.Clip("lose"), 0.9f); }
        public void Win() { Play(AssetLibrary.Clip("win") ?? synPick, 0.9f); }
        public void UiHover() { Play(synClick, 0.25f); }

        public static AudioClip BreathClip()
        {
            if (breath == null) breath = Synth("breath", 2.4f, (t, i) => { float ph = (t % 1.2f) / 1.2f; float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(ph * 1.6f)) * (ph < 0.62f ? 1f : 0f); return Noise() * 0.22f * env * (0.6f + 0.4f * Mathf.Sin(t * 30f)); }, true);
            return breath;
        }
        public static AudioClip WhisperClip()
        {
            if (whisperClip == null) whisperClip = Synth("whisper", 5f, (t, i) => Noise() * 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 5.3f)) * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 1.1f))), true);
            return whisperClip;
        }

        static uint seed = 1;
        static float Noise() { seed = seed * 1664525u + 1013904223u; return ((seed >> 9) / 4194304f) * 2f - 1f; }
        static AudioClip Synth(string name, float len, System.Func<float, int, float> f, bool loop = false)
        {
            int sr = 22050, n = (int)(sr * len);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float v = f(t, i);
                if (loop) { float e = Mathf.Min(1f, Mathf.Min(t, len - t) / 0.25f); v *= Mathf.Lerp(0.5f, 1f, e); }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            AudioClip c = AudioClip.Create(name, n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
