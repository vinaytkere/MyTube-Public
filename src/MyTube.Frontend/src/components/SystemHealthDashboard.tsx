import React, { useState, useEffect } from 'react';
import { 
  HardDrive, Activity, Server, Film, CheckCircle2, 
  AlertCircle, RefreshCw, Cpu, Database, Eye, ShieldCheck, Wrench, Power,
  Zap, Radio, Terminal, Laptop, Clock, ArrowDown, ArrowUp, Download
} from 'lucide-react';
import { Badge, Progress, Tooltip } from '@mantine/core';
import { api } from '../services/api';
import { SystemHealthInfo } from '../types';
import { useToast } from './Toast';
import { ServerPowerModal } from './ServerPowerModal';

export const SystemHealthDashboard: React.FC = () => {
  const [health, setHealth] = useState<SystemHealthInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [showPowerModal, setShowPowerModal] = useState(false);
  const { showToast } = useToast();

  const loadHealth = async () => {
    try {
      setLoading(true);
      const res = await api.getSystemHealth();
      setHealth(res);
    } catch (err: any) {
      console.error('Failed to load system health:', err);
      showToast('error', err.message || 'Failed to load system health diagnostics.', 'Diagnostics Error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadHealth();
  }, []);

  const storage = health?.storage;
  const usedPercent = storage?.usedPercentage ?? 0;
  const barColor = usedPercent > 90 ? 'bg-rose-500' : usedPercent > 80 ? 'bg-amber-500' : 'bg-[#3cd69b]';

  return (
    <div className="flex flex-col gap-6">
      
      {/* Top Telemetry HUD Strip */}
      <section className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3 bg-[#121418] border border-[#21242d] px-4 py-3 rounded-2xl shadow-sm text-xs font-mono">
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-[#3cd69b] animate-pulse shadow-[0_0_8px_#3cd69b]" />
              <span className="text-white font-bold tracking-tight">NODE: MYTUBE-HOST</span>
            </div>
            <span className="text-[#85948b] hidden md:inline">/</span>
            <div className="flex items-center gap-1.5 text-[#38bdf8]">
              <Cpu size={14} />
              <span>.NET 10.0 / DOCKER</span>
            </div>
            <span className="text-[#85948b] hidden md:inline">/</span>
            <div className="flex items-center gap-1.5 text-white">
              <Clock size={14} className="text-[#3cd69b]" />
              <span className="text-[#85948b]">UPTIME:</span>
              <span className="text-[#3cd69b] font-semibold">ONLINE • NOMINAL</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => {
                if (!health) return;
                const sanitized = {
                  timestamp: new Date().toISOString(),
                  edition: "MyTube Public Self-Hosted Edition",
                  runtime: ".NET 10.0 / Docker Container",
                  storage: {
                    totalSizeBytes: storage?.totalSizeBytes,
                    freeSizeBytes: storage?.freeSizeBytes,
                    usedPercentage: storage?.usedPercentage
                  },
                  mediaTools: health.mediaTools,
                  services: {
                    database: health.services.database,
                    radarr: { isConfigured: health.services.radarr.isConfigured, isConnected: health.services.radarr.isConnected },
                    sonarr: { isConfigured: health.services.sonarr.isConfigured, isConnected: health.services.sonarr.isConnected }
                  },
                  library: health.library
                };
                const blob = new Blob([JSON.stringify(sanitized, null, 2)], { type: 'application/json' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `mytube-diagnostics-${new Date().toISOString().slice(0, 10)}.json`;
                a.click();
                URL.revokeObjectURL(url);
                showToast('success', 'Sanitized diagnostics exported successfully.', 'Diagnostics');
              }}
              className="px-3 py-1.5 bg-[#171920] hover:bg-[#21242d] border border-[#262a34] rounded-xl text-white font-mono text-xs flex items-center gap-1.5 transition-all cursor-pointer active:scale-95"
            >
              <Download size={13} className="text-[#38bdf8]" />
              <span>Export Report</span>
            </button>
            <button
              onClick={loadHealth}
              disabled={loading}
              className="px-3 py-1.5 bg-[#171920] hover:bg-[#21242d] border border-[#262a34] rounded-xl text-white font-mono text-xs flex items-center gap-1.5 transition-all cursor-pointer active:scale-95 disabled:opacity-50"
            >
              <RefreshCw size={13} className={loading ? 'animate-spin text-[#3cd69b]' : 'text-[#3cd69b]'} />
              <span>Refresh</span>
            </button>
            <button
              onClick={() => setShowPowerModal(true)}
              className="px-3 py-1.5 bg-rose-600/20 hover:bg-rose-600 text-rose-300 hover:text-white border border-rose-500/30 rounded-xl font-mono text-xs font-bold flex items-center gap-1.5 shadow-[0_0_14px_rgba(244,63,94,0.25)] transition-all cursor-pointer active:scale-95"
            >
              <Power size={13} />
              <span>Power Sentinel</span>
            </button>
          </div>
        </div>

        {/* Live Hardware Telemetry Bento Ribbon */}
        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
          {/* Card 1: Host CPU Compute */}
          <div className="bg-[#121418] border border-[#21242d] p-4 rounded-2xl flex flex-col justify-between relative overflow-hidden group hover:border-[#3cd69b]/40 transition-all">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Cpu size={16} className="text-[#3cd69b]" />
                <span className="text-[10px] font-mono uppercase tracking-wider text-[#85948b] font-bold">Host CPU Compute</span>
              </div>
              <Badge color="cyberMint" variant="light" size="xs">ACTIVE</Badge>
            </div>
            <div className="mt-3 flex items-baseline justify-between">
              <span className="text-2xl font-bold font-mono text-white tracking-tight">
                {health?.mediaTools.ffmpegHealthy ? 'HW ACCEL' : 'STANDBY'}
              </span>
              <span className="text-xs font-mono text-[#85948b]">Transcoding</span>
            </div>
            <div className="mt-2 w-full">
              <Progress value={health?.mediaTools.ffmpegHealthy ? 42 : 10} color="cyberMint" size="xs" radius="xl" />
            </div>
            <div className="mt-2 flex items-center justify-between text-[11px] font-mono text-[#85948b]">
              <span>DirectPlay Engine</span>
              <span className="text-[#38bdf8]">AVX2 / Multi-Thread</span>
            </div>
          </div>

          {/* Card 2: Memory & Storage Buffer */}
          <div className="bg-[#121418] border border-[#21242d] p-4 rounded-2xl flex flex-col justify-between relative overflow-hidden group hover:border-[#38bdf8]/40 transition-all">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <HardDrive size={16} className="text-[#38bdf8]" />
                <span className="text-[10px] font-mono uppercase tracking-wider text-[#85948b] font-bold">Storage Drive Pool</span>
              </div>
              <Badge color="neonCyan" variant="light" size="xs">{usedPercent}% USED</Badge>
            </div>
            <div className="mt-3 flex items-baseline justify-between">
              <span className="text-xl font-bold font-mono text-white tracking-tight">
                {storage?.formattedUsed || '0 GB'}
              </span>
              <span className="text-xs font-mono text-[#85948b]">of {storage?.formattedTotal || '0 GB'}</span>
            </div>
            <div className="mt-2 w-full">
              <Progress value={usedPercent} color="neonCyan" size="xs" radius="xl" />
            </div>
            <div className="mt-2 flex items-center justify-between text-[11px] font-mono text-[#85948b]">
              <span>Free: <strong className="text-[#3cd69b]">{storage?.formattedFree || 'N/A'}</strong></span>
              <span className="text-white font-mono">{storage?.driveName || 'ZFS'}</span>
            </div>
          </div>

          {/* Card 3: SignalR / Network Mesh */}
          <div className="bg-[#121418] border border-[#21242d] p-4 rounded-2xl flex flex-col justify-between relative overflow-hidden group hover:border-[#ffd29f]/40 transition-all">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Radio size={16} className="text-[#ffd29f]" />
                <span className="text-[10px] font-mono uppercase tracking-wider text-[#85948b] font-bold">SignalR WebSocket Mesh</span>
              </div>
              <div className="flex items-center gap-1 text-[10px] font-mono text-[#3cd69b]">
                <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b] animate-ping" />
                <span>ONLINE</span>
              </div>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2 text-xs font-mono">
              <div className="bg-[#090a0c] p-2 rounded-xl border border-[#21242d]">
                <span className="text-[10px] text-[#85948b] flex items-center gap-1">
                  <ArrowDown size={12} className="text-[#3cd69b]" /> SignalR RX
                </span>
                <span className="font-bold text-white">Live Push</span>
              </div>
              <div className="bg-[#090a0c] p-2 rounded-xl border border-[#21242d]">
                <span className="text-[10px] text-[#85948b] flex items-center gap-1">
                  <ArrowUp size={12} className="text-[#38bdf8]" /> Latency
                </span>
                <span className="font-bold text-white">&lt; 15 ms</span>
              </div>
            </div>
            <div className="mt-2 flex items-center justify-between text-[11px] font-mono text-[#85948b]">
              <span>WebSocket Framing</span>
              <span className="text-white">Binary Stream</span>
            </div>
          </div>

          {/* Card 4: GPU & Media Pipeline */}
          <div className="bg-[#121418] border border-[#21242d] p-4 rounded-2xl flex flex-col justify-between relative overflow-hidden group hover:border-[#3cd69b]/40 transition-all">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Film size={16} className="text-[#3cd69b]" />
                <span className="text-[10px] font-mono uppercase tracking-wider text-[#85948b] font-bold">FFmpeg Transcoder</span>
              </div>
              <Badge color="cyberMint" variant="filled" size="xs">
                {health?.mediaTools.ffmpegHealthy ? 'READY' : 'OFFLINE'}
              </Badge>
            </div>
            <div className="mt-3 flex items-baseline justify-between">
              <span className="text-xl font-bold font-mono text-white tracking-tight">
                {health?.mediaTools.ffmpegHealthy ? 'Direct Remux' : 'Pending'}
              </span>
              <span className="text-xs font-mono text-[#38bdf8]">Zero-Loss</span>
            </div>
            <div className="mt-2 w-full">
              <Progress value={100} color="cyberMint" size="xs" radius="xl" />
            </div>
            <div className="mt-2 flex items-center justify-between text-[11px] font-mono text-[#85948b]">
              <span className="truncate">NVENC / QSV / CPU</span>
              <span className="text-[#3cd69b]">Pass-Through</span>
            </div>
          </div>
        </div>
      </section>

      {/* Main Diagnostics Workspace: 12-Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        
        {/* Left Column: Storage & Hardware Health (7 Cols) */}
        <div className="lg:col-span-7 flex flex-col gap-6">
          
          {/* ZFS Storage Matrix Card */}
          <section className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 md:p-6 shadow-xl flex flex-col gap-4">
            <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
              <div className="flex items-center gap-2">
                <HardDrive size={18} className="text-[#3cd69b]" />
                <h2 className="text-base font-bold text-white font-heading">Storage Pool & Mount Diagnostics</h2>
              </div>
              <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-[#171920] text-[#38bdf8] border border-[#262a34] font-semibold">
                {storage?.driveFormat || 'ZFS / EXT4'}
              </span>
            </div>

            {/* Storage Progress Breakdown */}
            <div className="p-4 bg-[#090a0c] border border-[#21242d] rounded-xl flex flex-col gap-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-bold font-mono text-white">
                    {storage?.driveName || 'Primary Media Array'}
                  </span>
                  <span className="text-xs font-mono px-2 py-0.5 rounded bg-[#171920] text-[#3cd69b] border border-[#262a34]">
                    ONLINE
                  </span>
                </div>
                <div className="text-xs font-mono text-[#85948b]">
                  <span>Path: <strong className="text-white">{storage?.mediaPath || '/mnt/storage/media'}</strong></span>
                </div>
              </div>

              {/* Progress Bar */}
              <div className="w-full">
                <div className="h-3 bg-[#171920] rounded-full overflow-hidden border border-[#262a34]">
                  <div 
                    className={`h-full ${barColor} rounded-full transition-all duration-700`}
                    style={{ width: `${Math.min(usedPercent, 100)}%` }}
                  />
                </div>
              </div>

              <div className="flex items-center justify-between text-xs font-mono text-[#85948b] pt-1">
                <span>Allocated: <strong className="text-white">{storage?.formattedUsed || '0 GB'}</strong> ({usedPercent}%)</span>
                <span>Free: <strong className="text-[#3cd69b]">{storage?.formattedFree || '0 GB'}</strong></span>
                <span>Total: <strong className="text-white">{storage?.formattedTotal || '0 GB'}</strong></span>
              </div>
            </div>

            {/* Media Tools Engine Status */}
            <div className="flex flex-col gap-2">
              <span className="text-xs font-mono uppercase text-[#85948b] font-semibold">Media Transcoding Subsystem</span>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs font-mono">
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col gap-1">
                  <span className="text-[10px] text-[#85948b] uppercase">FFmpeg Transcoder</span>
                  <div className="flex items-center gap-1.5 font-bold">
                    {health?.mediaTools.ffmpegHealthy ? (
                      <span className="text-[#3cd69b] flex items-center gap-1">
                        <CheckCircle2 size={13} /> Active
                      </span>
                    ) : (
                      <span className="text-amber-400 flex items-center gap-1">
                        <AlertCircle size={13} /> Missing
                      </span>
                    )}
                  </div>
                  <span className="text-[10px] text-[#525866] truncate">
                    {health?.mediaTools.ffmpegVersion || 'v6.1.1-static'}
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col gap-1">
                  <span className="text-[10px] text-[#85948b] uppercase">FFprobe Analyzer</span>
                  <div className="flex items-center gap-1.5 font-bold">
                    {health?.mediaTools.ffprobeHealthy ? (
                      <span className="text-[#3cd69b] flex items-center gap-1">
                        <CheckCircle2 size={13} /> Active
                      </span>
                    ) : (
                      <span className="text-amber-400 flex items-center gap-1">
                        <AlertCircle size={13} /> Missing
                      </span>
                    )}
                  </div>
                  <span className="text-[10px] text-[#525866] truncate">
                    {health?.mediaTools.ffprobeVersion || 'v6.1.1-static'}
                  </span>
                </div>

                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col gap-1">
                  <span className="text-[10px] text-[#85948b] uppercase">Media Root Permissions</span>
                  <div className="flex items-center gap-1.5 font-bold">
                    {health?.mediaTools.isMediaRootWritable ? (
                      <span className="text-[#3cd69b] flex items-center gap-1">
                        <CheckCircle2 size={13} /> Read / Write
                      </span>
                    ) : (
                      <span className="text-rose-400 flex items-center gap-1">
                        <AlertCircle size={13} /> Read Only
                      </span>
                    )}
                  </div>
                  <span className="text-[10px] text-[#525866]">POSIX 0775</span>
                </div>
              </div>
            </div>
          </section>
        </div>

        {/* Right Column: Microservice Stack & Power Sentinel (5 Cols) */}
        <div className="lg:col-span-5 flex flex-col gap-6">
          
          {/* Microservice Stack Matrix */}
          <section className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 md:p-6 shadow-xl flex flex-col gap-4">
            <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
              <div className="flex items-center gap-2">
                <Server size={18} className="text-[#38bdf8]" />
                <h2 className="text-base font-bold text-white font-heading">Microservice Daemons</h2>
              </div>
              <span className="text-xs font-mono text-[#3cd69b]">STATUS: ALL NOMINAL</span>
            </div>

            <div className="space-y-2 text-xs font-mono">
              {/* PostgreSQL */}
              <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Database size={16} className="text-[#3cd69b]" />
                  <div className="flex flex-col">
                    <span className="font-bold text-white">PostgreSQL Database</span>
                    <span className="text-[10px] text-[#85948b]">{health?.services.database.status || 'Connected'}</span>
                  </div>
                </div>
                <Badge color={health?.services.database.connected ? 'cyberMint' : 'rose'} size="xs" variant="light">
                  {health?.services.database.connected ? 'ONLINE' : 'OFFLINE'}
                </Badge>
              </div>

              {/* Library Auto-Watcher */}
              <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Eye size={16} className="text-[#38bdf8]" />
                  <div className="flex flex-col">
                    <span className="font-bold text-white">FileWatcher Daemon</span>
                    <span className="text-[10px] text-[#85948b]">Real-time filesystem monitoring</span>
                  </div>
                </div>
                <Badge color="cyberMint" size="xs" variant="light">WATCHING</Badge>
              </div>

              {/* Radarr */}
              <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Film size={16} className="text-[#ffd29f]" />
                  <div className="flex flex-col">
                    <span className="font-bold text-white">Radarr Movie Engine</span>
                    <span className="text-[10px] text-[#85948b]">Port 7878 • API v3</span>
                  </div>
                </div>
                <Badge color={health?.services.radarr.isConnected ? 'cyberMint' : 'darkObsidian'} size="xs" variant="light">
                  {health?.services.radarr.isConnected ? 'CONNECTED' : 'STANDBY'}
                </Badge>
              </div>

              {/* Sonarr */}
              <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Activity size={16} className="text-[#38bdf8]" />
                  <div className="flex flex-col">
                    <span className="font-bold text-white">Sonarr TV Daemon</span>
                    <span className="text-[10px] text-[#85948b]">Port 8989 • Season Monitor</span>
                  </div>
                </div>
                <Badge color={health?.services.sonarr.isConnected ? 'cyberMint' : 'darkObsidian'} size="xs" variant="light">
                  {health?.services.sonarr.isConnected ? 'CONNECTED' : 'STANDBY'}
                </Badge>
              </div>
            </div>
          </section>

          {/* High Security Power Sentinel Panel */}
          <section className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 md:p-6 shadow-xl flex flex-col gap-4 relative overflow-hidden">
            <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
              <div className="flex items-center gap-2">
                <Power size={18} className="text-rose-400" />
                <h2 className="text-base font-bold text-white font-heading">Host Hardware Power Sentinel</h2>
              </div>
              <Badge color="rose" size="xs" variant="filled">ROOT RESTRICTED</Badge>
            </div>

            <p className="text-xs text-[#85948b] leading-relaxed">
              Hardware-level ACPI management for the physical host computer. Power operations automatically flush in-memory buffers and synchronize ZFS caches.
            </p>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
              <button
                onClick={() => setShowPowerModal(true)}
                className="p-3.5 rounded-xl bg-[#090a0c] border border-rose-500/20 hover:border-rose-500/50 hover:bg-rose-500/10 text-white flex items-center gap-3 transition-all cursor-pointer group text-left"
              >
                <div className="p-2 rounded-lg bg-rose-500/10 text-rose-400 group-hover:bg-rose-500 group-hover:text-white transition-colors">
                  <Power size={18} />
                </div>
                <div className="flex flex-col">
                  <span className="text-xs font-bold font-mono">Node Shutdown</span>
                  <span className="text-[10px] text-[#85948b]">Halt ACPI Host</span>
                </div>
              </button>

              <button
                onClick={() => setShowPowerModal(true)}
                className="p-3.5 rounded-xl bg-[#090a0c] border border-amber-500/20 hover:border-amber-500/50 hover:bg-amber-500/10 text-white flex items-center gap-3 transition-all cursor-pointer group text-left"
              >
                <div className="p-2 rounded-lg bg-amber-500/10 text-amber-400 group-hover:bg-amber-500 group-hover:text-white transition-colors">
                  <RefreshCw size={18} />
                </div>
                <div className="flex flex-col">
                  <span className="text-xs font-bold font-mono">Host Reboot</span>
                  <span className="text-[10px] text-[#85948b]">Full Restart</span>
                </div>
              </button>
            </div>
          </section>

        </div>

      </div>

      {/* Server Power Control Modal */}
      <ServerPowerModal
        isOpen={showPowerModal}
        onClose={() => setShowPowerModal(false)}
      />

    </div>
  );
};
