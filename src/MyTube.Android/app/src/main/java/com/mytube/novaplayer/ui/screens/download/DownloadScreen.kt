package com.mytube.novaplayer.ui.screens.download

import android.content.ClipDescription
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.compose.animation.*
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material.icons.outlined.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.mytube.novaplayer.core.theme.*
import com.mytube.novaplayer.data.model.MediaItem
import com.mytube.novaplayer.data.remote.*
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DownloadScreen(
    downloadRepository: DownloadRepository,
    onPlayMedia: (MediaItem) -> Unit,
    onMediaDownloaded: () -> Unit
) {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    val clipboardManager = LocalClipboardManager.current

    // Navigation sub-tabs within Download screen
    var selectedTab by remember { mutableStateOf(0) } // 0 = New Download, 1 = Active Jobs, 2 = Server Library

    // URL input & probe state
    var urlInput by remember { mutableStateOf("") }
    var isProbing by remember { mutableStateOf(false) }
    var probeResult by remember { mutableStateOf<VideoProbeResultDto?>(null) }
    var probeError by remember { mutableStateOf<String?>(null) }

    // Download format and options state
    var mediaType by remember { mutableStateOf("Video") } // "Video" or "Audio"
    var selectedVideoFormat by remember { mutableStateOf("Mp4") } // "Mp4", "Mkv", "Webm"
    var selectedAudioFormat by remember { mutableStateOf("Mp3") } // "Mp3", "M4a", "Opus", "Wav"
    var selectedQuality by remember { mutableStateOf("Best") }
    var selectedAudioBitrate by remember { mutableStateOf("320k") }
    var includeSubtitles by remember { mutableStateOf(false) }
    var selectedSubtitleLang by remember { mutableStateOf<String?>("en") }
    var selectedCategory by remember { mutableStateOf("Default") }
    var isSubmitting by remember { mutableStateOf(false) }

    // Jobs state
    var jobs by remember { mutableStateOf<List<DownloadJobDto>>(emptyList()) }
    var isRefreshingJobs by remember { mutableStateOf(false) }
    val savingJobsProgress = remember { mutableStateMapOf<String, Float>() }

    // Server Library state
    var serverVideos by remember { mutableStateOf<List<VideoDto>>(emptyList()) }
    var isLoadingServerVideos by remember { mutableStateOf(false) }
    var serverSearchQuery by remember { mutableStateOf("") }

    // Server Settings Dialog state
    var showServerSettings by remember { mutableStateOf(false) }
    var serverUrlInput by remember { mutableStateOf(ApiClientProvider.getBaseUrl()) }
    var serverUsernameInput by remember { mutableStateOf(ApiClientProvider.getUsername()) }
    var serverPasswordInput by remember { mutableStateOf(ApiClientProvider.getPassword()) }
    var connectionTestStatus by remember { mutableStateOf<String?>(null) }
    var isTestingConnection by remember { mutableStateOf(false) }

    // Auto-poll jobs every 3 seconds and ensure authentication on launch
    LaunchedEffect(Unit) {
        ApiClientProvider.ensureAuthenticated()
        while (true) {
            try {
                val res = downloadRepository.getJobs()
                if (res.isSuccess) {
                    jobs = res.getOrDefault(emptyList())
                }
            } catch (_: Exception) {}
            delay(3000)
        }
    }

    // Refresh jobs manually
    fun refreshJobs() {
        coroutineScope.launch {
            isRefreshingJobs = true
            val res = downloadRepository.getJobs()
            if (res.isSuccess) {
                jobs = res.getOrDefault(emptyList())
            }
            isRefreshingJobs = false
        }
    }

    // Fetch server videos
    fun fetchServerVideos(query: String? = null) {
        coroutineScope.launch {
            isLoadingServerVideos = true
            val res = downloadRepository.getServerVideos(query)
            if (res.isSuccess) {
                serverVideos = res.getOrDefault(emptyList())
            }
            isLoadingServerVideos = false
        }
    }

    // Analyze URL function
    fun probeUrl() {
        if (urlInput.isBlank()) {
            Toast.makeText(context, "Please enter a valid URL", Toast.LENGTH_SHORT).show()
            return
        }
        coroutineScope.launch {
            isProbing = true
            probeError = null
            probeResult = null
            val res = downloadRepository.probeVideo(urlInput.trim())
            if (res.isSuccess) {
                val probed = res.getOrNull()
                probeResult = probed
                if (probed != null && probed.qualities.isNotEmpty()) {
                    selectedQuality = probed.qualities.first()
                }
            } else {
                probeError = res.exceptionOrNull()?.message ?: "Failed to analyze URL"
            }
            isProbing = false
        }
    }

    // Save completed job file to local phone storage
    fun saveJobToDevice(job: DownloadJobDto) {
        val ext = when {
            !job.filePath.isNullOrBlank() && File(job.filePath).extension.isNotBlank() -> File(job.filePath).extension.lowercase()
            !job.format.isNullOrBlank() -> job.format.lowercase().trimStart('.')
            job.mediaType?.equals("Audio", ignoreCase = true) == true -> "mp3"
            else -> "mp4"
        }
        val title = job.title ?: "download_${job.id}"
        coroutineScope.launch {
            savingJobsProgress[job.id] = 0.01f
            Toast.makeText(context, "Downloading to device storage...", Toast.LENGTH_SHORT).show()

            val res = downloadRepository.downloadMediaToDevice(
                videoIdOrJobId = job.id,
                title = title,
                extension = ext,
                sourceUrl = job.url,
                onProgress = { progress ->
                    savingJobsProgress[job.id] = progress
                }
            )

            if (res.isSuccess) {
                savingJobsProgress.remove(job.id)
                val file = res.getOrNull()
                Toast.makeText(context, "Saved to ${file?.name} in Downloads/MyTube!", Toast.LENGTH_LONG).show()
                onMediaDownloaded()
            } else {
                savingJobsProgress.remove(job.id)
                val err = res.exceptionOrNull()?.message ?: "Download to phone failed"
                Toast.makeText(context, "Error saving file: $err", Toast.LENGTH_LONG).show()
            }
        }
    }

    // Trigger download
    fun startDownload() {
        if (urlInput.isBlank()) {
            Toast.makeText(context, "Please enter a valid URL", Toast.LENGTH_SHORT).show()
            return
        }
        coroutineScope.launch {
            isSubmitting = true
            val formatToUse = if (mediaType == "Video") selectedVideoFormat else selectedAudioFormat
            val request = DownloadRequest(
                url = urlInput.trim(),
                categoryName = if (selectedCategory == "Default") null else selectedCategory,
                quality = if (mediaType == "Video") selectedQuality else null,
                mediaType = mediaType,
                format = formatToUse,
                audioBitrate = if (mediaType == "Audio") selectedAudioBitrate else null,
                includeSubtitles = includeSubtitles,
                subtitleLanguage = if (includeSubtitles) selectedSubtitleLang else null
            )

            val res = downloadRepository.startDownload(request)
            if (res.isSuccess) {
                val newJob = res.getOrNull()
                Toast.makeText(context, "Downloading! Media will automatically save to your phone when ready.", Toast.LENGTH_LONG).show()
                refreshJobs()
                selectedTab = 1 // Switch to active jobs tab

                // Automatically monitor and download to phone when ready
                newJob?.let { jobDto ->
                    coroutineScope.launch {
                        var finished = false
                        var attempts = 0
                        while (!finished && attempts < 120) {
                            delay(2500)
                            attempts++
                            val jobsRes = downloadRepository.getJobs()
                            val updatedJob = jobsRes.getOrNull()?.firstOrNull { it.id == jobDto.id }
                            if (updatedJob != null) {
                                if (updatedJob.status.equals("Completed", ignoreCase = true)) {
                                    finished = true
                                    saveJobToDevice(updatedJob)
                                } else if (updatedJob.status.contains("failed", ignoreCase = true) || updatedJob.status.equals("Cancelled", ignoreCase = true)) {
                                    finished = true
                                }
                            }
                        }
                    }
                }
            } else {
                val err = res.exceptionOrNull()?.message ?: "Failed to start download"
                Toast.makeText(context, "Error: $err", Toast.LENGTH_LONG).show()
            }
            isSubmitting = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Background)
            .statusBarsPadding()
    ) {
        // Top Header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Icon(
                        imageVector = Icons.Outlined.FileDownload,
                        contentDescription = null,
                        tint = PrimaryContainer,
                        modifier = Modifier.size(26.dp)
                    )
                    Text(
                        text = "MyTube Downloader",
                        style = Typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                        color = OnSurface
                    )
                }
                Text(
                    text = "Video & Audio downloader with high-speed MyTube API",
                    style = Typography.bodySmall,
                    color = OnSurfaceVariant
                )
            }

            // Server Settings Button with Status Chip
            Row(
                modifier = Modifier
                    .clip(RoundedCornerShape(20.dp))
                    .background(SurfaceContainerLow)
                    .clickable { showServerSettings = true }
                    .padding(horizontal = 10.dp, vertical = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(6.dp)
            ) {
                Box(
                    modifier = Modifier
                        .size(8.dp)
                        .clip(CircleShape)
                        .background(Color(0xFF4CAF50)) // Green indicator
                )
                val displayHost = remember(ApiClientProvider.getBaseUrl()) {
                    val raw = ApiClientProvider.getBaseUrl().removePrefix("https://").removePrefix("http://").trimEnd('/')
                    if (raw.isBlank() || raw.contains("10.0.2.2")) "Local Emulator (10.0.2.2)" else raw
                }
                Text(
                    text = displayHost,
                    style = Typography.labelSmall.copy(fontWeight = FontWeight.Medium),
                    color = OnSurface,
                    maxLines = 1
                )
                Icon(
                    imageVector = Icons.Default.Settings,
                    contentDescription = "Server Settings",
                    tint = OnSurfaceVariant,
                    modifier = Modifier.size(16.dp)
                )
            }
        }

        // Sub Navigation Tabs
        TabRow(
            selectedTabIndex = selectedTab,
            containerColor = SurfaceContainerLowest,
            contentColor = PrimaryContainer
        ) {
            Tab(
                selected = selectedTab == 0,
                onClick = { selectedTab = 0 },
                text = { Text("New Download", fontSize = 13.sp, fontWeight = if (selectedTab == 0) FontWeight.Bold else FontWeight.Normal) }
            )
            Tab(
                selected = selectedTab == 1,
                onClick = {
                    selectedTab = 1
                    refreshJobs()
                },
                text = {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text("Active Jobs", fontSize = 13.sp, fontWeight = if (selectedTab == 1) FontWeight.Bold else FontWeight.Normal)
                        if (jobs.any { it.status == "Downloading" || it.status == "Processing" || it.status == "Queued" }) {
                            Box(
                                modifier = Modifier
                                    .size(6.dp)
                                    .clip(CircleShape)
                                    .background(PrimaryContainer)
                            )
                        }
                    }
                }
            )
            Tab(
                selected = selectedTab == 2,
                onClick = {
                    selectedTab = 2
                    fetchServerVideos()
                },
                text = { Text("Server Library", fontSize = 13.sp, fontWeight = if (selectedTab == 2) FontWeight.Bold else FontWeight.Normal) }
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        // Tab Content
        Box(modifier = Modifier.fillMaxSize()) {
            when (selectedTab) {
                0 -> NewDownloadView(
                    urlInput = urlInput,
                    onUrlChange = { urlInput = it },
                    onPasteFromClipboard = {
                        val text = clipboardManager.getText()?.text ?: ""
                        if (text.isNotBlank()) {
                            urlInput = text
                            probeUrl()
                        }
                    },
                    onClearUrl = {
                        urlInput = ""
                        probeResult = null
                        probeError = null
                    },
                    isProbing = isProbing,
                    onProbe = { probeUrl() },
                    probeResult = probeResult,
                    probeError = probeError,
                    mediaType = mediaType,
                    onMediaTypeChange = { mediaType = it },
                    selectedVideoFormat = selectedVideoFormat,
                    onVideoFormatChange = { selectedVideoFormat = it },
                    selectedAudioFormat = selectedAudioFormat,
                    onAudioFormatChange = { selectedAudioFormat = it },
                    selectedQuality = selectedQuality,
                    onQualityChange = { selectedQuality = it },
                    selectedAudioBitrate = selectedAudioBitrate,
                    onAudioBitrateChange = { selectedAudioBitrate = it },
                    includeSubtitles = includeSubtitles,
                    onIncludeSubtitlesChange = { includeSubtitles = it },
                    selectedSubtitleLang = selectedSubtitleLang,
                    onSubtitleLangChange = { selectedSubtitleLang = it },
                    selectedCategory = selectedCategory,
                    onCategoryChange = { selectedCategory = it },
                    isSubmitting = isSubmitting,
                    onStartDownload = { startDownload() }
                )

                1 -> ActiveJobsView(
                    jobs = jobs,
                    isRefreshing = isRefreshingJobs,
                    onRefresh = { refreshJobs() },
                    onCancelJob = { jobId ->
                        coroutineScope.launch {
                            val res = downloadRepository.cancelJob(jobId)
                            if (res.isSuccess) {
                                Toast.makeText(context, "Cancelled job", Toast.LENGTH_SHORT).show()
                                refreshJobs()
                            }
                        }
                    },
                    savingJobsProgress = savingJobsProgress,
                    onSaveToDevice = { job -> saveJobToDevice(job) },
                    onPlayJob = { job ->
                        // If file path is available locally or streamable
                        val streamUrl = "${ApiClientProvider.getBaseUrl()}api/videos/${job.id}/stream"
                        val item = MediaItem(
                            id = job.id.hashCode().toLong(),
                            uri = streamUrl,
                            title = job.title ?: "Downloaded Media",
                            durationMs = 0L,
                            sizeBytes = 0L,
                            isVideo = job.mediaType != "Audio",
                            mimeType = if (job.mediaType == "Audio") "audio/*" else "video/*",
                            path = job.filePath ?: "",
                            artistOrSubtitle = "MyTube"
                        )
                        onPlayMedia(item)
                    }
                )

                2 -> ServerLibraryView(
                    serverVideos = serverVideos,
                    isLoading = isLoadingServerVideos,
                    searchQuery = serverSearchQuery,
                    onSearchQueryChange = {
                        serverSearchQuery = it
                        fetchServerVideos(it)
                    },
                    onRefresh = { fetchServerVideos(serverSearchQuery) },
                    onDownloadToDevice = { video ->
                        coroutineScope.launch {
                            val ext = when {
                                !video.fileName.isNullOrBlank() && File(video.fileName).extension.isNotBlank() -> File(video.fileName).extension.lowercase()
                                !video.containerFormat.isNullOrBlank() -> video.containerFormat.lowercase().trimStart('.')
                                !video.format.isNullOrBlank() -> video.format.lowercase().trimStart('.')
                                else -> "mp4"
                            }
                            Toast.makeText(context, "Downloading ${video.title}...", Toast.LENGTH_SHORT).show()
                            val res = downloadRepository.downloadMediaToDevice(
                                videoIdOrJobId = video.id,
                                title = video.title,
                                extension = ext,
                                onProgress = {}
                            )
                            if (res.isSuccess) {
                                Toast.makeText(context, "Saved to Downloads/MyTube!", Toast.LENGTH_LONG).show()
                                onMediaDownloaded()
                            } else {
                                Toast.makeText(context, "Error: ${res.exceptionOrNull()?.message}", Toast.LENGTH_LONG).show()
                            }
                        }
                    },
                    onStreamVideo = { video ->
                        val streamUrl = video.streamUrl ?: "${ApiClientProvider.getBaseUrl()}api/videos/${video.id}/stream"
                        val isAudio = video.format?.let { f ->
                            listOf("mp3", "m4a", "opus", "wav", "flac", "ogg", "aac").any { f.contains(it, ignoreCase = true) }
                        } == true || video.fileName?.let { fn ->
                            listOf(".mp3", ".m4a", ".opus", ".wav", ".flac", ".ogg", ".aac").any { fn.endsWith(it, ignoreCase = true) }
                        } == true

                        val item = MediaItem(
                            id = video.id.hashCode().toLong(),
                            uri = streamUrl,
                            title = video.title,
                            durationMs = (video.duration * 1000).toLong(),
                            sizeBytes = video.sizeBytes,
                            isVideo = !isAudio,
                            mimeType = if (isAudio) "audio/mpeg" else "video/mp4",
                            path = "",
                            artistOrSubtitle = "MyTube Server"
                        )
                        onPlayMedia(item)
                    }
                )
            }
        }
    }

    // Server Configuration Dialog
    if (showServerSettings) {
        AlertDialog(
            onDismissRequest = { showServerSettings = false },
            title = {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Icon(Icons.Default.Dns, contentDescription = null, tint = PrimaryContainer)
                    Text("MyTube Server Settings", style = Typography.titleLarge, color = OnSurface)
                }
            },
            text = {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text(
                        text = "Connect NovaPlayer to your local or remote MyTube server for downloading and media synchronization.",
                        style = Typography.bodySmall,
                        color = OnSurfaceVariant
                    )

                    OutlinedTextField(
                        value = serverUrlInput,
                        onValueChange = { serverUrlInput = it },
                        label = { Text("Server Base URL") },
                        placeholder = { Text("http://192.168.1.100:5003") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )

                    // Quick URL presets
                    Text("Quick Presets:", style = Typography.labelSmall, color = OnSurfaceVariant)
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        SuggestionChip(
                            onClick = { serverUrlInput = "http://10.0.2.2:5003" },
                            label = { Text("Emulator (5003)", fontSize = 11.sp) }
                        )
                        SuggestionChip(
                            onClick = { serverUrlInput = "http://192.168.1.100:5003" },
                            label = { Text("LAN Server", fontSize = 11.sp) }
                        )
                        SuggestionChip(
                            onClick = { serverUrlInput = "http://10.0.2.2:5000" },
                            label = { Text("Emulator", fontSize = 11.sp) }
                        )
                    }

                    OutlinedTextField(
                        value = serverUsernameInput,
                        onValueChange = { serverUsernameInput = it },
                        label = { Text("Username") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = serverPasswordInput,
                        onValueChange = { serverPasswordInput = it },
                        label = { Text("Password") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )

                    // Connection Test result
                    if (connectionTestStatus != null) {
                        Text(
                            text = connectionTestStatus!!,
                            style = Typography.bodySmall,
                            color = if (connectionTestStatus!!.contains("success", ignoreCase = true)) Color(0xFF4CAF50) else Color(0xFFFF5252)
                        )
                    }

                    // Test Connection button
                    OutlinedButton(
                        onClick = {
                            coroutineScope.launch {
                                isTestingConnection = true
                                connectionTestStatus = "Testing connection..."
                                ApiClientProvider.setServerConfig(serverUrlInput, serverUsernameInput, serverPasswordInput)
                                val res = ApiClientProvider.testConnection()
                                connectionTestStatus = if (res.isSuccess) {
                                    "✓ ${res.getOrNull()}"
                                } else {
                                    "✗ ${res.exceptionOrNull()?.message ?: "Failed"}"
                                }
                                isTestingConnection = false
                            }
                        },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = !isTestingConnection
                    ) {
                        if (isTestingConnection) {
                            CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp)
                            Spacer(modifier = Modifier.width(8.dp))
                        }
                        Text("Test Connection")
                    }
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        ApiClientProvider.setServerConfig(serverUrlInput, serverUsernameInput, serverPasswordInput)
                        coroutineScope.launch {
                            ApiClientProvider.ensureAuthenticated()
                        }
                        showServerSettings = false
                        Toast.makeText(context, "Server configuration saved!", Toast.LENGTH_SHORT).show()
                    }
                ) {
                    Text("Save & Connect")
                }
            },
            dismissButton = {
                TextButton(onClick = { showServerSettings = false }) {
                    Text("Cancel")
                }
            }
        )
    }
}

