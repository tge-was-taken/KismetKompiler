using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Syntax;
using KismetKompiler.Library.Syntax.Statements;
using KismetKompiler.Library.Syntax.Statements.Declarations;
using System.Collections;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.UnrealTypes;

namespace KismetKompiler.Library.Compiler.Context
{
    public enum SymbolCategory
    {
        Variable,
        Package,
        Class,
        Procedure,
        Label,
        Any,
        Enum,
        EnumValue
    }

    public interface IExportSymbol
    {
    }

    public record struct SymbolKey(string Name, SymbolCategory Category);


    public interface ISymbolTable : IEnumerable<Symbol>
    {
        Symbol? GetSymbol(string name);
        Symbol? GetSymbol(string name, SymbolCategory category);
        Symbol? GetSymbol(Declaration declaration);
        T? GetSymbol<T>(string name) where T : Symbol;
        T? GetSymbol<T>(Declaration declaration) where T : Symbol;

        Symbol? GetRequiredSymbol(string name);
        Symbol? GetRequiredSymbol(string name, SymbolCategory category);
        T? GetRequiredSymbol<T>(string name) where T : Symbol;

        void DeclareSymbol(Symbol symbol);
        bool SymbolExists(string name);
        bool SymbolExists(string name, SymbolCategory category);
        bool SymbolExists<T>(string name);
    }

    public abstract class SymbolTableBase : ISymbolTable
    {
        private static readonly Dictionary<Type, SymbolCategory> _typeToCategory = new()
        {
            [typeof(VariableSymbol)] = SymbolCategory.Variable,
            [typeof(PackageSymbol)] = SymbolCategory.Package,
            [typeof(ClassSymbol)] = SymbolCategory.Class,
            [typeof(ProcedureSymbol)] = SymbolCategory.Procedure,
            [typeof(LabelSymbol)] = SymbolCategory.Label
        };

        public abstract void DeclareSymbol(Symbol symbol);
        public abstract Symbol? GetSymbol(string name, SymbolCategory category);
        public abstract Symbol? GetSymbol(Declaration declaration);
        public abstract bool SymbolExists(string name, SymbolCategory category);
        public abstract IEnumerator<Symbol> GetEnumerator();

        public Symbol? GetSymbol(string name)
            => GetSymbol(name, SymbolCategory.Any);

        public T? GetSymbol<T>(string name) where T : Symbol
        {
            if (typeof(T) == typeof(Symbol)) return (T?)GetSymbol(name, SymbolCategory.Any);
            return (T?)GetSymbol(name, _typeToCategory[typeof(T)]);
        }

        public Symbol? GetRequiredSymbol(string name)
            => GetSymbol(name, SymbolCategory.Any) ?? throw new UnknownSymbolError(name);

        public Symbol? GetRequiredSymbol(string name, SymbolCategory category)
            => GetSymbol(name, category) ?? throw new UnknownSymbolError(name);

        public T? GetRequiredSymbol<T>(string name) where T : Symbol
            => GetSymbol<T>(name) ?? throw new UnknownSymbolError(name);

        public bool SymbolExists(string name)
            => SymbolExists(name, SymbolCategory.Any);

        public bool SymbolExists<T>(string name)
            => SymbolExists(name, _typeToCategory[typeof(T)]);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public T? GetSymbol<T>(Declaration declaration) where T : Symbol
            => (T)GetSymbol(declaration);
    }

    public abstract class Symbol : SymbolTableBase
    {
        private Symbol? _declaringSymbol;
        private Symbol? _baseSymbol;
        private Symbol? _innerSymbol;
        private List<Symbol> _members = new();
        private List<Symbol> _inheritors = new();
        private List<Symbol> _usedBy = new();

        public required Symbol? DeclaringSymbol
        {
            get => _declaringSymbol;
            set
            {
                if (_declaringSymbol != value &&
                    _declaringSymbol != null)
                {
                    _declaringSymbol._members.Remove(this);
                }
                _declaringSymbol = value;
                _declaringSymbol?._members.Add(this);
            }
        }

