import React, { useEffect, useState, useRef } from 'react';
import { 
  Shield, Play, Loader2, Server, CheckCircle2, AlertCircle, RefreshCw, 
  Download, Trash2, Folder, ExternalLink, Globe, Settings, XCircle, Info, Edit2, Search,
  Sparkles, Activity, FileText
} from 'lucide-react';
import { api } from '../services/api';
import { DownloadJob, Video, Category } from '../types';
import { useToast } from '../components/Toast';
import { FileNamingStudio } from '../components/FileNamingStudio';
import { SystemHealthDashboard } from '../components/SystemHealthDashboard';

export const AdminScanner: React.FC = () => {
  // Tabs state
  const [activeTab, setActiveTab] = useState<'scanner' | 'naming' | 'videos' | 'health'>('scanner');
  const { showToast } = useToast();

  // Scanner state
  const [isScanning, setIsScanning] = useState(false);
  const [statusMessage, setStatusMessage] = useState('Idle');
  const [videoCount, setVideoCount] = useState(0);
  const [readyCount, setReadyCount] = useState(0);
  const [loadingScanner, setLoadingScanner] = useState(true);

  // Videos management state
  const [videos, setVideos] = useState<Video[]>([]);
  const [selectedVideoIds, setSelectedVideoIds] = useState<string[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<number | 'all'>('all');
  const [categories, setCategories] = useState<Category[]>([]);
  const [loadingVideos, setLoadingVideos] = useState(false);
  const [renamingVideo, setRenamingVideo] = useState<Video | null>(null);
  const [renameTitle, setRenameTitle] = useState('');

  // Bulk move & delete category states
  const [showFolderModal, setShowFolderModal] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [targetFolderId, setTargetFolderId] = useState<number | 'new' | ''>('');

  // Load Library Stats
  const loadStats = async () => {
    setLoadingScanner(true);
    try {
      const res = await api.getVideos({ pageSize: 1 });
      setVideoCount(res.totalCount);

      // Get scanner status
      const status = await api.getScanStatus();
      setIsScanning(status.isScanning);
      setStatusMessage(status.statusMessage);

      const readyRes = await api.getVideos({ sortBy: 'title', pageSize: 1 });
      setReadyCount(readyRes.totalCount);
    } catch (err) {
      console.error('Error loading library stats:', err);
    } finally {
      setLoadingScanner(false);
    }
  };

  useEffect(() => {
    loadStats();
  }, []);

  // Poll scan status while scanning is active
  useEffect(() => {
    if (!isScanning) return;

    const interval = setInterval(async () => {
      try {
        const status = await api.getScanStatus();
        setIsScanning(status.isScanning);
        setStatusMessage(status.statusMessage);

        if (!status.isScanning) {
          loadStats();
          showToast('success', 'Scanner scan completed. Library stats updated.', 'Scan Complete');
          if (activeTab === 'videos') {
            loadManageVideos();
          }
        }
      } catch (err) {
        console.error('Error polling scanner status:', err);
      }
    }, 2000);

    return () => clearInterval(interval);
  }, [isScanning, activeTab]);

  const handleStartScan = async () => {
    try {
      setIsScanning(true);
      setStatusMessage('Triggering scan...');
      showToast('info', 'Triggering library scan on /srv/mytube/videos...', 'Scan Triggered');
      await api.triggerScan();
      showToast('success', 'Library scan started in the background.', 'Scan Started');
    } catch (err: any) {
      console.error('Failed to trigger scan:', err);
      setIsScanning(false);
      setStatusMessage('Failed to trigger scan.');
      showToast('error', err.message || 'Failed to trigger scan.', 'Scan Failed');
    }
  };

  const loadManageVideos = async () => {
    setLoadingVideos(true);
    try {
      const cats = await api.getCategories();
      setCategories(cats);

      const res = await api.getVideos({
        search: searchQuery,
        categoryId: selectedCategory === 'all' ? undefined : selectedCategory,
        pageSize: 100,
        sortBy: 'title'
      });
      setVideos(res.items);
    } catch (err) {
      console.error('Error loading videos for management:', err);
      showToast('error', 'Failed to load videos library list.', 'Error');
    } finally {
      setLoadingVideos(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'videos') {
      loadManageVideos();
    }
  }, [activeTab, searchQuery, selectedCategory]);

  const handleDeleteSelectedVideos = async () => {
    if (selectedVideoIds.length === 0) return;
    
    if (!window.confirm(`Are you sure you want to permanently delete the ${selectedVideoIds.length} selected video(s)? This will delete the files on disk.`)) {
      return;
    }

    try {
      showToast('info', `Deleting ${selectedVideoIds.length} video(s)...`, 'Deleting Videos');
      const res = await api.deleteVideos(selectedVideoIds);
      showToast('success', `Successfully deleted ${res.deletedCount} video(s) from disk and database.`, 'Delete Complete');
      setSelectedVideoIds([]);
      loadStats();
      loadManageVideos();
    } catch (err: any) {
      console.error('Error deleting videos:', err);
      showToast('error', err.message || 'Failed to delete selected videos', 'Delete Failed');
    }
  };

  const handleSaveRename = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!renamingVideo || !renameTitle.trim()) return;

    try {
      showToast('info', 'Renaming video...', 'Renaming');
      await api.renameVideo(renamingVideo.id, renameTitle.trim());
      showToast('success', 'Video renamed successfully.', 'Rename Complete');
      setRenamingVideo(null);
      loadManageVideos();
    } catch (err: any) {
      console.error('Error renaming video:', err);
      showToast('error', err.message || 'Failed to rename video', 'Rename Failed');
    }
  };

  const handleDeleteCategory = async () => {
    if (selectedCategory === 'all') return;
    
    const catName = categories.find(c => c.id === selectedCategory)?.name || '';
    if (!window.confirm(`Are you sure you want to permanently delete the folder "${catName}"? This will delete all videos inside it on disk and remove the category.`)) {
      return;
    }

    try {
      showToast('info', `Deleting folder "${catName}"...`, 'Deleting Folder');
      await api.deleteCategory(selectedCategory);
      showToast('success', `Successfully deleted folder "${catName}" and its videos.`, 'Delete Complete');
      setSelectedCategory('all');
      loadStats();
      window.dispatchEvent(new Event('categories_updated'));
    } catch (err: any) {
      console.error('Error deleting category:', err);
      showToast('error', err.message || 'Failed to delete category', 'Delete Failed');
    }
  };

  const handleBulkMove = async () => {
    if (selectedVideoIds.length === 0) return;
    
    let folderId: number;
    if (targetFolderId === 'new') {
      if (!newFolderName.trim()) return;
      try {
        const newCat = await api.createCategory(newFolderName.trim());
        folderId = newCat.id;
        loadManageVideos();
        window.dispatchEvent(new Event('categories_updated'));
      } catch (err) {
        console.error('Failed to create folder:', err);
        return;
      }
    } else if (typeof targetFolderId === 'number') {
      folderId = targetFolderId;
    } else {
      return;
    }

    try {
      showToast('info', `Moving ${selectedVideoIds.length} video(s)...`, 'Moving Videos');
      await api.updateVideosCategory(selectedVideoIds, folderId);
      showToast('success', `Moved video(s) successfully.`, 'Move Complete');
      
      setShowFolderModal(false);
      setNewFolderName('');
      setTargetFolderId('');
      setSelectedVideoIds([]);
      loadStats();
      loadManageVideos();
      window.dispatchEvent(new Event('categories_updated'));
    } catch (err: any) {
      console.error('Failed to update videos folder:', err);
      showToast('error', err.message || 'Failed to move video(s)', 'Move Failed');
    }
  };

  return (
    <div className="flex-1 p-6 overflow-y-auto text-neutral-100 bg-[#0f0f0f]">
      {/* Page Header */}
      <div className="flex items-center justify-between mb-6 pb-3 border-b border-neutral-900">
        <div className="flex items-center gap-2">
          <Shield className="text-emerald-500" size={20} />
          <h2 className="text-lg font-bold font-heading">Library Administration</h2>
        </div>
        <button 
          onClick={loadStats} 
          disabled={loadingScanner || isScanning}
          className="p-1.5 rounded-lg bg-neutral-900 border border-neutral-800 hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors disabled:opacity-40 cursor-pointer"
        >
          <RefreshCw size={14} className={loadingScanner ? 'animate-spin' : ''} />
        </button>
      </div>

      {/* Tabs Navigation */}
      <div className="flex border-b border-neutral-900 mb-6 gap-2 overflow-x-auto pb-1">
        <button
          onClick={() => setActiveTab('scanner')}
          className={`flex items-center gap-2 px-4 py-2 border-b-2 text-sm font-semibold transition-all whitespace-nowrap cursor-pointer ${
            activeTab === 'scanner'
              ? 'border-emerald-500 text-emerald-400'
              : 'border-transparent text-neutral-400 hover:text-neutral-200'
          }`}
        >
          <Server size={16} />
          Library Scanner
        </button>

        <button
          onClick={() => setActiveTab('naming')}
          className={`flex items-center gap-2 px-4 py-2 border-b-2 text-sm font-semibold transition-all whitespace-nowrap cursor-pointer ${
            activeTab === 'naming'
              ? 'border-emerald-500 text-emerald-400'
              : 'border-transparent text-neutral-400 hover:text-neutral-200'
          }`}
        >
          <Sparkles size={16} className="text-[#3cd69b]" />
          <span>File Naming & Organizer</span>
          <span className="text-[10px] bg-emerald-500/20 text-[#3cd69b] px-1.5 py-0.2 rounded font-bold border border-emerald-500/30">
            Sonarr
          </span>
        </button>

        <button
          onClick={() => setActiveTab('health')}
          className={`flex items-center gap-2 px-4 py-2 border-b-2 text-sm font-semibold transition-all whitespace-nowrap cursor-pointer ${
            activeTab === 'health'
              ? 'border-emerald-500 text-emerald-400'
              : 'border-transparent text-neutral-400 hover:text-neutral-200'
          }`}
        >
          <Activity size={16} />
          System Health & Storage
        </button>

        <button
          onClick={() => setActiveTab('videos')}
          className={`flex items-center gap-2 px-4 py-2 border-b-2 text-sm font-semibold transition-all whitespace-nowrap cursor-pointer ${
            activeTab === 'videos'
              ? 'border-emerald-500 text-emerald-400'
              : 'border-transparent text-neutral-400 hover:text-neutral-200'
          }`}
        >
          <Folder size={16} />
          Manage Videos
        </button>
      </div>

      {/* Tab Contents */}
      {activeTab === 'scanner' ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Scanner Control Card */}
          <div className="md:col-span-2 rounded-2xl bg-neutral-900 border border-neutral-800 p-6 glass-panel flex flex-col justify-between">
            <div>
              <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-400 mb-4 flex items-center gap-2">
                <Server size={16} className="text-emerald-500" />
                /srv/mytube/videos Scanner Status
              </h3>

              <div className="flex flex-col gap-4">
                <div className="flex items-center gap-3">
                  <div className={`flex h-10 w-10 items-center justify-center rounded-xl border ${
                    isScanning 
                      ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-500' 
                      : 'bg-neutral-950 border-neutral-800 text-neutral-500'
                  }`}>
                    {isScanning ? (
                      <Loader2 className="animate-spin" size={20} />
                    ) : (
                      <CheckCircle2 size={20} />
                    )}
                  </div>
                  
                  <div className="flex flex-col">
                    <span className="text-sm font-semibold">
                      {isScanning ? 'Scanner Active' : 'Scanner Idle'}
                    </span>
                    <span className="text-xs text-neutral-400 mt-0.5">
                      {statusMessage}
                    </span>
                  </div>
                </div>

                <p className="text-xs text-neutral-500 leading-relaxed mt-2 border-t border-neutral-850 pt-4">
                  The library scanner runs recursively over `/media` (`/srv/mytube/videos`). It automatically reads video tags, duration, resolution, and generates thumbnail posters asynchronously. Videos are loaded and downloaded from this folder.
                </p>
              </div>
            </div>

            <button
              onClick={handleStartScan}
              disabled={isScanning || loadingScanner}
              className="flex h-10 items-center justify-center gap-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:bg-neutral-880 disabled:text-neutral-500 text-sm font-semibold transition-all mt-6 shadow-md hover:shadow-emerald-950/20 cursor-pointer active:scale-[0.98] disabled:pointer-events-none"
            >
              {isScanning ? (
                <>
                  <Loader2 className="animate-spin" size={16} />
                  Scanning /srv/mytube/videos Library...
                </>
              ) : (
                <>
                  <Play size={16} className="fill-white" />
                  Scan /srv/mytube/videos Library Now
                </>
              )}
            </button>
          </div>

          {/* Statistics Card */}
          <div className="rounded-2xl bg-neutral-900 border border-neutral-800 p-6 glass-panel flex flex-col justify-between">
            <div>
              <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-400 mb-4 flex items-center gap-2">
                <CheckCircle2 size={16} className="text-emerald-500" />
                Library Statistics
              </h3>

              {loadingScanner ? (
                <div className="flex items-center justify-center py-10">
                  <Loader2 className="animate-spin text-emerald-500" size={24} />
                </div>
              ) : (
                <div className="flex flex-col gap-5">
                  <div className="flex items-center justify-between border-b border-neutral-850 pb-3">
                    <span className="text-xs text-neutral-400 font-medium">Ready Videos</span>
                    <span className="text-2xl font-bold font-heading text-emerald-400">{readyCount}</span>
                  </div>

                  <div className="flex items-center justify-between border-b border-neutral-850 pb-3">
                    <span className="text-xs text-neutral-400 font-medium">Server Storage Mount</span>
                    <span className="text-xs font-semibold bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 px-2.5 py-0.5 rounded-full">
                      Mounted (Read-Write)
                    </span>
                  </div>

                  <div className="flex flex-col gap-1 mt-1 text-[11px] text-neutral-500 leading-normal">
                    <div className="flex items-center gap-1.5">
                      <div className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                      <span>Watch progress and favorites are tracked per user</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <div className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                      <span>Metadata and thumbnails are cached to optimize CPU</span>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      ) : activeTab === 'naming' ? (
        <FileNamingStudio />
      ) : activeTab === 'health' ? (
        <SystemHealthDashboard />
      ) : (
        <div className="rounded-2xl bg-neutral-900 border border-neutral-800 p-6 glass-panel flex flex-col min-h-[500px]">
          {/* Header Controls */}
          <div className="flex flex-col sm:flex-row gap-4 justify-between items-center mb-6 pb-4 border-b border-neutral-850">
            <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-400 flex items-center gap-2">
              <Folder size={16} className="text-emerald-500" />
              Manage Videos Library
            </h3>

            <div className="flex flex-wrap items-center gap-3 w-full sm:w-auto">
              {/* Search input */}
              <div className="relative flex-1 sm:flex-initial">
                <Search size={14} className="absolute left-3.5 top-3 text-neutral-500" />
                <input
                  type="text"
                  placeholder="Search videos..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="w-full sm:w-60 pl-9 pr-4 py-2 rounded-xl bg-neutral-950 border border-neutral-800 text-xs text-neutral-200 placeholder-neutral-600 focus:outline-none focus:border-emerald-500 transition-colors"
                />
              </div>

              {/* Category Filter */}
              <select
                value={selectedCategory}
                onChange={(e) => {
                  const val = e.target.value;
                  setSelectedCategory(val === 'all' ? 'all' : parseInt(val, 10));
                }}
                className="bg-neutral-950 border border-neutral-800 rounded-xl px-3 py-2 text-xs text-neutral-300 hover:text-white outline-none cursor-pointer"
              >
                <option value="all">All Categories</option>
                {categories.map((cat) => (
                  <option key={cat.id} value={cat.id}>
                    {cat.name} ({cat.videoCount})
                  </option>
                ))}
              </select>

              {selectedCategory !== 'all' && (
                <button
                  onClick={handleDeleteCategory}
                  className="px-3 py-2 rounded-xl bg-red-950 hover:bg-red-900 border border-red-900/50 text-red-400 hover:text-red-300 text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer shrink-0"
                  title="Delete Folder"
                >
                  <Trash2 size={13} />
                  Delete Folder
                </button>
              )}
            </div>
          </div>

          {/* Bulk Action Sticky Bar */}
          {selectedVideoIds.length > 0 && (
            <div className="flex items-center justify-between p-3.5 mb-5 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-semibold animate-slide-in select-none">
              <div className="flex items-center gap-2">
                <span>{selectedVideoIds.length} video(s) selected</span>
              </div>
              <div className="flex gap-2">
                <button
                  onClick={() => setSelectedVideoIds([])}
                  className="px-3 py-1.5 rounded-lg border border-neutral-800 hover:bg-neutral-800 text-neutral-400 hover:text-neutral-200 transition-colors cursor-pointer"
                >
                  Clear Selection
                </button>
                <button
                  onClick={() => setShowFolderModal(true)}
                  className="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white flex items-center gap-1.5 transition-colors cursor-pointer shadow-md"
                >
                  <Folder size={12} />
                  Move Selected
                </button>
                <button
                  onClick={handleDeleteSelectedVideos}
                  className="px-3 py-1.5 rounded-lg bg-red-600 hover:bg-red-500 text-white flex items-center gap-1.5 transition-colors cursor-pointer shadow-md"
                >
                  <Trash2 size={12} />
                  Delete Selected
                </button>
              </div>
            </div>
          )}

          {/* Video List Table */}
          {loadingVideos ? (
            <div className="flex-1 flex items-center justify-center py-20">
              <Loader2 className="animate-spin text-emerald-500" size={32} />
            </div>
          ) : videos.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-12 text-neutral-500 gap-2">
              <Folder size={40} className="text-neutral-800" />
              <span className="text-sm font-semibold">No videos found</span>
              <span className="text-xs">Try adjusting your search query or category filters.</span>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs border-collapse">
                <thead>
                  <tr className="border-b border-neutral-850 text-neutral-400 font-bold select-none">
                    <th className="p-3 w-10">
                      <input
                        type="checkbox"
                        checked={videos.length > 0 && selectedVideoIds.length === videos.length}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setSelectedVideoIds(videos.map(v => v.id));
                          } else {
                            setSelectedVideoIds([]);
                          }
                        }}
                        className="rounded border-neutral-800 text-emerald-600 bg-neutral-950 focus:ring-emerald-500"
                      />
                    </th>
                    <th className="p-3">Video</th>
                    <th className="p-3 w-28">Category</th>
                    <th className="p-3 w-40">File Name</th>
                    <th className="p-3 w-28 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {videos.map((video) => {
                    const isSelected = selectedVideoIds.includes(video.id);
                    return (
                      <tr 
                        key={video.id} 
                        className={`border-b border-neutral-900 hover:bg-neutral-850/40 transition-colors ${
                          isSelected ? 'bg-emerald-500/5' : ''
                        }`}
                      >
                        <td className="p-3">
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setSelectedVideoIds(prev => [...prev, video.id]);
                              } else {
                                setSelectedVideoIds(prev => prev.filter(id => id !== video.id));
                              }
                            }}
                            className="rounded border-neutral-800 text-emerald-600 bg-neutral-950 focus:ring-emerald-500 cursor-pointer"
                          />
                        </td>
                        <td className="p-3">
                          <div className="flex items-center gap-3">
                            <div className="h-10 w-16 bg-neutral-950 rounded border border-neutral-850 overflow-hidden shrink-0 flex items-center justify-center relative">
                              {video.thumbnailUrl ? (
                                <img src={video.thumbnailUrl} alt={video.displayTitle} className="h-full w-full object-cover" />
                              ) : (
                                <Server size={14} className="text-neutral-700" />
                              )}
                            </div>
                            <span className="font-bold text-neutral-200 line-clamp-1" title={video.displayTitle}>
                              {video.displayTitle}
                            </span>
                          </div>
                        </td>
                        <td className="p-3">
                          <span className="px-2 py-0.5 bg-neutral-950 border border-neutral-800 rounded-full text-[10px] text-neutral-400 font-semibold truncate max-w-[100px] block text-center" title={video.categoryName}>
                            {video.categoryName}
                          </span>
                        </td>
                        <td className="p-3 font-mono text-[10px] text-neutral-500 truncate max-w-[150px]" title={video.fileName}>
                          {video.fileName}
                        </td>
                        <td className="p-3 text-right">
                          <div className="flex items-center justify-end gap-1 select-none">
                            <button
                              onClick={() => {
                                setRenamingVideo(video);
                                setRenameTitle(video.displayTitle);
                              }}
                              className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer"
                              title="Rename Video"
                            >
                              <Edit2 size={13} />
                            </button>
                            <button
                              onClick={async () => {
                                if (window.confirm(`Are you sure you want to permanently delete "${video.displayTitle}"?`)) {
                                  try {
                                    showToast('info', 'Deleting video...', 'Deleting Video');
                                    await api.deleteVideos([video.id]);
                                    showToast('success', 'Video deleted successfully.', 'Delete Complete');
                                    loadStats();
                                    loadManageVideos();
                                  } catch (err: any) {
                                    showToast('error', err.message || 'Failed to delete video', 'Delete Failed');
                                  }
                                }
                              }}
                              className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-500 hover:text-red-400 transition-colors cursor-pointer"
                              title="Delete Video"
                            >
                              <Trash2 size={13} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Rename Video Modal */}
      {renamingVideo && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-2xl bg-neutral-900 border border-neutral-800 p-6 shadow-2xl relative overflow-hidden flex flex-col">
            {/* Header */}
            <div className="flex items-center justify-between border-b border-neutral-800 pb-3 mb-4 select-none">
              <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-200">
                Rename Video
              </h3>
              <button 
                onClick={() => setRenamingVideo(null)} 
                className="text-neutral-500 hover:text-neutral-300 cursor-pointer text-sm font-medium"
              >
                ✕
              </button>
            </div>

            {/* Form */}
            <form onSubmit={handleSaveRename} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label htmlFor="admin-rename-input" className="text-xs text-neutral-400 font-semibold select-none">
                  New Title
                </label>
                <input
                  id="admin-rename-input"
                  type="text"
                  required
                  placeholder="e.g. Video Title..."
                  value={renameTitle}
                  onChange={(e) => setRenameTitle(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-xl bg-neutral-950 border border-neutral-800 text-sm focus:outline-none focus:border-emerald-500 transition-colors placeholder:text-neutral-700"
                />
              </div>

              <div className="flex justify-end gap-2 mt-2 select-none">
                <button
                  type="button"
                  onClick={() => setRenamingVideo(null)}
                  className="px-4 py-2 border border-neutral-800 hover:bg-neutral-850 text-neutral-300 rounded-xl text-xs font-bold transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!renameTitle.trim() || renameTitle.trim() === renamingVideo.displayTitle}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:bg-neutral-850 disabled:text-neutral-500 text-white rounded-xl text-xs font-bold transition-colors cursor-pointer"
                >
                  Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
      {/* Folder Move Modal */}
      {showFolderModal && (
        <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-neutral-900 border border-neutral-800 rounded-2xl p-6 shadow-2xl relative">
            <button 
              onClick={() => { setShowFolderModal(false); setNewFolderName(''); setTargetFolderId(''); }} 
              className="absolute top-4 right-4 text-neutral-500 hover:text-white transition-colors cursor-pointer"
            >
              ✕
            </button>
            
            <h3 className="text-base font-bold mb-4 font-heading text-neutral-200 flex items-center gap-2">
              <Folder size={18} className="text-emerald-500" />
              Move Videos to Folder
            </h3>

            <div className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-neutral-400 font-semibold">Select Target Folder</label>
                <select
                  value={targetFolderId}
                  onChange={(e) => {
                    const val = e.target.value;
                    if (val === 'new') {
                      setTargetFolderId('new');
                    } else if (val === '') {
                      setTargetFolderId('');
                    } else {
                      setTargetFolderId(parseInt(val, 10));
                    }
                  }}
                  className="bg-neutral-950 border border-neutral-800 text-sm rounded-xl px-4 py-2.5 outline-none focus:border-emerald-500 transition-colors text-neutral-200 cursor-pointer"
                >
                  <option value="">-- Choose Folder --</option>
                  <option value="new">+ Create New Folder...</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>

              {targetFolderId === 'new' && (
                <div className="flex flex-col gap-1.5 animate-fadeIn">
                  <label className="text-xs text-neutral-400 font-semibold">New Folder Name</label>
                  <input
                    type="text"
                    required
                    placeholder="Enter folder name..."
                    value={newFolderName}
                    onChange={(e) => setNewFolderName(e.target.value)}
                    className="bg-neutral-950 border border-neutral-800 text-sm rounded-xl px-4 py-2.5 outline-none focus:border-emerald-500 transition-colors text-neutral-200"
                  />
                </div>
              )}

              <button
                onClick={handleBulkMove}
                disabled={targetFolderId === '' || (targetFolderId === 'new' && !newFolderName.trim())}
                className="w-full h-10 mt-2 bg-emerald-600 hover:bg-emerald-500 disabled:bg-neutral-800 disabled:text-neutral-500 text-sm font-semibold rounded-xl transition-colors cursor-pointer"
              >
                Apply Move
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
