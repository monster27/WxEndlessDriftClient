//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;

///// <summary>
///// 图片导入自动设置工具
///// 所有图片自动设置为 Sprite 类型
///// </summary>
//public class AutoSetTextureSetting : AssetPostprocessor
//{
//    void OnPreprocessTexture()
//    {
//        if (assetImporter is not TextureImporter importer)
//            return;

//        if (assetPath.Contains("unity_builtin") || assetPath.Contains("Library/"))
//            return;

//        if (importer.userData == "AutoSet")
//            return;

//        ApplyTextureSettings(importer);
//    }

//    private static void ApplyTextureSettings(TextureImporter importer)
//    {
//        // ========== 全部设置为 Sprite ==========
//        importer.textureType = TextureImporterType.Sprite;
//        importer.spritePixelsPerUnit = 100;
//        importer.mipmapEnabled = false;

//        // ========== 压缩设置 ==========
//        importer.sRGBTexture = true;
//        importer.textureCompression = TextureImporterCompression.Compressed;

//        // ========== 平台设置（MiniGame） ==========
//        TextureImporterPlatformSettings platformSettings =
//            importer.GetPlatformTextureSettings("MiniGame");

//        if (platformSettings == null || string.IsNullOrEmpty(platformSettings.name))
//        {
//            platformSettings = new TextureImporterPlatformSettings
//            {
//                name = "MiniGame"
//            };
//        }

//        platformSettings.overridden = true;
//        platformSettings.maxTextureSize = 1024;
//        platformSettings.format = TextureImporterFormat.ASTC_6x6;
//        platformSettings.compressionQuality = 50;

//        importer.SetPlatformTextureSettings(platformSettings);

//        // ========== NPOT 处理 ==========
//        importer.npotScale = TextureImporterNPOTScale.ToNearest;

//        // ========== 标记已设置 ==========
//        importer.userData = "AutoSet";
//        importer.SaveAndReimport();

//        Debug.Log($"[AutoSet] ✅ 已设置: {importer.assetPath} (Sprite, ASTC_6x6, 1024)");
//    }

//    // ========== 批量处理已有图片 ==========

//    [MenuItem("Tools/批量设置纹理 (全部Sprite, ASTC 6x6, 1024)")]
//    static void BatchApply()
//    {
//        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
//        int processed = 0;

//        foreach (string guid in guids)
//        {
//            string path = AssetDatabase.GUIDToAssetPath(guid);
//            if (path.Contains("unity_builtin") || path.Contains("Library/"))
//                continue;

//            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
//            if (importer == null)
//                continue;

//            if (importer.userData == "AutoSet")
//                continue;

//            ApplyTextureSettings(importer);
//            processed++;
//        }

//        AssetDatabase.Refresh();
//        Debug.Log($"🎯 批量设置完成！共处理 {processed} 个纹理");
//    }

//    [MenuItem("Tools/查看纹理设置状态")]
//    static void CheckStatus()
//    {
//        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
//        int total = 0;
//        int set = 0;

//        foreach (string guid in guids)
//        {
//            string path = AssetDatabase.GUIDToAssetPath(guid);
//            if (path.Contains("unity_builtin") || path.Contains("Library/"))
//                continue;

//            total++;
//            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
//            if (importer != null && importer.userData == "AutoSet")
//                set++;
//        }

//        Debug.Log($"📊 纹理状态: {set}/{total} 已应用设置");
//    }
//}
//#endif