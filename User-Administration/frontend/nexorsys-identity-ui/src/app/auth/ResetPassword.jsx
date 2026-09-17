import React, { useState, useEffect } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { Lock, AlertCircle, ArrowRight, Loader2, CheckCircle } from "lucide-react";
import api from "../../api";

const ResetPassword = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get("token");

  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    if (!token) {
      setError("Le lien de réinitialisation est manquant ou invalide.");
    }
  }, [token]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!token) return;

    if (password !== confirmPassword) {
      setError("Les mots de passe ne correspondent pas.");
      return;
    }

    if (password.length < 8) {
      setError("Le mot de passe doit contenir au moins 8 caractères.");
      return;
    }

    setError(null);
    setLoading(true);

    try {
      await api.post("/auth/reset-password", { token, newPassword: password });
      setSuccess(true);
      setTimeout(() => {
        navigate("/login");
      }, 3000);
    } catch (err) {
      const responseError = err.response?.data;
      const readableError =
        typeof responseError === "string"
          ? responseError
          : responseError?.message || "Une erreur est survenue lors de la réinitialisation.";
      setError(readableError);
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
          {success ? (
            <div className="text-center space-y-4 py-4">
              <div className="w-16 h-16 bg-emerald-500/20 rounded-full flex items-center justify-center mx-auto">
                <CheckCircle className="text-emerald-500" size={32} />
              </div>
              <h3 className="text-xl font-bold">Mot de passe modifié !</h3>
              <p className="text-slate-600 text-sm">
                Votre mot de passe a été mis à jour avec succès.
                Vous allez être redirigé vers la page de connexion...
              </p>
              <button
                onClick={() => navigate("/login")}
                className="text-emerald-500 font-bold uppercase text-xs"
              >
                Retourner à la connexion
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="space-y-2 text-center mb-6">
                <h3 className="text-xl font-bold">Nouveau mot de passe</h3>
                <p className="text-slate-600 text-sm">
                  Veuillez choisir un nouveau mot de passe sécurisé.
                </p>
              </div>

              {error && (
                <div className="p-4 bg-red-500/10 border border-red-500/20 rounded-xl flex items-start gap-3 text-red-400 animate-in fade-in slide-in-from-top-4">
                  <AlertCircle size={20} className="shrink-0 mt-0.5" />
                  <div className="text-sm font-medium">{error}</div>
                </div>
              )}

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-widest text-slate-500 ml-1">
                  Nouveau mot de passe
                </label>
                <div className="relative">
                  <Lock
                    className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500"
                    size={18}
                  />
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    className="form-input w-full pl-12 h-12 bg-white/50 border-slate-200 transition-all focus:border-emerald-500/50 focus:ring-4 focus:ring-emerald-500/10"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-xs font-bold uppercase tracking-widest text-slate-500 ml-1">
                  Confirmer le mot de passe
                </label>
                <div className="relative">
                  <Lock
                    className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-500"
                    size={18}
                  />
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    className="form-input w-full pl-12 h-12 bg-white/50 border-slate-200 transition-all focus:border-emerald-500/50 focus:ring-4 focus:ring-emerald-500/10"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={loading || !token}
                className="btn-primary w-full h-12 flex items-center justify-center gap-2 text-lg font-bold group relative overflow-hidden transition-all active:scale-95 disabled:opacity-50"
              >
                {loading ? (
                  <Loader2 size={24} className="animate-spin" />
                ) : (
                  <>
                    Enregistrer
                    <ArrowRight
                      size={18}
                      className="transition-transform group-hover:translate-x-1"
                    />
                  </>
                )}
              </button>
            </form>
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

export default ResetPassword;
