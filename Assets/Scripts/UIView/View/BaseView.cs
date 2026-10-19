using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BaseView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    protected bool isInitialized = false;

    // ============================================================
    // ✅ 新增：View 标识（由 GameUIManager 在显示时注入）
    // ============================================================
    [HideInInspector] public string viewKey = "";

    /// <summary>
    /// GameUIManager 在显示实例时调用，注入 View 的唯一 key。
    /// 之后 BaseView.HideView() 会通过事件通知 GameUIManager 走 CloseView 状态机。
    /// </summary>
    public void RegisterViewKey(string key)
    {
        viewKey = key;
        Z_Logger.Log($"[BaseView] RegisterViewKey: {GetType().Name} <- '{key}'");
    }

    /// <summary>
    /// GameUIManager.HideInstance 调用 HideView 前会置 true，
    /// 避免 HideView 内部再发 UI_ViewSelfHide 导致 CloseView 递归。
    /// </summary>
    [HideInInspector] public bool _isHidingByManager = false;

    protected virtual void Awake()
    {
        FindAndBindMaskButton();
        FindAndBindCloseButton();
    }

    protected virtual void Start()
    {
        BaseViewInit();
    }

    /// <summary>
    /// 初始化方法，子类重写时需调用 base.BaseViewInit()
    /// </summary>
    public virtual void BaseViewInit()
    {
        if (isInitialized) return;
        isInitialized = true;
        FindAndBindMaskButton();
        FindAndBindCloseButton();
        BindBtns();
    }

    /// <summary>
    /// 初始化接口，供外部调用，子类可重写
    /// </summary>
    public virtual void Init()
    {
        BaseViewInit();
    }

    /// <summary>
    /// 按钮绑定方法，子类重写以绑定各自的按钮
    /// </summary>
    protected virtual void BindBtns()
    {
        // 子类重写此方法绑定特定按钮
    }

    private void FindAndBindMaskButton()
    {
        if (maskBtn == null)
        {
            Transform maskBtnTransform = transform.Find("MaskBtn");
            if (maskBtnTransform != null)
            {
                maskBtn = maskBtnTransform.GetComponent<Button>();
            }
        }

        if (maskBtn != null)
        {
            maskBtn.onClick.RemoveAllListeners();
            maskBtn.onClick.AddListener(OnCloseButtonClick);
        }
    }

    private void FindAndBindCloseButton()
    {
        if (closeBtn == null)
        {
            Transform closeBtnTransform = transform.Find("CloseBtn");
            if (closeBtnTransform != null)
            {
                closeBtn = closeBtnTransform.GetComponent<Button>();
            }
        }

        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(OnCloseButtonClick);
        }
    }

    protected virtual void OnCloseButtonClick()
    {
        Z_Logger.Log($"[{GetType().Name}] OnCloseButtonClick");
        HideView();
    }

    /// <summary>
    /// 显示界面，在 PreShow 之后执行
    /// </summary>
    public virtual void ShowView()
    {
        PreShow();
        gameObject.SetActive(true);
        Z_Logger.Log($"[{GetType().Name}] ShowView, viewKey='{viewKey}'");
    }

    /// <summary>
    /// 隐藏界面，在 PreHide 之前执行
    /// </summary>
    public virtual void HideView()
    {
        PreHide();

        // ============================================================
        // ✅ 新增：关闭前通知 GameUIManager 走 CloseView 状态机
        //    只在"外部主动调 HideView"时发；GameUIManager.HideInstance 调用时
        //    _isHidingByManager == true，不发，避免 CloseView 递归。
        // ============================================================
        if (!string.IsNullOrEmpty(viewKey) && !_isHidingByManager)
        {
            Z_Logger.Log($"[BaseView] {GetType().Name} HideView 主动通知 GameUIManager.CloseView('{viewKey}')");
            CommunicateEvent.Modify<string>("UI_ViewSelfHide", viewKey);
        }

        gameObject.SetActive(false);
        Z_Logger.Log($"[{GetType().Name}] HideView, viewKey='{viewKey}'");
    }

    /// <summary>
    /// 显示前的预处理，子类可重写
    /// </summary>
    protected virtual void PreShow()
    {
        // 子类重写以在显示前执行逻辑
    }

    /// <summary>
    /// 隐藏前的预处理，子类可重写
    /// </summary>
    protected virtual void PreHide()
    {
        // 子类重写以在隐藏前执行逻辑
    }
}
