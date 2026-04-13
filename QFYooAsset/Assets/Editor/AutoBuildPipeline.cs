using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor;
using HybridCLR.Editor.AOT;
using HybridCLR.Editor.Meta;
using Newtonsoft.Json;
using YooAsset;
using YooAsset.Editor;

// 消除命名空间冲突，使用别名
using UnityBuildResult = UnityEditor.Build.Reporting.BuildResult;
using UnityBuildReport = UnityEditor.Build.Reporting.BuildReport;

public class AutoBuildPipeline
{
    public static string ProjectPath = Directory.GetParent(Application.dataPath).FullName;

    private const string PackageName = "DefaultPackage";
    private const string BuildVersion = "1.0";

    #region MenuItem 入口

    [MenuItem("AutoBuild/一键构建APK (AOT->DLL->YooAsset->APK)", false, 0)]
    public static void BuildAll()
    {
        Debug.Log("========== 开始一键构建流程 ==========");

        try
        {
            // 第一步：编译热更新DLL
            Step1_CompileHotUpdateDll();

            // 第二步：获取需要补充元数据的AOT列表并更新HybridCLR设置
            Step2_GetPatchedAOTAssemblyList();

            // 第三步：生成AOT补充文件并复制到收集目录
            Step3_GenerateAOTDlls();

            // 第四步：复制热更新DLL到收集目录
            Step4_CopyHotUpdateDlls();

            // 第五步：构建YooAsset资源包
            Step5_BuildYooAssetBundle();

            // 第六步：构建APK
           // Step6_BuildAPK();

            Debug.Log("========== 一键构建流程完成！ ==========");
        }
        catch (Exception e)
        {
            Debug.LogError($"构建流程失败：{e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("构建失败", $"构建流程出错：{e.Message}", "确定");
        }
    }

    [MenuItem("AutoBuild/Step1 编译热更新DLL", false, 10)]
    public static void Step1_CompileHotUpdateDll()
    {
        Debug.Log("[Step1] 开始编译热更新DLL...");
        CompileDllCommand.CompileDllActiveBuildTarget();
        Debug.Log("[Step1] 热更新DLL编译完成");
    }

    [MenuItem("AutoBuild/Step2 获取需要补充元数据的AOT列表", false, 11)]
    public static void Step2_GetPatchedAOTAssemblyList()
    {
        Debug.Log("[Step2] 开始分析需要补充元数据的AOT...");

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

        List<string> patchedAOTAssemblies = new List<string>();
        foreach (dnlib.DotNet.ModuleDef module in modules)
        {
            var name = module.Name.Replace(".dll", string.Empty);
            Debug.Log($"[Step2] 需要补充元数据的AOT: {name}");
            patchedAOTAssemblies.Add(name);
        }

        gs.patchAOTAssemblies = patchedAOTAssemblies.ToArray();
        Debug.Log($"[Step2] AOT元数据分析完成，共需补充 {patchedAOTAssemblies.Count} 个AOT程序集");
    }

    [MenuItem("AutoBuild/Step3 生成AOT补充文件并复制", false, 12)]
    public static void Step3_GenerateAOTDlls()
    {
        Debug.Log("[Step3] 开始生成AOT补充文件...");

        // 生成AOT文件
        Il2CppDefGeneratorCommand.GenerateIl2CppDef();
        LinkGeneratorCommand.GenerateLinkXml();
        StripAOTDllCommand.GenerateStripedAOTDlls();

        // 复制到收集目录
        var aotDllRawFileCollectPath = Path.Combine(Application.dataPath, "HotUpdateAssets", "PatchedAOTDLL");
        CopyPatchedAOTDllToCollectPath(aotDllRawFileCollectPath);

        Debug.Log("[Step3] AOT补充文件生成并复制完成");
    }

    [MenuItem("AutoBuild/Step4 复制热更新DLL到收集目录", false, 13)]
    public static void Step4_CopyHotUpdateDlls()
    {
        Debug.Log("[Step4] 开始复制热更新DLL...");

        var hotUpdateDllRawFileCollectPath = Path.Combine(Application.dataPath, "HotUpdateAssets", "HotUpdateDLL");
        CopyHotUpdateDllToCollectPath(hotUpdateDllRawFileCollectPath);

        Debug.Log("[Step4] 热更新DLL复制完成");
    }

    [MenuItem("AutoBuild/Step5 构建YooAsset资源包", false, 14)]
    public static void Step5_BuildYooAssetBundle()
    {
        Debug.Log("[Step5] 开始构建YooAsset资源包...");

        string packageName = GetFirstPackageName();
        if (string.IsNullOrEmpty(packageName))
        {
            throw new Exception("未找到任何YooAsset Package，请检查收集器配置！");
        }

        string pipelineName = AssetBundleBuilderSetting.GetPackageBuildPipeline(packageName);
        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

        // 从设置中读取参数
        var fileNameStyle = AssetBundleBuilderSetting.GetPackageFileNameStyle(packageName, pipelineName);
        var buildinFileCopyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(packageName, pipelineName);
        var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(packageName, pipelineName);
        var clearBuildCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(packageName, pipelineName);
        var useAssetDependencyDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(packageName, pipelineName);
        var buildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;

        // 根据管线类型创建构建参数
        BuildParameters buildParameters = CreateBuildParameters(pipelineName, packageName, buildTarget,
            fileNameStyle, buildinFileCopyOption, buildinFileCopyParams, compressOption,
            clearBuildCache, useAssetDependencyDB);

        // 运行构建
        BuildResult buildResult = RunBuildPipeline(pipelineName, buildParameters);

        if (buildResult.Success)
        {
            Debug.Log($"[Step5] YooAsset资源包构建成功！输出目录: {buildResult.OutputPackageDirectory}");
        }
        else
        {
            throw new Exception($"YooAsset资源包构建失败！错误: {buildResult.ErrorInfo}");
        }
    }

    [MenuItem("AutoBuild/Step6 构建APK", false, 15)]
    public static void Step6_BuildAPK()
    {
        Debug.Log("[Step6] 开始构建APK...");

        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
        if (buildTarget != BuildTarget.Android)
        {
            Debug.LogWarning($"[Step6] 当前构建目标为 {buildTarget}，非Android平台，将切换到Android平台构建");
            // 注意：切换平台需要重新编译，这里仅提示
        }

        // 刷新资源
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 获取场景列表
        string[] scenes = GetEnabledScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogWarning("[Step6] 未找到启用的场景，将使用空场景列表构建");
        }

        // 构建APK输出路径
        string apkOutputDir = Path.Combine(ProjectPath, "BuildAPK");
        if (!Directory.Exists(apkOutputDir))
        {
            Directory.CreateDirectory(apkOutputDir);
        }

        string apkFileName = $"{PlayerSettings.productName}_{BuildVersion}_{DateTime.Now:yyyyMMdd_HHmmss}.apk";
        string apkOutputPath = Path.Combine(apkOutputDir, apkFileName);

        // 配置构建选项
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        // 执行构建
        UnityBuildReport report = UnityEditor.BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityBuildResult.Succeeded)
        {
            Debug.Log($"[Step6] APK构建成功！输出路径: {apkOutputPath}");
            EditorUtility.RevealInFinder(apkOutputDir);
        }
        else
        {
            string errorMsg = report.summary.result == UnityBuildResult.Failed
                ? $"APK构建失败！共 {report.summary.totalErrors} 个错误"
                : $"APK构建未成功，结果: {report.summary.result}";
            throw new Exception(errorMsg);
        }
    }

    #endregion

    #region 辅助方法 - DLL复制

    private static List<string> CopyDllFileToByte(string[] originFileNames, string originDir, string targetDir)
    {
        List<string> bytesFiles = new List<string>();
        foreach (var originFileName in originFileNames)
        {
            var dllFilePath = Path.Combine(ProjectPath, originDir, $"{originFileName}.dll");
            if (!File.Exists(dllFilePath))
            {
                Debug.LogWarning($"DLL文件不存在: {dllFilePath}");
                continue;
            }

            var targetFileName = $"{originFileName}.bytes";
            var dllRawFilePath = Path.Combine(targetDir, targetFileName);
            File.Copy(dllFilePath, dllRawFilePath, true);
            bytesFiles.Add(originFileName);
        }
        return bytesFiles;
    }

    private static void CopyPatchedAOTDllToCollectPath(string rawFileCollectPath)
    {
        if (string.IsNullOrEmpty(rawFileCollectPath))
        {
            throw new ArgumentException("rawFileCollectPath 不能为空");
        }

        if (!Directory.Exists(rawFileCollectPath))
        {
            Directory.CreateDirectory(rawFileCollectPath);
        }

        var patchedAOTAssemblies = SettingsUtil.HybridCLRSettings.patchAOTAssemblies;
        var dllOutputPath = SettingsUtil.GetAssembliesPostIl2CppStripDir(EditorUserBuildSettings.activeBuildTarget);
        var dllRawFileAssetNames = CopyDllFileToByte(patchedAOTAssemblies, dllOutputPath, rawFileCollectPath);

        if (dllRawFileAssetNames != null && dllRawFileAssetNames.Count > 0)
        {
            var namesJson = JsonConvert.SerializeObject(dllRawFileAssetNames);
            File.WriteAllText($"{rawFileCollectPath}/AOTDLLs.txt", namesJson);
            AssetDatabase.Refresh();
            Debug.Log($"AOT DLL复制完成，共 {dllRawFileAssetNames.Count} 个文件");
        }
        else
        {
            Debug.LogWarning("未复制任何AOT DLL文件，请检查patchAOTAssemblies配置");
        }
    }

    private static void CopyHotUpdateDllToCollectPath(string rawFileCollectPath)
    {
        if (string.IsNullOrEmpty(rawFileCollectPath))
        {
            throw new ArgumentException("rawFileCollectPath 不能为空");
        }

        if (!Directory.Exists(rawFileCollectPath))
        {
            Directory.CreateDirectory(rawFileCollectPath);
        }

        var hotUpdateAssemblies = SettingsUtil.HotUpdateAssemblyNamesExcludePreserved;
        var hotUpdateOutputPath = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(EditorUserBuildSettings.activeBuildTarget);
        var dllRawFileAssetNames = CopyDllFileToByte(hotUpdateAssemblies.ToArray(), hotUpdateOutputPath, rawFileCollectPath);

        if (dllRawFileAssetNames != null && dllRawFileAssetNames.Count > 0)
        {
            var json = JsonConvert.SerializeObject(dllRawFileAssetNames);
            File.WriteAllText(Path.Combine(rawFileCollectPath, "HotUpdateDLLs.txt"), json);
            AssetDatabase.Refresh();
            Debug.Log($"热更新DLL复制完成，共 {dllRawFileAssetNames.Count} 个文件");
        }
        else
        {
            Debug.LogWarning("未复制任何热更新DLL文件，请检查HotUpdateAssemblyNames配置");
        }
    }

    #endregion

    #region 辅助方法 - YooAsset构建

    private static string GetFirstPackageName()
    {
        foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
        {
            return package.PackageName;
        }
        return null;
    }

    private static BuildParameters CreateBuildParameters(string pipelineName, string packageName,
        BuildTarget buildTarget, EFileNameStyle fileNameStyle,
        EBuildinFileCopyOption buildinFileCopyOption, string buildinFileCopyParams,
        ECompressOption compressOption, bool clearBuildCache, bool useAssetDependencyDB)
    {
        string version = $"{BuildVersion}_{DateTime.Now:yyyyMMdd_HHmmss}";

        if (pipelineName == EBuildPipeline.ScriptableBuildPipeline.ToString())
        {
            ScriptableBuildParameters buildParameters = new ScriptableBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.BuildTarget = buildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version;
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.CompressOption = compressOption;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            return buildParameters;
        }
        else if (pipelineName == EBuildPipeline.BuiltinBuildPipeline.ToString())
        {
            BuiltinBuildParameters buildParameters = new BuiltinBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.BuildTarget = buildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version;
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.CompressOption = compressOption;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            return buildParameters;
        }
        else if (pipelineName == EBuildPipeline.RawFileBuildPipeline.ToString())
        {
            RawFileBuildParameters buildParameters = new RawFileBuildParameters();
            buildParameters.BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBuildBundleType.RawBundle;
            buildParameters.BuildTarget = buildTarget;
            buildParameters.PackageName = packageName;
            buildParameters.PackageVersion = version;
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = fileNameStyle;
            buildParameters.BuildinFileCopyOption = buildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = buildinFileCopyParams;
            buildParameters.ClearBuildCacheFiles = clearBuildCache;
            buildParameters.UseAssetDependencyDB = useAssetDependencyDB;
            return buildParameters;
        }
        else
        {
            throw new Exception($"不支持的构建管线: {pipelineName}");
        }
    }

    private static BuildResult RunBuildPipeline(string pipelineName, BuildParameters buildParameters)
    {
        if (pipelineName == EBuildPipeline.ScriptableBuildPipeline.ToString())
        {
            ScriptableBuildPipeline pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }
        else if (pipelineName == EBuildPipeline.BuiltinBuildPipeline.ToString())
        {
            BuiltinBuildPipeline pipeline = new BuiltinBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }
        else if (pipelineName == EBuildPipeline.RawFileBuildPipeline.ToString())
        {
            RawFileBuildPipeline pipeline = new RawFileBuildPipeline();
            return pipeline.Run(buildParameters, true);
        }
        else
        {
            throw new Exception($"不支持的构建管线: {pipelineName}");
        }
    }

    #endregion

    #region 辅助方法 - APK构建

    private static string[] GetEnabledScenePaths()
    {
        List<string> scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }
        return scenes.ToArray();
    }

    #endregion
}
