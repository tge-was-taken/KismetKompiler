using System.Diagnostics;
using System.Text.RegularExpressions;
using KismetKompiler.Library.Decompiler;
using KismetKompiler.Library.Decompiler.Analysis;
using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using KismetKompiler.Library.Decompiler.Context.Properties;
using KismetKompiler.Library.Decompiler.Passes;
using KismetKompiler.Library.Parser;
using KismetKompiler.Library.Utilities;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Decompiler;

public partial class KismetDecompiler
{
    enum ContextType
    {
        Default,
        Interface
    }

    class Context
    {
        public string Expression { get; set; }
        public ContextType Type { get; set; }
    }

    private UnrealPackage _asset;
    private FunctionExport _function;
    private int _depth = 0;
    private bool _useFullPropertyNames = false;
    private bool _useFullFunctionNames = false;
    private FunctionState _functionState;
    private bool _verbose = false;
    private readonly IndentedWriter _writer;
    private Context _context;
    private ClassExport _class;
    private PackageAnalysisResult _analysisResult;

    private static EClassFlags[] classModifierFlags = new[] { EClassFlags.CLASS_Abstract };

    public KismetDecompiler(TextWriter writer)
    {
        _writer = new IndentedWriter(writer);
    }

    public void Decompile(UnrealPackage asset)
    {
        _asset = asset;
        _class = _asset.GetClassExport();

        var analyser = new PackageAnalyser();
        _analysisResult = analyser.Analyse(asset);

        if (_class != null)
        {
            //_writer.WriteLine($"// LegacyFileVersion={_asset.LegacyFileVersion}");
            //_writer.WriteLine($"// UsesEventDrivenLoader={_asset.UsesEventDrivenLoader}");

            //WriteImportsOld();
            WriteBuiltins();
            WriteImports();
            WriteClass();
        }
        else
        {
            // Workaround for incomplete assets
            WriteImports();
            //WriteImportsOld();
            foreach (var func in asset.Exports.Where(x => x is FunctionExport).Cast<FunctionExport>())
            {
                DecompileFunction(func);
            }
        }
    }

    private void WriteBuiltins()
    {
    }

    private void WriteClass()
    {
        var classBaseClass = _asset.GetName(_class.SuperStruct);
        var classChildExports = _class.Children
            .Select(x => x.ToExport(_asset));

        var classProperties = classChildExports
            .Where(x => x is PropertyExport)
            .Cast<PropertyExport>()
            .Select(x => (IPropertyData)new PropertyExportData(x))
            .OrderBy(x => _asset.Exports.IndexOf((Export)x.Source))
            .Union(_class.LoadedProperties.Select(x => new FPropertyData(_asset, x)))
            .ToList();

        var classFunctions = classChildExports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .OrderBy(x => _class.FuncMap.IndexOf(x.ObjectName));

        var classModifiers = GetClassModifiers(_class);
        var classAttributes = GetClassAttributes(_class);

        var modifierText = string.Join(" ", classModifiers).Trim();
        var attributeText = string.Join(", ", classAttributes).Trim();
        var nameText = $"class {_class.ObjectName} : {classBaseClass}";

        if (!string.IsNullOrWhiteSpace(attributeText))
            attributeText = $"[{attributeText}]";

        _writer.WriteLine($"{string.Join(" ", new[] { attributeText, modifierText }.Where(x => !string.IsNullOrWhiteSpace(x)))}");
        _writer.WriteLine($"{nameText} {{");
        _writer.Push();

        foreach (var prop in classProperties)
        {
            _writer.WriteLine($"{GetDecompiledPropertyText(prop)};");
        }

        foreach (var fun in classFunctions)
        {
            DecompileFunction(fun);
        }

        _writer.Pop();
        _writer.WriteLine($"}}");
    }

