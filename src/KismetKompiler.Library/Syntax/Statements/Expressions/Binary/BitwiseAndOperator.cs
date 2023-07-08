namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class BitwiseAndOperator : BitwiseOperator
{
    public override int Precedence => 11;

    public override string ToString()
    {
        return $"({Left}) & ({Right})";
    }
}
