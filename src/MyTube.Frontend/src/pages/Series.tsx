import React from 'react';
import { Tv, Sparkles, Settings as SettingsIcon } from 'lucide-react';
import { SonarrSeriesManager } from '../components/SonarrSeriesManager';
import { Link } from 'react-router-dom';

export const Series: React.FC = () => {
  return (
    <div className="flex-1 overflow-y-auto bg-[#090a0c] text-white">
      {/* Top Banner Header */}
      <div className="border-b border-[#1a1d24] bg-[#0c0d10] px-6 py-6 sticky top-0 z-30 shadow-lg">
        <div className="max-w-7xl mx-auto flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="p-3 rounded-2xl bg-blue-500/10 border border-blue-500/20 text-blue-400">
              <Tv size={26} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-xl font-bold font-heading text-neutral-100">
                  TV Shows & Series
                </h1>
                <span className="text-[10px] bg-blue-500/20 text-blue-400 px-2 py-0.5 rounded-full font-bold border border-blue-500/30">
                  Sonarr Engine
                </span>
              </div>
              <p className="text-xs text-neutral-400 mt-0.5">
                Automated TV tracking, season monitoring, indexer search, and episode playback in MyTube
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <Link
              to="/settings"
              className="p-2 rounded-xl bg-[#121418] hover:bg-[#1a1d24] border border-[#21242d] text-neutral-400 hover:text-white transition-colors cursor-pointer"
              title="Sonarr Settings"
            >
              <SettingsIcon size={16} />
            </Link>
          </div>
        </div>
      </div>

      {/* Main Content Area */}
      <div className="max-w-7xl mx-auto px-6 py-6">
        <SonarrSeriesManager />
      </div>
    </div>
  );
};
