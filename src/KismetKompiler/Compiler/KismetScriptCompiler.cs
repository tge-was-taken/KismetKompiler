using KismetKompiler.Compiler.Exceptions;
using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Declarations;
using KismetKompiler.Syntax.Statements.Expressions;
using KismetKompiler.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Syntax.Statements.Expressions.Unary;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Xml.Linq;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace KismetKompiler.Compiler;


public enum ContextType
{
    None,
    Default,
    This,
    Interface,
    ObjectConst,
    Struct
}
public partial class KismetScriptCompiler
{
    private readonly UAsset _asset;
    private readonly ClassExport _class;
    private ObjectVersion _objectVersion = 0;
    private FunctionState _functionState;
    private ContextType _context;
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
        DeclareTopLevelDeclarations();

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
                throw new UnexpectedSyntaxError(declaration);
            }
        }

        PopScope();

        return script;
    }

    private void DeclareTopLevelDeclarations()
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

        void ScanExternalDeclaration(Declaration externalDeclaration)
        {
            if (!_scope.TryDeclareExternalSymbol(new ExternalSymbolInfo()
            {
                Declaration = externalDeclaration,
                PackageIndex = FindPackageIndexInAsset(externalDeclaration.Identifier.Text)
            }))
                throw new RedefinitionError(externalDeclaration);

            // TODO improve this
            if (externalDeclaration is ProcedureDeclaration procedureDeclaration)
            {
                if (!_scope.TryDeclareProcedure(new ProcedureInfo()
                {
                    Declaration = procedureDeclaration,
                    IsExternal = true,
                    PackageIndex = FindPackageIndexInAsset(externalDeclaration.Identifier.Text)
                }))
                    throw new RedefinitionError(externalDeclaration);
            }
            else if (externalDeclaration is ClassDeclaration classDeclaration)
            {
                foreach (var declaration in classDeclaration.Declarations)
                {
                    ScanExternalDeclaration(declaration);
                }
            }
            else
            {

            }
        }

        foreach (var import in _compilationUnit.Imports)
        {
            foreach (var item in import.Declarations)
            {
                ScanExternalDeclaration(item);
            }
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
            if (!System.Enum.TryParse<EClassFlags>(classFlagText, true, out var flag))
                throw new CompilationError(attribute, "Invalid class attribute");
            flags |= flag;
        }

        var functions = new List<KismetScriptFunction>();
        var properties = new List<KismetScriptProperty>();

        PushScope();

        foreach (var declaration in classDeclaration.Declarations)
        {
            if (declaration is VariableDeclaration variableDeclaration)
            {
                var variableInfo = new VariableInfo()
                {
                    Declaration = variableDeclaration,
                    PackageIndex = FindPackageIndexInAsset(variableDeclaration.Identifier.Text),
                    AllowShadowing = true
                };
                if (!_scope.TryDeclareVariable(variableInfo))
                    throw new RedefinitionError(variableDeclaration);
            }
            else if (declaration is ProcedureDeclaration procedureDeclaration)
            {
                var procedureInfo = new ProcedureInfo()
                {
                    Declaration = procedureDeclaration,
                    PackageIndex = FindPackageIndexInAsset(procedureDeclaration.Identifier.Text)
                };
                if (!_scope.TryDeclareProcedure(procedureInfo))
                    throw new RedefinitionError(procedureDeclaration);
            }
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
            ReturnLabel = CreateLabel("ReturnLabel")
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
                Parameter = param,
                PackageIndex = FindPackageIndexInAsset(param.Identifier.Text)
            }))
            {
                throw new CompilationError(param, "Unable to declare parameter");
            }
        }

        CompileCompoundStatement(procedureDeclaration.Body);
        ResolveLabel(_functionState.ReturnLabel);
        DoFixups();
        EnsureEndOfScriptPresent();

        PopScope();

        var function = new KismetScriptFunction()
        {
            Name = procedureDeclaration.Identifier.Text,
            Expressions = _functionState.PrimaryExpressions.SelectMany(x => x.CompiledExpressions).ToList(),
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
                        }, new[] { _functionState.ReturnLabel });
                    }
                    else 
                    {
                        throw new UnexpectedSyntaxError(statement);
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
                throw new UnexpectedSyntaxError(statement);
            }
        }

        PopScope();
    }

    private void CompileReturnStatement(ReturnStatement returnStatement)
    {
        if (returnStatement.Value == null)
        {
            // The original compiler has a quirk where, if you return in a block, it will always jump to a label
            // containing the return & end of script instructions
            EmitPrimaryExpression(returnStatement, new EX_Jump(), new[] { _functionState.ReturnLabel });
        }
        else
        {
            // TODO figure out how this should work?
            EmitPrimaryExpression(returnStatement, new EX_Return()
            {
                ReturnExpression = returnStatement.Value != null ?
                    CompileSubExpression(returnStatement.Value) :
                    new EX_Nothing()
            });
        }
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

    private FPackageIndex? GetImportPackageIndexByObjectName(string name)
    {
        var index = _asset.Imports.FindIndex(x => x.ObjectName.ToString() == name);
        if (index == -1)
            return null;
        return new FPackageIndex(-(index + 1));
    }

    private FPackageIndex? GetExportPackageIndexByObjectName(string name)
    {
        var index = _asset.Exports.FindIndex(x => x.ObjectName.ToString() == name);
        if (index == -1)
            return null;
        return new FPackageIndex(+(index + 1));
    }

    private FPackageIndex? GetExportPackageIndexByExport(Export export)
    {
        var index = _asset.Exports.IndexOf(export);
        if (index == -1)
            return null;
        return new FPackageIndex(+(index + 1));
    }

    private (FPackageIndex PackageIndex, PropertyExport PropertyExport) CreateVariable(VariableDeclaration variableDeclaration, bool isLocal)
    {
        string propertyType = null;
        int? serialSize = null;
        UProperty property = null;

        switch (variableDeclaration.Type.Text)
        {
            case "bool":
                propertyType = "BoolProperty";
                serialSize = 35;
                property = new UBoolProperty()
                {
                    NativeBool = true,
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 1,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            case "int":
                propertyType = "IntProperty";
                serialSize = 33;
                property = new UIntProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;

            case "string":
                propertyType = "StrProperty";
                serialSize = 33;
                property = new UStrProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;

            case "float":
                // TODO test
                propertyType = "FloatProperty";
                serialSize = 33;
                property = new UFloatProperty()
                {
                    ArrayDim = EArrayDim.TArray,
                    ElementSize = 0,
                    PropertyFlags = EPropertyFlags.CPF_None,
                    RepNotifyFunc = new FName(_asset, "None"),
                    BlueprintReplicationCondition = UAssetAPI.FieldTypes.ELifetimeCondition.COND_None,
                    RawValue = null,
                    Next = null,
                };
                break;
            default:
                throw new NotImplementedException();
        }
        var classIndex = GetExportPackageIndexByExport(_class) ?? throw new NotImplementedException();
        var functionIndex = GetExportPackageIndexByObjectName(_functionState.Name);
        var propertyClassImportIndex = GetImportPackageIndexByObjectName(propertyType);
        var coreUObjectIndex = GetImportPackageIndexByObjectName("/Script/CoreUObject") ?? throw new NotImplementedException();
        if (propertyClassImportIndex == null)
        {
            propertyClassImportIndex = _asset.AddImport(new UAssetAPI.Import()
            {
                ObjectName = new(_asset, propertyType),
                OuterIndex = coreUObjectIndex,
                ClassPackage = new(_asset, "/Script/CoreUObject"),
                ClassName = new(_asset, "Class"),
                bImportOptional = false
            });
        }

        var propertyTemplateImportIndex = GetImportPackageIndexByObjectName($"Default__{propertyType}");
        if (propertyTemplateImportIndex == null)
        {
            propertyTemplateImportIndex = _asset.AddImport(new UAssetAPI.Import()
            {
                ObjectName = new(_asset, $"Default__{propertyType}"),
                OuterIndex = coreUObjectIndex,
                ClassPackage = new(_asset, "/Script/CoreUObject"),
                ClassName = new(_asset, propertyType),
                bImportOptional = false
            });
        }

        var propertyOwnerIndex = isLocal ?
            functionIndex :
            classIndex;

        var propertyExport = new PropertyExport()
        {
            Asset = _asset,
            Property = property,
            Data = new(),
            ObjectName = new FName(_asset, variableDeclaration.Identifier.Text),
            ObjectFlags = EObjectFlags.RF_Public,
            SerialSize = serialSize.Value,
            SerialOffset = 0,
            bForcedExport = false,
            bNotForClient = false,
            bNotForServer = false,
            PackageGuid = Guid.Empty,
            IsInheritedInstance = false,
            PackageFlags = EPackageFlags.PKG_None,
            bNotAlwaysLoadedForEditorGame = false,
            bIsAsset = false,
            GeneratePublicHash = false,
            SerializationBeforeSerializationDependencies = new(),
            CreateBeforeSerializationDependencies = new(),
            SerializationBeforeCreateDependencies = new(),
            CreateBeforeCreateDependencies = new() { classIndex },
            PublicExportHash = 0,
            Padding = null,
            Extras = new byte[0],
            OuterIndex = propertyOwnerIndex,
            ClassIndex = propertyClassImportIndex,
            SuperIndex = new FPackageIndex(0),
            TemplateIndex = propertyTemplateImportIndex,
        };

        _asset.Exports.Add(propertyExport);
        return (GetExportPackageIndexByExport(propertyExport), propertyExport);
    }

    private void ProcessDeclaration(Declaration declaration)
    {
        if (declaration is LabelDeclaration labelDeclaration)
        {
            if (!_scope.TryGetLabel(labelDeclaration.Identifier.Text, out var label))
                throw new UnexpectedSyntaxError(labelDeclaration);

            label.CodeOffset = _functionState.CodeOffset;
            label.IsResolved = true;
        }
        else if (declaration is VariableDeclaration variableDeclaration)
        {
            if (!TryFindPackageIndexInAsset(variableDeclaration.Identifier.Text, out var variablePackageIndex))
            {
                (variablePackageIndex, var export) = CreateVariable(variableDeclaration, true);
            }

            if (!_scope.TryDeclareVariable(new VariableInfo()
            {
                Declaration = variableDeclaration,
                PackageIndex = variablePackageIndex
            }))
                throw new RedefinitionError(variableDeclaration);

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
            throw new UnexpectedSyntaxError(declaration);
        }
    }

    private void EnsureEndOfScriptPresent()
    {
        var beforeLastExpr = _functionState.PrimaryExpressions
            .Skip(_functionState.PrimaryExpressions.Count - 1)
            .FirstOrDefault();
        var lastExpr = _functionState.PrimaryExpressions.LastOrDefault();

        if (beforeLastExpr?.CompiledExpressions.Single() is not EX_Return &&
            lastExpr?.CompiledExpressions.Single() is not EX_Return)
        {
            EmitPrimaryExpression(null, new EX_Return()
            {
                ReturnExpression = Emit(null, new EX_Nothing()).CompiledExpressions.Single(),
            });
        }

        if (lastExpr?.CompiledExpressions.Single() is not EX_EndOfScript)
        {
            EmitPrimaryExpression(null, new EX_EndOfScript());
        }
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, CompiledExpressionContext expressionState)
    {
        _functionState.AllExpressions.Add(expressionState);
        _functionState.PrimaryExpressions.Add(expressionState);
        _functionState.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpressions);
        return expressionState;
    }

    private CompiledExpressionContext EmitPrimaryExpression(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelInfo>? referencedLabels = null)
    {
        var expressionState = Emit(syntaxNode, expression, referencedLabels);
        _functionState.AllExpressions.Add(expressionState);
        _functionState.PrimaryExpressions.Add(expressionState);
        _functionState.CodeOffset += KismetExpressionSizeCalculator.CalculateExpressionSize(expressionState.CompiledExpressions);
        return expressionState;
    }

    private void DoFixups()
    {
        foreach (var expression in _functionState.AllExpressions)
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

    private CompiledExpressionContext CompileExpression(Expression expression)
    {
        CompiledExpressionContext CompileExpressionInner()
        {
            if (expression is CallOperator callOperator)
            {
                if (IsIntrinsicFunction(callOperator.Identifier.Text))
                {
                    // Hardcoded intrinsic function call
                    return CompileIntrinsicCall(callOperator);
                }
                else
                {
                    // TODO improve call detection
                    if (_context != ContextType.None)
                    {
                        var doVirtualCall =
                            (!_scope.TryGetProcedure(callOperator.Identifier.Text, out var proc)) || // Virtual functions don't need to be explicitly imported, as they do a lookup by name
                            (proc.Declaration?.IsVirtual ?? false); // Function was explicitly defined as virtual

                        if (doVirtualCall)
                        {
                            return Emit(callOperator, new EX_LocalVirtualFunction()
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
                        // Math functions require no context
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
                return CompileIdentifierExpression(identifier);
            }
            else if (expression is Literal literal)
            {
                return CompileLiteralExpression(literal);
            }
            else if (expression is AssignmentOperator assignmentOperator)
            {
                // TODO find a better solution, EX_Context needs this in Expression
                TryGetPropertyPointer(assignmentOperator.Left, out _rvalue);
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
                            Value = pointer ?? new KismetPropertyPointer(),
                            Variable = CompileSubExpression(assignmentOperator.Left),
                            Expression = CompileSubExpression(assignmentOperator.Right),
                        });
                    }
                }
                finally
                {
                    _rvalue = null;
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
                throw new UnexpectedSyntaxError(expression);
            }
        }

        var expressionContext = CompileExpressionInner();
        _functionState.AllExpressions.Add(expressionContext);
        foreach (var compiledExpression in expressionContext.CompiledExpressions)
        {
            _functionState.ExpressionContextLookup[compiledExpression] = expressionContext;
        }
        return expressionContext;
    }

    private CompiledExpressionContext CompileIdentifierExpression(Identifier identifier)
    {
        if (identifier.Text == "this")
        {
            return Emit(identifier, new EX_Self());
        }

        if (TryGetLabel(identifier, out var label))
        {
            return Emit(identifier, new EX_IntConst()
            {
                Value = label.CodeOffset.Value
            });
        }

        if (_scope.TryGetVariable(identifier.Text, out var variable))
        {
            if (variable.Parameter?.Modifier == ParameterModifier.Out)
            {
                return Emit(identifier, new EX_LocalOutVariable()
                {
                    Variable = GetPropertyPointer(identifier.Text)
                });
            }
            else
            {
                return Emit(identifier, new EX_LocalVariable()
                {
                    Variable = GetPropertyPointer(identifier.Text)
                });
            }
        }

        if (_scope.TryGetExternalSymbol(identifier.Text, out var symbol))
        {
            return Emit(identifier, new EX_ObjectConst()
            {
                Value = symbol.PackageIndex
            });
        }

        throw new UnexpectedSyntaxError(identifier);
    }

    private CompiledExpressionContext EmitContextCallExpression(CallOperator callOperator)
    {
        var doVirtualCall =
            (!_scope.TryGetProcedure(callOperator.Identifier.Text, out var proc)) || // Virtual functions don't need to be explicitly imported, as they do a lookup by name
            (proc.Declaration?.IsVirtual ?? false); // Function was explicitly defined as virtual

        if (doVirtualCall)
        {
            return Emit(callOperator, new EX_LocalVirtualFunction()
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

    private CompiledExpressionContext CompileMemberExpression(Expression expression, MemberExpression memberExpression)
    {
        if (memberExpression.Context is Identifier contextIdentifier)
        {
            if (contextIdentifier.Text == "this")
            {
                if (memberExpression.Member is CallOperator callOperator)
                {
                    var doVirtualCall =
                                           !_scope.TryGetProcedure(callOperator.Identifier.Text, out var proc)  /* Virtual functions can be called by name without being imported */ ||
                                           (proc.Declaration?.IsVirtual ?? false);                             /* Function was declared as virtual */

                    if (doVirtualCall)
                    {

                        return Emit(expression, new EX_LocalVirtualFunction()
                        {
                            VirtualFunctionName = GetName(callOperator.Identifier),
                            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                        });
                    }
                    else
                    {
                        if (proc.Declaration.IsVirtual)
                        {
                            return Emit(expression, new EX_LocalVirtualFunction()
                            {
                                VirtualFunctionName = GetName(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else if (proc.Declaration.IsSealed)
                        {
                            return Emit(expression, new EX_LocalFinalFunction()
                            {
                                StackNode = GetPackageIndex(callOperator.Identifier),
                                Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                            });
                        }
                        else
                        {
                            throw new UnexpectedSyntaxError(proc.Declaration);
                        }
                    }

                    //var doVirtualCall = 
                    //    !_scope.TryGetProcedure(callOperator.Identifier.Text, out var proc)  /* Virtual functions can be called by name without being imported */ ||
                    //    (proc.Declaration?.IsVirtual ?? false);                             /* Function was declared as virtual */

                    //if (doVirtualCall)
                    //{

                    //    return Emit(expression, new EX_LocalVirtualFunction()
                    //    {
                    //        VirtualFunctionName = GetName(callOperator.Identifier),
                    //        Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                    //    });
                    //}
                    //else
                    //{
                    //    if (proc.Declaration.IsVirtual)
                    //    {
                    //        return Emit(expression, new EX_LocalVirtualFunction()
                    //        {
                    //            VirtualFunctionName = GetName(callOperator.Identifier),
                    //            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                    //        });
                    //    }
                    //    else if (proc.Declaration.IsSealed)
                    //    {
                    //        return Emit(expression, new EX_LocalFinalFunction()
                    //        {
                    //            StackNode = GetPackageIndex(callOperator.Identifier),
                    //            Parameters = callOperator.Arguments.Select(CompileSubExpression).ToArray()
                    //        });
                    //    }
                    //    else
                    //    {
                    //        throw new UnexpectedSyntaxError(proc.Declaration);
                    //    }
                    //}
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
                if (pointer == null)
                    pointer = _rvalue;

                _context = ContextType.Interface;
                try
                {
                    return Emit(expression, new EX_Context()
                    {
                        ObjectExpression = Emit(memberExpression.Context, new EX_InterfaceContext()
                        {
                            InterfaceValue = CompileSubExpression(memberExpression.Context)
                        }).CompiledExpressions.Single(),
                        ContextExpression = CompileSubExpression(memberExpression.Member),
                        RValuePointer = pointer ?? new(),
                    }); ;
                }
                finally
                {
                    _context = ContextType.None;
                }
            }
            else
            {
                // TODO improve this
                var isObjectConst = _scope.TryGetExternalSymbol(contextIdentifier.Text, out var symbol);
                if (isObjectConst)
                {
                    // Member access through object const
                    TryGetPropertyPointer(memberExpression.Member, out var pointer);
                    if (pointer == null)
                        pointer = _rvalue;

                    _context = ContextType.ObjectConst;
                    try
                    {
                        return Emit(expression, new EX_Context()
                        {
                            ObjectExpression = Emit(memberExpression.Context, new EX_ObjectConst()
                            {
                                Value = GetPackageIndex(memberExpression.Context)
                            }).CompiledExpressions.Single(),
                            ContextExpression = CompileSubExpression(memberExpression.Member),
                            RValuePointer = pointer ?? new(),
                        }); ;
                    }
                    finally
                    {
                        _context = ContextType.None;
                    }
                }
                else
                {
                    _context = ContextType.Struct;
                    try
                    {
                        return Emit(expression, new EX_StructMemberContext()
                        {
                            StructExpression = CompileSubExpression(memberExpression.Context),
                            StructMemberExpression = GetPropertyPointer(memberExpression.Member)
                        });
                    }
                    finally
                    {
                        _context = ContextType.None;
                    }
                }
            }
        }
        else
        {
            TryGetPropertyPointer(memberExpression.Member, out var pointer);
            if (pointer == null)
                pointer = _rvalue;


            _context = ContextType.Default;
            try
            {
                return Emit(expression, new EX_Context()
                {
                    ObjectExpression = CompileSubExpression(memberExpression.Context),
                    ContextExpression = CompileSubExpression(memberExpression.Member),
                    RValuePointer = pointer ?? new(),
                });
            }
            finally
            {
                _context = ContextType.None;
            }
        }
    }

    private KismetExpression CompileSubExpression(Expression right)
    {
        return CompileExpression(right).CompiledExpressions.Single();
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
            throw new UnexpectedSyntaxError(expression);
        return pointer;
    }

    private CompiledExpressionContext Emit(SyntaxNode syntaxNode, KismetExpression expression, KismetExpression expression2, IEnumerable<LabelInfo>? referencedLabels = null)
    {
        return new CompiledExpressionContext()
        {
            SyntaxNode = syntaxNode,
            CodeOffset = _functionState.CodeOffset,
            CompiledExpressions = new() { expression, expression2 },
            ReferencedLabels = referencedLabels?.ToList() ?? new()
        };
    }

    private CompiledExpressionContext Emit(SyntaxNode syntaxNode, KismetExpression expression, IEnumerable<LabelInfo>? referencedLabels = null)
    {
        return new CompiledExpressionContext()
        {
            SyntaxNode = syntaxNode,
            CodeOffset = _functionState.CodeOffset,
            CompiledExpressions = new() { expression },
            ReferencedLabels = referencedLabels?.ToList() ?? new()
        };
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
        return expressionState.CompiledExpressions.Single();
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

    private FPackageIndex? GetPackageIndex(string name)
    {
        if (name == "<null>")
            return null;

        if (_scope.TryGetVariable(name, out var variable))
        {
            Debug.Assert(variable.PackageIndex != null);
            return variable.PackageIndex;
        }

        if (_scope.TryGetProcedure(name, out var proc))
        {
            Debug.Assert(proc.PackageIndex != null);
            return proc.PackageIndex;
        }

        if (_scope.TryGetExternalSymbol(name, out var externalSymbol))
        {
            Debug.Assert(externalSymbol.PackageIndex != null);
            return externalSymbol.PackageIndex;
        }

        throw new KeyNotFoundException($"Unknown name \"{name}\"");

        //if (name == "<null>") 
        //    return null;

        //var classFunctionLocalName = $"{_class.ObjectName}.{_functionState.Name}.{name}";
        //var classLocalName = $"{_class.ObjectName}.{name}";
        //var localName = name;

        //var classFunctionLocalCandidates = GetPackageIndexByFullName(classFunctionLocalName).ToList();
        //if (classFunctionLocalCandidates.Count == 1)
        //    return classFunctionLocalCandidates[0].PackageIndex;

        //var classLocalCandidates = GetPackageIndexByFullName(classLocalName).ToList();
        //if (classLocalCandidates.Count == 1)
        //    return classLocalCandidates[0].PackageIndex;

        //var localCandidates = GetPackageIndexByLocalName(localName).ToList();
        //if (localCandidates.Count == 1)
        //    return localCandidates[0].PackageIndex;

        //throw new KeyNotFoundException($"Unknown name \"{name}\"");
    }

    private FFieldPath GetFieldPath(string name)
    {
        return null;
    }

    private bool TryFindPackageIndexInAsset(string name, out FPackageIndex? index)
    {
        index = null;
        if (name == "<null>")
            return true;

        var classFunctionLocalName = $"{_class.ObjectName}.{_functionState?.Name}.{name}";
        var classLocalName = $"{_class.ObjectName}.{name}";
        var localName = name;

        var classFunctionLocalCandidates = GetPackageIndexByFullName(classFunctionLocalName).ToList();
        if (classFunctionLocalCandidates.Count == 1)
        {
            index = classFunctionLocalCandidates[0].PackageIndex;
            return true;
        }

        var classLocalCandidates = GetPackageIndexByFullName(classLocalName).ToList();
        if (classLocalCandidates.Count == 1)
        {
            index = classLocalCandidates[0].PackageIndex;
            return true;
        }

        var localCandidates = GetPackageIndexByLocalName(localName).ToList();
        if (localCandidates.Count == 1)
        {
            index = localCandidates[0].PackageIndex;
            return true;
        }

        return false;
    }

    private FPackageIndex? FindPackageIndexInAsset(string name)
    {
        if (!TryFindPackageIndexInAsset(name, out var index))
            throw new KeyNotFoundException($"Unknown name \"{name}\"");
        return index;
    }
}