    private void WriteImports()
    {
        var importQueue = new Queue<Symbol>();
        var isInsideClassDecl = false;

        void WriteImport(Symbol symbol)
        {
            if (symbol.Parent == null)
            {
                if (symbol.Class?.Name != "Package")
                    Trace.WriteLine($"Invalid class for package {symbol}");

                foreach (var child in symbol.Children)
                    WriteImport(child);
                _writer.WriteLine();
            }
            else
            {
                if (symbol.Parent?.Class?.Name == "Package")
                {
                    _writer.WriteLine();
                    _writer.WriteLine($"[Import({FormatString(symbol.Parent.Name)})]");
                }
                if (symbol.Children.Count > 0)
                {
                    if (symbol.Class.Name == "ArrayProperty")
                    {
                        if (symbol.Children.Count() != 1)
                            throw new NotImplementedException();

                        _writer.Write($"Array<{GetDecompiledTypeName(symbol.Children.First().Class.Name)}> {FormatIdentifier(symbol.Name)}");
                    }
                    else if (isInsideClassDecl)
                    {
                        _writer.WriteLine($"public {FormatIdentifier(symbol.Class?.Name)} {FormatIdentifier(symbol.Name)};");
                        if (!importQueue.Any(x => x.Name == symbol.Name))
                            importQueue.Enqueue(symbol);
                    }
                    else
                    {
                        var modifiers = new List<string> { "public" };
                        if (symbol.ClassMetadata.IsStaticClass)
                            modifiers.Add("static");
                        var modifierText = string.Join(" ", modifiers);
                        if (!string.IsNullOrWhiteSpace(modifierText))
                            modifierText += " ";

                        if (symbol.Super != null)
                            _writer.WriteLine($"{modifierText}class {FormatIdentifier(symbol.Name)} : {FormatIdentifier(symbol.Super.Name)} {{");
                        else
                            _writer.WriteLine($"{modifierText}class {FormatIdentifier(symbol.Name)} {{");
                        _writer.Push();
                        isInsideClassDecl = true;
                        foreach (var child in symbol.Children)
                            WriteImport(child);
                        isInsideClassDecl = false;
                        _writer.Pop();
                        _writer.WriteLine("}");

                        while (importQueue.Count > 0)
                        {
                            var queuedSymbol = importQueue.Dequeue();
                            //WriteImport(queuedSymbol);
                        }
                    }
                }
                else if (symbol.Class?.Name == "Function")
                {
                    if (symbol.Name == "Default__Function")
                    {
                        _writer.WriteLine($"{FormatIdentifier(symbol.Class.Name)} {FormatIdentifier(symbol.Name)};");
                    }
                    else
                    {
                        var functionModifiers = new List<string>() { "public" };
                        var functionAttributes = new List<string>() { "UnknownSignature" };

                        var functionModifier = symbol.FunctionMetadata.CallingConvention switch
                        {
                            CallingConvention.FinalFunction => "sealed",
                            CallingConvention.LocalFinalFunction => "sealed",

                            CallingConvention.VirtualFunction => "virtual",
                            CallingConvention.LocalVirtualFunction => "virtual",

                            CallingConvention.CallMath => "static sealed",
                            _ => "",
                        };
                        functionModifiers.Add(functionModifier);
                        var functionAttribute = symbol.FunctionMetadata.CallingConvention switch
                        {
                            CallingConvention.FinalFunction => "FinalFunction",
                            CallingConvention.LocalFinalFunction => "LocalFinalFunction",

                            CallingConvention.VirtualFunction => "VirtualFunction",
                            CallingConvention.LocalVirtualFunction => "LocalVirtualFunction",

                            CallingConvention.CallMath => "MathFunction",
                            _ => ""
                        };
                        if (!string.IsNullOrWhiteSpace(functionAttribute))
                            functionAttributes.Add(functionAttribute);

                        var functionAttributeText = string.Join(", ", functionAttributes);
                        if (!string.IsNullOrWhiteSpace(functionAttributeText))
                            functionAttributeText = $"[{functionAttributeText}] ";

                        var functionModifierText = string.Join(" ", functionModifiers);
                        if (!string.IsNullOrWhiteSpace(functionModifierText))
                            functionModifierText = $"{functionModifierText} ";

                        var functionParameterText =
                            string.Join(", ", symbol.FunctionMetadata.Parameters.Select(x => $"{GetDecompiledTypeName(x.Class.Name)} {x.Name}"));

                        var functionReturnTypeText =
                            symbol.FunctionMetadata.ReturnType == null ? "void" : GetDecompiledTypeName(symbol.FunctionMetadata.ReturnType.Name);

                        _writer.WriteLine($"{functionAttributeText}{functionModifierText}{functionReturnTypeText} {FormatIdentifier(symbol.Name)}({functionParameterText});");
                    }
                }
                else
                {
                    if (isInsideClassDecl)
                    {
                        var cls = (symbol.Class?.Name == "Class" || symbol.Class?.Name == "BlueprintGeneratedClass" || symbol.Class == null) ? "object" : symbol.Class.Name;
                        _writer.WriteLine($"public {FormatIdentifier(cls)} {FormatIdentifier(symbol.Name)};");
                    }
                    else
                    {
                        if (symbol.Class != null && (symbol.Class.Name != "Class" && symbol.Class.Name != "BlueprintGeneratedClass"))
                            _writer.WriteLine($"public {FormatIdentifier(symbol.Class.Name)} {FormatIdentifier(symbol.Name)};");
                        else
                        {
                            if (symbol.Super != null)
                            {
                                _writer.WriteLine($"public class {FormatIdentifier(symbol.Name)} : {FormatIdentifier(symbol.Super.Name)} {{}}");
                            }
                            else
                            {
                                _writer.WriteLine($"public class {FormatIdentifier(symbol.Name)} {{}}");
                            }
                        }
                    }
                }
            }
        }

        var importSymbols = _analysisResult.RootSymbols
            .Where(x => x.Import != null)
            .OrderBy(x => x.ImportIndex!.Index)
            .ToList();
        foreach (var symbol in importSymbols)
            WriteImport(symbol);

        _writer.WriteLine();
    }

