// ==================== CharacterLevelUpConfigEditor.cs（编辑器工具）====================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 人物升级配置编辑器
/// </summary>
public class CharacterLevelUpConfigEditor : EditorWindow
{
    private List<LevelRangeExp> levelRangeExpList = new List<LevelRangeExp>();
    private string savePath = "Assets/Resources/JsonData/BaseFramework/level_up_exp_config.json";
    private Vector2 scrollPosition;

    // 等级区间配置
    private readonly string[] levelRanges = { "1-10", "11-20", "21-30", "31-40", "41-50",
                                              "51-60", "61-70", "71-80", "81-90", "91-100" };
    private readonly string[] levelRangeNames = { "1-10级", "11-20级", "21-30级", "31-40级", "41-50级",
                                                  "51-60级", "61-70级", "71-80级", "81-90级", "91-100级" };

    [MenuItem("Tools/基础框架/人物升级配置编辑器")]
    public static void ShowWindow()
    {
        CharacterLevelUpConfigEditor window = GetWindow<CharacterLevelUpConfigEditor>("人物升级配置编辑器");
        window.minSize = new Vector2(450, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            json = RemoveJsonComments(json);

            LevelUpConfigWrapper wrapper = JsonUtility.FromJson<LevelUpConfigWrapper>(json);

            if (wrapper != null)
            {
                ConvertWrapperToLists(wrapper);
                Debug.Log("加载配置成功");
            }
            else
            {
                ResetToDefault();
            }
        }
        else
        {
            ResetToDefault();
        }
    }

    private void ConvertWrapperToLists(LevelUpConfigWrapper wrapper)
    {
        // 转换等级经验配置
        levelRangeExpList.Clear();
        for (int i = 0; i < levelRanges.Length; i++)
        {
            string rangeKey = levelRanges[i];
            int expRequired = 0;

            if (wrapper.levelRangeExpList != null)
            {
                var found = wrapper.levelRangeExpList.Find(x => x.rangeKey == rangeKey);
                if (found != null)
                {
                    expRequired = found.expRequired;
                }
            }

            levelRangeExpList.Add(new LevelRangeExp
            {
                rangeKey = rangeKey,
                rangeName = levelRangeNames[i],
                expRequired = expRequired
            });
        }
    }

    private LevelUpConfigWrapper ConvertListsToWrapper()
    {
        LevelUpConfigWrapper wrapper = new LevelUpConfigWrapper();
        wrapper.levelRangeExpList = new List<LevelRangeExp>(levelRangeExpList);
        return wrapper;
    }

    private void ResetToDefault()
    {
        levelRangeExpList.Clear();
        int[] defaultExp = { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
        for (int i = 0; i < levelRanges.Length; i++)
        {
            levelRangeExpList.Add(new LevelRangeExp
            {
                rangeKey = levelRanges[i],
                rangeName = levelRangeNames[i],
                expRequired = defaultExp[i]
            });
        }
    }

    private string RemoveJsonComments(string json)
    {
        json = System.Text.RegularExpressions.Regex.Replace(json, @"//.*", string.Empty);
        json = System.Text.RegularExpressions.Regex.Replace(json, @"/\*[\s\S]*?\*/", string.Empty);
        return json;
    }

    private void SaveData()
    {
        LevelUpConfigWrapper wrapper = ConvertListsToWrapper();
        string json = JsonUtility.ToJson(wrapper, true);

        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(savePath, json);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("保存成功", $"配置已保存到\n{savePath}", "确定");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("人物升级配置编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        DrawToolbar();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawLevelUpExpSection();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
        DrawHelpBox();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("保存配置", GUILayout.Height(30)))
        {
            SaveData();
        }

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("刷新数据", GUILayout.Height(30)))
        {
            LoadData();
            Repaint();
        }

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("重置默认", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认重置", "确定要重置为默认配置吗？", "确定", "取消"))
            {
                ResetToDefault();
                Repaint();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLevelUpExpSection()
    {
        EditorGUILayout.LabelField("升级经验配置（每十级升级所需经验）", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");

        for (int i = 0; i < levelRangeExpList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(levelRangeExpList[i].rangeName, GUILayout.Width(80));
            int newExp = EditorGUILayout.IntField(levelRangeExpList[i].expRequired);
            if (newExp != levelRangeExpList[i].expRequired)
            {
                levelRangeExpList[i].expRequired = Mathf.Max(0, newExp);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHelpBox()
    {
        EditorGUILayout.HelpBox(
            $"配置文件路径: {savePath}\n" +
            $"支持等级: 1-100级\n" +
            $"稀有度: 普通(1) 稀有(2) 精良(3) 史诗(4) 传说(5)\n" +
            $"导出数据可用于服务器/客户端同步",
            MessageType.Info);
    }
}

// ==================== 编辑器内部数据结构 ====================

[System.Serializable]
public class LevelRangeExp
{
    public string rangeKey;
    public string rangeName;
    public int expRequired;
}

[System.Serializable]
public class LevelUpConfigWrapper
{
    public List<LevelRangeExp> levelRangeExpList = new List<LevelRangeExp>();
}
#endif