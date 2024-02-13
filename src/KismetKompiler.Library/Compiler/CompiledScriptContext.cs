using KismetKompiler.Library.Compiler.Context;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Compiler;

public abstract class CompiledDeclarationContext
{
    public virtual Symbol Symbol { get; }

    public CompiledDeclarationContext(Symbol symbol)
    {
        Symbol = symbol;
    }
}

public class CompiledDeclarationContext<T> : CompiledDeclarationContext where T : Symbol
{
    public CompiledDeclarationContext(T symbol) : base(symbol)
    {
        Symbol = symbol;
    }

    public override T Symbol { get; }
}

public class CompiledLabelContext : CompiledDeclarationContext<LabelSymbol>
{
    public CompiledLabelContext(LabelSymbol symbol) : base(symbol) { }
    public int CodeOffset { get; set; }
}

public class CompiledFunctionContext : CompiledDeclarationContext<ProcedureSymbol>
{
    public CompiledFunctionContext(ProcedureSymbol symbol) : base(symbol) { }
    public List<CompiledVariableContext> Variables { get; init; } = new();
    public List<CompiledLabelContext> Labels { get; init; } = new();
    public List<KismetExpression> Bytecode { get; init; } = new();
}

public class CompiledVariableContext : CompiledDeclarationContext<VariableSymbol>
{
    public CompiledVariableContext(VariableSymbol symbol) : base(symbol) { }

    public CompiledClassContext Type { get; set; }
}

public class CompiledClassContext : CompiledDeclarationContext<ClassSymbol>
{
    public CompiledClassContext(ClassSymbol symbol) : base(symbol) { }
    public EClassFlags Flags { get; set; }
    public CompiledClassContext? BaseClass { get; set; }
    public List<CompiledVariableContext> Variables { get; init; } = new();
    public List<CompiledFunctionContext> Functions { get; init; } = new();
}

public class CompiledImportContext : CompiledDeclarationContext<PackageSymbol>
{
    public CompiledImportContext(PackageSymbol symbol) : base(symbol) { }
    public List<CompiledDeclarationContext> Declarations { get; init; } = new();
}

public class CompiledScriptContext
{
    public List<CompiledImportContext> Imports { get; init; } = new();
    public List<CompiledVariableContext> Variables { get; init; } = new();
    public List<CompiledFunctionContext> Functions { get; init; } = new();
    public List<CompiledClassContext> Classes { get; init; } = new();
}