        public Symbol? BaseSymbol
        {
            get => _baseSymbol;
            set
            {
                if (_baseSymbol != value &&
                    _baseSymbol != null)
                {
                    _baseSymbol._inheritors.Remove(this);
                }
                _baseSymbol = value;
                _baseSymbol?._inheritors.Add(this);
            }
        }

        public Symbol? InnerSymbol
        {
            get => _innerSymbol;
            set
            {
                if (_innerSymbol != value &&
                    _innerSymbol != null)
                {
                    _innerSymbol._usedBy.Remove(this);
                }
                _innerSymbol = value;
                _innerSymbol?._usedBy.Add(this);
            }
        }

        public required string Name { get; init; }
        public required bool IsExternal { get; init; }
        public virtual Declaration Declaration { get; }
        public IReadOnlyList<Symbol> Members => _members;
        public IReadOnlyList<Symbol> Inheritors => _inheritors;
        public IReadOnlyList<Symbol> UsedBy => _usedBy;
        public abstract SymbolCategory SymbolCategory { get; }
        public SymbolKey Key => new(Name, SymbolCategory);
        public ClassSymbol? DeclaringClass
            => DeclaringSymbol is ClassSymbol symbol ? symbol : DeclaringSymbol?.DeclaringClass;
        public ProcedureSymbol? DeclaringProcedure
            => DeclaringSymbol is ProcedureSymbol symbol ? symbol : DeclaringSymbol?.DeclaringProcedure;
        public PackageSymbol? DeclaringPackage
            => DeclaringSymbol is PackageSymbol symbol ? symbol : DeclaringSymbol?.DeclaringPackage;
        public ClassSymbol? BaseClass
            => BaseSymbol is ClassSymbol symbol ? symbol : null;

        public override string ToString()
        {
            return $"{SymbolCategory} {Name} {IsExternal}";
        }

        public override void DeclareSymbol(Symbol symbol)
        {
            if (SymbolExists(symbol.Name, symbol.SymbolCategory))
                throw new RedefinitionError(symbol);
            symbol.DeclaringSymbol = this;
        }

        public override Symbol? GetSymbol(string name, SymbolCategory category)
            => _members.SingleOrDefault(x => x.Name == name && (category == SymbolCategory.Any || x.SymbolCategory == category)) ?? BaseSymbol?.GetSymbol(name, category);

        public override bool SymbolExists(string name, SymbolCategory category)
            => _members.Any(x => x.Name == name && (category == SymbolCategory.Any || x.SymbolCategory == category)) || (BaseSymbol?.SymbolExists(name, category) ?? false);

        public override IEnumerator<Symbol> GetEnumerator()
            => _members.Union(BaseSymbol ?? Enumerable.Empty<Symbol>()).Distinct().GetEnumerator();

        public override Symbol? GetSymbol(Declaration declaration)
            => _members.SingleOrDefault(x => x.Declaration == declaration) ?? BaseSymbol?.GetSymbol(declaration);
    }

    public abstract class DeclaredSymbol<T> : Symbol where T : Declaration
    {
        public override T Declaration { get; }

        public DeclaredSymbol(T declaration)
        {
            Declaration = declaration;
        }
    }

    public enum VariableCategory
    {
        Instance,
        Local,
        Global,
        This,
        Base
    }

    public class VariableSymbol : DeclaredSymbol<VariableDeclaration>, IExportSymbol
    {
        public VariableSymbol(VariableDeclaration declaration) : base(declaration)
        {
        }

        public bool IsParameter => Parameter != null;
        public bool IsOutParameter => Parameter?.Modifier.HasFlag(ParameterModifier.Out) ?? false;
        public override SymbolCategory SymbolCategory => SymbolCategory.Variable;
        public VariableCategory VariableCategory
        {
            get
            {
                if (Name == "this") return VariableCategory.This;
                if (Name == "base") return VariableCategory.Base;
                if (DeclaringProcedure != null) return VariableCategory.Local;
                if (DeclaringClass != null) return VariableCategory.Instance;
                return VariableCategory.Global;
            }
        }
        public Argument? Argument { get; set; }
        public Parameter? Parameter { get; set; }
        public bool AllowShadowing { get; set; } = false;
        public bool IsReadOnly { get; set; } = false;
        public bool IsReturnParameter { get; set; } = false;
        public EPropertyFlags Flags { get; set; }
    }

