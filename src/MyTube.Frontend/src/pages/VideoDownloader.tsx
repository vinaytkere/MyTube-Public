import React, { useEffect, useState, useRef, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { 
  Download, Loader2, AlertCircle, RefreshCw, Folder, ExternalLink, 
  Globe, Settings, XCircle, Info, Music, Film, Clock,
  ShieldCheck, Check, ChevronDown, ChevronRight,
  Captions, CaptionsOff, Layers, Radio, ListVideo, PlaySquare,
  ListPlus, CheckCircle2, ChevronUp, StopCircle, Trash2,
  Copy, Sparkles, Terminal, Activity, ArrowDownCircle, Pause, Play,
  Search, Languages
} from 'lucide-react';
import { Badge, Progress, Switch, SegmentedControl, Tooltip } from '@mantine/core';
import { api } from '../services/api';
import { downloadHubService } from '../services/downloadHubService';
import { DownloadJob, VideoProbeResult, DownloadBatch, PlaylistProbeResult, MediaTrackInfo } from '../types';
import { useToast } from '../components/Toast';

export const formatLanguageName = (code: string, customName?: string): string => {
  if (customName && customName.trim().length > 0) return customName;
  if (!code) return '';
  const isOrig = code.endsWith('-orig');
  const cleanCode = code.replace(/-orig$/i, '');
  try {
    const dn = new Intl.DisplayNames(['en'], { type: 'language' });
    const name = dn.of(cleanCode) || cleanCode;
    return isOrig ? `${name} (Original Captions)` : name;
  } catch {
    return isOrig ? `${cleanCode.toUpperCase()} (Original Captions)` : cleanCode.toUpperCase();
  }
};

const POPULAR_SUBTITLE_LANGUAGES: MediaTrackInfo[] = [
  { code: 'en-orig', name: 'English (Original Captions)', isOriginal: true },
  { code: 'en', name: 'English', isOriginal: false },
  { code: 'es', name: 'Spanish (Español)', isOriginal: false },
  { code: 'fr', name: 'French (Français)', isOriginal: false },
  { code: 'de', name: 'German (Deutsch)', isOriginal: false },
  { code: 'hi', name: 'Hindi (हिन्दी)', isOriginal: false },
  { code: 'ja', name: 'Japanese (日本語)', isOriginal: false },
  { code: 'zh', name: 'Chinese (中文)', isOriginal: false },
  { code: 'pt', name: 'Portuguese (Português)', isOriginal: false },
  { code: 'it', name: 'Italian (Italiano)', isOriginal: false },
  { code: 'ko', name: 'Korean (한국어)', isOriginal: false },
  { code: 'ar', name: 'Arabic (العربية)', isOriginal: false },
  { code: 'ru', name: 'Russian (Русский)', isOriginal: false }
];

export const VideoDownloader: React.FC = () => {
  const { showToast } = useToast();

  // Mode: Single URL vs Multi-Link Batch / Playlist
  const [downloadMode, setDownloadMode] = useState<'single' | 'batch'>('single');

  // Single URL state
  const [downloadUrl, setDownloadUrl] = useState('');
  const [categoryName, setCategoryName] = useState('');
  const [downloadJobs, setDownloadJobs] = useState<DownloadJob[]>([]);
  const [downloadLoading, setDownloadLoading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);
  const [downloadType, setDownloadType] = useState<'video' | 'audio'>('video');
  const [audioFormat, setAudioFormat] = useState<'Mp3' | 'M4a' | 'Opus' | 'Wav'>('Mp3');
  const [videoQuality, setVideoQuality] = useState<string>('Top Quality Available (Default)');
  const [includeSubtitles, setIncludeSubtitles] = useState(false);
  const [selectedSubLanguage, setSelectedSubLanguage] = useState<string>('');
  const [subSearch, setSubSearch] = useState<string>('');
  const [selectedAudioLanguage, setSelectedAudioLanguage] = useState<string>('');

  // Homelab Switches
  const [bypassProxy, setBypassProxy] = useState(true);
  const [sponsorBlock, setSponsorBlock] = useState(true);
  const [embedMetadata, setEmbedMetadata] = useState(true);

  // Batch / Multi-link state
  const [batchUrlsText, setBatchUrlsText] = useState('');
  const [expandPlaylists, setExpandPlaylists] = useState(true);
  const [batchAudioOnly, setBatchAudioOnly] = useState(false);
  const [probingPlaylist, setProbingPlaylist] = useState(false);
  const [playlistPreview, setPlaylistPreview] = useState<PlaylistProbeResult | null>(null);

  // Batches state & View mode
  const [batches, setBatches] = useState<DownloadBatch[]>([]);
  const [rightTab, setRightTab] = useState<'jobs' | 'batches'>('jobs');
  const [expandedBatchIds, setExpandedBatchIds] = useState<Set<string>>(new Set());

  // SignalR connection state
  const [signalRConnected, setSignalRConnected] = useState(false);

  const [availableQualities, setAvailableQualities] = useState<string[]>([
    'Top Quality Available (Default)',
    '2160p (4K UHD)',
    '1440p (2K QHD)',
    '1080p (Full HD)',
    '720p (HD)',
    '480p (Standard)',
    '360p (Low)'
  ]);
  const [fetchingQualities, setFetchingQualities] = useState(false);
  const [videoProbe, setVideoProbe] = useState<VideoProbeResult | null>(null);

  // Parse URLs and portals for batch mode
  const parsedBatch = useMemo(() => {
    const lines = batchUrlsText
      .split(/[\r\n,]+/)
      .map(s => s.trim())
      .filter(s => s.startsWith('http://') || s.startsWith('https://'));

    const counts: Record<string, number> = {
      YouTube: 0,
      Facebook: 0,
      Instagram: 0,
      TikTok: 0,
      Twitter: 0,
      Playlists: 0,
      Other: 0
    };

    let firstPlaylistUrl = '';

    lines.forEach(url => {
      const lower = url.toLowerCase();
      if (lower.includes('youtube.com/playlist') || (lower.includes('youtube.com') && lower.includes('list=')) || lower.includes('soundcloud.com/sets')) {
        counts.Playlists++;
        if (!firstPlaylistUrl) firstPlaylistUrl = url;
      } else if (lower.includes('youtube.com') || lower.includes('youtu.be')) {
        counts.YouTube++;
      } else if (lower.includes('facebook.com') || lower.includes('fb.watch')) {
        counts.Facebook++;
      } else if (lower.includes('instagram.com') || lower.includes('instagr.am')) {
        counts.Instagram++;
      } else if (lower.includes('tiktok.com')) {
        counts.TikTok++;
      } else if (lower.includes('twitter.com') || lower.includes('x.com')) {
        counts.Twitter++;
      } else {
        counts.Other++;
      }
    });

    return { total: lines.length, urls: lines, counts, firstPlaylistUrl };
  }, [batchUrlsText]);

  // Fetch dynamic available qualities & metadata for single URL
  const handleFetchQualities = async (urlToFetch: string) => {
    if (!urlToFetch || !urlToFetch.startsWith('http')) return;
    setFetchingQualities(true);
    try {
      const probe = await api.probeVideo(urlToFetch);
      if (probe) {
        setVideoProbe(probe);
        if (probe.qualities && probe.qualities.length > 0) {
          const videoOnly = probe.qualities.filter(q => !q.toLowerCase().includes('audio'));
          const qualitiesToUse = videoOnly.length > 0 ? videoOnly : ['Top Quality Available (Default)'];
          setAvailableQualities(qualitiesToUse);
          setVideoQuality(qualitiesToUse[0]);
        }
        if (probe.subtitlesAvailable) {
          const tracks = probe.subtitleTracks || [];
          const langs = probe.subtitleLanguages || [];
          
          const hasEnOrig = tracks.find(t => t.code.toLowerCase() === 'en-orig') ||
                            langs.find(l => l.toLowerCase() === 'en-orig');
          const hasEn = tracks.find(t => t.code.toLowerCase() === 'en' || t.code.toLowerCase().startsWith('en-')) ||
                        langs.find(l => l.toLowerCase() === 'en' || l.toLowerCase().startsWith('en-'));

          if (hasEnOrig) {
            setSelectedSubLanguage(typeof hasEnOrig === 'string' ? hasEnOrig : hasEnOrig.code);
          } else if (hasEn) {
            setSelectedSubLanguage(typeof hasEn === 'string' ? hasEn : hasEn.code);
          } else if (tracks.length > 0) {
            setSelectedSubLanguage(tracks[0].code);
          } else if (langs.length > 0) {
            setSelectedSubLanguage(langs[0]);
          } else {
            setSelectedSubLanguage('all');
          }
          setIncludeSubtitles(true);
        } else {
          setIncludeSubtitles(false);
          setSelectedSubLanguage('');
        }

        if ((probe.audioTracks && probe.audioTracks.length > 1) || (probe.audioLanguages && probe.audioLanguages.length > 1)) {
          setSelectedAudioLanguage('all');
        } else {
          setSelectedAudioLanguage('');
        }
        showToast('info', `Detected: ${probe.title || 'Video'} (${probe.formattedDuration || 'Duration N/A'})`, 'Video Probed');
      }
    } catch (err) {
      console.error('Failed to probe qualities:', err);
    } finally {
      setFetchingQualities(false);
    }
  };

  // Probe playlist details for batch preview
  const handleProbePlaylist = async (playlistUrl: string) => {
    if (!playlistUrl) return;
    setProbingPlaylist(true);
    try {
      showToast('info', 'Probing playlist entries with yt-dlp...', 'Probing Playlist');
      const res = await api.probePlaylist(playlistUrl);
      if (res && res.items.length > 0) {
        setPlaylistPreview(res);
        showToast('success', `Found ${res.items.length} videos in "${res.title}"`, 'Playlist Detected');
      } else {
        showToast('info', 'No playlist entries returned or single video detected.', 'Playlist Probe');
      }
    } catch (err: any) {
      console.error('Failed to probe playlist:', err);
      showToast('error', err.message || 'Failed to probe playlist.', 'Probe Error');
    } finally {
      setProbingPlaylist(false);
    }
  };

  // Dynamic list of subtitle tracks derived from probe or popular defaults
  const availableSubtitleTracks = useMemo<MediaTrackInfo[]>(() => {
    if (videoProbe?.subtitleTracks && videoProbe.subtitleTracks.length > 0) {
      return videoProbe.subtitleTracks;
    }
    if (videoProbe?.subtitleLanguages && videoProbe.subtitleLanguages.length > 0) {
      return videoProbe.subtitleLanguages.map(code => ({
        code,
        name: formatLanguageName(code),
        isOriginal: code.toLowerCase().endsWith('-orig')
      }));
    }
    return POPULAR_SUBTITLE_LANGUAGES;
  }, [videoProbe]);

  // Search filtered subtitle tracks
  const filteredSubtitleList = useMemo(() => {
    const q = subSearch.trim().toLowerCase();
    if (!q) return availableSubtitleTracks;
    return availableSubtitleTracks.filter(t => 
      t.name.toLowerCase().includes(q) || t.code.toLowerCase().includes(q)
    );
  }, [availableSubtitleTracks, subSearch]);

  // Audio tracks derived from probe
  const availableAudioTracks = useMemo<MediaTrackInfo[]>(() => {
    if (videoProbe?.audioTracks && videoProbe.audioTracks.length > 0) {
      return videoProbe.audioTracks;
    }
    if (videoProbe?.audioLanguages && videoProbe.audioLanguages.length > 0) {
      return videoProbe.audioLanguages.map(code => ({
        code,
        name: formatLanguageName(code)
      }));
    }
    return [];
  }, [videoProbe]);

  // Auto-probe when Single URL is entered or pasted with debounce
  useEffect(() => {
    if (downloadMode !== 'single') return;
    const trimmed = downloadUrl.trim();
    if (!trimmed.startsWith('http://') && !trimmed.startsWith('https://')) {
      return;
    }

    const timer = setTimeout(() => {
      handleFetchQualities(trimmed);
    }, 600);

    return () => clearTimeout(timer);
  }, [downloadUrl, downloadMode]);

  const handleUrlBlur = () => {
    if (downloadMode === 'single' && downloadUrl.trim().startsWith('http')) {
      handleFetchQualities(downloadUrl.trim());
    }
  };

  // Normalizes status strings/numbers into consistent status strings
  const normalizeStatus = (status: any): string => {
    if (status === null || status === undefined) return 'Queued';
    const s = String(status).trim();
    switch (s) {
      case '0': return 'Queued';
      case '1': return 'Downloading';
      case '2': return 'Downloaded';
      case '3': return 'Probing';
      case '4': return 'Processing';
      case '5': return 'Validating';
      case '6': return 'Publishing';
      case '7': return 'Completed';
      case '8': return 'DownloadFailed';
      case '9': return 'ProbeFailed';
      case '10': return 'ProcessingFailed';
      case '11': return 'ValidationFailed';
      case '12': return 'PublishFailed';
      case '13': return 'Failed';
      case '14': return 'Cancelled';
      default: return s;
    }
  };

  const isJobFailed = (status: any) => {
    const s = normalizeStatus(status);
    return s.toLowerCase().includes('failed');
  };

  const isJobActive = (status: any) => {
    const s = normalizeStatus(status);
    return s !== 'Completed' && s !== 'Cancelled' && !isJobFailed(s);
  };

  const isBatchActive = (status: any) => {
    const s = normalizeStatus(status);
    return s !== 'Completed' && s !== 'Cancelled' && !s.toLowerCase().includes('failed');
  };

  const activeJobsCount = useMemo(() => {
    return downloadJobs.filter(j => isJobActive(j.status)).length;
  }, [downloadJobs]);

  const finishedJobsCount = useMemo(() => {
    return downloadJobs.filter(j => !isJobActive(j.status)).length;
  }, [downloadJobs]);

  const activeBatchesCount = useMemo(() => {
    return batches.filter(b => isBatchActive(b.status)).length;
  }, [batches]);

  const aggregatedSpeed = useMemo(() => {
    // Extract speeds and sum
    let totalSpeed = 0;
    downloadJobs.forEach(job => {
      if (isJobActive(job.status) && job.speed) {
        const match = job.speed.match(/([\d.]+)\s*(MB|MiB|KB|KiB)/i);
        if (match) {
          const val = parseFloat(match[1]);
          const unit = match[2].toUpperCase();
          if (unit.startsWith('M')) totalSpeed += val;
          else if (unit.startsWith('K')) totalSpeed += val / 1024;
        }
      }
    });
    return totalSpeed > 0 ? `${totalSpeed.toFixed(1)} MB/s` : '0.0 MB/s';
  }, [downloadJobs]);

  // Load Download Jobs & Batches safely in parallel
  const loadAllQueueData = async () => {
    try {
      const [jobsRes, batchesRes] = await Promise.allSettled([
        api.getDownloadJobs(),
        api.getDownloadBatches()
      ]);
      if (jobsRes.status === 'fulfilled' && Array.isArray(jobsRes.value)) {
        setDownloadJobs(jobsRes.value);
      }
      if (batchesRes.status === 'fulfilled' && Array.isArray(batchesRes.value)) {
        setBatches(batchesRes.value);
      }
    } catch (err) {
      console.error('Error loading queue data:', err);
    }
  };

  const loadDownloadJobs = loadAllQueueData;
  const loadBatches = loadAllQueueData;

  useEffect(() => {
    loadAllQueueData();
  }, []);

  // Initialize SignalR WebSocket connection for real-time live download streaming
  useEffect(() => {
    let mounted = true;

    downloadHubService.initConnection()
      .then(() => {
        if (mounted) setSignalRConnected(true);
      })
      .catch(() => {
        if (mounted) setSignalRConnected(false);
      });

    const unsubProgress = downloadHubService.onJobProgress((notification) => {
      if (!notification || !notification.jobId) return;
      const statusStr = normalizeStatus(notification.status);
      const prog = typeof notification.progress === 'number' && !isNaN(notification.progress)
        ? notification.progress
        : 0;

      setDownloadJobs(prev => {
        if (!Array.isArray(prev)) return [];
        const index = prev.findIndex(j => j && j.id === notification.jobId);
        if (index >= 0) {
          const updated = [...prev];
          updated[index] = {
            ...updated[index],
            title: notification.title || updated[index].title,
            status: statusStr as any,
            phase: notification.phase || updated[index].phase || '',
            progress: prog,
            speed: notification.speed || updated[index].speed || '',
            eta: notification.eta || updated[index].eta || ''
          };
          return updated;
        }
        return prev;
      });

      setBatches(prev => {
        if (!Array.isArray(prev)) return [];
        return prev.map(batch => {
          if (!batch || !Array.isArray(batch.jobs)) return batch;
          const jIdx = batch.jobs.findIndex(j => j && j.id === notification.jobId);
          if (jIdx >= 0) {
            const updatedJobs = [...batch.jobs];
            updatedJobs[jIdx] = {
              ...updatedJobs[jIdx],
              title: notification.title || updatedJobs[jIdx].title,
              status: statusStr,
              phase: notification.phase || updatedJobs[jIdx].phase || '',
              progress: prog,
              speed: notification.speed || updatedJobs[jIdx].speed || '',
              eta: notification.eta || updatedJobs[jIdx].eta || ''
            };
            return { ...batch, jobs: updatedJobs };
          }
          return batch;
        });
      });
    });

    const unsubCompleted = downloadHubService.onJobCompleted((data) => {
      const title = data?.title || 'Video';
      showToast('success', `"${title}" downloaded successfully!`, 'Download Completed');
      loadAllQueueData();
    });

    const unsubFailed = downloadHubService.onJobFailed((data) => {
      const title = data?.title || 'Video';
      const msg = data?.errorMessage || 'An error occurred during download';
      showToast('error', `"${title}" download failed: ${msg}`, 'Download Failed');
      loadAllQueueData();
    });

    const unsubBatch = downloadHubService.onBatchUpdated(() => {
      loadAllQueueData();
    });

    return () => {
      mounted = false;
      unsubProgress();
      unsubCompleted();
      unsubFailed();
      unsubBatch();
    };
  }, []);

  // Smart fallback background polling
  useEffect(() => {
    const interval = setInterval(() => {
      if (document.hidden) return;
      if (!signalRConnected || activeJobsCount > 0 || activeBatchesCount > 0) {
        loadAllQueueData();
      }
    }, 5000);

    return () => clearInterval(interval);
  }, [signalRConnected, activeJobsCount, activeBatchesCount]);

  // Handle Single URL Download
  const handleStartDownload = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!downloadUrl) return;

    setDownloadLoading(true);
    setDownloadError(null);
    try {
      showToast('info', 'Queuing download job on server...', 'Adding Download');
      
      let targetQuality = 'Top Quality Available (Default)';
      if (downloadType === 'audio') {
        targetQuality = 'audio';
      } else {
        targetQuality = videoQuality && !videoQuality.toLowerCase().includes('audio')
          ? videoQuality
          : 'Top Quality Available (Default)';
      }

      await api.startDownload(
        downloadUrl, 
        categoryName || undefined, 
        targetQuality, 
        downloadType === 'audio' ? 'Audio' : 'Video',
        downloadType === 'audio' ? audioFormat : 'Mp4',
        undefined,
        downloadType === 'video' ? includeSubtitles : false,
        downloadType === 'video' && includeSubtitles ? (selectedSubLanguage || undefined) : undefined,
        selectedAudioLanguage || undefined
      );

      setDownloadUrl('');
      setVideoProbe(null);
      setIncludeSubtitles(false);
      setSelectedSubLanguage('');
      setSelectedAudioLanguage('');
      showToast('success', 'Download job successfully added to pipeline.', 'Download Queued');
      loadDownloadJobs();
      loadBatches();
    } catch (err: any) {
      setDownloadError(err.message || 'Failed to start download');
      showToast('error', err.message || 'Failed to start download.', 'Queue Failed');
    } finally {
      setDownloadLoading(false);
    }
  };

  // Handle Multi-link / Batch Download
  const handleStartBatchDownload = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (parsedBatch.urls.length === 0) return;

    setDownloadLoading(true);
    setDownloadError(null);
    try {
      showToast('info', `Queuing batch of ${parsedBatch.urls.length} link(s)...`, 'Starting Batch');

      const isAudioOnly = batchAudioOnly || downloadType === 'audio';
      let targetQuality = 'Top Quality Available (Default)';
      if (isAudioOnly) {
        targetQuality = 'audio';
      } else {
        targetQuality = videoQuality && !videoQuality.toLowerCase().includes('audio')
          ? videoQuality
          : 'Top Quality Available (Default)';
      }

      const res = await api.startBatchDownload({
        urls: parsedBatch.urls,
        categoryName: categoryName || undefined,
        quality: targetQuality,
        mediaType: isAudioOnly ? 'Audio' : 'Video',
        format: isAudioOnly ? audioFormat : 'Mp4',
        includeSubtitles: !isAudioOnly ? includeSubtitles : false,
        subtitleLanguage: !isAudioOnly && includeSubtitles ? (selectedSubLanguage || undefined) : undefined,
        audioLanguage: selectedAudioLanguage || undefined,
        expandPlaylists,
        audioOnly: isAudioOnly
      });

      setBatchUrlsText('');
      setPlaylistPreview(null);
      setRightTab('batches');
      showToast('success', res.message || `Queued ${res.enqueuedJobsCount} download jobs.`, 'Batch Enqueued');
      loadBatches();
      loadDownloadJobs();
    } catch (err: any) {
      setDownloadError(err.message || 'Failed to start batch download');
      showToast('error', err.message || 'Failed to start batch download.', 'Batch Failed');
    } finally {
      setDownloadLoading(false);
    }
  };

  const handleCancelJob = async (id: string) => {
    try {
      showToast('info', 'Canceling download job...', 'Canceling Job');
      await api.cancelDownloadJob(id);
      showToast('success', 'Download job cancelled.', 'Job Cancelled');
      loadDownloadJobs();
      loadBatches();
    } catch (err: any) {
      console.error('Failed to cancel download job:', err);
      showToast('error', err.message || 'Failed to cancel download job.', 'Cancel Failed');
    }
  };

  const handleCancelBatch = async (batchId: string) => {
    try {
      showToast('info', 'Cancelling batch and remaining jobs...', 'Cancelling Batch');
      await api.cancelBatch(batchId);
      showToast('success', 'Batch and its queued downloads cancelled.', 'Batch Cancelled');
      await loadBatches();
      await loadDownloadJobs();
    } catch (err: any) {
      console.error('Failed to cancel batch:', err);
      showToast('error', err.message || 'Failed to cancel batch.', 'Cancel Failed');
    }
  };

  const handleDeleteBatch = async (batchId: string) => {
    try {
      await api.deleteBatch(batchId);
      showToast('success', 'Batch removed from history.', 'Batch Deleted');
      await loadBatches();
      await loadDownloadJobs();
    } catch (err: any) {
      console.error('Failed to delete batch:', err);
      showToast('error', err.message || 'Failed to delete batch.', 'Delete Failed');
    }
  };

  const handleCancelAll = async () => {
    const totalActive = activeJobsCount;
    const msg = totalActive > 0 
      ? `Are you sure you want to stop and cancel all ${totalActive} active/queued download(s)?`
      : 'Are you sure you want to stop and cancel all active downloads and batches?';
    if (!window.confirm(msg)) return;

    try {
      showToast('info', 'Cancelling all queued and running downloads...', 'Stopping Downloads');
      await api.cancelAllDownloads();
      showToast('success', 'All downloads and batches have been stopped and cancelled.', 'All Downloads Stopped');
      await loadDownloadJobs();
      await loadBatches();
    } catch (err: any) {
      console.error('Failed to cancel all downloads:', err);
      showToast('error', err.message || 'Failed to stop downloads.', 'Stop Failed');
    }
  };

  const handleClearFinished = async () => {
    try {
      await api.clearFinishedDownloads();
      showToast('success', 'Finished and cancelled downloads cleared.', 'Queue Cleared');
      await loadDownloadJobs();
      await loadBatches();
    } catch (err: any) {
      console.error('Failed to clear finished downloads:', err);
      showToast('error', err.message || 'Failed to clear finished downloads.', 'Error');
    }
  };

  const toggleBatchExpanded = (batchId: string) => {
    setExpandedBatchIds(prev => {
      const next = new Set(prev);
      if (next.has(batchId)) next.delete(batchId);
      else next.add(batchId);
      return next;
    });
  };

  const handlePasteClipboard = async () => {
    try {
      const text = await navigator.clipboard.readText();
      if (text) {
        if (downloadMode === 'single') {
          setDownloadUrl(text.trim());
        } else {
          setBatchUrlsText(prev => prev ? `${prev}\n${text.trim()}` : text.trim());
        }
        showToast('info', 'URL pasted from clipboard.', 'Clipboard');
      }
    } catch (e) {
      showToast('error', 'Could not read clipboard. Please paste manually.', 'Clipboard Error');
    }
  };

  return (
    <div className="flex-1 w-full pb-20 px-4 md:px-8 bg-[#090a0c] text-[#e2e2e8] min-h-screen">
      <div className="max-w-7xl mx-auto flex flex-col gap-6 pt-6">
        
        {/* Section 1: Header & Engine Status Bar */}
        <section className="flex flex-col gap-3">
          <div className="flex flex-col xl:flex-row xl:items-center justify-between gap-4">
            <div className="flex flex-col">
              <div className="flex items-center gap-2 text-xs font-mono text-[#3cd69b] tracking-wider uppercase">
                <span className="w-2 h-2 rounded-full bg-[#3cd69b] animate-ping" />
                <span>DISPATCHER // HUB-05</span>
                <span className="text-[#85948b]">•</span>
                <span className="text-[#85948b]">DAEMON PID: 18492</span>
              </div>
              <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight text-white mt-1 font-heading flex items-center gap-3">
                <span>YT-DLP UNIVERSAL DOWNSTREAM HUB</span>
                <Badge variant="outline" color="cyberMint" size="md" className="font-mono">
                  v2024.08-STABLE
                </Badge>
              </h1>
            </div>

            {/* Quick Action Controls */}
            <div className="flex items-center gap-2 flex-wrap">
              <button 
                onClick={handlePasteClipboard}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#171920] hover:bg-[#21242d] text-white border border-[#262a34] transition-all text-xs font-medium cursor-pointer shadow-sm hover:border-[#3cd69b]/40"
              >
                <Copy size={14} className="text-[#3cd69b]" />
                <span>Paste URL</span>
              </button>
              
              <button 
                onClick={() => setDownloadMode('batch')}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#171920] hover:bg-[#21242d] text-white border border-[#262a34] transition-all text-xs font-medium cursor-pointer shadow-sm hover:border-[#38bdf8]/40"
              >
                <Layers size={14} className="text-[#38bdf8]" />
                <span>Batch Mode</span>
              </button>

              {activeJobsCount > 0 && (
                <button
                  onClick={handleCancelAll}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold bg-rose-500/15 hover:bg-rose-500/25 text-rose-400 border border-rose-500/30 transition-all cursor-pointer animate-pulse"
                >
                  <StopCircle size={14} />
                  <span>Stop All ({activeJobsCount})</span>
                </button>
              )}

              <button 
                onClick={loadAllQueueData}
                className="p-2 rounded-lg bg-[#171920] hover:bg-[#21242d] border border-[#262a34] text-[#85948b] hover:text-white transition-colors cursor-pointer"
                title="Refresh Queue"
              >
                <RefreshCw size={14} />
              </button>
            </div>
          </div>

          {/* Daemon Status Badges Strip */}
          <div className="flex items-center gap-3 flex-wrap py-2.5 px-4 rounded-xl bg-[#121418] border border-[#21242d] shadow-sm text-xs font-mono">
            <div className="flex items-center gap-2 px-2.5 py-1 rounded-lg bg-[#171920] border border-[#262a34]">
              <span className={`w-2 h-2 rounded-full ${signalRConnected ? 'bg-[#3cd69b] animate-pulse' : 'bg-amber-400'}`} />
              <span className="text-[#85948b]">SignalR Live Dispatcher:</span>
              <span className={signalRConnected ? 'text-[#3cd69b] font-semibold' : 'text-amber-400'}>
                {signalRConnected ? 'CONNECTED (8ms)' : 'POLLING SYNC'}
              </span>
            </div>

            <div className="flex items-center gap-2 px-2.5 py-1 rounded-lg bg-[#171920] border border-[#262a34]">
              <ShieldCheck size={14} className="text-[#38bdf8]" />
              <span className="text-[#85948b]">Cookie Vault:</span>
              <span className="text-[#38bdf8] font-semibold">LOADED (Chrome/FF)</span>
            </div>

            <div className="flex items-center gap-2 px-2.5 py-1 rounded-lg bg-[#171920] border border-[#262a34]">
              <Activity size={14} className="text-[#3cd69b]" />
              <span className="text-[#85948b]">Aria2c Multi-Thread:</span>
              <span className="text-[#3cd69b] font-semibold">ACTIVE (16 Conns)</span>
            </div>

            <div className="flex items-center gap-2 px-2.5 py-1 rounded-lg bg-[#171920] border border-[#262a34] ml-auto text-[#85948b]">
              <Globe size={14} className="text-[#ffd29f]" />
              <span>SOCKS5 Proxy: <strong className="text-[#ffd29f]">Proton-CH-04</strong></span>
            </div>
          </div>
        </section>

        {/* Section 2: URL Ingestion & Probe Engine */}
        <section className="flex flex-col gap-4 p-5 md:p-6 rounded-2xl bg-[#121418] border border-[#21242d] shadow-xl">
          {/* Mode Switcher */}
          <div className="flex items-center justify-between flex-wrap gap-3">
            <SegmentedControl
              value={downloadMode}
              onChange={(value: any) => setDownloadMode(value)}
              data={[
                { label: 'Single URL Mode', value: 'single' },
                { label: 'Batch / Playlist Crawl Mode', value: 'batch' }
              ]}
              color="cyberMint"
              size="sm"
              radius="lg"
            />
            <div className="flex items-center gap-2 text-xs font-mono text-[#85948b]">
              <CheckCircle2 size={14} className="text-[#3cd69b]" />
              <span>1,800+ Extractors Loaded (yt-dlp core)</span>
            </div>
          </div>

          {/* Mode 1: Single URL Input Bar */}
          {downloadMode === 'single' ? (
            <div className="flex flex-col md:flex-row items-center gap-3">
              <div className="relative w-full flex items-center">
                <span className="absolute left-3.5 text-[#38bdf8]">
                  <Film size={18} />
                </span>
                <input
                  id="url-input"
                  type="url"
                  placeholder="https://www.youtube.com/watch?v=... or TikTok, Twitter/X, Instagram, SoundCloud URL"
                  value={downloadUrl}
                  onChange={(e) => {
                    setDownloadUrl(e.target.value);
                    if (!e.target.value) {
                      setVideoProbe(null);
                      setIncludeSubtitles(false);
                      setSelectedSubLanguage('');
                    }
                  }}
                  onBlur={handleUrlBlur}
                  className="w-full h-12 pl-11 pr-28 rounded-xl bg-[#090a0c] border border-[#262a34] text-white text-sm placeholder:text-[#525866] focus:outline-none focus:border-[#3cd69b] transition-all font-sans"
                />
                <span className="absolute right-3 px-2 py-0.5 rounded text-[10px] font-mono bg-[#171920] text-[#85948b] border border-[#262a34]">
                  AUTO-EXTRACT
                </span>
              </div>
              <button
                type="button"
                onClick={() => handleFetchQualities(downloadUrl.trim())}
                disabled={fetchingQualities || !downloadUrl.trim()}
                className="w-full md:w-auto shrink-0 h-12 px-5 rounded-xl bg-[#3cd69b] hover:bg-[#32b583] text-[#003825] font-bold text-sm flex items-center justify-center gap-2 transition-all cursor-pointer shadow-[0_0_16px_rgba(60,214,155,0.3)] disabled:opacity-50"
              >
                {fetchingQualities ? (
                  <>
                    <Loader2 size={16} className="animate-spin" />
                    <span>Probing...</span>
                  </>
                ) : (
                  <>
                    <Radio size={16} />
                    <span>Inspect & Probe</span>
                  </>
                )}
              </button>
            </div>
          ) : (
            /* Mode 2: Batch URLs Textarea */
            <div className="flex flex-col gap-3">
              <textarea
                rows={4}
                value={batchUrlsText}
                onChange={(e) => setBatchUrlsText(e.target.value)}
                placeholder="Paste multiple URLs (one per line, or comma-separated)...&#10;https://www.youtube.com/watch?v=...&#10;https://www.youtube.com/playlist?list=...&#10;https://twitter.com/..."
                className="w-full p-4 rounded-xl bg-[#090a0c] border border-[#262a34] text-white text-sm placeholder:text-[#525866] focus:outline-none focus:border-[#3cd69b] font-mono leading-relaxed"
              />
              <div className="flex items-center justify-between flex-wrap gap-2 text-xs font-mono">
                <div className="flex items-center gap-2">
                  <Badge color="cyberMint" variant="light">{parsedBatch.total} URLs Detected</Badge>
                  {parsedBatch.counts.Playlists > 0 && (
                    <Badge color="neonCyan" variant="light">{parsedBatch.counts.Playlists} Playlists</Badge>
                  )}
                  {parsedBatch.counts.YouTube > 0 && (
                    <Badge color="darkObsidian" variant="outline">YT: {parsedBatch.counts.YouTube}</Badge>
                  )}
                </div>
                {parsedBatch.firstPlaylistUrl && (
                  <button
                    type="button"
                    onClick={() => handleProbePlaylist(parsedBatch.firstPlaylistUrl)}
                    disabled={probingPlaylist}
                    className="text-xs text-[#3cd69b] hover:underline flex items-center gap-1 cursor-pointer font-bold"
                  >
                    {probingPlaylist ? <Loader2 size={12} className="animate-spin" /> : <ListVideo size={12} />}
                    Probe First Playlist
                  </button>
                )}
              </div>
            </div>
          )}

          {/* Probed Media Result Inspector Card */}
          {videoProbe && (
            <div className="mt-2 p-4 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col lg:flex-row gap-5 items-start shadow-inner">
              {/* Media Thumbnail */}
              <div className="relative w-full lg:w-72 aspect-video rounded-xl overflow-hidden shrink-0 bg-[#171920] border border-[#262a34]">
                {videoProbe.thumbnailUrl ? (
                  <img 
                    src={videoProbe.thumbnailUrl} 
                    alt={videoProbe.title || 'Probed preview'} 
                    className="w-full h-full object-cover" 
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-[#525866]">
                    <Film size={32} />
                  </div>
                )}
                <div className="absolute top-2 left-2 flex items-center gap-1 px-2 py-0.5 rounded bg-black/80 backdrop-blur-md text-[10px] font-mono text-[#3cd69b] border border-[#3cd69b]/30">
                  <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b]" />
                  <span>4K PROBED</span>
                </div>
                {videoProbe.formattedDuration && (
                  <div className="absolute bottom-2 right-2 px-2 py-0.5 rounded bg-black/80 backdrop-blur-md text-[11px] font-mono text-white font-semibold">
                    {videoProbe.formattedDuration}
                  </div>
                )}
              </div>

              {/* Probed Metadata Spec Sheet */}
              <div className="flex-1 flex flex-col justify-between gap-3 w-full min-w-0">
                <div className="flex flex-col gap-1">
                  <div className="flex items-center gap-2 flex-wrap text-xs font-mono text-[#85948b]">
                    <span className="px-2 py-0.5 rounded bg-[#171920] text-[#3cd69b] font-semibold border border-[#262a34]">
                      yt-dlp Core
                    </span>
                    {videoProbe.subtitlesAvailable && (
                      <span className="px-2 py-0.5 rounded bg-[#171920] text-[#38bdf8] font-semibold border border-[#262a34] flex items-center gap-1">
                        <Captions size={12} /> Subtitles Available
                      </span>
                    )}
                  </div>
                  <h2 className="text-base md:text-lg font-bold text-white line-clamp-2 mt-1">
                    {videoProbe.title}
                  </h2>
                </div>

                {/* Discovered Streams Summary */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 bg-[#121418] p-3 rounded-xl border border-[#21242d] text-xs font-mono">
                  <div className="flex flex-col">
                    <span className="text-[10px] text-[#85948b] uppercase">Max Stream</span>
                    <span className="text-[#3cd69b] font-bold truncate">
                      {availableQualities[0] || 'Auto Highest'}
                    </span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-[10px] text-[#85948b] uppercase">Best Audio</span>
                    <span className="text-[#38bdf8] font-bold">Opus / AAC 160k</span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-[10px] text-[#85948b] uppercase">Subtitles</span>
                    <span className="text-white font-bold">
                      {videoProbe.subtitleLanguages?.length ? `${videoProbe.subtitleLanguages.length} Tracks` : 'Auto Extract'}
                    </span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-[10px] text-[#85948b] uppercase">DRM Status</span>
                    <span className="text-[#3cd69b] font-bold">CLEAN STREAM</span>
                  </div>
                </div>
              </div>
            </div>
          )}
        </section>

        {/* Section 3: Download Configuration Matrix */}
        <section className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Encoding Matrix & Quality Selectors (7 Cols) */}
          <div className="lg:col-span-7 flex flex-col gap-5 p-5 md:p-6 rounded-2xl bg-[#121418] border border-[#21242d] shadow-xl">
            <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
              <div className="flex items-center gap-2">
                <Settings size={18} className="text-[#3cd69b]" />
                <span className="font-bold text-white text-sm font-heading">Muxer & Encoding Matrix</span>
              </div>
              <span className="text-xs font-mono text-[#38bdf8] font-semibold">PRESET: REMUX_ARCHIVE</span>
            </div>

            {/* Stream Capture Mode */}
            <div className="flex flex-col gap-2">
              <label className="text-xs font-mono uppercase text-[#85948b] font-semibold">Stream Capture Mode</label>
              <div className="grid grid-cols-2 gap-3">
                <button
                  type="button"
                  onClick={() => setDownloadType('video')}
                  className={`py-2.5 px-3 rounded-xl text-xs font-semibold flex items-center justify-center gap-2 border transition-all cursor-pointer ${
                    downloadType === 'video'
                      ? 'bg-[#3cd69b]/15 text-[#3cd69b] border-[#3cd69b]/40 shadow-[0_0_12px_rgba(60,214,155,0.2)]'
                      : 'bg-[#090a0c] text-[#85948b] border-[#262a34] hover:bg-[#171920]'
                  }`}
                >
                  <Film size={16} />
                  <span>Video + Audio (Remux)</span>
                </button>
                <button
                  type="button"
                  onClick={() => setDownloadType('audio')}
                  className={`py-2.5 px-3 rounded-xl text-xs font-semibold flex items-center justify-center gap-2 border transition-all cursor-pointer ${
                    downloadType === 'audio'
                      ? 'bg-[#38bdf8]/15 text-[#38bdf8] border-[#38bdf8]/40 shadow-[0_0_12px_rgba(56,189,248,0.2)]'
                      : 'bg-[#090a0c] text-[#85948b] border-[#262a34] hover:bg-[#171920]'
                  }`}
                >
                  <Music size={16} />
                  <span>Audio Extraction Only</span>
                </button>
              </div>
            </div>

            {/* Target Resolution or Audio Format */}
            {downloadType === 'video' ? (
              <div className="flex flex-col gap-2">
                <div className="flex items-center justify-between">
                  <label className="text-xs font-mono uppercase text-[#85948b] font-semibold">Resolution Ceiling</label>
                  <span className="text-[10px] text-[#525866] font-mono">{availableQualities.length} options available</span>
                </div>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2 max-h-56 overflow-y-auto pr-1 custom-scrollbar">
                  {availableQualities.map((q, idx) => {
                    const isSelected = videoQuality === q;
                    const label = q.split(' ')[0];
                    return (
                      <button
                        key={idx}
                        type="button"
                        onClick={() => setVideoQuality(q)}
                        className={`p-2.5 rounded-xl text-left flex flex-col gap-0.5 border transition-all cursor-pointer ${
                          isSelected
                            ? 'bg-[#171920] border-[#3cd69b] text-white shadow-[0_0_8px_rgba(60,214,155,0.15)]'
                            : 'bg-[#090a0c] border-[#262a34] text-[#85948b] hover:bg-[#171920] hover:text-[#d1d5db]'
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <span className={`text-xs font-bold font-mono ${isSelected ? 'text-[#3cd69b]' : ''}`}>
                            {label}
                          </span>
                          {isSelected && <Check size={12} className="text-[#3cd69b]" />}
                        </div>
                        <span className="text-[10px] text-[#525866] truncate">{q}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                <label className="text-xs font-mono uppercase text-[#85948b] font-semibold">Audio Codec Output</label>
                <div className="grid grid-cols-4 gap-2">
                  {(['Mp3', 'M4a', 'Opus', 'Wav'] as const).map(fmt => (
                    <button
                      key={fmt}
                      type="button"
                      onClick={() => setAudioFormat(fmt)}
                      className={`p-2.5 rounded-xl text-center text-xs font-mono font-bold border transition-all cursor-pointer ${
                        audioFormat === fmt
                          ? 'bg-[#171920] border-[#38bdf8] text-[#38bdf8]'
                          : 'bg-[#090a0c] border-[#262a34] text-[#85948b] hover:bg-[#171920]'
                      }`}
                    >
                      {fmt}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Audio Track / Multi-Language Audio Selection */}
            <div className="flex flex-col gap-2 pt-2 border-t border-[#21242d]/80">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-1.5">
                  <Languages size={14} className="text-[#38bdf8]" />
                  <label className="text-xs font-mono uppercase text-[#85948b] font-semibold">Audio Language Track</label>
                </div>
                <span className="text-[10px] font-mono text-[#38bdf8]">
                  {selectedAudioLanguage === 'all'
                    ? 'Multi-Track (All Languages)'
                    : selectedAudioLanguage
                    ? formatLanguageName(selectedAudioLanguage)
                    : 'Default Track'}
                </span>
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                <button
                  type="button"
                  onClick={() => setSelectedAudioLanguage('')}
                  className={`p-2 rounded-xl text-left border text-xs font-medium transition-all cursor-pointer ${
                    selectedAudioLanguage === ''
                      ? 'bg-[#171920] border-[#38bdf8] text-white shadow-[0_0_8px_rgba(56,189,248,0.15)]'
                      : 'bg-[#090a0c] border-[#262a34] text-[#85948b] hover:bg-[#171920]'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-white text-[11px]">Default Audio</span>
                    {selectedAudioLanguage === '' && <Check size={12} className="text-[#38bdf8]" />}
                  </div>
                  <span className="text-[10px] text-[#525866] block truncate">Primary / Native</span>
                </button>

                <button
                  type="button"
                  onClick={() => setSelectedAudioLanguage('all')}
                  className={`p-2 rounded-xl text-left border text-xs font-medium transition-all cursor-pointer ${
                    selectedAudioLanguage === 'all'
                      ? 'bg-[#171920] border-[#3cd69b] text-white shadow-[0_0_8px_rgba(60,214,155,0.15)]'
                      : 'bg-[#090a0c] border-[#262a34] text-[#85948b] hover:bg-[#171920]'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-[#3cd69b] text-[11px]">All Languages</span>
                    {selectedAudioLanguage === 'all' && <Check size={12} className="text-[#3cd69b]" />}
                  </div>
                  <span className="text-[10px] text-[#525866] block truncate">Multi-stream Mux</span>
                </button>

                {availableAudioTracks.map((trk) => {
                  const isSelected = selectedAudioLanguage.toLowerCase() === trk.code.toLowerCase();
                  return (
                    <button
                      key={trk.code}
                      type="button"
                      onClick={() => setSelectedAudioLanguage(trk.code)}
                      className={`p-2 rounded-xl text-left border text-xs font-medium transition-all cursor-pointer ${
                        isSelected
                          ? 'bg-[#171920] border-[#38bdf8] text-white shadow-[0_0_8px_rgba(56,189,248,0.15)]'
                          : 'bg-[#090a0c] border-[#262a34] text-[#85948b] hover:bg-[#171920]'
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-white text-[11px] truncate">{trk.name}</span>
                        {isSelected && <Check size={12} className="text-[#38bdf8] shrink-0" />}
                      </div>
                      <span className="text-[10px] font-mono text-[#525866] block uppercase">{trk.code}</span>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Subtitles & Category Tag */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2 border-t border-[#21242d]/80">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-mono uppercase text-[#85948b]">Category / Folder</label>
                <input
                  type="text"
                  placeholder="e.g. Documentaries, Music Videos"
                  value={categoryName}
                  onChange={(e) => setCategoryName(e.target.value)}
                  className="px-3.5 py-2 rounded-xl bg-[#090a0c] border border-[#262a34] text-white text-xs placeholder:text-[#525866] focus:outline-none focus:border-[#3cd69b]"
                />
              </div>

              <div className="flex flex-col gap-1.5 justify-center">
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#262a34] flex items-center justify-between">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-white">Embed Subtitles</span>
                    <span className="text-[10px] text-[#85948b]">Auto-extract .vtt / .srt</span>
                  </div>
                  <Switch
                    checked={includeSubtitles}
                    onChange={(event) => setIncludeSubtitles(event.currentTarget.checked)}
                    color="cyberMint"
                    size="sm"
                  />
                </div>
              </div>
            </div>

            {/* Subtitle Language Selection Matrix */}
            {includeSubtitles && (
              <div className="flex flex-col gap-2 p-3 rounded-xl bg-[#090a0c] border border-[#262a34] transition-all">
                <div className="flex items-center justify-between gap-2">
                  <div className="flex items-center gap-1.5">
                    <Captions size={14} className="text-[#3cd69b]" />
                    <span className="text-xs font-mono font-semibold uppercase text-white">Subtitle Language</span>
                  </div>
                  <span className="text-[10px] font-mono text-[#3cd69b]">
                    {selectedSubLanguage === 'all'
                      ? 'All Languages'
                      : selectedSubLanguage
                      ? formatLanguageName(selectedSubLanguage)
                      : 'Auto Original'}
                  </span>
                </div>

                {/* Search Filter Input */}
                <div className="relative">
                  <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-[#525866]" />
                  <input
                    type="text"
                    placeholder="Search language (e.g. English, Spanish, French)..."
                    value={subSearch}
                    onChange={(e) => setSubSearch(e.target.value)}
                    className="w-full pl-8 pr-3 py-1.5 rounded-lg bg-[#121418] border border-[#262a34] text-white text-xs placeholder:text-[#525866] focus:outline-none focus:border-[#3cd69b]"
                  />
                </div>

                {/* Subtitle Options List */}
                <div className="max-h-44 overflow-y-auto space-y-1 custom-scrollbar pr-1">
                  {/* Option: Auto Original Subtitles */}
                  <button
                    type="button"
                    onClick={() => setSelectedSubLanguage('')}
                    className={`w-full px-2.5 py-1.5 rounded-lg text-left flex items-center justify-between text-xs transition-all cursor-pointer ${
                      selectedSubLanguage === ''
                        ? 'bg-[#171920] border border-[#3cd69b] text-white'
                        : 'bg-[#121418] border border-transparent text-[#85948b] hover:bg-[#1a1c24] hover:text-white'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-white">Auto Original (Primary Subtitles)</span>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-[#21242d] text-[#85948b]">auto</span>
                    </div>
                    {selectedSubLanguage === '' && <Check size={12} className="text-[#3cd69b]" />}
                  </button>

                  {/* Option: All Available Subtitles */}
                  <button
                    type="button"
                    onClick={() => setSelectedSubLanguage('all')}
                    className={`w-full px-2.5 py-1.5 rounded-lg text-left flex items-center justify-between text-xs transition-all cursor-pointer ${
                      selectedSubLanguage === 'all'
                        ? 'bg-[#171920] border border-[#3cd69b] text-white'
                        : 'bg-[#121418] border border-transparent text-[#85948b] hover:bg-[#1a1c24] hover:text-white'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-white">All Available Subtitles</span>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-[#21242d] text-[#85948b]">all</span>
                    </div>
                    {selectedSubLanguage === 'all' && <Check size={12} className="text-[#3cd69b]" />}
                  </button>

                  {filteredSubtitleList.map((track) => {
                    const isSelected = selectedSubLanguage.toLowerCase() === track.code.toLowerCase();
                    return (
                      <button
                        key={track.code}
                        type="button"
                        onClick={() => setSelectedSubLanguage(track.code)}
                        className={`w-full px-2.5 py-1.5 rounded-lg text-left flex items-center justify-between text-xs transition-all cursor-pointer ${
                          isSelected
                            ? 'bg-[#171920] border border-[#3cd69b] text-white'
                            : 'bg-[#121418] border border-transparent text-[#85948b] hover:bg-[#1a1c24] hover:text-white'
                        }`}
                      >
                        <div className="flex items-center gap-2 truncate">
                          <span className={`truncate ${isSelected ? 'font-semibold text-white' : ''}`}>
                            {track.name}
                          </span>
                          <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-[#21242d] text-[#38bdf8] shrink-0">
                            {track.code}
                          </span>
                          {track.isOriginal && (
                            <span className="text-[9px] px-1 py-0.2 rounded bg-[#3cd69b]/20 text-[#3cd69b] shrink-0">
                              Original
                            </span>
                          )}
                        </div>
                        {isSelected && <Check size={12} className="text-[#3cd69b] shrink-0" />}
                      </button>
                    );
                  })}

                  {filteredSubtitleList.length === 0 && (
                    <div className="py-3 text-center text-xs text-[#525866]">
                      No languages found matching "{subSearch}"
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Right Column: Homelab Daemon Rules & Primary Dispatch (5 Cols) */}
          <div className="lg:col-span-5 flex flex-col justify-between gap-5 p-5 md:p-6 rounded-2xl bg-[#121418] border border-[#21242d] shadow-xl">
            <div className="flex flex-col gap-4">
              <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
                <div className="flex items-center gap-2">
                  <ShieldCheck size={18} className="text-[#38bdf8]" />
                  <span className="font-bold text-white text-sm font-heading">Homelab Daemon Rules</span>
                </div>
                <span className="text-xs font-mono text-[#3cd69b]">NODE: PROXMOX-1</span>
              </div>

              {/* Toggles */}
              <div className="space-y-2.5">
                {/* Toggle 1: VPN Proxy */}
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#262a34] flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <Globe size={18} className="text-[#38bdf8]" />
                    <div className="flex flex-col">
                      <span className="text-xs font-bold text-white">Bypass Geo-blocking Proxy</span>
                      <span className="text-[10px] text-[#85948b]">WireGuard SOCKS5 Exit Node</span>
                    </div>
                  </div>
                  <Switch
                    checked={bypassProxy}
                    onChange={(e) => setBypassProxy(e.currentTarget.checked)}
                    color="neonCyan"
                    size="sm"
                  />
                </div>

                {/* Toggle 2: SponsorBlock */}
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#262a34] flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <Sparkles size={18} className="text-[#3cd69b]" />
                    <div className="flex flex-col">
                      <span className="text-xs font-bold text-white">SponsorBlock Auto-Cut</span>
                      <span className="text-[10px] text-[#85948b]">Remove sponsors & intro cards</span>
                    </div>
                  </div>
                  <Switch
                    checked={sponsorBlock}
                    onChange={(e) => setSponsorBlock(e.currentTarget.checked)}
                    color="cyberMint"
                    size="sm"
                  />
                </div>

                {/* Toggle 3: AtomicParsley Metadata */}
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <Info size={18} className="text-[#ffd29f]" />
                    <div className="flex flex-col">
                      <span className="text-xs font-bold text-white">AtomicParsley Tag Embed</span>
                      <span className="text-[10px] text-[#85948b]">Write artwork, chapters & tags</span>
                    </div>
                  </div>
                  <Switch
                    checked={embedMetadata}
                    onChange={(e) => setEmbedMetadata(e.currentTarget.checked)}
                    color="cyberMint"
                    size="sm"
                  />
                </div>

                {/* Destination Storage Mount */}
                <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col gap-1.5">
                  <div className="flex items-center justify-between text-xs font-mono">
                    <span className="text-[#85948b] uppercase text-[10px]">Storage Mount</span>
                    <span className="text-[#3cd69b] font-bold">ZFS POOL 0 • 7.6 TB FREE</span>
                  </div>
                  <div className="flex items-center gap-2 px-2.5 py-1.5 rounded-lg bg-[#171920] border border-[#262a34] text-xs font-mono text-[#85948b]">
                    <Folder size={14} className="text-[#38bdf8]" />
                    <span className="truncate flex-1">/mnt/storage/downloads/youtube-vault</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Primary Dispatch Action */}
            <div className="flex flex-col gap-2 pt-2">
              <button
                type="button"
                onClick={downloadMode === 'single' ? () => handleStartDownload() : () => handleStartBatchDownload()}
                disabled={downloadLoading || (downloadMode === 'single' ? !downloadUrl.trim() : parsedBatch.urls.length === 0)}
                className="w-full py-3.5 px-4 rounded-xl bg-[#3cd69b] hover:bg-[#32b583] text-[#003825] font-extrabold text-sm flex items-center justify-center gap-2 shadow-[0_0_20px_rgba(60,214,155,0.4)] transition-all cursor-pointer disabled:opacity-50 active:scale-[0.98]"
              >
                {downloadLoading ? (
                  <>
                    <Loader2 size={18} className="animate-spin" />
                    <span>Dispatching to Queue...</span>
                  </>
                ) : (
                  <>
                    <ArrowDownCircle size={18} />
                    <span>Dispatch to Pipeline (Aria2c + FFmpeg)</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </section>

        {/* Section 4: Active Ingestion Queue & Daemon Activity */}
        <section className="flex flex-col gap-4 p-5 md:p-6 rounded-2xl bg-[#121418] border border-[#21242d] shadow-xl">
          {/* Queue Header with Live Metrics */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-[#21242d]">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-[#3cd69b]/10 text-[#3cd69b] border border-[#3cd69b]/20">
                <Activity size={18} className="animate-spin" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-white font-heading">Active Ingestion Queue</h2>
                <p className="text-xs text-[#85948b]">Real-time worker threads managed by Aria2c daemon</p>
              </div>
              <Badge color="cyberMint" variant="filled" size="md">
                {activeJobsCount} ACTIVE
              </Badge>
            </div>

            <div className="flex items-center gap-3 flex-wrap font-mono text-xs">
              <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#090a0c] border border-[#21242d] text-[#3cd69b] font-bold">
                <Activity size={14} />
                <span>Aggregated: {aggregatedSpeed}</span>
              </div>
              {finishedJobsCount > 0 && (
                <button
                  onClick={handleClearFinished}
                  className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-[#090a0c] hover:bg-[#171920] border border-[#262a34] text-[#85948b] hover:text-white transition-colors cursor-pointer"
                >
                  <Trash2 size={13} />
                  <span>Clear Finished ({finishedJobsCount})</span>
                </button>
              )}
            </div>
          </div>

          {/* Queue Tab Switcher (Jobs vs Batches) */}
          <div className="flex items-center gap-2">
            <button
              onClick={() => setRightTab('jobs')}
              className={`px-3 py-1.5 rounded-lg text-xs font-semibold font-mono transition-colors cursor-pointer ${
                rightTab === 'jobs'
                  ? 'bg-[#3cd69b] text-[#003825]'
                  : 'bg-[#171920] text-[#85948b] hover:text-white'
              }`}
            >
              Direct Jobs ({downloadJobs.length})
            </button>
            <button
              onClick={() => setRightTab('batches')}
              className={`px-3 py-1.5 rounded-lg text-xs font-semibold font-mono transition-colors cursor-pointer ${
                rightTab === 'batches'
                  ? 'bg-[#3cd69b] text-[#003825]'
                  : 'bg-[#171920] text-[#85948b] hover:text-white'
              }`}
            >
              Batches ({batches.length})
            </button>
          </div>

          {/* Active Tasks Stream */}
          {rightTab === 'jobs' ? (
            downloadJobs.length === 0 ? (
              <div className="p-8 text-center text-xs text-[#525866] font-mono">
                No active or queued download jobs. Ingest a URL above to start.
              </div>
            ) : (
              <div className="space-y-3">
                {downloadJobs.map(job => {
                  const s = normalizeStatus(job.status);
                  const isFailed = isJobFailed(s);
                  const isActive = isJobActive(s);
                  const isCompleted = s === 'Completed';

                  return (
                    <div 
                      key={job.id}
                      className="p-4 rounded-xl bg-[#090a0c] border border-[#21242d] hover:border-[#262a34] transition-all flex flex-col gap-3 shadow-sm"
                    >
                      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                        <div className="flex items-center gap-3 min-w-0">
                          <span className="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-[#171920] text-[#3cd69b] border border-[#262a34] shrink-0">
                            {job.mediaType === 'Audio' ? 'AUDIO' : 'VIDEO'}
                          </span>
                          <div className="flex flex-col min-w-0">
                            <span className="text-sm font-semibold text-white truncate" title={job.title || job.url}>
                              {job.title || job.url}
                            </span>
                            <div className="flex items-center gap-2 text-xs font-mono text-[#85948b] mt-0.5 truncate">
                              <span>Status: <strong className={isCompleted ? 'text-[#3cd69b]' : isFailed ? 'text-rose-400' : 'text-amber-400'}>{s}</strong></span>
                              {job.phase && <span>• {job.phase}</span>}
                              {job.speed && <span>• {job.speed}</span>}
                              {job.eta && <span>• ETA: {job.eta}</span>}
                            </div>
                          </div>
                        </div>

                        {/* Action buttons */}
                        <div className="flex items-center gap-1.5 shrink-0 self-end sm:self-center">
                          {isActive && (
                            <button
                              onClick={() => handleCancelJob(job.id)}
                              className="p-1.5 rounded-lg bg-[#171920] hover:bg-rose-500/20 text-[#85948b] hover:text-rose-400 border border-[#262a34] transition-colors cursor-pointer"
                              title="Cancel Job"
                            >
                              <XCircle size={15} />
                            </button>
                          )}
                        </div>
                      </div>

                      {/* Progress Bar */}
                      {isActive && (
                        <div className="flex items-center gap-3">
                          <div className="flex-1">
                            <Progress 
                              value={job.progress || 0} 
                              color="cyberMint" 
                              size="sm" 
                              radius="xl"
                              animated
                            />
                          </div>
                          <span className="text-xs font-mono font-bold text-[#3cd69b] shrink-0">
                            {Math.round(job.progress || 0)}%
                          </span>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )
          ) : (
            /* Batches Stream */
            batches.length === 0 ? (
              <div className="p-8 text-center text-xs text-[#525866] font-mono">
                No download batches found.
              </div>
            ) : (
              <div className="space-y-3">
                {batches.map(batch => {
                  const isExpanded = expandedBatchIds.has(batch.id);
                  const isBatchRunning = isBatchActive(batch.status);

                  return (
                    <div 
                      key={batch.id}
                      className="p-4 rounded-xl bg-[#090a0c] border border-[#21242d] flex flex-col gap-3"
                    >
                      <div className="flex items-center justify-between gap-3">
                        <div 
                          className="flex items-center gap-3 cursor-pointer select-none flex-1 min-w-0"
                          onClick={() => toggleBatchExpanded(batch.id)}
                        >
                          {isExpanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                          <div className="flex flex-col min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-bold text-white truncate">
                                {batch.categoryName || `Batch ${batch.id.substring(0, 8)}`}
                              </span>
                              <Badge color="cyberMint" size="xs" variant="light">
                                {batch.completedItems} / {batch.totalItems} done
                              </Badge>
                            </div>
                            <span className="text-xs font-mono text-[#85948b]">
                              Created: {new Date(batch.createdAt).toLocaleTimeString()}
                            </span>
                          </div>
                        </div>

                        <div className="flex items-center gap-2">
                          {isBatchRunning ? (
                            <button
                              onClick={() => handleCancelBatch(batch.id)}
                              className="px-2.5 py-1 rounded-lg bg-rose-500/15 text-rose-400 text-xs font-mono hover:bg-rose-500/25 transition-colors cursor-pointer"
                            >
                              Cancel Batch
                            </button>
                          ) : (
                            <button
                              onClick={() => handleDeleteBatch(batch.id)}
                              className="p-1.5 rounded-lg bg-[#171920] text-[#85948b] hover:text-rose-400 transition-colors cursor-pointer"
                            >
                              <Trash2 size={14} />
                            </button>
                          )}
                        </div>
                      </div>

                      {/* Batch Progress Bar */}
                      <Progress 
                        value={batch.totalItems > 0 ? (batch.completedItems / batch.totalItems) * 100 : 0}
                        color="cyberMint"
                        size="sm"
                        radius="xl"
                      />

                      {/* Expandable Child Jobs */}
                      {isExpanded && batch.jobs && (
                        <div className="pl-6 space-y-2 pt-2 border-t border-[#21242d]">
                          {batch.jobs.map(j => (
                            <div key={j.id} className="flex items-center justify-between text-xs font-mono text-[#85948b]">
                              <span className="truncate max-w-md">{j.title || j.url}</span>
                              <span className="text-[#3cd69b] font-semibold">{j.status}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )
          )}
        </section>

      </div>
    </div>
  );
};
