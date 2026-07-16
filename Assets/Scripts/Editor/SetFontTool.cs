#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;

public class SetFontTool : EditorWindow
{
    private Font[] fonts;
    private string[] fontNames;
    private int selectedIndex = 0;
    private GameObject[] targetObjects;

    [MenuItem("Tools/设置选中字体")]
    private static void SetSelectedFonts()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("请先在 Hierarchy 中选择一个或多个游戏对象。");
            return;
        }

        // 创建并显示窗口
        SetFontTool window = GetWindow<SetFontTool>(true, "选择字体");
        window.targetObjects = selectedObjects;
        window.LoadFonts();
        window.Show();
    }

    private void LoadFonts()
    {
        // 加载项目中的所有字体
        fonts = Resources.FindObjectsOfTypeAll<Font>();
        
        // 过滤掉 Unity 内置字体（如果你想要也可以保留）
        List<Font> validFonts = new List<Font>();
        foreach (Font f in fonts)
        {
            if (f != null && !string.IsNullOrEmpty(f.name))
            {
                validFonts.Add(f);
            }
        }
        fonts = validFonts.ToArray();
        
        fontNames = new string[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
        {
            fontNames[i] = fonts[i].name;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("选择要应用的字体:", EditorStyles.boldLabel);

        if (fonts != null && fonts.Length > 0)
        {
            selectedIndex = EditorGUILayout.Popup("字体:", selectedIndex, fontNames);

            EditorGUILayout.Space();

            if (GUILayout.Button("应用字体", GUILayout.Height(30)))
            {
                ApplyFontToAll(targetObjects, fonts[selectedIndex]);
                Close();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"当前选中对象: {targetObjects?.Length ?? 0} 个");
        }
        else
        {
            EditorGUILayout.HelpBox("没有找到任何字体文件\n\n请将字体文件（.ttf/.otf）放入 Assets 目录下", MessageType.Warning);
        }
    }

    private void ApplyFontToAll(GameObject[] objects, Font targetFont)
    {
        if (targetFont == null)
        {
            Debug.LogError("选择的字体为空！");
            return;
        }

        if (objects == null || objects.Length == 0)
        {
            Debug.LogWarning("没有目标对象！");
            return;
        }

        int processedCount = 0;

        // 遍历所有选中的对象
        foreach (GameObject obj in objects)
        {
            // 获取该对象及其所有子对象上的 Text 组件（包括未激活的）
            Text[] textComponents = obj.GetComponentsInChildren<Text>(true);

            foreach (Text text in textComponents)
            {
                Undo.RecordObject(text, "修改字体");
                text.font = targetFont;
                EditorUtility.SetDirty(text);
                processedCount++;
            }
        }

        Debug.Log($"操作完成：共修改 {processedCount} 个 Text 组件");
        
        // 刷新场景视图
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }
}
#endif