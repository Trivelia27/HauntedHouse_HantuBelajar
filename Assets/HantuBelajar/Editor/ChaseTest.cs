using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>Uji kejar hantu: Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.ChaseTest.Run</summary>
    [InitializeOnLoad]
    public static class ChaseTest
    {
        static int phase, trial, frames, reversals, caught, stuck;
        static float minDist, t, startDist;
        static Vector3 lastPos, lastDir;
        static readonly StringBuilder log = new StringBuilder();
        const int Trials = 6;

        static ChaseTest()
        {
            if (SessionState.GetInt("HB_CHASE", 0) != 1) return;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetInt("HB_CHASE", 1);
            EditorSceneManager.OpenScene("Assets/Scenes/HauntedHouse.unity");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            GameManager gm = GameManager.I;
            if (gm == null || gm.layout == null) return;
            if (phase == 0)
            {
                Time.captureFramerate = 30;
                gm.state = GameManager.State.Playing;
                phase = 1; trial = 0;
                StartTrial(gm);
                return;
            }
            if (phase != 1) return;

            frames++; t += 1f / 30f;
            GhostAI g = gm.ghost;
            Vector3 gp = g.transform.position;
            Vector3 step = gp - lastPos; step.y = 0;
            if (step.magnitude > 0.01f)
            {
                Vector3 dir = step.normalized;
                if (lastDir != Vector3.zero && Vector3.Dot(dir, lastDir) < -0.5f) reversals++;
                lastDir = dir;
            }
            lastPos = gp;
            float dist = Vector3.Distance(new Vector3(gp.x, 0, gp.z), new Vector3(gm.player.transform.position.x, 0, gm.player.transform.position.z));
            minDist = Mathf.Min(minDist, dist);

            bool done = gm.state == GameManager.State.Caught || t > 15f;
            if (!done) return;
            bool c = gm.state == GameManager.State.Caught;
            if (c) caught++;
            log.AppendLine("percobaan " + (trial + 1) + ": jarak awal " + startDist.ToString("0.0") + " m, min " + minDist.ToString("0.0") + " m, berbalik arah " + reversals + "x, " + (c ? "TERTANGKAP dalam " + t.ToString("0.0") + " dtk" : "tidak tertangkap (state " + g.state + ")"));
            trial++;
            if (trial >= Trials)
            {
                log.AppendLine("HASIL: tertangkap " + caught + "/" + Trials);
                Directory.CreateDirectory("TestOutput");
                File.WriteAllText("TestOutput/chase_test.txt", log.ToString());
                SessionState.SetInt("HB_CHASE", 0);
                phase = 2;
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
                return;
            }
            gm.NewHouse(gm.seed + 1);
            gm.state = GameManager.State.Playing;
            StartTrial(gm);
        }

        static void StartTrial(GameManager gm)
        {
            // pemain diam (seperti saat demo menekan F6 lalu menunggu)
            gm.player.autopilot = true; gm.player.autoDir = Vector3.zero;
            gm.ghost.SummonAndChase(3);
            frames = 0; reversals = 0; t = 0f; lastDir = Vector3.zero;
            lastPos = gm.ghost.transform.position;
            startDist = Vector3.Distance(lastPos, gm.player.transform.position);
            minDist = 999f;
        }
    }
}
