//
//  SPDX-FileName: CustomizationTests.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Remora.Rest.Extensions;
using Remora.Rest.Xunit;
using Xunit;

namespace Remora.Rest.Tests;

/// <summary>
/// Tests various available user customizations of API types, such as overriding the implementation of a data type
/// or adding an entirely new one.
/// </summary>
public class CustomizationTests
{
    /// <summary>
    /// Represents the public interface of an existing data model.
    /// </summary>
    public interface IExisting
    {
        /// <summary>
        /// Gets some existing value.
        /// </summary>
        string ExistingValue { get; }
    }

    /// <summary>
    /// Represents the implementation of an existing data model.
    /// </summary>
    /// <param name="ExistingValue">Some existing value.</param>
    public record Existing(string ExistingValue) : IExisting;

    /// <summary>
    /// Represents the customized implementation of an existing data model.
    /// </summary>
    /// <param name="ExistingValue">Some existing value.</param>
    /// <param name="AdditionalValue">Some additional value.</param>
    public record Customized(string ExistingValue, string AdditionalValue) : Existing(ExistingValue);

    /// <summary>
    /// Tests whether an existing data type can be overridden.
    /// </summary>
    [Fact]
    public void CanOverrideExistingDataType()
    {
        var serviceCollection = new ServiceCollection()
            .Configure<JsonSerializerOptions>
            (
                options =>
                {
                    // Add the existing type
                    options.AddDataObjectConverter<IExisting, Existing>();
                }
            );

        serviceCollection.Configure<JsonSerializerOptions>
        (
            options =>
            {
                // Override the existing type
                options.AddDataObjectConverter<IExisting, Customized>();
            }
        );

        var services = serviceCollection.BuildServiceProvider();

        var json = @"
            {
                ""ExistingValue"": ""some-value"",
                ""AdditionalValue"": ""some-other-value""
            }";

        var jsonOptions = services.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;
        var value = JsonSerializer.Deserialize<IExisting>(json, jsonOptions);

        Assert.NotNull(value);
        Assert.IsType<Customized>(value);
        Assert.Equal("some-other-value", ((Customized)value).AdditionalValue);
    }

    /// <summary>
    /// Tests whether an existing data type can be overridden.
    /// </summary>
    [Fact]
    public void CanSerializeOverriddenDataType()
    {
        var serviceCollection = new ServiceCollection()
            .Configure<JsonSerializerOptions>
            (
                options =>
                {
                    // Add the existing type
                    options.AddDataObjectConverter<IExisting, Existing>();
                }
            );

        serviceCollection.Configure<JsonSerializerOptions>
        (
            options =>
            {
                // Override the existing type
                options.AddDataObjectConverter<IExisting, Customized>();
            }
        );

        var services = serviceCollection.BuildServiceProvider();
        var value = new Customized("some-value", "some-other-value");

        var json = @"
            {
                ""ExistingValue"": ""some-value"",
                ""AdditionalValue"": ""some-other-value""
            }";

        var jsonOptions = services.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;
        var serializedJson = JsonSerializer.Serialize<IExisting>(value, jsonOptions);

        using var original = JsonDocument.Parse(json);
        using var serialized = JsonDocument.Parse(serializedJson);

        JsonAssert.Equivalent(original, serialized);
    }
}
