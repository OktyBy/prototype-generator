# 🎮 Unity God Mode - Örnek Workflow'lar

Bu doküman, Unity God Mode ile yapabileceğiniz gerçek dünya örneklerini içerir.

## 📑 İçindekiler

1. [Temel Örnekler](#temel-örnekler)
2. [Oyun Prototipleri](#oyun-prototipleri)
3. [Level Design](#level-design)
4. [Physics ve Mekanikler](#physics-ve-mekanikler)
5. [UI ve Menu Sistemleri](#ui-ve-menu-sistemleri)
6. [Optimizasyon](#optimizasyon)

---

## Temel Örnekler

### Örnek 1: İlk Sahneniz

**Talep**:
```
Create my first Unity scene with:
- A ground plane (scale 10x10)
- A player cube with a camera following it
- Basic lighting
```

**Claude'un Yapacakları**:
1. `unity_create_scene("MyFirstScene")`
2. `unity_create_gameobject("Ground", "Plane")`
3. `unity_set_transform("Ground", scale: {x: 10, z: 10})`
4. `unity_create_gameobject("Player", "Cube")`
5. `unity_set_transform("Player", position: {x: 0, y: 1, z: 0})`
6. `unity_create_gameobject("Main Camera", "Empty")`
7. `unity_add_component("Main Camera", "Camera")`
8. `unity_set_transform("Main Camera", position: {x: 0, y: 5, z: -10}, rotation: {x: 30, y: 0, z: 0})`
9. `unity_create_gameobject("Directional Light", "Empty")`
10. `unity_add_component("Directional Light", "Light")`
11. `unity_save_scene("Assets/Scenes/MyFirstScene.unity")`

**Sonuç**: Tam çalışır bir başlangıç sahnesi!

---

### Örnek 2: Basit Movement Script

**Talep**:
```
Write a simple character controller script that:
- Moves with WASD
- Rotates with mouse
- Speed is adjustable in inspector
- Save to Assets/Scripts/SimpleController.cs
```

**Claude'un Oluşturacağı Script**:
```csharp
using UnityEngine;

public class SimpleController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 3f;

    void Update()
    {
        // Movement
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, 0, vertical);
        transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);

        // Rotation
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * rotationSpeed);
    }
}
```

**Sonraki Adım**:
```
Attach the SimpleController script to the Player GameObject
```

---

## Oyun Prototipleri

### Örnek 3: 3D Platformer Prototipi

**Talep**:
```
Create a 3D platformer prototype:

1. Scene setup:
   - Main platform (10x1x10) at origin
   - 5 smaller platforms (2x0.5x2) at different heights
   - Player spawn point
   - Goal sphere

2. Player setup:
   - Capsule with CharacterController
   - Jump capability
   - WASD movement
   - Camera following player

3. Make platforms colorful (different materials)

4. Add simple win condition when reaching goal
```

**Detaylı Uygulama**:

#### Adım 1: Scene ve Platformlar
```
// Main platform
Create a plane called "MainPlatform" scaled to 10x1x10
Set MainPlatform position to (0, 0, 0)

// Jumping platforms
Create 5 cubes named "Platform1" through "Platform5"
Scale them to 2x0.5x2
Position them at:
- Platform1: (3, 1, 0)
- Platform2: (6, 2, 2)
- Platform3: (9, 3, -1)
- Platform4: (12, 4, 1)
- Platform5: (15, 5, 0)
```

#### Adım 2: Player Setup
```
Create a capsule named "Player" at (0, 1, 0)
Add CharacterController to Player
Create PlayerController script with jumping
```

**PlayerController.cs**:
```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 8f;
    public float gravity = -20f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Movement
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
```

#### Adım 3: Camera Follow
```
Create CameraFollow script for smooth camera
```

**CameraFollow.cs**:
```csharp
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target);
    }
}
```

#### Adım 4: Goal System
```
Create a sphere called "Goal" at (15, 6, 0)
Add GoalTrigger script
```

**GoalTrigger.cs**:
```csharp
using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("🎉 You Win!");
            // Show win screen or restart level
        }
    }
}
```

**Sonuç**: Oynanabilir platformer prototipi tamamlandı!

---

### Örnek 4: Simple Shooter

**Talep**:
```
Create a top-down shooter prototype:
- Player that can move in 8 directions
- Player rotates to face mouse
- Shooting with left click
- 3 enemy spawners
- Simple enemy AI that moves toward player
```

**Uygulama** (Özet):
1. Top-down camera setup
2. Player movement script
3. Bullet prefab ve shooting system
4. Enemy AI script
5. Spawner system

---

## Level Design

### Örnek 5: Procedural Obstacle Course

**Talep**:
```
Generate a procedural obstacle course:
- 20 platform segments
- Random gaps between platforms
- Moving platforms
- Rotating obstacles
- Collectibles on each platform
```

**Script Yaklaşımı**:
```csharp
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public GameObject platformPrefab;
    public GameObject obstaclePrefab;
    public int segmentCount = 20;
    public float segmentLength = 10f;

    void Start()
    {
        GenerateLevel();
    }

    void GenerateLevel()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            float z = i * segmentLength;
            float randomGap = Random.Range(0f, 2f);

            // Platform
            Vector3 platformPos = new Vector3(0, 0, z + randomGap);
            Instantiate(platformPrefab, platformPos, Quaternion.identity);

            // Obstacle (50% chance)
            if (Random.value > 0.5f)
            {
                Vector3 obstaclePos = new Vector3(0, 1, z + segmentLength/2);
                Instantiate(obstaclePrefab, obstaclePos, Quaternion.identity);
            }
        }
    }
}
```

---

## Physics ve Mekanikler

### Örnek 6: Angry Birds Benzeri

**Talep**:
```
Create an Angry Birds style physics game:
- Slingshot mechanism
- Projectile with trajectory preview
- Destructible tower of blocks
- Target objects
```

**Ana Bileşenler**:

**1. Slingshot.cs**:
```csharp
using UnityEngine;

public class Slingshot : MonoBehaviour
{
    public Transform anchor;
    public LineRenderer trajectoryLine;
    public GameObject projectilePrefab;

    private GameObject currentProjectile;
    private bool isDragging = false;
    private Vector3 dragPosition;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartDrag();
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDrag();
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            ReleaseDrag();
        }
    }

    void StartDrag()
    {
        currentProjectile = Instantiate(projectilePrefab, anchor.position, Quaternion.identity);
        isDragging = true;
    }

    void UpdateDrag()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        dragPosition = mousePos;
        currentProjectile.transform.position = mousePos;

        DrawTrajectory();
    }

    void ReleaseDrag()
    {
        isDragging = false;

        Vector3 force = (anchor.position - dragPosition) * 10f;
        Rigidbody rb = currentProjectile.GetComponent<Rigidbody>();
        rb.AddForce(force, ForceMode.Impulse);

        trajectoryLine.enabled = false;
    }

    void DrawTrajectory()
    {
        // Trajectory prediction code
    }
}
```

---

### Örnek 7: Car Physics

**Talep**:
```
Create a simple car controller with:
- Wheel colliders
- Acceleration and braking
- Steering
- Realistic physics
- Drift effect
```

**Wheelcollider Setup**:
```
Add 4 WheelColliders to the car
Configure suspension, friction curves
Create CarController script
```

---

## UI ve Menu Sistemleri

### Örnek 8: Main Menu

**Talep**:
```
Create a main menu system:
- Background image
- Title text
- Play, Options, Quit buttons
- Button hover effects
- Scene transition
```

**UI Hierarchy**:
```
Canvas
├── Background (Image)
├── Title (Text)
└── ButtonPanel
    ├── PlayButton
    ├── OptionsButton
    └── QuitButton
```

**MenuManager.cs**:
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public Button playButton;
    public Button optionsButton;
    public Button quitButton;

    void Start()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        optionsButton.onClick.AddListener(OnOptionsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnPlayClicked()
    {
        SceneManager.LoadScene("GameLevel");
    }

    void OnOptionsClicked()
    {
        SceneManager.LoadScene("Options");
    }

    void OnQuitClicked()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
```

---

### Örnek 9: HUD System

**Talep**:
```
Create a game HUD with:
- Health bar
- Score counter
- Ammo display
- Mini-map
- Objective tracker
```

**HUDManager.cs**:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Health")]
    public Slider healthBar;
    public TMP_Text healthText;

    [Header("Score")]
    public TMP_Text scoreText;
    private int currentScore = 0;

    [Header("Ammo")]
    public TMP_Text ammoText;

    public void UpdateHealth(float current, float max)
    {
        healthBar.value = current / max;
        healthText.text = $"{current}/{max}";
    }

    public void AddScore(int points)
    {
        currentScore += points;
        scoreText.text = $"Score: {currentScore}";
    }

    public void UpdateAmmo(int current, int max)
    {
        ammoText.text = $"{current}/{max}";
    }
}
```

---

## Optimizasyon

### Örnek 10: Batch Performance Optimization

**Talep**:
```
Optimize this scene for mobile:
1. Combine static meshes
2. Setup occlusion culling
3. Reduce draw calls
4. Optimize textures
5. Remove unnecessary components
```

**Optimizasyon Scripti**:
```csharp
using UnityEngine;
using UnityEditor;

public class SceneOptimizer : MonoBehaviour
{
    [MenuItem("Tools/Optimize Scene")]
    static void OptimizeScene()
    {
        // Static batching
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.gameObject.isStatic)
                {
                    StaticBatchingUtility.Combine(renderer.gameObject);
                }
            }
        }

        // Remove unused components
        RemoveUnusedComponents();

        // Compress textures
        CompressTextures();

        Debug.Log("Scene optimized!");
    }

    static void RemoveUnusedComponents()
    {
        // Implementation
    }

    static void CompressTextures()
    {
        // Implementation
    }
}
```

---

## 🎯 Kompleks Örnek: Tam Oyun Prototipi

### Örnek 11: Complete FPS Prototype

**Talep**:
```
Create a complete FPS game prototype with:

1. Player System:
   - First person controller
   - Health system
   - Weapon system (rifle, pistol)
   - Ammo management
   - Reload mechanics

2. Enemy AI:
   - Patrol behavior
   - Chase player when spotted
   - Attack when in range
   - Health and death

3. Level:
   - Indoor environment
   - Cover objects
   - Spawn points
   - Collectibles

4. UI:
   - Crosshair
   - Health bar
   - Ammo counter
   - Weapon switcher
   - Game over screen

5. Game Logic:
   - Wave system
   - Score tracking
   - Win/lose conditions
```

Bu tam bir oyun prototipi oluşturur ve yaklaşık 50-100 satırlık kod gerektirir.

---

## 💡 Pro İpuçları

### İpucu 1: Incremental Development
```
Büyük projeleri adım adım oluşturun:
1. Önce basic mechanics
2. Sonra polish
3. Son olarak optimization
```

### İpucu 2: Prefab Kullanımı
```
Tekrarlayan objeler için prefab oluşturun:
- Enemies
- Collectibles
- Platform segments
```

### İpucu 3: Scene Organization
```
Boş GameObject'lerle organize edin:
- Managers
- Environment
- Enemies
- Collectibles
- UI
```

### İpucu 4: Testing
```
Her major değişiklikten sonra test edin:
"Test the player movement in play mode"
"Check if enemies spawn correctly"
```

---

## 🚀 Sonraki Adımlar

1. Bu örnekleri kendi projelerinizde deneyin
2. Kendi workflow'larınızı oluşturun
3. Complex sistemleri parçalara bölün
4. Claude'a adım adım talimatlar verin

---

**Daha Fazla Örnek İçin**:
- [Unity Learn](https://learn.unity.com)
- [Unity Manual](https://docs.unity3d.com/Manual/)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)

**Son Güncelleme**: 2025-11-21
