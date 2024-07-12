using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using CommandLine;
using KismetKompiler.Decompiler;
using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Compiler.Processing;
using KismetKompiler.Library.Decompiler;
using KismetKompiler.Library.Linker;
using KismetKompiler.Library.Packaging;
using KismetKompiler.Library.Parser;
using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.ConstrainedExecution;
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

if (args.Length == 0)
    args = new[] { "--help" };

CommandLine.Parser.Default.ParseArguments<CompileOptions, DecompileOptions, SdkOptions>(args)
    .WithParsed<CompileOptions>(o =>
    {
        var version = ParseVersion(o.VersionString);
        try
        {
            Compile(o.InputAssetFilePath, o.InputPath, version, o.UsmapFilePath, o.OutputPath, o.Overwrite, o.NoStrict, o.GlobalFilePath);
            Console.WriteLine($"Done.");
        }
        catch (ApplicationException ex)
        {
            Console.WriteLine(ex.Message);
        }
    })
    .WithParsed<DecompileOptions>(o =>
     {
         var version = ParseVersion(o.VersionString);
         try
         {
             Decompile(o.InputPath, version, o.UsmapFilePath, o.OutputPath, o.Overwrite, o.NoVerification, o.NoStrict, o.GlobalFilePath);
             Console.WriteLine($"Done.");
         }
         catch (ApplicationException ex)
         {
             Console.WriteLine(ex.Message);
         }
     })
    //.WithParsed<SdkOptions>(o =>
    //{
    //    try
    //    {
    //        CreateSdk(o);
    //        Console.WriteLine($"Done.");
    //    }
    //    catch (ApplicationException ex)
    //    {
    //        Console.WriteLine(ex.Message);
    //    }
    //});
    ;

static string PropertyTypeToType(EPropertyType type)
{
    switch (type)
    {
        case EPropertyType.MulticastDelegateProperty:
            return "MulticastDelegate";

        case EPropertyType.Int16Property:
            return "short";

        case EPropertyType.IntProperty:
            return "int";

        case EPropertyType.UInt16Property:
            return "ushort";

        case EPropertyType.UInt32Property:
            return "uint";

        case EPropertyType.Int8Property:
            return "sbyte";

        case EPropertyType.NameProperty:
            return "Name";

        case EPropertyType.StrProperty:
            return "string";

        case EPropertyType.MapProperty:
            return "Map";

        case EPropertyType.WeakObjectProperty:
            return "WeakObject";

        case EPropertyType.ArrayProperty:
            return "Array";

        default:
            return type.ToString().Replace("Property", "").ToLower();
    }
}

static TypeIdentifier GetPropertyTypeIdentifierFromClass(UsmapSchema schema)
{
    return new(schema.Name);
}

//static void CreateSdk(SdkOptions options)
//{
//    var version = ParseVersion(options.VersionString);
//    var usmap = new Usmap(options.UsmapFilePath);
//    var source = new CompilationUnit();
//    var schemas = usmap.SchemasByName.Values.ToList();
//    foreach (var enumType in usmap.EnumMap.Values)
//    {
//        var enumDecl = new EnumDeclaration()
//        {
//            Identifier = new(enumType.Name)
//        };
//        foreach ((var key, var value) in enumType.Values.OrderBy(x => x.Key))
//        {
//            enumDecl.Values.Add(new EnumValueDeclaration()
//            {
//                Value = new IntLiteral((int)key),
//                Identifier = new(value),
//            });
//        }
//        source.Declarations.Add(enumDecl);
//    }
//    foreach (var schema in usmap.Schemas)
//    {
//        var classDecl = new ClassDeclaration()
//        {
//            Identifier = new(schema.Name),
//            Modifiers = ClassModifiers.Public,
//        };
//        if (schema.SuperType != null)
//            classDecl.InheritedTypeIdentifiers.Add(new(schema.SuperType));

//        UsmapProperty lastProp = null;
//        var skipCount = 0;
//        foreach ((var propIndex, var prop) in schema.Properties)
//        {
//            if (skipCount > 0)
//            {
//                skipCount--;
//                continue;
//            }

