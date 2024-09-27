using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remora.Rest.Json.Internal;
using Remora.Rest.Json.Reflection;

namespace Remora.Rest.Json;

internal class Identify : IIdentify
{
    public string Token { get; set; } = Token;

    public void Deconstruct(out string Token)
    {
        Token = this.Token;
    }
}

internal interface IIdentify
{
    /// <summary>
    /// Gets the authentication token.
    /// </summary>
    string Token { get; }
}

/// <inheritdoc/>
internal class SampleDataObjectConverter : AbstractDataObjectConverter<IIdentify, Identify>
{
    private static ObjectFactory<Identify> CachedFactory { get; } = args =>
    {
        var value = new Identify();
        value.Token = (string)args[0]!;
        return value;
    };

    private static IReadOnlyList<CompileTimePropertyInfo> CachedDtoProperties { get; } =
    [
        new(
            Name: "Token",
            PropertyType: typeof(string),
            UnwrappedPropertyType: typeof(string),
            DeclaringType: typeof(IIdentify),
            GetValue: static instance => ((IIdentify)instance).Token,
            Writer: static (writer, dtoProperty, value, options) =>
            {
                // if is optional and not hasvalue don't write
                writer.WritePropertyName(dtoProperty.WriteName);
                ((JsonConverter<string>)(dtoProperty.Converter ?? options.GetConverter(typeof(string)))).Write(writer, (string)value, options);
            },
            Reader: static (ref Utf8JsonReader reader, DTOPropertyInfo dtoProperty, JsonSerializerOptions options) =>
            {
                return ((JsonConverter<string>)(dtoProperty.Converter ?? options.GetConverter(typeof(string)))).Read(ref reader, typeof(string), options);
            },
            AllowsNull: false,
            CanWrite: true,
            DefaultValue: default // TODO
        )
    ];

    /// <inheritdoc/>
    private protected override ObjectFactory<Identify> Factory => CachedFactory;

    /// <inheritdoc/>
    private protected override IReadOnlyList<CompileTimePropertyInfo> DtoProperties => CachedDtoProperties;
}
