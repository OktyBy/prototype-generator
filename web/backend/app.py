"""
LUDU Prototype Generator - Backend API

PM'lerden gelen prototip isteklerini alir,
Unity MCP Bridge'e gonderir, sonuclari doner.
"""

from fastapi import FastAPI, HTTPException, WebSocket
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional
import asyncio
import json
import socket
from datetime import datetime
import uuid

app = FastAPI(
    title="LUDU Prototype Generator API",
    description="PM'den Dev'e: Prototip 2 saatte hazir",
    version="1.0.0"
)

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Unity Bridge config
UNITY_BRIDGE_HOST = "127.0.0.1"
UNITY_BRIDGE_PORT = 7777

# In-memory storage (production'da DB kullan)
prototypes_db: dict = {}
queue: list = []

# ============================================================================
# Models
# ============================================================================

class PrototypeRequest(BaseModel):
    name: str
    gameType: str
    systems: List[str]
    reference: Optional[str] = None

class PrototypeResponse(BaseModel):
    id: str
    name: str
    gameType: str
    systems: List[str]
    status: str
    createdAt: str
    completedAt: Optional[str] = None

# ============================================================================
# Unity Bridge Communication
# ============================================================================

class UnityBridge:
    def __init__(self):
        self.socket = None
        self.connected = False

    async def connect(self):
        """Unity Bridge'e baglan"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.settimeout(5)
            self.socket.connect((UNITY_BRIDGE_HOST, UNITY_BRIDGE_PORT))
            self.connected = True
            return True
        except Exception as e:
            print(f"Unity Bridge baglanti hatasi: {e}")
            self.connected = False
            return False

    async def send_command(self, command: str, params: dict) -> dict:
        """Unity'ye komut gonder"""
        if not self.connected:
            await self.connect()

        if not self.connected:
            raise HTTPException(status_code=503, detail="Unity Bridge baglantisi yok")

        try:
            request = json.dumps({"command": command, "params": params}) + "\n"
            self.socket.send(request.encode())

            response = ""
            while not response.endswith("\n"):
                chunk = self.socket.recv(4096).decode()
                if not chunk:
                    break
                response += chunk

            return json.loads(response.strip())
        except Exception as e:
            self.connected = False
            raise HTTPException(status_code=500, detail=f"Unity komut hatasi: {e}")

    def disconnect(self):
        if self.socket:
            self.socket.close()
            self.connected = False

unity_bridge = UnityBridge()

# ============================================================================
# System Templates
# ============================================================================

SYSTEM_TEMPLATES = {
    "health": {
        "name": "HealthSystem",
        "scripts": ["HealthSystem.cs", "IDamageable.cs"],
        "prefabs": [],
        "ui": ["HealthBar"]
    },
    "mana": {
        "name": "ManaSystem",
        "scripts": ["ManaSystem.cs", "IResource.cs"],
        "prefabs": [],
        "ui": ["ManaBar"]
    },
    "inventory": {
        "name": "InventorySystem",
        "scripts": ["InventorySystem.cs", "InventorySlot.cs", "ItemSO.cs"],
        "prefabs": ["InventoryUI"],
        "ui": ["InventoryPanel"]
    },
    "equipment": {
        "name": "EquipmentSystem",
        "scripts": ["EquipmentManager.cs", "EquipSlot.cs"],
        "prefabs": [],
        "ui": []
    },
    "combat-melee": {
        "name": "MeleeCombat",
        "scripts": ["MeleeCombat.cs", "AttackData.cs", "Hitbox.cs"],
        "prefabs": ["HitboxPrefab"],
        "ui": []
    },
    "combat-ranged": {
        "name": "RangedCombat",
        "scripts": ["RangedCombat.cs", "Projectile.cs"],
        "prefabs": ["ProjectilePrefab"],
        "ui": []
    },
    "ai-fsm": {
        "name": "AIStateMachine",
        "scripts": ["AIStateMachine.cs", "AIState.cs", "IdleState.cs", "ChaseState.cs", "AttackState.cs"],
        "prefabs": [],
        "ui": []
    },
    "ai-patrol": {
        "name": "PatrolSystem",
        "scripts": ["PatrolSystem.cs", "Waypoint.cs"],
        "prefabs": ["WaypointPrefab"],
        "ui": []
    },
    "dialogue": {
        "name": "DialogueSystem",
        "scripts": ["DialogueSystem.cs", "DialogueSO.cs", "DialogueUI.cs"],
        "prefabs": ["DialoguePanel"],
        "ui": ["DialogueBox"]
    },
    "quest": {
        "name": "QuestSystem",
        "scripts": ["QuestSystem.cs", "Quest.cs", "QuestObjective.cs"],
        "prefabs": [],
        "ui": ["QuestTracker"]
    },
    "save-load": {
        "name": "SaveLoadSystem",
        "scripts": ["SaveManager.cs", "ISaveable.cs", "SaveData.cs"],
        "prefabs": [],
        "ui": []
    }
}

# ============================================================================
# Endpoints
# ============================================================================

@app.get("/")
async def root():
    return {"message": "LUDU Prototype Generator API", "version": "1.0.0"}


@app.get("/api/health")
async def health_check():
    """API ve Unity Bridge durumu"""
    bridge_status = "connected" if unity_bridge.connected else "disconnected"
    return {
        "api": "healthy",
        "unity_bridge": bridge_status,
        "queue_length": len(queue)
    }


