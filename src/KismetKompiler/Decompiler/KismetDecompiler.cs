using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net.WebSockets;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace KismetKompiler.Decompiler;

public class KismetDecompiler
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

    private UAsset _asset;
    private FunctionExport _function;
    private int _depth = 0;
    private bool _useFullPropertyNames = false;
    private bool _useFullFunctionNames = false;
    private FunctionState _functionState;
    private bool _verbose = false;
    private readonly TextWriter _writer;
    private Context _context;


    public KismetDecompiler(TextWriter writer)
    {
        _writer = writer;
    }

    private string GetFullName(object obj)
    {
        if (obj is Import import)
        {
            if (import.OuterIndex.Index != 0)
            {
                string parent = GetFullName(import.OuterIndex);
                return parent + "." + import.ObjectName.ToString();
            }
            else
            {
                return import.ObjectName.ToString();
            }
        }
        else if (obj is Export export)
        {
            if (export.OuterIndex.Index != 0)
            {
                string parent = GetFullName(export.OuterIndex);
                return parent + "." + export.ObjectName.ToString();
            }
            else
            {
                return export.ObjectName.ToString();
            }
        }
        else if (obj is FField field)
            return field.Name.ToString();
        else if (obj is FName fname)
            return fname.ToString();
        else
        {
            return "<null>";
        }
    }

    private string GetFullName(FPackageIndex index)
    {
        var obj = GetImportOrExport(index);
        return GetFullName(obj);
    }

    private string GetName(FPackageIndex index)
    {
        if (index.IsExport())
        {
            return index.ToExport(_asset).ObjectName.ToString();
        }
        else
        {
            return index.ToImport(_asset).ObjectName.ToString();
        }
    }

    private FunctionExport GetFunctionExport(FPackageIndex index)
    {
        return (FunctionExport)index.ToExport(_asset);
    }

    private Export GetExport(FPackageIndex index)
    {
        return index.ToExport(_asset);
    }

    private object GetImportOrExport(FPackageIndex index)
    {
        if (index != null)
        {
            if (index.IsExport())
                return index.ToExport(_asset);
            else if (index.IsImport())
                return index.ToImport(_asset);
            else if (index.IsNull())
                return null;
            else
                return null;
        }
        else
        {
            return null;
        }
    }

    public bool FindProperty(int index, FName propname, out FProperty property)
    {
        if (index < 0)
        {

            property = new FObjectProperty();
            return false;

        }
        Export export = _asset.Exports[index - 1];
        if (export is StructExport)
        {
            foreach (FProperty prop in (export as StructExport).LoadedProperties)
            {
                if (prop.Name == propname)
                {
                    property = prop;
                    return true;
                }
            }
        }
        property = new FObjectProperty();
        return false;
    }

    private object GetProperty(KismetPropertyPointer pointer)
    {
        if (pointer.Old != null)
        {
            return GetImportOrExport(pointer.Old);
        }
        else if (pointer.New != null)
        {
            if (pointer.New.ResolvedOwner.Index == 0)
                return null;

            if (FindProperty(pointer.New.ResolvedOwner.Index, pointer.New.Path[0], out var prop))
                return prop;
            else
                return pointer.New.Path[0];
        }

        return null;
    }

    private string GetPropertyName(KismetPropertyPointer pointer)
    {
        var prop = GetProperty(pointer);
        if (_useFullPropertyNames)
        {
            return EscapeFullName(GetFullName(prop));
        }
        else
        {
            if (prop is Export ex)
                return ex.ObjectName.ToString();
            else if (prop is Import im)
                return im.ObjectName.ToString();
            else if (prop is FField field)
                return field.Name.ToString();
            else if (prop is FName fname)
                return fname.ToString();
            else
                return "<null>";
        }
    }

    private string FormatCodeOffset(uint codeOffset) => $"{_function.ObjectName}_{codeOffset}";

    public void DecompileClass()
    {
        var classExport = _asset.GetClassExport();
        var classInterfaces = classExport.Interfaces;
        var classBaseClass = GetName(classExport.SuperStruct);
        var classChildExports = classExport.Children
            .Select(x => x.ToExport(_asset));

        var classFunctions = classChildExports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .OrderBy(x => _asset.Exports.IndexOf((x)));
        var classFunctionPackageIndices = classFunctions
            .Select(x => _asset.Exports.IndexOf(x) + 1);
        var classProperties = classChildExports
            .Where(x => x is PropertyExport)
            .Cast<PropertyExport>()
            .OrderBy(x => _asset.Exports.IndexOf((x)));
        var classUnknown = classChildExports
            .Except(classFunctions)
            .Except(classProperties);

        _writer.WriteLine($"class {classExport.ObjectName} : {classBaseClass} {{");

        foreach (var prop in classProperties)
        {
            _writer.WriteLine($"{GetDecompiledPropertyText(prop)};");
        }

        foreach (var fun in classFunctions)
        {
            DecompileFunction(fun);
        }

        _writer.WriteLine($"}}");
    }

    public string DecompileFunction(FunctionExport function)
    {
        _function = function;
        _functionState = new FunctionState();
        var root = CreateFunctionBasicNode(_function.ScriptBytecode);
        ResolveJumpTargets(root);
        ResolveReferences(root);
        CreateBasicBlocks(root);
        //ReorderBasicBlocks(root);
        //DebugPrintBlocks(root);



        //var result = string.Empty;
        //foreach (var kismetExpr in _export.ScriptBytecode)
        //{
        //    var line = $"_{_index}: {FormatExpression(kismetExpr)};";
        //    _writer.WriteLine(line);
        //    result += line + "\n";
        //}

        //var functionParams = function.Asset
        //    .Exports.Where(x => x is FunctionExport)
        //    .Select(x => (FunctionExport)x)
        //    .SelectMany(x => x.ScriptBytecode)
        //    .Where(x => x.Token == EExprToken.EX_LocalFinalFunction)
        //    .Select(x => ((EX_LocalFinalFunction)x))
        //    .Where(x => GetFunctionName(x.StackNode).ToString() == function.ObjectName.ToString())
        //    .Select(x => x.Parameters)
        //    .FirstOrDefault();

        if (!_verbose)
            WriteOutput(function, root);
        else
            WriteVerboseOutput(function, root);

        _writer.Flush();

        return string.Empty;
    }

    private string GetDecompiledType(PropertyExport prop)
    {
        var classType = prop.GetExportClassType().ToString();
        switch (classType)
        {
            case "IntProperty":
                return "int";
            case "StrProperty":
                return "string";
            case "InterfaceProperty":
                {
                    var interfaceName = GetName(((UInterfaceProperty)prop.Property).InterfaceClass);
                    return interfaceName;
                }
            case "StructProperty":
                {
                    var structName = GetName(((UStructProperty)prop.Property).Struct);
                    return structName;
                }
            case "BoolProperty":
                return "bool";
            case "ByteProperty":
                return "byte";
            case "ArrayProperty":
                {
                    var innerProp = (PropertyExport)((UArrayProperty)prop.Property).Inner.ToExport(_asset);
                    return $"{GetDecompiledType(innerProp)}[]";
                }
            default:
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

    private string GetDecompiledPropertyText(PropertyExport prop)
    {
        var flags =
            typeof(EPropertyFlags)
                .GetEnumValues()
                .Cast<EPropertyFlags>()
                .Where(x => (prop.Property.PropertyFlags & x) != 0 && x != EPropertyFlags.CPF_Parm);

        var modifierFlags = new[] {
            EPropertyFlags.CPF_ConstParm, EPropertyFlags.CPF_OutParm, EPropertyFlags.CPF_ReferenceParm};

        var modifiers = flags.Where(x => modifierFlags.Contains(x))
            .Select(GetModifierForPropertyFlag)
            .ToList();

        if (prop.Property is UArrayProperty)
        {
            modifiers.Remove("ref");
        }

        var attributes = flags
                .Except(modifierFlags)
                .Select(x => x.ToString().Replace("CPF_", "").Replace("Param", ""))
                .ToList();

        var modifierText = string.Join(" ", modifiers).Trim();
        var attributeText = string.Join(", ", attributes).Trim();
        var nameText = $"{GetDecompiledType(prop)} {prop.ObjectName}";

        if (!string.IsNullOrWhiteSpace(attributeText))
            attributeText = $"[{attributeText}]";

        var result = string.Join(" ", attributeText, modifierText, nameText).Trim();
        return result;
    }

    private void WriteOutput(FunctionExport function, Node root)
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
            .Select(x => x == "final" ? "sealed" : x).ToList();
        if (!functionModifiers.Contains("sealed"))
            functionModifiers.Add("virtual");

        var functionAttributes = functionFlags
                .Except(functionModifierFlags)
                .Select(x => x.ToString().Replace("FUNC_", ""))
                .ToList();

        var functionProperties = _asset.Exports
            .Where(x => x is PropertyExport)
            .Cast<PropertyExport>()
            //.Where(x => GetFullName(x).Contains(function.ObjectName.ToString()))
            .Where(x => x.OuterIndex.Index == _asset.Exports.IndexOf(function)+1);

        var functionParams = functionProperties
            .Where(x => (x.Property.PropertyFlags & EPropertyFlags.CPF_Parm) != 0);

        var functionLocals = functionProperties.Except(functionParams);

        var functionParameterText =
            string.Join(", ", functionParams.Select(GetDecompiledPropertyText));

        var isUbergraphFunction = (function.FunctionFlags & EFunctionFlags.FUNC_UbergraphFunction) != 0;

        var functionAttributeText = string.Join(", ", functionAttributes);

        if (!string.IsNullOrWhiteSpace(functionAttributeText))
        {
            _writer.WriteLine($"[{functionAttributeText}]");
        }
        var functionModifierText = string.Join(" ", functionModifiers);
        if (!string.IsNullOrWhiteSpace(functionModifierText))
        {
            _writer.Write(functionModifierText + " ");
        }
        _writer.WriteLine($"void {function.ObjectName}({functionParameterText}) {{");

        if (functionLocals.Any())
        {
            _writer.WriteLine($"    // Locals");
            foreach (var functionLocal in functionLocals)
                _writer.WriteLine($"    {GetDecompiledPropertyText(functionLocal)};");
            _writer.WriteLine();
        }

        var nextBlockIndex = 1;
        foreach (Node block in root.Children)
        {
            Debug.Assert(block.Source == null);
            _writer.WriteLine($"    // Block {nextBlockIndex++}");
            foreach (var node in block.Children)
            {
                string line = "";
                var expr = FormatExpressionVerbose(node.Source);
                var isBlockStart = node.ReferencedBy.Count > 0 ||
                    (isUbergraphFunction && IsUbergraphEntrypoint(node.CodeStartOffset));

                if (string.IsNullOrWhiteSpace(expr))
                {
                    if (isBlockStart)
                    {
                        line = $"    {FormatCodeOffset((uint)node.CodeStartOffset)}:";
                    }
                }
                else
                {
                    if (isBlockStart)
                    {
                        line = $"    {FormatCodeOffset((uint)node.CodeStartOffset)}: {expr}";
                    }
                    else
                    {
                        line = $"    {expr}";
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
            _writer.WriteLine();
        }
        _writer.WriteLine("}\n");
    }

    private bool IsUbergraphEntrypoint(int codeOffset)
    {
        var entryPoints = _asset.Exports
            .Where(x => x is FunctionExport)
            .Cast<FunctionExport>()
            .SelectMany(x => x.ScriptBytecode)
            .Where(x => x.Token == EExprToken.EX_LocalFinalFunction)
            .Cast<EX_LocalFinalFunction>()
            .Where(x => GetFunctionExport(x.StackNode).FunctionFlags.HasFlag(EFunctionFlags.FUNC_UbergraphFunction))
            .Select(x => x.Parameters[0] as EX_IntConst)
            .Select(x => x.Value);
        return entryPoints.Contains(codeOffset);
    }

    private void WriteVerboseOutput(FunctionExport function, Node root)
    {
        var isUbergraphFunction = (function.FunctionFlags & EFunctionFlags.FUNC_UbergraphFunction) != 0;
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
                    line = $"    _{node.CodeStartOffset}: {FormatExpressionVerbose(node.Source)}";
                }
                else
                {
                    line = $"    {FormatExpressionVerbose(node.Source)}";
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

    private int? ResolveComputedJump(KismetExpression expr)
    {
        // TODO
        if (expr is EX_LocalVariable var)
        {
            var prop = GetProperty(var.Variable);
            if (prop is Export exp)
            {
            }
        }

        return null;
    }

    private void ResolveJumpTargets(Node root)
    {
        foreach (var node in root.Children)
        {
            if (node is JumpNode jumpNode)
            {
                switch (jumpNode.Source)
                {
                    case EX_Jump expr:
                        jumpNode.Target = root.Children.First(x => x.CodeStartOffset == expr.CodeOffset);
                        break;
                    case EX_JumpIfNot expr:
                        jumpNode.Target = root.Children.First(x => x.CodeStartOffset == expr.CodeOffset);
                        break;
                    case EX_ComputedJump expr:
                        {
                            var codeOffset = ResolveComputedJump(expr.CodeOffsetExpression);
                            if (codeOffset != null)
                            {
                                jumpNode.Target = root.Children.First(x => x.CodeStartOffset == codeOffset);
                            }
                        }
                        break;
                }
            }
            else
            {
                ResolveJumpTargets(node);
            }
        }
    }

    private void ResolveReferences(Node root)
    {
        foreach (var node in root.Children)
        {
            foreach (var reference in
                    FindJumpNodesForIndex(root, node.CodeStartOffset)
                    .Union(FindPushExecutionFlowsForIndex(root, node.CodeStartOffset)))
                node.ReferencedBy.Add(reference);
        }
    }

    private IEnumerable<JumpNode> FindJumpNodesForIndex(Node root, int index)
    {
        for (int i = 0; i < root.Children.Count; i++)
        {
            if (root.Children[i] is JumpNode jumpNode &&
                jumpNode.Target?.CodeStartOffset == index)
            {
                yield return jumpNode;
            }
        }
    }

    private IEnumerable<Node> FindPushExecutionFlowsForIndex(Node root, int index)
    {
        for (int i = 0; i < root.Children.Count; i++)
        {
            if (root.Children[i].Source is EX_PushExecutionFlow expr && expr.PushingAddress == index)
            {
                yield return root.Children[i];
            }
            else
            {
                foreach (var item in FindPushExecutionFlowsForIndex(root.Children[i], index))
                    yield return item;
            }
        }
    }

    private void CreateBasicBlocks(Node root)
    {
        var newNodes = new List<Node>();

        for (int i = 0; i < root.Children.Count; i++)
        {
            var start = i;
            var end = root.Children.Count - 1;
            for (int j = i; j < root.Children.Count; j++)
            {
                if (root.Children[j] is JumpNode jumpNode ||
                    root.Children[j].Source.Token == EExprToken.EX_EndOfScript)
                {
                    // A jump has been found
                    end = j + 1;
                    break;
                }

                if (root.Children[j].ReferencedBy.Count != 0 && j != i)
                {
                    // Something jumps to this
                    end = j;
                    break;
                }
            }

            var nodes = root.Children
                .Skip(start)
                .Take(end - start);

            newNodes.Add(new Node()
            {
                Children = nodes.ToList()
            });

            i = end - 1;
        }

        root.Children.Clear();
        root.Children.AddRange(newNodes);
    }

    private Node CreateBasicNode(KismetExpression baseExpr, ref int codeOffset)
    {
        var codeStartOffset = codeOffset;
        codeOffset += 1;
        switch (baseExpr)
        {
            case EX_LocalVariable expr:
                codeOffset += 8;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset
                };
            case EX_InstanceVariable expr:
                codeOffset += 8;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset
                };
            case EX_Return expr:
                {
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    block.Children.Add(CreateBasicNode(expr.ReturnExpression, ref codeOffset));
                    return block;
                }
            case EX_Jump expr:
                codeOffset += 4;
                return new JumpNode()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                    Target = null
                };
            case EX_JumpIfNot expr:
                codeOffset += 4;
                return new ConditionalJumpNode()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                    Inverted = true,
                    Condition = CreateBasicNode(expr.BooleanExpression, ref codeOffset),
                };
            case EX_Let expr:
                {
                    codeOffset += 8;
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    block.Children.Add(CreateBasicNode(expr.Variable, ref codeOffset));
                    block.Children.Add(CreateBasicNode(expr.Expression, ref codeOffset));
                    return block;
                }
            case EX_LetBool expr:
                {
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    block.Children.Add(CreateBasicNode(expr.VariableExpression, ref codeOffset));
                    block.Children.Add(CreateBasicNode(expr.AssignmentExpression, ref codeOffset));
                    return block;
                }
            case EX_Context expr:
                {
                    var @object = CreateBasicNode(expr.ObjectExpression, ref codeOffset);
                    codeOffset += 4;
                    codeOffset += 8;
                    var context = CreateBasicNode(expr.ContextExpression, ref codeOffset);
                    return new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Children = { @object, context }
                    };
                }
            case EX_IntConst expr:
                codeOffset += 4;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                };
            case EX_FloatConst expr:
                codeOffset += 4;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                };
            case EX_StringConst expr:
                codeOffset += expr.Value.Length + 1;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                };
            case EX_ByteConst expr:
                codeOffset += 1;
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                };
            case EX_UnicodeStringConst expr:
                codeOffset += 2 * (expr.Value.Length + 1);
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                };
            case EX_LocalVirtualFunction expr:
                {
                    codeOffset += 12;
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    foreach (var param in expr.Parameters)
                        block.Children.Add(CreateBasicNode(param, ref codeOffset));
                    codeOffset += 1;
                    return block;
                }
            case EX_ComputedJump expr:
                return new JumpNode()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                    Children = { CreateBasicNode(expr.CodeOffsetExpression, ref codeOffset) }
                };
            case EX_InterfaceContext expr:
                return new Node()
                {
                    Source = expr,
                    CodeStartOffset = codeStartOffset,
                    CodeEndOffset = codeOffset,
                    Children = { CreateBasicNode(expr.InterfaceValue, ref codeOffset) }
                };
            case EX_CallMath expr:
                {
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    codeOffset += 8;
                    foreach (var param in expr.Parameters)
                    {
                        block.Children.Add(CreateBasicNode(param, ref codeOffset));
                    }
                    codeOffset += 1;
                    return block;
                }
            case EX_LocalFinalFunction expr:
                {
                    var node = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                    codeOffset += 8;
                    foreach (var param in expr.Parameters)
                    {
                        node.Children.Add(CreateBasicNode(param, ref codeOffset));
                    }
                    codeOffset += 1;
                    return node;
                }
            case EX_LocalOutVariable expr:
                {
                    codeOffset += 8;
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr };
                }
            case EX_StructMemberContext expr:
                {
                    codeOffset += 8;
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr, Children = { CreateBasicNode(expr.StructExpression, ref codeOffset) } };
                }
            case EX_ObjectConst expr:
                {
                    codeOffset += 8;
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr };
                }
            case EX_DeprecatedOp4A exp1:
            case EX_Nothing exp2:
            case EX_EndOfScript exp3:
            case EX_IntZero exp4:
            case EX_IntOne exp5:
            case EX_True exp6:
            case EX_False exp7:
            case EX_NoObject exp8:
            case EX_NoInterface exp9:
            case EX_Self exp10:
                {
                    return new Node() { CodeStartOffset = codeStartOffset, Source = baseExpr };
                }
            case EX_FinalFunction expr:
                {
                    var node = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                    codeOffset += 8;
                    foreach (var param in expr.Parameters)
                    {
                        node.Children.Add(CreateBasicNode(param, ref codeOffset));
                    }
                    codeOffset += 1;
                    return node;
                }
            case EX_PushExecutionFlow expr:
                {
                    codeOffset += 4;
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr };
                }
            case EX_SetArray expr:
                {
                    var node = new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr,
                        Children = { CreateBasicNode(expr.AssigningProperty, ref codeOffset) }
                    };
                    foreach (var item in expr.Elements)
                    {
                        node.Children.Add(CreateBasicNode(item, ref codeOffset));
                    }
                    codeOffset += 1;
                    return node;
                }
            case EX_PopExecutionFlow expr:
                {
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr };
                }
            case EX_PopExecutionFlowIfNot expr:
                {
                    return new Node() { CodeStartOffset = codeStartOffset, Source = expr, Children = { CreateBasicNode(expr.BooleanExpression, ref codeOffset) } };
                }
            case EX_SwitchValue expr:
                {
                    codeOffset += 6;
                    var node = new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr,
                        Children =
                        {
                            CreateBasicNode(expr.IndexTerm, ref codeOffset)
                        }
                    };
                    for (int i = 0; i < expr.Cases.Length; i++)
                    {
                        node.Children.Add(CreateBasicNode(expr.Cases[i].CaseIndexValueTerm, ref codeOffset));
                        codeOffset += 4;
                        node.Children.Add(CreateBasicNode(expr.Cases[i].CaseTerm, ref codeOffset));
                    }
                    node.Children.Add(CreateBasicNode(expr.DefaultTerm, ref codeOffset));
                    return node;
                }
            case EX_LetObj expr:
                {
                    return new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr,
                        Children =
                        {
                            CreateBasicNode(expr.VariableExpression, ref codeOffset),
                            CreateBasicNode(expr.AssignmentExpression, ref codeOffset)
                        }
                    };
                }
            case EX_VectorConst expr:
                {
                    codeOffset += sizeof(float) * 3;
                    return new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr
                    };
                }
            case EX_RotationConst expr:
                {
                    codeOffset += sizeof(float) * 3;
                    return new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr
                    };
                }
            case EX_VirtualFunction expr:
                {
                    var node = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                    codeOffset += 12;
                    foreach (var param in expr.Parameters)
                    {
                        node.Children.Add(CreateBasicNode(param, ref codeOffset));
                    }
                    codeOffset += 1;
                    return node;
                }
            case EX_LetValueOnPersistentFrame expr:
                {
                    codeOffset += 8;
                    var block = new Node()
                    {
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                    block.Children.Add(CreateBasicNode(expr.AssignmentExpression, ref codeOffset));
                    return block;
                }
            case EX_NameConst expr:
                {
                    codeOffset += 12;
                    return new Node()
                    {
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Source = expr
                    };
                }
            case EX_ArrayGetByRef expr:
                {
                    return new Node()
                    {
                        Children =
                        {
                            CreateBasicNode(expr.ArrayVariable, ref codeOffset),
                            CreateBasicNode(expr.ArrayIndex, ref codeOffset),
                        }
                    };
                }
            default:
                throw new NotImplementedException(baseExpr.Inst);
        }
    }

    private Node CreateFunctionBasicNode(IList<KismetExpression> expressions)
    {
        var root = new Node();
        var nextIndex = 0;
        foreach (var expr in expressions)
        {
            var node = CreateBasicNode(expr, ref nextIndex);
            root.Children.Add(node);
        }
        return root;
    }

    private string? GetPropertyType(KismetPropertyPointer kismetPropertyPointer)
    {
        var prop = GetProperty(kismetPropertyPointer);
        if (prop != null)
        {
            if (prop is Import import)
            {
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
                    var name = GetName(expr.StackNode);
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
        return name.Replace("/", "::");
    }

    private string GetFunctionName(FPackageIndex index)
    {
        return _useFullFunctionNames ?
            EscapeFullName(GetFullName(index)) :
            GetName(index);
    }

    private string FormatExpressionVerbose(KismetExpression kismetExpression, int? codeOffset = null)
    {
        _depth++;

        try
        {

            switch (kismetExpression)
            {
                case EX_LocalVariable expr:
                    return GetPropertyName(expr.Variable);
                case EX_InstanceVariable expr:
                    {
                        //var context = _context == null ? "this" : _context;
                        var context = "this";
                        return $"{context}.{GetPropertyName(expr.Variable)}";
                    }

                case EX_Return expr:
                    {
                        var exprIndex = Array.IndexOf(_function.ScriptBytecode, kismetExpression);
                        var isUnneccessary =
                            expr.ReturnExpression is EX_Nothing &&
                            exprIndex != -1 && exprIndex + 1 < _function.ScriptBytecode.Length &&
                            _function.ScriptBytecode[exprIndex + 1] is EX_EndOfScript;

                        if (isUnneccessary)
                        {
                            return "";
                        }
                        else
                        {
                            if (expr.ReturnExpression is EX_Nothing)
                            {
                                return $"return";
                            }
                            else
                            {
                                return $"return {FormatExpressionVerbose(expr.ReturnExpression)}";
                            }
                        }
                    }
                case EX_Jump expr:
                    return $"goto {FormatCodeOffset(expr.CodeOffset)}";
                case EX_JumpIfNot expr:
                    return $"if (!({FormatExpressionVerbose(expr.BooleanExpression)})) goto {FormatCodeOffset(expr.CodeOffset)}";
                case EX_Nothing expr:
                    return "";
                case EX_Let expr:
                    {
                        var type = GetExpressionType(expr.Expression) ?? "var";
                        var val = GetPropertyName(expr.Value);
                        var var = FormatExpressionVerbose(expr.Variable);
                        var ex = FormatExpressionVerbose(expr.Expression);
                        var varExists = !_functionState.DeclaredVariables.Add(var) ||
                            var.Contains(".");

                        return $"{var} = {ex}";
                        //return $"EX_Let(\"{val}\", {var},{ex})";
                    }
                case EX_LetBool expr:
                    {
                        var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
                        var var = FormatExpressionVerbose(expr.VariableExpression);
                        var ex = FormatExpressionVerbose(expr.AssignmentExpression);
                        var varExists = !_functionState.DeclaredVariables.Add(var) ||
                            var.Contains(".");

                        return $"{var} = (bool)({ex})";
                    }
                case EX_Context expr:
                    {
                        if (expr.ObjectExpression is EX_InterfaceContext subExpr)
                        {
                            _context = new Context()
                            {
                                Expression = FormatExpressionVerbose(subExpr.InterfaceValue),
                                Type = ContextType.Interface,
                            };
                        }
                        else
                        {
                            var @object = FormatExpressionVerbose(expr.ObjectExpression);
                            _context = new Context()
                            {
                                Expression = @object,
                                Type = ContextType.Default
                            };
                        }
                        var context = FormatExpressionVerbose(expr.ContextExpression);
                        _context = null;

                        var offset = expr.Offset;
                        var rvalue = GetPropertyName(expr.RValuePointer);



                        return context;
                        //return $"{context}.{@object}";
                    }
                case EX_IntConst expr:
                    return $"{expr.Value}";
                case EX_FloatConst expr:
                    return $"{expr.Value}f";
                case EX_StringConst expr:
                    return $"\"{expr.Value}\"";
                case EX_ByteConst expr:
                    return $"(byte)({expr.Value})";
                case EX_UnicodeStringConst expr:
                    return $"\"{expr.Value}\"";
                case EX_LocalVirtualFunction expr:
                    {
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "->" : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}{expr.VirtualFunctionName}()";
                        else
                            return $"{context}{op}{expr.VirtualFunctionName}({parameters})";
                    }
                case EX_ComputedJump expr:
                    return $"goto {FormatExpressionVerbose(expr.CodeOffsetExpression)}";
                case EX_InterfaceContext expr:
                    return $"EX_InterfaceContext({FormatExpressionVerbose(expr.InterfaceValue)})";
                case EX_EndOfScript expr:
                    return $"";
                case EX_CallMath expr:
                    {
                        var functionName = GetFunctionName(expr.StackNode);
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{functionName}()";
                        else
                            return $"{functionName}({parameters})";
                    }
                case EX_LocalFinalFunction expr:
                    {
                        var functionName = GetFunctionName(expr.StackNode);
                        var function = (FunctionExport)_asset.Exports.Where(x => x.ObjectName.ToString() == functionName)
                            .FirstOrDefault();

                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "->" : ".";

                        if (function != null &&
                            function.FunctionFlags.HasFlag(EFunctionFlags.FUNC_UbergraphFunction) &&
                            expr.Parameters.Length == 1 &&
                            expr.Parameters[0] is EX_IntConst firstParamInt)
                        {
                            return $"{context}{op}{functionName}({function.ObjectName}_{((uint)firstParamInt.Value)})";
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(parameters))
                                return $"{context}{op}{functionName}()";
                            else
                                return $"{context}{op}{functionName}({parameters})";
                        }
                    }
                case EX_FinalFunction expr:
                    {
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "->" : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}EX_FinalFunction(\"{GetFunctionName(expr.StackNode)}\")";
                        else
                            return $"{context}{op}EX_FinalFunction({string.Join(", ", $"\"{GetFunctionName(expr.StackNode)}\"", parameters)})";
                    }
                case EX_VirtualFunction expr:
                    {
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "->" : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}EX_VirtualFunction(\"{expr.VirtualFunctionName}\")";
                        else
                            return $"{context}{op}EX_VirtualFunction({string.Join(", ", $"\"{expr.VirtualFunctionName}\"", parameters)})";
                    }
                case EX_LocalOutVariable expr:
                    {
                        return GetPropertyName(expr.Variable);
                    }
                case EX_True expr:
                    return "true";
                case EX_False expr:
                    return "false";
                case EX_Self:
                    return "this";
                case EX_StructMemberContext expr:
                    {
                        var @struct = FormatExpressionVerbose(expr.StructExpression);
                        var prop = GetPropertyName(expr.StructMemberExpression);
                        return $"{@struct}.{prop}";
                    }
                case EX_ObjectConst expr:
                    {
                        var obj = GetName(expr.Value);
                        return $"EX_ObjectConst(\"{obj}\")";
                    }
                case EX_PushExecutionFlow expr:
                    {
                        return $"EX_PushExecutionFlow({FormatCodeOffset(expr.PushingAddress)})";
                    }
                case EX_PopExecutionFlow expr:
                    {
                        return $"EX_PopExecutionFlow()";
                    }
                case EX_PopExecutionFlowIfNot expr:
                    {
                        return $"EX_PopExecutionFlowIfNot({FormatExpressionVerbose(expr.BooleanExpression)})";
                    }
                case EX_SetArray expr:
                    {
                        var prop = FormatExpressionVerbose(expr.AssigningProperty);
                        var elems = string.Join(", ", expr.Elements.Select(x => FormatExpressionVerbose(x)));
                        return $"{prop} = [ {elems} ]";
                    }
                case EX_SwitchValue expr:
                    {
                        var endGotoOffset = FormatCodeOffset(expr.EndGotoOffset);
                        var indexTerm = FormatExpressionVerbose(expr.IndexTerm);
                        var defaultTerm = FormatExpressionVerbose(expr.DefaultTerm);
                        var result = $"EX_SwitchValue({endGotoOffset}, {indexTerm}, {defaultTerm}";
                        foreach (var @case in expr.Cases)
                        {
                            var caseIndexValueTerm = FormatExpressionVerbose(@case.CaseIndexValueTerm);
                            var caseTerm = FormatExpressionVerbose(@case.CaseTerm);
                            var nextCase = FormatCodeOffset(@case.NextOffset);
                            result += $", {caseIndexValueTerm}, {nextCase}, {caseTerm}";
                        }
                        result += $")";
                        return result;
                    }
                case EX_LetObj expr:
                    {
                        var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
                        var var = FormatExpressionVerbose(expr.VariableExpression);
                        var ex = FormatExpressionVerbose(expr.AssignmentExpression);
                        var varExists = !_functionState.DeclaredVariables.Add(var) ||
                            var.Contains(".");

                        return $"EX_LetObj({var},{ex})";

                        if (!varExists)
                        {
                            return $"{type} {var} = {ex}";
                        }
                        else
                        {
                            return $"{var} = {ex}";
                        }
                    }
                case EX_VectorConst expr:
                    {
                        return $"EX_VectorConst({expr.Value.X}, {expr.Value.Y}, {expr.Value.Z})";
                    }
                case EX_RotationConst expr:
                    {
                        return $"EX_RotationConst({expr.Value.Yaw}, {expr.Value.Pitch}, {expr.Value.Roll})";
                    }
                case EX_NoObject expr:
                    {
                        return $"null";
                    }
                case EX_LetValueOnPersistentFrame expr:
                    {
                        var prop = GetPropertyName(expr.DestinationProperty);
                        var assignment = FormatExpressionVerbose(expr.AssignmentExpression);
                        return $"EX_LetValueOnPersistentFrame(\"{prop}\", {assignment})";
                    }
                case EX_NameConst expr:
                    {
                        return $"EX_NameConst(\"{expr.Value}\")";
                    }
                case EX_ArrayGetByRef expr:
                    {
                        return $"EX_ArrayGetByRef({FormatExpressionVerbose(expr.ArrayVariable)}, {FormatExpressionVerbose(expr.ArrayIndex)})";
                    }
                default:
                    throw new NotImplementedException(kismetExpression.Inst);
            }
        }
        finally
        {
            _depth--;
        }
    }


    //private string FormatExpressionVerbose(KismetExpression kismetExpression, int? codeOffset = null)
    //{
    //    _depth++;

    //    try
    //    {

    //        switch (kismetExpression)
    //        {
    //            case EX_LocalVariable expr:
    //                return $"EX_LocalVariable(\"{GetPropertyName(expr.Variable)}\")";
    //            case EX_InstanceVariable expr:
    //                return $"EX_InstanceVariable(\"{GetPropertyName(expr.Variable)}\")";
    //            case EX_Return expr:
    //                return $"EX_Return({FormatExpressionVerbose(expr.ReturnExpression)})";
    //            case EX_Jump expr:
    //                return $"EX_Jump({FormatCodeOffset(expr.CodeOffset)})";
    //            case EX_JumpIfNot expr:
    //                return $"EX_JumpIfNot({FormatCodeOffset(expr.CodeOffset)}, {FormatExpressionVerbose(expr.BooleanExpression)})";
    //            case EX_Nothing expr:
    //                return "EX_Nothing()";
    //            case EX_Let expr:
    //                {
    //                    var type = GetExpressionType(expr.Expression) ?? "var";
    //                    var val = GetPropertyName(expr.Value);
    //                    var var = FormatExpressionVerbose(expr.Variable);
    //                    var ex = FormatExpressionVerbose(expr.Expression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    return $"EX_Let(\"{val}\", {var},{ex})";
    //                }
    //            case EX_LetBool expr:
    //                {
    //                    var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
    //                    var var = FormatExpressionVerbose(expr.VariableExpression);
    //                    var ex = FormatExpressionVerbose(expr.AssignmentExpression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    return $"EX_LetBool({var},{ex})";
    //                }
    //            case EX_Context expr:
    //                {
    //                    var @object = FormatExpressionVerbose(expr.ObjectExpression);
    //                    var offset = expr.Offset;
    //                    var rvalue = GetPropertyName(expr.RValuePointer);
    //                    var context = FormatExpressionVerbose(expr.ContextExpression);
    //                    return $"EX_Context({@object},{offset},\"{rvalue}\", {context})";
    //                }
    //            case EX_IntConst expr:
    //                return $"EX_IntConst({expr.Value})";
    //            case EX_FloatConst expr:
    //                return $"EX_FloatConst({expr.Value})";
    //            case EX_StringConst expr:
    //                return $"EX_StringConst(\"{expr.Value}\")";
    //            case EX_ByteConst expr:
    //                return $"EX_ByteConst({expr.Value})";
    //            case EX_UnicodeStringConst expr:
    //                return $"EX_UnicodeStringConst(\"{expr.Value}\")";
    //            case EX_LocalVirtualFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
    //                    if (string.IsNullOrWhiteSpace(parameters))
    //                        return $"EX_LocalVirtualFunction(\"{expr.VirtualFunctionName}\")";
    //                    else
    //                        return $"EX_LocalVirtualFunction({string.Join(", ", $"\"{expr.VirtualFunctionName}\"", parameters)})";
    //                }
    //            case EX_ComputedJump expr:
    //                return $"EX_ComputedJump({FormatExpressionVerbose(expr.CodeOffsetExpression)})";
    //            case EX_InterfaceContext expr:
    //                return $"EX_InterfaceContext({FormatExpressionVerbose(expr.InterfaceValue)})";
    //            case EX_EndOfScript expr:
    //                return $"EX_EndOfScript()";
    //            case EX_CallMath expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
    //                    if (string.IsNullOrWhiteSpace(parameters))
    //                        return $"EX_CallMath(\"{GetFunctionName(expr.StackNode)}\")";
    //                    else
    //                        return $"EX_CallMath({string.Join(", ", $"\"{GetFunctionName(expr.StackNode)}\"", parameters)})";
    //                }
    //            case EX_LocalFinalFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
    //                    if (string.IsNullOrWhiteSpace(parameters))
    //                        return $"EX_LocalFinalFunction(\"{GetFunctionName(expr.StackNode)}\")";
    //                    else
    //                        return $"EX_LocalFinalFunction({string.Join(", ", $"\"{GetFunctionName(expr.StackNode)}\"", parameters)})";
    //                }
    //            case EX_FinalFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
    //                    if (string.IsNullOrWhiteSpace(parameters))
    //                        return $"EX_FinalFunction(\"{GetFunctionName(expr.StackNode)}\")";
    //                    else
    //                        return $"EX_FinalFunction({string.Join(", ", $"\"{GetFunctionName(expr.StackNode)}\"", parameters)})";
    //                }
    //            case EX_VirtualFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
    //                    if (string.IsNullOrWhiteSpace(parameters))
    //                        return $"EX_VirtualFunction(\"{expr.VirtualFunctionName}\")";
    //                    else
    //                        return $"EX_VirtualFunction({string.Join(", ", $"\"{expr.VirtualFunctionName}\"", parameters)})";
    //                }
    //            case EX_LocalOutVariable expr:
    //                {
    //                    return $"EX_LocalOutVariable(\"{GetPropertyName(expr.Variable)}\")";
    //                }
    //            case EX_True expr:
    //                return "EX_True()";
    //            case EX_False expr:
    //                return "EX_False()";
    //            case EX_Self:
    //                return "EX_Self()";
    //            case EX_StructMemberContext expr:
    //                {
    //                    var @struct = FormatExpressionVerbose(expr.StructExpression);
    //                    var prop = GetPropertyName(expr.StructMemberExpression);
    //                    return $"EX_StructMemberContext(\"{prop}\", {@struct})";
    //                }
    //            case EX_ObjectConst expr:
    //                {
    //                    var obj = GetName(expr.Value);
    //                    return $"EX_ObjectConst(\"{obj}\")";
    //                }
    //            case EX_PushExecutionFlow expr:
    //                {
    //                    return $"EX_PushExecutionFlow({FormatCodeOffset(expr.PushingAddress)})";
    //                }
    //            case EX_PopExecutionFlow expr:
    //                {
    //                    return $"EX_PopExecutionFlow()";
    //                }
    //            case EX_PopExecutionFlowIfNot expr:
    //                {
    //                    return $"EX_PopExecutionFlowIfNot({FormatExpressionVerbose(expr.BooleanExpression)})";
    //                }
    //            case EX_SetArray expr:
    //                {
    //                    var prop = FormatExpressionVerbose(expr.AssigningProperty);
    //                    var elems = string.Join(", ", expr.Elements.Select(x => FormatExpressionVerbose(x)));
    //                    return $"EX_SetArray({prop}, {elems})";
    //                }
    //            case EX_SwitchValue expr:
    //                {
    //                    var indexTerm = FormatExpressionVerbose(expr.IndexTerm);
    //                    var defaultTerm = FormatExpressionVerbose(expr.DefaultTerm);
    //                    var result = $"EX_SwitchValue({indexTerm}, ";
    //                    foreach (var @case in expr.Cases)
    //                    {
    //                        var caseIndexValueTerm = FormatExpressionVerbose(@case.CaseIndexValueTerm);
    //                        var caseTerm = FormatExpressionVerbose(@case.CaseTerm);
    //                        var nextCase = FormatCodeOffset(@case.NextOffset);
    //                        result += $"{caseIndexValueTerm}, {caseTerm}, {nextCase}, ";
    //                    }
    //                    result += $"{defaultTerm})";
    //                    return result;
    //                }
    //            case EX_LetObj expr:
    //                {
    //                    var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
    //                    var var = FormatExpressionVerbose(expr.VariableExpression);
    //                    var ex = FormatExpressionVerbose(expr.AssignmentExpression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    return $"EX_LetObj({var},{ex})";

    //                    if (!varExists)
    //                    {
    //                        return $"{type} {var} = {ex}";
    //                    }
    //                    else
    //                    {
    //                        return $"{var} = {ex}";
    //                    }
    //                }
    //            case EX_VectorConst expr:
    //                {
    //                    return $"EX_VectorConst({expr.Value.X}, {expr.Value.Y}, {expr.Value.Z})";
    //                }
    //            case EX_RotationConst expr:
    //                {
    //                    return $"EX_RotationConst({expr.Value.Yaw}, {expr.Value.Pitch}, {expr.Value.Roll})";
    //                }
    //            case EX_NoObject expr:
    //                {
    //                    return $"EX_NoObject()";
    //                }
    //            case EX_LetValueOnPersistentFrame expr:
    //                {
    //                    var prop = GetPropertyName(expr.DestinationProperty);
    //                    var assignment = FormatExpressionVerbose(expr.AssignmentExpression);
    //                    return $"EX_LetValueOnPersistentFrame(\"{prop}\", {assignment})";
    //                }
    //            case EX_NameConst expr:
    //                {
    //                    return $"EX_NameConst(\"{expr.Value}\")";
    //                }
    //            default:
    //                throw new NotImplementedException(kismetExpression.Inst);
    //        }
    //    }
    //    finally
    //    {
    //        _depth--;
    //    }
    //}


    //private string FormatExpression(KismetExpression kismetExpression)
    //{
    //    _depth++;

    //    try
    //    {

    //        switch (kismetExpression)
    //        {
    //            case EX_LocalVariable expr:
    //                return $"{GetPropertyName(expr.Variable)}";
    //            case EX_InstanceVariable expr:
    //                return $"this.{GetPropertyName(expr.Variable)}";
    //            case EX_Return expr:
    //                return $"return {FormatExpression(expr.ReturnExpression)}";
    //            case EX_Jump expr:
    //                return $"goto {FormatCodeOffset(expr.CodeOffset)}";
    //            case EX_JumpIfNot expr:
    //                return $"if (!({FormatExpression(expr.BooleanExpression)})) goto {FormatCodeOffset(expr.CodeOffset)}";
    //            case EX_Nothing expr:
    //                return "/* EX_Nothing */";
    //            case EX_Let expr:
    //                {
    //                    var type = GetExpressionType(expr.Expression) ?? "var";
    //                    var var = FormatExpression(expr.Variable);
    //                    var ex = FormatExpression(expr.Expression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    if (!varExists)
    //                    {
    //                        return $"{type} {var} = {ex}";
    //                    }
    //                    else
    //                    {
    //                        return $"{var} = {ex}";
    //                    }
    //                }
    //            case EX_LetBool expr:
    //                {
    //                    var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
    //                    var var = FormatExpression(expr.VariableExpression);
    //                    var ex = FormatExpression(expr.AssignmentExpression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    if (!varExists)
    //                    {
    //                        return $"{type} {FormatExpression(expr.VariableExpression)} = {FormatExpression(expr.AssignmentExpression)}";
    //                    }
    //                    else
    //                    {
    //                        return $"{FormatExpression(expr.VariableExpression)} = {FormatExpression(expr.AssignmentExpression)}";
    //                    }
    //                }
    //            case EX_Context expr:
    //                {
    //                    var @object = FormatExpression(expr.ObjectExpression);
    //                    var offset = FormatCodeOffset(expr.Offset);
    //                    var rvalue = GetPropertyName(expr.RValuePointer);
    //                    var context = FormatExpression(expr.ContextExpression);
    //                    return $"{@object}.{context} // Offset={offset} RValuePointer={rvalue}";
    //                }
    //            case EX_IntConst expr:
    //                return $"(int){expr.Value}";
    //            case EX_FloatConst expr:
    //                return $"(float){expr.Value}";
    //            case EX_StringConst expr:
    //                return $"\"{expr.Value}\"";
    //            case EX_ByteConst expr:
    //                return $"(byte){expr.Value}";
    //            case EX_UnicodeStringConst expr:
    //                return $"\"{expr.Value}\"";
    //            case EX_LocalVirtualFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
    //                    return $"{expr.VirtualFunctionName}({parameters})";
    //                }
    //            case EX_ComputedJump expr:
    //                return $"goto {FormatExpression(expr.CodeOffsetExpression)}";
    //            case EX_InterfaceContext expr:
    //                return $"GetInterface({FormatExpression(expr.InterfaceValue)})";
    //            case EX_EndOfScript expr:
    //                return $"/* EX_EndOfScript */";
    //            case EX_CallMath expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
    //                    return $"{GetFunctionName(expr.StackNode)}({parameters})";
    //                }
    //            case EX_LocalFinalFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
    //                    return $"{GetFunctionName(expr.StackNode)}({parameters})";
    //                }
    //            case EX_FinalFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
    //                    return $"{GetFunctionName(expr.StackNode)}({parameters})";
    //                }
    //            case EX_LocalOutVariable expr:
    //                {
    //                    return $"[out] {GetPropertyName(expr.Variable)}";
    //                }
    //            case EX_True expr:
    //                return "true";
    //            case EX_False expr:
    //                return "false";
    //            case EX_Self:
    //                return "this";
    //            case EX_StructMemberContext expr:
    //                {
    //                    var @struct = FormatExpression(expr.StructExpression);
    //                    var prop = GetPropertyName(expr.StructMemberExpression);
    //                    return $"{@struct}.{prop}";
    //                }
    //            case EX_ObjectConst expr:
    //                {
    //                    var obj = GetName(expr.Value);
    //                    return obj;
    //                }
    //            case EX_PushExecutionFlow expr:
    //                {
    //                    return $"PushExecutionFlow({FormatCodeOffset(expr.PushingAddress)})";
    //                }
    //            case EX_PopExecutionFlow expr:
    //                {
    //                    return $"PopExecutionFlow()";
    //                }
    //            case EX_PopExecutionFlowIfNot expr:
    //                {
    //                    return $"if (!({FormatExpression(expr.BooleanExpression)})) PopExecutionFlow()";
    //                }
    //            case EX_SetArray expr:
    //                {
    //                    var prop = FormatExpression(expr.AssigningProperty);
    //                    var elems = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
    //                    return $"{prop} = [{elems}]";
    //                }
    //            case EX_SwitchValue expr:
    //                {
    //                    var indexTerm = FormatExpression(expr.IndexTerm);
    //                    var defaultTerm = FormatExpression(expr.DefaultTerm);
    //                    var result = $"{indexTerm} switch {{\n";
    //                    foreach (var @case in expr.Cases)
    //                    {
    //                        var caseIndexValueTerm = FormatExpression(@case.CaseIndexValueTerm);
    //                        var caseTerm = FormatExpression(@case.CaseTerm);
    //                        var nextCase = FormatCodeOffset(@case.NextOffset);
    //                        result += $"{caseIndexValueTerm} => {caseTerm}; /* NextOffset={nextCase} */";
    //                    }
    //                    result += $"_ => {defaultTerm}\n}}";
    //                    return result;
    //                }
    //            case EX_LetObj expr:
    //                {
    //                    var type = GetExpressionType(expr.AssignmentExpression) ?? "var";
    //                    var var = FormatExpression(expr.VariableExpression);
    //                    var ex = FormatExpression(expr.AssignmentExpression);
    //                    var varExists = (!_functionState.DeclaredVariables.Add(var)) ||
    //                        var.Contains(".");

    //                    if (!varExists)
    //                    {
    //                        return $"{type} {var} = {ex}";
    //                    }
    //                    else
    //                    {
    //                        return $"{var} = {ex}";
    //                    }
    //                }
    //            case EX_VectorConst expr:
    //                {
    //                    return $"Vector(X={expr.Value.X}, Y={expr.Value.Y}, Z={expr.Value.Z})";
    //                }
    //            case EX_RotationConst expr:
    //                {
    //                    return $"Rotation(Yaw={expr.Value.Yaw}, Pitch={expr.Value.Pitch}, Roll={expr.Value.Roll})";
    //                }
    //            case EX_NoObject expr:
    //                {
    //                    return $"null";
    //                }
    //            case EX_VirtualFunction expr:
    //                {
    //                    var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
    //                    return $"{expr.VirtualFunctionName}({parameters})";
    //                }
    //            case EX_LetValueOnPersistentFrame expr:
    //                {
    //                    var prop = GetPropertyName(expr.DestinationProperty);
    //                    var assignment = FormatExpression(expr.AssignmentExpression);
    //                    return $"[OnPersistentFrame] {prop} = {assignment}";
    //                }
    //            case EX_NameConst expr:
    //                {
    //                    return $"\"{expr.Value}\"";
    //                }
    //            default:
    //                throw new NotImplementedException(kismetExpression.Inst);
    //        }
    //    }
    //    finally
    //    {
    //        _depth--;
    //    }
    //}

    public void LoadAssetContext(UAsset asset)
    {
        _asset = asset;
    }

    private class Node
    {
        public KismetExpression Source { get; set; }
        public int CodeStartOffset { get; set; }
        public int CodeEndOffset { get; set; }
        public HashSet<Node> ReferencedBy { get; init; } = new();
        public List<Node> Children { get; init; } = new();

        public override string ToString()
        {
            return $"{CodeStartOffset}: {Source.Inst} {string.Join(' ', Children.Select(x => x.ToString()))}";
        }
    }

    private class JumpNode : Node
    {
        public Node Target { get; set; }

        public override string ToString()
        {
            return $"{CodeStartOffset}: {Source.Inst} -> {Target}";
        }
    }

    private class ConditionalJumpNode : JumpNode
    {
        public Node Condition { get; set; }
        public bool Inverted { get; set; }

        public override string ToString()
        {
            return $"{CodeStartOffset}: {Source.Inst} <{(Inverted ? "not " : "")}{Condition}> -> {Target}";
        }
    }

    private class FunctionState
    {
        public HashSet<string> DeclaredVariables { get; init; } = new();
    }

    public record KismetExpressionInfo(
        KismetExpression Expression,
        int Index);
}
