using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Utilities;

public static class KismetExpressionSizeCalculator2
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

    public static void CalculateExpressionSize(KismetExpression expression, ref int codeOffset, ObjectVersionUE5 objectVersionUE5 = 0)
    {
        codeOffset++;
        switch (expression)
        {
            case EX_PrimitiveCast exp:
                {
                    codeOffset++;
                    switch (exp.ConversionType)
                    {
                        case ECastToken.ObjectToInterface:
                            {
                                codeOffset += 8;
                                break;
                            }
                        default:
                            break;
                    }
                    CalculateExpressionSize(exp.Target, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_SetSet exp:
                {
                    CalculateExpressionSize(exp.SetProperty, ref codeOffset, objectVersionUE5);
                    codeOffset += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_SetConst exp:
                {
                    codeOffset += 8;
                    codeOffset += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_SetMap exp:
                {
                    CalculateExpressionSize(exp.MapProperty, ref codeOffset, objectVersionUE5);
                    codeOffset += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        CalculateExpressionSize(exp.Elements[2 * (j - 1)], ref codeOffset, objectVersionUE5);
                        CalculateExpressionSize(exp.Elements[2 * (j - 1) + 1], ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_MapConst exp:
                {
                    codeOffset += 8;
                    codeOffset += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        CalculateExpressionSize(exp.Elements[2 * (j - 1)], ref codeOffset, objectVersionUE5);
                        CalculateExpressionSize(exp.Elements[2 * (j - 1) + 1], ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_ObjToInterfaceCast exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.Target, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_CrossInterfaceCast exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.Target, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_InterfaceToObjCast exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.Target, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_Let exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.Variable, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.Expression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LetObj exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LetWeakObjPtr exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LetBool exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LetValueOnPersistentFrame exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_StructMemberContext exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.StructExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LetDelegate exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_LocalVirtualFunction exp:
                {
                    codeOffset += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_LocalFinalFunction exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_LetMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.VariableExpression, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.AssignmentExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_ComputedJump exp:
                {
                    CalculateExpressionSize(exp.CodeOffsetExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_Jump exp:
                {
                    codeOffset += 4;
                    break;
                }
            case EX_LocalVariable exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_DefaultVariable exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_InstanceVariable exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_LocalOutVariable exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_InterfaceContext exp:
                {
                    CalculateExpressionSize(exp.InterfaceValue, ref codeOffset, objectVersionUE5);
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
                    CalculateExpressionSize(exp.ReturnExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_CallMath exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_CallMulticastDelegate exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.Delegate, ref codeOffset, objectVersionUE5);
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_FinalFunction exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_VirtualFunction exp:
                {
                    codeOffset += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
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
                    CalculateExpressionSize(exp.ObjectExpression, ref codeOffset, objectVersionUE5);
                    codeOffset += 4;
                    codeOffset += 8;
                    CalculateExpressionSize(exp.ContextExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_IntConst exp:
                {
                    codeOffset += 4;
                    break;
                }
            case EX_SkipOffsetConst exp:
                {
                    codeOffset += 4;
                    break;
                }
            case EX_FloatConst exp:
                {
                    codeOffset += 4;
                    break;
                }
            case EX_StringConst exp:
                {
                    codeOffset += exp.Value.Length + 1;
                    break;
                }
            case EX_UnicodeStringConst exp:
                {
                    codeOffset += 2 * (exp.Value.Length + 1);
                    break;
                }
            case EX_TextConst exp:
                {
                    codeOffset++;
                    switch (exp.Value.TextLiteralType)
                    {
                        case EBlueprintTextLiteralType.Empty:
                            {
                                break;
                            }
                        case EBlueprintTextLiteralType.LocalizedText:
                            {
                                CalculateStringExpressionSize(exp.Value.LocalizedSource, ref codeOffset);
                                CalculateStringExpressionSize(exp.Value.LocalizedKey, ref codeOffset);
                                CalculateStringExpressionSize(exp.Value.LocalizedNamespace, ref codeOffset);
                                break;
                            }
                        case EBlueprintTextLiteralType.InvariantText:
                            {
                                CalculateStringExpressionSize(exp.Value.InvariantLiteralString, ref codeOffset);
                                break;
                            }
                        case EBlueprintTextLiteralType.LiteralString:
                            {
                                CalculateStringExpressionSize(exp.Value.LiteralString, ref codeOffset);
                                break;
                            }
                        case EBlueprintTextLiteralType.StringTableEntry:
                            {
                                codeOffset += 8;
                                CalculateStringExpressionSize(exp.Value.StringTableId, ref codeOffset);
                                CalculateStringExpressionSize(exp.Value.StringTableKey, ref codeOffset);
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
            case EX_ObjectConst exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_SoftObjectConst exp:
                {
                    CalculateExpressionSize(exp.Value, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_NameConst exp:
                {
                    codeOffset += 12;
                    break;
                }
            case EX_RotationConst exp:
                {
                    codeOffset += (objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_VectorConst exp:
                {
                    codeOffset += (objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_TransformConst exp:
                {
                    codeOffset += (objectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 10;
                    break;
                }
            case EX_StructConst exp:
                {
                    codeOffset += 8;
                    codeOffset += 4;
                    int tempindex = 0;
                    if (exp.Value != null)
                    {
                        foreach (KismetExpression param in exp.Value)
                        {
                            CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                            tempindex++;
                        }
                    }
                    codeOffset++;
                    break;
                }
            case EX_SetArray exp:
                {
                    CalculateExpressionSize(exp.AssigningProperty, ref codeOffset, objectVersionUE5);
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_ArrayConst exp:
                {
                    codeOffset += 8;
                    codeOffset += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        CalculateExpressionSize(param, ref codeOffset, objectVersionUE5);
                    }
                    codeOffset++;
                    break;
                }
            case EX_ByteConst exp:
                {
                    codeOffset++;
                    break;
                }
            case EX_IntConstByte exp:
                {
                    codeOffset++;
                    break;
                }
            case EX_Int64Const exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_UInt64Const exp:
                {
                    codeOffset += 8;
                    break;
                }
            case EX_FieldPathConst exp:
                {
                    CalculateExpressionSize(exp.Value, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_MetaCast exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.TargetExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_DynamicCast exp:
                {
                    codeOffset += 8;
                    CalculateExpressionSize(exp.TargetExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_JumpIfNot exp:
                {
                    codeOffset += 4;
                    CalculateExpressionSize(exp.BooleanExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_Assert exp:
                {
                    codeOffset += 3;
                    CalculateExpressionSize(exp.AssertExpression, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_InstanceDelegate exp:
                {
                    codeOffset += 12;
                    break;
                }
            case EX_AddMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.Delegate, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.DelegateToAdd, ref codeOffset, objectVersionUE5); // TODO: validate
                    break;
                }
            case EX_RemoveMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.Delegate, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.DelegateToAdd, ref codeOffset, objectVersionUE5);// TODO: validate
                    break;
                }
            case EX_ClearMulticastDelegate exp:
                {
                    CalculateExpressionSize(exp.DelegateToClear, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_BindDelegate exp:
                {
                    codeOffset += 12;
                    CalculateExpressionSize(exp.Delegate, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.ObjectTerm, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_PushExecutionFlow exp:
                {
                    codeOffset += 4;
                    break;
                }
            case EX_PopExecutionFlow exp:
                {
                    break;
                }
            case EX_PopExecutionFlowIfNot exp:
                {
                    CalculateExpressionSize(exp.BooleanExpression, ref codeOffset, objectVersionUE5);
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
                    codeOffset++;
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
                                codeOffset += 12;
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
                    codeOffset += 6;
                    CalculateExpressionSize(exp.IndexTerm, ref codeOffset, objectVersionUE5);
                    for (var j = 0; j < exp.Cases.Length; j++)
                    {
                        CalculateExpressionSize(exp.Cases[j].CaseIndexValueTerm, ref codeOffset, objectVersionUE5);
                        codeOffset += 4;
                        CalculateExpressionSize(exp.Cases[j].CaseTerm, ref codeOffset, objectVersionUE5);
                    }
                    CalculateExpressionSize(exp.DefaultTerm, ref codeOffset, objectVersionUE5);
                    break;
                }
            case EX_ArrayGetByRef exp:
                {
                    CalculateExpressionSize(exp.ArrayVariable, ref codeOffset, objectVersionUE5);
                    CalculateExpressionSize(exp.ArrayIndex, ref codeOffset, objectVersionUE5);
                    break;
                }
            default:
                {
                    break;
                }
        }
    }
}
