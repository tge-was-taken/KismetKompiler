﻿using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements.Expressions;
using System;

namespace KismetKompiler.Syntax.Statements.Expressions.Literals;

public class BoolLiteral : Literal<bool>, IEquatable<BoolLiteral>
{
    public BoolLiteral() : base(ValueKind.Bool)
    {
    }

    public BoolLiteral(bool value) : base(ValueKind.Bool, value)
    {
    }

    public bool Equals(BoolLiteral other)
    {
        return Value == other?.Value;
    }

    public static implicit operator BoolLiteral(bool value) => new BoolLiteral(value);
}
