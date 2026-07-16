using UnityEngine;
using UnityEditor;

public class FixTextureImport : EditorWindow
{
    [MenuItem("Tools/修复纹理导入设置(不修改源文件)")]
    static void FixImportSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/") || path.Contains("unity_builtin"))
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            // 获取纹理尺寸
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;

            int w = tex.width;
            int h = tex.height;

            // 如果已经是4的倍数，跳过
            if (w % 4 == 0 && h % 4 == 0)
                continue;

            // 关键：对于非4倍数的纹理，使用不同的压缩格式
            // 或者让Unity自动处理
            if (importer.textureType == TextureImporterType.Sprite)
            {
                // Sprite 可以使用其他格式
                importer.textureCompression = TextureImporterCompression.Compressed;

                // 使用 ETC2 或 DXT 格式（支持非4倍数）
#if UNITY_2021_1_OR_NEWER
                // 设置为自动选择最佳格式
                importer.SetPlatformTextureSettings("Default", -1, TextureImporterFormat.Automatic, 100, false);
#endif
            }
            else
            {
                // 对于普通纹理，设置为自动压缩
                importer.textureCompression = TextureImporterCompression.Compressed;
            }

            // 重要：让 Unity 自动处理尺寸问题
            importer.npotScale = TextureImporterNPOTScale.ToLarger;

            importer.SaveAndReimport();
            fixedCount++;
            Debug.Log($"修复导入设置: {path} ({w}x{h})");
        }

        AssetDatabase.Refresh();
        Debug.Log($"修复完成！共处理 {fixedCount} 个纹理的导入设置。");
    }
}