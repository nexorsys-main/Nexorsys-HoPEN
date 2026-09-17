import React from "react";
import { NavLink } from "react-router-dom";
import {
  User as UserIcon,
  LayoutDashboard,
  Users,
  GitPullRequest,
  Monitor,
  ShieldCheck,
  LogOut,
  Settings,
  HelpCircle,
  Globe,
  CreditCard,
  Activity,
  Server,
  Shield,
  ShieldAlert,
} from "lucide-react";
import { useNavigate, Link } from "react-router-dom";
import { useApp } from "../AppContext";
import api from "../api";

const Layout = ({ children }) => {
  const navigate = useNavigate();
  const { user: userData } = useApp();

  const handleLogout = () => {
    api.post("/auth/logout").catch(() => {});
    navigate("/login");
  };
  const menuItems = [
    {
      title: "Tableau de bord",
      path: "/",
      icon: <LayoutDashboard size={20} />,
    },
    { title: "Utilisateurs", path: "/users", icon: <Users size={20} /> },
    {
      title: "Workflows",
      path: "/workflows",
      icon: <GitPullRequest size={20} />,
    },
    { title: "Kiosque Hub", path: "/kiosk", icon: <Monitor size={20} /> },
    {
      title: "Journaux d'Audit",
      path: "/audit",
      icon: <ShieldCheck size={20} />,
    },
    { title: "Mon Profil", path: "/profile", icon: <UserIcon size={20} /> },
    {
      title: "Réinitialisation PIN",
      path: "/kiosk/pin-resets",
      icon: <ShieldAlert size={20} />,
    },
    { title: "Paramètres", path: "/settings", icon: <Settings size={20} /> },
    { title: "Fédération PSI", path: "/federation", icon: <Globe size={20} /> },
    { title: "Gestion MIE", path: "/devices", icon: <CreditCard size={20} /> },
    { title: "Supervision", path: "/monitoring", icon: <Activity size={20} /> },
    { title: "Flotte Windows", path: "/fleet", icon: <Server size={20} /> },
    { title: "Coffre-fort", path: "/vault", icon: <Shield size={20} /> },
    {
      title: "Politiques d'Accès",
      path: "/policies",
      icon: <Shield size={20} />,
    },
    { title: "Aide & Info", path: "/help", icon: <HelpCircle size={20} /> },
  ];

  return (
    <div className="flex h-screen bg-slate-50 text-slate-900 overflow-hidden print:h-auto print:overflow-visible print:bg-white">
      {/* Sidebar */}
      <aside className="w-64 bg-white border-r border-slate-200 flex flex-col print:hidden">
        <div className="p-6 flex flex-col items-start">
          <img
            src="/logo.svg"
            alt="NexorSys Identity logo"
            className="w-40 h-auto object-contain mb-2"
          />
          <p className="text-[11px] text-slate-500 font-medium uppercase tracking-wider mt-1">
            NexorSys Identity
          </p>
        </div>

        <nav className="flex-1 px-4 space-y-2">
          {menuItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-3 rounded-lg transition-all ${
                  isActive
                    ? "bg-emerald-600/10 text-emerald-500 border-r-2 border-emerald-500"
                    : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
                }`
              }
            >
              {item.icon}
              <span className="font-medium">{item.title}</span>
            </NavLink>
          ))}
        </nav>

        <div className="p-4 border-t border-slate-200">
          <button
            onClick={handleLogout}
            className="flex items-center gap-3 px-4 py-3 w-full text-slate-600 hover:text-red-400 transition-colors"
          >
            <LogOut size={20} />
            <span className="font-medium">Déconnexion</span>
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col overflow-hidden print:overflow-visible print:block">
        <header className="h-16 border-b border-slate-200 flex items-center justify-between px-8 bg-white/50 backdrop-blur-md print:hidden">
          <div className="text-sm font-medium text-slate-600">
            Bienvenue sur le système de gouvernance GHT
          </div>
          <Link
            to="/profile"
            className="flex items-center gap-4 hover:bg-slate-100/50 p-1.5 rounded-xl transition-colors group"
          >
            <div className="text-right">
              <div className="text-sm font-bold group-hover:text-emerald-500 transition-colors">
                {userData.displayName ||
                  userData.samAccountName ||
                  "Utilisateur"}
              </div>
              <div className="text-xs text-emerald-500">
                {userData.department || "Invité"}
              </div>
            </div>
            <div className="w-10 h-10 rounded-full bg-slate-100 border border-slate-300 flex items-center justify-center font-bold text-emerald-500 uppercase group-hover:border-emerald-500 transition-all">
              {userData.displayName?.substring(0, 2) ||
                userData.samAccountName?.substring(0, 2) ||
                "??"}
            </div>
          </Link>
        </header>

        <div className="flex-1 overflow-y-auto p-8 print:overflow-visible print:p-0">
          {children}
        </div>
      </main>
    </div>
  );
};

export default Layout;
