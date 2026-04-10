using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor;
using System.IO;
using System.Linq;
using System.Xml;
using HybridCLR.Editor.AOT;
using HybridCLR.Editor.HotUpdate;
using HybridCLR.Editor.Meta;
using Newtonsoft.Json;
using UnityEditor.Build.Reporting;


public class BuildHelper
{
    public static string ProjectPath = Directory.GetParent(Application.dataPath).FullName;
    /// <summary>
    /// 一般来说，发布热更新包时，由于中间可能调用过generate/all，SettingsUtil.GetAssembliesPostIl2CppStripDir(target)目录中包含了最新的aot dll，
    /// 肯定无法检查出类型或者函数裁剪的问题。
    /// 需要在构建完主包后，将当时的aot dll保存下来，供后面补充元数据或者裁剪检查。
    ///  换句话说,验证的可靠性是建立在当前热更新数据对比上次构建应用时已被裁切后的AOTDLL进行对比得到的
    /// </summary>
    /// <returns></returns>
    [MenuItem("HybridTool/验证元数据是否需要补充")]
    public static bool CheckAccessMissingMetadata()
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;


        string aotDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);

        // 第2个参数hotUpdateAssNames为热更新程序集列表。对于旗舰版本，该列表需要包含DHE程序集，即SettingsUtil.HotUpdateAndDHEAssemblyNamesIncludePreserved。
        var checker = new MissingMetadataChecker(aotDir, SettingsUtil.HotUpdateAssemblyNamesIncludePreserved);

        string hotUpdateDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
        bool notAnyMissing = false;
        foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
        {
            string dllPath = $"{hotUpdateDir}/{dll}";
            notAnyMissing = checker.Check(dllPath);
            if (!notAnyMissing)
            {
                Debug.unityLogger.LogError("验证元数据",$"验证{dll}失败");
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// 获取AOT之前,应先编译热更新代码
    /// 执行之前需要先编译热更新代码 CompileDllCommand.CompileDllActiveBuildTarget()
    ///
    /// 通过对比热更新DLL和AOTDLL,获取需要补充元数据的AOTDLL
    /// 并将结果写入HybridCLRSettings中
    /// </summary>
    public static void GetPatchedAOTAssemblyListToHybridCLRSettings()
    {
        var gs = SettingsUtil.HybridCLRSettings;
        List<string> hotUpdateDllNames = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;

        AssemblyReferenceDeepCollector collector = new AssemblyReferenceDeepCollector(
            MetaUtil.CreateHotUpdateAndAOTAssemblyResolver(EditorUserBuildSettings.activeBuildTarget,
                hotUpdateDllNames), hotUpdateDllNames);
        var analyzer = new Analyzer(new Analyzer.Options
        {
            MaxIterationCount = Math.Min(20, gs.maxGenericReferenceIteration),
            Collector = collector,
        });

        analyzer.Run();

        var types = analyzer.AotGenericTypes.ToList();
        var methods = analyzer.AotGenericMethods.ToList();

        List<dnlib.DotNet.ModuleDef> modules = new HashSet<dnlib.DotNet.ModuleDef>(
            types.Select(t => t.Type.Module).Concat(methods.Select(m => m.Method.Module))).ToList();
        modules.Sort((a, b) => a.Name.CompareTo(b.Name));

        List<string> patchtedAOTAssemblys = new List<string>();
        foreach (dnlib.DotNet.ModuleDef module in modules)
        {
            //替换掉程序集的拓展名,以方便后续拷贝AOTDll的时候可以和HotUpdateDll共用相同的拷贝逻辑
            var patchtedAOTAssemblysName = module.Name.Replace(".dll", string.Empty);
            Debug.Log($"需要补充元数据的AOT ========= {patchtedAOTAssemblysName}");
            patchtedAOTAssemblys.Add(patchtedAOTAssemblysName);
        }

        gs.patchAOTAssemblies = patchtedAOTAssemblys.ToArray();
    }

    [MenuItem("HybridTool/获取需要补充元数据的Dll")]
    public static void Debug_GetPatchedAOTAssemblyList()
    {
        CompileDllCommand.CompileDllActiveBuildTarget();

        GetPatchedAOTAssemblyListToHybridCLRSettings();
    }

    public static List<string> CopyDllFileToByte(string[] originFileNames, string originDir, string targetDir)
    {
        List<string> bytesFiles = new List<string>();
        Debug.Log($"originDir===>{originFileNames.Length}");
        foreach (var originFileName in originFileNames)
        {
            var dllFilePath = Path.Combine(ProjectPath, originDir, $"{originFileName}.dll");
            if (!File.Exists(dllFilePath))
            {
                Debug.Log($"{dllFilePath}不存在");
                continue;
            }

            var targetFileName = $"{originFileName}.bytes";
            var dllRawFilePath = Path.Combine(targetDir, targetFileName);
            File.Copy(dllFilePath, dllRawFilePath, true);
            bytesFiles.Add(originFileName);
        }

        return bytesFiles;
    }

    /// <summary>
    /// 将生成裁剪后的AOT dlls拷贝到AssetBundle打包路径下
    /// 依赖于   HybridCLR/Generate/Il2CppDef
    /// HybridCLR/Generate/LinkXmlH
    /// ybridCLR/Generate/AotDlls  三条指令生成数据
    /// </summary>
    /// <param name="rawFileCollectPath"></param>
    public static void CopyPatchedAOTDllToCollectPath(string rawFileCollectPath)
    {
        if (string.IsNullOrEmpty(rawFileCollectPath))
        {
            Debug.unityLogger.LogError("CopyPatchedAOTDllToCollectPath", $"{nameof(rawFileCollectPath)}===>Null");
            return;
        }

        var patchedAOTAssemblies = SettingsUtil.HybridCLRSettings.patchAOTAssemblies;

        var dllOutputPath = SettingsUtil.GetAssembliesPostIl2CppStripDir(EditorUserBuildSettings.activeBuildTarget);

        var dllRawFileAssetNames = CopyDllFileToByte(patchedAOTAssemblies, dllOutputPath, rawFileCollectPath);

        if (dllRawFileAssetNames != null && dllRawFileAssetNames.Count > 0)
        {
            var namesJson = JsonConvert.SerializeObject(dllRawFileAssetNames);
            File.WriteAllText($"{rawFileCollectPath}/AOTDLLs.txt", namesJson);
            AssetDatabase.Refresh();
            Debug.unityLogger.Log("CopyPatchedAOTDllToCollectPath Success!");
        }
        else
        {
            Debug.unityLogger.LogError("CopyPatchedAOTDllToCollectPath", $"{nameof(dllRawFileAssetNames)}===>Null");
        }
    }

    [MenuItem("HybridTool/生成AOT补充文件并复制进文件夹")]
    public static void Debug_GenerateAOTDllListFile()
    {
        //先生成AOT文件
        Il2CppDefGeneratorCommand.GenerateIl2CppDef();
        LinkGeneratorCommand.GenerateLinkXml();
        StripAOTDllCommand.GenerateStripedAOTDlls();

        var aotDllRawFileCollectPath = Path.Combine(Application.dataPath, "HotUpdateAssets", "PatchedAOTDLL");

        Debug.unityLogger.Log(aotDllRawFileCollectPath);
        CopyPatchedAOTDllToCollectPath(aotDllRawFileCollectPath);
    }

    [MenuItem("HybridTool/生成热更新Dll并复制进文件夹")]
    public static void Debug_GenerateHotUpdateDllListFile()
    {
        CompileDllCommand.CompileDllActiveBuildTarget();

        var hotUpdateDllRawFileCollectPath = Path.Combine(Application.dataPath, "HotUpdateAssets", "HotUpdateDLL");

        Debug.unityLogger.Log(hotUpdateDllRawFileCollectPath);
        CopyHotUpdateDllToCollectPath(hotUpdateDllRawFileCollectPath);
    }

    /// <summary>
    /// 将生成裁剪后的HotUpdate dlls拷贝到AssetBundle打包路径下
    /// 依赖于   CompileDllCommand.CompileDllActiveBuildTarget()  生成数据
    /// </summary>
    /// <param name="rawFileCollectPath"></param>
    public static void CopyHotUpdateDllToCollectPath(string rawFileCollectPath)
    {
        if (string.IsNullOrEmpty(rawFileCollectPath))
        {
            Debug.unityLogger.LogError("CopyHotUpdateDllToCollectPath", $"{nameof(rawFileCollectPath)}===>Null");
            return;
        }

        var hotUpdateAssemblies = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;

        var hotUpdateOutputPath =
            SettingsUtil.GetHotUpdateDllsOutputDirByTarget(EditorUserBuildSettings.activeBuildTarget);

        var dllRawFileAssetNames =
            CopyDllFileToByte(hotUpdateAssemblies.ToArray(), hotUpdateOutputPath, rawFileCollectPath);

        if (dllRawFileAssetNames != null && dllRawFileAssetNames.Count > 0)
        {
            var json = JsonConvert.SerializeObject(dllRawFileAssetNames);
            File.WriteAllText(Path.Combine(rawFileCollectPath, "HotUpdateDLLs.txt"), json);
            AssetDatabase.Refresh();
            Debug.unityLogger.Log("CopyHotUpdateDllToCollectPath  Success");
        }
        else
        {
            Debug.unityLogger.LogError("CopyHotUpdateDllToCollectPath", $"{nameof(dllRawFileAssetNames)}===>Null");
        }
    }



}