using System;
using System.Collections;
using UnityEngine;
using QFramework;
using YooAsset;
using Cysharp.Threading.Tasks;
public class YooAssetInfo
{
    /// <summary>
    /// 请在运行前设置远端地址，例如："http://cdn.example.com/YourGame"
    /// </summary>
    public static string BaseURL;
}

public struct YooAssetAsyncHandle<T> where T : UnityEngine.Object
{
    public EOperationStatus Status;  // YooAsset 操作状态
    public T Result;
    public string Error;
    public float Progress;
}

public interface IAddressableSystem : ISystem
{
    void SetCallBack(Action<long> OnCheckCompleteNeedUpdate = null,
        Action OnCompleteDownload = null,
        Action OnCheckCompleteNoUpdate = null,
        Action<float, long> OnUpdate = null);

    void GetDownloadAssets();
    void DownloadAsset();

    UniTask<YooAssetAsyncHandle<T>> LoadAssetAsync<T>(string path) where T : UnityEngine.Object;
    YooAssetAsyncHandle<T> LoadAsset<T>(string path) where T : UnityEngine.Object;
}

public class YooAssetAddressableSystem : AbstractSystem, IAddressableSystem
{
    private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";

    private string _packageName = DEFAULT_PACKAGE_NAME;
    private EPlayMode _playMode = EPlayMode.HostPlayMode;

    private ResourcePackage _package;
    private ResourceDownloaderOperation _downloader;

    private long _totalDownloadBytes;
    private int _downloadingMaxNum = 10;
    private int _failedTryAgain = 3;

    private Action<long> _onCheckCompleteNeedUpdate;
    private Action _onCheckCompleteNoUpdate;
    private Action _onCompleteDownload;
    private Action<float, long> _onUpdate;

    public void SetCallBack(Action<long> OnCheckCompleteNeedUpdate = null,
        Action OnCompleteDownload = null,
        Action OnCheckCompleteNoUpdate = null,
        Action<float, long> OnUpdate = null)
    {
        _onCheckCompleteNeedUpdate = OnCheckCompleteNeedUpdate;
        _onCompleteDownload = OnCompleteDownload;
        _onCheckCompleteNoUpdate = OnCheckCompleteNoUpdate;
        _onUpdate = OnUpdate;
    }

    public void SetPackageName(string packageName)
    {
        if (string.IsNullOrEmpty(packageName) == false)
            _packageName = packageName;
    }

    public void SetPlayMode(EPlayMode playMode)
    {
        _playMode = playMode;
    }

    protected override void OnInit()
    {
        YooAssets.Initialize();
        _package = YooAssets.TryGetPackage(_packageName) ?? YooAssets.CreatePackage(_packageName);
    }

    public void GetDownloadAssets()
    {
        StartCoroutineSafe(CheckForUpdateCoroutine());
    }

    public void DownloadAsset()
    {
        if (_downloader == null)
        {
            Debug.LogWarning("YooAssetAddressableSystem: downloader is not prepared. Call GetDownloadAssets() first.");
            return;
        }

        StartCoroutineSafe(DownloadUpdateCoroutine());
    }

    public async UniTask<YooAssetAsyncHandle<T>> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("path is null or empty", nameof(path));

