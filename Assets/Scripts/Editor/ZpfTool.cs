#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;


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

}
#endif