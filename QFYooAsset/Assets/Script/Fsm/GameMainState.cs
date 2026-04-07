using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMainState : AbstractState<LaunchStates, Launch>, IController
{
    public GameMainState(FSM<LaunchStates> fsm, Launch owner) : base(fsm, owner)
    {
    }
    public override async void OnEnter()
    {
        Debug.LogError("进入游戏主界面");
        // this.SendCommand<EnableMainSceneCommand>();
         UIController.Instance.ShowPage(new ShowPageInfo(UIPageType.UIHome, UILevelType.Main));
        
    }
    public IArchitecture GetArchitecture()
    {
        return GameSystemEventRegister.Interface;
    }
}