@Composable
fun NewDownloadView(
    urlInput: String,
    onUrlChange: (String) -> Unit,
    onPasteFromClipboard: () -> Unit,
    onClearUrl: () -> Unit,
    isProbing: Boolean,
    onProbe: () -> Unit,
    probeResult: VideoProbeResultDto?,
    probeError: String?,
    mediaType: String,
    onMediaTypeChange: (String) -> Unit,
    selectedVideoFormat: String,
    onVideoFormatChange: (String) -> Unit,
    selectedAudioFormat: String,
    onAudioFormatChange: (String) -> Unit,
    selectedQuality: String,
    onQualityChange: (String) -> Unit,
    selectedAudioBitrate: String,
    onAudioBitrateChange: (String) -> Unit,
    includeSubtitles: Boolean,
    onIncludeSubtitlesChange: (Boolean) -> Unit,
    selectedSubtitleLang: String?,
    onSubtitleLangChange: (String?) -> Unit,
    selectedCategory: String,
    onCategoryChange: (String) -> Unit,
    isSubmitting: Boolean,
    onStartDownload: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp)
            .padding(bottom = 80.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // URL Input Card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = SurfaceContainerLow)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                Text(
                    text = "Media Source Link",
                    style = Typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = OnSurface
                )

                OutlinedTextField(
                    value = urlInput,
                    onValueChange = onUrlChange,
                    modifier = Modifier.fillMaxWidth(),
                    placeholder = { Text("Paste YouTube, SoundCloud, or media link...") },
                    singleLine = false,
                    maxLines = 3,
                    trailingIcon = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            if (urlInput.isNotBlank()) {
                                IconButton(onClick = onClearUrl) {
                                    Icon(Icons.Default.Clear, contentDescription = "Clear", tint = OnSurfaceVariant)
                                }
                            }
                            IconButton(onClick = onPasteFromClipboard) {
                                Icon(Icons.Default.ContentPaste, contentDescription = "Paste", tint = PrimaryContainer)
                            }
                        }
                    },
                    shape = RoundedCornerShape(12.dp)
                )

                Button(
                    onClick = onProbe,
                    modifier = Modifier.fillMaxWidth(),
                    enabled = !isProbing && urlInput.isNotBlank(),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = SurfaceContainerHigh)
                ) {
                    if (isProbing) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(18.dp),
                            strokeWidth = 2.dp,
                            color = PrimaryContainer
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Analyzing Link & Qualities...", color = OnSurface)
                    } else {
                        Icon(Icons.Default.Search, contentDescription = null, tint = PrimaryContainer)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Analyze Link", color = OnSurface)
                    }
                }

                if (probeError != null) {
                    Text(
                        text = "⚠ $probeError",
                        style = Typography.bodySmall,
                        color = Color(0xFFFF5252)
                    )
                }
            }
        }

        // Probed Media Information Preview
        if (probeResult != null) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = SurfaceContainerHigh)
            ) {
                Row(
                    modifier = Modifier.padding(12.dp),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    if (!probeResult.thumbnailUrl.isNullOrBlank()) {
                        AsyncImage(
                            model = probeResult.thumbnailUrl,
                            contentDescription = "Thumbnail",
                            contentScale = ContentScale.Crop,
                            modifier = Modifier
                                .size(width = 100.dp, height = 65.dp)
                                .clip(RoundedCornerShape(8.dp))
                        )
                    }

                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = probeResult.title.ifBlank { "Probed Media" },
                            style = Typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                            color = OnSurface,
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            if (probeResult.formattedDuration.isNotBlank()) {
                                Surface(
                                    shape = RoundedCornerShape(6.dp),
                                    color = SurfaceContainerLowest
                                ) {
                                    Text(
                                        text = probeResult.formattedDuration,
                                        style = Typography.labelSmall,
                                        color = OnSurfaceVariant,
                                        modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                                    )
                                }
                            }
                            if (probeResult.qualities.isNotEmpty()) {
                                Surface(
                                    shape = RoundedCornerShape(6.dp),
                                    color = PrimaryContainer.copy(alpha = 0.15f)
                                ) {
                                    Text(
                                        text = "${probeResult.qualities.size} qualities found",
                                        style = Typography.labelSmall,
                                        color = PrimaryContainer,
                                        modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                                    )
                                }
                            }
                        }
                    }
                }
            }
        }

        // Format & Quality Configuration Card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = SurfaceContainerLow)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Text(
                    text = "Download Format & Options",
                    style = Typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = OnSurface
                )

                // Media Type Selector: Video vs Audio
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(SurfaceContainerLowest)
                        .padding(4.dp)
                ) {
                    val isVideo = mediaType == "Video"
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .clip(RoundedCornerShape(10.dp))
                            .background(if (isVideo) PrimaryContainer else Color.Transparent)
                            .clickable { onMediaTypeChange("Video") }
                            .padding(vertical = 10.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Icon(
                                Icons.Default.Videocam,
                                contentDescription = null,
                                tint = if (isVideo) Color.Black else OnSurfaceVariant,
                                modifier = Modifier.size(18.dp)
                            )
                            Text(
                                text = "Video",
                                fontWeight = FontWeight.Bold,
                                color = if (isVideo) Color.Black else OnSurfaceVariant
                            )
                        }
                    }

                    val isAudio = mediaType == "Audio"
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .clip(RoundedCornerShape(10.dp))
                            .background(if (isAudio) PrimaryContainer else Color.Transparent)
                            .clickable { onMediaTypeChange("Audio") }
                            .padding(vertical = 10.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            Icon(
                                Icons.Default.Audiotrack,
                                contentDescription = null,
                                tint = if (isAudio) Color.Black else OnSurfaceVariant,
                                modifier = Modifier.size(18.dp)
                            )
                            Text(
                                text = "Audio",
                                fontWeight = FontWeight.Bold,
                                color = if (isAudio) Color.Black else OnSurfaceVariant
                            )
                        }
                    }
                }

                // If Video selected:
                if (mediaType == "Video") {
                    // Video Format selector
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Output Video Format", style = Typography.labelMedium, color = OnSurfaceVariant)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            listOf("Mp4", "Mkv", "Webm").forEach { fmt ->
                                val selected = selectedVideoFormat.equals(fmt, ignoreCase = true)
                                FilterChip(
                                    selected = selected,
                                    onClick = { onVideoFormatChange(fmt) },
                                    label = { Text(fmt.uppercase()) },
                                    leadingIcon = if (selected) {
                                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                    } else null
                                )
                            }
                        }
                    }

                    // Video Quality selector
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Quality / Resolution", style = Typography.labelMedium, color = OnSurfaceVariant)
                        val qualitiesList = if (probeResult?.qualities?.isNotEmpty() == true) {
                            listOf("Best") + probeResult.qualities
                        } else {
                            listOf("Best", "1080p", "720p", "480p", "360p")
                        }

                        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            items(qualitiesList) { q ->
                                val selected = selectedQuality.equals(q, ignoreCase = true)
                                FilterChip(
                                    selected = selected,
                                    onClick = { onQualityChange(q) },
                                    label = { Text(q) },
                                    leadingIcon = if (selected) {
                                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                    } else null
                                )
                            }
                        }
                    }

                    // Subtitles toggle
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column {
                            Text("Include Subtitles", style = Typography.bodyMedium, color = OnSurface)
                            Text("Download and embed creator subtitles", style = Typography.bodySmall, color = OnSurfaceVariant)
                        }
                        Switch(
                            checked = includeSubtitles,
                            onCheckedChange = onIncludeSubtitlesChange
                        )
                    }

                    if (includeSubtitles && probeResult?.subtitleLanguages?.isNotEmpty() == true) {
                        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            Text("Subtitle Language", style = Typography.labelMedium, color = OnSurfaceVariant)
                            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                items(probeResult.subtitleLanguages) { lang ->
                                    val selected = selectedSubtitleLang == lang
                                    FilterChip(
                                        selected = selected,
                                        onClick = { onSubtitleLangChange(lang) },
                                        label = { Text(lang) }
                                    )
                                }
                            }
                        }
                    }
                } else {
                    // Audio Format selector
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Output Audio Format", style = Typography.labelMedium, color = OnSurfaceVariant)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            listOf("Mp3", "M4a", "Opus", "Wav").forEach { fmt ->
                                val selected = selectedAudioFormat.equals(fmt, ignoreCase = true)
                                FilterChip(
                                    selected = selected,
                                    onClick = { onAudioFormatChange(fmt) },
                                    label = { Text(fmt.uppercase()) },
                                    leadingIcon = if (selected) {
                                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                    } else null
                                )
                            }
                        }
                    }

                    // Audio Bitrate selector
                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Audio Bitrate", style = Typography.labelMedium, color = OnSurfaceVariant)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            listOf("320k", "256k", "192k", "128k").forEach { br ->
                                val selected = selectedAudioBitrate == br
                                FilterChip(
                                    selected = selected,
                                    onClick = { onAudioBitrateChange(br) },
                                    label = { Text(if (br == "320k") "320k (High)" else br) },
                                    leadingIcon = if (selected) {
                                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                                    } else null
                                )
                            }
                        }
                    }
                }

                // Category Tag
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Category / Library Tag", style = Typography.labelMedium, color = OnSurfaceVariant)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf("Default", "Music", "Videos", "Podcasts").forEach { cat ->
                            val selected = selectedCategory == cat
                            FilterChip(
                                selected = selected,
                                onClick = { onCategoryChange(cat) },
                                label = { Text(cat) }
                            )
                        }
                    }
                }
            }
        }

        // Start Download Button
        Button(
            onClick = onStartDownload,
            modifier = Modifier
                .fillMaxWidth()
                .height(54.dp),
            shape = RoundedCornerShape(16.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = PrimaryContainer,
                contentColor = Color.Black
            ),
            enabled = !isSubmitting && urlInput.isNotBlank()
        ) {
            if (isSubmitting) {
                CircularProgressIndicator(modifier = Modifier.size(24.dp), strokeWidth = 2.dp, color = Color.Black)
                Spacer(modifier = Modifier.width(12.dp))
                Text("Submitting to MyTube...", fontWeight = FontWeight.Bold, fontSize = 16.sp)
            } else {
                Icon(Icons.Default.Download, contentDescription = null, modifier = Modifier.size(22.dp))
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = if (mediaType == "Video") "Download Video to Phone (${selectedVideoFormat.uppercase()})" else "Download Audio to Phone (${selectedAudioFormat.uppercase()})",
                    fontWeight = FontWeight.Bold,
                    fontSize = 15.sp
                )
            }
        }
    }
}

