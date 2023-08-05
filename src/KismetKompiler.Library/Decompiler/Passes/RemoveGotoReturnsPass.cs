using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Passes
{
    internal class RemoveGotoReturnsPass : IDecompilerPass
    {
        public Node Execute(DecompilerContext context, Node block)
        {
            for (int i = 0; i < block.Children.Count; i++)
            {
                var node = block.Children[i];
                if (node is JumpNode jumpNode)
                {
                    if (jumpNode.Target?.Source is EX_Return returnExpr &&
                        returnExpr.ReturnExpression is EX_Nothing)
                    {
                        // Jump to return statement that does not return anything
                        block.Children[i] = new ReturnNode()
                        {
                            Parent = block,
                            CodeEndOffset = node.CodeEndOffset,
                            CodeStartOffset = node.CodeStartOffset,
                            Source = node.Source,
                            ReferencedBy = node.ReferencedBy
                        };
                    }
                }
                else if (node is BlockNode blockNode)
                {
                    Execute(context, blockNode);
                }
            }

            return block;
        }
    }
}
