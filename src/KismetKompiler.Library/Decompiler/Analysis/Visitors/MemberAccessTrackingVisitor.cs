using KismetKompiler.Library.Utilities;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Analysis.Visitors;

public class MemberAccessTrackingVisitor : KismetExpressionVisitor
{
    private readonly FunctionAnalysisContext _context;
    private Symbol _instance;
    private Stack<(EX_Context Context, Symbol ContextSymbol)> _contextStack = new();
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

    private Symbol GetContext(EX_Context ctx)
    {
        if (ctx.ObjectExpression is EX_InstanceVariable instanceVariable)
        {
            var prop = GetProperty(_instance, instanceVariable.Variable);
            return prop;
        }
        else if (ctx.ObjectExpression is EX_ObjectConst objectConst)
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
        else if (ctx.ObjectExpression is EX_LocalVariable localVariable)
        {
            var context = GetProperty(null, localVariable.Variable)
                ?? throw new NotImplementedException();
            return context.PropertyType ?? throw new NotImplementedException();
        }
        else if (ctx.ObjectExpression is EX_Context context)
        {
            return _expressionSymbolCache[context];
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private EX_Context? ActiveContext => _contextStack.Count == 0 ? null : _contextStack.Peek().Context;

    private Symbol ActiveContextSymbol => _contextStack.Count == 0 ? _instance : _contextStack.Peek().ContextSymbol;

    public override void Visit(KismetExpression expression, ref int codeOffset)
    {
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
                            VariableExpression = localVirtualFunction,
                            VariableSymbol = sym
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
                            VariableExpression = localFinalFunction,
                            VariableSymbol = sym
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
                            VariableExpression = instanceVariable,
                            VariableSymbol = sym
                        });
                    }
                    _expressionSymbolCache[instanceVariable] = sym;
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
                            VariableExpression = callMulticastDelegate,
                            VariableSymbol = sym
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
                            VariableExpression = callMath,
                            VariableSymbol = sym
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
                            VariableExpression = finalFunction,
                            VariableSymbol = sym
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
                            VariableExpression = finalFunction,
                            VariableSymbol = sym
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
                            VariableExpression = virtualFunction,
                            VariableSymbol = sym
                        });
                    }
                    _expressionSymbolCache[virtualFunction] = sym;

                    sym.Class = _context.Symbols.FunctionClass;
                    sym.Flags &= ~SymbolFlags.UnresolvedClass;
                    sym.FunctionMetadata.CallingConvention |= CallingConvention.VirtualFunction;
                    sym.Type = SymbolType.Function;
                }
                break;

            case EX_Context context:
                {
                    // Need to evaluate nested contexts depth-first
                    //var contextExprStack = new Stack<EX_Context>();
                    //contextExprStack.Push(context);
                    //var currentContextExpr = context;
                    //while (currentContextExpr.ObjectExpression is EX_Context subContextExpr)
                    //{
                    //    contextExprStack.Push(subContextExpr);
                    //    currentContextExpr = subContextExpr;
                    //}

                    //var contextSymbolStack = new Stack<Symbol>();
                    //while (contextExprStack.TryPop(out currentContextExpr))
                    //{

                    //}

                    Visit(context.ObjectExpression);
                    var contextSymbol = GetContext(context);
                    _contextStack.Push((context, contextSymbol));
                    Visit(context.ContextExpression);
                    _contextStack.Pop();
                    _expressionSymbolCache[context] = _expressionSymbolCache[context.ContextExpression];
                    return;
                }
        }

        // Don't visit EX_Context because we visit it manually so we can handle the 
        // context stack
        if (expression is not EX_Context)
            base.Visit(expression, ref codeOffset);
    }
}
