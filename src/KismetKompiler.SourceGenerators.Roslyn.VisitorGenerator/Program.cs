using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace KismetKompiler.SourceGenerators.VisitorGenerator
{
    [Generator]
    public class SyntaxNodeVisitorGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            // Retrieve the syntax trees of all compilation units
            var syntaxTrees = context.Compilation.SyntaxTrees;

            // Initialize the string builders for generated files
            var visitorInterfaceBuilder = new StringBuilder();
            var visitorImplementationBuilder = new StringBuilder();

            // Add necessary using statements
            var usingStatements = new HashSet<string>();

            // Visit all syntax trees
            foreach (var syntaxTree in syntaxTrees)
            {
                var root = syntaxTree.GetRoot();

                // Find all classes derived from SyntaxNode
                var syntaxNodeTypes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                                           .Where(c => c.BaseList?.Types.Any(t => t.Type is IdentifierNameSyntax ins && ins.Identifier.Text == "SyntaxNode") ?? false);

                foreach (var syntaxNodeType in syntaxNodeTypes)
                {
                    var className = syntaxNodeType.Identifier.Text;
                    var properties = syntaxNodeType.DescendantNodes().OfType<PropertyDeclarationSyntax>();

                    // Generate visitor interface method for this class
                    visitorInterfaceBuilder.AppendLine($"void Visit{className}({className} node);");

                    // Generate visitor implementation method for this class
                    visitorImplementationBuilder.AppendLine($"public virtual void Visit{className}({className} node)");
                    visitorImplementationBuilder.AppendLine("{");

                    // Visit properties
                    foreach (var property in properties)
                    {
                        var propertyType = property.Type.ToString();

                        // Add necessary using statements
                        var propertyTypeNamespace = propertyType.Substring(0, propertyType.LastIndexOf('.'));
                        usingStatements.Add(propertyTypeNamespace);

                        visitorImplementationBuilder.AppendLine($"    Visit(node.{property.Identifier});");
                    }

                    visitorImplementationBuilder.AppendLine("}");
                }
            }

            // Generate visitor interface file
            var interfaceSource = @$"
using System;
{string.Join("\n", usingStatements.Select(u => $"using {u};"))}

namespace Generated
{{
    public interface ISyntaxNodeVisitor
    {{
        {visitorInterfaceBuilder}
    }}
}}";

            // Generate visitor implementation file
            var implementationSource = @$"
using System;
{string.Join("\n", usingStatements.Select(u => $"using {u};"))}

namespace Generated
{{
    public abstract class SyntaxNodeVisitorBase : ISyntaxNodeVisitor
    {{
        {visitorImplementationBuilder}
    }}
}}";

            // Add the generated files to the compilation
            context.AddSource("ISyntaxNodeVisitor.generated.cs", SourceText.From(interfaceSource, Encoding.UTF8));
            context.AddSource("SyntaxNodeVisitorBase.generated.cs", SourceText.From(implementationSource, Encoding.UTF8));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}