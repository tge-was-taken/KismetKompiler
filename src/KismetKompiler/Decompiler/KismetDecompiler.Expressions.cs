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
                    return GetSafeName(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                case EX_InstanceVariable expr:
                    {
                        //var context = _context == null ? "this" : _context;
                        var context = "this";
                        return $"{context}.{GetSafeName(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames))}";
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
                        var val = _asset.GetPropertyName(expr.Value, _useFullPropertyNames);
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
                        var rvalue = _asset.GetPropertyName(expr.RValuePointer, _useFullPropertyNames);



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
                            return $"{context}{op}{GetSafeName(expr.VirtualFunctionName.ToString())}()";
                        else
                            return $"{context}{op}{GetSafeName(expr.VirtualFunctionName.ToString())}({parameters})";
                    }
                case EX_ComputedJump expr:
                    return $"goto {FormatExpressionVerbose(expr.CodeOffsetExpression)}";
                case EX_InterfaceContext expr:
                    return $"EX_InterfaceContext({FormatExpressionVerbose(expr.InterfaceValue)})";
                case EX_EndOfScript expr:
                    return $"";
                case EX_CallMath expr:
                    {
                        var functionName = GetSafeName(GetFunctionName(expr.StackNode));
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpressionVerbose(x)));
                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{functionName}()";
                        else
                            return $"{functionName}({parameters})";
                    }
                case EX_LocalFinalFunction expr:
                    {
                        var functionName = GetSafeName(GetFunctionName(expr.StackNode));
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
                            return $"{context}{op}{functionName}({GetSafeName(function.ObjectName.ToString())}_{((uint)firstParamInt.Value)})";
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
                            return $"{context}{op}{GetSafeName(GetFunctionName(expr.StackNode))}()";
                        else
                            return $"{context}{op}{GetSafeName(GetFunctionName(expr.StackNode))}({parameters})";
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
                        return GetSafeName(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
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
                        var prop = GetSafeName(_asset.GetPropertyName(expr.StructMemberExpression, _useFullPropertyNames));
                        return $"{@struct}.{prop}";
                    }
                case EX_ObjectConst expr:
                    {
                        var obj = GetSafeName(_asset.GetName(expr.Value));
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
                        return $"null";
                    }
                case EX_LetValueOnPersistentFrame expr:
                    {
                        var prop = _asset.GetPropertyName(expr.DestinationProperty, _useFullPropertyNames);
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
