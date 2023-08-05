using KismetKompiler.Library.Decompiler.Context;
using KismetKompiler.Library.Decompiler.Context.Nodes;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public interface IDecompilerPass
    {
        Node Execute(DecompilerContext context, Node root);
    }
}
