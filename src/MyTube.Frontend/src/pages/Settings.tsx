import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { 
  Settings as SettingsIcon, User, Shield, Key, Bell, Cpu, Tv,
  Globe, ShieldCheck, Check, Cookie, Network, Save, RefreshCw, Loader2, Download,
  Tag, SlidersHorizontal, Sparkles, Power
} from 'lucide-react';
import { AiQuotaDashboard } from '../components/AiQuotaDashboard';
import { FileNamingStudio } from '../components/FileNamingStudio';
import { SystemHealthDashboard } from '../components/SystemHealthDashboard';
import { User as UserType, DownloaderSettings } from '../types';
import { api } from '../services/api';
import { useToast } from '../components/Toast';

interface SettingsProps {
  currentUser: UserType | null;
}

export const Settings: React.FC<SettingsProps> = ({ currentUser }) => {
  const [searchParams, setSearchParams] = useSearchParams();
  const currentTab = searchParams.get('tab') || 'ai';

  const validTabs = ['ai', 'arr', 'ytdlp', 'naming', 'account', 'power'];
  const activeTab = validTabs.includes(currentTab) ? currentTab : 'ai';

  const setActiveTab = (tab: string) => {
    setSearchParams({ tab });
  };

  const tabs = [
    { id: 'ai', label: 'AI Models & Quota Dashboard', icon: Cpu },
    { id: 'arr', label: 'Radarr & Sonarr (*arr)', icon: Tv },
    { id: 'ytdlp', label: 'yt-dlp Downloader & Bypass', icon: Download },
    { id: 'naming', label: 'File Naming Studio', icon: Tag },
    ...(currentUser?.role === 'Administrator' ? [{ id: 'power', label: 'Homelab Host & Power', icon: Power }] : []),
    { id: 'account', label: 'User Account Details', icon: User },
  ];

  return (
    <div className="flex-1 p-6 overflow-y-auto text-neutral-100 bg-[#090a0c]">
      <div className="max-w-5xl mx-auto space-y-6">
        
        {/* Page Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-[#21242d]">
          <div className="flex items-center gap-3">
            <div className="p-3 rounded-2xl bg-emerald-500/10 border border-emerald-500/20 text-[#3cd69b]">
              <SettingsIcon size={24} />
            </div>
            <div>
              <h1 className="text-xl font-bold font-heading text-neutral-100">Settings & Configuration</h1>
              <p className="text-xs text-neutral-400">Manage AI infrastructure, media servers (Radarr & Sonarr), yt-dlp downloader bypass, and library naming</p>
            </div>
          </div>
        </div>

        {/* Tab Navigation Bar */}
        <div className="flex items-center gap-2 p-1.5 bg-[#121418] border border-[#21242d] rounded-2xl overflow-x-auto scrollbar-none">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-all cursor-pointer ${
                  isActive
                    ? 'bg-[#3cd69b] text-neutral-950 shadow-md shadow-emerald-950/20 font-bold'
                    : 'text-neutral-400 hover:text-neutral-200 hover:bg-[#1a1d24]'
                }`}
              >
                <Icon size={15} />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab Content Panes */}
        <div className="pt-2">
          {/* TAB 1: AI Models & Quota Dashboard */}
          {activeTab === 'ai' && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                    <Cpu size={18} className="text-[#3cd69b]" />
                    AI Infrastructure & Quota Status
                  </h2>
                  <p className="text-xs text-neutral-400 mt-0.5">
                    Unified multi-provider AI engine (Gemini, Groq, Cerebras, Ollama, DeepSeek) with real-time TPM/RPM rate quota tracking
                  </p>
                </div>
              </div>
              <AiQuotaDashboard isEmbedded={true} />
            </div>
          )}

          {/* TAB 2: Radarr and Sonarr Media Servers */}
          {activeTab === 'arr' && (
            <div className="space-y-6">
              <div>
                <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                  <Tv size={18} className="text-[#3cd69b]" />
                  Media Automation Integration (*arr)
                </h2>
                <p className="text-xs text-neutral-400 mt-0.5">
                  Configure connectivity and library paths for Radarr (Movies) and Sonarr (TV Series)
                </p>
              </div>

              {/* Radarr Server Integration Settings */}
              <RadarrSettingsCard currentUser={currentUser} />

              {/* Sonarr Server Integration Settings */}
              <SonarrSettingsCard currentUser={currentUser} />
            </div>
          )}

          {/* TAB 3: yt-dlp & Downloader Settings */}
          {activeTab === 'ytdlp' && (
            <div className="space-y-4">
              <div>
                <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                  <Download size={18} className="text-[#3cd69b]" />
                  yt-dlp Downloader & Geo-Bypass Settings
                </h2>
                <p className="text-xs text-neutral-400 mt-0.5">
                  Configure origin header spoofing, proxy verification, IPv4 enforcement, age-gate arguments, and authentication cookies
                </p>
              </div>
              <DownloaderSettingsCard currentUser={currentUser} />
            </div>
          )}

          {/* TAB 4: File Naming Studio */}
          {activeTab === 'naming' && (
            <div className="space-y-4">
              <div>
                <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                  <Tag size={18} className="text-[#3cd69b]" />
                  Media File Naming & Organization Studio
                </h2>
                <p className="text-xs text-neutral-400 mt-0.5">
                  Define metadata naming patterns, sanitize characters, preview bulk renames, and manage the recycle bin
                </p>
              </div>
              <FileNamingStudio />
            </div>
          )}

          {/* TAB 5: User Account Details */}
          {activeTab === 'account' && (
            <div className="space-y-4">
              <div>
                <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                  <User size={18} className="text-[#3cd69b]" />
                  User Account & Permissions
                </h2>
                <p className="text-xs text-neutral-400 mt-0.5">
                  Account profile details, authentication role, and security access permissions
                </p>
              </div>

              <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl">
                <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 text-xs">
                  <div className="p-3.5 rounded-xl bg-[#0c0d10] border border-[#21242d]">
                    <span className="text-neutral-500 block mb-1">Full Name</span>
                    <span className="font-semibold text-neutral-200">{currentUser?.fullName || currentUser?.username || 'N/A'}</span>
                  </div>

                  <div className="p-3.5 rounded-xl bg-[#0c0d10] border border-[#21242d]">
                    <span className="text-neutral-500 block mb-1">Username</span>
                    <span className="font-semibold text-neutral-200">@{currentUser?.username || 'N/A'}</span>
                  </div>

                  <div className="p-3.5 rounded-xl bg-[#0c0d10] border border-[#21242d]">
                    <span className="text-neutral-500 block mb-1">Email Address</span>
                    <span className="font-semibold text-neutral-200 truncate block">{currentUser?.email || 'N/A'}</span>
                  </div>

                  <div className="p-3.5 rounded-xl bg-[#0c0d10] border border-[#21242d]">
                    <span className="text-neutral-500 block mb-1">Role / Permissions</span>
                    <span className="inline-flex items-center gap-1 font-bold text-[#3cd69b] capitalize">
                      <Shield size={12} />
                      {currentUser?.role || 'User'}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* TAB 6: Homelab Host & Power */}
          {activeTab === 'power' && (
            <div className="space-y-4">
              <div>
                <h2 className="text-base font-bold text-neutral-200 flex items-center gap-2">
                  <Power size={18} className="text-rose-400" />
                  Homelab Physical Host & Power Management
                </h2>
                <p className="text-xs text-neutral-400 mt-0.5">
                  Monitor hardware health, media drive partitions, service statuses, and cleanly power off or reboot the physical server machine
                </p>
              </div>
              <SystemHealthDashboard />
            </div>
          )}
        </div>

      </div>
    </div>
  );
};

interface RadarrSettingsCardProps {
  currentUser: UserType | null;
}

const RadarrSettingsCard: React.FC<RadarrSettingsCardProps> = ({ currentUser }) => {
  const { showToast } = useToast();
  
  const [baseUrl, setBaseUrl] = React.useState('http://localhost:7878');
  const [apiKey, setApiKey] = React.useState('');
  const [rootFolderPath, setRootFolderPath] = React.useState('');
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [testing, setTesting] = React.useState(false);
  const [testResult, setTestResult] = React.useState<{ isConnected: boolean; message: string; version?: string } | null>(null);

  React.useEffect(() => {
    const loadSettings = async () => {
      try {
        setLoading(true);
        // Parallelize settings and status fetching to eliminate waterfall
        const [settingsRes, statusRes] = await Promise.allSettled([
          currentUser?.role === 'Administrator' ? api.getRadarrSettings() : Promise.resolve(null),
          api.getRadarrStatus()
        ]);

        if (settingsRes.status === 'fulfilled' && settingsRes.value) {
          const s = settingsRes.value;
          setBaseUrl(s.baseUrl || 'http://localhost:7878');
          setApiKey(s.apiKey || '');
          setRootFolderPath(s.rootFolderPath || '');
        }

        if (statusRes.status === 'fulfilled' && statusRes.value) {
          const status = statusRes.value;
          setTestResult({
            isConnected: status.isConnected,
            message: status.isConnected ? `Connected to ${status.appName || 'Radarr'}` : (status.errorMessage || 'Offline'),
            version: status.version
          });
        }
      } catch (err: any) {
        console.error('Failed to load Radarr settings:', err);
      } finally {
        setLoading(false);
      }
    };
    loadSettings();
  }, [currentUser]);

  const handleTest = async () => {
    if (!baseUrl) return;
    setTesting(true);
    try {
      const res = await api.testRadarrConnection(baseUrl, apiKey.includes('****') ? '' : apiKey);
      setTestResult({
        isConnected: res.isConnected,
        message: res.isConnected ? `Connected to ${res.appName || 'Radarr'}` : (res.errorMessage || 'Connection failed'),
        version: res.version
      });
      if (res.isConnected) {
        showToast('success', `Connected to ${res.appName || 'Radarr'}!`);
      } else {
        showToast('error', res.errorMessage || 'Connection failed');
      }
    } catch (err: any) {
      setTestResult({
        isConnected: false,
        message: err.message || 'Connection test failed'
      });
      showToast('error', err.message || 'Connection test failed');
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.updateRadarrSettings({
        baseUrl,
        apiKey: apiKey.includes('****') ? undefined : apiKey,
        rootFolderPath: rootFolderPath || undefined
      });
      showToast('success', 'Radarr settings saved successfully!');
      setTestResult({
        isConnected: true,
        message: 'Settings saved successfully. Testing connection...'
      });
      await handleTest();
    } catch (err: any) {
      setTestResult({
        isConnected: false,
        message: err.message || 'Failed to save settings'
      });
      showToast('error', err.message || 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-[#3cd69b]/10 text-[#3cd69b]">
            <Key size={18} />
          </div>
          <div>
            <h2 className="text-sm font-bold text-neutral-200">Radarr Server Integration</h2>
            <p className="text-xs text-neutral-400">
              Connect your Radarr instance to enable movie search, release indexing, and automated downloads
            </p>
          </div>
        </div>

        {testResult && (
          <div className={`px-3 py-1.5 rounded-xl border text-xs font-semibold flex items-center gap-1.5 ${
            testResult.isConnected 
              ? 'bg-emerald-950/40 border-emerald-500/30 text-emerald-300' 
              : 'bg-amber-950/40 border-amber-500/30 text-amber-300'
          }`}>
            <span className={`w-2 h-2 rounded-full ${testResult.isConnected ? 'bg-emerald-400' : 'bg-amber-400'}`} />
            <span>{testResult.isConnected ? 'Radarr Connected' : 'Radarr Offline'}</span>
            {testResult.version && <span className="text-[10px] font-mono text-neutral-400">v{testResult.version}</span>}
          </div>
        )}
      </div>

      {currentUser?.role === 'Administrator' ? (
        <form onSubmit={handleSave} className="space-y-4 pt-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
            <div>
              <label htmlFor="radarr-url" className="block text-neutral-400 font-semibold mb-1.5">Radarr Base URL</label>
              <input
                id="radarr-url"
                name="radarrUrl"
                type="text"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                placeholder="http://localhost:7878"
                className="w-full bg-[#0c0d10] border border-[#21242d] focus:border-[#3cd69b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#3cd69b] rounded-xl px-3.5 py-2.5 text-neutral-200 transition-all"
                required
              />
              <span className="text-[11px] text-neutral-500 mt-1 block">Default port for Radarr is 7878</span>
            </div>

            <div>
              <label htmlFor="radarr-apikey" className="block text-neutral-400 font-semibold mb-1.5">Radarr API Key</label>
              <input
                id="radarr-apikey"
                name="radarrApiKey"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="Paste API Key from Radarr -> Settings -> General"
                autoComplete="current-password"
                className="w-full bg-[#0c0d10] border border-[#21242d] focus:border-[#3cd69b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#3cd69b] rounded-xl px-3.5 py-2.5 text-neutral-200 transition-all font-mono"
              />
              <span className="text-[11px] text-neutral-500 mt-1 block">Found in Radarr Settings &gt; General &gt; Security</span>
            </div>
          </div>

          <div>
            <label htmlFor="radarr-rootfolder" className="block text-neutral-400 font-semibold mb-1.5 text-xs">Default Root Folder Path (Optional)</label>
            <input
              id="radarr-rootfolder"
              name="radarrRootFolder"
              type="text"
              value={rootFolderPath}
              onChange={(e) => setRootFolderPath(e.target.value)}
              placeholder="/movies or D:\Movies"
              className="w-full bg-[#0c0d10] border border-[#21242d] focus:border-[#3cd69b] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#3cd69b] rounded-xl px-3.5 py-2.5 text-xs text-neutral-200 transition-all"
            />
            <span className="text-[11px] text-neutral-500 mt-1 block">If empty, Radarr's first configured root folder will be used</span>
          </div>

          {testResult?.message && (
            <div className={`p-3 rounded-xl text-xs ${
              testResult.isConnected ? 'bg-emerald-950/20 text-emerald-300 border border-emerald-800/30' : 'bg-red-950/20 text-red-300 border border-red-800/30'
            }`}>
              {testResult.message}
            </div>
          )}

          <div className="flex items-center gap-3 pt-2">
            <button
              type="button"
              onClick={handleTest}
              disabled={testing || !baseUrl}
              className="px-4 py-2 rounded-xl bg-[#1e222a] hover:bg-[#282d38] border border-[#2d323e] text-neutral-200 text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50"
            >
              {testing ? 'Testing...' : 'Test Connection'}
            </button>

            <button
              type="submit"
              disabled={saving}
              className="px-5 py-2 rounded-xl bg-[#3cd69b] hover:bg-[#32b885] text-black text-xs font-bold transition-colors cursor-pointer disabled:opacity-50"
            >
              {saving ? 'Saving...' : 'Save Radarr Configuration'}
            </button>
          </div>
        </form>
      ) : (
        <div className="text-xs text-neutral-400 p-3 rounded-xl bg-[#0c0d10] border border-[#21242d]">
          Radarr settings can be configured by an Administrator.
        </div>
      )}
    </div>
  );
};

const SonarrSettingsCard: React.FC<{ currentUser: UserType | null }> = ({ currentUser }) => {
  const { showToast } = useToast();
  
  const [baseUrl, setBaseUrl] = React.useState('http://localhost:8989');
  const [apiKey, setApiKey] = React.useState('');
  const [rootFolderPath, setRootFolderPath] = React.useState('');
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [testing, setTesting] = React.useState(false);
  const [testResult, setTestResult] = React.useState<{ isConnected: boolean; message: string; version?: string } | null>(null);

  React.useEffect(() => {
    const loadSettings = async () => {
      try {
        setLoading(true);
        // Parallelize settings and status fetching to eliminate waterfall
        const [settingsRes, statusRes] = await Promise.allSettled([
          currentUser?.role === 'Administrator' ? api.getSonarrSettings() : Promise.resolve(null),
          api.getSonarrStatus()
        ]);

        if (settingsRes.status === 'fulfilled' && settingsRes.value) {
          const s = settingsRes.value;
          setBaseUrl(s.baseUrl || 'http://localhost:8989');
          setApiKey(s.apiKey || '');
          setRootFolderPath(s.rootFolderPath || '');
        }

        if (statusRes.status === 'fulfilled' && statusRes.value) {
          const status = statusRes.value;
          setTestResult({
            isConnected: status.isConnected,
            message: status.isConnected ? `Connected to ${status.appName || 'Sonarr'}` : (status.errorMessage || 'Offline'),
            version: status.version
          });
        }
      } catch (err: any) {
        console.error('Failed to load Sonarr settings:', err);
      } finally {
        setLoading(false);
      }
    };
    loadSettings();
  }, [currentUser]);

  const handleTest = async () => {
    if (!baseUrl) return;
    setTesting(true);
    try {
      const res = await api.testSonarrConnection({ 
        baseUrl, 
        apiKey: apiKey.includes('****') ? '' : apiKey 
      });
      setTestResult({
        isConnected: res.isConnected,
        message: res.isConnected ? `Connected to ${res.appName || 'Sonarr'}` : (res.errorMessage || 'Connection failed'),
        version: res.version
      });
      if (res.isConnected) {
        showToast('success', `Connected to ${res.appName || 'Sonarr'}!`);
      } else {
        showToast('error', res.errorMessage || 'Connection failed');
      }
    } catch (err: any) {
      setTestResult({
        isConnected: false,
        message: err.message || 'Connection test failed'
      });
      showToast('error', err.message || 'Connection test failed');
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.updateSonarrSettings({
        baseUrl,
        apiKey: apiKey.includes('****') ? undefined : apiKey,
        rootFolderPath: rootFolderPath || undefined,
        qualityProfileId: 1
      });
      showToast('success', 'Sonarr settings saved successfully!');
      setTestResult({
        isConnected: true,
        message: 'Settings saved successfully. Testing connection...'
      });
      await handleTest();
    } catch (err: any) {
      setTestResult({
        isConnected: false,
        message: err.message || 'Failed to save settings'
      });
      showToast('error', err.message || 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-xl bg-blue-500/10 text-blue-400">
            <Tv size={18} />
          </div>
          <div>
            <h2 className="text-sm font-bold text-neutral-200 uppercase tracking-wider">
              Sonarr TV Series Server Integration
            </h2>
            <p className="text-xs text-neutral-400">
              Automated TV series tracking, seasons management, and episode indexer downloads
            </p>
          </div>
        </div>

        {testResult && (
          <div className={`px-3 py-1 rounded-full text-xs font-semibold flex items-center gap-1.5 border ${
            testResult.isConnected 
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20' 
              : 'bg-red-500/10 text-red-400 border-red-500/20'
          }`}>
            <span className={`w-2 h-2 rounded-full ${testResult.isConnected ? 'bg-emerald-400' : 'bg-red-400'}`} />
            {testResult.isConnected ? `Connected (v${testResult.version || '3.x'})` : 'Offline / Error'}
          </div>
        )}
      </div>

      {currentUser?.role === 'Administrator' ? (
        <form onSubmit={handleSave} className="space-y-4 pt-2">
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4 text-xs">
            <div className="space-y-1.5">
              <label htmlFor="sonarr-url" className="text-neutral-400 font-semibold block">Sonarr Server URL</label>
              <input
                id="sonarr-url"
                name="sonarrUrl"
                type="text"
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                placeholder="http://localhost:8989"
                required
                className="w-full px-3 py-2 bg-[#0c0d10] border border-[#21242d] rounded-xl text-neutral-200 focus:outline-none focus:border-blue-500 focus-visible:ring-2 focus-visible:ring-blue-500 transition-all"
              />
            </div>

            <div className="space-y-1.5">
              <label htmlFor="sonarr-apikey" className="text-neutral-400 font-semibold block">API Key</label>
              <input
                id="sonarr-apikey"
                name="sonarrApiKey"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="Enter Sonarr API Key..."
                autoComplete="current-password"
                className="w-full px-3 py-2 bg-[#0c0d10] border border-[#21242d] rounded-xl text-neutral-200 focus:outline-none focus:border-blue-500 focus-visible:ring-2 focus-visible:ring-blue-500 transition-all font-mono"
              />
            </div>

            <div className="space-y-1.5">
              <label htmlFor="sonarr-rootfolder" className="text-neutral-400 font-semibold block">Root TV Folder Path</label>
              <input
                id="sonarr-rootfolder"
                name="sonarrRootFolder"
                type="text"
                value={rootFolderPath}
                onChange={(e) => setRootFolderPath(e.target.value)}
                placeholder="/tv or /srv/mytube/videos/TV"
                className="w-full px-3 py-2 bg-[#0c0d10] border border-[#21242d] rounded-xl text-neutral-200 focus:outline-none focus:border-blue-500 focus-visible:ring-2 focus-visible:ring-blue-500 transition-all"
              />
            </div>
          </div>

          <div className="flex items-center gap-3 pt-2">
            <button
              type="button"
              onClick={handleTest}
              disabled={testing || !baseUrl}
              className="px-4 py-2 rounded-xl bg-[#1e222a] hover:bg-[#282d38] border border-[#2d323e] text-neutral-200 text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50"
            >
              {testing ? 'Testing...' : 'Test Connection'}
            </button>

            <button
              type="submit"
              disabled={saving}
              className="px-5 py-2 rounded-xl bg-blue-500 hover:bg-blue-600 text-white text-xs font-bold transition-colors cursor-pointer disabled:opacity-50"
            >
              {saving ? 'Saving...' : 'Save Sonarr Configuration'}
            </button>
          </div>
        </form>
      ) : (
        <div className="text-xs text-neutral-400 p-3 rounded-xl bg-[#0c0d10] border border-[#21242d]">
          Sonarr settings can be configured by an Administrator.
        </div>
      )}
    </div>
  );
};

interface DownloaderSettingsCardProps {
  currentUser: UserType | null;
}

const DownloaderSettingsCard: React.FC<DownloaderSettingsCardProps> = ({ currentUser }) => {
  const { showToast } = useToast();
  const isAdmin = currentUser?.role === 'Administrator';

  const [settings, setSettings] = useState<DownloaderSettings>({
    geoBypass: true,
    xff: 'default',
    geoVerificationProxy: '',
    proxy: '',
    forceIpv4: true,
    cookiesPath: '',
    hasCookiesFile: false,
    extractorArgs: '',
    ytDlpVersion: ''
  });
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [updatingYtDlp, setUpdatingYtDlp] = useState(false);

  const loadSettings = async () => {
    if (!isAdmin) return;
    setLoading(true);
    try {
      const s = await api.getDownloaderSettings();
      if (s) setSettings(s);
    } catch (err: any) {
      console.error('Failed to load downloader settings:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSettings();
  }, [isAdmin]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isAdmin) return;
    setSaving(true);
    try {
      const res = await api.updateDownloaderSettings(settings);
      if (res.success) {
        showToast('success', 'Downloader geo-restriction bypass settings saved successfully!', 'Settings Saved');
        loadSettings();
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to save downloader settings', 'Error');
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateBinary = async () => {
    setUpdatingYtDlp(true);
    try {
      showToast('info', 'Triggering yt-dlp binary update in background...', 'Updating yt-dlp');
      await api.updateYtDlp();
      showToast('success', 'yt-dlp update process triggered.', 'Updated');
      setTimeout(loadSettings, 3000);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to update yt-dlp binary', 'Error');
    } finally {
      setUpdatingYtDlp(false);
    }
  };

  return (
    <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl">
      <div className="flex items-center justify-between mb-4 pb-3 border-b border-[#21242d]">
        <div className="flex items-center gap-2">
          <ShieldCheck size={18} className="text-[#3cd69b]" />
          <div>
            <h2 className="text-sm font-bold text-neutral-200 uppercase tracking-wider">
              yt-dlp Geo-Restriction & Bypass Settings
            </h2>
            <span className="text-[11px] text-neutral-400">
              Configure origin header spoofing, proxy verification, IPv4 enforcement, and client emulations
            </span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {settings.ytDlpVersion && (
            <span className="text-xs font-mono px-2.5 py-1 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-[#3cd69b]">
              v{settings.ytDlpVersion}
            </span>
          )}
          {isAdmin && (
            <button
              type="button"
              onClick={handleUpdateBinary}
              disabled={updatingYtDlp}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-[#2d323e] hover:bg-[#1e222a] text-xs text-neutral-300 transition-colors cursor-pointer disabled:opacity-50"
            >
              {updatingYtDlp ? <Loader2 size={12} className="animate-spin" /> : <RefreshCw size={12} />}
              Update yt-dlp
            </button>
          )}
        </div>
      </div>

      {isAdmin ? (
        <form onSubmit={handleSave} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Geo Bypass Toggle */}
            <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] flex items-start justify-between gap-3">
              <div>
                <span className="text-xs font-semibold text-neutral-200 block">
                  Bypass Geographic Restrictions (<code>--geo-bypass</code>)
                </span>
                <span className="text-[11px] text-neutral-500 block mt-1">
                  Enables automatic IP header spoofing for regional and country-blocked videos.
                </span>
              </div>
              <input
                type="checkbox"
                checked={settings.geoBypass}
                onChange={(e) => setSettings({ ...settings, geoBypass: e.target.checked })}
                className="w-4 h-4 accent-[#3cd69b] mt-1 cursor-pointer"
              />
            </div>

            {/* Force IPv4 Toggle */}
            <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] flex items-start justify-between gap-3">
              <div>
                <span className="text-xs font-semibold text-neutral-200 block">
                  Force IPv4 Connections (<code>-4 / --force-ipv4</code>)
                </span>
                <span className="text-[11px] text-neutral-500 block mt-1">
                  Bypasses IPv6 blocks and rate-limits frequently imposed by Cloudflare and YouTube.
                </span>
              </div>
              <input
                type="checkbox"
                checked={settings.forceIpv4}
                onChange={(e) => setSettings({ ...settings, forceIpv4: e.target.checked })}
                className="w-4 h-4 accent-[#3cd69b] mt-1 cursor-pointer"
              />
            </div>
          </div>

          {/* XFF Header */}
          <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-xs font-semibold text-neutral-200 flex items-center gap-1.5">
                <Network size={14} className="text-[#3cd69b]" />
                Fake X-Forwarded-For HTTP Header (<code>--xff</code>)
              </label>
              <span className="text-[11px] text-neutral-500">
                default, country code (US, GB, DE, NL), or CIDR
              </span>
            </div>
            <input
              type="text"
              value={settings.xff || ''}
              onChange={(e) => setSettings({ ...settings, xff: e.target.value })}
              placeholder="default"
              className="w-full px-3 py-2 text-xs bg-[#121418] border border-[#282d38] rounded-lg text-neutral-200 placeholder:text-neutral-600 focus:outline-none focus:border-[#3cd69b]"
            />
            <div className="flex flex-wrap items-center gap-1.5 pt-1">
              <span className="text-[10px] text-neutral-500 mr-1">Quick Presets:</span>
              {['default', 'US', 'GB', 'DE', 'NL', 'IN', 'never'].map((code) => (
                <button
                  key={code}
                  type="button"
                  onClick={() => setSettings({ ...settings, xff: code })}
                  className={`px-2 py-0.5 text-[10px] font-mono rounded border transition-colors cursor-pointer ${
                    settings.xff === code
                      ? 'bg-emerald-500/20 text-[#3cd69b] border-emerald-500/40 font-bold'
                      : 'bg-[#181a20] text-neutral-400 border-[#282d38] hover:text-white'
                  }`}
                >
                  {code}
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Geo-Verification Proxy */}
            <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] space-y-1.5">
              <label className="text-xs font-semibold text-neutral-200 flex items-center gap-1.5">
                <Globe size={14} className="text-[#3cd69b]" />
                Geo-Verification Proxy (<code>--geo-verification-proxy</code>)
              </label>
              <input
                type="text"
                value={settings.geoVerificationProxy || ''}
                onChange={(e) => setSettings({ ...settings, geoVerificationProxy: e.target.value })}
                placeholder="http://user:password@host:port"
                className="w-full px-3 py-2 text-xs bg-[#121418] border border-[#282d38] rounded-lg text-neutral-200 placeholder:text-neutral-600 focus:outline-none focus:border-[#3cd69b]"
              />
              <span className="text-[10px] text-neutral-500 block">
                Proxy used only to verify IP on geo-blocked platforms while downloading directly.
              </span>
            </div>

            {/* General Proxy */}
            <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] space-y-1.5">
              <label className="text-xs font-semibold text-neutral-200 flex items-center gap-1.5">
                <Globe size={14} className="text-blue-400" />
                Download Proxy (<code>--proxy</code>)
              </label>
              <input
                type="text"
                value={settings.proxy || ''}
                onChange={(e) => setSettings({ ...settings, proxy: e.target.value })}
                placeholder="Leave blank for direct download, or socks5://... / http://..."
                className="w-full px-3 py-2 text-xs bg-[#121418] border border-[#282d38] rounded-lg text-neutral-200 placeholder:text-neutral-600 focus:outline-none focus:border-[#3cd69b]"
              />
              <span className="text-[10px] text-amber-500/80 block">
                Optional. Note: VPN/Datacenter proxies (like Gluetun) often trigger YouTube bot verification ("Sign in to confirm you’re not a bot"). Leave empty for direct downloads.
              </span>
            </div>
          </div>

          {/* Age-gate & Mobile Extractor Args */}
          <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] space-y-1.5">
            <label className="text-xs font-semibold text-neutral-200 flex items-center gap-1.5">
              <ShieldCheck size={14} className="text-purple-400" />
              Client Spoofing & Age-Gate Bypass (<code>--extractor-args</code>)
            </label>
            <input
              type="text"
              value={settings.extractorArgs || ''}
              onChange={(e) => setSettings({ ...settings, extractorArgs: e.target.value })}
              placeholder="Leave empty for all qualities (or e.g. youtube:skip=dash)"
              className="w-full px-3 py-2 text-xs bg-[#121418] border border-[#282d38] rounded-lg text-neutral-200 placeholder:text-neutral-600 focus:outline-none focus:border-[#3cd69b]"
            />
            <span className="text-[10px] text-neutral-500 block">
              Bypasses YouTube 18+ age gates, sign-in demands, and bot detection without logging into an account.
            </span>
          </div>

          {/* Cookies File */}
          <div className="p-4 rounded-xl bg-[#0c0d10] border border-[#21242d] space-y-2">
            <div className="flex items-center justify-between">
              <label className="text-xs font-semibold text-neutral-200 flex items-center gap-1.5">
                <Cookie size={14} className="text-amber-400" />
                Session Cookies File (<code>--cookies</code>)
              </label>
              {settings.hasCookiesFile ? (
                <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-[#3cd69b] bg-emerald-500/10 px-2 py-0.5 rounded-full border border-emerald-500/20">
                  <Check size={11} /> Loaded ({settings.cookiesResolvedPath?.split(/[\\/]/).pop()})
                </span>
              ) : (
                <span className="text-[11px] text-neutral-500 bg-[#181a20] px-2 py-0.5 rounded-full border border-[#282d38]">
                  No cookies.txt detected
                </span>
              )}
            </div>
            <input
              type="text"
              value={settings.cookiesPath || ''}
              onChange={(e) => setSettings({ ...settings, cookiesPath: e.target.value })}
              placeholder="./cookies.txt or /media/cookies.txt"
              className="w-full px-3 py-2 text-xs bg-[#121418] border border-[#282d38] rounded-lg text-neutral-200 placeholder:text-neutral-600 focus:outline-none focus:border-[#3cd69b]"
            />
            <span className="text-[10px] text-neutral-500 block">
              Exports from browser (Netscape format) unlock private, member-only, and geo-gated account content.
            </span>
          </div>

          <div className="flex justify-end pt-2">
            <button
              type="submit"
              disabled={saving}
              className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#3cd69b] hover:bg-[#32b884] text-neutral-950 text-xs font-bold transition-all cursor-pointer disabled:opacity-50 shadow-lg shadow-emerald-950/20"
            >
              {saving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
              Save Downloader Settings
            </button>
          </div>
        </form>
      ) : (
        <div className="text-xs text-neutral-400 p-3 rounded-xl bg-[#0c0d10] border border-[#21242d]">
          Downloader and geo-bypass settings can be configured by an Administrator.
        </div>
      )}
    </div>
  );
};

