using System.Collections.Generic;
using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;
using KismetKompiler.Syntax.Statements.Declarations;

namespace KismetKompiler.Compiler;

internal class Enum
{
    public EnumDeclaration Declaration { get; set; }

    public Dictionary<string, Expression> Members { get; set; }
}