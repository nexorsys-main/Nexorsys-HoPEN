import React, { useState, useEffect } from "react";
import {
  ShieldCheck,
  Search,
  Filter,
  Clock,
  MapPin,
  User as UserIcon,
  Trash2,
  Monitor,
  Wrench,
} from "lucide-react";
import { useApp } from "../../AppContext";
import api from "../../api";
import { requestFailureMessage } from "../../security/authState";

export default function AuditLogs() {
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");
  const [filterType, setFilterType] = useState("");
  const [showTechnical, setShowTechnical] = useState(false);
  const [loadError, setLoadError] = useState(null);
  const { refreshKey } = useApp();

  const fetchLogs = async () => {
    try {
      setLoadError(null);
      const res = await api.get("/audit");
      if (res.data) {
        setLogs(res.data);
      }
    } catch (err) {
      setLoadError(requestFailureMessage(err, "service d'audit"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLogs();

    // Auto-refresh every 5 seconds for absolute real-time experience
    const interval = setInterval(fetchLogs, 5000);
    return () => clearInterval(interval);
  }, [refreshKey]);

  const handleClearLogs = async () => {
    if (
      !window.confirm(
        "⚠️ ATTENTION : Cette action supprimera DÉFINITIVEMENT tous les journaux d'audit du système. Cette opération est irréversible et sera consignée. Voulez-vous continuer ?",
      )
    ) {
      return;
    }

    try {
      await api.delete("/audit");
      setLogs([]);
      alert("L'historique d'audit a été vidé avec succès.");
    } catch (err) {
      alert("Erreur lors de la suppression des journaux d'audit.");
    }
  };

  const translateValue = (val) => {
    if (!val) return val;
    const mapping = Object.assign(Object.create(null), {
      "Portal Login Success": "Connexion au Portail Réussie",
      "Portal Login Failed": "Échec de Connexion au Portail",
      "Update workflow status": "Mise à jour du statut du workflow",
      Workflow: "Flux de travail",
      Auth: "Authentification",
      User: "Utilisateur",
      Utilisateur: "Utilisateur",
      Badge: "MIE / Badge",
      Profile: "Profil",
      Device: "Équipement MIE",
      Kiosk: "Kiosque Hub",
      System: "Système",
      Create: "Création",
      Update: "Mise à jour",
      Delete: "Suppression",
      "Status: pending": "Statut : En attente",
      "Status: approved": "Statut : Approuvé",
      "Status: rejected": "Statut : Rejeté",
      "Reset PIN": "Réinitialisation PIN",
      "Revoke Badge": "Révocation MIE / Badge",
      "Register Device": "Enregistrement MIE (Carte/Clé)",
      "Delete Device": "Suppression MIE",
      "Sync LDAP": "Synchronisation AD",
      "Kiosk Login Success": "Connexion Kiosque Réussie",
      "Kiosk Login Failed": "Échec du PIN Kiosque",
      "Account Locked": "Compte Verrouillé (PIN)",
      "Manual Unlock": "Déverrouillage Manuel",
      "Emergency PIN": "Génération PIN Temporaire",
      "Update Profile": "Mise à jour du profil",
      "Personal Profile Update": "Mise à jour du profil personnel",
      DÉTECTION_MATÉRIEL: "Détection Badge/Carte",
      "Kiosk Trace": "Diagnostic Kiosque",
      "Kiosque Hub Trace": "Diagnostic Kiosque",
      STATUT_MATÉRIEL: "Diagnostic Lecteur",
    });

    if (Object.prototype.hasOwnProperty.call(mapping, val) && mapping[val])
      return mapping[val];

    let translated = val;
    Object.entries(mapping).forEach(([eng, fra]) => {
      translated = translated.split(eng).join(fra);
    });

    return translated;
  };

  const cleanTraceMessage = (msg) => {
    if (!msg) return msg;

    // Fix encoding issues and strip trace brackets
    let clean = msg
      .split("Ã©")
      .join("é")
      .split("Ã")
      .join("à")
      .replace(/\[INFO\]\s+[A-Za-z0-9_-]+:\s*/g, ""); // strip legacy logger prefixes

    if (clean.includes("ATR:")) {
      const parts = clean.split("ATR:");
      return `Signature de carte CPS détectée (ATR : ${parts[1].trim()})`;
    }
    if (clean.includes("Card type:")) {
      const parts = clean.split("Card type:");
      return `Type de carte professionnelle : ${parts[1].trim().replace(/\'/g, "")}`;
    }
    if (
      clean.includes("Carte Insérée dans:") ||
      clean.includes("Carte Insérée dans :")
    ) {
      const reader = clean.includes("Carte Insérée dans:")
        ? clean.split("Carte Insérée dans:")[1]
        : clean.split("Carte Insérée dans :")[1];
      return `Carte CPS physiquement insérée dans le lecteur : ${reader.trim()}`;
    }
    if (clean.includes("NCrypt key")) {
      return `Clé cryptographique de sécurité initialisée (Smart Card Key)`;
    }
    if (clean.includes("KSP:")) {
      return `Fournisseur de sécurité cryptographique : Smart Card KSP`;
    }
    if (clean.includes("MIE / Badge")) {
      const parts = clean.split("MIE / Badge");
      return `Identifiant de Badge physique détecté : ${parts[1].trim()}`;
    }

    return translateValue(clean);
  };

  const formatDateTime = (dateStr) => {
    if (!dateStr) return "";

    // If the server string doesn't end with Z or timezone offset, append Z (as all backend dates are stored in UTC)
    let isoStr = dateStr;
    if (
      typeof isoStr === "string" &&
      !isoStr.endsWith("Z") &&
      !isoStr.includes("+") &&
      !isoStr.match(/-\d{2}:\d{2}$/)
    ) {
      isoStr = isoStr + "Z";
    }

    try {
      return new Date(isoStr).toLocaleString("fr-FR", {
        day: "2-digit",
        month: "2-digit",
        year: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      });
    } catch (e) {
      return new Date(dateStr).toLocaleString("fr-FR");
    }
  };

  const formatDetails = (details) => {
    if (!details) return null;

    if (
      details.includes(":") &&
      !details.includes("ATR:") &&
      !details.includes("Card type:")
    ) {
      return (
        <div className="space-y-1.5">
          {details.split(",").map((item, idx) => {
            const parts = item.split(":");
            const key = parts[0];
            const value = parts.slice(1).join(":");

            return (
              <div key={idx} className="flex flex-col">
                <span className="text-[9px] font-black text-slate-400 uppercase tracking-tighter mb-0.5">
                  {translateValue(key.trim())}
                </span>
                <span className="text-[11px] font-medium text-slate-700 bg-white/50 px-2 py-0.5 rounded border border-slate-200/50 inline-block w-fit break-all shadow-sm">
                  {cleanTraceMessage(value.trim())}
                </span>
              </div>
            );
          })}
        </div>
      );
    }

    return (
      <span className="font-mono text-[11px] bg-slate-100/50 px-2 py-1 rounded border border-slate-200/30 whitespace-pre-wrap leading-relaxed inline-block">
        {cleanTraceMessage(details)}
      </span>
    );
  };

  const filteredLogs = logs.filter((log) => {
    // Hide low level system diagnostics from regular users by default!
    const isTechnicalLog =
      log.action === "Kiosk Trace" ||
      log.action === "Kiosque Hub Trace" ||
      log.action === "STATUT_MATÉRIEL" ||
      log.action === "HARDWARE";

    if (!showTechnical && isTechnicalLog) {
      return false;
    }

    const translatedAction = translateValue(log.action);
    const translatedResource = translateValue(log.resourceType);
    const userName = log.user?.displayName || log.targetName || "Système";
    const userSam = log.user?.samAccountName || "";
    const computerName = log.userAgent || "";

    const matchesSearch =
      translatedAction.toLowerCase().includes(searchTerm.toLowerCase()) ||
      userName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      userSam.toLowerCase().includes(searchTerm.toLowerCase()) ||
      computerName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (translatedResource &&
        translatedResource.toLowerCase().includes(searchTerm.toLowerCase()));

    const matchesType = filterType
      ? log.action.toLowerCase().includes(filterType.toLowerCase())
      : true;
    return matchesSearch && matchesType;
  });

  return (
    <div className="space-y-6 animate-fade-in pb-8">
      {/* Header section */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight flex items-center gap-3">
            <ShieldCheck className="text-emerald-500 w-8 h-8" />
            Audit & Traçabilité (Hop'en)
          </h1>
          <p className="text-slate-600 mt-1">
            Registre inaltérable des actions et des accès au système d'identité
            en temps réel.
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => setShowTechnical(!showTechnical)}
            className={`flex items-center gap-2 px-3 py-2 border rounded-xl transition-all text-sm font-semibold shadow-sm ${
              showTechnical
                ? "bg-slate-800 border-slate-900 text-white hover:bg-slate-700"
                : "bg-white border-slate-200 text-slate-700 hover:bg-slate-50"
            }`}
            title="Afficher/Masquer les traces et diagnostics techniques du matériel"
          >
            <Wrench className="w-4 h-4" />
            Diagnostics {showTechnical ? "Actifs" : "Masqués"}
          </button>

          <button
            onClick={handleClearLogs}
            className="flex items-center gap-2 px-3 py-2 bg-rose-50 hover:bg-rose-100 border border-rose-200 text-rose-600 rounded-xl transition-all text-sm font-semibold shadow-sm"
            title="Supprimer définitivement tous les journaux d'audit du système"
          >
            <Trash2 className="w-4 h-4" />
            Vider l'historique
          </button>

          <div className="relative">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              placeholder="Rechercher (Action, PC, Agent...)"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="bg-white border border-slate-200 text-slate-900 text-sm rounded-xl pl-10 pr-4 py-2 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 transition-all w-64 shadow-sm"
            />
          </div>
          <select
            value={filterType}
            onChange={(e) => setFilterType(e.target.value)}
            className="flex items-center gap-2 px-3 py-2 bg-white border border-slate-200 text-slate-700 rounded-xl hover:border-emerald-500 transition-all text-sm focus:outline-none shadow-sm"
          >
            <option value="">Toutes les actions</option>
            <option value="Login">Connexions</option>
            <option value="Fail">Échecs & Verrouillages</option>
            <option value="Update">Modifications</option>
            <option value="Create">Créations</option>
            <option value="Delete">Suppressions</option>
            <option value="PIN">Sécurité PIN</option>
            <option value="MIE">Équipements (MIE)</option>
          </select>
        </div>
      </div>

      {/* Main Table Card */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-xl overflow-hidden backdrop-blur-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse table-fixed">
            <thead>
              <tr className="bg-slate-50/50 border-b border-slate-200 text-[10px] uppercase tracking-widest text-slate-500">
                <th className="p-4 font-bold w-[12%]">Date & Heure</th>
                <th className="p-4 font-bold w-[18%]">
                  Utilisateur Badge/Carte
                </th>
                <th className="p-4 font-bold w-[15%]">Ordinateur Client</th>
                <th className="p-4 font-bold w-[12%]">Action</th>
                <th className="p-4 font-bold w-[35%]">
                  Détails (Ancien → Nouveau)
                </th>
                <th className="p-4 font-bold w-[8%]">Source</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                <tr>
                  <td colSpan="6" className="p-12 text-center text-slate-600">
                    <div className="flex justify-center mb-4">
                      <div className="w-10 h-10 border-4 border-emerald-500 border-t-transparent rounded-full animate-spin"></div>
                    </div>
                    Récupération des données d'audit...
                  </td>
                </tr>
              ) : filteredLogs.length > 0 ? (
                filteredLogs.map((log) => {
                  const isCritical =
                    log.action.includes("Fail") ||
                    log.action.includes("Delete") ||
                    log.action.includes("Verrouillé");
                  const isSuccess =
                    log.action.includes("Success") ||
                    log.action.includes("Réussie") ||
                    log.action.includes("Create") ||
                    log.action === "DÉTECTION_MATÉRIEL";

                  return (
                    <tr
                      key={log.id}
                      className="hover:bg-slate-50/50 transition-colors text-sm group"
                    >
                      {/* Date & Heure */}
                      <td className="p-4 text-slate-600 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          <Clock className="w-3.5 h-3.5 text-slate-400" />
                          <span className="font-mono text-xs">
                            {formatDateTime(log.createdAt)}
                          </span>
                        </div>
                      </td>

                      {/* Utilisateur Badge/Carte */}
                      <td className="p-4">
                        <div className="flex items-center gap-2">
                          <div
                            className={`w-8 h-8 rounded-lg ${log.user || log.targetName ? "bg-emerald-50" : "bg-slate-100"} flex items-center justify-center`}
                          >
                            <UserIcon
                              className={`w-4 h-4 ${log.user || log.targetName ? "text-emerald-500" : "text-slate-400"}`}
                            />
                          </div>
                          <div>
                            <p className="text-slate-900 font-bold text-xs">
                              {log.targetName ||
                                log.user?.displayName ||
                                "SYSTÈME"}
                            </p>
                            <p className="text-[10px] text-slate-500 font-mono">
                              {log.user?.samAccountName || "automated-task"}
                            </p>
                          </div>
                        </div>
                      </td>

                      {/* Ordinateur Client */}
                      <td className="p-4">
                        <div className="flex items-center gap-1.5 text-slate-700 font-semibold text-xs whitespace-nowrap">
                          <Monitor className="w-3.5 h-3.5 text-slate-400" />
                          <span>{log.userAgent || "Navigateur Web"}</span>
                        </div>
                      </td>

                      {/* Action */}
                      <td className="p-4">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            isSuccess
                              ? "bg-emerald-100 text-emerald-700"
                              : isCritical
                                ? "bg-rose-100 text-rose-700"
                                : "bg-blue-100 text-blue-700"
                          }`}
                        >
                          {translateValue(log.action)}
                        </span>
                      </td>

                      {/* Détails (Ancien → Nouveau) */}
                      <td className="p-4 text-slate-600 min-w-[200px]">
                        {log.oldValues || log.newValues ? (
                          <div className="flex flex-col gap-1.5">
                            {log.oldValues && (
                              <div className="bg-rose-50/50 p-1.5 rounded border border-rose-100/50 text-[10px]">
                                <span className="text-rose-600 font-bold mr-1 italic">
                                  ANCIEN:
                                </span>
                                {formatDetails(log.oldValues)}
                              </div>
                            )}
                            {log.newValues && (
                              <div className="bg-emerald-50/50 p-1.5 rounded border border-emerald-100/50 text-[10px]">
                                <span className="text-emerald-600 font-bold mr-1 italic">
                                  NOUVEAU:
                                </span>
                                {formatDetails(log.newValues)}
                              </div>
                            )}
                          </div>
                        ) : (
                          <span className="text-slate-400 italic text-[10px]">
                            Action sans mutation de données
                          </span>
                        )}
                      </td>

                      {/* Source */}
                      <td className="p-4">
                        <div className="flex items-center gap-1.5 text-slate-400 font-mono text-[10px]">
                          <MapPin className="w-3 h-3" />
                          {log.ipAddress || "internal"}
                        </div>
                      </td>
                    </tr>
                  );
                })
              ) : loadError ? (
                <tr><td colSpan="6" className="p-20 text-center text-rose-600">{loadError}</td></tr>
              ) : (
                <tr>
                  <td colSpan="6" className="p-20 text-center">
                    <div className="text-slate-400 italic">
                      Aucun enregistrement d'audit trouvé pour ces critères.
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
