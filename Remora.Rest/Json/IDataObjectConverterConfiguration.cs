//
//  SPDX-FileName: IDataObjectConverterConfiguration.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Core;

namespace Remora.Rest.Json;

/// <summary>
/// Configuration type for <see cref="DataObjectConverter{TInterface,TImplementation}"/>.
/// </summary>
/// <typeparam name="TInterface">The interface that is seen in the objects.</typeparam>
/// <typeparam name="TImplementation">The concrete implementation.</typeparam>
public interface IDataObjectConverterConfiguration<TInterface, TImplementation> where TImplementation : TInterface
{
    /// <summary>
    /// Sets whether extra JSON properties without a matching DTO property are allowed. Such properties are, if
    /// allowed, ignored. Otherwise, they throw a <see cref="JsonException"/>.
    ///
    /// By default, this is true.
    /// </summary>
    /// <param name="allowExtraProperties">Whether to allow extra properties.</param>
    /// <returns>The converter, with the new setting.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> AllowExtraProperties(bool allowExtraProperties = true);

    /// <summary>
    /// Explicitly marks a property as included in the set of serialized properties. This is useful when readonly
    /// properties need to be serialized for some reason.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the inclusion.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> IncludeWhenSerializing<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression
    );

    /// <summary>
    /// Explicitly marks a property as excluded in the set of serialized properties. This is useful when read-write
    /// properties need to be kept off the wire for some reason.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the inclusion.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> ExcludeWhenSerializing<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression
    );

    /// <summary>
    /// Overrides the name of the given property when serializing and deserializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name
    );

    /// <summary>
    /// Overrides the name of the given property when serializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithWritePropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name
    );

    /// <summary>
    /// Overrides the name of the given property when deserializing JSON.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="name">The new name.</param>
    /// <param name="fallbacks">The fallback names to use if the primary name isn't present.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithReadPropertyName<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        string name,
        params string[] fallbacks
    );

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        JsonConverter<TProperty> converter
    );

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, Optional<TProperty>>> propertyExpression,
        JsonConverter<TProperty> converter
    );

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty?>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct;

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, Optional<TProperty?>>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct;

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable>
    (
        Expression<Func<TImplementation, TEnumerable>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>;

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converter">The JSON converter.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable>
    (
        Expression<Func<TImplementation, Optional<TEnumerable>>> propertyExpression,
        JsonConverter<TProperty> converter
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>;

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TConverter>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression
    )
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TConverter>
    (
        Expression<Func<TImplementation, Optional<TProperty>>> propertyExpression
    )
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TConverter>
    (
        Expression<Func<TImplementation, TProperty?>> propertyExpression
    )
        where TProperty : struct
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TConverter>
    (
        Expression<Func<TImplementation, Optional<TProperty?>>> propertyExpression
    )
        where TProperty : struct
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable, TConverter>
    (
        Expression<Func<TImplementation, TEnumerable>> propertyExpression
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <typeparam name="TEnumerable">The enumerable type.</typeparam>
    /// <typeparam name="TConverter">The JSON converter type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty, TEnumerable, TConverter>
    (
        Expression<Func<TImplementation, Optional<TEnumerable>>> propertyExpression
    )
        where TProperty : struct
        where TEnumerable : IEnumerable<TProperty>
        where TConverter : JsonConverter<TProperty>, new()
        => WithPropertyConverter(propertyExpression, new TConverter());

    /// <summary>
    /// Overrides the converter of the given property.
    /// </summary>
    /// <param name="propertyExpression">The property expression.</param>
    /// <param name="converterFactory">The JSON converter factory.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <returns>The converter, with the property name.</returns>
    IDataObjectConverterConfiguration<TInterface, TImplementation> WithPropertyConverter<TProperty>
    (
        Expression<Func<TImplementation, TProperty>> propertyExpression,
        JsonConverterFactory converterFactory
    );
}
