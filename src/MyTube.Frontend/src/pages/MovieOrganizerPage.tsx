import React from 'react';
import { MovieOrganizerTab } from '../components/MovieOrganizerTab';

export const MovieOrganizerPage: React.FC = () => {
  return (
    <div className="flex-1 overflow-y-auto min-h-screen bg-[#090a0c] text-white pb-16">
      <div className="max-w-7xl mx-auto px-6 pt-6">
        <MovieOrganizerTab initialFilter="movie" />
      </div>
    </div>
  );
};
