// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SimulationServer GUI控制面板部分
/// </summary>
public partial class SimulationServer : MonoBehaviour
{
    #region GUI控制面板

#if UNITY_EDITOR
    /// <summary>
    /// GUI面板是否展开
    /// </summary>
    private bool isGUIPanelExpanded = false;

    /// <summary>
    /// GUI面板折叠按钮的位置和大小
    /// </summary>
    private Rect toggleButtonRect = new Rect(10, 10, 60, 30);

    /// <summary>
    /// GUI面板的位置和大小
    /// </summary>
    private Rect panelRect = new Rect(10, 45, 440, 1450);

    /// <summary>
    /// 当前钓鱼动画状态
    /// </summary>
    private FishingAnimationState currentAnimationState = FishingAnimationState.Idle;

    /// <summary>
    /// 动画状态枚举
    /// </summary>
    public enum FishingAnimationState
    {
        Idle,           // 空闲
        Reeling,        // 收线
        Success,        // 成功钓到
        Trash,          // 钓到垃圾
        Lazy            // 懒惰动作(待机)
    }

    /// <summary>
    /// 动画状态名称映射
    /// </summary>
    private Dictionary<FishingAnimationState, string> animationStateNames = new Dictionary<FishingAnimationState, string>()
    {
        { FishingAnimationState.Idle, "空闲" },
        { FishingAnimationState.Reeling, "收线中..." },
        { FishingAnimationState.Success, "✅ 钓到鱼！" },
        { FishingAnimationState.Trash, "❌ 钓到垃圾" },
        { FishingAnimationState.Lazy, "😴 等待中..." } 
    };

    /// <summary>
    /// GUI绘制方法
    /// </summary>
    private void OnGUI()
    {
        // 绘制折叠/展开按钮
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.8f);
        if (GUI.Button(toggleButtonRect, isGUIPanelExpanded ? "▼ 收起" : "▲ 展开"))
        {
            isGUIPanelExpanded = !isGUIPanelExpanded;
        }
        GUI.backgroundColor = Color.white;

