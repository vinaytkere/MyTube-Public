import React, { useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Play, Star, Folder, Video as VideoIcon, Loader2, Music, Download, MoreVertical, Sparkles } from 'lucide-react';
import { Badge, Menu, Tooltip } from '@mantine/core';
import { Video } from '../types';
import { api } from '../services/api';

interface VideoCardProps {
  video: Video;
  onFavoriteToggled?: (videoId: string, isFav: boolean) => void;
  isSelectionMode?: boolean;
  isSelected?: boolean;
  onSelectToggle?: (videoId: string) => void;
  onLongPress?: (videoId: string) => void;
}

const formatDuration = (secs: number) => {
  if (isNaN(secs) || secs <= 0) return '0:00';
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = Math.floor(secs % 60);

  const sStr = s < 10 ? `0${s}` : `${s}`;
  if (h > 0) {
    const mStr = m < 10 ? `0${m}` : `${m}`;
    return `${h}:${mStr}:${sStr}`;
  }
  return `${m}:${sStr}`;
};

const formatBytes = (bytes: number) => {
  if (bytes <= 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};

const formatDate = (dateStr: string) => {
  try {
    const date = new Date(dateStr);
    return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  } catch {
    return '';
  }
};

export const VideoCard: React.FC<VideoCardProps> = ({ 
  video, 
  onFavoriteToggled,
  isSelectionMode = false,
  isSelected = false,
  onSelectToggle,
  onLongPress
}) => {
  const navigate = useNavigate();
  const timerRef = useRef<number | null>(null);

  const handlePointerDown = (e: React.PointerEvent) => {
    if (e.button !== 0) return;
    if (timerRef.current) clearTimeout(timerRef.current);
    
    timerRef.current = window.setTimeout(() => {
      if (onLongPress) {
        onLongPress(video.id);
      }
    }, 600);
  };

  const handlePointerUp = () => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const handlePointerLeave = () => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const handleCardClick = (e: React.MouseEvent) => {
    if (isSelectionMode) {
      e.preventDefault();
      e.stopPropagation();
      if (onSelectToggle) {
        onSelectToggle(video.id);
      }
    } else {
      if (video.scanStatus === 'Discovered' || video.scanStatus === 'Processing') {
        return;
      }
      navigate(`/video/${video.id}`, { state: { from: window.location.search } });
    }
  };

  const handleFavoriteClick = async (e: React.MouseEvent) => {
    e.stopPropagation();
    if (isSelectionMode) {
      if (onSelectToggle) onSelectToggle(video.id);
      return;
    }
    if (onFavoriteToggled) {
      onFavoriteToggled(video.id, !video.isFavorite);
    }
  };

  const hasProgress = video.progressSeconds !== undefined && video.progressSeconds !== null;
  const progressPercent = hasProgress && video.duration > 0
    ? Math.min(100, Math.round((video.progressSeconds! / video.duration) * 100))
    : 0;

  const isAudio = video.fileName.toLowerCase().endsWith('.mp3') || video.fileName.toLowerCase().endsWith('.m4a');
  const is4K = video.quality?.includes('2160') || video.quality?.includes('4K') || video.width >= 3840;

  return (
    <div 
      onClick={handleCardClick}
      onPointerDown={handlePointerDown}
      onPointerUp={handlePointerUp}
      onPointerLeave={handlePointerLeave}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          handleCardClick(e as any);
        }
      }}
      role="article"
      tabIndex={0}
      aria-label={`Video: ${video.displayTitle}`}
      className={`group flex flex-col bg-[#121418] border rounded-xl overflow-hidden cursor-pointer shadow-lg hover:shadow-2xl transition-all duration-300 transform hover:-translate-y-1 select-none ${
        isSelected 
          ? 'border-[#3cd69b] ring-1 ring-[#3cd69b] bg-[#171920]' 
          : 'border-[#1a1d24] hover:border-[#3cd69b]/70 hover:shadow-[0_0_18px_rgba(60,214,155,0.15)]'
      }`}
    >
      {/* Thumbnail Section */}
      <div className="relative aspect-video w-full bg-[#0c0e12] overflow-hidden select-none">
        {!isAudio && video.thumbnailUrl && (video.scanStatus === 'Ready' || video.scanStatus === 'Failed') ? (
          <img
            src={video.thumbnailUrl}
            alt={video.displayTitle}
            className="w-full h-full object-cover group-hover:scale-[1.04] transition-transform duration-500 ease-out brightness-95 group-hover:brightness-105"
            loading="lazy"
          />
        ) : (
          <div className="flex w-full h-full items-center justify-center bg-gradient-to-br from-[#121418] via-[#171920] to-[#0c0e12] text-[#3cd69b]/70">
            {isAudio ? <Music size={42} className="stroke-[1.5]" /> : <VideoIcon size={42} className="stroke-[1.5]" />}
          </div>
        )}

        {/* Scrim Overlay */}
        <div className="absolute inset-0 bg-gradient-to-t from-[#0c0e12]/80 via-transparent to-transparent pointer-events-none"></div>

        {/* Checkbox for Selection Mode */}
        <div className={`absolute top-2 left-2 z-30 transition-opacity duration-200 ${
          isSelectionMode ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
        }`}>
          <input
            type="checkbox"
            checked={isSelected}
            aria-label={`Select ${video.displayTitle}`}
            onChange={(e) => {
              e.stopPropagation();
              if (isSelectionMode) {
                if (onSelectToggle) onSelectToggle(video.id);
              } else {
                if (onLongPress) onLongPress(video.id);
              }
            }}
            onClick={(e) => e.stopPropagation()}
            className="h-4 w-4 rounded border-neutral-700 text-[#3cd69b] bg-[#121418] focus:ring-[#3cd69b] cursor-pointer shadow-md"
          />
        </div>

        {/* Processing / Transcode Pipeline Overlay */}
        {(video.scanStatus === 'Discovered' || video.scanStatus === 'Processing') && (
          <div className="absolute inset-0 bg-black/85 flex flex-col items-center justify-center gap-2 text-neutral-300 z-20">
            <Loader2 className="animate-spin text-[#3cd69b]" size={24} />
            <span className="font-mono-telemetry text-xs font-semibold text-[#3cd69b]">
              {video.scanStatus === 'Discovered' ? 'INGESTING STREAM...' : 'FFMPEG REMUXING...'}
            </span>
          </div>
        )}

        {/* Floating Top HUD Badges (Stitch Design) */}
        <div className="absolute top-2 right-2 z-20 flex items-center gap-1.5">
          {video.scanStatus === 'Ready' && (
            <span className="flex items-center gap-1 px-1.5 py-0.5 rounded font-mono-telemetry text-[10px] bg-[#0c0e12]/90 backdrop-blur-md text-[#3cd69b] border border-[#21242d] shadow-sm">
              <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b]"></span>
              {is4K ? '4K Remux' : (video.quality || '1080p')}
            </span>
          )}

          {video.scanStatus === 'Failed' && (
            <Badge size="xs" color="red" variant="filled">
              FAILED
            </Badge>
          )}
        </div>

        {/* Center Hover Play Button Bloom */}
        {video.scanStatus !== 'Discovered' && video.scanStatus !== 'Processing' && !isSelectionMode && (
          <div className="absolute inset-0 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity duration-300 pointer-events-none">
            <div className="w-11 h-11 rounded-full bg-[#3cd69b] text-neutral-950 flex items-center justify-center shadow-[0_0_20px_rgba(60,214,155,0.45)] transform scale-90 group-hover:scale-105 transition-transform duration-300">
              <Play size={18} className="fill-neutral-950 ml-0.5" />
            </div>
          </div>
        )}

        {/* Bottom Duration Badge */}
        {video.scanStatus !== 'Discovered' && video.scanStatus !== 'Processing' && video.duration > 0 && (
          <span className="absolute bottom-2 right-2 bg-[#0c0e12]/90 backdrop-blur-md text-[10px] font-semibold text-neutral-200 px-1.5 py-0.5 rounded border border-[#21242d] font-mono-telemetry shadow-sm">
            {formatDuration(video.duration)}
          </span>
        )}

        {/* Bottom Playback Progress Line (Stitch DESIGN.md) */}
        {hasProgress && progressPercent > 0 && (
          <div className="absolute bottom-0 left-0 right-0 h-[3px] bg-[#21242d] select-none">
            <div 
              className={`h-full ${video.completed ? 'bg-neutral-500' : 'bg-[#3cd69b] shadow-[0_0_8px_#3cd69b]'}`} 
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        )}
      </div>

      {/* Details & Telemetry Section */}
      <div className="flex flex-col p-3 flex-1 justify-between gap-2">
        <div>
          <h3 className="text-xs md:text-sm font-bold text-neutral-100 font-heading line-clamp-2 leading-snug group-hover:text-[#3cd69b] transition-colors">
            {video.displayTitle}
          </h3>
          
          <div className="flex items-center gap-1.5 text-neutral-400 text-xs mt-1">
            <Folder size={11} className="text-[#3cd69b]/80 shrink-0" />
            <span className="truncate text-[11px]">{video.categoryName}</span>
          </div>
        </div>

        {/* Codecs & File Telemetry Strip */}
        <div className="flex items-center justify-between pt-1.5 border-t border-[#1a1d24] text-[10px] font-mono-telemetry text-neutral-500">
          <div className="flex items-center gap-1.5">
            <span className="px-1 py-0.2 rounded bg-[#1a1c20] text-cyan-400 border border-[#21242d]">
              {video.videoCodec || 'HEVC'}
            </span>
            <span>{formatBytes(video.fileSize)}</span>
          </div>

          <div className="flex items-center gap-1 shrink-0">
            <button
              onClick={handleFavoriteClick}
              className={`p-1 rounded-md hover:bg-[#1a1c20] transition-colors cursor-pointer ${
                video.isFavorite ? 'text-amber-400 fill-amber-400' : 'text-neutral-500 hover:text-amber-400'
              }`}
              title={video.isFavorite ? 'Remove Favorite' : 'Add Favorite'}
            >
              <Star size={13} className={video.isFavorite ? 'fill-amber-400' : ''} />
            </button>

            <button
              onClick={(e) => {
                e.stopPropagation();
                api.downloadVideoFile(video);
              }}
              className="p-1 rounded-md text-neutral-500 hover:text-[#3cd69b] hover:bg-[#1a1c20] transition-colors cursor-pointer"
              title="Download File"
            >
              <Download size={13} />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
