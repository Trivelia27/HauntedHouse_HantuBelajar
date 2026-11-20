using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HantuBelajar.EditorTools
{
    /// <summary>
    /// Membersihkan peringatan yang muncul setiap kali proyek dibuka:
    /// "Missing types referenced from component UniversalRenderPipelineGlobalSettings ...".
    /// Aset pengaturan URP dari paket Asset Store dibuat dengan URP yang lebih baru dan menyimpan pengaturan bertipe
    /// (mis. LightmapSamplingSettings, VrsRenderPipelineRuntimeResources) yang tidak ada di Unity 6000.0 / URP 17.0,
    /// jadi data itu tidak pernah terpakai. Dibersihkan otomatis sekali per sesi editor dan bisa dijalankan dari menu.
    /// Batch: Unity.exe -batchmode -projectPath P -executeMethod HantuBelajar.EditorTools.ProjectHealth.FixBatch -quit
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectHealth
    {
        static readonly string[] Filters =
        {
            "t:UniversalRenderPipelineGlobalSettings", "t:UniversalRenderPipelineAsset", "t:UniversalRendererData", "t:VolumeProfile",
        };

        static ProjectHealth()
        {
            if (Application.isBatchMode || SessionState.GetBool("HB_ProjectHealth", false)) return;
            SessionState.SetBool("HB_ProjectHealth", true);
            EditorApplication.delayCall += () => ClearMissingTypes(false);
        }

        [MenuItem("Tools/Hantu Belajar/Bersihkan Peringatan Proyek")]
        public static void Fix() { ClearMissingTypes(true); }

        public static void FixBatch() { ClearMissingTypes(true); }

        /// <summary>Hapus referensi bertipe hilang pada aset render pipeline. Mengembalikan jumlah objek yang dibersihkan.</summary>
        public static int ClearMissingTypes(bool verbose)
        {
            HashSet<string> paths = new HashSet<string>();
            foreach (string f in Filters)
                foreach (string g in AssetDatabase.FindAssets(f, new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            int cleaned = 0;
            foreach (string p in paths)
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (o == null || !SerializationUtility.HasManagedReferencesWithMissingTypes(o)) continue;
                    foreach (ManagedReferenceMissingType m in SerializationUtility.GetManagedReferencesWithMissingTypes(o))
                        Debug.Log("[ProjectHealth] " + p + ": membuang pengaturan tak dikenal " + m.namespaceName + "." + m.className + " (" + m.assemblyName + ")");
                    SerializationUtility.ClearAllManagedReferencesWithMissingTypes(o);
                    EditorUtility.SetDirty(o);
                    cleaned++;
                }
            if (cleaned > 0) AssetDatabase.SaveAssets();
            if (verbose || cleaned > 0) Debug.Log("[ProjectHealth] aset render pipeline diperiksa: " + paths.Count + ", dibersihkan: " + cleaned);
            return cleaned;
        }
    }
}
