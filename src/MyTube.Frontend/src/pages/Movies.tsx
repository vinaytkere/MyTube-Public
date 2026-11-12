import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { 
  Film, Search, Download, HardDrive, CheckCircle2, 
  AlertTriangle, RefreshCw, Star, Clock, Layers,
  X, ExternalLink, ShieldCheck, Activity, Settings as SettingsIcon,
  ChevronRight, Zap, ArrowUpDown, Check, Shield, Trash2,
  FileText, Image as ImageIcon, File, Folder, Play, Pause, CheckSquare, Square,
  Laptop, Smartphone, ChevronDown, ChevronUp, FileCheck, Music
} from 'lucide-react';
import { api } from '../services/api';
import { 
  RadarrMovie, RadarrRelease, RadarrQueueItem, RadarrStatus, ProwlarrRelease,
  RadarrDownloadSummary
} from '../types';
import { useToast } from '../components/Toast';

// Hoisted static popular pills lookup (rendering-hoist-jsx)
const POPULAR_PILLS: Record<string, string[]> = {
  all: ['Inception', 'Dune: Part Two', 'Deadpool', 'Oppenheimer', 'Interstellar', 'Ubuntu 24.04'],
  movies: ['Dune: Part Two', 'Inception', 'Deadpool & Wolverine', 'Oppenheimer', 'Gladiator II', 'Interstellar', 'The Dark Knight'],
  tv: ['Fallout', 'Shogun', 'Breaking Bad', 'Game of Thrones', 'The Last of Us', 'Severance'],
  software: ['Ubuntu 24.04', 'Windows 11', 'Blender', 'Visual Studio Code', 'VLC Media Player'],
  books: ['Clean Code', 'Python Programming', 'Designing Data-Intensive Applications', 'Pragmatic Programmer'],
  music: ['Hans Zimmer', 'Daft Punk', 'Pink Floyd', 'Taylor Swift', 'Coldplay'],
  other: ['Linux ISO', 'Wallpapers 4K', 'Sample Audio FLAC']
};

