using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PromptsmithProtocol
{
    /// <summary>
    /// A permissive string->enum converter that falls back to the enum's default value (0) when
    /// the incoming string is unknown. This prevents noisy exceptions when servers return newer
    /// enum members the client does not yet know about.
    /// </summary>
    public sealed class SafeStringEnumConverter : JsonConverter
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

                // Unknown string -> fallback to default value (usually 0)
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

            // For any other token, fallback
            return isNullable ? null : Activator.CreateInstance(enumType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else writer.WriteValue(value.ToString());
        }
    }
}
