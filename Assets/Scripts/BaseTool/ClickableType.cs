// ==================== ClickableObject.cs ====================
using UnityEngine;

/// <summary>
/// 可点击物体类型
/// </summary>
public enum ClickableType
{
    Player,     // 玩家
    Shop,       // 商店
    NPC,        // NPC
    FishBag,    // 鱼篓
    NestBaitsPlacement,  // 窝料
    Decoration  // 装饰
}

/// <summary>
/// 挂载到Quad物体上，用于标识可点击
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ClickableObject : MonoBehaviour
{
    [Header("点击配置")]
    public ClickableType objectType;
    public string objData;

    private void OnMouseDown()
    {
        ClickManager.Instance.OnObjectClicked(this);
    }
}