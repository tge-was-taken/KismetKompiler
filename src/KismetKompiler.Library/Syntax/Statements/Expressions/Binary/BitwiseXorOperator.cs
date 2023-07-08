namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class BitwiseXorOperator : BitwiseOperator
{
    public override int Precedence => 12;

    public override string ToString()
    {
        return $"({Left}) ^ ({Right})";
    }
}
