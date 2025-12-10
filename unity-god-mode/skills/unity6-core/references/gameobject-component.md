# GameObject and Component System Reference

## GameObject Class

### Core Methods

#### Creation
```csharp
// Empty GameObject
GameObject go = new GameObject();
GameObject go = new GameObject("Name");
GameObject go = new GameObject("Name", typeof(Component1), typeof(Component2));

// Primitives
GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
// Types: Sphere, Capsule, Cylinder, Plane, Quad, Cube
```

#### Finding
```csharp
// By name (slow, use sparingly)
GameObject go = GameObject.Find("ObjectName");

// By tag
GameObject go = GameObject.FindWithTag("Player");
GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

// By type (very slow, avoid in Update)
MyScript script = GameObject.FindObjectOfType<MyScript>();
MyScript[] scripts = GameObject.FindObjectsOfType<MyScript>();
```

#### Component Operations
```csharp
// Add component
Rigidbody rb = go.AddComponent<Rigidbody>();

// Get component
Rigidbody rb = go.GetComponent<Rigidbody>();
Rigidbody rb = go.GetComponentInChildren<Rigidbody>();
Rigidbody rb = go.GetComponentInParent<Rigidbody>();

// Get multiple components
Collider[] colliders = go.GetComponents<Collider>();
Collider[] colliders = go.GetComponentsInChildren<Collider>();
Collider[] colliders = go.GetComponentsInParent<Collider>();

// Try get (safe, no errors if not found)
if (go.TryGetComponent(out Rigidbody rb))
{
    // Use rb safely
}
```

#### Activation
```csharp
// Active state
go.SetActive(true);
go.SetActive(false);
bool isActive = go.activeSelf; // This object only
bool isActiveInHierarchy = go.activeInHierarchy; // Including parents
```

#### Destruction
```csharp
// Runtime
Destroy(go);
Destroy(go, 2.0f); // Delay 2 seconds

// Immediate (dangerous, use carefully)
DestroyImmediate(go);

// Editor
Undo.DestroyObjectImmediate(go); // With undo support
```

### Properties

```csharp
string name = go.name;
string tag = go.tag;
int layer = go.layer;
Transform transform = go.transform;
GameObject parent = go.transform.parent?.gameObject;

bool isStatic = go.isStatic;
HideFlags hideFlags = go.hideFlags;
```

## Component Class

### Base Methods

```csharp
// Access GameObject
GameObject go = component.gameObject;

// Access Transform
Transform t = component.transform;

// Access other components
Rigidbody rb = component.GetComponent<Rigidbody>();

// Component properties
string tag = component.tag;
bool enabled = component.enabled; // Only works for Behaviour components
```

## MonoBehaviour Lifecycle

### Initialization
```csharp
// Called when script is loaded
void Awake() { }

// Called when script is enabled
void OnEnable() { }

// Called before first Update
void Start() { }
```

### Updates
```csharp
// Every frame (frame rate dependent)
void Update() { }

// Fixed time step (physics)
void FixedUpdate() { }

// After all Update() calls
void LateUpdate() { }
```

### Destruction
```csharp
// Called when object is destroyed
void OnDestroy() { }

// Called when script is disabled
void OnDisable() { }
```

### GUI and Gizmos
```csharp
// Legacy GUI
void OnGUI() { }

// Scene view gizmos
void OnDrawGizmos() { }
void OnDrawGizmosSelected() { }
```

## Common Patterns

### Singleton Pattern
```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
```

### Object Pooling
```csharp
public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    private Queue<GameObject> pool = new Queue<GameObject>();

    public GameObject Get()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        return Instantiate(prefab);
    }

    public void Return(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

### Component Cache
```csharp
public class Player : MonoBehaviour
{
    // Cache references
    private Rigidbody rb;
    private Animator anim;
    private Transform tr;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        tr = transform;
    }

    void Update()
    {
        // Use cached references (fast)
        rb.AddForce(Vector3.forward);
    }
}
```

## Tags and Layers

### Tags
```csharp
// Set tag
go.tag = "Player";

