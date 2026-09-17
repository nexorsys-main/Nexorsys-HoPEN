import React, { useState, useEffect, useCallback } from "react";
import {
  GitPullRequest,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  ArrowRight,
  User,
  ShieldAlert,
  Search,
  Loader2,
} from "lucide-react";
import WorkflowDetailsModal from "../../components/WorkflowDetailsModal";
import { useApp } from "../../AppContext";
import api from "../../api";

const Workflows = () => {
  const [filter, setFilter] = useState("all");
  const [workflows, setWorkflows] = useState([]);
  const [loading, setLoading] = useState(false);
  const [updatingId, setUpdatingId] = useState(null);
  const [selectedWorkflow, setSelectedWorkflow] = useState(null);
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);
  const { refreshKey, triggerRefresh } = useApp();
  const [tempPin, setTempPin] = useState(null);
  const [pinUserId, setPinUserId] = useState(null);

  const fetchWorkflows = useCallback(async () => {
    setLoading(true);
    try {
      const response = await api.get("/workflow");
      setWorkflows(response.data);
    } catch (err) {
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchWorkflows();
  }, [fetchWorkflows, refreshKey]);

  const handleUpdateStatus = async (id, newStatus) => {
    setUpdatingId(id);
    try {
      await api.patch(`/workflow/${id}/status`, `"${newStatus}"`, {
        headers: { "Content-Type": "application/json" },
      });
      fetchWorkflows();
      triggerRefresh();
    } catch (err) {
      alert("Erreur lors de la mise à jour du statut.");
    } finally {
      setUpdatingId(null);
    }
  };

  const handleGeneratePin = async (userId) => {
    try {
      const res = await api.post(`/kiosk/generate-temp-pin/${userId}`);
      setTempPin(res.data.pin);
      setPinUserId(userId);
    } catch (err) {
      alert("Erreur lors de la génération du code PIN.");
    }
  };

  const handleViewDetails = (workflow) => {
    setSelectedWorkflow(workflow);
    setIsDetailsModalOpen(true);
  };

  const getStats = () => {
    const counts = {
      pending: workflows.filter((w) => w.status === "pending").length,
      approved: workflows.filter((w) => w.status === "approved").length,
      rejected: workflows.filter((w) => w.status === "rejected").length,
    };
    return [
      { label: "En attente", count: counts.pending, color: "text-amber-500" },
      { label: "Approuvés", count: counts.approved, color: "text-emerald-500" },
      { label: "Rejetés", count: counts.rejected, color: "text-red-500" },
    ];
  };

  return (
    <div className="max-w-7xl mx-auto space-y-8">
      {tempPin && (
        <div className="fixed top-6 right-6 z-[100] p-6 glass-card border-amber-500 shadow-2xl animate-in slide-in-from-right max-w-sm">
          <div className="flex items-start gap-4">
            <div className="p-3 bg-amber-500/20 rounded-xl">
              <ShieldAlert size={24} className="text-amber-500" />
            </div>
            <div className="flex-1">
              <h4 className="font-bold text-slate-900">Code PIN Temporaire</h4>
              <p className="text-xs text-slate-500 mt-1">
                Utilisateur: {pinUserId.substring(0, 8)}...
              </p>
              <div className="mt-4 bg-slate-100 p-4 rounded-lg text-center border border-amber-500/30">
                <span className="text-3xl font-black tracking-widest text-slate-900 font-mono">
                  {tempPin}
                </span>
              </div>
              <p className="text-[10px] text-slate-500 mt-4 italic">
                Communiquez ce code à l'utilisateur. Il devra le changer lors de
                sa première connexion.
              </p>
              <button
                onClick={() => setTempPin(null)}
                className="w-full mt-6 py-2 bg-slate-900 text-white rounded-lg font-bold text-sm"
              >
                J'ai noté le code
              </button>
            </div>
          </div>
        </div>
      )}
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold">Workflows & Gouvernance</h1>
          <p className="text-slate-500 mt-1">
            Validation des habilitations et gestion du cycle de vie des agents
          </p>
        </div>
        <div className="flex gap-6 items-center px-6 py-3 glass-card !p-3">
          {getStats().map((s, i) => (
            <div
              key={i}
              className="flex flex-col items-center px-4 border-r last:border-0 border-slate-200"
            >
              <span className={`text-xl font-bold ${s.color}`}>{s.count}</span>
              <span className="text-[10px] uppercase font-bold text-slate-500">
                {s.label}
              </span>
            </div>
          ))}
        </div>
      </div>

      <div className="flex flex-col md:flex-row gap-6">
        {/* Left Sidebar Filters */}
        <div className="w-full md:w-64 space-y-4">
          <div className="glass-card">
            <h3 className="text-sm font-bold uppercase text-slate-500 mb-4">
              Statut
            </h3>
            <div className="space-y-2">
              {["pending", "approved", "rejected", "all"].map((s) => (
                <button
                  key={s}
                  onClick={() => setFilter(s)}
                  className={`flex items-center gap-2 w-full px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                    filter === s
                      ? "bg-emerald-600/10 text-emerald-500"
                      : "text-slate-600 hover:bg-slate-100"
                  }`}
                >
                  {s === "pending" && <Clock size={16} />}
                  {s === "approved" && <CheckCircle2 size={16} />}
                  {s === "rejected" && <XCircle size={16} />}
                  {s === "all" && <GitPullRequest size={16} />}
                  <span className="capitalize">
                    {s === "pending"
                      ? "En attente"
                      : s === "approved"
                        ? "Approuvé"
                        : s === "rejected"
                          ? "Rejeté"
                          : "Tous"}
                  </span>
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Workflow List */}
        <div className="flex-1 space-y-4 relative min-h-[400px]">
          {loading && (
            <div className="absolute inset-0 bg-slate-50/20 backdrop-blur-sm z-10 flex items-center justify-center">
              <Loader2 size={40} className="text-emerald-500 animate-spin" />
            </div>
          )}

          {workflows.filter((w) => filter === "all" || w.status === filter)
            .length > 0
            ? workflows
                .filter((w) => filter === "all" || w.status === filter)
                .map((workflow) => (
                  <div
                    key={workflow.id}
                    onClick={() => handleViewDetails(workflow)}
                    className="glass-card flex flex-col md:flex-row gap-6 hover:border-slate-300 transition-colors cursor-pointer group"
                  >
                    <div className="flex-1">
                      <div className="flex items-center gap-3 mb-3">
                        <span
                          className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider ${
                            workflow.type === "HABILITATION"
                              ? "bg-blue-500/10 text-blue-500 border border-blue-500/20"
                              : workflow.type === "SECURITY"
                                ? "bg-red-500/10 text-red-500 border border-red-500/20"
                                : workflow.type === "PIN_RESET_REQUEST"
                                  ? "bg-amber-500/10 text-amber-500 border border-amber-500/20"
                                  : "bg-emerald-500/10 text-emerald-500 border border-emerald-500/20"
                          }`}
                        >
                          {workflow.type === "PIN_RESET_REQUEST"
                            ? "DEMANDE CODE PIN"
                            : workflow.type}
                        </span>
                        <span className="text-xs text-slate-500 font-medium">
                          Créé le{" "}
                          {new Date(workflow.createdAt).toLocaleDateString()}
                        </span>
                      </div>
                      <h3 className="text-lg font-bold group-hover:text-emerald-500 transition-colors flex items-center gap-2">
                        {workflow.comments || "Demande sans titre"}
                        {workflow.status === "pending" && (
                          <AlertCircle size={16} className="text-amber-500" />
                        )}
                      </h3>
                      <p className="text-sm text-slate-600 mt-2 line-clamp-2">
                        {(() => {
                          if (!workflow.formData) return "Aucun détail fourni.";
                          try {
                            const data =
                              typeof workflow.formData === "string"
                                ? JSON.parse(workflow.formData)
                                : workflow.formData;
                            return (
                              data.details ||
                              data.reason ||
                              "Détails non spécifiés."
                            );
                          } catch (e) {
                            return workflow.formData; // Fallback to raw string if parsing fails
                          }
                        })()}
                      </p>
                      <div className="flex items-center gap-6 mt-6">
                        <div className="flex items-center gap-2">
                          <div className="w-6 h-6 rounded-full bg-emerald-600/10 text-emerald-500 flex items-center justify-center text-[10px] font-bold border border-emerald-500/20 uppercase">
                            {workflow.user
                              ? workflow.user.displayName.substring(0, 2)
                              : workflow.userId.substring(0, 2)}
                          </div>
                          <div className="flex flex-col">
                            <span className="text-xs font-bold text-slate-700">
                              Demandeur :{" "}
                              {workflow.user
                                ? workflow.user.displayName
                                : `Agent ID: ${workflow.userId.substring(0, 8)}`}
                            </span>
                            {workflow.user && (
                              <span className="text-[10px] text-slate-500 font-medium font-mono">
                                Identifiant AD : {workflow.user.samAccountName}
                              </span>
                            )}
                          </div>
                        </div>
                        {workflow.assignedTo && (
                          <div className="flex items-center gap-2 text-slate-500">
                            <ArrowRight size={14} />
                            <span className="text-xs font-medium italic">
                              Assigné
                            </span>
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="flex md:flex-col justify-between items-end border-t md:border-t-0 md:border-l border-slate-200 pt-4 md:pt-0 md:pl-6 w-full md:w-48 gap-4">
                      <div
                        className={`px-3 py-1 rounded-full text-xs font-bold uppercase ${
                          workflow.status === "pending"
                            ? "bg-amber-500/10 text-amber-500 border border-amber-500/20"
                            : workflow.status === "approved"
                              ? "bg-emerald-500/10 text-emerald-500 border border-emerald-500/20"
                              : "bg-red-500/10 text-red-500 border border-red-500/20"
                        }`}
                      >
                        {workflow.status === "pending"
                          ? "En attente"
                          : workflow.status === "approved"
                            ? "Approuvé"
                            : "Rejeté"}
                      </div>

                      {workflow.status === "pending" && (
                        <div className="flex flex-col gap-2 w-full">
                          {workflow.type === "PIN_RESET_REQUEST" && (
                            <button
                              onClick={(e) => {
                                e.stopPropagation();
                                handleGeneratePin(workflow.userId);
                              }}
                              className="w-full py-1.5 text-xs font-bold bg-amber-500 hover:bg-amber-600 text-slate-900 rounded-lg flex items-center justify-center gap-2 transition-colors mb-2"
                            >
                              <ShieldAlert size={14} /> Générer Code PIN
                            </button>
                          )}
                          <button
                            onClick={() =>
                              handleUpdateStatus(workflow.id, "approved")
                            }
                            disabled={updatingId === workflow.id}
                            className="btn-primary py-1.5 text-xs flex items-center justify-center gap-2"
                          >
                            {updatingId === workflow.id ? (
                              <Loader2 size={14} className="animate-spin" />
                            ) : (
                              "Approuver"
                            )}
                          </button>
                          <button
                            onClick={() =>
                              handleUpdateStatus(workflow.id, "rejected")
                            }
                            disabled={updatingId === workflow.id}
                            className="btn-secondary py-1.5 text-xs text-red-400 hover:bg-red-500/10 border border-red-500/20 flex items-center justify-center gap-2"
                          >
                            {updatingId === workflow.id ? (
                              <Loader2 size={14} className="animate-spin" />
                            ) : (
                              "Rejeter"
                            )}
                          </button>
                        </div>
                      )}
                      {workflow.status !== "pending" && (
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            handleViewDetails(workflow);
                          }}
                          className="btn-secondary py-1.5 text-xs w-full flex items-center justify-center gap-2"
                        >
                          <Search size={14} /> Détails
                        </button>
                      )}
                    </div>
                  </div>
                ))
            : !loading && (
                <div className="glass-card h-64 flex flex-col items-center justify-center text-slate-500">
                  <GitPullRequest size={48} className="mb-4 opacity-20" />
                  <p className="font-medium italic">
                    Aucun workflow trouvé avec ce statut.
                  </p>
                </div>
              )}
        </div>
      </div>

      <WorkflowDetailsModal
        isOpen={isDetailsModalOpen}
        onClose={() => setIsDetailsModalOpen(false)}
        workflow={selectedWorkflow}
      />
    </div>
  );
};

export default Workflows;
