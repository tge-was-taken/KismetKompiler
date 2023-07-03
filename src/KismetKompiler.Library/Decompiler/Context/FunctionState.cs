namespace KismetKompiler.Library.Decompiler.Context;

public class FunctionState
{
    public HashSet<string> DeclaredVariables { get; init; } = new();
}
