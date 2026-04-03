using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using QFramework;
using System.Threading.Tasks;
public enum LaunchStates
{
    Login,
    GameMain,
}
public class Launch : MonoBehaviour, IController
{
    public FSM<LaunchStates> FSM = new FSM<LaunchStates>();
     public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;
    private async Task Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
       IEnumerator Start()
    {

        GameManager.Instance.Behaviour = this;   
        // 初始化资源系统
        YooAssets.Initialize();
        var operation = new PatchOperation("DefaultPackage", PlayMode);
        YooAssets.StartOperation(operation);
        yield return operation;

        // 设置默认的资源包
        var gamePackage = YooAssets.GetPackage("DefaultPackage");
        YooAssets.SetDefaultPackage(gamePackage);
       FSM.AddState(LaunchStates.Login, new InitUIState(FSM, this));
        FSM.AddState(LaunchStates.GameMain, new GameMainState(FSM, this));
        FSM.StartState(LaunchStates.Login);
    }
    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }

}
