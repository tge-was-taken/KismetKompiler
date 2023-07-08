namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class GreaterThanOrEqualOperator : RelationalExpression
{
    public override string ToString()
    {
        return $"({Left}) >= ({Right})";
    }
}
