using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Remora.Rest.Extensions;
using Remora.Rest.Json;
using Remora.Rest.SourceGenerator;

var syntaxTree = CSharpSyntaxTree.ParseText("""
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Json;
using Remora.Rest;

namespace Testing
{
    /// <summary>
    /// Represents the public interface of an existing data model.
    /// </summary>
    public interface IExisting
    {
        /// <summary>
        /// Gets some existing value.
        /// </summary>
        string ExistingValue { get; }
    }

    /// <summary>
    /// Represents the implementation of an existing data model.
    /// </summary>
    /// <param name="ExistingValue">Some existing value.</param>
    public record Existing(string ExistingValue) : IExisting;

    /// <summary>
    /// Represents the customized implementation of an existing data model.
    /// </summary>
    /// <param name="ExistingValue">Some existing value.</param>
    /// <param name="AdditionalValue">Some additional value.</param>
    public record Customized(string ExistingValue, string AdditionalValue) : Existing(ExistingValue);

    public static class Program
    {
        public static void Test()
        {
            var serviceCollection = new ServiceCollection()
                .Configure<JsonSerializerOptions>
                (
                    options =>
                    {
                        // Add the existing type
                        options.AddDataObjectConverter<IExisting, Existing>();
                    }
                );

            serviceCollection.Configure<JsonSerializerOptions>
            (
                options =>
                {
                    // Override the existing type
                    options.AddDataObjectConverter<IExisting, Customized>();
                }
            );

            var services = serviceCollection.BuildServiceProvider();

        }
    }
}
""");

var references = AppDomain.CurrentDomain.GetAssemblies()
    .Where(assembly => !assembly.IsDynamic)
    .Append(typeof(JsonSerializerOptionsExtensions).Assembly)
    .Append(typeof(IDataObjectConverterConfiguration<,>).Assembly)
    .Distinct()
    .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
    .Cast<MetadataReference>();

var compilation = CSharpCompilation.Create("SourceGeneratorTests",
    [syntaxTree],
    references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

// Source Generator to test
var generator = new DataObjectConverterGenerator();

CSharpGeneratorDriver.Create(generator)
    .RunGeneratorsAndUpdateCompilation(compilation,
        out var outputCompilation,
        out var diagnostics);

// optional
diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
    .Should().BeEmpty();

foreach (var tree in outputCompilation.SyntaxTrees)
{
    if (tree.FilePath.Contains("Remora.Rest.SourceGenerator.DataObjectConverterGenerator"))
    {
        Console.WriteLine(tree.ToString());
    }
}