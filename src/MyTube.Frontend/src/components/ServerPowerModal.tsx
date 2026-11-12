import React, { useState } from 'react';
import { Power, RotateCcw, AlertTriangle, X, Loader2, CheckCircle2, ShieldAlert } from 'lucide-react';
import { Badge } from '@mantine/core';
import { api } from '../services/api';
import { useToast } from './Toast';

interface ServerPowerModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const ServerPowerModal: React.FC<ServerPowerModalProps> = ({ isOpen, onClose }) => {
  const { showToast } = useToast();
  const [loadingAction, setLoadingAction] = useState<'shutdown' | 'reboot' | null>(null);
  const [confirmedAction, setConfirmedAction] = useState<'shutdown' | 'reboot' | null>(null);
  const [completeMessage, setCompleteMessage] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleExecutePower = async (action: 'shutdown' | 'reboot') => {
    setLoadingAction(action);
    try {
      if (action === 'shutdown') {
        const res = await api.shutdownServer();
        showToast('warning', res.message || 'Homelab physical server shutdown initiated.', 'Server Power Off');
        setCompleteMessage('⚡ The physical homelab server is powering down now. All active services, downloads, and Jellyfin streams will terminate. To turn the server back on, physically press the machine’s power button.');
      } else {
        const res = await api.rebootServer();
        showToast('info', res.message || 'Homelab physical server reboot initiated.', 'Server Rebooting');
        setCompleteMessage('🔄 The physical homelab server is rebooting. It should come back online in 1-2 minutes.');
      }
    } catch (err: any) {
      showToast('error', err.message || `Failed to ${action} server.`, 'Power Operation Failed');
      setLoadingAction(null);
      setConfirmedAction(null);
    }
  };

