# 🎮 Unity God Mode for Claude Code

**Unity God Mode** tam anlamıyla bir "God Mode" sistemidir - Unity 6 Editor'ü doğrudan kontrol etmenizi sağlayan kapsamlı bir AI entegrasyonu. Natural language ile Unity projelerinizi yönetin, kod yazın, sahne oluşturun ve daha fazlası!

## ✨ Özellikler

- 🎯 **Gerçek Zamanlı Unity Kontrolü**: MCP server üzerinden Unity Editor'ü doğrudan kontrol edin
- 🧠 **Derin Bilgi Tabanı**: Unity 6 API dokümantasyonu ve best practice'lerle donanmış Skills
- 👥 **Uzman Subagent'lar**: Scene Builder, Script Generator, Asset Manager gibi uzmanlaşmış AI asistanlar
- ⚡ **Hızlı Aksiyonlar**: Slash command'lar ile tek satırda işlem yapın
- 🔄 **Çift Yönlü İletişim**: Unity ↔ Claude Code arasında TCP soketi üzerinden gerçek zamanlı haberleşme

## 🏗️ Mimari

```
Claude Code (AI) ↔ MCP Server (Node.js) ↔ TCP Socket ↔ Unity Editor Plugin (C#)
```

### Katmanlar

1. **MCP Server v3.0** - 3 meta tool, 100+ komut (Progressive Disclosure)
2. **Unity Bridge** - C# TCP socket plugin
3. **Game Generation** - High-level oyun oluşturma komutları

### v3.0 Optimizasyonları

- **%98.7 Token Tasarrufu**: 105 tool → 3 meta tool
- **Batch Operations**: Tek çağrıda çoklu işlem
- **Response Compression**: Akıllı yanıt sıkıştırma
- **Progressive Disclosure**: İhtiyaç halinde detay

## 📋 Gereksinimler

- **Unity 6** (6000.0.40f1 veya üzeri)
- **Claude Code** (2.0+)
- **Node.js** (v18 veya üzeri)
- **macOS, Linux, veya Windows**

## 🚀 Kurulum

### 1. MCP Server'ı Etkinleştirin

MCP server zaten build edilmiş durumda. Claude Code'a ekleyin:

```bash
# MCP config dosyası zaten ~/.mcp.json dosyasında oluşturuldu
# Claude Code'u yeniden başlatın veya:
```

Claude Code içinde `/mcp` komutunu çalıştırarak `unity-god-mode` server'ının aktif olduğunu doğrulayın.

### 2. Unity Editor Plugin'ini Kurun

#### Yöntem 1: Unity Package Manager (Önerilen)

1. Unity Editor'ü açın
2. Window → Package Manager
3. + → Add package from disk...
4. `~/unity-god-mode/unity-bridge/ClaudeMCPBridge/package.json` dosyasını seçin

#### Yöntem 2: Manuel Kurulum

Unity projenizin `Assets/Plugins/` klasörüne kopyalayın:

```bash
cp -r ~/unity-god-mode/unity-bridge/ClaudeMCPBridge /path/to/your/unity/project/Assets/Plugins/
```

### 3. Unity Bridge'i Başlatın

1. Unity Editor'de: **Window → Claude MCP Bridge**
2. Bridge window'u açık tutun (server otomatik başlar)
3. Yeşil "● Running" durumunu görmelisiniz

## 🎯 Kullanım

### İlk Test

Claude Code'da şunu yazın:

```
Create a simple scene with a cube, a light, and a camera
```

Claude otomatik olarak:
- `unity_create_scene` tool'unu kullanacak
- Cube GameObject oluşturacak
- Directional Light ekleyecek
- Main Camera'yı konumlandıracak

### Örnek Komutlar

#### Scene Oluşturma
```
Create a platformer level with player, ground plane, and obstacles
```

#### Script Yazma
```
Write a PlayerController script with WASD movement and jumping
```

#### Asset Optimizasyonu
```
Optimize all textures in the Materials folder for mobile
```

#### Debugging
```
Why is my scene running at 30 FPS? Analyze and fix performance issues
```

## 🛠️ MCP Tools v3.0 (Progressive Disclosure)

v3.0 ile 105 ayrı tool yerine **3 akıllı meta tool** kullanılıyor:

| Tool | Açıklama | Kullanım |
|------|----------|----------|
| `unity_discover` | Mevcut komutları keşfet | Kategori bazlı: scene, gameobject, component, script, asset, project |
| `unity_do` | İşlem yap (batch destekli) | Tek veya çoklu komut çalıştır |
| `unity_ask` | Bilgi sor | Proje durumu, sahne bilgisi, hatalar |

### unity_discover Örneği
```json
{
  "category": "gameobject"
}
// Dönen: create, delete, rename, duplicate, find, set_active...
```

### unity_do Örneği (Tekli)
```json
{
  "command": "create_gameobject",
  "params": { "name": "Player", "primitiveType": "Capsule" }
}
```

### unity_do Örneği (Batch - Çoklu İşlem)
```json
{
  "batch": [
    { "command": "create_gameobject", "params": { "name": "Player", "primitiveType": "Capsule" } },
    { "command": "create_gameobject", "params": { "name": "Enemy", "primitiveType": "Cube" } },
    { "command": "add_component", "params": { "gameObjectName": "Player", "componentType": "Rigidbody" } },
    { "command": "set_transform", "params": { "gameObjectName": "Player", "position": {"x":0,"y":1,"z":0} } }
  ]
}
```

