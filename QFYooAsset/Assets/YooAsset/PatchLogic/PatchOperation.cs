using UnityEngine;
using UniFramework.Machine;
using UniFramework.Event;
namespace YooAsset
{
    public class PatchOperation : GameAsyncOperation
    {
        private enum ESteps
        {
            None,
            Update,
            Done,
        }

        private readonly EventGroup _eventGroup = new EventGroup();
        private readonly StateMachine _machine;
        private readonly string _packageName;
        private ESteps _steps = ESteps.None;

        public PatchOperation(string packageName, EPlayMode playMode)
        {
            _packageName = packageName;

            // 注册监听事件
            _eventGroup.AddListener<UserEventDefine.UserTryInitialize>(OnHandleEventMessage);
            _eventGroup.AddListener<UserEventDefine.UserBeginDownloadWebFiles>(OnHandleEventMessage);
            _eventGroup.AddListener<UserEventDefine.UserTryRequestPackageVersion>(OnHandleEventMessage);
            _eventGroup.AddListener<UserEventDefine.UserTryUpdatePackageManifest>(OnHandleEventMessage);
            _eventGroup.AddListener<UserEventDefine.UserTryDownloadWebFiles>(OnHandleEventMessage);

            _eventGroup.AddListener<PatchEventDefine.InitializeFailed>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.PatchStepsChange>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.FoundUpdateFiles>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.DownloadUpdate>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.PackageVersionRequestFailed>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.PackageManifestUpdateFailed>(OnHandleEventMessage);
            _eventGroup.AddListener<PatchEventDefine.WebFileDownloadFailed>(OnHandleEventMessage);

            // 创建状态机
            _machine = new StateMachine(this);
            _machine.AddNode<FsmInitializePackage>();
            _machine.AddNode<FsmRequestPackageVersion>();
            _machine.AddNode<FsmUpdatePackageManifest>();
            _machine.AddNode<FsmCreateDownloader>();
            _machine.AddNode<FsmDownloadPackageFiles>();
            _machine.AddNode<FsmDownloadPackageOver>();
            _machine.AddNode<FsmClearCacheBundle>();
            _machine.AddNode<FsmStartGame>();

            _machine.SetBlackboardValue("PackageName", packageName);
            _machine.SetBlackboardValue("PlayMode", playMode);
        }
        protected override void OnStart()
        {
            _steps = ESteps.Update;
            _machine.Run<FsmInitializePackage>();
        }
        protected override void OnUpdate()
        {
            if (_steps == ESteps.None || _steps == ESteps.Done)
                return;

            if (_steps == ESteps.Update)
            {
                _machine.Update();
            }
        }
        protected override void OnAbort()
        {
        }

        public void SetFinish()
        {
            _steps = ESteps.Done;
            _eventGroup.RemoveAllListener();
            Status = EOperationStatus.Succeed;
            Debug.Log($"Package {_packageName} patch done !");
        }

           /// <summary>
        /// 接收事件
        /// </summary>
        private void OnHandleEventMessage(IEventMessage message)
        {
            if (message is UserEventDefine.UserTryInitialize)
            {
                _machine.ChangeState<FsmInitializePackage>();
            }
            else if (message is UserEventDefine.UserBeginDownloadWebFiles)
            {
                _machine.ChangeState<FsmDownloadPackageFiles>();
            }
            else if (message is UserEventDefine.UserTryRequestPackageVersion)
            {
                _machine.ChangeState<FsmRequestPackageVersion>();
            }
            else if (message is UserEventDefine.UserTryUpdatePackageManifest)
            {
                _machine.ChangeState<FsmUpdatePackageManifest>();
            }
            else if (message is UserEventDefine.UserTryDownloadWebFiles)
            {
                _machine.ChangeState<FsmCreateDownloader>();
            }
            else if (message is PatchEventDefine.InitializeFailed)
            {
                System.Action callback = () => { UserEventDefine.UserTryInitialize.SendEventMessage(); };
                ShowMessageBox($"Failed to initialize package !", callback);
            }
            else if (message is PatchEventDefine.PatchStepsChange)
            {
                var msg = message as PatchEventDefine.PatchStepsChange;
                UnityEngine.Debug.Log(msg.Tips);
            }
            else if (message is PatchEventDefine.FoundUpdateFiles)
            {
                var msg = message as PatchEventDefine.FoundUpdateFiles;
                System.Action callback = () => { UserEventDefine.UserBeginDownloadWebFiles.SendEventMessage(); };
                float sizeMB = msg.TotalSizeBytes / 1048576f;
                sizeMB = Mathf.Clamp(sizeMB, 0.1f, float.MaxValue);
                string totalSizeMB = sizeMB.ToString("f1");
                ShowMessageBox($"Found update patch files, Total count {msg.TotalCount} Total szie {totalSizeMB}MB", callback);
            }
            else if (message is PatchEventDefine.DownloadUpdate)
            {
                var msg = message as PatchEventDefine.DownloadUpdate;
                float  progress = (float)msg.CurrentDownloadCount / msg.TotalDownloadCount;
                string currentSizeMB = (msg.CurrentDownloadSizeBytes / 1048576f).ToString("f1");
                string totalSizeMB = (msg.TotalDownloadSizeBytes / 1048576f).ToString("f1");
                string progressStr = $"{msg.CurrentDownloadCount}/{msg.TotalDownloadCount} {currentSizeMB}MB/{totalSizeMB}MB";
                Debug.Log(progressStr);
            }
            else if (message is PatchEventDefine.PackageVersionRequestFailed)
            {
                System.Action callback = () => { UserEventDefine.UserTryRequestPackageVersion.SendEventMessage(); };
                ShowMessageBox($"Failed to request package version, please check the network status.", callback);
            }
            else if (message is PatchEventDefine.PackageManifestUpdateFailed)
            {
                System.Action callback = () => { UserEventDefine.UserTryUpdatePackageManifest.SendEventMessage(); };
                ShowMessageBox($"Failed to update patch manifest, please check the network status.", callback);
            }
            else if (message is PatchEventDefine.WebFileDownloadFailed)
            {
                var msg = message as PatchEventDefine.WebFileDownloadFailed;
                System.Action callback = () => { UserEventDefine.UserTryDownloadWebFiles.SendEventMessage(); };
                ShowMessageBox($"Failed to download file : {msg.FileName}", callback);
            }
            else
            {
                throw new System.NotImplementedException($"{message.GetType()}");
            }
        }

        private void ShowMessageBox(string tips, System.Action callback)
        {
            Debug.LogError(tips);
            callback?.Invoke();
        }
    }
}