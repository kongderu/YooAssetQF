using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset; 
public class FsmYooClearCacheBundle : AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooClearCacheBundle(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
       public override void OnEnter()
    {
          PatchEventDefine.PatchStepsChange.SendEventMessage("清理未使用的缓存文件！");
        var packageName = (string)GameConst.GetBlackboardValue("PackageName");
        var package = YooAssets.GetPackage(packageName);
        var operation = package.ClearCacheFilesAsync(EFileClearMode.ClearUnusedBundleFiles);
        operation.Completed += Operation_Completed;
    }
      private void Operation_Completed(YooAsset.AsyncOperationBase obj)
    {
          mFSM.ChangeState(LaunchStates.Login);
    }
       public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }
}
