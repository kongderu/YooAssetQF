using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset;
public class FsmYooCreateDownloader : AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooCreateDownloader(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
       public override void OnEnter()
    {
        CreateDownloader();
    }
     void CreateDownloader()
    {
        var packageName = (string)GameConst.GetBlackboardValue("PackageName");
        var package = YooAssets.GetPackage(packageName);
        int downloadingMaxNum = 10;
        int failedTryAgain = 3;
        var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
        GameConst.SetBlackboardValue("Downloader", downloader);

        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("Not found any download files !");
          mFSM.ChangeState(LaunchStates.Login);
        }
        else
        {
             mFSM.ChangeState(LaunchStates.FsmYooDownloadPackageFiles);
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
