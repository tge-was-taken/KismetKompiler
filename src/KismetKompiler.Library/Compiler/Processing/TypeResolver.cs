using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using System.Diagnostics;
using System.Xml.Linq;
using UAssetAPI.ExportTypes;

namespace KismetKompiler.Library.Compiler.Processing;

public class TypeResolver
{
    class FunctionState
    {
        public ProcedureDeclaration Declaration { get; set; }
        public string Name { get; set; }
        public List<PropertyExport> Parameters { get; init; } = new();
    }

    class TypeAnalysisError : Exception
    {
        public TypeAnalysisError(SyntaxNode syntaxNode, string message) : base(message)
        {

        }
    }

    public class DeclarationScope
    {
        private readonly DeclarationScope _parent;

        private readonly Dictionary<string, Declaration> _declarations;

        public DeclarationScope(DeclarationScope parent)
        {
            _parent = parent;
            _declarations = new Dictionary<string, Declaration>();
        }

        public bool IsDeclaredLocally(string name)
            => _declarations.ContainsKey(name);

        public bool IsDeclaredInScope(string name)
        {
            if (!IsDeclaredLocally(name))
                return _parent?.IsDeclaredInScope(name) ?? false;
            return true;
        }

        public bool TryRegisterDeclarationLocally(Declaration declaration)
        {
            if (IsDeclaredLocally(declaration.Identifier.Text))
                return false;

            _declarations[declaration.Identifier.Text] = declaration;
            return true;
        }

        public bool TryGetDeclarationLocally(string name, out Declaration? declaration)
            => _declarations.TryGetValue(name, out declaration);

        public bool TryGetDeclarationInScope(string name, out Declaration? declaration)
        {
            if (!_declarations.TryGetValue(name, out declaration))
                return _parent?.TryGetDeclarationInScope(name, out declaration) ?? false;
            return true;
        }
    }



    private Stack<DeclarationScope> _scopes;
    private DeclarationScope _rootScope;
    private Declaration _context;
    private DeclarationScope Scope => _scopes.Peek();

    public TypeResolver()
    {
        _scopes = new();
    }

    public void ResolveTypes(CompilationUnit compilationUnit)
    {
        ResolveTypesInCompilationUnit(compilationUnit);
    }

    private void ResolveTypesInCompilationUnit(CompilationUnit compilationUnit)
    {
        PushScope();

        foreach (var declaration in compilationUnit.Declarations)
            ResolveTypesInStatement(declaration);

        PopScope();
    }

    private void ResolveTypesInStatement(Statement statement)
    {
        if (statement is CompoundStatement compoundStatement)
        {
            ResolveTypesInCompoundStatement(compoundStatement);
        }
        else if (statement is Declaration declaration)
        {
            RegisterDeclaration(declaration);
            ResolveTypesInDeclaration(declaration);
        }
        else if (statement is Expression expression)
        {
            ResolveTypesInExpression(expression);
        }
        else if (statement is IfStatement ifStatement)
        {
            ResolveTypesInIfStatement(ifStatement);
        }
        else if (statement is ForStatement forStatement)
        {
            ResolveTypesInForStatement(forStatement);
        }
        else if (statement is WhileStatement whileStatement)
        {
            ResolveTypesInWhileStatement(whileStatement);
        }
        else if (statement is ReturnStatement returnStatement)
        {
            if (returnStatement.Value != null)
            {
                ResolveTypesInExpression(returnStatement.Value);
            }
        }
        else if (statement is GotoStatement gotoStatement)
        {
            gotoStatement.Label.ExpressionValueKind = ValueKind.Label;
        }
        else if (statement is SwitchStatement switchStatement)
        {
            ResolveTypesInSwitchStatement(switchStatement);
        }
        else if (statement is BreakStatement)
        {
            // Not an expression
        }
        else
        {
        }

        //_functionState = new()
        //{
        //    Declaration = declaration,
        //    Name = declaration.Identifier.Text,
        //};

        //var functionExport = _asset.Exports
        //    .FirstOrDefault(x => x.ObjectName.ToString() == declaration.Identifier.Text);
        //if (functionExport != null)
        //{
        //    // Function already exists in original asset
        //    var functionLocalExports = _asset.Exports
        //        .Where(x => x.OuterIndex.ToExport(_asset) == functionExport);
        //    var functionProperties = functionLocalExports.Where(x => x is PropertyExport).Cast<PropertyExport>();
        //    var functionPropertyParameters = functionProperties.Where(x => (x.Property.PropertyFlags & EPropertyFlags.CPF_Parm) != 0);
        //    var functionOutProperties = functionPropertyParameters.Where(x => (x.Property.PropertyFlags & EPropertyFlags.CPF_OutParm) != 0);
        //    var functionReturnProperties = functionPropertyParameters.Where(x => (x.Property.PropertyFlags & EPropertyFlags.CPF_ReturnParm) != 0);
        //}
        //else
        //{
        //    // Function does not yet exist in original asset
        //    // Additional context has to be provided in source code
        //    throw new NotImplementedException();
        //}
    }

