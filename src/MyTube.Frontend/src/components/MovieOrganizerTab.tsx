import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { 
  Folder, Film, Music, Sparkles, RefreshCw, CheckCircle2, 
  AlertTriangle, Settings, ArrowRight, FileText, Check, ShieldCheck, 
  Layers, ChevronDown, ChevronUp, FileCode, Search, Tag, Eye, Play,
  FolderCheck, ExternalLink, Cpu
} from 'lucide-react';
import { api } from '../services/api';
import { 
  Video, MediaOrganizeResult, MediaRenamePreview, MediaOrganizerSettings 
} from '../types';
import { useToast } from './Toast';

interface MovieOrganizerTabProps {
  initialFilter?: 'all' | 'movie' | 'audio';
  onPlayVideo?: (video: Video) => void;
}

export const MovieOrganizerTab: React.FC<MovieOrganizerTabProps> = ({ 
  initialFilter = 'all',
  onPlayVideo 
}) => {
  const { showToast } = useToast();

  // Media items state
  const [videos, setVideos] = useState<Video[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'movie' | 'audio'>(initialFilter);
  const [statusFilter, setStatusFilter] = useState<'all' | 'unorganized' | 'organized'>('all');

  // Selection state
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Settings state
  const [showSettings, setShowSettings] = useState(false);
  const [settings, setSettings] = useState<MediaOrganizerSettings>({
    autoOrganizeOnDownloadComplete: true,
    movieFolderPattern: 'media/Movies/{Genre}/{Title} ({Year})',
    audioFolderPattern: 'media/Audio/{Genre}',
    movieFilePattern: '{Title} ({Year}) [{Quality}]',
    audioFilePattern: '{Artist} - {Title}',
    generateNfoFile: true,
    moveSubtitles: true
  });
  const [savingSettings, setSavingSettings] = useState(false);

  // Preview Modal state
  const [previewItem, setPreviewItem] = useState<MediaRenamePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewVideo, setPreviewVideo] = useState<Video | null>(null);

  // Action states
  const [organizingId, setOrganizingId] = useState<string | null>(null);
  const [batchOrganizing, setBatchOrganizing] = useState(false);
  const [lastResults, setLastResults] = useState<MediaOrganizeResult[]>([]);
  const [showResultsModal, setShowResultsModal] = useState(false);

  // Consolidation & Dry Run state
  const [consolidating, setConsolidating] = useState(false);
  const [batchDryRunning, setBatchDryRunning] = useState(false);
  const [dryRunPreviews, setDryRunPreviews] = useState<MediaRenamePreview[]>([]);
  const [showDryRunModal, setShowDryRunModal] = useState(false);

  // Consolidate Categories & Prune Empty Folders
  const handleConsolidateCategories = async () => {
    try {
      setConsolidating(true);
      showToast('info', 'Consolidating fragmented folders and removing empty categories...', 'Cleanup');
      const res = await api.consolidateCategories();
      if (res.success) {
        showToast(
          'success',
          `Merged ${res.categoriesMerged} legacy folders, removed ${res.emptyCategoriesDeleted} empty folders, reassigned ${res.videosReassigned} videos!`,
          'Categories Consolidated'
        );
        await loadVideos();
        window.dispatchEvent(new Event('categories_updated'));
        window.dispatchEvent(new Event('videos_updated'));
      } else {
        showToast('error', res.errorMessage || 'Failed to consolidate categories', 'Error');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error consolidating categories', 'Error');
    } finally {
      setConsolidating(false);
    }
  };

  // Batch Dry Run Preview
  const handleBatchDryRun = async () => {
    const idsToPreview = selectedIds.size > 0 
      ? Array.from(selectedIds) 
      : filteredVideos.filter(v => !v.isOrganized).map(v => v.id);

    if (idsToPreview.length === 0) {
      showToast('info', 'No unorganized media selected', 'Selection Empty');
      return;
    }

    try {
      setBatchDryRunning(true);
      showToast('info', `Running SmartRoute Dry Run preview for ${idsToPreview.length} files...`, 'Dry Run');
      const previews = await api.batchPreviewMedia(idsToPreview);
      setDryRunPreviews(previews);
      setShowDryRunModal(true);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to generate dry run preview', 'Dry Run Error');
    } finally {
      setBatchDryRunning(false);
    }
  };

  // Load Videos
  const loadVideos = useCallback(async () => {
    try {
      setLoading(true);
      const res = await api.getVideos({
        pageSize: 100,
        sortBy: 'recentlyadded'
      });
      setVideos(res.items || []);
    } catch (err: any) {
      console.error('Failed to load videos for organizer:', err);
      showToast('error', err.message || 'Failed to load media files', 'Load Error');
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  // Load Settings
  const loadSettings = useCallback(async () => {
    try {
      const s = await api.getMediaOrganizerSettings();
      if (s) {
        setSettings(s);
      }
    } catch (err) {
      console.warn('Could not load organizer settings, using defaults:', err);
    }
  }, []);

  useEffect(() => {
    loadVideos();
    loadSettings();
  }, [loadVideos, loadSettings]);

  // Save Settings
  const handleSaveSettings = async () => {
    try {
      setSavingSettings(true);
      const res = await api.updateMediaOrganizerSettings(settings);
      if (res.success) {
        showToast('success', 'Media organizer settings saved successfully', 'Settings Updated');
        setShowSettings(false);
      } else {
        showToast('error', res.message || 'Failed to save settings', 'Save Failed');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to save settings', 'Error');
    } finally {
      setSavingSettings(false);
    }
  };

  // Preview Single Item Organization
  const handlePreview = async (video: Video) => {
    try {
      setPreviewVideo(video);
      setPreviewLoading(true);
      setPreviewItem(null);
      const preview = await api.previewMediaOrganization(video.id);
      setPreviewItem(preview);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to generate preview with SmartRoute', 'Preview Failed');
      setPreviewVideo(null);
    } finally {
      setPreviewLoading(false);
    }
  };

  // Organize Single Item
  const handleOrganizeSingle = async (videoId: string) => {
    try {
      setOrganizingId(videoId);
      const result = await api.organizeMedia(videoId);
      if (result.success) {
        showToast('success', `Organized to: ${result.newFileName}`, 'File Organized & Renamed');
        // Update local item
        setVideos(prev => prev.map(v => v.id === videoId ? {
          ...v,
          isOrganized: true,
          organizedPath: result.newPath,
          fileName: result.newFileName,
          primaryGenre: result.primaryGenre,
          releaseYear: result.releaseYear,
          mediaTypeDetected: result.mediaType,
          summary: result.summary || v.summary
        } : v));
        setPreviewItem(null);
        setPreviewVideo(null);
        window.dispatchEvent(new Event('videos_updated'));
      } else {
        showToast('error', result.errorMessage || 'Failed to organize media', 'Organization Failed');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error executing organization', 'Error');
    } finally {
      setOrganizingId(null);
    }
  };

  // Batch Organize Selected Items
  const handleBatchOrganize = async () => {
    const idsToOrganize = selectedIds.size > 0 
      ? Array.from(selectedIds) 
      : filteredVideos.filter(v => !v.isOrganized).map(v => v.id);

    if (idsToOrganize.length === 0) {
      showToast('info', 'No unorganized media selected', 'Selection Empty');
      return;
    }

    try {
      setBatchOrganizing(true);
      showToast('info', `SmartRoute is analyzing and organizing ${idsToOrganize.length} files...`, 'Batch Processing');
      const results = await api.batchOrganizeMedia(idsToOrganize);
      setLastResults(results);
      setShowResultsModal(true);

      const successCount = results.filter(r => r.success).length;
      showToast('success', `Successfully organized ${successCount} of ${results.length} files`, 'Batch Complete');
      
      setSelectedIds(new Set());
      await loadVideos();
      window.dispatchEvent(new Event('videos_updated'));
    } catch (err: any) {
      showToast('error', err.message || 'Batch organization error', 'Batch Error');
    } finally {
      setBatchOrganizing(false);
    }
  };

  // Toggle item selection
  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleSelectAll = () => {
    if (selectedIds.size === filteredVideos.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(filteredVideos.map(v => v.id)));
    }
  };

  // Filtered Media List
  const filteredVideos = useMemo(() => {
    return videos.filter(v => {
      // Type filter
      if (typeFilter === 'movie') {
        const isAudio = v.containerFormat?.toLowerCase() === 'mp3' || 
                        v.fileName?.toLowerCase().endsWith('.mp3') || 
                        v.mediaTypeDetected?.toLowerCase() === 'musictrack';
        if (isAudio) return false;
      } else if (typeFilter === 'audio') {
        const isAudio = v.containerFormat?.toLowerCase() === 'mp3' || 
                        v.fileName?.toLowerCase().endsWith('.mp3') || 
                        v.mediaTypeDetected?.toLowerCase() === 'musictrack';
        if (!isAudio) return false;
      }

      // Status filter
      if (statusFilter === 'unorganized' && v.isOrganized) return false;
      if (statusFilter === 'organized' && !v.isOrganized) return false;

      // Search query
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const matchTitle = v.displayTitle?.toLowerCase().includes(q);
        const matchFile = v.fileName?.toLowerCase().includes(q);
        const matchGenre = v.primaryGenre?.toLowerCase().includes(q);
        const matchSummary = v.summary?.toLowerCase().includes(q);
        const matchUploader = v.uploader?.toLowerCase().includes(q);
        if (!matchTitle && !matchFile && !matchGenre && !matchSummary && !matchUploader) {
          return false;
        }
      }

      return true;
    });
  }, [videos, typeFilter, statusFilter, searchQuery]);

  // Statistics
  const stats = useMemo(() => {
    const total = videos.length;
    const organized = videos.filter(v => v.isOrganized).length;
    const unorganized = total - organized;
    const moviesCount = videos.filter(v => !v.fileName.endsWith('.mp3')).length;
    const audioCount = total - moviesCount;
    return { total, organized, unorganized, moviesCount, audioCount };
  }, [videos]);

  return (
    <div className="space-y-6">
      {/* Top Banner & Remote OCI SmartRoute Status */}
      <div className="p-5 rounded-2xl bg-gradient-to-r from-[#121418] via-[#15181f] to-[#121418] border border-[#21242d] shadow-lg">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="p-3 rounded-xl bg-[#3cd69b]/10 text-[#3cd69b] border border-[#3cd69b]/25 shadow-inner">
              <Sparkles size={24} className="animate-pulse" />
            </div>
            <div>
              <div className="flex items-center gap-2.5">
                <h2 className="text-lg font-bold text-white tracking-tight">
                  AI Movie & Media Organizer
                </h2>
                <span className="flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-[#3cd69b]/15 text-[#3cd69b] border border-[#3cd69b]/30">
                  <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b] animate-ping" />
                  AI Media Organizer
                </span>
              </div>
              <p className="text-xs text-neutral-400 mt-0.5">
                Powered by configured AI Provider (Groq / Gemini / DeepSeek / SmartRoute). Automatically extracts plot synopses, categorizes genres, generates Kodi/Plex <code className="text-neutral-300">.nfo</code>, preserves subtitles, and cleans canonical names.
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 self-start md:self-auto">
            <button
              onClick={handleConsolidateCategories}
              disabled={consolidating}
              title="Consolidate legacy/fragmented folders and delete empty ones from database & disk"
              className="px-3 py-2 rounded-xl text-xs font-medium border bg-[#171920] hover:bg-[#1e222a] text-cyan-400 hover:text-cyan-300 border-cyan-800/40 hover:border-cyan-600/60 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm"
            >
              <RefreshCw size={14} className={consolidating ? 'animate-spin' : ''} />
              <span>{consolidating ? 'Consolidating...' : 'Consolidate & Prune Folders'}</span>
            </button>

            <button
              onClick={handleBatchDryRun}
              disabled={batchDryRunning || (selectedIds.size === 0 && stats.unorganized === 0)}
              title="Preview proposed folder moves and standardized names without touching filesystem"
              className="px-3 py-2 rounded-xl text-xs font-medium border bg-[#171920] hover:bg-[#1e222a] text-amber-300 hover:text-amber-200 border-amber-800/40 hover:border-amber-600/60 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm"
            >
              <Eye size={14} className={batchDryRunning ? 'animate-pulse' : ''} />
              <span>{batchDryRunning ? 'Analyzing...' : 'Dry Run Preview'}</span>
            </button>

            <button
              onClick={() => setShowSettings(!showSettings)}
              className={`px-3 py-2 rounded-xl text-xs font-medium border transition-all flex items-center gap-1.5 cursor-pointer ${
                showSettings 
                  ? 'bg-[#1e222a] text-[#3cd69b] border-[#3cd69b]/40 shadow-sm' 
                  : 'bg-[#171920] text-neutral-300 hover:text-white border-[#21242d] hover:border-[#2f3440]'
              }`}
            >
              <Settings size={14} />
              <span>Settings</span>
              {showSettings ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
            </button>

            <button
              onClick={handleBatchOrganize}
              disabled={batchOrganizing || (selectedIds.size === 0 && stats.unorganized === 0)}
              className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#3cd69b] hover:bg-[#32b885] disabled:opacity-40 disabled:hover:bg-[#3cd69b] text-black transition-all cursor-pointer flex items-center gap-1.5 shadow-md shadow-[#3cd69b]/10"
            >
              {batchOrganizing ? (
                <>
                  <RefreshCw size={14} className="animate-spin" />
                  <span>Organizing...</span>
                </>
              ) : (
                <>
                  <FolderCheck size={14} />
                  <span>
                    {selectedIds.size > 0 
                      ? `Organize Selected (${selectedIds.size})` 
                      : `Organize All (${stats.unorganized})`}
                  </span>
                </>
              )}
            </button>
          </div>
        </div>

        {/* Collapsible Settings Drawer */}
        {showSettings && (
          <div className="mt-5 pt-4 border-t border-[#21242d] grid grid-cols-1 md:grid-cols-2 gap-4 text-xs text-neutral-300">
            <div className="space-y-3 p-3.5 rounded-xl bg-[#0d0e11] border border-[#1a1d24]">
              <h4 className="font-semibold text-white flex items-center gap-1.5">
                <Folder size={14} className="text-[#3cd69b]" />
                Folder & File Routing Patterns
              </h4>
              
              <div>
                <label className="block text-[11px] text-neutral-400 mb-1">Movie Destination Pattern</label>
                <input
                  type="text"
                  value={settings.movieFolderPattern}
                  onChange={e => setSettings({ ...settings, movieFolderPattern: e.target.value })}
                  className="w-full bg-[#121418] border border-[#21242d] focus:border-[#3cd69b] rounded-lg px-3 py-1.5 text-xs text-white outline-none font-mono"
                />
                <span className="text-[10px] text-neutral-500 mt-0.5 block">
                  Example: media/Movies/{'{Genre}'}/{'{Title}'} ({'{Year}'})
                </span>
              </div>

              <div>
                <label className="block text-[11px] text-neutral-400 mb-1">Audio Destination Pattern</label>
                <input
                  type="text"
                  value={settings.audioFolderPattern}
                  onChange={e => setSettings({ ...settings, audioFolderPattern: e.target.value })}
                  className="w-full bg-[#121418] border border-[#21242d] focus:border-[#3cd69b] rounded-lg px-3 py-1.5 text-xs text-white outline-none font-mono"
                />
                <span className="text-[10px] text-neutral-500 mt-0.5 block">
                  Example: media/Audio/{'{Genre}'}
                </span>
              </div>
            </div>

            <div className="space-y-3 p-3.5 rounded-xl bg-[#0d0e11] border border-[#1a1d24]">
              <h4 className="font-semibold text-white flex items-center gap-1.5">
                <Cpu size={14} className="text-[#3cd69b]" />
                Automation & Companion Sidecars
              </h4>

              <div className="space-y-2.5">
                <label className="flex items-center gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settings.autoOrganizeOnDownloadComplete}
                    onChange={e => setSettings({ ...settings, autoOrganizeOnDownloadComplete: e.target.checked })}
                    className="rounded bg-[#121418] border-[#21242d] text-[#3cd69b] focus:ring-0 w-4 h-4 cursor-pointer"
                  />
                  <div>
                    <span className="font-medium text-white block">Auto-Organize on Download Complete</span>
                    <span className="text-[10px] text-neutral-400">Trigger SmartRoute AI automatically when yt-dlp finishes downloading.</span>
                  </div>
                </label>

                <label className="flex items-center gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settings.generateNfoFile}
                    onChange={e => setSettings({ ...settings, generateNfoFile: e.target.checked })}
                    className="rounded bg-[#121418] border-[#21242d] text-[#3cd69b] focus:ring-0 w-4 h-4 cursor-pointer"
                  />
                  <div>
                    <span className="font-medium text-white block">Generate Kodi/Plex .nfo Metadata</span>
                    <span className="text-[10px] text-neutral-400">Creates XML .nfo sidecar with plot, genre, release year, and director.</span>
                  </div>
                </label>

                <label className="flex items-center gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={settings.moveSubtitles}
                    onChange={e => setSettings({ ...settings, moveSubtitles: e.target.checked })}
                    className="rounded bg-[#121418] border-[#21242d] text-[#3cd69b] focus:ring-0 w-4 h-4 cursor-pointer"
                  />
                  <div>
                    <span className="font-medium text-white block">Rename Subtitles & Clean Formats</span>
                    <span className="text-[10px] text-neutral-400">Preserves and aligns .srt and .vtt companion files to match the new media title.</span>
                  </div>
                </label>
              </div>

              <div className="pt-2 flex justify-end">
                <button
                  onClick={handleSaveSettings}
                  disabled={savingSettings}
                  className="px-3.5 py-1.5 rounded-lg bg-[#3cd69b] hover:bg-[#32b885] disabled:opacity-50 text-black text-xs font-semibold cursor-pointer flex items-center gap-1.5"
                >
                  {savingSettings ? <RefreshCw size={12} className="animate-spin" /> : <Check size={12} />}
                  <span>Save Configuration</span>
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Quick Stats Summary */}
        <div className="mt-4 pt-3 border-t border-[#1a1d24] flex flex-wrap items-center gap-5 text-xs text-neutral-400">
          <div className="flex items-center gap-2">
            <span className="text-neutral-200 font-semibold">{stats.total}</span> Total Media Files
          </div>
          <div className="w-1 h-1 rounded-full bg-neutral-600" />
          <div className="flex items-center gap-2">
            <span className="text-[#3cd69b] font-semibold">{stats.organized}</span> Organized
          </div>
          <div className="w-1 h-1 rounded-full bg-neutral-600" />
          <div className="flex items-center gap-2">
            <span className="text-amber-400 font-semibold">{stats.unorganized}</span> Awaiting Organization
          </div>
          <div className="w-1 h-1 rounded-full bg-neutral-600" />
          <div className="flex items-center gap-2 text-neutral-300">
            <ShieldCheck size={13} className="text-[#3cd69b]" />
            Media Service Live
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="flex items-center gap-1.5 bg-[#121418] border border-[#21242d] p-1 rounded-xl w-full sm:w-auto">
          <button
            onClick={() => setTypeFilter('all')}
            className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer ${
              typeFilter === 'all' 
                ? 'bg-[#1e222a] text-white shadow-sm' 
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            All Media ({stats.total})
          </button>
          <button
            onClick={() => setTypeFilter('movie')}
            className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer flex items-center gap-1.5 ${
              typeFilter === 'movie' 
                ? 'bg-[#1e222a] text-[#3cd69b] shadow-sm font-semibold' 
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            <Film size={13} /> Movies & Videos ({stats.moviesCount})
          </button>
          <button
            onClick={() => setTypeFilter('audio')}
            className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer flex items-center gap-1.5 ${
              typeFilter === 'audio' 
                ? 'bg-[#1e222a] text-[#3cd69b] shadow-sm font-semibold' 
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            <Music size={13} /> Audio & Music ({stats.audioCount})
          </button>
        </div>

        <div className="flex items-center gap-2.5 w-full sm:w-auto">
          {/* Status selector */}
          <select
            value={statusFilter}
            onChange={e => setStatusFilter(e.target.value as any)}
            className="bg-[#121418] border border-[#21242d] text-xs text-neutral-300 rounded-xl px-3 py-2 outline-none focus:border-[#3cd69b] cursor-pointer"
          >
            <option value="all">All Statuses</option>
            <option value="unorganized">Pending Organization</option>
            <option value="organized">Organized</option>
          </select>

          {/* Search box */}
          <div className="relative flex-1 sm:w-64">
            <input
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="Filter by title, genre, artist..."
              className="w-full bg-[#121418] border border-[#21242d] focus:border-[#3cd69b] rounded-xl px-3 py-2 pl-8 text-xs text-neutral-200 outline-none"
            />
            <Search className="absolute left-2.5 top-2.5 text-neutral-500" size={14} />
          </div>

          <button
            onClick={loadVideos}
            disabled={loading}
            className="p-2 rounded-xl bg-[#121418] hover:bg-[#1a1d24] border border-[#21242d] text-neutral-400 hover:text-white transition-colors cursor-pointer"
            title="Refresh List"
          >
            <RefreshCw size={15} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* Media Items Table / Grid */}
      <div className="bg-[#0e1014] border border-[#1e2129] rounded-2xl overflow-hidden shadow-xl">
        <div className="px-5 py-3.5 bg-[#121418] border-b border-[#1e2129] flex items-center justify-between">
          <div className="flex items-center gap-3">
            <input
              type="checkbox"
              checked={filteredVideos.length > 0 && selectedIds.size === filteredVideos.length}
              onChange={toggleSelectAll}
              className="rounded bg-[#0d0e11] border-[#262a34] text-[#3cd69b] focus:ring-0 w-4 h-4 cursor-pointer"
            />
            <span className="text-xs font-semibold text-neutral-300">
              {selectedIds.size > 0 
                ? `${selectedIds.size} of ${filteredVideos.length} selected` 
                : `${filteredVideos.length} Items Available`}
            </span>
          </div>

          {selectedIds.size > 0 && (
            <div className="flex items-center gap-2">
              <button
                onClick={handleBatchOrganize}
                disabled={batchOrganizing}
                className="px-3 py-1 rounded-lg bg-[#3cd69b] hover:bg-[#32b885] text-black text-xs font-semibold cursor-pointer flex items-center gap-1.5"
              >
                <FolderCheck size={13} />
                <span>Organize Selected ({selectedIds.size})</span>
              </button>
              <button
                onClick={() => setSelectedIds(new Set())}
                className="px-2.5 py-1 rounded-lg bg-[#1e222a] text-neutral-400 hover:text-white text-xs cursor-pointer"
              >
                Clear
              </button>
            </div>
          )}
        </div>

        {loading ? (
          <div className="p-16 flex flex-col items-center justify-center gap-3 text-neutral-500">
            <RefreshCw size={28} className="animate-spin text-[#3cd69b]" />
            <span className="text-xs font-medium">Scanning media items & metadata...</span>
          </div>
        ) : filteredVideos.length === 0 ? (
          <div className="p-16 text-center text-neutral-500 space-y-2">
            <Folder size={36} className="mx-auto text-neutral-600 mb-2 opacity-50" />
            <p className="text-sm font-semibold text-neutral-300">No media files found</p>
            <p className="text-xs text-neutral-500">Try adjusting your filters or download a video/movie using the Downloader or Releases tab.</p>
          </div>
        ) : (
          <div className="divide-y divide-[#171a21]">
            {filteredVideos.map(video => {
              const isSelected = selectedIds.has(video.id);
              const isOrganizing = organizingId === video.id;
              const isAudio = video.containerFormat?.toLowerCase() === 'mp3' || video.fileName?.endsWith('.mp3');

              return (
                <div 
                  key={video.id} 
                  className={`p-4 hover:bg-[#13151b] transition-colors flex flex-col lg:flex-row lg:items-center justify-between gap-4 ${
                    isSelected ? 'bg-[#151921]/60' : ''
                  }`}
                >
                  {/* Left: Thumbnail & Main Info */}
                  <div className="flex items-start gap-3.5 flex-1 min-w-0">
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => toggleSelect(video.id)}
                      className="mt-1.5 rounded bg-[#0d0e11] border-[#262a34] text-[#3cd69b] focus:ring-0 w-4 h-4 cursor-pointer flex-shrink-0"
                    />

                    {/* Media Thumbnail */}
                    <div className="relative w-20 h-14 rounded-lg bg-[#1a1d24] overflow-hidden flex-shrink-0 border border-[#262a34]">
                      {video.thumbnailUrl ? (
                        <img 
                          src={video.thumbnailUrl} 
                          alt={video.displayTitle} 
                          className="w-full h-full object-cover"
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = 'none';
                          }}
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center text-neutral-500">
                          {isAudio ? <Music size={20} /> : <Film size={20} />}
                        </div>
                      )}
                      {onPlayVideo && (
                        <button
                          onClick={() => onPlayVideo(video)}
                          className="absolute inset-0 bg-black/40 hover:bg-black/20 flex items-center justify-center opacity-0 hover:opacity-100 transition-opacity cursor-pointer"
                        >
                          <Play size={16} className="text-white fill-white" />
                        </button>
                      )}
                    </div>

                    {/* Meta & Title */}
                    <div className="space-y-1 flex-1 min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold text-white truncate max-w-md block" title={video.displayTitle}>
                          {video.displayTitle}
                        </span>

                        {/* Status Chip */}
                        {video.isOrganized ? (
                          <span className="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 flex items-center gap-1">
                            <CheckCircle2 size={11} /> Organized
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-md text-[10px] font-semibold bg-amber-500/15 text-amber-400 border border-amber-500/30 flex items-center gap-1">
                            <AlertTriangle size={11} /> Unorganized
                          </span>
                        )}

                        {/* Genre Badge */}
                        {video.primaryGenre && (
                          <span className="px-2 py-0.5 rounded-md text-[10px] font-mono bg-[#1f242e] text-[#3cd69b] border border-[#2d3442]">
                            {video.primaryGenre}
                          </span>
                        )}

                        {/* Release Year */}
                        {video.releaseYear && (
                          <span className="px-1.5 py-0.5 rounded text-[10px] font-mono bg-[#181b22] text-neutral-400 border border-[#222730]">
                            {video.releaseYear}
                          </span>
                        )}

                        {/* Media Type Detected */}
                        {video.mediaTypeDetected && (
                          <span className="px-1.5 py-0.5 rounded text-[10px] uppercase font-bold bg-[#141d1a] text-[#3cd69b] border border-[#1f382b]">
                            {video.mediaTypeDetected}
                          </span>
                        )}
                      </div>

                      {/* File Path & Subtitle Info */}
                      <div className="flex flex-wrap items-center gap-3 text-xs text-neutral-400 font-mono">
                        <span className="truncate max-w-sm" title={video.fileName}>
                          📄 {video.fileName}
                        </span>
                        {video.organizedPath && (
                          <span className="text-neutral-500 truncate max-w-sm" title={video.organizedPath}>
                            📁 {video.organizedPath}
                          </span>
                        )}
                        {video.subtitleTracks && video.subtitleTracks.length > 0 && (
                          <span className="text-[11px] text-cyan-400">
                            {video.subtitleTracks.length} Subtitles
                          </span>
                        )}
                      </div>

                      {/* AI Synopsis / Plot */}
                      {video.summary && (
                        <p className="text-xs text-neutral-400 line-clamp-1 italic max-w-2xl">
                          "{video.summary}"
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Right Actions */}
                  <div className="flex items-center gap-2 self-end lg:self-center flex-shrink-0">
                    <button
                      onClick={() => handlePreview(video)}
                      disabled={previewLoading || isOrganizing}
                      className="px-3 py-1.5 rounded-xl bg-[#171920] hover:bg-[#1e222a] border border-[#262a34] text-neutral-300 hover:text-white text-xs font-medium transition-colors cursor-pointer flex items-center gap-1.5"
                    >
                      <Eye size={13} className="text-[#3cd69b]" />
                      <span>Preview</span>
                    </button>

                    <button
                      onClick={() => handleOrganizeSingle(video.id)}
                      disabled={isOrganizing}
                      className="px-3.5 py-1.5 rounded-xl bg-[#3cd69b] hover:bg-[#32b885] disabled:opacity-50 text-black text-xs font-semibold transition-all cursor-pointer flex items-center gap-1.5 shadow-sm"
                    >
                      {isOrganizing ? (
                        <>
                          <RefreshCw size={13} className="animate-spin" />
                          <span>Organizing...</span>
                        </>
                      ) : (
                        <>
                          <Sparkles size={13} />
                          <span>Organize</span>
                        </>
                      )}
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Preview Diff Modal */}
      {previewItem && previewVideo && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#101216] border border-[#262a34] rounded-2xl w-full max-w-2xl overflow-hidden shadow-2xl animate-in fade-in zoom-in duration-200">
            {/* Modal Header */}
            <div className="px-6 py-4 bg-[#14171e] border-b border-[#21242d] flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-lg bg-[#3cd69b]/15 text-[#3cd69b]">
                  <Sparkles size={18} />
                </div>
                <div>
                  <h3 className="text-base font-bold text-white">SmartRoute Organizer Preview</h3>
                  <p className="text-xs text-neutral-400">Classified using OCI Free Tier AI</p>
                </div>
              </div>
              <button
                onClick={() => setPreviewItem(null)}
                className="p-1.5 text-neutral-400 hover:text-white rounded-lg hover:bg-[#1e222a] cursor-pointer"
              >
                ✕
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
              {/* Classification Chips */}
              <div className="flex flex-wrap items-center gap-2">
                <span className="px-2.5 py-1 rounded-lg text-xs font-semibold bg-[#1a221f] text-[#3cd69b] border border-[#2b4238]">
                  Category: {previewItem.category || previewItem.mediaType}
                </span>
                {previewItem.language && (
                  <span className="px-2.5 py-1 rounded-lg text-xs font-semibold bg-[#18232c] text-sky-400 border border-[#22394c]">
                    Language: {previewItem.language}
                  </span>
                )}
                <span className="px-2.5 py-1 rounded-lg text-xs font-semibold bg-[#1c1f28] text-cyan-400 border border-[#2c3444]">
                  Genre: {previewItem.primaryGenre}
                </span>
                {previewItem.releaseYear && (
                  <span className="px-2.5 py-1 rounded-lg text-xs font-mono bg-[#181b22] text-neutral-300 border border-[#262a34]">
                    Year: {previewItem.releaseYear}
                  </span>
                )}
                {previewItem.confidence !== undefined && (
                  <span className={`px-2.5 py-1 rounded-lg text-xs font-semibold border ${
                    previewItem.confidence >= 0.8
                      ? 'bg-emerald-950/30 text-emerald-300 border-emerald-800/40'
                      : 'bg-amber-950/30 text-amber-300 border-amber-800/40'
                  }`}>
                    Confidence: {Math.round(previewItem.confidence * 100)}%
                  </span>
                )}
              </div>

              {/* Classification Reason */}
              {previewItem.classificationReason && (
                <div className="p-3 rounded-xl bg-[#0c0e12] border border-[#1e2129] text-xs text-neutral-400">
                  <span className="font-semibold text-neutral-300 block mb-0.5">Classification Evidence:</span>
                  <p className="italic text-neutral-300">{previewItem.classificationReason}</p>
                </div>
              )}

              {/* Summary / Plot */}
              {previewItem.summary && (
                <div className="p-3.5 rounded-xl bg-[#0c0e12] border border-[#1e2129] space-y-1">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-neutral-400">
                    AI Plot Synopsis / Summary
                  </span>
                  <p className="text-xs text-neutral-300 leading-relaxed">
                    {previewItem.summary}
                  </p>
                </div>
              )}

              {/* Path & File Name Diff */}
              <div className="space-y-3">
                <div className="p-3.5 rounded-xl bg-red-950/15 border border-red-900/30 space-y-1">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-red-400">
                    Current Location & File Name
                  </span>
                  <p className="text-xs font-mono text-neutral-300 break-all">
                    {previewItem.currentRelativePath}
                  </p>
                </div>

                <div className="flex justify-center">
                  <ArrowRight size={18} className="text-[#3cd69b] rotate-90 sm:rotate-0" />
                </div>

                <div className="p-3.5 rounded-xl bg-emerald-950/20 border border-emerald-900/40 space-y-1">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-emerald-400 flex items-center justify-between">
                    <span>Proposed Organized Location</span>
                    <span className="text-[10px] text-emerald-300/80 font-normal">Kodi & Plex Compliant</span>
                  </span>
                  <p className="text-xs font-mono text-emerald-200 break-all font-semibold">
                    {previewItem.proposedRelativePath}
                  </p>
                </div>
              </div>

              {/* Sidecar actions */}
              <div className="p-3 rounded-xl bg-[#121418] border border-[#21242d] space-y-1.5 text-xs text-neutral-400">
                <span className="font-semibold text-neutral-200 block">Companion Actions:</span>
                <ul className="list-disc list-inside space-y-1 text-[11px]">
                  <li>Rename & move companion subtitle files (.srt/.vtt) to match canonical name</li>
                  <li>Generate rich Kodi/Plex <code className="text-neutral-300">.nfo</code> metadata XML with summary and genre</li>
                  <li>Preserve thumbnail artwork as <code className="text-neutral-300">poster.jpg</code></li>
                </ul>
              </div>

              {/* Conflict warning */}
              {previewItem.hasConflict && (
                <div className="p-3 rounded-xl bg-amber-950/30 border border-amber-800/40 flex items-center gap-2 text-xs text-amber-300">
                  <AlertTriangle size={16} className="text-amber-400 flex-shrink-0" />
                  <span>{previewItem.conflictReason || 'Target destination file already exists.'}</span>
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-4 bg-[#14171e] border-t border-[#21242d] flex items-center justify-end gap-3">
              <button
                onClick={() => setPreviewItem(null)}
                className="px-4 py-2 rounded-xl text-xs font-medium text-neutral-400 hover:text-white hover:bg-[#1a1d24] transition-colors cursor-pointer"
              >
                Cancel
              </button>
              <button
                onClick={() => handleOrganizeSingle(previewVideo.id)}
                disabled={organizingId === previewVideo.id}
                className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#3cd69b] hover:bg-[#32b885] disabled:opacity-50 text-black cursor-pointer flex items-center gap-1.5 shadow-md"
              >
                {organizingId === previewVideo.id ? (
                  <>
                    <RefreshCw size={13} className="animate-spin" />
                    <span>Executing Move & Rename...</span>
                  </>
                ) : (
                  <>
                    <Check size={14} />
                    <span>Confirm & Organize Now</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Batch Results Modal */}
      {showResultsModal && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#101216] border border-[#262a34] rounded-2xl w-full max-w-2xl overflow-hidden shadow-2xl">
            <div className="px-6 py-4 bg-[#14171e] border-b border-[#21242d] flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <CheckCircle2 size={20} className="text-[#3cd69b]" />
                <h3 className="text-base font-bold text-white">Batch Organization Results</h3>
              </div>
              <button
                onClick={() => setShowResultsModal(false)}
                className="p-1 text-neutral-400 hover:text-white cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div className="p-6 max-h-[60vh] overflow-y-auto space-y-3">
              {lastResults.map((r, i) => (
                <div 
                  key={i} 
                  className={`p-3 rounded-xl border text-xs ${
                    r.success 
                      ? 'bg-emerald-950/15 border-emerald-900/30' 
                      : 'bg-red-950/15 border-red-900/30'
                  }`}
                >
                  <div className="flex items-center justify-between font-semibold">
                    <span className="text-white truncate max-w-md">{r.newFileName || r.originalFileName}</span>
                    <span className={r.success ? 'text-emerald-400' : 'text-red-400'}>
                      {r.success ? 'Success' : 'Failed'}
                    </span>
                  </div>
                  <p className="text-neutral-400 text-[11px] font-mono mt-1 break-all">
                    {r.newPath || r.originalPath}
                  </p>
                  {r.errorMessage && (
                    <p className="text-red-300 text-[11px] mt-1">{r.errorMessage}</p>
                  )}
                </div>
              ))}
            </div>

            <div className="px-6 py-3.5 bg-[#14171e] border-t border-[#21242d] flex justify-end">
              <button
                onClick={() => setShowResultsModal(false)}
                className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#3cd69b] hover:bg-[#32b885] text-black cursor-pointer"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Batch Dry Run Preview Modal */}
      {showDryRunModal && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-[#101216] border border-[#262a34] rounded-2xl w-full max-w-4xl overflow-hidden shadow-2xl flex flex-col max-h-[85vh]">
            <div className="px-6 py-4 bg-[#14171e] border-b border-[#21242d] flex items-center justify-between flex-shrink-0">
              <div className="flex items-center gap-2.5">
                <Eye size={20} className="text-amber-400" />
                <div>
                  <h3 className="text-base font-bold text-white">SmartRoute Media Organizer — Dry Run Preview</h3>
                  <p className="text-xs text-neutral-400">Previewing proposed destinations without touching filesystem ({dryRunPreviews.length} items)</p>
                </div>
              </div>
              <button
                onClick={() => setShowDryRunModal(false)}
                className="p-1 text-neutral-400 hover:text-white cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div className="p-6 overflow-y-auto space-y-3.5 flex-1 divide-y divide-[#1b1e26]">
              {dryRunPreviews.map((p, i) => (
                <div key={i} className="pt-3.5 first:pt-0 space-y-2">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <span className="px-2 py-0.5 rounded text-[11px] font-semibold bg-[#1a221f] text-[#3cd69b] border border-[#2b4238]">
                        Category: {p.category}
                      </span>
                      {p.language && (
                        <span className="px-2 py-0.5 rounded text-[11px] font-semibold bg-[#18232c] text-sky-400 border border-[#22394c]">
                          Language: {p.language}
                        </span>
                      )}
                      {p.releaseYear && (
                        <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-[#181b22] text-neutral-300 border border-[#262a34]">
                          Year: {p.releaseYear}
                        </span>
                      )}
                    </div>
                    {p.confidence !== undefined && (
                      <span className="text-[11px] text-neutral-400 font-mono">
                        Confidence: <strong className="text-emerald-400">{Math.round(p.confidence * 100)}%</strong>
                      </span>
                    )}
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs font-mono">
                    <div className="p-2.5 rounded-lg bg-red-950/15 border border-red-900/30 text-neutral-300 break-all">
                      <span className="text-[10px] uppercase font-bold text-red-400 block mb-0.5">Current:</span>
                      {p.currentRelativePath}
                    </div>
                    <div className="p-2.5 rounded-lg bg-emerald-950/20 border border-emerald-900/40 text-emerald-200 font-semibold break-all">
                      <span className="text-[10px] uppercase font-bold text-emerald-400 block mb-0.5">Proposed Destination:</span>
                      {p.proposedRelativePath}
                    </div>
                  </div>

                  {p.hasConflict && (
                    <div className="text-[11px] text-amber-400 flex items-center gap-1.5">
                      <AlertTriangle size={13} />
                      <span>{p.conflictReason || 'File conflict detected at target path.'}</span>
                    </div>
                  )}
                </div>
              ))}
            </div>

            <div className="px-6 py-4 bg-[#14171e] border-t border-[#21242d] flex items-center justify-between flex-shrink-0">
              <span className="text-xs text-neutral-400">
                {dryRunPreviews.filter(p => p.willChange).length} files will be renamed/moved.
              </span>
              <div className="flex items-center gap-2.5">
                <button
                  onClick={() => setShowDryRunModal(false)}
                  className="px-4 py-2 rounded-xl text-xs font-medium text-neutral-400 hover:text-white hover:bg-[#1a1d24] transition-colors cursor-pointer"
                >
                  Close
                </button>
                <button
                  onClick={() => {
                    setShowDryRunModal(false);
                    handleBatchOrganize();
                  }}
                  className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#3cd69b] hover:bg-[#32b885] text-black cursor-pointer flex items-center gap-1.5 shadow-md"
                >
                  <FolderCheck size={14} />
                  <span>Apply All Changes Now</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
