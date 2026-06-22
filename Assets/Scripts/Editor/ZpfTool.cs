#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;


public class ZpfTool : Editor
{
    /// <summary>
    /// 切换物体显隐状态
    /// </summary>
    [MenuItem("Tools/Zpf/显隐 &1")]
    public static void SetObjActive()
    {
        GameObject[] selectObjs = Selection.gameObjects;
        int objCtn = selectObjs.Length;
        for (int i = 0; i < objCtn; i++)
        {
            bool isAcitve = selectObjs[i].activeSelf;
            selectObjs[i].SetActive(!isAcitve);
        }
    }


    /// <summary>
    /// 设置名称
    /// </summary>
    [MenuItem("Tools/Zpf/名称 &2")]
    public static void SetObjName()
    {
        GameObject[] selectObjs = Selection.gameObjects;

        int objCtn = selectObjs.Length;

        for (int i = 0; i < objCtn; i++)
        {
            selectObjs[i].name = selectObjs[i].name + "_" + i;
        }
    }

    /// <summary>
    ///
    /// </summary>
    [MenuItem("Tools/Zpf/排序 &3")]
    public static void SetObjWH()
    {
        GameObject[] selectObjs = Selection.gameObjects;
        int objCtn = selectObjs.Length;

        Vector3 firstPos = selectObjs[0].transform.position;
        for (int i = 0; i < objCtn; i++)
        {
            selectObjs[i].GetComponent<Transform>().position = new Vector3(firstPos.x + i, firstPos.y, firstPos.z);
        }
    }
    /// <summary>
    /// 设置比例宽高
    /// </summary>
    [MenuItem("Tools/Zpf/宽高 &4")]
    public static void SetObjWH2()
    {
        GameObject[] selectObjs = Selection.gameObjects;

        int objCtn = selectObjs.Length;

        float proportion = 1.5f;

        for (int i = 0; i < objCtn; i++)
        {
            float width = selectObjs[i].GetComponent<RectTransform>().sizeDelta.x;

            float height = selectObjs[i].GetComponent<RectTransform>().sizeDelta.y;

            width *= proportion;

            height *= proportion;

            selectObjs[i].GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
        }
    }

    /// <summary>
    /// 检索所有NetServerManager partial脚本并合并内容到粘贴板
    /// </summary>
    [MenuItem("Tools/Zpf/合并NetServerManager脚本 &5")]
    public static void MergeNetServerManagerScripts()
    {
        // 查找所有NetServerManager相关的脚本文件
        string[] guids = AssetDatabase.FindAssets("NetServerManager t:Script");

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何NetServerManager脚本文件！", "确定");
            return;
        }

        Dictionary<string, string> scriptContents = new Dictionary<string, string>();
        StringBuilder mergedContent = new StringBuilder();
        int fileCount = 0;

        // 添加文件头信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// NetServerManager 合并脚本 - 共找到 {guids.Length} 个partial文件");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 只处理.cs文件
            if (!assetPath.EndsWith(".cs"))
                continue;

            string fileName = Path.GetFileName(assetPath);

            // 检查文件内容是否包含partial关键字（进一步过滤）
            string content = File.ReadAllText(assetPath, Encoding.UTF8);

            // 检查是否包含NetServerManager和partial
            if (content.Contains("partial") && content.Contains("NetServerManager"))
            {
                fileCount++;
                scriptContents[fileName] = content;

                // 添加文件分隔标记和内容
                mergedContent.AppendLine($"// ============================================");
                mergedContent.AppendLine($"// 文件: {fileName}");
                mergedContent.AppendLine($"// 路径: {assetPath}");
                mergedContent.AppendLine($"// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();
            }
        }

        if (fileCount == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到包含partial关键字的NetServerManager脚本文件！", "确定");
            return;
        }

        // 添加文件统计信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 总计合并 {fileCount} 个partial文件");
        mergedContent.AppendLine("// ============================================");

        // 复制到粘贴板
        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        // 显示成功信息
        string message = $"成功合并 {fileCount} 个NetServerManager partial文件！\n\n";
        message += "文件列表：\n";
        foreach (string fileName in scriptContents.Keys)
        {
            message += $"  - {fileName}\n";
        }
        message += "\n内容已复制到粘贴板，可以直接粘贴使用。";

        EditorUtility.DisplayDialog("合并完成", message, "确定");

        // 输出到控制台
        Debug.Log($"已合并 {fileCount} 个NetServerManager partial文件，内容已复制到粘贴板。");
    }

}
#endif