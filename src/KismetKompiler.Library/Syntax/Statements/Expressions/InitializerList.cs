using System.Text;

namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public enum InitializerListKind
{
    Brace,
    Bracket
}

public class InitializerList : Expression
{
    public List<Expression> Expressions { get; set; }

    public InitializerListKind Kind { get; set; }

    public InitializerList() : base(ValueKind.Unresolved)
    {
        Expressions = new List<Expression>();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.Append("{ ");

        if (Expressions.Count > 0)
            builder.Append(Expressions[0]);

        for (int i = 1; i < Expressions.Count; i++)
        {
            builder.Append($", {Expressions[i]}");
        }

        builder.Append(" }");

        return builder.ToString();
    }

    public override int GetDepth() => 1;
}