    public string DecompileFunction(FunctionExport function)
    {
        _asset ??= (UAsset)function.Asset;
        _function = function;
        _functionState = new FunctionState();
        var root = ExecutePass<CreateBasicNodesPass>(null);
        root = ExecutePass<ResolveJumpTargetsPass>(root);
        root = ExecutePass<ResolveReferencesPass>(root);
        root = ExecutePass<CreateBasicBlocksPass>(root);
        root = ExecutePass<RemoveGotoReturnsPass>(root);
        root = ExecutePass<CreateIfBlocksPass>(root);
        root = ExecutePass<CreateWhileBlocksPass>(root);

        WriteFunction(function, root);

        _writer.Flush();

        return string.Empty;
    }

    private void WriteFunction(FunctionExport function, Node root)
    {
        var functionFlags =
            typeof(EFunctionFlags)
                .GetEnumValues()
                .Cast<EFunctionFlags>()
                .Where(x => (function.FunctionFlags & x) != 0 && x != EFunctionFlags.FUNC_AllFlags);

        var functionModifierFlags = new[] {
            EFunctionFlags.FUNC_Final, EFunctionFlags.FUNC_Static, EFunctionFlags.FUNC_Public, EFunctionFlags.FUNC_Private, EFunctionFlags.FUNC_Protected};

        var functionModifiers = functionFlags.Where(x => functionModifierFlags.Contains(x))
            .Select(x => x.ToString().Replace("FUNC_", "").ToLower())
            .Select(x => x == "final" ? "sealed" : x)
            .ToList();
        if (!function.SuperIndex.IsNull())
            functionModifiers.Add("override");

        var functionAttributes = functionFlags
                .Except(functionModifierFlags)
                .Select(x => x.ToString().Replace("FUNC_", ""))
                .ToList();

        //var functionProperties = function.Children != null ?
        //    function.Children.Select(x => x.ToExport(_asset)).Cast<PropertyExport>() :
        //    _asset.Exports.Where(x => !x.OuterIndex.IsNull() && x.OuterIndex.ToExport(_asset) == function)
        //    .Cast<PropertyExport>();

        var functionChildExports =
            _asset.Exports.Where(x => !x.OuterIndex.IsNull() && x.OuterIndex.ToExport(_asset) == function);

        var functionProperties =
            functionChildExports
            .Where(x => x is PropertyExport)
            .Select(x => (IPropertyData)new PropertyExportData((PropertyExport)x))
            .Union(function.LoadedProperties.Select(x => new FPropertyData(_asset, x)))
            .ToList();

        var functionParams = functionProperties
            .Where(x => (x.PropertyFlags & EPropertyFlags.CPF_Parm) != 0);

        var functionLocals = functionProperties
            .Except(functionParams);


        var functionParameterText =
            string.Join(", ", functionParams.Select(GetDecompiledPropertyText));

        var isUbergraphFunction = function.IsUbergraphFunction();

        var functionAttributeText = string.Join(", ", functionAttributes);

        if (!string.IsNullOrWhiteSpace(functionAttributeText))
        {
            _writer.WriteLine($"[{functionAttributeText}]");
        }
        var functionName = function.ObjectName.ToString();
        var functionDeclaration = $"void {FormatIdentifier(functionName)}({functionParameterText}) {{";
        var functionModifierText = string.Join(" ", functionModifiers);
        if (!string.IsNullOrWhiteSpace(functionModifierText))
        {
            functionDeclaration = functionModifierText + " " + functionDeclaration;
        }

        _writer.WriteLine(functionDeclaration);
        _writer.Push();

        if (functionLocals.Any())
        {
            _writer.WriteLine($"// Locals");
            foreach (var functionLocal in functionLocals)
                _writer.WriteLine($"{GetDecompiledPropertyText(functionLocal)};");
            _writer.WriteLine();
        }

        var nextBlockIndex = 1;
        void WriteBlock(Node root)
        {
            foreach (Node block in root.Children)
            {
                if (block is IfBlockNode ifBlock)
                {
                    var isBlockStart = block.ReferencedBy.Count > 0 || (isUbergraphFunction && IsUbergraphEntrypoint(block.CodeStartOffset));
                    string? callingFunctionName = default;
                    if (isBlockStart)
                        callingFunctionName = GetUbergraphEntryFunction(block.CodeStartOffset)?.ObjectName?.ToString();

                    if (isBlockStart)
                        _writer.WriteLine($"{FormatCodeOffset((uint)block.CodeStartOffset, functionName, callingFunctionName)}:");

                    var cond = FormatExpression(ifBlock.Condition, null);
                    _writer.WriteLine($"if ({cond}) {{");
                    _writer.Push();
                    WriteBlock(ifBlock);
                    _writer.Pop();
                    _writer.WriteLine($"}}");
                    _writer.WriteLine();
                }
                else if (block is JumpBlockNode whileBlock)
                {
                    var isBlockStart = block.ReferencedBy.Count > 0 || (isUbergraphFunction && IsUbergraphEntrypoint(block.CodeStartOffset));
                    string? callingFunctionName = default;
                    if (isBlockStart)
                        callingFunctionName = GetUbergraphEntryFunction(block.CodeStartOffset)?.ObjectName?.ToString();

                    if (isBlockStart)
                        _writer.WriteLine($"{FormatCodeOffset((uint)block.CodeStartOffset, functionName, callingFunctionName)}:");

                    _writer.WriteLine($"while (true) {{");
                    _writer.Push();
                    WriteBlock(whileBlock);
                    _writer.Pop();
                    _writer.WriteLine($"}}");
                    _writer.WriteLine();
                }
                else
                {
                    _writer.WriteLine($"// Block {nextBlockIndex++}");
                    foreach (var node in block.Children)
                    {
                        string line = "";
                        string expr = "";
                        if (node is ReturnNode returnNode)
                        {
                            if (returnNode.Source is EX_Jump jump)
                            {
                                WriteExpression(node, isUbergraphFunction, "return");
                            }
                            else if (returnNode.Source is EX_JumpIfNot jumpIfNot)
                            {
                                WriteExpression(node, isUbergraphFunction, $"if (!{FormatExpression(jumpIfNot.BooleanExpression, null)}) return");
                            }
                            else
                            {
                                WriteExpression(node, isUbergraphFunction);
                            }
                        }
                        else if (node is JumpNode jumpNode)
                        {
                            if (!jumpNode.Parent.Children.Any(x => x.Source is EX_PushExecutionFlow) &&
                                !jumpNode.Parent.Parent.Children.SelectMany(x => x.Children).Any(x => x.Source is EX_PushExecutionFlow))
                            {
                                if (jumpNode.Source is EX_PopExecutionFlow)
                                {
                                    WriteExpression(node, isUbergraphFunction, "break");
                                }
                                else if (jumpNode.Source is EX_PopExecutionFlowIfNot popExecutionFlowIfNot)
                                {
                                    WriteExpression(node, isUbergraphFunction, $"if (!{FormatExpression(popExecutionFlowIfNot.BooleanExpression, null)}) break");
                                }
                                else
                                {
                                    WriteExpression(node, isUbergraphFunction);
                                }
                            }
                            else
                            {
                                WriteExpression(node, isUbergraphFunction);
                            }
                        }
                        else
                        {
                            WriteExpression(node, isUbergraphFunction);
                        }
  
                    }
                }
                _writer.WriteLine();
            }
        }

        WriteBlock(root);

        _writer.Pop();
        _writer.WriteLine("}\n");
    }

