//
//  JsonAssert.cs
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
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Remora.Rest.Xunit;

/// <summary>
/// Defines various additional assertions for xUnit.
/// </summary>
[PublicAPI]
public static class JsonAssert
{
    /// <summary>
    /// Asserts that the given <see cref="JsonDocument"/> values are equivalent, that is, ordering of properties
    /// may differ, but the logical contents must be the same.
    /// </summary>
    /// <param name="expected">The expected object.</param>
    /// <param name="actual">The actual object.</param>
    /// <param name="assertOptions">The assertion options.</param>
    public static void Equivalent
    (
        JsonDocument expected,
        JsonDocument actual,
        JsonAssertOptions? assertOptions = default
    )
        => Equivalent(expected.RootElement, actual.RootElement, assertOptions);

    /// <summary>
    /// Asserts that the given <see cref="JsonDocument"/> values are equivalent, that is, ordering of properties
    /// in an object may differ, but the logical contents must be the same. Arrays must equal in both count,
    /// elements, and order.
    /// </summary>
    /// <param name="expected">The expected object.</param>
    /// <param name="actual">The actual object.</param>
    /// <param name="assertOptions">The assertion options.</param>
    public static void Equivalent
    (
        JsonElement expected,
        JsonElement actual,
        JsonAssertOptions? assertOptions = default
    )
    {
        assertOptions ??= JsonAssertOptions.Default;

        actual.ValueKind
            .Should().Be(expected.ValueKind);

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var actualElements = actual.EnumerateObject().ToList();
                var expectedElements = expected.EnumerateObject().ToList();

                foreach (var expectedElement in expectedElements)
                {
                    var allowedToSkip = assertOptions.AllowMissing.Contains(expectedElement.Name) ||
                                        assertOptions.AllowMissingBy(expectedElement);

                    if (allowedToSkip)
                    {
                        if (!actualElements.Any(ae => ae.NameEquals(expectedElement.Name)))
                        {
                            continue;
                        }
                    }

                    actualElements
                        .Should().ContainSingle(e => e.NameEquals(expectedElement.Name));

                    var matchingElement = actualElements.Single(ae => ae.NameEquals(expectedElement.Name));
                    Equivalent(expectedElement.Value, matchingElement.Value, assertOptions);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var actualElements = actual.EnumerateArray().ToList();
                var expectedElements = expected.EnumerateArray()
                    .Where(e => !assertOptions.AllowSkip(e))
                    .ToList();

                actualElements.Should().HaveSameCount(expectedElements);

                for (var i = 0; i < expectedElements.Count; ++i)
                {
                    Equivalent(expectedElements[i], actualElements[i], assertOptions);
                }

                break;
            }
            case JsonValueKind.String:
            {
                actual.GetString().Should().BeEquivalentTo(expected.GetString());
                break;
            }
            case JsonValueKind.Number:
            {
                actual.GetDouble().Should().Be(expected.GetDouble());
                break;
            }
            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                actual.GetBoolean().Should().Be(expected.GetBoolean());
                break;
            }
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
            {
                // Equal by definition
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(expected));
            }
        }
    }
}
