import React from "react";
import {
  X,
  Clock,
  CheckCircle2,
  XCircle,
  Info,
  User,
  Tag,
  Calendar,
  FileJson,
} from "lucide-react";

const WorkflowDetailsModal = ({ isOpen, onClose, workflow }) => {
  if (!isOpen || !workflow) return null;

  const getStatusInfo = (status) => {
    switch (status) {
      case "pending":
        return {
          icon: <Clock className="text-amber-500" />,
          label: "En attente",
          color: "text-amber-500",
          bg: "bg-amber-500/10",
          border: "border-amber-500/20",
        };
      case "approved":
        return {
          icon: <CheckCircle2 className="text-emerald-500" />,
          label: "Approuvé",
          color: "text-emerald-500",
          bg: "bg-emerald-500/10",
          border: "border-emerald-500/20",
        };
      case "rejected":
        return {
          icon: <XCircle className="text-red-500" />,
          label: "Rejeté",
          color: "text-red-500",
          bg: "bg-red-500/10",
          border: "border-red-500/20",
        };
      default:
        return {
          icon: <Info />,
          label: status,
          color: "text-slate-600",
          bg: "bg-slate-100",
          border: "border-slate-300",
        };
    }
  };

  const statusInfo = getStatusInfo(workflow.status);

  const parsedFormData = (() => {
    if (!workflow.formData) return {};
    try {
      return typeof workflow.formData === "string"
        ? JSON.parse(workflow.formData)
        : workflow.formData;
    } catch (e) {
      return { raw: workflow.formData };
    }
  })();

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-50/80 backdrop-blur-sm">
      <div className="flex min-h-full items-center justify-center p-4 text-center">
        <div className="glass-card w-full max-w-2xl !p-0 overflow-hidden animate-in fade-in zoom-in duration-200 text-left relative">
          {/* Header */}
          <div className="px-6 py-4 border-b border-slate-200 flex justify-between items-center bg-white">
            <div className="flex items-center gap-3">
              <div
                className={`w-10 h-10 rounded-xl ${statusInfo.bg} flex items-center justify-center`}
              >
                {statusInfo.icon}
              </div>
              <div>
                <h3 className="text-lg font-bold text-slate-900">
                  Détails du Workflow
                </h3>
                <p className="text-xs text-slate-500">ID: {workflow.id}</p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-slate-100 rounded-full text-slate-500 hover:text-slate-900 transition-colors"
            >
              <X size={20} />
            </button>
          </div>

          {/* Body */}
          <div className="p-6 space-y-8">
            {/* Status & Basic Info */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-4">
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] font-bold uppercase text-slate-500 tracking-widest">
                    Type de demande
                  </span>
                  <div className="flex items-center gap-2 text-slate-800 font-bold">
                    <Tag size={16} className="text-emerald-500" />
                    {workflow.type}
                  </div>
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] font-bold uppercase text-slate-500 tracking-widest">
                    Statut Actuel
                  </span>
                  <div
                    className={`inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-bold w-fit ${statusInfo.bg} ${statusInfo.color} border ${statusInfo.border}`}
                  >
                    {statusInfo.label}
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] font-bold uppercase text-slate-500 tracking-widest">
                    Date de création
                  </span>
                  <div className="flex items-center gap-2 text-slate-700">
                    <Calendar size={16} />
                    {new Date(workflow.createdAt).toLocaleString()}
                  </div>
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-[10px] font-bold uppercase text-slate-500 tracking-widest">
                    Demandeur
                  </span>
                  <div className="flex items-center gap-2 text-slate-700">
                    <User size={16} />
                    <div className="flex flex-col">
                      <span className="font-bold">
                        {workflow.user
                          ? workflow.user.displayName
                          : `Agent ID: ${workflow.userId.substring(0, 8)}`}
                      </span>
                      {workflow.user && (
                        <span className="text-[10px] text-slate-500 font-mono">
                          Identifiant AD: {workflow.user.samAccountName}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Comments */}
            <div className="space-y-2">
              <span className="text-[10px] font-bold uppercase text-slate-500 tracking-widest">
                Description / Commentaires
              </span>
              <div className="p-4 bg-slate-100/50 rounded-xl border border-slate-300/50 text-slate-800 text-sm leading-relaxed">
                {workflow.comments || "Aucun commentaire."}
              </div>
            </div>

            {/* Form Data Details */}
            <div className="space-y-4">
              <div className="flex items-center gap-2 text-emerald-500">
                <FileJson size={18} />
                <h4 className="text-sm font-bold uppercase tracking-widest">
                  Données Structurées
                </h4>
              </div>

              <div className="grid grid-cols-1 gap-3">
                {Object.entries(parsedFormData).map(([key, value]) => (
                  <div
                    key={key}
                    className="flex flex-col p-3 bg-white/50 rounded-lg border border-slate-200"
                  >
                    <span className="text-[10px] font-bold text-slate-500 uppercase tracking-tighter mb-1">
                      {key.replace("_", " ")}
                    </span>
                    <span className="text-sm text-slate-800">
                      {typeof value === "object"
                        ? JSON.stringify(value)
                        : String(value)}
                    </span>
                  </div>
                ))}
                {Object.keys(parsedFormData).length === 0 && (
                  <p className="text-sm text-slate-500 italic">
                    Aucune donnée supplémentaire disponible.
                  </p>
                )}
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="px-6 py-4 bg-white/50 border-t border-slate-200 flex justify-end">
            <button onClick={onClose} className="btn-primary">
              Fermer
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default WorkflowDetailsModal;
