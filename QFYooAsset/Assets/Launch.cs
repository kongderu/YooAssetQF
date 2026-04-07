using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using QFramework;
using System.Threading.Tasks;
public enum LaunchStates
{  
    FsmYooInitializePackage,
    FsmYooCheckVersion,
    FsmYooUpdatePackageManifest,
    FsmYooCreateDownloader,
    FsmYooDownloadPackageFiles,
    FsmYooDownloadPackageOver,
    AssetsUpdate,
    Login,
    GameMain,
}
public class Launch : MonoBehaviour, IController
{
    public FSM<LaunchStates> FSM = new FSM<LaunchStates>();
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;
    private async Task Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
       void Start()
    {
        GameManager.Instance.Behaviour = this;

        YooAssets.Initialize();

        GameConst.SetBlackboardValue("PackageName", "DefaultPackage");
        GameConst.SetBlackboardValue("PlayMode", PlayMode);

        FSM.AddState(LaunchStates.FsmYooInitializePackage, new FsmYooInitializePackage(FSM, this));
        FSM.AddState(LaunchStates.FsmYooCheckVersion, new FsmYooCheckVersion(FSM, this));
        FSM.AddState(LaunchStates.FsmYooUpdatePackageManifest, new FsmYooUpdatePackageManifest(FSM, this));
        FSM.AddState(LaunchStates.FsmYooCreateDownloader, new FsmYooCreateDownloader(FSM, this));
        FSM.AddState(LaunchStates.FsmYooDownloadPackageFiles, new FsmYooDownloadPackageFiles(FSM, this));
        FSM.AddState(LaunchStates.FsmYooDownloadPackageOver, new FsmYooDownloadPackageOver(FSM, this));
                
        FSM.AddState(LaunchStates.Login, new InitUIState(FSM, this));
        FSM.AddState(LaunchStates.GameMain, new GameMainState(FSM, this));
        FSM.StartState(LaunchStates.FsmYooInitializePackage);
    }

    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }

}
