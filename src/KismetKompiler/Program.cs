using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using KismetKompiler;
using KismetKompiler.Compiler;
using KismetKompiler.Compiler.Exceptions;
using KismetKompiler.Compiler.Processing;
using KismetKompiler.Decompiler;
using KismetKompiler.Parser;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

Console.OutputEncoding = Encoding.Unicode;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

#if DEBUG
var path = @"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e139.uasset";
var outPath = @"C:\Users\cweer\AppData\Roaming\yuzu\load\010063B012DC6000\CustomAI\romfs\Project\Content\Paks\~mod\CustomAI\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e139.uasset";
var usmapPath = @"";
var ver = EngineVersion.VER_UE4_23;
//var path = @"D:\Users\smart\Downloads\Pikmin4DemoBlueprints\Pikmin4DemoBlueprints\ABP_Baby.uasset";
//var ver = EngineVersion.VER_UE4_26;
//var usmapPath = @"D:\Users\smart\Downloads\Mappings.usmap";
#else
var path = "";
var ver = EngineVersion.VER_UE4_23;
var usmapPath = "";
#endif

if (args.Length > 0)
{
    path = args[0];
}
if (args.Length > 1)
{
    ver = Enum.Parse<EngineVersion>(args[1]);
}
if (args.Length > 2)
{
    usmapPath = args[2];
}

if (!File.Exists(path))
{
    Console.WriteLine("Invalid file specified");
    return;
}

//CompileClass(new() { Exports = new() }, "Test_NoViableAltException.kms");
//DecompileFolder(@"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints\Battle", EngineVersion.VER_UE4_23, false, false);
//DecompileFolder(@"D:\Users\smart\Downloads\Pikmin4DemoBlueprints\Pikmin4DemoBlueprints", EngineVersion.VER_UE4_27, true, true);
//DecompileOne(path, ver, usmapPath);

var asset = LoadAsset(@"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e139.uasset", ver);
var script = CompileClass(asset, @"E:\Projects\smtv_ai\tools\UnrealPak\CustomAI\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e139.kms");
foreach (var cls in script.Classes)
{
    foreach (var func in cls.Functions)
    {
        var export = (FunctionExport)asset.Exports.Where(x => x.ObjectName.ToString() == func.Name).FirstOrDefault();
        export.ScriptBytecode = func.Expressions.ToArray();
    }
}
asset.Write(@"E:\Projects\smtv_ai\tools\UnrealPak\CustomAI\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e139.uasset");
File.Delete(@"E:\Projects\smtv_ai\tools\UnrealPak\CustomAI.pak");
Process.Start(@"E:\Projects\smtv_ai\tools\UnrealPak\UnrealPak-With-Compression.bat", @"E:\Projects\smtv_ai\tools\UnrealPak\CustomAI").WaitForExit();
File.Copy(@"E:\Projects\smtv_ai\tools\UnrealPak\CustomAI.pak", @"C:\Users\cweer\AppData\Roaming\yuzu\load\010063B012DC6000\CustomAI\romfs\Project\Content\Paks\~mod\CustomAI.pak", true);

Console.WriteLine("Done");
Console.ReadKey();

static void DecompileOne(string path, EngineVersion ver, string? usmapPath = default)
{
    UnrealPackage asset;
    if (!string.IsNullOrEmpty(usmapPath))
    {
        var usmap = new Usmap(usmapPath);
        asset = new ZenAsset(path, ver, usmap);
    }
    else
    {
        asset = LoadAsset(path, ver);
    }

    DecompileClass(asset, "old_out.c");
    var script = CompileClass(asset, "old_out.c");

    var outWriter = new StreamWriter("new_out.c", false, Encoding.Unicode);
    var decompiler = new KismetDecompiler(outWriter);
    decompiler.DecompileFunction(new()
    {
        Asset = asset,
        ScriptBytecode = script.Classes[0].Functions[0].Expressions.ToArray(),
        ObjectName = new(asset, script.Classes[0].Functions[0].Name),
        FunctionFlags = EFunctionFlags.FUNC_UbergraphFunction
    });
    outWriter.Close();


    var old = ((FunctionExport)asset.Exports.Where(x => x is FunctionExport).FirstOrDefault());
    KismetSerializer.asset = asset;

    DumpOldAndNew(path, asset, script);

}

