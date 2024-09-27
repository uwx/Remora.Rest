using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Core;
using Remora.Rest.Extensions;
using Remora.Rest.Json.Reflection;

namespace Remora.Rest.Json;

public class DataObjectConverterConfiguration<TInterface, TImplementation> : IDataObjectConverterConfiguration<TInterface, TImplementation> where TImplementation : TInterface
{
    internal readonly Dictionary<PropertyInfo, string[]> ReadNameOverrides = new();
    internal readonly Dictionary<PropertyInfo, string> WriteNameOverrides = new();
    internal readonly HashSet<PropertyInfo> IncludeReadOnlyOverrides = new();
    internal readonly HashSet<PropertyInfo> ExcludeOverrides = new();

    internal readonly Dictionary<PropertyInfo, JsonConverter> ConverterOverrides = new();
    internal readonly Dictionary<PropertyInfo, JsonConverterFactory> ConverterFactoryOverrides = new();

    /// <summary>
    /// Holds a value indicating whether extra undefined properties should be allowed.
    /// </summary>
    internal bool DoesAllowExtraProperties = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataObjectConverter{TInterface, TImplementation}"/> class.
    /// </summary>
    public DataObjectConverterConfiguration()
    {
    }

    /// <summary>
    /// Sets whether extra JSON properties without a matching DTO property are allowed. Such properties are, if
    /// allowed, ignored. Otherwise, they throw a <see cref="JsonException"/>.
    ///
    /// By default, this is true.
    /// </summary>
    /// <param name="allowExtraProperties">Whether to allow extra properties.</param>
    /// <returns>The converter, with the new setting.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> AllowExtraProperties(bool allowExtraProperties = true)
    {
        DoesAllowExtraProperties = allowExtraProperties;
        return this;
    }

    /// <summary>
    /// Explicitly marks a property as included in the set of serialized properties. This is useful when readonly
    /// properties need to be serialized for some reason.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the inclusion.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> IncludeWhenSerializing<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression
    )
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        if (!DtoProperties.Contains(property))
        {
            throw new InvalidOperationException();
        }

        if (IncludeReadOnlyOverrides.Contains(property))
        {
            return this;
        }

        IncludeReadOnlyOverrides.Add(property);
        return this;
    }

    /// <summary>
    /// Explicitly marks a property as excluded in the set of serialized properties. This is useful when read-write
    /// properties need to be kept off the wire for some reason.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the inclusion.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> ExcludeWhenSerializing<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression
    )
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        if (!DtoProperties.Contains(property))
        {
            throw new InvalidOperationException();
        }

        if (ExcludeOverrides.Contains(property))
        {
            return this;
        }

        ExcludeOverrides.Add(property);
        return this;
    }

    /// <summary>
    /// Overrides the name of the given property when serializing and deserializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name
    )
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        if (!DtoProperties.Contains(property))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        property = DtoProperties.First(p => p.Name == property.Name);

        WriteNameOverrides.Add(property, name );
        ReadNameOverrides.Add(property, new[] { name });
        return this;
    }

    /// <summary>
    /// Overrides the name of the given property when serializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithWritePropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name
    )
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        if (!DtoProperties.Contains(property))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        property = DtoProperties.First(p => p.Name == property.Name);

        WriteNameOverrides.Add(property, name );
        return this;
    }

    /// <summary>
    /// Overrides the name of the given property when deserializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <param name="fallbacks">The fallback names to use if the primary name isn't present.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithReadPropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name,
        params string[] fallbacks
    )
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        if (!DtoProperties.Contains(property))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        property = DtoProperties.First(p => p.Name == property.Name);

        var overrides =
            fallbacks.Length == 0
                ? new[] { name }
                : new[] { name }.Concat(fallbacks).ToArray();

        ReadNameOverrides.Add(property, overrides);

        return this;
    }

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        JsonConverter<TProperty> converter
    ) => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, Optional<TProperty>>> propertyExpression,
        JsonConverter<TProperty> converter
    ) => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty?>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, Optional<TProperty?>>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable>
    (
        Expression<Func<TImplementation, TEnumerable>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>
        => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable>
    (
        Expression<Func<TImplementation, Optional<TEnumerable>>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>
        => AddPropertyConverter(propertyExpression, converter);

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converterFactory">The JSON converter factory.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    public IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        JsonConverterFactory converterFactory
    ) => AddPropertyConverter(propertyExpression, converterFactory);

}