using System.Diagnostics;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using KismetKompiler.Library;
using KismetKompiler.Library.Decompiler;
using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Passes;
using KismetKompiler.Library.Parser;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.PropertyTypes.Objects;
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

    private static EClassFlags[] classModifierFlags = new[] { EClassFlags.CLASS_Abstract };

    public KismetDecompiler(TextWriter writer)
    {
        _writer = new IndentedWriter(writer);
    }

    public void Decompile(UnrealPackage asset)
    {
        _asset = asset;
        _class = _asset.GetClassExport();
        if (_class != null)
        {
            //_writer.WriteLine($"// LegacyFileVersion={_asset.LegacyFileVersion}");
            //_writer.WriteLine($"// UsesEventDrivenLoader={_asset.UsesEventDrivenLoader}");

            WriteImports();
            WriteClass();
        }
        else
        {
            // Workaround for incomplete assets
            WriteImports();
            foreach (var func in asset.Exports.Where(x => x is FunctionExport).Cast<FunctionExport>())
            {
                DecompileFunction(func);
            }
        }
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
        _writer.WriteLine("// Imports");
        List<Import> imports;
        if (_asset is UAsset)
            imports = ((UAsset)_asset).Imports;
        else
            imports = ((ZenAsset)_asset).Imports.Select(x => x.ToImport((ZenAsset)_asset)).ToList();

        foreach (var import in imports.Where(x => x?.OuterIndex.Index == 0))
        {
            void ProcessImport(Import import)
            {
                var importIndex = -(imports.IndexOf(import) + 1);
                var children = imports.Where(x => x?.OuterIndex.Index == importIndex);
                var className = import.ClassName.ToString();
                var objectName = import.ObjectName.ToString();

                if (className == "ArrayProperty" && children.Any())
                {
                    if (children.Count() != 1)
                        throw new NotImplementedException();

                    _writer.Write($"Array<{GetDecompiledTypeName(children.First())}> {FormatIdentifier(objectName)}");
                }
                else if (className == "Package")
                {
                    var packageName = $"{FormatString(objectName.ToString())}";
                    _writer.Write($"from {packageName} import");
                }
                else if (className == "Function")
                {
                    if (objectName == "Default__Function")
                    {
                        _writer.Write($"Function Default__Function");
                    }
                    else
                    {
                        var functionTokens = new[] { EExprToken.EX_FinalFunction, EExprToken.EX_LocalFinalFunction, EExprToken.EX_LocalVirtualFunction, EExprToken.EX_VirtualFunction, EExprToken.EX_CallMath };
                        var functionCalls = _asset.Exports
                            .Where(x => x is FunctionExport)
                            .Cast<FunctionExport>()
                            .SelectMany(x => x.ScriptBytecode.Flatten())
                            .Where(x => functionTokens.Contains(x.Token))
                            .Select(x => new
                            {
                                Token = x.Token,
                                StackNode = (x as EX_FinalFunction)?.StackNode,
                                Name = (x switch
                                {
                                    EX_FinalFunction funcExpr => _asset.GetName(funcExpr.StackNode),
                                    EX_VirtualFunction funcExpr => funcExpr.VirtualFunctionName.ToString(),
                                })
                            });

                        var callInstructions = functionCalls
                            .Where(x =>
                                (x.StackNode != null && x.StackNode.IsImport() && x.StackNode.ToImport(_asset) == import) ||
                                (x.StackNode == null && x.Name == import.ObjectName.ToString()))
                            .ToList();
                        if (callInstructions.Any(x => x.StackNode != null))
                            callInstructions.RemoveAll(x => x.StackNode == null);

                        var callInstructionTokens = callInstructions
                            .Select(x => x.Token)
                            .Distinct()
                            .ToList();

                        var functionModifiers = new List<string>() { "public" };
                        var functionAttributes = new List<string>() { "Extern", "UnknownSignature" };
                        if (callInstructionTokens.Count > 0)
                        {
                            var callInstruction = callInstructionTokens.First();
                            if (callInstructionTokens.Count > 1)
                            {
                                if (callInstructionTokens.All(x => x == EExprToken.EX_CallMath || x == EExprToken.EX_FinalFunction))
                                {
                                    // TODO
                                    callInstruction = EExprToken.EX_CallMath;
                                }
                                else
                                {
                                    throw new NotImplementedException();
                                }
                            }

                            var functionModifier = callInstruction switch
                            {
                                EExprToken.EX_FinalFunction => "sealed",
                                EExprToken.EX_LocalFinalFunction => "sealed",
                                EExprToken.EX_LocalVirtualFunction => "virtual",
                                EExprToken.EX_VirtualFunction => "virtual",
                                EExprToken.EX_CallMath => "static sealed",
                            };
                            functionModifiers.Add(functionModifier);
                            var functionAttribute = callInstruction switch
                            {
                                EExprToken.EX_LocalFinalFunction => "CalledLocally",
                                EExprToken.EX_LocalVirtualFunction => "CalledLocally",
                                _ => ""
                            };
                            if (!string.IsNullOrWhiteSpace(functionAttribute))
                                functionAttributes.Add(functionAttribute);
                        }

                        var functionAttributeText = string.Join(", ", functionAttributes);
                        if (!string.IsNullOrWhiteSpace(functionAttributeText))
                            functionAttributeText = $"[{functionAttributeText}] ";

                        var functionModifierText = string.Join(" ", functionModifiers);
                        if (!string.IsNullOrWhiteSpace(functionModifierText))
                            functionModifierText = $"{functionModifierText} ";

                        _writer.Write($"{functionAttributeText}{functionModifierText}void {FormatIdentifier(import.ObjectName.ToString())}()");
                    }
                }
                else if (_asset.ImportInheritsType(import, "Class"))
                {
                    if (className == "Class")
                    {
                        // Try to detect the base class based on the members accessed
                        // TODO: replace this with an evaluation phase where the code is evaluated
                        // and context is used to deduce which base class each type has

                        // Find properties of the imported type
                        var targetProperties = _asset.Exports
                            .Where(x => x is PropertyExport)
                            .Cast<PropertyExport>()
                            .Where(x =>
                                (x.Property as UObjectProperty)?.PropertyClass.Index == importIndex)
                            .ToList();

                        // Look for context expressions on these properties
                        bool IsTargetProperty(KismetPropertyPointer ptr)
                        {
                            if (ptr.Old != null)
                            {
                                return ptr.Old.IsExport() && targetProperties.Contains(ptr.Old.ToExport(_asset) as PropertyExport);
                            }
                            else
                            {
                                return ptr.New.ResolvedOwner.IsExport() && targetProperties.Contains(ptr.New.ResolvedOwner.ToExport(_asset) as PropertyExport);
                            }
                        }

                        Import GetImportFromProperty(KismetPropertyPointer ptr)
                        {
                            if (ptr.Old != null)
                            {
                                return ptr.Old.ToImport(_asset);
                            }
                            else
                            {
                                return ptr.New.ResolvedOwner.ToImport(_asset);
                            }
                        }


                        var baseClassImport = _asset.Exports
                            .Where(x => x is FunctionExport)
                            .Cast<FunctionExport>()
                            .SelectMany(x => x.ScriptBytecode)
                            .Flatten()
                            .Where(x =>
                                // Find a context expression
                                x is EX_Context context &&

                                // ..where the target property is accessed through local variable
                                context.ObjectExpression is EX_LocalVariable local &&
                                IsTargetProperty(local.Variable) &&

                                // ..and the member being accessed is an instance variable
                                // given that the import does not have any children, this must be the base class
                                context.ContextExpression is EX_InstanceVariable)
                            .Select(x => GetImportFromProperty(((EX_InstanceVariable)((EX_Context)x).ContextExpression).Variable))
                            .Select(x => x.OuterIndex.ToImport(_asset))
                            .Distinct()
                            .SingleOrDefault();

                        if (baseClassImport != null)
                        {
                            _writer.Write($"class {FormatIdentifier(objectName)} : {FormatIdentifier(baseClassImport.ObjectName.ToString())}");
                        }
                        else
                        {
                            _writer.Write($"class {FormatIdentifier(objectName)}");
                        }
                    }
                    else
                    {
                        if (children.Any())
                        {
                            _writer.Write($"class {FormatIdentifier(objectName)} : {(GetDecompiledTypeName(import))}");
                        }
                        else
                        {
                            _writer.Write($"{(GetDecompiledTypeName(import))} {FormatIdentifier(objectName)}");
                        }
                    }
                }
                else if (_asset.ImportInheritsType(import, "Struct"))
                {
                    if (className == "Struct")
                    {
                        _writer.Write($"struct {FormatIdentifier(objectName)}");
                    }
                    else
                    {
                        if (children.Any())
                        {
                            _writer.Write($"struct {FormatIdentifier(objectName)} : {(GetDecompiledTypeName(import))}");
                        }
                        else
                        {
                            _writer.Write($"{(GetDecompiledTypeName(import))} {FormatIdentifier(objectName)}");
                        }
                    }
                }
                else
                {
                    _writer.Write($"{(GetDecompiledTypeName(import))} {FormatIdentifier(import.ObjectName.ToString())}");
                }

                if (children.Any())
                {
                    _writer.WriteLine(" {");
                    _writer.Push();
                    foreach (var subImport in children)
                    {
                        ProcessImport(subImport);
                    }
                    _writer.Pop();
                    _writer.WriteLine("}");
                }
                else
                {
                    _writer.WriteLine(";");
                }
            }

            var name = _asset.GetFullName(import);
            var parentName = _asset.GetFullName(import.OuterIndex);
            var parentNameEscaped = parentName.Replace("/", ".").TrimStart('.');
            if (parentNameEscaped == "<null>")
                parentNameEscaped = "";
            else
                parentNameEscaped = $"{parentNameEscaped}";

            var fullClassName = _asset.GetFullName(import.ClassName);

            ProcessImport(import);
        }

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

        if (!_verbose)
            WriteFunction(function, root);
        else
            WriteFunctionVerbose(function, root);

        _writer.Flush();

        return string.Empty;
    }

    private interface IPropertyData
    {
        UnrealPackage Asset { get; }
        object Source { get; }

        string Name { get; }
        EPropertyFlags PropertyFlags { get; }
        string TypeName { get; }

        string? PropertyClassName { get; }
        string? InterfaceClassName { get; }
        string? StructName { get; }
        IPropertyData ArrayInnerProperty { get; }
    }

    private class FPropertyData : IPropertyData
    {
        public UnrealPackage Asset { get; }
        public FProperty Source { get; }

        public FPropertyData(UnrealPackage Asset, FProperty source)
        {
            this.Asset = Asset;
            Source = source;
        }

        public string Name => Source.Name.ToString();
        public EPropertyFlags PropertyFlags => Source.PropertyFlags;
        public string TypeName => Source.SerializedType.ToString();

        public string? PropertyClassName 
            => Asset.GetName(((FObjectProperty)Source).PropertyClass);

        public string? InterfaceClassName
            => Asset.GetName(((FInterfaceProperty)Source).InterfaceClass);

        public string? StructName
            => Asset.GetName(((FStructProperty)Source).Struct);

        public IPropertyData? ArrayInnerProperty
        {
            get
            {
                var inner = ((FArrayProperty)Source).Inner;
                if (inner != null)
                {
                    return new FPropertyData(Asset, inner);
                }
                return null;
            }
        }

        object IPropertyData.Source => Source;
    }

    private class PropertyExportData : IPropertyData
    {
        public UnrealPackage Asset => Source.Asset;
        public PropertyExport Source { get; }

        public PropertyExportData(PropertyExport source)
        {
            Source = source;
        }

        public string Name 
            => Source.ObjectName.ToString();

        public EPropertyFlags PropertyFlags 
            => Source.Property.PropertyFlags;

        public string TypeName 
            => Source.GetExportClassType().ToString();

        public string? PropertyClassName
            => Asset.GetName(((UObjectProperty)Source.Property).PropertyClass);

        public string? InterfaceClassName 
            => Asset.GetName(((UInterfaceProperty)Source.Property).InterfaceClass);

        public string? StructName 
            => Asset.GetName(((UStructProperty)Source.Property).Struct);

        public IPropertyData? ArrayInnerProperty
        {
            get
            {
                var inner = ((UArrayProperty)Source.Property).Inner;
                if (inner.IsExport())
                {
                    var export = inner.ToExport(Asset);
                    if (export is PropertyExport propertyExport)
                    {
                        var innerProp = (PropertyExport)export;
                        return new PropertyExportData(innerProp);
                    }
                }
                return null;
            }
        }

        object IPropertyData.Source => Source;
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
        var functionDeclaration = $"void {FormatIdentifier(function.ObjectName.ToString())}({functionParameterText}) {{";
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
                    var isBlockStart = block.ReferencedBy.Count > 0 ||
                        (isUbergraphFunction && IsUbergraphEntrypoint(block.CodeStartOffset));

                    if (isBlockStart)
                        _writer.WriteLine($"{FormatCodeOffset((uint)block.CodeStartOffset)}:");

                    var cond = FormatExpression(ifBlock.Condition);
                    _writer.WriteLine($"if ({cond}) {{");
                    _writer.Push();
                    WriteBlock(ifBlock);
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
                                WriteExpression(node, isUbergraphFunction, $"if (!{FormatExpression(jumpIfNot.BooleanExpression)}) return");
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
        var isBlockStart = node.ReferencedBy.Count > 0 ||
            (isUbergraphFunction && IsUbergraphEntrypoint(node.CodeStartOffset));

        if (string.IsNullOrWhiteSpace(expr))
        {
            if (isBlockStart)
            {
                line = $"{FormatCodeOffset((uint)node.CodeStartOffset)}:";
            }
        }
        else
        {
            if (isBlockStart)
            {
                line = $"{FormatCodeOffset((uint)node.CodeStartOffset)}: {expr}";
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
        => WriteExpression(node, isUbergraphFunction, FormatExpression(node.Source));

    private void WriteFunctionVerbose(FunctionExport function, Node root)
    {
        var isUbergraphFunction = function.IsUbergraphFunction();
        _writer.WriteLine($"void {function.ObjectName}() {{");
        var result = string.Empty;
        var nextBlockIndex = 1;
        foreach (Node block in root.Children)
        {
            Debug.Assert(block.Source == null);
            _writer.WriteLine($"    // Block {nextBlockIndex++}");
            foreach (var node in block.Children)
            {
                string line = "";
                if (node.ReferencedBy.Count > 0 ||
                    (isUbergraphFunction && IsUbergraphEntrypoint(node.CodeStartOffset)))
                {
                    line = $"    _{node.CodeStartOffset}: {FormatExpression(node.Source)}";
                }
                else
                {
                    line = $"    {FormatExpression(node.Source)}";
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
                _writer.WriteLine(line);
            }
            _writer.WriteLine();
        }
        _writer.WriteLine("}\n");
    }

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

    private string GetDecompiledTypeName(Import import)
    {
        var classType = import.ClassName.ToString();
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
            return $"``{name}``";

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

    private bool IsUbergraphEntrypoint(int codeOffset)
    {
        var entryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .SelectMany(x => x.ScriptBytecode)
            .Where(x => x.Token == EExprToken.EX_LocalFinalFunction)
            .Cast<EX_LocalFinalFunction>()
            .Where(x => x.StackNode.IsExport() && _asset.GetFunctionExport(x.StackNode).IsUbergraphFunction())
            .Select(x => x.Parameters[0] as EX_IntConst)
            .Select(x => x.Value);
        return entryPoints.Contains(codeOffset);
    }

    private string? GetPropertyType(KismetPropertyPointer kismetPropertyPointer)
    {
        var prop = _asset.GetProperty(kismetPropertyPointer);
        if (prop != null)
        {
            if (prop is Import import)
            {
                // TODO
            }
            else if (prop is Export export)
            {
                var classType = export.GetExportClassType();
                if (classType.ToString() == "IntProperty")
                {
                    return "int";
                }
                else if (classType.ToString() == "BoolProperty")
                {
                    return "bool";
                }
                else
                {
                    // TODO
                }
            }
        }
        return null;
    }

    private string? GetExpressionType(KismetExpression kismetExpression)
    {
        switch (kismetExpression)
        {
            case EX_LocalVariable expr:
                return GetPropertyType(expr.Variable);
            case EX_InstanceVariable expr:
                return GetPropertyType(expr.Variable);
            case EX_IntConst expr:
                return "int";
            case EX_FloatConst expr:
                return "float";
            case EX_StringConst expr:
                return "string";
            case EX_ByteConst expr:
                return "byte";
            case EX_UnicodeStringConst:
                return "wstring";
            case EX_True:
                return "bool";
            case EX_False:
                return "bool";
            case EX_CallMath expr:
                {
                    var name = _asset.GetName(expr.StackNode);
                    if (name.EndsWith("ToString"))
                    {
                        return "string";
                    }
                    else if (name.EndsWith("ToInt"))
                    {
                        return "int";
                    }
                    else if (name.EndsWith("ToByte"))
                    {
                        return "byte";
                    }
                    else if (
                        name.StartsWith("NotEqual_") ||
                        name.StartsWith("GreaterEqual_") ||
                        name.StartsWith("EqualEqual_") ||
                        name.StartsWith("Boolean") ||
                        name.StartsWith("Less_") ||
                        name.StartsWith("Greater_") ||
                        name.StartsWith("Not_"))
                    {
                        return "bool";
                    }

                    switch (name)
                    {
                        case "Conv_IntToString":
                        case "Concat_StrStr":
                            return "string";

                        case "NotEqual_IntInt":
                        case "GreaterEqual_IntInt":
                        case "EqualEqual_IntInt":
                        case "BooleanOR":
                        case "Less_IntInt":
                            return "bool";

                        case "Add_IntInt":
                            return "int";

                        case "RandomIntegerInRange":
                            return "int";

                        default:
                            break;
                    }
                    return null;
                }
            default:
                return null;
        }
    }

    private string EscapeFullName(string name)
    {
        return name;
    }

    private string GetFunctionName(FPackageIndex index)
    {
        return _useFullFunctionNames ?
            EscapeFullName(_asset.GetFullName(index)) :
            _asset.GetName(index);
    }

    private string FormatCodeOffset(uint codeOffset, string? functionName = null) => FormatIdentifier($"{(functionName ?? _function.ObjectName.ToString())}_{codeOffset}");
    [GeneratedRegex("^[A-Za-z_][A-Za-z_\\d]*$")]
    private static partial Regex IdentifierRegex();
}