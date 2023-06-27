using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler;

public static class KismetExpressionSizeCalculator
{
    public static void CalculateStringExpressionSize(KismetExpression expr, ref int index)
    {
        index++;
        switch (expr)
        {
            case EX_StringConst exp:
                {
                    index += exp.Value.Length + 1;
                    break;
                }
            case EX_UnicodeStringConst exp:
                {
                    index += 2 * (exp.Value.Length + 1);
                    break;
                }
            default:
                break;
        }
    }
    public static int CalculateExpressionSize(IEnumerable<KismetExpression> expressions, ObjectVersionUE5 objectVersionUE5 = 0)
        => expressions.Sum(x => CalculateExpressionSize(x, objectVersionUE5));

    public static int CalculateExpressionSize(KismetExpression expression, ObjectVersionUE5 objectVersionUE5 = 0)
    {
        var index = 0;
        CalculateExpressionSize(expression, ref index, objectVersionUE5);
        return index;
    }

    public static void CalculateExpressionSize(KismetExpression expression, ref int index, ObjectVersionUE5 objectVersionUE5 = 0)
    {
        index++;
        switch (expression)
        {
            case EX_PrimitiveCast exp:
                {
                    index++;
                    switch (exp.ConversionType)
                    {
                        case ECastToken.ObjectToInterface:
                            {
                                index += 8;
                                break;
                            }
                        default:
                            break;
                    }
                    CalculateExpressionSize(exp.Target, ref index, objectVersionUE5);
                    break;
                }
            case EX_SetSet exp:
                {
                    CalculateExpressionSize(exp.SetProperty, ref index, objectVersionUE5);
                    index += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_SetConst exp:
                {
                    index += 8;
                    index += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_SetMap exp:
                {
                    CalculateExpressionSize(exp.MapProperty, ref index, objectVersionUE5);
                    index += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        CalculateExpressionSize(exp.Elements[2 * (j - 1)], ref index, objectVersionUE5);
                        CalculateExpressionSize(exp.Elements[2 * (j - 1) + 1], ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_MapConst exp:
                {
                    index += 8;
                    index += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        CalculateExpressionSize(exp.Elements[2 * (j - 1)], ref index, objectVersionUE5);
                        CalculateExpressionSize(exp.Elements[2 * (j - 1) + 1], ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_ObjToInterfaceCast exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.Target, ref index, objectVersionUE5);
                    break;
                }
            case EX_CrossInterfaceCast exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.Target, ref index, objectVersionUE5);
                    break;
                }
            case EX_InterfaceToObjCast exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.Target, ref index, objectVersionUE5);
                    break;
                }
            case EX_Let exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.Variable, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.Expression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LetObj exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LetWeakObjPtr exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LetBool exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LetValueOnPersistentFrame exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_StructMemberContext exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.StructExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LetDelegate exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_LocalVirtualFunction exp:
                {
                    index += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_LocalFinalFunction exp:
                {
                    index += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_LetMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_ComputedJump exp:
                {
                    CalculateExpressionSize(exp.CodeOffsetExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_Jump exp:
                {
                    index += 4;
                    break;
                }
            case EX_LocalVariable exp:
                {
                    index += 8;
                    break;
                }
            case EX_DefaultVariable exp:
                {
                    index += 8;
                    break;
                }
            case EX_InstanceVariable exp:
                {
                    index += 8;
                    break;
                }
            case EX_LocalOutVariable exp:
                {
                    index += 8;
                    break;
                }
            case EX_InterfaceContext exp:
                {
                    CalculateExpressionSize(exp.InterfaceValue, ref index, objectVersionUE5);
                    break;
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
                    break;
                }
            case EX_Return exp:
                {
                    CalculateExpressionSize(exp.ReturnExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_CallMath exp:
                {
                    index += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_CallMulticastDelegate exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.Delegate, ref index, objectVersionUE5);
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_FinalFunction exp:
                {
                    index += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_VirtualFunction exp:
                {
                    index += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_Context exp:
                {
                    if (exp is EX_Context_FailSilent)
                    {
                        exp = exp as EX_Context_FailSilent;
                    }
                    else if (exp is EX_ClassContext)
                    {
                        exp = exp as EX_ClassContext;
                    }
                    else { }
                    CalculateExpressionSize(exp.ObjectExpression, ref index, objectVersionUE5);
                    index += 4;
                    index += 8;
                    CalculateExpressionSize(exp.ContextExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_IntConst exp:
                {
                    index += 4;
                    break;
                }
            case EX_SkipOffsetConst exp:
                {
                    index += 4;
                    break;
                }
            case EX_FloatConst exp:
                {
                    index += 4;
                    break;
                }
            case EX_StringConst exp:
                {
                    index += exp.Value.Length + 1;
                    break;
                }
            case EX_UnicodeStringConst exp:
                {
                    index += 2 * (exp.Value.Length + 1);
                    break;
                }
            case EX_TextConst exp:
                {
                    index++;
                    switch (exp.Value.TextLiteralType)
                    {
                        case EBlueprintTextLiteralType.Empty:
                            {
                                break;
                            }
                        case EBlueprintTextLiteralType.LocalizedText:
                            {
                                CalculateStringExpressionSize(exp.Value.LocalizedSource, ref index);
                                CalculateStringExpressionSize(exp.Value.LocalizedKey, ref index);
                                CalculateStringExpressionSize(exp.Value.LocalizedNamespace, ref index);
                                break;
                            }
                        case EBlueprintTextLiteralType.InvariantText:
                            {
                                CalculateStringExpressionSize(exp.Value.InvariantLiteralString, ref index);
                                break;
                            }
                        case EBlueprintTextLiteralType.LiteralString:
                            {
                                CalculateStringExpressionSize(exp.Value.LiteralString, ref index);
                                break;
                            }
                        case EBlueprintTextLiteralType.StringTableEntry:
                            {
                                index += 8;
                                CalculateStringExpressionSize(exp.Value.StringTableId, ref index);
                                CalculateStringExpressionSize(exp.Value.StringTableKey, ref index);
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
            case EX_ObjectConst exp:
                {
                    index += 8;
                    break;
                }
            case EX_SoftObjectConst exp:
                {
                    CalculateExpressionSize(exp.Value, ref index, objectVersionUE5);
                    break;
                }
            case EX_NameConst exp:
                {
                    index += 12;
                    break;
                }
            case EX_RotationConst exp:
                {
                    index += ((objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES) ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_VectorConst exp:
                {
                    index += ((objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES) ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_TransformConst exp:
                {
                    index += ((objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES) ? sizeof(double) : sizeof(float)) * 10;
                    break;
                }
            case EX_StructConst exp:
                {
                    index += 8;
                    index += 4;
                    int tempindex = 0;
                    foreach (KismetExpression param in exp.Value)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                        tempindex++;
                    }
                    index++;
                    break;
                }
            case EX_SetArray exp:
                {
                    CalculateExpressionSize(exp.AssigningProperty, ref index, objectVersionUE5);
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_ArrayConst exp:
                {
                    index += 8;
                    index += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref index, objectVersionUE5);
                    }
                    index++;
                    break;
                }
            case EX_ByteConst exp:
                {
                    index++;
                    break;
                }
            case EX_IntConstByte exp:
                {
                    index++;
                    break;
                }
            case EX_Int64Const exp:
                {
                    index += 8;
                    break;
                }
            case EX_UInt64Const exp:
                {
                    index += 8;
                    break;
                }
            case EX_FieldPathConst exp:
                {
                    CalculateExpressionSize(exp.Value, ref index, objectVersionUE5);
                    break;
                }
            case EX_MetaCast exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.TargetExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_DynamicCast exp:
                {
                    index += 8;
                    CalculateExpressionSize(exp.TargetExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_JumpIfNot exp:
                {
                    index += 4;
                    CalculateExpressionSize(exp.BooleanExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_Assert exp:
                {
                    index += 3;
                    CalculateExpressionSize(exp.AssertExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_InstanceDelegate exp:
                {
                    index += 12;
                    break;
                }
            case EX_AddMulticastDelegate exp:
            {
                    CalculateExpressionSize(exp.Delegate, ref index, objectVersionUE5);
                    break;
                }
            case EX_RemoveMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.Delegate, ref index, objectVersionUE5);
                    break;
                }
            case EX_ClearMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.DelegateToClear, ref index, objectVersionUE5);
                    break;
                }
            case EX_BindDelegate exp:
                {
                    index += 12;
                    CalculateExpressionSize(exp.Delegate, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.ObjectTerm, ref index, objectVersionUE5);
                    break;
                }
            case EX_PushExecutionFlow exp:
                {
                    index += 4;
                    break;
                }
            case EX_PopExecutionFlow exp:
                {
                    break;
                }
            case EX_PopExecutionFlowIfNot exp:
                {
                    CalculateExpressionSize(exp.BooleanExpression, ref index, objectVersionUE5);
                    break;
                }
            case EX_Breakpoint exp:
                {
                    break;
                }
            case EX_WireTracepoint exp:
                {
                    break;
                }
            case EX_InstrumentationEvent exp:
                {
                    index++;
                    switch (exp.EventType)
                    {
                        case EScriptInstrumentationType.Class:
                            break;
                        case EScriptInstrumentationType.ClassScope:
                            break;
                        case EScriptInstrumentationType.Instance:
                            break;
                        case EScriptInstrumentationType.Event:
                            break;
                        case EScriptInstrumentationType.InlineEvent:
                            {
                                index += 12;
                                break;
                            }
                        case EScriptInstrumentationType.ResumeEvent:
                            break;
                        case EScriptInstrumentationType.PureNodeEntry:
                            break;
                        case EScriptInstrumentationType.NodeDebugSite:
                            break;
                        case EScriptInstrumentationType.NodeEntry:
                            break;
                        case EScriptInstrumentationType.NodeExit:
                            break;
                        case EScriptInstrumentationType.PushState:
                            break;
                        case EScriptInstrumentationType.RestoreState:
                            break;
                        case EScriptInstrumentationType.ResetState:
                            break;
                        case EScriptInstrumentationType.SuspendState:
                            break;
                        case EScriptInstrumentationType.PopState:
                            break;
                        case EScriptInstrumentationType.TunnelEndOfThread:
                            break;
                        case EScriptInstrumentationType.Stop:
                            break;
                        default:
                            break;
                    }
                    break;
                }
            case EX_Tracepoint exp:
                {
                    break;
                }
            case EX_SwitchValue exp:
                {
                    index += 6;
                    CalculateExpressionSize(exp.IndexTerm, ref index, objectVersionUE5);
                    for (var j = 0; j < exp.Cases.Length; j++)
                    {
                        CalculateExpressionSize(exp.Cases[j].CaseIndexValueTerm, ref index, objectVersionUE5);
                        index += 4;
                        CalculateExpressionSize(exp.Cases[j].CaseTerm, ref index, objectVersionUE5);
                    }
                    CalculateExpressionSize(exp.DefaultTerm, ref index, objectVersionUE5);
                    break;
                }
            case EX_ArrayGetByRef exp:
                {
                    CalculateExpressionSize(exp.ArrayVariable, ref index, objectVersionUE5);
                    CalculateExpressionSize(exp.ArrayIndex, ref index, objectVersionUE5);
                    break;
                }
            default:
                {
                    break;
                }
        }
    }
}
