using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

public class InitUIState : AbstractState<LaunchStates, Launch>, IController {
    public InitUIState(FSM<LaunchStates> fsm, Launch target) : base(fsm, target) {
    }

    public override async void OnEnter() {
     await UIController.Instance.InitUI();
        ChangeState();
    }

    private void ChangeState() {
        mFSM.ChangeState(LaunchStates.GameMain);
    }

    public override void OnExit() {

        //UIController.Instance.ShowPage(new ShowPageInfo(UIPageType.LoadingUI, UILevelType.Prepare));
    }

    public IArchitecture GetArchitecture() {
        return GameSystemEventRegister.Interface;
    }
}