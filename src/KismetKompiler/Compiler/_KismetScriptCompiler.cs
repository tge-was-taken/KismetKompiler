﻿//using KismetKompiler.Compiler.Processing;
//using KismetKompiler.Parser;
//using KismetKompiler.Syntax;
//using KismetKompiler.Syntax.Statements;
//using KismetKompiler.Syntax.Statements.Declarations;
//using KismetKompiler.Syntax.Statements.Expressions;
//using KismetKompiler.Syntax.Statements.Expressions.Binary;
//using KismetKompiler.Syntax.Statements.Expressions.Identifiers;
//using KismetKompiler.Syntax.Statements.Expressions.Literals;
//using KismetKompiler.Syntax.Statements.Expressions.Unary;
//using System.Diagnostics;
//using System.Security.Cryptography;
//using System.Text;
//using UAssetAPI.Kismet.Bytecode;
//using UAssetAPI.Kismet.Bytecode.Expressions;

//namespace KismetKompiler.Compiler;

///// <summary>
///// Represents the compiler for KismetScripts. Responsible for transforming KismetScript sources into code.
///// </summary>
//public class KismetScriptCompiler
//{
//    //
//    // compiler state
//    //
//    private readonly Logger mLogger;
//    private readonly HashSet<int> mImportedFileHashSet;
//    private bool mReresolveImports;
//    private string mFilePath;
//    private string mCurrentBaseDirectory;
//    private int mNextLabelIndex;
//    private Stack<ScopeContext> mScopeStack;
//    private ScopeContext mRootScope;
//    private Dictionary<string, List<KismetExpression>> mProcedureInstructionCache;
//    private KismetScript mScript;

//    //
//    // procedure state
//    //
//    private ProcedureDeclaration mProcedureDeclaration;
//    private List<KismetExpression> mInstructions;
//    private Dictionary<string, LabelInfo> mLabels;

//    private ScopeContext Scope => mScopeStack.Peek();

//    /// <summary>
//    /// Initializes a KismetScript compiler with the given format version.
//    /// </summary>
//    /// <param name="version"></param>
//    public KismetScriptCompiler()
//    {
//        mLogger = new Logger(nameof(KismetScriptCompiler));
//        mImportedFileHashSet = new HashSet<int>();
//    }

//    /// <summary>
//    /// Adds a compiler log listener. Use this if you want to see what went wrong during compilation.
//    /// </summary>
//    /// <param name="listener">The listener to add.</param>
//    public void AddListener(LogListener listener)
//    {
//        listener.Subscribe(mLogger);
//    }

//    /// <summary>
//    /// Tries to compile the provided KismetScript source. Returns a boolean indicating if the operation succeeded.
//    /// </summary>
//    /// <param name="source"></param>
//    /// <param name="KismetScript"></param>
//    /// <returns></returns>
//    public bool TryCompile(string source, out KismetScript kismetScript)
//    {
//        Info("Start compiling KismetScript from source");

//        // Add source to prevent recursion
//        mImportedFileHashSet.Add(source.GetHashCode());

//        // Parse compilation unit
//        var parser = new KismetScriptASTParser();
//        parser.AddListener(new LoggerPassthroughListener(mLogger));
//        if (!parser.TryParse(source, out var compilationUnit))
//        {
//            Error("Failed to parse compilation unit");
//            kismetScript = null;
//            return false;
//        }

//        mCurrentBaseDirectory = "";
//        return TryCompile(compilationUnit, out kismetScript);
//    }

//    /// <summary>
//    /// Tries to compile the provided KismetScript source. Returns a boolean indicating if the operation succeeded.
//    /// </summary>
//    /// <param name="source"></param>
//    /// <param name="KismetScript"></param>
//    /// <returns></returns>
//    public bool TryCompile(Stream stream, out KismetScript kismetScript)
//    {
//        if (stream is FileStream fileStream)
//        {
//            mFilePath = Path.GetFullPath(fileStream.Name);
//            mCurrentBaseDirectory = Path.GetDirectoryName(mFilePath);
//            Info($"Start compiling KismetScript from file '{mFilePath}'");
//            Info($"Base directory set to '{mCurrentBaseDirectory}'");
//        }
//        else
//        {
//            Info("Start compiling KismetScript from stream");
//            Warning("Because the input is not a file, this means imports will not work!");
//        }

//        // Add hash for current file
//        var hashAlgo = new MD5CryptoServiceProvider();
//        var hashBytes = hashAlgo.ComputeHash(stream);
//        int hashInt = BitConverter.ToInt32(hashBytes, 0);
//        mImportedFileHashSet.Add(hashInt);
//        stream.Position = 0;

//        // Parse compilation unit
//        var parser = new KismetScriptASTParser();
//        parser.AddListener(new LoggerPassthroughListener(mLogger));
//        if (!parser.TryParse(stream, out var compilationUnit))
//        {
//            Error("Failed to parse compilation unit");
//            kismetScript = null;
//            return false;
//        }

//        return TryCompile(compilationUnit, out kismetScript);
//    }

//    /// <summary>
//    /// Tries to compile the provided KismetScript source. Returns a boolean indicating if the operation succeeded.
//    /// </summary>
//    /// <param name="source"></param>
//    /// <param name="KismetScript"></param>
//    /// <returns></returns>
//    public bool TryCompile(CompilationUnit compilationUnit, out KismetScript kismetScript)
//    {
//        // Resolve types that are unresolved at parse time
//        var resolver = new TypeResolver();
//        resolver.AddListener(new LoggerPassthroughListener(mLogger));
//        if (!resolver.TryResolveTypes(compilationUnit))
//        {
//            Error("Failed to resolve types in compilation unit");
//            kismetScript = null;
//            return false;
//        }

//        // Syntax checker?

//        // Compile compilation unit
//        if (!TryCompileCompilationUnit(compilationUnit))
//        {
//            kismetScript = null;
//            return false;
//        }

//        kismetScript = mScript;

//        return true;
//    }

//    //
//    // Compiling compilation units
//    //
//    private void InitializeCompilationState()
//    {
//        mScript = new KismetScript();
//        mNextLabelIndex = 0;

//        // Set up scope stack
//        mScopeStack = new Stack<ScopeContext>();

//        // Create & push root scope
//        // This is where all script-level declarations are stored
//        mRootScope = new ScopeContext(null);
//        mScopeStack.Push(mRootScope);

//        mProcedureInstructionCache = new Dictionary<string, List<KismetExpression>>();
//    }

//    private bool TryCompileCompilationUnit(CompilationUnit compilationUnit)
//    {
//        Info($"Start compiling KismetScript compilation unit");

//        // Initialize
//        InitializeCompilationState();

//        // Resolve imports
//        if (compilationUnit.Imports.Count > 0)
//        {
//            do
//            {
//                if (!TryResolveImports(compilationUnit))
//                {
//                    Error(compilationUnit, "Failed to resolve imports");
//                    return false;
//                }
//            } while (mReresolveImports);
//        }

//        // Evaluate declarations, return values, parameters etc
//        if (!TryEvaluateCompilationUnitBeforeCompilation(compilationUnit))
//            return false;

//        // Compile compilation unit body
//        foreach (var statement in compilationUnit.Declarations)
//        {
//            if (statement is ProcedureDeclaration procedureDeclaration)
//            {
//                if (procedureDeclaration.Body != null)
//                {
//                    if (!TryCompileProcedure(procedureDeclaration, out var procedure))
//                        return false;

//                    // Add compiled procedure
//                    AddCompiledProcedure(procedure);
//                }
//            }
//            else if (statement is VariableDeclaration variableDeclaration)
//            {
//                if (variableDeclaration.Initializer != null)
//                {
//                    if (variableDeclaration.Modifier == null || variableDeclaration.Modifier.Kind != VariableModifierKind.Constant)
//                    {
//                        Error(variableDeclaration.Initializer, "Non-constant variables declared outside of a procedure can't be initialized with a value");
//                        return false;
//                    }
//                }
//                else
//                {
//                    if (variableDeclaration.Modifier?.Kind == VariableModifierKind.Constant)
//                    {
//                        if (variableDeclaration.Initializer == null)
//                        {
//                            Error(variableDeclaration, "Missing initializer for constant variable");
//                            return false;
//                        }
//                    }
//                }
//            }
//            else if (!(statement is FunctionDeclaration) && !(statement is EnumDeclaration))
//            {
//                Error(statement, $"Unexpected top-level statement type: {statement}");
//                return false;
//            }
//        }

//        Info("Done compiling compilation unit");

//        return true;
//    }

//    private void ExpandImportStatementsPaths(CompilationUnit compilationUnit, string baseDirectory)
//    {
//        foreach (var import in compilationUnit.Imports)
//        {
//            import.CompilationUnitFileName = Path.Combine(baseDirectory, import.CompilationUnitFileName);
//        }
//    }

//    //
//    // Resolving imports
//    //
//    private bool TryResolveImports(CompilationUnit compilationUnit)
//    {
//        Info(compilationUnit, "Resolving imports");
//        ExpandImportStatementsPaths(compilationUnit, mCurrentBaseDirectory);
//        return true;
//    }

//    private bool TryGetFullImportPath(Import import, out string path)
//    {
//        var compilationUnitFilePath = import.CompilationUnitFileName;

//        if (!File.Exists(compilationUnitFilePath))
//        {
//            // Retry as relative path if we have a filename
//            if (mFilePath != null)
//            {
//                compilationUnitFilePath = Path.Combine(Path.GetDirectoryName(mFilePath), compilationUnitFilePath);

//                if (!File.Exists(compilationUnitFilePath))
//                {
//                    Error(import, $"File to import does not exist: {import.CompilationUnitFileName}");
//                    path = null;
//                    return false;
//                }
//            }
//            else
//            {
//                Error(import, $"File to import does not exist: {import.CompilationUnitFileName}");
//                path = null;
//                return false;
//            }
//        }

//        path = compilationUnitFilePath;
//        return true;
//    }

//    private bool TryEvaluateCompilationUnitBeforeCompilation(CompilationUnit compilationUnit)
//    {
//        bool hasIntReturnValue = false;
//        bool hasFloatReturnValue = false;
//        short maxIntParameterCount = 0;
//        short maxFloatParameterCount = 0;

