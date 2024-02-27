using KismetKompiler.Library.Utilities;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace KismetKompiler.Library.Decompiler.Analysis.Visitors;

/// <summary>
/// Creates any missing symbols inferred from Kismet property pointers used in various instructions.
/// </summary>
public class CreateKismetPropertyPointerSymbolsVisitor : KismetExpressionVisitor
{
    private readonly FunctionAnalysisContext _context;

    public CreateKismetPropertyPointerSymbolsVisitor(FunctionAnalysisContext context)
    {
        _context = context;
    }

    private Symbol? EnsurePropertySymbolCreated(KismetPropertyPointer pointer)
        => VisitorHelper.EnsurePropertySymbolCreated(_context, pointer);

    protected override void OnEnter(KismetExpressionContext<VisitorContext> context)
    {
        base.OnEnter(context);

        switch (context.Expression)
        {
            case EX_ArrayConst arrayConst:
                EnsurePropertySymbolCreated(arrayConst.InnerProperty);
                break;
            case EX_ClassSparseDataVariable classSparseDataVariable:
                EnsurePropertySymbolCreated(classSparseDataVariable.Variable);
                break;
            case EX_Context ctx:
                EnsurePropertySymbolCreated(ctx.RValuePointer);
                break;
            case EX_DefaultVariable defaultVariable:
                EnsurePropertySymbolCreated(defaultVariable.Variable);
                break;
            case EX_InstanceVariable instanceVariable:
                EnsurePropertySymbolCreated(instanceVariable.Variable);
                break;
            case EX_Let let:
                EnsurePropertySymbolCreated(let.Value);
                break;
            case EX_LetValueOnPersistentFrame letValueOnPersistentFrame:
                EnsurePropertySymbolCreated(letValueOnPersistentFrame.DestinationProperty);
                break;
            case EX_LocalOutVariable localOutVariable:
                EnsurePropertySymbolCreated(localOutVariable.Variable);
                break;
            case EX_LocalVariable localVariable:
                EnsurePropertySymbolCreated(localVariable.Variable);
                break;
            case EX_MapConst mapConst:
                EnsurePropertySymbolCreated(mapConst.KeyProperty);
                EnsurePropertySymbolCreated(mapConst.ValueProperty);
                break;
            case EX_PropertyConst propertyConst:
                EnsurePropertySymbolCreated(propertyConst.Property);
                break;
            case EX_SetConst setConst:
                EnsurePropertySymbolCreated(setConst.InnerProperty);
                break;
            case EX_StructMemberContext structMemberContext:
                EnsurePropertySymbolCreated(structMemberContext.StructMemberExpression);
                break;
        }
    }
}
