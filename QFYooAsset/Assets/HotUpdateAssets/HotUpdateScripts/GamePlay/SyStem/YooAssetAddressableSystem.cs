using System;
using System.Collections;
using UnityEngine;
using QFramework;
using YooAsset;
using Cysharp.Threading.Tasks;
public class YooAssetInfo
{

    public static string BaseURL;
}

public struct YooAssetAsyncHandle<T> where T : UnityEngine.Object
{
    public EOperationStatus Status;  
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

    protected override void OnInit()
    {
        _package = YooAssets.TryGetPackage(_packageName) ?? YooAssets.CreatePackage(_packageName);
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



}
