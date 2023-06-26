﻿using KismetKompiler.Syntax.Statements;

namespace KismetKompiler.Syntax.Statements.Expressions.Unary;

public class PostfixIncrementOperator : PostfixOperator
{
    public PostfixIncrementOperator()
    {

    }

    public PostfixIncrementOperator(Expression operand) : base(operand)
    {

    }

    public override string ToString()
    {
        return $"({Operand})++";
    }
}
