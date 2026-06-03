// ==================== BaseDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class BaseDataEditor<T> : EditorWindow where T : class, new()
{
    protected List<T> dataList = new List<T>();
    [System.NonSerialized]
    protected Vector2 scrollPosition;
    [System.NonSerialized]
    protected int selectedIndex = -1;

    // 表头宽度
    [System.NonSerialized]
    protected Dictionary<string, float> columnWidths = new Dictionary<string, float>();
    [System.NonSerialized]
    protected bool isResizing = false;
    [System.NonSerialized]
    protected string resizingCol = null;
    [System.NonSerialized]
    protected float startMouseX = 0;
    [System.NonSerialized]
    protected float startWidth = 0;

    protected string relativePath;

    protected BaseDataEditor(string relativePath)
    {
        this.relativePath = relativePath;
    }

    protected string FullPath => Path.Combine(Application.dataPath, relativePath);

    protected virtual void LoadData() { }
    protected virtual void SaveData() { }
    protected virtual void DrawDataTable() { }
    protected virtual void DrawDataRow(int index) { }

    protected void DrawToolbar(string title, int count)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadData();
        if (GUILayout.Button("新增", EditorStyles.toolbarButton, GUILayout.Width(60))) AddNewItem();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    protected virtual void AddNewItem() { }

    protected void DrawResizableColumn(string title, ref float width, string colKey)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(width));
        EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);

        // 绘制调整手柄
        Rect handleRect = new Rect(rect.x + rect.width - 3, rect.y, 5, rect.height);
        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);

        if (Event.current.type == EventType.MouseDown && handleRect.Contains(Event.current.mousePosition))
        {
            isResizing = true;
            resizingCol = colKey;
            startMouseX = Event.current.mousePosition.x;
            startWidth = width;
            Event.current.Use();
            EditorGUIUtility.SetWantsMouseJumping(1);
        }
    }

    protected void HandleColumnResize()
    {
        if (isResizing && Event.current != null)
        {
            float delta = Event.current.mousePosition.x - startMouseX;
            float newWidth = startWidth + delta;
            if (newWidth > 30)
            {
                // 这里不需要更新字典，因为子类会通过引用传递更新宽度
                Repaint();
            }
        }
    }

    protected void HandleMouseUp()
    {
        if (Event.current.type == EventType.MouseUp && isResizing)
        {
            isResizing = false;
            resizingCol = null;
            EditorGUIUtility.SetWantsMouseJumping(0);
        }
    }
}
#endif