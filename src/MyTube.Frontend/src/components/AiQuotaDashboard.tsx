import React, { useEffect, useState } from 'react';
import { Sparkles, RefreshCw, Zap, X, ShieldCheck } from 'lucide-react';
import { api } from '../services/api';
import { AiModelQuota } from '../types';

interface Props {
  isOpen?: boolean;
  onClose?: () => void;
  isEmbedded?: boolean;
}

export const AiQuotaDashboard: React.FC<Props> = ({ isOpen = true, onClose, isEmbedded = false }) => {
  const [quotas, setQuotas] = useState<AiModelQuota[]>([]);
  const [loading, setLoading] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [validating, setValidating] = useState(false);

  const fetchQuotas = async () => {
    setLoading(true);
    try {
      const data = await api.getAiQuotas();
      setQuotas(data);
    } catch (err) {
      console.error('Failed to load AI quotas:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async () => {
    setResetting(true);
    try {
      await api.resetAiQuotas();
      await fetchQuotas();
    } catch (err) {
      console.error('Failed to reset AI quotas:', err);
    } finally {
      setResetting(false);
    }
  };

  const handleValidate = async () => {
    setValidating(true);
    try {
      const res = await api.validateAiQuotas();
      if (res.quotas) {
        setQuotas(res.quotas);
      } else {
        await fetchQuotas();
      }
    } catch (err) {
      console.error('Failed to validate AI models:', err);
    } finally {
      setValidating(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchQuotas();
      const interval = setInterval(fetchQuotas, 5000); // Auto refresh every 5s
      return () => clearInterval(interval);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const renderStatusBadge = (q: AiModelQuota) => {
    const status = q.status || (q.isEnabled ? 'Active' : 'Disabled');

    if (!q.isEnabled || status === 'Disabled') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-neutral-800 text-neutral-400 border border-neutral-700">
          Disabled
        </span>
      );
    }
    if (status === 'ModelNotFound') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-purple-500/20 text-purple-300 border border-purple-500/30" title={q.lastErrorDetails || 'Model not found on provider API'}>
          Model Not Found (404)
        </span>
      );
    }
    if (status === 'RateLimited') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-amber-500/20 text-amber-400 border border-amber-500/30">
          Rate Limited (429)
        </span>
      );
    }
    if (status === 'AuthError') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-red-500/20 text-red-400 border border-red-500/30" title={q.lastErrorDetails || 'API key invalid'}>
          Auth Error (401)
        </span>
      );
    }
    if (status === 'PermissionError') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-orange-500/20 text-orange-400 border border-orange-500/30" title={q.lastErrorDetails || 'Access forbidden'}>
          Permission Error (403)
        </span>
      );
    }
    if (status === 'ProviderError') {
      return (
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-rose-500/20 text-rose-400 border border-rose-500/30" title={q.lastErrorDetails || 'Provider server error'}>
          Provider Error (5xx)
        </span>
      );
    }

    return (
      <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-emerald-500/20 text-[#3cd69b] border border-emerald-500/30">
        Active
      </span>
    );
  };

  const content = (
    <div className={`w-full bg-[#121418] border border-[#21242d] rounded-2xl shadow-xl overflow-hidden flex flex-col ${isEmbedded ? '' : 'max-h-[85vh] max-w-4xl'}`}>
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between px-6 py-4 border-b border-[#21242d] bg-[#0c0d10]/60 gap-3">
        <div className="flex items-center gap-3">
          <div className="p-2 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-[#3cd69b]">
            <Sparkles size={20} />
          </div>
          <div>
            <h2 className="text-base font-bold text-neutral-100 font-heading">AI Models & Quota Dashboard</h2>
            <p className="text-xs text-neutral-400">MyTube Local Tracked Requests & Provider Model Availability</p>
          </div>
        </div>

        <div className="flex items-center gap-2.5 self-end sm:self-auto">
          <button
            onClick={handleValidate}
            disabled={validating || loading}
            className="px-3.5 py-1.5 rounded-xl bg-blue-500/10 border border-blue-500/30 text-blue-400 hover:bg-blue-500/20 text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5"
            title="Check live provider model API availability"
          >
            <ShieldCheck size={14} className={validating ? 'animate-spin' : ''} />
            {validating ? 'Checking...' : 'Check Availability'}
          </button>

          <button
            onClick={handleReset}
            disabled={resetting || loading}
            className="px-3.5 py-1.5 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-[#3cd69b] hover:bg-emerald-500/20 text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5"
            title="Reset local tracked request counters"
          >
            <Zap size={14} className={resetting ? 'animate-pulse' : ''} />
            {resetting ? 'Resetting...' : 'Reset Limits'}
          </button>

          <button
            onClick={fetchQuotas}
            disabled={loading}
            className="p-2 rounded-xl bg-[#171920] border border-[#21242d] hover:bg-[#21242d] text-neutral-300 transition-colors cursor-pointer"
            title="Refresh Stats"
          >
            <RefreshCw size={16} className={loading ? 'animate-spin text-[#3cd69b]' : ''} />
          </button>

          {!isEmbedded && onClose && (
            <button
              onClick={onClose}
              className="p-2 rounded-xl bg-[#171920] border border-[#21242d] hover:bg-[#21242d] text-neutral-300 transition-colors cursor-pointer"
            >
              <X size={18} />
            </button>
          )}
        </div>
      </div>

      {/* Quota Grid */}
      <div className="p-6">
        {quotas.length === 0 ? (
          <div className="text-center py-8 text-neutral-500 text-sm">
            Loading AI model statistics...
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {quotas.map((q) => {
              const rpmPercent = q.rpmLimit > 0 ? Math.min(100, Math.round((q.currentMinuteRequests / q.rpmLimit) * 100)) : 0;
              const rpdPercent = q.rpdLimit > 0 ? Math.min(100, Math.round((q.currentDayRequests / q.rpdLimit) * 100)) : 0;

              const isRateLimited = q.status === 'RateLimited' ||
                (q.rpmLimit > 0 && q.currentMinuteRequests >= q.rpmLimit) ||
                (q.rpdLimit > 0 && q.currentDayRequests >= q.rpdLimit);

              return (
                <div
                  key={q.id}
                  className={`p-4 rounded-xl border transition-all ${
                    !q.isEnabled || q.status === 'ModelNotFound'
                      ? 'bg-purple-950/10 border-purple-500/20 opacity-80'
                      : isRateLimited
                      ? 'bg-amber-950/20 border-amber-500/30'
                      : 'bg-[#0c0d10]/60 border-[#21242d] hover:border-neutral-700'
                  }`}
                >
                  {/* Model Header */}
                  <div className="flex items-start justify-between gap-2 mb-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-bold text-neutral-200">{q.modelName}</span>
                        <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-full border ${
                          q.provider === 'Groq'
                            ? 'bg-purple-500/10 text-purple-400 border-purple-500/30'
                            : q.provider === 'Gemini'
                            ? 'bg-blue-500/10 text-blue-400 border-blue-500/30'
                            : 'bg-emerald-500/10 text-[#3cd69b] border-emerald-500/30'
                        }`}>
                          {q.provider}
                        </span>
                      </div>
                      <span className="text-[11px] text-neutral-500 font-mono mt-0.5 block">
                        Type: {q.taskType} • Priority #{q.priorityOrder}
                      </span>
                    </div>

                    {renderStatusBadge(q)}
                  </div>

                  {/* Usage Progress Bars */}
                  <div className="space-y-2.5 text-xs">
                    {/* RPM */}
                    <div>
                      <div className="flex justify-between text-[11px] text-neutral-400 mb-1">
                        <span>Tracked Requests / Min (RPM)</span>
                        <span className="font-mono font-medium">{q.currentMinuteRequests} / {q.rpmLimit > 0 ? q.rpmLimit : '∞'}</span>
                      </div>
                      <div className="w-full h-1.5 bg-[#171920] rounded-full overflow-hidden">
                        <div
                          className={`h-full transition-all duration-300 ${
                            rpmPercent >= 90 ? 'bg-red-500' : rpmPercent >= 70 ? 'bg-amber-500' : 'bg-[#3cd69b]'
                          }`}
                          style={{ width: `${q.rpmLimit > 0 ? Math.max(5, rpmPercent) : 5}%` }}
                        />
                      </div>
                    </div>

                    {/* RPD */}
                    <div>
                      <div className="flex justify-between text-[11px] text-neutral-400 mb-1">
                        <span>Tracked Requests / Day (RPD)</span>
                        <span className="font-mono font-medium">{q.currentDayRequests} / {q.rpdLimit > 0 ? q.rpdLimit.toLocaleString() : '∞'}</span>
                      </div>
                      <div className="w-full h-1.5 bg-[#171920] rounded-full overflow-hidden">
                        <div
                          className={`h-full transition-all duration-300 ${
                            rpdPercent >= 90 ? 'bg-red-500' : rpdPercent >= 70 ? 'bg-amber-500' : 'bg-blue-500'
                          }`}
                          style={{ width: `${q.rpdLimit > 0 ? Math.max(5, rpdPercent) : 5}%` }}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );

  if (isEmbedded) {
    return content;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-md p-4">
      {content}
    </div>
  );
};
