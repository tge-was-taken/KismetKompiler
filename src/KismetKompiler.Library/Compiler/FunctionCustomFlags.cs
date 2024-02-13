namespace KismetKompiler.Library.Compiler;

// TODO: refactor symbol/context handling
// How it works currently is very confusing

[Flags]
public enum FunctionCustomFlags
{
    FinalFunction = 1<<0,
    LocalFinalFunction = 1<<1,
    VirtualFunction = 1 << 2,
    LocalVirtualFunction = 1 << 3,
    MathFunction = 1 << 4,
    Extern = 1 << 5,
    UnknownSignature = 1 << 6,

    CallTypeOverride = FinalFunction | LocalFinalFunction | VirtualFunction | LocalVirtualFunction | MathFunction,
    LocalCall = LocalFinalFunction | LocalVirtualFunction,
}
