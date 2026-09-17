import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  CreditCard,
  Smartphone,
  Key,
  Fingerprint,
  Shield,
  Globe2,
  Search,
  Plus,
  Trash2,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Loader2,
  ShieldCheck,
  ShieldAlert,
  RefreshCw,
  ToggleLeft,
  ToggleRight,
  Award,
  User,
  Lock,
  Unlock,
} from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { useApp } from "../../AppContext";
import api from "../../api";
import * as signalR from "@microsoft/signalr";

const DEVICE_TYPES = [
  {
    value: "NFC_Badge",
    label: "Badge NFC",
    icon: <CreditCard size={20} />,
    color: "emerald",
    assurance: "Intermédiaire",
    desc: "Badge sans contact Mifare/Desfire pour kiosques",
    idLabel: "UID / Badge ID",
    idPlaceholder: "04:AB:CD:EF...",
  },
  {
    value: "CPS",
    label: "Carte CPS",
    icon: <Shield size={20} />,
    color: "blue",
    assurance: "Élevé",
    desc: "Carte Professionnel de Santé (puce contact)",
    idLabel: "N° de Carte CPS",
    idPlaceholder: "803100...",
  },
  {
    value: "FIDO2",
    label: "Clé USB FIDO2",
    icon: <Key size={20} />,
    color: "amber",
    assurance: "Élevé",
    desc: "Clé USB de sécurité (YubiKey, Titan, etc.)",
    idLabel: "Credential ID",
    idPlaceholder: "fido2_key_...",
  },
  {
    value: "CartePS",
    label: "Carte PS",
    icon: <Fingerprint size={20} />,
    color: "rose",
    assurance: "Élevé",
    desc: "Carte Professionnelle de Santé standard",
    idLabel: "N° de Carte PS",
    idPlaceholder: "281000...",
  },
  {
    value: "PSI",
    label: "Identité PSI",
    icon: <Globe2 size={20} />,
    color: "teal",
    assurance: "Renforcé",
    desc: "Identité Pro Santé Identité (fédération nationale)",
    idLabel: "Subject ID (PSC)",
    idPlaceholder: " rpps_1000...",
  },
];

const STATUS_COLORS = Object.assign(Object.create(null), {
  active: "bg-emerald-100 text-emerald-700",
  suspended: "bg-amber-100 text-amber-700",
  revoked: "bg-red-100 text-red-700",
  expired: "bg-slate-100 text-slate-600",
  pending_certification: "bg-blue-100 text-blue-700",
});

const STATUS_LABELS = Object.assign(Object.create(null), {
  active: "Actif",
  suspended: "Suspendu",
  revoked: "Révoqué",
  expired: "Expiré",
  pending_certification: "En attente certification",
});

