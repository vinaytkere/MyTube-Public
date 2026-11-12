import React from 'react';
import { Tv, Heart, Play, Layers } from 'lucide-react';
import { Badge } from '@mantine/core';
import { IptvChannel } from '../types';

interface IptvChannelCardProps {
  channel: IptvChannel;
  isSelected?: boolean;
  onSelect: (channel: IptvChannel) => void;
  onToggleFavorite: (channelId: string) => void;
}

export const IptvChannelCard: React.FC<IptvChannelCardProps> = ({
  channel,
  isSelected = false,
  onSelect,
  onToggleFavorite
}) => {
  const streamCount = channel.streams?.length || 0;
  const hasHttpsStream = channel.streams?.some(s => s.url?.toLowerCase().startsWith('https://'));

  return (
    <div
      onClick={() => onSelect(channel)}
      tabIndex={0}
      role="button"
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onSelect(channel);
        }
      }}
      aria-label={`Watch ${channel.name}`}
      className={`group relative flex flex-col justify-between p-3.5 rounded-xl bg-[#121418] border transition-all duration-200 cursor-pointer select-none ${
        isSelected
          ? 'border-[#3cd69b] shadow-[0_0_20px_rgba(60,214,155,0.25)] bg-[#171920]'
          : 'border-[#1a1d24] hover:border-[#3cd69b]/70 hover:shadow-[0_0_16px_rgba(60,214,155,0.15)] hover:bg-[#171920]'
      }`}
    >
      {/* Top Bar: Country Flag, Language & Favorite Button */}
      <div className="flex items-center justify-between gap-2 mb-2.5">
        <div className="flex items-center gap-1.5 min-w-0">
          {channel.countryFlag ? (
            <span className="text-sm shrink-0" title={channel.countryName || channel.countryCode}>
              {channel.countryFlag}
            </span>
          ) : (
            <span className="text-[10px] font-mono-telemetry font-bold text-neutral-400 bg-[#1a1c20] border border-[#21242d] px-1.5 py-0.5 rounded">
              {channel.countryCode || 'INT'}
            </span>
          )}
          {channel.primaryLanguageName ? (
            <span className="text-[10px] font-mono-telemetry text-neutral-400 truncate">
              {channel.primaryLanguageName}
            </span>
          ) : null}
        </div>

        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onToggleFavorite(channel.id);
          }}
          aria-label={channel.isFavorite ? `Remove ${channel.name} from favorites` : `Add ${channel.name} to favorites`}
          className={`p-1 rounded-lg transition-colors shrink-0 cursor-pointer ${
            channel.isFavorite
              ? 'text-rose-400 bg-rose-500/10'
              : 'text-neutral-500 hover:text-white hover:bg-[#1a1c20]'
          }`}
        >
          <Heart size={14} fill={channel.isFavorite ? 'currentColor' : 'none'} />
        </button>
      </div>

      {/* Middle: Logo & Thumbnail */}
      <div className="relative w-full aspect-video rounded-lg bg-[#0c0e12] border border-[#21242d] flex items-center justify-center p-3 overflow-hidden group-hover:border-[#3cd69b]/40 transition-colors">
        {channel.logoUrl ? (
          <img
            src={channel.logoUrl}
            alt=""
            loading="lazy"
            className="w-full h-full object-contain filter drop-shadow transition-transform duration-300 group-hover:scale-105"
            onError={(e) => {
              (e.target as HTMLElement).style.display = 'none';
              const parent = (e.target as HTMLElement).parentElement;
              if (parent) {
                const placeholder = parent.querySelector('.fallback-icon');
                if (placeholder) (placeholder as HTMLElement).style.display = 'flex';
              }
            }}
          />
        ) : null}

        <div className={`fallback-icon flex flex-col items-center justify-center text-neutral-500 ${channel.logoUrl ? 'hidden' : 'flex'}`}>
          <Tv size={26} className="text-[#3cd69b]/60 mb-1" />
          <span className="text-[11px] font-bold text-neutral-400 text-center line-clamp-1 px-2 font-heading">
            {channel.network || channel.name}
          </span>
        </div>

        {/* Hover Play Button Overlay with Neon Glow */}
        <div className="absolute inset-0 bg-black/40 backdrop-blur-xs opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center pointer-events-none">
          <div className="w-10 h-10 rounded-full bg-[#3cd69b] text-neutral-950 flex items-center justify-center shadow-[0_0_16px_rgba(60,214,155,0.4)] transform group-hover:scale-110 transition-transform">
            <Play size={16} fill="currentColor" className="ml-0.5" />
          </div>
        </div>

        {/* Stream Badges */}
        <div className="absolute bottom-2 right-2 flex items-center gap-1 z-10 pointer-events-none">
          {hasHttpsStream ? (
            <span className="px-1.5 py-0.2 rounded font-mono-telemetry text-[9px] font-bold bg-[#3cd69b]/20 text-[#3cd69b] border border-[#3cd69b]/40 backdrop-blur-md">
              HTTPS
            </span>
          ) : (
            <span className="px-1.5 py-0.2 rounded font-mono-telemetry text-[9px] font-medium bg-cyan-950/80 text-cyan-400 border border-cyan-500/30 backdrop-blur-md">
              RELAY
            </span>
          )}

          {streamCount > 1 && (
            <div className="px-1.5 py-0.2 rounded font-mono-telemetry text-[9px] font-medium bg-black/80 text-neutral-300 border border-[#21242d] flex items-center gap-1 backdrop-blur-md">
              <Layers size={9} />
              <span>{streamCount}</span>
            </div>
          )}
        </div>
      </div>

      {/* Bottom: Channel Title & Tags */}
      <div className="mt-2.5 flex flex-col min-w-0">
        <h4 className="text-xs md:text-sm font-bold text-white truncate group-hover:text-[#3cd69b] transition-colors font-heading" title={channel.name}>
          {channel.name}
        </h4>

        <div className="flex items-center gap-1.5 mt-1 text-[10px] font-mono-telemetry text-neutral-400 min-w-0">
          {channel.categories.length > 0 ? (
            <span className="truncate px-1.5 py-0.2 rounded bg-[#1a1c20] text-neutral-300 border border-[#21242d]">
              {channel.categories[0]}
            </span>
          ) : (
            <span className="text-neutral-500 italic">General</span>
          )}

          {channel.streams.length > 0 && channel.streams[0].quality ? (
            <span className="px-1.5 py-0.2 rounded bg-[#1a1c20] text-[#3cd69b] border border-[#21242d]">
              {channel.streams[0].quality}
            </span>
          ) : null}
        </div>
      </div>
    </div>
  );
};
