namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class LessThanOrEqualOperator : RelationalExpression
{
    public override string ToString()
    {
        return $"({Left}) <= ({Right})";
    }
}
