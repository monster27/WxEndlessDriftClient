public static partial class CommunicateEvent
{
    #region C2S - 客户端给服务器发消息

    /// <summary>【客户端向服务器发送请求】装备物品请求</summary>
    public const string EVENT_EQUIP_ITEM = "C2S_EVENT_EQUIP_ITEM";

    /// <summary>【客户端向服务器发送请求】装备鱼饵请求</summary>
    public const string EVENT_EQUIP_BAIT = "C2S_EVENT_EQUIP_BAIT";

    /// <summary>【客户端向服务器发送请求】卸下鱼饵请求</summary>
    public const string EVENT_UNEQUIP_BAIT = "C2S_EVENT_UNEQUIP_BAIT";

    /// <summary>【客户端向服务器发送请求】购买商城物品请求</summary>
    public const string EVENT_PURCHASE_MALL_ITEM = "C2S_EVENT_PURCHASE_MALL_ITEM";

    /// <summary>【客户端向服务器发送请求】卖鱼请求</summary>
    public const string EVENT_SELL_FISH_ITEMS = "C2S_EVENT_SELL_FISH_ITEMS";

    /// <summary>【客户端向服务器发送请求】连续模式请求</summary>
    public const string EVENT_CONTINUOUS_MODE_REQUEST = "C2S_EVENT_CONTINUOUS_MODE_REQUEST";

    /// <summary>【客户端向服务器发送请求】商城数据同步请求</summary>
    public const string EVENT_SYNC_MALL_DATA = "C2S_EVENT_SYNC_MALL_DATA";

    /// <summary>【客户端向服务器发送请求】添加物品请求</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <param name="quantity">数量 (int)</param>
    public const string EVENT_ADD_ITEM = "C2S_EVENT_ADD_ITEM";

    /// <summary>【客户端向服务器发送请求】移除物品请求</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <param name="quantity">数量 (int)</param>
    public const string EVENT_REMOVE_ITEM = "C2S_EVENT_REMOVE_ITEM";

    /// <summary>【客户端向服务器发送请求】添加鱼请求</summary>
    /// <param name="fishId">鱼ID (int)</param>
    /// <param name="quantity">数量 (int)</param>
    public const string EVENT_ADD_FISH = "C2S_EVENT_ADD_FISH";

    /// <summary>【客户端向服务器发送请求】背包数据同步请求</summary>
    public const string EVENT_SYNC_INVENTORY = "C2S_EVENT_SYNC_INVENTORY";

    /// <summary>【客户端向服务器发送请求】获取当前场景窝料数量</summary>
    public const string EVENT_GET_CURRENT_SCENE_BAIT_COUNT = "C2S_EVENT_GET_CURRENT_SCENE_BAIT_COUNT";

    /// <summary>【客户端向服务器发送请求】消耗窝料并进入持续模式</summary>
    public const string EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE = "C2S_EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE";

    /// <summary>【客户端向服务器发送请求】增加持续模式时间</summary>
    public const string EVENT_ADD_CONTINUOUS_MODE_TIME = "C2S_EVENT_ADD_CONTINUOUS_MODE_TIME";

    #endregion

    #region S2C - 服务器给客户端发消息

    /// <summary>【服务器向客户端发送通知】金币变更（服务器触发）</summary>
    public const string EVENT_GOLD_CHANGED = "S2C_EVENT_GOLD_CHANGED";

    /// <summary>【服务器向客户端发送通知】客户端金币变更（由ServerManager中转）</summary>
    public const string EVENT_CLIENT_GOLD_CHANGED = "S2C_EVENT_CLIENT_GOLD_CHANGED";

    /// <summary>【服务器向客户端发送通知】人物数据变更</summary>
    /// <param name="level">等级 (int)</param>
    /// <param name="currentExp">当前经验 (int)</param>
    /// <param name="requiredExp">所需经验 (int)</param>
    public const string EVENT_CHARACTER_DATA_CHANGED = "S2C_EVENT_CHARACTER_DATA_CHANGED";

    /// <summary>【服务器向客户端发送通知】装备变更</summary>
    /// <param name="slotType">槽位类型 (int)</param>
    /// <param name="itemId">物品ID (int)</param>
    public const string EVENT_EQUIP_CHANGED = "S2C_EVENT_EQUIP_CHANGED";

    /// <summary>【服务器向客户端发送通知】物品数量变更</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <param name="quantity">数量 (int)</param>
    public const string EVENT_ITEM_QUANTITY_CHANGED = "S2C_EVENT_ITEM_QUANTITY_CHANGED";

    /// <summary>【服务器向客户端发送通知】鱼被捕获</summary>
    /// <param name="fishId">鱼ID (int)</param>
    /// <param name="quantity">数量 (int)</param>
    public const string EVENT_FISH_CAUGHT = "S2C_EVENT_FISH_CAUGHT";

    /// <summary>【服务器向客户端发送通知】连续钓鱼模式变更</summary>
    /// <param name="isActive">是否激活 (bool)</param>
    /// <param name="remainingTime">剩余时间 (float)</param>
    public const string EVENT_CONTINUOUS_MODE_CHANGED = "S2C_EVENT_CONTINUOUS_MODE_CHANGED";

    /// <summary>【服务器向客户端发送通知】技能获取</summary>
    /// <param name="skillId">技能ID (int)</param>
    public const string EVENT_SKILL_OBTAINED = "S2C_EVENT_SKILL_OBTAINED";

    /// <summary>【服务器向客户端发送通知】技能等级变更</summary>
    /// <param name="skillId">技能ID (int)</param>
    /// <param name="level">等级 (int)</param>
    public const string EVENT_SKILL_LEVEL_CHANGED = "S2C_EVENT_SKILL_LEVEL_CHANGED";

    /// <summary>【服务器向客户端发送通知】背包数据同步</summary>
    public const string EVENT_INVENTORY_SYNC = "S2C_EVENT_INVENTORY_SYNC";

    /// <summary>【服务器向客户端发送通知】组件等级变更</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <param name="level">等级 (int)</param>
    public const string EVENT_COMPONENT_LEVEL_CHANGED = "S2C_EVENT_COMPONENT_LEVEL_CHANGED";

    /// <summary>【服务器向客户端发送通知】人物获取状态变更</summary>
    /// <param name="characterId">人物ID (int)</param>
    /// <param name="isObtained">是否已获取 (bool)</param>
    public const string EVENT_CHARACTER_OBTAINED = "S2C_EVENT_CHARACTER_OBTAINED";

    /// <summary>【服务器向客户端发送通知】商城数据变更</summary>
    public const string EVENT_MALL_DATA_CHANGED = "S2C_EVENT_MALL_DATA_CHANGED";

    /// <summary>【服务器向客户端发送通知】钓鱼结果事件</summary>
    public const string EVENT_FISHING_RESULT = "S2C_EVENT_FISHING_RESULT";

    /// <summary>【服务器向客户端发送通知】购买成功</summary>
    public const string EVENT_PURCHASE_SUCCESS = "S2C_EVENT_PURCHASE_SUCCESS";

    /// <summary>【服务器向客户端发送通知】购买失败</summary>
    public const string EVENT_PURCHASE_FAILED = "S2C_EVENT_PURCHASE_FAILED";

    /// <summary>【服务器向客户端发送通知】自动出售状态变更</summary>
    public const string EVENT_AUTO_SELL_STATUS_CHANGED = "S2C_EVENT_AUTO_SELL_STATUS_CHANGED";

    #endregion

    #region UI - UI层请求事件（View层发送到UIManager）

    /// <summary>【UI层请求事件】显示提示信息</summary>
    /// <param name="message">提示消息 (string)</param>
    public const string EVENT_UI_SHOW_TIP = "UI_EVENT_SHOW_TIP";

    /// <summary>【UI层请求事件】显示广告确认框</summary>
    /// <param name="info">广告信息 (string)</param>
    /// <param name="targetId">目标ID (int)</param>
    /// <param name="btnText">按钮文本 (string)</param>
    /// <param name="callbackId">回调ID (string)</param>
    public const string EVENT_UI_SHOW_ADVERTISING = "UI_EVENT_SHOW_ADVERTISING";

    /// <summary>【UI层请求事件】广告请求数据结构</summary>
    public class AdvertisingRequest
    {
        public string info;
        public int targetId;
        public string btnText;
        public string callbackId;
    }

    /// <summary>【UI层请求事件】请求同步金币</summary>
    public const string EVENT_SYNC_GOLD = "UI_EVENT_SYNC_GOLD";

    #endregion

    #region VIEW - View层请求SimulationServer数据事件

    /// <summary>【View层数据请求】请求获取已装备物品ID</summary>
    /// <param name="slotType">槽位类型 (int)</param>
    /// <returns>物品ID (int)</returns>
    public const string EVENT_GET_EQUIPPED_ITEM = "VIEW_EVENT_GET_EQUIPPED_ITEM";

    /// <summary>【View层数据请求】请求获取组件等级</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <returns>等级 (int)</returns>
    public const string EVENT_GET_COMPONENT_LEVEL = "VIEW_EVENT_GET_COMPONENT_LEVEL";

    /// <summary>【View层数据请求】请求获取人物等级</summary>
    /// <param name="characterId">人物ID (int)</param>
    /// <returns>等级 (int)</returns>
    public const string EVENT_GET_CHARACTER_LEVEL = "VIEW_EVENT_GET_CHARACTER_LEVEL";

    /// <summary>【View层数据请求】请求检查技能是否已获取</summary>
    /// <param name="skillId">技能ID (int)</param>
    /// <returns>是否已获取 (bool)</returns>
    public const string EVENT_IS_SKILL_OBTAINED = "VIEW_EVENT_IS_SKILL_OBTAINED";

    /// <summary>【View层数据请求】请求检查人物是否已获取</summary>
    /// <param name="characterId">人物ID (int)</param>
    /// <returns>是否已获取 (bool)</returns>
    public const string EVENT_IS_CHARACTER_OBTAINED = "VIEW_EVENT_IS_CHARACTER_OBTAINED";

    /// <summary>【View层数据请求】请求获取背包数据</summary>
    /// <returns>物品字典 (Dictionary<int, int>)</returns>
    public const string EVENT_GET_INVENTORY = "VIEW_EVENT_GET_INVENTORY";

    /// <summary>【View层数据请求】请求检查物品是否已装备</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <returns>是否已装备 (bool)</returns>
    public const string EVENT_IS_ITEM_EQUIPPED = "VIEW_EVENT_IS_ITEM_EQUIPPED";

    /// <summary>【View层数据请求】请求获取商城物品列表</summary>
    /// <returns>商城物品字典 (Dictionary<int, MallItemData>)</returns>
    public const string EVENT_GET_MALL_ITEMS = "VIEW_EVENT_GET_MALL_ITEMS";

    /// <summary>【View层数据请求】请求获取单个商城物品</summary>
    /// <param name="itemId">物品ID (int)</param>
    /// <returns>商城物品数据 (MallItemData)</returns>
    public const string EVENT_GET_MALL_ITEM = "VIEW_EVENT_GET_MALL_ITEM";

    /// <summary>【View层数据请求】请求获取玩家金币</summary>
    /// <returns>金币数量 (int)</returns>
    public const string EVENT_GET_GOLD = "VIEW_EVENT_GET_GOLD";

    /// <summary>【View层数据请求】请求获取鱼篓容量</summary>
    /// <returns>容量 (int)</returns>
    public const string EVENT_GET_FISH_BAG_CAPACITY = "VIEW_EVENT_GET_FISH_BAG_CAPACITY";

    /// <summary>【View层数据请求】请求获取鱼篓数据</summary>
    /// <returns>鱼字典 (Dictionary<int, int>)</returns>
    public const string EVENT_GET_FISH_INVENTORY = "VIEW_EVENT_GET_FISH_INVENTORY";

    /// <summary>【View层数据请求】请求检查是否在连续模式</summary>
    /// <returns>是否在连续模式 (bool)</returns>
    public const string EVENT_IS_IN_CONTINUOUS_MODE = "VIEW_EVENT_IS_IN_CONTINUOUS_MODE";

    /// <summary>【View层数据请求】请求获取连续模式剩余时间</summary>
    /// <returns>剩余时间 (float)</returns>
    public const string EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME = "VIEW_EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME";

    /// <summary>【View层数据请求】请求获取玩家数据</summary>
    /// <returns>玩家数据 (PlayerData)</returns>
    public const string EVENT_GET_PLAYER_DATA = "VIEW_EVENT_GET_PLAYER_DATA";

    #endregion

    #region MANAGER - Manager层发送到View的数据事件

    /// <summary>【Manager层数据推送】时间段变更</summary>
    public const string EVENT_TIME_SLOT_CHANGED = "MANAGER_EVENT_TIME_SLOT_CHANGED";

    /// <summary>【Manager层数据推送】天气变更</summary>
    public const string EVENT_WEATHER_CHANGED = "MANAGER_EVENT_WEATHER_CHANGED";

    #endregion

    #region OTHER - 其他事件

    /// <summary>【其他事件】显示窝料倒计时到指定位置</summary>
    public const string EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION = "OTHER_EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION";

    /// <summary>【其他事件】客户端时间段变更</summary>
    public const string EVENT_CLIENT_TIME_SLOT_CHANGED = "OTHER_EVENT_CLIENT_TIME_SLOT_CHANGED";

    /// <summary>【其他事件】客户端天气变更</summary>
    public const string EVENT_CLIENT_WEATHER_CHANGED = "OTHER_EVENT_CLIENT_WEATHER_CHANGED";

    #endregion
}