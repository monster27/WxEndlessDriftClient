// ========================================================
// 公共类型定义文件
// 注意：以下类型定义已迁移到 SharedModels 命名空间
// 此文件仅保留兼容用的空类，用于避免编译错误
// ========================================================

using UnityEngine;
using System.Collections.Generic;
using SharedModels;

// ========================================================
// 以下类型已迁移到 SharedModels 命名空间
// 请使用 using SharedModels; 来引用这些类型
// - EquipmentSlotType (SharedModels/EquipmentSlotType.cs)
// - MallItemData (SharedModels/MallItemData.cs)
// - MallCategoryData (SharedModels/MallData.cs)
// - MallConfigData (SharedModels/MallData.cs)
// - FishingResult (SharedModels/FishingResult.cs)
// ========================================================

/// <summary>
/// 自动钓鱼管理器（仅用于兼容旧代码引用）
/// </summary>
public class AutoFishingManager
{
    public void ResetNotificationState() { }
}

/// <summary>
/// 人物服务器管理器（仅用于兼容旧代码引用）
/// 实际的 CharacterConfig 类型定义在 Plugins/JsonData/BaseFramework/CharacterConfig.cs 中
/// </summary>
public class CharacterServerManager
{
    private static CharacterServerManager instance;
    public static CharacterServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new CharacterServerManager();
            }
            return instance;
        }
    }
    
    public void Initialize() { }
    public void EquipCharacter(int characterId) { }
    public List<CharacterConfig> GetAllCharacterConfigs() { return new List<CharacterConfig>(); }
}

///// <summary>
///// 空的SimulationServer类（仅用于兼容旧代码引用）
///// 实际功能已迁移到网络服务器
///// </summary>
//public class SimulationServer
//{
//    public static SimulationServer Instance => null;
    
//    public FishingResult CurrentFishingResult => null;
//    public AutoFishingManager AutoFishingManager => null;
    
//    public void Initialize() { }
//    public void Update(float deltaTime) { }
//    public bool IsRunning() => false;
//    public void AddItem(int itemId, int quantity) { }
//    public void RemoveItem(int itemId, int quantity) { }
//    public void AddFish(int fishId, int quantity) { }
//    public bool IsFishBagFull() => false;
//    public void ProcessHeartbeat(Dictionary<string, object> data) { }
//}
