using KismetKompiler.Library.Decompiler.Context;
using System.Diagnostics;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Passes;

public class TypePropagationPass : IDecompilerPass
{
    public Node Execute(DecompilerContext context, Node root)
    {
        foreach (var node in root.Children)
        {
            if (node is BlockNode blockNode)
            {
                Execute(context, blockNode);
            }
            else
            {
                if (node.Source is EX_Context contextExpr)
                {
                    if (contextExpr.ContextExpression is EX_LocalVariable contextLocalExpr)
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
        }

        return root;
    }
}
