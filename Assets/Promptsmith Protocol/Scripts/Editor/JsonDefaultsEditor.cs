#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using Newtonsoft.Json;

[InitializeOnLoad]
static class JsonDefaultsEditor
{
    static JsonDefaultsEditor()
    {
        SetJsonDefaults();
    }

    [InitializeOnLoadMethod]
    static void SetJsonDefaults()
    {
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = new List<JsonConverter> { new SafeStringEnumConverter() },
            Error = (sender, args) =>
            {
                // Swallow enum parse and other JsonSerialization exceptions to avoid noisy editor stack traces.
                // We mark the error as handled so deserialization continues and default values remain.
                args.ErrorContext.Handled = true;
            }
        };
    }
}
#endif