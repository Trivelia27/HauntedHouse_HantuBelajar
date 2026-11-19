using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Dynamic Difficulty Adjustment.
    /// Indikator performa (tiap 8 detik): tempo relik, keamanan (waktu dikejar), sumber daya (baterai), ketenangan (waktu sembunyi),
    /// penalti nyaris tertangkap -> skor P (0..1) -> dihaluskan (EMA) -> d += (P - 0,5) x 0,1, dibatasi per rumah.
    /// Event: tertangkap -0,15, relik +0,03, lolos +0,08.
    /// Output: parameter AI hantu (langsung) + parameter PCG rumah berikutnya (DdaMap).
    /// </summary>
    public class DDAManager : MonoBehaviour
    {
        public const float Target = 0.62f, Rate = 0.1f, Window = 8f, MaxRise = 0.15f, MaxDrop = 0.3f;
        public const float EvCaught = -0.15f, EvRelic = 0.03f, EvWin = 0.08f;
        public bool adaptive = true;
        public float d = 0.4f, P = 0.62f, Ps = 0.62f;
        public DdaMap.GhostParams gp;

        public float pace, safety, resource, calm;
        public float houseTime, timeSinceProgress, houseStartD, expectedRelicTime = 45f, evalT;
        public int caughtTotal, winsTotal, relicsTotal, housesPlayed, evals, nearMisses, chasesHouse, hidesHouse;
        public float chaseTimeHouse, hideTimeHouse;
        float winChase, winHide, winTime; int winNear; bool nearFlag;
        public readonly List<string> log = new List<string>();

        void Awake() { Apply(); }

        public void Apply() { gp = DdaMap.Ghost(d); }
        public PcgParams MakePcg(int seed) { return DdaMap.Pcg(d, seed); }

        public void BeginHouse(HouseLayout L)
        {
            houseStartD = d; houseTime = 0f; timeSinceProgress = 0f; evalT = 0f;
            winChase = winHide = winTime = 0f; winNear = 0; nearMisses = 0; chasesHouse = 0; hidesHouse = 0; chaseTimeHouse = 0f; hideTimeHouse = 0f;
            expectedRelicTime = 30f + 0.45f * L.freeCells;
            housesPlayed++;
            Apply();
        }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.Playing || gm.ghost == null || gm.ghost.frozen) return;
            float dt = Time.deltaTime;
            houseTime += dt; timeSinceProgress += dt; evalT += dt; winTime += dt;
            bool chased = gm.ghost.state == GhostState.Kejar;
            if (chased) { winChase += dt; chaseTimeHouse += dt; }
            if (gm.player.hidden) { winHide += dt; hideTimeHouse += dt; }
            float gd = gm.ghost.DistanceTo(gm.player.transform.position);
            if (chased && gd < 2.6f && !nearFlag) { nearFlag = true; winNear++; nearMisses++; }
            if (gd > 6f) nearFlag = false;
            if (evalT >= Window) Evaluate();
        }

        void Evaluate()
        {
            GameManager gm = GameManager.I;
            float Te = expectedRelicTime;
            // tempo: relik yang sudah didapat dibanding jadwal (houseTime / Te), dikurangi bila lama tanpa kemajuan
            float ahead = gm.relicsFound + (gm.player.hasKey ? 0.3f : 0f) - houseTime / Te;
            pace = Mathf.Clamp01(0.5f + 0.35f * ahead - Mathf.Max(0f, timeSinceProgress - 1.5f * Te) / (3f * Te));
            safety = 1f - Mathf.Clamp01(winChase / Mathf.Max(1f, winTime) * 2.5f);
            resource = Mathf.Clamp01(gm.player.battery / 80f);
            calm = 1f - Mathf.Clamp01(winHide / Mathf.Max(1f, winTime) * 1.6f);
            P = Mathf.Clamp01(0.35f * pace + 0.30f * safety + 0.15f * resource + 0.20f * calm - 0.08f * winNear);
            Ps = Mathf.Lerp(Ps, P, 0.5f);
            evals++;
            float old = d;
            if (adaptive)
            {
                d = Mathf.Clamp(d + (Ps - Target) * Rate, Mathf.Max(0.05f, houseStartD - MaxDrop), Mathf.Min(0.95f, houseStartD + MaxRise));
                Apply();
            }
            Log("P=" + P.ToString("0.00") + " (halus " + Ps.ToString("0.00") + ") -> d " + old.ToString("0.00") + " => " + d.ToString("0.00") + (Ps > Target + 0.03f ? "  pemain unggul" : Ps < Target - 0.03f ? "  pemain tertekan" : "  seimbang"));
            evalT = 0f; winChase = winHide = winTime = 0f; winNear = 0;
        }

        public void OnRelic() { relicsTotal++; timeSinceProgress = 0f; Shift(EvRelic, "relik diambil"); }
        public void OnProgress() { timeSinceProgress = Mathf.Min(timeSinceProgress, expectedRelicTime * 0.5f); }
        public void OnCaught() { caughtTotal++; Shift(EvCaught, "pemain tertangkap"); Ps = Mathf.Min(Ps, Target - 0.1f); }
        public void OnWin() { winsTotal++; Shift(EvWin, "pemain lolos"); }
        public void OnChaseStart() { chasesHouse++; }
        public void OnHide() { hidesHouse++; }
        public void OnNoise(float level) { }
        public void Force(float delta) { Shift(delta, "manual (F2/F3)", true); }

        void Shift(float delta, string why, bool force = false)
        {
            if (!adaptive && !force) return;
            float old = d;
            d = Mathf.Clamp(d + delta, 0.05f, 0.95f);
            Apply();
            Log("Event " + why + ": d " + old.ToString("0.00") + " => " + d.ToString("0.00"));
        }

        void Log(string s)
        {
            log.Add("[" + houseTime.ToString("0") + "s] " + s);
            if (log.Count > 6) log.RemoveAt(0);
        }

        public string Label { get { return d < 0.25f ? "MUDAH" : d < 0.5f ? "SEDANG" : d < 0.75f ? "SULIT" : "MIMPI BURUK"; } }
    }
}
