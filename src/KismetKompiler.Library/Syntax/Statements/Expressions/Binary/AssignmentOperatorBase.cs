﻿using KismetKompiler.Library.Syntax;

namespace KismetKompiler.Library.Syntax.Statements.Expressions.Binary;

public abstract class AssignmentOperatorBase : BinaryExpression, IOperator
{
    public int Precedence => 15;

    protected AssignmentOperatorBase() : base(ValueKind.Unresolved)
    {
    }

    protected AssignmentOperatorBase(Expression left, Expression right)
        : base(ValueKind.Unresolved, left, right)
    {

    }
}
