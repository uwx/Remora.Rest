using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Remora.Rest.SourceGenerator;

[Generator]
public class DataObjectConverterGenerator : IIncrementalGenerator
{
    private readonly record struct Data(
        IMethodSymbol AddDataObjectConverterSymbol,
        string FilePath,
        int Line,
        int Column,
        INamedTypeSymbol Interface,
        INamedTypeSymbol Implementation);
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.CreateSyntaxProvider<Data?>(
            predicate: (node, token) => node is InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { Identifier.Text: "AddDataObjectConverter" }
                }
            },
            transform: (context, token) =>
            {
                // we know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
                var invocation = (InvocationExpressionSyntax)context.Node;
                var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                var file = invocation.SyntaxTree.FilePath;
                var startLinePosition = memberAccess.Name.SyntaxTree.GetLineSpan(memberAccess.Name.Span).StartLinePosition;
                
                if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
                {
                    return null;
                }

                if (methodSymbol.ContainingType.ToDisplayString() != "Remora.Rest.Extensions.JsonSerializerOptionsExtensions")
                {
                    return null;
                }

                var genericArguments = ((GenericNameSyntax)memberAccess.Name).TypeArgumentList.Arguments
                    .Select(e => (INamedTypeSymbol) context.SemanticModel.GetSymbolInfo(e).Symbol)
                    .ToArray();
                
                return new Data(methodSymbol, file, startLinePosition.Line, startLinePosition.Character, genericArguments[0], genericArguments[1]);
            } 
        ).Where(static m => m is not null);
        
        
        context.RegisterSourceOutput(provider,
            static (spc, source) =>
            {
                Debug.Assert(source != null, nameof(source) + " != null");
                var data = source.Value!;

                var visibleProperties = data.Implementation.GetPublicProperties().ToArray();
                
                var dtoConstructor = FindBestMatchingConstructor(data.Implementation, visibleProperties);

                IReadOnlyList<IPropertySymbol> dtoProperties;

                if (dtoConstructor != null)
                {
                    dtoProperties = ReorderProperties(visibleProperties, dtoConstructor);
                }
                else
                {
                    dtoProperties = visibleProperties;
                }

                var sb = new StringBuilder();

                var interfaceNameString = data.Interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var implementationNameString = data.Implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine(
                    $$"""
                    using System.Collections.Generic;
                    using System.Text.Json;
                    using System.Text.Json.Serialization;
                    using Remora.Rest.Json.Internal;
                    using Remora.Rest.Json.Reflection;
                    
                    namespace Remora.Rest.Json;
                    
                    /// <inheritdoc/>
                    file class ActualConverter : AbstractDataObjectConverter<{{interfaceNameString}}, {{implementationNameString}}>
                    {
                    """);

                if (dtoConstructor != null)
                {
                    sb.Append(
                        $$"""
                        private static ObjectFactory<{{implementationNameString}}> CachedFactory { get; } = args => new {{implementationNameString}}(
                        """);

                    for (var i = 0; i < dtoProperties.Count; i++)
                    {
                        var property = dtoProperties[i];
                        var parameter = dtoConstructor?.Parameters[i];

                        sb.Append(
                            $"({property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})args[{i}]!"
                        );

                        if (i != dtoProperties.Count - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                    
                    sb.AppendLine(");");
                }
                else
                {
                    sb.AppendLine(
                        $$"""
                        private static ObjectFactory<{{implementationNameString}}> CachedFactory { get; } = args => {
                            var value = new {{implementationNameString}}();
                        """);

                    for (var i = 0; i < dtoProperties.Count; i++)
                    {
                        var property = dtoProperties[i];
                        var parameter = dtoConstructor?.Parameters[i];

                        sb.AppendLine(
                            $$"""
                                value.{{property.Name}} = ({{property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}})args[{{i}}]!;
                            """);
                    }

                    sb.AppendLine(
                        $$"""
                            return value;
                        };
                        """);
                }
                
                sb.AppendLine(
                    $$"""
                        private static IReadOnlyList<CompileTimePropertyInfo> CachedDtoProperties { get; } =
                        [
                    """);

                for (var i = 0; i < dtoProperties.Count; i++)
                {
                    var property = dtoProperties[i];
                    var parameter = dtoConstructor?.Parameters[i];

                    var propertyDisplayType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    sb.AppendLine(
                        $$"""
                              new(
                                  Name: "{{property.Name}}",
                                  PropertyType: typeof({{propertyDisplayType}}),
                                  UnwrappedPropertyType: typeof({{property.Type.Unwrap().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}}),
                                  DeclaringType: typeof({{property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}}),
                                  GetValue: static instance => (({{property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}})instance).{{property.Name}},
                                  Writer: static (writer, dtoProperty, value, options) =>
                                  {
                          """);

                    if (property.Type.IsOptional(out var innerType))
                    {
                        var displayType = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        sb.AppendLine(
                            $$"""
                                    var optionalValue = (({{propertyDisplayType}})property.Type;
                                    
                                    if (optionalValue.TryGet(out var innerValue))
                                    {
                                        writer.WritePropertyName(dtoProperty.WriteName);
                                        ((JsonConverter<{{displayType}}>)(dtoProperty.Converter ?? options.GetConverter(typeof({{displayType}})))).Write(writer, innerValue, options);
                                    }
                            """);
                    }
                    else
                    {
                        sb.AppendLine(
                            $$"""
                                    writer.WritePropertyName(dtoProperty.WriteName);
                                    ((JsonConverter<{{propertyDisplayType}}>)(dtoProperty.Converter ?? options.GetConverter(typeof({{propertyDisplayType}})))).Write(writer, ({{propertyDisplayType}})value, options);
                            """);
                    }

                    sb.AppendLine(
                        $$"""
                                },
                                Reader: static (ref Utf8JsonReader reader, DTOPropertyInfo dtoProperty, JsonSerializerOptions options) =>
                                {
                                    return ((JsonConverter<{{propertyDisplayType}}>)(dtoProperty.Converter ?? options.GetConverter(typeof({{propertyDisplayType}})))).Read(ref reader, typeof({{propertyDisplayType}}), options);
                                },
                                AllowsNull: {{property.AllowsNull()}},
                                CanWrite: {{property.SetMethod != null}},
                                DefaultValue: {{(parameter != null ? GetDefaultValueForParameter(property, parameter) : "default")}}
                            )
                        """);
                }

                sb.AppendLine(
                    $$"""
                        ];
                    
                        /// <inheritdoc/>
                        private protected override ObjectFactory<Identify> Factory => CachedFactory;
                    
                        /// <inheritdoc/>
                        private protected override IReadOnlyList<CompileTimePropertyInfo> DtoProperties => CachedDtoProperties;
                    }
                    
                    namespace System.Runtime.CompilerServices
                    {
                        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                        file sealed class InterceptsLocationAttribute(string filePath, int line, int character) : Attribute
                        {
                        }
                    }
                    """);
                
                sb.AppendLine(
                    $$"""
                    file static class Interception
                    {
                    """);
                
                sb.AppendLine(
                    $$"""
                        [InterceptsLocation(filePath: "{{data.FilePath}}", line: {{data.Line}}, character: {{data.Column}})]
                        public static IDataObjectConverterConfiguration<TInterface, TActual> AddDataObjectConverter<TInterface, TActual>
                        (
                            JsonSerializerOptions options
                        ) where TActual : TInterface
                        {
                            var converter = new ActualConverter();
                            options.Converters.Insert(0, converter);
                            
                            // Guaranteed to succeed!
                            return (IDataObjectConverterConfiguration<TInterface, TActual>)(object)converter;
                        }
                    """);
                
                sb.AppendLine(
                    $$"""
                    }
                    
                    """
                );
                spc.AddSource($"Remora.Rest.SourceGenerator.DataObjectConverterGenerator/{data.FilePath.GetHashCode()}.{data.Line}.{data.Column}.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            });
    }

    private static string GetDefaultValueForParameter(IPropertySymbol property, IParameterSymbol parameter)
    {
        // If there is an explicit default parameter, we use that.
        if (parameter.HasExplicitDefaultValue)
        {
            var defaultValue = parameter.ExplicitDefaultValue;

            if (parameter.Type.IsValueType && defaultValue is null)
            {
                // "default" default parameters for value-types are null here. Instantiate the appropriate value.
                // We try to grab an empty optional first since there is a good chance we're dealing with an Optional<T>.
                return $"default({parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
            }

            return defaultValue?.ToString() ?? "null";
        }

        // Polyfill default parameters for Optional<T> properties.
        if (parameter.Type.IsOptional(out _))
        {
            return $"default({parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
        }

        // Otherwise, we have no default value.
        return "default";
    }

    private static IReadOnlyList<IPropertySymbol> ReorderProperties
    (
        IPropertySymbol[] visibleProperties,
        IMethodSymbol constructor
    )
    {
        var reorderedProperties = new List<IPropertySymbol>(visibleProperties.Length);

        var constructorParameters = constructor.Parameters;
        foreach (var constructorParameter in constructorParameters)
        {
            var matchingProperty = visibleProperties.FirstOrDefault
            (
                p =>
                    p.Name.Equals(constructorParameter.Name, StringComparison.InvariantCultureIgnoreCase) &&
                    SymbolEqualityComparer.Default.Equals(p.Type, constructorParameter.Type)
            );

            if (matchingProperty is null)
            {
                throw new MissingMemberException(constructor.ContainingType.Name, constructorParameter.Name);
            }

            reorderedProperties.Add(matchingProperty);
        }

        // Add leftover properties at the end
        reorderedProperties.AddRange(visibleProperties.Except(reorderedProperties));

        return reorderedProperties;
    }
    
    private static IMethodSymbol? FindBestMatchingConstructor(INamedTypeSymbol implementationType, IPropertySymbol[] visibleProperties)
    {
        var visiblePropertyTypes = visibleProperties
            .Where(p => !p.IsReadOnly)
            .Select(p => p.Type)
            .ToArray();

        var implementationConstructors = implementationType.InstanceConstructors;
        if (implementationConstructors.Length == 1)
        {
            var singleCandidate = implementationConstructors[0];
            return IsMatchingConstructor(singleCandidate, visiblePropertyTypes)
                ? singleCandidate
                : null;
        }

        var matchingConstructors = implementationType.InstanceConstructors
            .Where(c => IsMatchingConstructor(c, visiblePropertyTypes)).ToList();

        if (matchingConstructors.Count == 1)
        {
            return matchingConstructors[0];
        }

        return null;
    }
    
    private static bool IsMatchingConstructor
    (
        IMethodSymbol constructor,
        IReadOnlyCollection<ITypeSymbol> visiblePropertyTypes
    )
    {
        if (constructor.Parameters.Length != visiblePropertyTypes.Count)
        {
            return false;
        }

        var parameterTypeCounts = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var parameterType in constructor.Parameters.Select(p => p.Type))
        {
            if (!parameterTypeCounts.TryAdd(parameterType, 1))
            {
                parameterTypeCounts[parameterType] += 1;
            }
        }

        var propertyTypeCounts = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var propertyType in visiblePropertyTypes)
        {
            if (!propertyTypeCounts.TryAdd(propertyType, 1))
            {
                propertyTypeCounts[propertyType] += 1;
            }
        }

        if (parameterTypeCounts.Count != propertyTypeCounts.Count)
        {
            return false;
        }

        foreach (var (propertyType, propertyTypeCount) in propertyTypeCounts)
        {
            if (!parameterTypeCounts.TryGetValue(propertyType, out var parameterTypeCount))
            {
                return false;
            }

            if (propertyTypeCount != parameterTypeCount)
            {
                return false;
            }
        }

        // This constructor matches
        return true;
    }
}

file static class Extensions
{
    /// <summary>
    /// Gets all publicly visible properties of the given type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The public properties.</returns>
    public static IEnumerable<IPropertySymbol> GetPublicProperties(this INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
        {
            foreach (var e in type.GetBaseTypesAndThis())
            foreach (var member in e.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property)
                {
                    yield return property;
                }
            }

            yield break;
        }

        foreach (var implementedInterface in type.AllInterfaces.Append(type))
        {
            foreach (var member in implementedInterface.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } property)
                {
                    yield return property;
                }
            }
        }
    }

    // https://github.com/dotnet/roslyn-analyzers/blob/main/src/Utilities/Compiler/Extensions/INamedTypeSymbolExtensions.cs
    public static IEnumerable<INamedTypeSymbol> GetBaseTypesAndThis(this INamedTypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
    {
        if (dict.ContainsKey(key)) return false;
        dict[key] = value;
        return true;
    }

    private static readonly SymbolDisplayFormat OptionalDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static bool IsOptional(this ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? innerType)
    {
        if (type is INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } n)
        {
            if (n.ToDisplayString(OptionalDisplayFormat) == "Remora.Rest.Core.Optional")
            {
                innerType = n.TypeArguments[0];
                return true;
            }
        }

        innerType = null;
        return false;
    }

    public static bool AllowsNull(this IPropertySymbol property)
    {
        if (property.Type is INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } t && t.ToDisplayString(OptionalDisplayFormat) == "System.Nullable")
        {
            return true;
        }

        if (property.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return true;
        }

        if (property.GetAttributes().Any(e => e.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.NullableAttribute"))
        {
            return true;
        }

        return false;
    }

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
    {
        key = kvp.Key;
        value = kvp.Value;
    }
    
    public static bool IsNullable(this ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } t &&
               t.ToDisplayString(OptionalDisplayFormat) == "System.Nullable";
    }
    
    public static ITypeSymbol Unwrap(this ITypeSymbol type)
    {
        var currentType = type;
        while (currentType is INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } t)
        {
            if (currentType.IsOptional(out var innerType))
            {
                currentType = innerType;
                continue;
            }

            if (currentType.IsNullable())
            {
                currentType = t.TypeArguments[0];
                continue;
            }

            break;
        }

        return currentType;
    }
}