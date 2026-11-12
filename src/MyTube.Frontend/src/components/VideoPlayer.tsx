import React, { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import {
  MediaPlayer,
  MediaProvider,
  Poster,
  Track,
  type MediaPlayerInstance,
  type MediaTimeUpdateEventDetail,
} from '@vidstack/react';
import {
  DefaultVideoLayout,
  DefaultAudioLayout,
  defaultLayoutIcons,
} from '@vidstack/react/player/layouts/default';
import '@vidstack/react/player/styles/default/theme.css';
import '@vidstack/react/player/styles/default/layouts/video.css';
import '@vidstack/react/player/styles/default/layouts/audio.css';
import { Volume2, Music, Sparkles, Activity } from 'lucide-react';
import { Badge } from '@mantine/core';
import { Video } from '../types';
import { api } from '../services/api';

interface VideoPlayerProps {
  video: Video;
  onTimeUpdate?: (currentTime: number) => void;
}

export const VideoPlayer: React.FC<VideoPlayerProps> = ({ video, onTimeUpdate }) => {
  const playerRef = useRef<MediaPlayerInstance>(null);
  const lastUpdatedTimeRef = useRef<number>(0);

  // Audio track & transcode states
  const [selectedAudioTrack, setSelectedAudioTrack] = useState<number>(0);
  const startPosition = video.progressSeconds || 0;
  const [transcodeStart, setTranscodeStart] = useState<number>(startPosition);
  const [resumePosition, setResumePosition] = useState<number>(startPosition);
  const [, setIsPaused] = useState<boolean>(true);

  // Check if browser natively supports the video format directly without backend remuxing
  const ext = video.fileName.split('.').pop()?.toLowerCase() || '';
  const isAudio = video.fileName.toLowerCase().endsWith('.mp3') || video.fileName.toLowerCase().endsWith('.m4a');
  const isNative = (ext === 'mp4' || ext === 'webm' || ext === 'ogg' || ext === 'mp3' || ext === 'm4a') && selectedAudioTrack === 0;

  const progressPercent = video.duration > 0 ? startPosition / video.duration : 0;
  const shouldResume = startPosition > 0 && progressPercent < 0.95;

  const downloadUrl = video.downloadUrl || api.getVideoDownloadUrl(video.id);

  // Source URL for playback
  const streamSrc = isNative
    ? video.streamUrl 
    : (transcodeStart > 0 
        ? `${video.streamUrl}?start=${Math.floor(transcodeStart)}&audioTrack=${selectedAudioTrack}` 
        : `${video.streamUrl}?audioTrack=${selectedAudioTrack}`);

  const mediaSource = useMemo(() => {
    let type = 'video/mp4';
    if (isAudio) {
      type = ext === 'mp3' ? 'audio/mpeg' : (ext === 'ogg' ? 'audio/ogg' : 'audio/mp4');
    } else {
      type = ext === 'webm' ? 'video/webm' : (ext === 'ogg' ? 'video/ogg' : 'video/mp4');
    }
    return {
      src: streamSrc,
      type: type as any,
    };
  }, [streamSrc, isAudio, ext]);

  const getActualTime = useCallback((curr: number) => {
    return isNative ? curr : (transcodeStart + curr);
  }, [isNative, transcodeStart]);

  const saveProgress = useCallback((pos: number, dur: number) => {
    const actualPos = getActualTime(pos);
    const actualDur = (dur > 0 && dur !== Infinity) ? dur : video.duration;

    if (actualDur > 0) {
      api.updateProgress(video.id, actualPos, actualDur).catch((err) => {
        console.error('Failed to save watch progress:', err);
      });
    }
  }, [getActualTime, video.duration, video.id]);

  useEffect(() => {
    lastUpdatedTimeRef.current = 0;
    setTranscodeStart(startPosition);
    setResumePosition(startPosition);
    setSelectedAudioTrack(0);
  }, [video.id, startPosition]);

  const handleAudioTrackChange = (trackIdx: number) => {
    const curr = playerRef.current ? playerRef.current.currentTime : 0;
    const currentActualTime = isNative ? curr : (transcodeStart + curr);
    setSelectedAudioTrack(trackIdx);
    setTranscodeStart(currentActualTime);
    setResumePosition(currentActualTime);
  };

  const handleTimeUpdate = (detail: MediaTimeUpdateEventDetail) => {
    const curr = detail.currentTime;
    const dur = playerRef.current?.duration || video.duration || 0;
    const actualTime = getActualTime(curr);

    if (onTimeUpdate) {
      onTimeUpdate(actualTime);
    }

    if (Math.abs(actualTime - lastUpdatedTimeRef.current) >= 5) {
      lastUpdatedTimeRef.current = actualTime;
      saveProgress(curr, dur);
    }
  };

  const handlePause = () => {
    setIsPaused(true);
    if (playerRef.current) {
      saveProgress(playerRef.current.currentTime, playerRef.current.duration);
    }
  };

  const handlePlay = () => {
    setIsPaused(false);
  };

  const handleEnded = () => {
    setIsPaused(true);
    if (playerRef.current) {
      saveProgress(playerRef.current.currentTime, playerRef.current.duration);
    }
  };

  const handleSeeked = (seekedTime: number) => {
    if (!isNative) {
      setTranscodeStart(seekedTime);
    }
  };

  useEffect(() => {
    return () => {
      if (playerRef.current) {
        saveProgress(playerRef.current.currentTime, playerRef.current.duration);
      }
    };
  }, [saveProgress]);

  const audioTrackSelector = video.audioTracks && video.audioTracks.length > 1 ? (
    <div className="flex items-center gap-1.5 text-xs bg-[#121418]/95 hover:bg-[#171920] border border-[#3cd69b]/60 rounded-lg px-2.5 py-1 text-white backdrop-blur-md transition-all shadow-lg pointer-events-auto">
      <Volume2 size={13} className="text-[#3cd69b] shrink-0" />
      <span className="text-[10px] font-mono text-[#3cd69b] font-bold uppercase hidden sm:inline">Audio:</span>
      <select
        value={selectedAudioTrack}
        onChange={(e) => handleAudioTrackChange(parseInt(e.target.value, 10))}
        className="bg-transparent border-none outline-none text-xs font-semibold text-white cursor-pointer pr-1 focus:ring-0"
        aria-label="Switch Audio Language Track"
        title="Switch Audio Language Track"
      >
        {video.audioTracks.map((track) => (
          <option key={track.index} value={track.index} className="bg-[#121418] text-white">
            {track.title || (track.language ? track.language.toUpperCase() : `Track ${track.index + 1}`)}
          </option>
        ))}
      </select>
    </div>
  ) : null;

  const topControlsContent = (
    <div className="flex items-center gap-2 pointer-events-auto">
      {audioTrackSelector}
      <span className="text-[10px] font-mono-telemetry font-bold border border-[#21242d] bg-[#0c0e12]/90 backdrop-blur-md text-[#3cd69b] px-2 py-0.5 rounded-md uppercase select-none tracking-widest hidden sm:inline-block">
        {isNative ? 'Native Direct Play' : 'FFmpeg Remuxing'}
      </span>
    </div>
  );

  return (
    <div className="w-full relative select-none">
      {/* Stitch Atmospheric Artwork Backglow */}
      <div className="absolute -top-16 left-1/2 -translate-x-1/2 w-4/5 h-44 bg-gradient-to-b from-[#3cd69b]/15 via-cyan-500/10 to-transparent blur-3xl pointer-events-none -z-0"></div>

      {/* Top HUD Telemetry Strip (Stitch Template Design) */}
      <div className="flex items-center justify-between pb-2.5 px-1 font-mono-telemetry text-xs text-neutral-400">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-[#3cd69b] animate-pulse"></span>
          <span className="text-[#3cd69b] font-semibold uppercase tracking-wider">LIVE TRANSCODE PIPELINE</span>
          <span className="text-neutral-600">/</span>
          <span className="text-neutral-300">{isNative ? 'Direct Play' : 'On-The-Fly Remux'}</span>
          <span className="px-1.5 py-0.2 rounded bg-[#3cd69b]/10 text-[#3cd69b] text-[10px] border border-[#3cd69b]/30">
            {video.quality || '4K REMUX'}
          </span>
        </div>

        <div className="flex items-center gap-2">
          {audioTrackSelector}

          <div className="hidden sm:flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-[#121418] border border-[#21242d] text-cyan-400 text-[11px]">
            <Sparkles size={12} />
            <span>Whisper Live Subtitles</span>
          </div>
        </div>
      </div>

      {isAudio ? (
        <div className="w-full rounded-2xl overflow-hidden shadow-2xl border border-[#21242d] bg-[#0c0d12] p-6 flex flex-col items-center justify-center gap-6 relative">
          <div className="relative flex items-center justify-center my-2">
            <div className="w-40 h-40 rounded-full bg-gradient-to-tr from-[#3cd69b]/20 to-[#3cd69b]/5 flex items-center justify-center border-2 border-[#3cd69b]/30 shadow-2xl relative overflow-hidden group">
              {video.thumbnailUrl ? (
                <img
                  src={video.thumbnailUrl}
                  alt={video.displayTitle}
                  className="w-full h-full object-cover rounded-full"
                />
              ) : (
                <Music size={56} className="text-[#3cd69b] transition-transform duration-500 group-hover:scale-110" />
              )}
            </div>
          </div>

          <div className="w-full max-w-2xl">
            <MediaPlayer
              key={`${video.id}-${selectedAudioTrack}-${transcodeStart}`}
              ref={playerRef}
              title={video.displayTitle}
              src={mediaSource}
              viewType="audio"
              load="eager"
              preload="auto"
              currentTime={isNative && shouldResume ? startPosition : undefined}
              autoPlay
              crossOrigin="anonymous"
              onTimeUpdate={handleTimeUpdate}
              onPlay={handlePlay}
              onPause={handlePause}
              onEnded={handleEnded}
              onSeeked={handleSeeked}
              className="w-full rounded-xl overflow-hidden"
            >
              <MediaProvider>
                {video.subtitleTracks && video.subtitleTracks.length > 0 && video.subtitleTracks.map((sub, idx) => (
                  <Track
                    key={sub.id || sub.src || `track-${idx}`}
                    src={sub.src}
                    kind="subtitles"
                    label={sub.label || 'Subtitles'}
                    lang={sub.language || 'en'}
                    default={sub.isDefault ?? (idx === 0)}
                  />
                ))}
              </MediaProvider>
              <DefaultAudioLayout
                icons={defaultLayoutIcons}
                colorScheme="dark"
                download={downloadUrl}
                slots={{
                  afterVolumeSlider: audioTrackSelector,
                  topControlsGroupEnd: topControlsContent,
                }}
              />
            </MediaPlayer>
          </div>
        </div>
      ) : (
        <div className="video-player-container w-full aspect-video rounded-2xl overflow-hidden shadow-2xl border border-[#21242d] bg-black relative">
          <MediaPlayer
            key={`${video.id}-${selectedAudioTrack}-${transcodeStart}`}
            ref={playerRef}
            title={video.displayTitle}
            src={mediaSource}
            poster={video.thumbnailUrl}
            viewType="video"
            aspectRatio="16/9"
            load="eager"
            preload="auto"
            currentTime={isNative ? (resumePosition > 0 ? resumePosition : (shouldResume ? startPosition : undefined)) : undefined}
            autoPlay
            playsInline
            crossOrigin="anonymous"
            onTimeUpdate={handleTimeUpdate}
            onPlay={handlePlay}
            onPause={handlePause}
            onEnded={handleEnded}
            onSeeked={handleSeeked}
            className="w-full h-full"
          >
            <MediaProvider>
              {video.thumbnailUrl && (
                <Poster
                  className="vds-poster"
                  src={video.thumbnailUrl}
                  alt={video.displayTitle}
                />
              )}
              {video.subtitleTracks && video.subtitleTracks.length > 0 && video.subtitleTracks.map((sub, idx) => (
                <Track
                  key={sub.id || sub.src || `track-${idx}`}
                  src={sub.src}
                  kind="subtitles"
                  label={sub.label || 'Subtitles'}
                  lang={sub.language || 'en'}
                  default={sub.isDefault ?? (idx === 0)}
                />
              ))}
            </MediaProvider>

            <DefaultVideoLayout
              icons={defaultLayoutIcons}
              colorScheme="dark"
              download={downloadUrl}
              slots={{
                afterVolumeSlider: audioTrackSelector,
                beforeCaptionButton: audioTrackSelector,
                topControlsGroupEnd: topControlsContent,
              }}
            />
          </MediaPlayer>
        </div>
      )}
    </div>
  );
};
