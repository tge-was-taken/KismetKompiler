using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;

namespace KismetKompiler.Library.Syntax;

public class Argument : SyntaxNode
{
    public virtual Expression Expression { get; set; }

    public Argument()
    {
    }

    public Argument(Expression expression)
    {
        Expression = expression;
    }

    public override string ToString()
    {
        return $"{Expression}";
    }
}

public class OutArgument : Argument
{
    public Identifier Identifier { get; set; }

    public override Expression Expression
        => Identifier;

    public OutArgument()
    {
    }
}

public class OutDeclarationArgument : OutArgument
{
    public TypeIdentifier Type { get; set; }
}