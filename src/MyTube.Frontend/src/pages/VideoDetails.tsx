import React, { useEffect, useState } from 'react';
import { useParams, Link, useNavigate, useLocation } from 'react-router-dom';
import { Star, Folder, Calendar, Cpu, HardDrive, Info, AlertCircle, RefreshCw, FolderPlus, ExternalLink, Edit2, Trash2, ArrowLeft, Sparkles, Tag, Globe, FileText, ChevronDown, ChevronUp, Copy, Check, Download } from 'lucide-react';
import { MessageSquare, Send, Search, HelpCircle, Loader2 } from 'lucide-react';
import { api } from '../services/api';
import { Video, Category, VideoQa } from '../types';
import { VideoPlayer } from '../components/VideoPlayer';
import { VideoCard } from '../components/VideoCard';
import { useToast } from '../components/Toast';

interface VideoDetailsProps {
  currentUser: { username: string; role: string } | null;
  onCollapseSidebar?: () => void;
}

const formatInlineMarkdown = (text: string) => {
  const parts: React.ReactNode[] = [];
  let remaining = text;
  let keyIdx = 0;

  while (remaining.length > 0) {
    const linkMatch = remaining.match(/\[([^\]]+)\]\(([^)]+)\)/);
    const boldMatch = remaining.match(/\*\*([^*]+)\*\*/);

    if (linkMatch && (!boldMatch || linkMatch.index! < boldMatch.index!)) {
      const prefix = remaining.substring(0, linkMatch.index);
      if (prefix) parts.push(prefix);

      const label = linkMatch[1];
      const url = linkMatch[2];
      parts.push(
        <a
          key={`link-${keyIdx++}`}
          href={url}
          target="_blank"
          rel="noopener noreferrer"
          className="text-[#3cd69b] hover:underline font-semibold break-all inline-flex items-center gap-1"
        >
          {label}
        </a>
      );
      remaining = remaining.substring(linkMatch.index! + linkMatch[0].length);
    } else if (boldMatch) {
      const prefix = remaining.substring(0, boldMatch.index);
      if (prefix) parts.push(prefix);

      const boldText = boldMatch[1];
      parts.push(
        <strong key={`bold-${keyIdx++}`} className="font-bold text-white">
          {boldText}
        </strong>
      );
      remaining = remaining.substring(boldMatch.index! + boldMatch[0].length);
    } else {
      parts.push(remaining);
      break;
    }
  }

  return parts;
};

