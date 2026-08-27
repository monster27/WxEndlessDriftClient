#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ZpfTool : Editor
{
    private const string SERVER_PATH_KEY = "ZpfTool_ServerPath";

    [MenuItem("Tools/通用/显隐 &1")]
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

    [MenuItem("Tools/通用/名称 &2")]
    public static void SetObjName()
    {
        GameObject[] selectObjs = Selection.gameObjects;
        int objCtn = selectObjs.Length;

        for (int i = 0; i < objCtn; i++)
        {
            selectObjs[i].name = selectObjs[i].name + "_" + i;
        }
    }

    [MenuItem("Tools/通用/排序 &3")]
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

    [MenuItem("Tools/通用/宽高 &4")]
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

    [MenuItem("Tools/通用/清除服务器路径")]
    public static void ClearServerPath()
    {
        if (EditorPrefs.HasKey(SERVER_PATH_KEY))
        {
            EditorPrefs.DeleteKey(SERVER_PATH_KEY);
            Z_Logger.Log("✅ 已清除保存的服务器路径");
            EditorUtility.DisplayDialog("提示", "已清除保存的服务器路径！", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有保存的服务器路径。", "确定");
        }
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public static string GetRelativePath(string basePath, string fullPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            return fullPath;

        try
        {
            string normalizedBase = System.IO.Path.GetFullPath(basePath).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            string normalizedFull = System.IO.Path.GetFullPath(fullPath).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedBase, normalizedFull, System.StringComparison.OrdinalIgnoreCase))
                return ".";

            if (!normalizedFull.StartsWith(normalizedBase, System.StringComparison.OrdinalIgnoreCase))
                return fullPath;

            if (normalizedFull.Length == normalizedBase.Length)
                return ".";

            int startIndex = normalizedBase.Length;
            if (normalizedFull[startIndex] == System.IO.Path.DirectorySeparatorChar ||
                normalizedFull[startIndex] == System.IO.Path.AltDirectorySeparatorChar)
            {
                startIndex++;
            }

            if (startIndex >= normalizedFull.Length)
                return ".";

            return normalizedFull.Substring(startIndex);
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"获取相对路径失败: basePath={basePath}, fullPath={fullPath}, 错误: {ex.Message}");
            return fullPath;
        }
    }
}
#endif
