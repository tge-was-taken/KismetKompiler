using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;

// FIXME: make this configurable?
string rootDirPath = @"..\..\..\..\..\..\Testdata";
var rootDirPathFull = Path.GetFullPath(rootDirPath);
var gameDirectories = Directory.GetDirectories(rootDirPathFull);
foreach (var gameDir in gameDirectories)
{
    var gameName = Path.GetFileName(gameDir);
    var gameEngineVer = gameName.ToLower() switch
    {
        "p3r" => UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_27,
        "dqxis" => UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_18,
    };
    var files = Directory.GetFiles(gameDir, "*.uasset", SearchOption.AllDirectories);

    var methods = new StringBuilder();
    foreach (var file in files)
    {
        var fileFullPath = Path.GetFullPath(file);
        var fileRelPath = Path.GetRelativePath(gameDir, fileFullPath);
        var fileRelPathNormalized = Path.ChangeExtension(fileRelPath, null)
            .Replace("/", "\\")
            .Replace("\\", "_")
            .Replace(".", "_");

        Console.WriteLine($"Processing {gameName} {fileRelPath} ({Array.IndexOf(files, file)}/{files.Length})");

        var methodName = $"{fileRelPathNormalized}";
        var skip = true;

        try
        {
            // We want to only include uassets that actually have blueprint functions that do something (instead of just empty stubs)
            var asset = new UAsset(fileFullPath, gameEngineVer);
            var functions = asset.Exports
                .Where(x => x is FunctionExport)
                .Select(x => (FunctionExport)x);

            if (functions.Any())
            {
                if (functions.SelectMany(x => x.ScriptBytecode)
                    .Any(x => x.Token != UAssetAPI.Kismet.Bytecode.EExprToken.EX_EndOfScript &&
                              x.Token != UAssetAPI.Kismet.Bytecode.EExprToken.EX_Return))
                {
                    skip = false;
                }
            }
        }
        catch (Exception)
        {
            // If something goes wrong during loading, make sure to not skip the file
            // as we won't notice the issue otherwise
            skip = false;
        }

        if (!skip)
        {
            methods.AppendLine(
$@"[TestMethod, Timeout(10000)] public void {methodName}() => Test(@""{Path.GetRelativePath(gameDir, file)}"");");
        }
    }

    // Create the source code
    var sourceCode = $@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace KismetKompiler.Tests.Recompilation;
            
[TestClass]
public sealed class {gameName} : RecompilationTestsBase
{{
    protected override string RootPath => @""..\..\..\..\..\..\Testdata\{gameName}"";
    protected override UAssetAPI.UnrealTypes.EngineVersion EngineVersion => UAssetAPI.UnrealTypes.EngineVersion.{gameEngineVer};

{methods}
}}
";

    File.WriteAllText($@"..\..\..\..\KismetKompiler.Tests\Recompilation\{gameName}.Generated.cs", sourceCode);
}