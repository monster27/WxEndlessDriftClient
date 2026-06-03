using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 模拟服务器 - 游戏核心管理器
/// 负责协调各子系统，管理钓鱼游戏的主要流程
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region 子管理器

    /// <summary>时间槽服务器管理器</summary>
    private TimeSlotServerManager timeSlotManager;
    /// <summary>天气服务器管理器</summary>
    private WeatherServerManager weatherManager;
    /// <summary>场景鱼库服务器管理器</summary>
    private SceneFishPoolServerManager sceneFishPoolManager;
    /// <summary>背包服务器管理器</summary>
    private PlayerInventoryServerManager inventoryManager;
    /// <summary>钓鱼算法服务器管理器</summary>
    private FishingAlgorithmServerManager fishingAlgorithmManager;
    /// <summary>自动钓鱼服务器管理器</summary>
    private AutoFishingServerManager autoFishingManager;
    /// <summary>玩家能力管理器</summary>
    private PlayerFishingAbilityManager playerAbilityManager;
    /// <summary>商城服务器管理器</summary>
    private MallServerManager mallManager;
    /// <summary>人物服务器管理器</summary>
    private CharacterServerManager characterServerManager;

    #endregion

    #region 计时器（替代Invoke）

    /// <summary>闲置动画延迟计时器</summary>
    private float idleAnimationTimer = 0f;
    private bool isWaitingForIdleAnimation = false;

    /// <summary>钓鱼动画序列计时器</summary>
    private float fishingAnimationTimer = 0f;
    private int fishingAnimationStep = 0;
    private bool isInFishingAnimation = false;
    
    /// <summary>检测到的鱼（拉杆前知道）</summary>
    private FishData detectedFish = null;

    /// <summary>当前钓鱼结果</summary>
    private FishingResult currentFishingResult = null;

    #endregion

    #region 属性

    /// <summary>
    /// 当前时间段状态
    /// </summary>
    public TimeStatus CurrentTimeStatus => timeSlotManager?.CurrentTimeStatus ?? TimeStatus.Daytime;

    /// <summary>
    /// 当前天气ID
    /// </summary>
    public int CurrentWeatherId => weatherManager?.CurrentWeatherId ?? 0;

    /// <summary>
    /// 当前场景ID
    /// </summary>
    public int CurrentSceneId => sceneFishPoolManager?.CurrentSceneId ?? 1;

    /// <summary>
    /// 背包是否已初始化
    /// </summary>
    public bool isInventoryInitialized => inventoryManager?.IsInventoryInitialized ?? false;

    /// <summary>
    /// 自动钓鱼服务器管理器
    /// </summary>
    public AutoFishingServerManager AutoFishingManager => autoFishingManager;

    /// <summary>
    /// 玩家能力管理器
    /// </summary>
    public PlayerFishingAbilityManager PlayerAbilityManager => playerAbilityManager;

    /// <summary>
    /// 是否正在自动钓鱼
    /// </summary>
    public bool IsAutoFishing => autoFishingManager?.IsAutoFishing ?? false;

    /// <summary>
    /// 上次挣扎时间
    /// </summary>
    public float LastStruggleTime { get; private set; } = 0f;

    /// <summary>
    /// 是否在钓鱼动画中
    /// </summary>
    public bool IsInFishingAnimation => isInFishingAnimation;

    /// <summary>
    /// 当前钓鱼结果
    /// </summary>
    public FishingResult CurrentFishingResult => currentFishingResult;

    /// <summary>
    /// 背包物品种类数量
    /// </summary>
    public int InventoryCount => inventoryManager?.InventoryCount ?? 0;

    /// <summary>
    /// 鱼篓物品种类数量
    /// </summary>
    public int FishCount => inventoryManager?.FishCount ?? 0;

    /// <summary>
    /// 鱼篓物品总数量（堆叠之和）
    /// </summary>
    public int TotalFishCount => inventoryManager?.GetTotalFishCount() ?? 0;

    /// <summary>
    /// 玩家金币数量
    /// </summary>
    public int Gold { get; private set; } = 4;

    /// <summary>
    /// 新钓到的鱼ID集合（用于UI显示新鱼标记）
    /// </summary>
    private HashSet<int> newlyCaughtFish = new HashSet<int>();

    /// <summary>
    /// 标记鱼为新钓到的鱼
    /// </summary>
    /// <param name="fishId">鱼的物品ID</param>
    public void MarkAsNewlyCaughtFish(int fishId)
    {
        newlyCaughtFish.Add(fishId);
        Debug.LogFormat("<color=orange>[SimulationServer] 标记新鱼: ID={0}</color>", fishId);
    }

    /// <summary>
    /// 清除新鱼标记
    /// </summary>
    /// <param name="fishId">鱼的物品ID</param>
    public void ClearNewlyCaughtMark(int fishId)
    {
        if (newlyCaughtFish.Contains(fishId))
        {
            newlyCaughtFish.Remove(fishId);
            Debug.LogFormat("<color=orange>[SimulationServer] 清除新鱼标记: ID={0}</color>", fishId);
        }
    }

    /// <summary>
    /// 判断是否是 Newly Caught鱼
    /// </summary>
    /// <param name="fishId">鱼的物品ID</param>
    /// <returns>是否是新钓到的鱼</returns>
    public bool IsNewlyCaughtFish(int fishId)
    {
        return newlyCaughtFish.Contains(fishId);
    }

    /// <summary>
    /// 清除所有新鱼标记
    /// </summary>
    public void ClearAllNewlyCaughtMarks()
    {
        newlyCaughtFish.Clear();
        Debug.LogFormat("<color=orange>[SimulationServer] 清除所有新鱼标记</color>");
    }

    /// <summary>
    /// 售卖鱼篓中的物品
    /// </summary>
    /// <param name="itemIds">要售卖的物品ID列表</param>
    /// <param name="totalPrice">总价</param>
    public void SellFishItems(List<int> itemIds, int totalPrice)
    {
        if (itemIds == null || itemIds.Count == 0)
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 售卖物品列表为空</color>");
            return;
        }

        int removedCount = 0;
        foreach (int itemId in itemIds)
        {
            if (inventoryManager != null)
            {
                // 只卖出1个物品，而不是所有同ID物品
                int quantity = inventoryManager.GetFishQuantity(itemId);
                if (quantity > 0)
                {
                    inventoryManager.RemoveFish(itemId, 1);
                    ClearNewlyCaughtMark(itemId);
                    removedCount++;
                    Debug.LogFormat("<color=orange>[SimulationServer] 移除鱼篓物品: ID={0}, 数量=1</color>", itemId);
                }
            }
        }

        if (removedCount > 0)
        {
            AddGold(totalPrice);
            Debug.LogFormat("<color=orange>[SimulationServer] 售卖成功: 物品数量={0}, 获得金币={1}</color>", removedCount, totalPrice);

            // 卖鱼后检查鱼篓状态，如果之前满了现在有空间，恢复自动钓鱼和动画
            bool isFishBagFull = IsFishBagFull();
            if (!isFishBagFull)
            {
                // 鱼篓现在有空间了
                if (autoFishingManager != null && !autoFishingManager.IsAutoFishing)
                {
                    // 恢复自动钓鱼
                    autoFishingManager.StartAutoFishing();
                    Debug.LogFormat("<color=orange>[SimulationServer] 卖鱼后鱼篓有空间，恢复自动钓鱼</color>");
                }

                ServerManager.Instance?.NotifyPlayIdleAnimation();
                Debug.LogFormat("<color=orange>[SimulationServer] 卖鱼后通知客户端切换到Idle动画</color>");
            }

            ServerManager.Instance?.NotifySyncInventoryFromServer();
            ServerManager.Instance?.NotifyRefreshUI();
        }
        else
        {
            Debug.LogWarningFormat("<color=orange>[SimulationServer] 没有可售卖的物品</color>");
        }
    }

    /// <summary>
    /// 鱼篓容量
    /// </summary>
    public int FishBagCapacity => inventoryManager?.FishBagCapacity ?? 20;

    /// <summary>
    /// 鱼篓是否已满
    /// </summary>
    public bool IsFishBagFull() => inventoryManager?.IsFishBagFull() ?? false;

    /// <summary>
    /// 最小钓鱼间隔
    /// </summary>
    public float MinFishingInterval => autoFishingManager?.MinFishingInterval ?? 5f;

    /// <summary>
    /// 距离下次钓鱼的剩余时间
    /// </summary>
    public float TimeUntilNextFishing => autoFishingManager?.GetTimeUntilNextFishing() ?? 0f;

    /// <summary>
    /// 当前钓鱼模式
    /// </summary>
    public AutoFishingServerManager.FishingMode CurrentFishingMode => autoFishingManager?.CurrentFishingMode ?? AutoFishingServerManager.FishingMode.Normal;

    /// <summary>
    /// 是否处于停滞状态
    /// </summary>
    public bool IsFishingPaused => autoFishingManager?.IsPaused ?? false;

    /// <summary>
    /// 停滞剩余时间
    /// </summary>
    public float PauseRemainingTime => autoFishingManager?.PauseRemainingTime ?? 0f;

    /// <summary>
    /// 连续钓鱼停滞时长
    /// </summary>
    public float ContinuousPauseDuration
    {
        get => autoFishingManager?.ContinuousPauseDuration ?? 0.5f;
        set
        {
            if (autoFishingManager != null)
                autoFishingManager.ContinuousPauseDuration = value;
        }
    }

    /// <summary>
    /// 切换钓鱼模式
    /// </summary>
    public void ToggleFishingMode()
    {
        autoFishingManager?.ToggleFishingMode();
    }

    /// <summary>
    /// 设置钓鱼模式
    /// </summary>
    public void SetFishingMode(AutoFishingServerManager.FishingMode mode)
    {
        autoFishingManager?.SetFishingMode(mode);
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 钓鱼结果数据结构
    /// </summary>
    public class FishingResult
    {
        /// <summary>第一个ID：通过GetRandomFishByScene获取的物品ID（检测到的鱼）</summary>
        public int detectedFishId;
        /// <summary>第二个ID：玩家实际钓到的物品ID（如果是垃圾则是垃圾ID，否则与第一个ID相同）</summary>
        public int actualItemId;
        /// <summary>是否成功（非垃圾）</summary>
        public bool isSuccess;
        /// <summary>检测到的鱼数据（玩家可能钓上来的）</summary>
        public FishData detectedFish;
        /// <summary>实际钓上来的物品数据</summary>
        public FishData actualFish;
        /// <summary>是否是垃圾</summary>
        public bool isTrash;
        /// <summary>挣扎时间（秒）- 用于拉杆动画时长</summary>
        public float struggleTime;
    }

    #endregion
}