//        // top-level only
//        Trace("Registering script declarations");
//        foreach (var statement in compilationUnit.Declarations)
//        {
//            switch (statement)
//            {
//                case FunctionDeclaration functionDeclaration:
//                    {
//                        if (!Scope.TryDeclareFunction(functionDeclaration))
//                        {
//                            Warning(functionDeclaration, $"Ignoring duplicate function declaration: {functionDeclaration}");
//                        }
//                        else
//                        {
//                            Trace($"Registered function declaration '{functionDeclaration}'");
//                        }
//                    }
//                    break;
//                case ProcedureDeclaration procedureDeclaration:
//                    {
//                        if (!Scope.TryDeclareProcedure(procedureDeclaration, out _))
//                        {
//                            Error(procedureDeclaration, $"Duplicate procedure declaration: {procedureDeclaration}");
//                            return false;
//                        }

//                        Trace($"Registered procedure declaration '{procedureDeclaration}'");

//                        if (procedureDeclaration.ReturnType.ValueKind != ValueKind.Void)
//                        {
//                            if (procedureDeclaration.ReturnType.ValueKind.GetBaseKind() == ValueKind.Int)
//                            {
//                                hasIntReturnValue = true;
//                            }
//                            else if (procedureDeclaration.ReturnType.ValueKind == ValueKind.Float)
//                            {
//                                hasFloatReturnValue = true;
//                            }
//                        }

//                        // Count parameter by type.
//                        short intParameterCount = 0;
//                        short floatParameterCount = 0;

//                        foreach (var parameter in procedureDeclaration.Parameters)
//                        {
//                            short count = 1;
//                            if (parameter.IsArray)
//                                count = (short)((ArrayParameter)parameter).Size;

//                            if (parameter.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//                                intParameterCount += count;
//                            else
//                                floatParameterCount += count;
//                        }

//                        maxIntParameterCount = Math.Max(intParameterCount, maxIntParameterCount);
//                        maxFloatParameterCount = Math.Max(floatParameterCount, maxFloatParameterCount);

//                        //if ( ProcedureHookMode == ProcedureHookMode.ImportedOnly )
//                        //{
//                        //    TryHookProcedure( procedureDeclaration.Identifier.Text );
//                        //}
//                    }
//                    break;

//                case VariableDeclaration variableDeclaration:
//                    {
//                        if (!TryRegisterVariableDeclaration(variableDeclaration, out _, out _))
//                        {
//                            Error(variableDeclaration, $"Duplicate variable declaration: {variableDeclaration}");
//                            return false;
//                        }
//                        Trace($"Registered variable declaration '{variableDeclaration}'");
//                    }
//                    break;

//                case EnumDeclaration enumDeclaration:
//                    {
//                        if (!Scope.TryDeclareEnum(enumDeclaration))
//                        {
//                            Error(enumDeclaration, $"Failed to declare enum: {enumDeclaration}");
//                            return false;
//                        }
//                    }
//                    break;
//            }
//        }
//        return true;
//    }

//    //
//    // Procedure code generation
//    //
//    private void InitializeProcedureCompilationState(ProcedureDeclaration declaration)
//    {
//        mProcedureDeclaration = declaration;
//        mInstructions = new List<KismetExpression>();
//        mLabels = new Dictionary<string, LabelInfo>();
//    }

//    private bool TryCompileProcedure(ProcedureDeclaration declaration, out KismetScriptFunction procedure)
//    {
//        Info(declaration, $"Compiling procedure: {declaration.Identifier.Text}");

//        // Initialize procedure to null so we can return without having to set it explicitly
//        procedure = null;

//        // Compile procedure body
//        if (!TryEmitProcedureBody(declaration))
//            return false;

//        // Create labels
//        if (!TryResolveProcedureLabels(out var labels))
//            return false;

//        // Create the procedure object
//        procedure = new KismetScriptFunction(declaration.Identifier.Text, mInstructions, labels);

//        return true;
//    }

//    private bool TryEmitProcedureBody(ProcedureDeclaration declaration)
//    {
//        Trace(declaration.Body, $"Emitting procedure body for {declaration}");

//        // Initialize some state
//        InitializeProcedureCompilationState(declaration);

//        // Emit procedure start  
//        PushScope();

//        // Register / forward declare labels in procedure body before codegen
//        Trace(declaration.Body, "Forward declaring labels in procedure body");
//        if (!TryRegisterLabels(declaration.Body))
//        {
//            Error(declaration.Body, "Failed to forward declare labels in procedure body");
//            return false;
//        }

//        // Emit procedure parameters
//        if (declaration.Parameters.Count > 0)
//        {
//            Trace(declaration, "Emitting code for procedure parameters");
//            if (!TryEmitProcedureParameters(declaration.Parameters))
//            {
//                Error(declaration, "Failed to emit procedure parameters");
//                return false;
//            }
//        }

//        ReturnStatement returnStatement = new ReturnStatement();

//        // Remove last return statement
//        if (declaration.Body.Statements.Count != 0 && declaration.Body.Statements.Last() is ReturnStatement)
//        {
//            returnStatement = (ReturnStatement)declaration.Body.Last();
//            declaration.Body.Statements.Remove(returnStatement);
//        }

//        // Emit procedure body
//        Trace(declaration.Body, "Emitting code for procedure body");
//        if (!TryEmitStatements(declaration.Body))
//        {
//            Error(declaration.Body, "Failed to emit procedure body");
//            return false;
//        }

//        //// Assign out parameters
//        //if (declaration.Parameters.Count > 0)
//        //{
//        //    var intVariableCount = 0;
//        //    var floatVariableCount = 0;

//        //    foreach (var parameter in declaration.Parameters)
//        //    {
//        //        Scope.TryGetVariable(parameter.Identifier.Text, out var variable);

//        //        if (parameter.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//        //        {
//        //            if (parameter.Modifier == ParameterModifier.Out)
//        //            {
//        //                Emit(Instruction.PUSHLIX(variable.Index));
//        //                Emit(Instruction.POPLIX((short)(startIntArgumentVariableIndex + intVariableCount)));
//        //            }

//        //            ++intVariableCount;
//        //        }
//        //        else
//        //        {
//        //            if (parameter.Modifier == ParameterModifier.Out)
//        //            {
//        //                Emit(Instruction.PUSHLFX(variable.Index));
//        //                Emit(Instruction.POPLFX((short)(startFloatArgumentVariableIndex + floatVariableCount)));
//        //            }

//        //            ++floatVariableCount;
//        //        }
//        //    }
//        //}

//        if (!TryEmitReturnStatement(returnStatement))
//        {
//            return false;
//        }

//        PopScope();

//        return true;
//    }

//    private bool TryEmitProcedureParameters(List<Parameter> parameters)
//    {
//        int intArgumentCount = 0;
//        int floatArgumentCount = 0;

//        foreach (var parameter in parameters)
//        {
//            Trace(parameter, $"Emitting parameter: {parameter}");

//            // Create declaration
//            VariableDeclaration declaration;
//            int count = 1;

//            if (!parameter.IsArray)
//            {
//                declaration = new VariableDeclaration(
//                    new VariableModifier(VariableModifierKind.Local),
//                    parameter.Type,
//                    parameter.Identifier,
//                    null);
//            }
//            else
//            {
//                count = ((ArrayParameter)parameter).Size;

//                declaration = new ArrayVariableDeclaration(
//                    new VariableModifier(VariableModifierKind.Local),
//                    parameter.Type,
//                    parameter.Identifier,
//                    count,
//                    null);
//            }

//            // Declare variable
//            if (!TryEmitVariableDeclaration(declaration, out var index))
//                return false;

//            // Push argument value
//            for (int i = 0; i < count; i++)
//            {
//                throw new NotImplementedException();
//                //if (declaration.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//                //{
//                //    if (parameter.Modifier != ParameterModifier.Out)
//                //        Emit(Instruction.PUSHLIX(mNextIntArgumentVariableIndex));

//                //    ++mNextIntArgumentVariableIndex;
//                //    ++intArgumentCount;
//                //}
//                //else
//                //{
//                //    if (parameter.Modifier != ParameterModifier.Out)
//                //        Emit(Instruction.PUSHLFX(mNextFloatArgumentVariableIndex));

//                //    ++mNextFloatArgumentVariableIndex;
//                //    ++floatArgumentCount;
//                //}

//                if (parameter.Modifier != ParameterModifier.Out)
//                {
//                    // Assign parameter with argument value
//                    if (!TryEmitVariableAssignment(declaration, (short)(index + i)))
//                        return false;
//                }
//            }
//        }

//        return true;
//    }

//    private bool TryRegisterLabels(CompoundStatement body)
//    {
//        foreach (var declaration in body.Select(x => x as Declaration).Where(x => x != null))
//        {
//            if (declaration.DeclarationType == DeclarationType.Label)
//            {
//                mLabels[declaration.Identifier.Text] = CreateLabel(declaration.Identifier.Text);
//            }
//        }

//        foreach (var statement in body)
//        {
//            switch (statement)
//            {
//                case IfStatement ifStatement:
//                    if (!TryRegisterLabels(ifStatement.Body))
//                        return false;

//                    if (ifStatement.ElseBody != null)
//                    {
//                        if (!TryRegisterLabels(ifStatement.ElseBody))
//                            return false;
//                    }
//                    break;

//                default:
//                    break;
//            }
//        }

//        return true;
//    }

//    private bool TryResolveProcedureLabels(out List<KismetScriptLabel> labels)
//    {
//        Trace("Resolving labels in procedure");
//        if (mLabels.Values.Any(x => !x.IsResolved))
//        {
//            foreach (var item in mLabels.Values.Where(x => !x.IsResolved))
//                mLogger.Error($"Label '{item.Name}' is referenced but not declared");

//            mLogger.Error("Failed to compile procedure because one or more undeclared labels are referenced");
//            labels = null;
//            return false;
//        }

//        labels = mLabels.Values
//            .Select(x => new KismetScriptLabel(x.Name, x.InstructionIndex))
//            .ToList();

//        mLabels.Clear();
//        return true;
//    }

//    //
//    // Statements
//    //
//    private bool TryEmitStatements(IEnumerable<Statement> statements)
//    {
//        foreach (var statement in statements)
//        {
//            if (!TryEmitStatement(statement))
//                return false;
//        }

//        return true;
//    }

//    private bool TryEmitCompoundStatement(CompoundStatement compoundStatement)
//    {
//        PushScope();

//        if (!TryEmitStatements(compoundStatement))
//            return false;

//        PopScope();

//        return true;
//    }

//    private bool TryEmitStatement(Statement statement)
//    {
//        switch (statement)
//        {
//            case CompoundStatement compoundStatement:
//                if (!TryEmitCompoundStatement(compoundStatement))
//                    return false;
//                break;
//            case Declaration _:
//                {
//                    if (statement is VariableDeclaration variableDeclaration)
//                    {
//                        if (!TryEmitVariableDeclaration(variableDeclaration, out _))
//                            return false;
//                    }
//                    else if (statement is LabelDeclaration labelDeclaration)
//                    {
//                        if (!TryRegisterLabelDeclaration(labelDeclaration))
//                            return false;
//                    }
//                    else
//                    {
//                        Error(statement, "Expected variable or label declaration");
//                        return false;
//                    }

