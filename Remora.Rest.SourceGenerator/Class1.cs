using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Remora.Rest.SourceGenerator;

[Generator]
public class Class1 : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, token) => node is InvocationExpressionSyntax && 
        )
    }
}