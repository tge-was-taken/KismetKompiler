using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Decompiler
{
    public partial class KismetDecompiler
    {
        // TODO factor this out of the class
        private string FormatExpressionVerbose(KismetExpression kismetExpression, int? codeOffset = null)
        {
            switch (kismetExpression)
            {
                case EX_LocalVariable expr:
                    return FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                case EX_InstanceVariable expr:
                    {
                        //var context = _context == null ? "this" : _context;
                        var context = "this";
                        return $"{context}.{FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames))}";
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
                        var val = FormatIdentifier(_asset.GetPropertyName(expr.Value, _useFullPropertyNames));
                        var var = FormatExpressionVerbose(expr.Variable);
                        var ex = FormatExpressionVerbose(expr.Expression);
                        var varExists = !_functionState.DeclaredVariables.Add(var) ||
                            var.Contains(".");

                        return $"{var} = {ex}";
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
                        var rvalue = _asset.GetPropertyName(expr.RValuePointer, _useFullPropertyNames);



                        return context;
                        //return $"{context}.{@object}";
                    }
                case EX_IntConst expr:
                    return $"{expr.Value}";
                case EX_FloatConst expr:
                    return $"{expr.Value}f";
                case EX_StringConst expr:
                    return $"{FormatString(expr.Value)}";
                case EX_ByteConst expr:
                    return $"(byte)({expr.Value})";
                case EX_UnicodeStringConst expr:
                    return $"{FormatString(expr.Value)}";
                case EX_LocalVirtualFunction expr:
                    {
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "." : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}{FormatIdentifier(expr.VirtualFunctionName.ToString())}()";
                        else
                            return $"{context}{op}{FormatIdentifier(expr.VirtualFunctionName.ToString())}({parameters})";
                    }
                case EX_ComputedJump expr:
                    return $"goto {FormatExpressionVerbose(expr.CodeOffsetExpression)}";
                case EX_InterfaceContext expr:
                    return $"EX_InterfaceContext({FormatExpressionVerbose(expr.InterfaceValue)})";
                case EX_EndOfScript expr:
                    return $"";
                case EX_CallMath expr:
                    {
                        var functionName = FormatIdentifier(GetFunctionName(expr.StackNode));
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{functionName}()";
                        else
                            return $"{functionName}({parameters})";
                    }
                case EX_LocalFinalFunction expr:
                    {
                        var functionName = FormatIdentifier(GetFunctionName(expr.StackNode));
                        var function = (FunctionExport)_asset.Exports.Where(x => x.ObjectName.ToString() == functionName && x is FunctionExport)
                        .FirstOrDefault();

                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "." : ".";

                        if (function != null &&
                            function.FunctionFlags.HasFlag(EFunctionFlags.FUNC_UbergraphFunction) &&
                            expr.Parameters.Length == 1 &&
                            expr.Parameters[0] is EX_IntConst firstParamInt)
                        {
                            return $"{context}{op}{functionName}({FormatIdentifier(function.ObjectName.ToString())}_{((uint)firstParamInt.Value)})";
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
                        var op = _context?.Type == ContextType.Interface ? "." : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}{FormatIdentifier(GetFunctionName(expr.StackNode))}()";
                        else
                            return $"{context}{op}{FormatIdentifier(GetFunctionName(expr.StackNode))}({parameters})";
                    }
                case EX_VirtualFunction expr:
                    {
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        var context = _context == null ? "this" : _context.Expression;
                        var op = _context?.Type == ContextType.Interface ? "." : ".";

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}{op}EX_VirtualFunction({FormatString(expr.VirtualFunctionName.ToString())})";
                        else
                            return $"{context}{op}EX_VirtualFunction({string.Join(", ", $"{FormatString(expr.VirtualFunctionName.ToString())}", parameters)})";
                    }
                case EX_LocalOutVariable expr:
                    {
                        return FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
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
                        var prop = FormatIdentifier(_asset.GetPropertyName(expr.StructMemberExpression, _useFullPropertyNames));
                        return $"{@struct}.{prop}";
                    }
                case EX_ObjectConst expr:
                    {
                        var obj = FormatIdentifier(_asset.GetName(expr.Value));
                        return obj;
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
                        var endGotoOffset = expr.EndGotoOffset;//FormatCodeOffset(expr.EndGotoOffset);
                        var indexTerm = FormatExpressionVerbose(expr.IndexTerm);
                        var defaultTerm = FormatExpressionVerbose(expr.DefaultTerm);
                        var result = $"EX_SwitchValue({endGotoOffset}, {indexTerm}, {defaultTerm}";
                        foreach (var @case in expr.Cases)
                        {
                            var caseIndexValueTerm = FormatExpressionVerbose(@case.CaseIndexValueTerm);
                            var caseTerm = FormatExpressionVerbose(@case.CaseTerm);
                            //var nextCase = FormatCodeOffset(@case.NextOffset);
                            var nextCase = @case.NextOffset;
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
                        return $"EX_NoObject()";
                    }
                case EX_LetValueOnPersistentFrame expr:
                    {
                        var prop = FormatIdentifier(_asset.GetPropertyName(expr.DestinationProperty, _useFullPropertyNames));
                        var assignment = FormatExpressionVerbose(expr.AssignmentExpression);
                        return $"EX_LetValueOnPersistentFrame({FormatString(prop)}, {assignment})";
                    }
                case EX_NameConst expr:
                    {
                        return $"EX_NameConst({FormatString(expr.Value.ToString())})";
                    }
                case EX_ArrayGetByRef expr:
                    {
                        return $"EX_ArrayGetByRef({FormatExpressionVerbose(expr.ArrayVariable)}, {FormatExpressionVerbose(expr.ArrayIndex)})";
                    }
                case EX_StructConst expr:
                    {
                        var structName = FormatIdentifier(_asset.GetName(expr.Struct));
                        if (expr.Value?.Length > 0)
                        {
                            var members = string.Join(", ", expr.Value.Select(x => FormatExpressionVerbose(x)));
                            return $"EX_StructConst({structName}, {expr.StructSize}, {members})";
                        }
                        else
                        {
                            return $"EX_StructConst({structName}, {expr.StructSize})";
                        }
                            
                    }
                case EX_ObjToInterfaceCast expr:
                    {
                        var classPtr = _asset.GetName(expr.ClassPtr);
                        var target = FormatExpressionVerbose(expr.Target);
                        return $"EX_ObjToInterfaceCast({FormatString(classPtr)}, {target})";
                    }
                case EX_PrimitiveCast expr:
                    {
                        var castType = expr.ConversionType.ToString();
                        var target = FormatExpressionVerbose(expr.Target);
                        return $"EX_PrimitiveCast({FormatString(castType)}, {target})";
                    }
                case EX_SkipOffsetConst expr:
                    {
                        var target = FormatCodeOffset(expr.Value);
                        return $"EX_SkipOffsetConst({target})";
                    }
                case EX_BindDelegate expr:
                    {
                        var name = expr.FunctionName.ToString();
                        var delegat = FormatExpressionVerbose(expr.Delegate);
                        var objectTerm = FormatExpressionVerbose(expr.ObjectTerm);
                        return $"EX_BindDelegate({FormatString(name)}, {delegat}, {objectTerm})";
                    }
                case EX_NoInterface expr:
                    {
                        return $"EX_NoInterface()";
                    }
                case EX_SoftObjectConst expr:
                    {
                        var val = FormatExpressionVerbose(expr.Value);
                        return $"EX_SoftObjectConst({val})";
                    }
                case EX_MetaCast expr:
                    {
                        var classPtr = _asset.GetName(expr.ClassPtr);
                        var target = FormatExpressionVerbose(expr.TargetExpression);
                        return $"EX_MetaCast({FormatString(classPtr)}, {target})";
                    }
                case EX_DynamicCast expr:
                    {
                        var classPtr = _asset.GetName(expr.ClassPtr);
                        var target = FormatExpressionVerbose(expr.TargetExpression);
                        return $"EX_DynamicCast({FormatString(classPtr)}, {target})";
                    }
                case EX_AddMulticastDelegate expr:
                    {
                        var delegat = FormatExpressionVerbose(expr.Delegate);
                        var delegateToAdd = FormatExpressionVerbose(expr.DelegateToAdd);
                        return $"EX_AddMulticastDelegate({delegat}, {delegateToAdd})";
                    }
                case EX_ArrayConst expr:
                    {
                        var innerProperty = FormatIdentifier(_asset.GetPropertyName(expr.InnerProperty, _useFullPropertyNames));
                        var elements = string.Join(", ", expr.Elements.Select(x => FormatExpressionVerbose(x)));
                        if (!string.IsNullOrWhiteSpace(elements))
                        {
                            return $"EX_ArrayConst({innerProperty}, {elements})";
                        }
                        else
                        {
                            return $"EX_ArrayConst({innerProperty})";
                        }
                    }
                case EX_TransformConst expr:
                    {
                        var @params = 
                            $"{expr.Value.Rotation.X}, {expr.Value.Rotation.Y}, {expr.Value.Rotation.Z}, {expr.Value.Rotation.W}, " +
                            $"{expr.Value.Translation.X}, {expr.Value.Translation.Y}, {expr.Value.Translation.Z}, " +
                            $"{expr.Value.Scale3D.X}, {expr.Value.Scale3D.Y}, {expr.Value.Scale3D.Z}";
                        return $"EX_TransformConst({@params})";
                    }
                case EX_ClearMulticastDelegate expr:
                    {
                        var delegateToClear = FormatExpressionVerbose(expr.DelegateToClear);
                        return $"EX_ClearMulticastDelegate({delegateToClear})";
                    }
                case EX_TextConst expr:
                    {
                        var type = FormatString(expr.Value.TextLiteralType.ToString());
                        switch (expr.Value.TextLiteralType)
                        {
                            case EBlueprintTextLiteralType.Empty:
                                return $"EX_TextConst({type})";
                            case EBlueprintTextLiteralType.LocalizedText:
                                {
                                    var localizedSource = FormatExpressionVerbose(expr.Value.LocalizedSource);
                                    var localizedKey = FormatExpressionVerbose(expr.Value.LocalizedKey);
                                    var localizedNamespace = FormatExpressionVerbose(expr.Value.LocalizedNamespace);
                                    return $"EX_TextConst({type}, {localizedSource}, {localizedKey}, {localizedNamespace})";
                                }
                            case EBlueprintTextLiteralType.InvariantText:
                                {
                                    var invariantLiteralString = FormatExpressionVerbose(expr.Value.InvariantLiteralString);
                                    return $"EX_TextConst({type}, {invariantLiteralString})";
                                }
                            case EBlueprintTextLiteralType.LiteralString:
                                {
                                    var literalString = FormatExpressionVerbose(expr.Value.LiteralString);
                                    return $"EX_TextConst({type}, {literalString})";
                                }
                            case EBlueprintTextLiteralType.StringTableEntry:
                                {
                                    var stringTableAsset = _asset.GetName(expr.Value.StringTableAsset);
                                    var stringTableId = FormatExpressionVerbose(expr.Value.StringTableId);
                                    var stringTableKey = FormatExpressionVerbose(expr.Value.StringTableKey);
                                    return $"EX_TextConst({type}, {stringTableAsset}, {stringTableId}, {stringTableKey})";
                                }
                            default:
                                throw new NotImplementedException($"EX_TextConst TextLiteralType {expr.Value.TextLiteralType} not implemented");
                        }
                    }
                case EX_RemoveMulticastDelegate expr:
                    {
                        var delegat = FormatExpressionVerbose(expr.Delegate);
                        var delegateToAdd = FormatExpressionVerbose(expr.DelegateToAdd);
                        return $"EX_RemoveMulticastDelegate({delegat}, {delegateToAdd})";
                    }
                case EX_InterfaceToObjCast expr:
                    {
                        var classPtr = _asset.GetName(expr.ClassPtr);
                        var target = FormatExpressionVerbose(expr.Target);
                        return $"EX_InterfaceToObjCast({FormatString(classPtr)}, {target})";
                    }
                case EX_SetMap expr:
                    {
                        var prop = FormatExpressionVerbose(expr.MapProperty);
                        var elems = string.Join(", ", expr.Elements.Select(x => FormatExpressionVerbose(x)));
                        if (!string.IsNullOrWhiteSpace(elems))
                            return $"EX_SetMap({prop}, {elems})";
                        else
                            return $"EX_SetMap()";
                    }
                default:
                    throw new NotImplementedException(kismetExpression.Inst);
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
    }
}
