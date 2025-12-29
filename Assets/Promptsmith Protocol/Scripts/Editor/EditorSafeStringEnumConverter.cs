#if UNITY_EDITOR
using System;
using Newtonsoft.Json;

/// <summary>
/// Editor-only permissive string->enum converter used at editor load time to avoid
/// noisy exceptions from package deserialization that occurs before runtime initializers run.
/// </summary>
public sealed class EditorSafeStringEnumConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        var t = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return t.IsEnum;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var isNullable = Nullable.GetUnderlyingType(objectType) != null;
        var enumType = Nullable.GetUnderlyingType(objectType) ?? objectType;

        if (reader.TokenType == JsonToken.String)
        {
            var s = (string)reader.Value;
            if (string.IsNullOrEmpty(s)) return isNullable ? null : Activator.CreateInstance(enumType);

            try
            {
                if (Enum.TryParse(enumType, s, true, out var parsed)) return parsed;
            }
            catch
            {
                // swallow and fallback below
            }

            return isNullable ? null : Activator.CreateInstance(enumType);
        }

        if (reader.TokenType == JsonToken.Integer)
        {
            try
            {
                var v = Convert.ToInt32(reader.Value);
                return Enum.ToObject(enumType, v);
            }
            catch
            {
                return isNullable ? null : Activator.CreateInstance(enumType);
            }
        }

        return isNullable ? null : Activator.CreateInstance(enumType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null) writer.WriteNull();
        else writer.WriteValue(value.ToString());
    }
}
#endif