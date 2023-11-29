// Licensed to the AiCorp- Buyconn.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

//https://www.cazzulino.com/source-generators.html
namespace DelegactorCodeGen
{
    // <summary>
    // Represents a source generator that generates partial classes and interfaces
    // for all classes in the project that implement the IDelegactor interface.
    // </summary>

    [Generator]
    public class ActorClientSourceGenerator : ISourceGenerator
    {
        private const string InterfaceName = "IDelegactorProxy";


        // <summary>
        // Generates source code for partial classes and interfaces based on classes
        // that implement the IDelegactor interface.
        //
        // Parameters:
        // - context: The source generator context.
        // </summary>
        public void Execute(GeneratorExecutionContext context)
        {
            // Get the syntax receiver from the context.
            if (!(context.SyntaxReceiver is InterfaceSyntaxReceiver receiver))
            {
                return;
            }

            foreach (var interfaceDefenitions in receiver.Collection)
            {
                var hintName = $"{interfaceDefenitions.ClassName}ClientProxy.g.cs";
                
                if (context.Compilation.SyntaxTrees.ToList().Any(x => x.FilePath.EndsWith(hintName)))
                {
                    continue;
                }
                
                var result = ProxyGenCodeTemplate.Generate(interfaceDefenitions);

                context.AddSource(hintName, SourceText.From(result, Encoding.Default));
            }
        }

        // <summary>
        // Initializes the source generator.
        //
        // Parameters:
        // - initializer: The source generator initializer.
        // </summary>
        public void Initialize(GeneratorInitializationContext initializer)
        {
#if DEBUG
            // if (!Debugger.IsAttached)
            // {
            //     Debugger.Launch();
            // }
#endif

            // Register the syntax receiver.
            initializer.RegisterForSyntaxNotifications(() => new InterfaceSyntaxReceiver());
        }

        // <summary>
        // Represents a syntax receiver that collects candidate classes for source generation.
        // </summary>
        internal class InterfaceSyntaxReceiver : ISyntaxReceiver
        {
            public List<ProxyGenModel> Collection { get; } = new List<ProxyGenModel>();


            // <summary>
            // Called for every syntax node in the compilation.
            //
            // Parameters:
            // - syntaxNode: The syntax node.
            // </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is InterfaceDeclarationSyntax interfaceDeclarationSyntax &&
                    interfaceDeclarationSyntax.BaseList != null)
                {
                    var baseTypes = interfaceDeclarationSyntax.BaseList.Types;
                    foreach (var baseType in baseTypes)
                    {
                        if (baseType.Type is SimpleNameSyntax simpleBaseTypeSyntax&&
                            simpleBaseTypeSyntax.Identifier.ValueText.StartsWith(InterfaceName))
                        {

                            var namespaceDeclarationSyntax =
                                interfaceDeclarationSyntax.Parent as NamespaceDeclarationSyntax;

                            if (namespaceDeclarationSyntax != null)
                            {
                                var nameSpaceName = namespaceDeclarationSyntax.Name.GetText().ToString();
                                var interfaceName = interfaceDeclarationSyntax.Identifier.Text;
                                var candidate = new ProxyGenModel
                                {
                                    NameSpaceName = nameSpaceName,
                                    ClassName = $"{interfaceName.TrimStart('I')}",
                                    InterfaceName = interfaceName,
                                    MethodsList = new List<Method>(),
                                    ModuleName = $"{nameSpaceName.Trim()}.{interfaceName.Trim()}"
                                };
                                foreach (var childNode in syntaxNode.ChildNodes())
                                {
                                    if (childNode is MethodDeclarationSyntax methodDeclarationSyntax)
                                    {
                                        var parameterListParameters =
                                            methodDeclarationSyntax.ParameterList.Parameters.ToList();

                                        var isFromReplicaFlagTrue = GetIsFromReplicaFlag(methodDeclarationSyntax);

                                        var method = new Method
                                        {
                                            ParameterDeclarations =
                                                methodDeclarationSyntax.ParameterList.ToFullString(),
                                            ParametersCollection = parameterListParameters
                                                .Select(x => x.Identifier.Text).ToList(),
                                            ReturnType = methodDeclarationSyntax.ReturnType.GetText().ToString().Replace("Task",""),
                                            IsFromReplica = isFromReplicaFlagTrue ? "replica" : "partition",
                                            MethodName = methodDeclarationSyntax.Identifier.Text
                                        };

                                        candidate.MethodsList.Add(method);
                                    }
                                }

                                Collection.Add(candidate);
                            }
                        }
                    }
                }
            }

            private static bool GetIsFromReplicaFlag(MethodDeclarationSyntax methodDeclarationSyntax)
            {
                var isFromReplicaFlagTrue = false;
                foreach (var x in methodDeclarationSyntax.AttributeLists)
                {
                    foreach (var y in x.Attributes)
                    {
                        if (y.GetText().ToString().Contains("fromReplica") &&
                            y.GetText().ToString().Contains("true"))
                        {
                            isFromReplicaFlagTrue = true;
                            break;
                        }
                    }

                    if (isFromReplicaFlagTrue)
                    {
                        break;
                    }
                }

                return isFromReplicaFlagTrue;
            }
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
        }
    }
}
