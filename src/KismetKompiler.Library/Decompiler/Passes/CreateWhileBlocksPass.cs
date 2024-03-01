using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using System.ComponentModel;
using System.Xml.Linq;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public class CreateWhileBlocksPass : IDecompilerPass
    {
        public Node Execute(DecompilerContext context, Node root)
        {
            for (int blockIndex = 0; blockIndex < root.Children.Count; blockIndex++)
            {
                Node? block = root.Children[blockIndex];
                var firstNode = block.Children.FirstOrDefault();

                // Assume EX_PushExecutionFlow has its own basic block due to being part of flow control
                if (firstNode?.Source is EX_PushExecutionFlow pushExecutionFlow &&
                    block.Children.Count == 1)
                {
                    var endBlock = root.Children.FirstOrDefault(x => x.CodeStartOffset == pushExecutionFlow.PushingAddress);
                    if (endBlock != null)
                    {
                        var endBlockIndex = root.Children.IndexOf(endBlock);

                        var bodyStartIndex = blockIndex + 1;
                        var bodyEndIndex = endBlockIndex;
                        var bodyBlockCount = bodyEndIndex - bodyStartIndex;

                        if (bodyBlockCount > 0)
                        {
                            var bodyNodes = root.Children.Skip(bodyStartIndex).Take(bodyBlockCount).ToList();
                            var newBlock = new JumpBlockNode()
                            {
                                Parent = block.Parent,
                                CodeStartOffset = block.CodeStartOffset,
                                CodeEndOffset = endBlock.CodeStartOffset,
                                Source = block.Source,
                                Children = bodyNodes,
                                ReferencedBy = block.ReferencedBy,
                            };
                            // Fix parent
                            foreach (var subNode in newBlock.Children)
                                subNode.Parent = newBlock;
                            root.Children[blockIndex] = newBlock;
                            root.Children.RemoveRange(bodyStartIndex, bodyBlockCount);
                            Execute(context, newBlock);
                        }
                    }
                }
                else
                {
                    Execute(context, block);
                }
            }

            return root;
        }
    }
}
