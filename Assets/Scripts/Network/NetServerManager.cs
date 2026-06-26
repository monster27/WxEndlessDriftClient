using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Utils;
using Logger = Utils.Logger;
using SharedModels;
using System;

/// <summary>
/// NetServerManager - 网络服务器管理器
/// 已按功能模块拆分为多个 partial class 文件：
/// - NetServerManager.Main.cs       核心框架（生命周期、连接管理、基础网络请求）
/// - NetServerManager.Heartbeat.cs  心跳管理
/// - NetServerManager.PlayerData.cs 玩家数据管理
/// - NetServerManager.Equipment.cs  装备系统
/// - NetServerManager.Fishing.cs    钓鱼系统
/// - NetServerManager.ContinuousMode.cs 连续钓鱼模式
/// - NetServerManager.Shop.cs       商城系统
/// - NetServerManager.Weather.cs    天气和时间系统
/// - NetServerManager.Events.cs     事件注册
/// - NetServerManager.Skill.cs      技能系统
/// </summary>
public partial class NetServerManager
{
    // 所有代码已拆分到各 partial class 文件中
    // 此文件仅保留类声明，作为入口文件
}