import React, { useState, useEffect, useRef } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { 
  Menu as MenuIcon, Search, Video, Bell, LogOut, Shield, Loader2, ArrowLeft, 
  SlidersHorizontal, Power, User, Cpu, Sparkles, RefreshCw
} from 'lucide-react';
import { Menu, Badge, Tooltip, Avatar, Indicator } from '@mantine/core';
import { api } from '../services/api';
import { Video as VideoType, User as UserType } from '../types';
import { ServerPowerModal } from './ServerPowerModal';

interface HeaderProps {
  onToggleSidebar: () => void;
  currentUser: UserType | null;
  onLogout: () => void;
}

export const Header: React.FC<HeaderProps> = ({ onToggleSidebar, currentUser, onLogout }) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [suggestions, setSuggestions] = useState<VideoType[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const [isSearchExpandedMobile, setIsSearchExpandedMobile] = useState(false);
  const [showPowerModal, setShowPowerModal] = useState(false);
  const navigate = useNavigate();
  const searchRef = useRef<HTMLFormElement>(null);

  const formatDuration = (secs: number) => {
    if (isNaN(secs) || secs <= 0) return '0:00';
    const m = Math.floor(secs / 60);
    const s = Math.floor(secs % 60);
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  };

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(event.target as Node)) {
        setShowSuggestions(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Global keyboard shortcut '/' to focus search
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === '/' && document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA') {
        e.preventDefault();
        searchRef.current?.querySelector('input')?.focus();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  useEffect(() => {
    if (!searchQuery.trim()) {
      setSuggestions([]);
      return;
    }

    setLoadingSuggestions(true);
    const delayDebounce = setTimeout(async () => {
      try {
        const res = await api.getVideos({ search: searchQuery.trim(), pageSize: 5 });
        setSuggestions(res.items);
      } catch (err) {
        console.error('Error loading search suggestions:', err);
      } finally {
        setLoadingSuggestions(false);
      }
    }, 280);

    return () => clearTimeout(delayDebounce);
  }, [searchQuery]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setShowSuggestions(false);
    setIsSearchExpandedMobile(false);
    if (searchQuery.trim()) {
      navigate(`/?search=${encodeURIComponent(searchQuery.trim())}`);
    } else {
      navigate('/');
    }
  };

  return (
    <header className="sticky top-0 z-50 flex h-14 items-center justify-between px-3 md:px-5 bg-[#090a0c]/85 backdrop-blur-xl border-b border-[#1a1d24] text-white shadow-[0_1px_12px_rgba(0,0,0,0.3)]">
      {/* Mobile search back button */}
      {isSearchExpandedMobile && (
        <button 
          onClick={() => { setIsSearchExpandedMobile(false); setSearchQuery(''); }}
          className="rounded-lg p-2 hover:bg-[#1e2024] text-neutral-300 transition-colors mr-2 shrink-0 sm:hidden"
          aria-label="Close search"
        >
          <ArrowLeft size={18} />
        </button>
      )}

      {/* Left side: Menu Toggle & Brand */}
      <div className={`items-center gap-2.5 ${isSearchExpandedMobile ? 'hidden' : 'flex'}`}>
        <button 
          onClick={onToggleSidebar} 
          className="rounded-lg p-2 hover:bg-[#1a1c20] text-neutral-400 hover:text-white transition-colors cursor-pointer"
          aria-label="Toggle navigation menu"
          title="Toggle Navigation Menu"
        >
          <MenuIcon size={19} />
        </button>
        
        <Link 
          to="/"
          onClick={() => setSearchQuery('')} 
          className="flex items-center gap-2 select-none rounded-lg group"
          aria-label="MyTube Home"
        >
          <div className="w-7 h-7 rounded-lg bg-[#3cd69b]/20 border border-[#3cd69b]/40 flex items-center justify-center text-[#3cd69b] group-hover:shadow-[0_0_12px_rgba(60,214,155,0.4)] transition-all">
            <Video size={16} className="text-[#3cd69b]" />
          </div>
          <span className="text-base font-extrabold tracking-tight font-heading hidden sm:inline">
            My<span className="text-[#3cd69b]">Tube</span>
          </span>
        </Link>
      </div>

      {/* Center: Search Bar with Autocomplete & Filter Chips (Stitch Design) */}
      <form 
        ref={searchRef} 
        onSubmit={handleSearchSubmit} 
        className={`relative flex-1 items-center ${
          isSearchExpandedMobile ? 'flex w-full mx-0' : 'hidden sm:flex max-w-2xl mx-4'
        }`}
        role="search"
      >
        <div className="flex w-full items-center rounded-xl bg-[#121418] border border-[#21242d] focus-within:border-[#3cd69b] focus-within:ring-1 focus-within:ring-[#3cd69b]/40 px-3 py-1.5 shadow-inner transition-all">
          <Search size={16} className="text-neutral-400 mr-2 shrink-0" aria-hidden="true" />
          <input
            type="text"
            placeholder="Search telemetry, films, indexers, live streams..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onFocus={() => setShowSuggestions(true)}
            autoComplete="off"
            aria-label="Search videos in library"
            className="w-full bg-transparent text-xs text-neutral-100 placeholder:text-neutral-500 outline-none"
          />

          {/* Quick Filter Pills inside Desktop search */}
          <div className="hidden lg:flex items-center gap-1 ml-2 shrink-0">
            <span 
              onClick={(e) => { e.stopPropagation(); navigate('/movies'); }} 
              className="px-2 py-0.5 rounded-full font-mono-telemetry text-[10px] bg-[#1a1c20] text-neutral-400 hover:text-white hover:bg-[#282a2e] transition-colors cursor-pointer"
            >
              Torrents
            </span>
            <span 
              onClick={(e) => { e.stopPropagation(); navigate('/iptv'); }} 
              className="px-2 py-0.5 rounded-full font-mono-telemetry text-[10px] bg-[#1a1c20] text-neutral-400 hover:text-white hover:bg-[#282a2e] transition-colors cursor-pointer"
            >
              IPTV
            </span>
          </div>

          <span className="ml-2 px-1.5 py-0.5 rounded font-mono-telemetry text-[10px] bg-[#1a1c20] text-neutral-500 border border-[#21242d] shrink-0 select-none">
            /
          </span>

          <button 
            type="button" 
            onClick={() => navigate('/?showFilters=true')}
            className="text-neutral-400 hover:text-[#3cd69b] ml-2 shrink-0 transition-colors cursor-pointer p-0.5"
            title="Filter Settings"
          >
            <SlidersHorizontal size={14} />
          </button>
        </div>

        {/* Autocomplete Dropdown Panel */}
        {showSuggestions && searchQuery.trim() && (
          <div className="absolute top-full left-0 right-0 mt-2 bg-[#121418]/95 border border-[#21242d] rounded-xl shadow-2xl backdrop-blur-xl overflow-hidden z-50 py-1.5 flex flex-col">
            {loadingSuggestions ? (
              <div className="flex items-center justify-center py-4 text-xs text-neutral-500 gap-2">
                <Loader2 className="animate-spin text-[#3cd69b]" size={14} />
                <span>Searching library & telemetry...</span>
              </div>
            ) : suggestions.length > 0 && (
              <div className="flex flex-col max-h-[300px] overflow-y-auto">
                {suggestions.map((video) => (
                  <div
                    key={video.id}
                    onClick={() => {
                      navigate(`/video/${video.id}`);
                      setShowSuggestions(false);
                      setSearchQuery('');
                      setIsSearchExpandedMobile(false);
                    }}
                    className="flex items-center gap-3 px-3 py-2 hover:bg-[#1a1c20] transition-colors cursor-pointer text-left"
                  >
                    <div className="h-9 w-14 bg-black rounded border border-[#21242d] overflow-hidden shrink-0 flex items-center justify-center">
                      {video.thumbnailUrl ? (
                        <img src={video.thumbnailUrl} alt={video.displayTitle} className="h-full w-full object-cover" />
                      ) : (
                        <Video className="text-neutral-700" size={13} />
                      )}
                    </div>
                    
                    <div className="flex-1 min-w-0 flex flex-col">
                      <span className="text-xs font-semibold text-neutral-200 truncate">
                        {video.displayTitle}
                      </span>
                      <span className="text-[10px] font-mono-telemetry text-neutral-500">
                        {video.categoryName} • {formatDuration(video.duration)} • {video.videoCodec || 'H.264'}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Quick Torrent Tracker Query Link */}
            <div
              onClick={() => {
                const q = searchQuery.trim();
                setShowSuggestions(false);
                setSearchQuery('');
                setIsSearchExpandedMobile(false);
                navigate(`/movies?query=${encodeURIComponent(q)}`);
              }}
              className="flex items-center gap-2 px-3 py-2 border-t border-[#21242d] hover:bg-[#1a1c20] text-neutral-300 hover:text-[#3cd69b] transition-colors cursor-pointer text-left"
            >
              <Search size={13} className="text-[#3cd69b] shrink-0" />
              <span className="text-xs font-semibold">
                Search Releases for "{searchQuery}" <span className="text-[10px] font-mono-telemetry text-neutral-500">(Prowlarr Indexers)</span>
              </span>
            </div>
          </div>
        )}
      </form>

      {/* Right side: Telemetry & Profile Menu (Stitch Design) */}
      <div className={`items-center gap-2 md:gap-3 ${isSearchExpandedMobile ? 'hidden' : 'flex'}`}>
        {/* Mobile search open button */}
        {!isSearchExpandedMobile && (
          <button 
            onClick={() => setIsSearchExpandedMobile(true)} 
            className="rounded-lg p-2 hover:bg-[#1a1c20] text-neutral-400 hover:text-white transition-colors cursor-pointer sm:hidden"
            aria-label="Open Search"
          >
            <Search size={18} />
          </button>
        )}

        {/* Telemetry pill: SYS NOMINAL */}
        <div className="hidden lg:flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-[#121418] border border-[#21242d]">
          <span className="w-2 h-2 rounded-full bg-[#3cd69b] animate-ping"></span>
          <span className="font-mono-telemetry text-[11px] text-neutral-200 font-semibold">SYS NOMINAL</span>
        </div>

        {/* Admin Server Power Quick Action */}
        {currentUser?.role === 'Administrator' && (
          <Tooltip label="Homelab Server Power Controls (Shut Down / Reboot)">
            <button 
              onClick={() => setShowPowerModal(true)} 
              className="p-1.5 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 transition-all cursor-pointer"
              aria-label="Homelab Power Controls"
            >
              <Power size={16} />
            </button>
          </Tooltip>
        )}

        {/* Notification Bell */}
        <button 
          className="rounded-lg p-1.5 hover:bg-[#1a1c20] text-neutral-300 hover:text-white transition-colors cursor-pointer relative"
          title="Notifications"
          onClick={() => alert('All homelab node services running normally.')}
        >
          <Bell size={18} />
          <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-[#3cd69b]"></span>
        </button>

        {/* Mantine User Profile Menu */}
        <Menu shadow="md" width={220} position="bottom-end" radius="lg">
          <Menu.Target>
            <div className="flex items-center gap-2 cursor-pointer p-1 rounded-lg hover:bg-[#1a1c20] transition-colors">
              <div className="hidden xl:flex flex-col text-right">
                <span className="text-xs font-semibold text-white leading-tight truncate max-w-[100px]">
                  {currentUser?.fullName || currentUser?.username}
                </span>
                <span className="font-mono-telemetry text-[9px] text-[#3cd69b] font-bold uppercase tracking-wider">
                  {currentUser?.role === 'Administrator' ? 'ROOT / ADMIN' : 'USER'}
                </span>
              </div>

              <div className="w-8 h-8 rounded-full bg-[#171920] border border-[#3cd69b]/40 flex items-center justify-center text-[#3cd69b] font-bold text-xs shadow-sm">
                {(currentUser?.username || 'U').substring(0, 2).toUpperCase()}
              </div>
            </div>
          </Menu.Target>

          <Menu.Dropdown bg="#121418" style={{ borderColor: '#21242d' }}>
            <Menu.Label className="font-mono-telemetry text-[10px] text-neutral-400">
              SIGNED IN AS @{currentUser?.username}
            </Menu.Label>

            <Menu.Item 
              leftSection={<User size={14} className="text-[#3cd69b]" />}
              onClick={() => navigate('/profile')}
              className="text-xs hover:bg-[#1a1c20]"
            >
              View User Profile
            </Menu.Item>

            <Menu.Item 
              leftSection={<Cpu size={14} className="text-cyan-400" />}
              onClick={() => navigate('/settings?tab=ai')}
              className="text-xs hover:bg-[#1a1c20]"
            >
              AI Quota & Models
            </Menu.Item>

            {currentUser?.role === 'Administrator' && (
              <Menu.Item 
                leftSection={<Power size={14} className="text-rose-400" />}
                onClick={() => setShowPowerModal(true)}
                className="text-xs text-rose-400 hover:bg-rose-950/20"
              >
                Server Power Sentinel
              </Menu.Item>
            )}

            <Menu.Divider style={{ borderColor: '#21242d' }} />

            <Menu.Item 
              leftSection={<LogOut size={14} className="text-red-400" />}
              onClick={onLogout}
              className="text-xs text-red-400 hover:bg-red-950/30"
            >
              Sign Out
            </Menu.Item>
          </Menu.Dropdown>
        </Menu>
      </div>

      {/* Server Power Control Modal */}
      <ServerPowerModal
        isOpen={showPowerModal}
        onClose={() => setShowPowerModal(false)}
      />
    </header>
  );
};
