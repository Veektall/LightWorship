using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace LightWorship
{
    public static class VoiceboxTranscriptParser
    {
        public static string ExtractText(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
            {
                return "";
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(json);
                if (data == null)
                {
                    return "";
                }

                foreach (var key in new[] { "text", "transcript", "result" })
                {
                    if (data.ContainsKey(key) && data[key] != null)
                    {
                        return data[key].ToString().Trim();
                    }
                }
            }
            catch
            {
            }

            return json.Trim().Trim('"');
        }
    }
}
