using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library;
using KismetKompiler.Library.Compiler;
using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Library.Syntax.Statements.Expressions.Unary;
using System.Data;
using System.Diagnostics;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;
using KismetKompiler.Library.Compiler.Intermediate;

namespace KismetKompiler.Compiler;

public class ClassContext
{
    public required ClassSymbol Symbol { get; init; }
}

public partial class KismetScriptCompiler
{
    private ObjectVersion _objectVersion = 0;
    private CompilationUnit _compilationUnit;
    private ClassContext _classContext;
    private FunctionContext _functionContext;
    private bool _strictMode = true;

    private readonly Stack<KismetPropertyPointer?> _rvalueStack;
    private readonly Stack<MemberContext> _contextStack;
    private readonly Stack<Scope> _scopeStack;

    private Scope Scope => _scopeStack.Peek()!;
    private MemberContext? Context => _contextStack.Peek();
    private KismetPropertyPointer? RValue => _rvalueStack.Peek();

    public KismetScriptCompiler()
    {
        _contextStack = new();
        _contextStack.Push(null);
        _scopeStack = new();
        _scopeStack.Push(new(null, null));
        _rvalueStack = new();
        _rvalueStack.Push(null);
    }

    private void PushScope(Symbol? declaringSymbol)
        => _scopeStack.Push(new(_scopeStack.Peek(), declaringSymbol));

    private Scope PopScope()
        => _scopeStack.Pop();

    private void PushContext(ContextType type, Symbol symbol)
        => _contextStack.Push(new MemberContext() { Type = type, Symbol = symbol });

    private void PushContext(MemberContext context)
    => _contextStack.Push(context);

    private MemberContext PopContext()
        => _contextStack.Pop();

    private void PushRValue(KismetPropertyPointer rvalue)
        => _rvalueStack.Push(rvalue);

    private KismetPropertyPointer PopRValue()
        => _rvalueStack.Pop();

    private Symbol? GetSymbol(string name)
    {
        var contextSymbol = Context?.GetSymbol(name);
        var scopeSymbol = Scope.GetSymbol(name);
        if (IsDefaultClassContext())
        {
            // Implicit this context has less priority
            return scopeSymbol ?? contextSymbol;
        }
        else
        {
            return contextSymbol ?? scopeSymbol;
        }
    }

    private T? GetSymbol<T>(string name) where T : Symbol
    {
        var contextSymbol = Context?.GetSymbol<T>(name);
        var scopeSymbol = Scope.GetSymbol<T>(name);
        if (IsDefaultClassContext())
        {
            // Implicit this context has less priority
            return scopeSymbol ?? contextSymbol;
        }
        else
        {
            return contextSymbol ?? scopeSymbol;
        }
    }

    private T GetSymbol<T>(Declaration declaration) where T : Symbol
    {
        var symbol = _functionContext?.Symbol.GetSymbol<T>(declaration);
        if (symbol == null)
        {
            symbol = _classContext?.Symbol.GetSymbol<T>(declaration);
        }

        return symbol ?? throw new UnknownSymbolError(declaration.Identifier);
    }

    private bool IsDefaultClassContext()
    {
        return Context?.Type == ContextType.This &&
                    Context?.IsImplicit == true;
    }

    private Symbol GetRequiredSymbol(string name)
        => GetSymbol(name) ?? throw new UnknownSymbolError(name);

    private T GetRequiredSymbol<T>(string name) where T : Symbol
        => GetSymbol<T>(name) ?? throw new UnknownSymbolError(name);

    private void DeclareSymbol(Symbol symbol)
        => Scope.DeclareSymbol(symbol);


    public CompiledScriptContext CompileCompilationUnit(CompilationUnit compilationUnit)
    {
        _compilationUnit = compilationUnit;

        var script = new CompiledScriptContext();

        PushScope(null);
        BuildSymbolTree(script);
        CompileScript(compilationUnit, script);
        PopScope();

        return script;
    }

    private void CompileScript(CompilationUnit compilationUnit, CompiledScriptContext script)
    {
        foreach (var declaration in compilationUnit.Declarations)
        {
            if (declaration is ProcedureDeclaration procedureDeclaration)
            {
                script.Functions.Add(CompileFunction(procedureDeclaration));
            }
            else if (declaration is VariableDeclaration variableDeclaration)
            {
                script.Variables.Add(CompileProperty(variableDeclaration));
            }
            else if (declaration is ClassDeclaration classDeclaration)
            {
                script.Classes.Add(CompileClass(classDeclaration));
            }
            else
            {
                throw new UnexpectedSyntaxError(declaration);
            }
        }
    }

