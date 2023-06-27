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
using UAssetAPI.Kismet;
using UAssetAPI.UnrealTypes;

Console.OutputEncoding = Encoding.Unicode;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var path = args.FirstOrDefault(
    //@"C:\Users\cweer\Documents\Unreal Projects\MyProject\Saved\Cooked\WindowsNoEditor\MyProject\Content\FirstPersonBP\Blueprints\FirstPersonCharacter.uasset");
    @"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e006.uasset");
//var ver = EngineVersion.VER_UE4_27;
var ver = EngineVersion.VER_UE4_23;
if (!File.Exists(path))
{
    Console.WriteLine("Invalid file specified");
    return;
}

//CompileClass(new() { Exports = new() }, "Test_NoViableAltException.kms");
DecompileFolder(@"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints", EngineVersion.VER_UE4_23, true);
DecompileOne(path);

Console.ReadKey();

static void DecompileOne(string path)
{
    var asset = LoadAsset(path);
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

static void DecompileFolder(string folderPath, EngineVersion version, bool throwOnException)
{
    static void Decompile(string path, EngineVersion version)
    {
        var asset = new UAsset(path, version);
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

    foreach (var path in Directory.EnumerateFiles(folderPath, "*.uasset", SearchOption.AllDirectories))
    {
        if (throwOnException)
            Decompile(path, version);
        else
        {
            try
            {
                Decompile(path, version);
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

static void DecompileClass(UAsset asset, string outPath)
{
    using var outWriter = new StreamWriter(outPath, false, Encoding.Unicode);
    var decompiler = new KismetDecompiler(outWriter);
    decompiler.DecompileClass(asset);
}

static KismetScript CompileClass(UAsset asset, string inPath)
{
    var parser = new KismetScriptASTParser();
    var compilationUnit = parser.Parse(new StreamReader(inPath, Encoding.Unicode));
    var typeResolver = new TypeResolver();
    typeResolver.ResolveTypes(compilationUnit);
    var compiler = new KismetScriptCompiler(asset);
    var script = compiler.CompileCompilationUnit(compilationUnit);
    return script;
}

static void DumpOld(UAsset asset)
{
    KismetSerializer.asset = asset;

    var oldJsons = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Formatting.Indented));

    var oldJsonText = string.Join("\n", oldJsons);

    File.WriteAllText($"old.json", oldJsonText);
}

static void DumpOldAndNew(string fileName, UAsset asset, KismetScript script)
{
    KismetSerializer.asset = asset;

    var oldJsons = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Formatting.Indented));

    var newJsons = script.Classes
        .SelectMany(x => x.Functions)
        .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.Expressions.ToArray()), Formatting.Indented));

    var oldJsonText = string.Join("\n", oldJsons);
    var newJsonText = string.Join("\n", newJsons);

    File.WriteAllText($"old.json", oldJsonText);
    File.WriteAllText($"new.json", newJsonText);

    if (oldJsonText != newJsonText)
        Console.WriteLine($"Verification failed: {fileName}");
}