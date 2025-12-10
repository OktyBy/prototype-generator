# 🚀 Unity God Mode - Hızlı Başlangıç

5 dakikada Unity God Mode'u çalıştırın!

## ✅ Ön Kontroller

```bash
# Unity 6 kurulu mu?
ls /Applications/Unity/Hub/Editor/

# Node.js var mı?
node --version  # v18+ olmalı

# Claude Code aktif mi?
claude --version
```

## 📦 Kurulum (3 Adım)

### Adım 1: MCP Server Hazır ✅

MCP server zaten build edilmiş ve yapılandırılmış! Kontrol edin:

```bash
# Config dosyası var mı?
cat ~/.mcp.json

# MCP server build'i var mı?
ls ~/unity-god-mode/mcp-server/dist/index.js
```

### Adım 2: Unity Plugin'i Yükle

**Seçenek A: Drag & Drop (En Hızlı)**

1. Unity projenizi açın
2. `~/unity-god-mode/unity-bridge/ClaudeMCPBridge` klasörünü sürükleyip
3. Unity Project window'da `Assets/Plugins/` içine bırakın

**Seçenek B: Terminal**

```bash
# Unity projenizin yolunu değiştirin
UNITY_PROJECT="/path/to/your/unity/project"

cp -r ~/unity-god-mode/unity-bridge/ClaudeMCPBridge \
      "$UNITY_PROJECT/Assets/Plugins/"
```

### Adım 3: Bridge'i Başlat

1. Unity Editor'de: **Window → Claude MCP Bridge**
2. Yeşil "● Running" yazısını görün
3. Hazırsınız! 🎉

## 🎮 İlk Komutunuz

Claude Code'da şunu yazın:

```
unity_get_project_info
```

Veya doğal dilde:

```
Show me the current Unity project information
```

Başarılı yanıt alırsanız, her şey çalışıyor demektir! ✅

## 🎯 Örnek Kullanımlar

### 1. Basit Bir Sahne Oluşturun

```
Create a new scene called "TestScene" with:
- A red cube at position (0, 0, 0)
- A directional light
- A main camera looking at the cube
```

**Sonuç**: Claude otomatik olarak sahneyi oluşturacak ve GameObject'leri ekleyecek.

### 2. Script Yazın

```
Create a simple PlayerController script that:
- Moves with WASD keys
- Rotates with mouse
- Can jump with Space
- Save it to Assets/Scripts/PlayerController.cs
```

**Sonuç**: Tam çalışır bir C# script oluşturulacak.

### 3. Component Ekleyin

```
Add a Rigidbody component to the Cube object and set its mass to 10
```

**Sonuç**: Rigidbody eklenip configure edilecek.

## 🔍 Doğrulama

Her şeyin çalıştığını test edin:

```bash
# Test 1: Project info
echo "Get Unity project info" | claude

# Test 2: Scene hierarchy
echo "List all GameObjects in the current scene" | claude

# Test 3: Create something
echo "Create a sphere called TestSphere" | claude
```

Hepsi çalışıyorsa, **Unity God Mode aktif!** 🎉

## ⚠️ Yaygın Sorunlar

### "Cannot connect to Unity Editor"

```bash
# Unity çalışıyor mu?
ps aux | grep Unity

# Bridge window açık mı?
# Unity'de: Window → Claude MCP Bridge
```

### "MCP server not found"

```bash
# Claude Code'u yeniden başlatın
claude --version

# MCP server'ları listeleyin
# Claude Code içinde: /mcp
```

### "Tool not available"

```bash
# MCP server'ı etkinleştirin
# Claude Code'da: /mcp
# unity-god-mode'u enable edin
```

## 📚 Daha Fazla Öğrenme

- **Tüm Özellikler**: [README.md](../README.md)
- **MCP Tools Listesi**: [TOOLS.md](./TOOLS.md)
- **Örnek Workflow'lar**: [EXAMPLES.md](./EXAMPLES.md)
- **Sorun Giderme**: [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

## 🎓 Sonraki Adımlar

1. ✅ **Temel kurulum tamamlandı**
2. 🎯 [Örnek workflow'ları deneyin](./EXAMPLES.md)
3. 🛠️ [Skills'leri yükleyin](./SKILLS.md) (gelişmiş)
4. 👥 [Subagent'ları kurun](./SUBAGENTS.md) (uzman)

## 💡 Pro İpuçları

**İpucu 1**: Unity Bridge window'unu her zaman açık tutun

**İpucu 2**: Karmaşık talepler için Claude'a adım adım plan yaptırın:
```
Think step by step: How would you create a complete player controller system?
```

**İpucu 3**: Hataları debug etmek için bridge log'larını kullanın:
```
# Unity'de Claude MCP Bridge window'daki log'lara bakın
```

**İpucu 4**: `/permissions` ile Unity tool'larını otomatik onaylayın:
```
# Claude Code'da
/permissions
# unity_* tool'larını "allow" listesine ekleyin
```

## 🚀 İlk Projenizi Oluşturun

Şimdi gerçek bir şey yapın:

```
I want to create a simple 3D game prototype:

1. Create a scene called "MainLevel"
2. Add a ground plane (10x10)
3. Create a player cube with:
   - CharacterController component
   - A simple movement script
4. Add 5 random obstacle cubes
5. Add a goal sphere at position (8, 0.5, 8)
6. Set up lighting with a directional light

Make it playable!
```

Claude tüm bunları otomatik olarak yapacak! 🎮

---

**Hazırsınız!** Artık Unity God Mode ile AI-powered game development yapabilirsiniz! 🎉

Sorular? [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) dosyasına bakın.
