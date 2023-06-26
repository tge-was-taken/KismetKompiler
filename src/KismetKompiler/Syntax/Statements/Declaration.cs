﻿using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements.Expressions;

namespace KismetKompiler.Syntax.Statements;

public abstract class Declaration : Statement
{
    public DeclarationType DeclarationType { get; }

    public Identifier Identifier { get; set; }

    protected Declaration(DeclarationType type)
    {
        DeclarationType = type;
    }

    protected Declaration(DeclarationType type, Identifier identifier)
    {
        DeclarationType = type;
        Identifier = identifier;
    }
}
