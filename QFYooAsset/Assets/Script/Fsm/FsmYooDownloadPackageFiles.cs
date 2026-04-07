using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using YooAsset;
public class FsmYooDownloadPackageFiles : AbstractState<LaunchStates, Launch>, IController
{
    public FsmYooDownloadPackageFiles(FSM<LaunchStates> fsm, Launch target) : base(fsm, target)
    {

    }
    public override void OnEnter()
    {
        GameManager.Instance.StartCoroutine(BeginDownload());
    }
    private IEnumerator BeginDownload()
    {
        var downloader = (ResourceDownloaderOperation)GameConst.GetBlackboardValue("Downloader");
        downloader.DownloadUpdateCallback += OnDownloadProgress;
        downloader.BeginDownload();
        yield return downloader;
        if (downloader.Status != EOperationStatus.Succeed)
            yield break;
        downloader.DownloadUpdateCallback -= OnDownloadProgress;
        downloader = null;
        mFSM.ChangeState(LaunchStates.FsmYooDownloadPackageOver);
    }
     private void OnDownloadProgress(DownloadUpdateData data) 
    {
        float progress = data.Progress;
        this.GetArchitecture().SendEvent(new DownloadProgressEvent
        {
            Progress = progress,
            CurrentBytes = data.CurrentDownloadBytes,
            TotalBytes = data.TotalDownloadBytes,
        });
    }
    public override void OnExit()
    {

    }
    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }

}
