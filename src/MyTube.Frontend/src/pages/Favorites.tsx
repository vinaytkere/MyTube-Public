import React, { useEffect, useState } from 'react';
import { Star, RefreshCw, Folder, Trash2, XCircle } from 'lucide-react';
import { api } from '../services/api';
import { Video, Category } from '../types';
import { VideoCard } from '../components/VideoCard';

import { useToast } from '../components/Toast';

export const Favorites: React.FC = () => {
  const { showToast } = useToast();
  const [videos, setVideos] = useState<Video[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);

  // Multi-select states
  const [isSelectionMode, setIsSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [showFolderModal, setShowFolderModal] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [targetFolderId, setTargetFolderId] = useState<number | 'new' | ''>('');

  const loadCategories = () => {
    api.getCategories()
      .then(setCategories)
      .catch(err => console.error('Error loading categories:', err));
  };

  const loadFavorites = () => {
    setLoading(true);
    api.getFavorites()
      .then(setVideos)
      .catch(err => console.error('Error loading favorites:', err))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadCategories();
    loadFavorites();
    const handleEvents = () => {
      loadCategories();
      loadFavorites();
    };
    window.addEventListener('categories_updated', handleEvents);
    window.addEventListener('videos_updated', handleEvents);
    return () => {
      window.removeEventListener('categories_updated', handleEvents);
      window.removeEventListener('videos_updated', handleEvents);
    };
  }, []);

  const handleFavoriteToggled = async (videoId: string, isFav: boolean) => {
    try {
      await api.toggleFavorite(videoId);
      
      // Since we are in Favorites tab, we want to immediately remove the item if it's unstarred
      if (!isFav) {
        setVideos(prev => prev.filter(v => v.id !== videoId));
      } else {
        setVideos(prev => prev.map(v => v.id === videoId ? { ...v, isFavorite: isFav } : v));
      }
      window.dispatchEvent(new Event('videos_updated'));
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
    if (!window.confirm(`Are you sure you want to delete the ${selectedIds.length} selected videos?`)) return;
    try {
      showToast('info', `Deleting ${selectedIds.length} videos...`, 'Deleting Videos');
      await api.deleteVideos(selectedIds);
      showToast('success', `Deleted ${selectedIds.length} videos successfully`, 'Delete Complete');
      
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      handleClearSelection();
    } catch (err: any) {
      console.error('Error during bulk delete:', err);
      showToast('error', err.message || 'Failed to delete videos', 'Delete Failed');
    }
  };

  const handleBulkMove = async () => {
    if (selectedIds.length === 0) return;
    
    let folderId: number;
    let categoryName = '';
    if (targetFolderId === 'new') {
      if (!newFolderName.trim()) return;
      try {
        const newCat = await api.createCategory(newFolderName.trim());
        folderId = newCat.id;
        categoryName = newCat.name;
        loadCategories();
      } catch (err: any) {
        console.error('Failed to create folder:', err);
        showToast('error', err.message || 'Failed to create playlist');
        return;
      }
    } else if (typeof targetFolderId === 'number') {
      folderId = targetFolderId;
      categoryName = categories.find(c => c.id === folderId)?.name || 'Playlist';
    } else {
      return;
    }

    try {
      await api.updateVideosCategory(selectedIds, folderId);
      showToast('success', `Added ${selectedIds.length} video(s) to playlist "${categoryName}"`, 'Playlist Updated');
      
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      
      setShowFolderModal(false);
      setNewFolderName('');
      setTargetFolderId('');
      handleClearSelection();
    } catch (err: any) {
      console.error('Failed to update videos folder:', err);
      showToast('error', err.message || 'Failed to update playlist');
    }
  };

  return (
    <div className="flex-1 p-6 overflow-y-auto text-[#e2e2e8] bg-[#090a0c]">
      {/* Page Header */}
      <div className="flex items-center gap-2 mb-6 pb-3 border-b border-[#21242d]">
        <Star className="text-amber-400 fill-amber-400" size={20} />
        <h2 className="text-lg font-bold font-heading text-white">Starred Favorites</h2>
        <span className="text-xs text-[#85948b] bg-[#121418] border border-[#21242d] px-2.5 py-0.5 rounded-full font-mono ml-1">
          {videos.length} videos
        </span>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-20">
          <RefreshCw className="animate-spin text-[#3cd69b]" size={28} />
        </div>
      ) : videos.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 text-[#85948b] gap-3">
          <Star size={44} className="stroke-[1.2] text-[#525866]" />
          <p className="text-sm font-medium">You haven't starred any videos yet.</p>
        </div>
      ) : (
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
      )}

      {/* Floating Bulk Action Bar */}
      {isSelectionMode && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 bg-neutral-900 border border-neutral-800 px-6 py-3.5 rounded-2xl shadow-2xl flex items-center gap-4 text-sm font-semibold backdrop-blur-md">
          <span className="text-neutral-300 font-bold text-xs select-none">
            {selectedIds.length} Selected
          </span>
          <div className="h-4 w-px bg-neutral-800" />
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
