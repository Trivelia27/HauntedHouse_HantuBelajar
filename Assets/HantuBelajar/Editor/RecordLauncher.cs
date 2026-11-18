using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>Rekam video demo: Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.RecordLauncher.Run</summary>
    [InitializeOnLoad]
    public static class RecordLauncher
    {
        static RecordLauncher()
        {
            if (SessionState.GetInt("HB_REC", 0) != 1) return;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update += Watch;
        }

        [MenuItem("Tools/Hantu Belajar/Rekam Video Demo (frame)")]
        public static void Run()
        {
            SessionState.SetInt("HB_REC", 1);
            SessionState.SetFloat("HB_REC_T0", (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene("Assets/Scenes/HauntedHouse.unity");
            EditorApplication.playModeStateChanged -= OnPlay;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlay(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode) new GameObject("DemoRecorder").AddComponent<DemoRecorder>();
        }

        static void Watch()
        {
            if (!EditorApplication.isPlaying) return;
            if (DemoRecorder.Done || EditorApplication.timeSinceStartup - SessionState.GetFloat("HB_REC_T0", 0f) > 1500)
            {
                Directory.CreateDirectory("TestOutput");
                File.WriteAllText("TestOutput/record_report.txt", "done=" + DemoRecorder.Done + " " + DemoRecorder.Report);
                SessionState.SetInt("HB_REC", 0);
                EditorApplication.update -= Watch;
                EditorApplication.Exit(0);
            }
        }
    }
}
