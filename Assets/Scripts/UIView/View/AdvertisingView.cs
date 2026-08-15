using UnityEngine;
using UnityEngine.UI;
using System;

public class AdvertisingView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;
    public Text infoText;
    public Button actionBtn;
    public Text actionBtnText;
    public Button skipBtn;
    public Text skipBtnText;

    private Action onActionCallback;
    private Action<bool> onActionCallbackWithResult;
    private Action onCloseCallback;

    void Start()
    {
        if (maskBtn != null)
        {
            maskBtn.onClick.AddListener(OnMaskClick);
        }

        if (closeBtn != null)
        {
            closeBtn.onClick.AddListener(OnCloseClick);
        }

        if (actionBtn != null)
        {
            actionBtn.onClick.AddListener(OnActionClick);
        }

        if (skipBtn != null)
        {
            skipBtn.onClick.AddListener(OnSkipClick);
        }
    }

    public void ShowAd(string info, Action onAction = null, Action onClose = null, string btnText = "确定", string skipText = "跳过")
    {
        Debug.Log($"[AdvertisingView] ShowAd 开始 - info={info}, btnText={btnText}, skipText={skipText}");
        
        if (infoText != null)
        {
            infoText.text = info;
        }

        if (actionBtnText != null)
        {
            actionBtnText.text = btnText;
        }

        if (skipBtnText != null)
        {
            skipBtnText.text = skipText;
        }

        onActionCallback = onAction;
        onActionCallbackWithResult = null;
        onCloseCallback = onClose;

        gameObject.SetActive(true);
        Debug.Log($"[AdvertisingView] ShowAd 完成 - 界面已显示");
    }

    public void ShowAd(string info, Action<bool> onActionWithResult, Action onClose = null, string btnText = "确定", string skipText = "跳过")
    {
        Debug.Log($"[AdvertisingView] ShowAd (带结果) 开始 - info={info}, btnText={btnText}, skipText={skipText}");
        
        if (infoText != null)
        {
            infoText.text = info;
        }

        if (actionBtnText != null)
        {
            actionBtnText.text = btnText;
        }

        if (skipBtnText != null)
        {
            skipBtnText.text = skipText;
        }

        onActionCallback = null;
        onActionCallbackWithResult = onActionWithResult;
        onCloseCallback = onClose;

        gameObject.SetActive(true);
        Debug.Log($"[AdvertisingView] ShowAd (带结果) 完成 - 界面已显示");
    }

    public void ShowAdWithUnlockSkill(string info, int skillId, string btnText = "看广告解锁")
    {
        ShowAd(info, () => OnUnlockSkillByAd(skillId), null, btnText);
    }

    public void ShowAdWithUpgradeSkill(string info, int skillId, string btnText = "看广告升级")
    {
        ShowAd(info, () => OnUpgradeSkillByAd(skillId), null, btnText);
    }

    private void OnMaskClick()
    {
        Debug.Log("[AdvertisingView] OnMaskClick - 点击遮罩关闭");
        onCloseCallback?.Invoke();
        Close();
    }

    private void OnCloseClick()
    {
        Debug.Log("[AdvertisingView] OnCloseClick - 点击关闭按钮");
        onCloseCallback?.Invoke();
        Close();
    }

    private void OnActionClick()
    {
        Debug.Log($"[AdvertisingView] OnActionClick - onActionCallback={onActionCallback != null}, onActionCallbackWithResult={onActionCallbackWithResult != null}");
        
        // 模拟广告播放结果（实际项目中应该调用广告SDK）
        // 80%成功率模拟
        bool adSuccess = UnityEngine.Random.value < 0.8f;
        
        Debug.Log($"[AdvertisingView] OnActionClick - 广告播放模拟结果: {(adSuccess ? "成功" : "失败")}");
        
        if (onActionCallback != null)
        {
            onActionCallback.Invoke();
        }
        else if (onActionCallbackWithResult != null)
        {
            onActionCallbackWithResult.Invoke(adSuccess);
        }
        
        Close();
    }

    private void OnSkipClick()
    {
        Debug.Log($"[AdvertisingView] OnSkipClick - onCloseCallback={onCloseCallback != null}");
        onCloseCallback?.Invoke();
        Close();
    }

    private void OnUnlockSkillByAd(int skillId)
    {
        Debug.Log($"[AdvertisingView] OnUnlockSkillByAd - skillId={skillId}, 准备触发事件 Skill_UnlockByAd");
        CommunicateEvent.Modify("Skill_UnlockByAd", skillId);
        Debug.Log($"[AdvertisingView] OnUnlockSkillByAd - 事件 Skill_UnlockByAd 已触发");
    }

    private void OnUpgradeSkillByAd(int skillId)
    {
        Debug.Log($"[AdvertisingView] OnUpgradeSkillByAd - skillId={skillId}");
        
        if (NetServerManager.Instance != null)
        {
            int currentLevel = NetServerManager.Instance.GetComponentLevel(skillId);
            NetServerManager.Instance.UpgradeSkill(skillId, currentLevel + 1, (success) =>
            {
                if (success)
                {
                    Debug.Log($"[AdvertisingView] 技能升级成功（广告）: skillId={skillId}");
                    CommunicateEvent.Modify<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, "升级成功！");
                    CommunicateEvent.Modify("Equipment_Refresh");
                }
                else
                {
                    Debug.LogWarning($"[AdvertisingView] 技能升级失败（广告）: skillId={skillId}");
                }
            });
        }
    }

    private void Close()
    {
        onActionCallback = null;
        onCloseCallback = null;
        gameObject.SetActive(false);
    }
}
