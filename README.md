# LUDU Prototype Generator

> "PM'den Dev'e: Prototip 2 saatte hazir"

Oyun studyolari icin hizli prototipleme araci. Product Manager'lar oyun fikrini secer, sistem otomatik calisir prototip olusturur, Developer devralir.

## Mimari

```
ludu-prototype-generator/
├── web/                    # Web Dashboard (lazy-bird based)
│   ├── frontend/          # React + Vite + TypeScript
│   └── backend/           # Python FastAPI
│
├── mcp/                    # Unity Control (unity-god-mode)
│   ├── server/            # MCP Server (3 meta tool)
│   └── bridge/            # Unity C# Plugin
│
├── vault/                  # Component Library
│   ├── editor/            # Unity Editor UI
│   └── library/           # 101 hazir sistem
│
├── templates/              # Game Templates
│   ├── action-rpg/
│   ├── platformer/
│   ├── tower-defense/
│   ├── idle-clicker/
│   └── puzzle/
│
├── docker-compose.yml      # Tek komutla calistir
└── README.md
```

## Hizli Baslangic

```bash
# 1. Klonla
git clone https://github.com/ludu/ludu-prototype-generator.git
cd ludu-prototype-generator

# 2. Calistir
docker-compose up -d

# 3. Ac
open http://localhost:3000
```

## Kullanim

1. Web dashboard'u ac (localhost:3000)
2. "Yeni Prototip" tikla
3. Oyun turunu sec (Action RPG, Platformer, vb.)
4. Mekanikleri sec (Health, Inventory, Combat, vb.)
5. "Olustur" tikla
6. Unity'de prototip hazir!

## Teknolojiler

- **Frontend:** React, Vite, TypeScript, TailwindCSS
- **Backend:** Python, FastAPI, WebSocket
- **MCP Server:** Node.js, TypeScript
- **Unity Bridge:** C#, TCP Socket
- **Container:** Docker, docker-compose

## Lisans

MIT - LUDU Arts
