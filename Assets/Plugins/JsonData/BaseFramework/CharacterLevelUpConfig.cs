using UnityEngine;
using System;
using System.Collections.Generic;

// 引入SharedModels命名空间以使用统一的数据类型
using SharedModels;

namespace JsonData
{
    /// <summary>
    /// 人物升级配置扩展类（Unity专用）
    /// 提供Unity相关的资源加载和调试功能
    /// 数据类型定义请参见 SharedModels/CharacterLevelUpConfig.cs
    /// </summary>
    public static class CharacterLevelUpConfigExtensions
    {
        /// <summary>
        /// 从Unity Resources加载升级配置
        /// </summary>
        public static CharacterLevelUpConfig LoadFromResources(string path = "JsonData/BaseFramework/levelup_exp")
        {
            TextAsset textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogError($"[CharacterLevelUpConfig] 加载失败: {path}");
                return null;
            }
            return ParseFromJson(textAsset.text);
        }

        /// <summary>
        /// 从JSON字符串解析（带Unity调试日志）
        /// </summary>
        public static CharacterLevelUpConfig ParseFromJson(string jsonString)
        {
            CharacterLevelUpConfig config = new CharacterLevelUpConfig();
            System.Text.StringBuilder debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("[CharacterLevelUpConfig] ParseFromJson 解析配置:");

            try
            {
                CharacterLevelUpConfig.JsonWrapper wrapper = JsonUtility.FromJson<CharacterLevelUpConfig.JsonWrapper>(jsonString);
                if (wrapper != null)
                {
                    debugInfo.AppendLine($"  levelRangeExpList.Count={wrapper.levelRangeExpList.Count}");
                    foreach (var item in wrapper.levelRangeExpList)
                    {
                        debugInfo.AppendLine($"    区间 {item.rangeKey}: {item.expRequired} 经验");
                        config.levelUpExpRequirements[item.rangeKey] = item.expRequired;
                    }
                }
                Debug.Log(debugInfo.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterLevelUpConfig] ParseFromJson failed: {e.Message}");
            }

            return config;
        }

        /// <summary>
        /// 打印配置调试信息
        /// </summary>
        public static void PrintDebugInfo(this CharacterLevelUpConfig config)
        {
            System.Text.StringBuilder debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine("[CharacterLevelUpConfig] 配置信息:");
            debugInfo.AppendLine($"  配置版本: {config.configVersion}");
            debugInfo.AppendLine($"  最后更新: {config.lastUpdateTime}");
            debugInfo.AppendLine($"  等级区间数量: {config.levelUpExpRequirements.Count}");
            
            foreach (var kvp in config.levelUpExpRequirements)
            {
                debugInfo.AppendLine($"    {kvp.Key}: {kvp.Value} 经验");
            }
            
            Debug.Log(debugInfo.ToString());
        }
    }
}