//                    break;
//                }

//            case Expression expression:
//                if (!TryEmitExpression(expression, true))
//                    return false;
//                break;
//            case IfStatement ifStatement:
//                if (!TryEmitIfStatement(ifStatement))
//                    return false;
//                break;
//            case ForStatement forStatement:
//                if (!TryEmitForStatement(forStatement))
//                    return false;
//                break;
//            case WhileStatement whileStatement:
//                if (!TryEmitWhileStatement(whileStatement))
//                    return false;
//                break;
//            case BreakStatement breakStatement:
//                if (!TryEmitBreakStatement(breakStatement))
//                    return false;
//                break;
//            case ContinueStatement continueStatement:
//                if (!TryEmitContinueStatement(continueStatement))
//                    return false;
//                break;
//            case ReturnStatement returnStatement:
//                if (!TryEmitReturnStatement(returnStatement))
//                {
//                    Error(returnStatement, $"Failed to compile return statement: {returnStatement}");
//                    return false;
//                }

//                break;
//            case GotoStatement gotoStatement:
//                if (!TryEmitGotoStatement(gotoStatement))
//                {
//                    Error(gotoStatement, $"Failed to compile goto statement: {gotoStatement}");
//                    return false;
//                }

//                break;
//            case SwitchStatement switchStatement:
//                if (!TryEmitSwitchStatement(switchStatement))
//                {
//                    Error(switchStatement, $"Failed to compile switch statement: {switchStatement}");
//                    return false;
//                }

//                break;
//            default:
//                Error(statement, $"Compiling statement '{statement}' not implemented");
//                return false;
//        }

//        return true;
//    }

//    private bool TryRegisterVariableDeclaration(VariableDeclaration declaration, out short index, out bool byReference)
//    {
//        Trace(declaration, $"Registering variable declaration: {declaration}");

//        // Get variable index
//        byReference = false;
//        index = -1;
//        if (declaration.IsArray && declaration.Initializer != null)
//        {
//            var identifier = declaration.Initializer as Identifier;
//            VariableInfo variable = null;
//            if (identifier != null && Scope.TryGetVariable(identifier.Text, out variable) && variable.Declaration.IsArray)
//            {
//                byReference = true;
//                index = variable!.Index;
//            }
//        }

//        if (!byReference)
//        {
//            //if (!TryGetVariableIndex(declaration, out index))
//            //{
//            //    Error(declaration, $"Failed to get index for variable '{declaration}'");
//            //    return false;
//            //}
//        }

//        // Declare variable in scope
//        short size = 1;
//        if (declaration.IsArray)
//            size = (short)((ArrayVariableDeclaration)declaration).Size;

//        if (!Scope.TryDeclareVariable(declaration, index, size))
//        {
//            Error(declaration, $"Variable '{declaration}' has already been declared");
//            return false;
//        }

//        return true;
//    }

//    private bool TryEmitVariableDeclaration(VariableDeclaration declaration, out short index)
//    {
//        Trace(declaration, $"Emitting variable declaration: {declaration}");

//        // Register variable
//        if (!TryRegisterVariableDeclaration(declaration, out index, out var byReference))
//        {
//            Error(declaration, "Failed to register variable declaration");
//            index = -1;
//            return false;
//        }

//        // Nothing to emit for constants
//        if (declaration.Modifier.Kind == VariableModifierKind.Constant)
//        {
//            index = -1;
//            return true;
//        }

//        // Emit the variable initializer if it has one         
//        if (!byReference && declaration.Initializer != null)
//        {
//            Trace(declaration.Initializer, "Emitting variable initializer");

//            if (!TryEmitVariableAssignment(declaration.Identifier, declaration.Initializer, true))
//            {
//                Error(declaration.Initializer, "Failed to emit code for variable initializer");
//                index = -1;
//                return false;
//            }
//        }

//        return true;
//    }

//    private bool TryRegisterLabelDeclaration(LabelDeclaration declaration)
//    {
//        Trace(declaration, $"Registering label declaration: {declaration}");

//        // register label
//        if (!mLabels.TryGetValue(declaration.Identifier.Text, out var label))
//        {
//            Error(declaration.Identifier, $"Unexpected declaration of an registered label: '{declaration}'");
//            return false;
//        }

//        ResolveLabel(label);

//        return true;
//    }

//    //
//    // Expressions
//    //
//    private bool TryEmitExpression(Expression expression, bool isStatement)
//    {
//        switch (expression)
//        {
//            case SubscriptOperator subscriptOperator:
//                {
//                    if (isStatement)
//                    {
//                        Error(subscriptOperator, "A subscript is an invalid statement");
//                        return false;
//                    }

//                    if (!TryEmitSubscriptOperator(subscriptOperator))
//                        return false;
//                }
//                break;

//            case MemberAccessExpression memberAccessExpression:
//                if (isStatement)
//                {
//                    Error(memberAccessExpression, "A member access is an invalid statement");
//                    return false;
//                }

//                if (!TryEmitMemberAccess(memberAccessExpression))
//                    return false;
//                break;

//            case CallOperator callExpression:
//                if (!TryEmitCall(callExpression, isStatement))
//                    return false;
//                break;
//            case UnaryExpression unaryExpression:
//                if (!TryEmitUnaryExpression(unaryExpression, isStatement))
//                    return false;
//                break;
//            case BinaryExpression binaryExpression:
//                if (!TryEmitBinaryExpression(binaryExpression, isStatement))
//                    return false;
//                break;
//            case Identifier identifier:
//                if (isStatement)
//                {
//                    Error(identifier, "An identifier is an invalid statement");
//                    return false;
//                }

//                if (!TryEmitPushVariableValue(identifier))
//                    return false;
//                break;
//            case BoolLiteral boolLiteral:
//                if (isStatement)
//                {
//                    Error(boolLiteral, "A boolean literal is an invalid statement");
//                    return false;
//                }

//                EmitPushBoolLiteral(boolLiteral);
//                break;
//            case IntLiteral intLiteral:
//                if (isStatement)
//                {
//                    Error(intLiteral, "A integer literal is an invalid statement");
//                    return false;
//                }

//                EmitPushIntLiteral(intLiteral);
//                break;
//            case FloatLiteral floatLiteral:
//                if (isStatement)
//                {
//                    Error(floatLiteral, "A float literal is an invalid statement");
//                    return false;
//                }

//                EmitPushFloatLiteral(floatLiteral);
//                break;
//            case StringLiteral stringLiteral:
//                if (isStatement)
//                {
//                    Error(stringLiteral, "A string literal is an invalid statement");
//                    return false;
//                }

//                EmitPushStringLiteral(stringLiteral);
//                break;
//            default:
//                Error(expression, $"Compiling expression '{expression}' not implemented");
//                return false;
//        }

//        return true;
//    }

//    private bool TryEmitSubscriptOperator(SubscriptOperator subscriptOperator)
//    {
//        throw new NotImplementedException();

//        //Trace(subscriptOperator, $"Emitting subscript '{subscriptOperator}'");

//        //if (!Scope.TryGetVariable(subscriptOperator.Operand.Text, out var variable))
//        //{
//        //    Error($"Referenced undeclared variable '{subscriptOperator.Operand.Text}'");
//        //    return false;
//        //}

//        //if (!variable.Declaration.IsArray)
//        //{
//        //    Error($"Subscript operator is not valid for non-array variables: '{subscriptOperator}'");
//        //    return false;
//        //}

//        //InitializerList arrayInitializer = variable.Declaration.Initializer as InitializerList;

//        //if (subscriptOperator.Index is IntLiteral intLiteral)
//        //{
//        //    // Known index
//        //    Expression initializer = null;
//        //    if (arrayInitializer != null)
//        //        initializer = arrayInitializer.Expressions[intLiteral];

//        //    if (!TryEmitPushVariableValue(variable.Declaration.Modifier, variable.Declaration.Type.ValueKind,
//        //                                    variable.GetArrayElementIndex(intLiteral),
//        //                                    initializer))
//        //    {
//        //        return false;
//        //    }
//        //}
//        //else
//        //{
//        //    // Unknown index
//        //    // Start emitting subscript code
//        //    var endLabel = CreateLabel($"SubscriptEndLabel");
//        //    for (int i = 0; i < variable.Size; i++)
//        //    {
//        //        var falseLabel = CreateLabel($"SubscriptIfNot{i}");

//        //        // Emit current index
//        //        EmitPushIntLiteral(i);

//        //        // Emit index expression
//        //        if (!TryEmitExpression(subscriptOperator.Index, false))
//        //            return false;

//        //        // Emit equals instruction (index == i)
//        //        Emit(Instruction.EQ());

//        //        // Check if index == i
//        //        Emit(Instruction.IF(falseLabel.Index));
//        //        {
//        //            // Fetch initializer from array initializer if one was supplied
//        //            Expression initializer = null;
//        //            if (arrayInitializer != null)
//        //            {
//        //                initializer = arrayInitializer.Expressions[i];
//        //            }

//        //            // Push the value of array[index]
//        //            if (!TryEmitPushVariableValue(variable.Declaration.Modifier, variable.Declaration.Type.ValueKind, variable.GetArrayElementIndex(i),
//        //                                            initializer))
//        //            {
//        //                return false;
//        //            }

//        //            // Jump to the end of the subscript code
//        //            Emit(Instruction.GOTO(endLabel.Index));
//        //        }

//        //        // Resolve the label for when the condition is not met
//        //        ResolveLabel(falseLabel);
//        //    }

//        //    // Resolve the end of the subscript code label
//        //    ResolveLabel(endLabel);
//        //}

//        //return true;
//    }

//    private bool TryEmitMemberAccess(MemberAccessExpression memberAccessExpression)
//    {
//        Trace(memberAccessExpression, $"Emitting member access '{memberAccessExpression}'");

//        if (!Scope.TryGetEnum(memberAccessExpression.Operand.Text, out var enumType))
//        {
//            Error($"Referenced undeclared enum '{memberAccessExpression.Operand.Text}'");
//            return false;
//        }

//        if (!enumType.Members.TryGetValue(memberAccessExpression.Member.Text, out var value))
//        {
//            Error($"Referenced undeclared enum member '{memberAccessExpression.Member.Text}' in enum '{memberAccessExpression.Operand.Text}'");
//            return false;
//        }

