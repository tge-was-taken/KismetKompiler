using System.Text;

string directoryPath = @"..\..\..\..\..\..\Testdata";
var directoryFullPath = Path.GetFullPath(directoryPath);
var files = Directory.GetFiles(directoryPath, "*.uasset", SearchOption.AllDirectories);

var methods = new StringBuilder();
foreach (var file in files)
{
    var fileFullPath = Path.GetFullPath(file);
    var fileRelPath = Path.GetRelativePath(directoryFullPath, fileFullPath);
    var fileRelPathNormalized = Path.ChangeExtension(fileRelPath, null)
        .Replace("/", "\\")
        .Replace("\\", "_")
        .Replace(".", "_");

    var methodName = $"{fileRelPathNormalized}";

    methods.AppendLine($@"
[TestMethod, Timeout(10000)]
public void {methodName}()
{{
    AssertBinaryEqualityAfterRecompilation(@""{Path.GetRelativePath(directoryFullPath, file)}"");
}}");
}

// Create the source code
var sourceCode = $@"
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace KismetKompiler.Tests;
            
[TestClass]
public partial class RecompilationTests
{{
    private const string RootPath = @""..\..\..\..\..\..\Testdata"";
    private const UAssetAPI.UnrealTypes.EngineVersion EngineVersion = UAssetAPI.UnrealTypes.EngineVersion.VER_UE4_27;

    {methods}
}}
";

File.WriteAllText(@"..\..\..\..\KismetKompiler.Tests\RecompilationTests.Generated.cs", sourceCode);