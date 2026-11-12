import React, { createContext, useContext, useState, useCallback } from 'react';
import { CheckCircle2, XCircle, AlertCircle, Info, X } from 'lucide-react';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface ToastMessage {
  id: string;
  type: ToastType;
  message: string;
  title?: string;
  duration?: number;
}

interface ToastContextType {
  showToast: (type: ToastType, message: string, title?: string, duration?: number) => void;
  removeToast: (id: string) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
};

export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback((type: ToastType, message: string, title?: string, duration = 4000) => {
    const id = Math.random().toString(36).substring(2, 9);
    const newToast: ToastMessage = { id, type, message, title, duration };
    setToasts((prev) => [...prev, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        removeToast(id);
      }, duration);
    }
  }, [removeToast]);

  const getToastStyles = (type: ToastType) => {
    switch (type) {
      case 'success':
        return {
          bg: 'bg-emerald-950/90 border-emerald-500/20 text-emerald-100',
          icon: <CheckCircle2 className="text-emerald-500 shrink-0" size={18} />,
          progressBg: 'bg-emerald-500',
        };
      case 'error':
        return {
          bg: 'bg-red-950/90 border-red-500/20 text-red-100',
          icon: <XCircle className="text-red-500 shrink-0" size={18} />,
          progressBg: 'bg-red-500',
        };
      case 'warning':
        return {
          bg: 'bg-amber-950/90 border-amber-500/20 text-amber-100',
          icon: <AlertCircle className="text-amber-500 shrink-0" size={18} />,
          progressBg: 'bg-amber-500',
        };
      default:
        return {
          bg: 'bg-neutral-900/90 border-neutral-800 text-neutral-100',
          icon: <Info className="text-emerald-500 shrink-0" size={18} />,
          progressBg: 'bg-emerald-500',
        };
    }
  };

  return (
    <ToastContext.Provider value={{ showToast, removeToast }}>
      {children}
      
      {/* Toast Portal/Container */}
      <div className="fixed bottom-6 right-6 z-50 flex flex-col gap-3 max-w-sm w-full pointer-events-none select-none">
        {toasts.map((toast) => {
          const styles = getToastStyles(toast.type);
          return (
            <div
              key={toast.id}
              className={`pointer-events-auto flex flex-col rounded-xl border p-4 shadow-xl backdrop-blur-md glass-panel ${styles.bg} transition-all duration-300 animate-slide-in relative overflow-hidden`}
              style={{
                animation: 'toastSlideIn 0.3s cubic-bezier(0.16, 1, 0.3, 1) forwards',
              }}
            >
              {/* Toast Core Content */}
              <div className="flex items-start gap-3">
                {styles.icon}
                <div className="flex-1 flex flex-col min-w-0">
                  {toast.title && (
                    <span className="text-xs font-bold font-heading mb-0.5 tracking-wide">
                      {toast.title}
                    </span>
                  )}
                  <span className="text-[11px] leading-relaxed text-neutral-300">
                    {toast.message}
                  </span>
                </div>
                <button
                  onClick={() => removeToast(toast.id)}
                  className="text-neutral-500 hover:text-neutral-300 transition-colors p-0.5 rounded-lg hover:bg-neutral-800/40 shrink-0 cursor-pointer"
                >
                  <X size={14} />
                </button>
              </div>

              {/* Progress bar animation */}
              {toast.duration && toast.duration > 0 && (
                <div className="absolute bottom-0 left-0 right-0 h-[2px] bg-neutral-900/30">
                  <div
                    className={`h-full ${styles.progressBg}`}
                    style={{
                      animation: `toastProgress ${toast.duration}ms linear forwards`,
                    }}
                  />
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Slide-in & Progress animations injected into styles */}
      <style>{`
        @keyframes toastSlideIn {
          from {
            transform: translateY(1.5rem) scale(0.95);
            opacity: 0;
          }
          to {
            transform: translateY(0) scale(1);
            opacity: 1;
          }
        }
        @keyframes toastProgress {
          from {
            width: 100%;
          }
          to {
            width: 0%;
          }
        }
      `}</style>
    </ToastContext.Provider>
  );
};
