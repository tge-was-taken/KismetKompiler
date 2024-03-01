using Antlr4.Runtime.Misc;
using Antlr4.Runtime;
using KismetKompiler.Decompiler;
using KismetKompiler.Library.Compiler.Processing;
using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Packaging;
using KismetKompiler.Library.Parser;
using System.Text;
using UAssetAPI;
using Newtonsoft.Json;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet;

namespace KismetKompiler.Tests;

public partial class RecompilationTests
{
    private void AssertBinaryEqualityAfterRecompilation(string filePath)
    {
        var fullFilePath = Path.Join(RootPath, filePath);
        var asset = new UAsset(fullFilePath, EngineVersion);
        if (!asset.VerifyBinaryEquality())
            throw new Exception("UAssetAPI UAsset verification failed");

        var outStream = new MemoryStream();
        using (var outWriter = new StreamWriter(outStream, Encoding.Unicode, leaveOpen: true))
        {
            var decompiler = new KismetDecompiler(outWriter);
            decompiler.Decompile(asset);
        }
        outStream.Position = 0;

        var script = CompileScript(outStream, false);
        var tempAsset = new UAsset(fullFilePath, EngineVersion);
        var newAsset = new UAssetLinker((UAsset)tempAsset)
            .LinkCompiledScript(script)
            .Build();
        if (!VerifyEquality(asset, newAsset))
            throw new Exception("Binaries do not match after recompilation");
    }

    static bool VerifyEquality(UnrealPackage oldAsset, UnrealPackage newAsset)
    {
        KismetSerializer.asset = oldAsset;

        var oldJsons = oldAsset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .OrderBy(x => oldAsset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
            .Select(x => new { Function = x.ObjectName.ToString(), Instructions = KismetSerializer.SerializeScript(x.ScriptBytecode) });

        KismetSerializer.asset = newAsset;

        var newJsons = newAsset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .OrderBy(x => newAsset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
            .Select(x => new { Function = x.ObjectName.ToString(), Instructions = KismetSerializer.SerializeScript(x.ScriptBytecode) });

        var oldJsonText = JsonConvert.SerializeObject(oldJsons, Formatting.Indented);
        var newJsonText = JsonConvert.SerializeObject(newJsons, Formatting.Indented);
        if (oldJsonText != newJsonText)
        {
            return false;
        }
        else
        {
            return true;
        }
    }


    static CompiledScriptContext CompileScript(Stream inStream, bool noStrict)
    {
        using var textStream = new StreamReader(inStream);
        var inputStream = new AntlrInputStream(textStream);
        var lexer = new KismetScriptLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);

        var parser = new KismetScriptParser(tokenStream);
        parser.BuildParseTree = true;
        parser.ErrorHandler = new BailErrorStrategy();

        var compilationUnitContext = parser.compilationUnit();

        var astParser = new KismetScriptASTParser();
        if (!astParser.TryParseCompilationUnit(compilationUnitContext, out var compilationUnit))
            throw new ApplicationException("Failed to parse compilation unit");
        var typeResolver = new TypeResolver();
        typeResolver.ResolveTypes(compilationUnit);
        var compiler = new KismetScriptCompiler();
        compiler.StrictMode = !noStrict;
        var script = compiler.CompileCompilationUnit(compilationUnit);
        return script;
    }
}