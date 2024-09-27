//
//  SPDX-FileName: AbstractDataObjectConverter.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Core;
using Remora.Rest.Json.Reflection;

namespace Remora.Rest.Json;

internal record CompileTimePropertyInfo(
    string Name,
    Type? DeclaringType
)
{
    public static CompileTimePropertyInfo GetForReflectedProperty(PropertyInfo property)
        => new(property.Name, property.DeclaringType);
}

internal record CompileTimePropertyInfo2(
    string Name,
    Type? DeclaringType,
    Action<object, object> SetValue,
    Func<object, object> GetValue,
    DTOPropertyWriter Writer,
    DTOPropertyReader Reader
) : CompileTimePropertyInfo(Name, DeclaringType), IEquatable<CompileTimePropertyInfo>;

public abstract class AbstractDataObjectConverter<TInterface, TImplementation> :
    JsonConverterFactory,
    IDataObjectConverterConfiguration<TInterface, TImplementation>
    where TImplementation : TInterface
{
    private protected abstract ObjectFactory<TImplementation> Factory { get; }

    private protected abstract IReadOnlyList<CompileTimePropertyInfo2> DtoProperties { get; }

    private protected abstract IReadOnlyDictionary<Type, object?> DtoEmptyOptionals { get; }

    private readonly Dictionary<CompileTimePropertyInfo, string[]> _readNameOverrides = new();
    private readonly Dictionary<CompileTimePropertyInfo, string> _writeNameOverrides = new();
    private readonly HashSet<CompileTimePropertyInfo> _includeReadOnlyOverrides = new();
    private readonly HashSet<CompileTimePropertyInfo> _excludeOverrides = new();

    private readonly Dictionary<CompileTimePropertyInfo, JsonConverter> _converterOverrides = new();
    private readonly Dictionary<CompileTimePropertyInfo, JsonConverterFactory> _converterFactoryOverrides = new();

    /// <summary>
    /// Holds a value indicating whether extra undefined properties should be allowed.
    /// </summary>
    private bool _allowExtraProperties = true;

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
        _allowExtraProperties = allowExtraProperties;
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

        var compProp = CompileTimePropertyInfo.GetForReflectedProperty(property);
        if (!DtoProperties.Contains(compProp))
        {
            throw new InvalidOperationException();
        }

        _includeReadOnlyOverrides.Add(compProp);
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

        var compProp = CompileTimePropertyInfo.GetForReflectedProperty(property);
        if (!DtoProperties.Contains(compProp))
        {
            throw new InvalidOperationException();
        }

        _excludeOverrides.Add(compProp);
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

        var compProp = CompileTimePropertyInfo.GetForReflectedProperty(property);
        if (!DtoProperties.Contains(compProp))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        compProp = DtoProperties.First(p => p.Name == compProp.Name);

        _writeNameOverrides.Add(compProp, name);
        _readNameOverrides.Add(compProp, [name]);
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

        var compProp = CompileTimePropertyInfo.GetForReflectedProperty(property);
        if (!DtoProperties.Contains(compProp))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        compProp = DtoProperties.First(p => p.Name == compProp.Name);

        _writeNameOverrides.Add(compProp, name );
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

        var compProp = CompileTimePropertyInfo.GetForReflectedProperty(property);
        if (!DtoProperties.Contains(compProp))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        compProp = DtoProperties.First(p => p.Name == compProp.Name);

        string[] overrides =
            fallbacks.Length == 0
                ? [name]
                : [name, ..fallbacks];

        _readNameOverrides.Add(property, overrides);

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

    private AbstractDataObjectConverter<TInterface, TImplementation> AddPropertyConverter<TExpression>
    (
        Expression<TExpression> expression,
        JsonConverter converter
    )
    {
        if (expression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        var compProp = DtoProperties.First(p => p.Name == property.Name);

        _converterOverrides.Add(compProp, converter);
        return this;
    }

    private AbstractDataObjectConverter<TInterface, TImplementation> AddPropertyConverter<TExpression>
    (
        Expression<TExpression> expression,
        JsonConverterFactory converterFactory
    )
    {
        if (expression.Body is not MemberExpression memberExpression)
        {
            throw new InvalidOperationException();
        }

        var member = memberExpression.Member;
        if (member is not PropertyInfo property)
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        var compProp = DtoProperties.First(p => p.Name == property.Name);

        _converterFactoryOverrides.Add(compProp, converterFactory);
        return this;
    }
}
