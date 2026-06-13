#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class SpriteTextureConverter : EditorWindow
{
    private string targetPath = "Assets/Resources";
    private Vector2 scrollPosition;
    private List<string> imageFiles = new List<string>();
    private bool includeSubdirectories = true;
    private int convertedCount = 0;
    private int skippedCount = 0;

    [MenuItem("Tools/Sprite纹理转换工具")]
    public static void ShowWindow()
    {
        GetWindow<SpriteTextureConverter>("Sprite纹理转换");
    }

    private void OnEnable()
    {
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        imageFiles.Clear();
        
        if (Directory.Exists(targetPath))
        {
            string[] extensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.gif" };
            
            foreach (string ext in extensions)
            {
                string[] files = Directory.GetFiles(targetPath, ext, 
                    includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                
                foreach (string file in files)
                {
                    if (!file.EndsWith(".meta"))
                    {
                        imageFiles.Add(file);
                    }
                }
            }
        }
        
        convertedCount = 0;
        skippedCount = 0;
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Space(10);
        
        EditorGUILayout.LabelField("📁 目标路径", EditorStyles.boldLabel);
        targetPath = EditorGUILayout.TextField("", targetPath);
        
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择图片目录", targetPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                targetPath = "Assets" + selectedPath.Replace(Application.dataPath, "");
            }
        }
        GUILayout.EndHorizontal();
        
        includeSubdirectories = EditorGUILayout.Toggle("包含子目录", includeSubdirectories);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("🔄 刷新文件列表", GUILayout.Height(30)))
        {
            RefreshFileList();
        }
        
        GUILayout.Space(20);
        
        EditorGUILayout.LabelField($"📋 待处理图片 ({imageFiles.Count} 个)", EditorStyles.boldLabel);
        
        if (imageFiles.Count == 0)
        {
            EditorGUILayout.HelpBox("当前目录下没有找到图片文件", MessageType.Info);
        }
        else
        {
            GUILayout.BeginVertical("Box");
            foreach (string file in imageFiles)
            {
                GUILayout.Label(file, EditorStyles.miniLabel);
            }
            GUILayout.EndVertical();
        }
        
        GUILayout.Space(20);
        
        GUI.backgroundColor = imageFiles.Count > 0 ? Color.green : Color.gray;
        GUI.enabled = imageFiles.Count > 0;
        if (GUILayout.Button("✨ 批量转换为Sprite2D", GUILayout.Height(40)))
        {
            ConvertToSprite();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        
        GUILayout.Space(10);
        
        if (Selection.activeObject != null)
        {
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("🔷 转换选中的图片", GUILayout.Height(30)))
            {
                ConvertSelectedToSprite();
            }
            GUI.backgroundColor = Color.white;
        }
        
        GUILayout.Space(20);
        
        if (convertedCount > 0 || skippedCount > 0)
        {
            GUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("📊 转换结果", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("已转换:", GUILayout.Width(80));
            GUILayout.Label($"{convertedCount} 个", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("已跳过:", GUILayout.Width(80));
            GUILayout.Label($"{skippedCount} 个", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void ConvertToSprite()
    {
        convertedCount = 0;
        skippedCount = 0;
        
        foreach (string file in imageFiles)
        {
            ConvertTextureToSprite(file);
        }
        
        string message = $"转换完成！\n\n";
        message += $"成功转换: {convertedCount} 个\n";
        message += $"跳过（已是Sprite）: {skippedCount} 个";
        
        EditorUtility.DisplayDialog("转换完成", message, "确定");
        RefreshFileList();
    }

    private void ConvertSelectedToSprite()
    {
        convertedCount = 0;
        skippedCount = 0;
        
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".tga" || ext == ".gif")
            {
                ConvertTextureToSprite(path);
            }
        }
        
        string message = $"转换完成！\n\n";
        message += $"成功转换: {convertedCount} 个\n";
        message += $"跳过（已是Sprite）: {skippedCount} 个";
        
        EditorUtility.DisplayDialog("转换完成", message, "确定");
        RefreshFileList();
    }

    private void ConvertTextureToSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        
        if (importer == null)
        {
            Debug.LogError($"无法获取导入器: {path}");
            return;
        }
        
        if (importer.textureType == TextureImporterType.Sprite)
        {
            skippedCount++;
            return;
        }
        
        importer.textureType = TextureImporterType.Sprite;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        
        convertedCount++;
        Debug.Log($"已转换为Sprite: {path}");
    }
}
#endif