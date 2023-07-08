namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class BitwiseOrOperator : BitwiseOperator
{
    public override int Precedence => 13;

    public override string ToString()
    {
        return $"({Left}) | ({Right})";
    }
}