    public class PackageSymbol : Symbol
    {
        public PackageSymbol() : base()
        {
        }

        // TODO
        public override SymbolCategory SymbolCategory => SymbolCategory.Package;
    }

    public class ClassSymbol : DeclaredSymbol<ClassDeclaration>, IExportSymbol
    {
        public ClassSymbol(ClassDeclaration declaration) : base(declaration)
        {
        }

        public override SymbolCategory SymbolCategory => SymbolCategory.Class;

        public bool IsInterface { get; internal set; }

        public bool IsStatic => Declaration.Modifiers.HasFlag(ClassModifiers.Static);
    }

    public class EnumSymbol : DeclaredSymbol<EnumDeclaration>, IExportSymbol
    {
        public EnumSymbol(EnumDeclaration declaration) : base(declaration)
        {
        }

        public override SymbolCategory SymbolCategory => SymbolCategory.Enum;
    }

    public class EnumValueSymbol : DeclaredSymbol<EnumValueDeclaration>, IExportSymbol
    {
        public EnumValueSymbol(EnumValueDeclaration declaration) : base(declaration)
        {
        }

        public int Value { get; init; }

        public override SymbolCategory SymbolCategory => SymbolCategory.EnumValue;
    }

    public class ProcedureSymbol : DeclaredSymbol<ProcedureDeclaration>, IExportSymbol
    {
        public ProcedureSymbol(ProcedureDeclaration declaration) : base(declaration)
        {
        }

        public override SymbolCategory SymbolCategory => SymbolCategory.Procedure;

        public EFunctionFlags Flags { get; init; } = 0;

        public FunctionCustomFlags CustomFlags { get; init; } = 0;

        public bool IsVirtual
            => Declaration?.IsVirtual ?? false;

        public bool HasAllFunctionFlags(EFunctionFlags flags)
            => (Flags & flags) == flags;

        public bool HasAnyFunctionFlags(EFunctionFlags flags)
            => (Flags & flags) != 0;

        public bool HasAllFunctionExtendedFlags(FunctionCustomFlags flags)
            => (CustomFlags & flags) == flags;

        public bool HasAnyFunctionCustomFlags(FunctionCustomFlags flags)
            => (CustomFlags & flags) != 0;
    }

    public class LabelSymbol : DeclaredSymbol<LabelDeclaration>
    {
        public LabelSymbol(LabelDeclaration declaration) : base(declaration)
        {
        }

        public int? CodeOffset { get; set; }

        public bool IsResolved { get; set; }

        public override SymbolCategory SymbolCategory => SymbolCategory.Label;
    }

    public class SymbolTable : SymbolTableBase
    {
        private Dictionary<SymbolKey, Symbol> symbolByKey = new();
        private Dictionary<string, List<Symbol>> symbolsByName = new();

        public bool CanDeclareSymbol(Symbol symbol)
            => !SymbolExists(symbol.Name, symbol.SymbolCategory);

        public override bool SymbolExists(string name, SymbolCategory category)
            => symbolByKey.ContainsKey(new(name, category));

        public override void DeclareSymbol(Symbol symbol)
        {
            if (symbolByKey.ContainsKey(symbol.Key))
                throw new RedefinitionError(symbol);

            symbolByKey[symbol.Key] = symbol;

            if (symbolsByName.ContainsKey(symbol.Name))
                symbolsByName[symbol.Name].Add(symbol);
            else
                symbolsByName[symbol.Name] = new List<Symbol> { symbol };
        }

        public override Symbol? GetSymbol(string name, SymbolCategory category)
        {
            if (category == SymbolCategory.Any)
            {
                if (!symbolsByName.ContainsKey(name))
                    return null;
                return symbolsByName[name].SingleOrDefault();
            }
            else
            {
                var key = new SymbolKey(name, category);
                if (!symbolByKey.ContainsKey(key))
                    return null;
                return symbolByKey[key];
            }
        }

        public override IEnumerator<Symbol> GetEnumerator()
            => symbolsByName.SelectMany(x => x.Value).GetEnumerator();

        public override Symbol? GetSymbol(Declaration declaration)
            => symbolsByName.SelectMany(x => x.Value).Where(x => x.Declaration == declaration).SingleOrDefault();
    }