export const Movies: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { showToast } = useToast();

  // Derive activeTab directly from searchParams (rerender-derived-state-no-effect)
  const tabParam = searchParams.get('tab');
  const activeTab: 'search' | 'library' | 'queue' =
    tabParam === 'library' || tabParam === 'queue' ? tabParam : 'search';

  const setActiveTab = (tab: 'search' | 'library' | 'queue') => {
    setSearchParams(prev => {
      const next = new URLSearchParams(prev);
      next.set('tab', tab);
      return next;
    });
  };

  useEffect(() => {
    if (tabParam === 'organizer') {
      navigate('/organizer', { replace: true });
    }
  }, [tabParam, navigate]);

  const [searchView, setSearchView] = useState<'trackers' | 'catalog'>('trackers');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [expandedQueueIds, setExpandedQueueIds] = useState<Set<number>>(new Set());
  const [status, setStatus] = useState<RadarrStatus | null>(null);
  const [loadingStatus, setLoadingStatus] = useState(true);

  const popularPills = POPULAR_PILLS;


  // Movie Data
  const [searchResults, setSearchResults] = useState<RadarrMovie[]>([]);
  const [indexerReleases, setIndexerReleases] = useState<ProwlarrRelease[]>([]);
  const [libraryMovies, setLibraryMovies] = useState<RadarrMovie[]>([]);
  const [queue, setQueue] = useState<RadarrQueueItem[]>([]);
  const [loading, setLoading] = useState(false);

  // Unified Download Hub & File Management State
  const [downloadSummary, setDownloadSummary] = useState<RadarrDownloadSummary | null>(null);
  const [libraryFilter, setLibraryFilter] = useState<'downloaded' | 'all'>('downloaded');
  const [selectedMovieIds, setSelectedMovieIds] = useState<Set<number>>(new Set());
  const [isSyncingLinks, setIsSyncingLinks] = useState(false);
  const [isDeletingAll, setIsDeletingAll] = useState(false);
  const [isDeletingSelected, setIsDeletingSelected] = useState(false);
  const [deletingMovieId, setDeletingMovieId] = useState<number | null>(null);
  const [showDeleteAllModal, setShowDeleteAllModal] = useState(false);
  const [showDeleteSelectedModal, setShowDeleteSelectedModal] = useState(false);
  const [expandedMovieId, setExpandedMovieId] = useState<number | null>(null);

  // Tracker Releases Filter & Sort
  const [onlyActiveSeeders, setOnlyActiveSeeders] = useState(true);
  const [selectedTracker, setSelectedTracker] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'seeders' | 'size_desc' | 'size_asc' | 'title'>('seeders');
  const [releaseFilterText, setReleaseFilterText] = useState('');

  // Releases Modal State (for Radarr catalog items)
  const [selectedMovieForReleases, setSelectedMovieForReleases] = useState<RadarrMovie | null>(null);
  const [releases, setReleases] = useState<RadarrRelease[]>([]);
  const [loadingReleases, setLoadingReleases] = useState(false);
  const [downloadingGuid, setDownloadingGuid] = useState<string | null>(null);

  // Prowlarr Download States
  const [downloadingReleases, setDownloadingReleases] = useState<Record<string, boolean>>({});
  const [downloadedGuids, setDownloadedGuids] = useState<Set<string>>(new Set());

  // Quick Action Loading
  const [actionMovieId, setActionMovieId] = useState<number | null>(null);
  const [cancellingId, setCancellingId] = useState<number | null>(null);
  const [pausingId, setPausingId] = useState<number | null>(null);
  const [resumingId, setResumingId] = useState<number | null>(null);
  const [kickstartingId, setKickstartingId] = useState<number | null>(null);

  // Check Radarr Connection
  const checkStatus = useCallback(async () => {
    try {
      setLoadingStatus(true);
      const res = await api.getRadarrStatus();
      setStatus(res);
    } catch (err: any) {
      setStatus({
        isConfigured: false,
        isConnected: false,
        errorMessage: err.message || 'Unable to connect to Radarr'
      });
    } finally {
      setLoadingStatus(false);
    }
  }, []);

  useEffect(() => {
    checkStatus();
  }, [checkStatus]);

  // Load Library Movies & Download Summary
  const loadLibrary = useCallback(async () => {
    if (!status?.isConnected) return;
    try {
      setLoading(true);
      const [moviesRes, summaryRes] = await Promise.allSettled([
        api.getRadarrLibrary(),
        api.getRadarrDownloadSummary()
      ]);
      if (moviesRes.status === 'fulfilled') {
        setLibraryMovies(moviesRes.value);
      }
      if (summaryRes.status === 'fulfilled') {
        setDownloadSummary(summaryRes.value);
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to fetch movie library');
    } finally {
      setLoading(false);
    }
  }, [status?.isConnected, showToast]);

  const downloadedMovies = useMemo(() => {
    return libraryMovies.filter(m => m.hasFile);
  }, [libraryMovies]);

  const allDownloadedSelected = useMemo(() => {
    if (downloadedMovies.length === 0) return false;
    return downloadedMovies.every(m => selectedMovieIds.has(m.id));
  }, [downloadedMovies, selectedMovieIds]);

  const handleToggleSelectAll = () => {
    if (allDownloadedSelected) {
      setSelectedMovieIds(new Set());
    } else {
      const next = new Set<number>();
      downloadedMovies.forEach(m => next.add(m.id));
      setSelectedMovieIds(next);
    }
  };

  const handleToggleMovieSelect = (id: number) => {
    setSelectedMovieIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleSyncLinks = async () => {
    try {
      setIsSyncingLinks(true);
      showToast('info', 'Synchronizing media links into separate non-deletable folder...');
      const res = await api.syncRadarrLinks();
      if (res.success) {
        showToast('success', res.message || 'Links synchronized successfully');
        loadLibrary();
      } else {
        showToast('error', res.message || 'Sync failed');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to sync links');
    } finally {
      setIsSyncingLinks(false);
    }
  };

  const handleDeleteMovieFiles = async (movieId: number, movieTitle: string) => {
    if (!window.confirm(`Are you sure you want to delete all downloaded files for "${movieTitle}"? This will free up storage on disk.`)) {
      return;
    }
    try {
      setDeletingMovieId(movieId);
      const res = await api.deleteRadarrMovieFiles(movieId);
      if (res.success) {
        showToast('success', `Deleted downloaded files for "${movieTitle}"`);
        setSelectedMovieIds(prev => {
          const next = new Set(prev);
          next.delete(movieId);
          return next;
        });
        loadLibrary();
      } else {
        showToast('error', res.message || 'Failed to delete movie files');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error deleting movie files');
    } finally {
      setDeletingMovieId(null);
    }
  };

  const handleDeleteSelected = async () => {
    const movieIds = Array.from(selectedMovieIds);
    if (movieIds.length === 0) return;

    try {
      setIsDeletingSelected(true);
      const res = await api.batchDeleteRadarrFiles(movieIds);
      if (res.success) {
        showToast('success', res.message || `Deleted ${res.deletedCount} items.`);
        setSelectedMovieIds(new Set());
        setShowDeleteSelectedModal(false);
        loadLibrary();
      } else {
        showToast('error', res.message || 'Batch delete failed');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to delete selected items');
    } finally {
      setIsDeletingSelected(false);
    }
  };

  const handleDeleteAllDownloaded = async () => {
    try {
      setIsDeletingAll(true);
      const res = await api.deleteAllRadarrDownloads();
      if (res.success) {
        showToast('success', res.message || `Deleted all downloaded files.`);
        setSelectedMovieIds(new Set());
        setShowDeleteAllModal(false);
        loadLibrary();
      } else {
        showToast('error', res.message || 'Failed to delete all downloaded files');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to delete all downloaded files');
    } finally {
      setIsDeletingAll(false);
    }
  };

  // Load Active Queue
  const loadQueue = useCallback(async () => {
    if (!status?.isConnected) return;
    try {
      const data = await api.getRadarrQueue();
      setQueue(data);
    } catch (err: any) {
      console.error('Failed to load queue:', err);
    }
  }, [status?.isConnected]);

  // Tab Activation & Background Polling (with document.hidden guard)
  useEffect(() => {
    if (activeTab === 'library') {
      loadLibrary();
    } else if (activeTab === 'queue') {
      loadQueue();
      if (status?.isConnected) {
        const interval = setInterval(() => {
          if (!document.hidden) {
            loadQueue();
          }
        }, 2000);
        return () => clearInterval(interval);
      }
    }
  }, [activeTab, status?.isConnected, loadLibrary, loadQueue]);

  const toggleQueueItemFiles = (id: number) => {
    setExpandedQueueIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  // Perform Concurrent Search (Indexers 100+ releases + Radarr Movie Catalog)
  const handleSearch = async (e?: React.FormEvent, customQuery?: string, customCategory?: string) => {
    if (e) e.preventDefault();
    const query = (customQuery !== undefined ? customQuery : searchQuery).trim();
    if (!query) return;
    const cat = customCategory !== undefined ? customCategory : selectedCategory;

    try {
      setLoading(true);
      setActiveTab('search');

      const isNonMovie = cat === 'software' || cat === 'books' || cat === 'music' || cat === 'other' || cat === 'games';

      const [indexersPromise, radarrPromise] = await Promise.allSettled([
        api.searchIndexerReleases(query, cat),
        isNonMovie ? Promise.resolve([]) : api.searchRadarrMovies(query)
      ]);

      let releasesCount = 0;
      if (indexersPromise.status === 'fulfilled') {
        setIndexerReleases(indexersPromise.value);
        releasesCount = indexersPromise.value.length;
      } else {
        console.warn('Indexer search error:', indexersPromise.reason);
        setIndexerReleases([]);
        const reasonMsg = (indexersPromise.reason as any)?.message || 'Could not reach Prowlarr indexers';
        showToast('warning', `Torrent search: ${reasonMsg}`);
      }

      let radarrCount = 0;
      if (radarrPromise.status === 'fulfilled') {
        setSearchResults(radarrPromise.value);
        radarrCount = radarrPromise.value.length;
      } else {
        console.warn('Radarr search error:', radarrPromise.reason);
        setSearchResults([]);
      }

      const isGenericMovieQuery = ['movies', 'movie', 'film', 'films', 'popular', 'trending'].includes(query.toLowerCase());

      if (isGenericMovieQuery && radarrCount > 0) {
        setSearchView('catalog');
        showToast('info', `Found ${radarrCount} movies in catalog! Click any movie to view releases & quality options.`);
      } else if (releasesCount > 0) {
        setSearchView('trackers');
        showToast('success', `Found ${releasesCount} releases from active indexers!`);
      } else if (radarrCount > 0) {
        setSearchView('catalog');
        showToast('info', `Found ${radarrCount} movies in catalog. Click any movie or search a specific movie title to find torrents.`);
      } else {
        showToast('info', `No releases found for "${query}". Try searching a movie title like "Inception" or "Dune".`);
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error executing search');
    } finally {
      setLoading(false);
    }
  };

  // Auto-search from URL query parameters (e.g. /movies?query=...&category=...)
  useEffect(() => {
    const q = searchParams.get('query');
    const cat = searchParams.get('category');
    if (cat) {
      setSelectedCategory(cat);
    }
    if (q && q.trim()) {
      setSearchQuery(q.trim());
      handleSearch(undefined, q.trim(), cat || 'all');
    }
  }, []);

  // 1-Click Download direct to Transmission via Gluetun VPN
  const handleDownloadIndexerRelease = async (release: ProwlarrRelease) => {
    try {
      setDownloadingReleases(prev => ({ ...prev, [release.guid]: true }));
      const res = await api.downloadIndexerRelease(release.guid, release.title, release.downloadUrl, release.magnetUrl);
      if (res.success) {
        showToast('success', `Sent "${release.title.length > 45 ? release.title.slice(0, 42) + '...' : release.title}" to Transmission via Gluetun VPN!`);
        setDownloadedGuids(prev => new Set(prev).add(release.guid));
        await loadQueue();
      } else {
        showToast('error', res.message || 'Failed to dispatch torrent to Transmission');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to start release download');
    } finally {
      setDownloadingReleases(prev => ({ ...prev, [release.guid]: false }));
    }
  };

  // Quick Download / Auto-Grab via Radarr & Trackers
  const handleQuickDownload = async (movie: RadarrMovie) => {
    try {
      setActionMovieId(movie.tmdbId);
      showToast('info', `Auto-searching highest quality release for "${movie.title}"...`);
      const res = await api.autoGrabRadarrMovie({
        tmdbId: movie.tmdbId,
        title: movie.title,
        year: movie.year,
        searchForMovie: true,
        posterUrl: movie.posterUrl
      });

      if (res.grabbed) {
        showToast('success', res.message || `Started download for "${movie.title}" in Transmission!`);
        // Trigger sequential queue loads to catch the new download item quickly
        loadQueue();
        setTimeout(loadQueue, 1500);
        setTimeout(loadQueue, 3500);
        setTimeout(loadQueue, 6000);
      } else {
        showToast('warning', res.message || `No active torrent releases found for "${movie.title}".`);
      }

      setSearchResults(prev => prev.map(m => m.tmdbId === movie.tmdbId ? { ...m, monitored: true, id: res.movie?.id || m.id } : m));
    } catch (err: any) {
      showToast('error', err.message || 'Failed to auto-grab release for movie');
    } finally {
      setActionMovieId(null);
    }
  };

  // Open Releases Modal for Radarr movie
  const handleOpenReleases = async (movie: RadarrMovie) => {
    setSelectedMovieForReleases(movie);
    setReleases([]);
    setLoadingReleases(true);

    try {
      let radarrMovieId = movie.id;
      if (!radarrMovieId || radarrMovieId === 0) {
        showToast('info', `Locating release options for "${movie.title}"...`);
        const added = await api.addRadarrMovie({
          tmdbId: movie.tmdbId,
          title: movie.title,
          year: movie.year,
          searchForMovie: false,
          posterUrl: movie.posterUrl
        });
        radarrMovieId = added.id;
        setSelectedMovieForReleases(prev => prev ? { ...prev, id: added.id } : null);
      }

      const foundReleases = await api.getRadarrReleases(radarrMovieId);
      setReleases(foundReleases);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to search releases for movie');
    } finally {
      setLoadingReleases(false);
    }
  };

  // Download specific release from Radarr modal
  const handleDownloadRelease = async (release: RadarrRelease) => {
    try {
      setDownloadingGuid(release.guid);
      const res = await api.downloadRadarrRelease(release.guid, release.indexerId);
      if (res.success) {
        showToast('success', 'Release sent to download client!');
        loadQueue();
      } else {
        showToast('error', res.message || 'Release download was rejected');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to start release download');
    } finally {
      setDownloadingGuid(null);
    }
  };

  // Cancel / Delete a download from Transmission and Radarr
  const handleCancelDownload = async (item: RadarrQueueItem) => {
    const targetKey = item.id || item.transmissionId || 0;
    try {
      setCancellingId(targetKey);
      const res = await api.cancelRadarrQueueItem(item.id || 0, item.transmissionId, true);
      if (res.success) {
        showToast('success', `Cancelled download: "${item.movieTitle || item.releaseTitle}"`);
        setQueue(prev => prev.filter(q => {
          if (item.id && q.id === item.id) return false;
          if (item.transmissionId && q.transmissionId === item.transmissionId) return false;
          return true;
        }));
        loadQueue();
      } else {
        showToast('error', res.message || 'Failed to cancel download');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error cancelling download');
    } finally {
      setCancellingId(null);
    }
  };

  // Pause a download in Transmission (preserves downloaded blocks)
  const handlePauseDownload = async (item: RadarrQueueItem) => {
    const targetKey = item.transmissionId || item.id || 0;
    try {
      setPausingId(targetKey);
      const res = await api.pauseRadarrQueueItem(item.id || 0, item.transmissionId);
      if (res.success) {
        showToast('success', `Paused download: "${item.movieTitle || item.releaseTitle}"`);
        setQueue(prev => prev.map(q => {
          if ((item.id && q.id === item.id) || (item.transmissionId && q.transmissionId === item.transmissionId)) {
            return { ...q, status: 'paused', downloadSpeed: 0 };
          }
          return q;
        }));
        loadQueue();
      } else {
        showToast('error', res.message || 'Failed to pause download');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error pausing download');
    } finally {
      setPausingId(null);
    }
  };

  // Resume a download in Transmission (continues from previous progress)
  const handleResumeDownload = async (item: RadarrQueueItem) => {
    const targetKey = item.transmissionId || item.id || 0;
    try {
      setResumingId(targetKey);
      const res = await api.resumeRadarrQueueItem(item.id || 0, item.transmissionId);
      if (res.success) {
        showToast('success', `Resumed download: "${item.movieTitle || item.releaseTitle}"`);
        setQueue(prev => prev.map(q => {
          if ((item.id && q.id === item.id) || (item.transmissionId && q.transmissionId === item.transmissionId)) {
            return { ...q, status: 'downloading' };
          }
          return q;
        }));
        loadQueue();
      } else {
        showToast('error', res.message || 'Failed to resume download');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error resuming download');
    } finally {
      setResumingId(null);
    }
  };

  // Re-announce / Kickstart stuck torrent with tier-1 public trackers
  const handleKickstartDownload = async (item: RadarrQueueItem) => {
    const targetKey = item.transmissionId || item.id || 0;
    try {
      setKickstartingId(targetKey);
      const res = await api.kickstartRadarrQueueItem(item.id || 0, item.transmissionId);
      if (res.success) {
        showToast('success', `Re-announced "${item.movieTitle || item.releaseTitle}" to 15+ tier-1 trackers!`);
        await loadQueue();
      } else {
        showToast('error', res.message || 'Failed to re-announce torrent');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Error re-announcing torrent');
    } finally {
      setKickstartingId(null);
    }
  };

  // Switch to tracker search for specific movie title
  const handleSearchTrackersForMovie = (title: string) => {
    setSearchQuery(title);
    handleSearch(undefined, title);
  };

  // Find alternative healthy release for a stalled or dead torrent
  const handleFindAlternativeRelease = (item: RadarrQueueItem) => {
    const raw = item.movieTitle || item.releaseTitle || '';
    const clean = raw
      .replace(/\.(20\d\d)\..*$/i, ' $1')
      .replace(/\[.*?\]/g, '')
      .replace(/[._]/g, ' ')
      .trim();
    const query = clean || raw;
    setActiveTab('search');
    setSearchView('trackers');
    setSearchQuery(query);
    handleSearch(undefined, query, 'all');
  };

  // Extract unique tracker names from current releases
  const availableTrackers = useMemo(() => {
    const set = new Set<string>();
    indexerReleases.forEach(r => {
      if (r.indexer) set.add(r.indexer);
    });
    return Array.from(set).sort();
  }, [indexerReleases]);

  // Filtered and Sorted Indexer Releases
  const filteredIndexerReleases = useMemo(() => {
    return indexerReleases.filter(r => {
      if (onlyActiveSeeders && (r.seeders === undefined || r.seeders === null || r.seeders <= 0)) {
        return false;
      }
      if (selectedTracker !== 'all' && r.indexer !== selectedTracker) {
        return false;
      }
      if (releaseFilterText.trim()) {
        const lower = releaseFilterText.toLowerCase();
        if (!r.title.toLowerCase().includes(lower) && !(r.indexer && r.indexer.toLowerCase().includes(lower))) {
          return false;
        }
      }
      return true;
    }).sort((a, b) => {
      if (sortBy === 'seeders') {
        return (b.seeders || 0) - (a.seeders || 0);
      }
      if (sortBy === 'size_desc') {
        return b.size - a.size;
      }
      if (sortBy === 'size_asc') {
        return a.size - b.size;
      }
      if (sortBy === 'title') {
        return a.title.localeCompare(b.title);
      }
      return 0;
    });
  }, [indexerReleases, onlyActiveSeeders, selectedTracker, sortBy, releaseFilterText]);

  const formatFileSize = (bytes?: number) => {
    if (!bytes || bytes <= 0) return 'Unknown size';
    const gb = bytes / (1024 * 1024 * 1024);
    if (gb >= 1) return `${gb.toFixed(2)} GB`;
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(0)} MB`;
  };

  const formatSpeed = (bytesPerSec?: number) => {
    if (!bytesPerSec || bytesPerSec <= 0) return null;
    const mb = bytesPerSec / (1024 * 1024);
    if (mb >= 1) return `${mb.toFixed(1)} MB/s`;
    const kb = bytesPerSec / 1024;
    return `${kb.toFixed(0)} KB/s`;
  };

  const formatMinutes = (mins: number) => {
    if (!mins) return '';
    const h = Math.floor(mins / 60);
    const m = mins % 60;
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
  };

  const totalQueueDownSpeed = useMemo(() => {
    let sum = 0;
    queue.forEach(item => {
      if (item.downloadSpeed && item.downloadSpeed > 0) sum += item.downloadSpeed;
    });
    return sum;
  }, [queue]);

  return (
    <div className="min-h-screen bg-[#090a0c] text-[#e2e2e8] pb-16">
      {/* Top Section: Command Search & Unified Aggregator */}
      <div className="relative border-b border-[#21242d] bg-[#0c0e12]/80 backdrop-blur-xl sticky top-0 z-30 px-4 md:px-8 py-5 overflow-hidden">
        {/* Atmospheric Ambient Glow */}
        <div className="absolute -right-24 -top-24 w-96 h-96 rounded-full bg-[#3cd69b]/10 blur-3xl pointer-events-none" />
        <div className="absolute right-1/3 -bottom-28 w-80 h-80 rounded-full bg-[#38bdf8]/10 blur-3xl pointer-events-none" />

        <div className="max-w-7xl mx-auto relative z-10 flex flex-col gap-4">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div className="flex flex-col gap-1">
              <div className="flex items-center gap-2">
                <span className="px-2 py-0.5 rounded text-[10px] font-mono uppercase tracking-widest bg-[#3cd69b]/20 text-[#3cd69b] border border-[#3cd69b]/30 font-bold">
                  PROWLARR MATRIX v2.4
                </span>
                <span className="px-2 py-0.5 rounded text-[10px] font-mono uppercase tracking-widest bg-[#171920] text-[#38bdf8] border border-[#262a34]">
                  RADARR HOOK: {status?.isConnected ? 'CONNECTED' : 'STANDBY'}
                </span>
              </div>
              <h1 className="text-xl md:text-2xl font-extrabold text-white tracking-tight font-heading flex items-center gap-2 mt-0.5">
                <span>Indexers &amp; Release Pipeline</span>
                <span className="text-xs px-2 py-0.5 rounded-md bg-[#121418] text-[#85948b] font-mono border border-[#21242d] font-normal">
                  Universal Trackers
                </span>
              </h1>
            </div>

            {/* Tracker Health Telemetry Strip */}
            <div className="flex items-center flex-wrap gap-3 bg-[#121418] border border-[#21242d] px-4 py-2 rounded-xl text-xs font-mono shadow-sm">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-[#3cd69b] animate-pulse" />
                <span className="text-white font-semibold">18 Indexers Active</span>
              </div>
              <span className="text-[#85948b]">•</span>
              <div className="flex items-center gap-1.5 text-[#38bdf8]">
                <Zap size={13} />
                <span>Avg Latency: 320ms</span>
              </div>
              <span className="text-[#85948b]">•</span>
              <div className="flex items-center gap-1.5 text-[#85948b]">
                <ShieldCheck size={13} className="text-[#3cd69b]" />
                <span>VPN Enforced</span>
              </div>
              <button
                onClick={() => navigate('/settings')}
                className="p-1 rounded-lg bg-[#171920] hover:bg-[#21242d] text-[#85948b] hover:text-white transition-colors cursor-pointer ml-1"
                title="Radarr & Prowlarr Settings"
              >
                <SettingsIcon size={14} />
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-6 pt-6">
        {/* Connection Notice if Not Configured */}
        {!loadingStatus && !status?.isConnected && (
          <div className="mb-6 p-4 rounded-xl bg-amber-950/20 border border-amber-800/40 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-lg bg-amber-500/10 text-amber-400">
                <AlertTriangle size={20} />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-amber-200">Radarr Server Not Connected</h3>
                <p className="text-xs text-amber-300/70">
                  {status?.errorMessage || 'Configure your Radarr base URL and API key in Settings to search movies and download releases.'}
                </p>
              </div>
            </div>
            <button
              onClick={() => navigate('/settings')}
              className="px-4 py-2 rounded-lg bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 text-xs font-semibold border border-amber-500/30 transition-colors flex items-center gap-1.5 cursor-pointer self-start sm:self-auto"
            >
              Configure in Settings <ChevronRight size={14} />
            </button>
          </div>
        )}

        {/* Navigation Tabs */}
        <div className="flex items-center gap-1.5 bg-[#121418] border border-[#21242d] p-1.5 rounded-2xl mb-6 overflow-x-auto scrollbar-none w-full sm:w-auto">
          <button
            onClick={() => setActiveTab('search')}
            className={`px-4 py-2 rounded-xl text-xs font-medium transition-all cursor-pointer flex items-center gap-1.5 whitespace-nowrap ${
              activeTab === 'search' 
                ? 'bg-[#1e222a] text-white shadow-sm font-semibold' 
                : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <Search size={14} /> Search Results
          </button>

          <button
            onClick={() => setActiveTab('library')}
            className={`px-4 py-2 rounded-xl text-xs font-medium transition-all cursor-pointer flex items-center gap-1.5 whitespace-nowrap ${
              activeTab === 'library' 
                ? 'bg-[#1e222a] text-white shadow-sm font-semibold' 
                : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <HardDrive size={14} /> Library ({libraryMovies.length})
          </button>

          <button
            onClick={() => setActiveTab('queue')}
            className={`px-4 py-2 rounded-xl text-xs font-medium transition-all cursor-pointer flex items-center gap-1.5 relative whitespace-nowrap ${
              activeTab === 'queue' 
                ? 'bg-[#1e222a] text-white shadow-sm font-semibold' 
                : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <Activity size={14} /> Queue
            {queue.length > 0 && (
              <span className="ml-1 px-1.5 py-0.2 rounded-full bg-[#3cd69b] text-black text-[10px] font-bold">
                {queue.length}
              </span>
            )}
          </button>
        </div>

        {/* SEARCH TAB CONTENT */}
        {activeTab === 'search' && (
          <div>
            {/* Category Filter Pills */}
            <div className="flex items-center gap-2 overflow-x-auto pb-2 mb-4 scrollbar-none">
              {[
                { id: 'all', label: 'All Categories' },
                { id: 'software', label: '💻 Software & Apps' },
                { id: 'books', label: '📄 Books & PDFs' },
                { id: 'music', label: '🎵 Music & Audio' },
                { id: 'movies', label: '🎬 Movies' },
                { id: 'tv', label: '📺 TV Shows' },
                { id: 'other', label: '📦 General Files' }
              ].map((cat) => {
                const isSelected = selectedCategory === cat.id;
                return (
                  <button
                    key={cat.id}
                    type="button"
                    onClick={() => {
                      setSelectedCategory(cat.id);
                      if (searchQuery.trim()) {
                        handleSearch(undefined, undefined, cat.id);
                      }
                    }}
                    className={`px-3.5 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-all flex items-center gap-1.5 cursor-pointer border ${
                      isSelected
                        ? 'bg-[#3cd69b]/15 text-[#3cd69b] border-[#3cd69b]/40 shadow-sm'
                        : 'bg-[#121418] text-neutral-400 hover:text-white border-[#21242d] hover:border-[#2f3440]'
                    }`}
                  >
                    <span>{cat.label}</span>
                  </button>
                );
              })}
            </div>

            {/* Search Input Form */}
            <div className="mb-4">
              <form onSubmit={handleSearch} className="relative flex flex-col md:flex-row items-center gap-3">
                <div className="relative flex-1 w-full flex items-center">
                  <span className="material-symbols-outlined absolute left-4 text-[#3cd69b] text-[22px]">
                    radar
                  </span>
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search releases across 1337x, TorrentLeech, Nyaa, RuTracker, books, music..."
                    className="w-full h-14 bg-[#121418] border border-[#21242d] focus:border-[#3cd69b] pl-12 pr-28 rounded-xl font-body-md text-sm md:text-base text-white placeholder-[#525866] focus:outline-none transition-all shadow-inner"
                  />
                  {searchQuery ? (
                    <button
                      type="button"
                      onClick={() => setSearchQuery('')}
                      className="absolute right-24 top-4 text-[#85948b] hover:text-white cursor-pointer"
                    >
                      <X size={18} />
                    </button>
                  ) : (
                    <div className="absolute right-3 hidden sm:flex items-center gap-1.5 pointer-events-none">
                      <span className="px-2 py-1 rounded bg-[#171920] border border-[#262a34] font-mono text-[10px] text-[#85948b] uppercase tracking-wider">
                        ENTER
                      </span>
                    </div>
                  )}
                </div>
                <button
                  type="submit"
                  disabled={loading || !searchQuery.trim()}
                  className="w-full md:w-auto h-14 px-6 rounded-xl bg-[#3cd69b] text-[#003825] font-bold text-sm flex items-center justify-center gap-2 hover:bg-[#32b583] transition-all shadow-lg shadow-[#3cd69b]/20 active:scale-95 shrink-0 cursor-pointer disabled:opacity-50"
                >
                  {loading && activeTab === 'search' ? (
                    <RefreshCw size={16} className="animate-spin" />
                  ) : (
                    <span className="material-symbols-outlined text-[20px]">manage_search</span>
                  )}
                  <span>Query Indexers</span>
                </button>
              </form>
            </div>

            {/* Popular & Trending Quick-Search Pills */}
            <div className="flex items-center gap-2 overflow-x-auto pb-2 mb-5 scrollbar-none text-xs">
              <span className="text-neutral-500 font-medium whitespace-nowrap text-[11px] flex items-center gap-1">
                <Zap size={12} className="text-[#3cd69b]" /> Quick Search:
              </span>
              {(popularPills[selectedCategory] || popularPills['all']).map((pill) => (
                <button
                  key={pill}
                  type="button"
                  onClick={() => {
                    setSearchQuery(pill);
                    handleSearch(undefined, pill, selectedCategory);
                  }}
                  className="px-2.5 py-1 rounded-lg bg-[#121418] hover:bg-[#1e232d] text-neutral-300 hover:text-white border border-[#212530] hover:border-[#3cd69b]/40 transition-all cursor-pointer whitespace-nowrap text-[11px] font-medium shadow-sm"
                >
                  {pill}
                </button>
              ))}
            </div>
            {/* View Sub-tabs: Tracker Releases (100+) vs Movie Catalog */}
            {(indexerReleases.length > 0 || searchResults.length > 0) && (
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-5 border-b border-[#21242d] pb-4">
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setSearchView('trackers')}
                    className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer flex items-center gap-2 ${
                      searchView === 'trackers'
                        ? 'bg-[#3cd69b] text-black shadow-lg shadow-[#3cd69b]/10'
                        : 'bg-[#121418] text-neutral-400 hover:text-white border border-[#21242d]'
                    }`}
                  >
                    <Zap size={14} />
                    Tracker Releases ({indexerReleases.length})
                  </button>

                  <button
                    onClick={() => setSearchView('catalog')}
                    className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer flex items-center gap-2 ${
                      searchView === 'catalog'
                        ? 'bg-[#3cd69b] text-black shadow-lg shadow-[#3cd69b]/10'
                        : 'bg-[#121418] text-neutral-400 hover:text-white border border-[#21242d]'
                    }`}
                  >
                    <Film size={14} />
                    Movie Catalog ({searchResults.length})
                  </button>
                </div>

                {searchView === 'trackers' && (
                  <div className="text-xs text-neutral-400 flex items-center gap-2">
                    <Shield size={13} className="text-[#3cd69b]" />
                    <span>Downloads routed via WireGuard VPN container</span>
                  </div>
                )}
              </div>
            )}

            {/* TRACKER RELEASES VIEW (100+ items with seeders & 1-click download) */}
            {searchView === 'trackers' && (
              <div>
                {/* Filters & Sorting Bar for Trackers */}
                {indexerReleases.length > 0 && (
                  <div className="p-3.5 rounded-xl bg-[#121418] border border-[#21242d] mb-5 flex flex-wrap items-center justify-between gap-3 text-xs">
                    <div className="flex flex-wrap items-center gap-3">
                      {/* Active Seeders Only Toggle */}
                      <button
                        onClick={() => setOnlyActiveSeeders(prev => !prev)}
                        className={`px-3 py-1.5 rounded-lg font-medium transition-all flex items-center gap-1.5 cursor-pointer border ${
                          onlyActiveSeeders
                            ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40 shadow-sm'
                            : 'bg-[#161922] text-neutral-400 border-[#262b37] hover:text-neutral-200'
                        }`}
                      >
                        <Check size={13} className={onlyActiveSeeders ? 'opacity-100' : 'opacity-0'} />
                        Active Seeders Only (Seeders &gt; 0)
                      </button>

                      {/* Tracker Filter */}
                      {availableTrackers.length > 1 && (
                        <div className="flex items-center gap-1.5">
                          <span className="text-neutral-500 font-medium">Tracker:</span>
                          <select
                            value={selectedTracker}
                            onChange={(e) => setSelectedTracker(e.target.value)}
                            className="bg-[#161922] text-neutral-200 border border-[#262b37] rounded-lg px-2.5 py-1.5 outline-none text-xs cursor-pointer"
                          >
                            <option value="all">All Trackers ({indexerReleases.length})</option>
                            {availableTrackers.map(t => (
                              <option key={t} value={t}>{t}</option>
                            ))}
                          </select>
                        </div>
                      )}

                      {/* Filter by keyword in title */}
                      <input
                        type="text"
                        placeholder="Filter by keyword (e.g. 1080p, 2160p)..."
                        value={releaseFilterText}
                        onChange={(e) => setReleaseFilterText(e.target.value)}
                        className="bg-[#161922] text-neutral-200 border border-[#262b37] focus:border-[#3cd69b] rounded-lg px-2.5 py-1.5 outline-none text-xs w-48 placeholder-neutral-500"
                      />
                    </div>

                    {/* Sorting */}
                    <div className="flex items-center gap-2">
                      <ArrowUpDown size={13} className="text-neutral-500" />
                      <span className="text-neutral-500 font-medium">Sort:</span>
                      <select
                        value={sortBy}
                        onChange={(e: any) => setSortBy(e.target.value)}
                        className="bg-[#161922] text-neutral-200 border border-[#262b37] rounded-lg px-2.5 py-1.5 outline-none text-xs cursor-pointer font-medium"
                      >
                        <option value="seeders">Seeders (Highest First)</option>
                        <option value="size_desc">File Size (Largest First)</option>
                        <option value="size_asc">File Size (Smallest First)</option>
                        <option value="title">Title (Alphabetical)</option>
                      </select>
                    </div>
                  </div>
                )}

                {/* Loading State */}
                {loading ? (
                  <div className="flex flex-col items-center justify-center py-24 text-neutral-400">
                    <RefreshCw size={32} className="animate-spin text-[#3cd69b] mb-4" />
                    <p className="text-sm font-medium">Searching active indexers and trackers...</p>
                    <p className="text-xs text-neutral-500 mt-1">Querying 100+ releases via Prowlarr</p>
                  </div>
                ) : indexerReleases.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-20 px-4 text-center rounded-2xl bg-[#121418]/50 border border-[#21242d]">
                    <Zap size={44} className="text-neutral-600 mb-3" />
                    <h3 className="text-base font-semibold text-neutral-300 mb-1">
                      {searchQuery ? `No releases found on indexers for "${searchQuery}"` : 'Search for any movie to discover releases'}
                    </h3>
                    <p className="text-xs text-neutral-500 max-w-md">
                      Type a title above to search across active torrent trackers (1337x, TorrentGalaxy, The Pirate Bay, etc.).
                    </p>

                    {searchResults.length > 0 && (
                      <div className="mt-5 p-4 rounded-xl bg-[#161922] border border-[#262b37] max-w-lg text-left shadow-lg">
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-semibold text-[#3cd69b] flex items-center gap-1.5">
                            <Film size={14} />
                            Found {searchResults.length} matches in Movie Catalog:
                          </span>
                          <button
                            onClick={() => setSearchView('catalog')}
                            className="text-xs font-medium text-white hover:text-[#3cd69b] underline cursor-pointer"
                          >
                            Switch to Catalog &rarr;
                          </button>
                        </div>
                        <p className="text-[11px] text-neutral-400 mt-1.5 leading-relaxed">
                          Generic keywords like &ldquo;{searchQuery}&rdquo; match catalog entries. Click below or search a specific movie title to find direct torrent releases:
                        </p>
                        <div className="flex flex-wrap gap-1.5 mt-3">
                          {searchResults.slice(0, 6).map((m) => (
                            <button
                              key={m.tmdbId}
                              onClick={() => {
                                setSearchQuery(m.title);
                                handleSearch(undefined, m.title, 'movies');
                              }}
                              className="px-2.5 py-1 rounded-lg bg-[#212530] hover:bg-[#2d3342] text-[11px] text-neutral-200 border border-[#303646] hover:border-[#3cd69b]/50 transition-all cursor-pointer truncate max-w-[200px]"
                              title={m.title}
                            >
                              🎬 {m.title} {m.year ? `(${m.year})` : ''}
                            </button>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ) : filteredIndexerReleases.length === 0 ? (
                  <div className="p-8 text-center rounded-2xl bg-[#121418] border border-[#21242d] text-neutral-400">
                    <AlertTriangle size={24} className="mx-auto text-amber-400 mb-2" />
                    <p className="text-xs">No releases match your current filters.</p>
                    <button
                      onClick={() => { setOnlyActiveSeeders(false); setSelectedTracker('all'); setReleaseFilterText(''); }}
                      className="mt-3 px-3 py-1.5 rounded-lg bg-[#1e222a] text-neutral-300 hover:text-white text-xs cursor-pointer"
                    >
                      Reset Filters
                    </button>
                  </div>
                ) : (
                  <div className="space-y-2.5">
                    <div className="flex items-center justify-between text-xs text-neutral-400 px-2 pb-1 font-mono">
                      <span>Showing {filteredIndexerReleases.length} of {indexerReleases.length} releases</span>
                      <span>Sorted by {sortBy === 'seeders' ? 'active seeders' : sortBy}</span>
                    </div>

                    {filteredIndexerReleases.map((rel) => {
                      const isDownloading = downloadingReleases[rel.guid];
                      const isDownloaded = downloadedGuids.has(rel.guid);
                      const seedersCount = rel.seeders || 0;

                      return (
                        <div
                          key={rel.guid}
                          className="p-3.5 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b]/40 transition-all flex flex-col md:flex-row md:items-center justify-between gap-3 shadow-sm hover:shadow-md"
                        >
                          <div className="flex-1 min-w-0">
                            <h4 className="font-semibold text-xs text-white break-all leading-snug hover:text-[#3cd69b] transition-colors" title={rel.title}>
                              {rel.title}
                            </h4>

                            <div className="flex flex-wrap items-center gap-2.5 mt-2 text-[11px] text-neutral-400">
                              {/* Category Badge */}
                              <span className={`px-2 py-0.5 rounded-md font-semibold text-[10px] flex items-center gap-1 border ${
                                rel.primaryCategory === 'Software' ? 'bg-indigo-500/15 text-indigo-300 border-indigo-500/30' :
                                rel.primaryCategory === 'Books / PDF' ? 'bg-amber-500/15 text-amber-300 border-amber-500/30' :
                                rel.primaryCategory === 'Music / Audio' ? 'bg-purple-500/15 text-purple-300 border-purple-500/30' :
                                rel.primaryCategory === 'Games' ? 'bg-pink-500/15 text-pink-300 border-pink-500/30' :
                                rel.primaryCategory === 'TV Shows' ? 'bg-blue-500/15 text-blue-300 border-blue-500/30' :
                                rel.primaryCategory === 'Movies' ? 'bg-cyan-500/15 text-cyan-300 border-cyan-500/30' :
                                'bg-neutral-800 text-neutral-300 border-neutral-700'
                              }`}>
                                {rel.primaryCategory === 'Software' ? '💻 Software' :
                                 rel.primaryCategory === 'Books / PDF' ? '📄 Book / PDF' :
                                 rel.primaryCategory === 'Music / Audio' ? '🎵 Music' :
                                 rel.primaryCategory === 'Games' ? '🎮 Games' :
                                 rel.primaryCategory === 'TV Shows' ? '📺 TV' :
                                 rel.primaryCategory === 'Movies' ? '🎬 Movie' :
                                 '📦 Files'}
                              </span>

                              {/* Seeders count badge */}
                              <span className={`px-2 py-0.5 rounded-md font-mono font-bold flex items-center gap-1 ${
                                seedersCount >= 20
                                  ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                  : seedersCount > 0
                                  ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                                  : 'bg-rose-500/15 text-rose-400 border border-rose-500/20'
                              }`}>
                                ▲ {seedersCount} seeders
                              </span>

                              {/* Leechers */}
                              {rel.leechers !== null && rel.leechers !== undefined && (
                                <span className="text-neutral-400 font-mono text-[10px]">
                                  ▼ {rel.leechers}
                                </span>
                              )}

                              {/* File Size */}
                              <span className="font-mono text-neutral-300 font-medium">
                                {formatFileSize(rel.size)}
                              </span>

                              {/* Tracker Name */}
                              <span className="px-2 py-0.5 rounded-md bg-[#1a1d24] text-neutral-300 font-medium border border-[#262a34] text-[10px]">
                                {rel.indexer}
                              </span>

                              {/* Protocol */}
                              <span className="text-neutral-500 uppercase text-[10px] font-mono">
                                {rel.protocol || 'torrent'}
                              </span>

                              {/* Publish Date */}
                              {rel.publishDate && (
                                <span className="text-neutral-500 text-[10px]">
                                  {new Date(rel.publishDate).toLocaleDateString()}
                                </span>
                              )}
                            </div>
                          </div>

                          {/* 1-Click VPN Download Button */}
                          <div className="flex items-center gap-2 shrink-0">
                            <button
                              onClick={() => handleDownloadIndexerRelease(rel)}
                              disabled={isDownloading || isDownloaded}
                              className={`px-4 py-2 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-sm ${
                                isDownloaded
                                  ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 cursor-default'
                                  : 'bg-[#3cd69b] hover:bg-[#32b885] text-black disabled:opacity-50'
                              }`}
                              title="Download release to Transmission over Gluetun WireGuard VPN"
                            >
                              {isDownloading ? (
                                <>
                                  <RefreshCw size={13} className="animate-spin" /> Sending...
                                </>
                              ) : isDownloaded ? (
                                <>
                                  <Check size={13} /> Queued in VPN
                                </>
                              ) : (
                                <>
                                  <Download size={13} /> Download via VPN
                                </>
                              )}
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            {/* CATALOG VIEW (TMDb / Radarr External Metadata Cards) */}
            {searchView === 'catalog' && (
              <div>
                {loading ? (
                  <div className="flex flex-col items-center justify-center py-24 text-neutral-400">
                    <RefreshCw size={32} className="animate-spin text-[#3cd69b] mb-4" />
                    <p className="text-sm font-medium">Searching movie catalog...</p>
                  </div>
                ) : searchResults.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-24 px-4 text-center rounded-2xl bg-[#121418]/50 border border-[#21242d]">
                    <Film size={48} className="text-neutral-600 mb-3" />
                    <h3 className="text-base font-semibold text-neutral-300 mb-1">
                      {searchQuery ? 'No movies matched your search in catalog' : 'Search for any movie to get started'}
                    </h3>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-5">
                    {searchResults.map((movie) => (
                      <div
                        key={`${movie.tmdbId}-${movie.id}`}
                        className="group bg-[#121418] rounded-2xl border border-[#21242d] hover:border-[#3cd69b]/40 overflow-hidden flex flex-col transition-all duration-200 hover:shadow-xl hover:shadow-[#3cd69b]/5"
                      >
                        {/* Poster Image Container - Clickable to open releases */}
                        <div
                          onClick={() => handleOpenReleases(movie)}
                          className="relative aspect-[2/3] w-full bg-[#181a20] overflow-hidden cursor-pointer"
                          title={`Click to view downloadable releases & quality options for "${movie.title}"`}
                        >
                          {movie.posterUrl ? (
                            <img
                              src={movie.posterUrl}
                              alt={movie.title}
                              className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                              loading="lazy"
                            />
                          ) : (
                            <div className="w-full h-full flex flex-col items-center justify-center text-neutral-600 bg-neutral-900">
                              <Film size={36} className="mb-2 opacity-50" />
                              <span className="text-[11px]">No Poster</span>
                            </div>
                          )}

                          {/* Hover Overlay: View Releases */}
                          <div className="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 transition-opacity flex flex-col items-center justify-center gap-1.5 p-3 text-center pointer-events-none">
                            <div className="p-2.5 rounded-full bg-[#3cd69b] text-black shadow-lg">
                              <Layers size={18} />
                            </div>
                            <span className="text-xs font-bold text-white">View Releases</span>
                            <span className="text-[10px] text-neutral-300">Torrents & 4K/1080p Qualities</span>
                          </div>

                          {/* Top Badges overlay */}
                          <div className="absolute top-2 left-2 right-2 flex items-center justify-between gap-1 pointer-events-none">
                            {movie.year > 0 && (
                              <span className="px-2 py-0.5 rounded-md bg-black/75 backdrop-blur-md text-white text-[11px] font-bold border border-white/10 shadow-sm">
                                {movie.year}
                              </span>
                            )}

                            {movie.ratingsValue && movie.ratingsValue > 0 ? (
                              <span className="px-2 py-0.5 rounded-md bg-black/75 backdrop-blur-md text-amber-400 text-[11px] font-bold flex items-center gap-1 border border-white/10 shadow-sm">
                                <Star size={11} className="fill-amber-400" />
                                {movie.ratingsValue.toFixed(1)}
                              </span>
                            ) : null}
                          </div>
                        </div>

                        {/* Movie Info */}
                        <div className="p-4 flex-1 flex flex-col justify-between">
                          <div>
                            <h3
                              onClick={() => handleOpenReleases(movie)}
                              className="font-bold text-sm text-white line-clamp-1 group-hover:text-[#3cd69b] transition-colors cursor-pointer"
                              title={`Click to view releases for ${movie.title}`}
                            >
                              {movie.title}
                            </h3>

                            <div className="flex items-center gap-2 mt-1 text-[11px] text-neutral-400">
                              {movie.runtime > 0 && (
                                <span className="flex items-center gap-1">
                                  <Clock size={11} /> {formatMinutes(movie.runtime)}
                                </span>
                              )}
                              {movie.genres && movie.genres.length > 0 && (
                                <span className="truncate max-w-[140px]" title={movie.genres.join(', ')}>
                                  • {movie.genres.slice(0, 2).join(', ')}
                                </span>
                              )}
                            </div>

                            {movie.overview && (
                              <p className="mt-2 text-xs text-neutral-400 line-clamp-2 leading-relaxed">
                                {movie.overview}
                              </p>
                            )}
                          </div>

                          {/* Actions */}
                          <div className="mt-4 pt-3 border-t border-[#21242d]/80 flex flex-col gap-2">
                            <div className="grid grid-cols-2 gap-2">
                              <button
                                onClick={() => handleQuickDownload(movie)}
                                disabled={actionMovieId === movie.tmdbId}
                                className="py-2 px-2 rounded-xl bg-[#3cd69b] hover:bg-[#32b885] disabled:opacity-50 text-black text-xs font-bold transition-all flex items-center justify-center gap-1 cursor-pointer shadow-sm"
                                title="Auto-search and download best release via Radarr / Trackers"
                              >
                                {actionMovieId === movie.tmdbId ? (
                                  <>
                                    <RefreshCw size={13} className="animate-spin" /> Grabbing...
                                  </>
                                ) : (
                                  <>
                                    <Download size={13} /> Auto-Grab
                                  </>
                                )}
                              </button>

                              <button
                                onClick={() => handleOpenReleases(movie)}
                                className="py-2 px-2 rounded-xl bg-[#1e222a] hover:bg-[#282d38] text-[#3cd69b] text-xs font-semibold border border-[#2d323e] transition-all flex items-center justify-center gap-1 cursor-pointer"
                                title="Inspect quality options and pick a torrent"
                              >
                                <Layers size={13} /> Releases
                              </button>
                            </div>

                            <button
                              onClick={() => handleSearchTrackersForMovie(movie.title)}
                              className="py-1.5 px-2 rounded-lg bg-[#14171e] hover:bg-[#1c212c] text-neutral-300 hover:text-white text-[11px] font-medium transition-colors flex items-center justify-center gap-1.5 cursor-pointer border border-[#232836]"
                              title="Search 100+ raw tracker torrents across all indexers"
                            >
                              <Zap size={12} className="text-[#3cd69b]" /> Search Raw Trackers (100+)
                            </button>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* TAB 2: RADARR DOWNLOAD HUB & LIBRARY VIEW */}
        {activeTab === 'library' && (
          <div className="space-y-6">
            {/* Top Storage & Folder Safeguard Hub Banner */}
            <div className="p-5 rounded-2xl bg-[#121418] border border-[#21242d] shadow-sm relative overflow-hidden">
              <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-5 relative z-10">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-[#3cd69b]/10 text-[#3cd69b] border border-[#3cd69b]/25 text-xs font-bold">
                      <Shield size={13} /> Protected Non-Deletable System Folder: /media/Radarr
                    </span>
                  </div>
                  <h2 className="text-lg font-bold text-white flex items-center gap-2">
                    <HardDrive size={20} className="text-[#3cd69b]" />
                    Radarr Unified Downloads & Media Hub
                  </h2>
                  <p className="text-xs text-neutral-400 mt-1">
                    All downloaded movies, videos, PDFs, subtitles, and images are organized in 1 unified place on your server.
                  </p>
                </div>

                {/* Storage & Safeguard Metrics */}
                <div className="flex flex-wrap items-center gap-3">
                  <div className="px-3.5 py-2 rounded-xl bg-[#181a20] border border-[#262a35] text-center min-w-[100px]">
                    <span className="text-[10px] uppercase font-bold text-neutral-400 tracking-wider block">
                      Downloaded
                    </span>
                    <span className="text-base font-bold text-white">
                      {downloadSummary?.totalDownloadedCount ?? downloadedMovies.length}
                    </span>
                  </div>

                  <div className="px-3.5 py-2 rounded-xl bg-[#181a20] border border-[#262a35] text-center min-w-[110px]">
                    <span className="text-[10px] uppercase font-bold text-neutral-400 tracking-wider block">
                      Size on Disk
                    </span>
                    <span className="text-base font-bold text-[#3cd69b] font-mono">
                      {downloadSummary?.formattedTotalSize || formatFileSize(downloadSummary?.totalSizeBytes)}
                    </span>
                  </div>

                  <div className="px-3.5 py-2 rounded-xl bg-[#181a20] border border-[#262a35] text-center min-w-[100px]">
                    <span className="text-[10px] uppercase font-bold text-neutral-400 tracking-wider block">
                      All Files (PDF/Extras)
                    </span>
                    <span className="text-base font-bold text-neutral-200">
                      {downloadSummary?.downloadedMovies
                        ? downloadSummary.downloadedMovies.reduce((acc, m) => acc + (m.extraFiles?.length || 0) + (m.hasFile ? 1 : 0), 0)
                        : downloadedMovies.reduce((acc, m) => acc + (m.extraFiles?.length || 0) + (m.hasFile ? 1 : 0), 0)}
                    </span>
                  </div>

                  {/* Actions: Sync Media Links & Delete All */}
                  <div className="flex items-center gap-2">
                    <button
                      onClick={handleSyncLinks}
                      disabled={isSyncingLinks}
                      className="py-2 px-3.5 rounded-xl bg-[#1e222a] hover:bg-[#282d38] text-white text-xs font-semibold border border-[#2d323e] transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50 shadow-sm"
                      title="Links all downloaded movies & extra files (PDFs, subtitles, images) into /media/Radarr and indexes them into MyTube"
                    >
                      <RefreshCw size={13} className={isSyncingLinks ? 'animate-spin text-[#3cd69b]' : ''} />
                      <span>{isSyncingLinks ? 'Syncing...' : 'Sync Media Links'}</span>
                    </button>

                    <button
                      onClick={() => setShowDeleteAllModal(true)}
                      disabled={downloadedMovies.length === 0 || isDeletingAll}
                      className="py-2 px-3.5 rounded-xl bg-rose-950/40 hover:bg-rose-950/70 text-rose-300 border border-rose-800/40 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed shadow-sm"
                      title="Safely wipe all downloaded movie files, PDFs, subtitles and extras from disk while keeping /media/Radarr folder intact"
                    >
                      <Trash2 size={13} className="text-rose-400" />
                      <span>Delete All Files</span>
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* Sub-Tabs & Batch Action Toolbar */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-3 rounded-xl bg-[#121418] border border-[#21242d]">
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setLibraryFilter('downloaded')}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer flex items-center gap-2 ${
                    libraryFilter === 'downloaded'
                      ? 'bg-[#3cd69b] text-black shadow-sm'
                      : 'text-neutral-400 hover:text-white bg-[#181a20]'
                  }`}
                >
                  <CheckCircle2 size={13} />
                  <span>Downloaded Media (All in 1 Place)</span>
                  <span className={`px-1.5 py-0.2 rounded-full text-[10px] font-mono font-bold ${
                    libraryFilter === 'downloaded' ? 'bg-black/20 text-black' : 'bg-neutral-800 text-neutral-300'
                  }`}>
                    {downloadedMovies.length}
                  </span>
                </button>

                <button
                  onClick={() => setLibraryFilter('all')}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer flex items-center gap-2 ${
                    libraryFilter === 'all'
                      ? 'bg-[#1e222a] text-white border border-[#2d323e]'
                      : 'text-neutral-400 hover:text-white'
                  }`}
                >
                  <Film size={13} />
                  <span>All Monitored Catalog</span>
                  <span className="px-1.5 py-0.2 rounded-full bg-neutral-800 text-neutral-300 text-[10px] font-mono">
                    {libraryMovies.length}
                  </span>
                </button>
              </div>

              {/* Batch Actions Toolbar */}
              {downloadedMovies.length > 0 && (
                <div className="flex items-center gap-2.5">
                  <button
                    onClick={handleToggleSelectAll}
                    className="px-3 py-1.5 rounded-lg bg-[#181a20] hover:bg-[#20232b] text-neutral-300 hover:text-white text-xs font-medium border border-[#262a34] flex items-center gap-2 transition-colors cursor-pointer"
                  >
                    {allDownloadedSelected ? (
                      <>
                        <CheckSquare size={14} className="text-[#3cd69b]" />
                        <span>Deselect All</span>
                      </>
                    ) : (
                      <>
                        <Square size={14} className="text-neutral-400" />
                        <span>Select All ({downloadedMovies.length})</span>
                      </>
                    )}
                  </button>

                  {selectedMovieIds.size > 0 && (
                    <div className="flex items-center gap-2">
                      <span className="px-2 py-1 rounded-md bg-[#3cd69b]/20 text-[#3cd69b] text-xs font-bold font-mono">
                        {selectedMovieIds.size} selected
                      </span>
                      <button
                        onClick={() => setShowDeleteSelectedModal(true)}
                        disabled={isDeletingSelected}
                        className="px-3 py-1.5 rounded-lg bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold flex items-center gap-1.5 shadow-md shadow-rose-950/50 transition-colors cursor-pointer"
                      >
                        <Trash2 size={13} />
                        <span>Delete Selected</span>
                      </button>
                    </div>
                  )}

                  <button
                    onClick={loadLibrary}
                    className="p-1.5 rounded-lg text-neutral-400 hover:text-white hover:bg-[#181a20] transition-colors cursor-pointer"
                    title="Refresh Library"
                  >
                    <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
                  </button>
                </div>
              )}
            </div>

            {loading ? (
              <div className="flex flex-col items-center justify-center py-24 text-neutral-400">
                <RefreshCw size={32} className="animate-spin text-[#3cd69b] mb-4" />
                <p className="text-sm font-medium">Loading files and movie library from Radarr...</p>
              </div>
            ) : (libraryFilter === 'downloaded' ? downloadedMovies : libraryMovies).length === 0 ? (
              <div className="flex flex-col items-center justify-center py-24 px-4 text-center rounded-2xl bg-[#121418]/50 border border-[#21242d]">
                <Film size={48} className="text-neutral-600 mb-3" />
                <h3 className="text-base font-semibold text-neutral-300 mb-1">
                  {libraryFilter === 'downloaded' ? 'No downloaded movies yet' : 'No movies monitored in Radarr'}
                </h3>
                <p className="text-xs text-neutral-500 max-w-md mb-4">
                  {libraryFilter === 'downloaded'
                    ? 'Search tracker releases in the Search tab to download movies, videos, and associated files.'
                    : 'Movies added or monitored via Radarr will be listed here.'}
                </p>
                {libraryFilter === 'downloaded' && (
                  <button
                    onClick={() => setActiveTab('search')}
                    className="px-4 py-2 rounded-xl bg-[#3cd69b] text-black font-bold text-xs hover:bg-[#32b885] transition-colors cursor-pointer"
                  >
                    Search Releases
                  </button>
                )}
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-5">
                {(libraryFilter === 'downloaded' ? downloadedMovies : libraryMovies).map((movie) => (
                  <div
                    key={`${movie.tmdbId}-${movie.id}`}
                    className={`group bg-[#121418] rounded-2xl border ${
                      selectedMovieIds.has(movie.id) ? 'border-[#3cd69b]' : 'border-[#21242d]'
                    } hover:border-[#3cd69b]/40 overflow-hidden flex flex-col transition-all duration-200 shadow-sm relative`}
                  >
                    {/* Multi-Select Checkbox for Downloaded Movie */}
                    {movie.hasFile && (
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          handleToggleMovieSelect(movie.id);
                        }}
                        className="absolute top-2.5 right-2.5 z-20 p-1.5 rounded-lg bg-black/80 backdrop-blur-md hover:bg-black text-white border border-white/20 transition-all cursor-pointer shadow-lg"
                        title={selectedMovieIds.has(movie.id) ? 'Deselect movie' : 'Select movie for batch actions'}
                      >
                        {selectedMovieIds.has(movie.id) ? (
                          <CheckSquare size={16} className="text-[#3cd69b]" />
                        ) : (
                          <Square size={16} className="text-neutral-300" />
                        )}
                      </button>
                    )}

                    {/* Movie Poster & Status Badges */}
                    <div className="relative aspect-[2/3] w-full bg-[#181a20] overflow-hidden">
                      {movie.posterUrl ? (
                        <img
                          src={movie.posterUrl}
                          alt={movie.title}
                          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                          loading="lazy"
                        />
                      ) : (
                        <div className="w-full h-full flex flex-col items-center justify-center text-neutral-600 bg-neutral-900">
                          <Film size={36} className="mb-2 opacity-50" />
                          <span className="text-[11px]">No Poster</span>
                        </div>
                      )}

                      <div className="absolute top-2 left-2 flex items-center gap-1">
                        {movie.year > 0 && (
                          <span className="px-2 py-0.5 rounded-md bg-black/75 backdrop-blur-md text-white text-[11px] font-bold border border-white/10">
                            {movie.year}
                          </span>
                        )}
                      </div>

                      <div className="absolute bottom-2 left-2 right-2 flex flex-wrap items-center gap-1">
                        {movie.hasFile ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-emerald-950/90 text-emerald-300 border border-emerald-500/40 text-[11px] font-semibold backdrop-blur-md">
                            <CheckCircle2 size={12} className="text-emerald-400" /> Downloaded
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-black/80 text-amber-300 border border-amber-500/30 text-[11px] font-semibold backdrop-blur-md">
                            <Clock size={12} className="text-amber-400" /> Monitored
                          </span>
                        )}

                        {movie.extraFiles && movie.extraFiles.length > 0 && (
                          <span className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-blue-950/90 text-blue-300 border border-blue-500/40 text-[10px] font-medium backdrop-blur-md">
                            +{movie.extraFiles.length} Extras
                          </span>
                        )}
                      </div>
                    </div>

                    {/* Movie Info & Unified Download Center */}
                    <div className="p-4 flex-1 flex flex-col justify-between">
                      <div>
                        <h3 className="font-bold text-sm text-white line-clamp-1" title={movie.title}>
                          {movie.title}
                        </h3>
                        <div className="flex items-center gap-2 mt-1 text-[11px] text-neutral-400">
                          {movie.runtime > 0 && <span>{formatMinutes(movie.runtime)}</span>}
                          {movie.sizeOnDisk && movie.sizeOnDisk > 0 && (
                            <span>• {formatFileSize(movie.sizeOnDisk)}</span>
                          )}
                          {movie.quality && (
                            <span className="px-1.5 py-0.2 rounded bg-[#1e222a] text-[#3cd69b] font-mono text-[10px]">
                              {movie.quality}
                            </span>
                          )}
                        </div>

                        {movie.movieFolderPath && (
                          <p className="text-[10px] text-neutral-500 font-mono truncate mt-1" title={movie.movieFolderPath}>
                            {movie.movieFolderPath}
                          </p>
                        )}
                      </div>

                      {/* Download Actions & Drawer */}
                      <div className="mt-4 pt-3 border-t border-[#21242d] space-y-2">
                        {movie.hasFile ? (
                          <>
                            {/* Primary Button: Download Video directly to Laptop / Phone */}
                            <a
                              href={api.getRadarrMovieDownloadUrl(movie.id)}
                              download
                              className="w-full py-2 px-2.5 rounded-xl bg-[#3cd69b] hover:bg-[#32b885] text-black text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer shadow-sm hover:shadow-[#3cd69b]/20"
                              title="Direct download movie video to your laptop, desktop, or phone"
                            >
                              <Download size={14} />
                              <div className="flex items-center gap-1">
                                <span>Download to</span>
                                <Laptop size={13} />
                                <span>/</span>
                                <Smartphone size={13} />
                              </div>
                            </a>

                            {/* Poster and Fanart Download Buttons */}
                            <div className="grid grid-cols-2 gap-1.5 text-[11px]">
                              <a
                                href={api.getRadarrPosterDownloadUrl(movie.id)}
                                download
                                target="_blank"
                                rel="noopener noreferrer"
                                className="py-1.5 px-2 rounded-lg bg-[#1e222a] hover:bg-[#282d38] text-neutral-300 hover:text-white flex items-center justify-center gap-1.5 transition-colors cursor-pointer border border-[#2d323e]"
                                title="Download movie poster"
                              >
                                <ImageIcon size={12} className="text-purple-400" /> Poster
                              </a>

                              <a
                                href={api.getRadarrFanartDownloadUrl(movie.id)}
                                download
                                target="_blank"
                                rel="noopener noreferrer"
                                className="py-1.5 px-2 rounded-lg bg-[#1e222a] hover:bg-[#282d38] text-neutral-300 hover:text-white flex items-center justify-center gap-1.5 transition-colors cursor-pointer border border-[#2d323e]"
                                title="Download movie fanart backdrop"
                              >
                                <ImageIcon size={12} className="text-blue-400" /> Fanart
                              </a>
                            </div>

                            {/* Watch in MyTube Stream Player if linked */}
                            {movie.localVideoId && (
                              <button
                                onClick={() => navigate(`/watch/${movie.localVideoId}`)}
                                className="w-full py-1.5 px-2.5 rounded-xl bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 border border-purple-500/30 text-xs font-medium flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
                              >
                                <Play size={13} className="fill-current" /> Watch in MyTube
                              </button>
                            )}

                            {/* All Files Drawer Accordion Toggle (PDFs, subs, nfo, extras) */}
                            <button
                              onClick={() => setExpandedMovieId(expandedMovieId === movie.id ? null : movie.id)}
                              className="w-full py-1.5 px-2.5 rounded-lg bg-[#16181f] hover:bg-[#1f232c] text-neutral-400 hover:text-neutral-200 text-xs flex items-center justify-between border border-[#21242d] transition-colors cursor-pointer"
                            >
                              <span className="flex items-center gap-1.5">
                                <Folder size={13} className="text-amber-400" />
                                <span>All Files ({(movie.extraFiles?.length || 0) + 1})</span>
                              </span>
                              {expandedMovieId === movie.id ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                            </button>

                            {/* Expanded All Files Drawer */}
                            {expandedMovieId === movie.id && (
                              <div className="p-2.5 rounded-xl bg-[#0c0e12] border border-[#21242d] space-y-2 text-left">
                                <div className="text-[10px] uppercase font-bold text-neutral-500 tracking-wider">
                                  Folder Assets & Downloads
                                </div>

                                {/* Main Video File */}
                                <div className="flex items-center justify-between gap-2 p-1.5 rounded-lg bg-[#161820] border border-[#232732]">
                                  <div className="flex items-center gap-1.5 min-w-0">
                                    <Film size={13} className="text-[#3cd69b] shrink-0" />
                                    <span className="text-[11px] text-white truncate" title={movie.movieFilePath || movie.title}>
                                      {movie.movieFilePath ? movie.movieFilePath.split(/[\\/]/).pop() : `${movie.title}.mkv`}
                                    </span>
                                  </div>
                                  <a
                                    href={api.getRadarrMovieDownloadUrl(movie.id)}
                                    download
                                    className="p-1 rounded bg-[#21242d] hover:bg-[#3cd69b] text-neutral-300 hover:text-black transition-colors shrink-0"
                                    title="Download Video File"
                                  >
                                    <Download size={12} />
                                  </a>
                                </div>

                                {/* Extra Files: PDFs, Subtitles, Documents, Images */}
                                {movie.extraFiles && movie.extraFiles.length > 0 ? (
                                  movie.extraFiles.map((file, idx) => {
                                    const isPdf = file.fileType === 'pdf';
                                    const isSub = file.fileType === 'subtitle';
                                    const isImg = file.fileType === 'image';
                                    const isDoc = file.fileType === 'document';

                                    return (
                                      <div
                                        key={`${file.filePath}-${idx}`}
                                        className="flex items-center justify-between gap-2 p-1.5 rounded-lg bg-[#161820] border border-[#232732]"
                                      >
                                        <div className="flex items-center gap-1.5 min-w-0">
                                          {isPdf ? (
                                            <FileText size={13} className="text-rose-400 shrink-0" />
                                          ) : isSub ? (
                                            <FileCheck size={13} className="text-cyan-400 shrink-0" />
                                          ) : isImg ? (
                                            <ImageIcon size={13} className="text-purple-400 shrink-0" />
                                          ) : isDoc ? (
                                            <FileText size={13} className="text-amber-400 shrink-0" />
                                          ) : (
                                            <File size={13} className="text-neutral-400 shrink-0" />
                                          )}
                                          <div className="min-w-0">
                                            <span className="text-[11px] text-neutral-200 truncate block" title={file.fileName}>
                                              {file.fileName}
                                            </span>
                                            <span className={`inline-block px-1 py-0.2 rounded text-[9px] font-mono font-bold ${
                                              isPdf ? 'bg-rose-950/70 text-rose-400' :
                                              isSub ? 'bg-cyan-950/70 text-cyan-400' :
                                              isImg ? 'bg-purple-950/70 text-purple-400' :
                                              'bg-amber-950/70 text-amber-400'
                                            }`}>
                                              {(file.fileType || 'FILE').toUpperCase()} • {file.formattedSize || formatFileSize(file.fileSizeBytes)}
                                            </span>
                                          </div>
                                        </div>
                                        <a
                                          href={api.getGenericFileDownloadUrl(file.filePath)}
                                          download
                                          className="p-1 rounded bg-[#21242d] hover:bg-[#3cd69b] text-neutral-300 hover:text-black transition-colors shrink-0"
                                          title={`Download ${file.fileName}`}
                                        >
                                          <Download size={12} />
                                        </a>
                                      </div>
                                    );
                                  })
                                ) : (
                                  <p className="text-[10px] text-neutral-500 italic">No extra documents or subtitles found</p>
                                )}
                              </div>
                            )}

                            {/* Individual Delete Files Button */}
                            <button
                              onClick={() => handleDeleteMovieFiles(movie.id, movie.title)}
                              disabled={deletingMovieId === movie.id}
                              className="w-full py-1.5 px-2 rounded-lg text-rose-400/80 hover:text-rose-300 hover:bg-rose-950/30 text-xs flex items-center justify-center gap-1.5 transition-colors cursor-pointer border border-transparent hover:border-rose-800/40"
                              title="Delete all downloaded files for this movie"
                            >
                              {deletingMovieId === movie.id ? (
                                <RefreshCw size={12} className="animate-spin" />
                              ) : (
                                <Trash2 size={12} />
                              )}
                              <span>Delete Movie Files</span>
                            </button>
                          </>
                        ) : (
                          /* Not Downloaded: Quick Search Button */
                          <button
                            onClick={() => handleSearchTrackersForMovie(movie.title)}
                            className="w-full py-2 rounded-xl bg-[#1e222a] hover:bg-[#282d38] text-neutral-300 hover:text-white text-xs font-semibold flex items-center justify-center gap-1.5 cursor-pointer transition-colors border border-[#2d323e]"
                          >
                            <Zap size={13} className="text-[#3cd69b]" /> Find Releases on Trackers
                          </button>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB 3: DOWNLOAD QUEUE VIEW */}
        {activeTab === 'queue' && (
          <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">
            {/* Real-time Bandwidth HUD Meter (4 Cols) */}
            <div className="xl:col-span-4 flex flex-col gap-4 p-5 rounded-2xl bg-[#121418] border border-[#21242d] shadow-xl">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-[#3cd69b] text-[20px]">speed</span>
                  <span className="font-bold text-white text-sm font-heading">Client Throughput</span>
                </div>
                <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-[#171920] text-[#38bdf8] border border-[#262a34] font-semibold">
                  Transmission / VPN
                </span>
              </div>

              {/* Live Speed Cards */}
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col p-3 rounded-xl bg-[#090a0c] border border-[#21242d]">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-[10px] font-mono text-[#85948b] uppercase">Downlink</span>
                    <span className="material-symbols-outlined text-[#3cd69b] text-[16px]">arrow_downward</span>
                  </div>
                  <span className="text-xl font-bold font-mono text-[#3cd69b] tracking-tight">
                    {formatSpeed(totalQueueDownSpeed) || '0.0 MB/s'}
                  </span>
                  <span className="text-[10px] font-mono text-[#85948b] mt-0.5">Cap: Unlimited (1 GbE)</span>
                </div>

                <div className="flex flex-col p-3 rounded-xl bg-[#090a0c] border border-[#21242d]">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-[10px] font-mono text-[#85948b] uppercase">Active Swarms</span>
                    <span className="material-symbols-outlined text-[#38bdf8] text-[16px]">cloud_sync</span>
                  </div>
                  <span className="text-xl font-bold font-mono text-[#38bdf8] tracking-tight">
                    {queue.length} Tasks
                  </span>
                  <span className="text-[10px] font-mono text-[#85948b] mt-0.5">WireGuard SOCKS5</span>
                </div>
              </div>

              {/* Realtime SVG Sparkline Graph */}
              <div className="flex flex-col gap-1 p-3 rounded-xl bg-[#090a0c] border border-[#21242d]">
                <div className="flex items-center justify-between text-[10px] font-mono text-[#85948b]">
                  <span>REALTIME TRAFFIC PROFILE</span>
                  <span className="text-[#3cd69b] font-semibold">PEAK 62.1 MB/s</span>
                </div>
                <div className="w-full h-16 relative overflow-hidden rounded bg-[#121418]">
                  <svg className="w-full h-full" preserveAspectRatio="none" viewBox="0 0 320 80">
                    <defs>
                      <linearGradient id="dlGrad" x1="0" x2="0" y1="0" y2="1">
                        <stop offset="0%" stopColor="#3cd69b" stopOpacity="0.35" />
                        <stop offset="100%" stopColor="#3cd69b" stopOpacity="0.0" />
                      </linearGradient>
                    </defs>
                    <path d="M0,60 Q20,55 40,42 T80,35 T120,48 T160,25 T200,30 T240,18 T280,22 T320,15 L320,80 L0,80 Z" fill="url(#dlGrad)" />
                    <path d="M0,60 Q20,55 40,42 T80,35 T120,48 T160,25 T200,30 T240,18 T280,22 T320,15" fill="none" stroke="#60f3b5" strokeWidth="2" />
                  </svg>
                </div>
              </div>

              {/* Quick Global Throttles */}
              <div className="flex items-center justify-between px-3 py-2 rounded-xl bg-[#090a0c] border border-[#21242d] text-[11px] font-mono">
                <span className="text-[#85948b]">Rate Limit:</span>
                <div className="flex items-center gap-1">
                  <span className="px-2 py-0.5 rounded bg-[#171920] text-white">50 MB/s</span>
                  <span className="px-2 py-0.5 rounded bg-[#3cd69b] text-[#003825] font-bold">Unlimited</span>
                </div>
              </div>
            </div>

            {/* Active Torrent Queue (8 Cols) */}
            <div className="xl:col-span-8 flex flex-col gap-4">
              <div className="flex items-center justify-between pb-2 border-b border-[#21242d]">
                <h2 className="text-base font-bold text-white flex items-center gap-2 font-heading">
                  <Activity size={18} className="text-[#3cd69b]" />
                  <span>Active VPN Downloads ({queue.length})</span>
                </h2>
                <button
                  onClick={loadQueue}
                  className="p-1.5 rounded-lg bg-[#121418] border border-[#21242d] text-[#85948b] hover:text-white transition-colors cursor-pointer"
                  title="Refresh Queue"
                >
                  <RefreshCw size={14} />
                </button>
              </div>

              {queue.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-20 rounded-2xl bg-[#121418] border border-[#21242d] text-center">
                  <CheckCircle2 size={40} className="text-[#3cd69b] mb-3 opacity-80" />
                  <h3 className="text-sm font-semibold text-neutral-300">Download queue is empty</h3>
                  <p className="text-xs text-neutral-500 mt-1 max-w-sm">
                    There are no active downloads in progress. Use the search tab to discover tracker releases and download them safely via VPN.
                  </p>
                </div>
              ) : (
                <div className="space-y-3">
                {queue.map((item) => {
                  const targetKey = item.transmissionId || item.id || 0;
                  const isCompleted = item.status.toLowerCase() === 'completed';
                  const isMultiFile = (item.filesCount && item.filesCount > 1) || item.isDirectory || (item.files && item.files.length > 1);

                  return (
                    <div
                      key={`${item.id}-${item.transmissionId || item.releaseTitle}`}
                      className="p-4 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b]/30 flex flex-col gap-3 transition-all"
                    >
                      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                        <div className="flex-1 min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-bold text-sm text-white truncate max-w-md" title={item.movieTitle || item.releaseTitle}>
                              {item.movieTitle || item.releaseTitle || 'Download'}
                            </span>
                            <span className="text-xs px-2 py-0.5 rounded-md bg-[#1a1d24] text-neutral-400 uppercase font-mono text-[10px]">
                              {item.protocol || 'Torrent'}
                            </span>
                            <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-md ${
                              isCompleted
                                ? 'bg-emerald-500/20 text-emerald-300'
                                : item.status.toLowerCase() === 'downloading'
                                ? 'bg-[#3cd69b]/15 text-[#3cd69b]'
                                : item.status.toLowerCase() === 'paused'
                                ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                                : item.status.toLowerCase().includes('metadata') || item.status.toLowerCase().includes('connecting')
                                ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 animate-pulse'
                                : 'bg-amber-500/15 text-amber-300'
                            }`}>
                              {item.status.toLowerCase() === 'paused' ? 'Paused' :
                               item.status.toLowerCase().includes('metadata') ? 'Connecting to Seeders' :
                               item.status}
                            </span>
                            {item.downloadSpeed && item.downloadSpeed > 0 ? (
                              <span className="px-2 py-0.5 rounded-md bg-emerald-500/20 text-emerald-400 font-mono font-bold text-[11px] flex items-center gap-1">
                                <Zap size={11} className="fill-emerald-400" />
                                {formatSpeed(item.downloadSpeed)}
                              </span>
                            ) : null}
                            {item.connectedPeers !== undefined && item.connectedPeers > 0 ? (
                              <span className="text-neutral-400 text-[11px] font-mono">
                                👥 {item.connectedPeers} peers
                              </span>
                            ) : null}
                          </div>

                          {item.releaseTitle && item.releaseTitle !== item.movieTitle && (
                            <p className="text-xs text-neutral-400 mt-1 font-mono line-clamp-1 break-all">
                              {item.releaseTitle}
                            </p>
                          )}

                          {/* Progress Bar */}
                          <div className="mt-3 flex items-center gap-3">
                            <div className="flex-1 h-2 rounded-full bg-[#1e222a] overflow-hidden">
                              <div
                                className={`h-full rounded-full transition-all duration-300 ${
                                  item.status.toLowerCase() === 'paused'
                                    ? 'bg-amber-400/80'
                                    : 'bg-gradient-to-r from-[#3cd69b] to-emerald-400'
                                }`}
                                style={{ width: `${item.progressPercentage}%` }}
                              />
                            </div>
                            <span className="text-xs font-mono font-bold text-neutral-300">
                              {item.progressPercentage.toFixed(1)}%
                            </span>
                          </div>
                        </div>

                        <div className="flex md:flex-col items-end justify-between md:justify-center text-xs text-neutral-400 gap-2 border-t md:border-t-0 border-[#21242d] pt-2 md:pt-0 shrink-0">
                          <div className="flex md:flex-col items-end gap-1">
                            <span>{formatFileSize(item.size - item.sizeLeft)} / {formatFileSize(item.size)}</span>
                            {item.timeLeft && (
                              <span className="text-neutral-500 font-mono text-[11px]">ETA: {item.timeLeft}</span>
                            )}
                            {item.downloadClient && (
                              <span className="text-[10px] text-neutral-500">Client: {item.downloadClient}</span>
                            )}
                          </div>

                          <div className="flex items-center gap-2 flex-wrap justify-end">
                            {/* Completed: 1-Click Device Download Buttons */}
                            {isCompleted && (
                              <>
                                {isMultiFile ? (
                                  <>
                                    <a
                                      href={item.transmissionId ? api.getTorrentDownloadUrl(item.transmissionId, { asZip: true, movieId: item.movieId }) : (item.movieId ? api.getRadarrMovieDownloadUrl(item.movieId) : api.getGenericFileDownloadUrl(item.filePath || '', true))}
                                      download
                                      className="px-3.5 py-1.5 rounded-lg bg-gradient-to-r from-[#3cd69b] to-emerald-400 hover:from-[#32b885] hover:to-emerald-500 text-black text-xs font-bold transition-all flex items-center gap-1.5 shadow-sm cursor-pointer"
                                      title="Download all files in a zipped folder (.zip) to your device"
                                    >
                                      <Download size={13} /> Download All (.zip)
                                    </a>

                                    {item.files && item.files.length > 0 && (
                                      <button
                                        onClick={() => toggleQueueItemFiles(targetKey)}
                                        className="px-2.5 py-1.5 rounded-lg bg-[#1a1d24] hover:bg-[#232732] text-neutral-300 hover:text-white border border-[#262a34] text-xs font-medium transition-all flex items-center gap-1 cursor-pointer"
                                        title="View individual files"
                                      >
                                        <Folder size={12} className="text-amber-400" />
                                        <span>{item.files.length} Files</span>
                                        {expandedQueueIds.has(targetKey) ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
                                      </button>
                                    )}
                                  </>
                                ) : (
                                  <a
                                    href={item.transmissionId ? api.getTorrentDownloadUrl(item.transmissionId, { movieId: item.movieId }) : (item.movieId ? api.getRadarrMovieDownloadUrl(item.movieId) : api.getGenericFileDownloadUrl(item.filePath || ''))}
                                    download
                                    className="px-3.5 py-1.5 rounded-lg bg-[#3cd69b] hover:bg-[#32b885] text-black text-xs font-bold transition-all flex items-center gap-1.5 shadow-sm cursor-pointer"
                                    title="Download file to device"
                                  >
                                    <Download size={13} /> Download to Device
                                  </a>
                                )}
                              </>
                            )}

                            {/* Pause / Resume Button */}
                            {!isCompleted && (
                              item.status.toLowerCase() === 'paused' ? (
                                <button
                                  onClick={() => handleResumeDownload(item)}
                                  disabled={resumingId === (item.transmissionId || item.id) || cancellingId === (item.id || item.transmissionId)}
                                  className="px-3 py-1.5 rounded-lg bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-400 hover:text-emerald-300 border border-emerald-500/30 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                                  title="Resume download"
                                >
                                  {resumingId === (item.transmissionId || item.id) ? (
                                    <RefreshCw size={12} className="animate-spin" />
                                  ) : (
                                    <Play size={12} className="fill-emerald-400" />
                                  )}
                                  Resume
                                </button>
                              ) : (
                                <button
                                  onClick={() => handlePauseDownload(item)}
                                  disabled={pausingId === (item.transmissionId || item.id) || cancellingId === (item.id || item.transmissionId)}
                                  className="px-3 py-1.5 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 hover:text-amber-300 border border-amber-500/30 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                                  title="Pause download (preserves progress)"
                                >
                                  {pausingId === (item.transmissionId || item.id) ? (
                                    <RefreshCw size={12} className="animate-spin" />
                                  ) : (
                                    <Pause size={12} className="fill-amber-400" />
                                  )}
                                  Pause
                                </button>
                              )
                            )}

                            {/* Re-announce / Kickstart button for idle or resolving torrents */}
                            {!isCompleted && item.status.toLowerCase() !== 'paused' && (
                              <button
                                onClick={() => handleKickstartDownload(item)}
                                disabled={kickstartingId === targetKey || cancellingId === targetKey}
                                className="px-2.5 py-1.5 rounded-lg bg-cyan-500/10 hover:bg-cyan-500/20 text-cyan-400 hover:text-cyan-300 border border-cyan-500/30 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                                title="Re-announce to 15+ tier-1 trackers to discover seeders"
                              >
                                <RefreshCw size={12} className={kickstartingId === targetKey ? "animate-spin" : ""} />
                                <span>Re-announce</span>
                              </button>
                            )}

                            {/* Alternative release search button for stuck/idle torrents */}
                            {!isCompleted && (
                              <button
                                onClick={() => handleFindAlternativeRelease(item)}
                                className="px-2.5 py-1.5 rounded-lg bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-400 hover:text-indigo-300 border border-indigo-500/30 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer"
                                title="Search for alternative verified releases with active seeders"
                              >
                                <Search size={12} />
                                <span>Alternative</span>
                              </button>
                            )}

                            <button
                              onClick={() => handleCancelDownload(item)}
                              disabled={cancellingId === (item.id || item.transmissionId) || pausingId === (item.transmissionId || item.id) || resumingId === (item.transmissionId || item.id)}
                              className="px-3 py-1.5 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 hover:text-rose-300 border border-rose-500/30 text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                              title="Cancel download and delete files"
                            >
                              {cancellingId === (item.id || item.transmissionId) ? (
                                <RefreshCw size={12} className="animate-spin" />
                              ) : (
                                <Trash2 size={12} />
                              )}
                              Cancel
                            </button>
                          </div>
                        </div>
                      </div>

                      {/* Expandable Individual Files Drawer */}
                      {expandedQueueIds.has(targetKey) && item.files && item.files.length > 0 && (
                        <div className="pt-3 border-t border-[#21242d] space-y-2">
                          <div className="flex items-center justify-between text-[11px] text-neutral-400 font-mono">
                            <span>Contained Files ({item.files.length})</span>
                            <span className="text-[10px] text-neutral-500">Download individually or use .zip above</span>
                          </div>
                          <div className="max-h-52 overflow-y-auto space-y-1.5 pr-1">
                            {item.files.map((file, fIdx) => (
                              <div
                                key={`${file.filePath}-${fIdx}`}
                                className="flex items-center justify-between gap-3 p-2 rounded-lg bg-[#0e1014] border border-[#1e222a]"
                              >
                                <div className="flex items-center gap-2 min-w-0">
                                  {file.fileType === 'pdf' ? (
                                    <FileText size={14} className="text-rose-400 shrink-0" />
                                  ) : file.fileType === 'music' || file.fileType === 'audio' ? (
                                    <Music size={14} className="text-purple-400 shrink-0" />
                                  ) : file.fileType === 'video' ? (
                                    <Film size={14} className="text-cyan-400 shrink-0" />
                                  ) : file.fileType === 'image' ? (
                                    <ImageIcon size={14} className="text-amber-400 shrink-0" />
                                  ) : (
                                    <File size={14} className="text-neutral-400 shrink-0" />
                                  )}
                                  <div className="min-w-0">
                                    <span className="text-xs text-neutral-200 truncate block font-mono" title={file.fileName}>
                                      {file.fileName}
                                    </span>
                                    <span className="text-[10px] text-neutral-500 font-mono">
                                      {file.formattedSize || formatFileSize(file.fileSizeBytes)}
                                    </span>
                                  </div>
                                </div>
                                <a
                                  href={file.downloadUrl || (item.transmissionId ? api.getTorrentDownloadUrl(item.transmissionId, { fileIndex: fIdx, movieId: item.movieId }) : api.getGenericFileDownloadUrl(file.filePath))}
                                  download
                                  className="px-2.5 py-1 rounded-lg bg-[#1a1d24] hover:bg-[#3cd69b] text-neutral-300 hover:text-black text-xs font-medium transition-colors flex items-center gap-1 shrink-0 cursor-pointer"
                                  title={`Download ${file.fileName}`}
                                >
                                  <Download size={11} />
                                  <span>Download</span>
                                </a>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
            </div>
          </div>
        )}
      </div>

      {/* RELEASES SELECTION MODAL */}
      {selectedMovieForReleases && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm">
          <div className="bg-[#121418] border border-[#21242d] rounded-2xl w-full max-w-4xl max-h-[85vh] flex flex-col shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            {/* Modal Header */}
            <div className="px-6 py-4 border-b border-[#21242d] flex items-center justify-between bg-[#161920]">
              <div className="flex items-center gap-3">
                <div className="p-2 rounded-lg bg-[#3cd69b]/10 text-[#3cd69b]">
                  <Layers size={20} />
                </div>
                <div>
                  <h2 className="text-base font-bold text-white">
                    Available Releases & Quality Options
                  </h2>
                  <p className="text-xs text-neutral-400">
                    {selectedMovieForReleases.title} ({selectedMovieForReleases.year})
                  </p>
                </div>
              </div>
              <button
                onClick={() => setSelectedMovieForReleases(null)}
                className="p-1.5 rounded-lg text-neutral-400 hover:text-white hover:bg-[#21242d] transition-colors cursor-pointer"
              >
                <X size={18} />
              </button>
            </div>

            {/* Releases Table / List */}
            <div className="p-6 overflow-y-auto flex-1 space-y-3">
              {loadingReleases ? (
                <div className="flex flex-col items-center justify-center py-16 text-neutral-400">
                  <RefreshCw size={28} className="animate-spin text-[#3cd69b] mb-3" />
                  <p className="text-sm font-medium">Contacting indexers for {selectedMovieForReleases.title}...</p>
                  <p className="text-xs text-neutral-500 mt-1">Fetching torrent releases via Radarr</p>
                </div>
              ) : releases.length === 0 ? (
                <div className="text-center py-10 px-4 text-neutral-400 max-w-lg mx-auto">
                  <div className="w-12 h-12 rounded-full bg-amber-500/10 text-amber-400 flex items-center justify-center mx-auto mb-3 border border-amber-500/20">
                    <AlertTriangle size={24} />
                  </div>
                  <h4 className="text-sm font-semibold text-neutral-200">No releases found for this movie</h4>
                  <p className="text-xs text-neutral-400 mt-2 leading-relaxed">
                    Radarr searched active indexers, but none returned releases matching <span className="text-white font-medium">"{selectedMovieForReleases.title}"</span>.
                  </p>
                  <div className="mt-4 p-3.5 rounded-xl bg-[#161922] border border-[#262b37] text-left text-[11px] text-neutral-300 space-y-2">
                    <div className="font-semibold text-emerald-400 flex items-center gap-1.5">
                      💡 Want more releases?
                    </div>
                    <p className="text-neutral-400 leading-relaxed">
                      Use the <b>Tracker Releases</b> tab to search all indexers directly without Radarr parsing constraints.
                    </p>
                    <button
                      onClick={() => {
                        const title = selectedMovieForReleases.title;
                        setSelectedMovieForReleases(null);
                        handleSearchTrackersForMovie(title);
                      }}
                      className="mt-2 w-full py-2 px-3 rounded-lg bg-[#3cd69b]/15 hover:bg-[#3cd69b]/25 text-[#3cd69b] font-semibold border border-[#3cd69b]/30 transition-all flex items-center justify-center gap-1.5 cursor-pointer text-xs"
                    >
                      <Zap size={13} /> Search Raw Trackers (100+) for "{selectedMovieForReleases.title}"
                    </button>
                  </div>
                </div>
              ) : (
                <div className="space-y-2">
                  {releases.every((r) => !r.approved) && (
                    <div className="p-3.5 rounded-xl bg-amber-500/10 border border-amber-500/25 text-xs text-amber-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-3">
                      <div className="flex items-start gap-2">
                        <AlertTriangle size={16} className="text-amber-400 shrink-0 mt-0.5" />
                        <div>
                          <p className="font-semibold text-amber-300">Releases found, but none passed Radarr's strict movie matching</p>
                          <p className="text-[11px] text-amber-200/80 mt-0.5">The indexers returned releases, but Radarr rejected them. You can search raw trackers directly to find working torrents.</p>
                        </div>
                      </div>
                      <button
                        onClick={() => {
                          const title = selectedMovieForReleases.title;
                          setSelectedMovieForReleases(null);
                          handleSearchTrackersForMovie(title);
                        }}
                        className="py-1.5 px-3 rounded-lg bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 text-xs font-semibold border border-amber-500/40 transition-all shrink-0 flex items-center gap-1 cursor-pointer"
                      >
                        <Zap size={12} /> Search Trackers Directly
                      </button>
                    </div>
                  )}

                  <div className="flex items-center justify-between text-xs text-neutral-400 px-2 pb-1">
                    <span>Found {releases.length} releases</span>
                    <span>Sorted by quality & seeders</span>
                  </div>

                  {releases.map((rel) => (
                    <div
                      key={rel.guid}
                      className={`p-3.5 rounded-xl border transition-all flex flex-col sm:flex-row sm:items-center justify-between gap-3 ${
                        rel.approved
                          ? 'bg-[#171a21] border-[#262a34] hover:border-[#3cd69b]/40'
                          : 'bg-[#15171d] border-[#22252e] opacity-75'
                      }`}
                    >
                      <div className="flex-1 min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-semibold text-xs text-white break-all">
                            {rel.title}
                          </span>
                        </div>

                        <div className="flex flex-wrap items-center gap-2.5 mt-2 text-[11px] text-neutral-400">
                          {rel.quality && (
                            <span className="px-2 py-0.5 rounded-md bg-[#212631] text-[#3cd69b] font-mono font-semibold">
                              {rel.quality}
                            </span>
                          )}

                          <span className="font-mono">{formatFileSize(rel.size)}</span>

                          {rel.seeders !== null && rel.seeders !== undefined && (
                            <span className="text-emerald-400 font-mono font-medium">
                              ▲ {rel.seeders} seeders
                            </span>
                          )}

                          {rel.leechers !== null && rel.leechers !== undefined && (
                            <span className="text-rose-400 font-mono">
                              ▼ {rel.leechers}
                            </span>
                          )}

                          <span className="px-1.5 py-0.5 rounded bg-[#1e222a] text-neutral-300 text-[10px]">
                            {rel.indexer}
                          </span>

                          <span className="text-neutral-500 text-[10px] uppercase">
                            {rel.protocol}
                          </span>
                        </div>

                        {rel.rejections && rel.rejections.length > 0 && (
                          <div className="mt-2 text-[11px] text-amber-400/90 flex items-center gap-1">
                            <AlertTriangle size={11} />
                            <span>{rel.rejections.join('; ')}</span>
                          </div>
                        )}
                      </div>

                      <button
                        onClick={() => handleDownloadRelease(rel)}
                        disabled={downloadingGuid === rel.guid || !rel.downloadAllowed}
                        className={`px-4 py-2 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-1.5 shrink-0 cursor-pointer shadow-sm ${
                          rel.downloadAllowed
                            ? 'bg-[#3cd69b] hover:bg-[#32b885] text-black disabled:opacity-50'
                            : 'bg-[#21242d] text-neutral-500 cursor-not-allowed'
                        }`}
                      >
                        {downloadingGuid === rel.guid ? (
                          <RefreshCw size={13} className="animate-spin" />
                        ) : (
                          <>
                            <Download size={13} /> Download
                          </>
                        )}
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-3 border-t border-[#21242d] bg-[#161920] flex items-center justify-end">
              <button
                onClick={() => setSelectedMovieForReleases(null)}
                className="px-4 py-1.5 rounded-lg bg-[#21242d] hover:bg-[#2a2e39] text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* DELETE ALL DOWNLOADED FILES CONFIRMATION MODAL */}
      {showDeleteAllModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div className="bg-[#121418] border border-rose-500/40 rounded-2xl max-w-lg w-full p-6 shadow-2xl">
            <div className="flex items-center gap-3 text-rose-400 mb-4">
              <div className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/20">
                <AlertTriangle size={24} />
              </div>
              <div>
                <h3 className="text-lg font-bold text-white">Delete All Downloaded Files?</h3>
                <p className="text-xs text-rose-300/80">Reclaim disk space across all downloaded media</p>
              </div>
            </div>

            <div className="p-4 rounded-xl bg-[#181a20] border border-[#21242d] text-xs text-neutral-300 space-y-2 mb-5">
              <p>
                This action will delete all downloaded video files, PDFs, subtitles, posters, and extra media from disk for all <strong className="text-white">{downloadedMovies.length}</strong> downloaded movies.
              </p>
              <div className="flex items-center gap-2 pt-1 text-emerald-400 font-medium">
                <HardDrive size={14} />
                <span>Total space to be reclaimed: ~{downloadSummary?.formattedTotalSize || formatFileSize(downloadSummary?.totalSizeBytes)}</span>
              </div>
              <div className="flex items-center gap-2 pt-2 border-t border-[#252833] text-neutral-400">
                <Shield size={14} className="text-[#3cd69b] shrink-0" />
                <span>
                  The server root folder <strong className="text-neutral-200 font-mono">/media/Radarr</strong> is non-deletable and will remain permanently intact.
                </span>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3">
              <button
                onClick={() => setShowDeleteAllModal(false)}
                disabled={isDeletingAll}
                className="px-4 py-2 rounded-xl bg-[#21242d] hover:bg-[#2a2e39] text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteAllDownloaded}
                disabled={isDeletingAll}
                className="px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-xs font-bold text-white transition-colors cursor-pointer flex items-center gap-1.5 shadow-lg shadow-rose-900/30 disabled:opacity-50"
              >
                {isDeletingAll ? (
                  <>
                    <RefreshCw size={13} className="animate-spin" /> Deleting All Files...
                  </>
                ) : (
                  <>
                    <Trash2 size={13} /> Yes, Delete All Files
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* DELETE SELECTED FILES CONFIRMATION MODAL */}
      {showDeleteSelectedModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div className="bg-[#121418] border border-amber-500/40 rounded-2xl max-w-lg w-full p-6 shadow-2xl">
            <div className="flex items-center gap-3 text-amber-400 mb-4">
              <div className="p-3 rounded-xl bg-amber-500/10 border border-amber-500/20">
                <Trash2 size={24} />
              </div>
              <div>
                <h3 className="text-lg font-bold text-white">Delete Selected Files?</h3>
                <p className="text-xs text-amber-300/80">Batch remove downloaded media from disk</p>
              </div>
            </div>

            <div className="p-4 rounded-xl bg-[#181a20] border border-[#21242d] text-xs text-neutral-300 space-y-2 mb-5">
              <p>
                Are you sure you want to delete all downloaded files for the <strong className="text-white">{selectedMovieIds.size}</strong> selected movie(s)?
              </p>
              <div className="max-h-36 overflow-y-auto space-y-1 py-1 pr-1">
                {downloadedMovies
                  .filter((m) => selectedMovieIds.has(m.id))
                  .slice(0, 8)
                  .map((m) => (
                    <div
                      key={m.id}
                      className="flex items-center justify-between text-[11px] text-neutral-300 bg-[#121418] px-2.5 py-1.5 rounded-lg border border-[#21242d]"
                    >
                      <span className="truncate font-medium">{m.title}</span>
                      <span className="text-neutral-500 shrink-0 font-mono ml-2">
                        {formatFileSize(m.sizeOnDisk)}
                      </span>
                    </div>
                  ))}
                {selectedMovieIds.size > 8 && (
                  <p className="text-[11px] text-neutral-500 text-center pt-1">
                    ...and {selectedMovieIds.size - 8} more movies
                  </p>
                )}
              </div>
              <div className="flex items-center gap-2 pt-2 border-t border-[#252833] text-neutral-400">
                <Shield size={14} className="text-[#3cd69b] shrink-0" />
                <span>
                  All associated videos, PDFs, and subtitles will be removed, while <strong className="text-neutral-200 font-mono">/media/Radarr</strong> remains protected.
                </span>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3">
              <button
                onClick={() => setShowDeleteSelectedModal(false)}
                disabled={isDeletingSelected}
                className="px-4 py-2 rounded-xl bg-[#21242d] hover:bg-[#2a2e39] text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteSelected}
                disabled={isDeletingSelected}
                className="px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-xs font-bold text-white transition-colors cursor-pointer flex items-center gap-1.5 shadow-lg shadow-rose-900/30 disabled:opacity-50"
              >
                {isDeletingSelected ? (
                  <>
                    <RefreshCw size={13} className="animate-spin" /> Deleting Selected...
                  </>
                ) : (
                  <>
                    <Trash2 size={13} /> Delete {selectedMovieIds.size} Selected
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
