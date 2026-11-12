import React, { useState } from 'react';
import { Mail, Lock, User as UserIcon, AlertCircle, ArrowRight, Eye, EyeOff, ShieldCheck, RefreshCw, RotateCcw } from 'lucide-react';
import { api } from '../services/api';
import { User } from '../types';

interface LoginProps {
  onLoginSuccess: (user: User) => void;
}

export const Login: React.FC<LoginProps> = ({ onLoginSuccess }) => {
  const [isSignUp, setIsSignUp] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  // Form Fields
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [rememberMe, setRememberMe] = useState(true);

  // OTP Verification Modal State
  const [showOtpModal, setShowOtpModal] = useState(false);
  const [otpCode, setOtpCode] = useState('');
  const [sendingOtp, setSendingOtp] = useState(false);
  const [verifyingOtp, setVerifyingOtp] = useState(false);
  const [otpSuccessMessage, setOtpSuccessMessage] = useState<string | null>(null);

  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Handle Sign In (Image 4)
  const handleSignIn = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password.trim()) {
      setError('Please enter both email/username and password.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const user = await api.login(email.trim(), password);
      onLoginSuccess(user);
    } catch (err: any) {
      setError(err.message || 'Login failed. Please verify your credentials.');
    } finally {
      setLoading(false);
    }
  };

  // Handle Sign Up Step 1 -> Request Brevo OTP (Image 3)
  const handleSignUpInit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!fullName.trim() || !email.trim() || !password.trim()) {
      setError('Please fill in all required fields.');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    if (!agreeTerms) {
      setError('You must agree to the Terms of Service and Privacy Policy.');
      return;
    }

    setSendingOtp(true);
    setError(null);

    try {
      const res = await api.sendOtp(email.trim(), fullName.trim(), 'register');
      setOtpSuccessMessage(res.message);
      setOtpCode('');
      setShowOtpModal(true);
    } catch (err: any) {
      setError(err.message || 'Failed to send verification code. Please check email address.');
    } finally {
      setSendingOtp(false);
    }
  };

  // Handle Sign Up Step 2 -> Verify Brevo OTP & Create User
  const handleVerifyOtpAndRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!otpCode.trim() || otpCode.trim().length !== 6) {
      setError('Please enter the valid 6-digit verification code.');
      return;
    }

    setVerifyingOtp(true);
    setError(null);

    try {
      const user = await api.register(fullName.trim(), email.trim(), password, otpCode.trim());
      setShowOtpModal(false);
      onLoginSuccess(user);
    } catch (err: any) {
      setError(err.message || 'OTP verification failed. Please try again.');
    } finally {
      setVerifyingOtp(false);
    }
  };

  const handleResendOtp = async () => {
    setSendingOtp(true);
    setError(null);
    try {
      const res = await api.sendOtp(email.trim(), fullName.trim(), 'register');
      setOtpSuccessMessage(res.message);
    } catch (err: any) {
      setError(err.message || 'Failed to resend code.');
    } finally {
      setSendingOtp(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#090a0c] bg-grid-pattern px-4 py-8 text-white relative overflow-hidden">
      
      {/* Background ambient lighting */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 h-96 w-96 rounded-full bg-[#3cd69b]/10 blur-[120px] pointer-events-none" />

      <div className="w-full max-w-md flex flex-col items-center">

        {/* Top Header Branding */}
        <div className="flex flex-col items-center gap-1 mb-6 text-center select-none">
          <h1 className="text-3xl font-extrabold tracking-tight font-heading text-[#3cd69b]">
            MyTube
          </h1>
          <p className="text-xs text-neutral-400 font-medium">
            {isSignUp ? 'Create your premium account' : 'Premium Streaming Experience'}
          </p>
        </div>

        {/* Auth Glass Card matching Image 3 & Image 4 */}
        <div className="w-full rounded-2xl bg-[#121418]/90 border border-[#21242d] p-8 shadow-2xl backdrop-blur-xl relative">
          
          <h2 className="text-xl font-bold text-neutral-100 mb-6 font-heading">
            {isSignUp ? 'Create Account' : 'Sign In'}
          </h2>

          {/* Error alert */}
          {error && (
            <div className="flex items-center gap-2.5 rounded-xl bg-red-950/40 border border-red-500/30 px-4 py-3 text-xs text-red-400 mb-6">
              <AlertCircle size={16} className="shrink-0" />
              <p>{error}</p>
            </div>
          )}

          {/* SIGN IN FORM (Matching Image 4) */}
          {!isSignUp ? (
            <form onSubmit={handleSignIn} className="flex flex-col gap-5">
              
              {/* Email Address with Mail icon on RIGHT */}
              <div className="flex flex-col gap-1.5">
                <label className="text-[11px] font-bold text-neutral-400 uppercase tracking-wider">
                  EMAIL ADDRESS
                </label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-4 py-3 transition-colors">
                  <input
                    type="text"
                    placeholder="hello@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    disabled={loading}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                  <Mail size={18} className="text-neutral-500 ml-2.5 shrink-0" />
                </div>
              </div>

              {/* Password with Eye/Lock icon on RIGHT */}
              <div className="flex flex-col gap-1.5">
                <label className="text-[11px] font-bold text-neutral-400 uppercase tracking-wider">
                  PASSWORD
                </label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-4 py-3 transition-colors">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    disabled={loading}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="text-neutral-500 hover:text-neutral-300 ml-2.5 shrink-0 cursor-pointer"
                  >
                    {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
              </div>

              {/* Options row */}
              <div className="flex items-center justify-between text-xs mt-1 select-none">
                <label className="flex items-center gap-2 text-neutral-400 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={rememberMe}
                    onChange={(e) => setRememberMe(e.target.checked)}
                    className="rounded bg-[#090a0c] border-[#21242d] text-[#3cd69b] focus:ring-0 cursor-pointer"
                  />
                  Remember me
                </label>
                <a href="#forgot" onClick={(e) => { e.preventDefault(); alert("Please contact Administrator to reset password."); }} className="text-[#3cd69b] hover:underline font-semibold">
                  Forgot password?
                </a>
              </div>

              {/* Primary Mint Green Button */}
              <button
                type="submit"
                disabled={loading}
                className="w-full py-3.5 mt-2 rounded-xl bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold text-sm transition-all duration-200 cursor-pointer shadow-lg shadow-[#3cd69b]/10 disabled:opacity-50 disabled:pointer-events-none"
              >
                {loading ? 'Signing In...' : 'Sign In'}
              </button>

              <div className="text-center text-xs text-neutral-400 mt-2 select-none">
                Don't have an account?{' '}
                <button
                  type="button"
                  onClick={() => { setIsSignUp(true); setError(null); }}
                  className="text-[#3cd69b] font-bold hover:underline cursor-pointer"
                >
                  Sign Up
                </button>
              </div>
            </form>
          ) : (
            /* SIGN UP FORM (Matching Image 3) */
            <form onSubmit={handleSignUpInit} className="flex flex-col gap-4">
              
              {/* Full Name */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-semibold text-neutral-300">Full Name</label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-3.5 py-2.5 transition-colors">
                  <UserIcon size={16} className="text-neutral-500 mr-2.5 shrink-0" />
                  <input
                    type="text"
                    placeholder="John Doe"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    disabled={sendingOtp}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                </div>
              </div>

              {/* Email Address */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-semibold text-neutral-300">Email Address</label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-3.5 py-2.5 transition-colors">
                  <Mail size={16} className="text-neutral-500 mr-2.5 shrink-0" />
                  <input
                    type="email"
                    placeholder="john@example.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    disabled={sendingOtp}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                </div>
              </div>

              {/* Password */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-semibold text-neutral-300">Password</label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-3.5 py-2.5 transition-colors">
                  <Lock size={16} className="text-neutral-500 mr-2.5 shrink-0" />
                  <input
                    type="password"
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    disabled={sendingOtp}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                </div>
              </div>

              {/* Confirm Password */}
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-semibold text-neutral-300">Confirm Password</label>
                <div className="flex items-center rounded-xl bg-[#090a0c] border border-[#21242d] focus-within:border-[#3cd69b] px-3.5 py-2.5 transition-colors">
                  <RotateCcw size={16} className="text-neutral-500 mr-2.5 shrink-0" />
                  <input
                    type="password"
                    placeholder="••••••••"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    disabled={sendingOtp}
                    autoComplete="off"
                    className="w-full bg-transparent text-sm text-neutral-100 placeholder-neutral-600 outline-none"
                  />
                </div>
              </div>

              {/* Terms Checkbox */}
              <div className="flex items-center gap-2 text-xs text-neutral-400 mt-1">
                <input
                  type="checkbox"
                  checked={agreeTerms}
                  onChange={(e) => setAgreeTerms(e.target.checked)}
                  className="rounded bg-[#090a0c] border-[#21242d] text-[#3cd69b] focus:ring-0 cursor-pointer"
                />
                <span>
                  I agree to the <a href="#terms" className="text-[#3cd69b] hover:underline">Terms of Service</a> and <a href="#privacy" className="text-[#3cd69b] hover:underline">Privacy Policy</a>.
                </span>
              </div>

              {/* Primary Mint Green Button matching Image 3 */}
              <button
                type="submit"
                disabled={sendingOtp}
                className="w-full py-3.5 mt-2 rounded-xl bg-[#3cd69b] hover:bg-[#34c78f] text-neutral-950 font-bold text-sm transition-all duration-200 cursor-pointer shadow-lg flex items-center justify-center gap-2 disabled:opacity-50"
              >
                {sendingOtp ? (
                  <span className="flex items-center gap-2">
                    <RefreshCw size={16} className="animate-spin" />
                    Sending Brevo OTP...
                  </span>
                ) : (
                  <>
                    Create Account
                    <ArrowRight size={16} />
                  </>
                )}
              </button>

              <div className="text-center text-xs text-neutral-400 mt-2 select-none">
                Already have an account?{' '}
                <button
                  type="button"
                  onClick={() => { setIsSignUp(false); setError(null); }}
                  className="text-[#3cd69b] font-bold hover:underline cursor-pointer"
                >
                  Sign In
                </button>
              </div>
            </form>
          )}

        </div>
      </div>

      {/* Interactive Brevo Email OTP Verification Modal */}
      {showOtpModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/85 backdrop-blur-md p-4">
          <div className="w-full max-w-md bg-[#121418] border border-[#21242d] rounded-2xl p-6 shadow-2xl relative flex flex-col gap-4 animate-fadeIn">
            
            <div className="flex items-center gap-3 border-b border-[#21242d] pb-3">
              <div className="p-2.5 rounded-xl bg-[#3cd69b]/10 border border-[#3cd69b]/20 text-[#3cd69b]">
                <ShieldCheck size={24} />
              </div>
              <div>
                <h3 className="text-base font-bold text-neutral-100 font-heading">Email Verification</h3>
                <p className="text-xs text-neutral-400">Powered by Brevo Transactional Email</p>
              </div>
            </div>

            {otpSuccessMessage && (
              <p className="text-xs text-neutral-300 leading-relaxed bg-[#090a0c] p-3 rounded-xl border border-[#21242d]">
                {otpSuccessMessage} Check inbox for <strong>{email}</strong>.
              </p>
            )}

            {error && (
              <div className="flex items-center gap-2 rounded-xl bg-red-950/40 border border-red-500/30 px-3.5 py-2.5 text-xs text-red-400">
                <AlertCircle size={15} className="shrink-0" />
                <p>{error}</p>
              </div>
            )}

            <form onSubmit={handleVerifyOtpAndRegister} className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-semibold text-neutral-300">Enter 6-Digit OTP Code</label>
                <input
                  type="text"
                  maxLength={6}
                  required
                  placeholder="123456"
                  value={otpCode}
                  onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, ''))}
                  disabled={verifyingOtp}
                  className="bg-[#090a0c] border border-[#21242d] focus:border-[#3cd69b] text-center text-2xl tracking-[8px] font-bold font-mono py-3 rounded-xl outline-none text-[#3cd69b]"
                />
              </div>

              <button
                type="submit"
                disabled={verifyingOtp || otpCode.length !== 6}
                className="w-full py-3 bg-[#3cd69b] hover:bg-[#34c78f] disabled:bg-neutral-800 disabled:text-neutral-500 text-neutral-950 font-bold text-sm rounded-xl transition-colors cursor-pointer flex items-center justify-center gap-2"
              >
                {verifyingOtp ? (
                  <span className="flex items-center gap-2">
                    <RefreshCw size={16} className="animate-spin" />
                    Verifying OTP...
                  </span>
                ) : (
                  'Verify & Create Account'
                )}
              </button>

              <div className="flex items-center justify-between text-xs text-neutral-400 pt-1">
                <button
                  type="button"
                  onClick={handleResendOtp}
                  disabled={sendingOtp}
                  className="text-[#3cd69b] font-semibold hover:underline cursor-pointer"
                >
                  {sendingOtp ? 'Sending code...' : 'Resend Code'}
                </button>
                
                <button
                  type="button"
                  onClick={() => setShowOtpModal(false)}
                  className="text-neutral-500 hover:text-neutral-300 cursor-pointer"
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
