using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Declarations;
using KismetKompiler.Syntax.Statements.Expressions;
using KismetKompiler.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Syntax.Statements.Expressions.Unary;
using Newtonsoft.Json.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using static System.Formats.Asn1.AsnWriter;

namespace KismetKompiler.Compiler;

public class KismetScriptCompiler
{
    class CompiledExpressionContext
    {
        public SyntaxNode SyntaxNode { get; init; }
        public KismetExpression CompiledExpression { get; init; }
        public List<LabelInfo> ReferencedLabels { get; init; } = new();
        public int CodeOffset { get; init; }

        public CompiledExpressionContext()
        {

        }

        public CompiledExpressionContext(SyntaxNode syntaxNode, int codeOffset, KismetExpression compiledExpression)
        {
            SyntaxNode = syntaxNode;
            CodeOffset = codeOffset;
            CompiledExpression = compiledExpression;
        }

        public CompiledExpressionContext(SyntaxNode syntaxNode, int codeOffset, KismetExpression compiledExpression, IEnumerable<LabelInfo> referencedLabels)
        {
            SyntaxNode = syntaxNode;
            CodeOffset = codeOffset;
            CompiledExpression = compiledExpression;
            ReferencedLabels = referencedLabels.ToList();
        }
    }

    class FunctionState
    {
        public string Name { get; init; }
        public List<CompiledExpressionContext> AllExpressions { get; init; } = new();
        public Dictionary<KismetExpression, CompiledExpressionContext> ExpressionContextLookup { get; init; } = new();
        public List<CompiledExpressionContext> PrimaryExpressions { get; init; } = new();
        public int CodeOffset { get; set; } = 0;
    }

    class CompilationError : Exception
    {
        public CompilationError(SyntaxNode syntaxNode, string message)
            : base(message)
        {

        }
    }

    class SemanticError : CompilationError
    {
        public SemanticError(SyntaxNode syntaxNode)
            : base(syntaxNode, $"{syntaxNode.SourceInfo?.Line}:{syntaxNode.SourceInfo?.Column}: {syntaxNode} was unexpected at this time.")
        {
        }
    }

    private readonly UAsset _asset;
    private readonly ClassExport _class;
    private ObjectVersion _objectVersion = 0;
    private FunctionState _functionState;
    private bool _inContext;
    private KismetPropertyPointer _rvalue;
    private CompilationUnit _compilationUnit;
    private Stack<Scope> _scopeStack = new();
    private Scope _rootScope;
    private Scope _scope => _scopeStack.Peek();

    public KismetScriptCompiler()
    {
        
    }

    public KismetScriptCompiler(UAsset asset)
    {
        _asset = asset;
        _class = _asset.GetClassExport();
        _objectVersion = _asset.ObjectVersion;
    }

    private void PushScope()
    {
        if (_scopeStack.Count == 0)
        {
            _rootScope = new(null);
            _scopeStack.Push(_rootScope);
        }
        else
        {
            _scopeStack.Push(new(_scopeStack.Peek()));
        }
    }

    private Scope PopScope()
    {
        return _scopeStack.Pop();
    }


    public KismetScript CompileCompilationUnit(CompilationUnit compilationUnit)
    {
        _compilationUnit = compilationUnit;

        var script = new KismetScript();

        PushScope();
        ScanCompilationUnit();

        foreach (var declaration in compilationUnit.Declarations)
        {
            if (declaration is ProcedureDeclaration procedureDeclaration)
            {
                script.Functions.Add(CompileFunction(procedureDeclaration));
            }
            else if (declaration is VariableDeclaration variableDeclaration)
            {
                script.Properties.Add(CompileProperty(variableDeclaration));
            }
            else if (declaration is ClassDeclaration classDeclaration)
            {
                script.Classes.Add(CompileClass(classDeclaration));
            }
            else
            {
                throw new SemanticError(declaration);
            }
        }

        PopScope();

        return script;
    }

