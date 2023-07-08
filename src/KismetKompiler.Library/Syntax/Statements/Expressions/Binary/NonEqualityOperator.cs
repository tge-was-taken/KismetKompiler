namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class NonEqualityOperator : EqualityExpression
{
    public override string ToString()
    {
        return $"({Left}) != ({Right})";
    }
}
