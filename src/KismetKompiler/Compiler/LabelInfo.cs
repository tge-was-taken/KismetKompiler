using KismetKompiler.Syntax.Statements.Declarations;

namespace KismetKompiler.Compiler;

public class LabelInfo
{
    public string Name { get; set; }

    public int? CodeOffset { get; set; }

    public bool IsResolved { get; set; }
    public LabelDeclaration Declaration { get; internal set; }
}