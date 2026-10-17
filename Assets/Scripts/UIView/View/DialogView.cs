using UnityEngine;
using UnityEngine.UI;

public enum DialogType
{
    Warning,
    Info
}

public class DialogView : BaseView
{
    public Button confirmBtn;
    public Button cancelBtn;
    public Text dialogText;
    public Text wariningText;
    public GameObject warningObj;
    public GameObject infoObj;

    private System.Action onConfirmCallback;
    private DialogType currentDialogType;

    protected override void Start()
    {
        base.Start();

        if (confirmBtn != null)
        {
            confirmBtn.onClick.AddListener(OnConfirmClick);
        }

        if (cancelBtn != null)
        {
            cancelBtn.onClick.AddListener(OnCancelClick);
        }
    }

    private void OnConfirmClick()
    {
        if (currentDialogType == DialogType.Info && onConfirmCallback != null)
        {
            onConfirmCallback.Invoke();
        }
        Hide();
    }

    private void OnCancelClick()
    {
        Hide();
    }

    public void Show(string message, DialogType type = DialogType.Warning, System.Action onConfirm = null)
    {
        currentDialogType = type;
        onConfirmCallback = onConfirm;

        if (dialogText != null)
        {
            dialogText.text = message;
        }

        if (wariningText != null)
        {
            wariningText.text = message;
        }

        if (warningObj != null)
        {
            warningObj.SetActive(type == DialogType.Warning);
        }

        if (infoObj != null)
        {
            infoObj.SetActive(type == DialogType.Info);
        }

        
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onConfirmCallback = null;
    }
}