@Composable
fun ActiveJobsView(
    jobs: List<DownloadJobDto>,
    isRefreshing: Boolean,
    onRefresh: () -> Unit,
    onCancelJob: (String) -> Unit,
    savingJobsProgress: Map<String, Float>,
    onSaveToDevice: (DownloadJobDto) -> Unit,
    onPlayJob: (DownloadJobDto) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp)
            .padding(bottom = 80.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "Download Tasks (${jobs.size})",
                style = Typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = OnSurface
            )

            IconButton(onClick = onRefresh) {
                if (isRefreshing) {
                    CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                } else {
                    Icon(Icons.Default.Refresh, contentDescription = "Refresh", tint = PrimaryContainer)
                }
            }
        }

        if (jobs.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(bottom = 60.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        Icons.Outlined.CloudDownload,
                        contentDescription = null,
                        tint = OnSurfaceVariant,
                        modifier = Modifier.size(56.dp)
                    )
                    Text("No active download jobs", style = Typography.bodyLarge, color = OnSurface)
                    Text("Paste a link in 'New Download' tab to get started", style = Typography.bodySmall, color = OnSurfaceVariant)
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                items(jobs, key = { it.id }) { job ->
                    JobCard(
                        job = job,
                        onCancel = { onCancelJob(job.id) },
                        savingProgress = savingJobsProgress[job.id],
                        onSaveToDevice = { onSaveToDevice(job) },
                        onPlay = { onPlayJob(job) }
                    )
                }
            }
        }
    }
}