//        if (!TryEmitExpression(value, false))
//        {
//            Error($"Failed to emit enum value '{value}'");
//            return false;
//        }

//        return true;
//    }

//    private bool TryEmitCall(CallOperator callExpression, bool isStatement)
//    {
//        Trace(callExpression, $"Emitting call: {callExpression}");

//        //if (mRootScope.TryGetFunction(callExpression.Identifier.Text, out var function))
//        //{
//        //    var libFunc = Library.KismetScriptModules.SelectMany(x => x.Functions).FirstOrDefault(x => x.Name == function.Declaration.Identifier.Text);

//        //    // Add default values
//        //    var foundDefaultValue = false;
//        //    for (var i = 0; i < function.Declaration.Parameters.Count; i++)
//        //    {
//        //        var param = function.Declaration.Parameters[i];
//        //        if (param.DefaultVaue == null)
//        //        {
//        //            if (foundDefaultValue)
//        //            {
//        //                Error($"Invalid library function definition: found parameter without default value after parameter with default value");
//        //                return false;
//        //            }
//        //        }
//        //        else
//        //        {
//        //            // Insert default values
//        //            foundDefaultValue = true;

//        //            if (i + 1 > callExpression.Arguments.Count)
//        //            {
//        //                // Add default value if not explicitly specified
//        //                callExpression.Arguments.Add(new Argument(param.DefaultVaue));
//        //            }
//        //        }
//        //    }

//        //    if (callExpression.Arguments.Count != function.Declaration.Parameters.Count)
//        //    {
//        //        // Check if function is marked variadic
//        //        if (libFunc == null || libFunc.Semantic != KismetScriptModuleFunctionSemantic.Variadic)
//        //        {
//        //            Error($"Function '{function.Declaration}' expects {function.Declaration.Parameters.Count} arguments but {callExpression.Arguments.Count} are given");
//        //            return false;
//        //        }
//        //    }

//        //    // Check MessageScript function call semantics
//        //    if (mScript.MessageScript != null && libFunc != null)
//        //    {
//        //        for (int i = 0; i < libFunc.Parameters.Count; i++)
//        //        {
//        //            var semantic = libFunc.Parameters[i].Semantic;
//        //            if (semantic != KismetScriptModuleParameterSemantic.MsgId &&
//        //                 semantic != KismetScriptModuleParameterSemantic.SelId)
//        //                continue;

//        //            var arg = callExpression.Arguments[i];
//        //            if (!(arg.Expression is IntLiteral argInt))
//        //            {
//        //                // only check constants for now
//        //                // TODO: evaluate expressions
//        //                continue;
//        //            }

//        //            var index = argInt.Value;
//        //            if (index < 0 || index >= mScript.MessageScript.Dialogs.Count)
//        //            {
//        //                Error($"Function call to {callExpression.Identifier.Text} references dialog that doesn't exist (index: {index})");
//        //                return false;
//        //            }

//        //            var expectedDialogKind = semantic == KismetScriptModuleParameterSemantic.MsgId
//        //                ? DialogKind.Message
//        //                : DialogKind.Selection;

//        //            var dialog = mScript.MessageScript.Dialogs[index];
//        //            if (dialog.Kind != expectedDialogKind)
//        //            {
//        //                Error($"Function call to {callExpression.Identifier.Text} doesn't reference a {expectedDialogKind} dialog, got dialog of type: {dialog.Kind} index: {index}");
//        //                return false;
//        //            }
//        //        }
//        //    }

//        //    if (EnableFunctionCallTracing)
//        //    {
//        //        TraceFunctionCall(function.Declaration);
//        //    }

//        //    if (function.Declaration.Parameters.Count > 0)
//        //    {
//        //        if (!TryEmitFunctionCallArguments(callExpression))
//        //            return false;
//        //    }

//        //    // call function
//        //    Emit(Instruction.COMM(function.Index));

//        //    if (!isStatement)
//        //    {
//        //        if (function.Declaration.ReturnType.ValueKind == ValueKind.Void)
//        //        {
//        //            Error(callExpression, $"Void-returning function '{function.Declaration}' used in expression");
//        //            return false;
//        //        }

//        //        if (!EnableFunctionCallTracing)
//        //        {
//        //            // push return value of function
//        //            Trace(callExpression, $"Emitting PUSHREG for {callExpression}");
//        //            Emit(Instruction.PUSHREG());
//        //        }
//        //        else
//        //        {
//        //            TraceFunctionCallReturnValue(function.Declaration);
//        //        }
//        //    }
//        //}
//        //else if (mRootScope.TryGetProcedure(callExpression.Identifier.Text, out var procedure))
//        //{
//        //    if (!TryEmitProcedureCall(callExpression, isStatement, procedure))
//        //        return false;
//        //}
//        if (IsIntrinsicFunction(callExpression.Identifier.Text))
//        {
//            TryEmitFunctionCallArguments(callExpression);

//            switch (callExpression.Identifier.Text)
//            {
//                case "EX_ComputedJump":
//                    Emit(new EX_ComputedJump() { CodeOffsetExpression = scope.ins})
//                    break;

//                default:
//                    Error(callExpression, $"Unhandled instrinsic function: {callExpression.Identifier}");
//                    return false;
//            }
//        }
//        else
//        {
//            Error(callExpression, $"Invalid call expression. Expected function or procedure identifier, got: {callExpression.Identifier}");
//            return false;
//        }

//        return true;
//    }

//    private bool IsIntrinsicFunction(string name)
//        => typeof(EExprToken).GetEnumNames().Contains(name);

//    private void AddCompiledProcedure(KismetScriptFunction compiledProcedure)
//    {
//        mRootScope.TryGetProcedure(compiledProcedure.Name, out var procedureInfo);
//        AddCompiledProcedure(procedureInfo, compiledProcedure);
//    }

//    private void AddCompiledProcedure(ProcedureInfo procedure, KismetScriptFunction compiledProcedure)
//    {
//        while (procedure.Index >= mScript.Functions.Count)
//            mScript.Functions.Add(null);

//        mScript.Functions[procedure.Index] = compiledProcedure;
//        procedure.Compiled = compiledProcedure;
//    }

//    private bool TryEmitProcedureCall(CallOperator callExpression, bool isStatement, ProcedureInfo procedure)
//    {
//        throw new NotImplementedException();

//        //if (callExpression.Arguments.Count != procedure.Declaration.Parameters.Count)
//        //{
//        //    Error($"Procedure '{procedure.Declaration}' expects {procedure.Declaration.Parameters.Count} arguments but {callExpression.Arguments.Count} are given");
//        //    return false;
//        //}

//        //if (!TryEmitParameterCallArguments(callExpression, procedure.Declaration, out var parameterIndices))
//        //    return false;

//        //// call procedure
//        //Emit(Instruction.CALL(procedure.Index));

//        //// Emit out parameter assignments
//        //for (int i = 0; i < procedure.Declaration.Parameters.Count; i++)
//        //{
//        //    var parameter = procedure.Declaration.Parameters[i];
//        //    if (parameter.Modifier != ParameterModifier.Out)
//        //        continue;

//        //    // Copy value of local variable copy of out parameter to actual out parameter
//        //    var index = parameterIndices[i];
//        //    var identifier = (Identifier)callExpression.Arguments[i].Expression;
//        //    if (!Scope.TryGetVariable(identifier.Text, out var variable))
//        //        return false;

//        //    if (variable.Declaration.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//        //    {
//        //        Emit(Instruction.PUSHLIX(index));
//        //        Emit(Instruction.POPLIX(variable.Index));
//        //    }
//        //    else
//        //    {
//        //        Emit(Instruction.PUSHLFX(index));
//        //        Emit(Instruction.POPLFX(variable.Index));
//        //    }

//        //}

//        //// Emit return value
//        //if (!isStatement)
//        //{
//        //    if (procedure.Declaration.ReturnType.ValueKind == ValueKind.Void)
//        //    {
//        //        Error($"Void-returning procedure '{procedure.Declaration}' used in expression");
//        //        return false;
//        //    }

//        //    if (!EnableProcedureCallTracing)
//        //    {
//        //        // Push return value of procedure
//        //        if (procedure.Declaration.ReturnType.ValueKind.GetBaseKind() == ValueKind.Int)
//        //            Emit(Instruction.PUSHLIX(mIntReturnValueVariable.Index));
//        //        else
//        //            Emit(Instruction.PUSHLFX(mFloatReturnValueVariable.Index));
//        //    }
//        //    else
//        //    {
//        //        TraceProcedureCallReturnValue(procedure.Declaration);
//        //    }
//        //}

//        //return true;
//    }

//    private bool TryEmitFunctionCallArguments(CallOperator callExpression)
//    {
//        Trace("Emitting function call arguments");

//        for (int i = 0; i < callExpression.Arguments.Count; ++i)
//        {
//            if (!TryEmitExpression(callExpression.Arguments[i].Expression, false))
//            {
//                Error(callExpression.Arguments[i], $"Failed to compile function call argument: {callExpression.Arguments[i]}");
//                return false;
//            }
//        }

//        return true;
//    }

//    private bool TryEmitParameterCallArguments(CallOperator callExpression, ProcedureDeclaration declaration, out List<short> argumentIndices)
//    {
//        throw new NotImplementedException();

//        //Trace("Emitting parameter call arguments");

//        //int intArgumentCount = 0;
//        //int floatArgumentCount = 0;
//        //argumentIndices = new List<short>();

//        //for (int i = 0; i < callExpression.Arguments.Count; i++)
//        //{
//        //    var argument = callExpression.Arguments[i];
//        //    var parameter = declaration.Parameters[i];

//        //    if (!parameter.IsArray)
//        //    {
//        //        if (argument.Modifier != ArgumentModifier.Out)
//        //        {
//        //            if (!TryEmitExpression(argument.Expression, false))
//        //            {
//        //                Error(callExpression.Arguments[i], $"Failed to compile function call argument: {argument}");
//        //                return false;
//        //            }
//        //        }

//        //        // Assign each required argument variable
//        //        if (parameter.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//        //        {
//        //            if (argument.Modifier != ArgumentModifier.Out)
//        //                Emit(Instruction.POPLIX(mNextIntArgumentVariableIndex));

//        //            argumentIndices.Add(mNextIntArgumentVariableIndex);

//        //            ++mNextIntArgumentVariableIndex;
//        //            ++intArgumentCount;
//        //        }
//        //        else
//        //        {
//        //            if (argument.Modifier != ArgumentModifier.Out)
//        //                Emit(Instruction.POPLFX(mNextFloatArgumentVariableIndex));

