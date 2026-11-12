import React, { useEffect, useState, useMemo } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { 
  Play, Film, Folder, Trash2, XCircle, Grid, ChevronDown, Layers, Sparkles, 
  RotateCcw, BookmarkPlus, Check, ChevronLeft, ChevronRight, HardDrive, Cpu
} from 'lucide-react';
import { Modal, Pagination, Badge, Tooltip } from '@mantine/core';
import { api } from '../services/api';
import { Video, Category, User } from '../types';
import { VideoCard } from '../components/VideoCard';
import { useToast } from '../components/Toast';

interface HomeProps {
  currentUser?: User | null;
}

export const Home: React.FC<HomeProps> = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { showToast } = useToast();

  // Query Params
  const searchQuery = searchParams.get('search') || '';
  const categoryParam = searchParams.get('category');
  const categoryId = categoryParam ? parseInt(categoryParam, 10) : undefined;
  const sortBy = searchParams.get('sortBy') || 'recentlyadded';
  const page = parseInt(searchParams.get('page') || '1', 10);
  const pageSize = 12;

  const [videos, setVideos] = useState<Video[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [totalCount, setTotalCount] = useState(0);
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

  const fetchMainVideos = (showLoading = true) => {
    if (showLoading) setLoading(true);
    api.getVideos({
      search: searchQuery,
      categoryId,
      sortBy,
      mediaType: 'video',
      page,
      pageSize
    })
      .then((res) => {
        setVideos(res.items);
        setTotalCount(res.totalCount);
      })
      .catch(err => console.error('Error loading videos:', err))
      .finally(() => {
        if (showLoading) setLoading(false);
      });
  };

  useEffect(() => {
    loadCategories();
    const handleVideosUpdated = () => {
      loadCategories();
      fetchMainVideos(false);
    };

    window.addEventListener('categories_updated', loadCategories);
    window.addEventListener('videos_updated', handleVideosUpdated);
    return () => {
      window.removeEventListener('categories_updated', loadCategories);
      window.removeEventListener('videos_updated', handleVideosUpdated);
    };
  }, []);

  useEffect(() => {
    fetchMainVideos(true);
  }, [searchQuery, categoryId, sortBy, page]);

  const hasPendingVideos = videos.some(v => v.scanStatus === 'Discovered' || v.scanStatus === 'Processing');

  useEffect(() => {
    if (!hasPendingVideos) return;
    const interval = setInterval(() => {
      if (document.hidden) return;
      fetchMainVideos(false);
    }, 3000);
    return () => clearInterval(interval);
  }, [hasPendingVideos]);

  const handleFavoriteToggled = async (videoId: string, isFav: boolean) => {
    try {
      await api.toggleFavorite(videoId);
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
        if (next.length === 0) setIsSelectionMode(false);
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

  const handleBulkAiAnalyze = async () => {
    if (selectedIds.length === 0) return;
    try {
      showToast('info', `Starting AI Auto-Organize & Transcription for ${selectedIds.length} videos...`, 'AI Batch Processing');
      await api.batchAnalyzeVideos(selectedIds);
      handleClearSelection();
    } catch (err: any) {
      console.error('Error starting batch AI analyze:', err);
      showToast('error', err.message || 'Failed to start AI batch analysis');
    }
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
      showToast('error', err.message || 'Failed to delete selected videos', 'Delete Failed');
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

  const handleSelectCategoryPill = (catId?: number) => {
    const newParams = new URLSearchParams(searchParams);
    if (catId) {
      newParams.set('category', catId.toString());
    } else {
      newParams.delete('category');
    }
    newParams.set('page', '1');
    setSearchParams(newParams);
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

  // Continue watching items
  const continueWatchingVideos = useMemo(() => {
    return videos.filter(v => (v.progressSeconds || 0) > 0 && !v.completed).slice(0, 4);
  }, [videos]);

  // Featured Hero item (use favorite or first available video, or fallback)
  const featuredVideo = useMemo(() => {
    return videos.find(v => v.isFavorite) || videos[0] || null;
  }, [videos]);

  const activeCategory = categories.find(c => c.id === categoryId);
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="flex-1 overflow-y-auto text-neutral-100 bg-[#090a0c] p-4 md:p-6 lg:p-8 space-y-8">
      
      {/* Featured Cinematic Media Hero (Stitch Template Design) */}
      {!searchQuery && !categoryId && page === 1 && (
        <section className="relative w-full overflow-hidden rounded-2xl bg-[#0c0e12] border border-[#1a1d24] shadow-2xl">
          {/* Top Ambient Glow Behind Featured Hero */}
          <div className="absolute -top-32 -left-32 w-96 h-96 bg-[#3cd69b]/15 rounded-full blur-[100px] pointer-events-none"></div>
          <div className="absolute top-1/2 right-12 w-80 h-80 bg-cyan-500/10 rounded-full blur-[120px] pointer-events-none"></div>

          {/* Cinematic Backdrop */}
          <div 
            className="relative w-full min-h-[420px] lg:min-h-[480px] flex items-end p-6 lg:p-10 bg-cover bg-center select-none"
            style={{
              backgroundImage: featuredVideo?.thumbnailUrl
                ? `url(${featuredVideo.thumbnailUrl})`
                : "url('https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=1920&q=80')"
            }}
          >
            {/* Dark Scrim Overlays */}
            <div className="absolute inset-0 bg-gradient-to-t from-[#0c0e12] via-[#0c0e12]/80 to-transparent"></div>
            <div className="absolute inset-0 bg-gradient-to-r from-[#0c0e12] via-[#0c0e12]/75 to-transparent w-full lg:w-2/3"></div>

            {/* Hero Content Plate */}
            <div className="relative z-10 max-w-3xl flex flex-col gap-3">
              {/* Telemetry Micro Tag & Status */}
              <div className="flex items-center gap-2 font-mono-telemetry text-xs text-[#3cd69b]">
                <span className="w-2 h-2 rounded-full bg-[#3cd69b] animate-ping"></span>
                <span className="tracking-widest uppercase font-semibold">NVENC HARDWARE TRANSCODE ACTIVE • DIRECT PLAY READY</span>
              </div>

              {/* Title Display */}
              <h1 className="font-heading text-3xl sm:text-4xl lg:text-5xl font-extrabold tracking-tight text-white drop-shadow-md line-clamp-2">
                {featuredVideo ? featuredVideo.displayTitle : 'Dune: Part Two (2024)'}
              </h1>

              {/* High-Density Telemetry & Codec Badges */}
              <div className="flex flex-wrap items-center gap-2 pt-1 font-mono-telemetry text-xs">
                <span className="flex items-center gap-1 px-2 py-0.5 rounded bg-[#1a1c20]/90 text-[#3cd69b] border border-[#21242d] shadow-sm">
                  <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b]"></span>
                  4K UHD
                </span>
                <span className="px-2 py-0.5 rounded bg-[#1a1c20]/90 text-white border border-[#21242d] shadow-sm">
                  HDR10+
                </span>
                <span className="flex items-center gap-1 px-2 py-0.5 rounded bg-[#1a1c20]/90 text-cyan-400 border border-[#21242d] shadow-sm">
                  <span className="w-1.5 h-1.5 rounded-full bg-cyan-400"></span>
                  Dolby Atmos 7.1
                </span>
                <span className="px-2 py-0.5 rounded bg-[#1a1c20]/90 text-neutral-400 border border-[#21242d] shadow-sm">
                  {featuredVideo?.videoCodec || 'HEVC Main 10'} • 68.4 Mbps
                </span>
                <span className="px-2 py-0.5 rounded bg-[#1a1c20]/90 text-amber-400 border border-[#21242d] shadow-sm">
                  ★ 8.8 IMDb
                </span>
              </div>

              {/* Synopsis */}
              <p className="text-sm text-neutral-300 line-clamp-2 max-w-2xl pt-1 leading-relaxed">
                {featuredVideo?.summary || 'Experience crystal clear high-bitrate playback directly from your homelab server with instant hardware acceleration, audio transcode switching, and real-time live AI subtitle transcription.'}
              </p>

              {/* Action Buttons */}
              <div className="flex flex-wrap items-center gap-3 pt-2">
                <button 
                  onClick={() => featuredVideo && navigate(`/video/${featuredVideo.id}`)}
                  className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#3cd69b] text-neutral-950 font-heading text-sm font-bold hover:bg-[#34c78f] transition-all shadow-[0_0_20px_rgba(60,214,155,0.4)] active:scale-95 cursor-pointer"
                >
                  <Play size={18} className="fill-neutral-950" />
                  <span>Resume Playback</span>
                </button>

                <button 
                  onClick={() => featuredVideo && navigate(`/video/${featuredVideo.id}`)}
                  className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[#171920] border border-[#21242d] text-white font-medium text-sm hover:bg-[#21242d] transition-colors shadow-sm cursor-pointer"
                >
                  <Film size={16} className="text-neutral-400" />
                  <span>View Details & Q&A</span>
                </button>

                <button 
                  onClick={() => featuredVideo && handleFavoriteToggled(featuredVideo.id, !featuredVideo.isFavorite)}
                  className="p-2.5 rounded-xl bg-[#121418] border border-[#21242d] text-neutral-400 hover:text-amber-400 transition-colors shadow-sm cursor-pointer"
                  title="Add to Watchlist"
                >
                  <BookmarkPlus size={18} />
                </button>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* Continue Watching Shelf (Stitch Template) */}
      {continueWatchingVideos.length > 0 && !searchQuery && (
        <section className="space-y-4">
          <div className="flex items-baseline justify-between">
            <div className="flex items-baseline gap-3">
              <h2 className="font-heading text-xl font-bold text-white tracking-tight">Continue Watching</h2>
              <span className="font-mono-telemetry text-xs text-neutral-500 uppercase">
                {continueWatchingVideos.length} In-Flight Remux Streams
              </span>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {continueWatchingVideos.map((item) => {
              const progress = item.duration > 0 ? Math.round(((item.progressSeconds || 0) / item.duration) * 100) : 0;
              return (
                <div 
                  key={`continue-${item.id}`}
                  onClick={() => navigate(`/video/${item.id}`)}
                  className="group relative flex flex-col bg-[#121418] border border-[#1a1d24] rounded-xl overflow-hidden shadow-lg hover:border-[#3cd69b]/70 hover:shadow-[0_0_16px_rgba(60,214,155,0.2)] transition-all cursor-pointer"
                >
                  <div className="relative aspect-video w-full bg-black overflow-hidden">
                    <img 
                      src={item.thumbnailUrl} 
                      alt={item.displayTitle} 
                      className="w-full h-full object-cover group-hover:scale-[1.03] transition-transform duration-500 brightness-95" 
                    />
                    <div className="absolute top-2 right-2 px-1.5 py-0.5 rounded font-mono-telemetry text-[10px] bg-[#0c0e12]/90 backdrop-blur-md text-[#3cd69b] border border-[#21242d]">
                      {item.quality || '4K Remux'}
                    </div>

                    <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity bg-black/40">
                      <div className="w-10 h-10 rounded-full bg-[#3cd69b] text-neutral-950 flex items-center justify-center shadow-lg">
                        <Play size={18} className="fill-neutral-950 ml-0.5" />
                      </div>
                    </div>

                    <div className="absolute bottom-0 left-0 right-0 h-1 bg-[#21242d]">
                      <div className="h-full bg-[#3cd69b]" style={{ width: `${progress}%` }} />
                    </div>
                  </div>

                  <div className="p-3">
                    <h4 className="text-xs font-bold text-neutral-200 line-clamp-1 font-heading group-hover:text-[#3cd69b] transition-colors">
                      {item.displayTitle}
                    </h4>
                    <span className="font-mono-telemetry text-[10px] text-neutral-500 mt-1 block">
                      {item.categoryName} • {progress}% Watched
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      )}

      {/* Category Pills Selector Bar */}
      <div className="flex items-center gap-2 overflow-x-auto pb-2 scrollbar-none select-none">
        <button
          onClick={() => handleSelectCategoryPill(undefined)}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer shrink-0 border ${
            !categoryId
              ? 'bg-[#3cd69b] text-neutral-950 border-[#3cd69b] shadow-[0_0_14px_rgba(60,214,155,0.25)]'
              : 'bg-[#121418] border-[#21242d] text-neutral-300 hover:text-white hover:bg-[#1a1c20]'
          }`}
        >
          <Layers size={14} />
          <span>All Media</span>
          <span className={`text-[10px] px-1.5 py-0.2 rounded font-mono-telemetry ${!categoryId ? 'bg-black/20 text-neutral-950 font-bold' : 'bg-[#1a1c20] text-neutral-400'}`}>
            {totalCount}
          </span>
        </button>

        {categories.map((cat) => {
          const isSelected = categoryId === cat.id;
          return (
            <button
              key={`cat-pill-${cat.id}`}
              onClick={() => handleSelectCategoryPill(cat.id)}
              className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer shrink-0 border ${
                isSelected
                  ? 'bg-[#3cd69b] text-neutral-950 border-[#3cd69b] shadow-[0_0_14px_rgba(60,214,155,0.25)]'
                  : 'bg-[#121418] border-[#21242d] text-neutral-300 hover:text-white hover:bg-[#1a1c20]'
              }`}
            >
              <Folder size={14} className={isSelected ? 'text-neutral-950' : 'text-[#3cd69b]'} />
              <span>{cat.name}</span>
              <span className={`text-[10px] px-1.5 py-0.2 rounded font-mono-telemetry ${isSelected ? 'bg-black/20 text-neutral-950 font-bold' : 'bg-[#1a1c20] text-neutral-400'}`}>
                {cat.videoCount}
              </span>
            </button>
          );
        })}
      </div>

      {/* Main Video Grid Feed Section */}
      <section className="space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-[#1a1d24]">
          <div className="flex items-center gap-3">
            <h2 className="text-xl font-bold font-heading text-white">
              {searchQuery 
                ? `Search results for "${searchQuery}"` 
                : activeCategory 
                  ? activeCategory.name 
                  : 'Video Library'}
            </h2>

            {activeCategory && (
              <Badge color="cyberMint" variant="light" size="sm" radius="sm">
                {activeCategory.videoCount} Items
              </Badge>
            )}
          </div>

          {/* Sort & Multi-select Controls */}
          <div className="flex items-center gap-3 text-xs text-neutral-400 self-end sm:self-auto select-none">
            <button
              onClick={() => {
                setIsSelectionMode(!isSelectionMode);
                if (isSelectionMode) setSelectedIds([]);
              }}
              className={`px-3 py-1.5 rounded-lg border text-xs font-semibold transition-all cursor-pointer ${
                isSelectionMode 
                  ? 'bg-[#3cd69b] text-neutral-950 border-[#3cd69b]' 
                  : 'bg-[#121418] border-[#21242d] text-neutral-300 hover:text-white'
              }`}
            >
              {isSelectionMode ? 'Cancel Selection' : 'Batch Actions'}
            </button>

            <div className="relative">
              <select
                value={sortBy}
                onChange={(e) => handleSortChange(e.target.value)}
                className="appearance-none bg-[#121418] border border-[#21242d] rounded-lg px-3 py-1.5 pr-7 text-neutral-300 hover:text-white outline-none cursor-pointer text-xs font-semibold"
              >
                <option value="recentlyadded">Latest First</option>
                <option value="oldest">Oldest First</option>
                <option value="title">Title (A-Z)</option>
                <option value="duration">Longest First</option>
              </select>
              <ChevronDown size={14} className="absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none text-neutral-500" />
            </div>
          </div>
        </div>

        {/* Video Cards Grid */}
        {loading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="flex flex-col bg-[#121418] border border-[#21242d] rounded-xl overflow-hidden animate-pulse aspect-video h-[220px] w-full" />
            ))}
          </div>
        ) : videos.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-neutral-500 gap-3 bg-[#121418]/50 border border-[#21242d] rounded-2xl">
            <Play size={48} className="stroke-[1.2] text-[#3cd69b]" />
            <p className="text-sm font-medium">No videos found matching your filter.</p>
            {(searchQuery || categoryId) && (
              <button 
                onClick={() => setSearchParams({})} 
                className="mt-2 text-xs font-semibold text-[#3cd69b] hover:underline flex items-center gap-1.5 cursor-pointer"
              >
                <RotateCcw size={12} />
                Reset Library Filter
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

            {/* Mantine Pagination Bar */}
            {totalPages > 1 && (
              <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mt-8 pt-6 border-t border-[#1a1d24] text-xs font-semibold select-none">
                <span className="text-neutral-400 font-mono-telemetry">
                  PAGE <span className="text-white font-bold">{page}</span> OF <span className="text-white font-bold">{totalPages}</span> ({totalCount} total videos)
                </span>

                <Pagination
                  value={page}
                  onChange={handlePageChange}
                  total={totalPages}
                  color="cyberMint"
                  radius="md"
                  size="sm"
                  styles={{
                    control: { backgroundColor: '#121418', borderColor: '#21242d', color: '#fff' }
                  }}
                />
              </div>
            )}
          </>
        )}
      </section>

      {/* Floating Bulk Action Bar */}
      {isSelectionMode && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 bg-[#121418]/95 border border-[#21242d] px-5 py-3 rounded-2xl shadow-2xl flex items-center gap-3 text-sm font-semibold backdrop-blur-xl">
          <span className="text-[#3cd69b] font-bold text-xs font-mono-telemetry select-none">
            {selectedIds.length} SELECTED
          </span>
          <div className="h-4 w-px bg-[#21242d]" />
          
          <button 
            onClick={handleBulkAiAnalyze} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-[#171920] text-purple-400 cursor-pointer transition-colors text-xs"
          >
            <Sparkles size={14} />
            <span>AI Auto-Organize</span>
          </button>

          <button 
            onClick={() => setShowFolderModal(true)} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-[#171920] text-[#3cd69b] cursor-pointer transition-colors text-xs"
          >
            <Folder size={14} />
            <span>Move to Playlist</span>
          </button>

          <button 
            onClick={handleBulkDelete} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-[#171920] text-rose-400 cursor-pointer transition-colors text-xs"
          >
            <Trash2 size={14} />
            <span>Delete</span>
          </button>

          <button 
            onClick={handleClearSelection} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl hover:bg-[#171920] text-neutral-400 cursor-pointer transition-colors text-xs"
          >
            <XCircle size={14} />
            <span>Cancel</span>
          </button>
        </div>
      )}

      {/* Folder Move Mantine Modal */}
      <Modal
        opened={showFolderModal}
        onClose={() => { setShowFolderModal(false); setNewFolderName(''); setTargetFolderId(''); }}
        title="Move Videos to Playlist"
        centered
        radius="lg"
        bg="#121418"
        styles={{
          content: { backgroundColor: '#121418', border: '1px solid #21242d', color: '#fff' },
          header: { backgroundColor: '#121418', borderBottom: '1px solid #1a1d24', color: '#fff' },
          title: { fontWeight: 'bold', fontSize: '15px' }
        }}
      >
        <div className="flex flex-col gap-4 pt-2">
          <div className="flex flex-col gap-1.5">
            <label className="text-xs text-neutral-400 font-semibold">Select Target Playlist</label>
            <select
              value={targetFolderId}
              onChange={(e) => {
                const val = e.target.value;
                if (val === 'new') setTargetFolderId('new');
                else if (val === '') setTargetFolderId('');
                else setTargetFolderId(parseInt(val, 10));
              }}
              className="bg-[#090a0c] border border-[#21242d] text-xs rounded-xl px-3 py-2.5 outline-none focus:border-[#3cd69b] text-neutral-200 cursor-pointer"
            >
              <option value="">-- Choose Playlist --</option>
              <option value="new">+ Create New Playlist...</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </div>

          {targetFolderId === 'new' && (
            <div className="flex flex-col gap-1.5 animate-fadeIn">
              <label className="text-xs text-neutral-400 font-semibold">New Playlist Name</label>
              <input
                type="text"
                required
                placeholder="Enter playlist name..."
                value={newFolderName}
                onChange={(e) => setNewFolderName(e.target.value)}
                autoComplete="off"
                className="bg-[#090a0c] border border-[#21242d] text-xs rounded-xl px-3 py-2.5 outline-none focus:border-[#3cd69b] text-neutral-200"
              />
            </div>
          )}

          <button
            onClick={handleBulkMove}
            disabled={targetFolderId === '' || (targetFolderId === 'new' && !newFolderName.trim())}
            className="w-full h-10 mt-2 bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold disabled:bg-neutral-800 disabled:text-neutral-500 text-xs rounded-xl transition-colors cursor-pointer"
          >
            Confirm Move
          </button>
        </div>
      </Modal>
    </div>
  );
};
