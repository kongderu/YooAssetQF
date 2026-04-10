using UnityEngine;
using QFramework;

namespace GamePlay.UI
{
    public abstract class UIPenal : MonoBehaviour, IController {
        public abstract void InitData(object data);

    public IArchitecture GetArchitecture() {
        return GameSystemEventRegister.Interface;
    }
    
    }

}