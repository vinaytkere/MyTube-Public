import { useEffect, useState, lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { api } from './services/api';
import { User } from './types';
import { ToastProvider } from './components/Toast';
import { ErrorBoundary } from './components/ErrorBoundary';

// Components (eagerly loaded layout)
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { MobileNavBar } from './components/MobileNavBar';
import { Login } from './pages/Login';

// Pages (lazy loaded for optimal bundle size and fast initial paint)
const Home = lazy(() => import('./pages/Home').then(m => ({ default: m.Home })));
const VideoDetails = lazy(() => import('./pages/VideoDetails').then(m => ({ default: m.VideoDetails })));
const Favorites = lazy(() => import('./pages/Favorites').then(m => ({ default: m.Favorites })));
const AdminScanner = lazy(() => import('./pages/AdminScanner').then(m => ({ default: m.AdminScanner })));
const VideoDownloader = lazy(() => import('./pages/VideoDownloader').then(m => ({ default: m.VideoDownloader })));
const Audio = lazy(() => import('./pages/Audio').then(m => ({ default: m.Audio })));
const History = lazy(() => import('./pages/History').then(m => ({ default: m.History })));
const Settings = lazy(() => import('./pages/Settings').then(m => ({ default: m.Settings })));
const Profile = lazy(() => import('./pages/Profile').then(m => ({ default: m.Profile })));
const Movies = lazy(() => import('./pages/Movies').then(m => ({ default: m.Movies })));
const Series = lazy(() => import('./pages/Series').then(m => ({ default: m.Series })));
const MovieOrganizerPage = lazy(() => import('./pages/MovieOrganizerPage').then(m => ({ default: m.MovieOrganizerPage })));
const IptvPage = lazy(() => import('./pages/IptvPage').then(m => ({ default: m.IptvPage })));

const PageLoader = () => (
  <div className="flex-1 flex items-center justify-center p-12 text-neutral-400">
    <div className="flex flex-col items-center gap-2">
      <Loader2 className="animate-spin text-[#3cd69b]" size={28} />
      <span className="text-xs font-medium">Loading page...</span>
    </div>
  </div>
);

function App() {
  const [currentUser, setCurrentUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [sidebarOpen, setSidebarOpen] = useState(window.innerWidth >= 768);

  const loadUser = async () => {
    const token = api.getToken();
    if (!token) {
      setLoading(false);
      return;
    }

    try {
      const user = await api.getMe();
      setCurrentUser(user);
    } catch (err) {
      console.error('Failed to load user session:', err);
      api.logout();
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUser();

    // Listen to global auth expiration event
    const handleAuthExpired = () => {
      setCurrentUser(null);
    };

    window.addEventListener('auth_expired', handleAuthExpired);
    return () => {
      window.removeEventListener('auth_expired', handleAuthExpired);
    };
  }, []);

  const handleLoginSuccess = (user: User) => {
    setCurrentUser(user);
  };

  const handleLogout = () => {
    api.logout();
    setCurrentUser(null);
  };

  const toggleSidebar = () => {
    setSidebarOpen(prev => !prev);
  };

  const collapseSidebar = () => {
    setSidebarOpen(false);
  };

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[#090a0c] text-neutral-400">
        <div className="flex flex-col items-center gap-2">
          <Loader2 className="animate-spin text-[#3cd69b]" size={32} />
          <span className="text-sm font-medium">Initializing session...</span>
        </div>
      </div>
    );
  }

  if (!currentUser) {
    return <Login onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <ErrorBoundary>
      <ToastProvider>
        <BrowserRouter>
          <div className="flex flex-col min-h-screen bg-[#090a0c] text-white">
            {/* Accessibility skip link */}
            <a
              href="#main-content"
              className="sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:px-4 focus:py-2 focus:bg-[#3cd69b] focus:text-neutral-950 focus:font-bold focus:rounded-lg focus:shadow-lg"
            >
              Skip to main content
            </a>

            {/* Main Header */}
            <Header 
              onToggleSidebar={toggleSidebar} 
              currentUser={currentUser} 
              onLogout={handleLogout} 
            />
            
            {/* Content Layout */}
            <div className="flex flex-1">
              {/* Collapsible Sidebar */}
              <Sidebar isOpen={sidebarOpen} currentUser={currentUser} onNavigate={() => { if (window.innerWidth < 768) collapseSidebar(); }} />
              
              {/* Routed Main View */}
              <main id="main-content" className="flex-1 flex flex-col min-w-0 pb-16 md:pb-0">
                <Suspense fallback={<PageLoader />}>
                  <Routes>
                    <Route path="/" element={<Home currentUser={currentUser} />} />
                    <Route path="/movies" element={<Movies />} />
                    <Route path="/organizer" element={<MovieOrganizerPage />} />
                    <Route path="/series" element={<Series />} />
                    <Route path="/tv" element={<Series />} />
                    <Route path="/iptv" element={<IptvPage currentUser={currentUser} />} />
                    <Route path="/releases" element={<Movies />} />
                    <Route path="/torrents" element={<Movies />} />
                    <Route path="/profile" element={<Profile currentUser={currentUser} />} />
                    <Route path="/video/:id" element={<VideoDetails currentUser={currentUser} onCollapseSidebar={collapseSidebar} />} />
                    <Route path="/favorites" element={<Favorites />} />
                    <Route path="/history" element={<History currentUser={currentUser} />} />
                    <Route path="/audio" element={<Audio />} />
                    <Route path="/settings" element={<Settings currentUser={currentUser} />} />
                    
                    {/* Standalone Video Downloader restricted to Administrator role */}
                    <Route 
                      path="/downloader" 
                      element={
                        currentUser.role === 'Administrator' 
                          ? <VideoDownloader /> 
                          : <Navigate to="/" replace />
                      } 
                    />

                    {/* Admin Scanner route restricted to Administrator role */}
                    <Route 
                      path="/admin" 
                      element={
                        currentUser.role === 'Administrator' 
                          ? <AdminScanner /> 
                          : <Navigate to="/" replace />
                      } 
                    />
                    
                    <Route path="*" element={<Navigate to="/" replace />} />
                  </Routes>
                </Suspense>
              </main>
            </div>

            {/* Mobile Bottom Navigation Dock */}
            <MobileNavBar currentUser={currentUser} />
          </div>
        </BrowserRouter>
      </ToastProvider>
    </ErrorBoundary>
  );
}

export default App;