  return (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/85 backdrop-blur-md p-4 animate-in fade-in duration-200">
      <div className="relative w-full max-w-lg bg-[#121418] border border-[#262a34] rounded-2xl shadow-2xl overflow-hidden text-[#e2e2e8]">
        
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-[#21242d] bg-[#171920]">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-rose-500/15 text-rose-400 border border-rose-500/30">
              <ShieldAlert size={20} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-white font-heading">Power Sentinel Controls</h2>
                <Badge color="rose" size="xs" variant="filled">ROOT RESTRICTED</Badge>
              </div>
              <p className="text-xs text-[#85948b]">Physical hardware ACPI management</p>
            </div>
          </div>
          {!completeMessage && (
            <button
              onClick={onClose}
              className="p-1.5 rounded-lg text-[#85948b] hover:text-white hover:bg-[#21242d] transition-colors cursor-pointer"
            >
              <X size={18} />
            </button>
          )}
        </div>

        {/* Content */}
        <div className="p-6 space-y-5">
          {completeMessage ? (
            <div className="py-4 text-center space-y-4">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-[#3cd69b]/15 border border-[#3cd69b]/30 text-[#3cd69b]">
                <CheckCircle2 size={32} />
              </div>
              <div className="space-y-1">
                <h3 className="text-lg font-bold text-white font-heading">Command Dispatched</h3>
                <p className="text-xs text-[#85948b] leading-relaxed max-w-md mx-auto">{completeMessage}</p>
              </div>
              <p className="text-[11px] text-[#525866] font-mono">You may safely close this browser window.</p>
            </div>
          ) : confirmedAction ? (
            /* Confirmation Step */
            <div className="space-y-4">
              <div className="flex items-start gap-3 p-4 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs leading-relaxed">
                <AlertTriangle size={20} className="shrink-0 text-rose-400 mt-0.5" />
                <div>
                  <span className="font-bold block text-sm text-rose-200 mb-1">
                    {confirmedAction === 'shutdown' ? 'Confirm Physical Power Off' : 'Confirm Physical Server Reboot'}
                  </span>
                  {confirmedAction === 'shutdown' ? (
                    <p>
                      Are you sure you want to <strong>completely shut down the physical homelab computer</strong>?
                      All active video downloads, torrents, and Jellyfin streams will stop immediately.
                      <span className="block mt-2 text-rose-400 font-semibold">
                        ⚠️ Physical access to the machine will be required to press the power button and turn it back on.
                      </span>
                    </p>
                  ) : (
                    <p>
                      Are you sure you want to <strong>reboot the physical homelab computer</strong>?
                      The system will restart and be unavailable for 1 to 2 minutes while rebooting.
                    </p>
                  )}
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 pt-2">
                <button
                  type="button"
                  disabled={loadingAction !== null}
                  onClick={() => setConfirmedAction(null)}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-[#85948b] hover:text-white bg-[#171920] hover:bg-[#21242d] transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  disabled={loadingAction !== null}
                  onClick={() => handleExecutePower(confirmedAction)}
                  className={`flex items-center gap-2 px-5 py-2 rounded-xl text-xs font-bold text-white transition-all shadow-lg cursor-pointer ${
                    confirmedAction === 'shutdown'
                      ? 'bg-rose-600 hover:bg-rose-500 shadow-rose-950/40'
                      : 'bg-amber-600 hover:bg-amber-500 shadow-amber-950/40'
                  }`}
                >
                  {loadingAction ? (
                    <>
                      <Loader2 size={14} className="animate-spin" />
                      <span>Dispatching command...</span>
                    </>
                  ) : (
                    <>
                      {confirmedAction === 'shutdown' ? <Power size={14} /> : <RotateCcw size={14} />}
                      <span>Yes, {confirmedAction === 'shutdown' ? 'Power Off Server' : 'Reboot Server'}</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          ) : (
            /* Action Choice Step */
            <div className="space-y-4">
              <p className="text-xs text-[#85948b]">
                Select an ACPI power control action for the physical homelab node (`192.168.1.16`).
              </p>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {/* Shut Down Card */}
                <button
                  type="button"
                  onClick={() => setConfirmedAction('shutdown')}
                  className="flex flex-col items-start gap-3 p-4 rounded-xl bg-[#090a0c] border border-[#21242d] hover:border-rose-500/50 hover:bg-rose-500/5 transition-all text-left cursor-pointer group"
                >
                  <div className="p-2.5 rounded-lg bg-rose-500/10 text-rose-400 group-hover:bg-rose-600 group-hover:text-white transition-colors">
                    <Power size={20} />
                  </div>
                  <div>
                    <span className="font-bold text-sm text-white block group-hover:text-rose-400 transition-colors font-mono">
                      Node Shutdown
                    </span>
                    <span className="text-[11px] text-[#85948b] leading-snug block mt-1">
                      Powers off the physical computer completely to conserve power.
                    </span>
                  </div>
                </button>

                {/* Reboot Card */}
                <button
                  type="button"
                  onClick={() => setConfirmedAction('reboot')}
                  className="flex flex-col items-start gap-3 p-4 rounded-xl bg-[#090a0c] border border-[#21242d] hover:border-amber-500/50 hover:bg-amber-500/5 transition-all text-left cursor-pointer group"
                >
                  <div className="p-2.5 rounded-lg bg-amber-500/10 text-amber-400 group-hover:bg-amber-600 group-hover:text-white transition-colors">
                    <RotateCcw size={20} />
                  </div>
                  <div>
                    <span className="font-bold text-sm text-white block group-hover:text-amber-400 transition-colors font-mono">
                      Host Reboot
                    </span>
                    <span className="text-[11px] text-[#85948b] leading-snug block mt-1">
                      Restarts the physical OS cleanly and reloads all services.
                    </span>
                  </div>
                </button>
              </div>

              <div className="p-3 rounded-xl bg-[#090a0c] border border-[#21242d] text-[11px] font-mono text-[#85948b] flex items-center justify-between">
                <span className="text-[#3cd69b] font-bold">Host Target:</span>
                <span>Proxmox / Debian Homelab Node</span>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