const renderFormattedAnswer = (rawAnswer: string) => {
  if (!rawAnswer) return null;

  let text = rawAnswer
    .replace(/!\[.*?\]\(.*?\)/g, '')
    .replace(/\[!\[.*?\]\(.*?\)\s*\\\s*\\\s*\d+:\d+\]\(.*?\)/g, '')
    .replace(/https:\/\/www\.youtube\.com\/redirect\?[^\s\)]+/g, '')
    .replace(/\[\*\*([^\*]+)\*\*\s+\*\*\1\*\*[^\]]*\]/g, '[$1]');

  const lines = text.split('\n');

  return (
    <div className="flex flex-col gap-2 text-xs leading-relaxed text-neutral-200 break-words [word-break:break-word] overflow-wrap-anywhere max-w-full">
      {lines.map((line, idx) => {
        const trimmed = line.trim();
        if (!trimmed) return <div key={idx} className="h-1" />;

        if (trimmed.startsWith('### ')) {
          return (
            <h4 key={idx} className="text-xs font-bold text-[#3cd69b] uppercase tracking-wider font-heading mt-2 mb-1 border-b border-[#21242d] pb-1">
              {trimmed.replace(/^###\s+/, '')}
            </h4>
          );
        }
        if (trimmed.startsWith('## ')) {
          return (
            <h3 key={idx} className="text-sm font-bold text-white font-heading mt-2.5 mb-1">
              {trimmed.replace(/^##\s+/, '')}
            </h3>
          );
        }
        if (trimmed.startsWith('# ')) {
          return (
            <h2 key={idx} className="text-base font-bold text-white font-heading mt-3 mb-1">
              {trimmed.replace(/^#\s+/, '')}
            </h2>
          );
        }

        if (trimmed.startsWith('- ') || trimmed.startsWith('* ')) {
          const content = trimmed.replace(/^[-*]\s+/, '');
          return (
            <div key={idx} className="flex items-start gap-2 pl-1 my-0.5">
              <span className="text-[#3cd69b] font-bold shrink-0 mt-0.5">•</span>
              <span className="flex-1 break-words">{formatInlineMarkdown(content)}</span>
            </div>
          );
        }

        if (trimmed === '---') {
          return <hr key={idx} className="border-[#21242d] my-2" />;
        }

        return (
          <p key={idx} className="break-words">
            {formatInlineMarkdown(trimmed)}
          </p>
        );
      })}
    </div>
  );
};

export const VideoDetails: React.FC<VideoDetailsProps> = ({ currentUser, onCollapseSidebar }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { id } = useParams<{ id: string }>();
  const { showToast } = useToast();
  const [video, setVideo] = useState<Video | null>(null);
  const [recommendations, setRecommendations] = useState<Video[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Q&A state
  const [qaHistory, setQaHistory] = useState<VideoQa[]>([]);
  const [userQuestion, setUserQuestion] = useState('');
  const [askingQuestion, setAskingQuestion] = useState(false);

  // Folder modal state
  const [showFolderDialog, setShowFolderDialog] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [newFolderName, setNewFolderName] = useState('');
  const [moving, setMoving] = useState(false);

  // Rename state
  const [showRenameDialog, setShowRenameDialog] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [renaming, setRenaming] = useState(false);

  // AI state
  const [analyzingAi, setAnalyzingAi] = useState(false);
  const [showTranscript, setShowTranscript] = useState(false);
  const [copiedTranscript, setCopiedTranscript] = useState(false);

  // Auto-collapse sidebar on mount to give full video view
  useEffect(() => {
    onCollapseSidebar?.();
  }, []);

  const loadCategories = () => {
    api.getCategories()
      .then(setCategories)
      .catch(err => console.error('Error loading categories:', err));
  };

  useEffect(() => {
    if (showFolderDialog) {
      loadCategories();
    }
  }, [showFolderDialog]);

  useEffect(() => {
    if (!id) return;

    setLoading(true);
    setError(null);
    
    api.getVideo(id)
      .then(async (res) => {
        setVideo(res);
        // Parallelize recommendations and Q&A history fetching (eliminating waterfall)
        const [recRes, qasRes] = await Promise.allSettled([
          api.getVideos({ categoryId: res.categoryId, pageSize: 6 }),
          api.getVideoQaHistory(res.id)
        ]);

        if (recRes.status === 'fulfilled') {
          setRecommendations(recRes.value.items.filter(v => v.id !== res.id));
        } else {
          console.error('Error loading recommendations:', recRes.reason);
        }

        if (qasRes.status === 'fulfilled') {
          setQaHistory(qasRes.value);
        } else {
          console.error('Error loading Q&A history:', qasRes.reason);
        }
      })
      .catch((err) => {
        console.error('Error loading video details:', err);
        setError('Could not load video. The file might have been moved or deleted.');
      })
      .finally(() => setLoading(false));
  }, [id]);

  const handleAskQuestion = async (e?: React.FormEvent, customQ?: string) => {
    if (e) e.preventDefault();
    const query = (customQ || userQuestion).trim();
    if (!video || !query || askingQuestion) return;

    setAskingQuestion(true);
    showToast('info', 'Querying Firecrawl web search & formatting AI response...', 'Firecrawl AI');
    try {
      const newQa = await api.askVideoQuestion(video.id, query);
      setQaHistory(prev => [...prev, newQa]);
      setUserQuestion('');
      showToast('success', 'Answer generated & saved to database!', 'Firecrawl AI');
    } catch (err: any) {
      console.error('Error asking video question:', err);
      showToast('error', err.message || 'Failed to answer question');
    } finally {
      setAskingQuestion(false);
    }
  };

  // Auto-poll video details if AI status is Processing
  useEffect(() => {
    if (!video || video.aiStatus !== 'Processing') return;

    const interval = setInterval(async () => {
      try {
        const updated = await api.getVideo(video.id);
        setVideo(updated);
        if (updated.aiStatus === 'Completed' || updated.aiStatus === 'Failed') {
          clearInterval(interval);
          setAnalyzingAi(false);
          if (updated.aiStatus === 'Completed') {
            showToast('success', 'AI audio transcription & metadata extraction completed!', 'AI Complete');
          } else {
            showToast('error', updated.processingError || 'AI processing failed.', 'AI Failed');
          }
        }
      } catch {}
    }, 3000);

    return () => clearInterval(interval);
  }, [video?.id, video?.aiStatus]);

  const handleFavoriteToggle = async () => {
    if (!video) return;

    try {
      const res = await api.toggleFavorite(video.id);
      setVideo(prev => prev ? { ...prev, isFavorite: res.isFavorite } : null);
    } catch (err) {
      console.error('Failed to toggle favorite:', err);
    }
  };

  const handleDownloadVideo = () => {
    if (!video) return;
    showToast('info', `Downloading "${video.displayTitle}" to your local device...`, 'Download Started');
    api.downloadVideoFile(video);
  };

  const handleDeleteVideo = async () => {
    if (!video) return;
    if (!window.confirm(`Are you sure you want to permanently delete "${video.displayTitle}"? This will delete the file on disk.`)) {
      return;
    }
    try {
      showToast('info', 'Deleting video...', 'Deleting Video');
      await api.deleteVideos([video.id]);
      showToast('success', 'Video deleted successfully', 'Delete Complete');
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      setTimeout(() => {
        navigate('/');
      }, 800);
    } catch (err: any) {
      console.error('Failed to delete video:', err);
      showToast('error', err.message || 'Failed to delete video', 'Delete Failed');
    }
  };

  const handleMoveToCategory = async (catId: number, catName: string) => {
    if (!video) return;
    setMoving(true);
    try {
      await api.updateVideoCategory(video.id, catId);
      setVideo(prev => prev ? { ...prev, categoryId: catId, categoryName: catName } : null);
      showToast('success', `Video added to playlist "${catName}" successfully`);
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      setShowFolderDialog(false);
    } catch (err: any) {
      console.error('Error moving video:', err);
      showToast('error', err.message || 'Failed to add video to playlist');
    } finally {
      setMoving(false);
    }
  };

  const handleCreateAndMove = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!video || !newFolderName.trim()) return;
    setMoving(true);
    try {
      const newCat = await api.createCategory(newFolderName.trim());
      await api.updateVideoCategory(video.id, newCat.id);
      setVideo(prev => prev ? { ...prev, categoryId: newCat.id, categoryName: newCat.name } : null);
      showToast('success', `Created playlist "${newCat.name}" and added video successfully`);
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      setNewFolderName('');
      setShowFolderDialog(false);
    } catch (err: any) {
      console.error('Error creating/moving folder:', err);
      showToast('error', err.message || 'Failed to create and add to playlist');
    } finally {
      setMoving(false);
    }
  };

  const handleRename = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!video || !newTitle.trim()) return;
    setRenaming(true);
    try {
      await api.renameVideo(video.id, newTitle.trim());
      setVideo(prev => prev ? { ...prev, displayTitle: newTitle.trim() } : null);
      showToast('success', 'Video renamed successfully');
      window.dispatchEvent(new Event('videos_updated'));
      setShowRenameDialog(false);
    } catch (err: any) {
      console.error('Error renaming video:', err);
      showToast('error', err.message || 'Failed to rename video');
    } finally {
      setRenaming(false);
    }
  };

  const handleTriggerAiAnalysis = async () => {
    if (!video) return;
    setAnalyzingAi(true);
    showToast('info', 'Starting AI audio extraction & transcription pipeline...', 'AI Processing');
    try {
      await api.analyzeVideo(video.id);
      
      let attempts = 0;
      const interval = setInterval(async () => {
        attempts++;
        try {
          const updated = await api.getVideo(video.id);
          setVideo(updated);
          if (updated.aiStatus === 'Completed') {
            clearInterval(interval);
            setAnalyzingAi(false);
            showToast('success', 'AI audio transcription & metadata extraction completed!', 'AI Complete');
          } else if (updated.aiStatus === 'Failed' || attempts >= 40) {
            clearInterval(interval);
            setAnalyzingAi(false);
            if (updated.aiStatus === 'Failed') {
              showToast('error', updated.processingError || 'AI processing failed. Check API keys or quota limits.', 'AI Failed');
            }
          }
        } catch {}
      }, 3000);
    } catch (err: any) {
      console.error('Error starting AI analysis:', err);
      setAnalyzingAi(false);
      showToast('error', err.message || 'Failed to start AI analysis');
    }
  };

  const handleCopyTranscript = () => {
    if (!video?.transcript) return;
    navigator.clipboard.writeText(video.transcript);
    setCopiedTranscript(true);
    setTimeout(() => setCopiedTranscript(false), 2000);
    showToast('success', 'Transcript copied to clipboard');
  };

  const formatBytes = (bytes: number) => {
    if (bytes <= 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center bg-[#090a0c] text-neutral-400">
        <div className="flex flex-col items-center gap-2">
          <RefreshCw className="animate-spin text-[#3cd69b]" size={32} />
          <span className="text-sm font-medium">Loading player...</span>
        </div>
      </div>
    );
  }

  if (error || !video) {
    return (
      <div className="flex-1 flex items-center justify-center p-6 bg-[#090a0c] text-neutral-400">
        <div className="flex flex-col items-center gap-3 max-w-md text-center bg-[#121418] border border-[#21242d] p-6 rounded-2xl">
          <AlertCircle className="text-red-400" size={40} />
          <h2 className="text-lg font-bold text-neutral-200">Playback Error</h2>
          <p className="text-sm text-neutral-400 leading-relaxed">{error || 'Video details not found.'}</p>
          <button 
            onClick={() => navigate('/')} 
            className="mt-3 px-4 py-1.5 rounded-xl bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold text-xs transition-colors cursor-pointer"
          >
            Back to Home
          </button>
        </div>
      </div>
    );
  }

  const handleGoBack = () => {
    const fromSearch = (location.state as any)?.from;
    if (fromSearch) {
      navigate(`/${fromSearch}`);
    } else {
      navigate('/');
    }
  };

  const isProcessing = analyzingAi || video.aiStatus === 'Processing';
  const hasSummary = Boolean(video.summary && video.summary.trim().length > 0);
  const hasTags = Boolean(video.tags && video.tags.length > 0);

  return (
    <div className="flex-1 flex flex-col lg:flex-row overflow-y-auto bg-[#090a0c] text-neutral-100 p-6 gap-6 relative">
      {/* Left Column (Player & Metadata) */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Back Button */}
        <div className="mb-4 flex items-center">
          <button
            onClick={handleGoBack}
            className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-[#121418] border border-[#21242d] hover:bg-[#171920] text-xs font-semibold text-neutral-300 hover:text-white transition-all cursor-pointer shadow-md select-none"
          >
            <ArrowLeft size={14} className="text-[#3cd69b]" />
            Go Back
          </button>
        </div>

        {/* Video Player */}
        <VideoPlayer key={video.id} video={video} />

        {/* Video Title and Action Bar matching Image 1 */}
        <div className="flex flex-col xl:flex-row xl:items-start justify-between gap-4 mt-6">
          <div>
            <div className="flex items-center flex-wrap gap-3">
              <h1 className="text-2xl font-black font-heading leading-tight text-white">
                {video.displayTitle}
              </h1>

              {/* Audio MP3 Pill Badge with pencil icon matching Image 1 */}
              <div className="flex items-center gap-1.5 bg-[#3cd69b]/10 border border-[#3cd69b]/30 text-[#3cd69b] px-3 py-1 rounded-xl text-xs font-bold">
                <span>Audio MP3</span>
                {currentUser?.role === 'Administrator' && (
                  <button
                    onClick={() => {
                      setNewTitle(video.displayTitle);
                      setShowRenameDialog(true);
                    }}
                    className="hover:text-white transition-colors cursor-pointer ml-1"
                    title="Rename Video"
                  >
                    <Edit2 size={12} />
                  </button>
                )}
              </div>
            </div>

            {/* Category & Added Metadata line matching Image 1 */}
            <div className="flex items-center gap-6 mt-3 text-xs text-neutral-400 font-medium">
              <span className="flex items-center gap-2">
                <Folder size={15} className="text-[#3cd69b]" />
                Category: <Link to={`/?category=${video.categoryId}`} className="hover:text-[#3cd69b] underline">{video.categoryName}</Link>
              </span>
              <span className="flex items-center gap-2">
                <Calendar size={15} className="text-neutral-500" />
                Added: {new Date(video.createdAt).toLocaleDateString()}
              </span>
            </div>
          </div>

          {/* Action Bar matching Image 1 */}
          <div className="flex flex-wrap gap-2.5 shrink-0 select-none">
            <button
              onClick={handleDownloadVideo}
              className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold border bg-[#3cd69b] text-neutral-950 hover:bg-[#34c78f] border-[#3cd69b] transition-all cursor-pointer shadow-md shadow-[#3cd69b]/20"
              title="Download video file to laptop / desktop / mobile device"
            >
              <Download size={14} className="stroke-[2.5]" />
              <span>Download ({formatBytes(video.fileSize)})</span>
            </button>

            <button
              onClick={handleFavoriteToggle}
              className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold border cursor-pointer transition-all ${
                video.isFavorite
                  ? 'bg-yellow-500/10 border-yellow-500/30 text-yellow-400 hover:bg-yellow-500/20'
                  : 'bg-[#121418] border-[#21242d] text-neutral-300 hover:bg-[#171920]'
              }`}
            >
              <Star size={14} className={video.isFavorite ? 'fill-yellow-400 text-yellow-400' : ''} />
              {video.isFavorite ? 'Starred' : 'Add to Favorites'}
            </button>

            <button
              onClick={handleTriggerAiAnalysis}
              disabled={isProcessing}
              className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold border bg-[#3cd69b]/10 border-[#3cd69b]/30 text-[#3cd69b] hover:bg-[#3cd69b]/20 transition-all cursor-pointer disabled:opacity-60"
            >
              <Sparkles size={14} className={`text-[#3cd69b] ${isProcessing ? 'animate-spin' : ''}`} />
              {isProcessing ? 'AI Processing...' : video.aiStatus === 'Completed' ? 'Re-Analyze with AI' : 'Analyze with AI'}
            </button>

            <button
              onClick={() => setShowFolderDialog(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold border bg-[#121418] border-[#21242d] text-neutral-300 hover:bg-[#171920] hover:text-white transition-all cursor-pointer"
            >
              <FolderPlus size={14} className="text-[#3cd69b]" />
              Move to Folder
            </button>

            {currentUser?.role === 'Administrator' && (
              <>
                <button
                  onClick={() => {
                    setNewTitle(video.displayTitle);
                    setShowRenameDialog(true);
                  }}
                  className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold border bg-[#121418] border-[#21242d] text-neutral-300 hover:bg-[#171920] hover:text-white transition-all cursor-pointer"
                >
                  <Edit2 size={14} className="text-[#3cd69b]" />
                  Rename
                </button>

                <button
                  onClick={handleDeleteVideo}
                  className="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold border bg-[#121418] border-[#21242d] text-neutral-300 hover:bg-red-950/30 hover:border-red-500/30 hover:text-red-400 transition-all cursor-pointer"
                >
                  <Trash2 size={14} className="text-red-400" />
                  Delete
                </button>
              </>
            )}
          </div>
        </div>

        {/* AI Failure Error Banner */}
        {video.aiStatus === 'Failed' && (
          <div className="mt-6 p-4 rounded-2xl bg-red-950/30 border border-red-500/30 text-xs text-red-300 flex items-start gap-3 shadow-lg">
            <AlertCircle className="text-red-400 shrink-0 mt-0.5" size={18} />
            <div className="flex-1">
              <span className="font-bold text-red-200 block mb-1">AI Processing Failed</span>
              <span className="leading-relaxed">{video.processingError || 'All AI providers returned null. Verify GROQ_API_KEY, GEMINI_API_KEY, or DEEPSEEK_API_KEY environment variables.'}</span>
            </div>
          </div>
        )}

        {/* AI INTELLIGENCE & SUMMARY Card matching Image 1 */}
        <div className="mt-6 p-6 rounded-2xl bg-[#121418] border border-[#3cd69b]/25 shadow-2xl relative overflow-hidden">
          {/* Top Bar: Sparkles Title left, Language badge right */}
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <Sparkles size={18} className="text-[#3cd69b]" />
              <h3 className="text-xs font-black uppercase tracking-wider text-[#3cd69b] font-heading">AI INTELLIGENCE & SUMMARY</h3>
            </div>

            <span className="flex items-center gap-1.5 text-xs font-semibold bg-[#171920] text-neutral-200 px-3 py-1 rounded-full border border-[#21242d]">
              <Globe size={13} className="text-blue-400" />
              Language: {isProcessing ? 'Analyzing...' : video.detectedLanguage || 'English'}
            </span>
          </div>

          {/* Generated Title */}
          <div className="mb-3">
            <span className="text-[10px] font-bold text-[#3cd69b] uppercase tracking-wider block mb-1">GENERATED TITLE</span>
            <h4 className="text-base font-bold text-white leading-snug">{video.displayTitle}</h4>
          </div>

          {/* Summary Text */}
          {isProcessing ? (
            <div className="mb-5 p-4 rounded-xl bg-[#090a0c] border border-[#3cd69b]/20 flex items-center gap-3 text-xs text-[#3cd69b] animate-pulse">
              <Sparkles size={16} className="animate-spin text-[#3cd69b] shrink-0" />
              <span>AI analysis in progress... Extracting speech transcript with Whisper and generating summary & tags.</span>
            </div>
          ) : hasSummary ? (
            <p className="text-xs leading-relaxed text-neutral-300 font-normal mb-5 select-text">
              {video.summary}
            </p>
          ) : video.aiStatus === 'Failed' ? (
            <p className="text-xs leading-relaxed text-red-400/90 font-normal mb-5 italic">
              AI processing was unsuccessful. Click "Re-Analyze with AI" above to try again.
            </p>
          ) : (
            <p className="text-xs leading-relaxed text-neutral-500 font-normal mb-5 italic">
              No AI summary generated yet. Click "Analyze with AI" above to transcribe audio and generate summary & tags.
            </p>
          )}

          {/* Divider & Hashtags */}
          <div className="pt-4 border-t border-[#21242d] flex flex-wrap gap-2 items-center">
            <Tag size={14} className="text-neutral-500 mr-1" />
            {isProcessing ? (
              <span className="text-xs text-[#3cd69b] animate-pulse italic">Generating tags...</span>
            ) : hasTags ? (
              video.tags!.map((tag, idx) => (
                <span
                  key={`tag-${idx}`}
                  className="text-xs font-semibold bg-[#3cd69b]/10 text-[#3cd69b] border border-[#3cd69b]/20 px-3 py-1 rounded-full hover:bg-[#3cd69b]/20 transition-colors cursor-pointer"
                >
                  #{tag}
                </span>
              ))
            ) : (
              <span className="text-xs text-neutral-500 italic">No tags generated yet.</span>
            )}
          </div>
        </div>

        {/* SPEECH-TO-TEXT TRANSCRIPT (WHISPER) Card matching Image 1 */}
        <div className="mt-4 p-5 rounded-2xl bg-[#121418] border border-[#21242d] shadow-lg">
          <div className="flex items-center justify-between">
            <button
              onClick={() => setShowTranscript(prev => !prev)}
              className="flex items-center gap-2.5 text-xs font-bold uppercase tracking-wider text-neutral-200 hover:text-white transition-colors cursor-pointer select-none"
            >
              <FileText size={16} className="text-purple-400" />
              SPEECH-TO-TEXT TRANSCRIPT (WHISPER)
              {showTranscript ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
            </button>

            <button
              onClick={handleCopyTranscript}
              className="flex items-center gap-1.5 text-xs font-semibold bg-[#171920] border border-[#21242d] hover:bg-[#21242d] text-neutral-200 px-3 py-1.5 rounded-xl transition-colors cursor-pointer"
            >
              {copiedTranscript ? <Check size={14} className="text-[#3cd69b]" /> : <Copy size={14} />}
              {copiedTranscript ? 'Copied!' : 'Copy Transcript'}
            </button>
          </div>

          {showTranscript && (
            <div className="mt-4 p-4 rounded-xl bg-[#090a0c] border border-[#21242d] text-xs text-neutral-300 font-mono leading-relaxed max-h-72 overflow-y-auto whitespace-pre-wrap select-text">
              {video.transcript || 'No transcript generated yet. Click "Analyze with AI" above to generate a Whisper speech-to-text transcript.'}
            </div>
          )}
        </div>

        {/* File & Stream Technical Diagnostics */}
        <div className="mt-6 p-5 rounded-2xl bg-[#121418] border border-[#21242d]">
          <h3 className="text-xs font-bold uppercase tracking-wider text-neutral-400 mb-4 flex items-center gap-2">
            <Info size={15} className="text-[#3cd69b]" />
            File & Stream Diagnostics
          </h3>
          
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-xs font-medium text-neutral-300">
            <div className="flex flex-col gap-1">
              <span className="text-neutral-500">File Name</span>
              <span className="truncate" title={video.fileName}>{video.fileName}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-neutral-500">File Size</span>
              <span className="flex items-center gap-1">
                <HardDrive size={13} className="text-neutral-500" />
                {formatBytes(video.fileSize)}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-neutral-500">Resolution & Quality</span>
              <span className="flex items-center gap-1.5">
                <span>{video.width > 0 ? `${video.width} x ${video.height}` : 'Audio File'}</span>
                {video.quality && (
                  <span className="text-[10px] bg-[#3cd69b]/10 text-[#3cd69b] border border-[#3cd69b]/30 px-1.5 py-0.2 rounded font-bold">
                    {video.quality}
                  </span>
                )}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-neutral-500">Media Codecs</span>
              <span className="flex items-center gap-1">
                <Cpu size={13} className="text-neutral-500" />
                {video.videoCodec || 'N/A'} / {video.audioCodec || 'N/A'}
              </span>
            </div>
          </div>

          {video.sourceUrl && (
            <div className="mt-4 pt-3.5 border-t border-[#21242d] flex items-center gap-2 text-xs font-medium text-neutral-300">
              <span className="text-neutral-500">Downloaded from:</span>
              <a 
                href={video.sourceUrl} 
                target="_blank" 
                rel="noopener noreferrer" 
                className="text-[#3cd69b] hover:underline inline-flex items-center gap-1 transition-colors truncate max-w-lg"
                title={video.sourceUrl}
              >
                <ExternalLink size={13} />
                {video.sourceUrl}
              </a>
            </div>
          )}
        </div>

        {/* FIRECRAWL AI VIDEO Q&A SECTION */}
        <div className="mt-6 p-6 rounded-2xl bg-[#121418] border border-[#21242d] shadow-2xl space-y-6">
          <div className="flex items-center justify-between border-b border-[#21242d] pb-4">
            <div className="flex items-center gap-2.5">
              <MessageSquare size={18} className="text-[#3cd69b]" />
              <h3 className="text-sm font-bold font-heading uppercase tracking-wider text-white">
                FIRECRAWL AI VIDEO Q&A
              </h3>
            </div>
            <span className="text-[10px] font-bold uppercase tracking-wider bg-[#3cd69b]/15 text-[#3cd69b] border border-[#3cd69b]/30 px-3 py-1 rounded-full flex items-center gap-1.5">
              <Search size={11} />
              Firecrawl Web Search & AI Models
            </span>
          </div>

          {/* Quick Preset Questions */}
          <div className="flex flex-wrap gap-2 text-xs select-none">
            <span className="text-neutral-500 font-semibold self-center text-[11px]">Suggested:</span>
            <button
              type="button"
              disabled={askingQuestion}
              onClick={() => handleAskQuestion(undefined, `Summarize key facts and web context about ${video.displayTitle}`)}
              className="px-3 py-1 rounded-full bg-[#171920] border border-[#21242d] text-neutral-300 hover:text-white hover:border-[#3cd69b]/50 transition-all cursor-pointer disabled:opacity-50"
            >
              Summary & Web Facts
            </button>
            <button
              type="button"
              disabled={askingQuestion}
              onClick={() => handleAskQuestion(undefined, `What are the main topics and details in this video?`)}
              className="px-3 py-1 rounded-full bg-[#171920] border border-[#21242d] text-neutral-300 hover:text-white hover:border-[#3cd69b]/50 transition-all cursor-pointer disabled:opacity-50"
            >
              Main Topics
            </button>
            <button
              type="button"
              disabled={askingQuestion}
              onClick={() => handleAskQuestion(undefined, `Find recent news or web context related to this video content.`)}
              className="px-3 py-1 rounded-full bg-[#171920] border border-[#21242d] text-neutral-300 hover:text-white hover:border-[#3cd69b]/50 transition-all cursor-pointer disabled:opacity-50"
            >
              Search Recent News
            </button>
          </div>

          {/* Question Input Form */}
          <form onSubmit={handleAskQuestion} className="flex gap-2">
            <input
              type="text"
              placeholder="Ask any question related to this video or search web context..."
              value={userQuestion}
              onChange={(e) => setUserQuestion(e.target.value)}
              disabled={askingQuestion}
              autoComplete="off"
              className="flex-1 bg-[#090a0c] border border-[#21242d] rounded-xl px-4 py-2.5 text-xs text-white placeholder-neutral-500 outline-none focus:border-[#3cd69b] transition-colors"
            />
            <button
              type="submit"
              disabled={askingQuestion || !userQuestion.trim()}
              className="flex items-center gap-2 px-5 py-2.5 bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold rounded-xl text-xs transition-colors cursor-pointer disabled:opacity-40 shrink-0"
            >
              {askingQuestion ? <Loader2 size={14} className="animate-spin" /> : <Send size={14} />}
              {askingQuestion ? 'Analyzing...' : 'Ask AI'}
            </button>
          </form>

          {/* Q&A Chat History List */}
          {qaHistory.length === 0 ? (
            <div className="py-8 text-center text-xs text-neutral-500 italic bg-[#090a0c]/50 rounded-xl border border-[#21242d]">
              No questions asked yet. Ask a question above to trigger Firecrawl web search & AI formatting!
            </div>
          ) : (
            <div className="flex flex-col gap-4 max-h-[500px] overflow-y-auto pr-1">
              {qaHistory.map((qa) => (
                <div key={`qa-${qa.id}`} className="flex flex-col gap-3 p-4 rounded-xl bg-[#090a0c] border border-[#21242d]">
                  {/* User Question */}
                  <div className="flex items-start gap-2.5">
                    <div className="h-6 w-6 rounded-full bg-[#3cd69b]/20 text-[#3cd69b] font-bold text-[10px] flex items-center justify-center shrink-0 mt-0.5">
                      Q
                    </div>
                    <div className="flex-1">
                      <span className="text-xs font-bold text-white block">{qa.question}</span>
                      <span className="text-[10px] text-neutral-500 block mt-0.5">{new Date(qa.createdAt).toLocaleTimeString()}</span>
                    </div>
                  </div>

                  {/* AI Answer */}
                  <div className="p-4 rounded-xl bg-[#121418] border border-[#21242d] text-xs text-neutral-300 leading-relaxed font-sans select-text break-words overflow-x-hidden max-w-full">
                    {renderFormattedAnswer(qa.answer)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Right Column (Recommendations Sidebar) */}
      <div className="w-full lg:w-[350px] shrink-0 flex flex-col gap-4">
        <h2 className="text-sm font-bold font-heading text-neutral-300 border-b border-[#21242d] pb-2 flex items-center gap-2">
          Recommended Videos
        </h2>

        {recommendations.length === 0 ? (
          <p className="text-xs text-neutral-500 mt-2">No other videos found in this category.</p>
        ) : (
          <div className="flex flex-col gap-4">
            {recommendations.map(recVideo => (
              <VideoCard 
                key={`rec-${recVideo.id}`} 
                video={recVideo}
                onFavoriteToggled={(vId, isFav) => {
                  setRecommendations(prev => prev.map(v => v.id === vId ? { ...v, isFavorite: isFav } : v));
                  if (video.id === vId) {
                    setVideo(prev => prev ? { ...prev, isFavorite: isFav } : null);
                  }
                }}
              />
            ))}
          </div>
        )}
      </div>

      {/* Move to Folder Dialog */}
      {showFolderDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-2xl bg-[#121418] border border-[#21242d] p-6 shadow-2xl relative overflow-hidden flex flex-col max-h-[85vh]">
            <div className="flex items-center justify-between border-b border-[#21242d] pb-3 mb-4 select-none">
              <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-200">
                Organize Video
              </h3>
              <button 
                onClick={() => setShowFolderDialog(false)} 
                className="text-neutral-500 hover:text-neutral-300 cursor-pointer text-sm font-medium"
              >
                ✕
              </button>
            </div>

            <div className="flex-1 overflow-y-auto mb-4 pr-1 flex flex-col gap-1.5 select-none">
              <span className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1">
                Move to existing folder:
              </span>
              
              {categories.map(cat => (
                <button
                  key={cat.id}
                  disabled={moving || cat.id === video.categoryId}
                  onClick={() => handleMoveToCategory(cat.id, cat.name)}
                  className={`flex items-center justify-between px-3.5 py-2.5 rounded-xl text-xs font-semibold text-left border transition-all cursor-pointer ${
                    cat.id === video.categoryId
                      ? 'bg-[#3cd69b]/10 border-[#3cd69b]/30 text-[#3cd69b] cursor-default'
                      : 'bg-[#090a0c] border-[#21242d] hover:bg-[#171920] hover:text-white text-neutral-300'
                  }`}
                >
                  <span className="truncate">{cat.name}</span>
                  <span className="text-[10px] bg-[#121418] border border-[#21242d] px-2 py-0.5 rounded-full text-neutral-500">
                    {cat.videoCount} videos
                  </span>
                </button>
              ))}
            </div>

            <form onSubmit={handleCreateAndMove} className="border-t border-[#21242d] pt-4 flex flex-col gap-2">
              <span className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-0.5 select-none">
                Or create new folder:
              </span>
              
              <div className="flex gap-2">
                <input
                  type="text"
                  placeholder="e.g. Education, Favorites..."
                  value={newFolderName}
                  onChange={(e) => setNewFolderName(e.target.value)}
                  disabled={moving}
                  autoComplete="off"
                  className="flex-1 bg-[#090a0c] border border-[#21242d] rounded-xl px-3.5 py-2 text-xs text-neutral-100 placeholder-neutral-600 outline-none focus:border-[#3cd69b] transition-colors"
                />
                
                <button
                  type="submit"
                  disabled={moving || !newFolderName.trim()}
                  className="px-4 py-2 bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold rounded-xl text-xs transition-colors cursor-pointer select-none"
                >
                  Create
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Rename Video Dialog */}
      {showRenameDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4">
          <div className="w-full max-w-md rounded-2xl bg-[#121418] border border-[#21242d] p-6 shadow-2xl relative overflow-hidden flex flex-col">
            <div className="flex items-center justify-between border-b border-[#21242d] pb-3 mb-4 select-none">
              <h3 className="text-sm font-bold uppercase tracking-wider text-neutral-200">
                Rename Video
              </h3>
              <button 
                onClick={() => setShowRenameDialog(false)} 
                className="text-neutral-500 hover:text-neutral-300 cursor-pointer text-sm font-medium"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleRename} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label htmlFor="rename-input" className="text-xs text-neutral-400 font-semibold select-none">
                  New Title
                </label>
                <input
                  id="rename-input"
                  type="text"
                  required
                  placeholder="e.g. My Awesome Video..."
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  disabled={renaming}
                  autoComplete="off"
                  className="w-full px-4 py-2.5 rounded-xl bg-[#090a0c] border border-[#21242d] text-sm focus:outline-none focus:border-[#3cd69b] transition-colors placeholder:text-neutral-700 text-white"
                />
              </div>

              <div className="flex justify-end gap-2 mt-2 select-none">
                <button
                  type="button"
                  onClick={() => setShowRenameDialog(false)}
                  disabled={renaming}
                  className="px-4 py-2 border border-[#21242d] hover:bg-[#171920] text-neutral-300 rounded-xl text-xs font-bold transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={renaming || !newTitle.trim() || newTitle.trim() === video.displayTitle}
                  className="px-4 py-2 bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold rounded-xl text-xs transition-colors cursor-pointer"
                >
                  {renaming ? 'Saving...' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
