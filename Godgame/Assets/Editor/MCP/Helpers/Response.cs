using Newtonsoft.Json.Linq;

namespace PureDOTS.Editor.MCP.Helpers
{
    /// <summary>
    /// Helper class for creating standardized responses from MCP tools.
    /// </summary>
    public static class Response
    {
        /// <summary>
        /// Create a success response with optional data.
        /// </summary>
        public static object Success(string message, object data = null)
        {
            var response = new JObject
            {
                ["success"] = true,
                ["message"] = message
            };
            
            if (data != null)
            {
                response["data"] = JToken.FromObject(data);
            }
            
            return response;
        }
        
        /// <summary>
        /// Create an error response.
        /// </summary>
        public static object Error(string errorMessage)
        {
            return new JObject
            {
                ["success"] = false,
                ["error"] = errorMessage
            };
        }
    }
}

