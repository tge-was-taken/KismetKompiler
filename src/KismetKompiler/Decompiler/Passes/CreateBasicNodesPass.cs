using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using KismetKompiler.Decompiler.Context;

namespace KismetKompiler.Decompiler.Passes
{
    public class CreateBasicNodesPass : IDecompilerPass
    {
        private Node CreateBasicNode(KismetExpression baseExpr, ref int codeOffset, Node parent)
        {
            var codeStartOffset = codeOffset;
            codeOffset += 1;
            switch (baseExpr)
            {
                case EX_LocalVariable expr:
                    codeOffset += 8;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                case EX_InstanceVariable expr:
                    codeOffset += 8;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset
                    };
                case EX_Return expr:
                    {
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        block.Children.Add(CreateBasicNode(expr.ReturnExpression, ref codeOffset, block));
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_Jump expr:
                    codeOffset += 4;
                    return new JumpNode()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                        Target = null
                    };
                case EX_JumpIfNot expr:
                    {
                        codeOffset += 4;
                        var node = new ConditionalJumpNode()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Inverted = true,
                        };
                        node.Condition = CreateBasicNode(expr.BooleanExpression, ref codeOffset, node);
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_Let expr:
                    {
                        codeOffset += 8;
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        block.Children.Add(CreateBasicNode(expr.Variable, ref codeOffset, block));
                        block.Children.Add(CreateBasicNode(expr.Expression, ref codeOffset, block));
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_LetBool expr:
                    {
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        block.Children.Add(CreateBasicNode(expr.VariableExpression, ref codeOffset, block));
                        block.Children.Add(CreateBasicNode(expr.AssignmentExpression, ref codeOffset, block));
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_Context expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                        };
                        var @object = CreateBasicNode(expr.ObjectExpression, ref codeOffset, node);
                        codeOffset += 4;
                        codeOffset += 8;
                        var context = CreateBasicNode(expr.ContextExpression, ref codeOffset, node);
                        node.Children.AddRange(new[] { @object, context });
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_IntConst expr:
                    codeOffset += 4;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                case EX_FloatConst expr:
                    codeOffset += 4;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                case EX_StringConst expr:
                    codeOffset += expr.Value.Length + 1;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                case EX_ByteConst expr:
                    codeOffset += 1;
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                case EX_UnicodeStringConst expr:
                    codeOffset += 2 * (expr.Value.Length + 1);
                    return new Node()
                    {
                        Parent = parent,
                        Source = expr,
                        CodeStartOffset = codeStartOffset,
                        CodeEndOffset = codeOffset,
                    };
                case EX_LocalVirtualFunction expr:
                    {
                        codeOffset += 12;
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        foreach (var param in expr.Parameters)
                            block.Children.Add(CreateBasicNode(param, ref codeOffset, block));
                        codeOffset += 1;
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_ComputedJump expr:
                    {
                        var node = new JumpNode()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Children = { }
                        };
                        node.Children.Add(CreateBasicNode(expr.CodeOffsetExpression, ref codeOffset, node));
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_InterfaceContext expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Children = { }
                        };
                        node.Children.Add(CreateBasicNode(expr.InterfaceValue, ref codeOffset, node));
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_CallMath expr:
                    {
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        codeOffset += 8;
                        foreach (var param in expr.Parameters)
                        {
                            block.Children.Add(CreateBasicNode(param, ref codeOffset, block));
                        }
                        codeOffset += 1;
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_LocalFinalFunction expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                        };
                        codeOffset += 8;
                        foreach (var param in expr.Parameters)
                        {
                            node.Children.Add(CreateBasicNode(param, ref codeOffset, node));
                        }
                        codeOffset += 1;
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_LocalOutVariable expr:
                    {
                        codeOffset += 8;
                        return new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr };
                    }
                case EX_StructMemberContext expr:
                    {
                        codeOffset += 8;
                        var node = new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr, Children = { } };
                        node.Children.Add(CreateBasicNode(expr.StructExpression, ref codeOffset, node));
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_ObjectConst expr:
                    {
                        codeOffset += 8;
                        return new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr };
                    }
                case EX_DeprecatedOp4A exp1:
                case EX_Nothing exp2:
                case EX_EndOfScript exp3:
                case EX_IntZero exp4:
                case EX_IntOne exp5:
                case EX_True exp6:
                case EX_False exp7:
                case EX_NoObject exp8:
                case EX_NoInterface exp9:
                case EX_Self exp10:
                    {
                        return new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = baseExpr };
                    }
                case EX_FinalFunction expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                        };
                        codeOffset += 8;
                        foreach (var param in expr.Parameters)
                        {
                            node.Children.Add(CreateBasicNode(param, ref codeOffset, node));
                        }
                        codeOffset += 1;
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_PushExecutionFlow expr:
                    {
                        codeOffset += 4;
                        return new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr };
                    }
                case EX_SetArray expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr,
                            Children = { }
                        };
                        node.Children.Add(CreateBasicNode(expr.AssigningProperty, ref codeOffset, node));
                        foreach (var item in expr.Elements)
                        {
                            node.Children.Add(CreateBasicNode(item, ref codeOffset, node));
                        }
                        codeOffset += 1;
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_PopExecutionFlow expr:
                    {
                        return new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr };
                    }
                case EX_PopExecutionFlowIfNot expr:
                    {
                        var node = new Node() { Parent = parent, CodeStartOffset = codeStartOffset, CodeEndOffset = codeOffset, Source = expr, Children = { } };
                        node.Children.Add(CreateBasicNode(expr.BooleanExpression, ref codeOffset, node));
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_SwitchValue expr:
                    {
                        codeOffset += 6;
                        var node = new JumpNode()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr,
                            Children =
                        {

                        }
                        };
                        node.Children.Add(CreateBasicNode(expr.IndexTerm, ref codeOffset, node));
                        for (int i = 0; i < expr.Cases.Length; i++)
                        {
                            node.Children.Add(CreateBasicNode(expr.Cases[i].CaseIndexValueTerm, ref codeOffset, node));
                            codeOffset += 4;
                            node.Children.Add(CreateBasicNode(expr.Cases[i].CaseTerm, ref codeOffset, node));
                        }
                        node.Children.Add(CreateBasicNode(expr.DefaultTerm, ref codeOffset, node));
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_LetObj expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr,
                            Children =
                        {
                        }
                        };
                        node.Children.AddRange(new[]{
                        CreateBasicNode(expr.VariableExpression, ref codeOffset, node),
                        CreateBasicNode(expr.AssignmentExpression, ref codeOffset, node) });
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_VectorConst expr:
                    {
                        codeOffset += sizeof(float) * 3;
                        return new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr
                        };
                    }
                case EX_RotationConst expr:
                    {
                        codeOffset += sizeof(float) * 3;
                        return new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr
                        };
                    }
                case EX_VirtualFunction expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                        };
                        codeOffset += 12;
                        foreach (var param in expr.Parameters)
                        {
                            node.Children.Add(CreateBasicNode(param, ref codeOffset, node));
                        }
                        codeOffset += 1;
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                case EX_LetValueOnPersistentFrame expr:
                    {
                        codeOffset += 8;
                        var block = new Node()
                        {
                            Parent = parent,
                            Source = expr,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset
                        };
                        block.Children.Add(CreateBasicNode(expr.AssignmentExpression, ref codeOffset, block));
                        block.CodeEndOffset = codeOffset;
                        return block;
                    }
                case EX_NameConst expr:
                    {
                        codeOffset += 12;
                        return new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr
                        };
                    }
                case EX_ArrayGetByRef expr:
                    {
                        var node = new Node()
                        {
                            Parent = parent,
                            CodeStartOffset = codeStartOffset,
                            CodeEndOffset = codeOffset,
                            Source = expr,
                            Children =
                        {

                        }
                        };
                        node.Children.AddRange(new[] { CreateBasicNode(expr.ArrayVariable, ref codeOffset, node),
                            CreateBasicNode(expr.ArrayIndex, ref codeOffset, node)});
                        node.CodeEndOffset = codeOffset;
                        return node;
                    }
                default:
                    throw new NotImplementedException(baseExpr.Inst);
            }
        }

        private BlockNode CreateFunctionBasicNode(IList<KismetExpression> expressions)
        {
            var root = new BlockNode()
            {
                CodeEndOffset = 0,
                CodeStartOffset = 0,
                Parent = null,
                Source = null,
            };
            var nextIndex = 0;
            foreach (var expr in expressions)
            {
                var node = CreateBasicNode(expr, ref nextIndex, root);
                root.Children.Add(node);
            }
            return root;
        }

        public Node Execute(DecompilerContext context, Node node)
        {
            return CreateFunctionBasicNode(context.Function.ScriptBytecode);
        }
    }
}
