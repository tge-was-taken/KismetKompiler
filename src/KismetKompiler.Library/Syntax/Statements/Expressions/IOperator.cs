namespace KismetKompiler.Library.Syntax.Statements.Expressions;

public interface IOperator
{
    int Precedence { get; }
}