        var handle = YooAssets.LoadAssetAsync<T>(path);
        if (handle == null)
            return new YooAssetAsyncHandle<T> { Status = EOperationStatus.Failed, Error = $"YooAssets.LoadAssetAsync<{typeof(T).Name}>('{path}') returned null." };
        await handle.Task;
        return new YooAssetAsyncHandle<T>
        {
            Status = handle.Status == EOperationStatus.Succeed ? EOperationStatus.Succeed : EOperationStatus.Failed,
            Result = handle.AssetObject as T,
            Error = handle.LastError,
            Progress = handle.Progress
        };
    }

    public YooAssetAsyncHandle<T> LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("path is null or empty", nameof(path));

        var handle = YooAssets.LoadAssetSync<T>(path);
        if (handle == null || handle.Status != EOperationStatus.Succeed)
        {
            return new YooAssetAsyncHandle<T>
            {
                Status = handle == null ? EOperationStatus.Failed : (handle.Status == EOperationStatus.Succeed ? EOperationStatus.Succeed : EOperationStatus.Failed),
                Result = handle?.AssetObject as T,
                Error = handle?.LastError,
                Progress = handle?.Progress ?? 0f
            };
        }

        return new YooAssetAsyncHandle<T>
        {
            Status = EOperationStatus.Succeed,
            Result = handle.AssetObject as T,
            Error = string.Empty,
            Progress = handle.Progress
        };
    }

    private IEnumerator CheckForUpdateCoroutine()
    {
        if (_package == null)
        {
            _package = YooAssets.TryGetPackage(_packageName) ?? YooAssets.CreatePackage(_packageName);
        }

        // 初始化包裹（如果尚未初始化）
        if (!_package.PackageValid)
        {
            InitializationOperation initOp = CreateInitializationOperation();
            if (initOp == null)
            {
                Debug.LogError("YooAssetAddressableSystem: create initialization operation failed");
                _onCheckCompleteNoUpdate?.Invoke();
                yield break;
            }

            yield return initOp;
            if (initOp.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"YooAsset initialize failed: {initOp.Error}");
                _onCheckCompleteNoUpdate?.Invoke();
                yield break;
            }
        }

        // 获取服务器版本
        var versionOp = _package.RequestPackageVersionAsync();
        yield return versionOp;
        if (versionOp.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning($"YooAsset request version failed: {versionOp.Error}");
            _onCheckCompleteNoUpdate?.Invoke();
            yield break;
        }

        var remoteVersion = versionOp.PackageVersion;
        if (string.IsNullOrEmpty(remoteVersion))
        {
            Debug.Log("YooAsset request version empty, no update needed");
            _onCheckCompleteNoUpdate?.Invoke();
            yield break;
        }

        // 更新清单
        var updateOp = _package.UpdatePackageManifestAsync(remoteVersion);
        yield return updateOp;
        if (updateOp.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning($"YooAsset update manifest failed: {updateOp.Error}");
            _onCheckCompleteNoUpdate?.Invoke();
            yield break;
        }

        // 创建下载器
        _downloader = _package.CreateResourceDownloader(_downloadingMaxNum, _failedTryAgain);

        if (_downloader.TotalDownloadCount == 0)
        {
            Debug.Log("YooAsset: no update files found");
            _onCheckCompleteNoUpdate?.Invoke();
            yield break;
        }

        _totalDownloadBytes = _downloader.TotalDownloadBytes;
        _onCheckCompleteNeedUpdate?.Invoke(_totalDownloadBytes);
    }

    private IEnumerator DownloadUpdateCoroutine()
    {
        if (_downloader == null)
        {
            yield break;
        }

        _downloader.DownloadErrorCallback = (data) =>
        {
            Debug.LogWarning($"YooAsset download error: {data.ErrorInfo} [{data.PackageName}/{data.FileName}]");
        };

        _downloader.DownloadUpdateCallback = (data) =>
        {
            _onUpdate?.Invoke(data.Progress, data.TotalDownloadBytes);
        };

        _downloader.BeginDownload();
        yield return _downloader;

        if (_downloader.Status == EOperationStatus.Succeed)
        {
            _onCompleteDownload?.Invoke();
        }
        else
        {
            Debug.LogWarning($"YooAsset download operation failed: {_downloader.Error}");
        }
    }

    private InitializationOperation CreateInitializationOperation()
    {
        if (_package == null)
            return null;

        switch (_playMode)
        {
            case EPlayMode.EditorSimulateMode:
#if UNITY_EDITOR
                var buildResult = EditorSimulateModeHelper.SimulateBuild(_packageName);
                var editorParam = new EditorSimulateModeParameters();
                editorParam.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
                return _package.InitializeAsync(editorParam);
#else
                Debug.LogError("EditorSimulateMode only available in Unity Editor");
                return null;
#endif

            case EPlayMode.OfflinePlayMode:
                var offlineParam = new OfflinePlayModeParameters();
                offlineParam.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                return _package.InitializeAsync(offlineParam);

            case EPlayMode.HostPlayMode:
                string hostURL = string.IsNullOrEmpty(YooAssetInfo.BaseURL) ? "http://127.0.0.1" : YooAssetInfo.BaseURL;
                var remoteServices = new YooAssetInfoRemoteServices(hostURL, hostURL);
                var hostParam = new HostPlayModeParameters();
                hostParam.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                hostParam.CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
                return _package.InitializeAsync(hostParam);

            case EPlayMode.WebPlayMode:
                var webParam = new WebPlayModeParameters();
                webParam.WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                return _package.InitializeAsync(webParam);

            default:
                Debug.LogError($"YooAssetAddressableSystem does not support play mode {_playMode}");
                return null;
        }
    }

    private void StartCoroutineSafe(IEnumerator coroutine)
    {
        if (coroutine == null)
            return;

        // 如果你项目里有CoroutineController，优先使用
        var cc = Type.GetType("CoroutineController");
        if (cc != null)
        {
            var instanceProp = cc.GetProperty("Instance");
            var instance = instanceProp?.GetValue(null);
            var startMethod = cc.GetMethod("StartCoroutine", new Type[] { typeof(IEnumerator) });
            startMethod?.Invoke(instance, new object[] { coroutine });
            return;
        }

        // 回退到 GameManager（如果项目里有）
        if (GameManager.Instance != null && GameManager.Instance.Behaviour != null)
        {
            GameManager.Instance.StartCoroutine(coroutine);
            return;
        }

        Debug.LogError("YooAssetAddressableSystem: no coroutine runner found (need CoroutineController/ GameManager)");
    }

    private class YooAssetInfoRemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public YooAssetInfoRemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }
}
