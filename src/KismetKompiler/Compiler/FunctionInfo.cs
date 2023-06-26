using KismetKompiler.Syntax.Statements.Declarations;

namespace KismetKompiler.Compiler;

internal class FunctionInfo
{
    public FunctionDeclaration Declaration { get; set; }

    public short Index { get; set; }
}