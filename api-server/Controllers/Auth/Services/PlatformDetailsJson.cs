using System.Text.Json;
using System.Text.Json.Nodes;

namespace api_server.Auth
{
    /// <summary>
    /// <c>ClientSideInfo.PlatformDetails</c> is a JSON document the client sends; the server adds
    /// the client IP it observed before the document is stored on the session.
    /// </summary>
    public static class PlatformDetailsJson
    {
        public const string ClientIpProperty = "ClientIp";

        /// <summary>
        /// Returns <paramref name="platformDetails"/> with <see cref="ClientIpProperty"/> set to <paramref name="clientIp"/>
        /// (replacing any value the client sent, so it can't be spoofed). Empty input gives a document holding only the IP;
        /// input that isn't a JSON object is kept under "Raw" so the stored value is always a valid JSON object.
        /// With no IP the input is returned unchanged.
        /// </summary>
        public static string WithClientIp(string? platformDetails, string? clientIp)
        {
            platformDetails ??= string.Empty;
            if (string.IsNullOrWhiteSpace(clientIp))
                return platformDetails;

            JsonObject root;
            if (string.IsNullOrWhiteSpace(platformDetails))
            {
                root = new JsonObject();
            }
            else
            {
                try
                {
                    root = JsonNode.Parse(platformDetails) as JsonObject
                           ?? new JsonObject { ["Raw"] = platformDetails };
                }
                catch (JsonException)
                {
                    root = new JsonObject { ["Raw"] = platformDetails };
                }
            }

            root[ClientIpProperty] = clientIp;
            return root.ToJsonString();
        }
    }
}
