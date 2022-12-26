//
//  SnowflakeDictionaryConverter.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Remora.Rest.Core;

namespace Remora.Rest.Json;

/// <inheritdoc />
[PublicAPI]
public class SnowflakeDictionaryConverter<TElement> : JsonConverter<IReadOnlyDictionary<Snowflake, TElement>>
{
    /// <summary>
    /// Gets the epoch used for converting snowflakes.
    /// </summary>
    public ulong Epoch { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SnowflakeDictionaryConverter{TElement}"/> class.
    /// </summary>
    /// <param name="epoch">The epoch to use.</param>
    public SnowflakeDictionaryConverter(ulong epoch)
    {
        this.Epoch = epoch;
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<Snowflake, TElement>? Read
    (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var dictionary = JsonSerializer.Deserialize<IReadOnlyDictionary<string, TElement>>(ref reader, options);
        if (dictionary is null)
        {
            return null;
        }

        var mappedDictionary = new Dictionary<Snowflake, TElement>();
        foreach (var (key, element) in dictionary)
        {
            if (!Snowflake.TryParse(key, out var snowflakeKey, this.Epoch))
            {
                throw new JsonException();
            }

            mappedDictionary.Add(snowflakeKey.Value, element);
        }

        return mappedDictionary;
    }

    /// <inheritdoc />
    public override void Write
    (
        Utf8JsonWriter writer,
        IReadOnlyDictionary<Snowflake, TElement> value,
        JsonSerializerOptions options
    )
    {
        var mappedDictionary = new Dictionary<string, TElement>();
        foreach (var (key, element) in value)
        {
            mappedDictionary.Add(key.ToString(), element);
        }

        JsonSerializer.Serialize(writer, mappedDictionary, options);
    }
}