    public class UnknownSymbol : Symbol
    {
        public override SymbolCategory SymbolCategory => SymbolCategory.Any;
    }

    public enum ContextType
    {
        None,
        ObjectConst,
        Interface,
        Struct,
        Class,
        This,
        Package,
        Procedure,
        Base,
        Enum,
        Object,
        SubContext
    }

    public class MemberContext : SymbolTableBase
    {
        public required ContextType Type { get; init; }
        public required Symbol Symbol { get; init; }
        public MemberContext? SubContext { get; set; }
        public bool CallVirtualFunctionAsFinal { get; internal set; }
        public bool IsImplicit { get; set; }

        public override void DeclareSymbol(Symbol symbol)
        {
            throw new InvalidOperationException();
        }

        public override Symbol? GetSymbol(string name, SymbolCategory category)
        {
            return SubContext?.GetSymbol(name, category) ?? Symbol?.GetSymbol(name, category);
        }

        public override bool SymbolExists(string name, SymbolCategory category)
        {
            return SubContext?.SymbolExists(name, category) ?? Symbol?.SymbolExists(name, category) ?? false;
        }

        public override IEnumerator<Symbol> GetEnumerator()
            => SubContext?.GetEnumerator() ?? Symbol?.GetEnumerator() ?? Enumerable.Empty<Symbol>().GetEnumerator();

        public override Symbol? GetSymbol(Declaration declaration)
            => SubContext?.GetSymbol(declaration) ?? Symbol?.GetSymbol(declaration);
    }

    public class Scope : SymbolTableBase
    {
        private LabelSymbol? _breakLabel;
        private LabelSymbol? _continueLabel;
        private bool? _isExecutionFlow;

        public Scope Parent { get; set; }
        public Symbol? DeclaringSymbol { get; set; }
        public ISymbolTable SymbolTable { get; set; }
        public LabelSymbol? BreakLabel
        {
            get => _breakLabel ?? Parent?.BreakLabel;
            set => _breakLabel = value;
        }
        public LabelSymbol? ContinueLabel
        {
            get => _continueLabel ?? Parent?.ContinueLabel;
            set => _continueLabel = value;
        }
        public bool? IsExecutionFlow
        {
            get => _isExecutionFlow ?? Parent?.IsExecutionFlow;
            set => _isExecutionFlow = value;
        }
        public Dictionary<Expression, LabelSymbol> SwitchLabels { get; set; } = new();

        public Scope(Scope parent, Symbol? declaringSymbol)
        {
            Parent = parent;
            SymbolTable = new SymbolTable();
            DeclaringSymbol = declaringSymbol;
        }

        public override Symbol? GetSymbol(string name, SymbolCategory category)
            => SymbolTable.GetSymbol(name, category) ?? Parent?.GetSymbol(name, category);

        public override void DeclareSymbol(Symbol symbol)
            => SymbolTable.DeclareSymbol(symbol);

        public override bool SymbolExists(string name, SymbolCategory category)
            => SymbolTable.SymbolExists(name, category) || (Parent?.SymbolExists(name, category) ?? false);

        public override IEnumerator<Symbol> GetEnumerator()
            => SymbolTable.Union(Parent ?? Enumerable.Empty<Symbol>()).GetEnumerator();

        public override Symbol? GetSymbol(Declaration declaration)
            => SymbolTable.GetSymbol(declaration) ?? Parent?.GetSymbol(declaration);
    }

    public class FunctionContext
    {
        public string Name { get; init; }
        public List<CompiledExpressionContext> AllExpressions { get; init; } = new();
        public Dictionary<KismetExpression, CompiledExpressionContext> ExpressionContextLookup { get; init; } = new();
        public List<CompiledExpressionContext> PrimaryExpressions { get; init; } = new();
        public int CodeOffset { get; set; } = 0;
        public LabelSymbol ReturnLabel { get; set; }
        public ProcedureSymbol Symbol { get; set; }
        public ProcedureDeclaration Declaration { get; set; }
        public CompiledFunctionContext CompiledFunctionContext { get; set; }
        public VariableSymbol? ReturnVariable { get; set; }
    }
}
