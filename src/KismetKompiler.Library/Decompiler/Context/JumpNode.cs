namespace KismetKompiler.Library.Decompiler.Context;

public class JumpNode : Node
{
    public Node Target { get; set; }

    public override string ToString()
    {
        return $"{CodeStartOffset}: {Source.Inst} -> {Target}";
    }
}
