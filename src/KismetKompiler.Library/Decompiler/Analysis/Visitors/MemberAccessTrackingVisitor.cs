using KismetKompiler.Library.Utilities;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Analysis.Visitors;

public class MemberAccessTrackingVisitor : KismetExpressionVisitor
{
    private readonly FunctionAnalysisContext _context;
    private Symbol _instance;
    private Stack<(KismetExpression Context, Symbol ContextSymbol)> _contextStack = new();
    private Dictionary<KismetExpression, Symbol> _expressionSymbolCache = new();

    public MemberAccessTrackingVisitor(FunctionAnalysisContext context, Symbol instance)
    {
        _context = context;
        _instance = instance;
    }

    private Symbol? EnsurePropertySymbolCreated(KismetPropertyPointer pointer)
        => VisitorHelper.EnsurePropertySymbolCreated(_context, pointer);

    private Symbol GetProperty(Symbol? context, KismetPropertyPointer pointer)
    {
        if (context != null)
        {
            // FIXME: use context?
            // Limit access to within the symbol context
            return EnsurePropertySymbolCreated(pointer);
        }
        else
        {
            // Global symbol lookup
            return EnsurePropertySymbolCreated(pointer);
        }
    }

    /// <summary>
    /// Returns the symbol referenced by the expression.
    /// </summary>
    /// <param name="expr"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private Symbol GetContextSymbolForExpression(KismetExpression expr)
    {
        if (expr is EX_InstanceVariable instanceVariable)
        {
            var prop = GetProperty(_instance, instanceVariable.Variable);
            return prop;
        }
        else if (expr is EX_ObjectConst objectConst)
        {
            if (objectConst.Value.IsExport())
            {
                var context = _context.Symbols
                    .Where(x => x.ExportIndex?.Index == objectConst.Value.Index)
                    .SingleOrDefault()
                    ?? throw new NotImplementedException();
                return context;
            }
            else if (objectConst.Value.IsImport())
            {
                var context = _context.Symbols
                    .Where(x => x.ImportIndex?.Index == objectConst.Value.Index)
                    .SingleOrDefault()
                    ?? throw new NotImplementedException();
                return context;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else if (expr is EX_LocalVariable localVariable)
        {
            var context = GetProperty(null, localVariable.Variable)
                ?? throw new NotImplementedException();
            return context.ResolvedType 
                ?? throw new NotImplementedException();
        }
        else if (expr is EX_LocalOutVariable localOutVariable)
        {
            var context = GetProperty(null, localOutVariable.Variable)
                ?? throw new NotImplementedException();
            return context.ResolvedType
                ?? throw new NotImplementedException();
        }
        else if (expr is EX_Context context)
        {
            return _expressionSymbolCache[context];
        }
        else if (expr is EX_StructMemberContext structMemberContext)
        {
            return GetContextSymbolForExpression(structMemberContext.StructExpression);
        }
        else if (expr is EX_InterfaceContext interfaceContext)
        {
            return GetContextSymbolForExpression(interfaceContext.InterfaceValue);
        }
        else if (expr is EX_SwitchValue switchValue)
        {
            return GetContextSymbolForExpression(switchValue.DefaultTerm);
        }
        else if (expr is EX_ArrayGetByRef arrayGetByRef)
        {
            return GetContextSymbolForExpression(arrayGetByRef.ArrayVariable);
        }
        else if (expr is EX_Self self)
        {
            return _instance;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private Symbol GetContextForInterfaceContext(EX_InterfaceContext ctx)
    {
        return GetContextSymbolForExpression(ctx.InterfaceValue);
    }

    private KismetExpression? ActiveContext => _contextStack.Count == 0 ? null : _contextStack.Peek().Context;

    private Symbol ActiveContextSymbol => _contextStack.Count == 0 ? _instance : _contextStack.Peek().ContextSymbol;

    public override void Visit(KismetExpression expression, ref int codeOffset)
    {
        var skipBaseVisit = false;

        switch (expression)
        {
            case EX_LocalVirtualFunction localVirtualFunction:
                {
                    var sym = ActiveContextSymbol.GetMember(localVirtualFunction.VirtualFunctionName.ToString());
                    if (sym == null)
                    {
                        sym = new Symbol()
                        {
                            Name = localVirtualFunction.VirtualFunctionName.ToString(),
                            Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                            Flags = SymbolFlags.EvaluationTemporary,
                            Type = SymbolType.Function,
                        };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = localVirtualFunction,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[localVirtualFunction] = sym;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.LocalVirtualFunction;
                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.Type = SymbolType.Function;
                }
                break;

            case EX_LocalFinalFunction localFinalFunction:
                {
                    var sym = ActiveContextSymbol.GetMember(localFinalFunction.StackNode);
                    if (sym == null)
                    {
                        sym = _context.Symbols.Where(x => x.ExportIndex?.Index == localFinalFunction.StackNode.Index || x.ImportIndex?.Index == localFinalFunction.StackNode.Index)
                            .FirstOrDefault() ??
                            new Symbol()
                            {
                                Name = _context.Asset.GetName(localFinalFunction.StackNode),
                                Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                                Flags = SymbolFlags.EvaluationTemporary,
                                Type = SymbolType.Function,
                            };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = localFinalFunction,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[localFinalFunction] = sym;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.LocalFinalFunction;
                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.Type = SymbolType.Function;
                    break;
                }

            case EX_InstanceVariable instanceVariable:
                {
                    var sym = EnsurePropertySymbolCreated(instanceVariable.Variable);

                    if (!ActiveContextSymbol.HasMember(sym))
                    {
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = instanceVariable,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[instanceVariable] = sym;
                }
                break;

            case EX_Let let:
                {
                    var member = EnsurePropertySymbolCreated(let.Value);
                    if (let.Variable is EX_StructMemberContext structMemberContext)
                    {
                        skipBaseVisit = true;
                        codeOffset += 8;
                        Visit(let.Variable, ref codeOffset);
                        Visit(let.Expression, ref codeOffset);

                        var structContext = GetContextSymbolForExpression(let.Variable);
                        //if (structContext.Flags.HasFlag(SymbolFlags.UnresolvedClass))
                        //{
                        //    structContext.
                        //}

                        if (!structContext.HasMember(member))
                        {
                            _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                            {
                                ContextExpression = let.Variable,
                                ContextSymbol = structContext,
                                MemberExpression = let,
                                MemberSymbol = member
                            });
                        }
                    }
                    _expressionSymbolCache[let] = member;
                }
                break;

            case EX_CallMulticastDelegate callMulticastDelegate:
                {
                    var sym = ActiveContextSymbol.GetMember(callMulticastDelegate.StackNode);
                    if (sym == null)
                    {
                        sym = _context.Symbols.Where(x => x.ExportIndex?.Index == callMulticastDelegate.StackNode.Index || x.ImportIndex?.Index == callMulticastDelegate.StackNode.Index)
                            .FirstOrDefault() ??
                            new Symbol()
                            {
                                Name = _context.Asset.GetName(callMulticastDelegate.StackNode),
                                Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                                Flags = SymbolFlags.EvaluationTemporary,
                                Type = SymbolType.Function,
                            };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = callMulticastDelegate,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[callMulticastDelegate] = sym;

                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.CallMulticastDelegate;
                    sym.Type = SymbolType.Function;
                }
                break;

            case EX_CallMath callMath:
                {
                    var sym = _context.Symbols.Where(x => x.ExportIndex?.Index == callMath.StackNode.Index || x.ImportIndex?.Index == callMath.StackNode.Index)
                        .FirstOrDefault();
                    if (sym == null)
                    {
                        sym = new Symbol()
                        {
                            Name = _context.Asset.GetName(callMath.StackNode),
                            Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                            Flags = SymbolFlags.EvaluationTemporary,
                            Type = SymbolType.Function,
                        };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = callMath,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[callMath] = sym;

                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.CallMath;
                    sym.Type = SymbolType.Function;
                    sym.Parent!.ClassMetadata.IsStaticClass = true;

                    //// Analyse final (static) function call
                    //var functionSymbol =
                    //    _context.Symbols.Where(x =>
                    //                        x.ImportIndex?.Index == finalFunction.StackNode.Index ||
                    //                        x.ExportIndex?.Index == finalFunction.StackNode.Index)
                    //    .SingleOrDefault();

                    //// Set class to appropriate type (Function)
                    //functionSymbol.Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault();

                    //// Set function signature
                    //functionSymbol.FunctionMetadata.CallingConvention |= finalFunction switch
                    //{
                    //    EX_CallMath => CallingConvention.CallMath,
                    //    EX_CallMulticastDelegate => CallingConvention.CallMulticastDelegate,
                    //    EX_LocalFinalFunction => CallingConvention.LocalFinalFunction,
                    //    EX_FinalFunction => CallingConvention.FinalFunction,
                    //};

                    //functionSymbol.FunctionMetadata.Parameters = finalFunction.Parameters
                    //    .Select((x, i) => new Symbol()
                    //    {
                    //        // FIXME: determine better name if it all feasible
                    //        Name = $"param{i}",
                    //        // FIXME: determine actual type
                    //        Class = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault()
                    //    }).ToList();

                    //if (ParentExpression != null)
                    //{
                    //    // FIXME: determine actual type
                    //    functionSymbol.FunctionMetadata.ReturnType = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault();
                    //}
                    break;
                }

            case EX_FinalFunction finalFunction:
                {
                    var sym = _context.Symbols.Where(x => x.ExportIndex?.Index == finalFunction.StackNode.Index || x.ImportIndex?.Index == finalFunction.StackNode.Index)
                        .FirstOrDefault();
                    if (sym == null)
                    {
                        sym = new Symbol()
                        {
                            Name = _context.Asset.GetName(finalFunction.StackNode),
                            Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                            Flags = SymbolFlags.EvaluationTemporary,
                            Type = SymbolType.Function,
                        };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = finalFunction,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[finalFunction] = sym;

                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.FinalFunction;
                    sym.Type = SymbolType.Function;

                    if (ActiveContextSymbol != null &&
                        !ActiveContextSymbol.HasMember(sym.Name))
                    {
                        // Function has been called that does not exist in the active context
                        // This probably means that the function is defined in the base class,
                        // but the base class has not been properly assigned to the context class yet
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = finalFunction,
                            MemberSymbol = sym
                        });
                    }

                    //// Analyse final (static) function call
                    //var functionSymbol =
                    //    _context.Symbols.Where(x =>
                    //                        x.ImportIndex?.Index == finalFunction.StackNode.Index ||
                    //                        x.ExportIndex?.Index == finalFunction.StackNode.Index)
                    //    .SingleOrDefault();

                    //// Set class to appropriate type (Function)
                    //functionSymbol.Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault();

                    //// Set function signature
                    //functionSymbol.FunctionMetadata.CallingConvention |= finalFunction switch
                    //{
                    //    EX_CallMath => CallingConvention.CallMath,
                    //    EX_CallMulticastDelegate => CallingConvention.CallMulticastDelegate,
                    //    EX_LocalFinalFunction => CallingConvention.LocalFinalFunction,
                    //    EX_FinalFunction => CallingConvention.FinalFunction,
                    //};

                    //functionSymbol.FunctionMetadata.Parameters = finalFunction.Parameters
                    //    .Select((x, i) => new Symbol()
                    //    {
                    //        // FIXME: determine better name if it all feasible
                    //        Name = $"param{i}",
                    //        // FIXME: determine actual type
                    //        Class = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault()
                    //    }).ToList();

                    //if (ParentExpression != null)
                    //{
                    //    // FIXME: determine actual type
                    //    functionSymbol.FunctionMetadata.ReturnType = _context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault();
                    //}
                    break;
                }


            case EX_VirtualFunction virtualFunction:
                {
                    var sym = ActiveContextSymbol.GetMember(virtualFunction.VirtualFunctionName.ToString());
                    if (sym == null)
                    {
                        sym = new Symbol()
                        {
                            Name = virtualFunction.VirtualFunctionName.ToString(),
                            Class = _context.Symbols.Where(x => x.Name == "Function").FirstOrDefault(),
                            Flags = SymbolFlags.EvaluationTemporary,
                            Type = SymbolType.Function,
                            FunctionMetadata = new SymbolFunctionMetadata()
                            {
                                CallingConvention = CallingConvention.VirtualFunction
                            }
                        };
                        _context.UnexpectedMemberAccesses.Add(new MemberAccessContext()
                        {
                            ContextExpression = ActiveContext,
                            ContextSymbol = ActiveContextSymbol,
                            MemberExpression = virtualFunction,
                            MemberSymbol = sym
                        });
                    }
                    _expressionSymbolCache[virtualFunction] = sym;

                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.VirtualFunction;
                    sym.Type = SymbolType.Function;
                }
                break;

            case EX_StructMemberContext structMemberContext:
                {
                    skipBaseVisit = true;
                    Visit(structMemberContext.StructExpression);
                    var contextSymbol = GetContextSymbolForExpression(structMemberContext.StructExpression);
                    //_contextStack.Push((structMemberContext, contextSymbol));
                    var memberSymbol = EnsurePropertySymbolCreated(structMemberContext.StructMemberExpression);
                    //_contextStack.Pop();
                    if (contextSymbol.Flags.HasFlag(SymbolFlags.UnresolvedClass))
                    {
                        contextSymbol.Class = memberSymbol.Parent;
                        contextSymbol.Flags &= ~SymbolFlags.UnresolvedClass;
                    }

                    _expressionSymbolCache[structMemberContext] = memberSymbol;
                    return;
                }

            case EX_Context context:
                {
                    skipBaseVisit = true;
                    Visit(context.ObjectExpression);
                    var contextSymbol = GetContextSymbolForExpression(context.ObjectExpression);
                    _contextStack.Push((context, contextSymbol));
                    Visit(context.ContextExpression);
                    _expressionSymbolCache[context] = _expressionSymbolCache[context.ContextExpression];
                    return;
                }
        }

        if (_contextStack.Count > 0)
            _contextStack.Pop();

        if (!skipBaseVisit)
            base.Visit(expression, ref codeOffset);
    }
}
