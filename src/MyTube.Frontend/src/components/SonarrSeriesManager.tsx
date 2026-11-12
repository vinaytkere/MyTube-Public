import React, { useState, useEffect } from 'react';
import { 
  Tv, Search, Plus, CheckCircle2, Download, AlertCircle, Loader2, 
  ChevronDown, ChevronUp, Play, Star, Calendar, Folder, ExternalLink, RefreshCw, Layers
} from 'lucide-react';
import { api } from '../services/api';
import { 
  SonarrSeries, SonarrEpisode, SonarrRelease, SonarrQueueItem, 
  SonarrQualityProfile, SonarrRootFolder, SonarrStatus 
} from '../types';
import { useToast } from './Toast';
import { Link } from 'react-router-dom';

export const SonarrSeriesManager: React.FC = () => {
  const { showToast } = useToast();

  const [activeTab, setActiveTab] = useState<'library' | 'search' | 'queue'>('library');
  const [status, setStatus] = useState<SonarrStatus | null>(null);
  const [loadingStatus, setLoadingStatus] = useState(true);

  // Library
  const [seriesList, setSeriesList] = useState<SonarrSeries[]>([]);
  const [loadingLibrary, setLoadingLibrary] = useState(false);
  const [expandedSeriesId, setExpandedSeriesId] = useState<number | null>(null);
  const [episodesMap, setEpisodesMap] = useState<Record<number, SonarrEpisode[]>>({});
  const [loadingEpisodes, setLoadingEpisodes] = useState<number | null>(null);

  // Search
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SonarrSeries[]>([]);
  const [searching, setSearching] = useState(false);

  // Add Series Modal / Options
  const [profiles, setProfiles] = useState<SonarrQualityProfile[]>([]);
  const [rootFolders, setRootFolders] = useState<SonarrRootFolder[]>([]);
  const [addingSeriesId, setAddingSeriesId] = useState<number | null>(null);

  // Releases Modal
  const [selectedEpisode, setSelectedEpisode] = useState<{ series: SonarrSeries; episode?: SonarrEpisode } | null>(null);
  const [releases, setReleases] = useState<SonarrRelease[]>([]);
  const [loadingReleases, setLoadingReleases] = useState(false);
  const [downloadingGuid, setDownloadingGuid] = useState<string | null>(null);

  // Queue
  const [queue, setQueue] = useState<SonarrQueueItem[]>([]);
  const [loadingQueue, setLoadingQueue] = useState(false);

  const loadStatus = async () => {
    try {
      setLoadingStatus(true);
      const res = await api.getSonarrStatus();
      setStatus(res);
      if (res.isConnected) {
        loadLibrary();
        loadProfilesAndFolders();
      }
    } catch (err) {
      console.error('Failed to get Sonarr status:', err);
    } finally {
      setLoadingStatus(false);
    }
  };

  const loadProfilesAndFolders = async () => {
    try {
      const [p, f] = await Promise.all([
        api.getSonarrQualityProfiles(),
        api.getSonarrRootFolders()
      ]);
      setProfiles(p);
      setRootFolders(f);
    } catch (err) {
      console.error('Failed to load Sonarr profiles or root folders:', err);
    }
  };

  const loadLibrary = async () => {
    try {
      setLoadingLibrary(true);
      const list = await api.getSonarrLibrary();
      setSeriesList(list);
    } catch (err: any) {
      console.error('Failed to load Sonarr series library:', err);
      showToast('error', err.message || 'Failed to load Sonarr series library.', 'Sonarr Error');
    } finally {
      setLoadingLibrary(false);
    }
  };

  const loadQueue = async () => {
    try {
      setLoadingQueue(true);
      const q = await api.getSonarrQueue();
      setQueue(q);
    } catch (err) {
      console.error('Failed to load queue:', err);
    } finally {
      setLoadingQueue(false);
    }
  };

  useEffect(() => {
    loadStatus();
  }, []);

  useEffect(() => {
    if (activeTab === 'queue' && status?.isConnected) {
      loadQueue();
      const interval = setInterval(loadQueue, 5000);
      return () => clearInterval(interval);
    }
  }, [activeTab, status?.isConnected]);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;

    try {
      setSearching(true);
      const results = await api.searchSonarrSeries(searchQuery.trim());
      setSearchResults(results);
      if (results.length === 0) {
        showToast('info', `No TV series found for "${searchQuery}".`, 'Search');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to search TV series.', 'Error');
    } finally {
      setSearching(false);
    }
  };

  const handleToggleExpandSeries = async (series: SonarrSeries) => {
    if (expandedSeriesId === series.id) {
      setExpandedSeriesId(null);
      return;
    }

    setExpandedSeriesId(series.id);
    if (!episodesMap[series.id]) {
      try {
        setLoadingEpisodes(series.id);
        const eps = await api.getSonarrEpisodes(series.id);
        setEpisodesMap(prev => ({ ...prev, [series.id]: eps }));
      } catch (err: any) {
        showToast('error', err.message || 'Failed to load episodes.', 'Error');
      } finally {
        setLoadingEpisodes(null);
      }
    }
  };

  const handleAddSeries = async (s: SonarrSeries) => {
    try {
      setAddingSeriesId(s.tvdbId);
      const rootFolder = rootFolders[0]?.path || '/tv';
      const profileId = profiles[0]?.id || 1;

      await api.addSonarrSeries({
        title: s.title,
        tvdbId: s.tvdbId,
        qualityProfileId: profileId,
        rootFolderPath: rootFolder,
        monitored: true,
        seasonFolder: true,
        searchForMissingEpisodes: true
      });

      showToast('success', `Added "${s.title}" to Sonarr library!`, 'Series Added');
      loadLibrary();
      setActiveTab('library');
    } catch (err: any) {
      let msg = err.message || 'Failed to add series to Sonarr.';
      try {
        if (msg.includes('Sonarr failed to add series: [')) {
          const jsonPart = msg.substring(msg.indexOf('['), msg.lastIndexOf(']') + 1);
          const parsed = JSON.parse(jsonPart);
          if (Array.isArray(parsed) && parsed[0]?.errorMessage) {
            msg = parsed.map((e: any) => e.errorMessage).join(', ');
          }
        }
      } catch {}
      showToast('error', msg, 'Add Failed');
    } finally {
      setAddingSeriesId(null);
    }
  };

  const handleOpenReleases = async (series: SonarrSeries, episode?: SonarrEpisode) => {
    setSelectedEpisode({ series, episode });
    try {
      setLoadingReleases(true);
      setReleases([]);
      const rels = await api.getSonarrReleases(series.id, episode?.id);
      setReleases(rels);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to search releases for episode.', 'Releases Error');
    } finally {
      setLoadingReleases(false);
    }
  };

  const handleDownloadRelease = async (release: SonarrRelease) => {
    if (!selectedEpisode) return;

    try {
      setDownloadingGuid(release.guid);
      const res = await api.downloadSonarrRelease({
        guid: release.guid,
        indexerId: release.indexerId,
        seriesId: selectedEpisode.series.id,
        episodeId: selectedEpisode.episode?.id
      });
      showToast('success', res.message || 'Release sent to download client.', 'Download Started');
      setSelectedEpisode(null);
    } catch (err: any) {
      showToast('error', err.message || 'Failed to dispatch download.', 'Download Error');
    } finally {
      setDownloadingGuid(null);
    }
  };

  if (!status?.isConnected && !loadingStatus) {
    return (
      <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-8 text-center space-y-4">
        <div className="w-14 h-14 rounded-2xl bg-amber-500/10 border border-amber-500/20 text-amber-400 flex items-center justify-center mx-auto">
          <Tv size={28} />
        </div>
        <div>
          <h3 className="text-base font-bold text-neutral-100">Sonarr Server is Offline or Not Configured</h3>
          <p className="text-xs text-neutral-400 max-w-md mx-auto mt-1">
            To manage TV series, monitor seasons, and auto-download episodes, please configure your Sonarr URL and API key in Settings.
          </p>
        </div>
        <Link
          to="/settings"
          className="inline-flex items-center gap-2 px-4 py-2 bg-[#3cd69b] text-neutral-950 font-bold rounded-xl text-xs hover:bg-[#34b785] transition-all"
        >
          Configure Sonarr in Settings
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      
      {/* Sub-Tabs: Library / Search / Queue */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-[#21242d] pb-4">
        <div className="flex items-center gap-2 bg-[#0c0d10] p-1 rounded-xl border border-[#21242d]">
          <button
            onClick={() => setActiveTab('library')}
            className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
              activeTab === 'library'
                ? 'bg-[#171920] text-[#3cd69b] shadow'
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            Series Library ({seriesList.length})
          </button>

          <button
            onClick={() => setActiveTab('search')}
            className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
              activeTab === 'search'
                ? 'bg-[#171920] text-[#3cd69b] shadow'
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            Add New TV Show
          </button>

          <button
            onClick={() => setActiveTab('queue')}
            className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
              activeTab === 'queue'
                ? 'bg-[#171920] text-[#3cd69b] shadow'
                : 'text-neutral-400 hover:text-white'
            }`}
          >
            Download Queue ({queue.length})
          </button>
        </div>

        <button
          onClick={loadLibrary}
          disabled={loadingLibrary}
          className="px-3 py-1.5 rounded-xl text-xs font-semibold bg-[#121418] border border-[#262a34] text-neutral-300 hover:text-white transition-all flex items-center gap-1.5 self-start cursor-pointer"
        >
          <RefreshCw size={12} className={loadingLibrary ? 'animate-spin' : ''} />
          <span>Refresh Library</span>
        </button>
      </div>

      {/* SEARCH TAB */}
      {activeTab === 'search' && (
        <div className="space-y-6">
          <form onSubmit={handleSearch} className="flex gap-2 max-w-xl">
            <div className="relative flex-1">
              <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-neutral-500" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search TV series (e.g. Breaking Bad, Severance, Arcane)..."
                className="w-full pl-10 pr-4 py-2.5 bg-[#121418] border border-[#21242d] rounded-xl text-xs text-neutral-100 focus:outline-none focus:border-[#3cd69b] transition-all"
              />
            </div>
            <button
              type="submit"
              disabled={searching}
              className="px-5 py-2.5 rounded-xl bg-[#3cd69b] hover:bg-[#34b785] text-neutral-950 font-bold text-xs transition-all flex items-center gap-2 cursor-pointer disabled:opacity-50"
            >
              {searching ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
              Search
            </button>
          </form>

          {/* Results Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            {searchResults.map((s) => (
              <div 
                key={s.tvdbId}
                className="bg-[#121418] border border-[#21242d] rounded-2xl overflow-hidden flex flex-col shadow-lg hover:border-[#3cd69b]/40 transition-all group"
              >
                <div className="aspect-[2/3] bg-[#0c0d10] relative overflow-hidden">
                  {s.posterUrl ? (
                    <img src={s.posterUrl} alt={s.title} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500" />
                  ) : (
                    <div className="w-full h-full flex items-center justify-center text-neutral-600">
                      <Tv size={40} />
                    </div>
                  )}
                  {s.network && (
                    <span className="absolute top-2 left-2 bg-black/70 backdrop-blur-md px-2 py-0.5 rounded text-[10px] font-semibold text-neutral-200">
                      {s.network}
                    </span>
                  )}
                  <span className="absolute top-2 right-2 bg-emerald-500/90 text-neutral-950 px-2 py-0.5 rounded text-[10px] font-bold">
                    {s.year}
                  </span>
                </div>

                <div className="p-4 flex-1 flex flex-col justify-between space-y-3">
                  <div>
                    <h4 className="font-bold text-sm text-neutral-100 line-clamp-1 group-hover:text-[#3cd69b] transition-colors">{s.title}</h4>
                    <p className="text-[11px] text-neutral-400 line-clamp-2 mt-1">{s.overview || 'No overview available.'}</p>
                  </div>

                  <div className="pt-3 border-t border-[#21242d] flex items-center justify-between">
                    <span className="text-[10px] text-neutral-500">{s.seasons?.length || 0} Seasons</span>
                    <button
                      onClick={() => handleAddSeries(s)}
                      disabled={addingSeriesId === s.tvdbId}
                      className="px-3 py-1.5 rounded-lg text-xs font-bold bg-[#3cd69b] hover:bg-[#34b785] text-neutral-950 transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                    >
                      {addingSeriesId === s.tvdbId ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                      Add Series
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* LIBRARY TAB */}
      {activeTab === 'library' && (
        <div className="space-y-4">
          {loadingLibrary ? (
            <div className="p-8 text-center text-xs text-neutral-400 flex items-center justify-center gap-2">
              <Loader2 size={16} className="animate-spin text-[#3cd69b]" />
              Loading TV Series Library...
            </div>
          ) : seriesList.length === 0 ? (
            <div className="p-8 bg-[#121418] border border-[#21242d] rounded-2xl text-center space-y-2">
              <Tv size={32} className="text-neutral-600 mx-auto" />
              <p className="text-xs text-neutral-400">No TV series found in Sonarr library.</p>
              <button
                onClick={() => setActiveTab('search')}
                className="text-xs text-[#3cd69b] hover:underline font-semibold cursor-pointer"
              >
                Search and add your first TV show
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              {seriesList.map((series) => {
                const isExpanded = expandedSeriesId === series.id;
                const episodes = episodesMap[series.id] || [];

                return (
                  <div 
                    key={series.id}
                    className="bg-[#121418] border border-[#21242d] rounded-2xl overflow-hidden transition-all shadow-md"
                  >
                    {/* Header Row */}
                    <div 
                      onClick={() => handleToggleExpandSeries(series)}
                      className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 cursor-pointer hover:bg-[#171920]/60 transition-colors"
                    >
                      <div className="flex items-center gap-3.5">
                        <div className="w-12 h-16 bg-[#0c0d10] rounded-xl overflow-hidden shrink-0 border border-[#21242d]">
                          {series.posterUrl ? (
                            <img src={series.posterUrl} alt={series.title} className="w-full h-full object-cover" />
                          ) : (
                            <div className="w-full h-full flex items-center justify-center text-neutral-600">
                              <Tv size={20} />
                            </div>
                          )}
                        </div>

                        <div>
                          <div className="flex items-center gap-2">
                            <h4 className="text-sm font-bold text-neutral-100">{series.title}</h4>
                            <span className="text-[10px] bg-[#171920] border border-[#262a34] text-neutral-400 px-2 py-0.5 rounded-full">
                              {series.year}
                            </span>
                            {series.isAvailableInHomelab && (
                              <span className="text-[10px] bg-emerald-500/20 text-[#3cd69b] px-2 py-0.5 rounded-full border border-emerald-500/30 font-semibold">
                                Ready in MyTube ({series.homelabMatchedEpisodes} eps)
                              </span>
                            )}
                          </div>
                          <p className="text-xs text-neutral-400 line-clamp-1 mt-0.5">{series.overview}</p>
                        </div>
                      </div>

                      <div className="flex items-center gap-4 self-end sm:self-center shrink-0">
                        <div className="text-right text-xs">
                          <span className="text-neutral-300 font-semibold block">
                            {series.episodeFileCount} / {series.totalEpisodeCount} eps
                          </span>
                          <span className="text-[10px] text-neutral-500">
                            {series.seasonCount} Seasons • {series.status}
                          </span>
                        </div>

                        <div className="p-2 rounded-xl bg-[#0c0d10] border border-[#21242d] text-neutral-400">
                          {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                        </div>
                      </div>
                    </div>

                    {/* Expanded Episodes Panel */}
                    {isExpanded && (
                      <div className="border-t border-[#21242d] bg-[#0c0d10] p-4 space-y-3">
                        <div className="flex items-center justify-between text-xs text-neutral-400 pb-2">
                          <span className="font-semibold text-neutral-300 flex items-center gap-1.5">
                            <Layers size={14} className="text-[#3cd69b]" />
                            Seasons & Episodes
                          </span>
                          <span>Path: {series.path}</span>
                        </div>

                        {loadingEpisodes === series.id ? (
                          <div className="py-6 text-center text-xs text-neutral-500 flex items-center justify-center gap-2">
                            <Loader2 size={14} className="animate-spin text-[#3cd69b]" />
                            Loading episode lists...
                          </div>
                        ) : episodes.length === 0 ? (
                          <p className="text-xs text-neutral-500 py-3 text-center">No episodes found.</p>
                        ) : (
                          <div className="max-h-72 overflow-y-auto space-y-1 pr-1 divide-y divide-[#171920]">
                            {episodes.map((ep) => (
                              <div 
                                key={ep.id}
                                className="py-2.5 px-3 flex items-center justify-between gap-3 text-xs hover:bg-[#121418] rounded-xl transition-colors"
                              >
                                <div className="flex items-center gap-3">
                                  <span className="font-mono text-emerald-400 font-semibold w-16 shrink-0">
                                    S{String(ep.seasonNumber).padStart(2, '0')}E{String(ep.episodeNumber).padStart(2, '0')}
                                  </span>
                                  <span className="font-medium text-neutral-200 line-clamp-1">{ep.title || 'TBA'}</span>
                                  {ep.airDate && (
                                    <span className="text-[10px] text-neutral-500 hidden sm:inline">{ep.airDate}</span>
                                  )}
                                </div>

                                <div className="flex items-center gap-2 shrink-0">
                                  {ep.localVideoId ? (
                                    <Link
                                      to={`/video/${ep.localVideoId}`}
                                      className="px-2.5 py-1 bg-emerald-500/20 hover:bg-emerald-500/30 text-[#3cd69b] rounded-lg text-[10px] font-bold border border-emerald-500/30 flex items-center gap-1"
                                    >
                                      <Play size={10} /> Watch in MyTube
                                    </Link>
                                  ) : ep.hasFile ? (
                                    <span className="px-2 py-0.5 bg-emerald-500/10 text-[#3cd69b] rounded text-[10px] font-semibold border border-emerald-500/20">
                                      Downloaded
                                    </span>
                                  ) : (
                                    <span className="px-2 py-0.5 bg-amber-500/10 text-amber-400 rounded text-[10px] font-semibold border border-amber-500/20">
                                      Missing
                                    </span>
                                  )}

                                  <button
                                    onClick={() => handleOpenReleases(series, ep)}
                                    className="p-1.5 rounded-lg bg-[#171920] border border-[#262a34] text-neutral-400 hover:text-white transition-colors cursor-pointer"
                                    title="Search releases for this episode"
                                  >
                                    <Search size={12} />
                                  </button>
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* QUEUE TAB */}
      {activeTab === 'queue' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between pb-2">
            <h4 className="text-sm font-bold text-neutral-200">Active TV Episode Downloads</h4>
            <span className="text-xs text-neutral-500">Auto-refreshes every 5s</span>
          </div>

          {queue.length === 0 ? (
            <div className="p-8 bg-[#121418] border border-[#21242d] rounded-2xl text-center text-xs text-neutral-500">
              No active episode downloads in Sonarr queue.
            </div>
          ) : (
            <div className="space-y-2">
              {queue.map((item) => (
                <div key={item.id} className="p-4 bg-[#121418] border border-[#21242d] rounded-2xl space-y-2 shadow-md">
                  <div className="flex items-center justify-between text-xs">
                    <div>
                      <span className="font-bold text-neutral-100">{item.seriesTitle}</span>
                      <span className="font-mono text-emerald-400 ml-2">
                        S{String(item.seasonNumber).padStart(2, '0')}E{String(item.episodeNumber).padStart(2, '0')}
                      </span>
                      <span className="text-neutral-400 ml-1.5">• {item.episodeTitle}</span>
                    </div>
                    <span className="font-semibold text-neutral-300">{item.progress}%</span>
                  </div>

                  {/* Progress bar */}
                  <div className="h-1.5 w-full bg-[#0c0d10] rounded-full overflow-hidden">
                    <div 
                      className="h-full bg-[#3cd69b] transition-all duration-300"
                      style={{ width: `${item.progress}%` }}
                    />
                  </div>

                  <div className="flex items-center justify-between text-[11px] text-neutral-500">
                    <span>Quality: {item.quality} • Status: {item.status}</span>
                    <span>ETA: {item.timeleft || 'Unknown'}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Releases Sheet Modal */}
      {selectedEpisode && (
        <div className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-[#121418] border border-[#21242d] rounded-2xl max-w-3xl w-full max-h-[85vh] flex flex-col shadow-2xl overflow-hidden">
            <div className="p-5 border-b border-[#21242d] flex items-center justify-between">
              <div>
                <h3 className="font-bold text-sm text-neutral-100">
                  Releases for {selectedEpisode.series.title}
                  {selectedEpisode.episode && ` - S${String(selectedEpisode.episode.seasonNumber).padStart(2, '0')}E${String(selectedEpisode.episode.episodeNumber).padStart(2, '0')}`}
                </h3>
                <p className="text-xs text-neutral-400 mt-0.5">Pick a release to send to your torrent or download client</p>
              </div>
              <button
                onClick={() => setSelectedEpisode(null)}
                className="p-1.5 text-neutral-400 hover:text-white rounded-lg bg-[#171920] cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div className="p-5 overflow-y-auto flex-1 space-y-2">
              {loadingReleases ? (
                <div className="py-12 text-center text-xs text-neutral-400 flex items-center justify-center gap-2">
                  <Loader2 size={16} className="animate-spin text-[#3cd69b]" />
                  Searching indexers via Sonarr...
                </div>
              ) : releases.length === 0 ? (
                <p className="text-xs text-neutral-400 text-center py-8">No releases found for this episode.</p>
              ) : (
                releases.map((rel) => (
                  <div 
                    key={rel.guid}
                    className="p-3 bg-[#0c0d10] border border-[#21242d] rounded-xl flex items-center justify-between gap-3 text-xs hover:border-[#3cd69b]/40 transition-all"
                  >
                    <div className="space-y-1 min-w-0">
                      <div className="font-semibold text-neutral-200 truncate" title={rel.title}>
                        {rel.title}
                      </div>
                      <div className="flex items-center gap-2 text-[10px] text-neutral-500">
                        <span className="text-emerald-400 font-medium">{rel.quality}</span>
                        <span>•</span>
                        <span>{rel.indexer}</span>
                        <span>•</span>
                        <span>Seeders: {rel.seeders ?? 0}</span>
                        <span>•</span>
                        <span>{(rel.size / (1024 * 1024)).toFixed(0)} MB</span>
                      </div>
                    </div>

                    <button
                      onClick={() => handleDownloadRelease(rel)}
                      disabled={downloadingGuid === rel.guid || !rel.approved}
                      className="px-3 py-1.5 rounded-lg text-xs font-bold bg-[#3cd69b] hover:bg-[#34b785] text-neutral-950 transition-all flex items-center gap-1.5 shrink-0 cursor-pointer disabled:opacity-40"
                    >
                      {downloadingGuid === rel.guid ? <Loader2 size={12} className="animate-spin" /> : <Download size={12} />}
                      Grab
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

    </div>
  );
};
