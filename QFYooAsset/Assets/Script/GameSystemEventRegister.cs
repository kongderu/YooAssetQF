using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.StandaloneInputModule;

public class GameSystemEventRegister : Architecture<GameSystemEventRegister>
{
    protected override void Init()
    {
        RegisterModel<IGameModel>(new GameModel());

        RegisterSystem<IAddressableSystem>(new YooAssetAddressableSystem());

    }
}
