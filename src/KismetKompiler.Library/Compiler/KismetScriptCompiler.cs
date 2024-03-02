using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Compiler.Context;
using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Library.Syntax.Statements.Expressions.Unary;
using System.Data;
using System.Diagnostics;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;
using KismetKompiler.Library.Compiler.Intermediate;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Library.Utilities;

namespace KismetKompiler.Library.Compiler;

public partial class KismetScriptCompiler
{
    private ObjectVersion _objectVersion = 0;
    private CompilationUnit _compilationUnit;
    private ClassContext _classContext;
    private FunctionContext _functionContext;

    private readonly Stack<KismetPropertyPointer?> _rvalueStack;
    private readonly Stack<MemberContext> _contextStack;
    private readonly Stack<Scope> _scopeStack;
    private readonly Scope _rootScope;
    private Scope _blockScope;

    private Scope RootScope => _rootScope;
    private Scope BlockScope => _blockScope;
    private Scope CurrentScope => _scopeStack.Peek()!;
    private MemberContext? Context => _contextStack.Peek();
    private KismetPropertyPointer? RValue => _rvalueStack.Peek();

    public bool StrictMode { get; set; }

    public KismetScriptCompiler()
    {
        _contextStack = new();
        _contextStack.Push(null);
        _scopeStack = new();
        _rootScope = new(null, null);
        _scopeStack.Push(_rootScope);
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

    private Symbol? GetSymbol(string name, MemberContext? context = default)
    {
        Symbol? GetSymbolInternal(string name)
        {
            var contextSymbol = context?.GetSymbol(name);
            var scopeSymbol = CurrentScope.GetSymbol(name);
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

        context ??= Context;
        var symbol = GetSymbolInternal(name);
        return symbol;
    }

    private T? GetSymbol<T>(string name, MemberContext? context = default) where T : Symbol
    {
        context ??= Context;
        var contextSymbol = context?.GetSymbol<T>(name);
        var scopeSymbol = CurrentScope.GetSymbol<T>(name);
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
            symbol = _classContext?.Symbol.GetSymbol<T>(declaration);
        if (symbol == null)
            symbol = CurrentScope.GetSymbol<T>(declaration);

        return symbol ?? throw new UnknownSymbolError(declaration.Identifier);
    }

    private bool IsDefaultClassContext()
    {
        return Context?.Type == ContextType.This &&
                    Context?.IsImplicit == true;
    }

    private Symbol GetRequiredSymbol(string name, MemberContext? context = default)
    {
        if (!StrictMode)
        {
            var symbol = GetSymbol(name, context);

            if (symbol == null)
            {
                symbol = CreateFakeSymbol(name, context);
            }

            return symbol;
        }
        else
        {
            return GetSymbol(name, context) ?? throw new UnknownSymbolError(name);
        }
    }

    private Symbol CreateFakeSymbol(string name, MemberContext? context)
    {
        Symbol? symbol;
        var candidates = CurrentScope
                             .SelectMany(x => x.Members)
                             .Where(x => x.Name == name);
        symbol = candidates.FirstOrDefault();
        if (symbol == null)
        {
            symbol = new UnknownSymbol()
            {
                DeclaringSymbol = context?.Symbol ?? CurrentScope.DeclaringSymbol,
                IsExternal = false,
                Name = name,
            };
        }

        return symbol;
    }

    private T GetRequiredSymbol<T>(string name, MemberContext? context = default) where T : Symbol
    {
        return GetSymbol<T>(name, context) ?? throw new UnknownSymbolError(name);
    }

    private void DeclareSymbol(Symbol symbol)
        => CurrentScope.DeclareSymbol(symbol);


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
        foreach (var declaration in compilationUnit.Declarations
            .Where(x => !x.Attributes.Any(x => x.Identifier.Text == "Import")))
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
            else if (declaration is EnumDeclaration enumDeclaration)
            {
                //
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
            else if (statement is ForStatement forStatement)
            {
                ScanStatement(forStatement.Initializer, parent, isExternal);
                if (forStatement.Body != null)
                    ScanCompoundStatement(forStatement.Body, parent, isExternal);
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
                    Flags = GetFunctionFlags(procedureDeclaration),
                    CustomFlags = GetExtendedFunctionFlags(procedureDeclaration)             
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
            else if (declaration is EnumDeclaration enumDeclaration)
            {
                var symbol = new EnumSymbol(enumDeclaration)
                {
                    Name = enumDeclaration.Identifier.Text,
                    DeclaringSymbol = parent,
                    IsExternal = isExternal,
                };
                var lastValue = 0;
                foreach (var enumValueDeclaration in enumDeclaration.Values)
                {
                    var value = (enumValueDeclaration.Value as IntLiteral)?.Value ?? lastValue + 1;
                    lastValue = value;

                    var valueSymbol = new EnumValueSymbol(enumValueDeclaration)
                    {
                        Name = enumValueDeclaration.Identifier.Text,
                        Value = value,
                        DeclaringSymbol = symbol,
                        IsExternal = isExternal,
                    };
                }
                return symbol;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        // Find imported packages, and group the declarations that are imported from it
        var packageImports = new List<(string PackagePath, List<Declaration> Declarations)>();
        foreach (var decl in _compilationUnit.Declarations)
        {
            var importAttrib = decl.Attributes.FirstOrDefault(x => x.Identifier.Text == "Import");
            if (importAttrib != null)
            {
                if (importAttrib.Arguments.Count != 1)
                    throw new UnexpectedSyntaxError(importAttrib);
                var packagePath = importAttrib.Arguments[0].Expression as StringLiteral;
                if (packagePath == null) 
                    throw new UnexpectedSyntaxError(importAttrib.Arguments[0]);
                var importedDeclarations = packageImports.Where(x => x.PackagePath == packagePath).FirstOrDefault().Declarations;
                if (importedDeclarations != null)
                    importedDeclarations.Add(decl);
                else
                    packageImports.Add((packagePath, new() { decl }));
            }
        }

        foreach ((var packagePath, var declarations) in packageImports)
        {
            var packageSymbol = new PackageSymbol()
            {
                IsExternal = true,
                Name = packagePath,
                DeclaringSymbol = null,
            };
            foreach (var item in declarations)
                CreateDeclarationSymbol(item, packageSymbol, true);

            // Declare package, classes and (static) functions as global symbols
            // but only if they're unambigious-- two imports can not have the same key
            // TODO: differenciate between static class functions and instance functions
            IEnumerable<Symbol> GetImportGlobalSymbols(Symbol symbol)
            {
                yield return symbol;
                foreach (var item in symbol.Members)
                {
                    if (item.DeclaringClass != null)
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

        foreach (var declaration in _compilationUnit.Declarations
            .Except(packageImports.SelectMany(x => x.Declarations)))
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
                    if (baseClassSymbol != null && baseClassSymbol != classSymbol)
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

        foreach (var item in CurrentScope)
        {
            ResolveSymbolReferences(item);
        }
    }

    private static bool ShouldGloballyDeclareProcecureLocalSymbol(Symbol item)
    {
        var isUbergraphFunction = item.DeclaringProcedure?.HasAnyFunctionFlags(EFunctionFlags.FUNC_UbergraphFunction) ?? false;
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
            if (attribute.Identifier.Text == "Import")
                continue;

            var classFlagText = $"CLASS_{attribute.Identifier.Text}";
            if (!System.Enum.TryParse<EClassFlags>(classFlagText, true, out var flag))
                throw new CompilationError(attribute, "Invalid class attribute");
            flags |= flag;
        }

        var functions = new List<CompiledFunctionContext>();
        var properties = new List<CompiledVariableContext>();

        //PushContext(new MemberContext() { Type = ContextType.This, Symbol = _classContext.Symbol, IsImplicit = true });
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
            //PopContext();
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
            var flagFormat = $"FUNC_{attr.Identifier.Text}";
            if (!Enum.TryParse<EFunctionFlags>(flagFormat, out var flag))
                continue;
            functionFlags |= flag;
        }
        return functionFlags;
    }

    private FunctionCustomFlags GetExtendedFunctionFlags(ProcedureDeclaration procedureDeclaration)
    {
        FunctionCustomFlags functionFlags = 0;
        foreach (var attr in procedureDeclaration.Attributes)
        {
            var flagFormat = $"{attr.Identifier.Text}";
            if (!Enum.TryParse<FunctionCustomFlags>(attr.Identifier.Text, out var flag))
                continue;
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
        };

        if (!procedureDeclaration.IsExternal)
        {
            _functionContext.ReturnLabel = CreateCompilerLabel("ReturnLabel");
            PushScope(_functionContext.Symbol);
            ForwardDeclareProcedureSymbols();

            var returnVar = _functionContext.Declaration.Parameters.FirstOrDefault(x => x.Attributes.Any(y => y.Identifier.Text == "Return" || y.Identifier.Text == "ReturnParm"));
            if (returnVar != null)
                _functionContext.ReturnVariable = GetRequiredSymbol<VariableSymbol>(returnVar.Identifier.Text);
            else if (_functionContext.Declaration.ReturnType.ValueKind != ValueKind.Void)
            {
                const string returnVariableName = "<>__ReturnValue";
                var variableDeclaration = new VariableDeclaration()
                {
                    Identifier = new(returnVariableName),
                    Type = _functionContext.Declaration.ReturnType,
                };

                var variableSymbol = new VariableSymbol(variableDeclaration)
                {
                    IsExternal = false,
                    Name = returnVariableName,
                    DeclaringSymbol = _functionContext.Symbol,
                    IsReturnParameter = true,
                };
                _functionContext.CompiledFunctionContext.Variables.Add(new(variableSymbol));
                _functionContext.ReturnVariable = variableSymbol;
                DeclareSymbol(variableSymbol);
            }

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
            var variableDeclaration = new VariableDeclaration()
            {
                Identifier = param.Identifier,
                Type = param.Type
            };
            var variableSymbol = new VariableSymbol(variableDeclaration)
            {
                Parameter = param,
                IsExternal = false,
                Name = param.Identifier.Text,
                DeclaringSymbol = _functionContext.Symbol,
                IsReturnParameter = param.Attributes.Any(x => x.Identifier.Text == "Return")
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
        _blockScope = CurrentScope;

        foreach (var statement in compoundStatement)
        {
            CompileStatement(statement);
        }

        _blockScope = null;
        PopScope();
    }

    private void CompileStatement(Statement statement)
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
            CompileGotoStatement(gotoStatement);
        }
        else if (statement is IfStatement ifStatement)
        {
            CompileIfStatement(statement, ifStatement);
        }
        else if (statement is WhileStatement whileStatement)
        {
            CompileWhileStatement(whileStatement);
        }
        else if (statement is ForStatement forStatement)
        {
            CompileForStatement(forStatement);
        }
        else if (statement is SwitchStatement switchStatement)
        {
            CompileSwitchStatement(switchStatement);
        }
        else if (statement is BreakStatement breakStatement)
        {
            CompileBreakStatement(breakStatement);
        }
        else if (statement is ContinueStatement continueStatement)
        {
            CompileContinueStatement(continueStatement);
        }
        else
        {
            throw new UnexpectedSyntaxError(statement);
        }
    }

    private void CompileContinueStatement(ContinueStatement continueStatement)
    {
        if (CurrentScope.ContinueLabel == null)
            throw new CompilationError(continueStatement, "continue is not valid in this context");

        EmitPrimaryExpression(continueStatement, new EX_Jump(), new[] { CurrentScope.ContinueLabel });
    }

    private void CompileBreakStatement(BreakStatement breakStatement)
    {
        if (CurrentScope.BreakLabel == null)
            throw new CompilationError(breakStatement, "break is not valid in this context");

        if (CurrentScope.IsExecutionFlow.GetValueOrDefault(false))
        {
            EmitPrimaryExpression(breakStatement, new EX_PopExecutionFlow());
        }
        else
        {
            EmitPrimaryExpression(breakStatement, new EX_Jump(), new[] { CurrentScope.BreakLabel });
        }
    }

    private void CompileSwitchStatement(SwitchStatement switchStatement)
    {
        PushScope(null);
        try
        {
            var defaultLabel = switchStatement.Labels.SingleOrDefault(x => x is DefaultSwitchLabel);
            if (switchStatement.Labels.Last() != defaultLabel)
            {
                switchStatement.Labels.Remove(defaultLabel);
                switchStatement.Labels.Add(defaultLabel);
            }

            // Set up switch labels in the context for gotos
            CurrentScope.SwitchLabels = switchStatement.Labels
                                                .Where(x => x is ConditionSwitchLabel)
                                                .Select(x => ((ConditionSwitchLabel)x).Condition)
                                                .ToDictionary(x => x, y => CreateCompilerLabel("SwitchConditionCaseBody"));

            var conditionCaseBodyLabels = CurrentScope.SwitchLabels.Values.ToList();

            var defaultCaseBodyLabel = defaultLabel != null ? CreateCompilerLabel("SwitchDefaultCaseBody") : null;
            CurrentScope.SwitchLabels.Add(new NullExpression(), defaultCaseBodyLabel);

            var switchEndLabel = CreateCompilerLabel("SwitchStatementEndLabel");
            for (var i = 0; i < switchStatement.Labels.Count; i++)
            {
                var label = switchStatement.Labels[i];
                if (label is ConditionSwitchLabel conditionLabel)
                {
                    // Jump to next label if condition is not met
                    var nextSwitchCaseLabel = CreateCompilerLabel("SwitchStatementNextLabel");
                    EmitPrimaryExpression(conditionLabel, new EX_JumpIfNot()
                    {
                        BooleanExpression = CompileSubExpression(
                            new EqualityOperator() 
                            {
                                Left = switchStatement.SwitchOn,
                                Right = conditionLabel.Condition,
                                ExpressionValueKind = ValueKind.Bool,
                            }
                        )
                    }, new[] { nextSwitchCaseLabel });

                    var labelBodyLabel = CurrentScope.SwitchLabels[conditionLabel.Condition];
                    ResolveLabel(labelBodyLabel);

                    // Emit body
                    CurrentScope.BreakLabel = switchEndLabel;
                    foreach (var statement in label.Body)
                        CompileStatement(statement);

                    // Jump to end of switch
                    EmitPrimaryExpression(conditionLabel, new EX_Jump(), new[] { switchEndLabel });
                    ResolveLabel(nextSwitchCaseLabel);
                }
            }

            if (defaultLabel != null)
            {
                // Emit body of default case first
                CurrentScope.BreakLabel = switchEndLabel;

                // Resolve label that jumps to the default case body
                ResolveLabel(defaultCaseBodyLabel);

                // Emit default case body
                foreach (var statement in defaultLabel.Body)
                    CompileStatement(statement);
            }

            ResolveLabel(switchEndLabel);
        }
        finally
        {
            PopScope();
        }
    }

    private void CompileGotoStatement(GotoStatement gotoStatement)
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

    private void CompileIfStatement(Statement statement, IfStatement ifStatement)
    {
        // Match 'if (!(K2Node_SwitchInteger_CmpSuccess)) goto _674;'
        if (ifStatement.Condition is LogicalNotOperator notOperator &&
            ifStatement.Body?.FirstOrDefault() is GotoStatement ifStatementBodyGotoStatement)
        {
            EmitPrimaryExpression(ifStatement, new EX_JumpIfNot()
            {
                BooleanExpression = CompileSubExpression(notOperator.Operand)
            }, new[] { GetLabel(ifStatementBodyGotoStatement.Label) });
        }
        // Match 'if (!CallFunc_BI_TempFlagCheck_retValue) return;'
        else if (ifStatement.Condition is LogicalNotOperator notOperator2 &&
            ifStatement.Body?.FirstOrDefault() is ReturnStatement)
        {
            EmitPrimaryExpression(ifStatement, new EX_JumpIfNot()
            {
                BooleanExpression = CompileSubExpression(notOperator2.Operand)
            }, new[] { _functionContext.ReturnLabel });
        }
        // Match 'if (!CallFunc_BI_TempFlagCheck_retValue) break;'
        else if (ifStatement.Condition is LogicalNotOperator notOperator3 &&
            ifStatement.Body?.FirstOrDefault() is BreakStatement &&
            CurrentScope.IsExecutionFlow.GetValueOrDefault(false))
        {
            EmitPrimaryExpression(ifStatement, new EX_PopExecutionFlowIfNot()
            {
                BooleanExpression = CompileSubExpression(notOperator3.Operand)
            });
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

    private void CompileWhileStatement(WhileStatement whileStatement)
    {
        PushScope(null);
        try
        {
            // Condition
            if (whileStatement.Condition is BoolLiteral boolLiteral &&
                boolLiteral.Value)
            {
                var endLabel = CreateCompilerLabel("WhileStatement_EndLabel");
                var conditionLabel = CreateCompilerLabel("WhileStatement_ConditionLabel");

                ResolveLabel(conditionLabel);
                EmitPrimaryExpression(whileStatement, new EX_PushExecutionFlow(), new[] { endLabel });

                // Body
                if (whileStatement.Body != null)
                {
                    CurrentScope.BreakLabel = endLabel;
                    CurrentScope.ContinueLabel = conditionLabel;
                    CurrentScope.IsExecutionFlow = true;
                    CompileCompoundStatement(whileStatement.Body);
                }

                // Jump to condition
                //EmitPrimaryExpression(whileStatement, new EX_Jump(), new[] { conditionLabel });

                // End
                ResolveLabel(endLabel);
            }
            else
            {
                var endLabel = CreateCompilerLabel("WhileStatement_EndLabel");
                var conditionLabel = CreateCompilerLabel("WhileStatement_ConditionLabel");

                ResolveLabel(conditionLabel);
                EmitPrimaryExpression(whileStatement, new EX_JumpIfNot()
                {
                    BooleanExpression = CompileSubExpression(whileStatement.Condition),
                }, new[] { endLabel });

                // Body
                if (whileStatement.Body != null)
                {
                    CurrentScope.BreakLabel = endLabel;
                    CurrentScope.ContinueLabel = conditionLabel;
                    CompileCompoundStatement(whileStatement.Body);
                }

                // Jump to condition
                EmitPrimaryExpression(whileStatement, new EX_Jump(), new[] { conditionLabel });

                // End
                ResolveLabel(endLabel);
            }
        }
        finally
        {
            PopScope();
        }
    }

    private void CompileForStatement(ForStatement forStatement)
    {
        var endLabel = CreateCompilerLabel("ForStatement_EndLabel");
        var conditionLabel = CreateCompilerLabel("ForStatement_ConditionLabel");

        PushScope(null);
        try
        {
            // Initialize i
            CompileStatement(forStatement.Initializer);

            // Condition
            ResolveLabel(conditionLabel);
            EmitPrimaryExpression(forStatement, new EX_JumpIfNot()
            {
                BooleanExpression = CompileSubExpression(forStatement.Condition),
            }, new[] { endLabel });

            // Body
            if (forStatement.Body != null)
            {
                CurrentScope.BreakLabel = endLabel;
                CurrentScope.ContinueLabel = conditionLabel;
                CompileCompoundStatement(forStatement.Body);
            }

            // Increment & jump to condition
            CompileExpression(forStatement.AfterLoop);
            EmitPrimaryExpression(forStatement, new EX_Jump(), new[] { conditionLabel });

            // End
            ResolveLabel(endLabel);
        }
        finally
        {
            PopScope();
        }
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
            if (returnStatement.Value != null)
            {
                EmitPrimaryExpression(returnStatement,
                    CompileAssignmentOperator(
                        new AssignmentOperator()
                        {
                            Left = new Identifier(_functionContext.ReturnVariable.Name),
                            Right = returnStatement.Value,
                            ExpressionValueKind = returnStatement.Value.ExpressionValueKind,
                            SourceInfo = returnStatement.SourceInfo
                        }
                     )
                );
            }

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
                EmitPrimaryExpression(variableDeclaration, 
                    CompileAssignmentOperator(new AssignmentOperator()
                    {
                        Left = new Identifier(variableSymbol.Name),
                        Right = variableDeclaration.Initializer,
                        ExpressionValueKind = variableDeclaration.Type.ValueKind,
                        SourceInfo = variableDeclaration.SourceInfo,
                    }));
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
            if (_functionContext.ReturnVariable != null)
            {
                EmitPrimaryExpression(null, new EX_Return()
                {
                    ReturnExpression = Emit(null, new EX_LocalOutVariable()
                    {
                        Variable = GetPropertyPointer(_functionContext.ReturnVariable.Name)
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

    private void ForwardDeclareCallOutParameters(CallOperator callOperator)
    {
        foreach (var outArgument in callOperator.Arguments
            .Where(x => x is OutDeclarationArgument)
            .Cast<OutDeclarationArgument>())
        {
            // TODO do this someplace better
            var decl = new VariableDeclaration()
            {
                Identifier = outArgument.Identifier,
                SourceInfo = outArgument.SourceInfo,
                Type = outArgument.Type,
            };
            var symbol = new VariableSymbol(decl)
            {
                DeclaringSymbol = _functionContext.Symbol,
                IsExternal = false,
                Name = outArgument.Identifier.Text,
                Argument = outArgument
            };
            _functionContext.CompiledFunctionContext.Variables.Add(new(symbol));
            _blockScope.DeclareSymbol(symbol);
        }
    }

    private CompiledExpressionContext CompileCallOperator(CallOperator callOperator)
    {
        ForwardDeclareCallOutParameters(callOperator);

        // Reset context so it doesn't keep propagating until another member access pops up
        // TODO: solve this properly by isolating the context to the member part of the expression only (without its sub expressions)
        var callContext = Context;

        //PushContext(new MemberContext() { Type = ContextType.This, Symbol = _classContext.Symbol, IsImplicit = true });
        PushContext(null);
        try
        {
            if (IsIntrinsicFunction(callOperator.Identifier.Text))
            {
                // Hardcoded intrinsic function call
                return CompileIntrinsicCall(callOperator);
            }
            else
            {
                var functionToCall = GetSymbol<ProcedureSymbol>(callOperator.Identifier.Text, context: callContext);
                if (functionToCall == null)
                {
                    throw new CompilationError(callOperator, $"Call to unknown function {callOperator.Identifier.Text}");
                }
                else
                {
                    if (functionToCall.HasAnyFunctionCustomFlags(FunctionCustomFlags.CallTypeOverride))
                    {
                        if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.LocalFinalFunction))
                        {
                            return Emit(callOperator, new EX_LocalFinalFunction()
                            {
                                StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.FinalFunction))
                        {
                            return Emit(callOperator, new EX_FinalFunction()
                            {
                                StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.LocalVirtualFunction))
                        {
                            return Emit(callOperator, new EX_LocalVirtualFunction()
                            {
                                VirtualFunctionName = GetName(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.VirtualFunction))
                        {
                            return Emit(callOperator, new EX_VirtualFunction()
                            {
                                VirtualFunctionName = GetName(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.LocalVirtualFunction))
                        {
                            return Emit(callOperator, new EX_LocalVirtualFunction()
                            {
                                VirtualFunctionName = GetName(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (functionToCall.HasAllFunctionExtendedFlags(FunctionCustomFlags.MathFunction))
                        {
                            return Emit(callOperator, new EX_CallMath()
                            {
                                StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else
                        {
                            throw new NotImplementedException($"Unknown call type override: {functionToCall.CustomFlags}");
                        }
                    }
                    else
                    {
                        // See Engine/Source/Editor/KismetCompiler/Private/KismetCompilerVMBackend.cpp EmitFunctionCall
                        var isParentContext = callContext?.Type == ContextType.Base;
                        var isFinalFunction = (functionToCall.HasAnyFunctionFlags(EFunctionFlags.FUNC_Final) || isParentContext);
                        var netFuncFlags = EFunctionFlags.FUNC_Net | EFunctionFlags.FUNC_NetReliable | EFunctionFlags.FUNC_NetServer | EFunctionFlags.FUNC_NetClient | EFunctionFlags.FUNC_NetMulticast;
                        var isMathCall = isFinalFunction
                            && functionToCall.HasAllFunctionFlags(EFunctionFlags.FUNC_Static | EFunctionFlags.FUNC_Final | EFunctionFlags.FUNC_Native)
                            && !functionToCall.HasAnyFunctionFlags(netFuncFlags | EFunctionFlags.FUNC_BlueprintAuthorityOnly | EFunctionFlags.FUNC_BlueprintCosmetic | EFunctionFlags.FUNC_NetRequest | EFunctionFlags.FUNC_NetResponse)
                            && !functionToCall.DeclaringClass!.IsInterface
                            && !HasWildcardParams(functionToCall);
                        var isLocalScriptFunction =
                            !functionToCall.HasAnyFunctionFlags(EFunctionFlags.FUNC_Native | netFuncFlags | EFunctionFlags.FUNC_BlueprintAuthorityOnly | EFunctionFlags.FUNC_BlueprintCosmetic | EFunctionFlags.FUNC_NetRequest | EFunctionFlags.FUNC_NetResponse);

                        if (functionToCall.HasAnyFunctionFlags(EFunctionFlags.FUNC_Delegate))
                        {
                            throw new InvalidOperationException("Invalid call to delegate function");
                        }
                        else if (isFinalFunction)
                        {
                            if (isMathCall)
                            {
                                return Emit(callOperator, new EX_CallMath()
                                {
                                    StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                            else if (isLocalScriptFunction)
                            {
                                return Emit(callOperator, new EX_LocalFinalFunction()
                                {
                                    StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                            else
                            {
                                return Emit(callOperator, new EX_FinalFunction()
                                {
                                    StackNode = GetPackageIndex(callOperator.Identifier, context: callContext),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                        }
                        else
                        {
                            if (isLocalScriptFunction)
                            {
                                return Emit(callOperator, new EX_LocalVirtualFunction()
                                {
                                    VirtualFunctionName = GetName(callOperator.Identifier),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                            else
                            {
                                return Emit(callOperator, new EX_VirtualFunction()
                                {
                                    VirtualFunctionName = GetName(callOperator.Identifier),
                                    Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                                });
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            PopContext();
        }
    }

    private bool HasWildcardParams(ProcedureSymbol procedureSymbol)
    {
        // TODO
        return false;
    }

    private CompiledExpressionContext CompileExpression(Expression expression)
    {
        CompiledExpressionContext CompileExpressionInner()
        {
            if (expression is NullExpression nullExpression)
            {
                return Emit(nullExpression, new EX_Nothing());
            }
            else if (expression is InitializerList initializerListExpression)
            {
                return CompileInitializerList(initializerListExpression);
            }
            else if (expression is NewExpression newExpression)
            {
                return CompileNewExpression(newExpression);
            }
            else if (expression is SubscriptOperator subscriptOperator)
            {
                return CompileSubscriptOperator(subscriptOperator);
            }
            else if (expression is MemberExpression memberExpression)
            {
                return CompileMemberExpression(memberExpression);
            }
            else if (expression is CastOperator castOperator)
            {
                return CompileCastExpression(castOperator);
            }
            else if (expression is CallOperator callOperator)
            {
                return CompileCallOperator(callOperator);
            }
            else if (expression is UnaryExpression unaryExpression)
            {
                return CompileUnaryExpression(unaryExpression);
            }
            else if (expression is BinaryExpression binaryExpression)
            {
                return CompileBinaryExpression(binaryExpression);
            }
            else if (expression is ConditionalExpression conditionalExpression)
            {
                return CompileConditionalExpression(conditionalExpression);
            }
            else if (expression is Literal literal)
            {
                return CompileLiteralExpression(literal);
            }
            else if (expression is Identifier identifier)
            {
                return CompileIdentifierExpression(identifier);
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

    private CompiledExpressionContext EmitLibraryCall(Expression expression, string library, string name, IEnumerable<KismetExpression> arguments)
    {
        var librarySymbol = RootScope.GetSymbol<ClassSymbol>(library);
        if (librarySymbol == null)
        {
            librarySymbol = new ClassSymbol(null)
            {
                DeclaringSymbol = new PackageSymbol()
                {
                    DeclaringSymbol = null,
                    IsExternal = true,
                    Name = "/Script/Engine"
                },
                IsExternal = true,
                Name = library,
            };
        }

        var functionSymbol = librarySymbol.GetSymbol<ProcedureSymbol>(name);
        if (functionSymbol == null)
        {
            functionSymbol = new ProcedureSymbol(null)
            {
                DeclaringSymbol = librarySymbol,
                IsExternal = true,
                Name = name
            };
        }

        return Emit(expression, new EX_CallMath()
        {
            StackNode = new IntermediatePackageIndex(functionSymbol),
            Parameters = arguments.ToArray()
        });
    }

    private CompiledExpressionContext EmitArrayLibraryCall(Expression expression, string name, IEnumerable<KismetExpression> arguments)
        => EmitLibraryCall(expression, "KismetArrayLibrary", name, arguments);

    private CompiledExpressionContext EmitStringLibraryCall(Expression expression, string name, IEnumerable<KismetExpression> arguments)
        => EmitLibraryCall(expression, "KismetStringLibrary", name, arguments);

    private CompiledExpressionContext EmitMathLibraryCall(Expression expression, string name, IEnumerable<KismetExpression> arguments)
        => EmitLibraryCall(expression, "KismetMathLibrary", name, arguments);

    private CompiledExpressionContext EmitMathLibraryCall(UnaryExpression expression, string name)
        => EmitLibraryCall(expression, "KismetMathLibrary", name, new[] { CompileSubExpression(expression.Operand) });

    private CompiledExpressionContext EmitMathLibraryCall(BinaryExpression expression, string name)
        => EmitLibraryCall(expression, "KismetMathLibrary", name, new[] { CompileSubExpression(expression.Left), CompileSubExpression(expression.Right) });

    private CompiledExpressionContext EmitTextLibraryCall(Expression expression, string name, IEnumerable<KismetExpression> arguments)
        => EmitLibraryCall(expression, "KismetTextLibrary", name, arguments);

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
        else if (symbol is EnumValueSymbol enumValueSymbol)
        {
            return Emit(identifier, new EX_IntConst()
            {
                Value = enumValueSymbol.Value
            });
        }
        else
        {
            throw new NotImplementedException();
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
                var typeSymbol = GetSymbol<ClassSymbol>(variableSymbol.Declaration.Type.TypeParameter.Text);
                if (typeSymbol != null)
                    return typeSymbol;
                var symbol = GetSymbol<Symbol>(variableSymbol.Declaration.Type.TypeParameter!);
                if (symbol is VariableSymbol typeVariableSymbol)
                    return GetVariableTypeSymbol(typeVariableSymbol);
                return symbol!;
            }
            else
            {
                var typeSymbol = GetSymbol<ClassSymbol>(variableSymbol.Declaration.Type.Text);
                if (typeSymbol != null)
                    return typeSymbol;
                var symbol = GetSymbol<Symbol>(variableSymbol.Declaration.Type);
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

    private MemberContext GetContextForExpression(Expression expression)
    {
        var contextSymbol = GetSymbol<Symbol>(expression);
        var contextSymbolTemp = contextSymbol;
        var contextType = ContextType.Class;
        MemberContext subContext = default;
        if (contextSymbol == null)
        {
            contextType = ContextType.SubContext;
            subContext = GetContextForMemberExpression((MemberExpression)expression);
        }
        else if (contextSymbol is VariableSymbol variableSymbol)
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
                        "Object" => ContextType.Object,
                        _ => throw new NotImplementedException()
                    };
                }
                else
                {
                    //if (variableSymbol.IsExternal)
                    //{
                    //    contextType = ContextType.ObjectConst;
                    //}
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
        else if (contextSymbol is EnumSymbol enumSymbol)
        {
            contextType = ContextType.Enum;
        }

        Debug.Assert(contextSymbol != null);

        var context = new MemberContext()
        {
            SubContext = subContext,
            Symbol = contextSymbol,
            Type = contextType,
        };

        if (expression is Identifier contextIdentifier &&
            contextIdentifier.Text == _classContext?.Symbol.Name)
        {
            // TODO: make more flexible
            // Explicit virtual method call
            context.CallVirtualFunctionAsFinal = true;
        }

        return context;
    }

    private MemberContext GetContextForMemberExpression(MemberExpression memberExpression)
    {
        var contextSymbol = GetContextForExpression(memberExpression.Context);
        return contextSymbol;
    }

    private (Identifier Identifier, bool IsLookup) GetMemberIdentifier(Expression expression, bool? isVirtual = null)
    {
        if (expression is StringLiteral stringLiteral)
        {
            return (new(stringLiteral.Value), isVirtual ?? false);
        }
        else if (expression is Identifier identifier)
        {
            return (identifier, isVirtual ?? false);
        }
        else if (expression is CallOperator callOperator)
        {
            if (IsIntrinsicFunction(callOperator.Identifier.Text))
            {
                var token = GetInstrinsicFunctionToken(callOperator.Identifier.Text);
                if (token == EExprToken.EX_LocalVariable ||
                    token == EExprToken.EX_InstanceVariable ||
                    token == EExprToken.EX_LocalOutVariable ||
                    token ==  EExprToken.EX_CallMath ||
                    token == EExprToken.EX_FinalFunction ||
                    token == EExprToken.EX_LocalFinalFunction ||
                    token == EExprToken.EX_StructMemberContext)
                {
                    return GetMemberIdentifier(callOperator.Arguments[0].Expression, isVirtual ?? false);
                }
                if (token == EExprToken.EX_VirtualFunction ||
                    token == EExprToken.EX_LocalVirtualFunction)
                {
                    return GetMemberIdentifier(callOperator.Arguments[0].Expression, true);
                }
            }
        }
        else if (expression is MemberExpression memberAccessExpression)
        {
            var context = GetContextForMemberExpression(memberAccessExpression);
            PushContext(context);
            try
            {
                return GetMemberIdentifier(memberAccessExpression.Member);
            }
            finally
            {
                PopContext();
            }
        }

        return (null, false);
    }

    private CompiledExpressionContext CompileMemberExpression(MemberExpression memberExpression)
    {
        var memberContext = GetContextForMemberExpression(memberExpression);

        if (memberContext.Type == ContextType.This ||
            memberContext.Type == ContextType.Base)
        {
            // These are handled through different opcodes rather than context
            PushContext(memberContext);
            try
            {
                return CompileExpression(memberExpression.Member);
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.SubContext)
        {
            // Special case for nested context expressions
            var objectExpression = CompileExpression(memberExpression.Context).CompiledExpressions.Single();
            PushContext(memberContext);
            try
            {
                TryGetPropertyPointer(memberExpression.Member, out var pointer);
                pointer ??= RValue;

                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = objectExpression,
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = FPackageIndex.Null, New = FFieldPath.Null },
                });
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.Interface)
        {
            var interfaceValue = CompileSubExpression(memberExpression.Context);
            PushContext(memberContext);
            try
            {
                TryGetPropertyPointer(memberExpression.Member, out var pointer);
                pointer ??= RValue;

                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = Emit(memberExpression.Context, new EX_InterfaceContext()
                    {
                        InterfaceValue = interfaceValue
                    }).CompiledExpressions.Single(),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = FPackageIndex.Null, New = FFieldPath.Null },
                });
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.Struct)
        {
            var structExpression = CompileSubExpression(memberExpression.Context);
            PushContext(memberContext);
            try
            {
                return Emit(memberExpression, new EX_StructMemberContext()
                {
                    StructExpression = structExpression,
                    StructMemberExpression = GetPropertyPointer(memberExpression.Member)
                });
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.ObjectConst)
        {
            var packageIndex = GetPackageIndex(memberExpression.Context);
            PushContext(memberContext);
            try
            {
                TryGetPropertyPointer(memberExpression.Member, out var pointer);
                pointer ??= RValue;

                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = Emit(memberExpression.Context, new EX_ObjectConst()
                    {
                        Value = packageIndex
                }).CompiledExpressions.Single(),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = FPackageIndex.Null, New = FFieldPath.Null },
                }); ;
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.Enum)
        {
            PushContext(memberContext);
            try
            {
                return Emit(memberExpression, CompileSubExpression(memberExpression.Member));
            }
            finally
            {
                PopContext();
            }
        }
        else if (memberContext.Type == ContextType.Class)
        {
            var classSymbol = (ClassSymbol)memberContext.Symbol;
            if (classSymbol.IsStatic)
            {
                // No context expression for static classes
                PushContext(memberContext);
                try
                {
                    return CompileExpression(memberExpression.Member);
                }
                finally
                {
                    PopContext();
                }
            }
            else
            {
                var objectExpression = CompileSubExpression(memberExpression.Context);
                PushContext(memberContext);
                try
                {
                    TryGetPropertyPointer(memberExpression.Member, out var pointer);
                    pointer ??= RValue;

                    return Emit(memberExpression, new EX_Context()
                    {
                        ObjectExpression = objectExpression,
                        ContextExpression = CompileSubExpression(memberExpression.Member),
                        RValuePointer = pointer ?? new() { Old = FPackageIndex.Null, New = FFieldPath.Null },
                    });
                }
                finally
                {
                    PopContext();
                }
            }
        }
        else
        {
            var objectExpression = CompileSubExpression(memberExpression.Context);
            PushContext(memberContext);
            try
            {
                TryGetPropertyPointer(memberExpression.Member, out var pointer);
                pointer ??= RValue;

                return Emit(memberExpression, new EX_Context()
                {
                    ObjectExpression = objectExpression,
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new() { Old = FPackageIndex.Null, New = FFieldPath.Null },
                });
            }
            finally
            {
                PopContext();
            }
        }
    }

    private FPackageIndex GetPackageIndex(Expression expression, MemberContext? context = default)
    {
        if (expression is StringLiteral stringLiteral)
        {
            return GetPackageIndex(stringLiteral.Value, context);
        }
        else if (expression is Identifier identifier)
        {
            return GetPackageIndex(identifier.Text, context);
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

    private FPackageIndex? GetPackageIndex(Symbol symbol)
    {
        return new IntermediatePackageIndex(symbol);
    }

    private FPackageIndex? GetPackageIndex(string name, MemberContext? context = default)
    {
        // TODO fix
        if (name == "<null>")
            return new FPackageIndex();

        var symbol = GetRequiredSymbol(name, context);
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
                    if (token == EExprToken.EX_InstanceVariable && Context == null)
                    {
                        PushContext(ContextType.This, _classContext.Symbol);
                        try
                        {
                            pointer = GetPropertyPointer(callOperator.Arguments[0]);
                        }
                        finally
                        {
                            PopContext();
                        }
                    }
                    else
                    {
                        pointer = GetPropertyPointer(callOperator.Arguments[0]);
                    }
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

    private string? GetSymbolName(Expression expression)
    {
        if (expression is TypeIdentifier typeIdentifier)
        {
            if (typeIdentifier.IsConstructedType)
            {
                // TODO handle base type info (Struct<>, Array<>, etc)
                return GetSymbolName(typeIdentifier.TypeParameter!);
            }
            else
            {
                return typeIdentifier.Text;
            }
        }
        else if (expression is StringLiteral stringLiteral)
        {
            return stringLiteral.Value;
        }
        else if (expression is Identifier identifier)
        {
            return identifier.Text;
        }
        else if (expression is MemberExpression memberExpression)
        {
            PushContext(GetContextForMemberExpression(memberExpression));
            try
            {
                if (Context.Type != ContextType.This &&
                    Context.Type != ContextType.Base)
                {
                    // Context is not a symbol, but a sub-context
                    return null;
                }
                else
                {
                    return GetSymbolName(memberExpression.Member);
                }
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
                return GetSymbolName(arrayObject.Expression);
            }
            else if (callOperator.Identifier.Text == "EX_InstanceVariable")
            {
                return GetSymbolName(callOperator.Arguments.First().Expression);
            }
            else if (callOperator.Identifier.Text == "EX_SwitchValue")
            {
                var defaultTerm = callOperator.Arguments[2];
                return GetSymbolName(defaultTerm.Expression);
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

    private Symbol? GetSymbol<T>(Expression expression) where T : Symbol
    {
        if (expression is TypeIdentifier typeIdentifier)
        {
            if (typeIdentifier.IsConstructedType)
            {
                // TODO handle base type info (Struct<>, Array<>, etc)
                return GetSymbol<T>(typeIdentifier.TypeParameter!);
            }
            else
            {
                return GetSymbol<T>(typeIdentifier.Text);
            }
        }
        else if (expression is StringLiteral stringLiteral)
        {
            return GetSymbol<T>(stringLiteral.Value);
        }
        else if (expression is Identifier identifier)
        {
            return GetSymbol<T>(identifier.Text);
        }
        else if (expression is MemberExpression memberExpression)
        {
            PushContext(GetContextForMemberExpression(memberExpression));
            try
            {
                return GetSymbol<T>(memberExpression.Member);
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
                return GetSymbol<T>(arrayObject.Expression);
            }
            else if (callOperator.Identifier.Text == "EX_InstanceVariable")
            {
                if (Context == null)
                {
                    PushContext(ContextType.This, _classContext.Symbol);
                    try
                    {
                        return GetSymbol<T>(callOperator.Arguments.First().Expression);
                    }
                    finally
                    {
                        PopContext();
                    }
                }
                else
                {
                    return GetSymbol<T>(callOperator.Arguments.First().Expression);
                }
            }
            else if (callOperator.Identifier.Text == "EX_SwitchValue")
            {
                var defaultTerm = callOperator.Arguments[2];
                return GetSymbol<T>(defaultTerm.Expression);
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
