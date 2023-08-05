namespace KismetKompiler.Library.Decompiler.Context.Nodes;

public class JumpNode : Node
{
    public Node Target { get; set; }

    public override string ToString()
    {
        return $"{CodeStartOffset}: {Source.Inst} -> {Target}";
    }
}