const DeviceManagement = () => {
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [selectedUser, setSelectedUser] = useState(null);
  const [devices, setDevices] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);
  const [status, setStatus] = useState({ type: "", message: "" });

  const [newDevice, setNewDevice] = useState({
    deviceType: "NFC_Badge",
    deviceName: "",
    deviceIdentifier: "",
    deviceSerial: "",
    isPrimary: true,
  });
  const [searchParams] = useSearchParams();

  const { lastEvent, refreshKey, triggerRefresh } = useApp();

  const fetchStats = useCallback(async () => {
    try {
      const res = await api.get("/devices/stats");
      setStats(res.data);
    } catch {
      /* ignore */
    }
  }, []);

  const handleSearch = useCallback(async (query) => {
    setLoading(true);
    try {
      let endpoint;
      if (query) {
        endpoint = `/users/search?query=${encodeURIComponent(query)}`;
      } else {
        endpoint = `/users?page=1&pageSize=100`; // Charger les 100 premiers utilisateurs par défaut
      }
      const res = await api.get(endpoint);
      setSearchResults(res.data);
    } catch {
      /* ignore */
    } finally {
      setLoading(false);
    }
  }, []);

  const toggleUserStatus = async () => {
    if (!selectedUser) return;
    const newState = !selectedUser.isActive;
    setLoading(true);
    try {
      const identifier = selectedUser.id || selectedUser.samAccountName;
      await api.put(`/users/${identifier}`, { isActive: newState });
      setStatus({
        type: "success",
        message: `Compte agent ${newState ? "déverrouillé" : "verrouillé"} avec succès.`,
      });
      // The SignalR listener will handle updating the state automatically
    } catch (err) {
      setStatus({
        type: "error",
        message: "Erreur lors du changement de statut.",
      });
    } finally {
      setLoading(false);
    }
  };

  const selectUser = useCallback(async (user) => {
    setSelectedUser(user);
    if (user.id) {
      try {
        const res = await api.get(`/devices/user/${user.id}`);
        setDevices(res.data);
      } catch {
        setStatus({
          type: "error",
          message: "Impossible de charger les MIE de cet agent; les données existantes sont conservées.",
        });
      }
    }
  }, []);

  useEffect(() => {
    fetchStats();
  }, [refreshKey, fetchStats]);
  useEffect(() => {
    if (status.message) {
      const t = setTimeout(() => setStatus({ type: "", message: "" }), 4000);
      return () => clearTimeout(t);
    }
  }, [status]);

  // Handle direct navigation with username
  useEffect(() => {
    const username = searchParams.get("username");
    if (username) {
      const loadDirectUser = async () => {
        try {
          const res = await api.get(`/users/${username}`);
          if (res.data) {
            setSelectedUser(res.data);
            if (res.data.id) {
              const deviceRes = await api.get(`/devices/user/${res.data.id}`);
              setDevices(deviceRes.data);
            }
          }
        } catch (err) {
        }
      };
      loadDirectUser();
    }
  }, [searchParams]);

  useEffect(() => {
    const timer = setTimeout(
      () => handleSearch(searchQuery),
      searchQuery ? 400 : 0,
    );
    return () => clearTimeout(timer);
  }, [searchQuery, handleSearch]);

  const lastProcessedEventRef = useRef(null);

  useEffect(() => {
    if (
      lastEvent?.type === "OnUserStatusChanged" &&
      lastEvent.timestamp !== lastProcessedEventRef.current
    ) {
      lastProcessedEventRef.current = lastEvent.timestamp;
      const data = lastEvent.data;

      // Update the main search list
      setSearchResults((prev) =>
        prev.map((u) =>
          u.id === data.userId || u.samAccountName === data.samAccountName
            ? { ...u, isActive: data.isActive }
            : u,
        ),
      );

      // Update the selected user if it's the one affected
      setSelectedUser((prev) => {
        if (
          prev &&
          (data.userId === prev.id ||
            data.samAccountName === prev.samAccountName)
        ) {
          if (prev.isActive !== data.isActive) {
            setStatus({
              type: "info",
              message: `Mise à jour en direct: L'agent est maintenant ${data.isActive ? "ACTIF" : "INACTIF"}`,
            });
            return { ...prev, isActive: data.isActive };
          }
        }
        return prev;
      });
    }
  }, [lastEvent]);

  const registerDevice = async () => {
    if (!selectedUser?.id || !newDevice.deviceName) return;
    try {
      await api.post("/devices", { ...newDevice, userId: selectedUser.id });
      setStatus({
        type: "success",
        message: `${DEVICE_TYPES.find((t) => t.value === newDevice.deviceType)?.label} enregistré avec succès.`,
      });
      setShowAddModal(false);
      setNewDevice({
        deviceType: "NFC_Badge",
        deviceName: "",
        deviceIdentifier: "",
        deviceSerial: "",
        isPrimary: true,
      });
      selectUser(selectedUser);
      fetchStats();
      triggerRefresh();
    } catch {
      setStatus({ type: "error", message: "Erreur lors de l'enregistrement." });
    }
  };

  const updateStatus = async (deviceId, newStatus) => {
    try {
      await api.put(`/devices/${deviceId}/status`, { status: newStatus });
      setStatus({
        type: "success",
        message: `Statut mis à jour: ${STATUS_LABELS[newStatus]}`,
      });
      selectUser(selectedUser);
      fetchStats();
      triggerRefresh();
    } catch {
      setStatus({ type: "error", message: "Erreur lors de la mise à jour." });
    }
  };

  const deleteDevice = async (deviceId) => {
    if (!deviceId) {
      setStatus({ type: "error", message: "ID du dispositif manquant." });
      return;
    }

    if (!window.confirm("Supprimer définitivement ce MIE ?")) return;

    try {
      await api.delete(`/devices/${deviceId}`);
      setStatus({ type: "success", message: "MIE supprimé." });
      selectUser(selectedUser);
      fetchStats();
      triggerRefresh();
    } catch (err) {
      const errorMsg =
        err.response?.data?.message || "Erreur lors de la suppression.";
      setStatus({ type: "error", message: errorMsg });
    }
  };

  const certifyDevice = async (deviceId, isCertified) => {
    const ref = isCertified
      ? prompt("Référence de certification ANS/PSI vérifiée par l'administrateur :")
      : null;
    if (isCertified && (!ref || !ref.trim())) {
      setStatus({
        type: "error",
        message: "Une référence vérifiée par l'administrateur est requise.",
      });
      return;
    }
    try {
      await api.put(`/devices/${deviceId}/certify`, {
        isCertified,
        certificationReference: ref?.trim() ?? null,
      });
      setStatus({
        type: "success",
        message: isCertified
          ? "Attestation administrative enregistrée. La référence n'est pas vérifiée par l'API."
          : "Attestation administrative retirée.",
      });
      selectUser(selectedUser);
      triggerRefresh();
    } catch {
      /* ignore */
    }
  };

  const getTypeInfo = (type) =>
    DEVICE_TYPES.find((t) => t.value === type) || DEVICE_TYPES[0];

  return (
    <>
      <div className="max-w-7xl mx-auto space-y-8 animate-in fade-in zoom-in duration-300">
        <div className="flex justify-between items-end">
          <div>
            <h1 className="text-3xl font-bold">Gestion des MIE</h1>
            <p className="text-slate-500 mt-1">
              Moyens d'Identification Électronique — Registre PSI Compatible
            </p>
          </div>
        </div>

        {status.message && (
          <div
            className={`fixed top-6 right-6 z-50 p-4 rounded-xl border shadow-2xl flex items-center gap-3 animate-in slide-in-from-right duration-300 ${status.type === "success" ? "bg-emerald-600 text-white border-emerald-400" : "bg-red-600 text-white border-red-400"}`}
          >
            {status.type === "success" ? (
              <CheckCircle size={20} />
            ) : (
              <XCircle size={20} />
            )}
            <span className="font-semibold">{status.message}</span>
          </div>
        )}

        {/* Stats Cards */}
        {stats && (
          <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
            {(stats.byType || []).map((t, i) => {
              const info = getTypeInfo(t.type);
              return (
                <div key={i} className="glass-card !p-4 text-center">
                  <div
                    className={`w-10 h-10 mx-auto rounded-xl bg-${info.color}-500/10 text-${info.color}-500 flex items-center justify-center mb-2`}
                  >
                    {info.icon}
                  </div>
                  <div className="text-2xl font-black text-slate-900">
                    {t.activeCount}
                    <span className="text-sm text-slate-400">/{t.count}</span>
                  </div>
                  <div className="text-[10px] text-slate-500 font-bold uppercase tracking-wider">
                    {info.label}
                  </div>
                </div>
              );
            })}
            {(stats.byType || []).length === 0 && (
              <div className="col-span-6 text-center py-6 text-slate-500 text-sm">
                Aucun MIE enregistré dans le système.
              </div>
            )}
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Search Panel */}
          <div className="space-y-6">
            <div className="glass-card">
              <h3 className="text-sm font-black uppercase text-slate-500 mb-4 tracking-widest flex items-center gap-2">
                <User size={16} className="text-emerald-500" />
                Annuaire des Agents
              </h3>
              <div className="relative">
                <Search
                  className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500"
                  size={18}
                />
                <input
                  type="text"
                  placeholder="Filtrer par nom ou matricule..."
                  className="form-input w-full pl-10 h-10 bg-slate-50/50 border-slate-200"
                  value={searchQuery || ""}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
              <div className="mt-4 space-y-1 max-h-[500px] overflow-y-auto pr-2 custom-scrollbar">
                {!searchQuery && (
                  <div className="text-[10px] font-black text-slate-400 uppercase tracking-widest mb-2 ml-1">
                    Agents récents
                  </div>
                )}
                {loading && !searchResults.length && (
                  <div className="p-8 text-center">
                    <Loader2
                      size={32}
                      className="animate-spin text-emerald-500 mx-auto opacity-50"
                    />
                  </div>
                )}
                {searchResults.map((user) => (
                  <div
                    key={user.id || user.samAccountName}
                    role="button"
                    tabIndex={0}
                    onClick={() => selectUser(user)}
                    onKeyDown={(e) =>
                      (e.key === "Enter" || e.key === " ") && selectUser(user)
                    }
                    className={`w-full flex items-center justify-between p-2.5 rounded-xl transition-all border group cursor-pointer ${selectedUser?.id === user.id ? "bg-emerald-500 text-white border-emerald-600 shadow-lg shadow-emerald-500/20" : "hover:bg-slate-100 border-transparent hover:border-slate-200"}`}
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className={`w-8 h-8 rounded-lg flex items-center justify-center text-xs font-black transition-colors ${selectedUser?.id === user.id ? "bg-white/20 text-white" : "bg-slate-200 text-slate-500 group-hover:bg-emerald-100 group-hover:text-emerald-600"}`}
                      >
                        {user.displayName?.substring(0, 2) || "??"}
                      </div>
                      <div className="text-left">
                        <div className="flex items-center gap-2">
                          <div
                            className={`text-sm font-bold leading-tight ${selectedUser?.id === user.id ? "text-white" : "text-slate-900"}`}
                          >
                            {user.displayName}
                          </div>
                          <div
                            className={`w-2 h-2 rounded-full ${user.isActive ? "bg-emerald-400" : "bg-rose-400"} ${selectedUser?.id === user.id ? "border border-white/30" : ""}`}
                          />
                        </div>
                        <div
                          className={`text-[9px] uppercase font-black tracking-tighter ${selectedUser?.id === user.id ? "text-white/70" : "text-slate-400"}`}
                        >
                          {user.department || "DÉPT. INCONNU"} •{" "}
                          {user.samAccountName}
                        </div>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        const identifier = user.id || user.samAccountName;
                        const newState = !user.isActive;
                        api
                          .put(`/users/${identifier}`, { isActive: newState })
                          .then(() =>
                            setStatus({
                              type: "success",
                              message: `Compte ${user.samAccountName} ${newState ? "débloqué" : "bloqué"}`,
                            }),
                          )
                          .catch(() =>
                            setStatus({ type: "error", message: "Erreur" }),
                          );
                      }}
                      className={`p-1.5 rounded-lg transition-all ${selectedUser?.id === user.id ? "bg-white/20 text-white hover:bg-white/30" : user.isActive ? "text-slate-400 hover:bg-rose-50 hover:text-rose-500" : "bg-rose-50 text-rose-600"}`}
                    >
                      {user.isActive ? (
                        <Unlock size={14} />
                      ) : (
                        <Lock size={14} />
                      )}
                    </button>
                  </div>
                ))}
                {searchResults.length === 0 && !loading && (
                  <div className="text-center py-10">
                    <User size={40} className="mx-auto mb-2 text-slate-200" />
                    <p className="text-xs text-slate-400 font-medium">
                      Aucun agent trouvé.
                    </p>
                  </div>
                )}
              </div>
            </div>

            {/* Device Type Legend */}
            <div className="glass-card !p-4">
              <h3 className="text-[10px] font-bold uppercase text-slate-400 tracking-wider mb-3">
                Types de MIE Supportés
              </h3>
              <div className="space-y-2">
                {DEVICE_TYPES.map((dt) => (
                  <div key={dt.value} className="flex items-center gap-2">
                    <div
                      className={`w-6 h-6 rounded bg-${dt.color}-500/10 text-${dt.color}-500 flex items-center justify-center`}
                    >
                      {React.cloneElement(dt.icon, { size: 14 })}
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-700">
                        {dt.label}
                      </span>
                      <span className="text-[10px] text-slate-400 ml-2">
                        {dt.assurance}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Device Panel */}
          <div className="lg:col-span-2 space-y-6">
            {selectedUser && (
              /* User Header — shown only when a user is selected */
              <div className="glass-card p-4 border-l-4 border-emerald-500 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-11 h-11 rounded-xl bg-emerald-600/10 flex items-center justify-center font-bold text-base text-emerald-500">
                    {selectedUser.displayName?.substring(0, 2)}
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-base font-bold text-slate-900">
                        {selectedUser.displayName}
                      </h3>
                      <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-lg bg-slate-100 border border-slate-200">
                        <div
                          onClick={toggleUserStatus}
                          className={`w-7 h-3.5 rounded-full p-0.5 transition-colors cursor-pointer ${selectedUser.isActive ? "bg-emerald-500" : "bg-rose-500"}`}
                        >
                          <div
                            className={`w-2.5 h-2.5 bg-white rounded-full transition-transform ${selectedUser.isActive ? "translate-x-3.5" : "translate-x-0"}`}
                          />
                        </div>
                        <span
                          className={`text-[10px] font-black uppercase tracking-tighter ${selectedUser.isActive ? "text-emerald-600" : "text-rose-600"}`}
                        >
                          {selectedUser.isActive ? "Actif" : "Bloqué"}
                        </span>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          setSelectedUser(null);
                          setStatus({
                            type: "error",
                            message: "Impossible de charger les MIE de cet agent; les données existantes sont conservées.",
                          });
                        }}
                        className="text-[10px] font-bold text-slate-400 hover:text-slate-600 underline"
                      >
                        Voir tous
                      </button>
                    </div>
                    <p className="text-xs text-slate-500">
                      {selectedUser.department || "Sans département"} —{" "}
                      {devices.length} MIE
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setShowAddModal(true)}
                  className="btn-primary flex items-center gap-2 text-sm"
                >
                  <Plus size={16} /> Ajouter un MIE
                </button>
              </div>
            )}

            {/* Device list / Dashboard */}
            {(() => {
              const displayDevices = selectedUser ? devices : [];

              // ── No user selected: show registry summary dashboard ──
              if (!selectedUser) {
                const total = stats?.total ?? 0;
                const active = stats?.active ?? 0;
                const suspended = stats?.suspended ?? 0;
                const revoked = stats?.revoked ?? 0;
                const certified = stats?.certified ?? 0;
                const byType = stats?.byType ?? [];

                return (
                  <div className="space-y-4">
                    <div className="grid grid-cols-4 gap-3">
                      {[
                        {
                          label: "MIE Total",
                          value: total,
                          color: "slate",
                          sub: "enregistrés",
                        },
                        {
                          label: "Actifs",
                          value: active,
                          color: "emerald",
                          sub: "en service",
                        },
                        {
                          label: "Suspendus",
                          value: suspended,
                          color: "amber",
                          sub: "temporairement",
                        },
                        {
                          label: "Révoqués",
                          value: revoked,
                          color: "rose",
                          sub: "définitivement",
                        },
                      ].map((card) => (
                        <div
                          key={card.label}
                          className="glass-card !p-4 text-center"
                        >
                          <div
                            className={`text-3xl font-black text-${card.color}-600`}
                          >
                            {card.value}
                          </div>
                          <div className="text-xs font-bold text-slate-700 mt-1">
                            {card.label}
                          </div>
                          <div className="text-[10px] text-slate-400">
                            {card.sub}
                          </div>
                        </div>
                      ))}
                    </div>

                    <div className="glass-card">
                      <h3 className="text-xs font-black text-slate-500 uppercase tracking-widest mb-4">
                        Répartition par type de MIE
                      </h3>
                      <div className="space-y-3">
                        {DEVICE_TYPES.map((dt) => {
                          const stat = byType.find((b) => b.type === dt.value);
                          const count = stat?.count ?? 0;
                          const activeCount = stat?.activeCount ?? 0;
                          const pct =
                            total > 0 ? Math.round((count / total) * 100) : 0;
                          return (
                            <div
                              key={dt.value}
                              className="flex items-center gap-3"
                            >
                              <div
                                className={`shrink-0 w-8 h-8 rounded-lg bg-${dt.color}-500/10 text-${dt.color}-500 flex items-center justify-center`}
                              >
                                {React.cloneElement(dt.icon, { size: 16 })}
                              </div>
                              <div className="flex-1 min-w-0">
                                <div className="flex justify-between items-center mb-1">
                                  <span className="text-xs font-bold text-slate-700">
                                    {dt.label}
                                  </span>
                                  <span className="text-xs text-slate-500">
                                    {activeCount} actif
                                    {activeCount !== 1 ? "s" : ""} / {count}{" "}
                                    total
                                  </span>
                                </div>
                                <div className="h-1.5 bg-slate-100 rounded-full overflow-hidden">
                                  <div
                                    className={`h-full bg-${dt.color}-500 rounded-full transition-all`}
                                    style={{ width: `${pct}%` }}
                                  />
                                </div>
                              </div>
                            </div>
                          );
                        })}
                        {byType.length === 0 && (
                          <p className="text-xs text-slate-400 text-center py-4">
                            Aucun MIE enregistré dans le registre PSI.
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="glass-card !p-4 flex items-center gap-4">
                      <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-500 flex items-center justify-center shrink-0">
                        <Award size={20} />
                      </div>
                      <div className="flex-1">
                        <div className="text-sm font-bold text-slate-900">
                          {certified} MIE avec attestation administrative
                        </div>
                        <div className="text-xs text-slate-500">
                          Références saisies par un administrateur, non vérifiées
                          par l'API
                        </div>
                      </div>
                      <div className="text-2xl font-black text-blue-500">
                        {total > 0 ? Math.round((certified / total) * 100) : 0}%
                      </div>
                    </div>

                    <div className="glass-card !p-5 border border-dashed border-slate-200 flex items-center gap-4 bg-slate-50/50">
                      <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-500 flex items-center justify-center shrink-0">
                        <User size={20} />
                      </div>
                      <div>
                        <div className="text-sm font-bold text-slate-700">
                          Sélectionnez un agent
                        </div>
                        <div className="text-xs text-slate-400">
                          Cliquez sur un agent dans la liste de gauche pour
                          consulter et gérer ses MIE.
                        </div>
                      </div>
                    </div>
                  </div>
                );
              }

              // ── User selected: show their devices ──────────────────
              if (displayDevices.length === 0) {
                return (
                  <div className="glass-card py-16 text-center">
                    <ShieldAlert
                      size={48}
                      className="mx-auto text-slate-300 mb-4"
                    />
                    <h3 className="text-base font-bold text-slate-900 mb-2">
                      Aucun MIE pour cet agent
                    </h3>
                    <p className="text-slate-500 mb-4 text-sm">
                      Cet agent n'a aucun moyen d'identification électronique.
                    </p>
                    <button
                      type="button"
                      onClick={() => setShowAddModal(true)}
                      className="btn-primary"
                    >
                      Enregistrer un MIE
                    </button>
                  </div>
                );
              }

              return (
                <div className="space-y-2">
                  {displayDevices.map((device) => {
                    const typeInfo = getTypeInfo(device.deviceType);
                    return (
                      <div
                        key={device.id}
                        className="glass-card !p-0 overflow-hidden"
                      >
                        <div className="px-4 py-3 flex items-center gap-4">
                          <div
                            className={`shrink-0 w-10 h-10 rounded-xl bg-${typeInfo.color}-500/10 text-${typeInfo.color}-500 flex items-center justify-center`}
                          >
                            {typeInfo.icon}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="font-bold text-slate-900 text-sm truncate">
                                {device.deviceName}
                              </span>
                              {device.isPrimary && (
                                <span className="shrink-0 px-1.5 py-0.5 bg-emerald-100 text-emerald-700 rounded text-[9px] font-bold uppercase">
                                  Principal
                                </span>
                              )}
                              {device.isCertified && (
                                <span className="shrink-0 px-1.5 py-0.5 bg-blue-100 text-blue-700 rounded text-[9px] font-bold uppercase flex items-center gap-0.5">
                                  <Award size={9} /> PSI
                                </span>
                              )}
                              <span
                                className={`shrink-0 px-2 py-0.5 rounded-full text-[10px] font-bold ${STATUS_COLORS[device.status] || STATUS_COLORS.active}`}
                              >
                                {STATUS_LABELS[device.status] || device.status}
                              </span>
                            </div>
                            <div className="text-[11px] text-slate-400 mt-0.5 truncate">
                              <span className="font-mono">
                                {typeInfo.label}
                              </span>
                              {device.deviceIdentifier && (
                                <span className="ml-1 font-mono">
                                  · {device.deviceIdentifier}
                                </span>
                              )}
                              {device.deviceSerial && (
                                <span className="ml-1 font-mono">
                                  · S/N: {device.deviceSerial}
                                </span>
                              )}
                            </div>
                          </div>
                          <div className="flex items-center gap-1.5 shrink-0">
                            <div className="flex items-center p-1 bg-slate-100 rounded-xl border border-slate-200">
                              {device.status === "active" ? (
                                <button
                                  type="button"
                                  onClick={() =>
                                    updateStatus(device.id, "suspended")
                                  }
                                  className="flex items-center gap-1 px-2.5 py-1 bg-white text-amber-600 text-[10px] font-black uppercase tracking-widest rounded-lg shadow-sm hover:bg-amber-50 transition-all border border-amber-100"
                                  title="Suspendre"
                                >
                                  <ToggleLeft size={13} /> Suspendre
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() =>
                                    updateStatus(device.id, "active")
                                  }
                                  className="flex items-center gap-1 px-2.5 py-1 bg-emerald-600 text-white text-[10px] font-black uppercase tracking-widest rounded-lg shadow-md hover:bg-emerald-500 transition-all"
                                  title="Activer"
                                >
                                  <ToggleRight size={13} /> Activer
                                </button>
                              )}
                            </div>
                            <button
                              type="button"
                              onClick={() => updateStatus(device.id, "revoked")}
                              className="p-1.5 text-red-400 hover:bg-red-50 rounded-lg transition-colors border border-transparent hover:border-red-100"
                              title="Révoquer"
                            >
                              <ShieldAlert size={16} />
                            </button>
                            <button
                              type="button"
                              onClick={() =>
                                certifyDevice(device.id, !device.isCertified)
                              }
                              className={`p-1.5 rounded-lg transition-all border border-transparent ${device.isCertified ? "bg-blue-50 text-blue-600 border-blue-100" : "text-slate-400 hover:bg-blue-50 hover:text-blue-600 hover:border-blue-100"}`}
                              title="Attestation administrative (référence non vérifiée par l'API)"
                            >
                              <Award size={16} />
                            </button>
                            <button
                              type="button"
                              onClick={() => deleteDevice(device.id)}
                              className="p-1.5 text-slate-300 hover:bg-rose-50 hover:text-rose-500 rounded-lg transition-colors border border-transparent hover:border-rose-100"
                              title="Supprimer"
                            >
                              <Trash2 size={16} />
                            </button>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              );
            })()}
          </div>
        </div>
      </div>

      {/* Add Device Modal */}
      {showAddModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl shadow-2xl max-w-lg w-full animate-in zoom-in duration-300">
            <div className="p-6 border-b border-slate-200">
              <h2 className="text-xl font-bold text-slate-900">
                Enregistrer un nouveau MIE
              </h2>
              <p className="text-sm text-slate-500 mt-1">
                Moyen d'Identification Électronique pour{" "}
                {selectedUser?.displayName}
              </p>
            </div>
            <div className="p-6 space-y-5">
              <div className="space-y-1">
                <label className="text-xs font-bold text-slate-500 uppercase">
                  Type de dispositif
                </label>
                <div className="grid grid-cols-3 gap-2">
                  {DEVICE_TYPES.map((dt) => (
                    <button
                      key={dt.value}
                      type="button"
                      onClick={() =>
                        setNewDevice((d) => ({ ...d, deviceType: dt.value }))
                      }
                      className={`p-3 rounded-xl border-2 text-center transition-all ${newDevice.deviceType === dt.value ? `border-${dt.color}-500 bg-${dt.color}-50` : "border-slate-200 hover:border-slate-300"}`}
                    >
                      <div
                        className={`w-8 h-8 mx-auto rounded-lg bg-${dt.color}-500/10 text-${dt.color}-500 flex items-center justify-center mb-1`}
                      >
                        {dt.icon}
                      </div>
                      <div className="text-xs font-bold text-slate-700">
                        {dt.label}
                      </div>
                      <div className="text-[9px] text-slate-400">
                        {dt.assurance}
                      </div>
                    </button>
                  ))}
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1">
                  <label className="text-xs font-bold text-slate-500 uppercase">
                    Nom / Libellé
                  </label>
                  <input
                    type="text"
                    value={newDevice.deviceName || ""}
                    onChange={(e) =>
                      setNewDevice((d) => ({
                        ...d,
                        deviceName: e.target.value,
                      }))
                    }
                    className="form-input w-full"
                    placeholder="Ex: Badge principal"
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-bold text-slate-500 uppercase">
                    {getTypeInfo(newDevice.deviceType)?.idLabel ||
                      "Identifiant"}
                  </label>
                  <input
                    type="text"
                    value={newDevice.deviceIdentifier || ""}
                    onChange={(e) =>
                      setNewDevice((d) => ({
                        ...d,
                        deviceIdentifier: e.target.value,
                      }))
                    }
                    className="form-input w-full font-mono"
                    placeholder={
                      getTypeInfo(newDevice.deviceType)?.idPlaceholder ||
                      "A1B2C3D4..."
                    }
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-xs font-bold text-slate-500 uppercase">
                    N° de série
                  </label>
                  <input
                    type="text"
                    value={newDevice.deviceSerial || ""}
                    onChange={(e) =>
                      setNewDevice((d) => ({
                        ...d,
                        deviceSerial: e.target.value,
                      }))
                    }
                    className="form-input w-full font-mono"
                    placeholder="Optionnel"
                  />
                </div>
                <div className="flex items-end pb-1">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={newDevice.isPrimary}
                      onChange={(e) =>
                        setNewDevice((d) => ({
                          ...d,
                          isPrimary: e.target.checked,
                        }))
                      }
                      className="w-4 h-4 rounded border-slate-300 text-emerald-500"
                    />
                    <span className="text-sm font-medium text-slate-700">
                      Dispositif principal
                    </span>
                  </label>
                </div>
              </div>
              <div className="p-3 bg-blue-50 border border-blue-100 rounded-xl">
                <p className="text-xs text-blue-800">
                  <strong>Niveau d'assurance :</strong>{" "}
                  {DEVICE_TYPES.find((t) => t.value === newDevice.deviceType)
                    ?.assurance || "Standard"}
                  <br />
                  {
                    DEVICE_TYPES.find((t) => t.value === newDevice.deviceType)
                      ?.desc
                  }
                </p>
              </div>
            </div>
            <div className="p-6 border-t border-slate-200 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowAddModal(false)}
                className="btn-secondary"
              >
                Annuler
              </button>
              <button
                type="button"
                onClick={registerDevice}
                disabled={!newDevice.deviceName}
                className="btn-primary flex items-center gap-2 disabled:opacity-50"
              >
                <Plus size={18} /> Enregistrer
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default DeviceManagement;
