//
//  SPDX-FileName: OptionalExtensionsStruct.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using JetBrains.Annotations;

namespace Remora.Rest.Core;

/// <summary>
/// Contains extension methods for <see cref="Optional{TValue}"/> where the contained value is a value type.
/// </summary>
[PublicAPI]
public static class OptionalExtensionsStruct
{
    /// <summary>
    /// Casts the current <see cref="Optional{TValue}"/> to a nullable <typeparamref name="T"/>?.
    /// </summary>
    /// <param name="optional">The optional.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>
    /// An <see cref="Optional{TValue}"/> with the type parameter changed to <typeparamref name="T"/>?.
    /// </returns>
    public static Optional<T?> AsNullableOptional<T>(this Optional<T> optional) where T : struct
    {
        return optional.TryGet(out var value)
            ? value
            : default(Optional<T?>);
    }

    /// <summary>
    /// Converts an <see cref="Optional{TValue}"/> with a null value to an Optional with no value; otherwise, returns
    /// the unmodified <see cref="Optional{TValue}"/>.
    /// </summary>
    /// <param name="optional">The optional.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The <see cref="Optional{TValue}"/>.</returns>
    public static Optional<T> ConvertNullToEmpty<T>(this Optional<T?> optional) where T : struct
    {
        return optional.IsDefined(out var value)
            ? new Optional<T>(value.Value)
            : default;
    }
}
