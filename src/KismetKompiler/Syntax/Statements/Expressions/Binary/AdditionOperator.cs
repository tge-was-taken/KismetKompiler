﻿using KismetKompiler.Syntax;
using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Binary;

public class AdditionOperator : BinaryExpression, IOperator
{
    public int Precedence => 6;

    public AdditionOperator() : base(ValueKind.Unresolved)
    {
    }

    public AdditionOperator(Expression left, Expression right)
        : base(ValueKind.Unresolved, left, right)
    {

    }

    public override string ToString()
    {
        return $"({Left}) + ({Right})";
    }
}
