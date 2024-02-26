using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;
using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Compiler.Intermediate;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

namespace KismetKompiler.Library.Compiler;

public partial class KismetScriptCompiler
{
    private CompiledExpressionContext CompileIntrinsicCall(CallOperator callOperator)
    {
        var token = GetInstrinsicFunctionToken(callOperator.Identifier.Text);
        var offset = _functionContext.CodeOffset;
        switch (token)
        {
            case EExprToken.EX_LocalVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_LocalVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_InstanceVariable:
                {
                    KismetPropertyPointer variable;
                    if (Context == null)
                    {
                        // Push implicit this context for instance member access
                        PushContext(ContextType.This, _classContext.Symbol);
                        try
                        {
                            variable = GetPropertyPointer(callOperator.Arguments[0]);
                        }
                        finally
                        {
                            PopContext();
                        }
                    }
                    else
                    {
                        variable = GetPropertyPointer(callOperator.Arguments[0]);
                    }

                    return new CompiledExpressionContext(callOperator, offset, new EX_InstanceVariable()
                    {
                        Variable = variable
                    });
                }
            case EExprToken.EX_DefaultVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_DefaultVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_Return:
                return new CompiledExpressionContext(callOperator, offset, new EX_Return()
                {
                    ReturnExpression = callOperator.Arguments.Any() ?
                        CompileSubExpression(callOperator.Arguments[0]) :
                        new EX_Nothing()
                });
            case EExprToken.EX_Jump:
                {
                    return new CompiledExpressionContext(callOperator, offset, new EX_Jump(), new[] { GetLabel(callOperator.Arguments[0]) });
                }
            case EExprToken.EX_JumpIfNot:
                return new CompiledExpressionContext(callOperator, offset, new EX_JumpIfNot()
                {
                    BooleanExpression = CompileSubExpression(callOperator.Arguments[1])
                }, new[] { GetLabel(callOperator.Arguments[0]) });
            case EExprToken.EX_Assert:
                return new CompiledExpressionContext(callOperator, offset, new EX_Assert()
                {
                    LineNumber = GetUInt16(callOperator.Arguments[0]),
                    DebugMode = GetBool(callOperator.Arguments[1]),
                    AssertExpression = CompileSubExpression(callOperator.Arguments[2])
                });
            case EExprToken.EX_Nothing:
                return new CompiledExpressionContext(callOperator, offset, new EX_Nothing());
            case EExprToken.EX_Let:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_Let()
                        {
                            Value = GetPropertyPointer(callOperator.Arguments[0]),
                            Variable = CompileSubExpression(callOperator.Arguments[1]),
                            Expression = CompileSubExpression(callOperator.Arguments[2])
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_ClassContext:
                return new CompiledExpressionContext(callOperator, offset, new EX_ClassContext()
                {
                    ObjectExpression = CompileSubExpression(callOperator.Arguments[0]),
                    RValuePointer = GetPropertyPointer(callOperator.Arguments[2]),
                    ContextExpression = CompileSubExpression(callOperator.Arguments[3])
                }, new[] { GetLabel(callOperator.Arguments[1]) });
            case EExprToken.EX_MetaCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_MetaCast()
                {
                    ClassPtr = GetPackageIndex(callOperator.Arguments[0]),
                    TargetExpression = CompileSubExpression(callOperator.Arguments[1]),
                });
            case EExprToken.EX_LetBool:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetBool()
                        {
                            VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_EndParmValue:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndParmValue());
            case EExprToken.EX_EndFunctionParms:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndFunctionParms());
            case EExprToken.EX_Self:
                return new CompiledExpressionContext(callOperator, offset, new EX_Self());
            case EExprToken.EX_Skip:
                return new CompiledExpressionContext(callOperator, offset, new EX_Skip()
                {
                    SkipExpression = CompileSubExpression(callOperator.Arguments[1])
                }, new[] { GetLabel(callOperator.Arguments[0]) });
            case EExprToken.EX_Context:
                {
                    var objectExpr = CompileSubExpression(callOperator.Arguments[0]);
                    var context = GetContextForExpression(callOperator.Arguments[0].Expression);
                    PushContext(context);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_Context()
                        {
                            ObjectExpression = objectExpr,
                            RValuePointer = GetPropertyPointer(callOperator.Arguments[2]),
                            ContextExpression = CompileSubExpression(callOperator.Arguments[3]),
                        }, new[] { GetLabel(callOperator.Arguments[1]) });
                    }
                    finally
                    {
                        PopContext();
                    }
                }
            case EExprToken.EX_Context_FailSilent:
                return new CompiledExpressionContext(callOperator, offset, new EX_Context_FailSilent()
                {
                    ObjectExpression = CompileSubExpression(callOperator.Arguments[0]),
                    RValuePointer = GetPropertyPointer(callOperator.Arguments[2]),
                    ContextExpression = CompileSubExpression(callOperator.Arguments[3]),
                }, new[] { GetLabel(callOperator.Arguments[1]) });
            case EExprToken.EX_VirtualFunction:
                return new CompiledExpressionContext(callOperator, offset, new EX_VirtualFunction()
                {
                    VirtualFunctionName = GetName(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_FinalFunction:
                return new CompiledExpressionContext(callOperator, offset, new EX_FinalFunction()
                {
                    StackNode = GetPackageIndex(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_IntConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_IntConst()
                {
                    Value = GetInt32(callOperator.Arguments[0])
                });
            case EExprToken.EX_FloatConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_FloatConst()
                {
                    Value = GetFloat(callOperator.Arguments[0])
                });
            case EExprToken.EX_StringConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_StringConst()
                {
                    Value = GetString(callOperator.Arguments[0])
                });
            case EExprToken.EX_ObjectConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_ObjectConst()
                {
                    Value = GetPackageIndex(callOperator.Arguments[0])
                });
            case EExprToken.EX_NameConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_NameConst()
                {
                    Value = GetName(callOperator.Arguments[0])
                });
            case EExprToken.EX_RotationConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_RotationConst()
                {
                    Value = new()
                    {
                        Pitch = GetFloat(callOperator.Arguments[0]),
                        Yaw = GetFloat(callOperator.Arguments[1]),
                        Roll = GetFloat(callOperator.Arguments[2])
                    }
                });
            case EExprToken.EX_VectorConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_VectorConst()
                {
                    Value = new()
                    {
                        X = GetFloat(callOperator.Arguments[0]),
                        Y = GetFloat(callOperator.Arguments[1]),
                        Z = GetFloat(callOperator.Arguments[2]),
                    }
                });
            case EExprToken.EX_ByteConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_ByteConst()
                {
                    Value = GetByte(callOperator.Arguments[0])
                });
            case EExprToken.EX_IntZero:
                return new CompiledExpressionContext(callOperator, offset, new EX_IntZero());
            case EExprToken.EX_IntOne:
                return new CompiledExpressionContext(callOperator, offset, new EX_IntOne());
            case EExprToken.EX_True:
                return new CompiledExpressionContext(callOperator, offset, new EX_True());
            case EExprToken.EX_False:
                return new CompiledExpressionContext(callOperator, offset, new EX_False());
            case EExprToken.EX_TextConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_TextConst()
                {
                    Value = GetScriptText(callOperator.Arguments),
                });
            case EExprToken.EX_NoObject:
                return new CompiledExpressionContext(callOperator, offset, new EX_NoObject());
            case EExprToken.EX_TransformConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_TransformConst()
                {
                    Value = new()
                    {
                        Rotation = new()
                        {
                            X = GetFloat(callOperator.Arguments[0]),
                            Y = GetFloat(callOperator.Arguments[1]),
                            Z = GetFloat(callOperator.Arguments[2]),
                            W = GetFloat(callOperator.Arguments[3]),
                        },
                        Translation = new()
                        {
                            X = GetFloat(callOperator.Arguments[4]),
                            Y = GetFloat(callOperator.Arguments[5]),
                            Z = GetFloat(callOperator.Arguments[6]),
                        },
                        Scale3D = new()
                        {
                            X = GetFloat(callOperator.Arguments[7]),
                            Y = GetFloat(callOperator.Arguments[8]),
                            Z = GetFloat(callOperator.Arguments[9]),
                        },
                    }
                });
            case EExprToken.EX_IntConstByte:
                return new CompiledExpressionContext(callOperator, offset, new EX_IntConstByte()
                {
                    Value = GetByte(callOperator.Arguments[0]),
                });
            case EExprToken.EX_NoInterface:
                return new CompiledExpressionContext(callOperator, offset, new EX_NoInterface());
            case EExprToken.EX_DynamicCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_DynamicCast()
                {
                    ClassPtr = GetPackageIndex(callOperator.Arguments[0]),
                    TargetExpression = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_StructConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_StructConst()
                {
                    Struct = GetPackageIndex(callOperator.Arguments[0]),
                    StructSize = GetInt32(callOperator.Arguments[1]),
                    Value = callOperator.Arguments.Skip(2).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndStructConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndStructConst());
            case EExprToken.EX_SetArray:
                return new CompiledExpressionContext(callOperator, offset, new EX_SetArray()
                {
                    ArrayInnerProp = _objectVersion < ObjectVersion.VER_UE4_CHANGE_SETARRAY_BYTECODE ? GetPackageIndex(callOperator.Arguments[0]) : null,
                    AssigningProperty = _objectVersion >= ObjectVersion.VER_UE4_CHANGE_SETARRAY_BYTECODE ? CompileSubExpression(callOperator.Arguments[0]) : null,
                    Elements = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndArray:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndArray());
            case EExprToken.EX_PropertyConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_PropertyConst()
                {
                    Property = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_UnicodeStringConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_UnicodeStringConst()
                {
                    Value = GetString(callOperator.Arguments[0])
                });
            case EExprToken.EX_Int64Const:
                return new CompiledExpressionContext(callOperator, offset, new EX_Int64Const()
                {
                    Value = GetInt64(callOperator.Arguments[0])
                });
            case EExprToken.EX_UInt64Const:
                return new CompiledExpressionContext(callOperator, offset, new EX_UInt64Const()
                {
                    Value = GetUInt64(callOperator.Arguments[0])
                });
            case EExprToken.EX_PrimitiveCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_PrimitiveCast()
                {
                    ConversionType = GetEnum<ECastToken>(callOperator.Arguments[0]),
                    Target = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_SetSet:
                return new CompiledExpressionContext(callOperator, offset, new EX_SetSet()
                {
                    SetProperty = CompileSubExpression(callOperator.Arguments[0]),
                    Elements = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndSet:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndSet());
            case EExprToken.EX_SetMap:
                return new CompiledExpressionContext(callOperator, offset, new EX_SetMap()
                {
                    MapProperty = CompileSubExpression(callOperator.Arguments[0]),
                    Elements = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndMap:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndMap());
            case EExprToken.EX_SetConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_SetConst()
                {
                    InnerProperty = GetPropertyPointer(callOperator.Arguments[0]),
                    Elements = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndSetConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndSetConst());
            case EExprToken.EX_MapConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_MapConst()
                {
                    KeyProperty = GetPropertyPointer(callOperator.Arguments[0]),
                    ValueProperty = GetPropertyPointer(callOperator.Arguments[1]),
                    Elements = callOperator.Arguments.Skip(2).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndMapConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndMapConst());
            case EExprToken.EX_StructMemberContext:
                return new CompiledExpressionContext(callOperator, offset, new EX_StructMemberContext()
                {
                    StructMemberExpression = GetPropertyPointer(callOperator.Arguments[0]),
                    StructExpression = CompileSubExpression(callOperator.Arguments[1]),
                });
            case EExprToken.EX_LetMulticastDelegate:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetMulticastDelegate()
                        {
                            VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_LetDelegate:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetDelegate()
                        {
                            VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_LocalVirtualFunction:
                return new CompiledExpressionContext(callOperator, offset, new EX_LocalVirtualFunction()
                {
                    VirtualFunctionName = GetName(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_LocalFinalFunction:
                return new CompiledExpressionContext(callOperator, offset, new EX_LocalFinalFunction()
                {
                    StackNode = GetPackageIndex(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_LocalOutVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_LocalOutVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_DeprecatedOp4A:
                return new CompiledExpressionContext(callOperator, offset, new EX_DeprecatedOp4A());
            case EExprToken.EX_InstanceDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_InstanceDelegate()
                {
                    FunctionName = GetName(callOperator.Arguments[0]),
                });
            case EExprToken.EX_PushExecutionFlow:
                return new CompiledExpressionContext(callOperator, offset, new EX_PushExecutionFlow()
                {
                }, new[] { GetLabel(callOperator.Arguments[0]) });
            case EExprToken.EX_PopExecutionFlow:
                return new CompiledExpressionContext(callOperator, offset, new EX_PopExecutionFlow());
            case EExprToken.EX_ComputedJump:
                return new CompiledExpressionContext(callOperator, offset, new EX_ComputedJump()
                {
                    CodeOffsetExpression = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_PopExecutionFlowIfNot:
                return new CompiledExpressionContext(callOperator, offset, new EX_PopExecutionFlowIfNot()
                {
                    BooleanExpression = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_Breakpoint:
                return new CompiledExpressionContext(callOperator, offset, new EX_Breakpoint());
            case EExprToken.EX_InterfaceContext:
                return new CompiledExpressionContext(callOperator, offset, new EX_InterfaceContext()
                {
                    InterfaceValue = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_ObjToInterfaceCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_ObjToInterfaceCast()
                {
                    ClassPtr = GetPackageIndex(callOperator.Arguments[0]),
                    Target = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_EndOfScript:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndOfScript());
            case EExprToken.EX_CrossInterfaceCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_CrossInterfaceCast()
                {
                    ClassPtr = GetPackageIndex(callOperator.Arguments[0]),
                    Target = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_InterfaceToObjCast:
                return new CompiledExpressionContext(callOperator, offset, new EX_InterfaceToObjCast()
                {
                    ClassPtr = GetPackageIndex(callOperator.Arguments[0]),
                    Target = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_WireTracepoint:
                return new CompiledExpressionContext(callOperator, offset, new EX_WireTracepoint());
            case EExprToken.EX_SkipOffsetConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_SkipOffsetConst()
                {
                }, new[] { GetLabel(callOperator.Arguments[0]) });
            case EExprToken.EX_AddMulticastDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_AddMulticastDelegate()
                {
                    Delegate = CompileSubExpression(callOperator.Arguments[0]),
                    DelegateToAdd = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_ClearMulticastDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_ClearMulticastDelegate()
                {
                    DelegateToClear = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_Tracepoint:
                return new CompiledExpressionContext(callOperator, offset, new EX_Tracepoint());
            case EExprToken.EX_LetObj:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetObj()
                        {
                            VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_LetWeakObjPtr:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetWeakObjPtr()
                        {
                            VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_BindDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_BindDelegate()
                {
                    FunctionName = GetName(callOperator.Arguments[0]),
                    Delegate = CompileSubExpression(callOperator.Arguments[1]),
                    ObjectTerm = CompileSubExpression(callOperator.Arguments[2])
                });
            case EExprToken.EX_RemoveMulticastDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_RemoveMulticastDelegate()
                {
                    Delegate = CompileSubExpression(callOperator.Arguments[0]),
                    DelegateToAdd = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_CallMulticastDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_CallMulticastDelegate()
                {
                    StackNode = GetPackageIndex(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).TakeWhile(x => x != callOperator.Arguments.Last()).Select(CompileSubExpression).ToArray(),
                    Delegate = CompileSubExpression(callOperator.Arguments.Last())
                });
            case EExprToken.EX_LetValueOnPersistentFrame:
                {
                    TryGetPropertyPointer(callOperator.Arguments[0].Expression, out var rvalue);
                    PushRValue(rvalue);
                    try
                    {
                        return new CompiledExpressionContext(callOperator, offset, new EX_LetValueOnPersistentFrame()
                        {
                            DestinationProperty = GetPropertyPointer(callOperator.Arguments[0]),
                            AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                        });
                    }
                    finally
                    {
                        PopRValue();
                    }
                }
            case EExprToken.EX_ArrayConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_ArrayConst()
                {
                    InnerProperty = GetPropertyPointer(callOperator.Arguments[0]),
                    Elements = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_EndArrayConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_EndArrayConst());
            case EExprToken.EX_SoftObjectConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_SoftObjectConst()
                {
                    Value = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_CallMath:
                return new CompiledExpressionContext(callOperator, offset, new EX_CallMath()
                {
                    StackNode = GetPackageIndex(callOperator.Arguments[0]),
                    Parameters = callOperator.Arguments.Skip(1).Select(CompileSubExpression).ToArray()
                });
            case EExprToken.EX_SwitchValue:
                var referencedLabels = new List<LabelSymbol>()
                {
                    GetLabel(callOperator.Arguments[0])
                };

                return new CompiledExpressionContext(callOperator, offset, new EX_SwitchValue()
                {
                    IndexTerm = CompileSubExpression(callOperator.Arguments[1]),
                    DefaultTerm = CompileSubExpression(callOperator.Arguments[2]),
                    Cases = CompileSwitchCases(callOperator.Arguments.Skip(3), referencedLabels)
                }, referencedLabels.ToList());
            case EExprToken.EX_InstrumentationEvent:
                return new CompiledExpressionContext(callOperator, offset, new EX_InstrumentationEvent()
                {
                    EventType = GetEnum<EScriptInstrumentationType>(callOperator.Arguments[0]),
                    EventName = GetName(callOperator.Arguments[1])
                });
            case EExprToken.EX_ArrayGetByRef:
                return new CompiledExpressionContext(callOperator, offset, new EX_ArrayGetByRef()
                {
                    ArrayVariable = CompileSubExpression(callOperator.Arguments[0]),
                    ArrayIndex = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_ClassSparseDataVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_ClassSparseDataVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_FieldPathConst:
                return new CompiledExpressionContext(callOperator, offset, new EX_FieldPathConst()
                {
                    Value = CompileSubExpression(callOperator.Arguments[0])
                });
            default:
                throw new CompilationError(callOperator, "Invalid call to intrinsic function");
        }
    }

    private FKismetSwitchCase[] CompileSwitchCases(IEnumerable<Argument> args, List<LabelSymbol> referencedLabels)
    {
        var enumerator = args.GetEnumerator();
        var result = new List<FKismetSwitchCase>();
        while (enumerator.MoveNext())
        {
            var caseIndexValueTerm = CompileSubExpression(enumerator.Current);
            enumerator.MoveNext();

            var nextOffset = GetLabel(enumerator.Current);
            referencedLabels.Add(nextOffset);
            enumerator.MoveNext();

            var caseTerm = CompileSubExpression(enumerator.Current);

            result.Add(new(caseIndexValueTerm, 0, caseTerm));
        }
        return result.ToArray();
    }

    private FName GetName(Expression expression)
    {
        if (expression is StringLiteral stringLiteral)
        {
            return new IntermediateName(stringLiteral.Value);
        }
        else if (expression is Identifier identifier)
        {
            return new IntermediateName(identifier.Text);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private T GetEnum<T>(Argument argument)
    {
        if (argument.Expression is Identifier identifier)
        {
            return (T)System.Enum.Parse(typeof(T), identifier.Text);
        }
        else if (argument.Expression is StringLiteral stringLiteral)
        {
            return (T)System.Enum.Parse(typeof(T), stringLiteral.Value);
        }
        else if (argument.Expression is IntLiteral intLiteral)
        {
            return (T)Convert.ChangeType(intLiteral.Value, System.Enum.GetUnderlyingType(typeof(T)));
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private FName GetName(Argument argument)
    {
        return GetName(argument.Expression);
    }

    private FScriptText GetScriptText(IList<Argument> arguments)
    {
        var typeStr = GetString(arguments[0]);
        var type = System.Enum.Parse<EBlueprintTextLiteralType>(typeStr);
        switch (type)
        {
            case EBlueprintTextLiteralType.Empty:
                return new FScriptText() { TextLiteralType = type };
            case EBlueprintTextLiteralType.LocalizedText:
                {
                    var localizedSource = CompileSubExpression(arguments[1]);
                    var localizedKey = CompileSubExpression(arguments[2]);
                    var localizedNamespace = CompileSubExpression(arguments[3]);
                    return new FScriptText()
                    {
                        TextLiteralType = type,
                        LocalizedSource = localizedSource,
                        LocalizedKey = localizedKey,
                        LocalizedNamespace = localizedNamespace
                    };
                }
            case EBlueprintTextLiteralType.InvariantText:
                {
                    var invariantLiteralString = CompileSubExpression(arguments[1]);
                    return new FScriptText()
                    {
                        TextLiteralType = type,
                        InvariantLiteralString = invariantLiteralString
                    };
                }
            case EBlueprintTextLiteralType.LiteralString:
                {
                    var literalString = CompileSubExpression(arguments[1]);
                    return new FScriptText() { TextLiteralType = type, InvariantLiteralString = literalString };
                }
            case EBlueprintTextLiteralType.StringTableEntry:
                {
                    var stringTableAsset = GetPackageIndex(arguments[1]);
                    var stringTableId = CompileSubExpression(arguments[2]);
                    var stringTableKey = CompileSubExpression(arguments[3]);
                    return new FScriptText()
                    {
                        TextLiteralType = type,
                        StringTableAsset = stringTableAsset,
                        StringTableId = stringTableId,
                        StringTableKey = stringTableKey
                    };
                }
            default:
                throw new NotImplementedException($"EX_TextConst TextLiteralType {type} not implemented");
        }
    }

    private string GetString(Argument argument)
    {
        if (argument.Expression is StringLiteral stringLiteral)
        {
            return stringLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private bool GetBool(Argument argument)
    {
        if (argument.Expression is BoolLiteral boolLiteral)
        {
            return boolLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private ushort GetUInt16(Argument argument)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            // TODO check bounds
            return (ushort)intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private long GetInt64(Argument argument)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            return (long)intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private ulong GetUInt64(Argument argument)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            return (ulong)intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private int GetInt32(Argument argument)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            return intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private byte GetByte(Argument argument)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            return (byte)intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private float GetFloat(Argument argument)
    {
        if (argument.Expression is FloatLiteral floatLiteral)
        {
            return floatLiteral.Value;
        }
        else if (argument.Expression is IntLiteral intLiteral)
        {
            return intLiteral.Value;
        }
        else
        {
            throw new NotImplementedException();
        }
    }
}