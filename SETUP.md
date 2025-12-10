# LUDU Prototype Generator - Kurulum

## Gereksinimler

- Node.js 18+
- Python 3.10+
- Unity 6 (6000.0.x)
- Claude MCP Bridge (Unity plugin)

## Hizli Kurulum

### 1. Bagimliliklari Yukle

```bash
cd ludu-prototype-generator

# Frontend
cd web/frontend
npm install

# Backend
cd ../backend
pip install -r requirements.txt
```

### 2. Unity Bridge Kur

Unity projende:
1. `Window > Claude MCP Bridge` ac
2. Port: 7777 (varsayilan)
3. "Start Bridge" tikla

### 3. LUDU'yu Baslat

```bash
# Kolay yol
./start.sh

# veya manuel
# Terminal 1 - Backend
cd web/backend
uvicorn app:app --port 8000

# Terminal 2 - Frontend
cd web/frontend
npm run dev
```

### 4. Tarayicida Ac

```
http://localhost:3000
```

## Docker ile Kurulum

```bash
docker-compose up -d
```

## MCP Server (Claude Code icin)

Eger Claude Code ile kullanmak istiyorsan:

```bash
# ~/.mcp.json dosyasina ekle
{
  "mcpServers": {
    "ludu": {
      "command": "npx",
      "args": ["tsx", "/Users/ludu/unity-god-mode/mcp-server/src/index-optimized.ts"]
    }
  }
}
```

## Klasor Yapisi

```
ludu-prototype-generator/
├── web/
│   ├── frontend/          # React dashboard
│   │   ├── src/
│   │   │   ├── pages/     # Sayfa componentleri
│   │   │   └── components/# UI componentleri
│   │   └── package.json
│   └── backend/           # Python API
│       ├── app.py         # FastAPI uygulamasi
│       └── requirements.txt
├── docker-compose.yml
├── start.sh
└── README.md
```

## API Endpoints

| Method | Endpoint | Aciklama |
|--------|----------|----------|
| GET | `/api/health` | API durumu |
| GET | `/api/templates` | Sistem sablonlari |
| GET | `/api/prototypes` | Tum prototipler |
| POST | `/api/prototypes` | Yeni prototip olustur |
| WS | `/ws/prototype/{id}` | Ilerleme WebSocket |

## Sorun Giderme

### Unity Bridge baglanmiyor

1. Unity Editor acik mi?
2. Claude MCP Bridge penceresi acik mi?
3. Port 7777 baska uygulama tarafindan kullaniliyor mu?

### Frontend baslamiyor

```bash
cd web/frontend
rm -rf node_modules
npm install
```

### Backend baslamiyor

```bash
cd web/backend
pip install -r requirements.txt --force-reinstall
```

## Destek

LUDU Arts - Internal Tool
