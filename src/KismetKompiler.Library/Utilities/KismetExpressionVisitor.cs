using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;
using System.CodeDom.Compiler;
using static KismetKompiler.Library.Utilities.KismetExpressionVisitor;

namespace KismetKompiler.Library.Utilities;

public abstract class KismetExpressionVisitor : KismetExpressionVisitor<VisitorContext>
{
    public class VisitorContext { }
}

public abstract class KismetExpressionVisitor<T>
{
    private Stack<KismetExpression> _parentStack = new();

    public ObjectVersionUE5 ObjectVersionUE5 { get; init; } = ObjectVersionUE5.UNKNOWN;

    public KismetExpression? ParentExpression
        => _parentStack.Count == 0 ? null : _parentStack.Peek();

    private static void CalculateStringExpressionSize(KismetExpression expr, ref int index)
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
                throw new ArgumentException("Invalid expression type for calculating string size", nameof(expr));
        }
    }

    protected virtual void OnEnter(KismetExpressionContext<T> context)
    {
        _parentStack.Push(context.Expression);
    }

    protected virtual void OnExit(KismetExpressionContext<T> context)
    {
        _parentStack.Pop();
    }

    public int Visit(KismetExpression expression)
    {
        var codeOffset = 0;
        Visit(expression, ref codeOffset);
        return codeOffset;
    }

    public int Visit(IEnumerable<KismetExpression> expressions)
    {
        var codeOffset = 0;
        foreach (var expression in expressions)
        {
            Visit(expression, ref codeOffset);
        }
        return codeOffset;
    }

    public virtual void Visit(KismetExpression expression, ref int codeOffset)
    {
        var codeStartOffset = codeOffset;
        var ctx = new KismetExpressionContext<T>(expression, codeOffset, default);
        OnEnter(ctx);

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
                    Visit(exp.Target, ref codeOffset);
                    break;
                }
            case EX_SetSet exp:
                {
                    Visit(exp.SetProperty, ref codeOffset);
                    codeOffset += 4;
                    foreach (KismetExpression param in exp.Elements)
                    {
                        Visit(param, ref codeOffset);
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
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_SetMap exp:
                {
                    Visit(exp.MapProperty, ref codeOffset);
                    codeOffset += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        Visit(exp.Elements[2 * (j - 1)], ref codeOffset);
                        Visit(exp.Elements[2 * (j - 1) + 1], ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_MapConst exp:
                {
                    codeOffset += 8;
                    codeOffset += 8;
                    codeOffset += 4;
                    for (var j = 1; j <= exp.Elements.Length / 2; j++)
                    {
                        Visit(exp.Elements[2 * (j - 1)], ref codeOffset);
                        Visit(exp.Elements[2 * (j - 1) + 1], ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_ObjToInterfaceCast exp:
                {
                    codeOffset += 8;
                    Visit(exp.Target, ref codeOffset);
                    break;
                }
            case EX_CrossInterfaceCast exp:
                {
                    codeOffset += 8;
                    Visit(exp.Target, ref codeOffset);
                    break;
                }
            case EX_InterfaceToObjCast exp:
                {
                    codeOffset += 8;
                    Visit(exp.Target, ref codeOffset);
                    break;
                }
            case EX_Let exp:
                {
                    codeOffset += 8;
                    Visit(exp.Variable, ref codeOffset);
                    Visit(exp.Expression, ref codeOffset);
                    break;
                }
            case EX_LetObj exp:
                {
                    Visit(exp.VariableExpression, ref codeOffset);
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_LetWeakObjPtr exp:
                {
                    Visit(exp.VariableExpression, ref codeOffset);
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_LetBool exp:
                {
                    Visit(exp.VariableExpression, ref codeOffset);
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_LetValueOnPersistentFrame exp:
                {
                    codeOffset += 8;
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_StructMemberContext exp:
                {
                    codeOffset += 8;
                    Visit(exp.StructExpression, ref codeOffset);
                    break;
                }
            case EX_LetDelegate exp:
                {
                    Visit(exp.VariableExpression, ref codeOffset);
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_LocalVirtualFunction exp:
                {
                    codeOffset += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_LocalFinalFunction exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_LetMulticastDelegate exp:
                {
                    Visit(exp.VariableExpression, ref codeOffset);
                    Visit(exp.AssignmentExpression, ref codeOffset);
                    break;
                }
            case EX_ComputedJump exp:
                {
                    Visit(exp.CodeOffsetExpression, ref codeOffset);
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
                    Visit(exp.InterfaceValue, ref codeOffset);
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
                    Visit(exp.ReturnExpression, ref codeOffset);
                    break;
                }
            case EX_CallMath exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_CallMulticastDelegate exp:
                {
                    codeOffset += 8;
                    Visit(exp.Delegate, ref codeOffset);
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_FinalFunction exp:
                {
                    codeOffset += 8;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
                    }
                    codeOffset++;
                    break;
                }
            case EX_VirtualFunction exp:
                {
                    codeOffset += 12;
                    foreach (KismetExpression param in exp.Parameters)
                    {
                        Visit(param, ref codeOffset);
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
                    Visit(exp.ObjectExpression, ref codeOffset);
                    codeOffset += 4;
                    codeOffset += 8;
                    Visit(exp.ContextExpression, ref codeOffset);
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
                    Visit(exp.Value, ref codeOffset);
                    break;
                }
            case EX_NameConst exp:
                {
                    codeOffset += 12;
                    break;
                }
            case EX_RotationConst exp:
                {
                    codeOffset += (ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_VectorConst exp:
                {
                    codeOffset += (ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 3;
                    break;
                }
            case EX_TransformConst exp:
                {
                    codeOffset += (ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? sizeof(double) : sizeof(float)) * 10;
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
                            Visit(param, ref codeOffset);
                            tempindex++;
                        }
                    }
                    codeOffset++;
                    break;
                }
            case EX_SetArray exp:
                {
                    // TODO
                    // if (reader.Asset.ObjectVersion >= ObjectVersion.VER_UE4_CHANGE_SETARRAY_BYTECODE)
                    if (exp.AssigningProperty != null)
                    {
                        Visit(exp.AssigningProperty, ref codeOffset);
                    }
                    else
                    {
                        codeOffset += 8;
                    }
                    foreach (KismetExpression param in exp.Elements)
                    {
                        Visit(param, ref codeOffset);
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
                        Visit(param, ref codeOffset);
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
                    Visit(exp.Value, ref codeOffset);
                    break;
                }
            case EX_MetaCast exp:
                {
                    codeOffset += 8;
                    Visit(exp.TargetExpression, ref codeOffset);
                    break;
                }
            case EX_DynamicCast exp:
                {
                    codeOffset += 8;
                    Visit(exp.TargetExpression, ref codeOffset);
                    break;
                }
            case EX_JumpIfNot exp:
                {
                    codeOffset += 4;
                    Visit(exp.BooleanExpression, ref codeOffset);
                    break;
                }
            case EX_Assert exp:
                {
                    codeOffset += 3;
                    Visit(exp.AssertExpression, ref codeOffset);
                    break;
                }
            case EX_InstanceDelegate exp:
                {
                    codeOffset += 12;
                    break;
                }
            case EX_AddMulticastDelegate exp:
                {
                    Visit(exp.Delegate, ref codeOffset);
                    Visit(exp.DelegateToAdd, ref codeOffset); // TODO: validate
                    break;
                }
            case EX_RemoveMulticastDelegate exp:
                {
                    Visit(exp.Delegate, ref codeOffset);
                    Visit(exp.DelegateToAdd, ref codeOffset);// TODO: validate
                    break;
                }
            case EX_ClearMulticastDelegate exp:
                {
                    Visit(exp.DelegateToClear, ref codeOffset);
                    break;
                }
            case EX_BindDelegate exp:
                {
                    codeOffset += 12;
                    Visit(exp.Delegate, ref codeOffset);
                    Visit(exp.ObjectTerm, ref codeOffset);
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
                    Visit(exp.BooleanExpression, ref codeOffset);
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
                    Visit(exp.IndexTerm, ref codeOffset);
                    for (var j = 0; j < exp.Cases.Length; j++)
                    {
                        Visit(exp.Cases[j].CaseIndexValueTerm, ref codeOffset);
                        codeOffset += 4;
                        Visit(exp.Cases[j].CaseTerm, ref codeOffset);
                    }
                    Visit(exp.DefaultTerm, ref codeOffset);
                    break;
                }
            case EX_ArrayGetByRef exp:
                {
                    Visit(exp.ArrayVariable, ref codeOffset);
                    Visit(exp.ArrayIndex, ref codeOffset);
                    break;
                }
            default:
                {
                    break;
                }
        }

        var codeEndOffset = codeOffset;
        ctx.CodeEndOffset = codeEndOffset;
        OnExit(ctx);
    }
}

public static class KismetExpressionEnumerableExtensions
{
    private class Visitor : KismetExpressionVisitor<object>
    {
        private List<KismetExpression> _expressions = new();
        public IReadOnlyList<KismetExpression> Expressions => _expressions;

        protected override void OnEnter(KismetExpressionContext<object> context)
        {
            _expressions.Add(context.Expression);
            base.OnEnter(context);
        }
    }

    public static IEnumerable<KismetExpression> Flatten(this KismetExpression enumerable)
    {
        var visitor = new Visitor();
        visitor.Visit(enumerable);
        return visitor.Expressions;
    }

    public static IEnumerable<KismetExpression> Flatten(this IEnumerable<KismetExpression> enumerable)
    {
        var visitor = new Visitor();
        visitor.Visit(enumerable);
        return visitor.Expressions;
    }
}

public static class KismetExpressionSizeCalculator
{
    private class KismetExpressionSizeCalculatorVisitor : KismetExpressionVisitor<object> { }

    public static int CalculateExpressionSize(IEnumerable<KismetExpression> expressions, ObjectVersionUE5 objectVersionUE5 = 0)
        => expressions.Sum(x => CalculateExpressionSize(x, objectVersionUE5));

    public static int CalculateExpressionSize(KismetExpression expression, ObjectVersionUE5 objectVersionUE5 = 0)
    {
        var visitor = new KismetExpressionSizeCalculatorVisitor() { ObjectVersionUE5 = objectVersionUE5 };
        return visitor.Visit(expression);
    }
}

public static class KismetExpressionPrinter
{
    private class Visitor : KismetExpressionVisitor<object>
    {
        private IndentedTextWriter _writer;

        public Visitor(TextWriter writer)
        {
            _writer = new(writer);
        }

        protected override void OnEnter(KismetExpressionContext<object> context)
        {
            _writer.WriteLine(context.Expression.Inst);
            _writer.Indent++;
        }

        protected override void OnExit(KismetExpressionContext<object> context)
        {
            _writer.Indent--;
        }
    }

    public static void Print(IEnumerable<KismetExpression> expressions)
    {
        foreach (var item in expressions)
        {
            Print(item);
        }
    }

    public static void Print(KismetExpression expression)
    {
        var visitor = new Visitor(Console.Out);
        visitor.Visit(expression);
    }
}