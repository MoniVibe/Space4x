using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_monobehaviour_script")]
    public static class CreateMonoBehaviourScriptTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string scriptName = @params["script_name"]?.ToString();
                string scriptPath = @params["script_path"]?.ToString();
                string namespaceName = @params["namespace"]?.ToString();
                JArray fields = @params["fields"]?.ToObject<JArray>();
                JArray methods = @params["methods"]?.ToObject<JArray>();
                
                if (string.IsNullOrEmpty(scriptName))
                {
                    return Response.Error("script_name is required");
                }
                
                if (string.IsNullOrEmpty(scriptPath))
                {
                    return Response.Error("script_path is required");
                }
                
                // Ensure path has .cs extension
                if (!scriptPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    scriptPath += ".cs";
                }
                
                // Check if file already exists
                if (File.Exists(scriptPath))
                {
                    return Response.Error($"Script already exists at {scriptPath}");
                }
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(scriptPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Generate script content
                string scriptContent = GenerateScriptContent(scriptName, namespaceName, fields, methods);
                
                // Write file
                File.WriteAllText(scriptPath, scriptContent, Encoding.UTF8);
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();
                
                return Response.Success($"MonoBehaviour script created successfully", new
                {
                    scriptName = scriptName,
                    scriptPath = scriptPath,
                    namespaceName = namespaceName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create MonoBehaviour script: {ex.Message}");
            }
        }
        
        private static string GenerateScriptContent(string className, string namespaceName, JArray fields, JArray methods)
        {
            var sb = new StringBuilder();
            
            // Using directives
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            
            // Namespace
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }
            
            // Class declaration
            sb.AppendLine($"    public class {className} : MonoBehaviour");
            sb.AppendLine("    {");
            
            // Fields
            if (fields != null && fields.Count > 0)
            {
                foreach (var field in fields)
                {
                    string fieldName = field["name"]?.ToString();
                    string fieldType = field["type"]?.ToString();
                    bool serialized = field["serialized"]?.ToObject<bool>() ?? false;
                    
                    if (!string.IsNullOrEmpty(fieldName) && !string.IsNullOrEmpty(fieldType))
                    {
                        if (serialized)
                        {
                            sb.AppendLine($"        [SerializeField] private {fieldType} {fieldName};");
                        }
                        else
                        {
                            sb.AppendLine($"        private {fieldType} {fieldName};");
                        }
                    }
                }
                sb.AppendLine();
            }
            
            // Methods
            if (methods != null && methods.Count > 0)
            {
                foreach (var method in methods)
                {
                    string methodName = method["name"]?.ToString();
                    string returnType = method["returnType"]?.ToString() ?? "void";
                    string body = method["body"]?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(methodName))
                    {
                        sb.AppendLine($"        private {returnType} {methodName}()");
                        sb.AppendLine("        {");
                        if (!string.IsNullOrEmpty(body))
                        {
                            sb.AppendLine($"            {body}");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }
                }
            }
            
            // Close class
            sb.AppendLine("    }");
            
            // Close namespace
            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine("}");
            }
            
            return sb.ToString();
        }
    }
}

