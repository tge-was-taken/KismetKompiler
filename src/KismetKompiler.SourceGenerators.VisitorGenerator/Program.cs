using System.IO;
using System.Text;
using KismetKompiler.Library.Decompiler.Context.Nodes;
using KismetKompiler.Library.Syntax;

var source = new StringBuilder();
var rootType = typeof(SyntaxNode);
var types = rootType.Assembly.GetTypes()
        .Where(x => x.IsAssignableTo(rootType))
        .ToList();
var namespaces = types.Select(x => x.Namespace).Distinct();
foreach (var ns in namespaces)
    source.AppendLine($"using {ns};");

source.AppendLine("namespace KismetKompiler.Library.Syntax;");
source.AppendLine("public interface ISyntaxNodeVisitor {");
foreach (var type in types)
{
    if (type.IsGenericType)
    {
        source.AppendLine($"    void Visit<T>({type.Name.Replace("`1", "<T>")} node);");
    }
    else
    {
        source.AppendLine($"    void Visit({type.Name} node);");
    }
}
source.AppendLine("}");

source.AppendLine("public abstract class SyntaxNodeVisitorBase : ISyntaxNodeVisitor {");
foreach (var type in types)
{
    if (type.IsGenericType)
    {
        source.AppendLine($"    public virtual void Visit<T>({type.Name.Replace("`1", "<T>")} node) {{");
    }
    else
    {
        source.AppendLine($"    public virtual void Visit({type.Name} node) {{");
    }

    var isTerminal = !rootType.Assembly.GetTypes()
        .Where(x => x != type && x.IsAssignableTo(type))
        .Any();

    if (type.IsAbstract)
    {
        source.AppendLine($"        Visit((dynamic)node);");
    }
    else
    {
        foreach (var prop in type.GetProperties().Where(x => !x.GetGetMethod().IsStatic))
        {
            if (prop.PropertyType.IsAssignableTo(rootType))
            {
                source.AppendLine($"        if (node.{prop.Name} != null) Visit(node.{prop.Name});");
            }
            else if (prop.PropertyType.IsGenericType &&
            prop.PropertyType.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var genericEnumerableInterface = prop.PropertyType.GetInterfaces().FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (genericEnumerableInterface != null &&
                    genericEnumerableInterface.GetGenericArguments()[0].IsAssignableTo(rootType))
                {
                    var propType = genericEnumerableInterface.GetGenericArguments()[0];

                    source.AppendLine($"        if (node.{prop.Name} != null) {{");
                    source.AppendLine($"            foreach (var item in node.{prop.Name})");
                    source.AppendLine($"            {{");

                    source.AppendLine($"                if (item != null) Visit(item);");

                    source.AppendLine($"            }}");
                    source.AppendLine($"        }}");
                }
            }
        }
    }
    source.AppendLine("    }");
}
source.AppendLine("}");

File.WriteAllText(@"..\..\..\..\KismetKompiler.Library\Syntax\SyntaxNodeVisitor.generated.cs", source.ToString());

static string GetTypeVarName(Type type)
{
    var typeName = type.Name.Replace("`", "_");
    return typeName.ToString().ToLowerInvariant() + typeName.Substring(1);
}

static string GetTypeName(Type type)
{
    return type.Name.Replace("`1", "<T>");
}

static void WriteMember(Type rootType, StringBuilder source, Type propType, string propName)
{
    var counter = 0;
    foreach (var derivedType in rootType.Assembly.GetTypes()
        .Where(x => x.IsAssignableTo(propType)))
    {
        var varName = GetTypeVarName(derivedType);
        if (counter == 0)
        {
            source.AppendLine($"        if ({propName} as {GetTypeName(derivedType)} {varName}) Visit({varName});");
        }
        else
        {
            source.AppendLine($"        else if ({propName} as {GetTypeName(derivedType)} {varName}) Visit({varName});");
        }
        counter++;
    }
    source.AppendLine($"        else throw new NotImplementedException();");
}