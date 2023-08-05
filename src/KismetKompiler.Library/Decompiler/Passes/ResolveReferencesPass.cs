using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public class ResolveReferencesPass : IDecompilerPass
    {
        private IEnumerable<JumpNode> FindJumpNodesForIndex(Node root, int index)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i] is JumpNode jumpNode &&
                    jumpNode.Target?.CodeStartOffset == index)
                {
                    yield return jumpNode;
                }
            }
        }

        private IEnumerable<Node> FindPushExecutionFlowsForIndex(Node root, int index)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i].Source is EX_PushExecutionFlow expr && expr.PushingAddress == index)
                {
                    yield return root.Children[i];
                }
                else
                {
                    foreach (var item in FindPushExecutionFlowsForIndex(root.Children[i], index))
                        yield return item;
                }
            }
        }

        public Node Execute(DecompilerContext context, Node root)
        {
            foreach (var node in root.Children)
            {
                foreach (var reference in
                        FindJumpNodesForIndex(root, node.CodeStartOffset)
                        .Union(FindPushExecutionFlowsForIndex(root, node.CodeStartOffset)))
                {
                    node.ReferencedBy.Add(reference);
                }
            }

            return root;
        }
    }
}