    private void TryRegisterDeclarations(IEnumerable<Statement> statements)
    {
        foreach (var statement in statements)
        {
            if (statement is Declaration declaration)
            {
                RegisterDeclaration(declaration);
            }
        }
    }

    private void RegisterDeclaration(Declaration declaration)
    {
        if (!Scope.TryRegisterDeclarationLocally(declaration))
        {
            // Special case: forward declared declarations on top level
            //if (Scope.Parent != null)
            //{
            //    Scope.TryGetDeclaration(declaration.Identifier, out var existingDeclaration);
            //    LogError($"Identifier {declaration.Identifier} already defined as: {existingDeclaration}");
            //    return false;
            //}
            throw new TypeAnalysisError(declaration, "Failed to register declaration");
        }
    }

    private void ResolveTypesInCompoundStatement(CompoundStatement compoundStatement)
    {
        PushScope();

        foreach (var statement in compoundStatement)
        {
            ResolveTypesInStatement(statement);
        }

        PopScope();
    }

    private void ResolveTypesInDeclaration(Declaration declaration)
    {
        if (declaration.DeclarationType != DeclarationType.Label)
        {
            ResolveTypesInIdentifier(declaration.Identifier);
        }
        else
        {
            declaration.Identifier.ExpressionValueKind = ValueKind.Label;
        }

        if (declaration is ProcedureDeclaration procedureDeclaration)
        {
            ResolveTypesInProcedureDeclaration(procedureDeclaration);
        }
        else if (declaration is VariableDeclaration variableDeclaration)
        {
            ResolveTypesInVariableDeclaration(variableDeclaration);
        }
        else if (declaration is ClassDeclaration classDeclaration)
        {
            ResolveTypesInClassDeclaration(classDeclaration);
        }
    }

    private void ResolveTypesInClassDeclaration(ClassDeclaration classDeclaration)
    {
        foreach (var decl in classDeclaration.Declarations)
        {
            ResolveTypesInDeclaration(decl);
        }
    }

    internal void ResolveTypesInExpression(Expression expression)
    {
        if (expression is InitializerList initializerList)
        {
            foreach (var expr in initializerList.Expressions)
            {
                ResolveTypesInExpression(expr);
            }
        }
        else if (expression is NewExpression newExpression)
        {
            foreach (var expr in newExpression.Initializer)
            {
                ResolveTypesInExpression(expr);
            }
        }
        else if (expression is SubscriptOperator subscriptOperator)
        {
            expression.ExpressionValueKind = subscriptOperator.Operand.ExpressionValueKind;
            ResolveTypesInExpression(subscriptOperator.Index);
        }
        else if (expression is MemberExpression memberExpression)
        {
            ResolveTypesInExpression(memberExpression.Context);

            var contextIdentifier = memberExpression.Context as Identifier;
            if (contextIdentifier != null)
            {
                Scope.TryGetDeclarationInScope(contextIdentifier.Text, out var declaration);
                _context = declaration;
            }

            ResolveTypesInExpression(memberExpression.Member);
            memberExpression.ExpressionValueKind = memberExpression.Member.ExpressionValueKind;
            _context = null;
        }
        else if (expression is CallOperator callExpression)
        {
            ResolveTypesInCallExpression(callExpression);
        }
        else if (expression is CastOperator castOperator)
        {
            ResolveTypesInCastOperator(castOperator);
        }
        else if (expression is UnaryExpression unaryExpression)
        {
            ResolveTypesInExpression(unaryExpression.Operand);

            unaryExpression.ExpressionValueKind = unaryExpression.Operand.ExpressionValueKind;
        }
        else if (expression is BinaryExpression binaryExpression)
        {
            ResolveTypesInExpression(binaryExpression.Left);

            ResolveTypesInExpression(binaryExpression.Right);

            if (!(expression is EqualityOperator || expression is NonEqualityOperator ||
                 expression is GreaterThanOperator || expression is GreaterThanOrEqualOperator ||
                 expression is LessThanOperator || expression is LessThanOrEqualOperator ||
                 expression is LogicalAndOperator || expression is LogicalOrOperator))
            {
                binaryExpression.ExpressionValueKind = binaryExpression.Left.ExpressionValueKind;
            }
        }
        else if (expression is ConditionalExpression conditionalExpression)
        {
            conditionalExpression.Condition.ExpressionValueKind = ValueKind.Bool;
            ResolveTypesInExpression(conditionalExpression.ValueIfTrue);
            ResolveTypesInExpression(conditionalExpression.ValueIfFalse);
            conditionalExpression.ExpressionValueKind = conditionalExpression.ValueIfTrue.ExpressionValueKind;
        }
        else if (expression is Identifier identifier)
        {
            ResolveTypesInIdentifier(identifier);
        }
        else if (expression is Literal literal)
        {
            // No processing necessary
        }
        else
        {
            if (expression.ExpressionValueKind == ValueKind.Unresolved)
            {
                LogError(expression, $"Unresolved expression: {expression}");
            }
        }

        LogTrace(expression, $"Resolved expression {expression} to type {expression.ExpressionValueKind}");

    }