### Mevcut Komutlar

**Scene:** `create_scene`, `save_scene`, `load_scene`, `list_scenes`, `get_hierarchy`

**GameObject:** `create_gameobject`, `delete_gameobject`, `rename_gameobject`, `duplicate_gameobject`, `find_gameobject`, `set_active`

**Component:** `add_component`, `remove_component`, `get_components`, `set_component_property`

**Transform:** `set_transform`, `get_transform`, `set_parent`

**Script:** `create_script`, `attach_script`, `list_scripts`

**Asset:** `import_asset`, `create_material`, `create_prefab`

**Project:** `get_project_info`, `get_settings`, `build_project`

## 🎓 İleri Seviye Kullanım

### Karmaşık Workflow Örneği

```
I need to build a complete 2D platformer game:

1. Create main menu scene with UI buttons
2. Create level 1 scene with player, platforms, enemies, and collectibles
3. Write player controller with double jump
4. Write enemy AI with patrol behavior
5. Write collectible system with score tracking
6. Set up scene transitions
7. Build for Windows and macOS
```

Claude bu talebi adım adım işleyecek ve tüm Unity işlemlerini otomatik olarak yapacaktır.

### Subagent Kullanımı (Gelecek Sürüm)

```bash
# Scene Builder subagent
@unity-scene-builder "Create a dark fantasy dungeon level"

# Performance Optimizer subagent
@unity-perf "Optimize this scene for 60 FPS on mobile"
```

## 🔧 Sorun Giderme

### "Cannot connect to Unity Editor" Hatası

**Çözüm**:
1. Unity Editor'ün çalıştığından emin olun
2. **Window → Claude MCP Bridge** window'unun açık olduğunu kontrol edin
3. Bridge window'da "● Running" durumunu görüyor musunuz?
4. Unity Console'da hata var mı kontrol edin

### MCP Server Görünmüyor

**Çözüm**:
```bash
# MCP config'i kontrol edin
cat ~/.mcp.json

# Claude Code'u yeniden başlatın
# /mcp komutunu çalıştırıp unity-god-mode'u görüyor musunuz?
```

### Tool Call'lar Timeout Oluyor

**Çözüm**:
1. Unity Editor donmuş mu kontrol edin
2. Bridge window log'larına bakın
3. Unity Console'da C# hataları olabilir

### Plugin Yüklenmiyor

**Çözüm**:
1. Unity'nin 2022.3+ veya Unity 6 olduğunu doğrulayın
2. `Assets/Plugins/ClaudeMCPBridge/` klasörünün var olduğunu kontrol edin
3. Unity Editor'ü yeniden başlatın
4. Console'da compiler hataları var mı bakın

## 📝 Konfigürasyon

### MCP Server Ayarları

`~/.mcp.json` dosyasını düzenleyin:

```json
{
  "mcpServers": {
    "unity-god-mode": {
      "command": "node",
      "args": ["/Users/ludu/unity-god-mode/mcp-server/dist/index.js"],
      "env": {
        "UNITY_BRIDGE_PORT": "7777"
      }
    }
  }
}
```

### Unity Bridge Port Değiştirme

`ClaudeMCPBridgeWindow.cs` dosyasında `PORT` değişkenini değiştirin (default: 7777).

## 🔜 Roadmap

### Faz 1: Temel Sistem ✅
- [x] MCP Server
- [x] Unity C# Bridge
- [x] 10 core tool
- [x] Documentation

### Faz 2: MCP v3.0 Optimizasyon ✅
- [x] Progressive Disclosure (105 → 3 meta tool)
- [x] %98.7 token tasarrufu
- [x] Batch operations desteği
- [x] Response compression
- [x] Kategori bazlı komut keşfi

### Faz 3: Skills (Sırada)
- [ ] Unity6-Core skill
- [ ] Unity6-Graphics skill
- [ ] Unity6-Physics skill
- [ ] Unity6-AI skill

### Faz 4: Subagents (Sırada)
- [ ] Scene Builder
- [ ] Script Generator
- [ ] Asset Manager
- [ ] Performance Optimizer
- [ ] Build Engineer

### Faz 5: Slash Commands (Sırada)
- [ ] `/unity-setup-project`
- [ ] `/unity-create-scene [name]`
- [ ] `/unity-debug-mode`
- [ ] `/unity-build [platform]`

### Gelecek Özellikler (v4.0)
- [ ] Visual debugging (screenshot capture)
- [ ] Shader generation
- [ ] Procedural asset creation
- [ ] Automated testing
- [ ] Multi-platform build automation
- [ ] Asset Store integration

## 🤝 Katkıda Bulunma

Bu proje açık kaynak değildir ancak önerilerinizi ve bug report'larınızı bekliyoruz!

## 📄 Lisans

MIT License - LUDU © 2025

## 🙏 Teşekkürler

- **Anthropic** - Claude Code ve MCP SDK için
- **Unity Technologies** - Unity Engine için
- **Topluluk** - Geri bildirim ve destek için

## 📞 İletişim

- **Website**: https://luduarts.com
- **Email**: producer@luduarts.com
- **GitHub**: https://github.com/ludu

---

**Unity God Mode** ile Unity development'ınızı bir üst seviyeye taşıyın! 🚀

Made with ❤️ by LUDU
