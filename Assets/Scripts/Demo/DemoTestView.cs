using UnityEngine;
using UnityEngine.UI;

public class DemoTestView : MonoBehaviour
{
    public Button[] closeBtns;

    public Button setBtn;
    public Button fishToolBtn;
    public Button bagBtn;
    public Button fishBagBtn;
    public Button guidBtn;
    public Button fishGuideBtn;
    public Button bugGuideBtn;
    public Button treasureGuideBtn;
    public Button otherGuideBtn;

    public Toggle bagTog_0;
    public Toggle bagTog_1;
    public Toggle bagTog_2;

    public GameObject bagTogGo_0;
    public GameObject bagTogGo_1;
    public GameObject bagTogGo_2;

    public GameObject setViewGo;
    public GameObject fishToolViewGo;
    public GameObject bagViewGo;
    public GameObject fishBagViewGo;
    public GameObject uiLeftGuideViewGo;
    public GameObject fishGuideViewGo;
    public GameObject bugGuideViewGo;
    public GameObject treasureGuideViewGo;
    public GameObject otherGuideViewGo;
    public void InitView()
    {
        foreach (var item in closeBtns)
        {
            GameObject go = item.gameObject;
            item.onClick.AddListener(() =>
            {
                go.SetActive(false);
            });
        }

        setBtn.onClick.AddListener(() => { CloseUiLeftGuideViewGo(); setViewGo.SetActive(true); });
        fishToolBtn.onClick.AddListener(() => { CloseUiLeftGuideViewGo(); fishToolViewGo.SetActive(true); });
        bagBtn.onClick.AddListener(() => { CloseUiLeftGuideViewGo(); bagViewGo.SetActive(true); });
        fishBagBtn.onClick.AddListener(() => { CloseUiLeftGuideViewGo(); fishBagViewGo.SetActive(true); });
        guidBtn.onClick.AddListener(() => { CloseUiLeftGuideViewGo();  uiLeftGuideViewGo.SetActive(true); });
        fishGuideBtn.onClick.AddListener(() => {/* CloseUiLeftGuideViewGo();*/ fishGuideViewGo.SetActive(true); });
        bugGuideBtn.onClick.AddListener(() => { /*CloseUiLeftGuideViewGo(); */bugGuideViewGo.SetActive(true); });
        treasureGuideBtn.onClick.AddListener(() => {/* CloseUiLeftGuideViewGo();*/ treasureGuideViewGo.SetActive(true); });
        otherGuideBtn.onClick.AddListener(() => {/* CloseUiLeftGuideViewGo();*/ otherGuideViewGo.SetActive(true); });
        BagTogInit();
    }

    void BagTogInit() 
    {
        bagTog_0.onValueChanged.AddListener((bool isT) =>
        {
            if (isT)
            {
                OpenBagComView();
            }
        });

        bagTog_1.onValueChanged.AddListener((bool isT) =>
        {
            if (isT)
            {
                bagTogGo_0.SetActive(false);
                bagTogGo_1.SetActive(true);
                bagTogGo_2.SetActive(false);
            }
        });

        bagTog_2.onValueChanged.AddListener((bool isT) =>
        {
            if (isT)
            {
                bagTogGo_0.SetActive(false);
                bagTogGo_1.SetActive(false);
                bagTogGo_2.SetActive(true);
            }
        });
    }

    void OpenBag() 
    {
        bagTog_0.isOn = true;
        //OpenBagComView();
    }

    void OpenBagComView() 
    {
        bagTogGo_0.SetActive(true);
        bagTogGo_1.SetActive(false);
        bagTogGo_2.SetActive(false);
    }



    void CloseUiLeftGuideViewGo()
    {
        uiLeftGuideViewGo.SetActive(false);
    }
    void Start()
    {
        InitView();
    }

    void Update()
    {

    }
}
