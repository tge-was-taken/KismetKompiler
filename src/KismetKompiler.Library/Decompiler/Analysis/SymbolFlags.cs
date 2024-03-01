namespace KismetKompiler.Library.Decompiler.Analysis;

[Flags]
public enum SymbolFlags
{
    Import = 1<<0,
    Export = 1<<1,
    FProperty = 1 << 4,
    InferredFromImportClassPackage = 1<<2,
    InferredFromImportClassName = 1<<3,
    InferredFromFPropertySerializedType = 1 << 5,
    InferredFromCall = 1 << 6,
    ClonedFromGenVariable = 1 << 7,
    InferredFromKismetPropertyPointer = 1 << 8,
    UnresolvedClass = 1 << 9,
    EvaluationTemporary = 1 << 10,
    AnonymousClass = 1 << 11,
    FakeRootClass = 1 << 12,
}