    private void ScanCompilationUnit()
    {
        void ScanClass(ClassDeclaration classDeclaration)
        {
            foreach (var declaration in classDeclaration.Declarations)
            {
                ScanStatement(declaration);
            }
        }

        void ScanCompoundStatement(CompoundStatement compoundStatement)
        {
            foreach (var statement in compoundStatement)
            {
                ScanStatement(statement);
            }
        }

        void ScanStatement(Statement statement)
        {
            if (statement is IBlockStatement blockStatement)
            {
                foreach (var block in blockStatement.Blocks)
                    ScanCompoundStatement(block);
            }
            else if (statement is Declaration declaration)
            {
                if (declaration is LabelDeclaration labelDeclaration)
                {
                    if (!_scope.TryDeclareLabel(labelDeclaration))
                    {
                        throw new CompilationError(labelDeclaration, $"Label {labelDeclaration.Identifier.Text} declared more than once");
                    }
                }
                else
                {
                    //
                }
            }
        }

        void ScanFunction(ProcedureDeclaration procedureDeclaration)
        {
            ScanCompoundStatement(procedureDeclaration.Body);
        }

        foreach (var declaration in _compilationUnit.Declarations)
        {
            if (declaration is ProcedureDeclaration procedureDeclaration)
            {
                ScanFunction(procedureDeclaration);
            }
            else if (declaration is ClassDeclaration classDeclaration)
            {
                ScanClass(classDeclaration);
            }
        }
    }