//        //            argumentIndices.Add(mNextFloatArgumentVariableIndex);

//        //            ++mNextFloatArgumentVariableIndex;
//        //            ++floatArgumentCount;
//        //        }
//        //    }
//        //    else
//        //    {
//        //        var identifier = argument.Expression as Identifier;
//        //        if (identifier == null)
//        //        {
//        //            Error(argument, "Expected array variable identifier");
//        //            return false;
//        //        }

//        //        if (!Scope.TryGetVariable(identifier.Text, out var variable))
//        //        {
//        //            Error(argument, $"Referenced undefined variable: {variable}");
//        //            return false;
//        //        }

//        //        if (!variable.Declaration.IsArray)
//        //        {
//        //            Error(argument, "Expected array variable");
//        //            return false;
//        //        }

//        //        // Copy array
//        //        var count = ((ArrayParameter)parameter).Size;
//        //        for (int j = 0; j < count; j++)
//        //        {
//        //            if (argument.Modifier != ArgumentModifier.Out)
//        //            {
//        //                if (!TryEmitPushVariableValue(variable.Declaration.Modifier, variable.Declaration.Type.ValueKind, variable.GetArrayElementIndex(j), null))
//        //                {
//        //                    Error(callExpression.Arguments[i], $"Failed to compile function call argument: {argument}");
//        //                    return false;
//        //                }
//        //            }

//        //            // Assign each required argument array variable, essentially copying the entire array
//        //            if (parameter.Type.ValueKind.GetBaseKind() == ValueKind.Int)
//        //            {
//        //                if (argument.Modifier != ArgumentModifier.Out)
//        //                    Emit(Instruction.POPLIX(mNextIntArgumentVariableIndex));

//        //                if (j == 0)
//        //                    argumentIndices.Add(mNextIntArgumentVariableIndex);

//        //                ++mNextIntArgumentVariableIndex;
//        //                ++intArgumentCount;
//        //            }
//        //            else
//        //            {
//        //                if (argument.Modifier != ArgumentModifier.Out)
//        //                    Emit(Instruction.POPLFX(mNextFloatArgumentVariableIndex));

//        //                if (j == 0)
//        //                    argumentIndices.Add(mNextFloatArgumentVariableIndex);

//        //                ++mNextFloatArgumentVariableIndex;
//        //                ++floatArgumentCount;
//        //            }
//        //        }
//        //    }
//        //}

//        return true;
//    }

//    private bool TryEmitUnaryExpression(UnaryExpression unaryExpression, bool isStatement)
//    {
//        Trace(unaryExpression, $"Emitting unary expression: {unaryExpression}");

//        switch (unaryExpression)
//        {
//            case PostfixOperator postfixOperator:
//                if (!TryEmitPostfixOperator(postfixOperator, isStatement))
//                {
//                    Error(postfixOperator, "Failed to emit postfix operator");
//                    return false;
//                }
//                break;

//            case PrefixOperator prefixOperator:
//                if (!TryEmitPrefixOperator(prefixOperator, isStatement))
//                {
//                    Error(prefixOperator, "Failed to emit prefix operator");
//                    return false;
//                }
//                break;

//            default:
//                Error(unaryExpression, $"Emitting unary expression '{unaryExpression}' not implemented");
//                return false;
//        }

//        return true;
//    }

//    private bool TryEmitPostfixOperator(PostfixOperator postfixOperator, bool isStatement)
//    {
//        throw new NotImplementedException();

//        //var identifier = (Identifier)postfixOperator.Operand;
//        //if (!Scope.TryGetVariable(identifier.Text, out var variable))
//        //{
//        //    Error(identifier, $"Reference to undefined variable: {identifier}");
//        //    return false;
//        //}

//        //short index;
//        //if (variable.Declaration.Type.ValueKind != ValueKind.Float)
//        //{
//        //    index = mNextIntVariableIndex++;
//        //}
//        //else
//        //{
//        //    index = mNextFloatVariableIndex++;
//        //}

//        //VariableInfo copy = null;
//        //if (!isStatement)
//        //{
//        //    // Make copy of variable
//        //    copy = Scope.GenerateVariable(variable.Declaration.Type.ValueKind, index);

//        //    // Push value of the variable to save in the copy
//        //    if (!TryEmitPushVariableValue(identifier))
//        //    {
//        //        Error(identifier, $"Failed to push variable value to copy variable: {identifier}");
//        //        return false;
//        //    }

//        //    // Assign the copy with the value of the variable
//        //    if (!TryEmitVariableAssignment(copy.Declaration.Identifier))
//        //    {
//        //        Error($"Failed to emit variable assignment to copy variable: {copy}");
//        //        return false;
//        //    }
//        //}

//        //// In/decrement the actual variable
//        //{
//        //    // Push 1
//        //    Emit(Instruction.PUSHIS(1));

//        //    // Push value of the variable
//        //    if (!TryEmitPushVariableValue(identifier))
//        //    {
//        //        Error(identifier, $"Failed to push variable value to copy variable: {identifier}");
//        //        return false;
//        //    }

//        //    // Subtract or add
//        //    if (postfixOperator is PostfixDecrementOperator)
//        //    {
//        //        Emit(Instruction.SUB());
//        //    }
//        //    else if (postfixOperator is PostfixIncrementOperator)
//        //    {
//        //        Emit(Instruction.ADD());
//        //    }
//        //    else
//        //    {
//        //        return false;
//        //    }

//        //    // Emit assignment with calculated value
//        //    if (!TryEmitVariableAssignment(identifier))
//        //    {
//        //        Error(identifier, $"Failed to emit variable assignment: {identifier}");
//        //        return false;
//        //    }
//        //}

//        //if (!isStatement)
//        //{
//        //    // Push the value of the copy
//        //    Trace($"Pushing variable value: {copy.Declaration.Identifier}");

//        //    if (!TryEmitPushVariableValue(copy.Declaration.Identifier))
//        //    {
//        //        Error($"Failed to push value for copy variable {copy}");
//        //        return false;
//        //    }
//        //}

//        //return true;
//    }

//    private bool TryEmitPrefixOperator(PrefixOperator prefixOperator, bool isStatement)
//    {
//        throw new NotImplementedException();

//        //switch (prefixOperator)
//        //{
//        //    case LogicalNotOperator _:
//        //    case NegationOperator _:
//        //        if (isStatement)
//        //        {
//        //            Error(prefixOperator, "A logical not operator is an invalid statement");
//        //            return false;
//        //        }

//        //        if (!TryEmitExpression(prefixOperator.Operand, false))
//        //        {
//        //            Error(prefixOperator.Operand, "Failed to emit operand for unary expression");
//        //            return false;
//        //        }

//        //        if (prefixOperator is LogicalNotOperator)
//        //        {
//        //            Trace(prefixOperator, "Emitting NOT");
//        //            Emit(Instruction.NOT());
//        //        }
//        //        else if (prefixOperator is NegationOperator)
//        //        {
//        //            Trace(prefixOperator, "Emitting MINUS");
//        //            Emit(Instruction.MINUS());
//        //        }
//        //        else
//        //        {
//        //            goto default;
//        //        }
//        //        break;

//        //    case PrefixDecrementOperator _:
//        //    case PrefixIncrementOperator _:
//        //        {
//        //            // Push 1
//        //            Emit(Instruction.PUSHIS(1));

//        //            // Push value
//        //            var identifier = (Identifier)prefixOperator.Operand;
//        //            if (!TryEmitPushVariableValue(identifier))
//        //            {
//        //                Error(identifier, $"Failed to emit variable value for: {identifier}");
//        //                return false;
//        //            }

//        //            // Emit operation
//        //            if (prefixOperator is PrefixDecrementOperator)
//        //            {
//        //                Emit(Instruction.SUB());
//        //            }
//        //            else if (prefixOperator is PrefixIncrementOperator)
//        //            {
//        //                Emit(Instruction.ADD());
//        //            }
//        //            else
//        //            {
//        //                goto default;
//        //            }

//        //            // Emit assignment
//        //            if (!TryEmitVariableAssignment(identifier))
//        //            {
//        //                Error(prefixOperator, $"Failed to emit variable assignment: {prefixOperator}");
//        //                return false;
//        //            }

//        //            if (!isStatement)
//        //            {
//        //                Trace(prefixOperator, $"Emitting variable value: {identifier}");

//        //                if (!TryEmitPushVariableValue(identifier))
//        //                {
//        //                    Error(identifier, $"Failed to emit variable value for: {identifier}");
//        //                    return false;
//        //                }
//        //            }
//        //        }
//        //        break;

//        //    default:
//        //        Error(prefixOperator, $"Unknown prefix operator: {prefixOperator}");
//        //        return false;
//        //}

//        return true;
//    }

//    private bool TryEmitBinaryExpression(BinaryExpression binaryExpression, bool isStatement)
//    {
//        throw new NotImplementedException();

//        //Trace(binaryExpression, $"Emitting binary expression: {binaryExpression}");

//        //if (binaryExpression is AssignmentOperatorBase assignment)
//        //{
//        //    if (!TryEmitVariableAssignmentBase(assignment, isStatement))
//        //    {
//        //        Error(assignment, $"Failed to emit variable assignment: {assignment}");
//        //        return false;
//        //    }
//        //}
//        //else
//        //{
//        //    if (isStatement)
//        //    {
//        //        Error(binaryExpression, "A binary operator is not a valid statement");
//        //        return false;
//        //    }

//        //    Trace("Emitting value for binary expression");

//        //    if (binaryExpression is ModulusOperator modulusOperator)
//        //    {
//        //        // This one is special
//        //        if (!TryEmitModulusOperator(modulusOperator))
//        //        {
//        //            Error(binaryExpression.Right, $"Failed to emit modulus expression: {binaryExpression.Left}");
//        //            return false;
//        //        }
//        //    }
//        //    else
//        //    {
//        //        if (!TryEmitExpression(binaryExpression.Right, false))
//        //        {
//        //            Error(binaryExpression.Right, $"Failed to emit right expression: {binaryExpression.Left}");
//        //            return false;
//        //        }

//        //        if (!TryEmitExpression(binaryExpression.Left, false))
//        //        {
//        //            Error(binaryExpression.Right, $"Failed to emit left expression: {binaryExpression.Right}");
//        //            return false;
//        //        }

