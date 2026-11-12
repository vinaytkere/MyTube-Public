import React, { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { PlayCircle, Clock, RotateCcw, CheckCircle2, Edit3, Share2, Grid, ChevronDown, Sparkles, Folder, Trash2, XCircle, ChevronLeft, ChevronRight } from 'lucide-react';
import { api } from '../services/api';
import { Video, Category, User } from '../types';
import { VideoCard } from '../components/VideoCard';
import { useToast } from '../components/Toast';
import { Settings } from './Settings';

interface ProfileProps {
  currentUser?: User | null;
}

export const Profile: React.FC<ProfileProps> = ({ currentUser }) => {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { showToast } = useToast();

  const tabParam = searchParams.get('tab') || 'uploads';
  const [activeTab, setActiveTab] = useState<'uploads' | 'history' | 'settings'>(
    tabParam === 'history' ? 'history' : tabParam === 'settings' ? 'settings' : 'uploads'
  );

  const sortBy = searchParams.get('sortBy') || 'recentlyadded';
  const page = parseInt(searchParams.get('page') || '1', 10);
  const pageSize = 12;

  const [videos, setVideos] = useState<Video[]>([]);
  const [continueWatching, setContinueWatching] = useState<Video[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);

  // Multi-select states
  const [isSelectionMode, setIsSelectionMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [showFolderModal, setShowFolderModal] = useState(false);
  const [newFolderName, setNewFolderName] = useState('');
  const [targetFolderId, setTargetFolderId] = useState<number | 'new' | ''>('');

  useEffect(() => {
    api.getCategories().then(setCategories).catch(() => {});
    api.getContinueWatching().then(setContinueWatching).catch(() => {});
  }, []);

  const fetchUploads = (showLoading = true) => {
    if (showLoading) setLoading(true);
    api.getVideos({
      sortBy,
      mediaType: 'video',
      page,
      pageSize
    })
      .then((res) => {
        setVideos(res.items);
        setTotalCount(res.totalCount);
      })
      .catch(err => console.error('Error loading profile videos:', err))
      .finally(() => {
        if (showLoading) setLoading(false);
      });
  };

  useEffect(() => {
    fetchUploads(true);
  }, [sortBy, page]);

  const handleFavoriteToggled = async (videoId: string, isFav: boolean) => {
    try {
      await api.toggleFavorite(videoId);
      setVideos(prev => prev.map(v => v.id === videoId ? { ...v, isFavorite: isFav } : v));
      setContinueWatching(prev => prev.map(v => v.id === videoId ? { ...v, isFavorite: isFav } : v));
    } catch (err) {
      console.error('Error toggling favorite:', err);
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

  const handleTabChange = (tab: 'uploads' | 'history' | 'settings') => {
    setActiveTab(tab);
    const newParams = new URLSearchParams(searchParams);
    newParams.set('tab', tab);
    setSearchParams(newParams);
  };

  const userNameDisplay = currentUser?.fullName || currentUser?.username || 'User';
  const roleDisplay = currentUser?.role || 'ADMINISTRATOR';
  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="flex-1 overflow-y-auto text-neutral-100 bg-[#090a0c]">
      {/* Profile Cover Banner matching screenshot */}
      <div className="relative w-full h-64 sm:h-80 bg-server-cover border-b border-[#21242d] p-6 sm:p-8 flex flex-col justify-end">
        {/* Cover Action Buttons */}
        <div className="absolute top-6 right-6 flex items-center gap-3">
          <button 
            onClick={() => handleTabChange('settings')}
            className="flex items-center gap-2 px-4 py-2 rounded-full border border-[#3cd69b] text-[#3cd69b] hover:bg-[#3cd69b]/10 text-xs font-semibold transition-all cursor-pointer shadow-lg backdrop-blur-md"
          >
            <Edit3 size={14} />
            Edit Profile
          </button>
          <button 
            onClick={() => { navigator.clipboard.writeText(window.location.href); showToast('success', 'Profile link copied to clipboard!'); }}
            className="p-2 rounded-full bg-[#121418]/80 border border-[#21242d] text-neutral-300 hover:text-white hover:bg-[#171920] transition-colors cursor-pointer"
            title="Share Profile"
          >
            <Share2 size={16} />
          </button>
        </div>

        {/* Profile Info Overlay */}
        <div className="flex flex-col sm:flex-row sm:items-end gap-5">
          {/* Avatar Inset */}
          <div className="h-28 w-28 sm:h-36 sm:w-36 rounded-2xl bg-[#121418] border-4 border-[#090a0c] overflow-hidden shadow-2xl shrink-0 -mb-4 sm:-mb-6 z-10 relative">
            <img 
              src="https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80" 
              alt={userNameDisplay} 
              className="h-full w-full object-cover"
              onError={(e) => {
                e.currentTarget.style.display = 'none';
              }}
            />
            <div className="h-full w-full flex items-center justify-center bg-gradient-to-br from-emerald-900 to-black text-[#3cd69b] font-bold text-3xl">
              {userNameDisplay.substring(0, 2).toUpperCase()}
            </div>
          </div>

          {/* Name & Role details */}
          <div className="flex flex-col gap-1.5 mb-2">
            <div className="flex items-center gap-2.5">
              <h1 className="text-3xl font-bold font-heading text-white tracking-tight">{userNameDisplay}</h1>
              <CheckCircle2 size={22} className="text-[#3cd69b] fill-[#3cd69b]/20" />
            </div>

            <div className="flex items-center gap-3 text-xs">
              <span className="bg-[#3cd69b]/15 text-[#3cd69b] border border-[#3cd69b]/30 px-2.5 py-0.5 rounded-md font-bold uppercase tracking-wider text-[10px]">
                {roleDisplay}
              </span>
              <span className="text-neutral-400 font-medium">Member since 2021</span>
            </div>
          </div>
        </div>
      </div>

      {/* Profile Metrics Row (4 Cards) matching screenshot */}
      <div className="p-6 sm:p-8 space-y-8">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 text-center shadow-lg hover:border-neutral-700 transition-colors">
            <div className="text-2xl sm:text-3xl font-black text-white font-heading">{totalCount > 0 ? totalCount.toLocaleString() : '1,774'}</div>
            <div className="text-xs text-neutral-400 font-semibold mt-1">Videos Uploaded</div>
          </div>

          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 text-center shadow-lg hover:border-neutral-700 transition-colors">
            <div className="text-2xl sm:text-3xl font-black text-white font-heading">45.2K</div>
            <div className="text-xs text-neutral-400 font-semibold mt-1">Followers</div>
          </div>

          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 text-center shadow-lg hover:border-neutral-700 transition-colors">
            <div className="text-2xl sm:text-3xl font-black text-white font-heading">8.1M</div>
            <div className="text-xs text-neutral-400 font-semibold mt-1">Total Views</div>
          </div>

          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 text-center shadow-lg hover:border-neutral-700 transition-colors">
            <div className="text-2xl sm:text-3xl font-black text-[#3cd69b] font-heading">342</div>
            <div className="text-xs text-neutral-400 font-semibold mt-1">Favorites</div>
          </div>
        </div>

        {/* Tab Navigation Bar matching screenshot */}
        <div className="flex items-center gap-8 border-b border-[#21242d] text-sm font-semibold select-none">
          <button
            onClick={() => handleTabChange('uploads')}
            className={`pb-3.5 relative transition-colors cursor-pointer ${
              activeTab === 'uploads' ? 'text-[#3cd69b]' : 'text-neutral-400 hover:text-white'
            }`}
          >
            My Uploads
            {activeTab === 'uploads' && (
              <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#3cd69b] rounded-full shadow-[0_0_8px_#3cd69b]" />
            )}
          </button>

          <button
            onClick={() => handleTabChange('history')}
            className={`pb-3.5 relative transition-colors cursor-pointer ${
              activeTab === 'history' ? 'text-[#3cd69b]' : 'text-neutral-400 hover:text-white'
            }`}
          >
            Watch History
            {activeTab === 'history' && (
              <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#3cd69b] rounded-full shadow-[0_0_8px_#3cd69b]" />
            )}
          </button>

          <button
            onClick={() => handleTabChange('settings')}
            className={`pb-3.5 relative transition-colors cursor-pointer ${
              activeTab === 'settings' ? 'text-[#3cd69b]' : 'text-neutral-400 hover:text-white'
            }`}
          >
            Account Settings
            {activeTab === 'settings' && (
              <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-[#3cd69b] rounded-full shadow-[0_0_8px_#3cd69b]" />
            )}
          </button>
        </div>

        {/* Tab Content */}
        {activeTab === 'history' ? (
          <section>
            <div className="flex items-center gap-2 mb-4">
              <Clock size={18} className="text-[#3cd69b]" />
              <h2 className="text-lg font-bold font-heading">Continue Watching & History</h2>
            </div>
            
            {continueWatching.length === 0 ? (
              <p className="text-xs text-neutral-500">No watch history found.</p>
            ) : (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
                {continueWatching.map(video => (
                  <VideoCard 
                    key={`profile-cw-${video.id}`} 
                    video={video} 
                    onFavoriteToggled={handleFavoriteToggled}
                  />
                ))}
              </div>
            )}
          </section>
        ) : activeTab === 'settings' ? (
          <Settings currentUser={currentUser ?? null} />
        ) : (
          /* Recent Uploads Section matching screenshot */
          <section>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
              <h2 className="text-lg font-bold font-heading text-neutral-100">
                Recent Uploads
              </h2>

              {/* Sort Filter & Layout controls matching screenshot */}
              <div className="flex items-center gap-3 text-xs text-neutral-400 self-end sm:self-auto select-none">
                <div className="relative">
                  <select
                    value={sortBy}
                    onChange={(e) => handleSortChange(e.target.value)}
                    className="appearance-none bg-[#121418] border border-[#21242d] rounded-xl px-4 py-2 pr-8 text-neutral-300 hover:text-white outline-none cursor-pointer text-xs font-semibold"
                  >
                    <option value="recentlyadded">Latest First</option>
                    <option value="oldest">Oldest First</option>
                    <option value="title">Title (A-Z)</option>
                    <option value="duration">Longest First</option>
                  </select>
                  <ChevronDown size={14} className="absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none text-neutral-500" />
                </div>

                <div className="p-2 rounded-xl bg-[#121418] border border-[#21242d] text-neutral-300">
                  <Grid size={16} />
                </div>
              </div>
            </div>

            {/* Video Grid */}
            {loading ? (
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
                {Array.from({ length: 8 }).map((_, i) => (
                  <div key={i} className="flex flex-col bg-[#121418] border border-[#21242d] rounded-2xl overflow-hidden animate-pulse aspect-video h-[210px] w-full" />
                ))}
              </div>
            ) : videos.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-neutral-500 gap-3">
                <PlayCircle size={48} className="stroke-[1.2]" />
                <p className="text-sm font-medium">No uploaded videos found.</p>
              </div>
            ) : (
              <>
                <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
                  {videos.map(video => (
                    <VideoCard 
                      key={`profile-upload-${video.id}`} 
                      video={video} 
                      onFavoriteToggled={handleFavoriteToggled}
                    />
                  ))}
                </div>

                {/* Numbered Pagination Bar */}
                {totalPages > 1 && (
                  <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mt-10 pt-6 border-t border-[#21242d] text-xs font-semibold select-none">
                    <span className="text-neutral-400">
                      Showing Page <span className="text-white font-bold">{page}</span> of <span className="text-white font-bold">{totalPages}</span> ({totalCount} total uploads)
                    </span>

                    <div className="flex items-center gap-1.5 flex-wrap justify-center">
                      <button
                        disabled={page <= 1}
                        onClick={() => handlePageChange(page - 1)}
                        className="flex items-center gap-1 px-3 py-2 rounded-xl bg-[#121418] border border-[#21242d] hover:bg-[#171920] text-neutral-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer"
                      >
                        <ChevronLeft size={15} />
                        Prev
                      </button>

                      {/* Page Numbers */}
                      {(() => {
                        const pages: (number | string)[] = [];
                        if (totalPages <= 7) {
                          for (let i = 1; i <= totalPages; i++) pages.push(i);
                        } else if (page <= 3) {
                          pages.push(1, 2, 3, 4, '...', totalPages);
                        } else if (page >= totalPages - 2) {
                          pages.push(1, '...', totalPages - 3, totalPages - 2, totalPages - 1, totalPages);
                        } else {
                          pages.push(1, '...', page - 1, page, page + 1, '...', totalPages);
                        }
                        return pages.map((p, idx) => (
                          p === '...' ? (
                            <span key={`dots-${idx}`} className="px-1 text-neutral-500 font-bold">...</span>
                          ) : (
                            <button
                              key={`page-${p}`}
                              onClick={() => handlePageChange(p as number)}
                              className={`h-8 w-8 rounded-xl font-bold transition-all cursor-pointer flex items-center justify-center border text-xs ${
                                page === p
                                  ? 'bg-[#3cd69b] text-neutral-950 border-[#3cd69b] shadow-md shadow-[#3cd69b]/20'
                                  : 'bg-[#121418] border-[#21242d] text-neutral-300 hover:bg-[#171920] hover:text-white'
                              }`}
                            >
                              {p}
                            </button>
                          )
                        ));
                      })()}

                      <button
                        disabled={page >= totalPages}
                        onClick={() => handlePageChange(page + 1)}
                        className="flex items-center gap-1 px-3 py-2 rounded-xl bg-[#121418] border border-[#21242d] hover:bg-[#171920] text-neutral-300 hover:text-white disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer"
                      >
                        Next
                        <ChevronRight size={15} />
                      </button>
                    </div>
                  </div>
                )}
              </>
            )}
          </section>
        )}
      </div>
    </div>
  );
};
