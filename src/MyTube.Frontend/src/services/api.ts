import { 
  Video, Category, WatchProgress, User, PagedResult, DownloadJob, VideoProbeResult, DownloaderSettings,
  BatchDownloadRequest, BatchDownloadResponse, DownloadBatch, PlaylistProbeResult,
  RadarrStatus, RadarrMovie, RadarrRelease, RadarrQueueItem, RadarrAddMovieRequest, 
  RadarrQualityProfile, RadarrRootFolder, RadarrSettings, ProwlarrRelease, RadarrDownloadSummary,
  FileNamingConfigResponse, FileNamingSettings, FileRenamePreviewRequest,
  FileRenamePreviewResult, FileRenameExecuteRequest, FileRenameExecuteResult,
  RecycleBinSummary, SystemHealthInfo,
  SonarrStatus, SonarrSeries, SonarrEpisode, SonarrRelease, SonarrQueueItem,
  SonarrAddSeriesRequest, SonarrQualityProfile, SonarrRootFolder, SonarrSettings,
  MediaOrganizeResult, MediaRenamePreview, MediaOrganizerSettings,
  CategoryConsolidationResult, MediaAuditReport,
  IptvChannel, IptvFilterOptions, IptvSyncStatus
} from '../types';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string) || 'http://localhost:5003';

class ApiService {
  private token: string | null = localStorage.getItem('mytube_token');

  setToken(token: string | null) {
    this.token = token;
    if (token) {
      localStorage.setItem('mytube_token', token);
    } else {
      localStorage.removeItem('mytube_token');
    }
  }

  getToken(): string | null {
    return this.token;
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    
    const headers = new Headers(options.headers || {});
    if (this.token) {
      headers.set('Authorization', `Bearer ${this.token}`);
    }
    
    // Default to JSON content type for non-GET/HEAD requests
    if (options.body && !headers.has('Content-Type') && !(options.body instanceof FormData)) {
      headers.set('Content-Type', 'application/json');
    }

    let response: Response;
    try {
      response = await fetch(url, {
        ...options,
        headers
      });
    } catch (err: any) {
      console.error(`Fetch error connecting to ${url}:`, err);
      throw new Error(`Unable to connect to MyTube backend server (${API_BASE_URL}). Please verify backend is running on port 5003.`);
    }

    if (response.status === 401) {
      // Token expired or invalid, log out
      this.setToken(null);
      window.dispatchEvent(new Event('auth_expired'));
      throw new Error('Unauthorized');
    }

    if (!response.ok) {
      const errBody = await response.json().catch(() => ({}));
      const errorMsg = errBody.details
        ? `${errBody.message || 'Request failed'} (${errBody.details})`
        : (errBody.message || `Request failed with status ${response.status}`);
      throw new Error(errorMsg);
    }

    // Handle empty responses
    if (response.status === 204) {
      return {} as T;
    }

    const text = await response.text();
    if (!text || text.trim() === '') {
      return {} as T;
    }

    try {
      return JSON.parse(text) as T;
    } catch {
      return {} as T;
    }
  }

  // Authentication
  async sendOtp(email: string, fullName?: string, purpose = 'register'): Promise<import('../types').SendOtpResponse> {
    return this.request<import('../types').SendOtpResponse>('/api/auth/send-otp', {
      method: 'POST',
      body: JSON.stringify({ email, fullName, purpose })
    });
  }

  async verifyOtp(email: string, code: string): Promise<import('../types').VerifyOtpResponse> {
    return this.request<import('../types').VerifyOtpResponse>('/api/auth/verify-otp', {
      method: 'POST',
      body: JSON.stringify({ email, code })
    });
  }

