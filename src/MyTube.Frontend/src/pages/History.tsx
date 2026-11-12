import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Clock, Video as VideoIcon, Music, RefreshCw, PlayCircle, 
  Trash2, Filter, Sparkles, CheckCircle2, ChevronRight, Heart
} from 'lucide-react';
import { api } from '../services/api';
import { Video, User } from '../types';
import { VideoCard } from '../components/VideoCard';
import { useToast } from '../components/Toast';

interface HistoryProps {
  currentUser?: User | null;
}

export const History: React.FC<HistoryProps> = ({ currentUser }) => {
  const [historyItems, setHistoryItems] = useState<Video[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterType, setFilterType] = useState<'all' | 'video' | 'audio'>('all');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const navigate = useNavigate();
  const { showToast } = useToast();

  const isAudioFile = (fileName: string) => {
    const lower = fileName.toLowerCase();
    return lower.endsWith('.mp3') || lower.endsWith('.m4a') || lower.endsWith('.opus') || lower.endsWith('.wav') || lower.endsWith('.flac');
  };

  const loadHistory = async (showLoadingState = true) => {
    if (showLoadingState) setLoading(true);
    setIsRefreshing(true);
    try {
      const items = await api.getContinueWatching();
      setHistoryItems(items);
    } catch (err: any) {
      console.error('Failed to load history:', err);
      showToast('error', 'Failed to load watch history');
    } finally {
      setLoading(false);
      setIsRefreshing(false);
    }
  };

  useEffect(() => {
    loadHistory(true);
  }, []);

  const handleFavoriteToggled = (videoId: string, isFav: boolean) => {
    setHistoryItems(prev => prev.map(v => v.id === videoId ? { ...v, isFavorite: isFav } : v));
  };

  const filteredItems = historyItems.filter(item => {
    const isAudio = isAudioFile(item.fileName);
    if (filterType === 'video') return !isAudio;
    if (filterType === 'audio') return isAudio;
    return true;
  });

  const videoCount = historyItems.filter(item => !isAudioFile(item.fileName)).length;
  const audioCount = historyItems.filter(item => isAudioFile(item.fileName)).length;

  return (
    <div className="flex-1 p-6 overflow-y-auto text-neutral-100 bg-[#090a0c]">
      <div className="max-w-7xl mx-auto space-y-6">
        
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-[#21242d]">
          <div className="flex items-center gap-3">
            <div className="p-3 rounded-2xl bg-emerald-500/10 border border-emerald-500/20 text-[#3cd69b]">
              <Clock size={24} />
            </div>
            <div>
              <h1 className="text-xl font-bold font-heading text-neutral-100 flex items-center gap-2">
                Continue Watching & History
                <span className="text-xs bg-[#171920] border border-[#21242d] text-neutral-400 font-normal px-2.5 py-0.5 rounded-full">
                  {historyItems.length} in progress
                </span>
              </h1>
              <p className="text-xs text-neutral-400">
                Pick up right where you left off across all your videos and audio tracks
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => loadHistory(false)}
              disabled={isRefreshing}
              className="flex items-center gap-2 px-3.5 py-2 rounded-xl bg-[#121418] hover:bg-[#1a1d24] border border-[#21242d] text-xs font-semibold text-neutral-300 hover:text-white transition-colors cursor-pointer disabled:opacity-50"
              title="Refresh Watch History"
            >
              <RefreshCw size={14} className={isRefreshing ? 'animate-spin text-[#3cd69b]' : ''} />
              <span>Refresh</span>
            </button>
          </div>
        </div>

        {/* Filter Tabs */}
        <div className="flex items-center gap-2 border-b border-[#1a1d24] pb-2">
          <button
            onClick={() => setFilterType('all')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
              filterType === 'all'
                ? 'bg-[#3cd69b] text-neutral-950 shadow-md shadow-emerald-950/20'
                : 'bg-[#121418] text-neutral-400 hover:text-neutral-200 border border-[#21242d]'
            }`}
          >
            <span>All History</span>
            <span className={`text-[10px] px-1.5 py-0.5 rounded-full ${filterType === 'all' ? 'bg-neutral-950/20 text-neutral-950 font-bold' : 'bg-[#171920] text-neutral-500'}`}>
              {historyItems.length}
            </span>
          </button>

          <button
            onClick={() => setFilterType('video')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
              filterType === 'video'
                ? 'bg-[#3cd69b] text-neutral-950 shadow-md shadow-emerald-950/20'
                : 'bg-[#121418] text-neutral-400 hover:text-neutral-200 border border-[#21242d]'
            }`}
          >
            <VideoIcon size={14} />
            <span>Videos</span>
            <span className={`text-[10px] px-1.5 py-0.5 rounded-full ${filterType === 'video' ? 'bg-neutral-950/20 text-neutral-950 font-bold' : 'bg-[#171920] text-neutral-500'}`}>
              {videoCount}
            </span>
          </button>

          <button
            onClick={() => setFilterType('audio')}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
              filterType === 'audio'
                ? 'bg-[#3cd69b] text-neutral-950 shadow-md shadow-emerald-950/20'
                : 'bg-[#121418] text-neutral-400 hover:text-neutral-200 border border-[#21242d]'
            }`}
          >
            <Music size={14} />
            <span>Audio Tracks</span>
            <span className={`text-[10px] px-1.5 py-0.5 rounded-full ${filterType === 'audio' ? 'bg-neutral-950/20 text-neutral-950 font-bold' : 'bg-[#171920] text-neutral-500'}`}>
              {audioCount}
            </span>
          </button>
        </div>

        {/* Content Body */}
        {loading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <div 
                key={i} 
                className="flex flex-col bg-[#121418] border border-[#21242d] rounded-2xl overflow-hidden animate-pulse aspect-video h-[220px] w-full" 
              />
            ))}
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-neutral-500 gap-4 bg-[#121418]/40 border border-[#21242d] rounded-2xl p-8 text-center">
            <div className="p-4 rounded-full bg-[#171920] border border-[#21242d] text-neutral-400">
              <Clock size={36} className="text-[#3cd69b]" />
            </div>
            <div>
              <h3 className="text-base font-bold text-neutral-200">
                {filterType === 'all' 
                  ? 'No Watch History Found' 
                  : filterType === 'video' 
                    ? 'No Video Watch History' 
                    : 'No Audio Listening History'}
              </h3>
              <p className="text-xs text-neutral-400 max-w-sm mt-1">
                When you play videos or audio tracks, your progress is automatically saved so you can resume anytime.
              </p>
            </div>
            <div className="flex items-center gap-3 pt-2">
              <button
                onClick={() => navigate('/')}
                className="px-4 py-2 bg-[#3cd69b] hover:bg-[#34b784] text-neutral-950 rounded-xl text-xs font-bold transition-colors cursor-pointer"
              >
                Browse Videos
              </button>
              <button
                onClick={() => navigate('/audio')}
                className="px-4 py-2 bg-[#171920] hover:bg-[#1f222b] text-neutral-300 border border-[#21242d] rounded-xl text-xs font-medium transition-colors cursor-pointer"
              >
                Browse Audio
              </button>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4">
            {filteredItems.map(video => (
              <VideoCard 
                key={`history-${video.id}`} 
                video={video} 
                onFavoriteToggled={handleFavoriteToggled}
              />
            ))}
          </div>
        )}

      </div>
    </div>
  );
};