    private void WriteExpression(Node node, bool isUbergraphFunction, string expr)
    {
        string line = "";
        var isBlockStart = node.ReferencedBy.Count > 0 || (isUbergraphFunction && IsUbergraphEntrypoint(node.CodeStartOffset));
        string? callingFunctionName = default;
        if (isBlockStart)
            callingFunctionName = GetUbergraphEntryFunction(node.CodeStartOffset)?.ObjectName?.ToString();

        if (string.IsNullOrWhiteSpace(expr))
        {
            if (isBlockStart)
            {
                line = $"{FormatCodeOffset((uint)node.CodeStartOffset, _function.ObjectName.ToString(), callingFunctionName)}:";
            }
        }
        else
        {
            if (isBlockStart)
            {
                line = $"{FormatCodeOffset((uint)node.CodeStartOffset, _function.ObjectName.ToString(), callingFunctionName)}: {expr}";
            }
            else
            {
                line = $"{expr}";
            }

            if (line.Contains(" //"))
            {
                var parts = line.Split(" //");
                if (!string.IsNullOrWhiteSpace(parts[0]))
                    line = string.Join("; //", parts);
            }
            else if (line.Contains(" /*"))
            {
                var parts = line.Split(" /*");
                if (!string.IsNullOrWhiteSpace(parts[0]))
                    line = string.Join("; /*", parts);
            }
            else
            {
                line += ";";
            }
        }

        if (!string.IsNullOrWhiteSpace(line))
            _writer.WriteLine(line);
    }

