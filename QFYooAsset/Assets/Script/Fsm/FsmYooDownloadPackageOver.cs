using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset;
public class FsmYooDownloadPackageOver: AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooDownloadPackageOver(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
       public override void OnEnter()
    {
        Debug.LogError("下载资源包完成");
       
        mFSM.ChangeState(LaunchStates.FsmYooClearCacheBundle);
    }
    public override void OnExit()
    {
    }
    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }

}