        // 如果面板展开，绘制完整内容
        if (isGUIPanelExpanded)
        {
            // 绘制面板背景
            GUI.DrawTexture(panelRect, MakeTex(panelRect.width, panelRect.height, new Color(0.15f, 0.15f, 0.15f, 0.95f)));

            GUILayout.BeginArea(panelRect);

            // 标题区域 - 金色高亮
            GUI.contentColor = new Color(1f, 0.7f, 0.2f);
            GUILayout.Label("══════════ 🎣 钓鱼模拟服务器 ══════════", new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUI.contentColor = Color.white;
            GUILayout.Space(5);

            // ========== 钓鱼状态总览区域 ==========
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(1f, 0.8f, 0.2f);
            GUILayout.Label("🎯 钓鱼状态", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            float countdownTime = GetTimeUntilNextFishing();
            bool isAutoFishing = IsAutoFishing;
            bool isPaused = IsFishingPaused;
            var fishingMode = CurrentFishingMode;

            // 状态显示
            string statusText;
            string statusColor;
            if (isPaused)
            {
                statusText = "⏸ 停滞等待";
                statusColor = "#FF6B6B";
            }
            else if (isAutoFishing)
            {
                statusText = "🎣 钓鱼中";
                statusColor = "#6BCB77";
            }
            else
            {
                statusText = "😴 待机状态";
                statusColor = "#9B59B6";
            }
            GUILayout.Label($"当前状态: <color={statusColor}>{statusText}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16, alignment = TextAnchor.MiddleCenter });

            GUILayout.Space(5);

            // 倒计时显示
            if (isPaused)
            {
                // 处于停滞状态
                float pauseRemaining = PauseRemainingTime;
                float pauseDuration = autoFishingManager != null 
                    ? (fishingMode == AutoFishingServerManager.FishingMode.Continuous 
                        ? autoFishingManager.ContinuousPauseDuration 
                        : autoFishingManager.NormalPauseDuration) 
                    : 0.5f;
                GUILayout.Label($"停滞剩余: <color=#FF6B6B>{pauseRemaining:F1}/{pauseDuration:F1}秒</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

                // 停滞进度条
                float pauseProgress = Mathf.Clamp01(1f - (pauseRemaining / pauseDuration));
                DrawProgressBar(pauseProgress, pauseRemaining, pauseDuration);
            }
            else if (isAutoFishing)
            {
                // 根据剩余时间改变颜色
                string countdownColor = countdownTime < 2f ? "#FF6B6B" : countdownTime < 5f ? "#FFD93D" : "#6BCB77";
                GUILayout.Label($"下次钓鱼: <color={countdownColor}>{countdownTime:F1}秒</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

                // 进度条 - 使用最大间隔时间
                float maxTime = fishingMode == AutoFishingServerManager.FishingMode.Continuous ? 
                    (ContinuousPauseDuration + 0.1f) : (autoFishingManager?.MaxFishingInterval ?? 20f);
                float progress = Mathf.Clamp01(1f - (countdownTime / maxTime));
                DrawProgressBar(progress, countdownTime, maxTime);
            }
            else
            {
                GUILayout.Label("<color=gray>等待开始钓鱼...</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            }

            GUILayout.Space(5);

            // 钓鱼模式显示和切换按钮
            GUILayout.BeginHorizontal();
            string modeColor = fishingMode == AutoFishingServerManager.FishingMode.Continuous ? "#FFD93D" : "#6BCB77";
            string modeName = fishingMode == AutoFishingServerManager.FishingMode.Continuous ? "连续钓鱼" : "普通钓鱼";
            string modeDesc = fishingMode == AutoFishingServerManager.FishingMode.Continuous ? "(间隔0.5秒)" : "(间隔3-20秒)";
            GUILayout.Label($"模式: <color={modeColor}>{modeName} {modeDesc}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            
            if (GUILayout.Button("切换模式", GUILayout.Width(80)))
            {
                ToggleFishingMode();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // ========== 角色动画状态区域 ==========
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.4f, 1f, 0.6f);
            GUILayout.Label("📦 角色动画状态", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"当前状态: {GetAnimationStatusColor(currentAnimationState)}", new GUIStyle(GUI.skin.label) { fontSize = 14 });

            // 动画控制按钮
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("😴 待机", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                PlayAnimation(FishingAnimationState.Lazy);
            }
            if (GUILayout.Button("⏹ 停止", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                PlayAnimation(FishingAnimationState.Idle);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // ========== 时间控制区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.6f, 0.8f, 1f);
            GUILayout.Label("📦 时间控制", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"当前时间段: <color=cyan>{GetTimeStatusName(CurrentTimeStatus)}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            if (GUILayout.Button("⏰ 切换时间段", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                timeSlotManager?.SwitchTimeSlot();
            }
            GUILayout.EndVertical();

            // ========== 天气控制区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.6f, 0.8f, 1f);
            GUILayout.Label("📦 天气控制", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"当前天气ID: <color=yellow>{CurrentWeatherId}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            if (GUILayout.Button("🌤️ 随机天气", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                weatherManager?.RandomWeather();
            }
            GUILayout.EndVertical();

            // ========== 环境渲染控制区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.4f, 0.9f, 0.6f);
            GUILayout.Label("🌄 环境渲染", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            int envTimeId = EnvironmentRenderManager.Instance?.GetCurrentTimeId() ?? 0;
            int envWeatherId = EnvironmentRenderManager.Instance?.GetCurrentWeatherId() ?? 0;
            GUILayout.Label($"当前时段ID: <color=cyan>{envTimeId}</color>  天气ID: <color=yellow>{envWeatherId}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });

            GUILayout.Space(3);
            GUILayout.Label("<color=#FFD93D>时段环境:</color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("清晨(401)", new GUIStyle(GUI.skin.button) { fontSize = 10 }))
            {
                EnvironmentRenderManager.Instance?.SwitchTimeEnvironment(401);
            }
            if (GUILayout.Button("日间(402)", new GUIStyle(GUI.skin.button) { fontSize = 10 }))
            {
                EnvironmentRenderManager.Instance?.SwitchTimeEnvironment(402);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("傍晚(403)", new GUIStyle(GUI.skin.button) { fontSize = 10 }))
            {
                EnvironmentRenderManager.Instance?.SwitchTimeEnvironment(403);
            }
            if (GUILayout.Button("深夜(404)", new GUIStyle(GUI.skin.button) { fontSize = 10 }))
            {
                EnvironmentRenderManager.Instance?.SwitchTimeEnvironment(404);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            GUILayout.Label("<color=#FFD93D>天气环境:</color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("晴(301)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(301);
            }
            if (GUILayout.Button("多云(302)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(302);
            }
            if (GUILayout.Button("阴(303)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(303);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("微风(304)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(304);
            }
            if (GUILayout.Button("小雨(305)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(305);
            }
            if (GUILayout.Button("薄雾(306)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(306);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("明月(307)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(307);
            }
            if (GUILayout.Button("雷雨(308)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(308);
            }
            if (GUILayout.Button("阵风(309)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(309);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("暴雨(310)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(310);
            }
            if (GUILayout.Button("大雾(311)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(311);
            }
            if (GUILayout.Button("彩虹(312)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(312);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("热浪(313)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(313);
            }
            if (GUILayout.Button("火烧云(314)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(314);
            }
            if (GUILayout.Button("荧光海(315)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(315);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("海市蜃楼(316)", new GUIStyle(GUI.skin.button) { fontSize = 9 }))
            {
                EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(316);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // ========== 场景控制区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.8f, 0.6f, 1f);
            GUILayout.Label("📦 场景控制", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"当前场景: <color=magenta>{sceneFishPoolManager?.CurrentSceneId ?? 1}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🌊 场景1", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                SetSceneId(1);
            }
            if (GUILayout.Button("🌊 场景2", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                SetSceneId(2);
            }
            if (GUILayout.Button("🌊 场景3", new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                SetSceneId(3);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // ========== 自动钓鱼控制区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.9f, 0.6f, 0.3f);
            GUILayout.Label("📦 自动钓鱼", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUI.backgroundColor = isAutoFishing ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.3f, 0.7f, 0.3f);
            if (GUILayout.Button(isAutoFishing ? "⏹ 停止自动钓鱼" : "▶ 开始自动钓鱼",
                new GUIStyle(GUI.skin.button) { fontSize = 13 }))
            {
                if (isAutoFishing)
                {
                    StopAutoFishing();
                    PlayAnimation(FishingAnimationState.Idle);
                }
                else
                {
                    StartAutoFishing();
                }
            }
            GUI.backgroundColor = Color.white;

            // 钓鱼模式切换按钮
            GUILayout.Space(5);
            string modeButtonText = CurrentFishingMode == AutoFishingServerManager.FishingMode.Continuous ? 
                "🔄 切换为普通钓鱼" : "⚡ 切换为连续钓鱼";
            GUI.backgroundColor = CurrentFishingMode == AutoFishingServerManager.FishingMode.Continuous ? 
                new Color(1f, 0.8f, 0.2f) : new Color(0.2f, 0.8f, 1f);
            if (GUILayout.Button(modeButtonText,
                new GUIStyle(GUI.skin.button) { fontSize = 12 }))
            {
                ToggleFishingMode();
            }
            GUI.backgroundColor = Color.white;

            // 连续钓鱼停滞时长设置
            if (CurrentFishingMode == AutoFishingServerManager.FishingMode.Continuous)
            {
                GUILayout.Space(3);
                GUILayout.Label($"停滞时长: {ContinuousPauseDuration:F1}秒", 
                    new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter });
            }

            // 自动钓鱼状态显示
            if (isAutoFishing)
            {
                GUILayout.Space(8);
                float lastStruggleTime = LastStruggleTime;

                GUILayout.Label($"上次挣扎时间: <color=orange>{lastStruggleTime:F1}秒</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }

            // 手动钓鱼状态显示
            if (currentFishingResult != null)
            {
                GUILayout.Space(8);
                float currentStruggleTime = currentFishingResult.struggleTime;
                GUILayout.Label($"当前挣扎时间: <color=cyan>{currentStruggleTime:F2}秒</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"第一个ID(检测): <color=green>{currentFishingResult.detectedFishId}</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"第二个ID(实际): <color={(currentFishingResult.isTrash ? "#FF6B6B" : "#6BCB77")}>{currentFishingResult.actualItemId}</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"是否垃圾: <color={(currentFishingResult.isTrash ? "#FF6B6B" : "#6BCB77")}>{(currentFishingResult.isTrash ? "是" : "否")}</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }
            GUILayout.EndVertical();

            // ========== 背包信息区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.7f, 0.9f, 0.7f);
            GUILayout.Label("📦 背包信息", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            GUILayout.Label($"背包物品种类: <color=green>{inventoryManager?.InventoryCount ?? 0}</color>",
                new GUIStyle(GUI.skin.label) { richText = true });

            // 鱼篓详细信息
            int fishBagUsed = inventoryManager?.GetTotalFishCount() ?? 0;
            int fishBagCapacity = inventoryManager?.FishBagCapacity ?? 20;
            string fishBagColor = fishBagUsed >= fishBagCapacity ? "#FF6B6B" : (fishBagUsed >= fishBagCapacity * 0.8f ? "#FFD93D" : "#6BCB77");
            GUILayout.Label($"鱼篓容量: <color={fishBagColor}>{fishBagUsed}/{fishBagCapacity}</color>",
                new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"鱼篓物品种类: <color=blue>{inventoryManager?.FishCount ?? 0}</color>",
                new GUIStyle(GUI.skin.label) { richText = true });

            // 鱼篓状态提示
            bool isFishBagFull = inventoryManager?.IsFishBagFull() ?? false;
            if (isFishBagFull)
            {
                GUILayout.Label($"⚠️ <color=red>鱼篓已满！</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }
            else
            {
                int remaining = fishBagCapacity - fishBagUsed;
                GUILayout.Label($"剩余空间: <color=cyan>{remaining}</color> 个",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }
            GUILayout.EndVertical();

            // ========== 自动钓鱼参数设置 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.7f, 0.9f, 0.7f);
            GUILayout.Label("📦 自动钓鱼参数", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.contentColor = Color.white;

            if (autoFishingManager != null)
            {
                GUILayout.Space(5);
                GUILayout.Label($"最小间隔: <color=purple>{autoFishingManager.MinFishingInterval:F1}</color>秒",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"最大间隔: <color=purple>{autoFishingManager.MaxFishingInterval:F1}</color>秒",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"下次钓鱼时间: <color=orange>{autoFishingManager.NextFishingTime:F1}</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }
            GUILayout.EndVertical();

            // ========== 装备信息区域 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.9f, 0.6f, 0.3f);
            GUILayout.Label("📦 装备信息", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.contentColor = Color.white;

            GUILayout.Space(5);
            if (inventoryManager != null)
            {
                var equippedItems = inventoryManager.GetAllEquippedItems();
                if (equippedItems != null && equippedItems.Count > 0)
                {
                    foreach (var kvp in equippedItems)
                    {
                        EquipmentSlotType slotType = kvp.Key;
                        int itemId = kvp.Value;
                        
                        if (itemId > 0)
                        {
                            string slotName = inventoryManager?.GetSlotName(slotType) ?? "未知槽位";
                            GUILayout.Label($"{slotName}: <color=yellow>物品ID {itemId}</color>",
                                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
                        }
                    }
                }
                else
                {
                    GUILayout.Label("<color=gray>未装备任何物品</color>",
                        new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
                }
            }
            else
            {
                GUILayout.Label("<color=gray>装备管理器未初始化</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            }

            // 装备控制按钮
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 显示装备详情", GUILayout.Width(120)))
            {
                ShowEquipment();
            }
            if (GUILayout.Button("🔄 刷新装备", GUILayout.Width(100)))
            {
                // 刷新装备数据
                inventoryManager?.RefreshInventoryData();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // ========== 服务器状态信息 ==========
            GUILayout.Space(10);
            GUILayout.BeginVertical("box");
            GUI.contentColor = new Color(0.6f, 0.8f, 1f);
            GUILayout.Label("📦 服务器状态", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUI.contentColor = Color.white;

            GUILayout.EndVertical();

            GUILayout.EndArea();
        }
    }

    /// <summary>
    /// 绘制进度条
    /// </summary>
    private void DrawProgressBar(float progress, float currentTime, float maxTime)
    {
        GUILayout.Space(5);

        // 进度条背景
        GUI.color = new Color(0.3f, 0.3f, 0.3f);
        Rect barRect = GUILayoutUtility.GetRect(400, 20);
        GUI.DrawTexture(barRect, MakeTex(400, 20, new Color(0.3f, 0.3f, 0.3f)));

        // 进度条前景
        float fillWidth = barRect.width * progress;
        if (fillWidth > 0)
        {
            string progressColor = progress < 0.3f ? "#FF6B6B" : progress < 0.7f ? "#FFD93D" : "#6BCB77";
            GUI.color = HexToColor(progressColor);
            Rect fillRect = new Rect(barRect.x, barRect.y, fillWidth, barRect.height);
            GUI.DrawTexture(fillRect, MakeTex((int)fillWidth, 20, HexToColor(progressColor)));
        }

        GUI.color = Color.white;
        GUILayout.Space(25);
    }

    /// <summary>
    /// 将十六进制颜色字符串转换为Color
    /// </summary>
    private Color HexToColor(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex.Substring(1);
        }

        if (hex.Length == 6)
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        return Color.white;
    }

    /// <summary>
    /// 获取时间段名称
    /// </summary>
    private string GetTimeStatusName(TimeStatus status)
    {
        switch (status)
        {
            case TimeStatus.Earlymorning: return "清晨";
            case TimeStatus.Daytime: return "白天";
            case TimeStatus.Evening: return "傍晚";
            case TimeStatus.LateAtNigh: return "深夜";
            default: return "未知";
        }
    }

    /// <summary>
    /// 获取动画状态颜色
    /// </summary>
    private string GetAnimationStatusColor(FishingAnimationState state)
    {
        string color = "#FFFFFF";
        switch (state)
        {
            case FishingAnimationState.Idle:
                color = "#6BCB77";
                break;
            case FishingAnimationState.Reeling:
                color = "#FFD93D";
                break;
            case FishingAnimationState.Success:
                color = "#6BCB77";
                break;
            case FishingAnimationState.Trash:
                color = "#FF6B6B";
                break;
            case FishingAnimationState.Lazy:
                color = "#9B9B9B";
                break;
        }
        return $"<color={color}>{animationStateNames[state]}</color>";
    }

    /// <summary>
    /// 播放动画
    /// </summary>
    private void PlayAnimation(FishingAnimationState state)
    {
        currentAnimationState = state;

        switch (state)
        {
            case FishingAnimationState.Idle:
                ServerManager.Instance?.NotifyPlayIdleAnimation();
                break;
            case FishingAnimationState.Reeling:
                ServerManager.Instance?.NotifyPlayReelAnimation(3f, null);
                break;
            case FishingAnimationState.Success:
                ServerManager.Instance?.NotifyPlayIdleAnimation();
                break;
            case FishingAnimationState.Trash:
                ServerManager.Instance?.NotifyPlayIdleAnimation();
                break;
            case FishingAnimationState.Lazy:
                ServerManager.Instance?.NotifyPlayLazyAnimation();
                break;
        }
    }

    /// <summary>
    /// 创建纯色纹理
    /// </summary>
    private Texture2D MakeTex(float width, float height, Color col)
    {
        Color[] pix = new Color[(int)(width * height)];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D((int)width, (int)height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

#endif

    #endregion
}
*/