//        //        switch (binaryExpression)
//        //        {
//        //            case AdditionOperator _:
//        //                Emit(Instruction.ADD());
//        //                break;
//        //            case SubtractionOperator _:
//        //                Emit(Instruction.SUB());
//        //                break;
//        //            case MultiplicationOperator _:
//        //                Emit(Instruction.MUL());
//        //                break;
//        //            case DivisionOperator _:
//        //                Emit(Instruction.DIV());
//        //                break;
//        //            case LogicalOrOperator _:
//        //                Emit(Instruction.OR());
//        //                break;
//        //            case LogicalAndOperator _:
//        //                Emit(Instruction.AND());
//        //                break;
//        //            case EqualityOperator _:
//        //                Emit(Instruction.EQ());
//        //                break;
//        //            case NonEqualityOperator _:
//        //                Emit(Instruction.NEQ());
//        //                break;
//        //            case LessThanOperator _:
//        //                Emit(Instruction.S());
//        //                break;
//        //            case GreaterThanOperator _:
//        //                Emit(Instruction.L());
//        //                break;
//        //            case LessThanOrEqualOperator _:
//        //                Emit(Instruction.SE());
//        //                break;
//        //            case GreaterThanOrEqualOperator _:
//        //                Emit(Instruction.LE());
//        //                break;
//        //            default:
//        //                Error(binaryExpression, $"Emitting binary expression '{binaryExpression}' not implemented");
//        //                return false;
//        //        }
//        //    }
//        //}

//        return true;
//    }

//    private bool TryEmitModulusOperator(ModulusOperator modulusOperator)
//    {
//        var value = modulusOperator.Left;
//        var number = modulusOperator.Right;

//        if (!TryEmitModulus(value, number))
//            return false;

//        return true;
//    }

//    private bool TryEmitModulus(Expression value, Expression number)
//    {
//        throw new NotImplementedException();

//        //// value % number turns into
//        //// value - ( ( value / number ) * value )

//        //// push number for multiplication
//        //if (!TryEmitExpression(number, false))
//        //    return false;

//        //// value / number
//        //if (!TryEmitExpression(number, false))
//        //    return false;

//        //if (!TryEmitExpression(value, false))
//        //    return false;

//        //Emit(Instruction.DIV());

//        //// *= number
//        //Emit(Instruction.MUL());

//        //// value - ( ( value / number ) * number )
//        //if (!TryEmitExpression(value, false))
//        //    return false;

//        //Emit(Instruction.SUB());

//        //// Result value is on stack
//        //return true;
//    }

//    private bool TryEmitPushVariableValue(Identifier identifier)
//    {
//        Trace(identifier, $"Emitting variable reference: {identifier}");

//        if (!Scope.TryGetVariable(identifier.Text, out var variable))
//        {
//            Error(identifier, $"Referenced undeclared variable '{identifier}'");
//            return false;
//        }

//        if (!TryEmitPushVariableValue(variable.Declaration.Modifier, variable.Declaration.Type.ValueKind, variable.Index,
//                                        variable.Declaration.Initializer))
//        {
//            return false;
//        }

//        return true;
//    }

//    private bool TryEmitPushVariableValue(VariableModifier modifier, ValueKind valueKind, short index, Expression initializer)
//    {
//        throw new NotImplementedException();

//        //if (modifier == null || modifier.Kind == VariableModifierKind.Local)
//        //{
//        //    if (valueKind != ValueKind.Float)
//        //        Emit(Instruction.PUSHLIX(index));
//        //    else
//        //        Emit(Instruction.PUSHLFX(index));
//        //}
//        //else if (modifier.Kind == VariableModifierKind.Global)
//        //{
//        //    if (valueKind != ValueKind.Float)
//        //        Emit(Instruction.PUSHIX(index));
//        //    else
//        //        Emit(Instruction.PUSHIF(index));
//        //}
//        //else if (modifier.Kind == VariableModifierKind.Constant)
//        //{
//        //    if (!TryEmitExpression(initializer, false))
//        //    {
//        //        Error(initializer, $"Failed to emit value for constant expression: {initializer}");
//        //        return false;
//        //    }
//        //}
//        //else if (modifier.Kind == VariableModifierKind.AiLocal)
//        //{
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.AiGetLocalFunctionIndex)); // AI_GET_LOCAL_PARAM
//        //    Emit(Instruction.PUSHREG());
//        //}
//        //else if (modifier.Kind == VariableModifierKind.AiGlobal)
//        //{
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.AiGetGlobalFunctionIndex)); // AI_GET_GLOBAL
//        //    Emit(Instruction.PUSHREG());
//        //}
//        //else if (modifier.Kind == VariableModifierKind.Bit)
//        //{
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.BitCheckFunctionIndex)); // BIT_CHK
//        //    Emit(Instruction.PUSHREG());
//        //}
//        //else if (modifier.Kind == VariableModifierKind.Count)
//        //{
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.GetCountFunctionIndex)); // GET_COUNT
//        //    Emit(Instruction.PUSHREG());
//        //}
//        //else
//        //{
//        //    Error(modifier, "Unsupported variable modifier type");
//        //    return false;
//        //}

//        //return true;
//    }

//    private bool TryEmitVariableAssignmentBase(AssignmentOperatorBase assignment, bool isStatement)
//    {
//        if (assignment is CompoundAssignmentOperator compoundAssignment)
//        {
//            if (!TryEmitVariableCompoundAssignment(compoundAssignment, isStatement))
//            {
//                Error(compoundAssignment, $"Failed to emit compound assignment: {compoundAssignment}");
//                return false;
//            }
//        }
//        else
//        {
//            if (assignment.Left is Identifier identifier)
//            {
//                if (!TryEmitVariableAssignment(identifier, assignment.Right, isStatement))
//                {
//                    Error(assignment, $"Failed to emit assignment: {assignment}");
//                    return false;
//                }
//            }
//            else if (assignment.Left is SubscriptOperator subscriptOperator)
//            {
//                if (!TryEmitSubscriptAssignment(assignment, isStatement))
//                {
//                    Error(subscriptOperator, $"Failed to emit subscript: {subscriptOperator}");
//                    return false;
//                }
//            }
//            else
//            {
//                Error(assignment, $"Failed to emit assignment: {assignment}");
//                return false;
//            }
//        }

//        return true;
//    }

//    private bool TryEmitSubscriptAssignment(AssignmentOperatorBase assignmentOperator, bool isStatement)
//    {
//        throw new NotImplementedException();
//        //var subscriptOperator = assignmentOperator.Left as SubscriptOperator;
//        //if (!Scope.TryGetVariable(subscriptOperator.Operand.Text, out var variable))
//        //{
//        //    Error(assignmentOperator, $"Reference to undefined variable '{subscriptOperator.Operand.Text}'");
//        //    return false;
//        //}

//        //if (!TryEmitExpression(assignmentOperator.Right, false))
//        //{
//        //    Error(assignmentOperator, "Invalid expression");
//        //    return false;
//        //}

//        //if (subscriptOperator.Index is IntLiteral intLiteral)
//        //{
//        //    // Known index
//        //    if (!TryEmitVariableAssignment(variable.Declaration, variable.GetArrayElementIndex(intLiteral)))
//        //        return false;
//        //}
//        //else
//        //{
//        //    // Unknown index
//        //    // Start emitting subscript code
//        //    var endLabel = CreateLabel($"SubscriptAssignmentEndLabel");
//        //    for (int i = 0; i < variable.Size; i++)
//        //    {
//        //        var falseLabel = CreateLabel($"SubscriptAssignmentIfNot{i}");

//        //        // Emit current index
//        //        EmitPushIntLiteral(i);

//        //        // Emit index expression
//        //        if (!TryEmitExpression(subscriptOperator.Index, false))
//        //            return false;

//        //        // Emit equals instruction (index == i)
//        //        Emit(Instruction.EQ());

//        //        // Check if index == i
//        //        Emit(Instruction.IF(falseLabel.Index));
//        //        {
//        //            // Assign value
//        //            if (!TryEmitVariableAssignment(variable.Declaration, variable.GetArrayElementIndex(i)))
//        //                return false;

//        //            // Jump to the end of the subscript code
//        //            Emit(Instruction.GOTO(endLabel.Index));
//        //        }

//        //        // Resolve the label for when the condition is not met
//        //        ResolveLabel(falseLabel);
//        //    }

//        //    // Resolve the end of the subscript code label
//        //    ResolveLabel(endLabel);
//        //}

//        //if (!isStatement)
//        //    TryEmitExpression(assignmentOperator.Right, false);

//        //return true;
//    }

//    private bool TryEmitVariableCompoundAssignment(CompoundAssignmentOperator compoundAssignment, bool isStatement)
//    {
//        throw new NotImplementedException();

//        //Trace(compoundAssignment, $"Emitting compound assignment: {compoundAssignment}");

//        //var identifier = compoundAssignment.Left as Identifier;
//        //if (identifier == null)
//        //{
//        //    Error(compoundAssignment, $"Expected assignment to variable: {compoundAssignment}");
//        //    return false;
//        //}

//        //if (compoundAssignment is ModulusAssignmentOperator _)
//        //{
//        //    // Special treatment because it doesnt have an instruction
//        //    if (!TryEmitModulus(compoundAssignment.Left, compoundAssignment.Right))
//        //    {
//        //        Error(compoundAssignment, $"Failed to emit modulus assignment operator: {compoundAssignment}");
//        //        return false;
//        //    }
//        //}
//        //else
//        //{
//        //    // Push value of right expression
//        //    if (!TryEmitExpression(compoundAssignment.Right, false))
//        //    {
//        //        Error(compoundAssignment.Right, $"Failed to emit expression: {compoundAssignment.Right}");
//        //        return false;
//        //    }

//        //    // Push value of variable
//        //    if (!TryEmitPushVariableValue(identifier))
//        //    {
//        //        Error(identifier, $"Failed to emit variable value for: {identifier}");
//        //        return false;
//        //    }

//        //    // Emit operation
//        //    switch (compoundAssignment)
//        //    {
//        //        case AdditionAssignmentOperator _:
//        //            Emit(Instruction.ADD());
//        //            break;

//        //        case SubtractionAssignmentOperator _:
//        //            Emit(Instruction.SUB());
//        //            break;

//        //        case MultiplicationAssignmentOperator _:
//        //            Emit(Instruction.MUL());
//        //            break;

//        //        case DivisionAssignmentOperator _:
//        //            Emit(Instruction.DIV());
//        //            break;

//        //        default:
//        //            Error(compoundAssignment, $"Unknown compound assignment type: {compoundAssignment}");
//        //            return false;
//        //    }
//        //}

//        //// Assign the value to the variable
//        //if (!TryEmitVariableAssignment(identifier))
//        //{
//        //    Error(identifier, $"Failed to assign value to variable: {identifier}");
//        //    return false;
//        //}