@Composable
fun JobCard(
    job: DownloadJobDto,
    onCancel: () -> Unit,
    savingProgress: Float?,
    onSaveToDevice: () -> Unit,
    onPlay: () -> Unit
) {
    val isCompleted = job.status.equals("Completed", ignoreCase = true)
    val isFailed = job.status.contains("failed", ignoreCase = true)
    val isProcessing = job.status.equals("Processing", ignoreCase = true) || job.status.equals("Validating", ignoreCase = true)
    val isDownloading = job.status.equals("Downloading", ignoreCase = true)

    val statusColor = when {
        isCompleted -> Color(0xFF4CAF50)
        isFailed -> Color(0xFFFF5252)
        isDownloading -> PrimaryContainer
        isProcessing -> Color(0xFFAB47BC)
        else -> OnSurfaceVariant
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceContainerLow)
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            // Header: Status badge & format
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    shape = RoundedCornerShape(8.dp),
                    color = statusColor.copy(alpha = 0.15f)
                ) {
                    Text(
                        text = job.status.uppercase(),
                        style = Typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = statusColor,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp)
                    )
                }

                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    val mediaTag = if (job.mediaType == "Audio") "AUDIO" else "VIDEO"
                    val fmtTag = job.format?.uppercase() ?: "MP4"
                    Surface(
                        shape = RoundedCornerShape(6.dp),
                        color = SurfaceContainerHighest
                    ) {
                        Text(
                            text = "$mediaTag • $fmtTag",
                            style = Typography.labelSmall,
                            color = OnSurfaceVariant,
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    }
                }
            }

            // Title
            Text(
                text = job.title?.ifBlank { job.url } ?: job.url,
                style = Typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = OnSurface,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )

            // Progress bar
            val progressFrac = (job.progress / 100.0).toFloat().coerceIn(0f, 1f)
            if (!isCompleted && !isFailed) {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    LinearProgressIndicator(
                        progress = { progressFrac },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(6.dp)
                            .clip(RoundedCornerShape(3.dp)),
                        color = PrimaryContainer,
                        trackColor = SurfaceContainerHighest
                    )

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(
                            text = if (job.speed.isNullOrBlank()) "${job.progress.toInt()}%" else "${job.progress.toInt()}% • ${job.speed}",
                            style = Typography.labelSmall,
                            color = OnSurfaceVariant
                        )
                        if (!job.eta.isNullOrBlank()) {
                            Text(
                                text = "ETA: ${job.eta}",
                                style = Typography.labelSmall,
                                color = OnSurfaceVariant
                            )
                        }
                    }
                }
            }

            // Error message
            if (!job.errorMessage.isNullOrBlank()) {
                Text(
                    text = "Error: ${job.errorMessage}",
                    style = Typography.bodySmall,
                    color = Color(0xFFFF5252)
                )
            }

            // Action row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End,
                verticalAlignment = Alignment.CenterVertically
            ) {
                if (!isCompleted && !isFailed && !job.status.equals("Cancelled", ignoreCase = true)) {
                    TextButton(onClick = onCancel) {
                        Icon(Icons.Default.Close, contentDescription = null, tint = Color(0xFFFF5252), modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Cancel", color = Color(0xFFFF5252), style = Typography.labelMedium)
                    }
                }

                if (isCompleted) {
                    // Save to Phone button
                    Button(
                        onClick = onSaveToDevice,
                        shape = RoundedCornerShape(10.dp),
                        colors = ButtonDefaults.buttonColors(containerColor = PrimaryContainer, contentColor = Color.Black),
                        enabled = savingProgress == null
                    ) {
                        if (savingProgress != null) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(16.dp),
                                strokeWidth = 2.dp,
                                color = Color.Black
                            )
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("${(savingProgress * 100).toInt()}%", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        } else {
                            Icon(Icons.Default.Download, contentDescription = null, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Save to Phone", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                        }
                    }

                    Spacer(modifier = Modifier.width(8.dp))

                    OutlinedButton(
                        onClick = onPlay,
                        shape = RoundedCornerShape(10.dp)
                    ) {
                        Icon(Icons.Default.PlayArrow, contentDescription = null, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Play", fontSize = 12.sp)
                    }
                }
            }
        }
    }
}

