using KismetKompiler.Library.Decompiler.Context;

namespace KismetKompiler.Library.Decompiler.Passes
{
    public interface IDecompilerPass
    {
        Node Execute(DecompilerContext context, Node root);
    }
}
