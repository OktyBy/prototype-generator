import { useState } from 'react'
import { Save, FolderOpen, Server, Key } from 'lucide-react'

export default function SettingsPage() {
  const [unityPath, setUnityPath] = useState('/Applications/Unity/Hub/Editor/6000.0.29f1')
  const [outputPath, setOutputPath] = useState('~/Unity Projects')
  const [bridgePort, setBridgePort] = useState('7777')
  const [apiKey, setApiKey] = useState('')

  return (
    <div className="p-8 max-w-2xl">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Ayarlar</h1>
        <p className="text-gray-400">LUDU Prototype Generator yapilandirmasi</p>
      </div>

      <div className="space-y-6">
        {/* Unity Path */}
        <div className="p-6 bg-gray-800/50 border border-gray-700 rounded-xl">
          <div className="flex items-center gap-3 mb-4">
            <FolderOpen className="w-5 h-5 text-violet-400" />
            <h3 className="font-bold">Unity Editor</h3>
          </div>
          <div className="space-y-4">
            <div>
              <label className="block text-sm text-gray-400 mb-2">Unity Editor Yolu</label>
              <input
                type="text"
                value={unityPath}
                onChange={(e) => setUnityPath(e.target.value)}
                className="w-full px-4 py-3 bg-gray-900 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
              />
            </div>
            <div>
              <label className="block text-sm text-gray-400 mb-2">Proje Cikti Klasoru</label>
              <input
                type="text"
                value={outputPath}
                onChange={(e) => setOutputPath(e.target.value)}
                className="w-full px-4 py-3 bg-gray-900 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
              />
            </div>
          </div>
        </div>

        {/* Bridge Settings */}
        <div className="p-6 bg-gray-800/50 border border-gray-700 rounded-xl">
          <div className="flex items-center gap-3 mb-4">
            <Server className="w-5 h-5 text-violet-400" />
            <h3 className="font-bold">Unity Bridge</h3>
          </div>
          <div>
            <label className="block text-sm text-gray-400 mb-2">Bridge Port</label>
            <input
              type="text"
              value={bridgePort}
              onChange={(e) => setBridgePort(e.target.value)}
              className="w-full px-4 py-3 bg-gray-900 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
            />
            <p className="text-xs text-gray-500 mt-2">
              Unity Editor'daki Claude MCP Bridge ile ayni port olmali
            </p>
          </div>
        </div>

        {/* API Key */}
        <div className="p-6 bg-gray-800/50 border border-gray-700 rounded-xl">
          <div className="flex items-center gap-3 mb-4">
            <Key className="w-5 h-5 text-violet-400" />
            <h3 className="font-bold">Claude API</h3>
          </div>
          <div>
            <label className="block text-sm text-gray-400 mb-2">API Key (opsiyonel)</label>
            <input
              type="password"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder="sk-ant-..."
              className="w-full px-4 py-3 bg-gray-900 border border-gray-700 rounded-lg focus:outline-none focus:border-violet-500"
            />
            <p className="text-xs text-gray-500 mt-2">
              Gelismis AI ozellikler icin (kod aciklamasi, otomatik duzeltme)
            </p>
          </div>
        </div>

        {/* Save Button */}
        <button className="w-full py-3 bg-violet-500 hover:bg-violet-600 rounded-lg font-medium transition-colors flex items-center justify-center gap-2">
          <Save className="w-5 h-5" />
          Ayarlari Kaydet
        </button>
      </div>
    </div>
  )
}
