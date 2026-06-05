using UnityEngine;

public enum PlayerAnimationState
{
    Idle,       // 钓鱼闲置
    Reel,       // 拉钩
    Lazy        // 懒动作（预留）
}

public class PlayerAniCtrl : MonoBehaviour
{
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private PlayerAnimationState initialState = PlayerAnimationState.Idle;

    // 动画参数 - 钓鱼闲置
    [Header("Idle Animation")]
    [SerializeField] private Texture2D idleSheet;
    [SerializeField] private int idleColumns = 4;
    [SerializeField] private float idleSpeed = 15f;

    // 动画参数 - 拉钩
    [Header("Reel Animation")]
    [SerializeField] private Texture2D reelSheet;
    [SerializeField] private int reelColumns = 4;
    [SerializeField] private float reelSpeed = 20f;

    // 动画参数 - 懒动作（预留）
    [Header("Lazy Animation (Reserved)")]
    [SerializeField] private Texture2D lazySheet;
    [SerializeField] private int lazyColumns = 4;
    [SerializeField] private float lazySpeed = 18f;

    private PlayerAnimationState currentState;
    private Material playerMaterial;
    private float blendSpeed = 5f;
    private float targetBlendValue = 0f;
    private float currentBlendValue = 0f;
    private bool isFlipped = false;
    private bool isAnimationInitialized = false;

    private void Start()
    {
        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<Renderer>();
        }

        if (playerRenderer != null)
        {
            playerMaterial = playerRenderer.material;
        }

        if (!isAnimationInitialized)
        {
            SetAnimationState(initialState);
        }
    }

    private void Update()
    {
        if (playerMaterial != null)
        {
            // 平滑过渡混合值
            currentBlendValue = Mathf.Lerp(currentBlendValue, targetBlendValue, blendSpeed * Time.deltaTime);
            playerMaterial.SetFloat("_Blend", currentBlendValue);

            // 更新翻转状态
            playerMaterial.SetFloat("_Flip", isFlipped ? 1f : 0f);
        }
    }

    private void OnGUI()
    {
        // 创建GUI按钮 - 放在右上角
        float screenWidth = Screen.width;
        float panelWidth = 200f;
        float panelHeight = 200f;

        GUILayout.BeginArea(new Rect(screenWidth - panelWidth - 10, 10, panelWidth, panelHeight));

        GUILayout.Label("Player Animation Control");
        GUILayout.Space(10);

        // 翻转按钮
        if (GUILayout.Button(isFlipped ? "Flip: ON" : "Flip: OFF"))
        {
            ToggleFlip();
        }

        GUILayout.Space(10);

        // 动画状态按钮
        if (GUILayout.Button("Idle Animation"))
        {
            PlayIdleAnimation();
        }

        if (GUILayout.Button("Reel Animation"))
        {
            PlayReelAnimation();
        }

        if (GUILayout.Button("Lazy Animation (Reserved)"))
        {
            PlayLazyAnimation();
        }

        GUILayout.Space(10);
        GUILayout.Label("Current State: " + currentState.ToString());

        GUILayout.EndArea();
    }

    // 设置动画状态
    public void SetAnimationState(PlayerAnimationState state)
    {
        if (currentState == state || playerMaterial == null)
        {
            return;
        }

        currentState = state;

        // 设置主要动画（_MainTex，对应Blend=0）
        switch (state)
        {
            case PlayerAnimationState.Idle:
                SetAnimationClip(idleSheet, 1, idleColumns, idleSpeed);
                break;
            case PlayerAnimationState.Reel:
                SetAnimationClip(reelSheet, 1, reelColumns, reelSpeed);
                break;
            case PlayerAnimationState.Lazy:
                SetAnimationClip(lazySheet, 1, lazyColumns, lazySpeed);
                break;
        }

        targetBlendValue = 0f;
    }

    // 播放钓鱼闲置动画
    public void PlayIdleAnimation()
    {
        SetAnimationState(PlayerAnimationState.Idle);
    }

    // 播放拉钩动画
    public void PlayReelAnimation()
    {
        SetAnimationState(PlayerAnimationState.Reel);
    }

    // 播放懒动作动画（预留）
    public void PlayLazyAnimation()
    {
        SetAnimationState(PlayerAnimationState.Lazy);
    }

    // 切换翻转状态
    public void ToggleFlip()
    {
        isFlipped = !isFlipped;
    }

    // 设置翻转状态
    public void SetFlip(bool flip)
    {
        isFlipped = flip;
    }

    // 获取当前翻转状态
    public bool IsFlipped()
    {
        return isFlipped;
    }

    // 设置人物纹理
    public void SetCharacterTextures(Texture2D idle, Texture2D lazy, Texture2D reel)
    {
        // 确保材质已初始化
        EnsureMaterialInitialized();

        // 先同步数据（更新纹理缓存）
        if (idle != null) idleSheet = idle;
        if (lazy != null) lazySheet = lazy;
        if (reel != null) reelSheet = reel;

        // 临时保存当前状态
        PlayerAnimationState tempState = currentState;

        // 强制重新设置纹理（即使状态相同）
        switch (tempState)
        {
            case PlayerAnimationState.Idle:
                SetAnimationClip(idleSheet, 1, idleColumns, idleSpeed);
                break;
            case PlayerAnimationState.Reel:
                SetAnimationClip(reelSheet, 1, reelColumns, reelSpeed);
                break;
            case PlayerAnimationState.Lazy:
                SetAnimationClip(lazySheet, 1, lazyColumns, lazySpeed);
                break;
        }
    }

    /// <summary>
    /// 确保材质已初始化
    /// </summary>
    private void EnsureMaterialInitialized()
    {
        if (playerMaterial != null) return;
        
        if (playerRenderer == null)
        {
            playerRenderer = GetComponent<Renderer>();
        }
        
        if (playerRenderer != null)
        {
            playerMaterial = playerRenderer.material;
            Debug.Log("[PlayerAniCtrl] EnsureMaterialInitialized - 材质已初始化");
        }
        else
        {
            Debug.LogWarning("[PlayerAniCtrl] EnsureMaterialInitialized - playerRenderer 为 null");
        }
    }

    public void SetAnimationParams(int idleCols, float idleSpd, int reelCols, float reelSpd, int lazyCols, float lazySpd)
    {
        idleColumns = idleCols;
        idleSpeed = idleSpd;
        reelColumns = reelCols;
        reelSpeed = reelSpd;
        lazyColumns = lazyCols;
        lazySpeed = lazySpd;
        isAnimationInitialized = true;
        Debug.Log($"[PlayerAniCtrl] 动画参数已更新 - idle: {idleColumns}x{idleSpeed}, reel: {reelColumns}x{reelSpeed}, lazy: {lazyColumns}x{lazySpeed}");
    }

    public PlayerAnimationState GetCurrentState()
    {
        return currentState;
    }

    private void SetAnimationClip(Texture2D spriteSheet, int rows, int columns, float speed)
    {
        if (spriteSheet != null)
        {
            playerMaterial.SetTexture("_MainTex", spriteSheet);
            playerMaterial.SetFloat("_Rows1", rows);
            playerMaterial.SetFloat("_Columns1", columns);
            playerMaterial.SetFloat("_Speed1", speed);
        }
    }

}
