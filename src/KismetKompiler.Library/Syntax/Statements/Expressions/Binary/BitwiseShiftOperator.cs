namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public abstract class BitwiseShiftOperator : BitwiseOperator
{
    public override int Precedence => 7;
}

public class BitwiseShiftLeftOperator : BitwiseShiftOperator
{
    public override string ToString()
    {
        return $"({Left}) << ({Right})";
    }
}

public class BitwiseShiftRightOperator : BitwiseShiftOperator
{
    public override string ToString()
    {
        return $"({Left}) >> ({Right})";
    }
}
