import React, { Component, ErrorInfo, ReactNode } from 'react';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null
    };
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      error,
      errorInfo: null
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('ErrorBoundary caught an unhandled error:', error, errorInfo);
    this.setState({
      error,
      errorInfo
    });
  }

  handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null
    });
  };

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div className="min-h-screen bg-[#0f0f0f] text-neutral-200 flex items-center justify-center p-6">
          <div className="max-w-md w-full p-6 rounded-2xl bg-neutral-900 border border-neutral-800 shadow-2xl flex flex-col items-center text-center">
            <div className="w-14 h-14 rounded-2xl bg-red-500/10 border border-red-500/20 text-red-400 flex items-center justify-center mb-4">
              <AlertTriangle size={28} />
            </div>

            <h1 className="text-lg font-bold text-neutral-100 mb-1">
              Something went wrong
            </h1>
            <p className="text-xs text-neutral-400 mb-6 leading-relaxed">
              An unexpected display error occurred while rendering the interface.
            </p>

            {this.state.error && (
              <div className="w-full mb-6 p-3 rounded-xl bg-neutral-950 border border-neutral-850 text-left overflow-hidden">
                <span className="text-[11px] font-mono text-red-400 font-semibold block truncate">
                  {this.state.error.name}: {this.state.error.message}
                </span>
              </div>
            )}

            <div className="flex items-center gap-3 w-full">
              <button
                type="button"
                onClick={this.handleReload}
                className="flex-1 h-10 flex items-center justify-center gap-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold cursor-pointer transition-colors shadow-md"
              >
                <RefreshCw size={14} />
                Reload Page
              </button>
              <button
                type="button"
                onClick={() => {
                  this.handleReset();
                  window.location.href = '/';
                }}
                className="h-10 px-4 flex items-center justify-center gap-2 rounded-xl bg-neutral-800 hover:bg-neutral-750 text-neutral-300 text-xs font-semibold cursor-pointer transition-colors"
              >
                <Home size={14} />
                Home
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
