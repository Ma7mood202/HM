using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hm.WebApi.Converters;

/// <summary>
/// Allows DateOnly to be deserialized from "yyyy-MM-dd" or other common date JSON strings.
/// </summary>
public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            throw new JsonException("Date value cannot be null or empty.");
        return DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