//        //if (!isStatement)
//        //{
//        //    Trace(compoundAssignment, $"Pushing variable value: {identifier}");

//        //    // Push value of variable
//        //    if (!TryEmitPushVariableValue(identifier))
//        //    {
//        //        Error(identifier, $"Failed to emit variable value for: {identifier}");
//        //        return false;
//        //    }
//        //}

//        //return true;
//    }


//    /// <summary>
//    /// Emit variable assignment with an explicit expression.
//    /// </summary>
//    /// <param name="identifier"></param>
//    /// <param name="expression"></param>
//    /// <returns></returns>
//    private bool TryEmitVariableAssignment(Identifier identifier, Expression expression, bool isStatement)
//    {
//        Trace($"Emitting variable assignment: {identifier} = {expression}");

//        if (expression is InitializerList initializerList)
//        {
//            if (!Scope.TryGetVariable(identifier.Text, out var variable))
//                return false;

//            if (!variable.Declaration.IsArray)
//                return false;

//            if (initializerList.Expressions.Count != variable.Size)
//            {
//                Error(initializerList, "Size of initializer list does not match size of declaration");
//                return false;
//            }

//            // Assign each array element with its value
//            for (int i = 0; i < initializerList.Expressions.Count; i++)
//            {
//                var expr = initializerList.Expressions[i];
//                var index = variable.GetArrayElementIndex(i);

//                if (!TryEmitExpression(expr, false))
//                {
//                    Error(expression, "Failed to emit code for assigment value expression");
//                    return false;
//                }

//                if (!TryEmitVariableAssignment(variable.Declaration, index))
//                {
//                    Error(identifier, "Failed to emit code for value assignment to variable");
//                    return false;
//                }
//            }
//        }
//        else
//        {
//            if (!TryEmitExpression(expression, false))
//            {
//                Error(expression, "Failed to emit code for assigment value expression");
//                return false;
//            }

//            if (!TryEmitVariableAssignment(identifier))
//            {
//                Error(identifier, "Failed to emit code for value assignment to variable");
//                return false;
//            }

//            if (!isStatement)
//            {
//                // Push value of variable
//                Trace(identifier, $"Pushing variable value: {identifier}");

//                if (!TryEmitPushVariableValue(identifier))
//                {
//                    Error(identifier, $"Failed to emit variable value for: {identifier}");
//                    return false;
//                }
//            }
//        }

//        return true;
//    }

//    /// <summary>
//    /// Emit variable assignment without explicit expression.
//    /// </summary>
//    /// <param name="identifier"></param>
//    /// <returns></returns>
//    private bool TryEmitVariableAssignment(Identifier identifier)
//    {
//        if (!Scope.TryGetVariable(identifier.Text, out var variable))
//        {
//            Error(identifier, $"Assignment to undeclared variable: {identifier}");
//            return false;
//        }

//        if (!TryEmitVariableAssignment(variable.Declaration, variable.Index))
//            return false;

//        return true;
//    }

//    private bool TryEmitVariableAssignment(VariableDeclaration declaration, short index)
//    {
//        throw new NotImplementedException();

//        //// load the value into the variable
//        //if (declaration.Modifier == null || declaration.Modifier.Kind == VariableModifierKind.Local)
//        //{
//        //    if (declaration.Type.ValueKind != ValueKind.Float)
//        //        Emit(Instruction.POPLIX(index));
//        //    else
//        //        Emit(Instruction.POPLFX(index));
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.Global)
//        //{
//        //    if (declaration.Type.ValueKind != ValueKind.Float)
//        //        Emit(Instruction.POPIX(index));
//        //    else
//        //        Emit(Instruction.POPFX(index));
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.Constant)
//        //{
//        //    Error(declaration.Identifier, "Illegal assignment to constant");
//        //    return false;
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.AiLocal)
//        //{
//        //    // implicit pop of value
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.AiSetLocalFunctionIndex)); // AI_SET_LOCAL_PARAM
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.AiGlobal)
//        //{
//        //    // implicit pop of value
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.AiSetGlobalFunctionIndex)); // AI_SET_GLOBAL
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.Bit)
//        //{
//        //    var falseLabel = CreateLabel("BitAssignmentIfFalse");
//        //    var endLabel = CreateLabel("BitAssignmentIfEnd");

//        //    // implicit pop of value
//        //    Emit(Instruction.IF(falseLabel.Index));
//        //    {
//        //        // Value assigned is true
//        //        Emit(Instruction.PUSHIS(index));
//        //        Emit(Instruction.COMM(mInstrinsic.BitOnFunctionIndex)); // BIT_ON
//        //        Emit(Instruction.GOTO(endLabel.Index));
//        //    }
//        //    // Else
//        //    {
//        //        // Value assigned is false
//        //        ResolveLabel(falseLabel);
//        //        Emit(Instruction.PUSHIS(index));
//        //        Emit(Instruction.COMM(mInstrinsic.BitOffFunctionIndex)); // BIT_OFF
//        //        Emit(Instruction.GOTO(endLabel.Index));
//        //    }
//        //    ResolveLabel(endLabel);
//        //}
//        //else if (declaration.Modifier.Kind == VariableModifierKind.Count)
//        //{
//        //    // implicit pop of value
//        //    Emit(Instruction.PUSHIS(index));
//        //    Emit(Instruction.COMM(mInstrinsic.SetCountFunctionIndex)); // SET_COUNT
//        //}
//        //else
//        //{
//        //    Error(declaration.Identifier, $"Unsupported variable modifier type: {declaration.Modifier}");
//        //    return false;
//        //}

//        //return true;
//    }

//    //
//    // Literal values
//    //
//    private void EmitPushBoolLiteral(BoolLiteral boolLiteral)
//    {
//        Trace(boolLiteral, $"Pushing bool literal: {boolLiteral}");

//        if (boolLiteral.Value)
//            Emit(new EX_True());
//        else
//            Emit(new EX_False());
//    }

//    private void EmitPushIntLiteral(IntLiteral intLiteral)
//    {
//        Trace(intLiteral, $"Pushing int literal: {intLiteral}");

//        // Original scripts never use negative literals
//        // so if our literal is negative, we make it positive
//        // and later negative it using the negation operator
//        var value = intLiteral.Value;
//        var sign = 1;
//        if (value < 0)
//        {
//            sign = -1;
//            value = -value;
//        }

//        Emit(new EX_IntConst() { Value = value * sign });
//    }

//    private void EmitPushFloatLiteral(FloatLiteral floatLiteral)
//    {
//        Trace(floatLiteral, $"Pushing float literal: {floatLiteral}");

//        // Original scripts never use negative literals
//        // so if our literal is negative, we make it positive
//        // and later negative it using the negation operator
//        var value = floatLiteral.Value;
//        var sign = 1;
//        if (value < 0)
//        {
//            sign = -1;
//            value = -value;
//        }

//        Emit(new EX_FloatConst() { Value = value * sign } );
//    }

//    private void EmitPushStringLiteral(StringLiteral stringLiteral)
//    {
//        Trace(stringLiteral, $"Pushing string literal: {stringLiteral}");

//        Emit(new EX_StringConst() { Value = stringLiteral.Value });
//    }

//    private bool IntFitsInShort(int value)
//    {
//        return ((value & 0xffff8000) + 0x8000 & 0xffff7fff) == 0;
//    }

//    // 
//    // If statement
//    //
//    private bool TryEmitIfStatement(IfStatement ifStatement)
//    {
//        Trace(ifStatement, $"Emitting if statement: '{ifStatement}'");

//        // emit condition expression, which should push a boolean value to the stack
//        if (!TryEmitExpression(ifStatement.Condition, false))
//        {
//            Error(ifStatement.Condition, "Failed to emit if statement condition");
//            return false;
//        }

//        // create else label
//        LabelInfo elseLabel = null;
//        if (ifStatement.ElseBody != null)
//            elseLabel = CreateLabel("IfElseLabel");

//        // generate label for jump if condition is false
//        var endLabel = CreateLabel("IfEndLabel");

//        // emit if instruction that jumps to the label if the condition is false
//        if (ifStatement.ElseBody == null)
//        {
//            Emit(new EX_Jump { CodeOffset = (uint)endLabel.InstructionIndex });
//        }
//        else
//        {
//            Emit(new EX_Jump { CodeOffset = (uint)endLabel.InstructionIndex });
//        }

//        // compile if body
//        if (ifStatement.ElseBody == null)
//        {
//            // If there's no else, then the end of the body will line up with the end label
//            if (!TryEmitIfStatementBody(ifStatement.Body, null))
//                return false;
//        }
//        else
//        {
//            // If there's an else body, then the end of the body will line up with the else label, but it should line up with the end label
//            if (!TryEmitIfStatementBody(ifStatement.Body, endLabel))
//                return false;
//        }

//        if (ifStatement.ElseBody != null)
//        {
//            ResolveLabel(elseLabel);

//            // compile if else body
//            // The else body will always line up with the end label
//            if (!TryEmitIfStatementBody(ifStatement.ElseBody, endLabel))
//                return false;
//        }

//        ResolveLabel(endLabel);

//        return true;
//    }

//    private bool TryEmitIfStatementBody(CompoundStatement body, LabelInfo endLabel)
//    {
//        Trace(body, "Compiling if statement body");
//        if (!TryEmitCompoundStatement(body))
//        {
//            Error(body, "Failed to compile if statement body");
//            return false;
//        }

//        // ensure that we end up at the right position after the body
//        if (endLabel != null)
//            Emit(new EX_Jump { CodeOffset = (uint)endLabel.InstructionIndex });

//        return true;
//    }

//    // 
//    // If statement
//    //
//    private bool TryEmitForStatement(ForStatement forStatement)
//    {
//        Trace(forStatement, $"Emitting for statement: '{forStatement}'");

//        // Enter for scope
//        PushScope();

//        // Emit initializer
//        if (!TryEmitStatement(forStatement.Initializer))
//        {
//            Error(forStatement.Condition, "Failed to emit for statement initializer");
//            return false;
//        }

//        // Create labels
//        var conditionLabel = CreateLabel("ForConditionLabel");
//        var afterLoopLabel = CreateLabel("ForAfterLoopLabel");
//        var endLabel = CreateLabel("ForEndLabel");

//        // Emit condition check
//        {
//            ResolveLabel(conditionLabel);

//            // Emit condition
//            if (!TryEmitExpression(forStatement.Condition, false))
//            {
//                Error(forStatement.Condition, "Failed to emit for statement condition");
//                return false;
//            }

//            // Jump to the end of the loop if condition is NOT true
//            Emit(new EX_Jump { CodeOffset = (uint)endLabel.InstructionIndex });
//        }

