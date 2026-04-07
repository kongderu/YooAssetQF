using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset;
public class FsmYooCheckVersion : AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooCheckVersion(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
       public override void OnEnter()
    {
        GameManager.Instance.StartCoroutine(UpdatePackageVersion());
    }
     private IEnumerator UpdatePackageVersion()
    {
        var packageName = (string)GameConst.GetBlackboardValue("PackageName");
        var package = YooAssets.GetPackage(packageName);
        var operation = package.RequestPackageVersionAsync();
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning(operation.Error);
           
        }
        else
        {
            Debug.LogWarning($"Request package version : {operation.PackageVersion}");
            GameConst.SetBlackboardValue("PackageVersion", operation.PackageVersion);
            mFSM.ChangeState(LaunchStates.FsmYooUpdatePackageManifest);
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
