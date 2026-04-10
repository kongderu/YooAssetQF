using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace GamePlay
{
public class EnableMainSceneCommand : AbstractCommand
{
    protected override void OnExecute()
    {
       this.SendCommand(new LoadSceneCommand(SceneID.Index));
    }

}
}
