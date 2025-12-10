import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Gamepad2,
  Swords,
  Heart,
  Backpack,
  Bot,
  MessageSquare,
  Target,
  Shield,
  Zap,
  Save,
  Sparkles,
  ChevronRight,
  Loader2
} from 'lucide-react'
import clsx from 'clsx'

// Oyun turleri
const gameTypes = [
  { id: 'action-rpg', name: 'Action RPG', icon: Swords, desc: 'Souls-like, hack & slash' },
  { id: 'platformer', name: 'Platformer', icon: Gamepad2, desc: '2D/3D platform oyunu' },
  { id: 'tower-defense', name: 'Tower Defense', icon: Shield, desc: 'Kule savunma, strateji' },
  { id: 'idle-clicker', name: 'Idle / Clicker', icon: Zap, desc: 'Incremental, idle game' },
  { id: 'puzzle', name: 'Puzzle', icon: Target, desc: 'Bulmaca, match-3' },
  { id: 'card-game', name: 'Card Game', icon: Sparkles, desc: 'Kart oyunu, deck builder' },
]

// Sistemler
const systems = [
  { id: 'health', name: 'Health System', icon: Heart, category: 'Core' },
  { id: 'mana', name: 'Mana/Energy', icon: Zap, category: 'Core' },
  { id: 'inventory', name: 'Inventory', icon: Backpack, category: 'Items' },
  { id: 'equipment', name: 'Equipment', icon: Shield, category: 'Items' },
  { id: 'combat-melee', name: 'Melee Combat', icon: Swords, category: 'Combat' },
  { id: 'combat-ranged', name: 'Ranged Combat', icon: Target, category: 'Combat' },
  { id: 'ai-fsm', name: 'AI State Machine', icon: Bot, category: 'AI' },
  { id: 'ai-patrol', name: 'Patrol System', icon: Bot, category: 'AI' },
  { id: 'dialogue', name: 'Dialogue System', icon: MessageSquare, category: 'Story' },
  { id: 'quest', name: 'Quest System', icon: Target, category: 'Story' },
  { id: 'save-load', name: 'Save/Load', icon: Save, category: 'Core' },
]

// Presets - tur secince otomatik secilecek sistemler
const presets: Record<string, string[]> = {
  'action-rpg': ['health', 'mana', 'inventory', 'equipment', 'combat-melee', 'ai-fsm', 'save-load'],
  'platformer': ['health', 'save-load'],
  'tower-defense': ['health', 'ai-patrol', 'save-load'],
  'idle-clicker': ['save-load'],
  'puzzle': ['save-load'],
  'card-game': ['inventory', 'save-load'],
}

