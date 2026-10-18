using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 按 Label 预下载 Addressables Bundle
/// 继承 SingletonMono<T>，第一次调用 Instance 时自动创建 + 常驻
/// </summary>
public class PreloadManager : SingletonMono<PreloadManager>
{
    public bool Tier1Done { get; private set; }
    public bool Tier2Done { get; private set; }
    public float Tier1Progress { get; private set; }
    public float Tier2Progress { get; private set; }

    private bool _tier1Running;
    private bool _tier2Running;

    protected override void Awake()
    {
        base.Awake();   // 必须调，SingletonMono 的 Awake 做 DontDestroyOnLoad
    }

    // ============================================================
    //  对外接口
    // ============================================================

    public void StartTier1() => StartCoroutine(PreloadCoroutine("tier1_preload", true));
    public void StartTier2() => StartCoroutine(PreloadCoroutine("tier2_idle", false));

    public bool ShouldSkipTier2()
    {
        return Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork;
    }

    // ============================================================
    //  核心
    // ============================================================

    private IEnumerator PreloadCoroutine(string label, bool isTier1)
    {
        // ---------------- 状态锁 & 早退 ----------------
        if (isTier1 && _tier1Running) yield break;
        if (!isTier1 && _tier2Running) yield break;
        if (isTier1 && Tier1Done) yield break;
        if (!isTier1 && Tier2Done) yield break;

        if (isTier1) _tier1Running = true;
        else _tier2Running = true;

        if (!isTier1 && ShouldSkipTier2())
        {
            Z_Logger.Log("[Preload] 4G 网络，跳过 tier2 预下载");
            _tier2Running = false;
            yield break;
        }

        Z_Logger.Log($"[Preload] 开始预下载: {label}");

        // ---------------- 发起请求 ----------------
        AsyncOperationHandle handle = default;
        bool syncFailed = false;

        // 同步 try-catch 仍保留：
        //   - 低版本 Addressables 对无效 Label 会【同步】抛 InvalidKeyException
        //   - 参数为 null 等场景也会同步抛
        // 但注意：高版本 Addressables 对无效 Label 是【异步失败】，
        //       这类错误 try-catch 抓不到，必须靠下面的 handle.Status 判定。
        try
        {
            handle = Addressables.DownloadDependenciesAsync(label, false);
        }
        catch (System.Exception e)
        {
            Z_Logger.LogWarning($"[Preload] ⚠️ 标签 '{label}' 不存在或无效（同步异常），跳过预下载。详细信息: {e.Message}");
            syncFailed = true;
        }

        if (syncFailed || !handle.IsValid())
        {
            // 同步失败 或 handle 无效：直接收尾，不要进入轮询
            if (isTier1) _tier1Running = false;
            else _tier2Running = false;
            yield break;
        }

        // ---------------- 轮询进度（同时防御异步失败） ----------------
        // 关键点：无效 Label 的异步失败会体现为 handle.Status == Failed，
        //         此时 handle.IsDone 也为 true，循环会自动退出；
        //         这里额外再判断一次 Status，防止个别版本 IsDone 延迟。
        while (!handle.IsDone)
        {
            if (handle.Status == AsyncOperationStatus.Failed)
                break;

            float p = handle.GetDownloadStatus().Percent;
            if (isTier1) Tier1Progress = p;
            else Tier2Progress = p;
            yield return null;
        }

        // ---------------- 结果判定（从这里读取真正的错误） ----------------
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Z_Logger.Log($"[Preload] ✅ {label} 预下载完成");
            if (isTier1) Tier1Done = true;
            else Tier2Done = true;
        }
        else
        {
            var ex = handle.OperationException;

            // 区分「标签不存在」和「真正的下载失败」，日志语义更清晰
            if (ex is InvalidKeyException ||
                (ex != null && ex.Message.IndexOf("InvalidKey", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Z_Logger.LogWarning($"[Preload] ⚠️ 标签 '{label}' 不存在，跳过预下载。（这不是错误，仅提示）");
            }
            else
            {
                Z_Logger.LogWarning($"[Preload] ⚠️ {label} 预下载失败: {ex?.Message}");
            }
        }

        // ---------------- 收尾 ----------------
        if (handle.IsValid()) Addressables.Release(handle);

        if (isTier1) _tier1Running = false;
        else _tier2Running = false;
    }

    public string GetStatus()
    {
        return $"[Preload] Tier1: {(Tier1Done ? "✓" : $"{Tier1Progress:P0}")}  " +
               $"Tier2: {(Tier2Done ? "✓" : $"{Tier2Progress:P0}")}";
    }
}
