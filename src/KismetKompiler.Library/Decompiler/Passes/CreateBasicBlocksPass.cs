using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public class CreateBasicBlocksPass : IDecompilerPass
    {
        public Node Execute(DecompilerContext context, Node root)
        {
            var newNodes = new List<Node>();

            for (int i = 0; i < root.Children.Count; i++)
            {
                var start = i;
                var end = root.Children.Count - 1;
                for (int j = i; j < root.Children.Count; j++)
                {
                    if (root.Children[j] is JumpNode ||
                        root.Children[j].Source.Token == EExprToken.EX_PushExecutionFlow ||
                        root.Children[j].Source.Token == EExprToken.EX_EndOfScript)
                    {
                        // A jump has been found
                        end = j + 1;
                        break;
                    }

                    if (root.Children[j].ReferencedBy.Count != 0 && j != i)
                    {
                        // Something jumps to this
                        end = j;
                        break;
                    }
                }

                var nodes = root.Children
                    .Skip(start)
                    .Take(end - start);
                var blockNode = new BlockNode()
                {
                    Source = null,
                    Parent = nodes.First().Parent,
                    CodeStartOffset = nodes.First().CodeStartOffset,
                    CodeEndOffset = nodes.Last().CodeEndOffset,
                    Children = nodes.ToList(),
                    ReferencedBy = nodes.First().ReferencedBy,
                };

                foreach (var node in blockNode.Children)
                {
                    node.Parent = blockNode;
                }

                newNodes.Add(blockNode);

                i = end - 1;
            }

            root.Children.Clear();
            root.Children.AddRange(newNodes);
            return root;
        }
    }
}
