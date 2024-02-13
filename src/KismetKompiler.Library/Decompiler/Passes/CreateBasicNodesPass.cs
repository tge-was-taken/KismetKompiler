using UAssetAPI.Kismet.Bytecode.Expressions;
using UAssetAPI.Kismet.Bytecode;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Utilities;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public class CreateBasicNodesPass : IDecompilerPass
    {
        private BlockNode CreateFunctionBasicNode(IList<KismetExpression> expressions)
        {
            var root = new BlockNode()
            {
                CodeStartOffset = 0,
                Parent = null,
                Source = null,
            };
            var visitor = new CreateBasicNodesPassExpressionVisitor(root);
            visitor.Visit(expressions);
            return root;
        }

        public Node Execute(DecompilerContext context, Node node)
        {
            return CreateFunctionBasicNode(context.Function.ScriptBytecode);
        }

        private class CreateBasicNodesPassExpressionVisitor : KismetExpressionVisitor<Node>
        {
            private Stack<Node> _stack = new();

            public CreateBasicNodesPassExpressionVisitor(Node root)
            {
                _stack.Push(root);
            }

            protected override void OnEnter(KismetExpressionContext<Node> context)
            {
                var parent = _stack.Peek();
                var node = context.Expression switch
                {
                    EX_Jump or EX_ComputedJump or EX_SwitchValue or EX_PopExecutionFlow => new JumpNode()
                    {
                        CodeStartOffset = context.CodeStartOffset,
                        Parent = parent,
                        Source = context.Expression,
                        Target = null
                    },
                    EX_JumpIfNot or EX_PopExecutionFlowIfNot => new ConditionalJumpNode()
                    {
                        CodeStartOffset = context.CodeStartOffset,
                        Parent = parent,
                        Source = context.Expression,
                        Target = null,
                        Inverted = true
                    },
                    _ => new Node()
                    {
                        CodeStartOffset = context.CodeStartOffset,
                        Parent = parent,
                        Source = context.Expression,
                    }
                };
                parent.Children.Add(node);
                _stack.Push(node);

                base.OnEnter(context);
            }

            protected override void OnExit(KismetExpressionContext<Node> context)
            {
                var node = _stack.Pop();
                node.CodeEndOffset = context.CodeEndOffset.Value;

                base.OnExit(context);
            }
        }
    }
}
