//
//  SPDX-FileName: DTOPropertyInfo.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Core;
using Remora.Rest.Json.Reflection;

namespace Remora.Rest.Json.Internal;

/// <summary>
/// Bundles data needed for properties in <see cref="BoundDataObjectConverter{T}"/>.
/// </summary>
/// <param name="Property">The CLR property of the implementation type.</param>
/// <param name="ReadNames">The names this property allows when reading.</param>
/// <param name="WriteName">The name used when writing this property.</param>
/// <param name="Reader">A delegate that reads the property.</param>
/// <param name="Writer">A delegate that writes the property.</param>
/// <param name="AllowsNull">Whether this property allows null.</param>
/// <param name="DefaultValue">Empty, if this property is required. Otherwise its default value.</param>
/// <param name="Converter">The <see cref="JsonConverter"/> used when serializing this property, if any.</param>
/// <param name="ReadIndex">
/// The index of this property in the _readProperties array. Has no meaning when writing the object to JSON.
/// </param>
internal sealed record DTOPropertyInfo
(
    PropertyInfo Property,
    string[] ReadNames,
    string WriteName,
    DTOPropertyReader Reader,
    DTOPropertyWriter Writer,
    bool AllowsNull,
    Optional<object?> DefaultValue,
    JsonConverter? Converter,
    int ReadIndex
);