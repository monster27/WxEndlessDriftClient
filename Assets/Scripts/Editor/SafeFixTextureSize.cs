using UnityEngine;
using UnityEditor;

public class SafeFixTextureSize : EditorWindow
{
    [MenuItem("Tools/安全修复纹理尺寸(需手动改Spirte)(AutoStream资源)")]
    static void SafeFixAllTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("unity_builtin") || path.Contains("Library/"))
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;

            int w = tex.width;
            int h = tex.height;

            // 计算下一个 4 的倍数（只放大，不缩小，保证内容不被裁剪）
            int newW = Mathf.CeilToInt(w / 4f) * 4;
            int newH = Mathf.CeilToInt(h / 4f) * 4;

            // 如果已经是 4 的倍数，跳过
            if (w == newW && h == newH)
                continue;

            // 重要：先把纹理类型改为 Default，避免 Sprite 模式干扰
            importer.textureType = TextureImporterType.Default;

            // 关键设置：设置为 None，Unity 就不会自动裁剪或拉伸
            importer.npotScale = TextureImporterNPOTScale.None;

            // 把 Max Size 设置成计算出的新尺寸（确保画布足够大）
            int maxSize = Mathf.Max(newW, newH);
            // 对齐到 2 的幂次方（因为 Max Size 必须是 2 的幂次方）
            if (maxSize <= 32) maxSize = 32;
            else if (maxSize <= 64) maxSize = 64;
            else if (maxSize <= 128) maxSize = 128;
            else if (maxSize <= 256) maxSize = 256;
            else if (maxSize <= 512) maxSize = 512;
            else if (maxSize <= 1024) maxSize = 1024;
            else if (maxSize <= 2048) maxSize = 2048;
            else maxSize = 4096;

            importer.maxTextureSize = maxSize;

            // 使用 RGBA 32 bit 保证无损
            importer.textureCompression = TextureImporterCompression.Uncompressed;
#if UNITY_2021_1_OR_NEWER
            importer.textureFormat = TextureImporterFormat.RGBA32;
#endif

            // 保存并重新导入
            importer.SaveAndReimport();
            fixedCount++;
            Debug.Log($"安全修复: {path} ({w}x{h} -> {newW}x{newH}, 画布: {maxSize}x{maxSize})");
        }

        AssetDatabase.Refresh();
        Debug.Log($"修复完成！共处理 {fixedCount} 个纹理。");
    }
}