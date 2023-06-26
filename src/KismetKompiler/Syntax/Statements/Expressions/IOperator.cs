namespace KismetKompiler.Syntax.Statements.Expressions;

public interface IOperator
{
    int Precedence { get; }
}