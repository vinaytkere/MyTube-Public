import React, { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { Home, Film, Radio, Download, MoreHorizontal, Tv, Music, FolderCheck, Terminal, Settings as SettingsIcon, Heart, Clock } from 'lucide-react';
import { Drawer, Badge } from '@mantine/core';
import { User } from '../types';

interface MobileNavBarProps {
  currentUser: User | null;
}

export const MobileNavBar: React.FC<MobileNavBarProps> = ({ currentUser }) => {
  const [drawerOpened, setDrawerOpened] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const location = useLocation();

  React.useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!(document.fullscreenElement || (document as any).webkitFullscreenElement));
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
      document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
    };
  }, []);

  if (isFullscreen) return null;

  const navItemClass = (isActive: boolean) =>
    `flex flex-col items-center justify-center flex-1 py-1.5 transition-colors ${
      isActive ? 'text-[#3cd69b] font-bold' : 'text-neutral-400 hover:text-white'
    }`;

  return (
    <>
      <nav 
        className="fixed bottom-0 left-0 right-0 z-40 flex items-center justify-around h-14 bg-[#0c0e12]/95 backdrop-blur-xl border-t border-[#1a1d24] md:hidden px-1 shadow-[0_-2px_12px_rgba(0,0,0,0.5)]"
        aria-label="Mobile Navigation Dock"
      >
        <NavLink to="/" className={({ isActive }) => navItemClass(isActive)}>
          <Home size={19} />
          <span className="text-[10px] mt-0.5 font-medium">Home</span>
        </NavLink>

        <NavLink to="/movies" className={({ isActive }) => navItemClass(isActive)}>
          <Film size={19} />
          <span className="text-[10px] mt-0.5 font-medium">Torrents</span>
        </NavLink>

        <NavLink to="/iptv" className={({ isActive }) => navItemClass(isActive)}>
          <Radio size={19} />
          <span className="text-[10px] mt-0.5 font-medium">Live TV</span>
        </NavLink>

        {currentUser?.role === 'Administrator' ? (
          <NavLink to="/downloader" className={({ isActive }) => navItemClass(isActive)}>
            <Download size={19} />
            <span className="text-[10px] mt-0.5 font-medium">Downloads</span>
          </NavLink>
        ) : (
          <NavLink to="/favorites" className={({ isActive }) => navItemClass(isActive)}>
            <Heart size={19} />
            <span className="text-[10px] mt-0.5 font-medium">Favorites</span>
          </NavLink>
        )}

        <button 
          onClick={() => setDrawerOpened(true)}
          className={`flex flex-col items-center justify-center flex-1 py-1.5 transition-colors ${
            drawerOpened ? 'text-[#3cd69b]' : 'text-neutral-400 hover:text-white'
          }`}
          aria-label="More Options"
        >
          <MoreHorizontal size={19} />
          <span className="text-[10px] mt-0.5 font-medium">More</span>
        </button>
      </nav>

      {/* Mantine Drawer for Mobile "More" Menu */}
      <Drawer
        opened={drawerOpened}
        onClose={() => setDrawerOpened(false)}
        position="bottom"
        size="auto"
        radius="xl"
        bg="#0c0e12"
        styles={{
          content: { backgroundColor: '#0c0e12', borderTop: '1px solid #21242d', color: '#fff' },
          header: { backgroundColor: '#0c0e12', borderBottom: '1px solid #1a1d24', color: '#fff' },
          title: { fontWeight: 'bold', fontSize: '14px', fontFamily: 'Plus Jakarta Sans, sans-serif' }
        }}
        title="MyTube OS — All Modules"
      >
        <div className="grid grid-cols-3 gap-3 p-2 pb-6">
          <NavLink 
            to="/series" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-blue-500/10 text-cyan-400">
              <Tv size={20} />
            </div>
            <span className="text-xs font-semibold">TV Shows</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Sonarr</span>
          </NavLink>

          <NavLink 
            to="/organizer" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-emerald-500/10 text-[#3cd69b]">
              <FolderCheck size={20} />
            </div>
            <span className="text-xs font-semibold">Organizer</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Plex/Jellyfin</span>
          </NavLink>

          <NavLink 
            to="/audio" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-amber-500/10 text-amber-400">
              <Music size={20} />
            </div>
            <span className="text-xs font-semibold">Audio & Pods</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Hi-Fi</span>
          </NavLink>

          <NavLink 
            to="/favorites" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-rose-500/10 text-rose-400">
              <Heart size={20} />
            </div>
            <span className="text-xs font-semibold">Favorites</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Pinned</span>
          </NavLink>

          <NavLink 
            to="/history" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-neutral-800 text-neutral-300">
              <Clock size={20} />
            </div>
            <span className="text-xs font-semibold">History</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Resume</span>
          </NavLink>

          <NavLink 
            to="/settings" 
            onClick={() => setDrawerOpened(false)}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-[#3cd69b] transition-all text-center"
          >
            <div className="p-2 rounded-lg bg-purple-500/10 text-purple-400">
              <SettingsIcon size={20} />
            </div>
            <span className="text-xs font-semibold">Settings</span>
            <span className="text-[9px] text-neutral-500 font-mono-telemetry">Config</span>
          </NavLink>

          {currentUser?.role === 'Administrator' && (
            <NavLink 
              to="/admin" 
              onClick={() => setDrawerOpened(false)}
              className="flex flex-col items-center gap-1.5 p-3 rounded-xl bg-[#121418] border border-[#21242d] hover:border-rose-400 transition-all text-center col-span-3"
            >
              <div className="flex items-center gap-2 text-rose-400 font-semibold text-xs">
                <Terminal size={16} />
                <span>Diagnostics & Server Power Sentinel</span>
              </div>
            </NavLink>
          )}
        </div>
      </Drawer>
    </>
  );
};