    private void WriteExpression(Node node, bool isUbergraphFunction)
        => WriteExpression(node, isUbergraphFunction, FormatExpression(node.Source, null));

    public IEnumerable<EClassFlags> GetClassFlags(ClassExport classExport)
    {
        var classFlags = typeof(EClassFlags)
            .GetEnumValues()
            .Cast<EClassFlags>()
            .Where(x => (classExport.ClassFlags & x) != 0);
        return classFlags;
    }

    public List<string> GetClassModifiers(ClassExport classExport)
    {
        var classModifiers =
            GetClassFlags(classExport)
            .Where(x => classModifierFlags.Contains(x))
            .Select(GetModifierForClassFlag)
            .ToList();

        return classModifiers;
    }

    public List<string> GetClassAttributes(ClassExport classExport)
    {
        var classAttributes =
                GetClassFlags(classExport)
                .Except(classModifierFlags)
                .Select(x => x.ToString().Replace("CLASS_", "").Trim())
                .ToList();
        return classAttributes;
    }

    private Node ExecutePass<T>(Node? root) where T : IDecompilerPass, new()
    {
        var pass = new T();
        return pass.Execute(new DecompilerContext()
        {
            Asset = _asset,
            Class = _class,
            Function = _function
        }, root);
    }

    private string GetDecompiledTypeName(string classType)
    {
        switch (classType)
        {
            case "Package":
                return "package";
            case "FloatProperty":
                return "float";
            case "IntProperty":
                return "int";
            case "StrProperty":
                return "string";
            case "BoolProperty":
                return "bool";
            case "ByteProperty":
                return "byte";
            case "Class":
                return "class";
            default:
                if (classType != "Property" &&
                    classType.EndsWith("Property"))
                    return FormatIdentifier(classType.Substring(0, classType.IndexOf("Property")));

                return FormatIdentifier(classType);
        }
    }


