# 🛠️ Unity God Mode - MCP Tools Dokümantasyonu

Bu doküman, Unity God Mode MCP server'ın sağladığı tüm tool'ların detaylı açıklamalarını içerir.

## 📑 İçindekiler

1. [Scene Management](#scene-management)
2. [GameObject Operations](#gameobject-operations)
3. [Component Management](#component-management)
4. [Script Operations](#script-operations)
5. [Project Information](#project-information)

---

## Scene Management

### `unity_create_scene`

Yeni bir Unity sahne oluşturur.

**Parametreler**:
- `sceneName` (string, required): Sahne adı
- `additive` (boolean, optional): Mevcut sahneye ekle (default: false)

**Örnek**:
```json
{
  "sceneName": "Level1",
  "additive": false
}
```

**Kullanım**:
```
Create a new scene called "MainMenu"
```

---

### `unity_save_scene`

Aktif sahneyi kaydeder.

**Parametreler**:
- `path` (string, optional): Kayıt yolu (default: mevcut sahne yolu)

**Örnek**:
```json
{
  "path": "Assets/Scenes/Level1.unity"
}
```

**Kullanım**:
```
Save the current scene as "Assets/Scenes/GameLevel.unity"
```

**Not**: Path belirtilmezse, mevcut sahnenin üzerine kaydeder.

---

### `unity_list_scenes`

Projedeki tüm sahneleri listeler.

**Parametreler**: Yok

**Örnek Yanıt**:
```json
{
  "scenes": [
    "Assets/Scenes/MainMenu.unity",
    "Assets/Scenes/Level1.unity",
    "Assets/Scenes/Level2.unity"
  ]
}
```

**Kullanım**:
```
List all scenes in the project
```

---

### `unity_get_hierarchy`

Aktif sahnedeki GameObject hiyerarşisini getirir.

**Parametreler**:
- `rootOnly` (boolean, optional): Sadece root GameObject'leri (default: false)

**Örnek**:
```json
{
  "rootOnly": false
}
```

**Örnek Yanıt**:
```json
{
  "hierarchy": [
    "Main Camera",
    "Directional Light",
    "Player",
    "  PlayerModel",
    "  PlayerController",
    "Ground",
    "Enemies",
    "  Enemy1",
    "  Enemy2"
  ]
}
```

**Kullanım**:
```
Show me the complete scene hierarchy
```

---

## GameObject Operations

### `unity_create_gameobject`

Yeni GameObject oluşturur.

**Parametreler**:
- `name` (string, required): GameObject adı
- `primitiveType` (string, optional): Primitive tipi
  - Seçenekler: `"Empty"`, `"Cube"`, `"Sphere"`, `"Capsule"`, `"Cylinder"`, `"Plane"`, `"Quad"`
  - Default: `"Empty"`
- `parent` (string, optional): Parent GameObject adı

**Örnekler**:
```json
// Boş GameObject
{
  "name": "GameManager",
  "primitiveType": "Empty"
}

// Cube primitive
{
  "name": "Player",
  "primitiveType": "Cube"
}

// Child GameObject
{
  "name": "Weapon",
  "primitiveType": "Empty",
  "parent": "Player"
}
```

**Kullanım**:
```
Create a sphere called "Ball"
Create an empty GameObject named "LevelContainer"
Create a cube as a child of "Player" named "Body"
```

---

### `unity_set_transform`

GameObject'in transform değerlerini ayarlar.

**Parametreler**:
- `gameObjectName` (string, required): GameObject adı
- `position` (object, optional): World pozisyonu
  - `x`, `y`, `z` (number)
- `rotation` (object, optional): Euler açıları
  - `x`, `y`, `z` (number)
- `scale` (object, optional): Local scale
  - `x`, `y`, `z` (number)

**Örnekler**:
```json
// Sadece pozisyon
{
  "gameObjectName": "Player",
  "position": { "x": 0, "y": 0, "z": 0 }
}

// Pozisyon ve rotasyon
{
  "gameObjectName": "Camera",
  "position": { "x": 0, "y": 5, "z": -10 },
  "rotation": { "x": 30, "y": 0, "z": 0 }
}

// Hepsi birden
{
  "gameObjectName": "Platform",
  "position": { "x": 0, "y": 0, "z": 0 },
  "rotation": { "x": 0, "y": 0, "z": 0 },
  "scale": { "x": 10, "y": 1, "z": 10 }
}
```

**Kullanım**:
```
Move the Player to position (5, 0, 3)
Rotate the Camera 45 degrees on the Y axis
Scale the Ground to 20x1x20
```

---

### `unity_delete_gameobject`

GameObject'i siler.

**Parametreler**:
- `gameObjectName` (string, required): Silinecek GameObject adı

**Örnek**:
```json
{
  "gameObjectName": "OldEnemy"
}
```

**Kullanım**:
```
Delete the "TestCube" GameObject
Remove all objects named "Obstacle"
```

**Uyarı**: Bu işlem geri alınamaz! Unity'nin Undo sistemi ile geri alınabilir.

---

## Component Management

### `unity_add_component`

GameObject'e component ekler.

**Parametreler**:
- `gameObjectName` (string, required): GameObject adı
- `componentType` (string, required): Component tipi

**Desteklenen Component'ler**:

#### Physics
- `Rigidbody` - 3D physics
- `Rigidbody2D` - 2D physics
- `BoxCollider`, `SphereCollider`, `CapsuleCollider` - 3D collider'lar
- `BoxCollider2D`, `CircleCollider2D` - 2D collider'lar
- `MeshCollider` - Mesh-based collider
- `CharacterController` - Character control

#### Rendering
- `MeshRenderer` - Mesh rendering
- `MeshFilter` - Mesh data
- `SkinnedMeshRenderer` - Skinned mesh
- `SpriteRenderer` - 2D sprite
- `LineRenderer` - Line drawing
- `TrailRenderer` - Trail effects

#### Lighting
- `Light` - Light source
- `LightProbeGroup` - Light probe group
- `ReflectionProbe` - Reflection probe

#### Audio
- `AudioSource` - Audio playback
- `AudioListener` - Audio receiver

#### Animation
- `Animator` - Mecanim animator
- `Animation` - Legacy animation

#### Miscellaneous
- `Camera` - Camera component
- `ParticleSystem` - Particle effects
- `Canvas` - UI canvas
- `CanvasRenderer` - UI rendering

**Örnekler**:
```json
// Rigidbody ekle
{
  "gameObjectName": "Player",
  "componentType": "Rigidbody"
}

// Box Collider ekle
{
  "gameObjectName": "Wall",
  "componentType": "BoxCollider"
}

// Audio Source ekle
{
  "gameObjectName": "BackgroundMusic",
  "componentType": "AudioSource"
}
```

**Kullanım**:
```
Add a Rigidbody to the Player
Give the Enemy a BoxCollider
Add a Light component to the Lamp
```

---

## Script Operations

### `unity_create_script`

Yeni C# script oluşturur.

**Parametreler**:
- `scriptName` (string, required): Script adı (.cs uzantısı olmadan)
- `scriptContent` (string, required): C# kod içeriği
- `path` (string, optional): Assets/ içindeki yol (default: `Assets/Scripts/`)

**Örnek**:
```json
{
  "scriptName": "PlayerController",
  "scriptContent": "using UnityEngine;\n\npublic class PlayerController : MonoBehaviour\n{\n    public float speed = 5f;\n    \n    void Update()\n    {\n        float horizontal = Input.GetAxis(\"Horizontal\");\n        float vertical = Input.GetAxis(\"Vertical\");\n        \n        Vector3 movement = new Vector3(horizontal, 0, vertical);\n        transform.Translate(movement * speed * Time.deltaTime);\n    }\n}",
  "path": "Assets/Scripts/Player/"
}
```

**Kullanım**:
```
Create a PlayerController script that moves with WASD keys at Assets/Scripts/PlayerController.cs
```

**Script Template Örneği**:
```csharp
using UnityEngine;

public class MyScript : MonoBehaviour
{
    void Start()
    {
        // Initialization code
    }

    void Update()
    {
        // Per-frame code
    }
}
```

**Not**: Script oluşturulduktan sonra Unity otomatik olarak compile edecektir.

---

## Project Information

### `unity_get_project_info`

Unity proje bilgilerini getirir.

**Parametreler**: Yok

**Örnek Yanıt**:
```json
{
  "unityVersion": "6000.0.40f1",
  "projectName": "MyAwesomeGame",
  "projectPath": "/Users/ludu/Projects/MyAwesomeGame/Assets",
  "platform": "OSXEditor",
  "companyName": "LUDU"
}
```

**Kullanım**:
```
What Unity version am I using?
Show project information
Get the project path
```

---

## 🎯 Best Practices

### 1. GameObject İsimlendirme
```
✅ İyi: "Player", "MainCamera", "EnemySpawner"
❌ Kötü: "GameObject", "Cube (1)", "temp"
```

### 2. Transform Değerleri
```
// Position'lar mantıklı olmalı
✅ İyi: { x: 0, y: 0, z: 0 }  // Origin
✅ İyi: { x: 0, y: 1, z: 5 }  // Zemin üzerinde

❌ Kötü: { x: 999999, y: -100000, z: 0 }  // Çok uzak
```

### 3. Component Ekleme Sırası
```
1. Rendering components (MeshRenderer, MeshFilter)
2. Physics components (Rigidbody, Collider)
3. Custom scripts
```

### 4. Script Organizasyonu
```
Assets/
  Scripts/
    Player/
      PlayerController.cs
      PlayerAnimation.cs
    Enemy/
      EnemyAI.cs
    Managers/
      GameManager.cs
```

---

## 🚨 Yaygın Hatalar ve Çözümleri

### "GameObject not found"
```
Sebep: GameObject ismi yanlış veya GameObject silinmiş
Çözüm: unity_get_hierarchy ile mevcut GameObject'leri kontrol edin
```

### "Component type not found"
```
Sebep: Component adı yanlış yazılmış
Çözüm: Tam namespace kullanın: "UnityEngine.Rigidbody"
```

### "Scene has no path"
```
Sebep: Sahne hiç kaydedilmemiş
Çözüm: unity_save_scene ile path belirtin
```

### "Directory doesn't exist"
```
Sebep: Script path mevcut değil
Çözüm: Unity Project window'da klasörü manuel oluşturun
```

---

## 🔗 İlgili Dokümanlar

- [Hızlı Başlangıç](./QUICKSTART.md)
- [Örnek Workflow'lar](./EXAMPLES.md)
- [Sorun Giderme](./TROUBLESHOOTING.md)
- [Ana README](../README.md)

---

**Son Güncelleme**: 2025-11-21
**Versiyon**: 1.0.0
