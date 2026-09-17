import React, { useState, useEffect } from "react";
import {
  User,
  Mail,
  Phone,
  Lock,
  Save,
  Loader2,
  AlertCircle,
  CheckCircle2,
  Camera,
  Shield,
} from "lucide-react";
import api from "../../api";

const Profile = () => {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState({ type: "", message: "" });

  const [formData, setFormData] = useState({
    email: "",
    phoneNumber: "",
    newPassword: "",
    confirmPassword: "",
  });

  useEffect(() => {
    const fetchProfile = async () => {
      try {
        const response = await api.get("/users/me");
        setProfile(response.data);
        setFormData({
          email: response.data.email || "",
          phoneNumber: response.data.phoneNumber || "",
          newPassword: "",
          confirmPassword: "",
        });
      } catch (err) {
        setStatus({
          type: "error",
          message: "Erreur lors du chargement du profil.",
        });
      } finally {
        setLoading(false);
      }
    };

    fetchProfile();
  }, []);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (
      formData.newPassword &&
      formData.newPassword !== formData.confirmPassword
    ) {
      setStatus({
        type: "error",
        message: "Les mots de passe ne correspondent pas.",
      });
      return;
    }

    setSaving(true);
    setStatus({ type: "", message: "" });

    try {
      const response = await api.patch("/users/me", {
        email: formData.email,
        phoneNumber: formData.phoneNumber,
        newPassword: formData.newPassword || null,
      });

      setProfile(response.data);
      setFormData((prev) => ({
        ...prev,
        newPassword: "",
        confirmPassword: "",
      }));
      setStatus({ type: "success", message: "Profil mis à jour avec succès." });

    } catch (err) {
      setStatus({
        type: "error",
        message: "Une erreur est survenue lors de la mise à jour.",
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 size={40} className="text-emerald-500 animate-spin" />
      </div>
    );
  }

  return (
    <>
      {status.message && (
        <div
          className={`fixed bottom-8 left-1/2 -translate-x-1/2 z-[200] px-6 py-3 rounded-xl shadow-lg flex items-center gap-3 border animate-in fade-in slide-in-from-bottom-2 duration-300 ${
            status.type === "success"
              ? "bg-emerald-50 border-emerald-200 text-emerald-800"
              : "bg-red-50 border-red-200 text-red-800"
          }`}
        >
          {status.type === "success" ? (
            <CheckCircle2 size={18} />
          ) : (
            <AlertCircle size={18} />
          )}
          <span className="text-sm font-bold">{status.message}</span>
        </div>
      )}

      <div className="max-w-4xl mx-auto space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className="flex items-end justify-between">
          <div>
            <h1 className="text-3xl font-bold">Mon Profil</h1>
            <p className="text-slate-500 mt-1">
              Gérez vos informations personnelles et vos identifiants
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Left Column: Avatar & Summary */}
          <div className="space-y-6">
            <div className="glass-card flex flex-col items-center text-center p-8">
              <div className="relative group">
                <div className="w-32 h-32 rounded-full bg-slate-100 border-2 border-slate-300 flex items-center justify-center text-4xl font-bold text-emerald-500 shadow-2xl transition-transform group-hover:scale-105">
                  {profile?.displayName?.substring(0, 2).toUpperCase() || "??"}
                </div>
                <button className="absolute bottom-0 right-0 p-2 bg-emerald-600 rounded-full border-4 border-slate-900 text-slate-900 hover:bg-emerald-500 transition-colors shadow-lg">
                  <Camera size={16} />
                </button>
              </div>

              <div className="mt-6">
                <h2 className="text-xl font-bold">{profile?.displayName}</h2>
                <p className="text-emerald-500 text-sm font-bold uppercase tracking-widest mt-1">
                  {profile?.department}
                </p>
                <div className="flex items-center justify-center gap-2 mt-2 py-1 px-3 bg-slate-100/50 rounded-full border border-slate-300/50">
                  <Shield size={12} className="text-slate-600" />
                  <span className="text-[10px] font-bold text-slate-600 uppercase">
                    {profile?.title || "Agent Hospitalier"}
                  </span>
                </div>
              </div>

              <div className="w-full mt-8 pt-8 border-t border-slate-200 space-y-4">
                <div className="flex justify-between items-center text-sm">
                  <span className="text-slate-500">Identifiant</span>
                  <span className="font-mono font-bold text-slate-700">
                    {profile?.samAccountName}
                  </span>
                </div>
                <div className="flex justify-between items-center text-sm">
                  <span className="text-slate-500">Statut</span>
                  <span className="flex items-center gap-1.5 text-emerald-500 font-bold">
                    <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                    Actif
                  </span>
                </div>
                <div className="flex justify-between items-center text-sm">
                  <span className="text-slate-500">Membre depuis</span>
                  <span className="text-slate-700">
                    {new Date(profile?.createdAt).toLocaleDateString()}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Edit Form */}
          <div className="lg:col-span-2 space-y-6">
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="glass-card">
                <h3 className="text-lg font-bold mb-6 flex items-center gap-2">
                  <User size={20} className="text-emerald-500" />
                  Informations de contact
                </h3>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider ml-1">
                      Email Professionnel
                    </label>
                    <div className="relative">
                      <Mail
                        className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500"
                        size={18}
                      />
                      <input
                        type="email"
                        name="email"
                        value={formData.email}
                        onChange={handleChange}
                        placeholder="name@example.com"
                        className="form-input w-full pl-10"
                      />
                    </div>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider ml-1">
                      Numéro de contact
                    </label>
                    <div className="relative">
                      <Phone
                        className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500"
                        size={18}
                      />
                      <input
                        type="tel"
                        name="phoneNumber"
                        value={formData.phoneNumber}
                        onChange={handleChange}
                        placeholder="06 12 34 56 78"
                        className="form-input w-full pl-10"
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="glass-card">
                <h3 className="text-lg font-bold mb-6 flex items-center gap-2">
                  <Lock size={20} className="text-emerald-500" />
                  Sécurité du compte
                </h3>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider ml-1">
                      Nouveau mot de passe
                    </label>
                    <input
                      type="password"
                      name="newPassword"
                      value={formData.newPassword}
                      onChange={handleChange}
                      placeholder="••••••••"
                      className="form-input w-full"
                    />
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider ml-1">
                      Confirmer mot de passe
                    </label>
                    <input
                      type="password"
                      name="confirmPassword"
                      value={formData.confirmPassword}
                      onChange={handleChange}
                      placeholder="••••••••"
                      className="form-input w-full"
                    />
                  </div>
                </div>
                <p className="text-xs text-slate-500 mt-4 italic">
                  Laissez les champs vides si vous ne souhaitez pas modifier
                  votre mot de passe.
                </p>
              </div>

              <div className="flex justify-end pt-2">
                <button
                  type="submit"
                  disabled={saving}
                  className="btn-primary px-8 py-3 flex items-center gap-3 shadow-lg shadow-emerald-600/20"
                >
                  {saving ? (
                    <>
                      <Loader2 size={20} className="animate-spin" />
                      Enregistrement...
                    </>
                  ) : (
                    <>
                      <Save size={20} />
                      Enregistrer les modifications
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </>
  );
};

export default Profile;
