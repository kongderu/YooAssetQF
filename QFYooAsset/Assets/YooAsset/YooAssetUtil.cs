using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YooAsset
{
    public class YooAssetUtil
    {
        public static IEnumerator Initialize(string defaultPackage, EPlayMode playMode, string hostServer, string appVersion)
        {
            // 初始化资源系统
            YooAssets.Initialize();
            var operation = new PatchOperation(defaultPackage, playMode);
            YooAssets.StartOperation(operation);
            yield return operation;

            // 设置默认的资源包
            var gamePackage = YooAssets.GetPackage(defaultPackage);
            YooAssets.SetDefaultPackage(gamePackage);
        }
    }
}