  async register(fullName: string, email: string, password: string, otpCode: string): Promise<User> {
    const data = await this.request<User>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ fullName, email, password, otpCode })
    });

    if (data.token) {
      this.setToken(data.token);
    }
    return data;
  }

  async login(username: string, password: string): Promise<User> {
    const data = await this.request<User>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password })
    });
    
    if (data.token) {
      this.setToken(data.token);
    }
    return data;
  }

  async getMe(): Promise<User> {
    return this.request<User>('/api/auth/me');
  }

  logout() {
    this.setToken(null);
  }

  // Videos
  async getVideos(params: {
    search?: string;
    categoryId?: number;
    sortBy?: string;
    mediaType?: string;
    page?: number;
    pageSize?: number;
  } = {}): Promise<PagedResult<Video>> {
    const query = new URLSearchParams();
    if (params.search) query.append('search', params.search);
    if (params.categoryId) query.append('categoryId', params.categoryId.toString());
    if (params.sortBy) query.append('sortBy', params.sortBy);
    if (params.mediaType) query.append('mediaType', params.mediaType);
    if (params.page) query.append('page', params.page.toString());
    if (params.pageSize) query.append('pageSize', params.pageSize.toString());

    return this.request<PagedResult<Video>>(`/api/videos?${query.toString()}`);
  }

  async getVideo(id: string): Promise<Video> {
    return this.request<Video>(`/api/videos/${id}`);
  }

  getVideoDownloadUrl(id: string): string {
    return `${API_BASE_URL}/api/videos/${id}/download`;
  }

  downloadVideoFile(video: Video): void {
    const url = video.downloadUrl || this.getVideoDownloadUrl(video.id);
    const a = document.createElement('a');
    a.href = url;
    a.download = video.fileName || `${video.displayTitle}.mp4`;
    a.target = '_blank';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }

  async getContinueWatching(limit = 6): Promise<Video[]> {
    return this.request<Video[]>(`/api/videos/continue-watching?limit=${limit}`);
  }

  async toggleFavorite(id: string): Promise<{ isFavorite: boolean }> {
    return this.request<{ isFavorite: boolean }>(`/api/videos/${id}/favorite`, {
      method: 'POST'
    });
  }

  async getFavorites(): Promise<Video[]> {
    return this.request<Video[]>('/api/videos/favorites'); // Wait, the endpoint was GET /api/favorites? No, let's look at controller: [HttpGet("favorites")] on VideosController?
    // Let's check: in VideosController, did we add a route for favorites?
    // Ah! In VideosController we have:
    // [HttpGet("continue-watching")]
    // [HttpPost("{id}/favorite")]
    // But we missed the GET favorites endpoint! Or did we have [HttpGet("favorites")]?
    // Let's look at the plan: GET /api/favorites.
    // Wait, let's check if VideosController has GET Favorites.
    // In VideosController, we see:
    // [HttpPost("{id}/favorite")]
    // [HttpPut("{id}/progress")]
    // Wait, did we miss the GET Favorites in VideosController?
    // Let's check: yes! We didn't write an endpoint for GET favorites.
    // Ah! Let's check `VideosController.cs` line 92-118.
    // No, we didn't add the `[HttpGet("favorites")]` endpoint!
    // Oh, wait, the method `GetFavoritesAsync` is in `IVideoService`, but we forgot to map it to a controller endpoint in `VideosController`!
    // Let's check if that's true. Yes! Let's add the route `[HttpGet("favorites")]` to `VideosController.cs`!
  }

  // Categories
  async getCategories(): Promise<Category[]> {
    return this.request<Category[]>('/api/categories');
  }

  async createCategory(name: string): Promise<Category> {
    return this.request<Category>('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name })
    });
  }

  async deleteCategory(id: number): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/api/categories/${id}`, {
      method: 'DELETE'
    });
  }

  async updateVideoCategory(videoId: string, categoryId: number): Promise<void> {
    return this.request<void>(`/api/videos/${videoId}/category`, {
      method: 'PUT',
      body: JSON.stringify({ categoryId })
    });
  }

  // Watch Progress
  async updateProgress(id: string, positionSeconds: number, durationSeconds: number): Promise<void> {
    return this.request<void>(`/api/videos/${id}/progress`, {
      method: 'PUT',
      body: JSON.stringify({ positionSeconds, durationSeconds })
    });
  }

  async getProgress(id: string): Promise<WatchProgress> {
    return this.request<WatchProgress>(`/api/videos/${id}/progress`);
  }

  // Admin Scanning
  async triggerScan(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/admin/scan', {
      method: 'POST'
    });
  }

  async getScanStatus(): Promise<{ isScanning: boolean; statusMessage: string }> {
    return this.request<{ isScanning: boolean; statusMessage: string }>('/api/admin/scan/status');
  }

  // Admin Downloader
  async startDownload(
    url: string, 
    categoryName?: string, 
    quality?: string, 
    mediaType?: 'Video' | 'Audio',
    format?: 'Mp4' | 'Mp3' | 'M4a' | 'Opus' | 'Wav',
    audioBitrate?: string,
    includeSubtitles?: boolean,
    subtitleLanguage?: string,
    audioLanguage?: string
  ): Promise<DownloadJob> {
    return this.request<DownloadJob>('/api/download', {
      method: 'POST',
      body: JSON.stringify({ 
        url, 
        categoryName, 
        quality, 
        mediaType,
        format,
        audioBitrate,
        includeSubtitles,
        subtitleLanguage,
        audioLanguage
      })
    });
  }

  async getDownloadJobs(): Promise<DownloadJob[]> {
    return this.request<DownloadJob[]>('/api/download');
  }

  async cancelDownloadJob(id: string): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/api/download/${id}`, {
      method: 'DELETE'
    });
  }

  async updateYtDlp(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/download/update-binary', {
      method: 'POST'
    });
  }

  async getAvailableQualities(url: string): Promise<string[]> {
    const encodedUrl = encodeURIComponent(url);
    return this.request<string[]>(`/api/download/qualities?url=${encodedUrl}`);
  }

  async probeVideo(url: string): Promise<VideoProbeResult> {
    const encodedUrl = encodeURIComponent(url);
    return this.request<VideoProbeResult>(`/api/download/probe?url=${encodedUrl}`);
  }

  async startBatchDownload(request: BatchDownloadRequest): Promise<BatchDownloadResponse> {
    return this.request<BatchDownloadResponse>('/api/download/batch', {
      method: 'POST',
      body: JSON.stringify(request)
    });
  }

  async getDownloadBatches(): Promise<DownloadBatch[]> {
    return this.request<DownloadBatch[]>('/api/download/batches');
  }

  async getBatchDetails(id: string): Promise<DownloadBatch> {
    return this.request<DownloadBatch>(`/api/download/batches/${id}`);
  }

  async cancelBatch(id: string): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/api/download/batches/${id}/cancel`, {
      method: 'POST'
    });
  }

  async deleteBatch(id: string): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/api/download/batches/${id}`, {
      method: 'DELETE'
    });
  }

  async cancelAllDownloads(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/download/cancel-all', {
      method: 'POST'
    });
  }

  async clearFinishedDownloads(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/download/clear-finished', {
      method: 'POST'
    });
  }

  async probePlaylist(url: string): Promise<PlaylistProbeResult> {
    const encodedUrl = encodeURIComponent(url);
    return this.request<PlaylistProbeResult>(`/api/download/probe-playlist?url=${encodedUrl}`);
  }


  async getDownloaderSettings(): Promise<DownloaderSettings> {
    return this.request<DownloaderSettings>('/api/download/settings');
  }

  async updateDownloaderSettings(settings: DownloaderSettings): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/download/settings', {
      method: 'POST',
      body: JSON.stringify(settings)
    });
  }


  // Videos Management
  async deleteVideos(videoIds: string[]): Promise<{ message: string; deletedCount: number }> {
    return this.request<{ message: string; deletedCount: number }>('/api/videos/delete-batch', {
      method: 'POST',
      body: JSON.stringify({ videoIds })
    });
  }

  async updateVideosCategory(videoIds: string[], categoryId: number): Promise<{ message: string; updatedCount: number }> {
    return this.request<{ message: string; updatedCount: number }>('/api/videos/category-batch', {
      method: 'PUT',
      body: JSON.stringify({ videoIds, categoryId })
    });
  }

  async renameVideo(id: string, newTitle: string): Promise<{ message: string }> {
    return this.request<{ message: string }>(`/api/admin/videos/${id}/rename`, {
      method: 'PUT',
      body: JSON.stringify({ newTitle })
    });
  }

  // AI Services
  async analyzeVideo(id: string): Promise<{ message: string; videoId: string }> {
    return this.request<{ message: string; videoId: string }>(`/api/ai/videos/${id}/analyze`, {
      method: 'POST'
    });
  }

  async batchAnalyzeVideos(videoIds: string[]): Promise<{ message: string; count: number }> {
    return this.request<{ message: string; count: number }>('/api/ai/videos/batch-analyze', {
      method: 'POST',
      body: JSON.stringify(videoIds)
    });
  }

  async getAiQuotas(): Promise<import('../types').AiModelQuota[]> {
    return this.request<import('../types').AiModelQuota[]>('/api/ai/quotas');
  }

  async validateAiQuotas(): Promise<{ message: string; quotas: import('../types').AiModelQuota[] }> {
    return this.request<{ message: string; quotas: import('../types').AiModelQuota[] }>('/api/ai/quotas/validate', {
      method: 'POST'
    });
  }

  async resetAiQuotas(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/ai/quotas/reset', {
      method: 'POST'
    });
  }

  // Firecrawl Video Q&A
  async getVideoQaHistory(videoId: string): Promise<import('../types').VideoQa[]> {
    return this.request<import('../types').VideoQa[]>(`/api/videos/${videoId}/qa`);
  }

  async askVideoQuestion(videoId: string, question: string): Promise<import('../types').VideoQa> {
    return this.request<import('../types').VideoQa>(`/api/videos/${videoId}/qa`, {
      method: 'POST',
      body: JSON.stringify({ question })
    });
  }

  // Radarr Movie Integration
  async getRadarrStatus(): Promise<RadarrStatus> {
    return this.request<RadarrStatus>('/api/radarr/status');
  }

  async searchRadarrMovies(query: string): Promise<RadarrMovie[]> {
    return this.request<RadarrMovie[]>(`/api/radarr/search?query=${encodeURIComponent(query)}`);
  }

  async getRadarrLibrary(): Promise<RadarrMovie[]> {
    return this.request<RadarrMovie[]>('/api/radarr/library');
  }

  async addRadarrMovie(request: RadarrAddMovieRequest): Promise<RadarrMovie> {
    return this.request<RadarrMovie>('/api/radarr/add', {
      method: 'POST',
      body: JSON.stringify(request)
    });
  }

  async autoGrabRadarrMovie(request: RadarrAddMovieRequest): Promise<{
    success: boolean;
    grabbed: boolean;
    releaseTitle?: string;
    quality?: string;
    indexer?: string;
    seeders?: number;
    size?: number;
    source?: string;
    message: string;
    movie?: RadarrMovie;
  }> {
    return this.request('/api/radarr/auto-grab', {
      method: 'POST',
      body: JSON.stringify(request)
    });
  }

  async getRadarrReleases(movieId: number): Promise<RadarrRelease[]> {
    return this.request<RadarrRelease[]>(`/api/radarr/releases?movieId=${movieId}`);
  }

  async downloadRadarrRelease(guid: string, indexerId: number): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/radarr/download-release', {
      method: 'POST',
      body: JSON.stringify({ guid, indexerId })
    });
  }

  async getRadarrQueue(): Promise<RadarrQueueItem[]> {
    return this.request<RadarrQueueItem[]>('/api/radarr/queue');
  }

  async cancelRadarrQueueItem(id: number, transmissionId?: number, deleteData = true): Promise<{ success: boolean; message: string }> {
    const queueId = id && id > 0 ? id : (transmissionId || 0);
    const query = new URLSearchParams();
    if (transmissionId) query.append('transmissionId', transmissionId.toString());
    query.append('deleteData', deleteData.toString());
    return this.request<{ success: boolean; message: string }>(`/api/radarr/queue/${queueId}?${query.toString()}`, {
      method: 'DELETE'
    });
  }

  async pauseRadarrQueueItem(id: number, transmissionId?: number): Promise<{ success: boolean; message: string }> {
    const queueId = id && id > 0 ? id : (transmissionId || 0);
    const query = new URLSearchParams();
    if (transmissionId) query.append('transmissionId', transmissionId.toString());
    return this.request<{ success: boolean; message: string }>(`/api/radarr/queue/${queueId}/pause?${query.toString()}`, {
      method: 'POST'
    });
  }

  async resumeRadarrQueueItem(id: number, transmissionId?: number): Promise<{ success: boolean; message: string }> {
    const queueId = id && id > 0 ? id : (transmissionId || 0);
    const query = new URLSearchParams();
    if (transmissionId) query.append('transmissionId', transmissionId.toString());
    return this.request<{ success: boolean; message: string }>(`/api/radarr/queue/${queueId}/resume?${query.toString()}`, {
      method: 'POST'
    });
  }

  async kickstartRadarrQueueItem(id: number, transmissionId?: number): Promise<{ success: boolean; message: string }> {
    const queueId = id && id > 0 ? id : (transmissionId || 0);
    const query = new URLSearchParams();
    if (transmissionId) query.append('transmissionId', transmissionId.toString());
    return this.request<{ success: boolean; message: string }>(`/api/radarr/queue/${queueId}/kickstart?${query.toString()}`, {
      method: 'POST'
    });
  }

  async getRadarrQualityProfiles(): Promise<RadarrQualityProfile[]> {
    return this.request<RadarrQualityProfile[]>('/api/radarr/qualityprofiles');
  }

  async getRadarrRootFolders(): Promise<RadarrRootFolder[]> {
    return this.request<RadarrRootFolder[]>('/api/radarr/rootfolders');
  }

  async testRadarrConnection(baseUrl: string, apiKey: string): Promise<RadarrStatus> {
    return this.request<RadarrStatus>('/api/radarr/test', {
      method: 'POST',
      body: JSON.stringify({ baseUrl, apiKey })
    });
  }

  async getRadarrSettings(): Promise<RadarrSettings> {
    return this.request<RadarrSettings>('/api/radarr/settings');
  }

  async updateRadarrSettings(settings: Partial<RadarrSettings>): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/radarr/settings', {
      method: 'POST',
      body: JSON.stringify(settings)
    });
  }

  // Prowlarr / Indexer Direct Releases (100+ releases & active seeders)
  async searchIndexerReleases(query: string, category?: string): Promise<ProwlarrRelease[]> {
    const params = new URLSearchParams({ query });
    if (category && category !== 'all') {
      params.append('category', category);
    }
    return this.request<ProwlarrRelease[]>(`/api/radarr/indexers/search?${params.toString()}`);
  }

  async downloadIndexerRelease(guid: string, title?: string, downloadUrl?: string, magnetUrl?: string): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/radarr/indexers/download', {
      method: 'POST',
      body: JSON.stringify({ guid, title, downloadUrl, magnetUrl })
    });
  }

  // Radarr Media & File Management
  async getRadarrDownloadSummary(): Promise<RadarrDownloadSummary> {
    return this.request<RadarrDownloadSummary>('/api/radarr/downloads/summary');
  }

  async syncRadarrLinks(): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/radarr/sync-links', {
      method: 'POST'
    });
  }

  async deleteRadarrMovieFiles(id: number): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>(`/api/radarr/movies/${id}/files`, {
      method: 'DELETE'
    });
  }

  async batchDeleteRadarrFiles(movieIds: number[], filePaths?: string[]): Promise<{ success: boolean; deletedCount: number; message: string }> {
    return this.request<{ success: boolean; deletedCount: number; message: string }>('/api/radarr/files/delete-batch', {
      method: 'POST',
      body: JSON.stringify({ movieIds, filePaths })
    });
  }

  async deleteAllRadarrDownloads(): Promise<{ success: boolean; deletedCount: number; message: string }> {
    return this.request<{ success: boolean; deletedCount: number; message: string }>('/api/radarr/downloads/all', {
      method: 'DELETE'
    });
  }

  getRadarrMovieDownloadUrl(movieId: number): string {
    return `${API_BASE_URL}/api/radarr/movies/${movieId}/download`;
  }

  getRadarrPosterDownloadUrl(movieId: number): string {
    return `${API_BASE_URL}/api/radarr/movies/${movieId}/poster/download`;
  }

  getRadarrFanartDownloadUrl(movieId: number): string {
    return `${API_BASE_URL}/api/radarr/movies/${movieId}/fanart/download`;
  }

  getGenericFileDownloadUrl(filePath: string, asZip: boolean = false): string {
    if (filePath.startsWith('/api/')) {
      return `${API_BASE_URL}${filePath}`;
    }
    const params = new URLSearchParams({ path: filePath });
    if (asZip) params.append('zip', 'true');
    return `${API_BASE_URL}/api/radarr/files/download?${params.toString()}`;
  }

  getTorrentDownloadUrl(transmissionId: number, options?: { fileIndex?: number; asZip?: boolean; movieId?: number }): string {
    const params = new URLSearchParams();
    if (options?.fileIndex !== undefined) {
      params.append('fileIndex', options.fileIndex.toString());
    }
    if (options?.movieId !== undefined && options.movieId > 0) {
      params.append('movieId', options.movieId.toString());
    }
    if (options?.asZip) {
      params.append('zip', 'true');
    }
    const query = params.toString() ? `?${params.toString()}` : '';
    return `${API_BASE_URL}/api/radarr/torrents/${transmissionId}/download${query}`;
  }

  // ==================== SYSTEM HEALTH ====================

  async getSystemHealth(): Promise<SystemHealthInfo> {
    return this.request<SystemHealthInfo>('/api/admin/system-health');
  }

  // ==================== FILE NAMING & RECYCLE BIN ====================

  async getFileNamingSettings(): Promise<FileNamingConfigResponse> {
    return this.request<FileNamingConfigResponse>('/api/filenaming/settings');
  }

  async updateFileNamingSettings(settings: FileNamingSettings): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/filenaming/settings', {
      method: 'POST',
      body: JSON.stringify(settings),
    });
  }

  async previewFileRename(data: FileRenamePreviewRequest): Promise<FileRenamePreviewResult> {
    return this.request<FileRenamePreviewResult>('/api/filenaming/preview', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async executeFileRename(data: FileRenameExecuteRequest): Promise<FileRenameExecuteResult> {
    return this.request<FileRenameExecuteResult>('/api/filenaming/execute', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getRecycleBin(): Promise<RecycleBinSummary> {
    return this.request<RecycleBinSummary>('/api/filenaming/recycle-bin');
  }

  async emptyRecycleBin(): Promise<{ success: boolean; deletedCount: number; message: string }> {
    return this.request<{ success: boolean; deletedCount: number; message: string }>('/api/filenaming/recycle-bin/empty', {
      method: 'POST',
    });
  }

  // ==================== SONARR TV SERIES ====================

  async getSonarrStatus(): Promise<SonarrStatus> {
    return this.request<SonarrStatus>('/api/sonarr/status');
  }

  async getSonarrSettings(): Promise<SonarrSettings> {
    return this.request<SonarrSettings>('/api/sonarr/settings');
  }

  async updateSonarrSettings(settings: SonarrSettings): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/sonarr/settings', {
      method: 'POST',
      body: JSON.stringify(settings),
    });
  }

  async testSonarrConnection(data: { baseUrl: string; apiKey: string }): Promise<SonarrStatus> {
    return this.request<SonarrStatus>('/api/sonarr/test', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async searchSonarrSeries(query: string): Promise<SonarrSeries[]> {
    return this.request<SonarrSeries[]>(`/api/sonarr/search?query=${encodeURIComponent(query)}`);
  }

  async getSonarrLibrary(): Promise<SonarrSeries[]> {
    return this.request<SonarrSeries[]>('/api/sonarr/library');
  }

  async getSonarrSeries(id: number): Promise<SonarrSeries> {
    return this.request<SonarrSeries>(`/api/sonarr/series/${id}`);
  }

  async getSonarrEpisodes(seriesId: number): Promise<SonarrEpisode[]> {
    return this.request<SonarrEpisode[]>(`/api/sonarr/series/${seriesId}/episodes`);
  }

  async addSonarrSeries(data: SonarrAddSeriesRequest): Promise<SonarrSeries> {
    return this.request<SonarrSeries>('/api/sonarr/add', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getSonarrReleases(seriesId: number, episodeId?: number): Promise<SonarrRelease[]> {
    const url = episodeId
      ? `/api/sonarr/releases?seriesId=${seriesId}&episodeId=${episodeId}`
      : `/api/sonarr/releases?seriesId=${seriesId}`;
    return this.request<SonarrRelease[]>(url);
  }

  async downloadSonarrRelease(data: { guid: string; indexerId: number; seriesId?: number; episodeId?: number }): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/sonarr/download-release', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async getSonarrQueue(): Promise<SonarrQueueItem[]> {
    return this.request<SonarrQueueItem[]>('/api/sonarr/queue');
  }

  async getSonarrQualityProfiles(): Promise<SonarrQualityProfile[]> {
    return this.request<SonarrQualityProfile[]>('/api/sonarr/qualityprofiles');
  }

  async getSonarrRootFolders(): Promise<SonarrRootFolder[]> {
    return this.request<SonarrRootFolder[]>('/api/sonarr/rootfolders');
  }

  // Media Organizer & OCI SmartRoute Endpoints
  async getMediaOrganizerSettings(): Promise<MediaOrganizerSettings> {
    return this.request<MediaOrganizerSettings>('/api/mediaorganizer/settings');
  }

  async updateMediaOrganizerSettings(settings: MediaOrganizerSettings): Promise<{ success: boolean; message: string }> {
    return this.request<{ success: boolean; message: string }>('/api/mediaorganizer/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  }

  async previewMediaOrganization(videoId: string): Promise<MediaRenamePreview> {
    return this.request<MediaRenamePreview>(`/api/mediaorganizer/preview/${videoId}`, {
      method: 'POST',
    });
  }

  async organizeMedia(videoId: string): Promise<MediaOrganizeResult> {
    return this.request<MediaOrganizeResult>(`/api/mediaorganizer/organize/${videoId}`, {
      method: 'POST',
    });
  }

  async batchOrganizeMedia(videoIds?: string[]): Promise<MediaOrganizeResult[]> {
    return this.request<MediaOrganizeResult[]>('/api/mediaorganizer/batch', {
      method: 'POST',
      body: JSON.stringify({ videoIds }),
    });
  }

  async batchPreviewMedia(videoIds?: string[]): Promise<MediaRenamePreview[]> {
    return this.request<MediaRenamePreview[]>('/api/mediaorganizer/batch-preview', {
      method: 'POST',
      body: JSON.stringify({ videoIds }),
    });
  }

  async consolidateCategories(): Promise<CategoryConsolidationResult> {
    return this.request<CategoryConsolidationResult>('/api/mediaorganizer/consolidate', {
      method: 'POST',
    });
  }

  async getMediaOrganizerAuditReport(): Promise<MediaAuditReport> {
    return this.request<MediaAuditReport>('/api/mediaorganizer/audit');
  }

  // ==================== SYSTEM POWER CONTROL ====================
  async shutdownServer(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/admin/system/shutdown', {
      method: 'POST',
    });
  }

  async rebootServer(): Promise<{ message: string }> {
    return this.request<{ message: string }>('/api/admin/system/reboot', {
      method: 'POST',
    });
  }

  // ==================== GLOBAL FREE IPTV ====================

  async getIptvChannels(params: {
    scope?: string;
    regionCode?: string;
    countryCode?: string;
    languageCode?: string;
    categoryId?: string;
    search?: string;
    favoritesOnly?: boolean;
    historyOnly?: boolean;
    page?: number;
    pageSize?: number;
    httpsOnly?: boolean;
  }): Promise<PagedResult<IptvChannel>> {
    const searchParams = new URLSearchParams();
    if (params.scope) searchParams.set('scope', params.scope);
    if (params.regionCode) searchParams.set('regionCode', params.regionCode);
    if (params.countryCode) searchParams.set('countryCode', params.countryCode);
    if (params.languageCode) searchParams.set('languageCode', params.languageCode);
    if (params.categoryId) searchParams.set('categoryId', params.categoryId);
    if (params.search) searchParams.set('search', params.search);
    if (params.favoritesOnly) searchParams.set('favoritesOnly', 'true');
    if (params.historyOnly) searchParams.set('historyOnly', 'true');
    if (params.httpsOnly) searchParams.set('httpsOnly', 'true');
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());

    const qs = searchParams.toString();
    return this.request<PagedResult<IptvChannel>>(`/api/iptv/channels${qs ? `?${qs}` : ''}`);
  }

  async getIptvChannel(id: string): Promise<IptvChannel> {
    return this.request<IptvChannel>(`/api/iptv/channels/${encodeURIComponent(id)}`);
  }

  async getIptvFilters(): Promise<IptvFilterOptions> {
    return this.request<IptvFilterOptions>('/api/iptv/filters');
  }

  async toggleIptvFavorite(channelId: string): Promise<{ channelId: string; isFavorite: boolean }> {
    return this.request<{ channelId: string; isFavorite: boolean }>(`/api/iptv/channels/${encodeURIComponent(channelId)}/favorite`, {
      method: 'POST',
    });
  }

  async recordIptvWatch(channelId: string, streamId?: number): Promise<void> {
    await this.request<void>(`/api/iptv/channels/${encodeURIComponent(channelId)}/watch`, {
      method: 'POST',
      body: JSON.stringify({ streamId }),
    });
  }

  async reportIptvStreamHealth(streamId: number, isWorking: boolean, errorMessage?: string): Promise<void> {
    await this.request<void>(`/api/iptv/streams/${streamId}/report`, {
      method: 'POST',
      body: JSON.stringify({ isWorking, errorMessage }),
    });
  }

  async triggerIptvSync(force = false): Promise<IptvSyncStatus> {
    return this.request<IptvSyncStatus>(`/api/iptv/sync?force=${force}`, {
      method: 'POST',
    });
  }

  async getIptvSyncStatus(): Promise<IptvSyncStatus> {
    return this.request<IptvSyncStatus>('/api/iptv/sync/status');
  }
}

export const api = new ApiService();
export { API_BASE_URL };
