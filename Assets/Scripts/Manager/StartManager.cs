using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartManager : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    public Button startBtn;

    private void Start()
    {
        startBtn.onClick.AddListener(() => 
        {
            LoadGameScene();
        });
    }

    //void Update()
    //{
    //    // 按空格键或点击鼠标左键跳转
    //    if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
    //    {
    //        LoadGameScene();
    //    }
    //}

    public void LoadGameScene()
    {
        Debug.Log("加载游戏场景: " + gameSceneName);
        SceneManager.LoadScene(gameSceneName);
    }
}