﻿using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;

namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public class GreaterThanOperator : BinaryExpression, IOperator
{
    public int Precedence => 8;

    public GreaterThanOperator() : base(ValueKind.Bool)
    {
    }

    public GreaterThanOperator(Expression left, Expression right)
        : base(ValueKind.Bool, left, right)
    {

    }

    public override string ToString()
    {
        return $"({Left}) > ({Right})";
    }
}