//        // Emit body
//        {
//            // Allow break & continue
//            Scope.BreakLabel = endLabel;
//            Scope.ContinueLabel = afterLoopLabel;

//            // emit body
//            Trace(forStatement.Body, "Emitting for statement body");
//            if (!TryEmitCompoundStatement(forStatement.Body))
//            {
//                Error(forStatement.Body, "Failed to emit for statement body");
//                return false;
//            }
//        }

//        // Emit after loop
//        {
//            ResolveLabel(afterLoopLabel);

//            if (!TryEmitExpression(forStatement.AfterLoop, true))
//            {
//                Error(forStatement.AfterLoop, "Failed to emit for statement after loop expression");
//                return false;
//            }

//            // jump to condition check
//            Emit(new EX_Jump { CodeOffset = (uint)conditionLabel.InstructionIndex });
//        }

//        // We're at the end of the for loop
//        ResolveLabel(endLabel);

//        // Exit for scope
//        PopScope();

//        return true;
//    }

//    // 
//    // While statement
//    //
//    private bool TryEmitWhileStatement(WhileStatement whileStatement)
//    {
//        Trace(whileStatement, $"Emitting while statement: '{whileStatement}'");

//        // Create labels
//        var conditionLabel = CreateLabel("WhileConditionLabel");
//        var endLabel = CreateLabel("WhileEndLabel");

//        // Emit condition check
//        {
//            ResolveLabel(conditionLabel);

//            // compile condition expression, which should push a boolean value to the stack
//            if (!TryEmitExpression(whileStatement.Condition, false))
//            {
//                Error(whileStatement.Condition, "Failed to emit while statement condition");
//                return false;
//            }

//            // Jump to the end of the loop if condition is NOT true
//            Emit(new EX_Jump { CodeOffset = (uint)endLabel.InstructionIndex });
//        }

//        // Emit body
//        {
//            // Enter while body scope
//            PushScope();

//            // allow break & continue
//            Scope.BreakLabel = endLabel;
//            Scope.ContinueLabel = conditionLabel;

//            // emit body
//            Trace(whileStatement.Body, "Emitting while statement body");
//            if (!TryEmitCompoundStatement(whileStatement.Body))
//            {
//                Error(whileStatement.Body, "Failed to emit while statement body");
//                return false;
//            }

//            // jump to condition check
//            Emit(new EX_Jump { CodeOffset = (uint)conditionLabel.InstructionIndex });

//            // Exit while body scope
//            PopScope();
//        }

//        // We're at the end of the while loop
//        ResolveLabel(endLabel);

//        return true;
//    }

//    //
//    // Switch statement
//    //
//    private bool TryEmitSwitchStatement(SwitchStatement switchStatement)
//    {
//        Trace(switchStatement, $"Emitting switch statement: '{switchStatement}'");
//        PushScope();

//        var defaultLabel = switchStatement.Labels.SingleOrDefault(x => x is DefaultSwitchLabel);
//        if (switchStatement.Labels.Last() != defaultLabel)
//        {
//            switchStatement.Labels.Remove(defaultLabel);
//            switchStatement.Labels.Add(defaultLabel);
//        }

//        // Set up switch labels in the context for gotos
//        Scope.SwitchLabels = switchStatement.Labels
//                                            .Where(x => x is ConditionSwitchLabel)
//                                            .Select(x => ((ConditionSwitchLabel)x).Condition)
//                                            .ToDictionary(x => x, y => CreateLabel("SwitchConditionCaseBody"));

//        var conditionCaseBodyLabels = Scope.SwitchLabels.Values.ToList();

//        var defaultCaseBodyLabel = defaultLabel != null ? CreateLabel("SwitchDefaultCaseBody") : null;
//        Scope.SwitchLabels.Add(new NullExpression(), defaultCaseBodyLabel);

//        var switchEndLabel = CreateLabel("SwitchStatementEndLabel");
//        for (var i = 0; i < switchStatement.Labels.Count; i++)
//        {
//            var label = switchStatement.Labels[i];
//            if (label is ConditionSwitchLabel conditionLabel)
//            {
//                // Emit condition expression, which should push a boolean value to the stack
//                if (!TryEmitExpression(conditionLabel.Condition, false))
//                {
//                    Error(conditionLabel.Condition, "Failed to emit switch statement label condition");
//                    return false;
//                }

//                // emit switch on expression
//                if (!TryEmitExpression(switchStatement.SwitchOn, false))
//                {
//                    Error(switchStatement.SwitchOn, "Failed to emit switch statement condition");
//                    return false;
//                }

//                throw new NotImplementedException();

//                //// emit equality check, but check if it's not equal to jump to the body if it is
//                //Emit(Instruction.NEQ());

//                //// generate label for jump if condition is false
//                //var labelBodyLabel = conditionCaseBodyLabels[i];

//                //// emit if instruction that jumps to the body if the condition is met
//                //Emit(Instruction.IF(labelBodyLabel.Index));
//            }
//        }

//        if (defaultLabel != null)
//        {
//            // Emit body of default case first
//            Scope.BreakLabel = switchEndLabel;

//            // Resolve label that jumps to the default case body
//            ResolveLabel(defaultCaseBodyLabel);

//            // Emit default case body
//            Trace("Compiling switch statement label body");
//            if (!TryEmitStatements(defaultLabel.Body))
//            {
//                Error("Failed to compile switch statement label body");
//                return false;
//            }
//        }

//        // Emit other label bodies
//        for (var i = 0; i < switchStatement.Labels.Count; i++)
//        {
//            var label = switchStatement.Labels[i];

//            if (label is ConditionSwitchLabel)
//            {
//                // Resolve body label
//                var labelBodyLabel = conditionCaseBodyLabels[i];
//                ResolveLabel(labelBodyLabel);

//                // Break jumps to end of switch
//                Scope.BreakLabel = switchEndLabel;

//                // Emit body
//                Trace("Compiling switch statement label body");
//                if (!TryEmitStatements(label.Body))
//                {
//                    Error("Failed to compile switch statement label body");
//                    return false;
//                }
//            }
//        }

//        ResolveLabel(switchEndLabel);

//        PopScope();
//        return true;
//    }

//    //
//    // Control statements
//    //
//    private bool TryEmitBreakStatement(BreakStatement breakStatement)
//    {
//        if (!Scope.TryGetBreakLabel(out var label))
//        {
//            Error(breakStatement, "Break statement is invalid in this context");
//            return false;
//        }

//        Emit(new EX_Jump() { CodeOffset = (uint)label.InstructionIndex });

//        return true;
//    }

//    private bool TryEmitContinueStatement(ContinueStatement continueStatement)
//    {
//        if (!Scope.TryGetContinueLabel(out var label))
//        {
//            Error(continueStatement, "Continue statement is invalid in this context");
//            return false;
//        }

//        Emit(new EX_Jump() { CodeOffset = (uint)label.InstructionIndex });

//        return true;
//    }

//    private bool TryEmitReturnStatement(ReturnStatement returnStatement)
//    {
//        Trace(returnStatement, $"Emitting return statement: '{returnStatement}'");
//        throw new NotImplementedException();
//    }

//    private bool TryEmitGotoStatement(GotoStatement gotoStatement)
//    {
//        Trace(gotoStatement, $"Emitting goto statement: '{gotoStatement}'");

//        LabelInfo label = null;

//        switch (gotoStatement.Label)
//        {
//            case Identifier identifier:
//                if (!mLabels.TryGetValue(identifier.Text, out label))
//                {
//                    if (!Scope.TryGetLabel(identifier, out label))
//                    {
//                        Error(gotoStatement.Label, $"Goto statement referenced undeclared label: {identifier}");
//                        return false;
//                    }
//                }
//                break;

//            case Expression expression:
//                if (!Scope.TryGetLabel(expression, out label))
//                {
//                    Error(gotoStatement.Label, $"Goto statement referenced undeclared label: {expression}");
//                    return false;
//                }
//                break;
//        }

//        // emit goto
//        throw new NotImplementedException();
//        return true;
//    }

//    private void Emit(KismetExpression instruction)
//    {
//        // Emit instruction
//        mInstructions.Add(instruction);
//    }

//    private LabelInfo CreateLabel(string name)
//    {
//        var label = new LabelInfo();
//        label.Index = (short)mLabels.Count;
//        label.Name = name + "_" + mNextLabelIndex++;

//        mLabels.Add(label.Name, label);

//        return label;
//    }

//    private void ResolveLabel(LabelInfo label)
//    {
//        label.InstructionIndex = (short)mInstructions.Count;
//        label.IsResolved = true;

//        Trace($"Resolved label {label.Name} to instruction index {label.InstructionIndex}");
//    }

//    private void PushScope()
//    {
//        mScopeStack.Push(new ScopeContext(mScopeStack.Peek()));
//        Trace("Entered scope");
//    }

//    private ScopeContext PopScope()
//    {
//        //mNextIntVariableIndex -= ( short )Scope.Variables.Count( x => sTypeToBaseTypeMap[x.Value.Declaration.Type.ValueType] == KismetScriptValueType.Int );
//        //mNextFloatVariableIndex -= ( short )Scope.Variables.Count( x => sTypeToBaseTypeMap[x.Value.Declaration.Type.ValueType] == KismetScriptValueType.Float );
//        var result = mScopeStack.Pop();
//        Trace("Exited scope");
//        return result;
//    }

//    //
//    // Logging
//    //
//    private void Trace(SyntaxNode node, string message)
//    {
//        if (node.SourceInfo != null)
//            Trace($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
//        else
//            Trace(message);
//    }

//    private void Trace(string message)
//    {
//        mLogger.Trace($"{message}");
//    }

//    private void Info(SyntaxNode node, string message)
//    {
//        if (node.SourceInfo != null)
//            Info($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
//        else
//            Info(message);
//    }

//    private void Info(string message)
//    {
//        mLogger.Info($"{message}");
//    }

//    private void Error(SyntaxNode node, string message)
//    {
//        if (node.SourceInfo != null)
//            Error($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
//        else
//            Error(message);

//        //if ( Debugger.IsAttached )
//        //    Debugger.Break();
//    }

//    private void Error(string message)
//    {
//        mLogger.Error($"{message}");

//        //if ( Debugger.IsAttached )
//        //    Debugger.Break();
//    }

//    private void Warning(SyntaxNode node, string message)
//    {
//        if (node.SourceInfo != null)
//            Warning($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
//        else
//            Warning(message);
//    }

//    private void Warning(string message)
//    {
//        mLogger.Warning($"{message}");
//    }
//}
