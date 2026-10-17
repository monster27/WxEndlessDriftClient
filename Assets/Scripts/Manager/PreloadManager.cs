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

        var handle = Addressables.DownloadDependenciesAsync(label, false);

        while (!handle.IsDone)
        {
            float p = handle.GetDownloadStatus().Percent;
            if (isTier1) Tier1Progress = p;
            else Tier2Progress = p;
            yield return null;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Z_Logger.Log($"[Preload] ✅ {label} 预下载完成");
            if (isTier1) Tier1Done = true;
            else Tier2Done = true;
        }
        else
        {
            Z_Logger.LogWarning($"[Preload] ⚠️ {label} 预下载失败: {handle.OperationException?.Message}");
        }

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
