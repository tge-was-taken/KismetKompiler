using KismetKompiler.Decompiler.Context;

namespace KismetKompiler.Decompiler.Passes
{
    public interface IDecompilerPass
    {
        Node Execute(DecompilerContext context, Node node);
    }
}
