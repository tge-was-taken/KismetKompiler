using Antlr4.Runtime;
using KismetKompiler.Library.Parser;

namespace KismetKompiler.Library.Syntax.Statements;

public abstract class Expression : Statement
{
    public virtual ValueKind ExpressionValueKind { get; set; }

    protected Expression(ValueKind kind)
    {
        ExpressionValueKind = kind;
    }

    public static Expression FromText(string source)
    {
        var lexer = new KismetScriptLexer(new AntlrInputStream(source));
        var tokenStream = new CommonTokenStream(lexer);

        // parse expression
        var parser = new KismetScriptParser(tokenStream);
        parser.BuildParseTree = true;
        var expressionParseTree = parser.expression();

        // parse ast nodes
        var compilationUnitParser = new KismetScriptASTParser();
        compilationUnitParser.TryParseExpression(expressionParseTree, out var expression);

        // resolve types
        //var typeResolver = new Analyzer();
        //typeResolver.TryResolveTypesInExpression(expression);

        return expression;
    }

    public abstract int GetDepth();
}