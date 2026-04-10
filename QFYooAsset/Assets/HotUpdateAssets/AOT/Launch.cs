using System;
using System.Collections;
using UnityEngine;
using YooAsset;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

public class Launch : MonoBehaviour
{
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;
    public string PackageName = "DefaultPackage";
    private async Task Awake()
    {

        DontDestroyOnLoad(gameObject);

    }
    IEnumerator Start()
    {
        GameManager.Instance.Behaviour = this;

        yield return Initialize(PackageName, PlayMode);
        LoadHotfix();
    }

    public static IEnumerator Initialize(string defaultPackage, EPlayMode playMode)
    {
        YooAssets.Initialize();
        var operation = new PatchOperation(defaultPackage, playMode);
        YooAssets.StartOperation(operation);
        yield return operation;
        var gamePackage = YooAssets.GetPackage(defaultPackage);
        YooAssets.SetDefaultPackage(gamePackage);
    }
    private async void LoadHotfix()
    {
        string defaultHotfix = PackageName;
        var gamePackage = YooAssets.GetPackage(defaultHotfix);
#if UNITY_EDITOR
        var patchAOTInfos = gamePackage.GetAssetInfos("PatchAOT");
        foreach (var info in patchAOTInfos)
        {
            var patchAOTAsset = YooAssetLoader.LoadAssetSync<TextAsset>(info.Address);
            var patchAOTAssetBytes = patchAOTAsset.bytes;
            HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(patchAOTAssetBytes, HybridCLR.HomologousImageMode.Consistent);
        }
#endif

        Assembly hotUpdateAss = null;

#if !UNITY_EDITOR
    var hotfixInfo = gamePackage.GetAssetInfo("HotUpdate.bytes"); 
    if (!hotfixInfo.IsInvalid)
    {
        Debug.LogError($"HotUpdate.bytes 资源无效！Address 配置错误或未打包");
        return;
    }
    
    var hotfixAsset = YooAssetLoader.LoadAssetSync<TextAsset>(hotfixInfo.Address);
    if (hotfixAsset == null)
    {
        Debug.LogError($"加载 HotUpdate.bytes 失败！");
        return;
    }
    
    var hotfixAssetBytes = hotfixAsset.bytes;
    hotUpdateAss = Assembly.Load(hotfixAssetBytes);
#else
        hotUpdateAss = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "HotUpdate");
#endif

        var type = hotUpdateAss.GetType("GamePlay.Main");
        var method = type.GetMethod("Start");
        var task = (Task)method.Invoke(null, null);
        await task;
        Debug.Log("✅ 热更异步启动完成");
    }
}

