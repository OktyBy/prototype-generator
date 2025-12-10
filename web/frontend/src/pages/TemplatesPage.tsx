import { Swords, Gamepad2, Shield, Zap, Target, Sparkles, Download } from 'lucide-react'

const templates = [
  {
    id: 'action-rpg',
    name: 'Action RPG',
    icon: Swords,
    desc: 'Souls-like, hack & slash tarzinda oyunlar icin',
    systems: ['Health', 'Mana', 'Inventory', 'Equipment', 'Melee Combat', 'AI FSM', 'Save/Load'],
    color: 'violet'
  },
  {
    id: 'platformer',
    name: 'Platformer',
    icon: Gamepad2,
    desc: '2D/3D platform oyunlari icin',
    systems: ['Health', 'Checkpoint', 'Save/Load'],
    color: 'blue'
  },
  {
    id: 'tower-defense',
    name: 'Tower Defense',
    icon: Shield,
    desc: 'Kule savunma ve strateji oyunlari icin',
    systems: ['Health', 'Wave Spawner', 'Tower Placement', 'AI Patrol', 'Save/Load'],
    color: 'green'
  },
  {
    id: 'idle-clicker',
    name: 'Idle / Clicker',
    icon: Zap,
    desc: 'Incremental ve idle oyunlar icin',
    systems: ['Resource Manager', 'Auto Clicker', 'Upgrade System', 'Save/Load'],
    color: 'yellow'
  },
  {
    id: 'puzzle',
    name: 'Puzzle',
    icon: Target,
    desc: 'Bulmaca ve match-3 oyunlari icin',
    systems: ['Grid System', 'Score Manager', 'Level Manager', 'Save/Load'],
    color: 'pink'
  },
  {
    id: 'card-game',
    name: 'Card Game',
    icon: Sparkles,
    desc: 'Kart oyunlari ve deck builder icin',
    systems: ['Card System', 'Deck Manager', 'Hand Manager', 'Turn System', 'Save/Load'],
    color: 'orange'
  },
]

export default function TemplatesPage() {
  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Templates</h1>
        <p className="text-gray-400">Hazir sablonlar ile hizli baslangic</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {templates.map((template) => (
          <div
            key={template.id}
            className="bg-gray-800/50 border border-gray-700 rounded-xl overflow-hidden hover:border-violet-500/50 transition-colors group"
          >
            <div className={`p-6 bg-gradient-to-br from-${template.color}-500/20 to-transparent`}>
              <template.icon className={`w-12 h-12 text-${template.color}-400 mb-4`} />
              <h3 className="text-xl font-bold mb-2">{template.name}</h3>
              <p className="text-gray-400 text-sm">{template.desc}</p>
            </div>
            <div className="p-6 border-t border-gray-700">
              <div className="text-sm text-gray-400 mb-3">Dahil Sistemler:</div>
              <div className="flex flex-wrap gap-2 mb-4">
                {template.systems.map((sys) => (
                  <span
                    key={sys}
                    className="px-2 py-1 bg-gray-700 rounded text-xs"
                  >
                    {sys}
                  </span>
                ))}
              </div>
              <button className="w-full py-2 bg-violet-500/20 text-violet-400 rounded-lg hover:bg-violet-500/30 transition-colors flex items-center justify-center gap-2">
                <Download className="w-4 h-4" />
                Bu Template ile Baslat
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
