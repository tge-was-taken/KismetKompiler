using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Unary;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Library.Syntax.Statements.Expressions.Identifiers;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Library.Syntax.Statements.Declarations;
namespace KismetKompiler.Library.Syntax;
public interface ISyntaxNodeVisitor {
    void Visit(Argument node);
    void Visit(OutArgument node);
    void Visit(OutDeclarationArgument node);
    void Visit(Comment node);
    void Visit(CompilationUnit node);
    void Visit(ConditionSwitchLabel node);
    void Visit(DefaultSwitchLabel node);
    void Visit(PackageDeclaration node);
    void Visit(Parameter node);
    void Visit(ArrayParameter node);
    void Visit(Statement node);
    void Visit(SwitchLabel node);
    void Visit(SyntaxNode node);
    void Visit(BreakStatement node);
    void Visit(CompoundStatement node);
    void Visit(ContinueStatement node);
    void Visit(Declaration node);
    void Visit(Expression node);
    void Visit(ForStatement node);
    void Visit(GotoStatement node);
    void Visit(IfStatement node);
    void Visit(NullStatement node);
    void Visit(ReturnStatement node);
    void Visit(SwitchStatement node);
    void Visit(WhileStatement node);
    void Visit(BinaryExpression node);
    void Visit(CallOperator node);
    void Visit(CastOperator node);
    void Visit(ConditionalExpression node);
    void Visit(Identifier node);
    void Visit(InitializerList node);
    void Visit(Literal node);
    void Visit<T>(Literal<T> node);
    void Visit(MemberExpression node);
    void Visit(NewExpression node);
    void Visit(NullExpression node);
    void Visit(PrimaryExpression node);
    void Visit(SubscriptOperator node);
    void Visit(TypeofOperator node);
    void Visit(UnaryExpression node);
    void Visit(LogicalNotOperator node);
    void Visit(NegationOperator node);
    void Visit(PostfixDecrementOperator node);
    void Visit(PostfixIncrementOperator node);
    void Visit(PostfixOperator node);
    void Visit(PrefixDecrementOperator node);
    void Visit(PrefixIncrementOperator node);
    void Visit(PrefixOperator node);
    void Visit(BoolLiteral node);
    void Visit(FloatLiteral node);
    void Visit(IntLiteral node);
    void Visit(StringLiteral node);
    void Visit(TypeIdentifier node);
    void Visit(AdditionAssignmentOperator node);
    void Visit(AdditionOperator node);
    void Visit(AssignmentOperator node);
    void Visit(AssignmentOperatorBase node);
    void Visit(BitwiseAndOperator node);
    void Visit(BitwiseOperator node);
    void Visit(BitwiseOrOperator node);
    void Visit(BitwiseShiftOperator node);
    void Visit(BitwiseShiftLeftOperator node);
    void Visit(BitwiseShiftRightOperator node);
    void Visit(BitwiseXorOperator node);
    void Visit(CompoundAssignmentOperator node);
    void Visit(DivisionAssignmentOperator node);
    void Visit(DivisionOperator node);
    void Visit(EqualityExpression node);
    void Visit(EqualityOperator node);
    void Visit(GreaterThanOperator node);
    void Visit(GreaterThanOrEqualOperator node);
    void Visit(LessThanOperator node);
    void Visit(LessThanOrEqualOperator node);
    void Visit(LogicalAndOperator node);
    void Visit(LogicalOrOperator node);
    void Visit(ModulusAssignmentOperator node);
    void Visit(ModulusOperator node);
    void Visit(MultiplicationAssignmentOperator node);
    void Visit(MultiplicationOperator node);
    void Visit(NonEqualityOperator node);
    void Visit(RelationalExpression node);
    void Visit(SubtractionAssignmentOperator node);
    void Visit(SubtractionOperator node);
    void Visit(AttributeDeclaration node);
    void Visit(ClassDeclaration node);
    void Visit(EnumDeclaration node);
    void Visit(EnumValueDeclaration node);
    void Visit(FunctionDeclaration node);
    void Visit(LabelDeclaration node);
    void Visit(ProcedureDeclaration node);
    void Visit(VariableDeclaration node);
    void Visit(ArrayVariableDeclaration node);
}
public abstract class SyntaxNodeVisitorBase : ISyntaxNodeVisitor {
    public virtual void Visit(Argument node) {
        if (node.Expression != null) Visit(node.Expression);
    }
    public virtual void Visit(OutArgument node) {
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.Expression != null) Visit(node.Expression);
    }
    public virtual void Visit(OutDeclarationArgument node) {
        if (node.Type != null) Visit(node.Type);
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.Expression != null) Visit(node.Expression);
    }
    public virtual void Visit(Comment node) {
    }
    public virtual void Visit(CompilationUnit node) {
        if (node.Declarations != null) {
            foreach (var item in node.Declarations)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(ConditionSwitchLabel node) {
        if (node.Condition != null) Visit(node.Condition);
        if (node.Body != null) {
            foreach (var item in node.Body)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(DefaultSwitchLabel node) {
        if (node.Body != null) {
            foreach (var item in node.Body)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(PackageDeclaration node) {
        if (node.Declarations != null) {
            foreach (var item in node.Declarations)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(Parameter node) {
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Type != null) Visit(node.Type);
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.DefaultVaue != null) Visit(node.DefaultVaue);
    }
    public virtual void Visit(ArrayParameter node) {
        if (node.Size != null) Visit(node.Size);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Type != null) Visit(node.Type);
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.DefaultVaue != null) Visit(node.DefaultVaue);
    }
    public virtual void Visit(Statement node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(SwitchLabel node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(SyntaxNode node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(BreakStatement node) {
    }
    public virtual void Visit(CompoundStatement node) {
        if (node.Statements != null) {
            foreach (var item in node.Statements)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(ContinueStatement node) {
    }
    public virtual void Visit(Declaration node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(Expression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(ForStatement node) {
        if (node.Initializer != null) Visit(node.Initializer);
        if (node.Condition != null) Visit(node.Condition);
        if (node.AfterLoop != null) Visit(node.AfterLoop);
        if (node.Body != null) Visit(node.Body);
    }
    public virtual void Visit(GotoStatement node) {
        if (node.Label != null) Visit(node.Label);
    }
    public virtual void Visit(IfStatement node) {
        if (node.Condition != null) Visit(node.Condition);
        if (node.Body != null) Visit(node.Body);
        if (node.ElseBody != null) Visit(node.ElseBody);
    }
    public virtual void Visit(NullStatement node) {
    }
    public virtual void Visit(ReturnStatement node) {
        if (node.Value != null) Visit(node.Value);
    }
    public virtual void Visit(SwitchStatement node) {
        if (node.SwitchOn != null) Visit(node.SwitchOn);
        if (node.Labels != null) {
            foreach (var item in node.Labels)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(WhileStatement node) {
        if (node.Condition != null) Visit(node.Condition);
        if (node.Body != null) Visit(node.Body);
    }
    public virtual void Visit(BinaryExpression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(CallOperator node) {
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.Arguments != null) {
            foreach (var item in node.Arguments)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(CastOperator node) {
        if (node.TypeIdentifier != null) Visit(node.TypeIdentifier);
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(ConditionalExpression node) {
        if (node.Condition != null) Visit(node.Condition);
        if (node.ValueIfTrue != null) Visit(node.ValueIfTrue);
        if (node.ValueIfFalse != null) Visit(node.ValueIfFalse);
    }
    public virtual void Visit(Identifier node) {
    }
    public virtual void Visit(InitializerList node) {
        if (node.Expressions != null) {
            foreach (var item in node.Expressions)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(Literal node) {
        Visit((dynamic)node);
    }
    public virtual void Visit<T>(Literal<T> node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(MemberExpression node) {
        if (node.Context != null) Visit(node.Context);
        if (node.Member != null) Visit(node.Member);
    }
    public virtual void Visit(NewExpression node) {
        if (node.TypeIdentifier != null) Visit(node.TypeIdentifier);
        if (node.Initializer != null) {
            foreach (var item in node.Initializer)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(NullExpression node) {
    }
    public virtual void Visit(PrimaryExpression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(SubscriptOperator node) {
        if (node.Operand != null) Visit(node.Operand);
        if (node.Index != null) Visit(node.Index);
    }
    public virtual void Visit(TypeofOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(UnaryExpression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(LogicalNotOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(NegationOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(PostfixDecrementOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(PostfixIncrementOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(PostfixOperator node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(PrefixDecrementOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(PrefixIncrementOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(PrefixOperator node) {
        if (node.Operand != null) Visit(node.Operand);
    }
    public virtual void Visit(BoolLiteral node) {
    }
    public virtual void Visit(FloatLiteral node) {
    }
    public virtual void Visit(IntLiteral node) {
    }
    public virtual void Visit(StringLiteral node) {
    }
    public virtual void Visit(TypeIdentifier node) {
        if (node.TypeParameter != null) Visit(node.TypeParameter);
    }
    public virtual void Visit(AdditionAssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(AdditionOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(AssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(AssignmentOperatorBase node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(BitwiseAndOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(BitwiseOperator node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(BitwiseOrOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(BitwiseShiftOperator node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(BitwiseShiftLeftOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(BitwiseShiftRightOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(BitwiseXorOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(CompoundAssignmentOperator node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(DivisionAssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(DivisionOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(EqualityExpression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(EqualityOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(GreaterThanOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(GreaterThanOrEqualOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(LessThanOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(LessThanOrEqualOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(LogicalAndOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(LogicalOrOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(ModulusAssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(ModulusOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(MultiplicationAssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(MultiplicationOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(NonEqualityOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(RelationalExpression node) {
        Visit((dynamic)node);
    }
    public virtual void Visit(SubtractionAssignmentOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(SubtractionOperator node) {
        if (node.Left != null) Visit(node.Left);
        if (node.Right != null) Visit(node.Right);
    }
    public virtual void Visit(AttributeDeclaration node) {
        if (node.Identifier != null) Visit(node.Identifier);
        if (node.Arguments != null) {
            foreach (var item in node.Arguments)
            {
                if (item != null) Visit(item);
            }
        }
    }
    public virtual void Visit(ClassDeclaration node) {
        if (node.InheritedTypeIdentifiers != null) {
            foreach (var item in node.InheritedTypeIdentifiers)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Declarations != null) {
            foreach (var item in node.Declarations)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.BaseClassIdentifier != null) Visit(node.BaseClassIdentifier);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(EnumDeclaration node) {
        if (node.Values != null) {
            foreach (var item in node.Values)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(EnumValueDeclaration node) {
        if (node.Value != null) Visit(node.Value);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(FunctionDeclaration node) {
        if (node.Index != null) Visit(node.Index);
        if (node.ReturnType != null) Visit(node.ReturnType);
        if (node.Parameters != null) {
            foreach (var item in node.Parameters)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(LabelDeclaration node) {
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(ProcedureDeclaration node) {
        if (node.Index != null) Visit(node.Index);
        if (node.ReturnType != null) Visit(node.ReturnType);
        if (node.Parameters != null) {
            foreach (var item in node.Parameters)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Body != null) Visit(node.Body);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(VariableDeclaration node) {
        if (node.Type != null) Visit(node.Type);
        if (node.Initializer != null) Visit(node.Initializer);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
    public virtual void Visit(ArrayVariableDeclaration node) {
        if (node.Size != null) Visit(node.Size);
        if (node.Type != null) Visit(node.Type);
        if (node.Initializer != null) Visit(node.Initializer);
        if (node.Attributes != null) {
            foreach (var item in node.Attributes)
            {
                if (item != null) Visit(item);
            }
        }
        if (node.Identifier != null) Visit(node.Identifier);
    }
}
