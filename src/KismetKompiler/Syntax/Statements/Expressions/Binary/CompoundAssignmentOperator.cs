﻿using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Binary;

public abstract class CompoundAssignmentOperator : AssignmentOperatorBase
{
    protected CompoundAssignmentOperator()
    {

    }

    protected CompoundAssignmentOperator(Expression left, Expression right)
        : base(left, right)
    {
    }
}
