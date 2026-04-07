using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset;
public class FsmYooUpdatePackageManifest : AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooUpdatePackageManifest(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
       public override void OnEnter()
    {  GameManager.Instance.StartCoroutine(UpdateManifest());
      
    }
     private IEnumerator UpdateManifest()
    {
        var packageName = (string)GameConst.GetBlackboardValue("PackageName");
        var packageVersion = (string)GameConst.GetBlackboardValue("PackageVersion");
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageManifestAsync(packageVersion);
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning(operation.Error);
            PatchEventDefine.PackageManifestUpdateFailed.SendEventMessage();
            yield break;
        }
        else
        {
             mFSM.ChangeState(LaunchStates.FsmYooCreateDownloader);
        }
    }
    public override void OnExit()
    {

    }
    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }

}