    private void ResolveTypesInIfStatement(IfStatement ifStatement)
    {
        ResolveTypesInExpression(ifStatement.Condition);

        ResolveTypesInCompoundStatement(ifStatement.Body);

        if (ifStatement.ElseBody != null)
        {
            ResolveTypesInCompoundStatement(ifStatement.ElseBody);
        }

    }

    private void ResolveTypesInForStatement(ForStatement forStatement)
    {
        // Enter for scope
        PushScope();

        // For loop Initializer
        ResolveTypesInStatement(forStatement.Initializer);

        // For loop Condition
        ResolveTypesInExpression(forStatement.Condition);

        // For loop After loop expression
        ResolveTypesInExpression(forStatement.AfterLoop);

        // For loop Body
        ResolveTypesInCompoundStatement(forStatement.Body);

        // Exit for scope
        PopScope();

    }

    private void ResolveTypesInWhileStatement(WhileStatement whileStatement)
    {
        // Resolve types in while statement condition
        ResolveTypesInExpression(whileStatement.Condition);

        // Resolve types in body
        ResolveTypesInCompoundStatement(whileStatement.Body);

    }

    private void ResolveTypesInSwitchStatement(SwitchStatement switchStatement)
    {
        ResolveTypesInExpression(switchStatement.SwitchOn);

        foreach (var label in switchStatement.Labels)
        {
            if (label is ConditionSwitchLabel conditionLabel)
            {
                ResolveTypesInExpression(conditionLabel.Condition);
            }

            foreach (var statement in label.Body)
            {
                ResolveTypesInStatement(statement);
            }
        }

    }

    // Declarations
    private void ResolveTypesInProcedureDeclaration(ProcedureDeclaration declaration)
    {
        LogInfo(declaration, $"Resolving types in procedure '{declaration.Identifier.Text}'");

        // Nothing to resolve if there's no body
        if (declaration.Body == null)
            return;

        // Enter procedure body scope
        PushScope();

        foreach (var parameter in declaration.Parameters)
        {
            var parameterDeclaration = new VariableDeclaration(
                VariableModifier.Local,
                parameter.Type,
                parameter.Identifier,
                null);

            RegisterDeclaration(parameterDeclaration);
        }

        ResolveTypesInCompoundStatement(declaration.Body);

        // Exit procedure body scope
        PopScope();

    }

    private void ResolveTypesInVariableDeclaration(VariableDeclaration declaration)
    {
        ResolveTypesInTypeIdentifier(declaration.Type);
        if (declaration.Initializer != null)
            ResolveTypesInExpression(declaration.Initializer);

    }

    private void ResolveTypesInTypeIdentifier(TypeIdentifier typeIdentifier)
    {
        switch (typeIdentifier.Text)
        {
            case "byte":
                typeIdentifier.ValueKind = ValueKind.Byte;
                break;
            case "bool":
                typeIdentifier.ValueKind = ValueKind.Bool;
                break;
            case "int":
                typeIdentifier.ValueKind = ValueKind.Int;
                break;
            case "float":
                typeIdentifier.ValueKind = ValueKind.Float;
                break;
            case "double":
                typeIdentifier.ValueKind = ValueKind.Double;
                break;
            case "string":
                typeIdentifier.ValueKind = ValueKind.String;
                break;
            case "Enum":
                typeIdentifier.ValueKind = ValueKind.Int;
                break;
            case "Interface":
                typeIdentifier.ValueKind = ValueKind.Interface;
                break;
            case "Struct":
                typeIdentifier.ValueKind = ValueKind.Struct;
                break;
            case "Vector2D":
                typeIdentifier.ValueKind = ValueKind.Vector2D;
                break;
            case "TimeSpan":
                typeIdentifier.ValueKind = ValueKind.TimeSpan;
                break;
            case "DateTime":
                typeIdentifier.ValueKind = ValueKind.DateTime;
                break;
            case "Vector":
                typeIdentifier.ValueKind = ValueKind.Vector;
                break;
            case "LinearColor":
                typeIdentifier.ValueKind = ValueKind.LinearColor;
                break;
            case "Rotator":
                typeIdentifier.ValueKind = ValueKind.Rotator;
                break;
            case "Transform":
                typeIdentifier.ValueKind = ValueKind.Transform;
                break;
            case "Name":
                typeIdentifier.ValueKind = ValueKind.Name;
                break;
            case "Class":
                typeIdentifier.ValueKind = ValueKind.Class;
                break;
            case "Object":
                typeIdentifier.ValueKind = ValueKind.Object;
                break;
            default:
                break;
        }

    }

