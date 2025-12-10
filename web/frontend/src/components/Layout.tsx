import { Outlet, NavLink } from 'react-router-dom'
import {
  LayoutDashboard,
  Plus,
  FolderOpen,
  Layers,
  Settings,
  Gamepad2
} from 'lucide-react'
import clsx from 'clsx'

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/new', icon: Plus, label: 'Yeni Prototip' },
  { to: '/prototypes', icon: FolderOpen, label: 'Prototipler' },
  { to: '/templates', icon: Layers, label: 'Templates' },
  { to: '/settings', icon: Settings, label: 'Ayarlar' },
]

export default function Layout() {
  return (
    <div className="min-h-screen bg-gray-950 text-white flex">
      {/* Sidebar */}
      <aside className="w-64 bg-gray-900 border-r border-gray-800 flex flex-col">
        {/* Logo */}
        <div className="p-6 border-b border-gray-800">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-xl flex items-center justify-center">
              <Gamepad2 className="w-6 h-6" />
            </div>
            <div>
              <h1 className="font-bold text-lg">LUDU</h1>
              <p className="text-xs text-gray-400">Prototype Generator</p>
            </div>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 p-4 space-y-1">
          {navItems.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                clsx(
                  'flex items-center gap-3 px-4 py-3 rounded-lg transition-colors',
                  isActive
                    ? 'bg-violet-500/20 text-violet-400'
                    : 'text-gray-400 hover:bg-gray-800 hover:text-white'
                )
              }
            >
              <Icon className="w-5 h-5" />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        <div className="p-4 border-t border-gray-800">
          <div className="text-xs text-gray-500 text-center">
            LUDU Arts - v1.0.0
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
