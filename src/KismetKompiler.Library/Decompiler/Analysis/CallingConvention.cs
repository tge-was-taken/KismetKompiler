namespace KismetKompiler.Library.Decompiler.Analysis;

[Flags]
public enum CallingConvention
{
    CallMath = 1<<0,
    LocalVirtualFunction = 1<<1,
    LocalFinalFunction = 1<<2,
    VirtualFunction = 1<<3,
    FinalFunction = 1<<4,
    CallMulticastDelegate = 1 << 5
}
