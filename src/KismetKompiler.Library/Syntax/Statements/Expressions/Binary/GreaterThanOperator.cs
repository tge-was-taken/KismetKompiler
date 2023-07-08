namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class GreaterThanOperator : RelationalExpression
{
    public override string ToString()
    {
        return $"({Left}) > ({Right})";
    }
}
