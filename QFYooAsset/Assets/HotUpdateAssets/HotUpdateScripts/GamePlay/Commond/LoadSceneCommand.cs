using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using UnityEngine.SceneManagement;
    public enum SceneID
    {
        Login = 0,
        Index = 1,
        Loading = 2,
        Game = 3
    }

public class LoadSceneCommand : AbstractCommand {
    private SceneID mSceneID;

    public LoadSceneCommand(SceneID sceneID) {
        Debug.Log($"准备加载场景：{sceneID}");
        mSceneID = sceneID;
    }

    protected override void OnExecute() {
        if (this.GetModel<IGameModel>().SceneLoading.Value) {
            return;
        }

        this.GetModel<IGameModel>().LoadingTargetSceneID.Value = mSceneID;
        SceneManager.LoadScene((int)SceneID.Loading);
    }
}