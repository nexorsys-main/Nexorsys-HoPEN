import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  CreditCard,
  Hash,
  RefreshCcw,
  Lock,
  Unlock,
  Search,
  CheckCircle2,
  AlertTriangle,
  History,
  ShieldCheck,
  ShieldAlert,
  ShieldPlus,
  Zap,
  Activity,
  Key,
  User,
  Loader2,
  Check,
  Smartphone,
  Shield,
  Fingerprint,
  Globe2,
  Award,
  Plus,
  Trash2,
  ToggleLeft,
  ToggleRight,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import api from "../../api";
import { useApp } from "../../AppContext";
import * as signalR from "@microsoft/signalr";

const KioskHub = () => {
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState(null);
  const [selectedUser, setSelectedUser] = useState(null);
  const [securityStatus, setSecurityStatus] = useState(null);
  const [syncing, setSyncing] = useState(false);
  const [error, setError] = useState(null);
  const [status, setStatus] = useState({ type: "", message: "" });
  const [devices, setDevices] = useState([]);
  const [kioskUrl, setKioskUrl] = useState("nexorsyskiosk://launch");
  const [showAddMieModal, setShowAddMieModal] = useState(false);
  const [newMie, setNewMie] = useState({
    deviceType: "NFC_Badge",
    deviceName: "",
    deviceIdentifier: "",
    isPrimary: true,
  });
  const [showPinModal, setShowPinModal] = useState(false);
  const [showRevokeModal, setShowRevokeModal] = useState(false);
  const [showUnlockModal, setShowUnlockModal] = useState(false);
  const [pinValue, setPinValue] = useState("");
  const [pinType, setPinType] = useState("reset"); // 'reset' or 'badge'
  const { lastEvent, triggerRefresh } = useApp();
  const navigate = useNavigate();

  const [lastMie, setLastMie] = useState(null);
  const lastProcessedEventRef = useRef(null);

  useEffect(() => {
    if (status.message) {
      const timer = setTimeout(() => {
        setStatus({ type: "", message: "" });
      }, 4000);
      return () => clearTimeout(timer);
    }
  }, [status]);

  const fetchDevices = useCallback(async (userId) => {
    if (!userId) return;
    try {
      const response = await api.get(`/devices/user/${userId}`);
      setDevices(response.data);
    } catch {
      setError("Impossible de charger les MIE du kiosque; les données existantes sont conservées.");
    }
  }, []);

  const fetchStatus = useCallback(
    async (userId) => {
      if (!userId) return;
      setLoading(true);
      setError(null);
      try {
        const response = await api.get(`/kiosk/status/${userId}`);
        setSecurityStatus((prev) => {
          // Only update if data changed to prevent unnecessary re-renders
          if (JSON.stringify(prev) === JSON.stringify(response.data))
            return prev;
          return response.data;
        });
        await fetchDevices(userId);
      } catch (err) {
        if (err.response?.status === 404) {
          setSecurityStatus({ needsInit: true });
        } else {
          setError("Impossible de charger l'état de sécurité.");
        }
      } finally {
        setLoading(false);
      }
    },
    [fetchDevices],
  );

  const handleSearch = useCallback(async (query) => {
    if (!query) {
      setSearchResults([]);
      return;
    }
    setLoading(true);
    try {
      const response = await api.get(`/users/search?query=${query}`);
      setSearchResults(response.data);
    } catch {
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      handleSearch(searchQuery);
    }, 500);
    return () => clearTimeout(timer);
  }, [searchQuery, handleSearch]);

  useEffect(() => {
    if (selectedUserId) {
      fetchStatus(selectedUserId);
    } else {
      setSecurityStatus(null);
    }
  }, [selectedUserId, fetchStatus]);

  useEffect(() => {
    if (
      lastEvent?.timestamp &&
      lastEvent.timestamp !== lastProcessedEventRef.current
    ) {
      lastProcessedEventRef.current = lastEvent.timestamp;

      if (lastEvent.type === "OnUserStatusChanged") {
        const data = lastEvent.data;
        if (
          selectedUserId &&
          (data.userId === selectedUserId ||
            data.samAccountName === selectedUser?.samAccountName)
        ) {
          setStatus({
            type: "info",
            message: `Mise à jour statut en direct pour ${data.samAccountName}: ${data.isActive ? "ACTIF" : "BLOQUÉ"}`,
          });
          fetchStatus(selectedUserId);
        }
      }
    }
  }, [lastEvent, selectedUserId, selectedUser?.samAccountName]); // Removed fetchStatus to break potential circularity

  const handleSelectUser = (user) => {
    setSelectedUser(user);
    if (user.id) {
      setSelectedUserId(user.id);
    } else {
      setSelectedUserId(null);
      setSecurityStatus({
        needsSync: true,
        samAccountName: user.samAccountName,
      });
    }
  };

  const handleSyncUser = async (samAccountName) => {
    setSyncing(true);
    try {
      const response = await api.post(`/users/sync/${samAccountName}`);
      const newUser = response.data;
      setSelectedUser(newUser);
      setSelectedUserId(newUser.id);
      setStatus({
        type: "success",
        message: "L'utilisateur a été importé de l'AD avec succès.",
      });
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la synchronisation de l'utilisateur.",
      });
    } finally {
      setSyncing(false);
    }
  };

  const handleResetPin = async () => {
    if (!selectedUserId || !pinValue || pinValue.length < 4) {
      alert("Format du PIN invalide (min 4 chiffres).");
      return;
    }

    setActionLoading(true);
    try {
      await api.post("/kiosk/reset-pin", {
        userId: selectedUserId,
        newPin: pinValue,
      });
      setStatus({
        type: "success",
        message: "Code PIN réinitialisé et activé.",
      });
      setShowPinModal(false);
      setPinValue("");
      fetchStatus(selectedUserId);
      triggerRefresh();
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la réinitialisation du PIN.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handleRevokeBadge = async () => {
    if (!selectedUserId) return;

    setActionLoading(true);
    try {
      await api.post(`/kiosk/revoke-badge/${selectedUserId}`);
      setStatus({
        type: "success",
        message: "Badge et accès révoqués avec succès.",
      });
      setShowRevokeModal(false);
      fetchStatus(selectedUserId);
      triggerRefresh();
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la révocation du badge.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handleUpdateBadge = async () => {
    if (!selectedUserId || !pinValue) return;

    setActionLoading(true);
    try {
      await api.post("/kiosk/update-badge", {
        userId: selectedUserId,
        badgeUid: pinValue,
      });
      setStatus({ type: "success", message: "Badge associé avec succès." });
      setShowPinModal(false);
      setPinValue("");
      fetchStatus(selectedUserId);
      triggerRefresh();
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de l'association du badge.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handleUnlockAccount = async () => {
    if (!selectedUserId) return;

    setActionLoading(true);
    try {
      await api.post(`/kiosk/unlock-account/${selectedUserId}`);
      setStatus({
        type: "success",
        message: "Compte déverrouillé avec succès.",
      });
      setShowUnlockModal(false);
      fetchStatus(selectedUserId);
      triggerRefresh();
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors du déverrouillage du compte.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  const getMieIcon = (type) => {
    switch (type) {
      case "NFC_Badge":
        return <CreditCard size={16} />;
      case "CPS":
        return <Shield size={16} />;
      case "FIDO2":
        return <Key size={16} />;
      case "CartePS":
        return <Fingerprint size={16} />;
      case "PSI":
        return <Globe2 size={16} />;
      default:
        return <Smartphone size={16} />;
    }
  };

  const handleUpdateMieStatus = async (deviceId, newStatus) => {
    setActionLoading(true);
    try {
      await api.put(`/devices/${deviceId}/status`, { status: newStatus });
      setStatus({ type: "success", message: "Statut du MIE mis à jour." });
      fetchDevices(selectedUserId);
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la mise à jour du MIE.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handleAddMie = async () => {
    if (!selectedUserId || !newMie.deviceName) return;
    setActionLoading(true);
    try {
      await api.post("/devices", { ...newMie, userId: selectedUserId });
      setStatus({ type: "success", message: "MIE enregistré avec succès." });
      setShowAddMieModal(false);
      setNewMie({
        deviceType: "NFC_Badge",
        deviceName: "",
        deviceIdentifier: "",
        isPrimary: true,
      });
      fetchDevices(selectedUserId);
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de l'enregistrement du MIE.",
      });
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="max-w-7xl mx-auto space-y-8">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold">Kiosque Hub</h1>
          <p className="text-slate-500 mt-1">
            Gestion des badges NFC, réinitialisation des PIN et sécurité des
            terminaux
          </p>
        </div>

        {status.message && (
          <div
            className={`fixed top-6 right-6 z-50 p-4 rounded-xl border shadow-2xl flex items-center gap-3 animate-in slide-in-from-right duration-300 ${
              status.type === "success"
                ? "bg-emerald-500/10 border-emerald-500/20 text-emerald-400"
                : "bg-red-500/10 border-red-500/20 text-red-400"
            }`}
          >
            {status.type === "success" ? (
              <CheckCircle2 size={20} />
            ) : (
              <AlertTriangle size={20} />
            )}
            <span className="font-semibold">{status.message}</span>
          </div>
        )}

        <div className="flex gap-3">
          {import.meta.env.DEV ? (
            <button
              type="button"
              onClick={() => { window.location.href = kioskUrl; }}
              className="flex items-center gap-2 px-4 py-2 bg-nexorsys-teal text-slate-900 rounded-lg hover:bg-emerald-600 transition-colors font-semibold"
              title="Lancer NexorSys Kiosk localement"
            >
              <Lock size={18} />
              Lancer Kiosque Bureau
            </button>
          ) : (
            <a
              href={kioskUrl}
              className="flex items-center gap-2 px-4 py-2 bg-nexorsys-teal text-slate-900 rounded-lg hover:bg-emerald-600 transition-colors font-semibold"
              title="Nécessite l'installation locale de NexorSys Kiosk"
            >
              <Lock size={18} />
              Lancer Kiosque Bureau
            </a>
          )}
          <button
            onClick={() => navigate("/audit")}
            className="btn-secondary flex items-center gap-2"
          >
            <History size={18} />
            Journaux Kiosque
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Left Column: Search & Selection */}
        <div className="space-y-6">
          <div className="glass-card">
            <h3 className="text-sm font-bold uppercase text-slate-500 mb-4">
              Rechercher un agent
            </h3>
            <div className="relative">
              <Search
                className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500"
                size={18}
              />
              <input
                type="text"
                placeholder="Nom ou Matricule..."
                className="form-input w-full pl-10 h-10"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className="mt-6 space-y-2 max-h-[400px] overflow-y-auto pr-2 custom-scrollbar">
              {loading && (
                <div className="p-4 text-center">
                  <Loader2
                    size={24}
                    className="animate-spin text-emerald-500 mx-auto"
                  />
                </div>
              )}

              {searchResults.map((user) => (
                <button
                  key={user.id || user.samAccountName}
                  onClick={() => handleSelectUser(user)}
                  className={`w-full flex items-center justify-between p-3 rounded-xl transition-all border ${
                    selectedUserId === user.id ||
                    selectedUser?.samAccountName === user.samAccountName
                      ? "bg-emerald-500/10 border-emerald-500/30"
                      : "hover:bg-slate-100/50 border-slate-200/50"
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-full bg-slate-200 flex items-center justify-center text-xs font-bold text-slate-500">
                      {user.displayName?.substring(0, 2) || "??"}
                    </div>
                    <div className="text-left">
                      <div className="text-sm font-bold text-slate-900">
                        {user.displayName}
                      </div>
                      <div className="text-[10px] text-slate-500 uppercase font-bold tracking-tighter truncate max-w-[120px]">
                        {user.department || "Sans département"}
                      </div>
                    </div>
                  </div>
                  {(selectedUserId === user.id ||
                    selectedUser?.samAccountName === user.samAccountName) && (
                    <div className="w-5 h-5 rounded-full bg-emerald-500 flex items-center justify-center">
                      <ShieldCheck size={12} className="text-white" />
                    </div>
                  )}
                </button>
              ))}

              {!loading && searchQuery && searchResults.length === 0 && (
                <div className="p-4 text-center text-xs text-slate-500 italic">
                  Aucun agent trouvé
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right Column: User Management Actions */}
        <div className="lg:col-span-2 space-y-6">
          {loading ? (
            <div className="glass-card h-[400px] flex flex-col items-center justify-center">
              <Loader2 className="animate-spin text-emerald-500" size={48} />
              <p className="mt-4 text-slate-500 font-medium">
                Analyse du profil de sécurité...
              </p>
            </div>
          ) : securityStatus?.needsSync ? (
            <div className="glass-card h-[400px] flex flex-col items-center justify-center text-center p-8">
              <div className="w-16 h-16 rounded-full bg-amber-500/10 flex items-center justify-center text-amber-500 mb-6">
                <ShieldAlert size={32} />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-2">
                Utilisateur non provisionné
              </h3>
              <p className="text-slate-500 mb-8 max-w-md">
                Cet agent existe dans l'Active Directory mais n'a pas encore de
                profil métier local. Vous devez l'importer pour gérer son accès
                NFC.
              </p>
              <button
                onClick={() => handleSyncUser(securityStatus.samAccountName)}
                disabled={syncing}
                className="btn-primary flex items-center gap-3"
              >
                {syncing ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <ShieldPlus size={18} />
                )}
                Importer et Activer le profil
              </button>
            </div>
          ) : securityStatus && !securityStatus.needsSync ? (
            <>
              {/* Profile Overview */}
              <div className="glass-card p-6 border-l-4 border-emerald-500">
                <div className="flex justify-between items-start">
                  <div className="flex gap-4">
                    <div className="w-16 h-16 rounded-2xl bg-emerald-600/10 flex items-center justify-center font-bold text-2xl text-emerald-500">
                      {securityStatus.displayName?.substring(0, 2)}
                    </div>
                    <div>
                      <h3 className="text-xl font-bold text-slate-900">
                        {securityStatus.displayName}
                      </h3>
                      <p className="text-slate-500 text-sm">
                        Identifiant unique :{" "}
                        <span className="font-mono">
                          {securityStatus.userId}
                        </span>
                      </p>
                      <div className="mt-3 flex gap-2">
                        {securityStatus.hasBadge ? (
                          <span className="badge badge-success flex items-center gap-1">
                            <ShieldCheck size={12} />{" "}
                            {securityStatus.deviceType === "CPS"
                              ? "Carte CPS Active"
                              : securityStatus.deviceType === "FIDO2"
                                ? "Clé FIDO2 Active"
                                : securityStatus.deviceType === "CartePS"
                                  ? "Carte PS Active"
                                  : "Badge Actif"}
                          </span>
                        ) : (
                          <span className="badge bg-slate-100 text-slate-600 flex items-center gap-1">
                            <ShieldAlert size={12} /> Aucun badge
                          </span>
                        )}
                        {securityStatus.isAccountLocked && (
                          <span className="badge badge-danger flex items-center gap-1">
                            <Lock size={12} /> Compte Verrouillé
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-[10px] uppercase font-bold text-slate-400 tracking-widest">
                      Dernière activité
                    </p>
                    <p className="text-sm font-bold text-slate-600">
                      Aujourd'hui, 10:42
                    </p>
                  </div>
                </div>
              </div>

              {/* Actions Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {/* Reset PIN Card */}
                <div className="glass-card p-6 hover:border-blue-500/30 transition-all group">
                  <div className="w-12 h-12 rounded-xl bg-blue-600/10 flex items-center justify-center text-blue-500 mb-4 group-hover:scale-110 transition-transform">
                    <Key size={24} />
                  </div>
                  <h4 className="text-lg font-bold text-slate-900">
                    Réinitialiser le PIN
                  </h4>
                  <p className="text-sm text-slate-500 mt-2 mb-6">
                    Changez le code secret à 4 chiffres de l'agent. Recommandé
                    en cas d'oubli ou suspicion.
                  </p>
                  <button
                    onClick={() => {
                      setPinType("reset");
                      setPinValue("");
                      setShowPinModal(true);
                    }}
                    className="btn-secondary w-full py-2 text-xs"
                  >
                    Réinitialisation contrôlée
                  </button>
                </div>

                {/* Badge Access Card */}
                <div className="glass-card p-6 hover:border-amber-500/30 transition-all group">
                  <div className="w-12 h-12 rounded-xl bg-amber-600/10 flex items-center justify-center text-amber-500 mb-4 group-hover:scale-110 transition-transform">
                    <Zap size={24} />
                  </div>
                  <h4 className="text-lg font-bold text-slate-900">
                    Gestion du Badge
                  </h4>
                  <p className="text-sm text-slate-500 mt-2 mb-6">
                    Activer ou révoquer l'accès physique immédiat de cet agent
                    aux terminaux kiosques.
                  </p>
                  <div className="flex gap-2">
                    {securityStatus?.hasBadge ? (
                      <button
                        onClick={() => setShowRevokeModal(true)}
                        disabled={actionLoading}
                        className="btn-danger w-full py-2 text-xs"
                      >
                        Révoquer{" "}
                        {securityStatus.deviceType === "CPS"
                          ? "la CPS"
                          : "le Badge"}
                      </button>
                    ) : (
                      <button
                        onClick={() => {
                          setPinType("badge");
                          setPinValue("");
                          setShowPinModal(true);
                        }}
                        disabled={actionLoading}
                        className="btn-primary w-full py-2 text-xs"
                      >
                        Assigner un Badge
                      </button>
                    )}
                  </div>
                </div>

                {/* Security Lock Card */}
                <div className="glass-card p-6 hover:border-red-500/30 transition-all group">
                  <div className="w-12 h-12 rounded-xl bg-red-600/10 flex items-center justify-center text-red-500 mb-4 group-hover:scale-110 transition-transform">
                    <Lock size={24} />
                  </div>
                  <h4 className="text-lg font-bold text-slate-900">
                    Déverrouillage
                  </h4>
                  <p className="text-sm text-slate-500 mt-2 mb-6">
                    {securityStatus?.isAccountLocked
                      ? "Le compte est actuellement BLOQUÉ (3+ échecs)."
                      : `Le compte est opérationnel (${securityStatus?.failedAttempts || 0} échec${(securityStatus?.failedAttempts || 0) > 1 ? "s" : ""} détecté${(securityStatus?.failedAttempts || 0) > 1 ? "s" : ""}).`}
                    {(securityStatus?.failedAttempts > 0 ||
                      securityStatus?.isAccountLocked) &&
                      " Vous pouvez réinitialiser la sécurité."}
                  </p>
                  <button
                    onClick={() => setShowUnlockModal(true)}
                    disabled={
                      (!securityStatus?.isAccountLocked &&
                        securityStatus?.failedAttempts === 0) ||
                      actionLoading
                    }
                    className="btn-secondary w-full py-2 text-xs disabled:opacity-50"
                  >
                    Déverrouiller le compte
                  </button>
                </div>

                {/* Logs Card */}
                <div className="glass-card p-6 hover:border-slate-500/30 transition-all group bg-slate-50/30">
                  <div className="w-12 h-12 rounded-xl bg-slate-600/10 flex items-center justify-center text-slate-500 mb-4 group-hover:scale-110 transition-transform">
                    <Activity size={24} />
                  </div>
                  <h4 className="text-lg font-bold text-slate-900">
                    Logs Individuels
                  </h4>
                  <p className="text-sm text-slate-500 mt-2 mb-6">
                    Consultez l'historique précis des connexions et des
                    changements de sécurité pour cet agent.
                  </p>
                  <button
                    onClick={() => navigate("/audit")}
                    className="text-emerald-500 font-bold text-sm hover:underline"
                  >
                    Voir les journaux →
                  </button>
                </div>
              </div>

              {/* MIE Devices Registry Section (PSI Compliance) */}
              <div className="glass-card !p-0 overflow-hidden border-2 border-emerald-500/10 mt-6">
                <div className="p-4 bg-emerald-500/5 border-b border-emerald-500/10 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="text-emerald-500" size={18} />
                    <h4 className="text-sm font-bold text-emerald-800 uppercase tracking-wider">
                      Registre des MIE (Conformité PSI)
                    </h4>
                  </div>
                  <button
                    onClick={() => setShowAddMieModal(true)}
                    className="text-[10px] font-bold bg-emerald-500 text-white px-2 py-1 rounded hover:bg-emerald-600 transition-colors flex items-center gap-1"
                  >
                    <Plus size={10} /> Nouveau MIE
                  </button>
                </div>

                <div className="p-2 space-y-1">
                  {devices.length === 0 ? (
                    <div className="p-6 text-center text-slate-400 text-xs italic">
                      Aucun dispositif MIE enregistré pour cet agent.
                    </div>
                  ) : (
                    devices.map((device) => (
                      <div
                        key={device.id}
                        className="flex items-center justify-between p-3 bg-white hover:bg-slate-50 border border-slate-100 rounded-lg transition-colors group"
                      >
                        <div className="flex items-center gap-3">
                          <div
                            className={`w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center text-slate-500`}
                          >
                            {getMieIcon(device.deviceType)}
                          </div>
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-bold text-slate-900">
                                {device.deviceName}
                              </span>
                              {device.isPrimary && (
                                <span className="text-[8px] bg-emerald-100 text-emerald-700 px-1 py-0.5 rounded font-bold uppercase">
                                  Principal
                                </span>
                              )}
                            </div>
                            <div className="text-[10px] text-slate-500 font-mono">
                              {device.deviceType} •{" "}
                              {device.deviceIdentifier || "N/A"}
                            </div>
                          </div>
                        </div>
                        <div className="flex items-center gap-2">
                          <span
                            className={`px-1.5 py-0.5 rounded text-[8px] font-bold uppercase ${
                              device.status === "active"
                                ? "bg-emerald-100 text-emerald-700"
                                : "bg-red-100 text-red-700"
                            }`}
                          >
                            {device.status === "active" ? "Actif" : "Désactivé"}
                          </span>
                          <div className="opacity-0 group-hover:opacity-100 transition-opacity flex gap-1">
                            {device.status === "active" ? (
                              <button
                                onClick={() =>
                                  handleUpdateMieStatus(device.id, "suspended")
                                }
                                className="p-1 hover:bg-amber-100 text-amber-600 rounded"
                                title="Désactiver"
                              >
                                <ToggleLeft size={14} />
                              </button>
                            ) : (
                              <button
                                onClick={() =>
                                  handleUpdateMieStatus(device.id, "active")
                                }
                                className="p-1 hover:bg-emerald-100 text-emerald-600 rounded"
                                title="Activer"
                              >
                                <ToggleRight size={14} />
                              </button>
                            )}
                          </div>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </>
          ) : (
            <div className="glass-card h-[400px] flex flex-col items-center justify-center text-slate-500">
              <div className="w-20 h-20 rounded-full bg-slate-50 flex items-center justify-center mb-6">
                <User size={40} className="text-slate-300" />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-2">
                Sélectionnez un agent pour gérer son accès kiosque
              </h3>
              <p className="text-slate-500">
                Utilisez la barre de recherche à gauche
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Add MIE Modal */}
      {showAddMieModal && (
        <div className="fixed inset-0 bg-black/50 z-[100] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-2xl max-w-md w-full animate-in zoom-in duration-300">
            <div className="p-6 border-b border-slate-200">
              <h2 className="text-xl font-bold text-slate-900">Nouveau MIE</h2>
              <p className="text-sm text-slate-500 mt-1">
                Enregistrer un dispositif pour {securityStatus?.displayName}
              </p>
            </div>
            <div className="p-6 space-y-4">
              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase">
                  Type de MIE
                </label>
                <select
                  className="form-input w-full"
                  value={newMie.deviceType}
                  onChange={(e) =>
                    setNewMie({ ...newMie, deviceType: e.target.value })
                  }
                >
                  <option value="NFC_Badge">Badge NFC</option>
                  <option value="CPS">Carte CPS</option>
                  <option value="FIDO2">Clé FIDO2</option>
                  <option value="CartePS">Carte PS</option>
                  <option value="PSI">PSI Token</option>
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase">
                  Libellé
                </label>
                <input
                  type="text"
                  className="form-input w-full"
                  placeholder="Ex: Badge principal"
                  value={newMie.deviceName}
                  onChange={(e) =>
                    setNewMie({ ...newMie, deviceName: e.target.value })
                  }
                />
              </div>
              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase">
                  Identifiant (UID / N°)
                </label>
                <input
                  type="text"
                  className="form-input w-full font-mono"
                  placeholder="ID du dispositif"
                  value={newMie.deviceIdentifier}
                  onChange={(e) =>
                    setNewMie({ ...newMie, deviceIdentifier: e.target.value })
                  }
                />
              </div>
            </div>
            <div className="p-6 border-t border-slate-200 flex justify-end gap-3">
              <button
                onClick={() => setShowAddMieModal(false)}
                className="btn-secondary"
              >
                Annuler
              </button>
              <button
                onClick={handleAddMie}
                disabled={actionLoading || !newMie.deviceName}
                className="btn-primary flex items-center gap-2"
              >
                {actionLoading ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  <Plus size={16} />
                )}
                Enregistrer
              </button>
            </div>
          </div>
        </div>
      )}
      {/* PIN / Badge Admin Modal */}
      {showPinModal && (
        <div className="fixed inset-0 bg-black/50 z-[110] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-2xl max-w-sm w-full animate-in zoom-in duration-300 overflow-hidden">
            <div
              className={`p-6 border-b border-slate-200 ${pinType === "badge" ? "bg-emerald-500/10" : "bg-blue-500/10"}`}
            >
              <h2 className="text-xl font-bold text-slate-900">
                {pinType === "reset" ? "Réinitialiser PIN" : "Assigner Badge"}
              </h2>
              <p className="text-xs text-slate-500 mt-1 font-medium">
                Agent: {securityStatus?.displayName}
              </p>
            </div>

            <div className="p-6 space-y-4">
              <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                    {pinType === "reset"
                      ? "Nouveau code PIN (4 chiffres)"
                      : "UID du Badge NFC"}
                  </label>
                  <div className="relative">
                    <input
                      type={pinType === "reset" ? "password" : "text"}
                      maxLength={pinType === "reset" ? 6 : 20}
                      className="form-input w-full text-center text-lg font-bold tracking-widest font-mono"
                      placeholder={pinType === "reset" ? "••••" : "A1B2C3D4"}
                      value={pinValue}
                      onChange={(e) => setPinValue(e.target.value)}
                      autoFocus
                    />
                  </div>
                  {pinType === "reset" && (
                    <p className="text-[10px] text-slate-400 italic">
                      L'agent devra le changer à la première connexion.
                    </p>
                  )}
              </div>
            </div>

            <div className="p-4 bg-slate-50 flex justify-end gap-3">
              <button
                onClick={() => setShowPinModal(false)}
                className="px-4 py-2 text-sm font-bold text-slate-500 hover:text-slate-700"
              >
                Annuler
              </button>
              <button
                onClick={() => {
                  if (pinType === "reset") handleResetPin();
                  else if (pinType === "badge") handleUpdateBadge();
                }}
                disabled={actionLoading || !pinValue}
                className={`px-6 py-2 rounded-xl text-sm font-bold text-white shadow-lg transition-all flex items-center gap-2 ${
                  pinType === "badge"
                    ? "bg-emerald-500 hover:bg-emerald-600 shadow-emerald-500/20"
                    : "bg-blue-500 hover:bg-blue-600 shadow-blue-500/20"
                }`}
              >
                {actionLoading ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  <Check size={16} />
                )}
                Confirmer
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Revoke Confirmation Modal */}
      {showRevokeModal && (
        <div className="fixed inset-0 bg-black/50 z-[120] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-2xl max-w-sm w-full animate-in zoom-in duration-300 overflow-hidden">
            <div className="p-6 bg-red-500/10 border-b border-red-100">
              <div className="w-12 h-12 rounded-full bg-red-100 flex items-center justify-center text-red-600 mb-4 mx-auto">
                <AlertTriangle size={24} />
              </div>
              <h2 className="text-xl font-bold text-slate-900 text-center">
                Révoquer l'Accès ?
              </h2>
              <p className="text-sm text-slate-500 mt-2 text-center">
                Cette action désactivera immédiatement le badge de l'agent{" "}
                <strong>{securityStatus?.displayName}</strong>.
              </p>
            </div>

            <div className="p-6 space-y-4">
              <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-2">
                <div className="flex justify-between text-xs">
                  <span className="text-slate-500">Identifiant du badge:</span>
                  <span className="font-mono font-bold text-slate-700">
                    {securityStatus?.hasBadge ? "Enregistré" : "Aucun badge"}
                  </span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-slate-500">Dernière activité:</span>
                  <span className="font-bold text-slate-700">
                    Aujourd'hui, 10:42
                  </span>
                </div>
              </div>
            </div>

            <div className="p-4 bg-slate-50 flex gap-3">
              <button
                onClick={() => setShowRevokeModal(false)}
                className="flex-1 px-4 py-2 text-sm font-bold text-slate-500 hover:text-slate-700"
              >
                Conserver
              </button>
              <button
                onClick={handleRevokeBadge}
                disabled={actionLoading}
                className="flex-1 px-6 py-2 rounded-xl text-sm font-bold text-white bg-red-600 hover:bg-red-500 shadow-lg shadow-red-500/20 transition-all flex items-center justify-center gap-2"
              >
                {actionLoading ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  <Trash2 size={16} />
                )}
                Révoquer
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Unlock Confirmation Modal */}
      {showUnlockModal && (
        <div className="fixed inset-0 bg-black/50 z-[120] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-2xl max-w-sm w-full animate-in zoom-in duration-300 overflow-hidden">
            <div className="p-6 bg-emerald-500/10 border-b border-emerald-100">
              <div className="w-12 h-12 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600 mb-4 mx-auto">
                <ShieldCheck size={24} />
              </div>
              <h2 className="text-xl font-bold text-slate-900 text-center">
                Déverrouiller le Compte ?
              </h2>
              <p className="text-sm text-slate-500 mt-2 text-center">
                Voulez-vous réinitialiser les tentatives de connexion pour{" "}
                <strong>{securityStatus?.displayName}</strong> ?
              </p>
            </div>

            <div className="p-6 space-y-4">
              <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-2">
                <div className="flex justify-between text-xs">
                  <span className="text-slate-500">Tentatives échouées:</span>
                  <span className="font-bold text-red-600">
                    {securityStatus?.failedAttempts} / 3
                  </span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-slate-500">Verrouillé jusqu'à:</span>
                  <span className="font-bold text-slate-700">
                    {securityStatus?.lockedUntil
                      ? new Date(
                          securityStatus.lockedUntil,
                        ).toLocaleTimeString()
                      : "N/A"}
                  </span>
                </div>
              </div>
            </div>

            <div className="p-4 bg-slate-50 flex gap-3">
              <button
                onClick={() => setShowUnlockModal(false)}
                className="flex-1 px-4 py-2 text-sm font-bold text-slate-500 hover:text-slate-700"
              >
                Annuler
              </button>
              <button
                onClick={handleUnlockAccount}
                disabled={actionLoading}
                className="flex-1 px-6 py-2 rounded-xl text-sm font-bold text-white bg-emerald-600 hover:bg-emerald-500 shadow-lg shadow-emerald-500/20 transition-all flex items-center justify-center gap-2"
              >
                {actionLoading ? (
                  <Loader2 size={16} className="animate-spin" />
                ) : (
                  <Unlock size={16} />
                )}
                Déverrouiller
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default KioskHub;
