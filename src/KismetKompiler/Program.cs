using KismetKompiler;
using KismetKompiler.Compiler;
using KismetKompiler.Compiler.Processing;
using KismetKompiler.Decompiler;
using KismetKompiler.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

Console.OutputEncoding = Encoding.Default;
var path = args.FirstOrDefault(
    //@"C:\Users\cweer\Documents\Unreal Projects\MyProject\Saved\Cooked\WindowsNoEditor\MyProject\Content\FirstPersonBP\Blueprints\FirstPersonCharacter.uasset");
    @"E:\Projects\smtv_ai\pakchunk0-Switch\Project\Content\Blueprints\Battle\Logic\AI\Enemy\BtlAI_e050.uasset");
//var ver = EngineVersion.VER_UE4_27;
var ver = EngineVersion.VER_UE4_23;
if (!File.Exists(path))
{
    Console.WriteLine("Invalid file specified");
    return;   
}
var asset = new UAsset(path, ver);
asset.VerifyBinaryEquality();

StreamWriter outWriter;
KismetDecompiler decompiler;

// outWriter = new StreamWriter("old_out.c", false, Encoding.Unicode);
//decompiler = new KismetDecompiler(outWriter);
//decompiler.LoadAssetContext(asset);
//decompiler.DecompileClass();

//for (int i = 0; i < asset.Exports.Count; i++)
//{
//    var export = asset.Exports[i];
//    if (export is FunctionExport functionExport && functionExport.ScriptBytecode.Length > 0)
//    {
//        File.WriteAllText($"export{i}.json", JsonConvert.SerializeObject(functionExport.ScriptBytecode));

//        //var result = decompiler.DecompileFunction(functionExport);
//    }
//}
//outWriter.Close();

var parser = new KismetScriptASTParser();
var compilationUnit = parser.Parse(new StreamReader("old_out.c", Encoding.Unicode));
var typeResolver = new TypeResolver();
typeResolver.ResolveTypes(compilationUnit);
var compiler = new KismetScriptCompiler(asset);
var script = compiler.CompileCompilationUnit(compilationUnit);

outWriter = new StreamWriter("new_out.c", false, Encoding.Unicode);
decompiler = new KismetDecompiler(outWriter);
decompiler.LoadAssetContext(asset);
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

var oldJsons = asset.Exports
    .Where(x => x is FunctionExport)
    .Cast<FunctionExport>()
    .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Formatting.Indented));

var newJsons = script.Classes
    .SelectMany(x => x.Functions)
    .Select(x => JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.Expressions.ToArray()), Formatting.Indented));

File.WriteAllText($"old.json", string.Join("\n", oldJsons));
File.WriteAllText($"new.json", string.Join("\n", newJsons));

//var tempStream = File.Create("code.bin");
//var writer = new AssetBinaryWriter(tempStream, Encoding.Unicode, true, asset);
//foreach (var item in old.ScriptBytecode)
//{
//    ExpressionSerializer.WriteExpression(item, writer);
//}
//tempStream.Close();

Console.ReadKey();
