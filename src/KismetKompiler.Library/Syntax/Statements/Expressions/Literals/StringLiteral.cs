﻿using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using System;

namespace KismetKompiler.Library.Syntax.Statements.Expressions.Literals;

public class StringLiteral : Literal<string>, IEquatable<StringLiteral>
{
    public StringLiteral() : base(ValueKind.String)
    {
    }

    public StringLiteral(string value) : base(ValueKind.String, value)
    {
    }

    public static implicit operator StringLiteral(string value) => new StringLiteral(value);

    public override string ToString()
    {
        return $"\"{Value}\"";
    }

    public bool Equals(StringLiteral other)
    {
        return Value == other?.Value;
    }
}
