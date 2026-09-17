import React, { useState } from "react";
import { Link } from "react-router-dom";
import {
  Shield,
  Lock,
  User,
  AlertCircle,
  ArrowRight,
  Loader2,
  Eye,
  EyeOff,
} from "lucide-react";
import api from "../../api";

const Login = () => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [isForgot, setIsForgot] = useState(false);
  const [forgotSuccess, setForgotSuccess] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const response = await api.post("/auth/login", { username, password });
      const { user } = response.data;

      // Reload the authenticated application context so the session cookie is
      // read before protected routes and the SignalR connection are started.
      window.location.assign("/");
    } catch (err) {
      const responseError = err.response?.data;
      const readableError =
        typeof responseError === "string"
          ? responseError
          : responseError?.message || responseError?.code;
      setError(
        readableError ||
          "Une erreur est survenue lors de l'authentification.",
      );
    } finally {
      setLoading(false);
    }
  };

  const [email, setEmail] = useState("");

  const handleForgot = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.post("/auth/forgot-password", { email });
      setForgotSuccess(true);
    } catch (err) {
      // Still show success even on error to prevent email enumeration, but log it
      console.error("Forgot password error", err);
      setForgotSuccess(true);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-6 relative overflow-hidden">
      {/* Background Decorative Elements */}
      <div className="absolute top-0 left-0 w-full h-full overflow-hidden pointer-events-none">
        <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] bg-emerald-500/10 blur-[120px] rounded-full" />
        <div className="absolute bottom-[-10%] right-[-10%] w-[40%] h-[40%] bg-blue-500/10 blur-[120px] rounded-full" />
      </div>

      <div className="w-full max-w-md relative z-10">
        <div className="flex flex-col items-center mb-10">
          <img
            src="/logo.svg"
            alt="NexorSys Identity logo"
            className="w-48 h-auto object-contain mb-4 drop-shadow-[0_4px_8px_rgba(16,185,129,0.3)]"
          />
          <p className="text-sm text-slate-500 font-medium">
            NexorSys Identity — secure access management
          </p>
        </div>

        <div className="glass-card !p-8 border border-slate-200/50 shadow-2xl">
          {!isForgot ? (
            <form onSubmit={handleSubmit} className="space-y-6">
              {error && (
                <div className="p-4 bg-red-500/10 border border-red-500/20 rounded-xl flex items-start gap-3 text-red-400 animate-in fade-in slide-in-from-top-4">
                  <AlertCircle size={20} className="shrink-0 mt-0.5" />
                  <div className="text-sm font-medium">{error}</div>
                </div>
              )}

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-widest text-slate-500 ml-1">
                  Identifiant de connexion
                </label>
                <div className="relative">
                  <User
                    className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500"
                    size={18}
                  />
                  <input
                    type="text"
                    required
                    autoFocus
                    placeholder="ex: jdoe"
                    className="form-input w-full pl-12 h-12 bg-white/50 border-slate-200 transition-all focus:border-emerald-500/50 focus:ring-4 focus:ring-emerald-500/10"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-widest text-slate-500 ml-1">
                  Mot de passe
                </label>
                <div className="relative">
                  <Lock
                    className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500"
                    size={18}
                  />
                  <input
                    type={showPassword ? "text" : "password"}
                    required
                    placeholder="••••••••"
                    className="form-input w-full pl-12 pr-12 h-12 bg-white/50 border-slate-200 transition-all focus:border-emerald-500/50 focus:ring-4 focus:ring-emerald-500/10"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-500 hover:text-emerald-500 transition-colors focus:outline-none"
                    aria-label={
                      showPassword
                        ? "Masquer le mot de passe"
                        : "Afficher le mot de passe"
                    }
                  >
                    {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
              </div>

              <div className="flex justify-end mb-4">
                <Link to="/forgot-password" className="text-sm text-blue-400 hover:text-blue-300 transition-colors">
                  Mot de passe oublié ?
                </Link>
              </div>

              <button
                type="submit"
                disabled={loading}
                className="btn-primary w-full h-12 flex items-center justify-center gap-2 text-lg font-bold group relative overflow-hidden transition-all active:scale-95 disabled:opacity-50"
              >
                {loading ? (
                  <Loader2 size={24} className="animate-spin" />
                ) : (
                  <>
                    Se connecter
                    <ArrowRight
                      size={18}
                      className="transition-transform group-hover:translate-x-1"
                    />
                  </>
                )}
              </button>
            </form>
          ) : (
            <div className="space-y-6">
              {forgotSuccess ? (
                <div className="text-center space-y-4 py-4">
                  <div className="w-16 h-16 bg-emerald-500/20 rounded-full flex items-center justify-center mx-auto">
                    <AlertCircle className="text-emerald-500" size={32} />
                  </div>
                  <h3 className="text-xl font-bold">Demande envoyée</h3>
                  <p className="text-slate-600 text-sm">
                    Un email contenant un lien de réinitialisation vous a été envoyé. 
                    Il sera valable 15 minutes.
                  </p>
                  <button
                    onClick={() => {
                      setIsForgot(false);
                      setForgotSuccess(false);
                    }}
                    className="text-emerald-500 font-bold uppercase text-xs"
                  >
                    Retour à la connexion
                  </button>
                </div>
              ) : (
                <form onSubmit={handleForgot} className="space-y-6">
                  <div className="space-y-2 text-center mb-6">
                    <h3 className="text-xl font-bold">Mot de passe oublié</h3>
                    <p className="text-slate-600 text-sm">
                      Saisissez votre adresse email pour recevoir un lien de réinitialisation.
                    </p>
                  </div>
                  <div className="space-y-2">
                    <label className="text-xs font-bold uppercase tracking-widest text-slate-500 ml-1">
                      Adresse Email
                    </label>
                    <div className="relative">
                      <User
                        className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500"
                        size={18}
                      />
                      <input
                        type="email"
                        required
                        placeholder="ex: jdoe@example.com"
                        className="form-input w-full pl-12 h-12 bg-white/50 border-slate-200"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                      />
                    </div>
                  </div>
                  <button
                    type="submit"
                    disabled={loading}
                    className="btn-primary w-full h-12 flex items-center justify-center gap-2"
                  >
                    {loading ? (
                      <Loader2 size={24} className="animate-spin" />
                    ) : (
                      "Envoyer la demande"
                    )}
                  </button>
                  <div className="text-center">
                    <button
                      type="button"
                      onClick={() => setIsForgot(false)}
                      className="text-slate-500 hover:text-slate-900 text-xs font-bold uppercase"
                    >
                      Annuler
                    </button>
                  </div>
                </form>
              )}
            </div>
          )}

          <div className="mt-8 pt-6 border-t border-slate-200/50 text-center">
            <p className="text-xs text-slate-500 font-medium">
              Authentification sécurisée via Active Directory (NEXORSYS.LOCAL)
              <br />
              <span className="text-[10px] mt-1 block opacity-50 uppercase tracking-tighter">
                Accès réservé au personnel autorisé
              </span>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Login;