    private void BuildSymbolTree(CompiledScriptContext script)
    {
        void ScanCompoundStatement(CompoundStatement compoundStatement, Symbol parent, bool isExternal)
        {
            foreach (var statement in compoundStatement)
            {
                ScanStatement(statement, parent, isExternal);
            }
        }

        void ScanStatement(Statement statement, Symbol parent, bool isExternal)
        {
            if (statement is Declaration declaration)
            {
                CreateDeclarationSymbol(declaration, parent, isExternal);
            }
            else if (statement is IBlockStatement blockStatement)
            {
                foreach (var block in blockStatement.Blocks)
                    ScanCompoundStatement(block, parent, isExternal);
            }
            else
            {
                // 
            }
        }

        Symbol CreateDeclarationSymbol(Declaration declaration, Symbol? parent, bool isExternal)
        {
            var declaringPackage = (parent is PackageSymbol packageSymbol) ? packageSymbol : parent?.DeclaringPackage;
            var declaringClass = (parent is ClassSymbol classSymbol) ? classSymbol : parent?.DeclaringClass;
            var declaringProcedure = (parent is ProcedureSymbol procedureSymbol) ? procedureSymbol : parent?.DeclaringProcedure;

            if (declaration is LabelDeclaration labelDeclaration)
            {
                return new LabelSymbol(labelDeclaration)
                {
                    Name = labelDeclaration.Identifier.Text,
                    DeclaringSymbol = parent,
                    IsExternal = isExternal,
                };
            }
            else if (declaration is VariableDeclaration variableDeclaration)
            {
                return new VariableSymbol(variableDeclaration)
                {
                    Name = variableDeclaration.Identifier.Text,
                    DeclaringSymbol = parent,
                    IsExternal = isExternal,
                };
            }
            else if (declaration is ProcedureDeclaration procedureDeclaration)
            {
                var symbol = new ProcedureSymbol(procedureDeclaration)
                {
                    Name = procedureDeclaration.Identifier.Text,
                    DeclaringSymbol = parent,
                    IsExternal = isExternal,
                };
                if (procedureDeclaration.Body != null)
                {
                    ScanCompoundStatement(procedureDeclaration.Body, symbol, isExternal);
                }
                return symbol;
            }
            else if (declaration is ClassDeclaration classDeclaration)
            {
                var symbol = new ClassSymbol(classDeclaration)
                {
                    Name = classDeclaration.Identifier.Text,
                    DeclaringSymbol = parent,
                    IsExternal = isExternal,
                };
                if (classDeclaration.Declarations != null)
                {
                    foreach (var subDeclaration in classDeclaration.Declarations)
                        ScanStatement(subDeclaration, symbol, isExternal);
                }
                return symbol;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        foreach (var packageDeclaration in _compilationUnit.Imports)
        {
            var packageSymbol = new PackageSymbol(packageDeclaration)
            {
                IsExternal = true,
                Name = packageDeclaration.Identifier.Text,
                DeclaringSymbol = null,
            };
            foreach (var item in packageDeclaration.Declarations)
                CreateDeclarationSymbol(item, packageSymbol, true);

            // Declare package, classes and (static) functions as global symbols
            // but only if they're unambigious-- two imports can not have the same key
            // TODO: differenciate between static class functions and instance functions
            IEnumerable<Symbol> GetImportGlobalSymbols(Symbol symbol)
            {
                yield return symbol;
                foreach (var item in symbol.Members)
                {
                    if (item.DeclaringClass != null && item is VariableSymbol)
                    {
                        // Do not globally declare class properties
                        continue;
                    }
                    foreach (var sub in GetImportGlobalSymbols(item))
                        yield return sub;
                }
            }

            var globalSymbols = GetImportGlobalSymbols(packageSymbol);
            var distinctGlobalSymbols = globalSymbols
                .DistinctBy(x => x.Key);
            foreach (var symbol in distinctGlobalSymbols)
                DeclareSymbol(symbol);
        }

        foreach (var declaration in _compilationUnit.Declarations)
        {
            var declarationSymbol = CreateDeclarationSymbol(declaration, null, false);
            DeclareSymbol(declarationSymbol);

            // The Ubergraph function does not adhere to standard scoping rules
            // As such, all symbols defined in it will be imported into the global scope
            void DeclareUbergraphFunctionGlobalSymbols(Symbol symbol)
            {
                foreach (var item in symbol.Members)
                {
                    if (ShouldGloballyDeclareProcecureLocalSymbol(item))
                    {
                        DeclareSymbol(item);
                    }

                    DeclareUbergraphFunctionGlobalSymbols(item);
                }
            }

            DeclareUbergraphFunctionGlobalSymbols(declarationSymbol);
        }

        void ResolveSymbolReferences(Symbol symbol)
        {
            if (symbol is ClassSymbol classSymbol)
            {
                var baseClass = classSymbol.Declaration.BaseClassIdentifier;
                if (baseClass != null && classSymbol.BaseClass == null)
                {
                    var baseClassSymbol = GetSymbol(baseClass.Text);
                    if (baseClassSymbol != null)
                    {
                        classSymbol.BaseSymbol = baseClassSymbol;

                        // TODO: figure out a better solution
                        // HACK: import members from an object named Default__ClassName into ClassName
                        if (classSymbol.Name.StartsWith("Default__"))
                        {
                            foreach (var member in classSymbol.Members.ToList())
                                classSymbol.BaseClass!.DeclareSymbol(member);
                        }
                    }
                    else
                    {
                        // UserDefinedStruct, etc.
                        symbol.BaseSymbol = new ClassSymbol(null)
                        {
                            DeclaringSymbol = null,
                            IsExternal = true,
                            Name = baseClass.Text,
                        };
                    }
                }
            }
            else if (symbol is VariableSymbol variableSymbol)
            {
                var type = variableSymbol.Declaration.Type;
                if (type.IsConstructedType)
                    type = type.TypeParameter;
                variableSymbol.InnerSymbol = GetSymbol(type.Text);
            }

            foreach (var member in symbol.Members)
            {
                ResolveSymbolReferences(member);
            }
        }

        foreach (var item in Scope)
        {
            ResolveSymbolReferences(item);
        }
    }

    private static bool ShouldGloballyDeclareProcecureLocalSymbol(Symbol item)
    {
        var isUbergraphFunction = item.DeclaringProcedure?.IsUbergraphFunction ?? false;
        var isK2NodeVariable = item.Name.StartsWith("K2Node") && item.SymbolCategory == SymbolCategory.Variable;
        var isLabel = item.SymbolCategory == SymbolCategory.Label;
        var shouldDeclareSymbol = isUbergraphFunction && (isK2NodeVariable || isLabel);
        return shouldDeclareSymbol;
    }

    public CompiledClassContext CompileClass(ClassDeclaration classDeclaration)
    {
        var classSymbol = GetRequiredSymbol<ClassSymbol>(classDeclaration.Identifier.Text)!;
        var compiledBaseClass = classSymbol?.BaseClass?.Declaration != null ?
            CompileClass(classSymbol.BaseClass.Declaration) : 
            null;

        _classContext = new ClassContext()
        {
            Symbol = classSymbol
        };

        EClassFlags flags = 0;
        foreach (var attribute in classDeclaration.Attributes)
        {
            var classFlagText = $"CLASS_{attribute.Identifier.Text}";
            if (!System.Enum.TryParse<EClassFlags>(classFlagText, true, out var flag))
                throw new CompilationError(attribute, "Invalid class attribute");
            flags |= flag;
        }

        var functions = new List<CompiledFunctionContext>();
        var properties = new List<CompiledVariableContext>();

        PushContext(new MemberContext() { Type = ContextType.This, Symbol = _classContext.Symbol, IsImplicit = true });
        try
        {
            PushScope(_classContext.Symbol);
            try
            {
                DeclareSymbol(new VariableSymbol(null)
                {
                    DeclaringSymbol = _classContext.Symbol,
                    IsExternal = false,
                    Name = "this",
                    IsReadOnly = true
                });

                if (_classContext.Symbol.BaseClass != null)
                {
                    DeclareSymbol(new VariableSymbol(null)
                    {
                        DeclaringSymbol = _classContext.Symbol.BaseClass,
                        IsExternal = false,
                        Name = "base",
                        IsReadOnly = true,
                    });
                }

                foreach (var declaration in classDeclaration.Declarations)
                {
                    if (declaration is ProcedureDeclaration procedureDeclaration)
                    {
                        functions.Add(CompileFunction(procedureDeclaration));
                    }
                    else if (declaration is VariableDeclaration variableDeclaration)
                    {
                        properties.Add(CompileProperty(variableDeclaration));
                    }
                    else
                    {
                        throw new UnexpectedSyntaxError(declaration);
                    }
                }
            }
            finally
            {
                PopScope();
            }
        }
        finally
        {
            PopContext();
        }

        return new CompiledClassContext(classSymbol)
        {
            BaseClass = compiledBaseClass,
            Flags = flags,
            Functions = functions,
            Variables = properties
        };
    }

    private CompiledVariableContext CompileProperty(VariableDeclaration variableDeclaration)
    {
        var symbol = GetSymbol<VariableSymbol>(variableDeclaration);
        return new CompiledVariableContext(symbol)
        {
            Type = null, // TODO
        };
    }

    private EFunctionFlags GetFunctionFlags(ProcedureDeclaration procedureDeclaration)
    {
        EFunctionFlags functionFlags = 0;
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Public))
            functionFlags |= EFunctionFlags.FUNC_Public;
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Private))
            functionFlags |= EFunctionFlags.FUNC_Private;
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Sealed))
            functionFlags |= EFunctionFlags.FUNC_Final;
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Virtual))
            ; // Not sealed
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Protected))
            functionFlags |= EFunctionFlags.FUNC_Protected;
        if (procedureDeclaration.Modifiers.HasFlag(ProcedureModifier.Static))
            functionFlags |= EFunctionFlags.FUNC_Static;

        foreach (var attr in procedureDeclaration.Attributes)
        {
            if (attr.Identifier.Text == "Extern" ||
                attr.Identifier.Text == "UnknownSignature")
                continue;

            var flagFormat = $"FUNC_{attr.Identifier.Text}";
            if (!Enum.TryParse<EFunctionFlags>(flagFormat, out var flag))
                throw new CompilationError(attr, $"Unknown function attribute: {attr}");
            functionFlags |= flag;
        }
        return functionFlags;
    }

    public CompiledFunctionContext CompileFunction(ProcedureDeclaration procedureDeclaration)
    {
        var symbol = GetSymbol<ProcedureSymbol>(procedureDeclaration);
        _functionContext = new()
        {
            Name = procedureDeclaration.Identifier.Text,
            Declaration = procedureDeclaration,
            Symbol = symbol,
            CompiledFunctionContext = new CompiledFunctionContext(symbol)
            {
                Flags = GetFunctionFlags(procedureDeclaration)
            }
        };

        if (!procedureDeclaration.IsExternal)
        {
            _functionContext.ReturnLabel = CreateCompilerLabel("ReturnLabel");

            PushScope(_functionContext.Symbol);
            ForwardDeclareProcedureSymbols();
            if (procedureDeclaration.Body != null)
            {
                CompileCompoundStatement(procedureDeclaration.Body);
            }
            ResolveLabel(_functionContext.ReturnLabel);
            DoFixups();
            EnsureEndOfScriptPresent();

            PopScope();
        }

        _functionContext.CompiledFunctionContext.Bytecode.AddRange(_functionContext.PrimaryExpressions.SelectMany(x => x.CompiledExpressions));
        return _functionContext.CompiledFunctionContext;
    }

    private void ForwardDeclareProcedureSymbols()
    {
        foreach (var param in _functionContext.Declaration.Parameters)
        {
            var variableSymbol = new VariableSymbol(null)
            {
                Parameter = param,
                IsExternal = false,
                Name = param.Identifier.Text,
                DeclaringSymbol = _functionContext.Symbol,
            };
            _functionContext.CompiledFunctionContext.Variables.Add(new(variableSymbol));
            DeclareSymbol(variableSymbol);
        }

        foreach (var label in _functionContext.Symbol.Members)
        {
            if (label is LabelSymbol labelSymbol)
            {
                DeclareSymbol(labelSymbol);
            }
        }
    }

    private void CompileCompoundStatement(CompoundStatement compoundStatement)
    {
        PushScope(null);

        foreach (var statement in compoundStatement)
        {
            if (statement is Declaration declaration)
            {
                ProcessDeclaration(declaration);
            }
            else if (statement is Expression expression)
            {
                EmitPrimaryExpression(expression, CompileExpression(expression));
            }
            else if (statement is ReturnStatement returnStatement)
            {
                CompileReturnStatement(returnStatement);
            }
            else if (statement is GotoStatement gotoStatement)
            {
                if (gotoStatement.Label == null)
                    throw new CompilationError(gotoStatement, "Missing goto statement label");

                if (TryGetLabel(gotoStatement.Label, out var label))
                {
                    EmitPrimaryExpression(gotoStatement, new EX_Jump(), new[] { label });
                }
                else
                {
                    EmitPrimaryExpression(gotoStatement, new EX_ComputedJump()
                    {
                        CodeOffsetExpression = CompileSubExpression(gotoStatement.Label)
                    });
                }
            }
            else if (statement is IfStatement ifStatement)
            {
                if (ifStatement.Condition is LogicalNotOperator notOperator)
                {
                    if (ifStatement.Body?.FirstOrDefault() is GotoStatement ifStatementBodyGotoStatement)
                    {
                        // Match 'if (!(K2Node_SwitchInteger_CmpSuccess)) goto _674;'
                        EmitPrimaryExpression(notOperator, new EX_JumpIfNot()
                        {
                            BooleanExpression = CompileSubExpression(notOperator.Operand)
                        }, new[] { GetLabel(ifStatementBodyGotoStatement.Label) });
                    }
                    else if (ifStatement.Body?.FirstOrDefault() is ReturnStatement ifStatementReturnStatement)
                    {
                        // Match 'if (!CallFunc_BI_TempFlagCheck_retValue) return;'
                        EmitPrimaryExpression(notOperator, new EX_JumpIfNot()
                        {
                            BooleanExpression = CompileSubExpression(notOperator.Operand)
                        }, new[] { _functionContext.ReturnLabel });
                    }
                    else
                    {
                        throw new UnexpectedSyntaxError(statement);
                    }
                }
                else
                {
                    var endLabel = CreateCompilerLabel("IfStatementEndLabel");
                    if (ifStatement.ElseBody == null)
                    {
                        EmitPrimaryExpression(ifStatement, new EX_JumpIfNot()
                        {
                            BooleanExpression = CompileSubExpression(ifStatement.Condition),
                        }, new[] { endLabel });
                        CompileCompoundStatement(ifStatement.Body);

                    }
                    else
                    {
                        var elseLabel = CreateCompilerLabel("IfStatementElseLabel");
                        EmitPrimaryExpression(ifStatement, new EX_JumpIfNot()
                        {
                            BooleanExpression = CompileSubExpression(ifStatement.Condition),
                        }, new[] { elseLabel });
                        CompileCompoundStatement(ifStatement.Body);
                        EmitPrimaryExpression(null, new EX_Jump(), new[] { endLabel });
                        ResolveLabel(elseLabel);
                        CompileCompoundStatement(ifStatement.ElseBody);
                    }

                    ResolveLabel(endLabel);
                }
            }
            else
            {
                throw new UnexpectedSyntaxError(statement);
            }
        }

        PopScope();
    }

    private void CompileReturnStatement(ReturnStatement returnStatement)
    {
        var isLastStatement = _functionContext.Declaration.Body.Last() == returnStatement;
        if (isLastStatement)
        {
            // Let the fixup handle it
        }
        else
        {
            // The original compiler has a quirk where, if you return in a block, it will always jump to a label
            // containing the return & end of script instructions
            EmitPrimaryExpression(returnStatement, new EX_Jump(), new[] { _functionContext.ReturnLabel });
        }
    }

    private LabelSymbol CreateCompilerLabel(string name)
    {
        return new LabelSymbol(null)
        {
            CodeOffset = null,
            IsResolved = false,
            Name = name,
            IsExternal = false,
            DeclaringSymbol = _functionContext.Symbol,
        };
    }

    private void ResolveLabel(LabelSymbol labelInfo)
    {
        labelInfo.IsResolved = true;
        labelInfo.CodeOffset = _functionContext.CodeOffset;
    }

    private void ProcessDeclaration(Declaration declaration)
    {
        if (declaration is LabelDeclaration labelDeclaration)
        {
            var label = GetSymbol<LabelSymbol>(labelDeclaration);
            label.CodeOffset = _functionContext.CodeOffset;
            label.IsResolved = true;

            _functionContext.CompiledFunctionContext.Labels.Add(new(label)
            {
                CodeOffset = label.CodeOffset.Value,
            });
        }
        else if (declaration is VariableDeclaration variableDeclaration)
        {
            var variableSymbol = GetSymbol<VariableSymbol>(variableDeclaration);
            DeclareSymbol(variableSymbol);

            if (variableDeclaration.Initializer != null)
            {
                EmitPrimaryExpression(variableDeclaration, new EX_Let()
                {
                    Value = GetPropertyPointer(variableDeclaration.Identifier),
                    Variable = CompileSubExpression(variableDeclaration.Identifier),
                    Expression = CompileSubExpression(variableDeclaration.Initializer)
                });
            }

            _functionContext.CompiledFunctionContext.Variables.Add(new(variableSymbol));
        }
        else
        {
            throw new UnexpectedSyntaxError(declaration);
        }
    }

    private void EnsureEndOfScriptPresent()
    {
        var beforeLastExpr = _functionContext.PrimaryExpressions
            .Skip(_functionContext.PrimaryExpressions.Count - 1)
            .FirstOrDefault();
        var lastExpr = _functionContext.PrimaryExpressions.LastOrDefault();

        if (beforeLastExpr?.CompiledExpressions.Single() is not EX_Return &&
            lastExpr?.CompiledExpressions.Single() is not EX_Return)
        {
            var returnVar = _functionContext.Declaration.Parameters.FirstOrDefault(x => x.Attributes.Any(y => y.Identifier.Text == "Return"));
            if (returnVar != null)
            {
                EmitPrimaryExpression(null, new EX_Return()
                {
                    ReturnExpression = Emit(null, new EX_LocalOutVariable()
                    {
                        Variable = GetPropertyPointer(returnVar.Identifier)
                    }).CompiledExpressions.Single(),
                });
            }
            else
            {
                EmitPrimaryExpression(null, new EX_Return()
                {
                    ReturnExpression = Emit(null, new EX_Nothing()).CompiledExpressions.Single(),
                });
            }
        }

        if (lastExpr?.CompiledExpressions.Single() is not EX_EndOfScript)
        {
            EmitPrimaryExpression(null, new EX_EndOfScript());
        }
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, CompiledExpressionContext expressionState)
    {
        _functionContext.AllExpressions.Add(expressionState);
        _functionContext.PrimaryExpressions.Add(expressionState);
        _functionContext.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpressions);
        return expressionState;
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelSymbol>? referencedLabels = null)
    {
        var expressionState = Emit(syntaxNode, expression, referencedLabels);
        _functionContext.AllExpressions.Add(expressionState);
        _functionContext.PrimaryExpressions.Add(expressionState);
        _functionContext.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpressions);
        return expressionState;
    }

    private void DoFixups()
    {
        foreach (var expression in _functionContext.AllExpressions)
        {
            foreach (var compiledExpression in expression.CompiledExpressions)
            {
                switch (compiledExpression)
                {
                    case EX_Jump compiledExpr:
                        compiledExpr.CodeOffset = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        break;
                    case EX_JumpIfNot compiledExpr:
                        compiledExpr.CodeOffset = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        break;
                    case EX_ClassContext compiledExpr:
                        compiledExpr.Offset = (uint)KismetExpressionSizeCalculator.CalculateExpressionSize(compiledExpr.ContextExpression);
                        break;
                    case EX_Skip compiledExpr:
                        compiledExpr.CodeOffset = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        break;
                    // EX_Context_FailSilent
                    case EX_Context compiledExpr:
                        compiledExpr.Offset = (uint)KismetExpressionSizeCalculator.CalculateExpressionSize(compiledExpr.ContextExpression);
                        break;
                    case EX_PushExecutionFlow compiledExpr:
                        compiledExpr.PushingAddress = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        break;
                    case EX_SkipOffsetConst compiledExpr:
                        compiledExpr.Value = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        break;
                    case EX_SwitchValue compiledExpr:
                        compiledExpr.EndGotoOffset = (uint)expression.ReferencedLabels[0].CodeOffset.Value;
                        for (int i = 0; i < compiledExpr.Cases.Length; i++)
                        {
                            compiledExpr.Cases[i].NextOffset = (uint)expression.ReferencedLabels[i + 1].CodeOffset.Value;
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    private CompiledExpressionContext CompileCallOperator(CallOperator callOperator)
    {
        // Reset context so it doesn't keep propagating until another member access pops up
        // TODO: solve this properly by isolating the context to the member part of the expression only (without its sub expressions)
        var callContext = Context;
        PushContext(new MemberContext() { Type = ContextType.This, Symbol = _classContext.Symbol, IsImplicit = true });
        try
        {
            if (IsIntrinsicFunction(callOperator.Identifier.Text))
            {
                // Hardcoded intrinsic function call
                return CompileIntrinsicCall(callOperator);
            }
            else
            {
                var procedureSymbol = GetSymbol<ProcedureSymbol>(callOperator.Identifier.Text);
                if (procedureSymbol == null)
                {
                    return Emit(callOperator, new EX_LocalVirtualFunction()
                    {
                        VirtualFunctionName = GetName(callOperator.Identifier),
                        Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                    });
                }
                else
                {
                    if (callContext != null)
                    {
                        if (callContext.Type == ContextType.This ||
                            callContext.Type == ContextType.Base)
                        {
                            if (callContext.SymbolExists(procedureSymbol.Name, procedureSymbol.SymbolCategory))
                            {
                                // Procedure is local to class
                                if (procedureSymbol.IsVirtual && !callContext.CallVirtualFunctionAsFinal)
                                {
                                    return Emit(callOperator, new EX_LocalVirtualFunction()
                                    {
                                        VirtualFunctionName = GetName(callOperator.Identifier),
                                        Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                    });
                                }
                                else
                                {
                                    return Emit(callOperator, new EX_LocalFinalFunction()
                                    {
                                        StackNode = GetPackageIndex(callOperator.Identifier),
                                        Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                    });
                                }
                            }
                            else
                            {
                                // Static function
                                return Emit(callOperator, new EX_CallMath()
                                {
                                    StackNode = GetPackageIndex(callOperator.Identifier),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                        }
                        else if (callContext.Type == ContextType.ObjectConst ||
                                callContext.Type == ContextType.Class)
                        {
                            if (procedureSymbol.IsVirtual)
                            {
                                return Emit(callOperator, new EX_VirtualFunction()
                                {
                                    VirtualFunctionName = GetName(callOperator.Identifier),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                            else
                            {
                                return Emit(callOperator, new EX_FinalFunction()
                                {
                                    StackNode = GetPackageIndex(callOperator.Identifier),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
        }
        finally
        {
            PopContext();
        }
    }

    private CompiledExpressionContext CompileExpression(Expression expression)
    {
        CompiledExpressionContext CompileExpressionInner()
        {
            if (expression is CallOperator callOperator)
            {
                return CompileCallOperator(callOperator);
            }
            else if (expression is Identifier identifier)
            {
                return CompileIdentifierExpression(identifier);
            }
            else if (expression is Literal literal)
            {
                return CompileLiteralExpression(literal);
            }
            else if (expression is AssignmentOperator assignmentOperator)
            {
                // TODO find a better solution, EX_Context needs this in Expression
                TryGetPropertyPointer(assignmentOperator.Left, out var rvalue);

                PushRValue(rvalue);
                try
                {
                    if (assignmentOperator.Right.ExpressionValueKind == ValueKind.Bool)
                    {
                        return Emit(expression, new EX_LetBool()
                        {
                            VariableExpression = CompileSubExpression(assignmentOperator.Left),
                            AssignmentExpression = CompileSubExpression(assignmentOperator.Right),
                        });
                    }
                    else if (assignmentOperator.Right is InitializerList initializerList)
                    {
                        return Emit(expression, new EX_SetArray()
                        {
                            ArrayInnerProp = GetPackageIndex(assignmentOperator.Left),
                            AssigningProperty = CompileSubExpression(assignmentOperator.Left),
                            Elements = initializerList.Expressions.Select(x => CompileSubExpression(x)).ToArray()
                        });
                    }
                    else
                    {
                        TryGetPropertyPointer(assignmentOperator.Left, out var pointer);
                        return Emit(expression, new EX_Let()
                        {
                            Value = pointer ?? new KismetPropertyPointer() { Old = new() },
                            Variable = CompileSubExpression(assignmentOperator.Left),
                            Expression = CompileSubExpression(assignmentOperator.Right),
                        });
                    }
                }
                finally
                {
                    PopRValue();
                }
            }
            else if (expression is CastOperator castOperator)
            {
                return CompileCastExpression(castOperator);
            }
            else if (expression is MemberExpression memberExpression)
            {
                return CompileMemberExpression(memberExpression);
            }
            else
            {
                throw new UnexpectedSyntaxError(expression);
            }
        }

        var expressionContext = CompileExpressionInner();
        _functionContext.AllExpressions.Add(expressionContext);
        foreach (var compiledExpression in expressionContext.CompiledExpressions)
        {
            _functionContext.ExpressionContextLookup[compiledExpression] = expressionContext;
        }
        return expressionContext;
    }

    private CompiledExpressionContext CompileIdentifierExpression(Identifier identifier)
    {
        var symbol = GetRequiredSymbol(identifier.Text);
        if (symbol is VariableSymbol variableSymbol)
        {
            if (variableSymbol.VariableCategory == VariableCategory.This ||
                variableSymbol.VariableCategory == VariableCategory.Base)
            {
                return Emit(identifier, new EX_Self());
            }
            else if (variableSymbol.VariableCategory == VariableCategory.Local)
            {
                if (variableSymbol.IsOutParameter)
                {
                    return Emit(identifier, new EX_LocalOutVariable()
                    {
                        Variable = GetPropertyPointer(identifier)
                    });
                }
                else
                {
                    return Emit(identifier, new EX_LocalVariable()
                    {
                        Variable = GetPropertyPointer(identifier)
                    });
                }
            }
            else if (variableSymbol.VariableCategory == VariableCategory.Instance)
            {
                return Emit(identifier, new EX_InstanceVariable()
                {
                    Variable = GetPropertyPointer(identifier)
                });
            }
            else if (variableSymbol.VariableCategory == VariableCategory.Global)
            {
                return Emit(identifier, new EX_ObjectConst()
                {
                    Value = GetPackageIndex(identifier)
                });
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else if (symbol is LabelSymbol labelSymbol)
        {
            // TODO resolve label later if possible
            return Emit(identifier, new EX_IntConst()
            {
                Value = labelSymbol.CodeOffset.Value
            });
        }
        else if (symbol is ClassSymbol classSymbol)
        {
            return Emit(identifier, new EX_ObjectConst()
            {
                Value = GetPackageIndex(identifier)
            });
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileCastExpression(CastOperator castOperator)
    {
        if (castOperator.Operand is Literal literal)
        {
            if (literal is IntLiteral intLiteral)
            {
                switch (castOperator.TypeIdentifier.ValueKind)
                {
                    case ValueKind.Byte:
                        return Emit(intLiteral, new EX_ByteConst() { Value = (byte)intLiteral.Value });
                    case ValueKind.Bool:
                        return Emit(intLiteral, intLiteral.Value > 0 ? new EX_True() : new EX_False());
                    case ValueKind.Int:
                        return Emit(intLiteral, new EX_IntConst() { Value = intLiteral.Value });
                    case ValueKind.Float:
                        return Emit(intLiteral, new EX_FloatConst() { Value = intLiteral.Value });
                    default:
                        throw new UnexpectedSyntaxError(literal);
                }
            }
            else if (literal is BoolLiteral boolLiteral)
            {
                switch (castOperator.TypeIdentifier.ValueKind)
                {
                    case ValueKind.Bool:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_True() : new EX_False());
                    case ValueKind.Int:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_IntConst() { Value = 1 } : new EX_IntConst() { Value = 0 });
                    case ValueKind.Float:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_FloatConst() { Value = 1 } : new EX_FloatConst() { Value = 0 });
                    case ValueKind.Byte:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_ByteConst() { Value = 1 } : new EX_ByteConst() { Value = 0 });
                    default:
                        throw new UnexpectedSyntaxError(literal);
                }
            }
            else
            {
                throw new UnexpectedSyntaxError(literal);
            }
        }
        else
        {
            return CompileExpression(castOperator.Operand);
        }
    }

    private CompiledExpressionContext CompileLiteralExpression(Literal literal)
    {
        if (literal is StringLiteral stringLiteral)
        {
            var isUnicode = stringLiteral.Value.Any(x => ((int)x) > 127);
            if (isUnicode)
            {
                return Emit(literal, new EX_UnicodeStringConst()
                {
                    Value = stringLiteral.Value,
                });
            }
            else
            {
                return Emit(literal, new EX_StringConst()
                {
                    Value = stringLiteral.Value,
                });
            }
        }
        else if (literal is IntLiteral intLiteral)
        {
            return Emit(literal, new EX_IntConst() { Value = intLiteral.Value });
        }
        else if (literal is FloatLiteral floatLiteral)
        {
            return Emit(literal, new EX_FloatConst() { Value = floatLiteral.Value });
        }
        else if (literal is BoolLiteral boolLiteral)
        {
            return Emit(literal, boolLiteral.Value ?
                new EX_True() : new EX_False());
        }
        else
        {
            throw new UnexpectedSyntaxError(literal);
        }
    }

    private Symbol GetVariableTypeSymbol(VariableSymbol variableSymbol)
    {
        if (variableSymbol.Declaration != null)
        {
            if (variableSymbol.Declaration.Type.IsConstructedType)
            {
                var symbol = GetSymbol(variableSymbol.Declaration.Type.TypeParameter!);
                if (symbol is VariableSymbol typeVariableSymbol)
                    return GetVariableTypeSymbol(typeVariableSymbol);
                return symbol!;
            }
            else
            {
                var symbol = GetSymbol(variableSymbol.Declaration.Type);
                if (symbol is VariableSymbol typeVariableSymbol)
                    return GetVariableTypeSymbol(typeVariableSymbol);
                return symbol!;
            }
        }
        else
        {

            throw new NotImplementedException();
        }
    }

    private MemberContext GetContextForMemberExpression(MemberExpression memberExpression)
    {
        var contextSymbol = GetSymbol(memberExpression.Context);
        var contextSymbolTemp = contextSymbol;
        var contextType = ContextType.Class;
        if (contextSymbol is VariableSymbol variableSymbol)
        {
            if (variableSymbol.VariableCategory == VariableCategory.This)
            {
                return new MemberContext()
                {
                    Symbol = variableSymbol.DeclaringSymbol,
                    Type = ContextType.This
                };
            }
            else if (variableSymbol.VariableCategory == VariableCategory.Base)
            {
                return new MemberContext()
                {
                    Symbol = variableSymbol.DeclaringSymbol,
                    Type = ContextType.Base
                };
            }

            if (variableSymbol.Declaration != null)
            {
                if (variableSymbol.Declaration.Type.IsConstructedType)
                {
                    contextType = variableSymbol.Declaration.Type.Text switch
                    {
                        "Struct" => ContextType.Struct,
                        "Interface" => ContextType.Interface,
                        "Class" => ContextType.Class,
                        _ => throw new NotImplementedException()
                    };
                }
                else
                {
                    if (variableSymbol.IsExternal)
                    {
                        contextType = ContextType.ObjectConst;
                    }
                }

                contextSymbol = GetVariableTypeSymbol(variableSymbol);

                // TODO check base type
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else if (contextSymbol is ClassSymbol classSymbol)
        {
            if (classSymbol == _classContext.Symbol)
            {
                contextType = ContextType.This;
            }
            else if (classSymbol == _classContext.Symbol.BaseSymbol)
            {
                contextType = ContextType.Base;
            }
            else
            {
                contextType = ContextType.Class;
            }
        }

        if (contextSymbol == null)
        {
            // TODO fix this in the decompiler
            // Fuzzy match
            if (!_strictMode)
            {
                var memberIdentifier = GetIdentifier(memberExpression.Member);
                contextSymbol = Scope
                     .Where(x => x is ClassSymbol)
                     .SelectMany(x => x.Members)
                     .Where(x => x.Name == memberIdentifier.Text)
                     .Select(x => x.DeclaringClass)
                     .Single();
            }
        }

        Debug.Assert(contextSymbol is ClassSymbol);

        var context = new MemberContext()
        {
            Symbol = contextSymbol,
            Type = contextType,
        };

        if (memberExpression.Context is Identifier contextIdentifier &&
            contextIdentifier.Text == _classContext.Symbol.Name)
        {
            // TODO: make more flexible
            // Explicit virtual method call
            context.CallVirtualFunctionAsFinal = true;
        }

        return context;
    }

    private Identifier GetIdentifier(Expression expression)
    {
        if (expression is Identifier identifier)
            return identifier;
        if (expression is CallOperator callOperator)
            return callOperator.Identifier;
        throw new NotImplementedException();
    }

    private CompiledExpressionContext CompileMemberExpression(MemberExpression memberExpression)
    {
        PushContext(GetContextForMemberExpression(memberExpression));
        try
        {
            Debug.Assert(Context != null);

            TryGetPropertyPointer(memberExpression.Member, out var pointer);
            pointer ??= RValue;

            if (Context.Type == ContextType.This ||
                Context.Type == ContextType.Base)
            {
                // These are handled through different opcodes rather than context
                return CompileExpression(memberExpression.Member);
            }
            else if (Context.Type == ContextType.Interface)
            {
                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = Emit(memberExpression.Context, new EX_InterfaceContext()
                    {
                        InterfaceValue = CompileSubExpression(memberExpression.Context)
                    }).CompiledExpressions.Single(),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = new() },
                }); ;
            }
            else if (Context.Type == ContextType.Struct)
            {
                return Emit(memberExpression, new EX_StructMemberContext()
                {
                    StructExpression = CompileSubExpression(memberExpression.Context),
                    StructMemberExpression = GetPropertyPointer(memberExpression.Member)
                });
            }
            else if (Context.Type == ContextType.ObjectConst)
            {
                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = Emit(memberExpression.Context, new EX_ObjectConst()
                    {
                        Value = GetPackageIndex(memberExpression.Context)
                    }).CompiledExpressions.Single(),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = new() },
                }); ;
            }
            else
            {
                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = CompileSubExpression(memberExpression.Context),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = new() },
                });
            }
        }
        finally
        {
            PopContext();
        }
    }

    private FPackageIndex GetPackageIndex(Expression expression)
    {
        if (expression is StringLiteral stringLiteral)
        {
            return GetPackageIndex(stringLiteral.Value);
        }
        else if (expression is Identifier identifier)
        {
            return GetPackageIndex(identifier.Text);
        }
        else if (expression is MemberExpression memberExpression)
        {
            PushContext(GetContextForMemberExpression(memberExpression));
            try
            {
                return GetPackageIndex(memberExpression.Member);
            }
            finally
            {
                PopContext();
            }
        }
        else if (expression is CallOperator callOperator)
        {
            if (callOperator.Identifier.Text == "EX_ArrayGetByRef")
            {
                var arrayObject = callOperator.Arguments.First();
                return GetPackageIndex(arrayObject);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private FPackageIndex? GetPackageIndex(string name)
    {
        // TODO fix
        if (name == "<null>")
            return new FPackageIndex();

        var symbol = GetRequiredSymbol(name);
        return new IntermediatePackageIndex(symbol);
    }

    private FPackageIndex GetPackageIndex(Argument argument)
        => GetPackageIndex(argument.Expression);

    private bool TryGetPropertyPointer(Expression expression, out KismetPropertyPointer pointer)
    {
        pointer = null;

        if (expression is StringLiteral stringLiteral)
        {
            pointer = GetPropertyPointer(stringLiteral.Value);
        }
        else if (expression is Identifier identifier)
        {
            pointer = GetPropertyPointer(identifier.Text);
        }
        else if (expression is CallOperator callOperator)
        {
            if (IsIntrinsicFunction(callOperator.Identifier.Text))
            {
                var token = GetInstrinsicFunctionToken(callOperator.Identifier.Text);
                if (token == EExprToken.EX_LocalVariable ||
                    token == EExprToken.EX_InstanceVariable ||
                    token == EExprToken.EX_LocalOutVariable)
                {
                    pointer = GetPropertyPointer(callOperator.Arguments[0]);
                }
                else if (token == EExprToken.EX_StructMemberContext)
                {
                    pointer = GetPropertyPointer(callOperator.Arguments[0]);
                }
            }
        }
        else if (expression is MemberExpression memberAccessExpression)
        {
            var context = GetContextForMemberExpression(memberAccessExpression);
            PushContext(context);
            try
            {
                pointer = GetPropertyPointer(memberAccessExpression.Member);
            }
            finally
            {
                PopContext();
            }
        }

        return pointer != null;
    }

    private KismetPropertyPointer GetPropertyPointer(Expression expression)
    {
        if (!TryGetPropertyPointer(expression, out var pointer))
            throw new UnexpectedSyntaxError(expression);
        return pointer;
    }

    private KismetPropertyPointer GetPropertyPointer(string name)
    {
        var symbol = GetRequiredSymbol(name);
        return new IntermediatePropertyPointer(symbol);
    }

    private KismetPropertyPointer GetPropertyPointer(Argument argument)
    {
        return GetPropertyPointer(argument.Expression);
    }

    private KismetExpression CompileSubExpression(Expression right)
    {
        return CompileExpression(right).CompiledExpressions.Single();
    }

    private Symbol? GetSymbol(Expression expression)
    {
        if (expression is TypeIdentifier typeIdentifier)
        {
            if (typeIdentifier.IsConstructedType)
            {
                // TODO handle base type info (Struct<>, Array<>, etc)
                return GetSymbol(typeIdentifier.TypeParameter!);
            }
            else
            {
                return GetSymbol(typeIdentifier.Text);
            }
        }
        else if (expression is Identifier identifier)
        {
            return GetSymbol(identifier.Text);
        }
        else if (expression is MemberExpression memberExpression)
        {
            PushContext(GetContextForMemberExpression(memberExpression));
            try
            {
                return GetSymbol(memberExpression.Member);
            }
            finally
            {
                PopContext();
            }
        }
        else if (expression is CallOperator callOperator)
        {
            if (callOperator.Identifier.Text == "EX_ArrayGetByRef")
            {
                // 0: Array, 1: Index
                var arrayObject = callOperator.Arguments.First();
                return GetSymbol(arrayObject.Expression);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext Emit(SyntaxNode syntaxNode, KismetExpression expression, KismetExpression expression2, IEnumerable<LabelSymbol>? referencedLabels = null)
    {
        return new CompiledExpressionContext()
        {
            SyntaxNode = syntaxNode,
            CodeOffset = _functionContext.CodeOffset,
            CompiledExpressions = new() { expression, expression2 },
            ReferencedLabels = referencedLabels?.ToList() ?? new()
        };
    }

    private CompiledExpressionContext Emit(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelSymbol>? referencedLabels = null)
    {
        return new CompiledExpressionContext()
        {
            SyntaxNode = syntaxNode,
            CodeOffset = _functionContext.CodeOffset,
            CompiledExpressions = new() { expression },
            ReferencedLabels = referencedLabels?.ToList() ?? new()
        };
    }

    private bool TryGetLabel(Expression expression, out LabelSymbol label)
    {
        if (expression is IntLiteral literal)
        {
            // TODO fix properly
            label = new LabelSymbol(null)
            {
                DeclaringSymbol = null,
                IsExternal = false,
                Name = $"_{literal.Value}",
                CodeOffset = literal.Value,
                IsResolved = true,
            };
            return true;
        }
        else if (expression is Identifier identifier)
        {
            label = GetSymbol<LabelSymbol>(identifier.Text);
            if (label != null)
            {
                return true;
            }
        }

        label = null;
        return false;
    }

    private LabelSymbol GetLabel(Expression expression)
    {
        if (!TryGetLabel(expression, out var label))
            throw new NotImplementedException();
        return label;
    }

    private LabelSymbol GetLabel(Argument argument)
    {
        return GetLabel(argument.Expression);
    }

    private LabelSymbol GetLabel(string name)
    {
        var label = GetSymbol<LabelSymbol>(name);
        return label;
    }

    private uint? GetCodeOffset(Argument argument)
    {
        if (!TryGetCodeOffset(argument, out var codeOffset))
            return null;

        return codeOffset;
    }

    private bool TryGetCodeOffset(Argument argument, out uint codeOffset)
    {
        if (argument.Expression is IntLiteral intLiteral)
        {
            codeOffset = (uint)intLiteral.Value;
            return true;
        }
        else if (argument.Expression is Identifier identifier)
        {
            var label = GetLabel(identifier.Text);
            if (!label.IsResolved)
            {
                codeOffset = 0;
                return false;
            }
            else
            {
                codeOffset = (uint)label.CodeOffset;
                return true;
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private KismetExpression CompileSubExpression(Argument argument)
    {
        var expressionState = CompileExpression(argument.Expression);
        return expressionState.CompiledExpressions.Single();
    }

    private bool IsIntrinsicFunction(string name)
        => typeof(EExprToken).GetEnumNames().Contains(name);

    private EExprToken GetInstrinsicFunctionToken(string name)
        => (EExprToken)System.Enum.Parse(typeof(EExprToken), name);
}
