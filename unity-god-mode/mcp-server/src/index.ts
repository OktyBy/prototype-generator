#!/usr/bin/env node

/**
 * Unity God Mode MCP Server
 *
 * This MCP server provides Claude Code with direct control over Unity Editor
 * through a TCP socket connection to a Unity C# bridge plugin.
 *
 * Architecture:
 * Claude Code <-> MCP Server (this) <-> TCP Socket <-> Unity Editor Bridge (C#)
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import * as net from "net";

// Configuration
const UNITY_BRIDGE_HOST = "127.0.0.1";
const UNITY_BRIDGE_PORT = 7777;
const CONNECTION_TIMEOUT = 5000;

// Unity Bridge Client
class UnityBridgeClient {
  private client: net.Socket | null = null;
  private connected = false;
  private responseBuffer = "";

  async connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.client = new net.Socket();

      const timeout = setTimeout(() => {
        this.client?.destroy();
        reject(new Error("Connection timeout. Is Unity Editor running with the bridge plugin?"));
      }, CONNECTION_TIMEOUT);

      this.client.connect(UNITY_BRIDGE_PORT, UNITY_BRIDGE_HOST, () => {
        clearTimeout(timeout);
        this.connected = true;
        console.error("✅ Connected to Unity Editor Bridge");
        resolve();
      });

      this.client.on("error", (err) => {
        clearTimeout(timeout);
        reject(new Error(`Unity Bridge connection error: ${err.message}`));
      });

      this.client.on("close", () => {
        this.connected = false;
        console.error("⚠️ Unity Bridge connection closed");
      });
    });
  }

  async sendCommand(command: string, params: any): Promise<any> {
    if (!this.connected || !this.client) {
      try {
        await this.connect();
      } catch (error) {
        throw new Error(
          "Cannot connect to Unity Editor. Please ensure:\n" +
          "1. Unity Editor is running\n" +
          "2. Claude MCP Bridge plugin is installed\n" +
          "3. Bridge window is open (Window > Claude MCP Bridge)\n\n" +
          `Error: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    }

    return new Promise((resolve, reject) => {
      const request = JSON.stringify({ command, params }) + "\n";

      const onData = (data: Buffer) => {
        this.responseBuffer += data.toString();

        // Check if we have complete JSON response (ending with newline)
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
            reject(new Error(`Failed to parse Unity response: ${err}`));
          }
        }
      };

      this.client?.on("data", onData);
      this.client?.write(request);

      // Timeout for response
      setTimeout(() => {
        this.client?.off("data", onData);
        reject(new Error("Unity Bridge response timeout"));
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

// Initialize Unity Bridge
const unityBridge = new UnityBridgeClient();

// Initialize MCP Server
const server = new Server(
  {
    name: "unity-god-mode",
    version: "2.0.0",
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Common Zod Schemas
const Vector3Schema = z.object({ x: z.number(), y: z.number(), z: z.number() });
const GameObjectNameSchema = z.object({ gameObjectName: z.string() });
const EmptySchema = z.object({});

// Tool Definition Type
interface UnityTool {
  name: string;
  description: string;
  inputSchema: any;
  bridgeCommand: string;
}

// ALL 105 UNITY COMMANDS - Comprehensive Tool Definitions
const UNITY_TOOLS: UnityTool[] = [
  // Core GameObject Operations
  {
    name: "unity_create_gameobject",
    description: "Create a new GameObject in the current Unity scene",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string", description: "Name of the GameObject" },
        primitiveType: { type: "string", enum: ["Empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"], description: "Primitive type" },
        parent: { type: "string", description: "Parent GameObject name" },
      },
      required: ["name"],
    },
    bridgeCommand: "CreateGameObject",
  },
  {
    name: "unity_set_transform",
    description: "Set position, rotation, or scale of a GameObject",
    inputSchema: {
      type: "object",
      properties: {
        gameObjectName: { type: "string" },
        position: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
        rotation: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
        scale: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
      },
      required: ["gameObjectName"],
    },
    bridgeCommand: "SetTransform",
  },
  {
    name: "unity_delete_gameobject",
    description: "Delete a GameObject from the scene",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "DeleteGameObject",
  },
  {
    name: "unity_duplicate_gameobject",
    description: "Duplicate a GameObject in the scene",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "DuplicateGameObject",
  },
  {
    name: "unity_rename_gameobject",
    description: "Rename a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, newName: { type: "string" } }, required: ["gameObjectName", "newName"] },
    bridgeCommand: "RenameGameObject",
  },
  {
    name: "unity_set_active",
    description: "Enable or disable a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, active: { type: "boolean" } }, required: ["gameObjectName", "active"] },
    bridgeCommand: "SetActive",
  },
  {
    name: "unity_get_active_state",
    description: "Get the active state of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetActiveState",
  },
  {
    name: "unity_set_parent",
    description: "Set the parent of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, parentName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "SetParent",
  },
  {
    name: "unity_get_parent",
    description: "Get the parent of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetParent",
  },
  {
    name: "unity_get_children",
    description: "Get all children of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetChildren",
  },
  {
    name: "unity_set_layer",
    description: "Set the layer of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, layer: { type: "number" } }, required: ["gameObjectName", "layer"] },
    bridgeCommand: "SetLayer",
  },
  {
    name: "unity_set_tag",
    description: "Set the tag of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, tag: { type: "string" } }, required: ["gameObjectName", "tag"] },
    bridgeCommand: "SetTag",
  },
  {
    name: "unity_set_sibling_index",
    description: "Set the sibling index of a GameObject in hierarchy",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, index: { type: "number" } }, required: ["gameObjectName", "index"] },
    bridgeCommand: "SetSiblingIndex",
  },
  {
    name: "unity_get_sibling_index",
    description: "Get the sibling index of a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetSiblingIndex",
  },
  {
    name: "unity_select_gameobject",
    description: "Select a GameObject in the Unity Editor",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "SelectGameObject",
  },
  {
    name: "unity_focus_gameobject",
    description: "Focus the Scene view on a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "FocusGameObject",
  },
  {
    name: "unity_find_gameobjects_by_tag",
    description: "Find all GameObjects with a specific tag",
    inputSchema: { type: "object", properties: { tag: { type: "string" } }, required: ["tag"] },
    bridgeCommand: "FindGameObjectsByTag",
  },
  {
    name: "unity_find_gameobjects_by_layer",
    description: "Find all GameObjects on a specific layer",
    inputSchema: { type: "object", properties: { layer: { type: "number" } }, required: ["layer"] },
    bridgeCommand: "FindGameObjectsByLayer",
  },
  {
    name: "unity_find_gameobjects_with_component",
    description: "Find all GameObjects with a specific component",
    inputSchema: { type: "object", properties: { componentType: { type: "string" } }, required: ["componentType"] },
    bridgeCommand: "FindGameObjectsWithComponent",
  },
  {
    name: "unity_batch_create_gameobjects",
    description: "Create multiple GameObjects at once with transforms",
    inputSchema: {
      type: "object",
      properties: {
        gameObjects: {
          type: "array",
          items: {
            type: "object",
            properties: {
              name: { type: "string" },
              primitiveType: { type: "string", enum: ["Empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad"] },
              position: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
              rotation: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
              scale: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
              parent: { type: "string" },
            },
            required: ["name"],
          },
        },
      },
      required: ["gameObjects"],
    },
    bridgeCommand: "BatchCreateGameObjects",
  },

  // Component Operations
  {
    name: "unity_add_component",
    description: "Add a component to a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" } }, required: ["gameObjectName", "componentType"] },
    bridgeCommand: "AddComponent",
  },
  {
    name: "unity_remove_component",
    description: "Remove a component from a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" } }, required: ["gameObjectName", "componentType"] },
    bridgeCommand: "RemoveComponent",
  },
  {
    name: "unity_get_all_components",
    description: "Get all components on a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetAllComponents",
  },
  {
    name: "unity_has_component",
    description: "Check if a GameObject has a specific component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" } }, required: ["gameObjectName", "componentType"] },
    bridgeCommand: "HasComponent",
  },
  {
    name: "unity_copy_component",
    description: "Copy a component from one GameObject to another",
    inputSchema: { type: "object", properties: { sourceGameObject: { type: "string" }, targetGameObject: { type: "string" }, componentType: { type: "string" } }, required: ["sourceGameObject", "targetGameObject", "componentType"] },
    bridgeCommand: "CopyComponent",
  },
  {
    name: "unity_set_component_property",
    description: "Set a property or field on a component using reflection",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" }, valueType: { type: "string" } }, required: ["gameObjectName", "componentType", "propertyName", "value", "valueType"] },
    bridgeCommand: "SetComponentProperty",
  },
  {
    name: "unity_get_component_property",
    description: "Get a property or field value from a component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" }, propertyName: { type: "string" } }, required: ["gameObjectName", "componentType", "propertyName"] },
    bridgeCommand: "GetComponentProperty",
  },
  {
    name: "unity_send_message",
    description: "Send a message to a GameObject (call a method)",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, methodName: { type: "string" } }, required: ["gameObjectName", "methodName"] },
    bridgeCommand: "SendMessage",
  },

  // Scene Management
  {
    name: "unity_create_scene",
    description: "Create a new Unity scene",
    inputSchema: { type: "object", properties: { sceneName: { type: "string" }, additive: { type: "boolean" } }, required: ["sceneName"] },
    bridgeCommand: "CreateScene",
  },
  {
    name: "unity_save_scene",
    description: "Save the current Unity scene",
    inputSchema: { type: "object", properties: { path: { type: "string" } } },
    bridgeCommand: "SaveScene",
  },
  {
    name: "unity_load_scene",
    description: "Load a Unity scene",
    inputSchema: { type: "object", properties: { sceneName: { type: "string" } }, required: ["sceneName"] },
    bridgeCommand: "LoadScene",
  },
  {
    name: "unity_list_scenes",
    description: "List all scenes in the Unity project",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "ListScenes",
  },
  {
    name: "unity_get_active_scene",
    description: "Get the name of the currently active scene",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetActiveScene",
  },
  {
    name: "unity_get_all_scenes",
    description: "Get all loaded scenes",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetAllScenes",
  },
  {
    name: "unity_get_hierarchy",
    description: "Get the current scene hierarchy (all GameObjects)",
    inputSchema: { type: "object", properties: { rootOnly: { type: "boolean" } } },
    bridgeCommand: "GetHierarchy",
  },

  // Prefab Operations
  {
    name: "unity_create_prefab",
    description: "Create a prefab from a GameObject",
    inputSchema: { type: "object", properties: { sourceGameObjectName: { type: "string" }, prefabPath: { type: "string" } }, required: ["sourceGameObjectName", "prefabPath"] },
    bridgeCommand: "CreatePrefab",
  },
  {
    name: "unity_instantiate_prefab",
    description: "Instantiate a prefab into the scene",
    inputSchema: { type: "object", properties: { prefabPath: { type: "string" } }, required: ["prefabPath"] },
    bridgeCommand: "InstantiatePrefab",
  },
  {
    name: "unity_unpack_prefab",
    description: "Unpack a prefab instance completely",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "UnpackPrefab",
  },
  {
    name: "unity_apply_prefab_changes",
    description: "Apply changes from prefab instance to prefab asset",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "ApplyPrefabChanges",
  },

  // Asset Management
  {
    name: "unity_find_assets",
    description: "Search for assets in the project",
    inputSchema: { type: "object", properties: { searchQuery: { type: "string" } }, required: ["searchQuery"] },
    bridgeCommand: "FindAssets",
  },
  {
    name: "unity_create_folder",
    description: "Create a folder in the Assets directory",
    inputSchema: { type: "object", properties: { folderPath: { type: "string" } }, required: ["folderPath"] },
    bridgeCommand: "CreateFolder",
  },
  {
    name: "unity_delete_asset",
    description: "Delete an asset",
    inputSchema: { type: "object", properties: { assetPath: { type: "string" } }, required: ["assetPath"] },
    bridgeCommand: "DeleteAsset",
  },
  {
    name: "unity_move_asset",
    description: "Move an asset to a different location",
    inputSchema: { type: "object", properties: { oldPath: { type: "string" }, newPath: { type: "string" } }, required: ["oldPath", "newPath"] },
    bridgeCommand: "MoveAsset",
  },
  {
    name: "unity_duplicate_asset",
    description: "Duplicate an asset",
    inputSchema: { type: "object", properties: { assetPath: { type: "string" } }, required: ["assetPath"] },
    bridgeCommand: "DuplicateAsset",
  },
  {
    name: "unity_rename_asset",
    description: "Rename an asset",
    inputSchema: { type: "object", properties: { assetPath: { type: "string" }, newName: { type: "string" } }, required: ["assetPath", "newName"] },
    bridgeCommand: "RenameAsset",
  },
  {
    name: "unity_get_asset_dependencies",
    description: "Get all dependencies of an asset",
    inputSchema: { type: "object", properties: { assetPath: { type: "string" } }, required: ["assetPath"] },
    bridgeCommand: "GetAssetDependencies",
  },
  {
    name: "unity_refresh_asset_database",
    description: "Refresh the Unity Asset Database",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "RefreshAssetDatabase",
  },

  // Material & Rendering
  {
    name: "unity_create_material",
    description: "Create a new material asset",
    inputSchema: { type: "object", properties: { materialPath: { type: "string" } }, required: ["materialPath"] },
    bridgeCommand: "CreateMaterial",
  },
  {
    name: "unity_set_material",
    description: "Set a material on a GameObject's Renderer",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, materialPath: { type: "string" } }, required: ["gameObjectName", "materialPath"] },
    bridgeCommand: "SetMaterial",
  },
  {
    name: "unity_set_material_property",
    description: "Set a property on a material",
    inputSchema: { type: "object", properties: { materialPath: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["materialPath", "propertyName", "value"] },
    bridgeCommand: "SetMaterialProperty",
  },
  {
    name: "unity_set_shader",
    description: "Set the shader on a material",
    inputSchema: { type: "object", properties: { materialPath: { type: "string" }, shaderName: { type: "string" } }, required: ["materialPath", "shaderName"] },
    bridgeCommand: "SetShader",
  },
  {
    name: "unity_assign_texture",
    description: "Assign a texture to a material property",
    inputSchema: { type: "object", properties: { materialPath: { type: "string" }, propertyName: { type: "string" }, texturePath: { type: "string" } }, required: ["materialPath", "propertyName", "texturePath"] },
    bridgeCommand: "AssignTexture",
  },
  {
    name: "unity_set_light_property",
    description: "Set properties on a Light component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetLightProperty",
  },
  {
    name: "unity_set_camera_property",
    description: "Set properties on a Camera component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetCameraProperty",
  },

  // Physics
  {
    name: "unity_set_rigidbody_property",
    description: "Set properties on a Rigidbody component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetRigidbodyProperty",
  },
  {
    name: "unity_set_collider_property",
    description: "Set properties on a Collider component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetColliderProperty",
  },
  {
    name: "unity_add_force",
    description: "Add force to a Rigidbody",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, forceX: { type: "number" }, forceY: { type: "number" }, forceZ: { type: "number" } }, required: ["gameObjectName", "forceX", "forceY", "forceZ"] },
    bridgeCommand: "AddForce",
  },
  {
    name: "unity_set_gravity",
    description: "Set the global physics gravity",
    inputSchema: { type: "object", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } }, required: ["x", "y", "z"] },
    bridgeCommand: "SetGravity",
  },
  {
    name: "unity_get_gravity",
    description: "Get the current global physics gravity",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetGravity",
  },

  // Navigation
  {
    name: "unity_bake_navmesh",
    description: "Bake the NavMesh for AI navigation",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "BakeNavMesh",
  },

  // Terrain
  {
    name: "unity_create_terrain",
    description: "Create a new Terrain GameObject",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "CreateTerrain",
  },
  {
    name: "unity_set_terrain_height",
    description: "Set terrain height at a specific position",
    inputSchema: { type: "object", properties: { terrainName: { type: "string" }, x: { type: "number" }, z: { type: "number" }, height: { type: "number" } }, required: ["terrainName", "x", "z", "height"] },
    bridgeCommand: "SetTerrainHeight",
  },

  // Animation
  {
    name: "unity_create_animator",
    description: "Add an Animator component to a GameObject",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "CreateAnimator",
  },
  {
    name: "unity_set_animator_parameter",
    description: "Set an Animator parameter value",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, parameterName: { type: "string" }, value: { type: "string" }, valueType: { type: "string" } }, required: ["gameObjectName", "parameterName", "value", "valueType"] },
    bridgeCommand: "SetAnimatorParameter",
  },
  {
    name: "unity_create_animation_clip",
    description: "Create a new AnimationClip asset",
    inputSchema: { type: "object", properties: { clipPath: { type: "string" } }, required: ["clipPath"] },
    bridgeCommand: "CreateAnimationClip",
  },
  {
    name: "unity_create_animator_controller",
    description: "Create a new AnimatorController asset",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" } }, required: ["controllerPath"] },
    bridgeCommand: "CreateAnimatorController",
  },
  {
    name: "unity_add_animator_state",
    description: "Add a state to an AnimatorController",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, stateName: { type: "string" }, layerIndex: { type: "number" }, positionX: { type: "number" }, positionY: { type: "number" } }, required: ["controllerPath", "stateName"] },
    bridgeCommand: "AddAnimatorState",
  },
  {
    name: "unity_add_animator_transition",
    description: "Add a transition between animator states",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, sourceState: { type: "string" }, destState: { type: "string" }, hasExitTime: { type: "boolean" }, exitTime: { type: "number" }, duration: { type: "number" } }, required: ["controllerPath", "sourceState", "destState"] },
    bridgeCommand: "AddAnimatorTransition",
  },
  {
    name: "unity_add_animator_parameter",
    description: "Add a parameter to an AnimatorController (float, int, bool, trigger)",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, parameterName: { type: "string" }, parameterType: { type: "string", enum: ["float", "int", "bool", "trigger"] }, defaultValue: { type: "string" } }, required: ["controllerPath", "parameterName", "parameterType"] },
    bridgeCommand: "AddAnimatorParameter",
  },
  {
    name: "unity_set_animator_controller",
    description: "Assign an AnimatorController to a GameObject's Animator",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, controllerPath: { type: "string" } }, required: ["gameObjectName", "controllerPath"] },
    bridgeCommand: "SetAnimatorController",
  },
  {
    name: "unity_get_animator_parameters",
    description: "Get all parameters from an AnimatorController",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" } }, required: ["controllerPath"] },
    bridgeCommand: "GetAnimatorParameters",
  },
  {
    name: "unity_get_animator_states",
    description: "Get all states from an AnimatorController layer",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, layerIndex: { type: "number" } }, required: ["controllerPath"] },
    bridgeCommand: "GetAnimatorStates",
  },
  {
    name: "unity_add_blend_tree",
    description: "Create a BlendTree in an AnimatorController",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, stateName: { type: "string" }, blendParameter: { type: "string" }, blendType: { type: "string", enum: ["Simple1D", "SimpleDirectional2D", "FreeformDirectional2D", "FreeformCartesian2D"] } }, required: ["controllerPath", "stateName", "blendParameter"] },
    bridgeCommand: "AddBlendTree",
  },
  {
    name: "unity_set_state_motion",
    description: "Assign an AnimationClip to an animator state",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, stateName: { type: "string" }, clipPath: { type: "string" } }, required: ["controllerPath", "stateName", "clipPath"] },
    bridgeCommand: "SetStateMotion",
  },
  {
    name: "unity_add_animator_layer",
    description: "Add a new layer to an AnimatorController",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, layerName: { type: "string" }, weight: { type: "number" }, blendingMode: { type: "string", enum: ["Override", "Additive"] } }, required: ["controllerPath", "layerName"] },
    bridgeCommand: "AddAnimatorLayer",
  },
  {
    name: "unity_set_transition_conditions",
    description: "Set conditions on an animator transition",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, sourceState: { type: "string" }, destState: { type: "string" }, conditions: { type: "array", items: { type: "object", properties: { parameter: { type: "string" }, mode: { type: "string" }, threshold: { type: "number" } } } } }, required: ["controllerPath", "sourceState", "destState", "conditions"] },
    bridgeCommand: "SetTransitionConditions",
  },
  {
    name: "unity_create_sub_state_machine",
    description: "Create a sub-state machine in an AnimatorController",
    inputSchema: { type: "object", properties: { controllerPath: { type: "string" }, name: { type: "string" }, layerIndex: { type: "number" }, positionX: { type: "number" }, positionY: { type: "number" } }, required: ["controllerPath", "name"] },
    bridgeCommand: "CreateSubStateMachine",
  },
  {
    name: "unity_get_current_animator_state",
    description: "Get the current state of a runtime Animator",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, layerIndex: { type: "number" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetCurrentAnimatorState",
  },
  {
    name: "unity_play_animator_state",
    description: "Play a specific animator state",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, stateName: { type: "string" }, layerIndex: { type: "number" }, normalizedTime: { type: "number" } }, required: ["gameObjectName", "stateName"] },
    bridgeCommand: "PlayAnimatorState",
  },
  {
    name: "unity_set_animator_speed",
    description: "Set the speed of an Animator",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, speed: { type: "number" } }, required: ["gameObjectName", "speed"] },
    bridgeCommand: "SetAnimatorSpeed",
  },
  {
    name: "unity_get_animator_clip_info",
    description: "Get information about currently playing clips",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, layerIndex: { type: "number" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetAnimatorClipInfo",
  },
  {
    name: "unity_crossfade_animator",
    description: "Crossfade to a target state",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, stateName: { type: "string" }, transitionDuration: { type: "number" }, layerIndex: { type: "number" }, normalizedTime: { type: "number" } }, required: ["gameObjectName", "stateName", "transitionDuration"] },
    bridgeCommand: "CrossfadeAnimator",
  },
  {
    name: "unity_set_avatar",
    description: "Set the Avatar on an Animator",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, avatarPath: { type: "string" } }, required: ["gameObjectName", "avatarPath"] },
    bridgeCommand: "SetAvatar",
  },
  {
    name: "unity_set_root_motion",
    description: "Enable or disable root motion on an Animator",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, enabled: { type: "boolean" } }, required: ["gameObjectName", "enabled"] },
    bridgeCommand: "SetRootMotion",
  },

  // Audio
  {
    name: "unity_play_audio",
    description: "Play audio from an AudioSource component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "PlayAudio",
  },
  {
    name: "unity_stop_audio",
    description: "Stop audio on an AudioSource component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "StopAudio",
  },
  {
    name: "unity_set_audio_property",
    description: "Set properties on an AudioSource component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetAudioProperty",
  },
  {
    name: "unity_set_audio_clip",
    description: "Set the AudioClip on an AudioSource",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, clipPath: { type: "string" } }, required: ["gameObjectName", "clipPath"] },
    bridgeCommand: "SetAudioClip",
  },

  // UI
  {
    name: "unity_create_canvas",
    description: "Create a Canvas for UI elements",
    inputSchema: { type: "object", properties: { name: { type: "string" } } },
    bridgeCommand: "CreateCanvas",
  },
  {
    name: "unity_create_button",
    description: "Create a UI Button",
    inputSchema: { type: "object", properties: { name: { type: "string" }, parentName: { type: "string" } }, required: ["name"] },
    bridgeCommand: "CreateButton",
  },
  {
    name: "unity_create_image",
    description: "Create a UI Image",
    inputSchema: { type: "object", properties: { name: { type: "string" }, parentName: { type: "string" } }, required: ["name"] },
    bridgeCommand: "CreateImage",
  },
  {
    name: "unity_create_panel",
    description: "Create a UI Panel",
    inputSchema: { type: "object", properties: { name: { type: "string" }, parentName: { type: "string" } }, required: ["name"] },
    bridgeCommand: "CreatePanel",
  },
  {
    name: "unity_set_text",
    description: "Set text on a Text or TextMeshPro component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, text: { type: "string" } }, required: ["gameObjectName", "text"] },
    bridgeCommand: "SetText",
  },
  {
    name: "unity_set_rect_transform",
    description: "Set properties on a RectTransform component",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetRectTransform",
  },

  // Particles
  {
    name: "unity_create_particle_system",
    description: "Create a ParticleSystem GameObject",
    inputSchema: { type: "object", properties: { name: { type: "string" } }, required: ["name"] },
    bridgeCommand: "CreateParticleSystem",
  },
  {
    name: "unity_set_particle_property",
    description: "Set properties on a ParticleSystem",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, propertyName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "propertyName", "value"] },
    bridgeCommand: "SetParticleProperty",
  },

  // Mesh Operations
  {
    name: "unity_get_mesh_info",
    description: "Get information about a mesh (vertex count, triangle count)",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" } }, required: ["gameObjectName"] },
    bridgeCommand: "GetMeshInfo",
  },
  {
    name: "unity_combine_meshes",
    description: "Combine multiple meshes into one",
    inputSchema: { type: "object", properties: { gameObjectNames: { type: "array", items: { type: "string" } }, targetName: { type: "string" } }, required: ["gameObjectNames", "targetName"] },
    bridgeCommand: "CombineMeshes",
  },

  // Play Mode & Editor Control
  {
    name: "unity_enter_play_mode",
    description: "Enter Play Mode in the Unity Editor",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "EnterPlayMode",
  },
  {
    name: "unity_exit_play_mode",
    description: "Exit Play Mode in the Unity Editor",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "ExitPlayMode",
  },
  {
    name: "unity_pause_editor",
    description: "Pause or unpause the Unity Editor",
    inputSchema: { type: "object", properties: { paused: { type: "boolean" } }, required: ["paused"] },
    bridgeCommand: "PauseEditor",
  },
  {
    name: "unity_set_time_scale",
    description: "Set the time scale (speed of time in game)",
    inputSchema: { type: "object", properties: { timeScale: { type: "number" } }, required: ["timeScale"] },
    bridgeCommand: "SetTimeScale",
  },
  {
    name: "unity_get_time_scale",
    description: "Get the current time scale",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetTimeScale",
  },

  // Build & Settings
  {
    name: "unity_build_project",
    description: "Build the Unity project",
    inputSchema: { type: "object", properties: { outputPath: { type: "string" }, scenes: { type: "array", items: { type: "string" } } }, required: ["outputPath"] },
    bridgeCommand: "BuildProject",
  },
  {
    name: "unity_get_build_target",
    description: "Get the current build target platform",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetBuildTarget",
  },
  {
    name: "unity_set_quality_level",
    description: "Set the quality level",
    inputSchema: { type: "object", properties: { level: { type: "number" } }, required: ["level"] },
    bridgeCommand: "SetQualityLevel",
  },
  {
    name: "unity_set_editor_pref",
    description: "Set an Editor preference value",
    inputSchema: { type: "object", properties: { key: { type: "string" }, value: { type: "string" } }, required: ["key", "value"] },
    bridgeCommand: "SetEditorPref",
  },
  {
    name: "unity_get_editor_pref",
    description: "Get an Editor preference value",
    inputSchema: { type: "object", properties: { key: { type: "string" } }, required: ["key"] },
    bridgeCommand: "GetEditorPref",
  },

  // Script & Code
  {
    name: "unity_create_script",
    description: "Create a new C# script in the Unity project",
    inputSchema: { type: "object", properties: { scriptName: { type: "string" }, scriptContent: { type: "string" }, path: { type: "string" } }, required: ["scriptName", "scriptContent"] },
    bridgeCommand: "CreateScript",
  },

  // Reflection & Advanced
  {
    name: "unity_invoke_method",
    description: "Invoke a method on a component using reflection",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" }, methodName: { type: "string" } }, required: ["gameObjectName", "componentType", "methodName"] },
    bridgeCommand: "InvokeMethod",
  },
  {
    name: "unity_get_field_value",
    description: "Get a field value from a component using reflection",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" }, fieldName: { type: "string" } }, required: ["gameObjectName", "componentType", "fieldName"] },
    bridgeCommand: "GetFieldValue",
  },
  {
    name: "unity_set_field_value",
    description: "Set a field value on a component using reflection",
    inputSchema: { type: "object", properties: { gameObjectName: { type: "string" }, componentType: { type: "string" }, fieldName: { type: "string" }, value: { type: "string" } }, required: ["gameObjectName", "componentType", "fieldName", "value"] },
    bridgeCommand: "SetFieldValue",
  },

  // Debug & Logging
  {
    name: "unity_log_message",
    description: "Log a message to the Unity Console",
    inputSchema: { type: "object", properties: { message: { type: "string" } }, required: ["message"] },
    bridgeCommand: "LogMessage",
  },
  {
    name: "unity_clear_console",
    description: "Clear the Unity Console",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "ClearConsole",
  },
  {
    name: "unity_capture_screenshot",
    description: "Capture a screenshot from the Game view",
    inputSchema: { type: "object", properties: { filePath: { type: "string" } }, required: ["filePath"] },
    bridgeCommand: "CaptureScreenshot",
  },

  // System Info
  {
    name: "unity_get_project_info",
    description: "Get Unity project information (version, name, path, etc.)",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetProjectInfo",
  },
  {
    name: "unity_get_system_info",
    description: "Get system information (OS, GPU, CPU, etc.)",
    inputSchema: { type: "object", properties: {} },
    bridgeCommand: "GetSystemInfo",
  },

  // HIGH-LEVEL GAME GENERATION COMMANDS (Lazy-Bird)
  {
    name: "unity_generate_game",
    description: "Generate a complete game setup with selected systems. Creates scene, GameManager, Player, UI Canvas, and wires systems together.",
    inputSchema: {
      type: "object",
      properties: {
        gameName: { type: "string", description: "Name of the game/project" },
        gameType: { type: "string", enum: ["3D Action", "3D RPG", "2D Platformer", "2D TopDown", "FPS", "TPS"], description: "Type of game to generate" },
        systems: {
          type: "array",
          items: { type: "string" },
          description: "List of system IDs to include (e.g., ['health_system', 'inventory_system'])"
        },
        createUI: { type: "boolean", description: "Whether to create UI Canvas with relevant UI elements" },
        createPlayer: { type: "boolean", description: "Whether to create a Player GameObject with systems attached" }
      },
      required: ["gameName", "systems"]
    },
    bridgeCommand: "GenerateGame",
  },
  {
    name: "unity_setup_game_manager",
    description: "Create a GameManager GameObject with specified core systems attached",
    inputSchema: {
      type: "object",
      properties: {
        systems: {
          type: "array",
          items: { type: "string" },
          description: "List of system scripts to attach (e.g., ['GameStateManager', 'SaveManager', 'AudioManager'])"
        }
      },
      required: ["systems"]
    },
    bridgeCommand: "SetupGameManager",
  },
  {
    name: "unity_setup_player",
    description: "Create a Player GameObject with specified systems and components attached",
    inputSchema: {
      type: "object",
      properties: {
        playerType: { type: "string", enum: ["3D", "2D", "FirstPerson", "ThirdPerson", "TopDown"], description: "Type of player controller" },
        systems: {
          type: "array",
          items: { type: "string" },
          description: "List of system scripts to attach (e.g., ['HealthSystem', 'ManaSystem', 'InventorySystem'])"
        },
        createModel: { type: "boolean", description: "Whether to create a basic visual model (capsule/sprite)" }
      },
      required: ["systems"]
    },
    bridgeCommand: "SetupPlayer",
  },
  {
    name: "unity_setup_game_ui",
    description: "Create a UI Canvas with elements based on the systems used (e.g., health bar for HealthSystem)",
    inputSchema: {
      type: "object",
      properties: {
        systems: {
          type: "array",
          items: { type: "string" },
          description: "List of systems that need UI (e.g., ['HealthSystem', 'ManaSystem', 'InventorySystem'])"
        },
        uiStyle: { type: "string", enum: ["Minimal", "RPG", "Action", "Retro"], description: "Visual style for UI elements" }
      },
      required: ["systems"]
    },
    bridgeCommand: "SetupGameUI",
  },
  {
    name: "unity_wire_systems",
    description: "Connect systems together with event subscriptions and references",
    inputSchema: {
      type: "object",
      properties: {
        connections: {
          type: "array",
          items: {
            type: "object",
            properties: {
              source: { type: "string", description: "Source system (e.g., 'HealthSystem')" },
              target: { type: "string", description: "Target system (e.g., 'HealthBarUI')" },
              eventName: { type: "string", description: "Event to subscribe to (e.g., 'OnHealthChanged')" }
            }
          },
          description: "List of system connections to establish"
        }
      },
      required: ["connections"]
    },
    bridgeCommand: "WireSystems",
  },
  {
    name: "unity_import_vault_system",
    description: "Import a system from UnityVault library into the current project",
    inputSchema: {
      type: "object",
      properties: {
        systemId: { type: "string", description: "System ID from UnityVault catalog (e.g., 'health_system')" },
        systemPath: { type: "string", description: "Path to the system files in the library" },
        targetPath: { type: "string", description: "Target path in Unity project (default: Assets/Scripts/Systems)" },
        namespace: { type: "string", description: "Namespace to use for the scripts (optional)" }
      },
      required: ["systemId", "systemPath"]
    },
    bridgeCommand: "ImportVaultSystem",
  },
  {
    name: "unity_create_enemy",
    description: "Create an enemy GameObject with AI and combat systems",
    inputSchema: {
      type: "object",
      properties: {
        enemyName: { type: "string", description: "Name of the enemy" },
        enemyType: { type: "string", enum: ["Melee", "Ranged", "Boss", "Patrol"], description: "Type of enemy behavior" },
        systems: {
          type: "array",
          items: { type: "string" },
          description: "List of system scripts to attach (e.g., ['HealthSystem', 'AIStateMachine', 'MeleeCombat'])"
        },
        createModel: { type: "boolean", description: "Whether to create a basic visual model" }
      },
      required: ["enemyName", "systems"]
    },
    bridgeCommand: "CreateEnemy",
  },
  {
    name: "unity_setup_scene_structure",
    description: "Create a standard scene hierarchy structure with folders for organization",
    inputSchema: {
      type: "object",
      properties: {
        structure: {
          type: "array",
          items: { type: "string" },
          description: "List of root GameObjects to create (e.g., ['--- MANAGERS ---', '--- PLAYER ---', '--- ENEMIES ---', '--- ENVIRONMENT ---', '--- UI ---'])"
        }
      }
    },
    bridgeCommand: "SetupSceneStructure",
  },
];

// List Tools Handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: UNITY_TOOLS.map(tool => ({
      name: tool.name,
      description: tool.description,
      inputSchema: tool.inputSchema,
    })),
  };
});

// Call Tool Handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    // Find the tool definition
    const tool = UNITY_TOOLS.find(t => t.name === name);
    if (!tool) {
      throw new Error(`Unknown tool: ${name}`);
    }

    // Send command to Unity Bridge
    const result = await unityBridge.sendCommand(tool.bridgeCommand, args || {});

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify(result, null, 2),
        },
      ],
    };
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    return {
      content: [
        {
          type: "text",
          text: `Error: ${errorMessage}`,
        },
      ],
      isError: true,
    };
  }
});

// Start Server
async function main() {
  console.error("🎮 Unity God Mode MCP Server v2.0 starting...");
  console.error(`📦 Loaded ${UNITY_TOOLS.length} Unity commands`);

  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error("✅ MCP Server ready. Waiting for Unity Bridge connection...");
  console.error("💡 Make sure Unity Editor is running with Claude MCP Bridge plugin");
}

// Cleanup
process.on("SIGINT", () => {
  console.error("\n🛑 Shutting down Unity God Mode MCP Server...");
  unityBridge.disconnect();
  process.exit(0);
});

main().catch((error) => {
  console.error("❌ Fatal error:", error);
  process.exit(1);
});