static void DecompileFolder(string folderPath, EngineVersion version, bool useZen, bool throwOnException)
{
    static void Decompile(string path, EngineVersion version, bool useZen)
    {
        if (!useZen)
        {
            var asset = new UAsset(path, version);
            if (asset.GetClassExport() == null)
                return;
            Console.Write(path + " ");
            //var kmsPath = Path.ChangeExtension(path, ".kms");
            var kmsPath = "old_out.c";
            DumpOld(asset);
            DecompileClass(asset, kmsPath);
            if (string.IsNullOrWhiteSpace(File.ReadAllText(kmsPath)))
                return;
            try
            {
                var script = CompileClass(asset, kmsPath);
                DumpOldAndNew(path, asset, script);
                Console.WriteLine($"Success");
            }
            catch (UnexpectedSyntaxError ex)
            {
                // TODO
            }
            catch (RedefinitionError ex)
            {
                // TODO
            }
            catch (KeyNotFoundException exx)
            {
                // TODO 
            }
        }
        else
        {
            var asset = new ZenAsset(path, version, new UAssetAPI.Unversioned.Usmap());
            if (asset.GetClassExport() == null)
                return;
            var kmsPath = Path.ChangeExtension(path, ".kms");
            DumpOld(asset);
            DecompileClass(asset, kmsPath);
            if (string.IsNullOrWhiteSpace(File.ReadAllText(kmsPath)))
                return;
            try
            {
                var script = CompileClass(asset, kmsPath);
                DumpOldAndNew(path, asset, script);
                Console.WriteLine($"Success: {path}");
            }
            catch (UnexpectedSyntaxError ex)
            {
                // TODO
                Console.WriteLine($"Exception: {path}\t\t{ex.Message.ReplaceLineEndings(" ")}");
            }
            catch (RedefinitionError ex)
            {
                // TODO
                Console.WriteLine($"Exception: {path}\t\t{ex.Message.ReplaceLineEndings(" ")}");
            }
            catch (KeyNotFoundException ex)
            {
                // TODO 
                Console.WriteLine($"Exception: {path}\t\t{ex.Message.ReplaceLineEndings(" ")}");
            }
        }
    }

    foreach (var path in Directory.EnumerateFiles(folderPath, "*.uasset", SearchOption.AllDirectories))
    {
        if (throwOnException)
            Decompile(path, version, useZen);
        else
        {
            try
            {
                Decompile(path, version, useZen);
            }
            catch (Exception ex)
            {
               // Console.WriteLine($"Crash: {path}");
                Console.WriteLine($"Failed: {Path.GetFileName(path)} {ex}");
            }
        }
    }
}

static UAsset LoadAsset(string filePath, EngineVersion version = EngineVersion.VER_UE4_23)
{
    var asset = new UAsset(filePath, version);
    asset.VerifyBinaryEquality();
    return asset;
}

static void DecompileClass(UnrealPackage asset, string outPath)
{
    using var outWriter = new StreamWriter(outPath, false, Encoding.Unicode);
    var decompiler = new KismetDecompiler(outWriter);
    decompiler.DecompileClass(asset);
}

static void PrintSyntaxError(int lineNumber, int startIndex, int endIndex, string[] lines)
{
    if (lineNumber < 1 || lineNumber > lines.Length)
    {
        throw new ArgumentOutOfRangeException(nameof(lineNumber), "Invalid line number.");
    }

    string line = lines[lineNumber - 1];
    int lineLength = line.Length;

    if (startIndex < 0 || endIndex < 0 || startIndex >= lineLength || endIndex >= lineLength)
    {
        throw new ArgumentOutOfRangeException(nameof(startIndex), "Invalid character index.");
    }

    string highlightedLine = line.Substring(0, startIndex) +
                             new string('^', endIndex - startIndex + 1) +
                             line.Substring(endIndex + 1);

    var messagePrefix = $"Syntax error at line {lineNumber}:";
    Console.WriteLine($"{messagePrefix}{line}");
    Console.WriteLine(new string(' ', messagePrefix.Length) + highlightedLine);
}

static KismetScript CompileClass(UnrealPackage asset, string inPath)
{
    try
    {
        var parser = new KismetScriptASTParser();
        using var reader = new StreamReader(inPath, Encoding.Unicode);
        var compilationUnit = parser.Parse(reader);
        var typeResolver = new TypeResolver();
        typeResolver.ResolveTypes(compilationUnit);
        var compiler = new KismetScriptCompiler(asset);
        var script = compiler.CompileCompilationUnit(compilationUnit);
        return script;
    }
    catch (ParseCanceledException ex)
    {
        if (ex.InnerException is InputMismatchException innerEx)
        {
            var lines = File.ReadAllLines(inPath);
            PrintSyntaxError(innerEx.OffendingToken.Line, innerEx.OffendingToken.Column, innerEx.OffendingToken.Column+innerEx.OffendingToken.Text.Length-1,
                lines);
        }

        throw;
    }
}

static void DumpOld(UnrealPackage asset)
{
    KismetSerializer.asset = asset;

    var oldJsons = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Formatting.Indented));

    var oldJsonText = string.Join("\n", oldJsons);

    File.WriteAllText($"old.json", oldJsonText);
}

static void DumpOldAndNew(string fileName, UnrealPackage asset, KismetScript script)
{
    KismetSerializer.asset = asset;

    var oldJsons = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .OrderBy(x => asset.GetClassExport().FuncMap.IndexOf(x.ObjectName))
        .Select(x => (x.ObjectName.ToString(), JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Formatting.Indented)));

    var newJsons = script.Classes
        .SelectMany(x => x.Functions)
        .Select(x => (x.Name, JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.Expressions.ToArray()), Formatting.Indented)));

    var oldJsonText = string.Join("\n", oldJsons);
    var newJsonText = string.Join("\n", newJsons);

    File.WriteAllText($"old.json", oldJsonText);
    File.WriteAllText($"new.json", newJsonText);

    if (oldJsonText != newJsonText)
        Console.WriteLine($"Verification failed: {fileName}");
}