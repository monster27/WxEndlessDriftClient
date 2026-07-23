using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BaseView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    protected bool isInitialized = false;

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
        Debug.Log($"[{GetType().Name}] OnCloseButtonClick");
        HideView();
    }

    /// <summary>
    /// 显示界面，在 PreShow 之后执行
    /// </summary>
    public virtual void ShowView()
    {
        PreShow();
        gameObject.SetActive(true);
        Debug.Log($"[{GetType().Name}] ShowView");
    }

    /// <summary>
    /// 隐藏界面，在 PreHide 之前执行
    /// </summary>
    public virtual void HideView()
    {
        PreHide();
        gameObject.SetActive(false);
        Debug.Log($"[{GetType().Name}] HideView");
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
