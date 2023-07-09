﻿using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using KismetKompiler.Library.Compiler.Processing;
using KismetKompiler.Decompiler;
using System.Globalization;
using System.Text;
using UAssetAPI;
using UAssetAPI.IO;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using KismetKompiler.Library.Parser;
using KismetKompiler.Library.Packaging;
using KismetKompiler.Library.Compiler;
using CommandLine;
using Newtonsoft.Json;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet;

Console.OutputEncoding = Encoding.Unicode;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

if (args.Length == 0)
    args = new[] { "--help" };

CommandLine.Parser.Default.ParseArguments<CompileOptions, DecompileOptions>(args)
    .WithParsed<CompileOptions>(o =>
    {
        var version = ParseVersion(o.Version);
        Compile(o.InAssetFilePath, o.ScriptFilePath, version, o.UsmapFilePath, o.OutAssetFilePath, o.Overwrite);
        Console.WriteLine($"Done.");
    })
    .WithParsed<DecompileOptions>(o =>
     {
         var version = ParseVersion(o.Version);
         Decompile(o.InAssetFilePath, version, o.UsmapFilePath, o.OutScriptFilePath, o.Overwrite, o.NoVerification);
         Console.WriteLine($"Done.");
     });

static UAsset LoadUAsset(string path, EngineVersion ver, string? usmapPath = default)
{
    Usmap usmap = default;
    if (!string.IsNullOrWhiteSpace(usmapPath))
        usmap = new(usmapPath);

    var asset = new UAsset(path, ver, usmap);
    asset.VerifyBinaryEquality();
    return asset;
}

static ZenAsset LoadZenAsset(string path, EngineVersion ver, string? usmapPath = default)
{
    Usmap usmap = default;
    if (!string.IsNullOrWhiteSpace(usmapPath))
        usmap = new(usmapPath);

    var asset = new ZenAsset(path, ver, usmap);
    return asset;
}

static UnrealPackage LoadAsset(string path, EngineVersion ver, string? usmapPath = default)
{
    UnrealPackage asset;
    if (!string.IsNullOrEmpty(usmapPath))
    {
        asset = LoadZenAsset(path, ver, usmapPath);
    }
    else
    {
        asset = LoadUAsset(path, ver, usmapPath);
    }
    return asset;
}

static void Decompile(string path, EngineVersion ver, string? usmapPath = default, string outScriptPath = default, bool overwrite = false, bool noVerification = false)
{ 

    var asset = LoadAsset(path, ver, usmapPath);
    var outPath = outScriptPath ?? Path.ChangeExtension(path, ".kms");
    if (File.Exists(outPath))
    {
        if (overwrite)
        {
            File.Delete(outPath);
        }
        else
        {
            Console.WriteLine($"File {outPath} already exists.");
            Environment.Exit(1);
        }
    }

    Console.WriteLine($"Decompiling to {outPath}");
    using (var outWriter = new StreamWriter(outPath, false, Encoding.Unicode))
    {
        var decompiler = new KismetDecompiler(outWriter);
        decompiler.Decompile(asset);
    }

    if (!noVerification)
    {
        Console.WriteLine($"Verifying equality...");
        var script = CompileScript(outPath);
        var tempAsset = LoadAsset(path, ver, usmapPath);
        var newAsset = new UAssetLinker((UAsset)tempAsset)
            .LinkCompiledScript(script)
            .Build();
        VerifyEquality(outPath, asset, newAsset);
    }
}

static void Compile(string? assetPath, string scriptPath, EngineVersion ver, string? usmapPath = default, string? outAssetPath = default, bool overwrite = false)
{
    var assetFilePath = assetPath;
    if (assetFilePath != null &&
        Path.GetExtension(assetFilePath).Equals(".uexp", StringComparison.InvariantCultureIgnoreCase))
    {
        assetFilePath = Path.ChangeExtension(assetFilePath, ".uasset");
    }

    outAssetPath ??= Path.ChangeExtension(scriptPath, ".uasset");
    if (File.Exists(outAssetPath))
    {
        if (overwrite)
        {
            File.Delete(outAssetPath);
        }
        else
        {
            Console.WriteLine($"File {outAssetPath} already exists.");
            Environment.Exit(1);
        }
    }

    Console.WriteLine($"Compiling {scriptPath}");
    var script = CompileScript(scriptPath);

    UAsset newAsset;
    if (assetFilePath != null)
    {
        Console.WriteLine($"Merging with {assetFilePath}");
        var asset = LoadAsset(assetFilePath, ver, usmapPath);
        newAsset = new UAssetLinker((UAsset)asset)
            .LinkCompiledScript(script)
            .Build();
    }
    else
    {
        newAsset = new UAssetLinker()
            .LinkCompiledScript(script)
            .Build();
    }

    Console.WriteLine($"Writing asset file to {outAssetPath}");
    newAsset.Write(outAssetPath);
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

    string highlightedLine = new string(' ', startIndex) +
                             new string('^', endIndex - startIndex + 1);

    var messagePrefix = $"Syntax error at line {lineNumber}: ";
    Console.WriteLine($"{messagePrefix}{line}");
    Console.WriteLine(new string(' ', messagePrefix.Length) + highlightedLine);
}

