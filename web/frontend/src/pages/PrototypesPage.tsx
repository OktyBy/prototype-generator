import { FolderOpen, CheckCircle, Clock, Search, Filter, AlertCircle, Inbox } from 'lucide-react'
import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'

interface Prototype {
  id: string
  name: string
  gameType: string
  systems: string[]
  status: string
  createdAt: string
}

export default function PrototypesPage() {
  const [prototypes, setPrototypes] = useState<Prototype[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchPrototypes()
  }, [])

  const fetchPrototypes = async () => {
    try {
      const res = await fetch('http://localhost:8000/api/prototypes')
      const data = await res.json()
      setPrototypes(data.prototypes || [])
    } catch (err) {
      console.error('API error:', err)
    } finally {
      setLoading(false)
    }
  }

  const filtered = prototypes.filter(p =>
    p.name.toLowerCase().includes(search.toLowerCase()) ||
    p.gameType.toLowerCase().includes(search.toLowerCase())
  )

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('tr-TR')
  }

  return (
    <div className="p-8">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold mb-2">Prototipler</h1>
          <p className="text-gray-400">{prototypes.length} prototip olusturuldu</p>
        </div>
      </div>

      {/* Search & Filter */}
      <div className="flex gap-4 mb-6">
        <div className="flex-1 relative">
          <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Prototip ara..."
            className="w-full pl-12 pr-4 py-3 bg-gray-800 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
          />
        </div>
        <button className="px-4 py-3 bg-gray-800 border border-gray-700 rounded-lg flex items-center gap-2 hover:bg-gray-700 transition-colors">
          <Filter className="w-5 h-5" />
          Filtrele
        </button>
      </div>

      {/* Content */}
      {loading ? (
        <div className="p-12 text-center text-gray-400">Yukleniyor...</div>
      ) : prototypes.length === 0 ? (
        <div className="bg-gray-800/50 border border-gray-700 rounded-xl p-12 text-center">
          <Inbox className="w-12 h-12 text-gray-600 mx-auto mb-4" />
          <p className="text-gray-400 mb-2">Henuz prototip yok</p>
          <Link to="/new" className="text-violet-400 hover:text-violet-300 text-sm">
            Ilk prototipini olustur →
          </Link>
        </div>
      ) : (
        <div className="bg-gray-800/50 border border-gray-700 rounded-xl overflow-hidden">
          <table className="w-full">
            <thead className="bg-gray-800">
              <tr>
                <th className="px-6 py-4 text-left text-sm font-medium text-gray-400">Proje</th>
                <th className="px-6 py-4 text-left text-sm font-medium text-gray-400">Tur</th>
                <th className="px-6 py-4 text-left text-sm font-medium text-gray-400">Sistemler</th>
                <th className="px-6 py-4 text-left text-sm font-medium text-gray-400">Tarih</th>
                <th className="px-6 py-4 text-left text-sm font-medium text-gray-400">Durum</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-700">
              {filtered.map((proto) => (
                <tr key={proto.id} className="hover:bg-gray-700/30 transition-colors cursor-pointer">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-violet-500/20 rounded-lg flex items-center justify-center">
                        <FolderOpen className="w-5 h-5 text-violet-400" />
                      </div>
                      <span className="font-medium">{proto.name}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-gray-300">{proto.gameType}</td>
                  <td className="px-6 py-4">
                    <span className="px-2 py-1 bg-gray-700 rounded text-sm">
                      {proto.systems?.length || 0}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-gray-400">{formatDate(proto.createdAt)}</td>
                  <td className="px-6 py-4">
                    {proto.status === 'completed' ? (
                      <span className="px-3 py-1 bg-green-500/20 text-green-400 rounded-full text-sm flex items-center gap-1 w-fit">
                        <CheckCircle className="w-4 h-4" />
                        Tamamlandi
                      </span>
                    ) : proto.status === 'failed' ? (
                      <span className="px-3 py-1 bg-red-500/20 text-red-400 rounded-full text-sm flex items-center gap-1 w-fit">
                        <AlertCircle className="w-4 h-4" />
                        Hata
                      </span>
                    ) : (
                      <span className="px-3 py-1 bg-yellow-500/20 text-yellow-400 rounded-full text-sm flex items-center gap-1 w-fit">
                        <Clock className="w-4 h-4" />
                        Devam Ediyor
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