//            if (prop.ArraySize > 1)
//            {
//                skipCount = prop.ArraySize;
//                if (prop.PropertyData.Type == EPropertyType.ArrayProperty)
//                {
//                    var arrayType = prop.PropertyData as UsmapArrayData;
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Size = prop.ArraySize,
//                        Type = new(PropertyTypeToType(arrayType.InnerType.Type)),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.ObjectProperty)
//                {
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Size = prop.ArraySize,
//                        Identifier = new(prop.Name),
//                        Type = GetPropertyTypeIdentifierFromClass(usmap.Schemas[prop.SchemaIndex]),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.StructProperty)
//                {
//                    var structType = prop.PropertyData as UsmapStructData;
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Size = prop.ArraySize,
//                        Identifier = new(prop.Name),
//                        Type = new(structType.StructType),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.EnumProperty)
//                {
//                    var enumType = prop.PropertyData as UsmapEnumData;
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Size = prop.ArraySize,
//                        Identifier = new(prop.Name),
//                        Type = new(enumType.Name),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else
//                {
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Size = prop.ArraySize,
//                        Identifier = new(prop.Name),
//                        Type = new(PropertyTypeToType(prop.PropertyData.Type)),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//            }
//            else
//            {
//                if (prop.PropertyData.Type == EPropertyType.ArrayProperty)
//                {
//                    var arrayType = prop.PropertyData as UsmapArrayData;
//                    var varDecl = new ArrayVariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(PropertyTypeToType(arrayType.InnerType.Type)),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.ObjectProperty)
//                {
//                    var varDecl = new VariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = GetPropertyTypeIdentifierFromClass(usmap.Schemas[prop.SchemaIndex]),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.StructProperty)
//                {
//                    var structType = prop.PropertyData as UsmapStructData;
//                    var varDecl = new VariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(structType.StructType),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.EnumProperty)
//                {
//                    var enumType = prop.PropertyData as UsmapEnumData;
//                    var varDecl = new VariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(enumType.Name),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//                else
//                {
//                    var varDecl = new VariableDeclaration()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(PropertyTypeToType(prop.PropertyData.Type)),
//                        Modifiers = VariableModifier.Public
//                    };
//                    classDecl.Declarations.Add(varDecl);
//                }
//            }

//        }

//        if (schema.Name == "BtlCoreComponent")
//            Debugger.Break();

//        foreach ((var funcIndex, var func) in schema.Functions)
//        {
//            var modifiers = (ProcedureModifier)0;
//            if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Final))
//                modifiers |= ProcedureModifier.Sealed;
//            if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Static))
//                modifiers |= ProcedureModifier.Static;
//            if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Private))
//                modifiers |= ProcedureModifier.Private;
//            if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Protected))
//                modifiers |= ProcedureModifier.Protected;
//            if (func.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Public))
//                modifiers |= ProcedureModifier.Public;

//            var returnProp = func.Properties.Values.Where(x => x.PropertyData.Flags.HasFlag(EPropertyFlags.CPF_ReturnParm)).FirstOrDefault();

//            var funcDecl = new ProcedureDeclaration()
//            {
//                Identifier = new(func.Name),
//                ReturnType = new(ValueKind.Void),
//                Modifiers = modifiers
//            };
//            if (returnProp != null)
//            {
//                funcDecl.ReturnType = new() { ValueKind = ValueKind.Type, Text = PropertyTypeToType(returnProp.PropertyData.Type) };
//            }

//            foreach ((var propIdx, var prop) in func.Properties)
//            {
//                if (prop.PropertyData.Flags.HasFlag(EPropertyFlags.CPF_ReturnParm))
//                    continue;

//                if (prop.PropertyData.Type == EPropertyType.ArrayProperty)
//                {
//                    var arrayType = prop.PropertyData as UsmapArrayData;
//                    var varDecl = new Parameter()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(PropertyTypeToType(arrayType.InnerType.Type)),
//                    };
//                    funcDecl.Parameters.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.ObjectProperty)
//                {
//                    var varDecl = new Parameter()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = GetPropertyTypeIdentifierFromClass(usmap.Schemas[prop.SchemaIndex]),
//                    };
//                    funcDecl.Parameters.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.StructProperty)
//                {
//                    var structType = prop.PropertyData as UsmapStructData;
//                    var varDecl = new Parameter()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(structType.StructType),
//                    };
//                    funcDecl.Parameters.Add(varDecl);
//                }
//                else if (prop.PropertyData.Type == EPropertyType.EnumProperty)
//                {
//                    var enumType = prop.PropertyData as UsmapEnumData;
//                    var varDecl = new Parameter()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(enumType.Name),
//                    };
//                    funcDecl.Parameters.Add(varDecl);
//                }
//                else
//                {
//                    var varDecl = new Parameter()
//                    {
//                        Identifier = new(prop.Name),
//                        Type = new(PropertyTypeToType(prop.PropertyData.Type)),
//                    };
//                    funcDecl.Parameters.Add(varDecl);
//                }
//            }
//            classDecl.Declarations.Add(funcDecl);
//        }
//        source.Declarations.Add(classDecl);
//    }
//    var writer = new CompilationUnitWriter();
//    writer.Write(source, Path.GetFileNameWithoutExtension(options.UsmapFilePath) + ".kms");
//}