// Compare tag (efficient)
if (go.CompareTag("Enemy")) { }

// Find by tag
GameObject player = GameObject.FindWithTag("Player");
GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
```

### Layers
```csharp
// Set layer
go.layer = LayerMask.NameToLayer("Ignore Raycast");

// Layer mask for filtering
LayerMask mask = LayerMask.GetMask("Default", "UI");

// Raycast with layer mask
Physics.Raycast(origin, direction, out hit, distance, mask);
```

## Parent-Child Hierarchy

```csharp
// Set parent
child.transform.SetParent(parent.transform);
child.transform.SetParent(parent.transform, worldPositionStays: false);

// Get parent
Transform parent = go.transform.parent;

// Get children
Transform child = go.transform.GetChild(0);
int childCount = go.transform.childCount;

// Iterate children
foreach (Transform child in go.transform)
{
    // Process each child
}

// Find child by name
Transform child = go.transform.Find("ChildName");
Transform deepChild = go.transform.Find("Child/DeepChild/DeeperChild");

// Detach all children
go.transform.DetachChildren();
```

## Prefabs

### Instantiation
```csharp
// Basic instantiation
GameObject instance = Instantiate(prefab);

// With position and rotation
GameObject instance = Instantiate(prefab, position, rotation);

// With parent
GameObject instance = Instantiate(prefab, parent);
```

### Prefab Utilities (Editor)
```csharp
using UnityEditor;

// Check if prefab
bool isPrefab = PrefabUtility.IsPartOfPrefabAsset(go);
bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go);

// Get prefab asset
GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(instance);

// Apply changes to prefab
PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);

// Revert to prefab
PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
```

## Performance Tips

### ✅ Do This
```csharp
// Cache component references
private Rigidbody rb;
void Awake() { rb = GetComponent<Rigidbody>(); }

// Use CompareTag
if (go.CompareTag("Player")) { }

// Use TryGetComponent
if (TryGetComponent(out Rigidbody rb)) { }

// Disable instead of destroy
go.SetActive(false);
```

### ❌ Don't Do This
```csharp
// Get component every frame
void Update()
{
    GetComponent<Rigidbody>().AddForce(force); // Slow!
}

// String comparison
if (go.tag == "Player") { } // Allocates memory

// Find in Update
void Update()
{
    GameObject.Find("Player"); // Very slow!
}

// Destroy and recreate
Destroy(bullet);
bullet = Instantiate(bulletPrefab); // Use pooling instead
```

## Unity 6 Updates

### Enhanced GameObject APIs
```csharp
// Unity 6: Batch operations
GameObject[] objects = new GameObject[100];
GameObject.Instantiate(objects); // Faster batch instantiation

// Unity 6: Better Find alternatives
GameObject.FindAnyObjectByType<MyScript>(); // Faster than FindObjectOfType
GameObject.FindFirstObjectByType<MyScript>(); // Even faster when you need just one
```

### ExecuteAlways
```csharp
// Unity 6 preferred over ExecuteInEditMode
[ExecuteAlways]
public class MyEditor : MonoBehaviour
{
    void Update()
    {
        // Runs in editor and play mode
    }
}
```

## Common Use Cases

### Player Setup
```csharp
GameObject player = new GameObject("Player");
player.tag = "Player";
player.layer = LayerMask.NameToLayer("Player");
player.AddComponent<CharacterController>();
player.AddComponent<PlayerController>();
player.AddComponent<AudioSource>();
```

### Enemy Spawner
```csharp
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 2f;

    void Start()
    {
        InvokeRepeating(nameof(SpawnEnemy), 0f, spawnInterval);
    }

    void SpawnEnemy()
    {
        GameObject enemy = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
        enemy.transform.SetParent(transform); // Organize in hierarchy
    }
}
```

### Scene Cleanup
```csharp
public static void DestroyAllTagged(string tag)
{
    GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
    foreach (GameObject obj in objects)
    {
        Destroy(obj);
    }
}
```
