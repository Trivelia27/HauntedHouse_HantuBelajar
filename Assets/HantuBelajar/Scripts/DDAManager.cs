using System.Collections.Generic;
using UnityEngine;

namespace HantuBelajar
{
    /// <summary>
    /// Dynamic Difficulty Adjustment.
    /// Indikator performa pemain -> skor P (0..1) -> tingkat kesulitan d (0..1)
    /// -> parameter AI hantu (langsung) + parameter PCG (rumah berikutnya).
    /// </summary>
    public class DDAManager : MonoBehaviour
    {
        public float d = 0.4f, P = 0.5f;
        public const float Target = 0.5f, Rate = 0.12f, Window = 8f;

        // indikator
        public int caughtTotal, winsTotal, nearMissWin, chasesWin;
        public float timeSinceProgress, chaseWin, hideWin, evalT, houseTime;
        public float pace, safety, resource, calm;

        // output -> AI hantu
        public float patrolSpeed, chaseSpeed, visionRange, fov, hearingMult, searchTime, learnWeight, checkMul, loseTime;
        // output -> pemain
        public float batteryDrain;
        public readonly List<string> log = new List<string>();

        void Awake() { Apply(); }

        public void Apply()
        {
            patrolSpeed = Mathf.Lerp(1.5f, 2.6f, d);
            chaseSpeed = Mathf.Lerp(3.0f, 5.0f, d);
            visionRange = Mathf.Lerp(7f, 15f, d);
            fov = Mathf.Lerp(70f, 115f, d);
            hearingMult = Mathf.Lerp(0.6f, 1.4f, d);
            searchTime = Mathf.Lerp(8f, 22f, d);
            learnWeight = Mathf.Lerp(0.25f, 0.9f, d);
            checkMul = Mathf.Lerp(0.5f, 1.25f, d);
            loseTime = Mathf.Lerp(2f, 4.5f, d);
            batteryDrain = Mathf.Lerp(0.6f, 1.5f, d);
        }

        /// <summary>Parameter PCG untuk rumah berikutnya.</summary>
        public PcgParams MakePcg(int seed)
        {
            PcgParams p = new PcgParams { seed = seed };
            p.width = Mathf.RoundToInt(Mathf.Lerp(11f, 16f, d));
            p.height = Mathf.RoundToInt(Mathf.Lerp(8f, 12f, d));
            p.hidingSpots = Mathf.RoundToInt(Mathf.Lerp(10f, 4f, d));
            p.batteries = Mathf.RoundToInt(Mathf.Lerp(5f, 1f, d));
            p.relicCount = d > 0.65f ? 4 : 3;
            p.loopChance = Mathf.Lerp(0.4f, 0.12f, d);
            p.propDensity = Mathf.Lerp(0.3f, 0.45f, d);
            return p;
        }

        public void BeginHouse() { timeSinceProgress = 0; chaseWin = hideWin = 0; nearMissWin = chasesWin = 0; evalT = 0; houseTime = 0; }

        void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || !gm.Playing) return;
            float dt = Time.deltaTime;
            houseTime += dt; timeSinceProgress += dt; evalT += dt;
            if (gm.ghost.state == GhostState.Kejar) chaseWin += dt;
            if (gm.player.hidden) hideWin += dt;
            float gd = Vector3.Distance(gm.ghost.transform.position, gm.player.transform.position);
            if (gd < 3f && !nearFlag) { nearFlag = true; nearMissWin++; }
            if (gd > 6f) nearFlag = false;
            if (evalT >= Window) Evaluate();
        }
        bool nearFlag;

        void Evaluate()
        {
            GameManager gm = GameManager.I;
            pace = Mathf.Clamp01(1.2f - timeSinceProgress / 90f);
            safety = 1f - Mathf.Clamp01(chaseWin / evalT * 2.5f);
            resource = gm.player.battery / 100f;
            calm = 1f - Mathf.Clamp01(hideWin / evalT * 1.5f);
            P = Mathf.Clamp01(0.35f * pace + 0.30f * safety + 0.15f * resource + 0.20f * calm - nearMissWin * 0.08f);
            float old = d;
            d = Mathf.Clamp(d + (P - Target) * Rate, 0.05f, 0.95f);
            Apply();
            Log("P=" + P.ToString("0.00") + " -> d " + old.ToString("0.00") + " => " + d.ToString("0.00") + (P > Target ? " (pemain unggul, hantu diperkuat)" : " (pemain tertekan, hantu dilemahkan)"));
            evalT = 0; chaseWin = 0; hideWin = 0; nearMissWin = 0;
        }

        public void OnRelic() { Shift(+0.04f, "relik diambil"); timeSinceProgress = 0f; }
        public void OnCaught() { caughtTotal++; Shift(-0.15f, "pemain tertangkap"); }
        public void OnWin() { winsTotal++; Shift(+0.10f, "pemain lolos"); }
        public void OnChaseStart() { chasesWin++; }
        public void Force(float delta) { Shift(delta, "manual (demo F2/F3)"); }

        void Shift(float delta, string why)
        {
            float old = d;
            d = Mathf.Clamp(d + delta, 0.05f, 0.95f);
            Apply();
            Log("Event " + why + ": d " + old.ToString("0.00") + " => " + d.ToString("0.00"));
        }

        void Log(string s)
        {
            string t = GameManager.I != null ? "[" + houseTime.ToString("0") + "s] " : "";
            log.Add(t + s); if (log.Count > 6) log.RemoveAt(0);
            Debug.Log("[DDA] " + s);
        }

        public string Label { get { return d < 0.3f ? "MUDAH" : d < 0.6f ? "SEDANG" : d < 0.8f ? "SULIT" : "MIMPI BURUK"; } }
    }
}
