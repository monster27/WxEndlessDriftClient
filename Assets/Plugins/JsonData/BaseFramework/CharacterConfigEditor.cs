using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 人物配置编辑器 - 完美解决版本
/// </summary>
public class CharacterConfigEditor : EditorWindow
{
    private List<CharacterConfig> characterList = new List<CharacterConfig>();
    private string savePath = "Assets/Resources/JsonData/BaseFramework/characters.json";
    private const int CHARACTER_START_ID = 3401;
    private const int CHARACTER_END_ID = 3499;
    private Vector2 scrollPosition;

    // 使用EditorWindow的序列化字段来保存状态
    [SerializeField] private int deleteIndex = -1;
    [SerializeField] private bool needDelete = false;
    
    // 存储每个人物的折叠状态
    [SerializeField] private Dictionary<int, bool> characterFoldStates = new Dictionary<int, bool>();

    [MenuItem("Tools/游戏内容/2.物品内部数据/3401_人物配置")]
    public static void ShowWindow()
    {
        CharacterConfigEditor window = GetWindow<CharacterConfigEditor>("人物配置编辑器");
        window.minSize = new Vector2(450, 550);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // 在编辑器更新循环中处理删除，完全避免OnGUI中的布局问题
        if (needDelete && deleteIndex >= 0 && deleteIndex < characterList.Count)
        {
            needDelete = false;
            int index = deleteIndex;
            deleteIndex = -1;

            characterList.RemoveAt(index);
            SaveData();
            LoadData();
            Repaint();
        }
    }