static CompiledScriptContext CompileScript(string inPath)
{
    using var textStream = new StreamReader(inPath);
    var inputStream = new AntlrInputStream(textStream);
    var lexer = new KismetScriptLexer(inputStream);
    var tokenStream = new CommonTokenStream(lexer);

    var parser = new KismetScriptParser(tokenStream);
    parser.BuildParseTree = true;
    parser.ErrorHandler = new BailErrorStrategy();

    try
    {
        var compilationUnitContext = parser.compilationUnit();

        var astParser = new KismetScriptASTParser();
        if (!astParser.TryParseCompilationUnit(compilationUnitContext, out var compilationUnit))
        {
            Console.WriteLine("Failed to parse compilation unit");
            Environment.Exit(1);
        }
        var typeResolver = new TypeResolver();
        typeResolver.ResolveTypes(compilationUnit);
        var compiler = new KismetScriptCompiler();
        var script = compiler.CompileCompilationUnit(compilationUnit);
        return script;
    }
    catch (ParseCanceledException ex)
    {
        if (ex.InnerException is RecognitionException innerEx)
        {
            var lines = File.ReadAllLines(inPath);
            PrintSyntaxError(innerEx.OffendingToken.Line, innerEx.OffendingToken.Column, innerEx.OffendingToken.Column + innerEx.OffendingToken.Text.Length - 1,
                lines);
            //var expectedTokens = string.Join(", ", innerEx.GetExpectedTokens().ToList().Select(x => parser.Vocabulary.GetDisplayName(x)));
            //Console.WriteLine($"Expected tokens: {expectedTokens}");
            Environment.Exit(1);
        }

        throw;
    }
}

static EngineVersion ParseVersion(string version)
{
    var versionParts = version.Split('.');
    if (versionParts.Length != 2)
    {
        Console.WriteLine($"Invalid version. Expected <major>.<minor>, got: {version}");
        Environment.Exit(1);
    }

    var versionFormat = $"VER_UE{versionParts[0]}_{versionParts[1]}";
    if (!Enum.TryParse<EngineVersion>(versionFormat, out var engineVersion))
    {
        Console.WriteLine($"Unknown version: {version}");
        Environment.Exit(1);
    }

    return engineVersion;
}

static void VerifyEquality(string fileName, UnrealPackage oldAsset, UnrealPackage newAsset)
{
    KismetSerializer.asset = oldAsset;

    var oldJsons = oldAsset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .OrderBy(x => oldAsset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
        .Select(x => (x.ObjectName.ToString(), JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Newtonsoft.Json.Formatting.Indented)));

    var newJsons = newAsset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .OrderBy(x => newAsset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
        .Select(x => (x.ObjectName.ToString(), JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Newtonsoft.Json.Formatting.Indented)));

    var oldJsonText = string.Join("\n", oldJsons);
    var newJsonText = string.Join("\n", newJsons);

    if (oldJsonText != newJsonText)
        Console.WriteLine("Verification failed");
    else
        Console.WriteLine("Verification succeeded");
}

class OptionsBase
{
    [Option('v', "version", Required = false, HelpText = "Unreal Engine version (eg. 4.23)")]
    public string Version { get; set; } = "4.23";

    [Option('f', "overwrite", Required = false, HelpText = "Overwrite existing files")]
    public bool Overwrite { get; set; } = false;
    [Option("usmap", Required = false, HelpText = "Path to a .usmap file")]
    public string UsmapFilePath { get; set; }
}

[Verb("compile", HelpText = "Compile a script into a new or existing blueprint asset")]
class CompileOptions : OptionsBase
{
    [Option('i', "input", Required = true, HelpText = "Path to a .kms file")]
    public string ScriptFilePath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Path to the output .uasset file")]
    public string OutAssetFilePath { get; set; }

    [Option("asset", Required = false, HelpText = "Path to an input .uasset file.")]
    public string InAssetFilePath { get; set; }
}

[Verb("decompile", HelpText = "Decompile a blueprint asset")]
class DecompileOptions : OptionsBase
{
    [Option('i', "input", Required = true, HelpText = "Path to an input .uasset file")]
    public string InAssetFilePath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Path to the output .kms file")]
    public string OutScriptFilePath { get; set; }

    [Option("no-verify", Required = false, HelpText = "Skip verifying equality of decompiled code")]
    public bool NoVerification { get; set; } = false;
}