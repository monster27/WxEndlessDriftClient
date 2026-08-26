using UnityEngine;

/// <summary>
/// 鱼饵组件 - 支持对象池复用
/// </summary>
public class FishTankBaitCtrl : MonoBehaviour
{
    private FishTankManager _manager;   // 管理器引用
    private Rect _totalRect;            // 全屏区域矩形
    private float _fallSpeed;           // 下落速度
    private float _scale;               // 鱼饵缩放
    private bool _isFalling = true;     // 是否正在下落
    private bool _isTriggered = false;  // 是否已被触发（被吃掉或移除）
    private bool _isActive = false;     // 是否激活（从池中取出）

    /// <summary>
    /// 初始化鱼饵组件（由Manager调用）
    /// </summary>
    public void Init(FishTankManager manager, Rect totalRect, float fallSpeed, float scale)
    {
        _manager = manager;
        _totalRect = totalRect;
        _fallSpeed = fallSpeed;
        _scale = scale;
        _isFalling = true;
        _isTriggered = false;
        _isActive = false;

        transform.localScale = Vector3.one * _scale;

        if (_manager != null && _manager.EnableDebugLog)
            Z_Logger.Log($"[FishTankBaitComponent] 初始化完成");
    }

    /// <summary>
    /// 重置鱼饵到指定位置（从池中取出时调用）
    /// </summary>
    public void ResetBait(Vector3 position)
    {
        transform.position = position;
        _isFalling = true;
        _isTriggered = false;
        _isActive = true;

        if (_manager != null && _manager.EnableDebugLog)
            Z_Logger.Log($"[FishTankBaitComponent] 重置鱼饵到位置: ({position.x:F2}, {position.y:F2})");
    }

    /// <summary>
    /// 停用鱼饵（归还到池中时调用）
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _isTriggered = true;
    }

    /// <summary>
    /// 更新鱼饵状态（每帧由Manager调用）
    /// </summary>
    public void UpdateBait()
    {
        if (_isTriggered || !_isActive) return;

        if (_isFalling)
        {
            Vector3 pos = transform.position;
            pos.y -= _fallSpeed * Time.deltaTime;

            // 触底停止下落
            if (pos.y <= _totalRect.yMin + 0.3f)
            {
                pos.y = _totalRect.yMin + 0.3f;
                _isFalling = false;
                if (_manager != null && _manager.EnableDebugLog)
                    Z_Logger.Log("[FishTankBaitComponent] 鱼饵停止移动");
            }

            transform.position = pos;
        }
    }

    public bool IsActive => _isActive;
    public bool IsFalling => _isFalling;
}
