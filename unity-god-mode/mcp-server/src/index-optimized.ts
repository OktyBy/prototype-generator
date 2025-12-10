#!/usr/bin/env node

/**
 * Unity God Mode MCP Server v3.0 - OPTIMIZED
 *
 * Context-optimized architecture using Progressive Disclosure:
 * - 3 meta tools instead of 105+ individual tools
 * - ~200 tokens vs ~15,000 tokens initial context
 * - 98.7% token savings
 *
 * Based on:
 * - scottspence.com/posts/optimising-mcp-server-context-usage-in-claude-code
 * - mcpcat.io/guides/managing-claude-code-context/
 * - anthropic.com/engineering/code-execution-with-mcp
 * - obot.ai/resources/learning-center/mcp-anthropic/
 * - claudelog.com/mechanics/context-inspection/
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import * as net from "net";

// Configuration
const UNITY_BRIDGE_HOST = "127.0.0.1";
const UNITY_BRIDGE_PORT = 7777;
const CONNECTION_TIMEOUT = 5000;

// ============================================================================
// COMMAND REGISTRY - All commands organized by category
// ============================================================================

interface CommandDef {
  cmd: string;          // Bridge command name
  desc: string;         // Short description (max 15 tokens)
  params: string[];     // Required params
  optional?: string[];  // Optional params
}

const COMMAND_REGISTRY: Record<string, Record<string, CommandDef>> = {
  gameobject: {
    create: { cmd: "CreateGameObject", desc: "Create GameObject", params: ["name"], optional: ["primitiveType", "parent"] },
    delete: { cmd: "DeleteGameObject", desc: "Delete GameObject", params: ["gameObjectName"] },
    duplicate: { cmd: "DuplicateGameObject", desc: "Duplicate GameObject", params: ["gameObjectName"] },
    rename: { cmd: "RenameGameObject", desc: "Rename GameObject", params: ["gameObjectName", "newName"] },
    setActive: { cmd: "SetActive", desc: "Enable/disable", params: ["gameObjectName", "active"] },
    getActive: { cmd: "GetActiveState", desc: "Get active state", params: ["gameObjectName"] },
    setParent: { cmd: "SetParent", desc: "Set parent", params: ["gameObjectName"], optional: ["parentName"] },
    getParent: { cmd: "GetParent", desc: "Get parent", params: ["gameObjectName"] },
    getChildren: { cmd: "GetChildren", desc: "Get children", params: ["gameObjectName"] },
    setLayer: { cmd: "SetLayer", desc: "Set layer", params: ["gameObjectName", "layer"] },
    setTag: { cmd: "SetTag", desc: "Set tag", params: ["gameObjectName", "tag"] },
    select: { cmd: "SelectGameObject", desc: "Select in Editor", params: ["gameObjectName"] },
    focus: { cmd: "FocusGameObject", desc: "Focus in Scene", params: ["gameObjectName"] },
    findByTag: { cmd: "FindGameObjectsByTag", desc: "Find by tag", params: ["tag"] },
    findByLayer: { cmd: "FindGameObjectsByLayer", desc: "Find by layer", params: ["layer"] },
    findByComponent: { cmd: "FindGameObjectsWithComponent", desc: "Find by component", params: ["componentType"] },
    batchCreate: { cmd: "BatchCreateGameObjects", desc: "Create multiple", params: ["gameObjects"] },
  },
  transform: {
    set: { cmd: "SetTransform", desc: "Set transform", params: ["gameObjectName"], optional: ["position", "rotation", "scale"] },
    setSibling: { cmd: "SetSiblingIndex", desc: "Set sibling index", params: ["gameObjectName", "index"] },
    getSibling: { cmd: "GetSiblingIndex", desc: "Get sibling index", params: ["gameObjectName"] },
  },
  component: {
    add: { cmd: "AddComponent", desc: "Add component", params: ["gameObjectName", "componentType"] },
    remove: { cmd: "RemoveComponent", desc: "Remove component", params: ["gameObjectName", "componentType"] },
    getAll: { cmd: "GetAllComponents", desc: "List components", params: ["gameObjectName"] },
    has: { cmd: "HasComponent", desc: "Check component", params: ["gameObjectName", "componentType"] },
    copy: { cmd: "CopyComponent", desc: "Copy component", params: ["sourceGameObject", "targetGameObject", "componentType"] },
    setProperty: { cmd: "SetComponentProperty", desc: "Set property", params: ["gameObjectName", "componentType", "propertyName", "value", "valueType"] },
    getProperty: { cmd: "GetComponentProperty", desc: "Get property", params: ["gameObjectName", "componentType", "propertyName"] },
    sendMessage: { cmd: "SendMessage", desc: "Call method", params: ["gameObjectName", "methodName"] },
  },
  scene: {
    create: { cmd: "CreateScene", desc: "Create scene", params: ["sceneName"], optional: ["additive"] },
    save: { cmd: "SaveScene", desc: "Save scene", params: [], optional: ["path"] },
    load: { cmd: "LoadScene", desc: "Load scene", params: ["sceneName"] },
    list: { cmd: "ListScenes", desc: "List scenes", params: [] },
    getActive: { cmd: "GetActiveScene", desc: "Get active scene", params: [] },
    getAll: { cmd: "GetAllScenes", desc: "Get loaded scenes", params: [] },
    getHierarchy: { cmd: "GetHierarchy", desc: "Get hierarchy", params: [], optional: ["rootOnly"] },
  },
  prefab: {
    create: { cmd: "CreatePrefab", desc: "Create prefab", params: ["sourceGameObjectName", "prefabPath"] },
    instantiate: { cmd: "InstantiatePrefab", desc: "Spawn prefab", params: ["prefabPath"] },
    unpack: { cmd: "UnpackPrefab", desc: "Unpack prefab", params: ["gameObjectName"] },
    apply: { cmd: "ApplyPrefabChanges", desc: "Apply changes", params: ["gameObjectName"] },
  },
  asset: {
    find: { cmd: "FindAssets", desc: "Search assets", params: ["searchQuery"] },
    createFolder: { cmd: "CreateFolder", desc: "Create folder", params: ["folderPath"] },
    delete: { cmd: "DeleteAsset", desc: "Delete asset", params: ["assetPath"] },
    move: { cmd: "MoveAsset", desc: "Move asset", params: ["oldPath", "newPath"] },
    duplicate: { cmd: "DuplicateAsset", desc: "Duplicate asset", params: ["assetPath"] },
    rename: { cmd: "RenameAsset", desc: "Rename asset", params: ["assetPath", "newName"] },
    getDependencies: { cmd: "GetAssetDependencies", desc: "Get dependencies", params: ["assetPath"] },
    refresh: { cmd: "RefreshAssetDatabase", desc: "Refresh database", params: [] },
  },
  material: {
    create: { cmd: "CreateMaterial", desc: "Create material", params: ["materialPath"] },
    set: { cmd: "SetMaterial", desc: "Set material", params: ["gameObjectName", "materialPath"] },
    setProperty: { cmd: "SetMaterialProperty", desc: "Set property", params: ["materialPath", "propertyName", "value"] },
    setShader: { cmd: "SetShader", desc: "Set shader", params: ["materialPath", "shaderName"] },
    setTexture: { cmd: "AssignTexture", desc: "Set texture", params: ["materialPath", "propertyName", "texturePath"] },
  },
  physics: {
    setRigidbody: { cmd: "SetRigidbodyProperty", desc: "Set rigidbody", params: ["gameObjectName", "propertyName", "value"] },
    setCollider: { cmd: "SetColliderProperty", desc: "Set collider", params: ["gameObjectName", "propertyName", "value"] },
    addForce: { cmd: "AddForce", desc: "Add force", params: ["gameObjectName", "forceX", "forceY", "forceZ"] },
    setGravity: { cmd: "SetGravity", desc: "Set gravity", params: ["x", "y", "z"] },
    getGravity: { cmd: "GetGravity", desc: "Get gravity", params: [] },
    bakeNavMesh: { cmd: "BakeNavMesh", desc: "Bake NavMesh", params: [] },
  },
  animation: {
    createAnimator: { cmd: "CreateAnimator", desc: "Add Animator", params: ["gameObjectName"] },
    setParameter: { cmd: "SetAnimatorParameter", desc: "Set parameter", params: ["gameObjectName", "parameterName", "value", "valueType"] },
    createClip: { cmd: "CreateAnimationClip", desc: "Create clip", params: ["clipPath"] },
    createController: { cmd: "CreateAnimatorController", desc: "Create controller", params: ["controllerPath"] },
    addState: { cmd: "AddAnimatorState", desc: "Add state", params: ["controllerPath", "stateName"], optional: ["layerIndex", "positionX", "positionY"] },
    addTransition: { cmd: "AddAnimatorTransition", desc: "Add transition", params: ["controllerPath", "sourceState", "destState"], optional: ["hasExitTime", "exitTime", "duration"] },
    addParameter: { cmd: "AddAnimatorParameter", desc: "Add parameter", params: ["controllerPath", "parameterName", "parameterType"], optional: ["defaultValue"] },
    setController: { cmd: "SetAnimatorController", desc: "Set controller", params: ["gameObjectName", "controllerPath"] },
    getParameters: { cmd: "GetAnimatorParameters", desc: "Get parameters", params: ["controllerPath"] },
    getStates: { cmd: "GetAnimatorStates", desc: "Get states", params: ["controllerPath"], optional: ["layerIndex"] },
    play: { cmd: "PlayAnimatorState", desc: "Play state", params: ["gameObjectName", "stateName"], optional: ["layerIndex", "normalizedTime"] },
    setSpeed: { cmd: "SetAnimatorSpeed", desc: "Set speed", params: ["gameObjectName", "speed"] },
    crossfade: { cmd: "CrossfadeAnimator", desc: "Crossfade", params: ["gameObjectName", "stateName", "transitionDuration"], optional: ["layerIndex", "normalizedTime"] },
  },
  audio: {
    play: { cmd: "PlayAudio", desc: "Play audio", params: ["gameObjectName"] },
    stop: { cmd: "StopAudio", desc: "Stop audio", params: ["gameObjectName"] },
    setProperty: { cmd: "SetAudioProperty", desc: "Set property", params: ["gameObjectName", "propertyName", "value"] },
    setClip: { cmd: "SetAudioClip", desc: "Set clip", params: ["gameObjectName", "clipPath"] },
  },
  ui: {
    createCanvas: { cmd: "CreateCanvas", desc: "Create Canvas", params: [], optional: ["name"] },
    createButton: { cmd: "CreateButton", desc: "Create Button", params: ["name"], optional: ["parentName"] },
    createImage: { cmd: "CreateImage", desc: "Create Image", params: ["name"], optional: ["parentName"] },
    createPanel: { cmd: "CreatePanel", desc: "Create Panel", params: ["name"], optional: ["parentName"] },
    setText: { cmd: "SetText", desc: "Set text", params: ["gameObjectName", "text"] },
    setRect: { cmd: "SetRectTransform", desc: "Set RectTransform", params: ["gameObjectName", "propertyName", "value"] },
  },
  particles: {
    create: { cmd: "CreateParticleSystem", desc: "Create particles", params: ["name"] },
    setProperty: { cmd: "SetParticleProperty", desc: "Set property", params: ["gameObjectName", "propertyName", "value"] },
  },
  editor: {
    play: { cmd: "EnterPlayMode", desc: "Enter play mode", params: [] },
    stop: { cmd: "ExitPlayMode", desc: "Exit play mode", params: [] },
    pause: { cmd: "PauseEditor", desc: "Pause editor", params: ["paused"] },
    setTimeScale: { cmd: "SetTimeScale", desc: "Set time scale", params: ["timeScale"] },
    getTimeScale: { cmd: "GetTimeScale", desc: "Get time scale", params: [] },
    build: { cmd: "BuildProject", desc: "Build project", params: ["outputPath"], optional: ["scenes"] },
    getBuildTarget: { cmd: "GetBuildTarget", desc: "Get build target", params: [] },
    setQuality: { cmd: "SetQualityLevel", desc: "Set quality", params: ["level"] },
    setPref: { cmd: "SetEditorPref", desc: "Set preference", params: ["key", "value"] },
    getPref: { cmd: "GetEditorPref", desc: "Get preference", params: ["key"] },
  },
  script: {
    create: { cmd: "CreateScript", desc: "Create C# script", params: ["scriptName", "scriptContent"], optional: ["path"] },
  },
  reflection: {
    invoke: { cmd: "InvokeMethod", desc: "Invoke method", params: ["gameObjectName", "componentType", "methodName"] },
    getField: { cmd: "GetFieldValue", desc: "Get field", params: ["gameObjectName", "componentType", "fieldName"] },
    setField: { cmd: "SetFieldValue", desc: "Set field", params: ["gameObjectName", "componentType", "fieldName", "value"] },
  },
  debug: {
    log: { cmd: "LogMessage", desc: "Log message", params: ["message"] },
    clear: { cmd: "ClearConsole", desc: "Clear console", params: [] },
    screenshot: { cmd: "CaptureScreenshot", desc: "Screenshot", params: ["filePath"] },
    getErrors: { cmd: "GetConsoleErrors", desc: "Get console errors", params: [], optional: ["limit"] },
    getWarnings: { cmd: "GetConsoleWarnings", desc: "Get console warnings", params: [], optional: ["limit"] },
    getLogs: { cmd: "GetConsoleLogs", desc: "Get all console logs", params: [], optional: ["limit", "type"] },
  },
  info: {
    project: { cmd: "GetProjectInfo", desc: "Project info", params: [] },
    system: { cmd: "GetSystemInfo", desc: "System info", params: [] },
  },
  terrain: {
    create: { cmd: "CreateTerrain", desc: "Create terrain", params: [] },
    setHeight: { cmd: "SetTerrainHeight", desc: "Set height", params: ["terrainName", "x", "z", "height"] },
  },
  mesh: {
    getInfo: { cmd: "GetMeshInfo", desc: "Get mesh info", params: ["gameObjectName"] },
    combine: { cmd: "CombineMeshes", desc: "Combine meshes", params: ["gameObjectNames", "targetName"] },
  },
  light: {
    setProperty: { cmd: "SetLightProperty", desc: "Set light", params: ["gameObjectName", "propertyName", "value"] },
  },
  camera: {
    setProperty: { cmd: "SetCameraProperty", desc: "Set camera", params: ["gameObjectName", "propertyName", "value"] },
  },
  // HIGH-LEVEL GAME GENERATION
  game: {
    generate: { cmd: "GenerateGame", desc: "Generate game", params: ["gameName", "systems"], optional: ["gameType", "createUI", "createPlayer"] },
    setupManager: { cmd: "SetupGameManager", desc: "Setup manager", params: ["systems"] },
    setupPlayer: { cmd: "SetupPlayer", desc: "Setup player", params: ["systems"], optional: ["playerType", "createModel"] },
    setupUI: { cmd: "SetupGameUI", desc: "Setup UI", params: ["systems"], optional: ["uiStyle"] },
    wireSystems: { cmd: "WireSystems", desc: "Wire systems", params: ["connections"] },
    importVault: { cmd: "ImportVaultSystem", desc: "Import Vault", params: ["systemId", "systemPath"], optional: ["targetPath", "namespace"] },
    createEnemy: { cmd: "CreateEnemy", desc: "Create enemy", params: ["enemyName", "systems"], optional: ["enemyType", "createModel"] },
    setupScene: { cmd: "SetupSceneStructure", desc: "Setup scene", params: [], optional: ["structure"] },
  },
};

// ============================================================================
// Unity Bridge Client
// ============================================================================

class UnityBridgeClient {
  private client: net.Socket | null = null;
  private connected = false;
  private responseBuffer = "";

  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.client = new net.Socket();

      const timeout = setTimeout(() => {
        this.client?.destroy();
        reject(new Error("Connection timeout"));
      }, CONNECTION_TIMEOUT);

      this.client.connect(UNITY_BRIDGE_PORT, UNITY_BRIDGE_HOST, () => {
        clearTimeout(timeout);
        this.connected = true;
        resolve();
      });

      this.client.on("error", (err) => {
        clearTimeout(timeout);
        reject(new Error(`Bridge error: ${err.message}`));
      });

      this.client.on("close", () => {
        this.connected = false;
      });
    });
  }

  async sendCommand(command: string, params: any): Promise<any> {
    if (!this.connected || !this.client) {
      await this.connect();
    }

    return new Promise((resolve, reject) => {
      const request = JSON.stringify({ command, params }) + "\n";

      const onData = (data: Buffer) => {
        this.responseBuffer += data.toString();

        if (this.responseBuffer.endsWith("\n")) {
          try {
            const response = JSON.parse(this.responseBuffer.trim());
            this.responseBuffer = "";
            this.client?.off("data", onData);

            if (response.error) {
              reject(new Error(response.error));
            } else {
              resolve(response.result);
            }
          } catch (err) {
            reject(new Error(`Parse error: ${err}`));
          }
        }
      };

      this.client?.on("data", onData);
      this.client?.write(request);

      setTimeout(() => {
        this.client?.off("data", onData);
        reject(new Error("Response timeout"));
      }, 10000);
    });
  }

  disconnect(): void {
    if (this.client) {
      this.client.destroy();
      this.connected = false;
    }
  }
}

const unityBridge = new UnityBridgeClient();

// ============================================================================
// Response Compression
// ============================================================================

function compressResponse(result: any): string {
  if (typeof result === "string") {
    if (result.length > 500) {
      return result.substring(0, 500) + "...[truncated]";
    }
    return result;
  }

  if (Array.isArray(result)) {
    if (result.length > 20) {
      return JSON.stringify({
        count: result.length,
        first20: result.slice(0, 20),
        note: `${result.length - 20} more items...`
      });
    }
  }

  const cleaned = JSON.parse(JSON.stringify(result, (k, v) => v ?? undefined));
  return JSON.stringify(cleaned, null, 1);
}

// ============================================================================
// MCP Server with 3 Meta Tools
// ============================================================================

const server = new Server(
  { name: "unity-god-mode", version: "3.0.0" },
  { capabilities: { tools: {} } }
);

// 3 META TOOLS - Progressive Disclosure Pattern
const META_TOOLS = [
  {
    name: "unity_discover",
    description: "List Unity commands by category",
    inputSchema: {
      type: "object",
      properties: {
        category: {
          type: "string",
          description: "Category: gameobject, transform, component, scene, prefab, asset, material, physics, animation, audio, ui, particles, editor, script, reflection, debug, info, terrain, mesh, light, camera, game. Omit to list all."
        }
      }
    }
  },
  {
    name: "unity_do",
    description: "Execute Unity command(s). Single or batch mode.",
    inputSchema: {
      type: "object",
      properties: {
        category: { type: "string", description: "Category (single mode)" },
        action: { type: "string", description: "Action (single mode)" },
        params: { type: "object", description: "Parameters (single mode)" },
        batch: {
          type: "array",
          description: "Batch mode: array of {category, action, params}",
          items: {
            type: "object",
            properties: {
              category: { type: "string" },
              action: { type: "string" },
              params: { type: "object" }
            },
            required: ["category", "action"]
          }
        }
      }
    }
  },
  {
    name: "unity_ask",
    description: "Query Unity state",
    inputSchema: {
      type: "object",
      properties: {
        query: {
          type: "string",
          enum: ["hierarchy", "scene", "project", "system", "components", "assets"],
          description: "Query type"
        },
        target: { type: "string", description: "Target (for components)" },
        search: { type: "string", description: "Search (for assets)" }
      },
      required: ["query"]
    }
  }
];

// List Tools Handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return { tools: META_TOOLS };
});

// Call Tool Handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    let result: any;

    switch (name) {
      // ========================================
      // DISCOVER - List available commands
      // ========================================
      case "unity_discover": {
        const category = (args as any)?.category;

        if (!category) {
          // List all categories with command counts
          const categories = Object.entries(COMMAND_REGISTRY).map(([cat, cmds]) => ({
            category: cat,
            commands: Object.keys(cmds).length,
            examples: Object.keys(cmds).slice(0, 3).join(", ")
          }));
          const total = Object.values(COMMAND_REGISTRY).reduce((a, c) => a + Object.keys(c).length, 0);
          result = { categories, total };
        } else {
          // List commands in category
          const commands = COMMAND_REGISTRY[category];
          if (!commands) {
            throw new Error(`Unknown category: ${category}. Available: ${Object.keys(COMMAND_REGISTRY).join(", ")}`);
          }
          result = Object.entries(commands).map(([action, def]) => ({
            action,
            desc: def.desc,
            required: def.params,
            optional: def.optional || []
          }));
        }
        break;
      }

      // ========================================
      // DO - Execute any command (single or batch)
      // ========================================
      case "unity_do": {
        const { category, action, params = {}, batch } = args as any;

        // BATCH MODE
        if (batch && Array.isArray(batch)) {
          const results: Array<{ ok: boolean; result?: any; error?: string }> = [];

          for (const op of batch) {
            try {
              const catCmds = COMMAND_REGISTRY[op.category];
              if (!catCmds) throw new Error(`Unknown category: ${op.category}`);

              const cmdDef = catCmds[op.action];
              if (!cmdDef) throw new Error(`Unknown action: ${op.action}`);

              // Validate required params
              for (const param of cmdDef.params) {
                if ((op.params || {})[param] === undefined) {
                  throw new Error(`Missing: ${param}`);
                }
              }

              const r = await unityBridge.sendCommand(cmdDef.cmd, op.params || {});
              results.push({ ok: true, result: r });
            } catch (err) {
              results.push({ ok: false, error: err instanceof Error ? err.message : String(err) });
            }
          }

          const success = results.filter(r => r.ok).length;
          result = {
            batch: true,
            total: batch.length,
            success,
            failed: batch.length - success,
            results
          };
          break;
        }

        // SINGLE MODE
        if (!category || !action) {
          throw new Error("Provide category+action for single mode, or batch array for batch mode");
        }

        const categoryCommands = COMMAND_REGISTRY[category];
        if (!categoryCommands) {
          throw new Error(`Unknown category: ${category}`);
        }

        const cmdDef = categoryCommands[action];
        if (!cmdDef) {
          throw new Error(`Unknown action: ${action}. Available: ${Object.keys(categoryCommands).join(", ")}`);
        }

        // Validate required params
        for (const param of cmdDef.params) {
          if (params[param] === undefined) {
            throw new Error(`Missing: ${param}. Required: ${cmdDef.params.join(", ")}`);
          }
        }

        result = await unityBridge.sendCommand(cmdDef.cmd, params);
        break;
      }

      // ========================================
      // ASK - Query Unity state
      // ========================================
      case "unity_ask": {
        const { query, target, search } = args as any;

        switch (query) {
          case "hierarchy":
            result = await unityBridge.sendCommand("GetHierarchy", { rootOnly: false });
            break;
          case "scene":
            result = await unityBridge.sendCommand("GetActiveScene", {});
            break;
          case "project":
            result = await unityBridge.sendCommand("GetProjectInfo", {});
            break;
          case "system":
            result = await unityBridge.sendCommand("GetSystemInfo", {});
            break;
          case "components":
            if (!target) throw new Error("'target' required for components query");
            result = await unityBridge.sendCommand("GetAllComponents", { gameObjectName: target });
            break;
          case "assets":
            result = await unityBridge.sendCommand("FindAssets", { searchQuery: search || "t:Object" });
            break;
          default:
            throw new Error(`Unknown query: ${query}`);
        }
        break;
      }

      default:
        throw new Error(`Unknown tool: ${name}`);
    }

    return {
      content: [{
        type: "text",
        text: compressResponse(result)
      }]
    };

  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    return {
      content: [{ type: "text", text: `Error: ${errorMessage}` }],
      isError: true
    };
  }
});

// ============================================================================
// Start Server
// ============================================================================

async function main() {
  console.error("Unity God Mode MCP Server v3.0 (Optimized)");
  const cmdCount = Object.values(COMMAND_REGISTRY).reduce((a, c) => a + Object.keys(c).length, 0);
  console.error(`3 meta tools, ${cmdCount} commands, ${Object.keys(COMMAND_REGISTRY).length} categories`);

  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error("Ready. Waiting for Unity Bridge...");
}

process.on("SIGINT", () => {
  unityBridge.disconnect();
  process.exit(0);
});

main().catch((error) => {
  console.error("Fatal:", error);
  process.exit(1);
});