@app.get("/api/templates")
async def get_templates():
    """Mevcut sistem template'lerini dondur"""
    return {"templates": SYSTEM_TEMPLATES}


@app.get("/api/prototypes")
async def list_prototypes():
    """Tum prototipleri listele"""
    return {"prototypes": list(prototypes_db.values())}


@app.get("/api/prototypes/{prototype_id}")
async def get_prototype(prototype_id: str):
    """Belirli bir prototipi getir"""
    if prototype_id not in prototypes_db:
        raise HTTPException(status_code=404, detail="Prototip bulunamadi")
    return prototypes_db[prototype_id]


@app.post("/api/prototypes")
async def create_prototype(request: PrototypeRequest):
    """Yeni prototip olustur"""

    prototype_id = str(uuid.uuid4())[:8]

    prototype = {
        "id": prototype_id,
        "name": request.name,
        "gameType": request.gameType,
        "systems": request.systems,
        "reference": request.reference,
        "status": "queued",
        "createdAt": datetime.now().isoformat(),
        "completedAt": None,
        "progress": 0,
        "logs": []
    }

    prototypes_db[prototype_id] = prototype

    # Background'da prototip olustur
    asyncio.create_task(generate_prototype(prototype_id))

    return prototype


async def generate_prototype(prototype_id: str):
    """Prototip olusturma sureci"""

    prototype = prototypes_db[prototype_id]
    prototype["status"] = "generating"
    prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Basladi")

    try:
        # 1. Unity'ye baglan
        connected = await unity_bridge.connect()
        if not connected:
            # Simule et (Unity yoksa)
            await simulate_generation(prototype)
            return

        total_steps = len(prototype["systems"]) + 3  # systems + scene + player + ui
        current_step = 0

        # 2. Scene olustur
        prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Scene olusturuluyor...")
        await unity_bridge.send_command("SetupSceneStructure", {
            "structure": ["--- MANAGERS ---", "--- PLAYER ---", "--- ENEMIES ---", "--- ENVIRONMENT ---", "--- UI ---"]
        })
        current_step += 1
        prototype["progress"] = int((current_step / total_steps) * 100)

        # 3. Her sistem icin script olustur
        for system_id in prototype["systems"]:
            if system_id in SYSTEM_TEMPLATES:
                template = SYSTEM_TEMPLATES[system_id]
                prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] {template['name']} ekleniyor...")

                # Vault'tan import et
                await unity_bridge.send_command("ImportVaultSystem", {
                    "systemId": system_id,
                    "systemPath": f"Core/{template['name']}",
                    "targetPath": f"Assets/_Prototype_{prototype['name']}/Scripts"
                })

                await asyncio.sleep(0.5)  # Rate limiting

            current_step += 1
            prototype["progress"] = int((current_step / total_steps) * 100)

        # 4. Player olustur
        prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Player olusturuluyor...")
        await unity_bridge.send_command("SetupPlayer", {
            "playerType": "3D" if "3d" in prototype["gameType"].lower() else "2D",
            "systems": [SYSTEM_TEMPLATES[s]["name"] for s in prototype["systems"] if s in SYSTEM_TEMPLATES],
            "createModel": True
        })
        current_step += 1
        prototype["progress"] = int((current_step / total_steps) * 100)

        # 5. UI olustur
        prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] UI olusturuluyor...")
        await unity_bridge.send_command("SetupGameUI", {
            "systems": [SYSTEM_TEMPLATES[s]["name"] for s in prototype["systems"] if s in SYSTEM_TEMPLATES],
            "uiStyle": "Minimal"
        })
        current_step += 1
        prototype["progress"] = 100

        prototype["status"] = "completed"
        prototype["completedAt"] = datetime.now().isoformat()
        prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Tamamlandi!")

    except Exception as e:
        prototype["status"] = "failed"
        prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] HATA: {str(e)}")


async def simulate_generation(prototype: dict):
    """Unity baglantisi yokken simule et"""

    total_steps = len(prototype["systems"]) + 3

    for i in range(total_steps):
        await asyncio.sleep(0.5)
        prototype["progress"] = int(((i + 1) / total_steps) * 100)

        if i == 0:
            prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Scene olusturuluyor (simule)...")
        elif i < len(prototype["systems"]) + 1:
            system_idx = i - 1
            if system_idx < len(prototype["systems"]):
                system = prototype["systems"][system_idx]
                prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] {system} ekleniyor (simule)...")
        elif i == total_steps - 2:
            prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Player olusturuluyor (simule)...")
        else:
            prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] UI olusturuluyor (simule)...")

    prototype["status"] = "completed"
    prototype["completedAt"] = datetime.now().isoformat()
    prototype["logs"].append(f"[{datetime.now().strftime('%H:%M:%S')}] Tamamlandi (simule)!")


@app.websocket("/ws/prototype/{prototype_id}")
async def websocket_progress(websocket: WebSocket, prototype_id: str):
    """Prototip ilerleme durumu icin WebSocket"""
    await websocket.accept()

    if prototype_id not in prototypes_db:
        await websocket.close(code=4004)
        return

    try:
        while True:
            prototype = prototypes_db[prototype_id]
            await websocket.send_json({
                "progress": prototype["progress"],
                "status": prototype["status"],
                "logs": prototype["logs"][-5:]  # Son 5 log
            })

            if prototype["status"] in ["completed", "failed"]:
                break

            await asyncio.sleep(0.5)
    except Exception:
        pass
    finally:
        await websocket.close()


# ============================================================================
# Run
# ============================================================================

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