export default function NewPrototypePage() {
  const navigate = useNavigate()
  const [step, setStep] = useState(1)
  const [projectName, setProjectName] = useState('')
  const [selectedType, setSelectedType] = useState<string | null>(null)
  const [selectedSystems, setSelectedSystems] = useState<Set<string>>(new Set())
  const [reference, setReference] = useState('')
  const [isGenerating, setIsGenerating] = useState(false)
  const [progress, setProgress] = useState(0)

  // Tur secilince preset'i yukle
  const handleTypeSelect = (typeId: string) => {
    setSelectedType(typeId)
    setSelectedSystems(new Set(presets[typeId] || []))
  }

  // Sistem toggle
  const toggleSystem = (systemId: string) => {
    const newSet = new Set(selectedSystems)
    if (newSet.has(systemId)) {
      newSet.delete(systemId)
    } else {
      newSet.add(systemId)
    }
    setSelectedSystems(newSet)
  }

  // Generate prototype
  const handleGenerate = async () => {
    setIsGenerating(true)
    setProgress(0)

    // Simulate progress
    const interval = setInterval(() => {
      setProgress(p => {
        if (p >= 100) {
          clearInterval(interval)
          return 100
        }
        return p + Math.random() * 15
      })
    }, 500)

    try {
      const response = await fetch('http://localhost:8000/api/prototypes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: projectName,
          gameType: selectedType,
          systems: Array.from(selectedSystems),
          reference,
        }),
      })

      if (response.ok) {
        clearInterval(interval)
        setProgress(100)
        setTimeout(() => navigate('/prototypes'), 1000)
      }
    } catch (error) {
      console.error('Generation failed:', error)
      clearInterval(interval)
      setIsGenerating(false)
    }
  }

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Yeni Prototip Olustur</h1>
        <p className="text-gray-400">PM'den Dev'e: 2 saatte calisir prototip</p>
      </div>

      {/* Progress Steps */}
      <div className="flex items-center gap-4 mb-8">
        {[1, 2, 3].map((s) => (
          <div key={s} className="flex items-center gap-2">
            <div
              className={clsx(
                'w-8 h-8 rounded-full flex items-center justify-center font-bold text-sm',
                step >= s
                  ? 'bg-violet-500 text-white'
                  : 'bg-gray-800 text-gray-500'
              )}
            >
              {s}
            </div>
            <span className={step >= s ? 'text-white' : 'text-gray-500'}>
              {s === 1 ? 'Temel Bilgi' : s === 2 ? 'Sistemler' : 'Olustur'}
            </span>
            {s < 3 && <ChevronRight className="w-4 h-4 text-gray-600" />}
          </div>
        ))}
      </div>

      {/* Step 1: Basic Info */}
      {step === 1 && (
        <div className="space-y-6">
          {/* Project Name */}
          <div>
            <label className="block text-sm font-medium mb-2">Proje Adi</label>
            <input
              type="text"
              value={projectName}
              onChange={(e) => setProjectName(e.target.value)}
              placeholder="ornek: DivineSiege2"
              className="w-full px-4 py-3 bg-gray-800 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
            />
          </div>

          {/* Game Type */}
          <div>
            <label className="block text-sm font-medium mb-4">Oyun Turu</label>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              {gameTypes.map((type) => (
                <button
                  key={type.id}
                  onClick={() => handleTypeSelect(type.id)}
                  className={clsx(
                    'p-4 rounded-xl border-2 transition-all text-left',
                    selectedType === type.id
                      ? 'border-violet-500 bg-violet-500/10'
                      : 'border-gray-700 bg-gray-800/50 hover:border-gray-600'
                  )}
                >
                  <type.icon className={clsx(
                    'w-8 h-8 mb-2',
                    selectedType === type.id ? 'text-violet-400' : 'text-gray-400'
                  )} />
                  <div className="font-medium">{type.name}</div>
                  <div className="text-xs text-gray-500">{type.desc}</div>
                </button>
              ))}
            </div>
          </div>

          {/* Reference */}
          <div>
            <label className="block text-sm font-medium mb-2">Referans (opsiyonel)</label>
            <textarea
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder='"Dark Souls meets Hades" veya "Clash Royale benzeri ama 3D"'
              rows={3}
              className="w-full px-4 py-3 bg-gray-800 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500 resize-none"
            />
          </div>

          {/* Next Button */}
          <button
            onClick={() => setStep(2)}
            disabled={!projectName || !selectedType}
            className="w-full py-3 bg-violet-500 hover:bg-violet-600 disabled:bg-gray-700 disabled:cursor-not-allowed rounded-lg font-medium transition-colors"
          >
            Devam Et
          </button>
        </div>
      )}

      {/* Step 2: Systems */}
      {step === 2 && (
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <label className="block text-sm font-medium">Sistemler</label>
            <span className="text-sm text-gray-400">
              {selectedSystems.size} sistem secili
            </span>
          </div>

          {/* Systems Grid */}
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            {systems.map((system) => (
              <button
                key={system.id}
                onClick={() => toggleSystem(system.id)}
                className={clsx(
                  'p-4 rounded-lg border transition-all text-left',
                  selectedSystems.has(system.id)
                    ? 'border-violet-500 bg-violet-500/10'
                    : 'border-gray-700 bg-gray-800/50 hover:border-gray-600'
                )}
              >
                <div className="flex items-center gap-3">
                  <system.icon className={clsx(
                    'w-5 h-5',
                    selectedSystems.has(system.id) ? 'text-violet-400' : 'text-gray-400'
                  )} />
                  <div>
                    <div className="font-medium text-sm">{system.name}</div>
                    <div className="text-xs text-gray-500">{system.category}</div>
                  </div>
                </div>
              </button>
            ))}
          </div>

          {/* Buttons */}
          <div className="flex gap-4">
            <button
              onClick={() => setStep(1)}
              className="flex-1 py-3 bg-gray-800 hover:bg-gray-700 rounded-lg font-medium transition-colors"
            >
              Geri
            </button>
            <button
              onClick={() => setStep(3)}
              className="flex-1 py-3 bg-violet-500 hover:bg-violet-600 rounded-lg font-medium transition-colors"
            >
              Devam Et
            </button>
          </div>
        </div>
      )}

      {/* Step 3: Generate */}
      {step === 3 && (
        <div className="space-y-6">
          {/* Summary */}
          <div className="p-6 bg-gray-800/50 rounded-xl border border-gray-700">
            <h3 className="font-bold text-lg mb-4">Ozet</h3>
            <div className="space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-400">Proje:</span>
                <span className="font-medium">{projectName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-400">Tur:</span>
                <span className="font-medium">
                  {gameTypes.find(t => t.id === selectedType)?.name}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-400">Sistemler:</span>
                <span className="font-medium">{selectedSystems.size} adet</span>
              </div>
              {reference && (
                <div className="pt-3 border-t border-gray-700">
                  <span className="text-gray-400">Referans:</span>
                  <p className="mt-1 text-gray-300">{reference}</p>
                </div>
              )}
            </div>
          </div>

          {/* What will be created */}
          <div className="p-6 bg-gray-800/50 rounded-xl border border-gray-700">
            <h3 className="font-bold text-lg mb-4">Olusturulacaklar</h3>
            <ul className="space-y-2 text-sm text-gray-300">
              <li className="flex items-center gap-2">
                <div className="w-2 h-2 bg-green-500 rounded-full" />
                Unity projesi hazir sahne ile
              </li>
              <li className="flex items-center gap-2">
                <div className="w-2 h-2 bg-green-500 rounded-full" />
                {selectedSystems.size} sistem scripti
              </li>
              <li className="flex items-center gap-2">
                <div className="w-2 h-2 bg-green-500 rounded-full" />
                Player prefab (sistemler bagli)
              </li>
              <li className="flex items-center gap-2">
                <div className="w-2 h-2 bg-green-500 rounded-full" />
                Temel UI (health bar, vb.)
              </li>
              <li className="flex items-center gap-2">
                <div className="w-2 h-2 bg-green-500 rounded-full" />
                README.md (handoff dokumani)
              </li>
            </ul>
          </div>

          {/* Progress */}
          {isGenerating && (
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span>Olusturuluyor...</span>
                <span>{Math.round(progress)}%</span>
              </div>
              <div className="h-2 bg-gray-800 rounded-full overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-violet-500 to-fuchsia-500 transition-all"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>
          )}

          {/* Buttons */}
          <div className="flex gap-4">
            <button
              onClick={() => setStep(2)}
              disabled={isGenerating}
              className="flex-1 py-3 bg-gray-800 hover:bg-gray-700 disabled:opacity-50 rounded-lg font-medium transition-colors"
            >
              Geri
            </button>
            <button
              onClick={handleGenerate}
              disabled={isGenerating}
              className="flex-1 py-3 bg-gradient-to-r from-violet-500 to-fuchsia-500 hover:from-violet-600 hover:to-fuchsia-600 disabled:opacity-50 rounded-lg font-medium transition-colors flex items-center justify-center gap-2"
            >
              {isGenerating ? (
                <>
                  <Loader2 className="w-5 h-5 animate-spin" />
                  Olusturuluyor...
                </>
              ) : (
                <>
                  <Sparkles className="w-5 h-5" />
                  Prototip Olustur
                </>
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
