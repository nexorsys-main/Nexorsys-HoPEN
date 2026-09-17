import React, { useState, useEffect } from "react";
import {
  Monitor,
  ShieldCheck,
  ShieldAlert,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Search,
  Cpu,
  HardDrive,
  Hash,
  MapPin,
  Loader2,
  Key,
  Plus,
} from "lucide-react";
import fleetService from "../../services/fleetService";
import api from "../../api";
import { requestFailureMessage } from "../../security/authState";

export default function WindowsAuthDashboard() {
  const [fleet, setFleet] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusMsg, setStatusMsg] = useState({ type: "", message: "" });

  useEffect(() => {
    loadFleetData();
  }, []);

  const loadFleetData = async () => {
    try {
      setLoading(true);
      setStatusMsg({ type: "", message: "" });
      const data = await fleetService.getFleetWorkstations();
      setFleet(data);
    } catch (error) {
      setStatusMsg({ type: "error", message: `${requestFailureMessage(error, "parc Windows")} Aucune confiance n'a été modifiée.` });
    } finally {
      setLoading(false);
    }
  };

  const toggleTrust = async (id, currentTrust) => {
    try {
      // Assuming a PUT endpoint exists to update trust status
      await api.put(`/fleet/workstations/${id}/trust`, {
        isTrusted: !currentTrust,
      });
      setFleet(
        fleet.map((w) =>
          w.id === id ? { ...w, isTrusted: !currentTrust } : w,
        ),
      );
      setStatusMsg({
        type: "success",
        message: `Statut de confiance mis à jour.`,
      });
      setTimeout(() => setStatusMsg({ type: "", message: "" }), 3000);
    } catch (error) {
      setStatusMsg({
        type: "error",
        message: `Le serveur n'a pas confirmé le changement de confiance.`,
      });
      setTimeout(() => setStatusMsg({ type: "", message: "" }), 3000);
    }
  };

  const bootstrapLocalWorkstation = async () => {
    try {
      const response = await api.post("/workstations/enrollment/dev-bootstrap", {
        hostname: window.location.hostname === "127.0.0.1" ? "LOCALHOST" : window.location.hostname,
        department: "Local development",
      });
      setStatusMsg({ type: "success", message: `Poste local enregistré : ${response.data.hostname}` });
      await loadFleetData();
    } catch (error) {
      setStatusMsg({ type: "error", message: "Impossible d'enregistrer le poste local. Cette action est disponible uniquement en Development." });
    }
  };

  const filteredFleet = fleet.filter(
    (w) =>
      w.hostname.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (w.department &&
        w.department.toLowerCase().includes(searchQuery.toLowerCase())),
  );

  const trustedCount = fleet.filter((w) => w.isTrusted).length;
  const untrustedCount = fleet.length - trustedCount;

  return (
    <div className="max-w-7xl mx-auto space-y-8 animate-in fade-in zoom-in duration-300 pb-12">
      {/* Header */}
      <div className="flex justify-between items-end gap-6">
        <div>
          <h1 className="text-3xl font-bold text-slate-900 tracking-tight flex items-center gap-3">
            <Monitor className="text-blue-500 w-8 h-8" />
            Parc Informatique & Confiance (Deep Trust)
          </h1>
          <p className="text-slate-500 mt-1">
            Gestion des postes de travail Windows et validation cryptographique
            (Machine SID, Certificats).
          </p>
        </div>
        <button
          type="button"
          onClick={bootstrapLocalWorkstation}
          className="shrink-0 inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold uppercase tracking-wider hover:bg-emerald-700 shadow-sm"
        >
          <Plus size={16} /> Enregistrer ce poste local
        </button>
      </div>

      {statusMsg.message && (
        <div
          className={`fixed top-6 right-6 z-50 p-4 rounded-xl border shadow-2xl flex items-center gap-3 animate-in slide-in-from-right duration-300 ${statusMsg.type === "success" ? "bg-emerald-600 text-white border-emerald-400" : "bg-red-600 text-white border-red-400"}`}
        >
          {statusMsg.type === "success" ? (
            <CheckCircle size={20} />
          ) : (
            <XCircle size={20} />
          )}
          <span className="font-semibold">{statusMsg.message}</span>
        </div>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-card !p-6 flex items-center gap-4 border-l-4 border-blue-500">
          <div className="w-14 h-14 rounded-2xl bg-blue-50 text-blue-500 flex items-center justify-center shrink-0 shadow-sm">
            <Monitor size={28} />
          </div>
          <div>
            <div className="text-3xl font-black text-slate-900">
              {fleet.length}
            </div>
            <div className="text-sm font-bold text-slate-500 uppercase tracking-widest">
              Postes Enregistrés
            </div>
          </div>
        </div>

        <div className="glass-card !p-6 flex items-center gap-4 border-l-4 border-emerald-500">
          <div className="w-14 h-14 rounded-2xl bg-emerald-50 text-emerald-500 flex items-center justify-center shrink-0 shadow-sm">
            <ShieldCheck size={28} />
          </div>
          <div>
            <div className="text-3xl font-black text-slate-900">
              {trustedCount}
            </div>
            <div className="text-sm font-bold text-slate-500 uppercase tracking-widest">
              Postes Approuvés
            </div>
          </div>
        </div>

        <div className="glass-card !p-6 flex items-center gap-4 border-l-4 border-rose-500">
          <div className="w-14 h-14 rounded-2xl bg-rose-50 text-rose-500 flex items-center justify-center shrink-0 shadow-sm">
            <ShieldAlert size={28} />
          </div>
          <div>
            <div className="text-3xl font-black text-slate-900">
              {untrustedCount}
            </div>
            <div className="text-sm font-bold text-slate-500 uppercase tracking-widest">
              Postes Non Approuvés
            </div>
          </div>
        </div>
      </div>

      {/* Search and Filters */}
      <div className="flex items-center justify-between glass-card !p-3">
        <div className="relative w-full max-w-md">
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400"
            size={18}
          />
          <input
            type="text"
            placeholder="Rechercher un poste (nom, département)..."
            className="w-full bg-slate-50/50 border border-slate-200 text-slate-900 text-sm rounded-xl pl-10 pr-4 py-2.5 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all outline-none"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
      </div>

      {/* Workstations Grid */}
      {loading ? (
        <div className="py-20 text-center">
          <Loader2
            size={40}
            className="animate-spin text-blue-500 mx-auto opacity-50 mb-4"
          />
          <p className="text-slate-500 font-medium">
            Chargement des postes de travail...
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
          {filteredFleet.map((workstation) => (
            <div
              key={workstation.id}
              className={`glass-card !p-0 overflow-hidden border-2 transition-all hover:shadow-lg ${workstation.isTrusted ? "border-transparent hover:border-emerald-200" : "border-rose-200 bg-rose-50/30"}`}
            >
              <div className="p-5 flex items-start justify-between border-b border-slate-100">
                <div className="flex items-start gap-4">
                  <div
                    className={`w-12 h-12 rounded-xl flex items-center justify-center shrink-0 ${workstation.isTrusted ? "bg-emerald-100 text-emerald-600" : "bg-rose-100 text-rose-600"}`}
                  >
                    {workstation.isTrusted ? (
                      <ShieldCheck size={24} />
                    ) : (
                      <ShieldAlert size={24} />
                    )}
                  </div>
                  <div>
                    <h3 className="text-lg font-bold text-slate-900 flex items-center gap-2">
                      {workstation.hostname}
                      {!workstation.isTrusted && (
                        <span className="px-2 py-0.5 bg-rose-100 text-rose-700 rounded text-[10px] font-black uppercase tracking-widest">
                          En quarantaine
                        </span>
                      )}
                    </h3>
                    <div className="flex items-center gap-3 text-xs text-slate-500 mt-1 font-medium">
                      <span className="flex items-center gap-1.5">
                        <MapPin size={14} />{" "}
                        {workstation.department || "Non assigné"}
                      </span>
                      <span className="flex items-center gap-1.5">
                        <Cpu size={14} /> v{workstation.version}
                      </span>
                    </div>
                  </div>
                </div>

                <button
                  onClick={() =>
                    toggleTrust(workstation.id, workstation.isTrusted)
                  }
                  className={`px-4 py-2 rounded-xl text-xs font-bold uppercase tracking-widest transition-all shadow-sm ${workstation.isTrusted ? "bg-white border border-slate-200 text-rose-600 hover:bg-rose-50 hover:border-rose-200" : "bg-emerald-500 text-white hover:bg-emerald-600"}`}
                >
                  {workstation.isTrusted ? "Révoquer" : "Approuver"}
                </button>
              </div>

              <div className="p-5 bg-slate-50/50 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2 text-xs font-bold text-slate-500 uppercase tracking-widest">
                    <Hash size={14} /> Machine SID
                  </div>
                  <div className="font-mono text-xs text-slate-700 bg-white px-2 py-1 rounded border border-slate-200">
                    {workstation.machineSid || "Non détecté"}
                  </div>
                </div>

                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2 text-xs font-bold text-slate-500 uppercase tracking-widest">
                    <Key size={14} /> Empreinte Certificat
                  </div>
                  <div
                    className={`font-mono text-xs px-2 py-1 rounded border ${workstation.certificateThumbprint ? "bg-white text-slate-700 border-slate-200" : "bg-amber-50 text-amber-700 border-amber-200"}`}
                  >
                    {workstation.certificateThumbprint ||
                      "Aucun certificat client"}
                  </div>
                </div>
              </div>

              <div className="px-5 py-3 bg-slate-100/50 border-t border-slate-100 text-[11px] text-slate-400 font-medium flex justify-between">
                <span>
                  Dernière activité:{" "}
                  {new Date(workstation.lastSeen).toLocaleString("fr-FR")}
                </span>
                <span>ID: {workstation.id}</span>
              </div>
            </div>
          ))}

          {filteredFleet.length === 0 && !loading && statusMsg.type !== "error" && (
            <div className="col-span-1 xl:col-span-2 py-12 text-center text-slate-500">
              <HardDrive size={48} className="mx-auto text-slate-300 mb-4" />
              <p className="font-medium">
                Aucun poste de travail ne correspond à votre recherche.
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
