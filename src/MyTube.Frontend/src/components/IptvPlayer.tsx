import React, { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import Hls from 'hls.js';
import { 
  Play, Pause, Volume2, VolumeX, Maximize, Minimize, AlertCircle, 
  RotateCcw, Tv, Heart, CheckCircle2, ChevronDown, Copy, Check, ExternalLink 
} from 'lucide-react';
import { IptvChannel, IptvStream } from '../types';
import { api, API_BASE_URL } from '../services/api';

interface IptvPlayerProps {
  channel: IptvChannel;
  initialStreamIndex?: number;
  onClose?: () => void;
  onFavoriteToggle?: (channelId: string) => void;
}

export const IptvPlayer: React.FC<IptvPlayerProps> = ({
  channel,
  initialStreamIndex = 0,
  onClose,
  onFavoriteToggle
}) => {
  const isPageHttps = typeof window !== 'undefined' && window.location.protocol === 'https:';
  const videoRef = useRef<HTMLVideoElement>(null);
  const hlsRef = useRef<Hls | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [currentStreamIndex, setCurrentStreamIndex] = useState(initialStreamIndex);
  const [isPlaying, setIsPlaying] = useState(true);
  const [isMuted, setIsMuted] = useState(false);
  const [volume, setVolume] = useState(1);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [fallbackNotice, setFallbackNotice] = useState<string | null>(null);
  const [isFavorite, setIsFavorite] = useState(channel.isFavorite);
  const [showStreamMenu, setShowStreamMenu] = useState(false);
  const [copied, setCopied] = useState(false);

  const rawStreams = channel.streams.length > 0 
    ? channel.streams 
    : [{ id: 0, url: '', status: 'Unknown', orderIndex: 0 }];

  // When on HTTPS, prioritize HTTPS streams first to avoid browser mixed-content blocks
  const streams = useMemo(() => {
    if (!isPageHttps || rawStreams.length <= 1) return rawStreams;
    return [...rawStreams].sort((a, b) => {
      const aIsHttps = a.url?.toLowerCase().startsWith('https://') ? 1 : 0;
      const bIsHttps = b.url?.toLowerCase().startsWith('https://') ? 1 : 0;
      return bIsHttps - aIsHttps;
    });
  }, [rawStreams, isPageHttps]);

  const currentStream = streams[currentStreamIndex] || streams[0];

  const handleCopyStreamUrl = async (e?: React.MouseEvent) => {
    e?.stopPropagation();
    if (!currentStream?.url) return;
    try {
      await navigator.clipboard.writeText(currentStream.url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      // Ignore clipboard write failures
    }
  };

  // Record watch history when channel starts playing
  useEffect(() => {
    if (channel.id && currentStream?.id) {
      api.recordIptvWatch(channel.id, currentStream.id).catch(err => {
        console.error('Failed to record IPTV watch history:', err);
      });
    }
  }, [channel.id, currentStream?.id]);

  // Attempt automatic fallback to next stream
  const triggerFallback = useCallback((reason: string) => {
    const isMixedContent = isPageHttps && currentStream?.url?.toLowerCase().startsWith('http://');
    const adjustedReason = isMixedContent 
      ? 'Insecure HTTP stream blocked on HTTPS site' 
      : reason;

    const nextIndex = currentStreamIndex + 1;
    if (nextIndex < streams.length) {
      const nextStream = streams[nextIndex];
      const fallbackMsg = `Stream ${currentStreamIndex + 1} unavailable (${adjustedReason}). Switched to Alternative Stream ${nextIndex + 1}${nextStream.quality ? ` (${nextStream.quality})` : ''}.`;
      setFallbackNotice(fallbackMsg);
      setErrorMessage(null);
      setIsLoading(true);

      // Report bad stream health asynchronously
      if (currentStream?.id) {
        api.reportIptvStreamHealth(currentStream.id, false, adjustedReason).catch(() => {});
      }

      setCurrentStreamIndex(nextIndex);
    } else {
      if (isMixedContent) {
        setErrorMessage(
          `This stream uses unencrypted HTTP (${currentStream?.url}), which modern browsers block on HTTPS websites due to Mixed Content policies. You can copy the stream link to watch in VLC or MPV, or select a channel with secure (HTTPS) broadcast feeds.`
        );
      } else {
        setErrorMessage(
          `All ${streams.length} stream sources for this channel are currently unreachable, offline, or CORS-restricted by the broadcaster.`
        );
      }
      setIsLoading(false);
      if (currentStream?.id) {
        api.reportIptvStreamHealth(currentStream.id, false, adjustedReason).catch(() => {});
      }
    }
  }, [currentStreamIndex, streams, currentStream?.id, currentStream?.url, isPageHttps]);

  // Initialize or re-attach HLS playback when current stream changes
  useEffect(() => {
    const video = videoRef.current;
    if (!video || !currentStream?.url) return;

    setIsLoading(true);
    setErrorMessage(null);

    // Clean up prior HLS instance
    if (hlsRef.current) {
      hlsRef.current.destroy();
      hlsRef.current = null;
    }

    const isStreamHttp = currentStream.url.toLowerCase().startsWith('http://');

    // When visiting on HTTPS, route unencrypted HTTP streams through our secure backend relay
    // to bypass browser mixed-content blocks seamlessly!
    let streamUrl = currentStream.url;
    if (isPageHttps && isStreamHttp) {
      const relayBase = API_BASE_URL || (typeof window !== 'undefined' ? window.location.origin : '');
      const userAgentParam = currentStream.userAgent ? `&userAgent=${encodeURIComponent(currentStream.userAgent)}` : '';
      const referrerParam = currentStream.referrer ? `&referrer=${encodeURIComponent(currentStream.referrer)}` : '';
      streamUrl = `${relayBase}/api/iptv/stream-relay?url=${encodeURIComponent(currentStream.url)}${userAgentParam}${referrerParam}`;
    }

    if (Hls.isSupported()) {
      const hls = new Hls({
        enableWorker: true,
        lowLatencyMode: true,
        backBufferLength: 60,
        manifestLoadingTimeOut: 10000,
        manifestLoadingMaxRetry: 2,
        levelLoadingTimeOut: 10000,
        fragLoadingTimeOut: 15000,
      });
      hlsRef.current = hls;

      hls.loadSource(streamUrl);
      hls.attachMedia(video);

      hls.on(Hls.Events.MANIFEST_PARSED, () => {
        setIsLoading(false);
        video.play().then(() => setIsPlaying(true)).catch(() => {
          // Browser autoplay policy might require user gesture or muted
          video.muted = true;
          setIsMuted(true);
          video.play().then(() => setIsPlaying(true)).catch(() => {});
        });
      });

      let retryCount = 0;
      hls.on(Hls.Events.ERROR, (_event, data) => {
        if (!data.fatal) return;

        const statusCode = data.response?.code;
        const isForbiddenOrCors = statusCode === 403 || 
                                  statusCode === 401 || 
                                  (data.type === Hls.ErrorTypes.NETWORK_ERROR && data.details === 'manifestLoadError');

        if (!isForbiddenOrCors && data.type === Hls.ErrorTypes.NETWORK_ERROR && retryCount < 1) {
          retryCount++;
          hls.startLoad();
        } else if (!isForbiddenOrCors && data.type === Hls.ErrorTypes.MEDIA_ERROR && retryCount < 1) {
          retryCount++;
          hls.recoverMediaError();
        } else {
          // Fatal unrecoverable error -> trigger automatic fallback stream
          const reason = statusCode === 403 
            ? 'Broadcaster CDN returned HTTP 403 Forbidden'
            : statusCode === 401
            ? 'Stream requires authorization token'
            : data.details === 'manifestLoadError'
            ? 'CORS restricted or stream unreachable'
            : (data.details || 'Network playback error');

          triggerFallback(reason);
        }
      });
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      // Native Apple Safari HLS playback
      video.src = streamUrl;
      video.addEventListener('loadedmetadata', () => {
        setIsLoading(false);
        video.play().then(() => setIsPlaying(true)).catch(() => {});
      }, { once: true });

      const handleError = () => {
        triggerFallback('Native playback error');
      };
      video.addEventListener('error', handleError, { once: true });
    } else {
      setErrorMessage('Your browser does not support HLS stream playback.');
      setIsLoading(false);
    }

    return () => {
      if (hlsRef.current) {
        hlsRef.current.destroy();
        hlsRef.current = null;
      }
    };
  }, [currentStream?.url, triggerFallback]);

  // Video playback controls
  const togglePlay = () => {
    const video = videoRef.current;
    if (!video) return;

    if (video.paused) {
      video.play().then(() => setIsPlaying(true)).catch(() => {});
    } else {
      video.pause();
      setIsPlaying(false);
    }
  };

  const toggleMute = () => {
    const video = videoRef.current;
    if (!video) return;

    video.muted = !video.muted;
    setIsMuted(video.muted);
  };

  const handleVolumeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = parseFloat(e.target.value);
    setVolume(val);
    const video = videoRef.current;
    if (video) {
      video.volume = val;
      video.muted = val === 0;
      setIsMuted(val === 0);
    }
  };

  const toggleFullscreen = async () => {
    const container = containerRef.current;
    const video = videoRef.current;
    if (!container) return;

    const isCurrentlyFullscreen = !!(document.fullscreenElement || (document as any).webkitFullscreenElement);

    if (!isCurrentlyFullscreen) {
      try {
        if (container.requestFullscreen) {
          await container.requestFullscreen();
        } else if ((container as any).webkitRequestFullscreen) {
          await (container as any).webkitRequestFullscreen();
        } else if (video && (video as any).webkitEnterFullscreen) {
          (video as any).webkitEnterFullscreen();
          return;
        }
        setIsFullscreen(true);
        // Lock to landscape in mobile if supported
        if (screen.orientation && typeof (screen.orientation as any).lock === 'function') {
          (screen.orientation as any).lock('landscape').catch(() => {});
        }
      } catch {
        if (video && (video as any).webkitEnterFullscreen) {
          (video as any).webkitEnterFullscreen();
        }
      }
    } else {
      try {
        if (document.exitFullscreen) {
          await document.exitFullscreen();
        } else if ((document as any).webkitExitFullscreen) {
          await (document as any).webkitExitFullscreen();
        }
        if (screen.orientation && typeof (screen.orientation as any).unlock === 'function') {
          (screen.orientation as any).unlock().catch(() => {});
        }
      } catch {}
      setIsFullscreen(false);
    }
  };

  // Synchronize fullscreen state with browser/OS events
  useEffect(() => {
    const handleFullscreenChange = () => {
      const isFs = !!(document.fullscreenElement || (document as any).webkitFullscreenElement);
      setIsFullscreen(isFs);
      if (!isFs && screen.orientation && typeof (screen.orientation as any).unlock === 'function') {
        (screen.orientation as any).unlock().catch(() => {});
      }
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
    };
  }, []);

  const handleToggleFavorite = async () => {
    try {
      const res = await api.toggleIptvFavorite(channel.id);
      setIsFavorite(res.isFavorite);
      if (onFavoriteToggle) {
        onFavoriteToggle(channel.id);
      }
    } catch (err) {
      console.error('Failed to toggle IPTV favorite:', err);
    }
  };

  const handleManualStreamSelect = (index: number) => {
    setShowStreamMenu(false);
    if (index !== currentStreamIndex) {
      setFallbackNotice(null);
      setCurrentStreamIndex(index);
    }
  };

  // Keyboard shortcut listener
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;

      if (e.code === 'Space') {
        e.preventDefault();
        togglePlay();
      } else if (e.code === 'KeyM') {
        e.preventDefault();
        toggleMute();
      } else if (e.code === 'KeyF') {
        e.preventDefault();
        toggleFullscreen();
      } else if (e.code === 'Escape' && onClose && !document.fullscreenElement) {
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  });

  return (
    <div 
      ref={containerRef}
      className={`relative flex flex-col w-full bg-black overflow-hidden shadow-2xl group select-none ${
        isFullscreen 
          ? 'fixed inset-0 z-50 w-screen h-screen rounded-none border-0' 
          : 'rounded-2xl border border-[#1a1d24]'
      }`}
    >
      {/* Top Overlay Header */}
      <div className="absolute top-0 left-0 right-0 z-30 flex items-center justify-between p-2.5 sm:p-4 bg-gradient-to-b from-black/85 via-black/50 to-transparent pointer-events-auto transition-opacity duration-300 opacity-90 group-hover:opacity-100 gap-2">
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          {channel.logoUrl ? (
            <img 
              src={channel.logoUrl} 
              alt={`${channel.name} logo`}
              className="w-8 h-8 sm:w-10 sm:h-10 object-contain bg-white/10 rounded-lg p-1 border border-white/10 flex-shrink-0"
              onError={(e) => { (e.target as HTMLElement).style.display = 'none'; }}
            />
          ) : (
            <div className="w-8 h-8 sm:w-10 sm:h-10 rounded-lg bg-[#171920] border border-[#2a2e39] flex items-center justify-center flex-shrink-0 text-[#3cd69b]">
              <Tv size={18} aria-hidden="true" />
            </div>
          )}

          <div className="flex flex-col min-w-0 flex-1">
            <div className="flex items-center gap-1.5 sm:gap-2 flex-wrap">
              <span className="font-bold text-xs sm:text-base text-white truncate max-w-[130px] xs:max-w-[180px] sm:max-w-xs md:max-w-md">
                {channel.name}
              </span>
              {channel.countryFlag ? (
                <span className="text-xs sm:text-sm flex-shrink-0" title={channel.countryName || channel.countryCode}>
                  {channel.countryFlag}
                </span>
              ) : null}
              <span className="px-1.5 py-0.5 rounded-full text-[9px] sm:text-[10px] font-semibold bg-red-600/80 text-white tracking-wider flex items-center gap-1 flex-shrink-0">
                <span className="w-1.5 h-1.5 rounded-full bg-white animate-pulse" />
                LIVE
              </span>
            </div>

            <div className="hidden xs:flex items-center gap-1.5 text-[10px] sm:text-xs text-neutral-400 truncate">
              {channel.countryName ? <span>{channel.countryName}</span> : null}
              {channel.primaryLanguageName ? <span>• {channel.primaryLanguageName}</span> : null}
              {channel.categories.length > 0 ? <span>• {channel.categories.join(', ')}</span> : null}
            </div>
          </div>
        </div>

        {/* Top Right Actions */}
        <div className="flex items-center gap-1.5 sm:gap-2 flex-shrink-0">
          {/* Copy Stream URL Button (for VLC / MPV) */}
          <button
            type="button"
            onClick={handleCopyStreamUrl}
            aria-label={copied ? 'Stream URL copied' : 'Copy stream URL for VLC or MPV'}
            title={copied ? 'Copied to clipboard!' : 'Copy stream URL for VLC or MPV'}
            className={`flex items-center gap-1.5 p-2 sm:px-2.5 sm:py-2 rounded-xl border text-xs font-medium transition-all ${
              copied
                ? 'bg-[#3cd69b]/20 border-[#3cd69b]/50 text-[#3cd69b]'
                : 'bg-black/40 border-white/10 text-neutral-300 hover:bg-white/10 hover:text-white'
            }`}
          >
            {copied ? <Check size={15} aria-hidden="true" /> : <Copy size={15} aria-hidden="true" />}
            <span className="hidden sm:inline">{copied ? 'Copied!' : 'Copy Stream'}</span>
          </button>

          {/* Favorite Toggle Button */}
          <button
            type="button"
            onClick={handleToggleFavorite}
            aria-label={isFavorite ? 'Remove from favorites' : 'Add to favorites'}
            className={`p-2 rounded-xl border transition-colors ${
              isFavorite 
                ? 'bg-red-500/20 border-red-500/40 text-red-400 hover:bg-red-500/30' 
                : 'bg-black/40 border-white/10 text-neutral-300 hover:bg-white/10 hover:text-white'
            }`}
          >
            <Heart size={16} fill={isFavorite ? 'currentColor' : 'none'} aria-hidden="true" />
          </button>

          {/* Close Player Modal Button (if provided) */}
          {onClose ? (
            <button
              type="button"
              onClick={onClose}
              aria-label="Close player"
              className="p-2 rounded-xl bg-black/40 border border-white/10 text-neutral-300 hover:bg-white/10 hover:text-white transition-colors"
            >
              ✕
            </button>
          ) : null}
        </div>
      </div>

      {/* Video Viewport */}
      <div 
        className={`relative w-full bg-neutral-950 flex items-center justify-center cursor-pointer ${
          isFullscreen ? 'flex-1 h-full' : 'aspect-video'
        }`}
        onClick={togglePlay}
      >
        <video
          ref={videoRef}
          className="w-full h-full object-contain"
          playsInline
        />

        {/* Loading Spinner */}
        {isLoading && !errorMessage ? (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/60 backdrop-blur-xs z-10 pointer-events-none">
            <div className="w-12 h-12 rounded-full border-3 border-[#3cd69b]/20 border-t-[#3cd69b] animate-spin mb-3" />
            <span className="text-sm text-neutral-200 font-medium">Connecting live broadcast...</span>
            <span className="text-xs text-neutral-400 mt-1">Stream source {currentStreamIndex + 1} of {streams.length}</span>
          </div>
        ) : null}

        {/* Fallback Banner Alert */}
        {fallbackNotice ? (
          <div className="absolute top-20 left-4 right-4 z-20 flex items-center gap-2 px-4 py-2.5 rounded-xl bg-amber-500/90 text-neutral-950 font-medium text-xs shadow-lg animate-in fade-in duration-300">
            <AlertCircle size={16} className="flex-shrink-0" aria-hidden="true" />
            <span className="flex-1">{fallbackNotice}</span>
            <button
              type="button"
              onClick={(e) => { e.stopPropagation(); setFallbackNotice(null); }}
              className="p-1 hover:bg-black/10 rounded text-xs font-bold"
              aria-label="Dismiss notice"
            >
              ✕
            </button>
          </div>
        ) : null}

        {/* Error Overlay */}
        {errorMessage ? (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-neutral-950/95 p-6 text-center z-20 pointer-events-auto">
            <AlertCircle size={44} className="text-red-400 mb-3" aria-hidden="true" />
            <h3 className="text-base font-semibold text-white mb-1">Playback Interrupted</h3>
            <p className="text-xs text-neutral-300 max-w-md mb-4 leading-relaxed">{errorMessage}</p>

            {/* Quick Actions: Copy Stream Link or Open Directly */}
            <div className="flex flex-wrap items-center justify-center gap-2 mb-4">
              <button
                type="button"
                onClick={handleCopyStreamUrl}
                className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-white text-xs font-medium transition-colors border border-neutral-700"
              >
                {copied ? <Check size={14} className="text-[#3cd69b]" aria-hidden="true" /> : <Copy size={14} aria-hidden="true" />}
                <span>{copied ? 'Copied to Clipboard!' : 'Copy Stream Link for VLC / MPV'}</span>
              </button>

              {currentStream?.url ? (
                <a
                  href={currentStream.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-white text-xs font-medium transition-colors border border-neutral-700"
                  onClick={(e) => e.stopPropagation()}
                >
                  <ExternalLink size={14} aria-hidden="true" />
                  <span>Open URL Directly</span>
                </a>
              ) : null}
            </div>

            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setCurrentStreamIndex(0);
                  setIsLoading(true);
                  setErrorMessage(null);
                }}
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-[#3cd69b] hover:bg-[#34b885] text-neutral-950 font-bold text-xs transition-colors"
              >
                <RotateCcw size={14} aria-hidden="true" />
                Retry from Stream 1
              </button>

              {streams.length > 1 ? (
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    const next = (currentStreamIndex + 1) % streams.length;
                    setCurrentStreamIndex(next);
                  }}
                  className="px-4 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-white text-xs font-medium transition-colors border border-neutral-700"
                >
                  Try Stream {((currentStreamIndex + 1) % streams.length) + 1}
                </button>
              ) : null}
            </div>
          </div>
        ) : null}
      </div>

      {/* Bottom Control Bar */}
      <div className="flex items-center justify-between px-3 py-2 sm:px-4 sm:py-3 bg-[#0d0f13] border-t border-[#1a1d24] z-30">
        {/* Left: Play/Pause & Volume */}
        <div className="flex items-center gap-2 sm:gap-3">
          <button
            type="button"
            onClick={togglePlay}
            aria-label={isPlaying ? 'Pause' : 'Play'}
            className="p-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-white transition-colors focus-visible:ring-2 focus-visible:ring-[#3cd69b]"
          >
            {isPlaying ? <Pause size={17} aria-hidden="true" /> : <Play size={17} aria-hidden="true" />}
          </button>

          <div className="hidden sm:flex items-center gap-2 group/vol">
            <button
              type="button"
              onClick={toggleMute}
              aria-label={isMuted ? 'Unmute' : 'Mute'}
              className="p-1.5 text-neutral-400 hover:text-white transition-colors focus-visible:ring-2 focus-visible:ring-[#3cd69b]"
            >
              {isMuted || volume === 0 ? <VolumeX size={18} aria-hidden="true" /> : <Volume2 size={18} aria-hidden="true" />}
            </button>
            <input
              type="range"
              min="0"
              max="1"
              step="0.05"
              value={isMuted ? 0 : volume}
              onChange={handleVolumeChange}
              aria-label="Volume slider"
              className="w-16 md:w-24 h-1.5 bg-neutral-700 rounded-lg appearance-none cursor-pointer accent-[#3cd69b]"
            />
          </div>
        </div>

        {/* Right: Multi-Stream Selector & Fullscreen */}
        <div className="flex items-center gap-1.5 sm:gap-2">
          {/* Multiple Stream Switcher Menu */}
          {streams.length > 1 ? (
            <div className="relative">
              <button
                type="button"
                onClick={() => setShowStreamMenu(prev => !prev)}
                aria-expanded={showStreamMenu}
                aria-label="Select alternative stream feed"
                className="flex items-center gap-1 px-2 py-1.5 sm:px-3 sm:py-1.5 rounded-lg bg-neutral-800/80 hover:bg-neutral-700 text-[11px] sm:text-xs text-neutral-300 hover:text-white border border-neutral-700 transition-colors"
              >
                <span>Stream {currentStreamIndex + 1}/{streams.length}</span>
                {currentStream?.quality ? (
                  <span className="hidden xs:inline px-1.5 py-0.5 rounded text-[10px] bg-[#3cd69b]/20 text-[#3cd69b] font-semibold">
                    {currentStream.quality}
                  </span>
                ) : null}
                <ChevronDown size={13} aria-hidden="true" />
              </button>

              {showStreamMenu ? (
                <div 
                  className="absolute bottom-full right-0 mb-2 w-56 sm:w-64 max-h-60 overflow-y-auto rounded-xl bg-[#14171e] border border-[#232733] shadow-2xl p-2 z-50 flex flex-col gap-1"
                  role="menu"
                >
                  <span className="px-2.5 py-1 text-[10px] sm:text-[11px] font-bold text-neutral-400 uppercase tracking-wider">
                    Available Stream Feeds
                  </span>
                  {streams.map((s, idx) => {
                    const isSelected = idx === currentStreamIndex;
                    const isHttps = s.url?.toLowerCase().startsWith('https://');
                    return (
                      <button
                        key={s.id || idx}
                        type="button"
                        role="menuitem"
                        onClick={() => handleManualStreamSelect(idx)}
                        className={`flex items-center justify-between px-2.5 py-2 rounded-lg text-xs transition-colors ${
                          isSelected 
                            ? 'bg-[#3cd69b]/15 text-[#3cd69b] font-semibold' 
                            : 'text-neutral-300 hover:bg-neutral-800 hover:text-white'
                        }`}
                      >
                        <div className="flex flex-col text-left min-w-0 pr-1">
                          <span className="truncate">Feed {idx + 1}: {s.title || s.feed || `Source ${idx + 1}`}</span>
                          {s.labels ? <span className="text-[10px] text-neutral-500 truncate">{s.labels}</span> : null}
                        </div>
                        <div className="flex items-center gap-1.5 flex-shrink-0">
                          <span className={`px-1.5 py-0.5 text-[9px] rounded font-semibold ${
                            isHttps 
                              ? 'bg-emerald-500/20 text-emerald-400' 
                              : isPageHttps 
                              ? 'bg-blue-500/20 text-blue-400' 
                              : 'bg-amber-500/20 text-amber-400'
                          }`}>
                            {isHttps ? 'HTTPS' : isPageHttps ? 'Relay' : 'HTTP'}
                          </span>
                          {s.quality ? (
                            <span className="hidden xs:inline px-1.5 py-0.5 text-[10px] rounded bg-neutral-700 text-neutral-200">
                              {s.quality}
                            </span>
                          ) : null}
                          {isSelected ? <CheckCircle2 size={13} className="text-[#3cd69b]" aria-hidden="true" /> : null}
                        </div>
                      </button>
                    );
                  })}
                </div>
              ) : null}
            </div>
          ) : currentStream?.quality ? (
            <span className="px-2 py-1 rounded bg-neutral-800 text-[10px] sm:text-[11px] text-neutral-300 border border-neutral-700 font-medium">
              {currentStream.quality}
            </span>
          ) : null}

          {/* Fullscreen Button */}
          <button
            type="button"
            onClick={toggleFullscreen}
            aria-label={isFullscreen ? 'Exit Fullscreen' : 'Enter Fullscreen'}
            className="p-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-white transition-colors focus-visible:ring-2 focus-visible:ring-[#3cd69b]"
          >
            {isFullscreen ? <Minimize size={17} aria-hidden="true" /> : <Maximize size={17} aria-hidden="true" />}
          </button>
        </div>
      </div>
    </div>
  );
};

