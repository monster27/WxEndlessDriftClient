// ============================================================
// 文件: PetAutoFeedFilterPanel.cs
// 说明: 宠物自动喂食过滤配置
//       继承 BaseFilterPanel，只实现服务器同步
// 路径: Assets/Scripts/UI/
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class PetAutoFeedFilterPanel : BaseFilterPanel
{
    // ============================================================
    // 本地 Key 前缀
    // ============================================================

    protected override string PrefsKeyPrefix => "PetAutoFeed_";

    // ============================================================
    // 服务器同步
    // ============================================================

    protected override void SyncSettingsToServer()
    {
        if (NetServerManager.Instance == null) return;

        var f = GetCurrentFilter();

        var data = new AutoFeedFilterData
        {
            rarity201 = f.rarity201,
            rarity202 = f.rarity202,
            rarity203 = f.rarity203,
            rarity204 = f.rarity204,
            rarity205 = f.rarity205,
            rarity206 = f.rarity206,

            starRate501 = f.starRate501,
            starRate502 = f.starRate502,
            starRate503 = f.starRate503,
            starRate504 = f.starRate504,

            notShine = f.notShine,
            isShine = f.isShine,
            skipSelected = f.skipSelected
        };

        NetServerManager.Instance.SaveAutoFeedFilterConfig(data, (success, message) =>
        {
            if (!success)
            {
                Z_Logger.LogWarning($"[PetAutoFeedFilterPanel] 同步失败: {message}");
            }
        });
    }

    public override void FetchSettingsFromServer()
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.FetchAutoFeedFilterConfig((success, data) =>
        {
            if (success && data != null)
            {
                var f = new FilterData
                {
                    rarity201 = data.rarity201,
                    rarity202 = data.rarity202,
                    rarity203 = data.rarity203,
                    rarity204 = data.rarity204,
                    rarity205 = data.rarity205,
                    rarity206 = data.rarity206,

                    starRate501 = data.starRate501,
                    starRate502 = data.starRate502,
                    starRate503 = data.starRate503,
                    starRate504 = data.starRate504,

                    notShine = data.notShine,
                    isShine = data.isShine,
                    skipSelected = data.skipSelected
                };

                ApplyFilter(f);
            }
        });
    }
}