    private void LoadData()
    {
        // 只加载配置文件
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            CharacterConfigList configList = JsonUtility.FromJson<CharacterConfigList>(json);
            if (configList != null && configList.characters != null)
            {
                characterList = configList.characters;
            }
        }
        else
        {
            characterList = new List<CharacterConfig>();
        }
    }

    private void SaveData()
    {
        CharacterConfigList configList = new CharacterConfigList();
        configList.characters = characterList;
        string json = JsonUtility.ToJson(configList, true);

        // 只保存到配置文件
        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(savePath, json);
        AssetDatabase.Refresh();
        Debug.Log("人物配置已保存: " + savePath);
    }

    private void OnGUI()
    {
        DrawMainGUI();
    }

    private void DrawMainGUI()
    {
        EditorGUILayout.LabelField("人物配置编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 工具栏
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加新人物", GUILayout.Height(30)))
        {
            AddNewCharacter();
        }

        if (GUILayout.Button("保存配置", GUILayout.Height(30)))
        {
            SaveData();
        }

        if (GUILayout.Button("刷新数据", GUILayout.Height(30)))
        {
            LoadData();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 使用垂直布局，确保Begin/End配对
        EditorGUILayout.BeginVertical();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (characterList.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无数据，点击\"添加新人物\"创建", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < characterList.Count; i++)
            {
                DrawCharacterItem(i);
                EditorGUILayout.Space(5);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"共 {characterList.Count} 个人物", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawCharacterItem(int index)
    {
        CharacterConfig character = characterList[index];

        // 获取或初始化折叠状态
        if (!characterFoldStates.ContainsKey(index))
        {
            characterFoldStates[index] = false; // 默认展开
        }
        bool isFolded = characterFoldStates[index];

        // 使用GUILayout.BeginArea来隔离每个项目的布局
        EditorGUILayout.BeginVertical("box");

        // 标题行
        EditorGUILayout.BeginHorizontal();
        
        // 折叠/展开按钮
        GUIContent foldIcon = new GUIContent(isFolded ? "▶" : "▼");
        if (GUILayout.Button(foldIcon, GUILayout.Width(20), GUILayout.Height(20)))
        {
            characterFoldStates[index] = !isFolded;
        }
        
        EditorGUILayout.LabelField($"人物 {index + 1}: {character.name} (ID: {character.id})", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        // 删除按钮 - 只设置标志，不执行实际删除
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除人物", GUILayout.Width(80), GUILayout.Height(25)))
        {
            deleteIndex = index;
            needDelete = true;
            // 不要在OnGUI中做任何其他操作
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 根据折叠状态决定是否显示详细内容
        if (!isFolded)
        {
            EditorGUILayout.Space(5);

            // 编辑区域
            EditorGUI.indentLevel++;

            // ID范围提示
            EditorGUILayout.HelpBox($"人物ID范围: {CHARACTER_START_ID} - {CHARACTER_END_ID}", MessageType.Info);
            
            int newId = EditorGUILayout.IntField("人物ID", character.id);
            if (newId != character.id)
            {
                // 验证ID范围
                if (newId < CHARACTER_START_ID || newId > CHARACTER_END_ID)
                {
                    Debug.LogWarning($"ID {newId} 超出范围 {CHARACTER_START_ID}-{CHARACTER_END_ID}");
                }
                else if (!IsIdDuplicate(newId, index))
                {
                    character.id = newId;
                }
            }

            character.name = EditorGUILayout.TextField("人物名称", character.name);

            // 描述
            EditorGUILayout.LabelField("人物描述");
            character.description = EditorGUILayout.TextArea(character.description, GUILayout.Height(50));

            // 图标路径
            if (string.IsNullOrEmpty(character.iconPath))
            {
                character.iconPath = $"Characters/{character.id}";
            }
            character.iconPath = EditorGUILayout.TextField("图标路径", character.iconPath);

            // 最大等级（固定100）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("最大等级", GUILayout.Width(80));
            EditorGUILayout.LabelField("100 (固定)", EditorStyles.label);
            character.maxLevel = 100;
            EditorGUILayout.EndHorizontal();

            character.skillIdAtLevel50 = EditorGUILayout.IntField("50级技能ID", character.skillIdAtLevel50);
            character.skillIdAtLevel100 = EditorGUILayout.IntField("100级技能ID", character.skillIdAtLevel100);
            
            // 每十级固定奖励配置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("奖励配置", EditorStyles.boldLabel);
            character.tenLevelGoldReward = EditorGUILayout.IntField("每十级固定金币奖励", character.tenLevelGoldReward);
            
            // 动画参数配置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("动画参数配置", EditorStyles.boldLabel);
            
            // Idle动画（空闲状态）
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Idle - 空闲动画", EditorStyles.boldLabel);
            character.idleColumns = EditorGUILayout.IntField("  图片列数（总帧数）", character.idleColumns);
            character.idleSpeed = EditorGUILayout.FloatField("  播放速度（帧/秒）", character.idleSpeed);
            EditorGUILayout.EndVertical();
            
            // Reel动画（收杆状态）
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Reel - 收杆动画", EditorStyles.boldLabel);
            character.reelColumns = EditorGUILayout.IntField("  图片列数（总帧数）", character.reelColumns);
            character.reelSpeed = EditorGUILayout.FloatField("  播放速度（帧/秒）", character.reelSpeed);
            EditorGUILayout.EndVertical();
            
            // Lazy动画（懒怠状态）
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lazy - 懒怠动画", EditorStyles.boldLabel);
            character.lazyColumns = EditorGUILayout.IntField("  图片列数（总帧数）", character.lazyColumns);
            character.lazySpeed = EditorGUILayout.FloatField("  播放速度（帧/秒）", character.lazySpeed);
            EditorGUILayout.EndVertical();
            
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void AddNewCharacter()
    {
        int newId = GetNextAvailableId();

        CharacterConfig newCharacter = new CharacterConfig
        {
            id = newId,
            name = $"新人物{newId}",
            description = "人物描述",
            iconPath = $"Characters/{newId}",
            maxLevel = 100,
            skillIdAtLevel50 = 0,
            skillIdAtLevel100 = 0,
            tenLevelGoldReward = 500,
            // 默认动画参数
            idleColumns = 15,
            idleSpeed = 15.0f,
            reelColumns = 12,
            reelSpeed = 20.0f,
            lazyColumns = 15,
            lazySpeed = 18.0f
        };

        characterList.Add(newCharacter);
        SaveData();
        LoadData();
        Repaint();
    }

    private int GetNextAvailableId()
    {
        if (characterList.Count == 0) return CHARACTER_START_ID;
        
        // 找到最大的ID
        int maxId = characterList.Max(c => c.id);
        
        // 如果最大ID小于起始ID，则从起始ID开始
        if (maxId < CHARACTER_START_ID) return CHARACTER_START_ID;
        
        // 否则在最大ID基础上加1
        return maxId + 1;
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < characterList.Count; i++)
        {
            if (i != excludeIndex && characterList[i].id == id)
                return true;
        }
        return false;
    }
}