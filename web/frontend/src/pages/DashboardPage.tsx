import { Link } from 'react-router-dom'
import { useState, useEffect } from 'react'
import {
  Plus,
  FolderOpen,
  Clock,
  CheckCircle,
  AlertCircle,
  TrendingUp,
  Inbox
} from 'lucide-react'

interface Prototype {
  id: string
  name: string
  gameType: string
  status: string
  createdAt: string
}

interface Stats {
  total: number
  thisWeek: number
  avgDuration: string
  successRate: number
}

export default function DashboardPage() {
  const [prototypes, setPrototypes] = useState<Prototype[]>([])
  const [stats, setStats] = useState<Stats>({ total: 0, thisWeek: 0, avgDuration: '-', successRate: 0 })
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchData()
  }, [])

  const fetchData = async () => {
    try {
      const res = await fetch('http://localhost:8000/api/prototypes')
      const data = await res.json()
      const list = data.prototypes || []

      setPrototypes(list.slice(0, 5)) // Son 5 tane

      // Stats hesapla
      const completed = list.filter((p: Prototype) => p.status === 'completed')
      const oneWeekAgo = new Date()
      oneWeekAgo.setDate(oneWeekAgo.getDate() - 7)
      const thisWeek = list.filter((p: Prototype) => new Date(p.createdAt) > oneWeekAgo)

      setStats({
        total: list.length,
        thisWeek: thisWeek.length,
        avgDuration: list.length > 0 ? '~2 saat' : '-',
        successRate: list.length > 0 ? Math.round((completed.length / list.length) * 100) : 0
      })
    } catch (err) {
      console.error('API error:', err)
    } finally {
      setLoading(false)
    }
  }

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr)
    const now = new Date()
    const diff = now.getTime() - date.getTime()
    const hours = Math.floor(diff / (1000 * 60 * 60))

    if (hours < 1) return 'Az once'
    if (hours < 24) return `${hours} saat once`
    const days = Math.floor(hours / 24)
    return `${days} gun once`
  }

  const statCards = [
    { label: 'Toplam Prototip', value: stats.total.toString(), icon: FolderOpen, color: 'violet' },
    { label: 'Bu Hafta', value: stats.thisWeek.toString(), icon: TrendingUp, color: 'green' },
    { label: 'Ortalama Sure', value: stats.avgDuration, icon: Clock, color: 'blue' },
    { label: 'Basari Orani', value: `%${stats.successRate}`, icon: CheckCircle, color: 'emerald' },
  ]

  return (
    <div className="p-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold mb-2">Dashboard</h1>
          <p className="text-gray-400">Hosgeldin! Bugun ne prototipleyecegiz?</p>
        </div>
        <Link
          to="/new"
          className="px-6 py-3 bg-gradient-to-r from-violet-500 to-fuchsia-500 hover:from-violet-600 hover:to-fuchsia-600 rounded-lg font-medium transition-colors flex items-center gap-2"
        >
          <Plus className="w-5 h-5" />
          Yeni Prototip
        </Link>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {statCards.map((stat) => (
          <div
            key={stat.label}
            className="p-6 bg-gray-800/50 border border-gray-700 rounded-xl"
          >
            <div className="flex items-center gap-4">
              <div className={`p-3 rounded-lg bg-${stat.color}-500/20`}>
                <stat.icon className={`w-6 h-6 text-${stat.color}-400`} />
              </div>
              <div>
                <div className="text-2xl font-bold">{stat.value}</div>
                <div className="text-sm text-gray-400">{stat.label}</div>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Recent Prototypes */}
      <div className="bg-gray-800/50 border border-gray-700 rounded-xl">
        <div className="p-6 border-b border-gray-700">
          <h2 className="text-xl font-bold">Son Prototipler</h2>
        </div>

        {loading ? (
          <div className="p-12 text-center text-gray-400">Yukleniyor...</div>
        ) : prototypes.length === 0 ? (
          <div className="p-12 text-center">
            <Inbox className="w-12 h-12 text-gray-600 mx-auto mb-4" />
            <p className="text-gray-400">Henuz prototip yok</p>
            <Link to="/new" className="text-violet-400 hover:text-violet-300 text-sm">
              Ilk prototipini olustur →
            </Link>
          </div>
        ) : (
          <div className="divide-y divide-gray-700">
            {prototypes.map((proto) => (
              <div
                key={proto.id}
                className="p-6 flex items-center justify-between hover:bg-gray-700/30 transition-colors"
              >
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 bg-violet-500/20 rounded-lg flex items-center justify-center">
                    <FolderOpen className="w-6 h-6 text-violet-400" />
                  </div>
                  <div>
                    <div className="font-medium">{proto.name}</div>
                    <div className="text-sm text-gray-400">{proto.gameType}</div>
                  </div>
                </div>
                <div className="flex items-center gap-6">
                  <span className="text-sm text-gray-400">{formatDate(proto.createdAt)}</span>
                  {proto.status === 'completed' ? (
                    <span className="px-3 py-1 bg-green-500/20 text-green-400 rounded-full text-sm flex items-center gap-1">
                      <CheckCircle className="w-4 h-4" />
                      Tamamlandi
                    </span>
                  ) : proto.status === 'failed' ? (
                    <span className="px-3 py-1 bg-red-500/20 text-red-400 rounded-full text-sm flex items-center gap-1">
                      <AlertCircle className="w-4 h-4" />
                      Hata
                    </span>
                  ) : (
                    <span className="px-3 py-1 bg-yellow-500/20 text-yellow-400 rounded-full text-sm flex items-center gap-1">
                      <AlertCircle className="w-4 h-4" />
                      Devam Ediyor
                    </span>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}

        {prototypes.length > 0 && (
          <div className="p-4 border-t border-gray-700">
            <Link
              to="/prototypes"
              className="text-violet-400 hover:text-violet-300 text-sm"
            >
              Tum Prototipleri Gor →
            </Link>
          </div>
        )}
      </div>
    </div>
  )
}
