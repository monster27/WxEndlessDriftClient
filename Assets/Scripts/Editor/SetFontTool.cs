#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class SetFontTool : EditorWindow
{
    private Font[] fonts;
    private string[] fontNames;
    private int selectedFontIndex = 0;

    private TMP_FontAsset[] tmpFontAssets;
    private string[] tmpFontNames;
    private int selectedTMPIndex = 0;

    private GameObject[] targetObjects;
    private Vector2 scrollPos;
    private bool showLogs = true;
    private bool showReferences = true;

    private List<ReferenceInfo> foundReferences = new List<ReferenceInfo>();
    private List<string> operationLogs = new List<string>();

    [MenuItem("Tools/设置选中字体")]
    private static void SetSelectedFonts()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Z_Logger.LogWarning("请先在 Hierarchy 中选择一个或多个游戏对象。");
            return;
        }

        SetFontTool window = GetWindow<SetFontTool>(true, "批量替换字体工具");
        window.targetObjects = selectedObjects;
        window.LoadFonts();
        window.foundReferences.Clear();
        window.operationLogs.Clear();
        window.Show();
    }

    private void LoadFonts()
    {
        // ===== 加载旧版 Text 字体（编辑器用同步加载） =====
        string[] fontGuids = AssetDatabase.FindAssets("t:Font");
        List<Font> validFonts = new List<Font>();

        foreach (string guid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);

            if (font != null && !string.IsNullOrEmpty(font.name))
            {
                if (!font.name.StartsWith("Arial") && !font.name.StartsWith("Legacy"))
                {
                    validFonts.Add(font);
                }
            }
        }

        fonts = validFonts.ToArray();
        fontNames = new string[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
        {
            fontNames[i] = fonts[i].name;
        }

        // ===== 加载 TMP 字体资源 =====
        string[] tmpGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        List<TMP_FontAsset> validTMPFonts = new List<TMP_FontAsset>();

        foreach (string guid in tmpGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (tmpFont != null && !string.IsNullOrEmpty(tmpFont.name))
            {
                validTMPFonts.Add(tmpFont);
            }
        }

        tmpFontAssets = validTMPFonts.ToArray();
        tmpFontNames = new string[tmpFontAssets.Length];
        for (int i = 0; i < tmpFontAssets.Length; i++)
        {
            tmpFontNames[i] = tmpFontAssets[i].name;
        }

        Z_Logger.Log($"找到 {fonts.Length} 个旧版字体，{tmpFontAssets.Length} 个 TMP 字体资源。");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("选择要应用的字体:", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (fonts != null && fonts.Length > 0)
        {
            selectedFontIndex = EditorGUILayout.Popup("旧版 Text 字体:", selectedFontIndex, fontNames);
        }
        else
        {
            EditorGUILayout.HelpBox("没有找到任何旧版 Text 字体文件 (.ttf/.otf)", MessageType.Warning);
        }

        EditorGUILayout.Space();

        if (tmpFontAssets != null && tmpFontAssets.Length > 0)
        {
            selectedTMPIndex = EditorGUILayout.Popup("TMP 字体资源:", selectedTMPIndex, tmpFontNames);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "没有找到任何 TMP 字体资源 (.asset)\n\n" +
                "请先在 Unity 中创建 TMP Font Asset：\n" +
                "1. 右键点击 .ttf/.otf 字体文件\n" +
                "2. 选择 Create -> TextMeshPro -> Font Asset",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("基本功能", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = fonts != null && fonts.Length > 0 && selectedFontIndex < fonts.Length && fonts[selectedFontIndex] != null;
        if (GUILayout.Button("应用字体到 Text", GUILayout.Height(30)))
        {
            ApplyFontToAllTexts(targetObjects, fonts[selectedFontIndex]);
            Close();
        }
        GUI.enabled = true;

        GUI.enabled = tmpFontAssets != null && tmpFontAssets.Length > 0 && selectedTMPIndex < tmpFontAssets.Length && tmpFontAssets[selectedTMPIndex] != null;
        if (GUILayout.Button("替换为 TextMeshPro", GUILayout.Height(30)))
        {
            if (targetObjects == null || targetObjects.Length == 0)
            {
                Z_Logger.LogWarning("没有目标对象！请先在 Hierarchy 中选择对象。");
                EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个或多个游戏对象。", "确定");
                return;
            }

            TMP_FontAsset selectedTMP = tmpFontAssets[selectedTMPIndex];
            if (selectedTMP == null)
            {
                Z_Logger.LogError("选中的 TMP 字体资源为空！");
                EditorUtility.DisplayDialog("错误", "选中的 TMP 字体资源为空，请重新选择。", "确定");
                return;
            }

            ReplaceTextToTMP(targetObjects, selectedTMP);
            Close();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("高级功能 (自动处理脚本引用)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此功能会:\n" +
            "1. 扫描选中对象上的所有脚本，查找 Text 引用\n" +
            "2. 自动修改脚本源码: Text -> TextMeshProUGUI\n" +
            "3. 执行组件替换",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("1. 扫描 Text 引用", GUILayout.Height(25)))
        {
            ScanTextReferences();
        }

        GUI.enabled = foundReferences.Count > 0;
        if (GUILayout.Button("2. 修改脚本源码", GUILayout.Height(25)))
        {
            ModifyScripts();
        }
        GUI.enabled = true;

        if (GUILayout.Button("3. 一键全部执行", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "将自动执行：扫描引用 -> 修改脚本 -> 替换组件\n\n确认继续？", "确认", "取消"))
            {
                ScanTextReferences();
                if (foundReferences.Count > 0)
                {
                    ModifyScripts();
                    EditorUtility.DisplayDialog("提示", "脚本修改完成，请等待 Unity 编译完成后再执行组件替换。", "确定");
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                    return;
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "没有找到 Text 引用，直接执行组件替换。", "确定");
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        showReferences = EditorGUILayout.Foldout(showReferences, $"找到的 Text 引用 ({foundReferences.Count})");
        if (showReferences && foundReferences.Count > 0)
        {
            foreach (var info in foundReferences)
            {
                EditorGUILayout.LabelField(
                    $"{info.GameObjectName}.{info.ScriptName}.{info.FieldName} -> {info.TargetTextName}",
                    EditorStyles.miniLabel
                );
            }
        }

        EditorGUILayout.Space();

        showLogs = EditorGUILayout.Foldout(showLogs, $"操作日志 ({operationLogs.Count})");
        if (showLogs && operationLogs.Count > 0)
        {
            foreach (string log in operationLogs)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedMiniLabel);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"当前选中对象: {targetObjects?.Length ?? 0} 个", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();
    }

    private void ApplyFontToAllTexts(GameObject[] objects, Font targetFont)
    {
        if (targetFont == null)
        {
            Z_Logger.LogError("选择的字体为空！");
            return;
        }

        if (objects == null || objects.Length == 0)
        {
            Z_Logger.LogWarning("没有目标对象！");
            return;
        }

        int processedCount = 0;

        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;

            Text[] textComponents = obj.GetComponentsInChildren<Text>(true);

            foreach (Text text in textComponents)
            {
                if (text == null) continue;

                Undo.RecordObject(text, "修改字体");
                text.font = targetFont;
                EditorUtility.SetDirty(text);
                processedCount++;
            }
        }

        Z_Logger.Log($"操作完成：共修改 {processedCount} 个 Text 组件");
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    private void ScanTextReferences()
    {
        foundReferences.Clear();
        AddLog("开始扫描 Text 引用...");

        if (targetObjects == null || targetObjects.Length == 0)
        {
            AddLog("错误: 没有目标对象！");
            EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个或多个游戏对象。", "确定");
            return;
        }

        int totalBehaviours = 0;

        foreach (GameObject obj in targetObjects)
        {
            if (obj == null) continue;

            MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                totalBehaviours++;

                System.Type type = behaviour.GetType();
                if (type.Namespace == "UnityEngine" || type.Namespace == "UnityEngine.UI") continue;

                var fields = type.GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Text))
                    {
                        Text textValue = field.GetValue(behaviour) as Text;
                        if (textValue != null && IsChildOfTarget(textValue.gameObject))
                        {
                            string scriptPath = GetScriptPath(type);
                            if (!string.IsNullOrEmpty(scriptPath))
                            {
                                ReferenceInfo info = new ReferenceInfo
                                {
                                    GameObjectName = behaviour.gameObject.name,
                                    ScriptName = type.Name,
                                    FieldName = field.Name,
                                    FieldType = "Text",
                                    IsPublic = field.IsPublic,
                                    TargetTextName = textValue.gameObject.name,
                                    ScriptPath = scriptPath
                                };
                                foundReferences.Add(info);
                                AddLog($"找到: {behaviour.gameObject.name}.{type.Name}.{field.Name}");
                            }
                        }
                    }
                }
            }
        }

        AddLog($"扫描完成！扫描了 {totalBehaviours} 个脚本，找到 {foundReferences.Count} 个 Text 引用");
        EditorUtility.DisplayDialog("扫描完成", $"扫描了 {totalBehaviours} 个脚本\n找到 {foundReferences.Count} 个 Text 引用", "确定");
    }

    private void ModifyScripts()
    {
        if (foundReferences.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到 Text 引用，请先执行扫描！", "确定");
            return;
        }

        var groupedByScript = foundReferences.GroupBy(r => r.ScriptPath)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToList();

        if (groupedByScript.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到可修改的脚本文件。", "确定");
            return;
        }

        string message = $"将修改以下脚本:\n\n";
        foreach (var group in groupedByScript)
        {
            message += $"• {Path.GetFileName(group.Key)} ({group.Count()} 处)\n";
        }

        if (!EditorUtility.DisplayDialog("确认修改脚本", message, "确认", "取消"))
            return;

        int modifiedFiles = 0;
        int totalChanges = 0;

        foreach (var group in groupedByScript)
        {
            string scriptPath = group.Key;
            if (!File.Exists(scriptPath))
            {
                AddLog($"警告: 脚本不存在: {scriptPath}");
                continue;
            }

            try
            {
                string content = File.ReadAllText(scriptPath);
                File.Copy(scriptPath, scriptPath + ".backup", true);

                string modifiedContent = ModifyScriptContent(content);
                File.WriteAllText(scriptPath, modifiedContent);

                modifiedFiles++;
                totalChanges += group.Count();
                AddLog($"已修改: {Path.GetFileName(scriptPath)} ({group.Count()} 处)");
            }
            catch (System.Exception e)
            {
                AddLog($"错误: 修改 {Path.GetFileName(scriptPath)} 失败: {e.Message}");
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成",
            $"修改了 {modifiedFiles} 个脚本\n共 {totalChanges} 处引用\n\n备份文件已保存为 .backup",
            "确定");
    }

    private string ModifyScriptContent(string content)
    {
        if (!content.Contains("using TMPro;"))
        {
            var usingMatch = Regex.Match(content, @"using\s+[^;]+;\s*$", RegexOptions.Multiline);
            if (usingMatch.Success)
            {
                int insertPos = usingMatch.Index + usingMatch.Length;
                content = content.Insert(insertPos, "\nusing TMPro;\n");
            }
            else
            {
                content = "using TMPro;\n" + content;
            }
        }

        string pattern1 = @"(\[[^\]]*SerializeField[^\]]*\]\s*)(private|public|protected)\s+Text\s+([a-zA-Z_][a-zA-Z0-9_]*)";
        content = Regex.Replace(content, pattern1, "$1$2 TextMeshProUGUI $3");

        string pattern2 = @"(?<!(///.*\n))\b(private|public|protected)\s+Text\s+([a-zA-Z_][a-zA-Z0-9_]*)";
        content = Regex.Replace(content, pattern2, "$1 TextMeshProUGUI $2");

        string pattern3 = @"(?<!(///.*\n))\bText\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\{";
        content = Regex.Replace(content, pattern3, "TextMeshProUGUI $1 {");

        string pattern4 = @"\(([^)]*)\bText\b([^)]*)\)";
        content = Regex.Replace(content, pattern4, "($1TextMeshProUGUI$2)");

        return content;
    }

    private void ReplaceTextToTMP(GameObject[] objects, TMP_FontAsset targetTMPFont)
    {
        if (targetTMPFont == null)
        {
            Z_Logger.LogError("选择的 TMP 字体资源为空！");
            EditorUtility.DisplayDialog("错误", "选择的 TMP 字体资源为空，请重新选择。", "确定");
            return;
        }

        if (objects == null || objects.Length == 0)
        {
            Z_Logger.LogWarning("没有目标对象！");
            EditorUtility.DisplayDialog("提示", "请先在 Hierarchy 中选择一个或多个游戏对象。", "确定");
            return;
        }

        int replaceCount = 0;
        int skipCount = 0;
        int errorCount = 0;

        foreach (GameObject obj in objects)
        {
            if (obj == null)
            {
                errorCount++;
                continue;
            }

            Text[] textComponents = obj.GetComponentsInChildren<Text>(true);

            foreach (Text oldText in textComponents)
            {
                if (oldText == null) continue;

                try
                {
                    GameObject gameObject = oldText.gameObject;

                    string textContent = oldText.text;
                    Color color = oldText.color;
                    int fontSize = oldText.fontSize;
                    TextAnchor alignment = oldText.alignment;
                    bool raycastTarget = oldText.raycastTarget;
                    bool richText = oldText.supportRichText;

                    TextMeshProUGUI existingTMP = gameObject.GetComponent<TextMeshProUGUI>();
                    if (existingTMP != null)
                    {
                        existingTMP.text = textContent ?? "";
                        existingTMP.color = color;
                        existingTMP.fontSize = fontSize > 0 ? fontSize : 36;
                        existingTMP.font = targetTMPFont;
                        existingTMP.alignment = ConvertAlignment(alignment);
                        existingTMP.raycastTarget = raycastTarget;
                        existingTMP.richText = richText;
                        skipCount++;
                        continue;
                    }

                    RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
                    Vector2 sizeDelta = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;

                    Undo.DestroyObjectImmediate(oldText);

                    TextMeshProUGUI tmp = gameObject.AddComponent<TextMeshProUGUI>();

                    if (rectTransform != null && sizeDelta != Vector2.zero)
                    {
                        rectTransform.sizeDelta = sizeDelta;
                    }

                    tmp.text = textContent ?? "";
                    tmp.color = color;
                    tmp.fontSize = fontSize > 0 ? fontSize : 36;
                    tmp.font = targetTMPFont;
                    tmp.alignment = ConvertAlignment(alignment);
                    tmp.raycastTarget = raycastTarget;
                    tmp.richText = richText;

                    replaceCount++;
                    AddLog($"替换: {gameObject.name}");
                }
                catch (System.Exception e)
                {
                    Z_Logger.LogError($"替换 {oldText.gameObject.name} 时发生错误: {e.Message}");
                    errorCount++;
                }
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        string result = $"替换完成！\n\n成功: {replaceCount}\n跳过: {skipCount}\n错误: {errorCount}";
        AddLog(result);
        EditorUtility.DisplayDialog("完成", result, "确定");
    }

    private bool IsChildOfTarget(GameObject obj)
    {
        foreach (GameObject target in targetObjects)
        {
            if (target == null) continue;
            if (obj == target) return true;
            if (obj.transform.IsChildOf(target.transform)) return true;
        }
        return false;
    }

    private string GetScriptPath(System.Type type)
    {
        var monoScripts = Resources.FindObjectsOfTypeAll<MonoScript>();
        foreach (var script in monoScripts)
        {
            if (script != null && script.GetClass() == type)
            {
                return AssetDatabase.GetAssetPath(script);
            }
        }
        return "";
    }

    private TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private void AddLog(string message)
    {
        string log = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
        operationLogs.Add(log);
        Z_Logger.Log(log);
    }

    private class ReferenceInfo
    {
        public string GameObjectName;
        public string ScriptName;
        public string FieldName;
        public string FieldType;
        public bool IsPublic;
        public string TargetTextName;
        public string ScriptPath;
    }
}
#endif