static string NormalizeAssetPath(string? path)
{
    if (path != null && Path.GetExtension(path).Equals(".uexp", StringComparison.InvariantCultureIgnoreCase))
    {
        return Path.ChangeExtension(path, ".uasset");
    }
    return path;
}

static UAsset LoadUAsset(string path, EngineVersion ver, string? usmapPath = default)
{
    Usmap usmap = default;
    if (!string.IsNullOrWhiteSpace(usmapPath))
        usmap = new(usmapPath);

    var asset = new UAsset(path, ver, usmap);
    asset.VerifyBinaryEquality();
    return asset;
}

static ZenAsset LoadZenAsset(string path, EngineVersion ver, string? usmapPath = default, string? globalPath = default)
{
    Usmap usmap = default;
    if (!string.IsNullOrWhiteSpace(usmapPath))
        usmap = new(usmapPath);

    IOGlobalData globalData = default;
    if (!string.IsNullOrWhiteSpace(globalPath))
    {
        var globalContainer = new IOStoreContainer(Path.ChangeExtension(globalPath, ".utoc"));

        globalData = new IOGlobalData(
            globalContainer,
            ver);
    }

    var asset = new ZenAsset(path, ver, usmap, globalData);
    return asset;
}

static UnrealPackage LoadAsset(string path, EngineVersion ver, string? usmapPath = default, string? globalPath = default)
{
    UnrealPackage asset;
    if (!string.IsNullOrEmpty(globalPath))
    {
        asset = LoadZenAsset(path, ver, usmapPath, globalPath);
    }
    else
    {
        asset = LoadUAsset(path, ver, usmapPath);
    }
    return asset;
}

