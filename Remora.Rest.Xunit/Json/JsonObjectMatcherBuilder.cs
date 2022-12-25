//
//  JsonObjectMatcherBuilder.cs
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
using FluentAssertions;
using JetBrains.Annotations;

namespace Remora.Rest.Xunit.Json;

/// <summary>
/// Builds instances of the <see cref="JsonObjectMatcher"/> class.
/// </summary>
[PublicAPI]
public class JsonObjectMatcherBuilder
{
    private readonly List<Func<JsonElement, bool>> _matchers = new();

    /// <summary>
    /// Adds a requirement that a given property should exist.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="elementMatcherBuilder">The additional requirements on the property value.</param>
    /// <returns>The builder, with the requirement.</returns>
    public JsonObjectMatcherBuilder WithProperty
    (
        string name,
        Action<JsonElementMatcherBuilder>? elementMatcherBuilder = null
    )
    {
        _matchers.Add
        (
            obj =>
            {
                obj.TryGetProperty(name, out var property)
                    .Should().NotBe(false, $"because a property named {name} should be present");

                if (elementMatcherBuilder is null)
                {
                    return true;
                }

                var matcherBuilder = new JsonElementMatcherBuilder();
                elementMatcherBuilder(matcherBuilder);

                return matcherBuilder.Build().Matches(property);
            }
        );

        return this;
    }

    /// <summary>
    /// Adds a requirement that a given property should not exist.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns>The builder, with the requirement.</returns>
    public JsonObjectMatcherBuilder WithoutProperty
    (
        string name
    )
    {
        _matchers.Add
        (
            obj =>
            {
                obj.TryGetProperty(name, out _)
                    .Should().Be(false, $"because a property named {name} should not be present");

                return true;
            }
        );

        return this;
    }

    /// <summary>
    /// Builds the object matcher.
    /// </summary>
    /// <returns>The built object matcher.</returns>
    public JsonObjectMatcher Build()
    {
        return new(_matchers);
    }
}
