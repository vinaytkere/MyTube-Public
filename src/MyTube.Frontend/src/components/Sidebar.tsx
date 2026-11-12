import React, { useEffect, useState } from 'react';
import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import { 
  Home, Clock, Heart, Shield, Folder, Download, Trash2, Music, Film, 
  Settings as SettingsIcon, Tv, FolderCheck, Radio, Terminal, Cpu
} from 'lucide-react';
import { Progress, Badge, Tooltip } from '@mantine/core';
import { Category, User, SystemHealthInfo } from '../types';
import { api } from '../services/api';
import { useToast } from './Toast';

interface SidebarProps {
  isOpen: boolean;
  currentUser: User | null;
  onNavigate?: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ isOpen, currentUser, onNavigate }) => {
  const [categories, setCategories] = useState<Category[]>([]);
  const [storageHealth, setStorageHealth] = useState<{ usedPercent: number; formattedUsed: string; formattedTotal: string }>({
    usedPercent: 52,
    formattedUsed: '8.4 TB',
    formattedTotal: '16 TB'
  });
  const navigate = useNavigate();
  const location = useLocation();
  const { showToast } = useToast();

  const handleDeleteCategory = async (id: number, name: string) => {
    if (!window.confirm(`Are you sure you want to delete the playlist "${name}"? Videos will remain in your library under Downloads.`)) {
      return;
    }

    try {
      showToast('info', `Deleting playlist "${name}"...`, 'Deleting Playlist');
      await api.deleteCategory(id);
      showToast('success', `Playlist "${name}" deleted successfully`, 'Playlist Deleted');
      window.dispatchEvent(new Event('categories_updated'));
      window.dispatchEvent(new Event('videos_updated'));
      
      const searchParams = new URLSearchParams(location.search);
      const activeCategoryId = searchParams.get('category');
      if (activeCategoryId && parseInt(activeCategoryId, 10) === id) {
        navigate('/');
      }
    } catch (err: any) {
      showToast('error', err.message || 'Failed to delete playlist', 'Delete Failed');
    }
  };

  const loadData = () => {
    if (currentUser) {
      api.getCategories()
        .then(setCategories)
        .catch(err => console.log('Error loading categories:', err));

      api.getSystemHealth()
        .then((data: SystemHealthInfo) => {
          if (data?.storage) {
            setStorageHealth({
              usedPercent: data.storage.usedPercentage ?? 52,
              formattedUsed: data.storage.formattedUsed ?? '8.4 TB',
              formattedTotal: data.storage.formattedTotal ?? '16 TB'
            });
          }
        })
        .catch(() => {
          // Keep default storage telemetry if offline
        });
    }
  };

  useEffect(() => {
    loadData();
    window.addEventListener('categories_updated', loadData);
    window.addEventListener('videos_updated', loadData);
    return () => {
      window.removeEventListener('categories_updated', loadData);
      window.removeEventListener('videos_updated', loadData);
    };
  }, [currentUser]);

  if (!isOpen) return null;

  const handleLinkClick = () => {
    if (onNavigate) onNavigate();
  };

  const navLinkClasses = (isActive: boolean) =>
    `flex items-center gap-3 px-3 py-2 rounded-lg text-xs font-medium transition-all ${
      isActive 
        ? 'bg-[#3cd69b] text-neutral-950 font-bold shadow-[0_0_14px_rgba(60,214,155,0.25)]' 
        : 'text-neutral-400 hover:bg-[#171920] hover:text-white'
    }`;

  return (
    <aside className="w-60 bg-[#0c0e12] border-r border-[#1a1d24] flex-shrink-0 flex flex-col justify-between h-[calc(100vh-3.5rem)] fixed md:sticky top-14 left-0 z-40 overflow-y-auto text-neutral-300 shadow-[0_1px_8px_rgba(0,0,0,0.5)]">
      
      {/* Top Section */}
      <div className="flex flex-col gap-1 p-3">
        {/* Emblem & Node Status matching Stitch */}
        <div className="flex flex-col gap-2 p-2 bg-[#121418] rounded-xl border border-[#21242d] mb-2">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-[#3cd69b]/15 border border-[#3cd69b]/30 flex items-center justify-center text-[#3cd69b] font-bold font-heading text-sm shadow-[0_0_12px_rgba(60,214,155,0.2)]">
              MT
            </div>
            <div className="flex flex-col min-w-0">
              <span className="font-heading font-extrabold text-sm tracking-tight text-white truncate">MyTube OS</span>
              <span className="font-mono-telemetry text-[10px] text-neutral-400 uppercase tracking-wider truncate">Homelab Node v4.19</span>
            </div>
          </div>
          
          <div className="flex items-center justify-between px-2 py-1 bg-[#1a1c20] rounded-md text-[10px] font-mono-telemetry">
            <div className="flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b] animate-pulse"></span>
              <span className="text-[#3cd69b] font-semibold">ONLINE • 14ms</span>
            </div>
            <Badge size="xs" variant="light" color="cyan" radius="sm">SignalR</Badge>
          </div>
        </div>

        {/* Primary Navigation List */}
        <nav className="flex flex-col gap-0.5">
          <NavLink to="/" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Home size={17} />
            <span>Home</span>
          </NavLink>

          <NavLink to="/movies" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Film size={17} />
            <span>Releases & Torrents</span>
          </NavLink>

          <NavLink to="/series" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Tv size={17} />
            <span>TV Shows (Sonarr)</span>
          </NavLink>

          <NavLink to="/iptv" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Radio size={17} />
            <span>Live IPTV</span>
          </NavLink>

          <NavLink to="/organizer" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <FolderCheck size={17} />
            <span>Movie Organizer</span>
          </NavLink>

          <NavLink to="/audio" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Music size={17} />
            <span>Audio & Podcasts</span>
          </NavLink>

          <NavLink to="/favorites" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Heart size={17} />
            <span>Favorites</span>
          </NavLink>

          <NavLink to="/history" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
            <Clock size={17} />
            <span>History</span>
          </NavLink>

          {currentUser?.role === 'Administrator' && (
            <>
              <div className="pt-2 pb-1 px-1">
                <span className="font-mono-telemetry text-[9px] font-bold tracking-widest text-neutral-500 uppercase">Admin Utilities</span>
              </div>

              <NavLink to="/downloader" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
                <Download size={17} />
                <span>Downloader (yt-dlp)</span>
              </NavLink>

              <NavLink to="/admin" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
                <Terminal size={17} />
                <span>Diagnostics & Power</span>
              </NavLink>

              <NavLink to="/settings?tab=ai" onClick={handleLinkClick} className={({ isActive }) => navLinkClasses(isActive)}>
                <Cpu size={17} />
                <span>AI Quota & Models</span>
              </NavLink>
            </>
          )}
        </nav>

        {/* Playlists Shelf */}
        <div className="mt-3 pt-3 border-t border-[#1a1d24]">
          <div className="flex items-center justify-between px-2 mb-1.5">
            <span className="font-mono-telemetry text-[10px] font-bold uppercase tracking-wider text-neutral-500">
              PLAYLISTS
            </span>
            <span className="text-[10px] text-neutral-600 font-mono-telemetry">{categories.length}</span>
          </div>

          <div className="flex flex-col gap-0.5 max-h-40 overflow-y-auto pr-1">
            {categories.length === 0 ? (
              <span className="px-2 py-1 text-xs text-neutral-600 italic">No playlists yet</span>
            ) : (
              categories.map((category) => (
                <NavLink
                  key={category.id}
                  to={`/?category=${category.id}`}
                  onClick={handleLinkClick}
                  className={({ isActive }) =>
                    `group/item flex items-center justify-between px-2.5 py-1.5 rounded-lg text-xs font-medium transition-colors ${
                      isActive ? 'bg-[#171920] text-[#3cd69b] font-semibold' : 'text-neutral-400 hover:bg-[#121418] hover:text-white'
                    }`
                  }
                >
                  <div className="flex items-center gap-2 truncate min-w-0 flex-1">
                    <Folder size={14} className="text-neutral-500 shrink-0" aria-hidden="true" />
                    <span className="truncate">{category.name}</span>
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    {currentUser?.role === 'Administrator' && (
                      category.name.toLowerCase() === 'radarr' || category.name.toLowerCase() === 'downloads' ? (
                        <span className="p-0.5 text-emerald-400/80" title="Protected System Folder">
                          <Shield size={11} />
                        </span>
                      ) : (
                        <button
                          onClick={(e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            handleDeleteCategory(category.id, category.name);
                          }}
                          className="opacity-0 group-hover/item:opacity-100 p-0.5 rounded text-neutral-500 hover:text-red-400 hover:bg-[#171920] transition-all cursor-pointer"
                          title={`Delete playlist "${category.name}"`}
                        >
                          <Trash2 size={11} />
                        </button>
                      )
                    )}
                    <span className="text-[10px] bg-[#121418] border border-[#21242d] text-neutral-400 px-1.5 py-0.2 rounded-full font-mono-telemetry">
                      {category.videoCount}
                    </span>
                  </div>
                </NavLink>
              ))
            )}
          </div>
        </div>
      </div>

      {/* Bottom Storage Telemetry Section from Stitch DESIGN.md */}
      <div className="p-3 bg-[#0c0e12] border-t border-[#1a1d24] flex flex-col gap-2 mt-auto">
        <div className="flex items-center justify-between font-mono-telemetry text-[10px]">
          <span className="text-neutral-400 uppercase tracking-tight">Storage Array</span>
          <span className="text-[#3cd69b] font-bold">{Math.round(storageHealth.usedPercent)}%</span>
        </div>

        <Progress
          value={storageHealth.usedPercent}
          color={storageHealth.usedPercent > 85 ? 'red' : 'cyberMint'}
          size="xs"
          radius="xl"
          bg="#1e2024"
        />

        <div className="flex items-center justify-between font-mono-telemetry text-[10px] text-neutral-500">
          <span>{storageHealth.formattedUsed} / {storageHealth.formattedTotal}</span>
          <span className="text-cyan-400 font-semibold">ZFS POOL 0</span>
        </div>

        <div className="pt-2 border-t border-[#1a1d24] flex items-center justify-between">
          <NavLink
            to="/settings"
            onClick={handleLinkClick}
            className="flex items-center gap-2 text-xs text-neutral-400 hover:text-white transition-colors"
          >
            <SettingsIcon size={14} />
            <span>Settings</span>
          </NavLink>

          <Tooltip label="MyTube Homelab OS v4.19">
            <span className="flex items-center gap-1 font-mono-telemetry text-[10px] text-neutral-500">
              <span className="w-1.5 h-1.5 rounded-full bg-[#3cd69b]"></span>
              <span>NODE1</span>
            </span>
          </Tooltip>
        </div>
      </div>
    </aside>
  );
};