static bool DecompileFile(string inputPath, EngineVersion ver, string? usmapPath, string outScriptPath, bool overwrite, bool noVerification, bool noStrict, string? globalPath)
{
    Console.WriteLine($"Decompiling {inputPath}");
    var assetPath = NormalizeAssetPath(inputPath);
    var asset = LoadAsset(assetPath, ver, usmapPath, globalPath);
    var outPath = outScriptPath ?? Path.ChangeExtension(assetPath, ".kms");
    if (File.Exists(outPath))
    {
        if (overwrite)
        {
            File.Delete(outPath);
        }
        else
        {
            Console.WriteLine($"File {outPath} already exists.");
            return false;
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
        File.Copy(outPath, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out.kms"), true);
        DumpToJson(asset, "old.json");

        var script = CompileScript(outPath, noStrict, ver);
        var tempAsset = LoadAsset(assetPath, ver, usmapPath, globalPath);
        var newAsset = new UAssetLinker((UAsset)tempAsset)
            .LinkCompiledScript(script)
            .Build();
        return VerifyEquality(outPath, asset, newAsset);
    }

    return true;
}

static void Decompile(string inputPath, EngineVersion ver, string? usmapPath = default, string outScriptPath = default, bool overwrite = false, bool noVerification = false, bool noStrict = false, string? globalPath = default)
{
    if (Directory.Exists(inputPath))
    {
        var assetFiles = Directory.EnumerateFiles(inputPath, "*.uasset", SearchOption.AllDirectories).ToList();
        var failed = 0;

        for (int i = 0; i < assetFiles.Count; i++)
        {
            var assetFilePath = assetFiles[i];
            var assetScriptFilePath = Path.ChangeExtension(assetFilePath, ".kms");
            try
            {
                if (!DecompileFile(assetFilePath, ver, usmapPath, assetScriptFilePath, overwrite, noVerification, noStrict, globalPath))
                    failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                failed++;
            }
        }

        Console.WriteLine($"{assetFiles.Count - failed}/{assetFiles.Count} decompiled successfully");
    }
    else
    {
        DecompileFile(inputPath, ver, usmapPath, outScriptPath, overwrite, noVerification, noStrict, globalPath);
    }
}

static void Compile(string? inputPath, string scriptPath, EngineVersion ver, string? usmapPath = default, string? outAssetPath = default, bool overwrite = false, bool noStrict = false, string? globalPath = default)
{
    var assetFilePath = NormalizeAssetPath(inputPath);

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
    var script = CompileScript(scriptPath, noStrict, ver);

    UnrealPackage newAsset;
    if (assetFilePath != null)
    {
        Console.WriteLine($"Merging with {assetFilePath}");
        var asset = LoadAsset(assetFilePath, ver, usmapPath, globalPath);
        if (asset is ZenAsset zenAsset)
        {
            newAsset = new ZenAssetLinker(zenAsset)
              //.LinkCompiledScript(script)
              .Build();
        }
        else
        {
            newAsset = new UAssetLinker((UAsset)asset)
              .LinkCompiledScript(script)
              .Build();
        }
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

static CompiledScriptContext CompileScript(string inPath, bool noStrict, EngineVersion engineVersion)
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
            throw new ApplicationException("Failed to parse compilation unit");
        var typeResolver = new TypeResolver();
        typeResolver.ResolveTypes(compilationUnit);
        var compiler = new KismetScriptCompiler();
        compiler.EngineVersion = engineVersion;
        compiler.StrictMode = !noStrict;
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
            throw new ApplicationException("Compilation failed due to syntax error.");
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

static string DumpToJson(UnrealPackage asset, string fileName)
{
    KismetSerializer.asset = asset;

    var jsonObj = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .OrderBy(x => asset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
        .Select(x => new { Function = x.ObjectName.ToString(), Instructions = KismetSerializer.SerializeScript(x.ScriptBytecode) });

    var jsonText = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName), jsonText);
    return jsonText;
}

static bool VerifyEquality(string fileName, UnrealPackage oldAsset, UnrealPackage newAsset)
{
    try
    {
        var oldJsonText = DumpToJson(oldAsset, "old.json");
        var newJsonText = DumpToJson(newAsset, "new.json");
        if (oldJsonText != newJsonText)
        {
            Console.WriteLine("Verification failed");
            PrintDiff(oldJsonText, newJsonText);
            return false;
        }
        else
        {
            Console.WriteLine("Verification succeeded");

            return true;
        }
    }
    catch (Exception)
    {
        Console.WriteLine($"Failed to write verification dumps");
        return false;
    }
}

static void PrintDiff(string text1, string text2)
{
    string[] lines1 = text1.Split('\n');
    string[] lines2 = text2.Split('\n');

    int minLength = Math.Min(lines1.Length, lines2.Length);

    for (int i = 0; i < minLength; i++)
    {
        if (lines1[i] != lines2[i])
        {
            Console.WriteLine($"First difference at line {i + 1}:");
            Console.WriteLine($"Old: {lines1[i]}");
            Console.WriteLine($"New: {lines2[i]}");
            return;
        }
    }

    if (lines1.Length != lines2.Length)
    {
        int differingLine = minLength;
        Console.WriteLine($"First difference at line {differingLine + 1}:");
        Console.WriteLine(lines1.Length > lines2.Length ? $"Old: {lines1[differingLine]}" : $"New: {lines2[differingLine]}");
        return;
    }
}

class OptionsBase
{
    [Option('v', "version", Required = false, HelpText = "Unreal Engine version (eg. 4.23)")]
    public string VersionString { get; set; } = "4.23";

    [Option('f', "overwrite", Required = false, HelpText = "Overwrite existing files")]
    public bool Overwrite { get; set; } = false;
    [Option("usmap", Required = false, HelpText = "Path to a .usmap file")]
    public string? UsmapFilePath { get; set; }
    [Option("global", Required = false, HelpText = "Path to a global.ucas/utoc file")]
    public string? GlobalFilePath { get; set; }
}

[Verb("compile", HelpText = "Compile a script into a new or existing blueprint asset")]
class CompileOptions : OptionsBase
{
    [Option('i', "input", Required = true, HelpText = "Path to a .kms file")]
    public string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Path to the output .uasset file")]
    public string OutputPath { get; set; }

    [Option("asset", Required = false, HelpText = "Path to an input .uasset file.")]
    public string InputAssetFilePath { get; set; }

    [Option("no-strict", Required = false, HelpText = "Allow the compiler to be less strict when evaluating scoping rules as a workaround for unknown symbol errors.")]
    public bool NoStrict { get; set; } = false;
}

[Verb("decompile", HelpText = "Decompile a blueprint asset")]
class DecompileOptions : OptionsBase
{
    [Option('i', "input", Required = true, HelpText = "Path to an input .uasset file or a folder containing .uasset files")]
    public string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Path to the output .kms file. Not applicable if the input is a folder.")]
    public string OutputPath { get; set; }

    [Option("no-verify", Required = false, HelpText = "Skip verifying equality of decompiled code")]
    public bool NoVerification { get; set; } = false;

    [Option("no-strict", Required = false, HelpText = "Allow the compiler to be less strict when evaluating scoping rules as a workaround for unknown symbol errors.")]
    public bool NoStrict { get; set; } = false;
}

[Verb("sdk", HelpText = "Create an SDK")]
class SdkOptions : OptionsBase
{
    [Option('d', "directory", Required = false, HelpText = "Path to a folder containing .uasset files")]
    public string? InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Path to the output .kms file.")]
    public string? OutputPath { get; set; }
}