@Composable
fun ServerLibraryView(
    serverVideos: List<VideoDto>,
    isLoading: Boolean,
    searchQuery: String,
    onSearchQueryChange: (String) -> Unit,
    onRefresh: () -> Unit,
    onDownloadToDevice: (VideoDto) -> Unit,
    onStreamVideo: (VideoDto) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp)
            .padding(bottom = 80.dp)
    ) {
        // Search & Refresh bar
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            OutlinedTextField(
                value = searchQuery,
                onValueChange = onSearchQueryChange,
                modifier = Modifier.weight(1f),
                placeholder = { Text("Search server videos...") },
                leadingIcon = { Icon(Icons.Default.Search, contentDescription = null) },
                singleLine = true,
                shape = RoundedCornerShape(12.dp)
            )

            IconButton(onClick = onRefresh) {
                if (isLoading) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                } else {
                    Icon(Icons.Default.Refresh, contentDescription = "Refresh", tint = PrimaryContainer)
                }
            }
        }

        if (serverVideos.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(bottom = 60.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(Icons.Outlined.VideoLibrary, contentDescription = null, tint = OnSurfaceVariant, modifier = Modifier.size(56.dp))
                    Text("No media on MyTube server", style = Typography.bodyLarge, color = OnSurface)
                    Text("Downloads initiated via API will show up here", style = Typography.bodySmall, color = OnSurfaceVariant)
                }
            }
        } else {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                items(serverVideos, key = { it.id }) { video ->
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(14.dp),
                        colors = CardDefaults.cardColors(containerColor = SurfaceContainerLow)
                    ) {
                        Row(
                            modifier = Modifier.padding(12.dp),
                            horizontalArrangement = Arrangement.spacedBy(12.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            if (!video.thumbnailUrl.isNullOrBlank()) {
                                AsyncImage(
                                    model = video.thumbnailUrl,
                                    contentDescription = null,
                                    contentScale = ContentScale.Crop,
                                    modifier = Modifier
                                        .size(width = 80.dp, height = 55.dp)
                                        .clip(RoundedCornerShape(8.dp))
                                )
                            }

                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = video.title,
                                    style = Typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                                    color = OnSurface,
                                    maxLines = 2,
                                    overflow = TextOverflow.Ellipsis
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                    if (!video.quality.isNullOrBlank()) {
                                        Text(video.quality, style = Typography.labelSmall, color = PrimaryContainer)
                                    }
                                    if (!video.format.isNullOrBlank()) {
                                        Text(video.format.uppercase(), style = Typography.labelSmall, color = OnSurfaceVariant)
                                    }
                                }
                            }

                            Row {
                                IconButton(onClick = { onDownloadToDevice(video) }) {
                                    Icon(Icons.Default.Download, contentDescription = "Download to Phone", tint = PrimaryContainer)
                                }
                                IconButton(onClick = { onStreamVideo(video) }) {
                                    Icon(Icons.Default.PlayArrow, contentDescription = "Stream", tint = OnSurface)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
