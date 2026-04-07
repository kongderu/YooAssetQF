using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;

public enum GameType
{
    TimeTrial = 0,
    Match
}

public interface IGameModel : IModel
{
    public BindableProperty<bool> SceneLoaded { get; }
    public BindableProperty<bool> SceneLoading { get; }
    public BindableProperty<SceneID> LoadingTargetSceneID { get; }
}
public class GameModel : AbstractModel, IGameModel
{
    public BindableProperty<bool> SceneLoaded { get; } = new BindableProperty<bool>();
    public BindableProperty<bool> SceneLoading { get; } = new BindableProperty<bool>();
    public BindableProperty<SceneID> LoadingTargetSceneID { get; } = new BindableProperty<SceneID>();

    protected override void OnInit()
    {
        SceneLoaded.Value = false;
    }
}