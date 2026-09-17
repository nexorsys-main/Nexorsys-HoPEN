import React, { useState, useEffect, useCallback } from "react";
import {
  Search,
  RefreshCcw,
  MoreVertical,
  CheckCircle,
  XCircle,
  Shield,
  User,
  UserPlus,
  UserCheck,
  ChevronLeft,
  ChevronRight,
  Loader2,
  Trash2,
  FileSpreadsheet,
  CreditCard,
  AlertTriangle,
  Key,
  Lock,
  Unlock,
  DownloadCloud,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useApp } from "../../AppContext";
import UserProfileModal from "../../components/UserProfileModal";
import api from "../../api";
// Global SignalR is handled in AppContext

const UserDirectory = () => {
  const [query, setQuery] = useState("");
  const [filterDept, setFilterDept] = useState("");
  const [loading, setLoading] = useState(false);
  const [syncingId, setSyncingId] = useState(null);
  const [globalSyncing, setGlobalSyncing] = useState(false);
  const [users, setUsers] = useState([]);
  const [totalUsers, setTotalUsers] = useState(0);
  const [page, setPage] = useState(1);
  const [selectedUser, setSelectedUser] = useState(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [status, setStatus] = useState({ type: "", message: "" });

  const { user: currentUser, lastEvent } = useApp();
  const navigate = useNavigate();

  const userRole = currentUser?.role?.toUpperCase() || "";
  const isSuperAdmin =
    userRole === "SUPERADMIN" ||
    userRole === "ADMIN" ||
    userRole === "ADMIN_DSI";
  const canEdit =
    isSuperAdmin || userRole === "STAFF" || userRole === "ADMIN_RH";
  const canSync = isSuperAdmin;

  const isFetchingRef = React.useRef(false);

  const fetchUsers = useCallback(
    async (useCacheBuster = false) => {
      if (isFetchingRef.current) return;
      isFetchingRef.current = true;
      setLoading(true);
      try {
        const endpoint = query || filterDept ? "/users/search" : "/users";
        const params = { query, department: filterDept, page, pageSize: 20 };

        if (useCacheBuster) {
          params._t = Date.now();
        }

        const response = await api.get(endpoint, { params });
        const data = Array.isArray(response.data)
          ? response.data
          : response.data.items || [];
        setUsers(data);
        setTotalUsers(response.data.totalCount || data.length);
      } catch (err) {
        setStatus({ type: "error", message: "Erreur de chargement" });
      } finally {
        setLoading(false);
        isFetchingRef.current = false;
      }
    },
    [query, filterDept, page],
  );

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  // Auto-refresh every 30 seconds to catch background updates (CPS/NFC assignments from kiosks)
  useEffect(() => {
    const interval = setInterval(() => {
      if (!isModalOpen) {
        fetchUsers(true);
      }
    }, 30000);
    return () => clearInterval(interval);
  }, [fetchUsers, isModalOpen]);

  useEffect(() => {
    if (status.message) {
      const timer = setTimeout(
        () => setStatus({ type: "", message: "" }),
        status.duration || 4000,
      );
      return () => clearTimeout(timer);
    }
  }, [status]);

  useEffect(() => {
    if (lastEvent?.type === "OnUserStatusChanged") {
      const data = lastEvent.data;
      setUsers((prevUsers) =>
        prevUsers.map((u) =>
          u.id === data.userId || u.samAccountName === data.samAccountName
            ? { ...u, isActive: data.isActive }
            : u,
        ),
      );
    }
  }, [lastEvent]);

  const handleSync = async (username) => {
    setSyncingId(username);
    try {
      await api.post(`/users/sync/${username}`);
      setStatus({
        type: "success",
        message: `Utilisateur ${username} synchronisé`,
      });
      fetchUsers();
    } catch (err) {
      setStatus({ type: "error", message: "Échec de la synchronisation AD" });
    } finally {
      setSyncingId(null);
    }
  };

  const handleGlobalSync = async () => {
    setGlobalSyncing(true);
    try {
      await api.post("/users/sync/all");
      setStatus({
        type: "success",
        message: "Synchronisation globale terminée",
      });
      fetchUsers();
    } catch (err) {
      setStatus({
        type: "error",
        message: "Échec de la synchronisation globale",
      });
    } finally {
      setGlobalSyncing(false);
    }
  };

  const handleDelete = async (id, name) => {
    if (
      !window.confirm(
        `Êtes-vous certain de vouloir supprimer l'utilisateur ${name} ? Cette action est irréversible.`,
      )
    )
      return;
    try {
      await api.delete(`/users/${id}`);
      setStatus({
        type: "success",
        message: `Utilisateur ${name} supprimé définitivement.`,
      });
      fetchUsers();
    } catch (err) {
      const msg =
        err.response?.data?.message || "Erreur lors de la suppression";
      setStatus({ type: "error", message: msg });
    }
  };

  const handleToggleStatus = async (user) => {
    // Defensively check both property cases from the API
    const currentActive = user.isActive ?? user.IsActive ?? false;
    const newState = !currentActive;

    /* 
    // Optimistic UI Update disabled for debugging
    setUsers(prev => prev.map(u => {
      if (u.id?.toString().toLowerCase() === user.id?.toString().toLowerCase()) {
        return { 
          ...u, 
          isActive: newState,
          IsActive: newState 
        };
      }
      return u;
    }));
    */

    try {
      // Send both cases and MINIMAL payload to avoid overwriting unrelated fields
      const payload = {
        isActive: newState,
        IsActive: newState,
      };

      const identifier = user.id || user.samAccountName;
      const response = await api.put(`/users/${identifier}`, payload);
      setStatus({
        type: "success",
        message: newState
          ? "Compte réactivé avec succès"
          : "Compte verrouillé (Accès Kiosque bloqué)",
      });

      // Final sync with cache buster to ensure UI matches DB
      // Increased delay to see if background sync is the cause
      setTimeout(() => fetchUsers(true), 3000);
    } catch (err) {
      // Rollback on error
      setUsers((prev) =>
        prev.map((u) =>
          u.id === user.id
            ? { ...u, isActive: user.isActive, IsActive: user.isActive }
            : u,
        ),
      );
      setStatus({
        type: "error",
        message: "Échec de la communication avec le serveur",
      });
    }
  };

  const handleResetPin = async (user) => {
    try {
      const response = await api.post(`/kiosk/generate-temp-pin/${user.id}`);
      const newPin = response.data.pin;
      setStatus({
        type: "success",
        message: `Nouveau PIN généré pour ${user.samAccountName} : ${newPin}. Un email a été envoyé.`,
        duration: 20000
      });
    } catch (err) {
      setStatus({
        type: "error",
        message: "Erreur lors de la génération du PIN",
      });
    }
  };

  const handleEdit = (user) => {
    setSelectedUser(user);
    setIsModalOpen(true);
  };

  const handleExport = () => {
    const csv = [
      ["Nom", "Email", "Service", "Fonction", "Statut"].join(","),
      ...users.map((u) =>
        [
          u.displayName,
          u.email,
          u.department,
          u.title,
          u.isActive ? "Actif" : "Inactif",
        ].join(","),
      ),
    ].join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.setAttribute(
      "download",
      `nexorsys_users_${new Date().toISOString().split("T")[0]}.csv`,
    );
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <>
      {status.message && (
        <div
          className={`fixed bottom-8 left-1/2 -translate-x-1/2 z-[200] px-6 py-3 rounded-xl shadow-lg flex items-center gap-3 border animate-in fade-in slide-in-from-bottom-2 duration-300 ${
            status.type === "success"
              ? "bg-emerald-50 border-emerald-200 text-emerald-800"
              : "bg-rose-50 border-rose-200 text-rose-800"
          }`}
        >
          {status.type === "success" ? (
            <CheckCircle size={18} />
          ) : (
            <AlertTriangle size={18} />
          )}
          <span className="text-sm font-bold">{status.message}</span>
        </div>
      )}

      <div className="max-w-7xl mx-auto space-y-6">
        {!isModalOpen && (
          <div className="flex flex-col lg:flex-row justify-between items-start lg:items-center gap-4 bg-white border border-slate-200 p-6 rounded-2xl shadow-sm">
            <div className="space-y-1">
              <div className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-slate-100 border border-slate-200 text-slate-500 text-[10px] font-bold uppercase tracking-widest">
                <Shield size={10} /> Administration
              </div>
              <h1 className="text-2xl font-bold text-slate-800 tracking-tight flex items-center gap-3">
                Annuaire des Utilisateurs
                <div className="flex items-center gap-1.5 px-2 py-1 rounded-md bg-emerald-50 border border-emerald-100 text-[9px] text-emerald-600 font-black tracking-tighter animate-pulse">
                  <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]"></div>
                  LIVE SYNC (30s)
                </div>
              </h1>
              <p className="text-slate-400 font-medium text-xs">
                Gestion des identités Active Directory et profils métier
              </p>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              {canSync && (
                <button
                  onClick={handleGlobalSync}
                  disabled={globalSyncing}
                  className="flex items-center gap-2 px-4 py-2 bg-slate-100 border border-slate-200 text-slate-700 text-xs font-bold rounded-lg hover:bg-slate-200 transition-all disabled:opacity-50"
                >
                  <RefreshCcw
                    size={14}
                    className={globalSyncing ? "animate-spin" : ""}
                  />
                  Synchronisation Globale
                </button>
              )}
              {canEdit && (
                <button
                  onClick={() => handleEdit(null)}
                  className="flex items-center gap-2 px-6 py-2.5 bg-emerald-600 text-white text-xs font-bold rounded-xl hover:bg-emerald-500 transition-all shadow-lg shadow-emerald-600/20 active:scale-95 animate-pulse-subtle"
                >
                  <UserPlus size={16} />
                  Ajouter un utilisateur
                </button>
              )}
              <button
                onClick={handleExport}
                className="flex items-center gap-2 px-4 py-2 bg-white border border-slate-200 text-slate-600 text-xs font-bold rounded-lg hover:bg-slate-50 transition-all"
              >
                <FileSpreadsheet size={14} />
                Exporter
              </button>
            </div>
          </div>
        )}

        {!isModalOpen && (
          <div className="bg-emerald-50 border border-emerald-100 p-5 rounded-2xl flex items-start gap-5 shadow-sm animate-in fade-in slide-in-from-top-2 duration-500">
            <div className="w-12 h-12 rounded-2xl bg-white border border-emerald-100 flex items-center justify-center shrink-0 shadow-sm">
              <UserPlus className="text-emerald-600" size={24} />
            </div>
            <div className="flex-1 space-y-3">
              <div className="space-y-1">
                <h4 className="text-sm font-bold text-emerald-900">
                  Nouveau collaborateur ?
                </h4>
                <p className="text-xs text-emerald-700 leading-relaxed font-medium">
                  Pour ajouter un utilisateur, vous pouvez soit{" "}
                  <span className="font-bold">rechercher</span> son identifiant
                  AD ci-dessous pour l'importer, soit créer un compte manuel
                  immédiatement :
                </p>
              </div>
              <button
                onClick={() => handleEdit(null)}
                className="flex items-center gap-2 px-4 py-2 bg-emerald-600 text-white text-[10px] font-black uppercase tracking-widest rounded-lg hover:bg-emerald-700 transition-all shadow-md shadow-emerald-600/20 active:scale-95"
              >
                <UserPlus size={14} />
                Créer Profil Local
              </button>
            </div>
          </div>
        )}

        {!isModalOpen && (
          <div className="bg-white p-4 rounded-2xl border border-slate-200 flex flex-col md:flex-row gap-4 items-center shadow-sm">
            <div className="relative flex-1 w-full">
              <Search
                className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
                size={16}
              />
              <input
                type="text"
                placeholder="Rechercher par nom, identifiant..."
                className="w-full pl-10 pr-4 py-2 bg-slate-50 border border-slate-100 rounded-lg text-sm outline-none focus:border-emerald-500 transition-all font-medium"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </div>
            <select
              className="px-4 py-2 bg-slate-50 border border-slate-100 rounded-lg text-sm font-medium text-slate-600 outline-none focus:border-emerald-500 w-full md:w-auto"
              value={filterDept}
              onChange={(e) => {
                setFilterDept(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tous les services</option>
              <option value="DSI">DSI</option>
              <option value="RH">RH</option>
              <option value="MED">Médecine</option>
              <option value="CHIR">Chirurgie</option>
              <option value="URG">Urgences</option>
              <option value="ACC">Accueil</option>
            </select>
          </div>
        )}

        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden min-h-[400px] relative">
          {loading && (
            <div className="absolute inset-0 bg-white/60 backdrop-blur-[2px] z-10 flex items-center justify-center">
              <Loader2 size={32} className="text-emerald-500 animate-spin" />
            </div>
          )}
          <table className="w-full text-left">
            <thead className="bg-slate-50/50 border-b border-slate-100">
              <tr>
                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                  Utilisateur
                </th>
                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                  Service / Fonction
                </th>
                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                  Statut
                </th>
                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-widest text-slate-400">
                  Source
                </th>
                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-widest text-slate-400 text-right">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-50">
              {users.length > 0
                ? users.map((u) => (
                    <tr
                      key={u.id}
                      className="hover:bg-slate-50/50 transition-colors"
                    >
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-9 h-9 rounded-lg bg-slate-100 border border-slate-200 flex items-center justify-center font-bold text-emerald-600 text-xs uppercase">
                            {(u.displayName || u.samAccountName).substring(
                              0,
                              2,
                            )}
                          </div>
                          <div>
                            <div className="text-sm font-bold text-slate-800">
                              {u.displayName || u.samAccountName}
                            </div>
                            <div className="text-[10px] text-slate-400 font-medium">
                              {u.samAccountName}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <div className="text-sm font-medium text-slate-700">
                          {u.department || "Non défini"}
                        </div>
                        <div className="text-[11px] text-slate-400">
                          {u.title || "-"}
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[10px] font-bold border ${u.isActive ? "bg-emerald-50 text-emerald-600 border-emerald-100" : "bg-rose-50 text-rose-600 border-rose-100"}`}
                        >
                          {u.isActive ? "ACTIF" : "INACTIF"}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        {u.isLocalProfile ? (
                          <div className="flex items-center gap-2">
                            <UserCheck size={14} className="text-amber-500" />
                            <span className="text-[10px] font-bold uppercase text-amber-600">
                              Local
                            </span>
                          </div>
                        ) : (
                          <div className="flex items-center gap-2">
                            <Shield size={14} className="text-blue-500" />
                            <span className="text-[10px] font-bold uppercase text-blue-600">
                              AD
                            </span>
                          </div>
                        )}
                      </td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex justify-end gap-1">
                          {canSync && !u.isLocalProfile && (
                            <button
                              onClick={() => handleSync(u.samAccountName)}
                              disabled={syncingId === u.samAccountName}
                              className="p-2 hover:bg-slate-100 rounded-lg text-slate-400 hover:text-emerald-600 transition-all"
                              title="Synchroniser depuis AD"
                            >
                              <RefreshCcw
                                size={16}
                                className={
                                  syncingId === u.samAccountName
                                    ? "animate-spin"
                                    : ""
                                }
                              />
                            </button>
                          )}

                          <button
                            onClick={() => handleToggleStatus(u)}
                            className={`p-2 rounded-lg transition-all ${!u.isActive ? "bg-rose-50 text-rose-600 shadow-inner" : "hover:bg-slate-100 text-slate-400 hover:text-emerald-600"}`}
                            title={
                              u.isActive
                                ? "Verrouiller le compte"
                                : "Déverrouiller le compte"
                            }
                          >
                            {u.isActive ? (
                              <Unlock size={16} />
                            ) : (
                              <Lock size={16} />
                            )}
                          </button>

                          <button
                            onClick={() => handleResetPin(u)}
                            className="p-2 hover:bg-amber-50 rounded-lg text-slate-400 hover:text-amber-600 transition-all"
                            title="Réinitialiser le PIN"
                          >
                            <Key size={16} />
                          </button>

                          <button
                            onClick={() => handleEdit(u)}
                            className="p-2 hover:bg-slate-100 rounded-lg text-slate-400 hover:text-slate-800 transition-all"
                            title="Modifier habilitations"
                          >
                            <MoreVertical size={16} />
                          </button>

                          {isSuperAdmin && u.samAccountName !== "admin" && (
                            <button
                              onClick={() =>
                                handleDelete(
                                  u.id,
                                  u.displayName || u.samAccountName,
                                )
                              }
                              className="p-2 hover:bg-rose-100 rounded-lg text-rose-400 hover:text-rose-600 transition-all font-bold"
                              title="Supprimer l'utilisateur"
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                : !loading && (
                    <tr>
                      <td
                        colSpan="4"
                        className="px-6 py-20 text-center text-slate-400 italic text-sm"
                      >
                        Aucun utilisateur trouvé.
                      </td>
                    </tr>
                  )}
            </tbody>
          </table>
          <div className="px-6 py-4 border-t border-slate-50 flex justify-between items-center bg-slate-50/30">
            <div className="text-xs text-slate-400 font-medium">
              Page <span className="text-slate-700 font-bold">{page}</span> sur{" "}
              {Math.ceil(totalUsers / 20) || 1}
            </div>
            <div className="flex gap-1">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-30"
              >
                <ChevronLeft size={16} />
              </button>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={users.length < 20}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-30"
              >
                <ChevronRight size={16} />
              </button>
            </div>
          </div>
        </div>
      </div>
      <UserProfileModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        user={selectedUser}
        onSave={() => fetchUsers()}
      />
    </>
  );
};

export default UserDirectory;
