namespace KismetKompiler.Library.Decompiler.Analysis;

public class SymbolFunctionMetadata
{
    public CallingConvention CallingConvention { get; set; }
    public List<Symbol> Parameters { get; set; } = new();
    public Symbol? ReturnType { get; set; }
}
