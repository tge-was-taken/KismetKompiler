using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public class CreateIfBlocksPass : IDecompilerPass
    {
        public Node Execute(DecompilerContext context, Node root)
        {
            // TODO: else block
            for (int i = 0; i < root.Children.Count; i++)
            {
                Node? block = root.Children[i];
                var terminalNode = block.Children.LastOrDefault();
                if (terminalNode is JumpNode jumpNode)
                {
                    // Block ends with a jump node
                    if (jumpNode.Source is EX_JumpIfNot jumpIfNot &&
                        i + 1 < root.Children.Count)
                    {
                        // Block ends with a jump if not, and is not the last instruction
                        var startBlockIndex = i + 1;
                        if (startBlockIndex < root.Children.Count)
                        {
                            // Target of the if statement must be at the start of a block to seamlessly connect
                            if (jumpNode.Target.CodeStartOffset == jumpNode.Target.Parent?.CodeStartOffset)
                            {
                                var endBlockIndex = root.Children.IndexOf(jumpNode.Target.Parent);
                                var blockCount = endBlockIndex - startBlockIndex;
                                if (blockCount > 0)
                                {
                                    var blocks = root.Children.Skip(startBlockIndex).Take(blockCount).ToList();

                                    var ifBlockNode = new IfBlockNode()
                                    {
                                        Parent = root,
                                        CodeStartOffset = jumpNode.CodeStartOffset,
                                        CodeEndOffset = jumpNode.Target.CodeEndOffset,
                                        Condition = jumpIfNot.BooleanExpression,
                                        Source = jumpNode.Source,
                                        Children = blocks,
                                        ReferencedBy = jumpNode.ReferencedBy,
                                    };
                                    block.Children.Remove(terminalNode);

                                    // Fix parent
                                    foreach (var subNode in ifBlockNode.Children)
                                        subNode.Parent = ifBlockNode;


                                    root.Children[startBlockIndex] = ifBlockNode;
                                    if (blockCount > 1)
                                    {
                                        // Remove merged blocks
                                        root.Children.RemoveRange(startBlockIndex + 1, blockCount - 1);
                                    }

                                    Execute(context, ifBlockNode);
                                }
                            }
                        }
                    }
                }
            }

            return root;
        }
    }
}
