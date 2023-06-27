namespace KismetKompiler.Decompiler.Context;

public class FunctionState
{
    public HashSet<string> DeclaredVariables { get; init; } = new();
}
