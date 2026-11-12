import React, { useState, useEffect } from 'react';
import { 
  FileText, Sparkles, CheckCircle2, AlertTriangle, ArrowRight, 
  Trash2, RefreshCw, Folder, Settings, ShieldAlert, Check, Copy, Tag, HardDrive
} from 'lucide-react';
import { api } from '../services/api';
import { 
  FileNamingSettings, FileNamingPreset, AvailableToken,
  FileRenameItemPreview, FileRenamePreviewResult, RecycleBinSummary,
  SpaceReplacement, ColonReplacement
} from '../types';
import { useToast } from './Toast';

export const FileNamingStudio: React.FC = () => {
  const { showToast } = useToast();

  const [pattern, setPattern] = useState('{Title} [{Quality}] [{VideoCodec}]');
  const [spaceReplacement, setSpaceReplacement] = useState<SpaceReplacement>(0);
  const [colonReplacement, setColonReplacement] = useState<ColonReplacement>(1);
  const [autoOrganize, setAutoOrganize] = useState(false);
  const [moveToRecycleBin, setMoveToRecycleBin] = useState(true);

  const [presets, setPresets] = useState<FileNamingPreset[]>([]);
  const [tokens, setTokens] = useState<AvailableToken[]>([]);

  const [loadingConfig, setLoadingConfig] = useState(true);
  const [savingSettings, setSavingSettings] = useState(false);

  // Preview state
  const [previewing, setPreviewing] = useState(false);
  const [previewResult, setPreviewResult] = useState<FileRenamePreviewResult | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [executing, setExecuting] = useState(false);

  // Recycle Bin state
  const [recycleBin, setRecycleBin] = useState<RecycleBinSummary | null>(null);
  const [emptyingBin, setEmptyingBin] = useState(false);

  // Load naming config & recycle bin
  const loadConfig = async () => {
    try {
      setLoadingConfig(true);
      const res = await api.getFileNamingSettings();
      if (res) {
        setPattern(res.settings.pattern || '{Title} [{Quality}] [{VideoCodec}]');
        setSpaceReplacement(res.settings.spaceReplacement ?? 0);
        setColonReplacement(res.settings.colonReplacement ?? 1);
        setAutoOrganize(res.settings.autoOrganizeIntoCategoryFolders ?? false);
        setMoveToRecycleBin(res.settings.moveToRecycleBinOnDelete ?? true);
        setPresets(res.presets || []);
        setTokens(res.availableTokens || []);
      }
    } catch (err: any) {
      console.error('Failed to load file naming settings:', err);
    } finally {
      setLoadingConfig(false);
    }
  };

  const loadRecycleBin = async () => {
    try {
      const res = await api.getRecycleBin();
      setRecycleBin(res);
    } catch (err) {
      console.error('Failed to load recycle bin:', err);
    }
  };

  useEffect(() => {
    loadConfig();
    loadRecycleBin();
  }, []);

  const handleSaveSettings = async () => {
    try {
      setSavingSettings(true);
      const payload: FileNamingSettings = {
        pattern,
        spaceReplacement,
        colonReplacement,
        autoOrganizeIntoCategoryFolders: autoOrganize,
        moveToRecycleBinOnDelete: moveToRecycleBin
      };
      const res = await api.updateFileNamingSettings(payload);
      showToast('success', res.message || 'File naming configuration saved.', 'Saved');
    } catch (err: any) {
      showToast('error', err.message || 'Failed to save naming settings.', 'Error');
    } finally {
      setSavingSettings(false);
    }
  };

  const handleApplyPreset = (p: FileNamingPreset) => {
    setPattern(p.pattern);
    showToast('info', `Applied preset: ${p.name}`, 'Preset Applied');
  };

  const handleInsertToken = (token: string) => {
    setPattern(prev => prev + ' ' + token);
  };

  // Generate live sample output
  const generateSamplePreview = () => {
    let title = 'Interstellar';
    let cleanTitle = 'Interstellar';
    let category = 'Sci-Fi';
    let year = '2014';
    let date = '2014-11-07';
    let quality = '1080p';
    let resolution = '1920x1080';
    let videoCodec = 'h264';
    let audioCodec = 'aac';
    let orig = 'Interstellar.2014.1080p.BluRay.x264';

    let res = pattern
      .replace(/{Title}/gi, title)
      .replace(/{CleanTitle}/gi, cleanTitle)
      .replace(/{OriginalFilename}/gi, orig)
      .replace(/{Category}/gi, category)
      .replace(/{Year}/gi, year)
      .replace(/{Date}/gi, date)
      .replace(/{Quality}/gi, quality)
      .replace(/{Resolution}/gi, resolution)
      .replace(/{VideoCodec}/gi, videoCodec)
      .replace(/{AudioCodec}/gi, audioCodec);

    if (spaceReplacement === 1) res = res.replace(/ /g, '.');
    else if (spaceReplacement === 2) res = res.replace(/ /g, '-');
    else if (spaceReplacement === 3) res = res.replace(/ /g, '_');

    if (autoOrganize && !res.includes('/')) {
      res = `${category}/${res}`;
    }

    return res + '.mp4';
  };

  const handlePreviewRename = async () => {
    try {
      setPreviewing(true);
      const res = await api.previewFileRename({
        pattern,
        spaceReplacement,
        colonReplacement,
        autoOrganizeIntoCategoryFolders: autoOrganize
      });
      setPreviewResult(res);
      // Pre-select all items that will change and have no conflict
      const changeIds = res.items.filter(i => i.willChange && !i.hasConflict).map(i => i.videoId);
      setSelectedIds(changeIds);

      if (res.videosToRename === 0) {
        showToast('info', 'All files already match this naming format.', 'Up to Date');
      } else {
        showToast('success', `Found ${res.videosToRename} files to rename.`, 'Preview Ready');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to preview file renaming.', 'Preview Error');
    } finally {
      setPreviewing(false);
    }
  };

  const handleExecuteRename = async () => {
    if (selectedIds.length === 0) {
      showToast('info', 'No files selected for renaming.', 'Notice');
      return;
    }

    const confirmMsg = `Are you sure you want to rename and organize ${selectedIds.length} files on disk? This will modify the files directly in your storage.`;
    if (!window.confirm(confirmMsg)) return;

    try {
      setExecuting(true);
      showToast('info', `Renaming ${selectedIds.length} video files on disk...`, 'Renaming Files');
      const res = await api.executeFileRename({
        pattern,
        spaceReplacement,
        colonReplacement,
        autoOrganizeIntoCategoryFolders: autoOrganize,
        videoIds: selectedIds
      });

      if (res.success) {
        showToast('success', res.messages[0] || `Successfully renamed ${res.renamedCount} files.`, 'Rename Completed');
      } else {
        showToast('warning', `Renamed ${res.renamedCount} files with ${res.errorCount} errors.`, 'Partial Completion');
      }

      // Re-run preview to refresh table
      handlePreviewRename();
      loadRecycleBin();
      window.dispatchEvent(new Event('videos_updated'));
      window.dispatchEvent(new Event('categories_updated'));
    } catch (err: any) {
      showToast('error', err.message || 'Execution failed.', 'Error');
    } finally {
      setExecuting(false);
    }
  };

  const handleEmptyRecycleBin = async () => {
    if (!window.confirm("Are you sure you want to permanently delete all files in the Recycle Bin?")) return;

    try {
      setEmptyingBin(true);
      const res = await api.emptyRecycleBin();
      showToast('success', res.message || 'Recycle bin emptied.', 'Recycle Bin');
      loadRecycleBin();
    } catch (err: any) {
      showToast('error', err.message || 'Failed to empty recycle bin.', 'Error');
    } finally {
      setEmptyingBin(false);
    }
  };

  const toggleSelectAll = () => {
    if (!previewResult) return;
    const eligible = previewResult.items.filter(i => i.willChange && !i.hasConflict).map(i => i.videoId);
    if (selectedIds.length === eligible.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(eligible);
    }
  };

  const toggleSelectOne = (id: string) => {
    setSelectedIds(prev => 
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );
  };

  return (
    <div className="space-y-6 text-neutral-200">
      
      {/* Header Banner */}
      <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl relative overflow-hidden">
        <div className="absolute top-0 right-0 w-96 h-96 bg-emerald-500/5 rounded-full blur-3xl pointer-events-none" />
        
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="p-3 rounded-2xl bg-emerald-500/10 border border-emerald-500/20 text-[#3cd69b]">
              <Sparkles size={24} />
            </div>
            <div>
              <h2 className="text-lg font-bold font-heading text-neutral-100 flex items-center gap-2">
                Sonarr Media File Naming & Organizer
                <span className="text-[10px] bg-emerald-500/20 text-[#3cd69b] px-2 py-0.5 rounded-full font-semibold border border-emerald-500/30">
                  Sonarr Engine
                </span>
              </h2>
              <p className="text-xs text-neutral-400 mt-0.5">
                Automatically standardize messy filenames, format resolution & codecs, and organize videos into clean category folders.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2.5">
            <button
              onClick={handleSaveSettings}
              disabled={savingSettings || loadingConfig}
              className="px-4 py-2 rounded-xl text-xs font-semibold bg-[#171920] hover:bg-[#20232b] text-neutral-200 border border-[#262a34] transition-all flex items-center gap-2 cursor-pointer disabled:opacity-50"
            >
              <Settings size={14} />
              {savingSettings ? 'Saving...' : 'Save Default'}
            </button>

            <button
              onClick={handlePreviewRename}
              disabled={previewing || loadingConfig}
              className="px-4 py-2 rounded-xl text-xs font-bold bg-[#3cd69b] hover:bg-[#34b785] text-neutral-950 shadow-lg shadow-emerald-500/10 transition-all flex items-center gap-2 cursor-pointer disabled:opacity-50"
            >
              <RefreshCw size={14} className={previewing ? 'animate-spin' : ''} />
              {previewing ? 'Evaluating Library...' : 'Preview Renaming'}
            </button>
          </div>
        </div>

        {/* Live Example Simulation */}
        <div className="mt-5 p-3.5 rounded-xl bg-[#0c0d10] border border-[#21242d] flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
          <div className="flex items-center gap-2 text-neutral-400">
            <FileText size={15} className="text-[#3cd69b] shrink-0" />
            <span className="font-semibold text-neutral-300">Live Sample Output:</span>
            <span className="font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20 truncate max-w-md">
              {generateSamplePreview()}
            </span>
          </div>
          <span className="text-[11px] text-neutral-500 italic">Simulated using sample metadata</span>
        </div>
      </div>

      {/* Preset & Token Builder Cards */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left Column: Pattern Editor */}
        <div className="lg:col-span-2 space-y-5 bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl">
          <div>
            <label className="text-xs font-bold text-neutral-300 uppercase tracking-wider mb-2 flex items-center gap-1.5">
              <Tag size={14} className="text-[#3cd69b]" />
              File Naming Format Pattern
            </label>
            <div className="relative">
              <input
                type="text"
                value={pattern}
                onChange={(e) => setPattern(e.target.value)}
                placeholder="{Title} [{Quality}] [{VideoCodec}]"
                className="w-full px-4 py-3 bg-[#0c0d10] border border-[#2a2e39] rounded-xl text-neutral-100 font-mono text-sm focus:outline-none focus:border-[#3cd69b] transition-all shadow-inner"
              />
            </div>
            <p className="text-[11px] text-neutral-500 mt-1.5">
              Use forward slashes (<code className="text-neutral-400">/</code>) to organize into subdirectories, e.g. <code className="text-neutral-400">&#123;Category&#125;/&#123;Title&#125;</code>
            </p>
          </div>

          {/* Quick Presets */}
          <div>
            <span className="text-[11px] font-bold uppercase tracking-wider text-neutral-400 block mb-2">
              Standard Presets (Sonarr Styles)
            </span>
            <div className="flex flex-wrap gap-2">
              {presets.map(p => (
                <button
                  key={p.id}
                  onClick={() => handleApplyPreset(p)}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-all cursor-pointer ${
                    pattern === p.pattern 
                      ? 'bg-emerald-500/10 border-emerald-500/40 text-[#3cd69b]' 
                      : 'bg-[#0c0d10] border-[#21242d] text-neutral-400 hover:text-white hover:border-[#3cd69b]/40'
                  }`}
                  title={p.description}
                >
                  {p.name}
                </button>
              ))}
            </div>
          </div>

          {/* Clickable Token Pills */}
          <div>
            <span className="text-[11px] font-bold uppercase tracking-wider text-neutral-400 block mb-2">
              Available Tokens (Click to insert into pattern)
            </span>
            <div className="flex flex-wrap gap-1.5">
              {(tokens.length > 0 ? tokens : [
                { token: '{Title}', description: 'Display Title' },
                { token: '{CleanTitle}', description: 'Clean Title' },
                { token: '{Category}', description: 'Playlist/Category' },
                { token: '{Year}', description: 'Release Year' },
                { token: '{Date}', description: 'Date YYYY-MM-DD' },
                { token: '{Quality}', description: 'Resolution (1080p, 4K)' },
                { token: '{Resolution}', description: 'Exact Dimensions' },
                { token: '{VideoCodec}', description: 'Video Codec' },
                { token: '{AudioCodec}', description: 'Audio Codec' },
                { token: '{OriginalFilename}', description: 'Raw Filename' }
              ]).map(t => (
                <button
                  key={t.token}
                  onClick={() => handleInsertToken(t.token)}
                  className="px-2.5 py-1 bg-[#171920] hover:bg-[#20232b] text-neutral-300 hover:text-[#3cd69b] border border-[#262a34] rounded-md text-xs font-mono transition-colors cursor-pointer"
                  title={t.description}
                >
                  +{t.token}
                </button>
              ))}
            </div>
          </div>

          {/* Formatter Rules (Spaces & Colons) */}
          <div className="pt-4 border-t border-[#21242d] grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
            <div>
              <span className="text-neutral-400 block mb-2 font-semibold">Space Replacement</span>
              <div className="grid grid-cols-4 gap-1.5">
                {[
                  { id: 0, label: 'Space', desc: ' ' },
                  { id: 1, label: 'Dot (.)', desc: '.' },
                  { id: 2, label: 'Dash (-)', desc: '-' },
                  { id: 3, label: 'Under (_)', desc: '_' }
                ].map(opt => (
                  <button
                    key={opt.id}
                    onClick={() => setSpaceReplacement(opt.id as SpaceReplacement)}
                    className={`py-2 px-1 rounded-lg text-center font-medium border transition-all cursor-pointer ${
                      spaceReplacement === opt.id 
                        ? 'bg-emerald-500/10 border-emerald-500/40 text-[#3cd69b]' 
                        : 'bg-[#0c0d10] border-[#21242d] text-neutral-400 hover:text-white'
                    }`}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <span className="text-neutral-400 block mb-2 font-semibold">Colon Replacement (Unsafe Char)</span>
              <div className="grid grid-cols-3 gap-1.5">
                {[
                  { id: 1, label: 'Dash ( - )' },
                  { id: 2, label: 'Spaced Dash' },
                  { id: 0, label: 'Delete' }
                ].map(opt => (
                  <button
                    key={opt.id}
                    onClick={() => setColonReplacement(opt.id as ColonReplacement)}
                    className={`py-2 px-1 rounded-lg text-center font-medium border transition-all cursor-pointer ${
                      colonReplacement === opt.id 
                        ? 'bg-emerald-500/10 border-emerald-500/40 text-[#3cd69b]' 
                        : 'bg-[#0c0d10] border-[#21242d] text-neutral-400 hover:text-white'
                    }`}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Options Checkboxes */}
          <div className="pt-4 border-t border-[#21242d] flex flex-col sm:flex-row gap-4 text-xs">
            <label className="flex items-center gap-2 cursor-pointer select-none text-neutral-300">
              <input
                type="checkbox"
                checked={autoOrganize}
                onChange={(e) => setAutoOrganize(e.target.checked)}
                className="rounded border-[#262a34] text-[#3cd69b] focus:ring-0 focus:ring-offset-0 bg-[#0c0d10]"
              />
              <span>Auto-organize into Category folders (<code className="text-[#3cd69b]">&#123;Category&#125;/...</code>)</span>
            </label>

            <label className="flex items-center gap-2 cursor-pointer select-none text-neutral-300">
              <input
                type="checkbox"
                checked={moveToRecycleBin}
                onChange={(e) => setMoveToRecycleBin(e.target.checked)}
                className="rounded border-[#262a34] text-[#3cd69b] focus:ring-0 focus:ring-offset-0 bg-[#0c0d10]"
              />
              <span>Safe Recycle Bin (Move replaced/deleted files to <code className="text-[#3cd69b]">.recycle</code>)</span>
            </label>
          </div>
        </div>

        {/* Right Column: Safe Recycle Bin & Storage Overview */}
        <div className="space-y-5">
          {/* Recycle Bin Card */}
          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl flex flex-col justify-between">
            <div>
              <div className="flex items-center justify-between pb-3 border-b border-[#21242d] mb-4">
                <div className="flex items-center gap-2 font-bold text-sm text-neutral-100">
                  <Trash2 size={16} className="text-emerald-400" />
                  <span>Media Recycle Bin</span>
                </div>
                <span className="text-[10px] bg-[#171920] border border-[#262a34] text-neutral-400 px-2 py-0.5 rounded-full font-mono">
                  .recycle
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3 mb-4">
                <div className="p-3 bg-[#0c0d10] border border-[#21242d] rounded-xl text-center">
                  <span className="text-[10px] text-neutral-500 uppercase block mb-1">Recycled Files</span>
                  <span className="text-lg font-bold text-neutral-200">{recycleBin?.fileCount ?? 0}</span>
                </div>
                <div className="p-3 bg-[#0c0d10] border border-[#21242d] rounded-xl text-center">
                  <span className="text-[10px] text-neutral-500 uppercase block mb-1">Total Size</span>
                  <span className="text-lg font-bold text-emerald-400">{recycleBin?.formattedTotalSize ?? '0 B'}</span>
                </div>
              </div>

              <p className="text-xs text-neutral-400 leading-relaxed">
                Files deleted or replaced during batch operations are moved to your safe recycle bin instead of being permanently erased immediately.
              </p>
            </div>

            <div className="mt-5 pt-4 border-t border-[#21242d]">
              <button
                onClick={handleEmptyRecycleBin}
                disabled={emptyingBin || !recycleBin || recycleBin.fileCount === 0}
                className="w-full py-2.5 px-4 rounded-xl text-xs font-semibold bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/20 transition-all flex items-center justify-center gap-2 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <Trash2 size={14} />
                {emptyingBin ? 'Emptying...' : 'Empty Recycle Bin'}
              </button>
            </div>
          </div>

          {/* Quick Help Card */}
          <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-5 shadow-xl text-xs space-y-2.5 text-neutral-400">
            <span className="font-bold text-neutral-200 block mb-1">Best Practices:</span>
            <ul className="space-y-1.5 list-disc list-inside text-neutral-400">
              <li>Always test with <strong className="text-neutral-300">Preview Renaming</strong> first.</li>
              <li>Tokens are case-insensitive e.g. <code className="text-neutral-300">&#123;title&#125;</code> works.</li>
              <li>Files on disk will be renamed and thumbnails updated automatically.</li>
            </ul>
          </div>
        </div>
      </div>

      {/* Preview Table & Execution Section */}
      {previewResult && (
        <div className="bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-xl space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-[#21242d]">
            <div>
              <h3 className="text-base font-bold text-neutral-100 flex items-center gap-2">
                Renaming Preview Results
                <span className="text-xs bg-[#171920] border border-[#262a34] text-neutral-400 px-2 py-0.5 rounded-full font-normal">
                  {previewResult.totalVideos} total videos evaluated
                </span>
              </h3>
              <p className="text-xs text-neutral-400 mt-0.5">
                {previewResult.videosToRename} files will be renamed • {previewResult.conflictsCount} conflicts detected
              </p>
            </div>

            <div className="flex items-center gap-3">
              <button
                onClick={toggleSelectAll}
                className="text-xs text-[#3cd69b] hover:underline cursor-pointer font-medium"
              >
                {selectedIds.length === previewResult.items.filter(i => i.willChange && !i.hasConflict).length 
                  ? 'Deselect All' 
                  : 'Select All Eligible'}
              </button>

              <button
                onClick={handleExecuteRename}
                disabled={executing || selectedIds.length === 0}
                className="px-5 py-2.5 rounded-xl text-xs font-bold bg-[#3cd69b] hover:bg-[#34b785] text-neutral-950 shadow-lg shadow-emerald-500/10 transition-all flex items-center gap-2 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <CheckCircle2 size={15} />
                {executing ? 'Renaming on Disk...' : `Apply Renaming (${selectedIds.length} Files)`}
              </button>
            </div>
          </div>

          {/* Table */}
          <div className="overflow-x-auto max-h-[500px] overflow-y-auto rounded-xl border border-[#21242d]">
            <table className="w-full text-left text-xs">
              <thead className="bg-[#0c0d10] text-neutral-400 sticky top-0 z-10 border-b border-[#21242d]">
                <tr>
                  <th className="p-3 w-10 text-center">
                    <input
                      type="checkbox"
                      checked={selectedIds.length > 0 && selectedIds.length === previewResult.items.filter(i => i.willChange && !i.hasConflict).length}
                      onChange={toggleSelectAll}
                      className="rounded border-[#262a34] text-[#3cd69b] bg-[#121418]"
                    />
                  </th>
                  <th className="p-3 font-semibold">Current File Path</th>
                  <th className="p-3 w-8 text-center"></th>
                  <th className="p-3 font-semibold">Proposed New File Path</th>
                  <th className="p-3 font-semibold w-24 text-center">Quality</th>
                  <th className="p-3 font-semibold w-28 text-center">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1a1d24]">
                {previewResult.items.map((item) => (
                  <tr 
                    key={item.videoId}
                    className={`hover:bg-[#171920]/60 transition-colors ${
                      item.hasConflict ? 'bg-red-500/5' : item.willChange ? 'bg-emerald-500/5' : ''
                    }`}
                  >
                    <td className="p-3 text-center">
                      <input
                        type="checkbox"
                        disabled={!item.willChange || item.hasConflict}
                        checked={selectedIds.includes(item.videoId)}
                        onChange={() => toggleSelectOne(item.videoId)}
                        className="rounded border-[#262a34] text-[#3cd69b] bg-[#121418] disabled:opacity-30"
                      />
                    </td>
                    
                    <td className="p-3 font-mono text-[11px] text-neutral-400 max-w-xs truncate" title={item.currentRelativePath}>
                      <span className="text-neutral-500 block text-[10px]">{item.categoryName}</span>
                      {item.currentRelativePath}
                    </td>

                    <td className="p-3 text-center text-neutral-500">
                      <ArrowRight size={14} />
                    </td>

                    <td className="p-3 font-mono text-[11px] text-emerald-400 font-medium max-w-xs truncate" title={item.newRelativePath}>
                      {item.newRelativePath}
                    </td>

                    <td className="p-3 text-center">
                      <span className="px-2 py-0.5 rounded-full text-[10px] bg-[#171920] border border-[#262a34] text-neutral-300 font-semibold">
                        {item.quality}
                      </span>
                    </td>

                    <td className="p-3 text-center">
                      {item.hasConflict ? (
                        <span className="inline-flex items-center gap-1 text-red-400 bg-red-500/10 px-2 py-0.5 rounded text-[10px] font-semibold border border-red-500/20" title={item.conflictReason}>
                          <AlertTriangle size={11} /> Conflict
                        </span>
                      ) : item.willChange ? (
                        <span className="inline-flex items-center gap-1 text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded text-[10px] font-semibold border border-emerald-500/20">
                          <Sparkles size={11} /> Rename
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-neutral-500 bg-neutral-500/10 px-2 py-0.5 rounded text-[10px] font-medium">
                          <Check size={11} /> No change
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

    </div>
  );
};
