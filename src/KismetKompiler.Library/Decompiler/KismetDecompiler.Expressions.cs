using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;
using KismetKompiler.Library;

namespace KismetKompiler.Decompiler
{
    public partial class KismetDecompiler
    {
        // TODO factor this out of the class
        private string FormatExpression(KismetExpression kismetExpression, KismetExpression? parentKismetExpression = default)
        {
            switch (kismetExpression)
            {
                case EX_PrimitiveCast expr:
                    {
                        var castType = FormatString(expr.ConversionType.ToString());
                        var target = FormatExpression(expr.Target, expr);
                        return $"EX_PrimitiveCast({castType}, {target})";
                    }
                case EX_SetSet expr:
                    {
                        var setProperty = FormatExpression(expr.SetProperty, expr);
                        var elements = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        if (!string.IsNullOrWhiteSpace(elements))
                            return $"EX_SetSet({setProperty}, {elements})";
                        else
                            return $"EX_SetSet()";
                    }
                case EX_SetConst expr:
                    {
                        var innerProperty = FormatString(_asset.GetPropertyName(expr.InnerProperty, _useFullPropertyNames));
                        var elements = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        if (!string.IsNullOrWhiteSpace(elements))
                            return $"EX_SetConst({innerProperty}, {elements})";
                        else
                            return $"EX_SetConst()";
                    }
                case EX_SetMap expr:
                    {
                        var prop = FormatExpression(expr.MapProperty, expr);
                        var elems = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        if (!string.IsNullOrWhiteSpace(elems))
                            return $"EX_SetMap({prop}, {elems})";
                        else
                            return $"EX_SetMap()";
                    }
                case EX_MapConst expr:
                    {
                        var keyProperty = FormatString(_asset.GetPropertyName(expr.KeyProperty, _useFullPropertyNames));
                        var valueProperty = FormatString(_asset.GetPropertyName(expr.ValueProperty, _useFullPropertyNames));
                        var elements = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        if (!string.IsNullOrWhiteSpace(elements))
                            return $"EX_MapConst({keyProperty}, {valueProperty}, {elements})";
                        else
                            return $"EX_MapConst({keyProperty}, {valueProperty})";
                    }
                case EX_ObjToInterfaceCast expr:
                    {
                        var classPtr = FormatString(_asset.GetName(expr.ClassPtr));
                        var target = FormatExpression(expr.Target, expr);
                        return $"EX_ObjToInterfaceCast({classPtr}, {target})";
                    }
                case EX_CrossInterfaceCast expr:
                    {
                        var classPtr = FormatString(_asset.GetName(expr.ClassPtr));
                        var target = FormatExpression(expr.Target, expr);
                        return $"EX_CrossInterfaceCast({classPtr}, {target})";
                    }
                case EX_InterfaceToObjCast expr:
                    {
                        var classPtr = FormatString(_asset.GetName(expr.ClassPtr));
                        var target = FormatExpression(expr.Target, expr);
                        return $"EX_InterfaceToObjCast({classPtr}, {target})";
                    }
                case EX_Let expr:
                    {
                        var value = FormatIdentifier(_asset.GetPropertyName(expr.Value, _useFullPropertyNames));
                        var variable = FormatExpression(expr.Variable, expr);
                        var expression = FormatExpression(expr.Expression, expr);
                        return $"{variable} = {expression}";
                    }
                case EX_LetObj expr:
                    {
                        var variableExpression = FormatExpression(expr.VariableExpression, expr);
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"EX_LetObj({variableExpression},{assignmentExpression})";
                    }
                case EX_LetWeakObjPtr expr:
                    {
                        var variableExpression = FormatExpression(expr.VariableExpression, expr);
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"EX_LetWeakObjPtr({variableExpression},{assignmentExpression})";
                    }
                case EX_LetBool expr:
                    {
                        var variableExpression = FormatExpression(expr.VariableExpression, expr);
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"{variableExpression} = (bool)({assignmentExpression})";
                    }
                case EX_LetValueOnPersistentFrame expr:
                    {
                        var destinationProperty = FormatString(_asset.GetPropertyName(expr.DestinationProperty, _useFullPropertyNames));
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"EX_LetValueOnPersistentFrame({destinationProperty}, {assignmentExpression})";
                    }
                case EX_StructMemberContext expr:
                    {
                        var structExpression = FormatExpression(expr.StructExpression, expr);
                        var structMemberExpression = FormatIdentifier(_asset.GetPropertyName(expr.StructMemberExpression, _useFullPropertyNames));
                        return $"{structExpression}.{structMemberExpression}";
                    }
                case EX_LetDelegate expr:
                    {
                        var variableExpression = FormatExpression(expr.VariableExpression, expr);
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"EX_LetDelegate({variableExpression},{assignmentExpression})";
                    }
                case EX_LocalVirtualFunction expr:
                    {
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = context;
                        _context = null;

                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
                        var virtualFunctionName = FormatIdentifier(expr.VirtualFunctionName.ToString());

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}.{virtualFunctionName}()";
                        else
                            return $"{context}.{virtualFunctionName}({parameters})";
                    }
                case EX_LocalFinalFunction expr:
                    {
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = _context;
                        _context = null;

                        var functionName = FormatIdentifier(GetFunctionName(expr.StackNode));
                        var function = (FunctionExport)_asset.Exports.Where(x => x.ObjectName.ToString() == functionName && x is FunctionExport)
                        .FirstOrDefault();

                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));