    // Expressions

    private void ResolveTypesInCastOperator(CastOperator castOperator)
    {
        castOperator.ExpressionValueKind = castOperator.TypeIdentifier.ValueKind;
        ResolveTypesInExpression(castOperator.Operand);
    }

    private void ResolveTypesInCallExpression(CallOperator callExpression)
    {
        if (!Scope.TryGetDeclarationInScope(callExpression.Identifier.Text, out var declaration))
        {
            // Disable for now because we import functions at compile time
            //LogWarning( callExpression, $"Call expression references undeclared identifier '{callExpression.Identifier.Value}'" );
        }

        if (declaration is FunctionDeclaration functionDeclaration)
        {
            callExpression.ExpressionValueKind = functionDeclaration.ReturnType.ValueKind;
            callExpression.Identifier.ExpressionValueKind = ValueKind.Function;
        }
        else if (declaration is ProcedureDeclaration procedureDeclaration)
        {
            callExpression.ExpressionValueKind = procedureDeclaration.ReturnType.ValueKind;
            callExpression.Identifier.ExpressionValueKind = ValueKind.Procedure;
        }

        foreach (var arg in callExpression.Arguments)
        {
            if (arg is OutDeclarationArgument outArg)
            {
                var decl = new VariableDeclaration()
                {
                    Identifier = outArg.Identifier,
                    SourceInfo = outArg.SourceInfo,
                    Type = outArg.Type
                };
                ResolveTypesInDeclaration(decl);
                Scope.TryRegisterDeclarationLocally(decl);
            }

            ResolveTypesInExpression(arg.Expression);
        }

    }

    private void ResolveTypesInIdentifier(Identifier identifier)
    {
        bool isUndeclared = false;
        if (!Scope.TryGetDeclarationInScope(identifier.Text, out var declaration))
        {
            if (_context != null)
            {
                if (_context is EnumDeclaration enumContext)
                {
                    identifier.ExpressionValueKind = ValueKind.Int;
                }
                else
                {
                    // TODO
                }
            }

            LogInfo(identifier, $"Identifiers references undeclared identifier '{identifier.Text}'. Is this a compile time variable?");
            isUndeclared = true;
        }

        if (declaration is FunctionDeclaration)
        {
            identifier.ExpressionValueKind = ValueKind.Function;
        }
        else if (declaration is ProcedureDeclaration)
        {
            identifier.ExpressionValueKind = ValueKind.Procedure;
        }
        else if (declaration is VariableDeclaration variableDeclaration)
        {
            identifier.ExpressionValueKind = variableDeclaration.Type.ValueKind;
        }
        else if (declaration is LabelDeclaration)
        {
            identifier.ExpressionValueKind = ValueKind.Label;
        }
        else if (declaration is EnumDeclaration)
        {
            identifier.ExpressionValueKind = ValueKind.Void;
        }
        else if (declaration is ClassDeclaration)
        {
            identifier.ExpressionValueKind = ValueKind.Class;
        }
        else if (!isUndeclared)
        {
            LogWarning(identifier, "Expected function, procedure, variable or label identifier");
        }

    }

    private void PushScope()
    {
        if (_scopes.Count != 0)
        {
            _scopes.Push(new DeclarationScope(Scope));
        }
        else
        {
            _rootScope = new DeclarationScope(null);
            _scopes.Push(_rootScope);
        }
    }

    private void PopScope()
    {
        _scopes.Pop();
    }

    private void LogTrace(SyntaxNode node, string message)
    {
        // if (node.SourceInfo != null)
        //     LogTrace($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
        // else
        //     LogTrace(message);
    }

    private void LogTrace(string message)
    {
        //Console.WriteLine(message);
    }

    private void LogInfo(string message)
    {
        // Console.WriteLine(message);
    }

    private void LogInfo(SyntaxNode node, string message)
    {
        //Console.WriteLine($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
    }

    private void LogError(SyntaxNode node, string message)
    {
        //if (node.SourceInfo != null)
        //     LogError($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
        // else
        //     LogError(message);
    }

    private void LogError(string message)
    {
        //Console.WriteLine($"{message}");
    }

    private void LogWarning(string message)
    {
        //Console.WriteLine($"{message}");
    }

    private void LogWarning(SyntaxNode node, string message)
    {
        //Console.WriteLine($"({node.SourceInfo.Line:D4}:{node.SourceInfo.Column:D4}) {message}");
    }
}