    private string GetDecompiledTypeName(Import import)
    {
        var classType = import.ClassName.ToString();
        return GetDecompiledTypeName(classType);
    }

    private string GetDecompiledType(IPropertyData prop)
    {
        var classType = prop.TypeName;
        switch (classType)
        {
            case "IntProperty":
                return "int";
            case "StrProperty":
                return "string";
            case "FloatProperty":
                return "float";
            case "InterfaceProperty":
                {
                    var interfaceName = prop.InterfaceClassName;
                    return $"Interface<{interfaceName}>";
                }
            case "StructProperty":
                {
                    var structName = prop.StructName;
                    return $"Struct<{structName}>";
                }
            case "BoolProperty":
                return "bool";
            case "ByteProperty":
                return "byte";
            case "ArrayProperty":
                {
                    if (prop.ArrayInnerProperty != null)
                    {
                        return $"Array<{GetDecompiledType(prop.ArrayInnerProperty)}>";
                    }

                    // TODO
                    return $"Array";
                }
            case "ObjectProperty":
                {
                    return $"Object<{prop.PropertyClassName}>";
                }
            default:
                if (classType != "Property" &&
                    classType.EndsWith("Property"))
                    return classType.Substring(0, classType.IndexOf("Property"));

                return classType;
        }
    }

    private string GetModifierForPropertyFlag(EPropertyFlags flag)
    {
        switch (flag)
        {
            case EPropertyFlags.CPF_ConstParm:
                return "const";
            case EPropertyFlags.CPF_Parm:
                return "";
            case EPropertyFlags.CPF_OutParm:
                return "out";
            case EPropertyFlags.CPF_ReferenceParm:
                return "ref";
            default:
                throw new ArgumentOutOfRangeException(nameof(flag));
        }
    }

    private string GetModifierForClassFlag(EClassFlags flag)
    {
        switch (flag)
        {
            case EClassFlags.CLASS_Abstract:
                return "abstract";
            default:
                throw new ArgumentOutOfRangeException(nameof(flag));
        }
    }

    private string FormatIdentifier(string name, bool allowKeywords = false)
    {
        if (!IdentifierRegex().IsMatch(name) ||
            (!allowKeywords && KismetScriptParser.IsKeyword(name)))
            return $"`{name}`";

        return name;
    }

    private string FormatString(string value)
    {
        if (value.Contains("\\"))
            value = value.Replace("\\", "\\\\");
        return $"\"{value}\"";
    }

