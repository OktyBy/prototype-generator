# Unity 6 Core Skill

## Overview

This skill provides comprehensive knowledge about Unity 6 (Unity 6000.x) core functionality, including the GameObject/Component architecture, Transform system, Scene management, and fundamental Unity Editor workflows.

## Capabilities

When this skill is active, I have deep knowledge of:

- **GameObject & Component System**: Creating, manipulating, and organizing GameObjects
- **Transform System**: Position, rotation, scale operations in world and local space
- **Scene Management**: Creating, loading, and managing Unity scenes
- **Prefab System**: Prefab creation, instantiation, and variants
- **Unity Editor API**: Editor scripting and automation
- **Asset Pipeline**: Asset import, management, and organization
- **C# Scripting**: MonoBehaviour lifecycle, coroutines, and Unity-specific C# patterns
- **Input System**: Legacy and new Input System
- **Time and Delta Time**: Frame-independent operations
- **Tags and Layers**: Organizing and filtering GameObjects

## Unity 6 Specific Features

Unity 6 (released October 2024) includes these major improvements:

### RenderGraph System
- Automated GPU resource management
- Improved rendering performance
- Better memory handling

### CoreCLR Migration (In Progress)
- Significant runtime performance improvements
- Better debugging capabilities
- Modern .NET features

### Enhanced Editor APIs
- `ExecuteAlways` attribute (replaces `ExecuteInEditMode`)
- Improved EditorSceneManager
- Enhanced AssetDatabase operations

### GPU Resident Drawer
- Reduced CPU overhead
- Better batching
- Improved instancing

## Common Patterns

### GameObject Creation Pattern
```csharp
// Runtime
GameObject go = new GameObject("MyObject");
go.transform.position = new Vector3(0, 0, 0);

// Editor
GameObject go = new GameObject("MyObject");
Undo.RegisterCreatedObjectUndo(go, "Create GameObject");
```

### Component Access Pattern
```csharp
// Get component
Rigidbody rb = GetComponent<Rigidbody>();

// Try get component (safe)
if (TryGetComponent(out Rigidbody rb))
{
    // Use rb
}

// Add component
Rigidbody rb = gameObject.AddComponent<Rigidbody>();
```

### Transform Manipulation
```csharp
// World space
transform.position = new Vector3(1, 2, 3);
transform.rotation = Quaternion.Euler(0, 90, 0);

// Local space
transform.localPosition = Vector3.zero;
transform.localRotation = Quaternion.identity;
transform.localScale = Vector3.one;
```

### Scene Management
```csharp
using UnityEngine.SceneManagement;

// Load scene
SceneManager.LoadScene("MainMenu");

// Load additive
SceneManager.LoadScene("UI", LoadSceneMode.Additive);

// Get active scene
Scene activeScene = SceneManager.GetActiveScene();
```

## Best Practices

### Performance
1. **Cache component references** instead of calling GetComponent every frame
2. **Use object pooling** for frequently instantiated/destroyed objects
3. **Avoid FindObjectOfType** in Update/FixedUpdate
4. **Use LayerMasks** to filter raycasts and collisions

### Code Organization
1. **Separate concerns**: One script per responsibility
2. **Use namespaces** to organize code
3. **Interface-based design** for flexible systems
4. **ScriptableObjects** for data-driven design

### Editor Workflow
1. **Always use Undo** for editor scripts
2. **Validate inputs** in custom inspectors
3. **Use ExecuteAlways** carefully (performance impact)
4. **EditorApplication.delayCall** for safe editor operations

## Common Pitfalls

### Transform Modifications
❌ **Bad**: Modifying position component-by-component
```csharp
transform.position.x = 5; // This doesn't work!
```

✅ **Good**: Create new Vector3
```csharp
transform.position = new Vector3(5, transform.position.y, transform.position.z);
```

### Instantiate without Parent
❌ **Bad**: Creates clutter in hierarchy
```csharp
Instantiate(prefab);
```

✅ **Good**: Organize with parent
```csharp
Instantiate(prefab, parent);
```

### Finding Objects
❌ **Bad**: Expensive search every frame
```csharp
void Update()
{
    GameObject player = GameObject.Find("Player");
}
```

✅ **Good**: Cache reference
```csharp
private GameObject player;
void Start()
{
    player = GameObject.Find("Player");
}
```

## Unity 6 vs Earlier Versions

### ExecuteAlways (Unity 6)
```csharp
[ExecuteAlways] // New in Unity 6
public class MyScript : MonoBehaviour
{
    void Update()
    {
        // Runs in edit and play mode
    }
}
```

### Legacy (Unity 2022 and earlier)
```csharp
[ExecuteInEditMode] // Deprecated
public class MyScript : MonoBehaviour
{
    void Update()
    {
        // Only runs in edit mode
    }
}
```

## Integration with Unity God Mode Tools

This skill enhances the Unity God Mode MCP tools by providing:

1. **Context-aware suggestions**: Understand the implications of tool operations
2. **Best practice guidance**: Suggest optimal approaches for tasks
3. **Error prevention**: Anticipate and avoid common mistakes
4. **Complete implementations**: Generate full, working C# scripts

## When to Use This Skill

Use this skill when working with:
- GameObject and Component manipulation
- Scene setup and management
- Transform operations
- Basic Unity scripting
- Editor tool development
- Unity project organization

## Related Skills

- **Unity6-Physics**: For Rigidbody, Collider, and physics simulation
- **Unity6-Graphics**: For rendering, shaders, and visual effects
- **Unity6-UI**: For Canvas, UI elements, and layout
- **Unity6-Animation**: For Animator, Animation, and Timeline

## Resources

### Official Documentation
- [Unity 6 Manual](https://docs.unity3d.com/6000.0/Documentation/Manual/)
- [Scripting API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/)
- [Unity 6 Release Notes](https://unity.com/releases/unity-6)

### Key Classes to Know
- `GameObject`: Base class for all entities
- `Transform`: Position, rotation, scale
- `MonoBehaviour`: Base for all scripts
- `Component`: Base class for all components
- `Scene`: Represents a Unity scene
- `EditorUtility`: Editor utility functions (Editor only)
- `AssetDatabase`: Asset management (Editor only)
