using UnityEditor;
using UnityEditor.SceneManagement;

namespace HantuBelajar.EditorTools
{
    /// <summary>
    /// Menjalankan TestRunner di play mode.
    /// Batch (tanpa -quit, tanpa -nographics): Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.HarnessLauncher.RunShots
    /// </summary>
    public static class HarnessLauncher
    {
        [MenuItem("Tools/Hantu Belajar/Uji/Screenshot visual")] public static void RunShots() { Run("shots"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Bot bermain (smoke)")] public static void RunSmoke() { Run("smoke"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Eksperimen DDA")] public static void RunDda() { Run("dda"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Uji kejar hantu")] public static void RunChase() { Run("chase"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Alur pemain (menu-main-menang)")] public static void RunFlow() { Run("flow"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Apakah hantu tahu pemain bersembunyi")] public static void RunHide() { Run("hide"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Kerapian tangga")] public static void RunStairs() { Run("stairs"); }
        [MenuItem("Tools/Hantu Belajar/Uji/Informasi HUD (notifikasi, indikator hantu, petunjuk)")] public static void RunHud() { Run("hud"); }

        static void Run(string kind)
        {
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetString("HB_TEST_KIND", kind);
            EditorSceneManager.OpenScene("Assets/Scenes/HauntedHouse.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