    private string GetDecompiledPropertyText(IPropertyData prop)
    {
        var flags =
            typeof(EPropertyFlags)
                .GetEnumValues()
                .Cast<EPropertyFlags>()
                .Where(x => (prop.PropertyFlags & x) != 0 && x != EPropertyFlags.CPF_Parm);

        var modifierFlags = new[] {
            EPropertyFlags.CPF_ConstParm, EPropertyFlags.CPF_OutParm, EPropertyFlags.CPF_ReferenceParm};

        var modifiers = flags.Where(x => modifierFlags.Contains(x))
            .Select(GetModifierForPropertyFlag)
            .ToList();

        var attributes = flags
                .Except(modifierFlags)
                .Select(x => x.ToString().Replace("CPF_", "").Replace("Param", "").Replace("Parm", "").Trim())
                .ToList();


        //if (!prop.Property.PropertyFlags.HasFlag(EPropertyFlags.CPF_Parm))
        //{
        //    if (modifiers.Contains("ref"))
        //    {
        //        modifiers.Remove("ref");
        //        attributes.Add("Ref");
        //    }
        //}

        var modifierText = string.Join(" ", modifiers).Trim();
        var attributeText = string.Join(", ", attributes).Trim();
        var nameText = $"{GetDecompiledType(prop)} {FormatIdentifier(prop.Name)}";

        if (!string.IsNullOrWhiteSpace(attributeText))
            attributeText = $"[{attributeText}]";

        var result = string.Join(" ", new[] { attributeText, modifierText, nameText }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return result;
    }

    private FunctionExport? GetUbergraphEntryFunction(int codeOffset)
    {
        var finalFunctionEntryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .Where(x =>
                x.ScriptBytecode
                .Where(x => x is EX_FinalFunction)
                .Cast<EX_FinalFunction>()
                .Where(x => x.StackNode.IsExport() && _asset.GetFunctionExport(x.StackNode).IsUbergraphFunction())
                .Where(x => x.Parameters.Any() && (x.Parameters[0] as EX_IntConst)?.Value == codeOffset)
                .Any()
            );

        var virtualFunctionEntryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .Where(x =>
                x.ScriptBytecode
                .Where(x => x is EX_VirtualFunction)
                .Cast<EX_VirtualFunction>()
                .Where(x => x.VirtualFunctionName.ToString().StartsWith("ExecuteUbergraph_"))
                .Where(x => x.Parameters.Any() && (x.Parameters[0] as EX_IntConst)?.Value == codeOffset)
                .Any()
            );

        return finalFunctionEntryPoints.Union(virtualFunctionEntryPoints).SingleOrDefault();
    }

    private bool IsUbergraphEntrypoint(int codeOffset)
    {
        var finalFunctionEntryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .SelectMany(x => x.ScriptBytecode)
            .Where(x => x.Token == EExprToken.EX_LocalFinalFunction || x.Token == EExprToken.EX_FinalFunction)
            .Cast<EX_FinalFunction>()
            .Where(x => x.StackNode.IsExport() && _asset.GetFunctionExport(x.StackNode).IsUbergraphFunction())
            .Select(x => x.Parameters[0] as EX_IntConst)
            .Select(x => x.Value);

        var virtualFunctionEntryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .SelectMany(x => x.ScriptBytecode)
            .Where(x => x.Token == EExprToken.EX_VirtualFunction || x.Token == EExprToken.EX_LocalVirtualFunction)
            .Cast<EX_VirtualFunction>()
            .Where(x => x.VirtualFunctionName.ToString().StartsWith("ExecuteUbergraph_"))
            .Select(x => x.Parameters[0] as EX_IntConst)
            .Select(x => x.Value);

        return finalFunctionEntryPoints.Union(virtualFunctionEntryPoints).Contains(codeOffset);
    }

    private string EscapeFullName(string name)
    {
        return name;
    }

    private string GetFunctionClassName(FPackageIndex index)
    {
        var classIndex = _asset.GetOuterIndex(index);
        return _asset.GetName(classIndex);
    }

    private string GetFunctionName(FPackageIndex index)
    {
        return _useFullFunctionNames ?
            EscapeFullName(_asset.GetFullName(index)) :
            _asset.GetName(index);
    }

    private string FormatCodeOffset(uint codeOffset, string? functionName = null, string? callingFunctionName = null)
    {
        if (callingFunctionName != null &&
            callingFunctionName != functionName)
        {
            return FormatIdentifier($"{(functionName ?? _function.ObjectName.ToString())}_{codeOffset}_{callingFunctionName}");
        }
        else
        {
            return FormatIdentifier($"{(functionName ?? _function.ObjectName.ToString())}_{codeOffset}");
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z_\\d]*$")]
    private static partial Regex IdentifierRegex();
}