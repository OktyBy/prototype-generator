# LUDU Prototype Generator - PM Kılavuzu

## Hızlı Başlangıç (5 Dakika)

### 1. Gereksinimler
- Unity 6 (6000.0.40f1+) kurulu
- Node.js (v18+) kurulu
- Python 3.10+ kurulu

### 2. İlk Kurulum (Sadece 1 kere)

```bash
# Klasöre git
cd "/Users/ludu/Desktop/Ludu/Ludu Prototype Generator"

# Başlat
./start.sh
```

### 3. Kullanım

**Web Arayüzü:** http://localhost:5173

1. "Yeni Prototip" butonuna tıkla
2. Oyun türünü seç (Action RPG, Platformer, vb.)
3. İstediğin sistemleri tikle (Health, Inventory, Combat...)
4. "Generate" tıkla
5. Unity'de prototip hazır!

---

## İki Kullanım Yöntemi

### Yöntem 1: Web Arayüzü (Kolay)
- Tarayıcıdan http://localhost:5173 aç
- Tıkla, seç, generate et
- En hızlı yöntem

### Yöntem 2: Claude Code (Gelişmiş)
```
"Create a 3D action RPG prototype with health, inventory and combat systems"
```
Claude direkt Unity'ye bağlanıp oluşturur.

---

## Oyun Türleri ve Önerilen Sistemler

### Action RPG
- Health System
- Mana/Energy
- Inventory
- Equipment
- Melee Combat
- AI State Machine
- Save/Load

### 2D Platformer
- Health System
- Character Controller 2D
- Save/Load
- Checkpoint System

### FPS
- Health System
- First Person Controller
- Ranged Combat
- Weapon System
- Ammo System

### Puzzle
- Game State Manager
- Save/Load
- Interactable System
- Trigger Zones

---

## Sık Kullanılan Sistemler

| Sistem | Ne Yapar |
|--------|----------|
| Health | Can, hasar, ölüm |
| Mana | Enerji, stamina |
| Inventory | Envanter yönetimi |
| Combat | Savaş mekaniği |
| Save/Load | Kayıt sistemi |
| AI FSM | Düşman yapay zekası |

---

## Sorun Giderme

### "Unity'ye bağlanamıyor"
1. Unity Editor açık mı?
2. Window → Claude MCP Bridge aç
3. "● Running" görüyor musun?

### "Web açılmıyor"
```bash
# Servisleri yeniden başlat
./start.sh
```

### "Sistem eklenmiyor"
- UnityVault Unity'de açık olmalı
- Window → UnityVault kontrol et

---

## İpuçları

1. **Küçük başla:** İlk prototipler için 3-5 sistem yeter
2. **Test et:** Her prototipten sonra Play Mode'da test et
3. **Kaydet:** Scene'i kaydetmeyi unutma (Ctrl+S)
4. **Export:** Prototip hazırsa File → Export Package

---

## Destek

- **Slack:** #prototype-generator
- **Email:** producer@luduarts.com

---

Made with LUDU Prototype Generator v1.0
