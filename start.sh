#!/bin/bash

echo "🎮 LUDU Prototype Generator baslatiliyor..."
echo ""

# Renk kodlari
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 1. Backend baslat
echo -e "${BLUE}[1/3]${NC} Backend baslatiliyor..."
cd web/backend
python3 -m venv venv 2>/dev/null
source venv/bin/activate 2>/dev/null || source venv/Scripts/activate 2>/dev/null
pip3 install -r requirements.txt -q
python3 -m uvicorn app:app --host 0.0.0.0 --port 8000 &
BACKEND_PID=$!
cd ../..

# 2. Frontend baslat
echo -e "${BLUE}[2/3]${NC} Frontend baslatiliyor..."
cd web/frontend
npm install -q
npm run dev &
FRONTEND_PID=$!
cd ../..

# 3. Bilgi ver
sleep 3
echo ""
echo -e "${GREEN}✅ LUDU Prototype Generator hazir!${NC}"
echo ""
echo "   Dashboard:  http://localhost:3000"
echo "   API:        http://localhost:8000"
echo "   API Docs:   http://localhost:8000/docs"
echo ""
echo "   Unity Bridge'in acik oldugundan emin ol!"
echo "   (Window > Claude MCP Bridge)"
echo ""
echo "   Durdurmak icin: Ctrl+C"
echo ""

# Cleanup on exit
trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null" EXIT

# Bekle
wait
