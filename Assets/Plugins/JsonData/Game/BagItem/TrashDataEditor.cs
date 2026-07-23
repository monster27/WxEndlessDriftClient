// ==================== TrashDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public class TrashDataEditor : BaseDataEditor<TrashData>
{
    private float idWidth = 60;
    private float nameWidth = 150;
    private float weightWidth = 80;
    private float weightValueWidth = 80;
    private float experienceWidth = 80;
    private float actionWidth = 80;

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/9001_垃圾")]
    public static void ShowWindow()
    {
        TrashDataEditor window = GetWindow<TrashDataEditor>("垃圾数据编辑器");
        window.minSize = new Vector2(600, 400);
        window.relativePath = "Resources/JsonData/Game/BagItem/trash.json";
        window.LoadData();
        window.Show();
    }

    public TrashDataEditor() : base("Resources/JsonData/Game/BagItem/trash.json") { }

    protected override void LoadData()
    {
        dataList.Clear();

        if (File.Exists(FullPath))
        {
            string json = File.ReadAllText(FullPath);
            TrashListWrapper wrapper = JsonUtility.FromJson<TrashListWrapper>(json);
            if (wrapper != null && wrapper.trashList != null)
            {
                dataList = wrapper.trashList;
                Debug.Log($"[TrashDataEditor] 加载了 {dataList.Count} 条垃圾数据");
            }
        }
        else
        {
            Debug.Log("[TrashDataEditor] 未找到垃圾数据文件，将创建新文件");
        }

        Repaint();
    }

    protected override void SaveData()
    {
        TrashListWrapper wrapper = new TrashListWrapper
        {
            trashList = dataList
        };

        string json = JsonUtility.ToJson(wrapper, true);

        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FullPath, json);
        Debug.Log($"[TrashDataEditor] 保存了 {dataList.Count} 条垃圾数据");
        AssetDatabase.Refresh();
    }

    protected override void AddNewItem()
    {
        TrashData newTrash = new TrashData();
        newTrash.id = dataList.Count > 0 ? dataList[dataList.Count - 1].id + 1 : 3001;
        newTrash.name = "新垃圾";
        newTrash.weight = 1.0f;
        newTrash.weightValue = 10;
        newTrash.experience = 5;
        dataList.Add(newTrash);
        Repaint();
    }

    protected override void DrawDataTable()
    {
        if (dataList.Count == 0)
        {
            EditorGUILayout.LabelField("暂无垃圾数据", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 绘制表头
        EditorGUILayout.BeginHorizontal("box");
        DrawResizableColumn("ID", ref idWidth, "id");
        DrawResizableColumn("名称", ref nameWidth, "name");
        DrawResizableColumn("重量", ref weightWidth, "weight");
        DrawResizableColumn("权重", ref weightValueWidth, "weightValue");
        DrawResizableColumn("经验", ref experienceWidth, "experience");
        DrawResizableColumn("操作", ref actionWidth, "action");
        EditorGUILayout.EndHorizontal();

        // 绘制数据行
        for (int i = 0; i < dataList.Count; i++)
        {
            DrawDataRow(i);
        }

        EditorGUILayout.EndScrollView();

        // 保存按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("保存数据", GUILayout.Height(30), GUILayout.Width(150)))
        {
            SaveData();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    protected override void DrawDataRow(int index)
    {
        TrashData trash = dataList[index];

        EditorGUILayout.BeginHorizontal("box");

        trash.id = EditorGUILayout.IntField(trash.id, GUILayout.Width(idWidth));
        trash.name = EditorGUILayout.TextField(trash.name, GUILayout.Width(nameWidth));
        trash.weight = EditorGUILayout.FloatField(trash.weight, GUILayout.Width(weightWidth));
        trash.weightValue = EditorGUILayout.IntField(trash.weightValue, GUILayout.Width(weightValueWidth));
        trash.experience = EditorGUILayout.IntField(trash.experience, GUILayout.Width(experienceWidth));

        if (GUILayout.Button("删除", GUILayout.Width(actionWidth)))
        {
            dataList.RemoveAt(index);
            Repaint();
            return;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        DrawToolbar("垃圾数据", dataList.Count);
        DrawDataTable();

        HandleColumnResize();
        HandleMouseUp();

        EditorGUILayout.EndVertical();
    }

    [Serializable]
    private class TrashListWrapper
    {
        public List<TrashData> trashList;
    }
}
#endif
