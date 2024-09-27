//
//  SPDX-FileName: AbstractDataObjectConverter.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Core;
using Remora.Rest.Json.Internal;
using Remora.Rest.Json.Reflection;

namespace Remora.Rest.Json;

internal readonly record struct CompileTimePropertyKey(
    string Name,
    Type? DeclaringType
)
{
    public static CompileTimePropertyKey GetForReflectedProperty(PropertyInfo property)
        => new(property.Name, property.DeclaringType);
}

// UnwrappedPropertyType: if nullable<T> or optional<T>, get T. see special case for enum.
internal readonly record struct CompileTimePropertyInfo(
    string Name,
    Type PropertyType,
    Type UnwrappedPropertyType,
    Type? DeclaringType,
    Func<object, object?> GetValue,
    DTOPropertyWriter Writer,
    DTOPropertyReader Reader,
    bool AllowsNull, // see PropertyInfoExtensions.AllowsNull
    bool CanWrite,
    Optional<object?> DefaultValue = default // for construcotr invocation. see GetDefaultValueForParameter
)
{
    public static implicit operator CompileTimePropertyKey(CompileTimePropertyInfo property) => new(property.Name, property.PropertyType);
}

public abstract class AbstractDataObjectConverter<TInterface, TImplementation> :
    JsonConverterFactory,
    IDataObjectConverterConfiguration<TInterface, TImplementation>
    where TImplementation : TInterface
{
    private protected abstract ObjectFactory<TImplementation> Factory { get; }

    // this can be cached, but there is not much of an impulse for it
    private ObjectFactory<TInterface> InterfaceFactory => args => Factory(args);

    // MUST be sorted in constructor invocation order!
    private protected abstract IReadOnlyList<CompileTimePropertyInfo> DtoProperties { get; }

    private readonly Dictionary<CompileTimePropertyKey, string[]> _readNameOverrides = new();
    private readonly Dictionary<CompileTimePropertyKey, string> _writeNameOverrides = new();
    private readonly HashSet<CompileTimePropertyKey> _includeReadOnlyOverrides = new();
    private readonly HashSet<CompileTimePropertyKey> _excludeOverrides = new();

    private readonly Dictionary<CompileTimePropertyKey, JsonConverter> _converterOverrides = new();
    private readonly Dictionary<CompileTimePropertyKey, JsonConverterFactory> _converterFactoryOverrides = new();

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

        var compProp = CompileTimePropertyKey.GetForReflectedProperty(property);
        if (DtoProperties.All(e => e != compProp))
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

        var compProp = CompileTimePropertyKey.GetForReflectedProperty(property);
        if (DtoProperties.All(e => e != compProp))
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

        var compProp = CompileTimePropertyKey.GetForReflectedProperty(property);
        if (DtoProperties.All(e => e != compProp))
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

        var compProp = CompileTimePropertyKey.GetForReflectedProperty(property);
        if (DtoProperties.All(e => e != compProp))
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

        var compProp = CompileTimePropertyKey.GetForReflectedProperty(property);
        if (DtoProperties.All(e => e != compProp))
        {
            throw new InvalidOperationException();
        }

        // Resolve the matching interface property
        compProp = DtoProperties.First(p => p.Name == compProp.Name);

        string[] overrides =
            fallbacks.Length == 0
                ? [name]
                : [name, ..fallbacks];

        _readNameOverrides.Add(compProp, overrides);

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

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(TImplementation) || typeToConvert == typeof(TInterface);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var writeProperties = new List<DTOPropertyInfo>();
        var readProperties = new List<DTOPropertyInfo>();

        var properties = DtoProperties;
        for (int i = 0; i < properties.Count; i++)
        {
            var property = properties[i];

            var converter = GetConverter(property, options);
            var readNames = GetReadJsonPropertyName(property, options);
            var writeNames = GetWriteJsonPropertyName(property, options);
            var reader = property.Reader;
            var writer = property.Writer;

            // We cache this as well since the check is somewhat complex
            var allowsNull = property.AllowsNull;

            var data = new DTOPropertyInfo
            (
                property.Name,
                readNames,
                writeNames,
                reader,
                writer,
                allowsNull,
                property.DefaultValue,
                converter,
                readProperties.Count
            );

            if (property.CanWrite)
            {
                // If a property is writable, it can be *read* from JSON.
                readProperties.Add(data);
            }

            if ((property.CanWrite || ShouldIncludeReadOnlyProperty(property)) && !_excludeOverrides.Contains(property))
            {
                // Any property that is writable and not excluded due to being read-only,
                // can be *written* to JSON.
                writeProperties.Add(data);
            }
        }

        if (typeToConvert == typeof(TInterface))
        {
            return new BoundDataObjectConverter<TInterface>
            (
                InterfaceFactory,
                _allowExtraProperties,
                writeProperties.ToArray(),
                readProperties.ToArray()
            );
        }

        // ReSharper disable once InvertIf
        if (typeToConvert == typeof(TImplementation))
        {
            return new BoundDataObjectConverter<TImplementation>
            (
                Factory,
                _allowExtraProperties,
                writeProperties.ToArray(),
                readProperties.ToArray()
            );
        }

        throw new ArgumentException("This converter cannot convert the provided type.", nameof(typeToConvert));
    }

    /// <summary>
    /// Returns whether the specified property should be included when serializing even if it is read-only.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>Whether the property should be included even if it is read-only.</returns>
    private bool ShouldIncludeReadOnlyProperty(CompileTimePropertyKey property)
    {
        return _includeReadOnlyOverrides.Contains(property);
    }

    /// <summary>
    /// Gets the JSON property names for reading the specified property.
    /// </summary>
    /// <param name="dtoProperty">The property to get the names for.</param>
    /// <param name="options">The active serializer options.</param>
    /// <returns>An array of the supported names for this property.</returns>
    private string[] GetReadJsonPropertyName(CompileTimePropertyKey dtoProperty, JsonSerializerOptions options)
    {
        return _readNameOverrides.TryGetValue(dtoProperty, out var overriddenName)
            ? overriddenName
            : [options.PropertyNamingPolicy?.ConvertName(dtoProperty.Name) ?? dtoProperty.Name];
    }

    /// <summary>
    /// Gets the JSON property name for writing the specified property.
    /// </summary>
    /// <param name="dtoProperty">The property to get the name for.</param>
    /// <param name="options">The active serializer options.</param>
    /// <returns>The name to write the property with.</returns>
    private string GetWriteJsonPropertyName(CompileTimePropertyKey dtoProperty, JsonSerializerOptions options)
    {
        if (_writeNameOverrides.TryGetValue(dtoProperty, out var overriddenName))
        {
            return overriddenName;
        }

        return options.PropertyNamingPolicy?.ConvertName(dtoProperty.Name) ?? dtoProperty.Name;
    }

    /// <summary>
    /// Gets the property converter for a specified property.
    /// </summary>
    /// <param name="dtoProperty">The property to get a property converter for.</param>
    /// <param name="options">The active serializer options.</param>
    /// <returns>
    /// The registered property converter, or <see langword="null"/> if no property converter was added.
    /// </returns>
    private JsonConverter? GetConverter(CompileTimePropertyInfo dtoProperty, JsonSerializerOptions options)
    {
        if (_converterOverrides.TryGetValue(dtoProperty, out var converter))
        {
            return converter;
        }

        if (!_converterFactoryOverrides.TryGetValue(dtoProperty, out var converterFactory))
        {
            return null;
        }

        var innerType = dtoProperty.UnwrappedPropertyType;

        return converterFactory.CreateConverter(innerType, options);
    }
}
