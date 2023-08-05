using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Syntax.Statements.Expressions.Binary;
using KismetKompiler.Library.Syntax.Statements.Expressions;
using KismetKompiler.Library.Syntax;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using KismetKompiler.Library.Syntax.Statements.Expressions.Literals;
using KismetKompiler.Library.Syntax.Statements.Expressions.Unary;
using KismetKompiler.Library.Compiler.Context;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Compiler;

public partial class KismetScriptCompiler
{
    private CompiledExpressionContext CompileBinaryExpression(BinaryExpression expression)
    {
        if (expression is MultiplicationOperator multiplicationOperator)
        {
            return CompileMultiplicationOperator(multiplicationOperator);
        }
        else if (expression is DivisionOperator divisionOperator)
        {
            return CompileDivisionOperator(divisionOperator);
        }
        else if (expression is ModulusOperator modulusOperator)
        {
            return CompileModulusOperator(modulusOperator);
        }
        else if (expression is AdditionOperator additionOperator)
        {
            return CompileAdditionOperator(additionOperator);
        }
        else if (expression is SubtractionOperator subtractionOperator)
        {
            return CompileSubtractionOperator(subtractionOperator);
        }
        else if (expression is BitwiseShiftOperator bitwiseShiftOperator)
        {
            return CompileBitwiseShiftOperator(bitwiseShiftOperator);
        }
        else if (expression is RelationalExpression relationalExpression)
        {
            return CompileRelationalExpression(relationalExpression);
        }
        else if (expression is EqualityExpression equalityExpression)
        {
            return CompileEqualityExpression(equalityExpression);
        }
        else if (expression is BitwiseOperator bitwiseOperator)
        {
            return CompileBitwiseOperator(bitwiseOperator);
        }
        else if (expression is LogicalAndOperator logicalAndOperator)
        {
            return EmitMathLibraryCall(logicalAndOperator, "BooleanAND");
        }
        else if (expression is LogicalOrOperator logicalOrOperator)
        {
            return EmitMathLibraryCall(logicalOrOperator, "BooleanOR");
        }
        else if (expression is AssignmentOperator assignmentOperator)
        {
            return CompileAssignmentOperator(assignmentOperator);
        }
        else if (expression is CompoundAssignmentOperator compoundAssignmentOperator)
        {
            return CompileCompoundAssignmentOperator(compoundAssignmentOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileCompoundAssignmentOperator(CompoundAssignmentOperator compoundAssignmentOperator)
    {
        if (compoundAssignmentOperator is AdditionAssignmentOperator additionAssignmentOperator)
        {
            return CompileAssignmentOperator(new AssignmentOperator()
            {
                Left = additionAssignmentOperator.Left,
                Right = new AdditionOperator()
                {
                    Left = additionAssignmentOperator.Left,
                    Right = additionAssignmentOperator.Right,
                    ExpressionValueKind = additionAssignmentOperator.ExpressionValueKind,
                    SourceInfo = additionAssignmentOperator.SourceInfo
                },
                ExpressionValueKind = additionAssignmentOperator.ExpressionValueKind,
                SourceInfo = additionAssignmentOperator.SourceInfo
            });
        }
        else if (compoundAssignmentOperator is DivisionAssignmentOperator divisionAssignmentOperator)
        {
            return CompileAssignmentOperator(new AssignmentOperator()
            {
                Left = divisionAssignmentOperator.Left,
                Right = new DivisionOperator()
                {
                    Left = divisionAssignmentOperator.Left,
                    Right = divisionAssignmentOperator.Right,
                    ExpressionValueKind = divisionAssignmentOperator.ExpressionValueKind,
                    SourceInfo = divisionAssignmentOperator.SourceInfo
                },
                ExpressionValueKind = divisionAssignmentOperator.ExpressionValueKind,
                SourceInfo = divisionAssignmentOperator.SourceInfo
            });
        }
        else if (compoundAssignmentOperator is ModulusAssignmentOperator modulusAssignmentOperator)
        {
            return CompileAssignmentOperator(new AssignmentOperator()
            {
                Left = modulusAssignmentOperator.Left,
                Right = new ModulusOperator()
                {
                    Left = modulusAssignmentOperator.Left,
                    Right = modulusAssignmentOperator.Right,
                    ExpressionValueKind = modulusAssignmentOperator.ExpressionValueKind,
                    SourceInfo = modulusAssignmentOperator.SourceInfo
                },
                ExpressionValueKind = modulusAssignmentOperator.ExpressionValueKind,
                SourceInfo = modulusAssignmentOperator.SourceInfo
            });
        }
        else if (compoundAssignmentOperator is MultiplicationAssignmentOperator multiplicationAssignmentOperator)
        {
            return CompileAssignmentOperator(new AssignmentOperator()
            {
                Left = multiplicationAssignmentOperator.Left,
                Right = new MultiplicationOperator()
                {
                    Left = multiplicationAssignmentOperator.Left,
                    Right = multiplicationAssignmentOperator.Right,
                    ExpressionValueKind = multiplicationAssignmentOperator.ExpressionValueKind,
                    SourceInfo = multiplicationAssignmentOperator.SourceInfo
                },
                ExpressionValueKind = multiplicationAssignmentOperator.ExpressionValueKind,
                SourceInfo = multiplicationAssignmentOperator.SourceInfo
            });
        }
        else if (compoundAssignmentOperator is SubtractionAssignmentOperator subtractionAssignmentOperator)
        {
            return CompileAssignmentOperator(new AssignmentOperator()
            {
                Left = subtractionAssignmentOperator.Left,
                Right = new SubtractionOperator()
                {
                    Left = subtractionAssignmentOperator.Left,
                    Right = subtractionAssignmentOperator.Right,
                    ExpressionValueKind = subtractionAssignmentOperator.ExpressionValueKind,
                    SourceInfo = subtractionAssignmentOperator.SourceInfo
                },
                ExpressionValueKind = subtractionAssignmentOperator.ExpressionValueKind,
                SourceInfo = subtractionAssignmentOperator.SourceInfo
            });
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileModulusOperator(ModulusOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Percent_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Percent_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Percent_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"% operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileDivisionOperator(DivisionOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Divide_Vector2DFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "Divide_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Divide_VectorInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Divide_VectorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Divide_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Divide_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Divide_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"/ operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileBitwiseOperator(BitwiseOperator bitwiseOperator)
    {
        if (bitwiseOperator is BitwiseAndOperator bitwiseAndOperator)
        {
            return CompileBitwiseAndOperator(bitwiseAndOperator);
        }
        else if (bitwiseOperator is BitwiseOrOperator bitwiseOrOperator)
        {
            return CompileBitwiseOrOperator(bitwiseOrOperator);
        }
        else if (bitwiseOperator is BitwiseShiftOperator bitwiseShiftOperator)
        {
            return CompileBitwiseShiftOperator(bitwiseShiftOperator);
        }
        else if (bitwiseOperator is BitwiseXorOperator bitwiseXorOperator)
        {
            return CompileBitwiseXorOperator(bitwiseXorOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileBitwiseXorOperator(BitwiseXorOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Xor_IntInt");
        }
        else
        {
            throw new CompilationError(op, $"^ operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileBitwiseOrOperator(BitwiseOrOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Or_IntInt");
        }
        else
        {
            throw new CompilationError(op, $"| operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileBitwiseAndOperator(BitwiseAndOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "And_IntInt");
        }
        else
        {
            throw new CompilationError(op, $"& operator not supported betweentypes  {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileEqualityExpression(EqualityExpression equalityExpression)
    {
        if (equalityExpression is EqualityOperator equalityOperator)
        {
            return CompileEqualityOperator(equalityOperator);
        }
        else if (equalityExpression is NonEqualityOperator nonEqualityOperator)
        {
            return CompileNonEqualityOperator(nonEqualityOperator);
        }
        else
        {
            throw new CompilationError(equalityExpression, "Invalid equality expression");
        }
    }

    private CompiledExpressionContext CompileNonEqualityOperator(NonEqualityOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Vector2D)
        {
            return EmitMathLibraryCall(op, "NotEqual_Vector2DVector2D");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Name && op.Right.ExpressionValueKind == ValueKind.Name)
        {
            return EmitMathLibraryCall(op, "NotEqual_NameName");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Class && op.Right.ExpressionValueKind == ValueKind.Class)
        {
            return EmitMathLibraryCall(op, "NotEqual_ClassClass");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Object && op.Right.ExpressionValueKind == ValueKind.Object)
        {
            return EmitMathLibraryCall(op, "NotEqual_ObjectObject");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "NotEqual_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "NotEqual_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Rotator && op.Right.ExpressionValueKind == ValueKind.Rotator)
        {
            return EmitMathLibraryCall(op, "NotEqual_RotatorRotator");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "NotEqual_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "NotEqual_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "NotEqual_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "NotEqual_ByteByte");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Bool && op.Right.ExpressionValueKind == ValueKind.Bool)
        {
            return EmitMathLibraryCall(op, "NotEqual_BoolBool");
        }
        else
        {
            throw new CompilationError(op, $"!= operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileEqualityOperator(EqualityOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Vector2D)
        {
            return EmitMathLibraryCall(op, "EqualEqual_Vector2DVector2D");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Transform && op.Right.ExpressionValueKind == ValueKind.Transform)
        {
            return EmitMathLibraryCall(op, "EqualEqual_TransformTransform");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Name && op.Right.ExpressionValueKind == ValueKind.Name)
        {
            return EmitMathLibraryCall(op, "EqualEqual_NameName");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Class && op.Right.ExpressionValueKind == ValueKind.Class)
        {
            return EmitMathLibraryCall(op, "EqualEqual_ClassClass");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Object && op.Right.ExpressionValueKind == ValueKind.Object)
        {
            return EmitMathLibraryCall(op, "EqualEqual_ObjectObject");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "EqualEqual_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "EqualEqual_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Rotator && op.Right.ExpressionValueKind == ValueKind.Rotator)
        {
            return EmitMathLibraryCall(op, "EqualEqual_RotatorRotator");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "EqualEqual_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "EqualEqual_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "EqualEqual_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "EqualEqual_ByteByte");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Bool && op.Right.ExpressionValueKind == ValueKind.Bool)
        {
            return EmitMathLibraryCall(op, "EqualEqual_BoolBool");
        }
        else
        {
            throw new CompilationError(op, $"== operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileRelationalExpression(RelationalExpression relationalExpression)
    {
        if (relationalExpression is GreaterThanOperator greaterThanOperator)
        {
            return CompileGreaterThanOperator(greaterThanOperator);
        }
        else if (relationalExpression is GreaterThanOrEqualOperator greaterThanOrEqualOperator)
        {
            return CompileGreaterThanOrEqualOperator(greaterThanOrEqualOperator);
        }
        else if (relationalExpression is LessThanOperator lessThanOperator)
        {
            return CompileLessThanOperator(lessThanOperator);
        }
        else if (relationalExpression is LessThanOrEqualOperator lessThanOrEqualOperator)
        {
            return CompileLessThanOrEqualOperator(lessThanOrEqualOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileLessThanOrEqualOperator(LessThanOrEqualOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "LessEqual_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "LessEqual_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "LessEqual_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "LessEqual_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "LessEqual_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"< operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileLessThanOperator(LessThanOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Less_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "Less_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Rotator)
        {
            return EmitMathLibraryCall(op, "LessLess_VectorRotator");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Less_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Less_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Less_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"< operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileGreaterThanOrEqualOperator(GreaterThanOrEqualOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "GreaterEqual_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "GreaterEqual_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "GreaterEqual_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "GreaterEqual_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "GreaterEqual_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $">= operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileGreaterThanOperator(GreaterThanOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Greater_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.DateTime)
        {
            return EmitMathLibraryCall(op, "Greater_DateTimeDateTime");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Greater_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Greater_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Greater_ByteByte");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Rotator)
        {
            return EmitMathLibraryCall(op, "GreaterGreater_VectorRotator");
        }
        else
        {
            throw new CompilationError(op, $"> operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileBitwiseShiftOperator(BitwiseShiftOperator bitwiseShiftOperator)
    {
        if (bitwiseShiftOperator is BitwiseShiftLeftOperator bitwiseShiftLeftOperator)
        {
            return CompileBitwiseShiftLeftOperator(bitwiseShiftLeftOperator);
        }
        else if (bitwiseShiftOperator is BitwiseShiftRightOperator bitwiseShiftRightOperator)
        {
            return CompileBitwiseShiftRightOperator(bitwiseShiftRightOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileBitwiseShiftRightOperator(BitwiseShiftRightOperator op)
    {
        throw new CompilationError(op, $">> operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
    }

    private CompiledExpressionContext CompileBitwiseShiftLeftOperator(BitwiseShiftLeftOperator op)
    {
        throw new CompilationError(op, $"<< operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
    }

    private CompiledExpressionContext CompileMultiplicationOperator(MultiplicationOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Vector2D)
        {
            return EmitMathLibraryCall(op, "Multiply_Vector2DVector2D");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_Vector2DFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_TimespanFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.LinearColor && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_LinearColorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.LinearColor && op.Right.ExpressionValueKind == ValueKind.LinearColor)
        {
            return EmitMathLibraryCall(op, "Multiply_LinearColorLinearColor");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Rotator && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Multiply_RotatorInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Rotator && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_RotatorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "Multiply_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Multiply_VectorInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_VectorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_IntFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Multiply_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Multiply_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Multiply_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"* operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileAssignmentOperator(AssignmentOperator assignmentOperator)
    {
        // TODO find a better solution, EX_Context needs this in Expression
        TryGetPropertyPointer(assignmentOperator.Left, out var rvalue);

        PushRValue(rvalue);
        try
        {
            if (assignmentOperator.Right.ExpressionValueKind == ValueKind.Bool)
            {
                return Emit(assignmentOperator, new EX_LetBool()
                {
                    VariableExpression = CompileSubExpression(assignmentOperator.Left),
                    AssignmentExpression = CompileSubExpression(assignmentOperator.Right),
                });
            }
            else if (assignmentOperator.Right is InitializerList initializerList)
            {
                return Emit(assignmentOperator, new EX_SetArray()
                {
                    ArrayInnerProp = GetPackageIndex(assignmentOperator.Left),
                    AssigningProperty = CompileSubExpression(assignmentOperator.Left),
                    Elements = initializerList.Expressions.Select(x => CompileSubExpression(x)).ToArray()
                });
            }
            else
            {
                TryGetPropertyPointer(assignmentOperator.Left, out var pointer);
                return Emit(assignmentOperator, new EX_Let()
                {
                    Value = pointer ?? new KismetPropertyPointer()
                    {
                        Old = FPackageIndex.Null,
                        New = FFieldPath.Null,
                    },
                    Variable = CompileSubExpression(assignmentOperator.Left),
                    Expression = CompileSubExpression(assignmentOperator.Right),
                });
            }
        }
        finally
        {
            PopRValue();
        }
    }

    private CompiledExpressionContext CompileAdditionOperator(AdditionOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Add_Vector2DFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Vector2D)
        {
            return EmitMathLibraryCall(op, "Add_Vector2DVector2D");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Add_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Add_DateTimeTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Add_VectorInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Add_VectorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "Add_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Add_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Add_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Add_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"+ operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileSubtractionOperator(SubtractionOperator op)
    {
        if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Subtract_Vector2DFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector2D && op.Right.ExpressionValueKind == ValueKind.Vector2D)
        {
            return EmitMathLibraryCall(op, "Subtract_Vector2DVector2D");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.TimeSpan && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Subtract_TimespanTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.DateTime && op.Right.ExpressionValueKind == ValueKind.TimeSpan)
        {
            return EmitMathLibraryCall(op, "Subtract_DateTimeTimespan");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Subtract_VectorInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Subtract_VectorFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Vector && op.Right.ExpressionValueKind == ValueKind.Vector)
        {
            return EmitMathLibraryCall(op, "Subtract_VectorVector");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Float && op.Right.ExpressionValueKind == ValueKind.Float)
        {
            return EmitMathLibraryCall(op, "Subtract_FloatFloat");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Int && op.Right.ExpressionValueKind == ValueKind.Int)
        {
            return EmitMathLibraryCall(op, "Subtract_IntInt");
        }
        else if (op.Left.ExpressionValueKind == ValueKind.Byte && op.Right.ExpressionValueKind == ValueKind.Byte)
        {
            return EmitMathLibraryCall(op, "Subtract_ByteByte");
        }
        else
        {
            throw new CompilationError(op, $"- operator not supported between types {op.Left.ExpressionValueKind} and {op.Right.ExpressionValueKind}");
        }
    }

    private CompiledExpressionContext CompileCastExpression(CastOperator castOperator)
    {
        if (castOperator.Operand is Literal literal)
        {
            if (literal is IntLiteral intLiteral)
            {
                switch (castOperator.TypeIdentifier.ValueKind)
                {
                    case ValueKind.Byte:
                        return Emit(intLiteral, new EX_ByteConst() { Value = (byte)intLiteral.Value });
                    case ValueKind.Bool:
                        return Emit(intLiteral, intLiteral.Value > 0 ? new EX_True() : new EX_False());
                    case ValueKind.Int:
                        return Emit(intLiteral, new EX_IntConst() { Value = intLiteral.Value });
                    case ValueKind.Float:
                        return Emit(intLiteral, new EX_FloatConst() { Value = intLiteral.Value });
                    default:
                        throw new UnexpectedSyntaxError(literal);
                }
            }
            else if (literal is BoolLiteral boolLiteral)
            {
                switch (castOperator.TypeIdentifier.ValueKind)
                {
                    case ValueKind.Bool:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_True() : new EX_False());
                    case ValueKind.Int:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_IntConst() { Value = 1 } : new EX_IntConst() { Value = 0 });
                    case ValueKind.Float:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_FloatConst() { Value = 1 } : new EX_FloatConst() { Value = 0 });
                    case ValueKind.Byte:
                        return Emit(boolLiteral, boolLiteral.Value ? new EX_ByteConst() { Value = 1 } : new EX_ByteConst() { Value = 0 });
                    default:
                        throw new UnexpectedSyntaxError(literal);
                }
            }
            else
            {
                throw new UnexpectedSyntaxError(literal);
            }
        }
        else
        {
            return CompileExpression(castOperator.Operand);
        }
    }

    private CompiledExpressionContext CompileConditionalExpression(ConditionalExpression conditionalExpression)
    {
        var endLabel = CreateCompilerLabel("ConditionalExpression_SwitchValue_EndLabel");
        var nextCaseLabel = CreateCompilerLabel("ConditionalExpression_SwitchValue_NextCaseLabel");
        var expr = new EX_SwitchValue()
        {
            EndGotoOffset = 0, // Placeholder
            IndexTerm = CompileSubExpression(conditionalExpression.Condition),
            Cases = new FKismetSwitchCase[]
            {
                new()
                {
                    CaseIndexValueTerm = new EX_True(),
                    NextOffset = 0, // Placeholder
                    CaseTerm = CompileSubExpression(conditionalExpression.ValueIfTrue)
                }
            },
            DefaultTerm = CompileSubExpression(conditionalExpression.ValueIfFalse)
        };
        var compiledExpr = Emit(conditionalExpression, expr, new[] { endLabel, nextCaseLabel });

        // TODO verify that this works correctly
        endLabel.IsResolved = true;
        endLabel.CodeOffset = compiledExpr.CodeOffset + KismetExpressionSizeCalculator.CalculateExpressionSize(expr);

        nextCaseLabel.IsResolved = true;
        nextCaseLabel.CodeOffset = endLabel.CodeOffset - KismetExpressionSizeCalculator.CalculateExpressionSize(expr.DefaultTerm);

        return compiledExpr;
    }

    private CompiledExpressionContext CompileUnaryExpression(UnaryExpression unaryExpression)
    {
        if (unaryExpression is CastOperator castOperator)
        {
            return CompileCastExpression(castOperator);
        }
        else if (unaryExpression is PrefixOperator prefixOperator)
        {
            return CompilePrefixOperator(prefixOperator);
        }
        else if (unaryExpression is PostfixOperator postfixOperator)
        {
            return CompilePostfixOperator(postfixOperator);
        }
        else if (unaryExpression is TypeofOperator typeofOperator)
        {
            return CompileTypeofExpression(typeofOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompileTypeofExpression(TypeofOperator typeofOperator)
    {
        var typeSymbol = GetSymbol<ClassSymbol>(typeofOperator.Operand);
        return Emit(typeofOperator, new EX_ObjectConst()
        {
            Value = GetPackageIndex(typeSymbol),
        });
    }

    private CompiledExpressionContext CompilePrefixOperator(PrefixOperator prefixOperator)
    {
        if (prefixOperator is LogicalNotOperator logicalNotOperator)
        {
            return CompileLogicalNotOperator(logicalNotOperator);
        }
        else if (prefixOperator is NegationOperator negationOperator)
        {
            return CompileNegationOperator(negationOperator);
        }
        else if (prefixOperator is PrefixDecrementOperator prefixDecrementOperator)
        {
            return CompilePrefixDecrementOperator(prefixDecrementOperator);
        }
        else if (prefixOperator is PrefixIncrementOperator prefixIncrementOperator)
        {
            return CompilePrefixIncrementOperator(prefixIncrementOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompilePrefixIncrementOperator(PrefixIncrementOperator prefixIncrementOperator)
    {
        return CompileAssignmentOperator(new AssignmentOperator()
        {
            Left = prefixIncrementOperator.Operand,
            Right = new AdditionOperator()
            {
                Left = prefixIncrementOperator.Operand,
                Right = new IntLiteral(1),
                ExpressionValueKind = prefixIncrementOperator.ExpressionValueKind,
                SourceInfo = prefixIncrementOperator.SourceInfo
            },
            ExpressionValueKind = prefixIncrementOperator.ExpressionValueKind,
            SourceInfo = prefixIncrementOperator.SourceInfo
        });
    }

    private CompiledExpressionContext CompilePrefixDecrementOperator(PrefixDecrementOperator prefixDecrementOperator)
    {
        return CompileAssignmentOperator(new AssignmentOperator()
        {
            Left = prefixDecrementOperator.Operand,
            Right = new SubtractionOperator()
            {
                Left = prefixDecrementOperator.Operand,
                Right = new IntLiteral(1),
                ExpressionValueKind = prefixDecrementOperator.ExpressionValueKind,
                SourceInfo = prefixDecrementOperator.SourceInfo
            },
            ExpressionValueKind = prefixDecrementOperator.ExpressionValueKind,
            SourceInfo = prefixDecrementOperator.SourceInfo
        });
    }

    private CompiledExpressionContext CompileNegationOperator(NegationOperator negationOperator)
    {
        throw new NotImplementedException();
    }

    private CompiledExpressionContext CompileLogicalNotOperator(LogicalNotOperator logicalNotOperator)
    {
        return EmitMathLibraryCall(logicalNotOperator, "Not_PreBool");
    }

    private CompiledExpressionContext CompilePostfixOperator(PostfixOperator postfixOperator)
    {
        if (postfixOperator is PostfixDecrementOperator postfixDecrementOperator)
        {
            return CompilePostfixDecrementOperator(postfixDecrementOperator);
        }
        else if (postfixOperator is PostfixIncrementOperator postfixIncrementOperator)
        {
            return CompilePostfixIncrementOperator(postfixIncrementOperator);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private CompiledExpressionContext CompilePostfixIncrementOperator(PostfixIncrementOperator postfixIncrementOperator)
    {
        // TODO: ensure value is returned from expression
        return CompileAssignmentOperator(new AssignmentOperator()
        {
            Left = postfixIncrementOperator.Operand,
            Right = new AdditionOperator()
            {
                Left = postfixIncrementOperator.Operand,
                Right = new IntLiteral(1),
                ExpressionValueKind = postfixIncrementOperator.ExpressionValueKind,
                SourceInfo = postfixIncrementOperator.SourceInfo
            },
            ExpressionValueKind = postfixIncrementOperator.ExpressionValueKind,
            SourceInfo = postfixIncrementOperator.SourceInfo
        });
    }

    private CompiledExpressionContext CompilePostfixDecrementOperator(PostfixDecrementOperator postfixDecrementOperator)
    {
        // TODO: ensure value is returned from expression
        return CompileAssignmentOperator(new AssignmentOperator()
        {
            Left = postfixDecrementOperator.Operand,
            Right = new SubtractionOperator()
            {
                Left = postfixDecrementOperator.Operand,
                Right = new IntLiteral(1),
                ExpressionValueKind = postfixDecrementOperator.ExpressionValueKind,
                SourceInfo = postfixDecrementOperator.SourceInfo
            },
            ExpressionValueKind = postfixDecrementOperator.ExpressionValueKind,
            SourceInfo = postfixDecrementOperator.SourceInfo
        });
    }

    private CompiledExpressionContext CompileSubscriptOperator(SubscriptOperator subscriptOperator)
    {
        return Emit(subscriptOperator, new EX_ArrayGetByRef()
        {
            ArrayVariable = CompileSubExpression(subscriptOperator.Operand),
            ArrayIndex = CompileSubExpression(subscriptOperator.Index)
        });
    }

    private CompiledExpressionContext CompileInitializerList(InitializerList initializerListExpression)
    {
        throw new NotImplementedException();
    }
}
