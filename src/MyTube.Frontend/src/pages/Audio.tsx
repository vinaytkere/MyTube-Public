import React, { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PlayCircle, Music, ListFilter, RotateCcw, Folder, Trash2, XCircle, Sparkles, FolderCheck, X } from 'lucide-react';
import { api } from '../services/api';
import { Video, Category } from '../types';
import { VideoCard } from '../components/VideoCard';
import { useToast } from '../components/Toast';
import { MovieOrganizerTab } from '../components/MovieOrganizerTab';

export const Audio: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const { showToast } = useToast();
  const [showOrganizerModal, setShowOrganizerModal] = useState(false);

  const handleBulkAiAnalyze = async () => {
    if (selectedIds.length === 0) return;
    try {
      showToast('info', `Starting AI Auto-Organize & Transcription for ${selectedIds.length} tracks...`, 'AI Batch Processing');
      await api.batchAnalyzeVideos(selectedIds);
      handleClearSelection();
    } catch (err: any) {
      console.error('Error starting batch AI analyze:', err);
      showToast('error', err.message || 'Failed to start AI batch analysis');
    }
  };

  const handleSmartRouteOrganize = async () => {
    if (selectedIds.length === 0) return;
    try {
      showToast('info', `SmartRoute is analyzing & organizing ${selectedIds.length} tracks...`, 'AI Media Organizer');
      const results = await api.batchOrganizeMedia(selectedIds);
      const successCount = results.filter(r => r.success).length;
      showToast('success', `Organized & renamed ${successCount} tracks using OCI SmartRoute`, 'Complete');
      handleClearSelection();
      fetchMainAudios(false);
      window.dispatchEvent(new Event('videos_updated'));
    } catch (err: any) {
      showToast('error', err.message || 'SmartRoute organization failed');
    }
  };
  
  // Params from URL
  const searchQuery = searchParams.get('search') || '';
  const categoryParam = searchParams.get('category');
  const categoryId = categoryParam ? parseInt(categoryParam, 10) : undefined;
  const sortBy = searchParams.get('sortBy') || 'recentlyadded';
  const page = parseInt(searchParams.get('page') || '1', 10);

  const [videos, setVideos] = useState<Video[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const pageSize = 12;

  // Multi-select states
  const [isSelectionMode, setIsSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [showFolderModal, setShowFolderModal] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [targetFolderId, setTargetFolderId] = useState<number | 'new' | ''>('');

  // Load Categories
  const loadCategories = () => {
    api.getCategories()
      .then(setCategories)
      .catch(err => console.error('Error loading categories:', err));
  };

  useEffect(() => {
    loadCategories();
  }, []);

  // Load Main Feed Audios
  const fetchMainAudios = (showLoading = true) => {
    if (showLoading) setLoading(true);
    api.getVideos({
      search: searchQuery,
      categoryId,
      sortBy,
      mediaType: 'audio',
      page,
      pageSize
    })
      .then((res) => {
        setVideos(res.items);
        setTotalCount(res.totalCount);
      })
      .catch(err => console.error('Error loading audios:', err))
      .finally(() => {
        if (showLoading) setLoading(false);
      });
  };

  useEffect(() => {
    fetchMainAudios(true);
  }, [searchQuery, categoryId, sortBy, page]);

  // Poll for status updates if any audio is still downloading or processing
  useEffect(() => {
    const hasPendingAudios = videos.some(v => v.scanStatus === 'Discovered' || v.scanStatus === 'Processing');
    if (!hasPendingAudios) return;

    const interval = setInterval(() => {
      fetchMainAudios(false);
    }, 3000);

    return () => clearInterval(interval);
  }, [videos, searchQuery, categoryId, sortBy, page]);

  // Handle toggling favorites from inside grid
  const handleFavoriteToggled = async (videoId: string, isFav: boolean) => {
    try {
      await api.toggleFavorite(videoId);
      
      // Update videos list state
      setVideos(prev => prev.map(v => v.id === videoId ? { ...v, isFavorite: isFav } : v));
    } catch (err) {
      console.error('Error toggling favorite:', err);
    }
  };

  const handleLongPress = (videoId: string) => {
    setIsSelectionMode(true);
    setSelectedIds([videoId]);
  };

  const handleSelectToggle = (videoId: string) => {
    setSelectedIds(prev => {
      const idx = prev.indexOf(videoId);
      if (idx > -1) {
        const next = prev.filter(id => id !== videoId);
        if (next.length === 0) {
          setIsSelectionMode(false);
        }
        return next;
      } else {
        return [...prev, videoId];
      }
    });
  };

  const handleClearSelection = () => {
    setIsSelectionMode(false);
    setSelectedIds([]);
  };

  const handleBulkDelete = async () => {
    if (selectedIds.length === 0) return;
    if (!window.confirm(`Are you sure you want to delete the ${selectedIds.length} selected items?`)) return;
    try {
      await api.deleteVideos(selectedIds);
      
      // Update UI state immediately
      setVideos(prev => prev.filter(v => !selectedIds.includes(v.id)));
      setTotalCount(prev => Math.max(0, prev - selectedIds.length));
      
      handleClearSelection();
    } catch (err) {
      console.error('Error during bulk delete:', err);
    }
  };

  const handleBulkMove = async () => {
    if (selectedIds.length === 0) return;
    
    let folderId: number;
    if (targetFolderId === 'new') {
      if (!newFolderName.trim()) return;
      try {
        const newCat = await api.createCategory(newFolderName.trim());
        folderId = newCat.id;
        // Refresh categories
        loadCategories();
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
      await api.updateVideosCategory(selectedIds, folderId);
      
      // Update UI state immediately
      const categoryName = categories.find(c => c.id === folderId)?.name || newFolderName.trim();
      setVideos(prev => prev.map(v => selectedIds.includes(v.id) ? { ...v, categoryId: folderId, categoryName } : v));
      
      setShowFolderModal(false);
      setNewFolderName('');
      setTargetFolderId('');
      handleClearSelection();
    } catch (err) {
      console.error('Failed to update items folder:', err);
    }
  };

  const handleSortChange = (newSort: string) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set('sortBy', newSort);
    newParams.set('page', '1');
    setSearchParams(newParams);
  };

  const handlePageChange = (newPage: number) => {
    const newParams = new URLSearchParams(searchParams);
    newParams.set('page', newPage.toString());
    setSearchParams(newParams);
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  // Category list including "All" chip
  const activeCategory = categories.find(c => c.id === categoryId);

  return (
    <div className="flex-1 p-6 overflow-y-auto text-[#e2e2e8] bg-[#090a0c]">
      {/* Main Feed Section */}
      <section>
        {/* Section Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-5 pb-3 border-b border-[#21242d]">
          <div className="flex items-center gap-2">
            <Music size={18} className="text-[#3cd69b]" />
            <h2 className="text-lg font-bold font-heading text-white">
              {searchQuery 
                ? `Search results for "${searchQuery}"` 
                : activeCategory 
                  ? activeCategory.name 
                  : 'Audio Library'}
            </h2>
            <span className="text-xs text-[#85948b] bg-[#121418] border border-[#21242d] px-2.5 py-0.5 rounded-full font-mono ml-1">
              {totalCount} tracks
            </span>
          </div>

          {/* Sort Filter & AI Organizer */}
          <div className="flex items-center gap-3 text-xs text-neutral-400 self-end sm:self-auto select-none">
            <button
              onClick={() => setShowOrganizerModal(true)}
              className="px-3 py-1.5 rounded-lg bg-[#171920] hover:bg-[#1e222a] border border-[#262a34] text-[#3cd69b] hover:text-[#32b885] font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <Sparkles size={14} />
              AI Audio Organizer
            </button>

            <div className="flex items-center gap-1.5">
              <ListFilter size={14} className="text-neutral-500" />
              <span>Sort:</span>
              <select
                value={sortBy}
                onChange={(e) => handleSortChange(e.target.value)}
                className="bg-neutral-900 border border-neutral-850 rounded px-2.5 py-1 text-neutral-300 hover:text-white outline-none cursor-pointer"
              >
                <option value="recentlyadded">Recently Added</option>
                <option value="oldest">Oldest</option>
                <option value="title">Title (A-Z)</option>
                <option value="duration">Longest First</option>
              </select>
            </div>
          </div>
        </div>

        {/* Video Grid */}
        {loading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="flex flex-col bg-neutral-900 border border-neutral-800 rounded-xl overflow-hidden animate-pulse aspect-video h-[210px] w-full" />
            ))}
          </div>
        ) : videos.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-neutral-500 gap-3">
            <PlayCircle size={48} className="stroke-[1.2]" />
            <p className="text-sm font-medium">No audio tracks found matching your filters.</p>
            {(searchQuery || categoryId) && (
              <button 
                onClick={() => setSearchParams({})} 
                className="mt-2 text-xs font-semibold text-emerald-500 hover:text-emerald-400 flex items-center gap-1.5 cursor-pointer"
              >
                <RotateCcw size={12} />
                Reset Filters
              </button>
            )}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
              {videos.map(video => (
                <VideoCard 
                  key={video.id} 
                  video={video} 
                  onFavoriteToggled={handleFavoriteToggled}
                  isSelectionMode={isSelectionMode}
                  isSelected={selectedIds.includes(video.id)}
                  onSelectToggle={handleSelectToggle}
                  onLongPress={handleLongPress}
                />
              ))}
            </div>

            {/* Pagination Controls */}
            {totalPages > 1 && (
              <div className="flex items-center justify-center gap-2 mt-10 select-none">
                <button
                  disabled={page <= 1}
                  onClick={() => handlePageChange(page - 1)}
                  className="px-3.5 py-1.5 rounded-lg bg-neutral-900 border border-neutral-800 hover:bg-neutral-800 text-xs font-semibold disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
                >
                  Previous
                </button>
                <div className="flex items-center gap-1">
                  {(() => {
                    const pages: (number | string)[] = [];
                    const maxVisible = 5;
                    
                    if (totalPages <= maxVisible + 2) {
                      for (let i = 1; i <= totalPages; i++) pages.push(i);
                    } else {
                      pages.push(1);
                      let start = Math.max(2, page - 1);
                      let end = Math.min(totalPages - 1, page + 1);
                      
                      if (page <= 3) {
                        end = 4;
                      } else if (page >= totalPages - 2) {
                        start = totalPages - 3;
                      }
                      
                      if (start > 2) pages.push('... ');
                      for (let i = start; i <= end; i++) pages.push(i);
                      if (end < totalPages - 1) pages.push(' ...');
                      pages.push(totalPages);
                    }
                    
                    return pages.map((p, idx) => {
                      if (typeof p === 'string') {
                        return (
                          <span key={`ellipsis-${idx}`} className="px-2 text-xs text-neutral-500 font-bold select-none">
                            {p.trim()}
                          </span>
                        );
                      }
                      return (
                        <button
                          key={p}
                          onClick={() => handlePageChange(p)}
                          className={`h-8 w-8 rounded-lg text-xs font-semibold cursor-pointer transition-colors ${
                            page === p
                              ? 'bg-emerald-600 text-white font-bold'
                              : 'bg-neutral-900 border border-neutral-800 hover:bg-neutral-855 text-neutral-300'
                          }`}
                        >
                          {p}
                        </button>
                      );
                    });
                  })()}
                </div>
                <button
                  disabled={page >= totalPages}
                  onClick={() => handlePageChange(page + 1)}
                  className="px-3.5 py-1.5 rounded-lg bg-neutral-900 border border-neutral-800 hover:bg-neutral-800 text-xs font-semibold disabled:opacity-40 disabled:pointer-events-none transition-colors cursor-pointer"
                >
                  Next
                </button>
              </div>
            )}
          </>
        )}
      </section>

      {/* Floating Bulk Action Bar */}
      {isSelectionMode && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 bg-neutral-900 border border-neutral-800 px-6 py-3.5 rounded-2xl shadow-2xl flex items-center gap-4 text-sm font-semibold backdrop-blur-md">
          <span className="text-neutral-300 font-bold text-xs select-none">
            {selectedIds.length} Selected
          </span>
          <div className="h-4 w-px bg-neutral-800" />
          <button 
            onClick={handleSmartRouteOrganize} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-neutral-800 text-[#3cd69b] hover:text-[#32b885] cursor-pointer transition-colors"
          >
            <FolderCheck size={15} />
            SmartRoute Organize
          </button>
          <button 
            onClick={handleBulkAiAnalyze} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-neutral-800 text-purple-400 hover:text-purple-300 cursor-pointer transition-colors"
          >
            <Sparkles size={15} />
            AI Auto-Organize
          </button>
          <button 
            onClick={() => setShowFolderModal(true)} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-neutral-800 text-emerald-500 hover:text-emerald-400 cursor-pointer transition-colors"
          >
            <Folder size={15} />
            Move to Folder
          </button>
          <button 
            onClick={handleBulkDelete} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-neutral-800 text-red-500 hover:text-red-400 cursor-pointer transition-colors"
          >
            <Trash2 size={15} />
            Delete
          </button>
          <button 
            onClick={handleClearSelection} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-neutral-800 text-neutral-400 hover:text-neutral-200 cursor-pointer transition-colors"
          >
            <XCircle size={15} />
            Cancel
          </button>
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
              <XCircle size={20} />
            </button>
            
            <h3 className="text-base font-bold mb-4 font-heading text-neutral-200 flex items-center gap-2">
              <Folder size={18} className="text-emerald-500" />
              Move Items to Folder
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
                    autoComplete="off"
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

      {/* Full AI Audio Organizer Modal */}
      {showOrganizerModal && (
        <div className="fixed inset-0 z-50 bg-black/85 backdrop-blur-md flex items-center justify-center p-4 sm:p-6 overflow-y-auto">
          <div className="bg-[#0e1014] border border-[#21242d] rounded-2xl w-full max-w-6xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden animate-in fade-in zoom-in-95">
            <div className="px-6 py-4 bg-[#14171e] border-b border-[#21242d] flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <Music size={20} className="text-[#3cd69b]" />
                <h3 className="text-base font-bold text-white">SmartRoute Audio Organizer</h3>
              </div>
              <button
                onClick={() => {
                  setShowOrganizerModal(false);
                  fetchMainAudios(false);
                }}
                className="p-1.5 text-neutral-400 hover:text-white rounded-lg hover:bg-[#1e222a] cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            <div className="p-6 overflow-y-auto flex-1">
              <MovieOrganizerTab initialFilter="audio" />
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