    public KismetScriptClass CompileClass(ClassDeclaration classDeclaration)
    {
        EClassFlags flags = 0;
        foreach (var attribute in classDeclaration.Attributes)
        {
            var classFlagText = $"CLASS_{attribute.Identifier.Text}";
            if (System.Enum.TryParse<EClassFlags>(classFlagText, true, out var flag))
                throw new CompilationError(attribute, "Invalid class attribute");
            flags |= flag;
        }

        var functions = new List<KismetScriptFunction>();
        var properties = new List<KismetScriptProperty>();

        PushScope();


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
                throw new SemanticError(declaration);
            }
        }

        PopScope();

        return new KismetScriptClass()
        {
            Name = classDeclaration.Identifier.Text,
            BaseClass = classDeclaration.BaseClassIdentifier?.Text,
            Flags = flags,
            Functions = functions,
            Properties = properties
        };
    }

    private KismetScriptProperty CompileProperty(VariableDeclaration variableDeclaration)
    {
        return new KismetScriptProperty()
        {
            Name = variableDeclaration.Identifier.Text,
            Type = variableDeclaration.Type.Text
        };
    }

    public KismetScriptFunction CompileFunction(ProcedureDeclaration procedureDeclaration)
    {
        _functionState = new()
        {
            Name = procedureDeclaration.Identifier.Text,
        };

        PushScope();

        foreach (var param in procedureDeclaration.Parameters)
        {
            if (!_scope.TryDeclareVariable(new VariableInfo()
            {
                Declaration = new VariableDeclaration()
                {
                    Identifier = param.Identifier,
                    Initializer = param.DefaultVaue,
                    SourceInfo = param.SourceInfo,
                    Type = param.Type,
                },
                Parameter = param
            }))
            {
                throw new CompilationError(param, "Unable to declare parameter");
            }
        }

        CompileCompoundStatement(procedureDeclaration.Body);
        DoFixups();
        EnsureEndOfScriptPresent();

        PopScope();

        var function = new KismetScriptFunction()
        {
            Name = procedureDeclaration.Identifier.Text,
            Expressions = _functionState.PrimaryExpressions.Select(x => x.CompiledExpression).ToList(),
        };

        return function;
    }

    private void CompileCompoundStatement(CompoundStatement compoundStatement)
    {
        PushScope();

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
                EmitPrimaryExpression(returnStatement, new EX_Return()
                {
                    ReturnExpression = returnStatement.Value != null ?
                        CompileSubExpression(returnStatement.Value) :
                        new EX_Nothing()
                });
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
                // Match 'if (!(K2Node_SwitchInteger_CmpSuccess)) goto _674;'

                if (ifStatement.Condition is LogicalNotOperator notOperator)
                {
                    if (ifStatement.Body != null &&
                        ifStatement.Body.First() is GotoStatement ifStatementBodyGotoStatement)
                    {

                        EmitPrimaryExpression(notOperator, new EX_JumpIfNot()
                        {
                            BooleanExpression = CompileSubExpression(notOperator.Operand)
                        }, new[] { GetLabel(ifStatementBodyGotoStatement.Label) });
                    }
                    else
                    {
                        throw new SemanticError(statement);
                    }
                }
                else
                {
                    var endLabel = CreateLabel("IfStatementEndLabel");
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
                        var elseLabel = CreateLabel("IfStatementElseLabel");
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
                throw new SemanticError(statement);
            }
        }

        PopScope();
    }

    private LabelInfo CreateLabel(string name)
    {
        return new LabelInfo()
        {
            CodeOffset = null,
            IsResolved = false,
            Name = name,
        };
    }

    private void ResolveLabel(LabelInfo labelInfo)
    {
        labelInfo.IsResolved = true;
        labelInfo.CodeOffset = _functionState.CodeOffset;
    }

    private void ProcessDeclaration(Declaration declaration)
    {
        if (declaration is LabelDeclaration labelDeclaration)
        {
            if (!_scope.TryGetLabel(labelDeclaration.Identifier.Text, out var label))
                throw new SemanticError(labelDeclaration);

            label.CodeOffset = _functionState.CodeOffset;
            label.IsResolved = true;
        }
        else if (declaration is VariableDeclaration variableDeclaration)
        {
            if (!_scope.TryDeclareVariable(variableDeclaration))
                throw new CompilationError(variableDeclaration, $"Variable {variableDeclaration.Identifier} redeclared");

            if (variableDeclaration.Initializer != null)
            {
                EmitPrimaryExpression(variableDeclaration, new EX_Let()
                {
                    Value = GetPropertyPointer(variableDeclaration.Identifier),
                    Variable = CompileSubExpression(variableDeclaration.Identifier),
                    Expression = CompileSubExpression(variableDeclaration.Initializer)
                });
            }
        }
        else
        {
            throw new SemanticError(declaration);
        }
    }

    private void EnsureEndOfScriptPresent()
    {
        var beforeLastExpr = _functionState.PrimaryExpressions
            .Skip(_functionState.PrimaryExpressions.Count - 1)
            .FirstOrDefault();
        var lastExpr = _functionState.PrimaryExpressions.LastOrDefault();

        if (beforeLastExpr?.CompiledExpression is not EX_Return &&
            lastExpr?.CompiledExpression is not EX_Return)
        {
            EmitPrimaryExpression(null, new EX_Return()
            {
                ReturnExpression = Emit(null, new EX_Nothing()).CompiledExpression,
            });
        }

        if (lastExpr?.CompiledExpression is not EX_EndOfScript)
        {
            EmitPrimaryExpression(null, new EX_EndOfScript());
        }
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, CompiledExpressionContext expressionState)
    {
        _functionState.AllExpressions.Add(expressionState);
        _functionState.PrimaryExpressions.Add(expressionState);
        _functionState.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpression);
        return expressionState;
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelInfo>? referencedLabels = null)
    {
        var expressionState = Emit(syntaxNode, expression, referencedLabels);
        _functionState.AllExpressions.Add(expressionState);
        _functionState.PrimaryExpressions.Add(expressionState);
        _functionState.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpression);
        return expressionState;
    }

    private void DoFixups()
    {
        foreach (var expression in _functionState.AllExpressions)
        {
            switch (expression.CompiledExpression)
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

    private CompiledExpressionContext CompileExpression(Expression expression)
    {
        CompiledExpressionContext CompileExpressionInner()
        {
            if (expression is CallOperator callOperator)
            {
                if (IsIntrinsicFunction(callOperator.Identifier.Text))
                {
                    return CompileIntrinsicCall(callOperator);
                }
                else
                {
                    // TODO improve call detection
                    if (_inContext)
                    {
                        return Emit(callOperator, new EX_LocalVirtualFunction()
                        {
                            VirtualFunctionName = GetName(callOperator.Identifier),
                            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                        });
                    }
                    else
                    {
                        return Emit(callOperator, new EX_CallMath()
                        {
                            StackNode = GetPackageIndex(callOperator.Identifier),
                            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                        });
                    }
                }
            }
            else if (expression is Identifier identifier)
            {
                if (identifier.Text == "this")
                {
                    return Emit(expression, new EX_Self());
                }
                else
                {
                    if (TryGetLabel(expression, out var label))
                    {
                        return Emit(expression, new EX_IntConst()
                        {
                            Value = label.CodeOffset.Value
                        });
                    }
                    else
                    {  
                        if (_scope.TryGetVariable(identifier.Text, out var variable) &&
                            variable.Parameter?.Modifier == ParameterModifier.Out)
                        {
                            return Emit(expression, new EX_LocalOutVariable()
                            {
                                Variable = GetPropertyPointer(identifier.Text)
                            });
                        }
                        else
                        {
                            return Emit(expression, new EX_LocalVariable()
                            {
                                Variable = GetPropertyPointer(identifier.Text)
                            });
                        }
                    }
                }
            }
            else if (expression is Literal literal)
            {
                return CompileLiteralExpression(literal);
            }
            else if (expression is AssignmentOperator assignmentOperator)
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
                    // TODO find a better solution, EX_Context needs this in Expression
                    _rvalue = GetPropertyPointer(assignmentOperator.Left);
                    try
                    {
                        return Emit(expression, new EX_Let()
                        {
                            Value = GetPropertyPointer(assignmentOperator.Left),
                            Variable = CompileSubExpression(assignmentOperator.Left),
                            Expression = CompileSubExpression(assignmentOperator.Right),
                        });
                    }
                    finally
                    {
                        _rvalue = null;
                    }
                }
            }
            else if (expression is CastOperator castOperator)
            {
                return CompileCastExpression(castOperator);
            }
            else if (expression is MemberExpression memberExpression)
            {
                return CompileMemberExpression(expression, memberExpression);
            }
            else
            {
                throw new SemanticError(expression);
            }
        }

        var expressionContext = CompileExpressionInner();
        _functionState.AllExpressions.Add(expressionContext);
        _functionState.ExpressionContextLookup[expressionContext.CompiledExpression] = expressionContext;
        return expressionContext;
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
                        throw new SemanticError(literal);
                }
            }
            else if (literal is BoolLiteral boolLiteral)
            {
                switch (castOperator.TypeIdentifier.ValueKind)
                {
                    case ValueKind.Bool:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_True() : new EX_False());
                    case ValueKind.Int:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_IntConst() {  Value = 1 } : new EX_IntConst() { Value = 0 });
                    case ValueKind.Float:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_FloatConst() { Value = 1 } : new EX_FloatConst() { Value = 0 });
                    case ValueKind.Byte:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_ByteConst() { Value = 1 } : new EX_ByteConst() { Value = 0 });
                    default:
                        throw new SemanticError(literal);
                }
            }
            else
            {
                throw new SemanticError(literal);
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
            throw new SemanticError(literal);
        }
    }

    private ProcedureDeclaration? GetLocalFunction(string name)
    {
        return (ProcedureDeclaration?)_compilationUnit.Declarations
            .Where(x => x.Identifier.Text == name)
            .FirstOrDefault(); 
    }

    private CompiledExpressionContext CompileMemberExpression(Expression expression, MemberExpression memberExpression)
    {
        if (memberExpression.Context is Identifier contextIdentifier)
        {
            if (contextIdentifier.Text == "this")
            {
                if (memberExpression.Member is CallOperator callOperator)
                {
                    var procedureDecl = GetLocalFunction(callOperator.Identifier.Text);
                    if (procedureDecl != null)
                    {
                        if (procedureDecl.IsVirtual)
                        {
                            return Emit(expression, new EX_LocalVirtualFunction()
                            {
                                VirtualFunctionName = GetName(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (procedureDecl.IsSealed)
                        {
                            return Emit(expression, new EX_LocalFinalFunction()
                            {
                                StackNode = GetPackageIndex(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else
                        {
                            throw new SemanticError(procedureDecl);
                        }
                    }
                    else
                    {
                        return Emit(expression, new EX_LocalVirtualFunction()
                        {
                            VirtualFunctionName = GetName(callOperator.Identifier),
                            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                        });
                    }
                }
                else
                {
                    return Emit(expression, new EX_InstanceVariable()
                    {
                        Variable = GetPropertyPointer(memberExpression.Member),
                    });
                }
            }
            else if (memberExpression.Kind == MemberExpressionKind.Pointer)
            {
                // Member access through interface
                TryGetPropertyPointer(memberExpression.Member, out var pointer);

                _inContext = true;
                try
                {
                    return Emit(expression, new EX_Context()
                    {
                        ObjectExpression = Emit(memberExpression.Context, new EX_InterfaceContext()
                        {
                            InterfaceValue = CompileSubExpression(memberExpression.Context)
                        }).CompiledExpression,
                        ContextExpression = CompileSubExpression(memberExpression.Member),
                        RValuePointer = pointer ?? new KismetPropertyPointer(),
                    }); ;
                }
                finally
                {
                    _inContext = false;
                }
            }
            else
            {
                return Emit(expression, new EX_StructMemberContext()
                {
                    StructExpression = CompileSubExpression(memberExpression.Context),
                    StructMemberExpression = GetPropertyPointer(memberExpression.Member)
                });
            }
        }
        else
        {
            TryGetPropertyPointer(memberExpression.Member, out var pointer);
            if (pointer == null)
                pointer = _rvalue;


            _inContext = true;
            try
            {
                return Emit(expression, new EX_Context()
                {
                    ObjectExpression = CompileSubExpression(memberExpression.Context),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new KismetPropertyPointer(),
                });
            }
            finally
            {
                _inContext = false;
            }
        }
    }

    private KismetExpression CompileSubExpression(Expression right)
    {
        return CompileExpression(right).CompiledExpression;
    }

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
            pointer = GetPropertyPointer(memberAccessExpression.Member);
        }

        return pointer != null;
    }

    private KismetPropertyPointer GetPropertyPointer(Expression expression)
    {
        if (!TryGetPropertyPointer(expression, out var pointer))
            throw new SemanticError(expression);
        return pointer;
    }

    private CompiledExpressionContext Emit(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelInfo>? referencedLabels = null)
    {
        return new CompiledExpressionContext()
        {
            SyntaxNode = syntaxNode,
            CodeOffset = _functionState.CodeOffset,
            CompiledExpression = expression,
            ReferencedLabels = referencedLabels?.ToList() ?? new()
        };
    }

    private static bool FitsInType(long value, Type type)
    {
        if (type == typeof(sbyte))
        {
            return value >= sbyte.MinValue && value <= sbyte.MaxValue;
        }
        else if (type == typeof(byte))
        {
            return value >= byte.MinValue && value <= byte.MaxValue;
        }
        else if (type == typeof(short))
        {
            return value >= short.MinValue && value <= short.MaxValue;
        }
        else if (type == typeof(ushort))
        {
            return value >= ushort.MinValue && value <= ushort.MaxValue;
        }
        else if (type == typeof(int))
        {
            return value >= int.MinValue && value <= int.MaxValue;
        }
        else if (type == typeof(uint))
        {
            return value >= uint.MinValue && value <= uint.MaxValue;
        }
        else if (type == typeof(long))
        {
            return true; // long can always fit within itself
        }
        else if (type == typeof(ulong))
        {
            return value >= 0; // ulong only accommodates non-negative values
        }
        else
        {
            throw new ArgumentException("Unsupported numeric type.");
        }
    }

    private CompiledExpressionContext CompileIntrinsicCall(CallOperator callOperator)
    {
        var token = GetInstrinsicFunctionToken(callOperator.Identifier.Text);
        var offset = _functionState.CodeOffset;
        switch (token)
        {
            case EExprToken.EX_LocalVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_LocalVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
            case EExprToken.EX_InstanceVariable:
                return new CompiledExpressionContext(callOperator, offset, new EX_InstanceVariable()
                {
                    Variable = GetPropertyPointer(callOperator.Arguments[0])
                });
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
                }, new[] { GetLabel(callOperator.Arguments[0])});
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
                return new CompiledExpressionContext(callOperator, offset, new EX_Let()
                {
                    Value = GetPropertyPointer(callOperator.Arguments[0]),
                    Variable = CompileSubExpression(callOperator.Arguments[1]),
                    Expression = CompileSubExpression(callOperator.Arguments[2])
                });
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
                return new CompiledExpressionContext(callOperator, offset, new EX_LetBool()
                {
                    VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                });
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
                return new CompiledExpressionContext(callOperator, offset, new EX_Context()
                {
                    ObjectExpression = CompileSubExpression(callOperator.Arguments[0]),
                    RValuePointer = GetPropertyPointer(callOperator.Arguments[2]),
                    ContextExpression = CompileSubExpression(callOperator.Arguments[3]),
                }, new[] { GetLabel(callOperator.Arguments[1]) });
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
                    Value = GetScriptText(callOperator.Arguments[0]),
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
                    StructSize = GetInt32(callOperator.Arguments[1])
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
                return new CompiledExpressionContext(callOperator, offset, new EX_LetMulticastDelegate()
                {
                    VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                });
            case EExprToken.EX_LetDelegate:
                return new CompiledExpressionContext(callOperator, offset, new EX_LetDelegate()
                {
                    VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1]),
                });
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
                return new CompiledExpressionContext(callOperator, offset, new EX_LetObj()
                {
                    VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                });
            case EExprToken.EX_LetWeakObjPtr:
                return new CompiledExpressionContext(callOperator, offset, new EX_LetWeakObjPtr()
                {
                    VariableExpression = CompileSubExpression(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                });
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
                    Delegate = CompileSubExpression(callOperator.Arguments[0])
                });
            case EExprToken.EX_LetValueOnPersistentFrame:
                return new CompiledExpressionContext(callOperator, offset, new EX_LetValueOnPersistentFrame()
                {
                    DestinationProperty = GetPropertyPointer(callOperator.Arguments[0]),
                    AssignmentExpression = CompileSubExpression(callOperator.Arguments[1])
                });
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
                var referencedLabels = new List<LabelInfo>()
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

    private FKismetSwitchCase[] CompileSwitchCases(IEnumerable<Argument> args, List<LabelInfo> referencedLabels)
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

    private FName GetName(Expression expression)
    {
        if (expression is StringLiteral stringLiteral)
        {
            return new FName(_asset, _asset.AddNameReference(new(stringLiteral.Value)));
        }
        else if (expression is Identifier identifier)
        {
            return new FName(_asset, _asset.AddNameReference(new(identifier.Text)));
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

    private FScriptText GetScriptText(Argument argument)
    {
        throw new NotImplementedException();
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
        else
        {
            throw new NotImplementedException();
        }
    }

    private bool TryGetLabel(Expression expression, out LabelInfo label)
    {
        if (expression is IntLiteral intLiteral)
        {
            label = new LabelInfo()
            {
                CodeOffset = intLiteral.Value,
                IsResolved = true,
                Name = $"_{intLiteral.Value}"
            };
            return true;
        }
        else if (expression is Identifier identifier)
        {
            if (_scope.TryGetLabel(identifier.Text, out label))
            {
                return true;
            }
            else if (identifier.Text.StartsWith("_") && int.TryParse(identifier.Text.Substring(1), out var value))
            {
                label = new LabelInfo()
                {
                    CodeOffset = value,
                    IsResolved = true,
                    Name = identifier.Text
                };
                return true;
            }
        }

        label = null;
        return false;
    }

    private LabelInfo GetLabel(Expression expression)
    {
        if (!TryGetLabel(expression, out var label))
            throw new NotImplementedException();
        return label;
    }

    private LabelInfo GetLabel(Argument argument)
    {
        return GetLabel(argument.Expression);
    }

    private LabelInfo GetLabel(string name)
    {
        if (!_scope.TryGetLabel(name, out var label))
        {
            throw new CompilationError(null, $"Label {name} not found");
        }
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
        return expressionState.CompiledExpression;
    }

    private bool IsIntrinsicFunction(string name)
        => typeof(EExprToken).GetEnumNames().Contains(name);

    private EExprToken GetInstrinsicFunctionToken(string name)
        => (EExprToken)System.Enum.Parse(typeof(EExprToken), name);

    private KismetPropertyPointer GetPropertyPointer(Argument argument)
    {
        return GetPropertyPointer(argument.Expression);
    }

    private KismetPropertyPointer GetPropertyPointer(string name)
    {
        return new KismetPropertyPointer()
        {
            Old = GetPackageIndex(name),
            New = GetFieldPath(name)
        };
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
        else
        {
            throw new NotImplementedException();
        }
    }

    private FPackageIndex GetPackageIndex(Argument argument)
        => GetPackageIndex(argument.Expression);

    private string? GetFullName(object obj)
    {
        if (obj is UAssetAPI.Import import)
        {
            if (import.OuterIndex.Index != 0)
            {
                string parent = GetFullName(import.OuterIndex);
                return parent + "." + import.ObjectName.ToString();
            }
            else
            {
                return import.ObjectName.ToString();
            }
        }
        else if (obj is Export export)
        {
            if (export.OuterIndex.Index != 0)
            {
                string parent = GetFullName(export.OuterIndex);
                return parent + "." + export.ObjectName.ToString();
            }
            else
            {
                return export.ObjectName.ToString();
            }
        }
        else if (obj is FField field)
            return field.Name.ToString();
        else if (obj is FName fname)
            return fname.ToString();
        else if (obj is FPackageIndex packageIndex)
        {
            if (packageIndex.IsImport())
                return GetFullName(packageIndex.ToImport(_asset));
            else if (packageIndex.IsExport())
                return GetFullName(packageIndex.ToExport(_asset));
            else if (packageIndex.IsNull())
                return null;
            else
                throw new NotImplementedException();
        }
        else
        {
            return null;
        }
    }

    private IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByLocalName(string name)
    {
        foreach (var import in _asset.Imports)
        {
            if (import.ObjectName.ToString() == name)
            {
                yield return (import, new FPackageIndex(-(_asset.Imports.IndexOf(import) + 1)));
            }
        }
        foreach (var export in _asset.Exports)
        {
            if (export.ObjectName.ToString() == name)
            {
                yield return ((export, new FPackageIndex(+(_asset.Exports.IndexOf(export) + 1))));
            }
        }
    }

    private IEnumerable<(object ImportOrExport, FPackageIndex PackageIndex)> GetPackageIndexByFullName(string name)
    {
        foreach (var import in _asset.Imports)
        {
            var importFullName = GetFullName(import);
            if (importFullName == name)
            {
                yield return (import, new FPackageIndex(-(_asset.Imports.IndexOf(import) + 1)));
            }
        }
        foreach (var export in _asset.Exports)
        {
            var exportFullName = GetFullName(export);
            if (exportFullName == name)
            {
                yield return ((export, new FPackageIndex(+(_asset.Exports.IndexOf(export) + 1))));
            }
        }
    }

    private string GetFullName(string name)
    {
        return $"{_class.ObjectName}.{_functionState.Name}.{name}";
    }

    private FPackageIndex? GetPackageIndex(string name)
    {
        if (name == "<null>") 
            return null;

        var classFunctionLocalName = $"{_class.ObjectName}.{_functionState.Name}.{name}";
        var classLocalName = $"{_class.ObjectName}.{name}";
        var localName = name;

        var classFunctionLocalCandidates = GetPackageIndexByFullName(classFunctionLocalName).ToList();
        if (classFunctionLocalCandidates.Count == 1)
            return classFunctionLocalCandidates[0].PackageIndex;

        var classLocalCandidates = GetPackageIndexByFullName(classLocalName).ToList();
        if (classLocalCandidates.Count == 1)
            return classLocalCandidates[0].PackageIndex;

        var localCandidates = GetPackageIndexByLocalName(localName).ToList();
        if (localCandidates.Count == 1)
            return localCandidates[0].PackageIndex;

        throw new KeyNotFoundException($"Unknown name \"{name}\"");
    }

    private FFieldPath GetFieldPath(string name)
    {
        return null;
    }
}
