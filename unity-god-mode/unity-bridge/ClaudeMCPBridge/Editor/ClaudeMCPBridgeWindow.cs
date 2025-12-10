using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeMCP
{
    /// <summary>
    /// Unity Editor window that acts as a bridge between Claude Code MCP Server and Unity Editor.
    /// Listens on TCP port 7777 for commands from the MCP server.
    /// </summary>
    public class ClaudeMCPBridgeWindow : EditorWindow
    {
        private TcpListener tcpListener;
        private Thread listenerThread;
        private bool isRunning = false;
        private const int PORT = 7777;

        private List<string> logMessages = new List<string>();
        private Vector2 scrollPosition;
        private const int MAX_LOG_MESSAGES = 100;
        private Queue<string> pendingLogs = new Queue<string>();
        private readonly object logLock = new object();

        [MenuItem("Window/Claude MCP Bridge")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClaudeMCPBridgeWindow>("Claude MCP Bridge");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            StartServer();
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void OnGUI()
        {
            // Process pending logs on main thread
            ProcessPendingLogs();

            EditorGUILayout.Space(10);

            // Header
            EditorGUILayout.LabelField("Claude MCP Bridge for Unity 6", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(60));

            if (isRunning)
            {
                GUI.contentColor = Color.green;
                EditorGUILayout.LabelField("● Running", EditorStyles.boldLabel);
            }
            else
            {
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("● Stopped", EditorStyles.boldLabel);
            }
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(60));
            EditorGUILayout.LabelField(PORT.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Server Log", EditorStyles.boldLabel);

            // Log area
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            foreach (var message in logMessages)
            {
                EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(isRunning ? "Restart Server" : "Start Server"))
            {
                if (isRunning) StopServer();
                StartServer();
            }

            if (GUILayout.Button("Clear Log"))
            {
                logMessages.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "This bridge enables Claude Code to control Unity Editor in real-time.\n" +
                "Keep this window open while using Unity God Mode features in Claude Code.",
                MessageType.Info
            );
        }

        private void StartServer()
        {
            if (isRunning) return;

            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, PORT);
                tcpListener.Start();

                listenerThread = new Thread(ListenForClients);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                isRunning = true;
                AddLog($"✅ Server started on port {PORT}");
                Debug.Log($"[Claude MCP Bridge] Server started on port {PORT}");
            }
            catch (Exception ex)
            {
                AddLog($"❌ Failed to start server: {ex.Message}");
                Debug.LogError($"[Claude MCP Bridge] Failed to start server: {ex.Message}");
            }
        }

        private void StopServer()
        {
            if (!isRunning) return;

            isRunning = false;

            try
            {
                if (tcpListener != null)
                {
                    tcpListener.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Claude MCP Bridge] Error stopping listener: {ex.Message}");
            }

            // Don't abort thread, just let it finish naturally
            listenerThread = null;

            AddLog("⚠️ Server stopped");
            Debug.Log("[Claude MCP Bridge] Server stopped");
        }

        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"[Claude MCP Bridge] Listener error: {ex.Message}");
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            try
            {
                string line;
                while ((line = reader.ReadLine()) != null && isRunning)
                {
                    string response = ProcessCommand(line);
                    writer.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Claude MCP Bridge] Client handler error: {ex.Message}");
            }
            finally
            {
                stream.Close();
                client.Close();
            }
        }

        private string ProcessCommand(string jsonCommand)
        {
            try
            {
                // Parse the incoming JSON manually to handle params properly
                var jsonObj = MiniJSON.Json.Deserialize(jsonCommand) as Dictionary<string, object>;
                if (jsonObj == null)
                    throw new Exception("Invalid JSON command");

                string commandName = jsonObj.ContainsKey("command") ? jsonObj["command"] as string : null;
                if (string.IsNullOrEmpty(commandName))
                    throw new Exception("Command name is missing");

                AddLogThreadSafe($"📨 Command: {commandName}");

                // Convert params object to JSON string
                string paramsJson = "{}";
                if (jsonObj.ContainsKey("params") && jsonObj["params"] != null)
                {
                    paramsJson = MiniJSON.Json.Serialize(jsonObj["params"]);
                }

                object result = null;
                Exception executionError = null;
                bool executed = false;

                // Execute on main thread - use update delegate for immediate execution
                Debug.Log($"[Bridge] Scheduling command: {commandName} with params: {paramsJson}");
                EditorApplication.CallbackFunction callback = null;
                callback = () =>
                {
                    Debug.Log($"[Bridge] Executing command: {commandName}");
                    EditorApplication.update -= callback;
                    try
                    {
                        result = ExecuteCommand(commandName, paramsJson);
                        Debug.Log($"[Bridge] Command executed successfully: {commandName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Bridge] Command execution error: {ex.Message}\n{ex.StackTrace}");
                        executionError = ex;
                    }
                    finally
                    {
                        executed = true;
                    }
                };
                EditorApplication.update += callback;

                // Wait for execution (with timeout) - 10 seconds
                int timeout = 0;
                while (!executed && timeout < 1000)
                {
                    Thread.Sleep(10);
                    timeout++;
                }

                if (!executed)
                {
                    string error = "Command execution timeout";
                    AddLogThreadSafe($"❌ Error: {error}");
                    var errorResponse = new ErrorResponse { error = error };
                    return JsonUtility.ToJson(errorResponse);
                }

                if (executionError != null)
                {
                    AddLogThreadSafe($"❌ Error: {executionError.Message}");
                    var errorResponse = new ErrorResponse { error = executionError.Message };
                    return JsonUtility.ToJson(errorResponse);
                }

                AddLogThreadSafe($"✅ Success: {commandName}");

                // Directly serialize the result
                if (result != null)
                {
                    string resultJson = JsonUtility.ToJson(result);
                    Debug.Log($"[Bridge] Result type: {result.GetType().Name}, JSON: {resultJson}");

                    // If JsonUtility returns empty object, use manual serialization for ProjectInfo
                    if (result is ProjectInfo info)
                    {
                        return $"{{\"result\":{{\"unityVersion\":\"{info.unityVersion}\",\"projectName\":\"{info.projectName}\",\"projectPath\":\"{info.projectPath}\",\"platform\":\"{info.platform}\",\"companyName\":\"{info.companyName}\"}}}}";
                    }

                    // Manual serialization for HierarchyResult (arrays not supported by JsonUtility)
                    if (result is HierarchyResult hierarchyResult)
                    {
                        string hierarchyJson = "[" + string.Join(",", hierarchyResult.hierarchy.Select(h => $"\"{h}\"")) + "]";
                        return $"{{\"result\":{{\"hierarchy\":{hierarchyJson}}}}}";
                    }

                    return $"{{\"result\":{resultJson}}}";
                }

                return "{\"result\":null}";
            }
            catch (Exception ex)
            {
                AddLogThreadSafe($"❌ Error: {ex.Message}");
                var errorResponse = new ErrorResponse { error = ex.Message };
                return JsonUtility.ToJson(errorResponse);
            }
        }

        private object ExecuteCommand(string command, string paramsJson)
        {
            Debug.Log($"[Bridge] ExecuteCommand - command: '{command}', params: '{paramsJson}'");

            switch (command)
            {
                case "CreateGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: CreateGameObject case");
                    return CreateGameObject(paramsJson);

                case "SetTransform":
                    Debug.Log("[Bridge] ExecuteCommand: SetTransform case");
                    return SetTransform(paramsJson);

                case "AddComponent":
                    Debug.Log("[Bridge] ExecuteCommand: AddComponent case");
                    return AddComponent(paramsJson);

                case "CreateScene":
                    Debug.Log("[Bridge] ExecuteCommand: CreateScene case");
                    return CreateScene(paramsJson);

                case "SaveScene":
                    Debug.Log("[Bridge] ExecuteCommand: SaveScene case");
                    return SaveScene(paramsJson);

                case "ListScenes":
                    Debug.Log("[Bridge] ExecuteCommand: ListScenes case");
                    return ListScenes();

                case "GetHierarchy":
                    Debug.Log("[Bridge] ExecuteCommand: GetHierarchy case");
                    return GetHierarchy(paramsJson);

                case "DeleteGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: DeleteGameObject case");
                    return DeleteGameObject(paramsJson);

                case "GetProjectInfo":
                    Debug.Log("[Bridge] ExecuteCommand: GetProjectInfo case");
                    return GetProjectInfo();

                case "CreateScript":
                    Debug.Log("[Bridge] ExecuteCommand: CreateScript case");
                    return CreateScript(paramsJson);

                case "SetComponentProperty":
                    Debug.Log("[Bridge] ExecuteCommand: SetComponentProperty case");
                    return SetComponentProperty(paramsJson);

                case "CreatePrefab":
                    Debug.Log("[Bridge] ExecuteCommand: CreatePrefab case");
                    return CreatePrefab(paramsJson);

                case "SetMaterial":
                    Debug.Log("[Bridge] ExecuteCommand: SetMaterial case");
                    return SetMaterial(paramsJson);

                case "GetComponentProperty":
                    Debug.Log("[Bridge] ExecuteCommand: GetComponentProperty case");
                    return GetComponentProperty(paramsJson);

                case "FindAssets":
                    Debug.Log("[Bridge] ExecuteCommand: FindAssets case");
                    return FindAssets(paramsJson);

                case "BatchCreateGameObjects":
                    Debug.Log("[Bridge] ExecuteCommand: BatchCreateGameObjects case");
                    return BatchCreateGameObjects(paramsJson);

                // Category A Commands
                case "SetActive":
                    Debug.Log("[Bridge] ExecuteCommand: SetActive case");
                    return SetActive(paramsJson);

                case "GetActiveState":
                    Debug.Log("[Bridge] ExecuteCommand: GetActiveState case");
                    return GetActiveState(paramsJson);

                case "RenameGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: RenameGameObject case");
                    return RenameGameObject(paramsJson);

                case "DeleteAsset":
                    Debug.Log("[Bridge] ExecuteCommand: DeleteAsset case");
                    return DeleteAsset(paramsJson);

                case "SelectGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: SelectGameObject case");
                    return SelectGameObject(paramsJson);

                case "FindGameObjectsByTag":
                    Debug.Log("[Bridge] ExecuteCommand: FindGameObjectsByTag case");
                    return FindGameObjectsByTag(paramsJson);

                case "LogMessage":
                    Debug.Log("[Bridge] ExecuteCommand: LogMessage case");
                    return LogMessage(paramsJson);

                case "GetActiveScene":
                    Debug.Log("[Bridge] ExecuteCommand: GetActiveScene case");
                    return GetActiveScene(paramsJson);

                case "EnterPlayMode":
                    Debug.Log("[Bridge] ExecuteCommand: EnterPlayMode case");
                    return EnterPlayMode(paramsJson);

                case "ExitPlayMode":
                    Debug.Log("[Bridge] ExecuteCommand: ExitPlayMode case");
                    return ExitPlayMode(paramsJson);

                // Category B Commands
                case "DuplicateGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: DuplicateGameObject case");
                    return DuplicateGameObject(paramsJson);

                case "SetParent":
                    Debug.Log("[Bridge] ExecuteCommand: SetParent case");
                    return SetParent(paramsJson);

                case "GetParent":
                    Debug.Log("[Bridge] ExecuteCommand: GetParent case");
                    return GetParent(paramsJson);

                case "GetChildren":
                    Debug.Log("[Bridge] ExecuteCommand: GetChildren case");
                    return GetChildren(paramsJson);

                case "SetLayer":
                    Debug.Log("[Bridge] ExecuteCommand: SetLayer case");
                    return SetLayer(paramsJson);

                case "SetTag":
                    Debug.Log("[Bridge] ExecuteCommand: SetTag case");
                    return SetTag(paramsJson);

                case "RemoveComponent":
                    Debug.Log("[Bridge] ExecuteCommand: RemoveComponent case");
                    return RemoveComponent(paramsJson);

                case "HasComponent":
                    Debug.Log("[Bridge] ExecuteCommand: HasComponent case");
                    return HasComponent(paramsJson);

                // Category C Commands
                case "CreateFolder":
                    Debug.Log("[Bridge] ExecuteCommand: CreateFolder case");
                    return CreateFolder(paramsJson);

                case "RefreshAssetDatabase":
                    Debug.Log("[Bridge] ExecuteCommand: RefreshAssetDatabase case");
                    return RefreshAssetDatabase(paramsJson);

                case "GetAllScenes":
                    Debug.Log("[Bridge] ExecuteCommand: GetAllScenes case");
                    return GetAllScenes(paramsJson);

                case "GetAllComponents":
                    Debug.Log("[Bridge] ExecuteCommand: GetAllComponents case");
                    return GetAllComponents(paramsJson);

                case "FocusGameObject":
                    Debug.Log("[Bridge] ExecuteCommand: FocusGameObject case");
                    return FocusGameObject(paramsJson);

                case "ClearConsole":
                    Debug.Log("[Bridge] ExecuteCommand: ClearConsole case");
                    return ClearConsole(paramsJson);

                case "GetConsoleErrors":
                    Debug.Log("[Bridge] ExecuteCommand: GetConsoleErrors case");
                    return GetConsoleLogs(paramsJson, "error");

                case "GetConsoleWarnings":
                    Debug.Log("[Bridge] ExecuteCommand: GetConsoleWarnings case");
                    return GetConsoleLogs(paramsJson, "warning");

                case "GetConsoleLogs":
                    Debug.Log("[Bridge] ExecuteCommand: GetConsoleLogs case");
                    return GetConsoleLogs(paramsJson, "all");

                case "FindGameObjectsByLayer":
                    Debug.Log("[Bridge] ExecuteCommand: FindGameObjectsByLayer case");
                    return FindGameObjectsByLayer(paramsJson);

                // UI Commands
                case "CreateCanvas":
                    Debug.Log("[Bridge] ExecuteCommand: CreateCanvas case");
                    return CreateCanvas(paramsJson);

                case "CreateUIText":
                    Debug.Log("[Bridge] ExecuteCommand: CreateUIText case");
                    return CreateUIText(paramsJson);

                case "CreateUIButton":
                    Debug.Log("[Bridge] ExecuteCommand: CreateUIButton case");
                    return CreateUIButton(paramsJson);

                case "CreateUIImage":
                    Debug.Log("[Bridge] ExecuteCommand: CreateUIImage case");
                    return CreateUIImage(paramsJson);

                case "CreateUIPanel":
                    Debug.Log("[Bridge] ExecuteCommand: CreateUIPanel case");
                    return CreateUIPanel(paramsJson);

                case "SetUIText":
                    Debug.Log("[Bridge] ExecuteCommand: SetUIText case");
                    return SetUIText(paramsJson);

                // RectTransform Commands
                case "SetRectTransform":
                    Debug.Log("[Bridge] ExecuteCommand: SetRectTransform case");
                    return SetRectTransform(paramsJson);

                case "SetAnchor":
                    Debug.Log("[Bridge] ExecuteCommand: SetAnchor case");
                    return SetAnchor(paramsJson);

                case "SetPivot":
                    Debug.Log("[Bridge] ExecuteCommand: SetPivot case");
                    return SetPivot(paramsJson);

                case "GetRectTransform":
                    Debug.Log("[Bridge] ExecuteCommand: GetRectTransform case");
                    return GetRectTransform(paramsJson);

                // Material/Color Commands
                case "SetColor":
                    Debug.Log("[Bridge] ExecuteCommand: SetColor case");
                    return SetColor(paramsJson);

                case "SetTextureToMaterial":
                    Debug.Log("[Bridge] ExecuteCommand: SetTextureToMaterial case");
                    return SetTextureToMaterial(paramsJson);

                // InputField Command
                case "CreateUIInputField":
                    Debug.Log("[Bridge] ExecuteCommand: CreateUIInputField case");
                    return CreateUIInputField(paramsJson);

                // Particle System Commands
                case "CreateParticleSystem":
                    Debug.Log("[Bridge] ExecuteCommand: CreateParticleSystem case");
                    return CreateParticleSystem(paramsJson);

                case "SetParticleProperty":
                    Debug.Log("[Bridge] ExecuteCommand: SetParticleProperty case");
                    return SetParticleProperty(paramsJson);

                // Animator Commands
                case "CreateAnimatorController":
                    Debug.Log("[Bridge] ExecuteCommand: CreateAnimatorController case");
                    return CreateAnimatorController(paramsJson);

                case "AddAnimatorState":
                    Debug.Log("[Bridge] ExecuteCommand: AddAnimatorState case");
                    return AddAnimatorState(paramsJson);

                case "AddAnimatorTransition":
                    Debug.Log("[Bridge] ExecuteCommand: AddAnimatorTransition case");
                    return AddAnimatorTransition(paramsJson);

                case "AddAnimatorParameter":
                    Debug.Log("[Bridge] ExecuteCommand: AddAnimatorParameter case");
                    return AddAnimatorParameter(paramsJson);

                case "SetAnimatorController":
                    Debug.Log("[Bridge] ExecuteCommand: SetAnimatorController case");
                    return SetAnimatorController(paramsJson);

                case "GetAnimatorParameters":
                    Debug.Log("[Bridge] ExecuteCommand: GetAnimatorParameters case");
                    return GetAnimatorParameters(paramsJson);

                case "GetAnimatorStates":
                    Debug.Log("[Bridge] ExecuteCommand: GetAnimatorStates case");
                    return GetAnimatorStates(paramsJson);

                case "AddBlendTree":
                    Debug.Log("[Bridge] ExecuteCommand: AddBlendTree case");
                    return AddBlendTree(paramsJson);

                case "SetStateMotion":
                    Debug.Log("[Bridge] ExecuteCommand: SetStateMotion case");
                    return SetStateMotion(paramsJson);

                case "AddAnimatorLayer":
                    Debug.Log("[Bridge] ExecuteCommand: AddAnimatorLayer case");
                    return AddAnimatorLayer(paramsJson);

                case "SetTransitionConditions":
                    Debug.Log("[Bridge] ExecuteCommand: SetTransitionConditions case");
                    return SetTransitionConditions(paramsJson);

                case "CreateSubStateMachine":
                    Debug.Log("[Bridge] ExecuteCommand: CreateSubStateMachine case");
                    return CreateSubStateMachine(paramsJson);

                case "GetCurrentAnimatorState":
                    Debug.Log("[Bridge] ExecuteCommand: GetCurrentAnimatorState case");
                    return GetCurrentAnimatorState(paramsJson);

                case "PlayAnimatorState":
                    Debug.Log("[Bridge] ExecuteCommand: PlayAnimatorState case");
                    return PlayAnimatorState(paramsJson);

                case "SetAnimatorSpeed":
                    Debug.Log("[Bridge] ExecuteCommand: SetAnimatorSpeed case");
                    return SetAnimatorSpeed(paramsJson);

                case "GetAnimatorClipInfo":
                    Debug.Log("[Bridge] ExecuteCommand: GetAnimatorClipInfo case");
                    return GetAnimatorClipInfo(paramsJson);

                case "CrossfadeAnimator":
                    Debug.Log("[Bridge] ExecuteCommand: CrossfadeAnimator case");
                    return CrossfadeAnimator(paramsJson);

                case "SetAvatar":
                    Debug.Log("[Bridge] ExecuteCommand: SetAvatar case");
                    return SetAvatar(paramsJson);

                case "SetRootMotion":
                    Debug.Log("[Bridge] ExecuteCommand: SetRootMotion case");
                    return SetRootMotion(paramsJson);

                // HIGH-LEVEL GAME GENERATION COMMANDS (Lazy-Bird)
                case "GenerateGame":
                    Debug.Log("[Bridge] ExecuteCommand: GenerateGame case");
                    return GenerateGame(paramsJson);

                case "SetupGameManager":
                    Debug.Log("[Bridge] ExecuteCommand: SetupGameManager case");
                    return SetupGameManager(paramsJson);

                case "SetupPlayer":
                    Debug.Log("[Bridge] ExecuteCommand: SetupPlayer case");
                    return SetupPlayer(paramsJson);

                case "SetupGameUI":
                    Debug.Log("[Bridge] ExecuteCommand: SetupGameUI case");
                    return SetupGameUI(paramsJson);

                case "WireSystems":
                    Debug.Log("[Bridge] ExecuteCommand: WireSystems case");
                    return WireSystems(paramsJson);

                case "ImportVaultSystem":
                    Debug.Log("[Bridge] ExecuteCommand: ImportVaultSystem case");
                    return ImportVaultSystem(paramsJson);

                case "CreateEnemy":
                    Debug.Log("[Bridge] ExecuteCommand: CreateEnemy case");
                    return CreateEnemy(paramsJson);

                case "SetupSceneStructure":
                    Debug.Log("[Bridge] ExecuteCommand: SetupSceneStructure case");
                    return SetupSceneStructure(paramsJson);

                default:
                    throw new Exception($"Unknown command: {command}");
            }
        }

        // Command Implementations

        private object CreateGameObject(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson))
                throw new Exception("CreateGameObject params cannot be null or empty");

            var p = JsonUtility.FromJson<CreateGameObjectParams>(paramsJson);

            if (p == null || string.IsNullOrEmpty(p.name))
                throw new Exception("Invalid CreateGameObject parameters");

            GameObject go;

            switch (p.primitiveType)
            {
                case "Cube":
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case "Sphere":
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case "Capsule":
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    break;
                case "Cylinder":
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    break;
                case "Plane":
                    go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    break;
                case "Quad":
                    go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    break;
                default:
                    go = new GameObject();
                    break;
            }

            go.name = p.name;

            if (!string.IsNullOrEmpty(p.parent))
            {
                GameObject parentObj = GameObject.Find(p.parent);
                if (parentObj != null)
                {
                    go.transform.SetParent(parentObj.transform);
                }
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {p.name}");
            Selection.activeGameObject = go;

            return new GameObjectResult { success = true, name = p.name, instanceId = go.GetInstanceID() };
        }

        private object SetTransform(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetTransformParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            Undo.RecordObject(go.transform, "Set Transform");

            if (p.position != null)
            {
                go.transform.position = new Vector3(p.position.x, p.position.y, p.position.z);
            }

            if (p.rotation != null)
            {
                go.transform.eulerAngles = new Vector3(p.rotation.x, p.rotation.y, p.rotation.z);
            }

            if (p.scale != null)
            {
                go.transform.localScale = new Vector3(p.scale.x, p.scale.y, p.scale.z);
            }

            return new SimpleResult { success = true };
        }

        private object AddComponent(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddComponentParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            // Try UnityEngine namespace first
            Type componentType = Type.GetType($"UnityEngine.{p.componentType}, UnityEngine");

            // Try direct type name
            if (componentType == null)
            {
                componentType = Type.GetType(p.componentType);
            }

            // Search through all loaded assemblies
            if (componentType == null)
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == p.componentType && typeof(Component).IsAssignableFrom(t));

                    if (componentType != null)
                        break;
                }
            }

            if (componentType == null)
            {
                throw new Exception($"Component type not found: {p.componentType}. Make sure the script is compiled and inherits from MonoBehaviour or Component.");
            }

            Component component = Undo.AddComponent(go, componentType);

            return new ComponentResult { success = true, componentType = componentType.Name };
        }

        private object CreateScene(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateSceneParams>(paramsJson);

            var mode = p.additive ? UnityEditor.SceneManagement.NewSceneMode.Additive :
                                    UnityEditor.SceneManagement.NewSceneMode.Single;

            var scene = EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects, mode);

            return new SceneResult { success = true, sceneName = scene.name };
        }

        private object SaveScene(string paramsJson)
        {
            var p = JsonUtility.FromJson<SaveSceneParams>(paramsJson);

            string path = p.path;
            if (string.IsNullOrEmpty(path))
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    throw new Exception("Scene has no path. Please provide a save path.");
                }
                path = activeScene.path;
            }

            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/Scenes/" + path;
            }

            if (!path.EndsWith(".unity"))
            {
                path += ".unity";
            }

            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);

            return new PathResult { success = true, path = path };
        }

        private object ListScenes()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            List<string> scenes = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                scenes.Add(path);
            }

            return new { scenes = scenes.ToArray() };
        }

        private object GetHierarchy(string paramsJson)
        {
            try
            {
                Debug.Log($"[Bridge] GetHierarchy START - paramsJson: {paramsJson}");
                var p = JsonUtility.FromJson<GetHierarchyParams>(paramsJson);
                Debug.Log($"[Bridge] GetHierarchy params parsed - rootOnly: {p.rootOnly}");

                List<string> hierarchy = new List<string>();
                GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

                Debug.Log($"[Bridge] GetHierarchy: Found {rootObjects.Length} root objects");

                foreach (GameObject root in rootObjects)
                {
                    if (p.rootOnly)
                    {
                        hierarchy.Add(root.name);
                    }
                    else
                    {
                        AddHierarchyRecursive(root.transform, "", hierarchy);
                    }
                }

                Debug.Log($"[Bridge] GetHierarchy: Total hierarchy items: {hierarchy.Count}");
                var result = new HierarchyResult { hierarchy = hierarchy.ToArray() };
                Debug.Log($"[Bridge] GetHierarchy: Result array length: {result.hierarchy.Length}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bridge] GetHierarchy ERROR: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void AddHierarchyRecursive(Transform transform, string indent, List<string> list)
        {
            list.Add($"{indent}{transform.name}");
            foreach (Transform child in transform)
            {
                AddHierarchyRecursive(child, indent + "  ", list);
            }
        }

        private object DeleteGameObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<DeleteGameObjectParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            Undo.DestroyObjectImmediate(go);

            return new SimpleResult { success = true };
        }

        private object GetProjectInfo()
        {
            var info = new ProjectInfo
            {
                unityVersion = Application.unityVersion,
                projectName = Application.productName,
                projectPath = Application.dataPath,
                platform = Application.platform.ToString(),
                companyName = Application.companyName
            };
            return info;
        }

        private object CreateScript(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateScriptParams>(paramsJson);

            string path = p.path;
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/Scripts/";
            }

            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }

            if (!path.EndsWith("/"))
            {
                path += "/";
            }

            string fullPath = path + p.scriptName + ".cs";

            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, p.scriptContent);
            AssetDatabase.Refresh();

            return new PathResult { success = true, path = fullPath };
        }

        private object SetComponentProperty(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetComponentPropertyParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            // Find component by type name
            Component component = null;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp.GetType().Name == p.componentType)
                {
                    component = comp;
                    break;
                }
            }

            if (component == null)
            {
                throw new Exception($"Component '{p.componentType}' not found on GameObject '{p.gameObjectName}'");
            }

            // Get the field or property
            var type = component.GetType();
            var field = type.GetField(p.propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var property = type.GetProperty(p.propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null && property == null)
            {
                throw new Exception($"Property/Field '{p.propertyName}' not found on component '{p.componentType}'");
            }

            Undo.RecordObject(component, $"Set {p.propertyName}");

            // Convert value based on target type
            Type targetType = field != null ? field.FieldType : property.PropertyType;
            object convertedValue = ConvertValue(p.value, p.valueType, targetType);

            // Set the value
            if (field != null)
            {
                field.SetValue(component, convertedValue);
            }
            else
            {
                property.SetValue(component, convertedValue);
            }

            EditorUtility.SetDirty(component);

            return new SimpleResult { success = true };
        }

        private object ConvertValue(string value, string valueType, Type targetType)
        {
            // Handle GameObject/UnityEngine.Object references
            if (targetType == typeof(GameObject) || typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                if (string.IsNullOrEmpty(value))
                    return null;

                // Try to find GameObject first
                GameObject go = GameObject.Find(value);
                if (go != null)
                {
                    if (targetType == typeof(GameObject))
                        return go;

                    // Try to get component of target type
                    var comp = go.GetComponent(targetType);
                    if (comp != null)
                        return comp;
                }

                // Try to load asset
                var asset = AssetDatabase.LoadAssetAtPath(value, targetType);
                if (asset != null)
                    return asset;

                // Search for prefab by name
                string[] guids = AssetDatabase.FindAssets($"{value} t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                    if (asset != null)
                        return asset;
                }

                return null;
            }

            // Handle primitive types
            switch (valueType.ToLower())
            {
                case "int":
                    return int.Parse(value);
                case "float":
                    return float.Parse(value);
                case "bool":
                    return bool.Parse(value);
                case "string":
                    return value;
                default:
                    return value;
            }
        }

        private object CreatePrefab(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreatePrefabParams>(paramsJson);

            GameObject go = GameObject.Find(p.sourceGameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.sourceGameObjectName}");
            }

            string fullPath = p.prefabPath.StartsWith("Assets/") ? p.prefabPath : $"Assets/{p.prefabPath}";

            // Ensure directory exists
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, fullPath);

            return new PrefabResult
            {
                success = true,
                prefabPath = fullPath,
                prefabName = prefab.name
            };
        }

        private object SetMaterial(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetMaterialParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                throw new Exception($"No Renderer component found on GameObject: {p.gameObjectName}");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null)
            {
                throw new Exception($"Material not found at path: {p.materialPath}");
            }

            Undo.RecordObject(renderer, "Set Material");
            renderer.material = mat;
            EditorUtility.SetDirty(renderer);

            return new SimpleResult { success = true };
        }

        private object GetComponentProperty(string paramsJson)
        {
            var p = JsonUtility.FromJson<GetComponentPropertyParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null)
            {
                throw new Exception($"GameObject not found: {p.gameObjectName}");
            }

            // Find component by type name
            Component component = null;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp.GetType().Name == p.componentType)
                {
                    component = comp;
                    break;
                }
            }

            if (component == null)
            {
                throw new Exception($"Component '{p.componentType}' not found on GameObject '{p.gameObjectName}'");
            }

            // Get the field or property
            var type = component.GetType();
            var field = type.GetField(p.propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var property = type.GetProperty(p.propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field == null && property == null)
            {
                throw new Exception($"Property/Field '{p.propertyName}' not found on component '{p.componentType}'");
            }

            object value = field != null ? field.GetValue(component) : property.GetValue(component);
            string valueStr = value != null ? value.ToString() : "null";

            return new PropertyResult
            {
                success = true,
                value = valueStr,
                valueType = value != null ? value.GetType().Name : "null"
            };
        }

        private object FindAssets(string paramsJson)
        {
            var p = JsonUtility.FromJson<FindAssetsParams>(paramsJson);

            string[] guids = AssetDatabase.FindAssets(p.searchQuery);
            List<string> paths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                paths.Add(path);
            }

            return new AssetListResult
            {
                success = true,
                assetPaths = paths.ToArray(),
                count = paths.Count
            };
        }

        private object BatchCreateGameObjects(string paramsJson)
        {
            var p = JsonUtility.FromJson<BatchCreateGameObjectsParams>(paramsJson);
            List<string> createdNames = new List<string>();

            foreach (var item in p.gameObjects)
            {
                GameObject go = null;

                // Create based on primitive type
                switch (item.primitiveType)
                {
                    case "Cube":
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        break;
                    case "Sphere":
                        go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        break;
                    case "Capsule":
                        go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        break;
                    case "Cylinder":
                        go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        break;
                    case "Plane":
                        go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                        break;
                    case "Quad":
                        go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        break;
                    default:
                        go = new GameObject();
                        break;
                }

                go.name = item.name;

                // Set parent first (if specified)
                if (!string.IsNullOrEmpty(item.parent))
                {
                    GameObject parent = GameObject.Find(item.parent);
                    if (parent != null)
                    {
                        go.transform.SetParent(parent.transform, false); // worldPositionStays = false
                    }
                }

                // Set transform (use local coordinates since we have a parent)
                if (item.position != null)
                {
                    go.transform.localPosition = new Vector3(item.position.x, item.position.y, item.position.z);
                }
                if (item.rotation != null)
                {
                    go.transform.localEulerAngles = new Vector3(item.rotation.x, item.rotation.y, item.rotation.z);
                }

                // Check if scale was provided (Vector3Data is struct, so check if it has non-zero values)
                bool hasScale = item.scale.x != 0 || item.scale.y != 0 || item.scale.z != 0;
                if (hasScale)
                {
                    go.transform.localScale = new Vector3(item.scale.x, item.scale.y, item.scale.z);
                }
                else
                {
                    // Default scale to (1,1,1) if not specified
                    go.transform.localScale = Vector3.one;
                }

                Undo.RegisterCreatedObjectUndo(go, "Batch Create GameObject");
                createdNames.Add(go.name);
            }

            return new BatchCreateResult
            {
                success = true,
                createdObjects = createdNames.ToArray(),
                count = createdNames.Count
            };
        }

        // Helper Methods

        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            logMessages.Add($"[{timestamp}] {message}");

            if (logMessages.Count > MAX_LOG_MESSAGES)
            {
                logMessages.RemoveAt(0);
            }

            Repaint();
        }

        private void AddLogThreadSafe(string message)
        {
            lock (logLock)
            {
                pendingLogs.Enqueue(message);
            }
            EditorApplication.delayCall += () => Repaint();
        }

        private void ProcessPendingLogs()
        {
            lock (logLock)
            {
                while (pendingLogs.Count > 0)
                {
                    string message = pendingLogs.Dequeue();
                    string timestamp = DateTime.Now.ToString("HH:mm:ss");
                    logMessages.Add($"[{timestamp}] {message}");

                    if (logMessages.Count > MAX_LOG_MESSAGES)
                    {
                        logMessages.RemoveAt(0);
                    }
                }
            }
        }

        // Data Classes

        [Serializable]
        private class CommandRequest
        {
            public string command;
            public string @params;
        }

        [Serializable]
        private class CreateGameObjectParams
        {
            public string name;
            public string primitiveType;
            public string parent;
        }

        [Serializable]
        private class SetTransformParams
        {
            public string gameObjectName;
            public Vector3Data position;
            public Vector3Data rotation;
            public Vector3Data scale;
        }

        [Serializable]
        private class Vector3Data
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private class AddComponentParams
        {
            public string gameObjectName;
            public string componentType;
        }

        [Serializable]
        private class CreateSceneParams
        {
            public string sceneName;
            public bool additive;
        }

        [Serializable]
        private class SaveSceneParams
        {
            public string path;
        }

        [Serializable]
        private class GetHierarchyParams
        {
            public bool rootOnly;
        }

        [Serializable]
        private class DeleteGameObjectParams
        {
            public string gameObjectName;
        }

        // ===== KATEGORI A: KRITIK + KOLAY KOMUTLAR (10) =====

        private object SetActive(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Undo.RecordObject(go, "Set Active");
            go.SetActive(p.value);
            return new { success = true };
        }

        private object GetActiveState(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            return new { success = true, active = go.activeSelf };
        }

        private object RenameGameObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<RenameParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Undo.RecordObject(go, "Rename GameObject");
            go.name = p.newName;
            return new { success = true };
        }

        private object DeleteAsset(string paramsJson)
        {
            var p = JsonUtility.FromJson<AssetPathParams>(paramsJson);
            if (!AssetDatabase.DeleteAsset(p.assetPath))
                throw new Exception($"Failed to delete asset: {p.assetPath}");

            return new { success = true };
        }

        private object SelectGameObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Selection.activeGameObject = go;
            return new { success = true };
        }

        private object FindGameObjectsByTag(string paramsJson)
        {
            var p = JsonUtility.FromJson<TagParams>(paramsJson);
            GameObject[] objects = GameObject.FindGameObjectsWithTag(p.tag);

            string[] names = objects.Select(o => o.name).ToArray();
            return new { success = true, gameObjects = names, count = names.Length };
        }

        private object LogMessage(string paramsJson)
        {
            var p = JsonUtility.FromJson<LogParams>(paramsJson);

            switch (p.type?.ToLower())
            {
                case "warning": Debug.LogWarning(p.message); break;
                case "error": Debug.LogError(p.message); break;
                default: Debug.Log(p.message); break;
            }

            return new { success = true };
        }

        private object GetActiveScene(string paramsJson)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            return new {
                success = true,
                sceneName = scene.name,
                scenePath = scene.path,
                isLoaded = scene.isLoaded
            };
        }

        private object EnterPlayMode(string paramsJson)
        {
            EditorApplication.isPlaying = true;
            return new { success = true };
        }

        private object ExitPlayMode(string paramsJson)
        {
            EditorApplication.isPlaying = false;
            return new { success = true };
        }

        // Category B Implementations (8 commands)
        private object DuplicateGameObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            GameObject duplicate = UnityEngine.Object.Instantiate(go);
            duplicate.name = go.name + " (Clone)";
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate GameObject");

            return new {
                success = true,
                name = duplicate.name,
                position = new { x = duplicate.transform.position.x, y = duplicate.transform.position.y, z = duplicate.transform.position.z }
            };
        }

        private object SetParent(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetParentParams>(paramsJson);
            GameObject child = GameObject.Find(p.gameObjectName);
            if (child == null) throw new Exception($"Child GameObject not found: {p.gameObjectName}");

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parentName))
            {
                GameObject parent = GameObject.Find(p.parentName);
                if (parent == null) throw new Exception($"Parent GameObject not found: {p.parentName}");
                parentTransform = parent.transform;
            }

            Undo.SetTransformParent(child.transform, parentTransform, "Set Parent");
            return new { success = true };
        }

        private object GetParent(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Transform parent = go.transform.parent;
            if (parent == null)
                return new { success = true, hasParent = false };

            return new {
                success = true,
                hasParent = true,
                parentName = parent.gameObject.name,
                parentPath = GetFullPath(parent)
            };
        }

        private object GetChildren(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            var children = new System.Collections.Generic.List<object>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform child = go.transform.GetChild(i);
                children.Add(new {
                    name = child.gameObject.name,
                    active = child.gameObject.activeSelf,
                    childCount = child.childCount
                });
            }

            return new { success = true, childCount = go.transform.childCount, children };
        }

        private object SetLayer(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetLayerParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            int layer = LayerMask.NameToLayer(p.layerName);
            if (layer == -1) throw new Exception($"Layer not found: {p.layerName}");

            Undo.RecordObject(go, "Set Layer");
            go.layer = layer;
            return new { success = true, layer, layerName = LayerMask.LayerToName(layer) };
        }

        private object SetTag(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetTagParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            try
            {
                Undo.RecordObject(go, "Set Tag");
                go.tag = p.tagName;
                return new { success = true, tag = go.tag };
            }
            catch (UnityException ex)
            {
                throw new Exception($"Invalid tag: {p.tagName}. {ex.Message}");
            }
        }

        private object RemoveComponent(string paramsJson)
        {
            var p = JsonUtility.FromJson<ComponentParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Component comp = go.GetComponent(p.componentType);
            if (comp == null) throw new Exception($"Component {p.componentType} not found on {p.gameObjectName}");

            Undo.DestroyObjectImmediate(comp);
            return new { success = true, message = $"Removed {p.componentType} from {p.gameObjectName}" };
        }

        private object HasComponent(string paramsJson)
        {
            var p = JsonUtility.FromJson<ComponentParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Component comp = go.GetComponent(p.componentType);
            return new { success = true, hasComponent = comp != null, componentType = p.componentType };
        }

        // Category C Implementations (7 commands)
        private object CreateFolder(string paramsJson)
        {
            var p = JsonUtility.FromJson<FolderParams>(paramsJson);

            if (AssetDatabase.IsValidFolder(p.folderPath))
            {
                return new { success = true, message = "Folder already exists", path = p.folderPath };
            }

            string parentPath = System.IO.Path.GetDirectoryName(p.folderPath);
            string folderName = System.IO.Path.GetFileName(p.folderPath);

            string guid = AssetDatabase.CreateFolder(parentPath, folderName);
            AssetDatabase.Refresh();

            return new { success = true, message = "Folder created", path = p.folderPath, guid };
        }

        private object RefreshAssetDatabase(string paramsJson)
        {
            AssetDatabase.Refresh();
            return new { success = true, message = "AssetDatabase refreshed" };
        }

        private object GetAllScenes(string paramsJson)
        {
            var scenes = new System.Collections.Generic.List<object>();
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;

            for (int i = 0; i < sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                scenes.Add(new {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = scene.rootCount
                });
            }

            return new { success = true, sceneCount, scenes };
        }

        private object GetAllComponents(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Component[] components = go.GetComponents<Component>();
            var componentList = new System.Collections.Generic.List<object>();

            foreach (Component comp in components)
            {
                if (comp != null)
                {
                    componentList.Add(new {
                        type = comp.GetType().Name,
                        fullType = comp.GetType().FullName
                    });
                }
            }

            return new { success = true, componentCount = componentList.Count, components = componentList };
        }

        private object FocusGameObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);
            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();

            return new { success = true, message = $"Focused on {p.gameObjectName}" };
        }

        private object ClearConsole(string paramsJson)
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(new object(), null);

            return new { success = true, message = "Console cleared" };
        }

        [System.Serializable]
        private class ConsoleLogParams
        {
            public int limit = 50;
            public string type = "all";
        }

        private object GetConsoleLogs(string paramsJson, string filterType)
        {
            var p = new ConsoleLogParams();
            if (!string.IsNullOrEmpty(paramsJson))
            {
                try { p = JsonUtility.FromJson<ConsoleLogParams>(paramsJson); } catch { }
            }

            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
            var logEntryType = assembly.GetType("UnityEditor.LogEntry");

            // Get count
            var getCountMethod = logEntriesType.GetMethod("GetCount");
            int count = (int)getCountMethod.Invoke(null, null);

            // Start getting entries
            var startMethod = logEntriesType.GetMethod("StartGettingEntries");
            startMethod.Invoke(null, null);

            var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal");
            var logEntry = System.Activator.CreateInstance(logEntryType);

            var logs = new System.Collections.Generic.List<object>();
            int limit = Mathf.Min(p.limit, count);

            for (int i = 0; i < count && logs.Count < limit; i++)
            {
                getEntryMethod.Invoke(null, new object[] { i, logEntry });

                var mode = (int)logEntryType.GetField("mode").GetValue(logEntry);
                var message = (string)logEntryType.GetField("message").GetValue(logEntry);
                var file = (string)logEntryType.GetField("file").GetValue(logEntry);
                var line = (int)logEntryType.GetField("line").GetValue(logEntry);

                // mode: 1=error, 2=assert, 4=warning, 8=log, 16=exception
                string logType = "log";
                if ((mode & 1) != 0 || (mode & 16) != 0 || (mode & 2) != 0) logType = "error";
                else if ((mode & 4) != 0) logType = "warning";

                // Filter by type
                if (filterType == "error" && logType != "error") continue;
                if (filterType == "warning" && logType != "warning") continue;

                logs.Add(new {
                    type = logType,
                    message = message.Length > 500 ? message.Substring(0, 500) + "..." : message,
                    file = file,
                    line = line
                });
            }

            var endMethod = logEntriesType.GetMethod("EndGettingEntries");
            endMethod.Invoke(null, null);

            return new {
                success = true,
                total = count,
                returned = logs.Count,
                filter = filterType,
                logs = logs
            };
        }

        private object FindGameObjectsByLayer(string paramsJson)
        {
            var p = JsonUtility.FromJson<LayerParams>(paramsJson);
            int layer = LayerMask.NameToLayer(p.layerName);
            if (layer == -1) throw new Exception($"Layer not found: {p.layerName}");

            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            var matchingObjects = new System.Collections.Generic.List<object>();

            foreach (GameObject go in allObjects)
            {
                if (go.layer == layer)
                {
                    matchingObjects.Add(new {
                        name = go.name,
                        path = GetFullPath(go.transform),
                        active = go.activeSelf
                    });
                }
            }

            return new {
                success = true,
                layer = layer,
                layerName = p.layerName,
                count = matchingObjects.Count,
                gameObjects = matchingObjects
            };
        }

        // UI Commands (5 commands)
        private object CreateCanvas(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateCanvasParams>(paramsJson);

            // Create Canvas GameObject
            GameObject canvasGO = new GameObject(p.canvasName);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = p.renderMode == "ScreenSpaceOverlay" ? RenderMode.ScreenSpaceOverlay :
                               p.renderMode == "ScreenSpaceCamera" ? RenderMode.ScreenSpaceCamera :
                               RenderMode.WorldSpace;

            // Add CanvasScaler
            UnityEngine.UI.CanvasScaler scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Add GraphicRaycaster
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create EventSystem if it doesn't exist
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

            return new {
                success = true,
                canvasName = canvasGO.name,
                renderMode = canvas.renderMode.ToString()
            };
        }

        private object CreateUIText(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateUITextParams>(paramsJson);

            // Find parent (Canvas or other UI element)
            GameObject parent = string.IsNullOrEmpty(p.parentName) ? null : GameObject.Find(p.parentName);

            // Create Text GameObject
            GameObject textGO = new GameObject(p.textName);
            RectTransform rectTransform = textGO.AddComponent<RectTransform>();
            UnityEngine.UI.Text textComponent = textGO.AddComponent<UnityEngine.UI.Text>();

            // Set text properties
            textComponent.text = p.text;
            textComponent.fontSize = p.fontSize > 0 ? p.fontSize : 14;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = UnityEngine.Color.black;

            // Set RectTransform
            rectTransform.sizeDelta = new Vector2(160, 30);

            // Set parent
            if (parent != null)
            {
                textGO.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(textGO, "Create UI Text");

            return new {
                success = true,
                textName = textGO.name,
                text = textComponent.text,
                fontSize = textComponent.fontSize
            };
        }

        private object CreateUIButton(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateUIButtonParams>(paramsJson);

            // Find parent
            GameObject parent = string.IsNullOrEmpty(p.parentName) ? null : GameObject.Find(p.parentName);

            // Create Button GameObject
            GameObject buttonGO = new GameObject(p.buttonName);
            RectTransform rectTransform = buttonGO.AddComponent<RectTransform>();
            UnityEngine.UI.Image image = buttonGO.AddComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Button button = buttonGO.AddComponent<UnityEngine.UI.Button>();

            // Set RectTransform
            rectTransform.sizeDelta = new Vector2(160, 30);

            // Create Text child
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            UnityEngine.UI.Text text = textGO.AddComponent<UnityEngine.UI.Text>();

            text.text = p.buttonText;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = UnityEngine.Color.black;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Set parent
            if (parent != null)
            {
                buttonGO.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(buttonGO, "Create UI Button");

            return new {
                success = true,
                buttonName = buttonGO.name,
                buttonText = text.text
            };
        }

        private object CreateUIImage(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateUIImageParams>(paramsJson);

            // Find parent
            GameObject parent = string.IsNullOrEmpty(p.parentName) ? null : GameObject.Find(p.parentName);

            // Create Image GameObject
            GameObject imageGO = new GameObject(p.imageName);
            RectTransform rectTransform = imageGO.AddComponent<RectTransform>();
            UnityEngine.UI.Image image = imageGO.AddComponent<UnityEngine.UI.Image>();

            // Set RectTransform
            rectTransform.sizeDelta = new Vector2(100, 100);

            // Set parent
            if (parent != null)
            {
                imageGO.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(imageGO, "Create UI Image");

            return new {
                success = true,
                imageName = imageGO.name,
                width = rectTransform.sizeDelta.x,
                height = rectTransform.sizeDelta.y
            };
        }

        private object CreateUIPanel(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateUIPanelParams>(paramsJson);

            // Find parent
            GameObject parent = string.IsNullOrEmpty(p.parentName) ? null : GameObject.Find(p.parentName);

            // Create Panel GameObject
            GameObject panelGO = new GameObject(p.panelName);
            RectTransform rectTransform = panelGO.AddComponent<RectTransform>();
            UnityEngine.UI.Image image = panelGO.AddComponent<UnityEngine.UI.Image>();

            // Set default panel color (semi-transparent white)
            image.color = new UnityEngine.Color(1f, 1f, 1f, 0.392f);

            // Set RectTransform - default to stretch
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            // Set parent
            if (parent != null)
            {
                panelGO.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(panelGO, "Create UI Panel");

            return new {
                success = true,
                panelName = panelGO.name
            };
        }

        private object SetUIText(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetUITextParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            UnityEngine.UI.Text text = go.GetComponent<UnityEngine.UI.Text>();
            if (text == null) throw new Exception($"Text component not found on {p.gameObjectName}");

            Undo.RecordObject(text, "Set UI Text");
            text.text = p.text;

            if (p.fontSize > 0)
            {
                text.fontSize = p.fontSize;
            }

            return new {
                success = true,
                text = text.text,
                fontSize = text.fontSize
            };
        }

        // RectTransform Commands (4 commands)
        private object SetRectTransform(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetRectTransformParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) throw new Exception($"RectTransform not found on {p.gameObjectName}");

            Undo.RecordObject(rect, "Set RectTransform");

            // Set anchored position
            if (p.anchoredX != 0 || p.anchoredY != 0)
            {
                rect.anchoredPosition = new Vector2(p.anchoredX, p.anchoredY);
            }

            // Set size delta (width/height)
            if (p.width > 0 || p.height > 0)
            {
                rect.sizeDelta = new Vector2(p.width > 0 ? p.width : rect.sizeDelta.x,
                                              p.height > 0 ? p.height : rect.sizeDelta.y);
            }

            return new {
                success = true,
                anchoredPosition = new { x = rect.anchoredPosition.x, y = rect.anchoredPosition.y },
                sizeDelta = new { width = rect.sizeDelta.x, height = rect.sizeDelta.y }
            };
        }

        private object SetAnchor(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetAnchorParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) throw new Exception($"RectTransform not found on {p.gameObjectName}");

            Undo.RecordObject(rect, "Set Anchor");

            // Set anchor presets or custom anchors
            if (!string.IsNullOrEmpty(p.anchorPreset))
            {
                SetAnchorPreset(rect, p.anchorPreset);
            }
            else
            {
                // Custom anchor values
                rect.anchorMin = new Vector2(p.anchorMinX, p.anchorMinY);
                rect.anchorMax = new Vector2(p.anchorMaxX, p.anchorMaxY);
            }

            return new {
                success = true,
                anchorMin = new { x = rect.anchorMin.x, y = rect.anchorMin.y },
                anchorMax = new { x = rect.anchorMax.x, y = rect.anchorMax.y }
            };
        }

        private object SetPivot(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetPivotParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) throw new Exception($"RectTransform not found on {p.gameObjectName}");

            Undo.RecordObject(rect, "Set Pivot");
            rect.pivot = new Vector2(p.pivotX, p.pivotY);

            return new {
                success = true,
                pivot = new { x = rect.pivot.x, y = rect.pivot.y }
            };
        }

        private object GetRectTransform(string paramsJson)
        {
            var p = JsonUtility.FromJson<GameObjectNameOnlyParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) throw new Exception($"RectTransform not found on {p.gameObjectName}");

            return new {
                success = true,
                anchoredPosition = new { x = rect.anchoredPosition.x, y = rect.anchoredPosition.y },
                sizeDelta = new { width = rect.sizeDelta.x, height = rect.sizeDelta.y },
                anchorMin = new { x = rect.anchorMin.x, y = rect.anchorMin.y },
                anchorMax = new { x = rect.anchorMax.x, y = rect.anchorMax.y },
                pivot = new { x = rect.pivot.x, y = rect.pivot.y }
            };
        }

        // Helper method for anchor presets
        private void SetAnchorPreset(RectTransform rect, string preset)
        {
            switch (preset.ToLower())
            {
                case "topleft":
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    break;
                case "topcenter":
                    rect.anchorMin = new Vector2(0.5f, 1);
                    rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    break;
                case "topright":
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    break;
                case "middleleft":
                    rect.anchorMin = new Vector2(0, 0.5f);
                    rect.anchorMax = new Vector2(0, 0.5f);
                    rect.pivot = new Vector2(0, 0.5f);
                    break;
                case "middlecenter":
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "middleright":
                    rect.anchorMin = new Vector2(1, 0.5f);
                    rect.anchorMax = new Vector2(1, 0.5f);
                    rect.pivot = new Vector2(1, 0.5f);
                    break;
                case "bottomleft":
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 0);
                    break;
                case "bottomcenter":
                    rect.anchorMin = new Vector2(0.5f, 0);
                    rect.anchorMax = new Vector2(0.5f, 0);
                    rect.pivot = new Vector2(0.5f, 0);
                    break;
                case "bottomright":
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(1, 0);
                    break;
                case "stretchleft":
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 0.5f);
                    break;
                case "stretchcenter":
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "stretchright":
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 0.5f);
                    break;
                default:
                    throw new Exception($"Unknown anchor preset: {preset}");
            }
        }

        // Material/Color Commands (2 commands)
        private object SetColor(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetColorParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) throw new Exception($"Renderer not found on {p.gameObjectName}");

            Material mat = renderer.material;
            if (mat == null) throw new Exception($"Material not found on {p.gameObjectName}");

            Undo.RecordObject(renderer, "Set Color");

            UnityEngine.Color color = new UnityEngine.Color(p.r, p.g, p.b, p.a);

            // Set color to the specified property or default _Color
            string propertyName = string.IsNullOrEmpty(p.propertyName) ? "_Color" : p.propertyName;
            mat.SetColor(propertyName, color);

            return new {
                success = true,
                color = new { r = color.r, g = color.g, b = color.b, a = color.a },
                propertyName = propertyName
            };
        }

        private object SetTextureToMaterial(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetTextureParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) throw new Exception($"Renderer not found on {p.gameObjectName}");

            Material mat = renderer.material;
            if (mat == null) throw new Exception($"Material not found on {p.gameObjectName}");

            // Load texture from Assets
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.texturePath);
            if (texture == null) throw new Exception($"Texture not found at path: {p.texturePath}");

            Undo.RecordObject(renderer, "Set Texture");

            string propertyName = string.IsNullOrEmpty(p.propertyName) ? "_MainTex" : p.propertyName;
            mat.SetTexture(propertyName, texture);

            return new {
                success = true,
                texturePath = p.texturePath,
                propertyName = propertyName,
                textureName = texture.name
            };
        }

        // UI InputField Command (1 command)
        private object CreateUIInputField(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateUIInputFieldParams>(paramsJson);

            GameObject parent = string.IsNullOrEmpty(p.parentName) ? null : GameObject.Find(p.parentName);

            // Create InputField GameObject
            GameObject inputFieldGO = new GameObject(p.inputFieldName);
            RectTransform rectTransform = inputFieldGO.AddComponent<RectTransform>();
            UnityEngine.UI.Image image = inputFieldGO.AddComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.InputField inputField = inputFieldGO.AddComponent<UnityEngine.UI.InputField>();

            rectTransform.sizeDelta = new Vector2(160, 30);
            image.color = UnityEngine.Color.white;

            // Create Placeholder
            GameObject placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(inputFieldGO.transform, false);
            RectTransform placeholderRect = placeholderGO.AddComponent<RectTransform>();
            UnityEngine.UI.Text placeholderText = placeholderGO.AddComponent<UnityEngine.UI.Text>();

            placeholderText.text = p.placeholder;
            placeholderText.fontSize = 14;
            placeholderText.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAnchor.MiddleLeft;

            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10, 6);
            placeholderRect.offsetMax = new Vector2(-10, -7);

            // Create Text (actual input text)
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(inputFieldGO.transform, false);
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            UnityEngine.UI.Text text = textGO.AddComponent<UnityEngine.UI.Text>();

            text.text = "";
            text.fontSize = 14;
            text.color = UnityEngine.Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 6);
            textRect.offsetMax = new Vector2(-10, -7);

            // Assign to InputField
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;

            if (parent != null)
            {
                inputFieldGO.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(inputFieldGO, "Create UI InputField");

            return new {
                success = true,
                inputFieldName = inputFieldGO.name,
                placeholder = p.placeholder
            };
        }

        // Particle System Commands (2 commands)
        private object CreateParticleSystem(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParticleSystemParams>(paramsJson);

            GameObject particleGO = new GameObject(p.particleName);
            ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();

            // Set position if provided
            if (p.posX != 0 || p.posY != 0 || p.posZ != 0)
            {
                particleGO.transform.position = new Vector3(p.posX, p.posY, p.posZ);
            }

            // Configure main module
            var main = ps.main;
            main.startLifetime = 5.0f;
            main.startSpeed = 5.0f;
            main.startSize = 1.0f;
            main.startColor = UnityEngine.Color.white;

            // Configure emission
            var emission = ps.emission;
            emission.rateOverTime = 10;

            Undo.RegisterCreatedObjectUndo(particleGO, "Create Particle System");

            return new {
                success = true,
                particleName = particleGO.name,
                position = new { x = particleGO.transform.position.x, y = particleGO.transform.position.y, z = particleGO.transform.position.z }
            };
        }

        private object SetParticleProperty(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetParticlePropertyParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            if (ps == null) throw new Exception($"ParticleSystem not found on {p.gameObjectName}");

            Undo.RecordObject(ps, "Set Particle Property");

            var main = ps.main;

            if (p.startLifetime > 0) main.startLifetime = p.startLifetime;
            if (p.startSpeed > 0) main.startSpeed = p.startSpeed;
            if (p.startSize > 0) main.startSize = p.startSize;
            if (p.emissionRate > 0)
            {
                var emission = ps.emission;
                emission.rateOverTime = p.emissionRate;
            }

            // Set color if provided
            if (p.r >= 0 && p.g >= 0 && p.b >= 0)
            {
                main.startColor = new UnityEngine.Color(p.r, p.g, p.b, p.a >= 0 ? p.a : 1.0f);
            }

            return new {
                success = true,
                startLifetime = main.startLifetime.constant,
                startSpeed = main.startSpeed.constant,
                startSize = main.startSize.constant
            };
        }

        // Animator Commands Implementation
        private object CreateAnimatorController(string paramsJson)
        {
            var p = JsonUtility.FromJson<AnimatorControllerPathParams>(paramsJson);

            string folderPath = System.IO.Path.GetDirectoryName(p.controllerPath);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                System.IO.Directory.CreateDirectory(Application.dataPath.Replace("Assets", "") + folderPath);
                AssetDatabase.Refresh();
            }

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(p.controllerPath);
            AssetDatabase.SaveAssets();

            return new { success = true, controllerPath = p.controllerPath };
        }

        private object AddAnimatorState(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddAnimatorStateParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            if (layerIndex >= controller.layers.Length) throw new Exception($"Layer index out of range: {layerIndex}");

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var position = new Vector3(p.positionX != 0 ? p.positionX : 300, p.positionY != 0 ? p.positionY : 0, 0);
            var state = stateMachine.AddState(p.stateName, position);

            AssetDatabase.SaveAssets();

            return new { success = true, stateName = p.stateName };
        }

        private object AddAnimatorTransition(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddAnimatorTransitionParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            var stateMachine = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState sourceState = null;
            UnityEditor.Animations.AnimatorState destState = null;

            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.sourceState) sourceState = childState.state;
                if (childState.state.name == p.destState) destState = childState.state;
            }

            if (sourceState == null) throw new Exception($"Source state not found: {p.sourceState}");
            if (destState == null) throw new Exception($"Destination state not found: {p.destState}");

            var transition = sourceState.AddTransition(destState);
            transition.hasExitTime = p.hasExitTime;
            if (p.exitTime > 0) transition.exitTime = p.exitTime;
            if (p.duration > 0) transition.duration = p.duration;

            AssetDatabase.SaveAssets();

            return new { success = true, sourceState = p.sourceState, destState = p.destState };
        }

        private object AddAnimatorParameter(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddAnimatorParameterParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            AnimatorControllerParameterType paramType;
            switch (p.parameterType.ToLower())
            {
                case "float": paramType = AnimatorControllerParameterType.Float; break;
                case "int": paramType = AnimatorControllerParameterType.Int; break;
                case "bool": paramType = AnimatorControllerParameterType.Bool; break;
                case "trigger": paramType = AnimatorControllerParameterType.Trigger; break;
                default: throw new Exception($"Unknown parameter type: {p.parameterType}");
            }

            controller.AddParameter(p.parameterName, paramType);
            AssetDatabase.SaveAssets();

            return new { success = true, parameterName = p.parameterName, parameterType = p.parameterType };
        }

        private object SetAnimatorController(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetAnimatorControllerParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            Undo.RecordObject(animator, "Set Animator Controller");
            animator.runtimeAnimatorController = controller;

            return new { success = true, gameObjectName = p.gameObjectName, controllerPath = p.controllerPath };
        }

        private object GetAnimatorParameters(string paramsJson)
        {
            var p = JsonUtility.FromJson<AnimatorControllerPathParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            var paramList = new List<string>();
            foreach (var param in controller.parameters)
            {
                paramList.Add($"{param.name}:{param.type}");
            }

            return new { success = true, parameters = string.Join(",", paramList), count = controller.parameters.Length };
        }

        private object GetAnimatorStates(string paramsJson)
        {
            var p = JsonUtility.FromJson<GetAnimatorStatesParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            if (layerIndex >= controller.layers.Length) throw new Exception($"Layer index out of range: {layerIndex}");

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var stateList = new List<string>();
            foreach (var childState in stateMachine.states)
            {
                stateList.Add(childState.state.name);
            }

            return new { success = true, states = string.Join(",", stateList), count = stateList.Count };
        }

        private object AddBlendTree(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddBlendTreeParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            UnityEditor.Animations.BlendTree blendTree;
            var state = controller.CreateBlendTreeInController(p.stateName, out blendTree);
            blendTree.blendParameter = p.blendParameter;

            AssetDatabase.SaveAssets();

            return new { success = true, stateName = p.stateName, blendParameter = p.blendParameter };
        }

        private object SetStateMotion(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetStateMotionParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p.clipPath);
            if (clip == null) throw new Exception($"AnimationClip not found: {p.clipPath}");

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.stateName)
                {
                    childState.state.motion = clip;
                    AssetDatabase.SaveAssets();
                    return new { success = true, stateName = p.stateName, clipPath = p.clipPath };
                }
            }

            throw new Exception($"State not found: {p.stateName}");
        }

        private object AddAnimatorLayer(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddAnimatorLayerParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            controller.AddLayer(p.layerName);

            // Set weight and blending mode if provided
            var layers = controller.layers;
            var newLayer = layers[layers.Length - 1];
            newLayer.defaultWeight = p.weight > 0 ? p.weight : 1.0f;
            if (p.blendingMode == "Additive")
            {
                newLayer.blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Additive;
            }
            controller.layers = layers;

            AssetDatabase.SaveAssets();

            return new { success = true, layerName = p.layerName };
        }

        private object SetTransitionConditions(string paramsJson)
        {
            // Simplified implementation - use JSON manually
            var jsonObj = MiniJSON.Json.Deserialize(paramsJson) as Dictionary<string, object>;
            string controllerPath = jsonObj["controllerPath"] as string;
            string sourceState = jsonObj["sourceState"] as string;
            string destState = jsonObj["destState"] as string;

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {controllerPath}");

            // Find the transition
            var stateMachine = controller.layers[0].stateMachine;
            UnityEditor.Animations.AnimatorState source = null;

            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == sourceState)
                {
                    source = childState.state;
                    break;
                }
            }

            if (source == null) throw new Exception($"Source state not found: {sourceState}");

            // Find transition to destState and add conditions
            foreach (var transition in source.transitions)
            {
                if (transition.destinationState != null && transition.destinationState.name == destState)
                {
                    // Clear existing conditions and add new ones
                    if (jsonObj.ContainsKey("conditions"))
                    {
                        var conditions = jsonObj["conditions"] as List<object>;
                        foreach (var condObj in conditions)
                        {
                            var cond = condObj as Dictionary<string, object>;
                            string param = cond["parameter"] as string;
                            string mode = cond["mode"] as string;
                            float threshold = cond.ContainsKey("threshold") ? System.Convert.ToSingle(cond["threshold"]) : 0f;

                            UnityEditor.Animations.AnimatorConditionMode condMode = UnityEditor.Animations.AnimatorConditionMode.If;
                            switch (mode.ToLower())
                            {
                                case "if": condMode = UnityEditor.Animations.AnimatorConditionMode.If; break;
                                case "ifnot": condMode = UnityEditor.Animations.AnimatorConditionMode.IfNot; break;
                                case "greater": condMode = UnityEditor.Animations.AnimatorConditionMode.Greater; break;
                                case "less": condMode = UnityEditor.Animations.AnimatorConditionMode.Less; break;
                                case "equals": condMode = UnityEditor.Animations.AnimatorConditionMode.Equals; break;
                                case "notequal": condMode = UnityEditor.Animations.AnimatorConditionMode.NotEqual; break;
                            }

                            transition.AddCondition(condMode, threshold, param);
                        }
                    }
                    AssetDatabase.SaveAssets();
                    return new { success = true };
                }
            }

            throw new Exception($"Transition from {sourceState} to {destState} not found");
        }

        private object CreateSubStateMachine(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateSubStateMachineParams>(paramsJson);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(p.controllerPath);
            if (controller == null) throw new Exception($"AnimatorController not found: {p.controllerPath}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            var stateMachine = controller.layers[layerIndex].stateMachine;
            var position = new Vector3(p.positionX != 0 ? p.positionX : 400, p.positionY != 0 ? p.positionY : 0, 0);

            var subStateMachine = stateMachine.AddStateMachine(p.name, position);
            AssetDatabase.SaveAssets();

            return new { success = true, name = p.name };
        }

        private object GetCurrentAnimatorState(string paramsJson)
        {
            var p = JsonUtility.FromJson<GetCurrentAnimatorStateParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);

            return new {
                success = true,
                normalizedTime = stateInfo.normalizedTime,
                length = stateInfo.length,
                isLooping = stateInfo.loop
            };
        }

        private object PlayAnimatorState(string paramsJson)
        {
            var p = JsonUtility.FromJson<PlayAnimatorStateParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            float normalizedTime = p.normalizedTime > 0 ? p.normalizedTime : 0f;

            animator.Play(p.stateName, layerIndex, normalizedTime);

            return new { success = true, stateName = p.stateName };
        }

        private object SetAnimatorSpeed(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetAnimatorSpeedParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            Undo.RecordObject(animator, "Set Animator Speed");
            animator.speed = p.speed;

            return new { success = true, speed = p.speed };
        }

        private object GetAnimatorClipInfo(string paramsJson)
        {
            var p = JsonUtility.FromJson<GetAnimatorClipInfoParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            var clipInfo = animator.GetCurrentAnimatorClipInfo(layerIndex);

            var clipNames = new List<string>();
            foreach (var info in clipInfo)
            {
                clipNames.Add(info.clip.name);
            }

            return new { success = true, clips = string.Join(",", clipNames), count = clipInfo.Length };
        }

        private object CrossfadeAnimator(string paramsJson)
        {
            var p = JsonUtility.FromJson<CrossfadeAnimatorParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            int layerIndex = p.layerIndex > 0 ? p.layerIndex : 0;
            float normalizedTime = p.normalizedTime > 0 ? p.normalizedTime : float.NegativeInfinity;

            animator.CrossFade(p.stateName, p.transitionDuration, layerIndex, normalizedTime);

            return new { success = true, stateName = p.stateName, transitionDuration = p.transitionDuration };
        }

        private object SetAvatar(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetAvatarParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(p.avatarPath);
            if (avatar == null) throw new Exception($"Avatar not found: {p.avatarPath}");

            Undo.RecordObject(animator, "Set Avatar");
            animator.avatar = avatar;

            return new { success = true, avatarPath = p.avatarPath };
        }

        private object SetRootMotion(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetRootMotionParams>(paramsJson);

            GameObject go = GameObject.Find(p.gameObjectName);
            if (go == null) throw new Exception($"GameObject not found: {p.gameObjectName}");

            Animator animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.GetComponentInChildren<Animator>();
            if (animator == null) throw new Exception($"Animator not found on {p.gameObjectName}");

            Undo.RecordObject(animator, "Set Root Motion");
            animator.applyRootMotion = p.enabled;

            return new { success = true, enabled = p.enabled };
        }

        // Helper method for GetParent
        private string GetFullPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        // Category A Parameter Classes
        [Serializable]
        private class GameObjectNameParams
        {
            public string gameObjectName;
            public bool value;
        }

        [Serializable]
        private class GameObjectNameOnlyParams
        {
            public string gameObjectName;
        }

        [Serializable]
        private class RenameParams
        {
            public string gameObjectName;
            public string newName;
        }

        [Serializable]
        private class AssetPathParams
        {
            public string assetPath;
        }

        [Serializable]
        private class TagParams
        {
            public string tag;
        }

        [Serializable]
        private class LogParams
        {
            public string message;
            public string type; // "log", "warning", "error"
        }

        // Category B Parameter Classes
        [Serializable]
        private class SetParentParams
        {
            public string gameObjectName;
            public string parentName;
        }

        [Serializable]
        private class SetLayerParams
        {
            public string gameObjectName;
            public string layerName;
        }

        [Serializable]
        private class SetTagParams
        {
            public string gameObjectName;
            public string tagName;
        }

        [Serializable]
        private class ComponentParams
        {
            public string gameObjectName;
            public string componentType;
        }

        // Category C Parameter Classes
        [Serializable]
        private class FolderParams
        {
            public string folderPath;
        }

        [Serializable]
        private class LayerParams
        {
            public string layerName;
        }

        // UI Parameter Classes
        [Serializable]
        private class CreateCanvasParams
        {
            public string canvasName;
            public string renderMode; // "ScreenSpaceOverlay", "ScreenSpaceCamera", "WorldSpace"
        }

        [Serializable]
        private class CreateUITextParams
        {
            public string textName;
            public string text;
            public int fontSize;
            public string parentName;
        }

        [Serializable]
        private class CreateUIButtonParams
        {
            public string buttonName;
            public string buttonText;
            public string parentName;
        }

        [Serializable]
        private class CreateUIImageParams
        {
            public string imageName;
            public string parentName;
        }

        [Serializable]
        private class CreateUIPanelParams
        {
            public string panelName;
            public string parentName;
        }

        [Serializable]
        private class SetUITextParams
        {
            public string gameObjectName;
            public string text;
            public int fontSize;
        }

        // RectTransform Parameter Classes
        [Serializable]
        private class SetRectTransformParams
        {
            public string gameObjectName;
            public float anchoredX;
            public float anchoredY;
            public float width;
            public float height;
        }

        [Serializable]
        private class SetAnchorParams
        {
            public string gameObjectName;
            public string anchorPreset; // "TopLeft", "MiddleCenter", "StretchCenter", etc.
            public float anchorMinX;
            public float anchorMinY;
            public float anchorMaxX;
            public float anchorMaxY;
        }

        [Serializable]
        private class SetPivotParams
        {
            public string gameObjectName;
            public float pivotX;
            public float pivotY;
        }

        // Material/Color Parameter Classes
        [Serializable]
        private class SetColorParams
        {
            public string gameObjectName;
            public float r;
            public float g;
            public float b;
            public float a;
            public string propertyName; // "_Color", "_EmissionColor", etc.
        }

        [Serializable]
        private class SetTextureParams
        {
            public string gameObjectName;
            public string texturePath; // Assets path
            public string propertyName; // "_MainTex", "_BumpMap", etc.
        }

        // InputField Parameter Class
        [Serializable]
        private class CreateUIInputFieldParams
        {
            public string inputFieldName;
            public string placeholder;
            public string parentName;
        }

        // Particle System Parameter Classes
        [Serializable]
        private class CreateParticleSystemParams
        {
            public string particleName;
            public float posX;
            public float posY;
            public float posZ;
        }

        [Serializable]
        private class SetParticlePropertyParams
        {
            public string gameObjectName;
            public float startLifetime;
            public float startSpeed;
            public float startSize;
            public float emissionRate;
            public float r;
            public float g;
            public float b;
            public float a;
        }

        [Serializable]
        private class CreateScriptParams
        {
            public string scriptName;
            public string scriptContent;
            public string path;
        }

        [Serializable]
        private class SetComponentPropertyParams
        {
            public string gameObjectName;
            public string componentType;
            public string propertyName;
            public string value;
            public string valueType; // "int", "float", "bool", "string", "GameObject", "Object"
        }

        [Serializable]
        private class CreatePrefabParams
        {
            public string sourceGameObjectName;
            public string prefabPath;
        }

        [Serializable]
        private class SetMaterialParams
        {
            public string gameObjectName;
            public string materialPath;
        }

        [Serializable]
        private class GetComponentPropertyParams
        {
            public string gameObjectName;
            public string componentType;
            public string propertyName;
        }

        [Serializable]
        private class FindAssetsParams
        {
            public string searchQuery;
        }

        [Serializable]
        private class BatchCreateGameObjectsParams
        {
            public BatchGameObjectData[] gameObjects;
        }

        [Serializable]
        private class BatchGameObjectData
        {
            public string name;
            public string primitiveType;
            public Vector3Data position;
            public Vector3Data rotation;
            public Vector3Data scale;
            public string parent;
        }

        [Serializable]
        private class PrefabResult
        {
            public bool success;
            public string prefabPath;
            public string prefabName;
        }

        [Serializable]
        private class PropertyResult
        {
            public bool success;
            public string value;
            public string valueType;
        }

        [Serializable]
        private class AssetListResult
        {
            public bool success;
            public string[] assetPaths;
            public int count;
        }

        [Serializable]
        private class BatchCreateResult
        {
            public bool success;
            public string[] createdObjects;
            public int count;
        }

        [Serializable]
        private class ProjectInfo
        {
            public string unityVersion;
            public string projectName;
            public string projectPath;
            public string platform;
            public string companyName;
        }

        [Serializable]
        private class ErrorResponse
        {
            public string error;
        }

        [Serializable]
        private class SimpleResult
        {
            public bool success;
        }

        [Serializable]
        private class GameObjectResult
        {
            public bool success;
            public string name;
            public int instanceId;
        }

        [Serializable]
        private class ComponentResult
        {
            public bool success;
            public string componentType;
        }

        [Serializable]
        private class SceneResult
        {
            public bool success;
            public string sceneName;
        }

        [Serializable]
        private class PathResult
        {
            public bool success;
            public string path;
        }

        [Serializable]
        private class HierarchyResult
        {
            public string[] hierarchy;
        }

        // Animator Parameter Classes
        [Serializable]
        private class AnimatorControllerPathParams
        {
            public string controllerPath;
        }

        [Serializable]
        private class AddAnimatorStateParams
        {
            public string controllerPath;
            public string stateName;
            public int layerIndex;
            public float positionX;
            public float positionY;
        }

        [Serializable]
        private class AddAnimatorTransitionParams
        {
            public string controllerPath;
            public string sourceState;
            public string destState;
            public bool hasExitTime;
            public float exitTime;
            public float duration;
        }

        [Serializable]
        private class AddAnimatorParameterParams
        {
            public string controllerPath;
            public string parameterName;
            public string parameterType;
            public float defaultFloat;
            public int defaultInt;
            public bool defaultBool;
        }

        [Serializable]
        private class SetAnimatorControllerParams
        {
            public string gameObjectName;
            public string controllerPath;
        }

        [Serializable]
        private class GetAnimatorParamsParams
        {
            public string controllerPath;
        }

        [Serializable]
        private class GetAnimatorStatesParams
        {
            public string controllerPath;
            public int layerIndex;
        }

        [Serializable]
        private class AddBlendTreeParams
        {
            public string controllerPath;
            public string stateName;
            public string blendParameter;
            public int layerIndex;
        }

        [Serializable]
        private class SetStateMotionParams
        {
            public string controllerPath;
            public string stateName;
            public string clipPath;
            public int layerIndex;
        }

        [Serializable]
        private class AddAnimatorLayerParams
        {
            public string controllerPath;
            public string layerName;
            public float weight;
            public string blendingMode;
        }

        [Serializable]
        private class SetTransitionConditionsParams
        {
            public string controllerPath;
            public string sourceState;
            public string destState;
            public string parameterName;
            public string conditionMode;
            public float threshold;
        }

        [Serializable]
        private class CreateSubStateMachineParams
        {
            public string controllerPath;
            public string name;
            public int layerIndex;
            public float positionX;
            public float positionY;
        }

        [Serializable]
        private class GetCurrentAnimatorStateParams
        {
            public string gameObjectName;
            public int layerIndex;
        }

        [Serializable]
        private class PlayAnimatorStateParams
        {
            public string gameObjectName;
            public string stateName;
            public int layerIndex;
            public float normalizedTime;
        }

        [Serializable]
        private class SetAnimatorSpeedParams
        {
            public string gameObjectName;
            public float speed;
        }

        [Serializable]
        private class GetAnimatorClipInfoParams
        {
            public string gameObjectName;
            public int layerIndex;
        }

        [Serializable]
        private class CrossfadeAnimatorParams
        {
            public string gameObjectName;
            public string stateName;
            public float transitionDuration;
            public int layerIndex;
            public float normalizedTime;
        }

        [Serializable]
        private class SetAvatarParams
        {
            public string gameObjectName;
            public string avatarPath;
        }

        [Serializable]
        private class SetRootMotionParams
        {
            public string gameObjectName;
            public bool enabled;
        }

        // HIGH-LEVEL GAME GENERATION PARAM CLASSES

        [Serializable]
        private class GenerateGameParams
        {
            public string gameName;
            public string gameType;
            public string[] systems;
            public bool createUI;
            public bool createPlayer;
        }

        [Serializable]
        private class SetupGameManagerParams
        {
            public string[] systems;
        }

        [Serializable]
        private class SetupPlayerParams
        {
            public string playerType;
            public string[] systems;
            public bool createModel;
        }

        [Serializable]
        private class SetupGameUIParams
        {
            public string[] systems;
            public string uiStyle;
        }

        [Serializable]
        private class WireSystemsParams
        {
            public SystemConnection[] connections;
        }

        [Serializable]
        private class SystemConnection
        {
            public string source;
            public string target;
            public string eventName;
        }

        [Serializable]
        private class ImportVaultSystemParams
        {
            public string systemId;
            public string systemPath;
            public string targetPath;
            public string namespaceName;
        }

        [Serializable]
        private class CreateEnemyParams
        {
            public string enemyName;
            public string enemyType;
            public string[] systems;
            public bool createModel;
        }

        [Serializable]
        private class SetupSceneStructureParams
        {
            public string[] structure;
        }

        // HIGH-LEVEL GAME GENERATION IMPLEMENTATIONS

        private object GenerateGame(string paramsJson)
        {
            var p = JsonUtility.FromJson<GenerateGameParams>(paramsJson);
            if (p == null) throw new Exception("Invalid GenerateGame parameters");

            var result = new Dictionary<string, object>();
            var createdObjects = new List<string>();

            // 1. Setup scene structure
            SetupSceneStructure(JsonUtility.ToJson(new SetupSceneStructureParams
            {
                structure = new[] { "--- MANAGERS ---", "--- PLAYER ---", "--- ENEMIES ---", "--- ENVIRONMENT ---", "--- UI ---" }
            }));
            createdObjects.Add("Scene structure created");

            // 2. Create GameManager with manager systems
            var managerSystems = new List<string>();
            foreach (var sys in p.systems)
            {
                if (sys.Contains("Manager") || sys.Contains("Save") || sys.Contains("Audio") || sys.Contains("Event"))
                    managerSystems.Add(sys);
            }
            if (managerSystems.Count > 0)
            {
                SetupGameManager(JsonUtility.ToJson(new SetupGameManagerParams { systems = managerSystems.ToArray() }));
                createdObjects.Add("GameManager with " + managerSystems.Count + " systems");
            }

            // 3. Create Player if requested
            if (p.createPlayer)
            {
                var playerSystems = new List<string>();
                foreach (var sys in p.systems)
                {
                    if (sys.Contains("Health") || sys.Contains("Mana") || sys.Contains("Inventory") ||
                        sys.Contains("Combat") || sys.Contains("Controller") || sys.Contains("Stat"))
                        playerSystems.Add(sys);
                }
                SetupPlayer(JsonUtility.ToJson(new SetupPlayerParams
                {
                    playerType = p.gameType?.Contains("2D") == true ? "2D" : "3D",
                    systems = playerSystems.ToArray(),
                    createModel = true
                }));
                createdObjects.Add("Player with " + playerSystems.Count + " systems");
            }

            // 4. Create UI if requested
            if (p.createUI)
            {
                var uiSystems = new List<string>();
                foreach (var sys in p.systems)
                {
                    if (sys.Contains("Health") || sys.Contains("Mana") || sys.Contains("Inventory") ||
                        sys.Contains("Quest") || sys.Contains("Minimap"))
                        uiSystems.Add(sys);
                }
                SetupGameUI(JsonUtility.ToJson(new SetupGameUIParams
                {
                    systems = uiSystems.ToArray(),
                    uiStyle = "Minimal"
                }));
                createdObjects.Add("UI Canvas with " + uiSystems.Count + " elements");
            }

            // 5. Save the scene
            string scenePath = $"Assets/Scenes/{p.gameName}.unity";
            if (!Directory.Exists("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);

            result["success"] = true;
            result["gameName"] = p.gameName;
            result["scenePath"] = scenePath;
            result["created"] = createdObjects;
            result["message"] = $"Game '{p.gameName}' generated with {createdObjects.Count} components";

            return result;
        }

        private object SetupGameManager(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetupGameManagerParams>(paramsJson);
            if (p == null || p.systems == null) throw new Exception("Invalid SetupGameManager parameters");

            // Find or create Managers parent
            var managersParent = GameObject.Find("--- MANAGERS ---");
            if (managersParent == null)
            {
                managersParent = new GameObject("--- MANAGERS ---");
            }

            // Create GameManager GameObject
            var gameManager = new GameObject("GameManager");
            gameManager.transform.SetParent(managersParent.transform);

            // Add systems as components (scripts must exist in project)
            var attached = new List<string>();
            foreach (var systemName in p.systems)
            {
                var type = GetTypeByName(systemName);
                if (type != null)
                {
                    gameManager.AddComponent(type);
                    attached.Add(systemName);
                }
                else
                {
                    Debug.LogWarning($"[Bridge] System script not found: {systemName}");
                }
            }

            Undo.RegisterCreatedObjectUndo(gameManager, "Create GameManager");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", "GameManager" },
                { "attachedSystems", attached },
                { "message", $"GameManager created with {attached.Count} systems" }
            };
        }

        private object SetupPlayer(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetupPlayerParams>(paramsJson);
            if (p == null || p.systems == null) throw new Exception("Invalid SetupPlayer parameters");

            // Find or create Player parent
            var playerParent = GameObject.Find("--- PLAYER ---");
            if (playerParent == null)
            {
                playerParent = new GameObject("--- PLAYER ---");
            }

            // Create Player GameObject with basic components
            GameObject player;
            if (p.createModel)
            {
                bool is2D = p.playerType == "2D";
                if (is2D)
                {
                    player = new GameObject("Player");
                    var sr = player.AddComponent<SpriteRenderer>();
                    sr.color = Color.green;
                    player.AddComponent<Rigidbody2D>();
                    player.AddComponent<BoxCollider2D>();
                }
                else
                {
                    player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    player.name = "Player";
                    player.AddComponent<Rigidbody>();
                    // Capsule already has CapsuleCollider
                }
            }
            else
            {
                player = new GameObject("Player");
            }

            player.transform.SetParent(playerParent.transform);
            player.tag = "Player";

            // Add systems as components
            var attached = new List<string>();
            foreach (var systemName in p.systems)
            {
                var type = GetTypeByName(systemName);
                if (type != null)
                {
                    player.AddComponent(type);
                    attached.Add(systemName);
                }
            }

            Undo.RegisterCreatedObjectUndo(player, "Create Player");

            // Save as prefab
            string prefabPath = null;
            if (!Directory.Exists("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!Directory.Exists("Assets/Prefabs/Characters"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Characters");

            prefabPath = "Assets/Prefabs/Characters/Player.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, prefabPath, InteractionMode.UserAction);
            Debug.Log($"[Bridge] Player prefab saved to {prefabPath}");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", "Player" },
                { "prefabPath", prefabPath },
                { "attachedSystems", attached },
                { "playerType", p.playerType ?? "3D" },
                { "message", $"Player created with {attached.Count} systems and saved as prefab" }
            };
        }

        private object SetupGameUI(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetupGameUIParams>(paramsJson);
            if (p == null || p.systems == null) throw new Exception("Invalid SetupGameUI parameters");

            // Find or create UI parent
            var uiParent = GameObject.Find("--- UI ---");
            if (uiParent == null)
            {
                uiParent = new GameObject("--- UI ---");
            }

            // Create Canvas
            var canvasGO = new GameObject("GameCanvas");
            canvasGO.transform.SetParent(uiParent.transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var createdElements = new List<string>();
            var createdScripts = new List<string>();

            // Ensure Scripts/UI folder exists
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Scripts/UI");

            // Find player for wiring
            var player = GameObject.FindWithTag("Player");

            // Create UI elements based on systems
            foreach (var system in p.systems)
            {
                if (system.Contains("Health"))
                {
                    var healthBar = CreateHealthBarWithScript(canvasGO.transform, player);
                    createdElements.Add("HealthBar");
                    createdScripts.Add("HealthBarUI.cs");
                }
                if (system.Contains("Mana") || system.Contains("Energy"))
                {
                    var manaBar = CreateManaBarWithScript(canvasGO.transform, player);
                    createdElements.Add("ManaBar");
                    createdScripts.Add("ManaBarUI.cs");
                }
                if (system.Contains("Inventory"))
                {
                    CreateInventoryPanel(canvasGO.transform);
                    createdElements.Add("InventoryPanel");
                }
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Game UI");

            // Save UI as prefab
            string prefabPath = null;
            if (!Directory.Exists("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!Directory.Exists("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            prefabPath = "Assets/Prefabs/UI/GameCanvas.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGO, prefabPath, InteractionMode.UserAction);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "canvas", "GameCanvas" },
                { "prefabPath", prefabPath },
                { "elements", createdElements },
                { "scripts", createdScripts },
                { "wiredToPlayer", player != null },
                { "message", $"UI Canvas created with {createdElements.Count} elements, wired to Player: {player != null}" }
            };
        }

        private void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path);
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private GameObject CreateHealthBarWithScript(Transform parent, GameObject player)
        {
            // Generate HealthBarUI script if it doesn't exist
            string scriptPath = "Assets/Scripts/UI/HealthBarUI.cs";
            if (!File.Exists(scriptPath))
            {
                string scriptContent = @"using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header(""References"")]
    public Image fillImage;
    public GameObject targetObject;

    [Header(""Settings"")]
    public string healthFieldName = ""currentHealth"";
    public string maxHealthFieldName = ""maxHealth"";
    public Color fullHealthColor = new Color(0.2f, 0.8f, 0.2f);
    public Color lowHealthColor = new Color(0.8f, 0.2f, 0.2f);
    public float lowHealthThreshold = 0.3f;

    private Component healthSystem;
    private System.Reflection.FieldInfo currentHealthField;
    private System.Reflection.FieldInfo maxHealthField;

    void Start()
    {
        if (targetObject == null)
            targetObject = GameObject.FindWithTag(""Player"");

        if (targetObject != null)
            FindHealthSystem();
    }

    void FindHealthSystem()
    {
        foreach (var comp in targetObject.GetComponents<Component>())
        {
            var type = comp.GetType();
            if (type.Name.Contains(""Health""))
            {
                healthSystem = comp;
                currentHealthField = type.GetField(healthFieldName) ?? type.GetField(""CurrentHealth"") ?? type.GetField(""health"");
                maxHealthField = type.GetField(maxHealthFieldName) ?? type.GetField(""MaxHealth"") ?? type.GetField(""maxHP"");

                // Try properties if fields not found
                if (currentHealthField == null)
                {
                    var prop = type.GetProperty(healthFieldName) ?? type.GetProperty(""CurrentHealth"");
                    if (prop != null) currentHealthField = null; // Will use property instead
                }
                break;
            }
        }
    }

    void Update()
    {
        if (healthSystem == null || fillImage == null) return;

        float current = GetValue(currentHealthField, healthFieldName);
        float max = GetValue(maxHealthField, maxHealthFieldName);

        if (max > 0)
        {
            float ratio = current / max;
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, ratio > lowHealthThreshold ? 1 : ratio / lowHealthThreshold);
        }
    }

    float GetValue(System.Reflection.FieldInfo field, string fieldName)
    {
        if (healthSystem == null) return 0;

        if (field != null)
        {
            var val = field.GetValue(healthSystem);
            if (val is float f) return f;
            if (val is int i) return i;
        }

        // Try property
        var prop = healthSystem.GetType().GetProperty(fieldName);
        if (prop != null)
        {
            var val = prop.GetValue(healthSystem);
            if (val is float f) return f;
            if (val is int i) return i;
        }

        return 100;
    }
}";
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();
                Debug.Log($"[Bridge] Created HealthBarUI script at {scriptPath}");
            }

            // Create HealthBar UI
            var healthBar = new GameObject("HealthBar");
            healthBar.transform.SetParent(parent);
            var rect = healthBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(250, 35);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(healthBar.transform);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(healthBar.transform);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3, 3);
            fillRect.offsetMax = new Vector2(-3, -3);
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            fillImage.type = UnityEngine.UI.Image.Type.Filled;
            fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

            // Attach script after compilation
            EditorApplication.delayCall += () => {
                var scriptType = GetTypeByName("HealthBarUI");
                if (scriptType != null && healthBar != null)
                {
                    var comp = healthBar.AddComponent(scriptType);
                    // Set references via reflection
                    var fillField = scriptType.GetField("fillImage");
                    var targetField = scriptType.GetField("targetObject");
                    if (fillField != null) fillField.SetValue(comp, fillImage);
                    if (targetField != null && player != null) targetField.SetValue(comp, player);
                    Debug.Log("[Bridge] HealthBarUI component attached and wired");
                }
            };

            return healthBar;
        }

        private GameObject CreateManaBarWithScript(Transform parent, GameObject player)
        {
            // Generate ManaBarUI script if it doesn't exist
            string scriptPath = "Assets/Scripts/UI/ManaBarUI.cs";
            if (!File.Exists(scriptPath))
            {
                string scriptContent = @"using UnityEngine;
using UnityEngine.UI;

public class ManaBarUI : MonoBehaviour
{
    [Header(""References"")]
    public Image fillImage;
    public GameObject targetObject;

    [Header(""Settings"")]
    public string manaFieldName = ""currentMana"";
    public string maxManaFieldName = ""maxMana"";
    public Color fullManaColor = new Color(0.2f, 0.4f, 0.9f);
    public Color lowManaColor = new Color(0.1f, 0.2f, 0.5f);

    private Component manaSystem;
    private System.Reflection.FieldInfo currentManaField;
    private System.Reflection.FieldInfo maxManaField;

    void Start()
    {
        if (targetObject == null)
            targetObject = GameObject.FindWithTag(""Player"");

        if (targetObject != null)
            FindManaSystem();
    }

    void FindManaSystem()
    {
        foreach (var comp in targetObject.GetComponents<Component>())
        {
            var type = comp.GetType();
            if (type.Name.Contains(""Mana"") || type.Name.Contains(""Energy""))
            {
                manaSystem = comp;
                currentManaField = type.GetField(manaFieldName) ?? type.GetField(""CurrentMana"") ?? type.GetField(""mana"");
                maxManaField = type.GetField(maxManaFieldName) ?? type.GetField(""MaxMana"") ?? type.GetField(""maxMP"");
                break;
            }
        }
    }

    void Update()
    {
        if (manaSystem == null || fillImage == null) return;

        float current = GetValue(currentManaField, manaFieldName);
        float max = GetValue(maxManaField, maxManaFieldName);

        if (max > 0)
        {
            float ratio = current / max;
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(lowManaColor, fullManaColor, ratio);
        }
    }

    float GetValue(System.Reflection.FieldInfo field, string fieldName)
    {
        if (manaSystem == null) return 0;

        if (field != null)
        {
            var val = field.GetValue(manaSystem);
            if (val is float f) return f;
            if (val is int i) return i;
        }

        var prop = manaSystem.GetType().GetProperty(fieldName);
        if (prop != null)
        {
            var val = prop.GetValue(manaSystem);
            if (val is float f) return f;
            if (val is int i) return i;
        }

        return 100;
    }
}";
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();
                Debug.Log($"[Bridge] Created ManaBarUI script at {scriptPath}");
            }

            // Create ManaBar UI
            var manaBar = new GameObject("ManaBar");
            manaBar.transform.SetParent(parent);
            var rect = manaBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -65);
            rect.sizeDelta = new Vector2(200, 25);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(manaBar.transform);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(manaBar.transform);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.2f, 0.4f, 0.9f, 1f);
            fillImage.type = UnityEngine.UI.Image.Type.Filled;
            fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;

            // Attach script after compilation
            EditorApplication.delayCall += () => {
                var scriptType = GetTypeByName("ManaBarUI");
                if (scriptType != null && manaBar != null)
                {
                    var comp = manaBar.AddComponent(scriptType);
                    var fillField = scriptType.GetField("fillImage");
                    var targetField = scriptType.GetField("targetObject");
                    if (fillField != null) fillField.SetValue(comp, fillImage);
                    if (targetField != null && player != null) targetField.SetValue(comp, player);
                    Debug.Log("[Bridge] ManaBarUI component attached and wired");
                }
            };

            return manaBar;
        }

        private void CreateHealthBar(Transform parent)
        {
            var healthBar = new GameObject("HealthBar");
            healthBar.transform.SetParent(parent);
            var rect = healthBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(200, 30);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(healthBar.transform);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(healthBar.transform);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        }

        private void CreateManaBar(Transform parent)
        {
            var manaBar = new GameObject("ManaBar");
            manaBar.transform.SetParent(parent);
            var rect = manaBar.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(20, -60);
            rect.sizeDelta = new Vector2(200, 20);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(manaBar.transform);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(manaBar.transform);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.2f, 0.4f, 0.9f, 1f);
        }

        private void CreateInventoryPanel(Transform parent)
        {
            var panel = new GameObject("InventoryPanel");
            panel.transform.SetParent(parent);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-20, 20);
            rect.sizeDelta = new Vector2(300, 400);

            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(panel.transform);
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -10);
            titleRect.sizeDelta = new Vector2(0, 30);
            var titleText = title.AddComponent<UnityEngine.UI.Text>();
            titleText.text = "INVENTORY";
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.fontSize = 18;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panel.SetActive(false); // Hidden by default
        }

        private object WireSystems(string paramsJson)
        {
            var p = JsonUtility.FromJson<WireSystemsParams>(paramsJson);
            if (p == null || p.connections == null) throw new Exception("Invalid WireSystems parameters");

            var wired = new List<string>();
            var failed = new List<string>();

            foreach (var conn in p.connections)
            {
                bool success = TryWireConnection(conn.source, conn.target, conn.eventName);
                if (success)
                {
                    wired.Add($"{conn.source} -> {conn.target}");
                    Debug.Log($"[Bridge] Wired: {conn.source} -> {conn.target}");
                }
                else
                {
                    failed.Add($"{conn.source} -> {conn.target}");
                    Debug.LogWarning($"[Bridge] Failed to wire: {conn.source} -> {conn.target}");
                }
            }

            return new Dictionary<string, object>
            {
                { "success", failed.Count == 0 },
                { "wired", wired },
                { "failed", failed },
                { "message", $"Wired {wired.Count} connections, {failed.Count} failed" }
            };
        }

        private bool TryWireConnection(string sourceName, string targetName, string eventName)
        {
            // Find source component (can be on any GameObject)
            Component sourceComp = null;
            Component targetComp = null;

            // Search all GameObjects for source component type
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;

                    if (typeName == sourceName || typeName.Contains(sourceName))
                        sourceComp = comp;
                    if (typeName == targetName || typeName.Contains(targetName))
                        targetComp = comp;

                    if (sourceComp != null && targetComp != null)
                        break;
                }
                if (sourceComp != null && targetComp != null)
                    break;
            }

            if (sourceComp == null || targetComp == null)
            {
                Debug.LogWarning($"[Bridge] Could not find {(sourceComp == null ? sourceName : targetName)}");
                return false;
            }

            // Try to find and set a reference field on target that matches source type
            var targetType = targetComp.GetType();
            var sourceType = sourceComp.GetType();

            // Look for fields that can hold the source component
            foreach (var field in targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (field.FieldType == sourceType || field.FieldType.IsAssignableFrom(sourceType) ||
                    field.Name.ToLower().Contains(sourceName.ToLower().Replace("system", "")))
                {
                    try
                    {
                        if (field.FieldType == typeof(GameObject))
                            field.SetValue(targetComp, sourceComp.gameObject);
                        else if (field.FieldType.IsAssignableFrom(sourceType))
                            field.SetValue(targetComp, sourceComp);

                        EditorUtility.SetDirty(targetComp);
                        Debug.Log($"[Bridge] Set {targetName}.{field.Name} = {sourceName}");
                        return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Bridge] Failed to set field: {e.Message}");
                    }
                }
            }

            // Look for serialized fields with SerializeField attribute
            foreach (var field in targetType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (field.GetCustomAttributes(typeof(SerializeField), true).Length > 0)
                {
                    if (field.FieldType == sourceType || field.FieldType.IsAssignableFrom(sourceType) ||
                        field.Name.ToLower().Contains(sourceName.ToLower().Replace("system", "")))
                    {
                        try
                        {
                            if (field.FieldType == typeof(GameObject))
                                field.SetValue(targetComp, sourceComp.gameObject);
                            else if (field.FieldType.IsAssignableFrom(sourceType))
                                field.SetValue(targetComp, sourceComp);

                            EditorUtility.SetDirty(targetComp);
                            Debug.Log($"[Bridge] Set {targetName}.{field.Name} = {sourceName} (serialized)");
                            return true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Bridge] Failed to set serialized field: {e.Message}");
                        }
                    }
                }
            }

            // If we have an eventName, try to find UnityEvent and add listener
            if (!string.IsNullOrEmpty(eventName))
            {
                var eventField = sourceType.GetField(eventName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (eventField != null && eventField.FieldType.Name.Contains("UnityEvent"))
                {
                    Debug.Log($"[Bridge] Found event {eventName} on {sourceName} - runtime subscription needed");
                    return true; // Event exists, will be wired at runtime
                }
            }

            return false;
        }

        private object ImportVaultSystem(string paramsJson)
        {
            var p = JsonUtility.FromJson<ImportVaultSystemParams>(paramsJson);
            if (p == null || string.IsNullOrEmpty(p.systemPath)) throw new Exception("Invalid ImportVaultSystem parameters");

            string targetPath = string.IsNullOrEmpty(p.targetPath) ? "Assets/Scripts/Systems" : p.targetPath;

            // Ensure target directory exists
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            // Copy files from library path
            var copiedFiles = new List<string>();
            if (Directory.Exists(p.systemPath))
            {
                var files = Directory.GetFiles(p.systemPath, "*.cs");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destPath = Path.Combine(targetPath, fileName);
                    File.Copy(file, destPath, true);
                    copiedFiles.Add(fileName);
                }
            }

            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "systemId", p.systemId },
                { "copiedFiles", copiedFiles },
                { "targetPath", targetPath },
                { "message", $"Imported {copiedFiles.Count} files from {p.systemId}" }
            };
        }

        private object CreateEnemy(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateEnemyParams>(paramsJson);
            if (p == null || string.IsNullOrEmpty(p.enemyName)) throw new Exception("Invalid CreateEnemy parameters");

            // Find or create Enemies parent
            var enemiesParent = GameObject.Find("--- ENEMIES ---");
            if (enemiesParent == null)
            {
                enemiesParent = new GameObject("--- ENEMIES ---");
            }

            // Create Enemy GameObject
            GameObject enemy;
            if (p.createModel)
            {
                enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = p.enemyName;
                enemy.GetComponent<Renderer>().material.color = Color.red;
                enemy.AddComponent<Rigidbody>();
            }
            else
            {
                enemy = new GameObject(p.enemyName);
            }

            enemy.transform.SetParent(enemiesParent.transform);
            enemy.tag = "Enemy";

            // Add systems
            var attached = new List<string>();
            if (p.systems != null)
            {
                foreach (var systemName in p.systems)
                {
                    var type = GetTypeByName(systemName);
                    if (type != null)
                    {
                        enemy.AddComponent(type);
                        attached.Add(systemName);
                    }
                }
            }

            Undo.RegisterCreatedObjectUndo(enemy, "Create Enemy");

            // Save as prefab
            string prefabPath = null;
            if (!Directory.Exists("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!Directory.Exists("Assets/Prefabs/Enemies"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Enemies");

            string safeName = p.enemyName.Replace(" ", "_");
            prefabPath = $"Assets/Prefabs/Enemies/{safeName}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(enemy, prefabPath, InteractionMode.UserAction);
            Debug.Log($"[Bridge] Enemy prefab saved to {prefabPath}");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", p.enemyName },
                { "prefabPath", prefabPath },
                { "enemyType", p.enemyType ?? "Melee" },
                { "attachedSystems", attached },
                { "message", $"Enemy '{p.enemyName}' created with {attached.Count} systems and saved as prefab" }
            };
        }

        private object SetupSceneStructure(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetupSceneStructureParams>(paramsJson);

            string[] defaultStructure = new[] {
                "--- MANAGERS ---",
                "--- PLAYER ---",
                "--- ENEMIES ---",
                "--- ENVIRONMENT ---",
                "--- UI ---"
            };

            var structure = p?.structure ?? defaultStructure;
            var created = new List<string>();

            foreach (var name in structure)
            {
                if (GameObject.Find(name) == null)
                {
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                    created.Add(name);
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "created", created },
                { "message", $"Scene structure created with {created.Count} root objects" }
            };
        }

        private Type GetTypeByName(string typeName)
        {
            // Try to find the type in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;

                // Also try without namespace
                var types = assembly.GetTypes().Where(t => t.Name == typeName).ToArray();
                if (types.Length > 0) return types[0];
            }
            return null;
        }

    // Simple JSON parser for handling dynamic params
    public static class MiniJSON
    {
        public static class Json
        {
            public static object Deserialize(string json)
            {
                return Parser.Parse(json);
            }

            public static string Serialize(object obj)
            {
                return Serializer.Serialize(obj);
            }
        }

        sealed class Parser
        {
            const string WORD_BREAK = "{}[],:\"";

            public static object Parse(string json)
            {
                if (json == null) return null;
                return new Parser(json).ParseValue();
            }

            StringReader json;

            Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }

            object ParseValue()
            {
                NextToken();
                switch (json.Peek())
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        return ParseNumber();
                }

                string word = NextWord();
                if (word == "false") return false;
                if (word == "true") return true;
                if (word == "null") return null;
                return word;
            }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                json.Read(); // {

                while (true)
                {
                    switch (NextToken())
                    {
                        case '}':
                            json.Read();
                            return table;
                        case ',':
                            json.Read();
                            continue;
                        default:
                            string name = ParseString();
                            if (name == null) return table;

                            NextToken();
                            if ((char)json.Read() != ':') return null;

                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [

                while (true)
                {
                    char nextToken = NextToken();
                    if (nextToken == ']')
                    {
                        json.Read();
                        return array;
                    }

                    if (nextToken == ',')
                    {
                        json.Read();
                        continue;
                    }

                    array.Add(ParseValue());
                }
            }

            string ParseString()
            {
                var s = new System.Text.StringBuilder();
                json.Read(); // "

                while (true)
                {
                    if (json.Peek() == -1) break;

                    char c = (char)json.Read();
                    if (c == '"') return s.ToString();

                    if (c == '\\')
                    {
                        if (json.Peek() == -1) break;

                        c = (char)json.Read();
                        if (c == '"' || c == '\\' || c == '/') s.Append(c);
                        else if (c == 'b') s.Append('\b');
                        else if (c == 'f') s.Append('\f');
                        else if (c == 'n') s.Append('\n');
                        else if (c == 'r') s.Append('\r');
                        else if (c == 't') s.Append('\t');
                    }
                    else
                    {
                        s.Append(c);
                    }
                }

                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord();
                if (number.IndexOf('.') == -1)
                {
                    long parsedInt;
                    long.TryParse(number, out parsedInt);
                    return parsedInt;
                }

                double parsedDouble;
                double.TryParse(number, out parsedDouble);
                return parsedDouble;
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace((char)json.Peek()))
                    json.Read();
            }

            char NextToken()
            {
                EatWhitespace();
                return (char)json.Peek();
            }

            string NextWord()
            {
                var word = new System.Text.StringBuilder();

                while (!IsWordBreak((char)json.Peek()))
                {
                    word.Append((char)json.Read());
                    if (json.Peek() == -1) break;
                }

                return word.ToString();
            }

            bool IsWordBreak(char c)
            {
                return char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
            }
        }

        sealed class Serializer
        {
            System.Text.StringBuilder builder;

            Serializer()
            {
                builder = new System.Text.StringBuilder();
            }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }

            void SerializeValue(object value)
            {
                if (value == null)
                {
                    builder.Append("null");
                }
                else if (value is string)
                {
                    SerializeString((string)value);
                }
                else if (value is bool)
                {
                    builder.Append(((bool)value) ? "true" : "false");
                }
                else if (value is Dictionary<string, object>)
                {
                    SerializeObject((Dictionary<string, object>)value);
                }
                else if (value is List<object>)
                {
                    SerializeArray((List<object>)value);
                }
                else if (value is long || value is int)
                {
                    builder.Append(value.ToString());
                }
                else if (value is double || value is float)
                {
                    builder.Append(((double)value).ToString("R"));
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }

            void SerializeObject(Dictionary<string, object> obj)
            {
                bool first = true;
                builder.Append('{');

                foreach (var e in obj)
                {
                    if (!first) builder.Append(',');
                    SerializeString(e.Key);
                    builder.Append(':');
                    SerializeValue(e.Value);
                    first = false;
                }

                builder.Append('}');
            }

            void SerializeArray(List<object> array)
            {
                bool first = true;
                builder.Append('[');

                foreach (var obj in array)
                {
                    if (!first) builder.Append(',');
                    SerializeValue(obj);
                    first = false;
                }

                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('\"');

                foreach (char c in str)
                {
                    if (c == '"') builder.Append("\\\"");
                    else if (c == '\\') builder.Append("\\\\");
                    else if (c == '\b') builder.Append("\\b");
                    else if (c == '\f') builder.Append("\\f");
                    else if (c == '\n') builder.Append("\\n");
                    else if (c == '\r') builder.Append("\\r");
                    else if (c == '\t') builder.Append("\\t");
                    else builder.Append(c);
                }

                builder.Append('\"');
            }
        }
    }

    }
}
