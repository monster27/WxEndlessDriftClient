using UnityEngine;

/// <summary>
/// 钓鱼能力基类
/// 所有钓鱼能力的抽象基类，定义能力的基本属性和生命周期
/// </summary>
public abstract class FishingAbilityBase
{
    /// <summary>
    /// 能力ID
    /// </summary>
    public int Id { get; protected set; }
    
    /// <summary>
    /// 能力名称
    /// </summary>
    public string Name { get; protected set; }
    
    /// <summary>
    /// 能力描述
    /// </summary>
    public string Description { get; protected set; }
    
    /// <summary>
    /// 能力数值（如加成百分比、倍率等）
    /// </summary>
    public float Value { get; protected set; }
    
    /// <summary>
    /// 能力持续时间（0表示永久生效）
    /// </summary>
    public float Duration { get; protected set; }
    
    /// <summary>
    /// 剩余持续时间
    /// </summary>
    public float RemainingDuration { get; protected set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">能力ID</param>
    /// <param name="name">能力名称</param>
    /// <param name="value">能力数值</param>
    /// <param name="duration">持续时间（0表示永久）</param>
    protected FishingAbilityBase(int id, string name, float value, float duration = 0f)
    {
        Id = id;
        Name = name;
        Value = value;
        Duration = duration;
        RemainingDuration = 0;
    }
    
    /// <summary>
    /// 检查能力是否激活
    /// 永久能力始终返回true，限时能力检查剩余时间
    /// </summary>
    /// <returns>是否激活</returns>
    public virtual bool IsActive()
    {
        return Duration == 0 || RemainingDuration > 0;
    }
    
    /// <summary>
    /// 激活能力
    /// </summary>
    /// <param name="duration">持续时间（默认使用能力自带的持续时间）</param>
    public virtual void Activate(float duration = 0f)
    {
        RemainingDuration = duration > 0 ? duration : Duration;
    }
    
    /// <summary>
    /// 更新能力状态（每帧调用）
    /// 处理限时能力的倒计时
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public virtual void Update(float deltaTime)
    {
        if (Duration > 0 && RemainingDuration > 0)
        {
            RemainingDuration -= deltaTime;
            if (RemainingDuration < 0) RemainingDuration = 0;
        }
    }
    
    /// <summary>
    /// 应用能力效果到计算属性
    /// 子类必须实现此方法来定义具体的能力效果
    /// </summary>
    /// <param name="stats">钓鱼计算属性</param>
    public abstract void Apply(FishingCalculatedStats stats);
}