                        if (function != null &&
                            function.IsUbergraphFunction() &&
                            expr.Parameters.Length == 1 &&
                            expr.Parameters[0] is EX_IntConst firstParamInt)
                        {
                            var uberGraphFunctionLabel = FormatCodeOffset((uint)firstParamInt.Value, function.ObjectName.ToString());
                            return $"{context}.{functionName}({uberGraphFunctionLabel})";
                        }
                        else
                        {
                            var isFinalFunction = function?.FunctionFlags.HasFlag(EFunctionFlags.FUNC_Final) ?? true;
                            var isClassMemberFunction = (function?.OuterIndex.IsExport() ?? false) && (function?.OuterIndex.ToExport(_asset) == _class);
                            if (!isFinalFunction && isClassMemberFunction && callContext == null)
                            {
                                context = _class.ObjectName.ToString();
                            }

                            if (string.IsNullOrWhiteSpace(parameters))
                                return $"{context}.{functionName}()";
                            else
                                return $"{context}.{functionName}({parameters})";
                        }
                    }
                case EX_LetMulticastDelegate expr:
                    {
                        // TODO: validate context
                        var variableExpression = FormatExpression(expr.VariableExpression, expr);
                        var assignmentExpression = FormatExpression(expr.AssignmentExpression, expr);
                        return $"EX_LetMulticastDelegate({variableExpression},{assignmentExpression})";
                    }
                case EX_ComputedJump expr:
                    {
                        var codeOffsetExpression = FormatExpression(expr.CodeOffsetExpression, expr);
                        return $"goto {codeOffsetExpression}";
                    }
                case EX_Jump expr:
                    {
                        var codeOffset = FormatCodeOffset(expr.CodeOffset);
                        return $"goto {codeOffset}";
                    }
                case EX_LocalVariable expr:
                    {
                        var variable = FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                        return variable;
                    }
                case EX_DefaultVariable expr:
                    {
                        var variable = FormatString(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                        return $"EX_DefaultVariable({variable})";
                    }
                case EX_InstanceVariable expr:
                    {
                        var variable = FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = _context;
                        _context = null;
                        return $"{context}.{variable}";
                    }
                case EX_LocalOutVariable expr:
                    {
                        var variable = FormatIdentifier(_asset.GetPropertyName(expr.Variable, _useFullPropertyNames));
                        return variable;
                    }
                case EX_InterfaceContext expr:
                    {
                        var interfaceValue = FormatExpression(expr.InterfaceValue, expr);
                        return $"EX_InterfaceContext({interfaceValue})";
                    }
                case EX_DeprecatedOp4A expr:
                    {
                        return $"EX_DeprecatedOp4A()";
                    }
                case EX_Nothing expr:
                    return "";
                case EX_EndOfScript expr:
                    return $"";
                case EX_IntZero expr:
                    {
                        return $"EX_IntZero()";
                    }
                case EX_IntOne expr:
                    {
                        return $"EX_IntOne()";
                    }
                case EX_True expr:
                    return "true";
                case EX_False expr:
                    return "false";
                case EX_NoObject expr:
                    {
                        return $"EX_NoObject()";
                    }
                case EX_NoInterface expr:
                    {
                        return $"EX_NoInterface()";
                    }
                case EX_Self:
                    return "this";
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
                                return $"return {FormatExpression(expr.ReturnExpression, expr)}";
                            }
                        }
                    }
                case EX_CallMath expr:
                    {
                        var functionName = FormatIdentifier(GetFunctionName(expr.StackNode));
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{functionName}()";
                        else
                            return $"{functionName}({parameters})";
                    }
                case EX_CallMulticastDelegate expr:
                    {
                        // TODO: validate context
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = context;
                        _context = null;

                        var stackNode = FormatIdentifier(GetFunctionName(expr.StackNode));
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
                        var @delegate = FormatExpression(expr.Delegate, expr);

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}.EX_CallMulticastDelegate({stackNode}, {@delegate})";
                        else
                            return $"{context}.EX_CallMulticastDelegate({stackNode}, {parameters}, {@delegate})";
                    }
                case EX_FinalFunction expr:
                    {
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = context;
                        _context = null;

                        var stackNode = GetFunctionName(expr.StackNode);
                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));

                        if (true)
                        {
                            stackNode = FormatIdentifier(stackNode);
                            if (string.IsNullOrWhiteSpace(parameters))
                                return $"{context}.{stackNode}()";
                            else
                                return $"{context}.{stackNode}({parameters})";
                        }
                        else
                        {
                            stackNode = FormatString(stackNode);
                            if (string.IsNullOrWhiteSpace(parameters))
                                return $"{context}.EX_FinalFunction({stackNode})";
                            else
                                return $"{context}.EX_FinalFunction({stackNode}, {parameters})";
                        }
                    }
                case EX_VirtualFunction expr:
                    {
                        var context = _context == null ? "this" : _context.Expression;
                        var callContext = context;
                        _context = null;

                        var parameters = string.Join(", ", expr.Parameters.Select(x => FormatExpression(x)));
                        var virtualFunctionName = FormatString(expr.VirtualFunctionName.ToString());

                        if (string.IsNullOrWhiteSpace(parameters))
                            return $"{context}.EX_VirtualFunction({virtualFunctionName})";
                        else
                            return $"{context}.EX_VirtualFunction({virtualFunctionName}, {parameters})";
                    }
                case EX_Context expr:
                    {
                        if (expr.ObjectExpression is EX_InterfaceContext subExpr)
                        {
                            _context = new Context()
                            {
                                Expression = FormatExpression(subExpr.InterfaceValue, expr),
                                Type = ContextType.Interface,
                            };
                        }
                        else
                        {
                            var @object = FormatExpression(expr.ObjectExpression,expr);
                            _context = new Context()
                            {
                                Expression = @object,
                                Type = ContextType.Default
                            };
                        }
                        var context = FormatExpression(expr.ContextExpression, expr);
                        _context = null;

                        var offset = expr.Offset;
                        var rvalue = _asset.GetPropertyName(expr.RValuePointer, _useFullPropertyNames);



                        return context;
                        //return $"{context}.{@object}";
                    }
                case EX_IntConst expr:
                    return $"{expr.Value}";
                case EX_SkipOffsetConst expr:
                    {
                        //var target = FormatCodeOffset(expr.Value);
                        var target = expr.Value; // TODO
                        return $"EX_SkipOffsetConst({target})";
                    }
                case EX_FloatConst expr:
                    return $"{expr.Value}f";
                case EX_StringConst expr:
                    return $"{FormatString(expr.Value)}";
                case EX_UnicodeStringConst expr:
                    return $"{FormatString(expr.Value)}";
                case EX_TextConst expr:
                    {
                        var type = FormatString(expr.Value.TextLiteralType.ToString());
                        switch (expr.Value.TextLiteralType)
                        {
                            case EBlueprintTextLiteralType.Empty:
                                return $"EX_TextConst({type})";
                            case EBlueprintTextLiteralType.LocalizedText:
                                {
                                    var localizedSource = FormatExpression(expr.Value.LocalizedSource);
                                    var localizedKey = FormatExpression(expr.Value.LocalizedKey);
                                    var localizedNamespace = FormatExpression(expr.Value.LocalizedNamespace);
                                    return $"EX_TextConst({type}, {localizedSource}, {localizedKey}, {localizedNamespace})";
                                }
                            case EBlueprintTextLiteralType.InvariantText:
                                {
                                    var invariantLiteralString = FormatExpression(expr.Value.InvariantLiteralString);
                                    return $"EX_TextConst({type}, {invariantLiteralString})";
                                }
                            case EBlueprintTextLiteralType.LiteralString:
                                {
                                    var literalString = FormatExpression(expr.Value.LiteralString);
                                    return $"EX_TextConst({type}, {literalString})";
                                }
                            case EBlueprintTextLiteralType.StringTableEntry:
                                {
                                    var stringTableAsset = _asset.GetName(expr.Value.StringTableAsset);
                                    var stringTableId = FormatExpression(expr.Value.StringTableId);
                                    var stringTableKey = FormatExpression(expr.Value.StringTableKey);
                                    return $"EX_TextConst({type}, {stringTableAsset}, {stringTableId}, {stringTableKey})";
                                }
                            default:
                                throw new NotImplementedException($"EX_TextConst TextLiteralType {expr.Value.TextLiteralType} not implemented");
                        }
                    }
                case EX_ObjectConst expr:
                    {
                        if (parentKismetExpression is EX_Context)
                        {
                            return FormatIdentifier(_asset.GetName(expr.Value));
                        }
                        else
                        {
                            return $"typeof({FormatIdentifier(_asset.GetName(expr.Value))})";
                        }
                    }
                case EX_SoftObjectConst expr:
                    {
                        var val = FormatExpression(expr.Value, expr);
                        return $"EX_SoftObjectConst({val})";
                    }
                case EX_NameConst expr:
                    {
                        var value = FormatString(expr.Value.ToString());
                        return $"EX_NameConst({value})";
                    }
                case EX_RotationConst expr:
                    {
                        return $"EX_RotationConst({expr.Value.Yaw}, {expr.Value.Pitch}, {expr.Value.Roll})";
                    }
                case EX_VectorConst expr:
                    {
                        return $"EX_VectorConst({expr.Value.X}, {expr.Value.Y}, {expr.Value.Z})";
                    }
                case EX_TransformConst expr:
                    {
                        var @params =
                            $"{expr.Value.Rotation.X}, {expr.Value.Rotation.Y}, {expr.Value.Rotation.Z}, {expr.Value.Rotation.W}, " +
                            $"{expr.Value.Translation.X}, {expr.Value.Translation.Y}, {expr.Value.Translation.Z}, " +
                            $"{expr.Value.Scale3D.X}, {expr.Value.Scale3D.Y}, {expr.Value.Scale3D.Z}";
                        return $"EX_TransformConst({@params})";
                    }
                case EX_StructConst expr:
                    {
                        var structName = FormatIdentifier(_asset.GetName(expr.Struct));
                        if (expr.Value?.Length > 0)
                        {
                            var members = string.Join(", ", expr.Value.Select(x => FormatExpression(x)));
                            return $"EX_StructConst({structName}, {expr.StructSize}, {members})";
                        }
                        else
                        {
                            return $"EX_StructConst({structName}, {expr.StructSize})";
                        }

                    }
                case EX_SetArray expr:
                    {
                        var prop = FormatExpression(expr.AssigningProperty, expr);
                        var elems = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        return $"{prop} = [ {elems} ]";
                    }
                case EX_ArrayConst expr:
                    {
                        var innerProperty = FormatIdentifier(_asset.GetPropertyName(expr.InnerProperty, _useFullPropertyNames));
                        var elements = string.Join(", ", expr.Elements.Select(x => FormatExpression(x)));
                        if (!string.IsNullOrWhiteSpace(elements))
                        {
                            return $"EX_ArrayConst({innerProperty}, {elements})";
                        }
                        else
                        {
                            return $"EX_ArrayConst({innerProperty})";
                        }
                    }
                case EX_ByteConst expr:
                    return $"(byte)({expr.Value})";
                case EX_IntConstByte expr:
                    return $"EX_IntConstByte({expr.Value})";
                case EX_Int64Const expr:
                    return $"EX_Int64Const({expr.Value})";
                case EX_UInt64Const expr:
                    return $"EX_UInt64Const({expr.Value})";
                case EX_FieldPathConst expr:
                    {
                        var value = FormatExpression(expr.Value, expr);
                        return $"EX_FieldPathConst({value})";
                    }
                case EX_MetaCast expr:
                    {
                        var classPtr = FormatString(_asset.GetName(expr.ClassPtr));
                        var target = FormatExpression(expr.TargetExpression, expr);
                        return $"EX_MetaCast({classPtr}, {target})";
                    }
                case EX_DynamicCast expr:
                    {
                        var classPtr = FormatString(_asset.GetName(expr.ClassPtr));
                        var target = FormatExpression(expr.TargetExpression, expr);
                        return $"EX_DynamicCast({classPtr}, {target})";
                    }
                case EX_JumpIfNot expr:
                    {
                        var booleanExpression = FormatExpression(expr.BooleanExpression, expr);
                        var codeOffset = FormatCodeOffset(expr.CodeOffset);
                        return $"if (!({booleanExpression})) goto {codeOffset}";
                    }
                case EX_Assert expr:
                    {
                        var debugMode = expr.DebugMode.ToString().ToLower();
                        var assertExpression = FormatExpression(expr.AssertExpression, expr);
                        return $"EX_Assert({expr.LineNumber}, {debugMode}, {assertExpression})";
                    }
                case EX_InstanceDelegate expr:
                    {
                        // TODO: validate context
                        var functionName = FormatString(expr.FunctionName.ToString());
                        return $"EX_InstanceDelegate({functionName})";
                    }
                case EX_AddMulticastDelegate expr:
                    {
                        // TODO: validate context
                        var delegat = FormatExpression(expr.Delegate, expr);
                        var delegateToAdd = FormatExpression(expr.DelegateToAdd, expr);
                        return $"EX_AddMulticastDelegate({delegat}, {delegateToAdd})";
                    }
                case EX_RemoveMulticastDelegate expr:
                    {
                        // TODO: validate context
                        var delegat = FormatExpression(expr.Delegate, expr);
                        var delegateToAdd = FormatExpression(expr.DelegateToAdd, expr);
                        return $"EX_RemoveMulticastDelegate({delegat}, {delegateToAdd})";
                    }
                case EX_ClearMulticastDelegate expr:
                    {
                        // TODO: validate context
                        var delegateToClear = FormatExpression(expr.DelegateToClear, expr);
                        return $"EX_ClearMulticastDelegate({delegateToClear})";
                    }
                case EX_BindDelegate expr:
                    {
                        // TODO: validate context
                        var name = FormatString(expr.FunctionName.ToString());
                        var delegat = FormatExpression(expr.Delegate, expr);
                        var objectTerm = FormatExpression(expr.ObjectTerm, expr);
                        return $"EX_BindDelegate({name}, {delegat}, {objectTerm})";
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
                        return $"EX_PopExecutionFlowIfNot({FormatExpression(expr.BooleanExpression, expr)})";
                    }
                case EX_Breakpoint expr:
                    return $"EX_Breakpoint()";
                case EX_WireTracepoint expr:
                    return $"EX_WireTracepoint()";
                case EX_InstrumentationEvent expr:
                    {
                        var eventType = expr.EventType.ToString();

                        if (expr.EventType == EScriptInstrumentationType.InlineEvent)
                        {
                            var eventName = FormatString(expr.EventName.ToString());
                            return $"EX_InstrumentationEvent({eventType}, {eventName})";
                        }
                        else
                        {
                            return $"EX_InstrumentationEvent({eventType})";
                        }
                    }
                case EX_Tracepoint expr:
                    {
                        return $"EX_Tracepoint()";
                    }
                case EX_SwitchValue expr:
                    {
                        var endGotoOffset = expr.EndGotoOffset;//FormatCodeOffset(expr.EndGotoOffset);
                        var indexTerm = FormatExpression(expr.IndexTerm, expr);
                        var defaultTerm = FormatExpression(expr.DefaultTerm, expr);
                        var result = $"EX_SwitchValue({endGotoOffset}, {indexTerm}, {defaultTerm}";
                        foreach (var @case in expr.Cases)
                        {
                            var caseIndexValueTerm = FormatExpression(@case.CaseIndexValueTerm);
                            var caseTerm = FormatExpression(@case.CaseTerm);
                            //var nextCase = FormatCodeOffset(@case.NextOffset);
                            var nextCase = @case.NextOffset;
                            result += $", {caseIndexValueTerm}, {nextCase}, {caseTerm}";
                        }
                        result += $")";
                        return result;
                    }
                case EX_ArrayGetByRef expr:
                    {
                        return $"EX_ArrayGetByRef({FormatExpression(expr.ArrayVariable, expr)}, {FormatExpression(expr.ArrayIndex, expr)})";
                    }
                default:
                    throw new NotImplementedException($"Kismet instruction {kismetExpression.Inst} not implemented");
            }
        }
    }
}
