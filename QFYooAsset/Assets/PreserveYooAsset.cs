using UnityEngine;
using YooAsset;

/// <summary>
/// 强制保留 YooAsset 基类，解决打包 IL1999 错误
/// </summary>
public static class PreserveYooAsset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForcePreserveBaseTypes()
    {
        // 强制让编译器认为这些类被使用，绝对不会被裁剪
        System.Type type1 = typeof(GameAsyncOperation);
        System.Type type2 = typeof(PatchOperation);
        
        // 防止编译器优化掉变量
        if (type1 == null || type2 == null)
            throw new System.Exception();
    }
}