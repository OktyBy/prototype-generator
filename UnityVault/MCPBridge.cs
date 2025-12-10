using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using static UnityVault.Editor.LibraryManager;

namespace UnityVault.Editor
{
    /// <summary>
    /// Bridge to communicate with Claude MCP server for generating missing systems.
    /// Uses TCP connection to unity-god-mode MCP server.
    /// </summary>
    public static class MCPBridge
    {
        #region Constants

        private const string DEFAULT_HOST = "127.0.0.1";
        private const int DEFAULT_PORT = 7777;
        private const int CONNECTION_TIMEOUT = 5000;
        private const int READ_TIMEOUT = 30000;

        #endregion

        #region Properties

        public static bool IsConnected => CheckConnection();
        public static string LastError { get; private set; }

        #endregion

        #region Connection Methods

        /// <summary>
        /// Check if MCP server is available.
        /// </summary>
        public static bool CheckConnection()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(DEFAULT_HOST, DEFAULT_PORT, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000));

                    if (success && client.Connected)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Connection failed
            }
            return false;
        }

        /// <summary>
        /// Ping the MCP server.
        /// </summary>
        public static async Task<bool> PingAsync()
        {
            try
            {
                var response = await SendCommandAsync("ping", new { });
                return response != null && response.Contains("pong");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        #endregion

        #region System Generation

        /// <summary>
        /// Request Claude to generate a missing system.
        /// </summary>
        public static async Task<GenerationResult> GenerateSystemAsync(string systemId, SystemData systemInfo)
        {
            var result = new GenerationResult { systemId = systemId };

            if (!CheckConnection())
            {
                result.errorMessage = "MCP server not available. Please ensure unity-god-mode bridge is running.";
                return result;
            }

            try
            {
                // Build generation prompt
                var prompt = BuildGenerationPrompt(systemInfo);

                // Send to Claude via MCP
                var response = await SendCommandAsync("generate_script", new
                {
                    name = systemInfo.name.Replace(" ", ""),
                    description = systemInfo.description,
                    category = systemInfo.category,
                    prompt = prompt,
                    namespace_name = $"UnityVault.{systemInfo.category}"
                });

                if (string.IsNullOrEmpty(response))
                {
                    result.errorMessage = "No response from MCP server";
                    return result;
                }

                // Parse response
                result.success = true;
                result.generatedCode = response;
                result.outputPath = SaveGeneratedSystem(systemId, systemInfo, response);

                Debug.Log($"[MCPBridge] Generated system: {systemId}");
            }
            catch (Exception ex)
            {
                result.errorMessage = $"Generation failed: {ex.Message}";
                Debug.LogError($"[MCPBridge] {result.errorMessage}");
            }

            return result;
        }

        /// <summary>
        /// Generate multiple systems in batch.
        /// </summary>
        public static async Task<List<GenerationResult>> GenerateSystemsBatchAsync(List<SystemData> systems)
        {
            var results = new List<GenerationResult>();

            foreach (var system in systems)
            {
                var result = await GenerateSystemAsync(system.id, system);
                results.Add(result);

                // Small delay between requests
                await Task.Delay(500);
            }

            return results;
        }

        #endregion

        #region Script Operations

        /// <summary>
        /// Create a script via MCP.
        /// </summary>
        public static async Task<string> CreateScriptAsync(string scriptName, string code, string folder = null)
        {
            try
            {
                var response = await SendCommandAsync("unity_create_script", new
                {
                    name = scriptName,
                    code = code,
                    folder = folder ?? "Assets/UnityVault/Generated"
                });

                return response;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Read a script via MCP.
        /// </summary>
        public static async Task<string> ReadScriptAsync(string scriptPath)
        {
            try
            {
                var response = await SendCommandAsync("unity_read_script", new
                {
                    path = scriptPath
                });

                return response;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        #endregion

        #region Private Methods

        private static async Task<string> SendCommandAsync(string command, object parameters)
        {
            using (var client = new TcpClient())
            {
                // Connect with timeout
                var connectTask = client.ConnectAsync(DEFAULT_HOST, DEFAULT_PORT);
                if (await Task.WhenAny(connectTask, Task.Delay(CONNECTION_TIMEOUT)) != connectTask)
                {
                    throw new TimeoutException("Connection timeout");
                }

                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = READ_TIMEOUT;
                    stream.WriteTimeout = CONNECTION_TIMEOUT;

                    // Build JSON-RPC request
                    var request = new
                    {
                        jsonrpc = "2.0",
                        id = Guid.NewGuid().ToString(),
                        method = command,
                        @params = parameters
                    };

                    var json = JsonUtility.ToJson(request);
                    var data = Encoding.UTF8.GetBytes(json + "\n");

                    // Send
                    await stream.WriteAsync(data, 0, data.Length);
                    await stream.FlushAsync();

                    // Read response
                    var buffer = new byte[65536];
                    var responseBuilder = new StringBuilder();

                    do
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    }
                    while (stream.DataAvailable);

                    return responseBuilder.ToString();
                }
            }
        }

        private static string BuildGenerationPrompt(SystemData system)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Generate a complete Unity C# script for: {system.name}");
            sb.AppendLine();
            sb.AppendLine($"Description: {system.description}");
            sb.AppendLine($"Category: {system.category}");
            sb.AppendLine($"Namespace: UnityVault.{system.category}");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Use MonoBehaviour as base class");
            sb.AppendLine("- Include both UnityEvents and C# events");
            sb.AppendLine("- Add [Header] attributes for inspector organization");
            sb.AppendLine("- Include XML documentation comments");
            sb.AppendLine("- Make it production-ready with proper error handling");
            sb.AppendLine("- Follow Unity best practices");

            if (system.dependencies != null && system.dependencies.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Dependencies: {string.Join(", ", system.dependencies)}");
            }

            if (system.tags != null && system.tags.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Tags/Features: {string.Join(", ", system.tags)}");
            }

            return sb.ToString();
        }

        private static string SaveGeneratedSystem(string systemId, SystemData systemInfo, string code)
        {
            // Save to library
            var libraryPath = LibraryManager.LibraryPath;
            var systemFolder = Path.Combine(libraryPath, systemInfo.category, systemId);

            if (!Directory.Exists(systemFolder))
            {
                Directory.CreateDirectory(systemFolder);
            }

            var scriptPath = Path.Combine(systemFolder, $"{systemId}.cs");
            File.WriteAllText(scriptPath, code);

            // Create metadata
            var metadata = new SystemMetadata
            {
                name = systemInfo.name,
                version = "1.0.0",
                author = "Claude (UnityVault)",
                category = systemInfo.category,
                description = systemInfo.description,
                files = new[] { $"{systemId}.cs" },
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var metadataPath = Path.Combine(systemFolder, "metadata.json");
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));

            return scriptPath;
        }

        #endregion

        #region Data Classes

        [Serializable]
        public class GenerationResult
        {
            public bool success;
            public string systemId;
            public string generatedCode;
            public string outputPath;
            public string errorMessage;
        }

        #endregion

        #region Scene Setup Methods

        /// <summary>
        /// Setup a system in the scene hierarchy via MCP.
        /// </summary>
        public static async Task<bool> SetupSystemInSceneAsync(string systemId, SystemSetupData setupData)
        {
            if (!CheckConnection())
            {
                LastError = "MCP not connected";
                return false;
            }

            try
            {
                // Create parent GameObject if needed
                if (!string.IsNullOrEmpty(setupData.parentPath))
                {
                    await SendCommandAsync("unity_create_gameobject", new
                    {
                        name = setupData.parentPath,
                        parent = ""
                    });
                }

                // Create main GameObject
                var createResult = await SendCommandAsync("unity_create_gameobject", new
                {
                    name = setupData.gameObjectName,
                    parent = setupData.parentPath ?? ""
                });

                if (string.IsNullOrEmpty(createResult))
                {
                    LastError = "Failed to create GameObject";
                    return false;
                }

                // Add required components
                foreach (var component in setupData.components)
                {
                    await SendCommandAsync("unity_add_component", new
                    {
                        game_object = setupData.gameObjectName,
                        component_type = component.typeName
                    });

                    // Set component properties
                    if (component.properties != null)
                    {
                        foreach (var prop in component.properties)
                        {
                            await SendCommandAsync("unity_set_component_property", new
                            {
                                game_object = setupData.gameObjectName,
                                component_type = component.typeName,
                                property_name = prop.Key,
                                value = prop.Value
                            });
                        }
                    }
                }

                // Set transform if specified
                if (setupData.position != null || setupData.rotation != null || setupData.scale != null)
                {
                    await SendCommandAsync("unity_set_transform", new
                    {
                        game_object = setupData.gameObjectName,
                        position = setupData.position ?? new float[] { 0, 0, 0 },
                        rotation = setupData.rotation ?? new float[] { 0, 0, 0 },
                        scale = setupData.scale ?? new float[] { 1, 1, 1 }
                    });
                }

                // Create child objects
                if (setupData.children != null)
                {
                    foreach (var child in setupData.children)
                    {
                        child.parentPath = setupData.gameObjectName;
                        await SetupSystemInSceneAsync(systemId + "_child", child);
                    }
                }

                Debug.Log($"[MCPBridge] Setup complete for: {setupData.gameObjectName}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[MCPBridge] Setup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Setup UI system with Canvas.
        /// </summary>
        public static async Task<bool> SetupUISystemAsync(string systemId, UISetupData uiData)
        {
            if (!CheckConnection())
            {
                LastError = "MCP not connected";
                return false;
            }

            try
            {
                // Create or find Canvas
                string canvasName = uiData.canvasName ?? "Canvas";

                await SendCommandAsync("unity_create_gameobject", new
                {
                    name = canvasName,
                    parent = ""
                });

                await SendCommandAsync("unity_add_component", new
                {
                    game_object = canvasName,
                    component_type = "UnityEngine.Canvas"
                });

                await SendCommandAsync("unity_set_component_property", new
                {
                    game_object = canvasName,
                    component_type = "UnityEngine.Canvas",
                    property_name = "renderMode",
                    value = (int)uiData.renderMode
                });

                await SendCommandAsync("unity_add_component", new
                {
                    game_object = canvasName,
                    component_type = "UnityEngine.UI.CanvasScaler"
                });

                await SendCommandAsync("unity_add_component", new
                {
                    game_object = canvasName,
                    component_type = "UnityEngine.UI.GraphicRaycaster"
                });

                // Create UI element
                await SendCommandAsync("unity_create_gameobject", new
                {
                    name = uiData.elementName,
                    parent = canvasName
                });

                // Add RectTransform properties
                if (uiData.anchorMin != null && uiData.anchorMax != null)
                {
                    await SendCommandAsync("unity_set_component_property", new
                    {
                        game_object = uiData.elementName,
                        component_type = "UnityEngine.RectTransform",
                        property_name = "anchorMin",
                        value = uiData.anchorMin
                    });

                    await SendCommandAsync("unity_set_component_property", new
                    {
                        game_object = uiData.elementName,
                        component_type = "UnityEngine.RectTransform",
                        property_name = "anchorMax",
                        value = uiData.anchorMax
                    });
                }

                if (uiData.sizeDelta != null)
                {
                    await SendCommandAsync("unity_set_component_property", new
                    {
                        game_object = uiData.elementName,
                        component_type = "UnityEngine.RectTransform",
                        property_name = "sizeDelta",
                        value = uiData.sizeDelta
                    });
                }

                // Add UI components
                foreach (var component in uiData.components)
                {
                    await SendCommandAsync("unity_add_component", new
                    {
                        game_object = uiData.elementName,
                        component_type = component.typeName
                    });

                    if (component.properties != null)
                    {
                        foreach (var prop in component.properties)
                        {
                            await SendCommandAsync("unity_set_component_property", new
                            {
                                game_object = uiData.elementName,
                                component_type = component.typeName,
                                property_name = prop.Key,
                                value = prop.Value
                            });
                        }
                    }
                }

                // Create child UI elements
                if (uiData.children != null)
                {
                    foreach (var child in uiData.children)
                    {
                        child.canvasName = null; // Don't create new canvas
                        child.parentElement = uiData.elementName;
                        await SetupUIChildAsync(child);
                    }
                }

                Debug.Log($"[MCPBridge] UI Setup complete for: {uiData.elementName}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[MCPBridge] UI Setup failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> SetupUIChildAsync(UISetupData child)
        {
            await SendCommandAsync("unity_create_gameobject", new
            {
                name = child.elementName,
                parent = child.parentElement
            });

            foreach (var component in child.components)
            {
                await SendCommandAsync("unity_add_component", new
                {
                    game_object = child.elementName,
                    component_type = component.typeName
                });
            }

            return true;
        }

        #endregion

        #region Setup Data Classes

        [Serializable]
        public class SystemSetupData
        {
            public string gameObjectName;
            public string parentPath;
            public ComponentSetup[] components;
            public float[] position;
            public float[] rotation;
            public float[] scale;
            public SystemSetupData[] children;
        }

        [Serializable]
        public class UISetupData
        {
            public string canvasName;
            public string elementName;
            public string parentElement;
            public int renderMode; // 0=ScreenSpaceOverlay, 1=ScreenSpaceCamera, 2=WorldSpace
            public float[] anchorMin;
            public float[] anchorMax;
            public float[] sizeDelta;
            public float[] anchoredPosition;
            public ComponentSetup[] components;
            public UISetupData[] children;
        }

        [Serializable]
        public class ComponentSetup
        {
            public string typeName;
            public Dictionary<string, object> properties;

            public ComponentSetup(string type)
            {
                typeName = type;
                properties = new Dictionary<string, object>();
            }
        }

        #endregion

        #region Editor Window Integration

        /// <summary>
        /// Show connection status in editor.
        /// </summary>
        public static void DrawConnectionStatus()
        {
            var connected = IsConnected;
            var statusColor = connected ? Color.green : Color.red;
            var statusText = connected ? "● Connected" : "○ Disconnected";

            var oldColor = GUI.color;
            GUI.color = statusColor;
            GUILayout.Label(statusText, EditorStyles.boldLabel);
            GUI.color = oldColor;

            if (!connected)
            {
                EditorGUILayout.HelpBox(
                    "MCP server not available. Missing systems cannot be generated.\n" +
                    "Start unity-god-mode bridge to enable generation.",
                    MessageType.Warning
                );
            }
        }

        #endregion
    }

    /// <summary>
    /// Editor window for MCP connection testing.
    /// </summary>
    public class MCPBridgeDebugWindow : EditorWindow
    {
        private string statusMessage = "Not tested";
        private bool isConnected;

        [MenuItem("Window/UnityVault/MCP Debug")]
        public static void ShowWindow()
        {
            GetWindow<MCPBridgeDebugWindow>("MCP Debug");
        }

        private void OnGUI()
        {
            GUILayout.Label("MCP Bridge Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            MCPBridge.DrawConnectionStatus();

            EditorGUILayout.Space();

            if (GUILayout.Button("Test Connection"))
            {
                TestConnection();
            }

            if (GUILayout.Button("Ping Server"))
            {
                PingServer();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status:", statusMessage);

            if (!string.IsNullOrEmpty(MCPBridge.LastError))
            {
                EditorGUILayout.HelpBox($"Last Error: {MCPBridge.LastError}", MessageType.Error);
            }
        }

        private void TestConnection()
        {
            isConnected = MCPBridge.CheckConnection();
            statusMessage = isConnected ? "Connected!" : "Connection failed";
            Repaint();
        }

        private async void PingServer()
        {
            statusMessage = "Pinging...";
            Repaint();

            var result = await MCPBridge.PingAsync();
            statusMessage = result ? "Pong received!" : "Ping failed";
            Repaint();
        }
    }
}
