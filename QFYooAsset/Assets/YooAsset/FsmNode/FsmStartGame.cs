using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

internal class FsmStartGame : IStateNode
{
    private PatchOperation _owner;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _owner = machine.Owner as PatchOperation;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("开始游戏！");
        _owner.SetFinish();
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }
}