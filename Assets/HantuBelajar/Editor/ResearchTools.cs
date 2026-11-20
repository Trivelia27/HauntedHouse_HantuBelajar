using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>Menu analisis data uji coba pemain (gabungan DataUjiCoba/ di folder proyek dan di samping exe).</summary>
    public static class ResearchTools
    {
        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }

        static List<string> DataRoots()
        {
            return new List<string>
            {
                Path.Combine(ProjectRoot, "DataUjiCoba"),
                Path.Combine(ProjectRoot, "Builds", "Windows", "DataUjiCoba"),
            };
        }

        static string Analyze()
        {
            ResearchReport.AnalyzePlaytest(DataRoots(), ProjectRoot, "Hasil_Uji_Coba.md", "ujicoba_", false);
            return Path.Combine(ProjectRoot, "Hasil_Uji_Coba.md");
        }

        [MenuItem("Tools/Hantu Belajar/Analisis Data Uji Coba", false, 40)]
        public static void AnalyzeMenu()
        {
            string md = Analyze();
            Debug.Log("[Uji coba] laporan: " + md);
            EditorUtility.RevealInFinder(md);
        }

        [MenuItem("Tools/Hantu Belajar/Buka Folder Data Uji Coba", false, 41)]
        public static void OpenData()
        {
            foreach (string d in DataRoots())
                if (Directory.Exists(d)) { EditorUtility.RevealInFinder(d); return; }
            string def = DataRoots()[0];
            Directory.CreateDirectory(def);
            EditorUtility.RevealInFinder(def);
        }

        /// <summary>Baris perintah: Unity.exe -batchmode -nographics -projectPath P -executeMethod HantuBelajar.EditorTools.ResearchTools.AnalyzeBatch -quit</summary>
        public static void AnalyzeBatch()
        {
            string md = Analyze();
            Debug.Log("[Uji coba] laporan: " + md);
        }
    }
}
