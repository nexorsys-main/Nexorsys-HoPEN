import React, { lazy, Suspense, useState, useEffect } from "react";
const DashboardWorkflowChart = lazy(() => import("./DashboardWorkflowChart"));
import {
  Users,
  Clock,
  CheckCircle2,
  ChevronRight,
  UserPlus,
  Loader2,
  Database,
  Globe,
  CreditCard,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useApp } from "../../AppContext";
import api from "../../api";

const StatCard = ({ title, value, icon: Icon, color, loading }) => (
  <div className={`stat-card border-l-4 ${color}`}>
    <div className="flex justify-between w-full">
      <div className="p-2 rounded-lg bg-slate-100/50">
        <Icon className="text-emerald-500" size={24} />
      </div>
      <div className="text-xs font-bold text-slate-500 uppercase tracking-tighter">
        Stats en direct
      </div>
    </div>
    <div className="mt-4">
      {loading ? (
        <Loader2 className="animate-spin text-slate-700" size={32} />
      ) : (
        <div className="text-4xl font-bold">{value}</div>
      )}
      <div className="text-slate-600 text-sm font-medium mt-1">{title}</div>
    </div>
  </div>
);

const Dashboard = () => {
  const [stats, setStats] = useState({
    totalUsers: null,
    pendingWorkflows: null,
    totalMie: null,
  });
  const [mieDetails, setMieDetails] = useState(null);
  const [systemStatus, setSystemStatus] = useState({
    database: "loading",
    activeDirectory: "loading",
  });
  const [recentWorkflows, setRecentWorkflows] = useState([]);
  const [workflowStatusCounts, setWorkflowStatusCounts] = useState(null);
  const [dashboardError, setDashboardError] = useState(false);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();
  const { refreshKey, user: currentUser } = useApp();

  const isAdmin = ["SUPERADMIN", "ADMIN_DSI", "ADMIN_RH", "DIRECTOR"].includes(
    currentUser?.role,
  );

  useEffect(() => {
    const fetchDashboardData = async () => {
      setLoading(true);
      try {
        const results = await Promise.allSettled([
          api.get("/users/stats"),
          api.get("/workflow"),
          api.get("/health/status"),
          api.get("/devices/stats"),
        ]);

        const [userStats, workflows, health, mieStats] = results;
        setDashboardError(results.some((result) => result.status === "rejected"));

        if (userStats.status === "fulfilled") {
          setStats((current) => ({ ...current, totalUsers: userStats.value.data.totalUsers }));
        }

        if (workflows.status === "fulfilled" && Array.isArray(workflows.value.data)) {
          const workflowItems = workflows.value.data;
          setStats((current) => ({
            ...current,
            pendingWorkflows: workflowItems.filter((workflow) => workflow.status === "pending").length,
          }));
          setRecentWorkflows(workflowItems.slice(0, 4));
          setWorkflowStatusCounts({
            pending: workflowItems.filter((workflow) => workflow.status === "pending").length,
            approved: workflowItems.filter((workflow) => workflow.status === "approved").length,
            rejected: workflowItems.filter((workflow) => workflow.status === "rejected").length,
          });
        }

        if (health.status === "fulfilled") {
          const databaseStatus = health.value.data.database?.toLowerCase();
          setSystemStatus({
            database: databaseStatus === "ready" ? "connected" : databaseStatus || "unavailable",
            activeDirectory: health.value.data.activeDirectory?.toLowerCase() || "unavailable",
          });
        } else {
          setSystemStatus({ database: "unavailable", activeDirectory: "unavailable" });
        }

        if (mieStats.status === "fulfilled") {
          const deviceStats = mieStats.value.data;
          setStats((current) => ({ ...current, totalMie: deviceStats.total }));
          setMieDetails({
            active: deviceStats.active,
            suspended: deviceStats.suspended,
            revoked: deviceStats.revoked,
            certified: deviceStats.certified,
          });
        }
      } catch (err) {
        setDashboardError(true);
        setSystemStatus({ database: "unavailable", activeDirectory: "unavailable" });
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, [refreshKey]);

  return (
    <div className="max-w-7xl mx-auto space-y-8">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold">Tableau de bord IAM</h1>
          <div className="flex items-center gap-4 mt-2">
            <div className="flex items-center gap-2 px-2 py-0.5 rounded-md bg-slate-100/50 border border-slate-300/50">
              <Database
                size={12}
                className={
                  systemStatus.database === "connected"
                    ? "text-emerald-500"
                    : "text-red-500"
                }
              />
              <span className="text-[10px] font-bold uppercase tracking-wider text-slate-600">
                Base de données :
              </span>
              <span
                className={`text-[10px] font-bold uppercase ${systemStatus.database === "connected" ? "text-emerald-500" : systemStatus.database === "unavailable" || systemStatus.database === "loading" ? "text-amber-500" : "text-red-500"}`}
              >
                {systemStatus.database === "connected"
                  ? "connecté"
                  : systemStatus.database === "unavailable"
                    ? "indisponible"
                    : systemStatus.database === "loading"
                      ? "vérification"
                      : "déconnecté"}
              </span>
            </div>
            <div className="flex items-center gap-2 px-2 py-0.5 rounded-md bg-slate-100/50 border border-slate-300/50">
              <Globe
                size={12}
                className={
                  systemStatus.activeDirectory === "connected"
                    ? "text-emerald-500"
                    : systemStatus.activeDirectory === "simulated"
                      ? "text-amber-500"
                      : systemStatus.activeDirectory === "unavailable" || systemStatus.activeDirectory === "loading"
                        ? "text-amber-500"
                        : "text-red-500"
                }
              />
              <span className="text-[10px] font-bold uppercase tracking-wider text-slate-600">
                Sync AD :
              </span>
              <span
                className={`text-[10px] font-bold uppercase ${
                  systemStatus.activeDirectory === "connected"
                    ? "text-emerald-500"
                    : systemStatus.activeDirectory === "simulated"
                      ? "text-amber-500"
                      : systemStatus.activeDirectory === "unavailable" || systemStatus.activeDirectory === "loading"
                        ? "text-amber-500"
                        : "text-red-500"
                }`}
              >
                {systemStatus.activeDirectory === "connected"
                  ? "connecté"
                  : systemStatus.activeDirectory === "simulated"
                    ? "simulé"
                    : systemStatus.activeDirectory === "unavailable"
                      ? "indisponible"
                      : systemStatus.activeDirectory === "loading"
                        ? "vérification"
                        : "déconnecté"}
              </span>
            </div>
          </div>
        </div>
        <div className="flex gap-3">
          <button
            onClick={() => navigate("/audit")}
            className="btn-secondary flex items-center gap-2"
          >
            <Clock size={18} />
            Historique
          </button>
          {isAdmin && (
            <button
              onClick={() => navigate("/users")}
              className="btn-primary flex items-center gap-2"
            >
              <UserPlus size={18} />
              Nouveau Profil
            </button>
          )}
        </div>
      </div>

      {/* KPI Section */}
      {dashboardError && (
        <div role="status" className="rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
          Certaines statistiques sont indisponibles pour votre compte ou le service. Les valeurs manquantes sont indiquées par un tiret.
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <StatCard
          title="Utilisateurs"
          value={stats.totalUsers?.toLocaleString() ?? "—"}
          icon={Users}
          color="border-emerald-500"
          loading={loading}
        />
        <StatCard
          title="Dispositifs MIE"
          value={stats.totalMie ?? "—"}
          icon={CreditCard}
          color="border-blue-500"
          loading={loading}
        />
        <StatCard
          title="Demandes en attente"
          value={stats.pendingWorkflows ?? "—"}
          icon={CheckCircle2}
          color="border-amber-500"
          loading={loading}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Activity Chart */}
        <div className="lg:col-span-2 glass-card h-[400px] flex flex-col shadow-xl">
          <h3 className="text-lg font-bold mb-6">
            Répartition des workflows chargés
          </h3>
          <div className="w-full h-[300px] flex items-center justify-center">
            {loading && !workflowStatusCounts ? (
              <Loader2 className="animate-spin text-emerald-500/20" size={48} />
            ) : workflowStatusCounts ? (
              <Suspense fallback={<Loader2 className="animate-spin text-emerald-500/20" size={48} />}>
                <DashboardWorkflowChart counts={workflowStatusCounts} />
              </Suspense>
            ) : (
              <p role="status" className="text-sm text-slate-500">
                Les workflows ne sont pas accessibles ou aucune donnée n’a été chargée.
              </p>
            )}
          </div>
        </div>

        {/* Recent Workflows */}
        <div className="glass-card flex flex-col shadow-xl">
          <div className="flex justify-between items-center mb-6">
            <h3 className="text-lg font-bold">Derniers Workflows</h3>
            <button
              onClick={() => navigate("/workflows")}
              className="text-emerald-500 hover:text-emerald-400 text-sm font-bold"
            >
              Tout voir
            </button>
          </div>
          <div className="space-y-4">
            {recentWorkflows.length > 0
              ? recentWorkflows.map((wf, i) => (
                  <div
                    key={wf.id}
                    onClick={() => navigate("/workflows")}
                    className="flex items-center justify-between p-3 rounded-xl hover:bg-slate-100/50 transition-all group cursor-pointer border border-transparent hover:border-slate-200"
                  >
                    <div className="flex items-center gap-4">
                      <div className="w-10 h-10 rounded-full bg-white border border-slate-200 flex items-center justify-center font-bold text-xs uppercase text-emerald-500">
                        {wf.userId.substring(0, 2)}
                      </div>
                      <div>
                        <div className="text-sm font-bold truncate max-w-[120px]">
                          {wf.comments || "Workflow"}
                        </div>
                        <div
                          className={`text-[10px] font-bold uppercase ${
                            wf.status === "pending"
                              ? "text-amber-500"
                              : wf.status === "approved"
                                ? "text-emerald-500"
                                : "text-red-500"
                          }`}
                        >
                          {wf.status === "pending"
                            ? "en attente"
                            : wf.status === "approved"
                              ? "approuvé"
                              : "rejeté"}
                        </div>
                      </div>
                    </div>
                    <div className="text-right flex items-center gap-2">
                      <div className="text-[10px] font-bold uppercase tracking-tight text-slate-500">
                        {new Date(wf.createdAt).toLocaleDateString()}
                      </div>
                      <ChevronRight
                        size={16}
                        className="text-slate-700 group-hover:text-emerald-500 transition-colors"
                      />
                    </div>
                  </div>
                ))
              : !loading && (
                  <div className="flex flex-col items-center justify-center py-12 text-slate-500 italic text-sm">
                    Aucun workflow récent
                  </div>
                )}
            {loading && (
              <div className="py-12 flex justify-center">
                <Loader2 className="animate-spin text-emerald-500" />
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Real-time MIE Status Breakdown Section */}
      <div className="glass-card !p-0 overflow-hidden shadow-2xl border-2 border-slate-100">
        <div className="p-6 border-b border-slate-200 bg-slate-50/50 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
            <h3 className="text-lg font-bold text-slate-900 uppercase tracking-tight">
              Statut Global des MIE (Temps Réel)
            </h3>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest bg-slate-200 px-2 py-1 rounded">
              Certification enregistrée
            </span>
            <button
              onClick={() => navigate("/devices")}
              className="text-emerald-500 hover:underline text-xs font-bold"
            >
              Gérer le registre →
            </button>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-4 divide-y md:divide-y-0 md:divide-x divide-slate-200">
          <div className="p-8 text-center space-y-2">
            <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
              Actifs & Opérationnels
            </p>
            <p className="text-4xl font-black text-emerald-500">
              {mieDetails?.active ?? "—"}
            </p>
            <div className="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
              <div
                className="bg-emerald-500 h-full"
                style={{
                  width:
                    stats.totalMie > 0 && mieDetails
                      ? `${(mieDetails.active / stats.totalMie) * 100}%`
                      : "0%",
                }}
              />
            </div>
          </div>
          <div className="p-8 text-center space-y-2">
            <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
              Suspendus (Temporaire)
            </p>
            <p className="text-4xl font-black text-amber-500">
              {mieDetails?.suspended ?? "—"}
            </p>
            <div className="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
              <div
                className="bg-amber-500 h-full"
                style={{
                  width:
                    stats.totalMie > 0 && mieDetails
                      ? `${(mieDetails.suspended / stats.totalMie) * 100}%`
                      : "0%",
                }}
              />
            </div>
          </div>
          <div className="p-8 text-center space-y-2">
            <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
              Révoqués / Perdus
            </p>
            <p className="text-4xl font-black text-red-500">
              {mieDetails?.revoked ?? "—"}
            </p>
            <div className="w-full bg-slate-100 h-1.5 rounded-full overflow-hidden">
              <div
                className="bg-red-500 h-full"
                style={{
                  width:
                    stats.totalMie > 0 && mieDetails
                      ? `${(mieDetails.revoked / stats.totalMie) * 100}%`
                      : "0%",
                }}
              />
            </div>
          </div>
          <div className="p-8 text-center space-y-2 bg-blue-50/30">
            <p className="text-[10px] font-black text-blue-400 uppercase tracking-widest">
              Certifiés PSI (ANS)
            </p>
            <p className="text-4xl font-black text-blue-600">
              {mieDetails?.certified ?? "—"}
            </p>
            <div className="flex items-center justify-center gap-2 mt-2">
              <CheckCircle2 size={14} className="text-blue-500" />
              <span className="text-[10px] font-bold text-blue-500 uppercase">
                Certifiés
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
