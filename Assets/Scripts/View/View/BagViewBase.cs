using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BagViewBase : MonoBehaviour
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
        Init();
    }

    public virtual void Init()
    {
        if (isInitialized) return;
        isInitialized = true;
        FindAndBindMaskButton();
        FindAndBindCloseButton();
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
            closeBtn.onClick.AddListener(OnCloseButtonClick);
        }
    }

    protected virtual void OnCloseButtonClick()
    {
        CloseBag();
    }

    public void OpenBag()
    {
        gameObject.SetActive(true);
    }

    public void CloseBag()
    {
        gameObject.SetActive(false);
    }
}