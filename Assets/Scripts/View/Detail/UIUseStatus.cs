using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 使用状态组件 - 管理物品/人物的 已拥有已装备 / 已拥有未装备 / 未拥有 三种状态
/// </summary>
public class UIUseStatus : MonoBehaviour
{
    [Header("状态对象")]
    public GameObject ownerUseObj;      // 已拥有已装备
    public GameObject ownerUnUseObj;    // 已拥有未装备
    public GameObject lockedObj;        // 未拥有/未解锁

    [Header("状态按钮（可选）")]
    public Button unlockBtn;            // 解锁按钮（锁定状态显示）
    public Button equipBtn;             // 装备按钮（未装备状态显示）

    public enum Status
    {
        OwnerUse,       // 已拥有已装备
        OwnerUnUse,     // 已拥有未装备
        Locked          // 未拥有/未解锁
    }

    private Status _currentStatus = Status.Locked;
    public Status CurrentStatus => _currentStatus;

    /// <summary>
    /// 设置状态
    /// </summary>
    public void SetStatus(Status status)
    {
        _currentStatus = status;

        if (ownerUseObj != null)
            ownerUseObj.SetActive(status == Status.OwnerUse);

        if (ownerUnUseObj != null)
            ownerUnUseObj.SetActive(status == Status.OwnerUnUse);

        if (lockedObj != null)
            lockedObj.SetActive(status == Status.Locked);

        // 自动控制解锁按钮显示
        if (unlockBtn != null)
            unlockBtn.gameObject.SetActive(status == Status.Locked);
    }

    /// <summary>
    /// 设置状态（通过 bool 参数）
    /// </summary>
    public void SetStatus(bool hasItem, bool isEquipped)
    {
        Status status;
        if (isEquipped)
            status = Status.OwnerUse;
        else if (hasItem)
            status = Status.OwnerUnUse;
        else
            status = Status.Locked;

        SetStatus(status);
    }

    /// <summary>
    /// 设置状态（通过 EquipState 枚举）
    /// </summary>
    public void SetStatus(EquipState state)
    {
        Status status = state switch
        {
            EquipState.OwnerUse => Status.OwnerUse,
            EquipState.OwnerUnUse => Status.OwnerUnUse,
            EquipState.Locked => Status.Locked,
            _ => Status.Locked
        };
        SetStatus(status);
    }

    /// <summary>
    /// 判断当前是否处于已装备状态
    /// </summary>
    public bool IsEquipped => _currentStatus == Status.OwnerUse;

    /// <summary>
    /// 判断当前是否已拥有
    /// </summary>
    public bool IsOwned => _currentStatus == Status.OwnerUse || _currentStatus == Status.OwnerUnUse;
}
