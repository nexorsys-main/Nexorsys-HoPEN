import React, { useState, useEffect } from "react";
import {
  Save,
  Server,
  Shield,
  Loader2,
  AlertCircle,
  CheckCircle,
  Database,
  RotateCcw,
  Monitor,
  Smartphone,
  Cpu,
  Key,
  Copy,
  HelpCircle,
  Plus,
  Trash2,
  FileText,
  Layout,
  CreditCard,
  Globe,
  Download,
  Mail,
} from "lucide-react";
import { useApp } from "../../AppContext";
import api from "../../api";

const Settings = () => {
  const [settings, setSettings] = useState({
    ldapUrl: "",
    ldapBaseDn: "",
    ldapUsername: "",
    ldapPassword: "",
    mockMode: false,
    cheminEmed: "",
    cheminHestia: "",
    cheminSigems: "",
    cheminBlueKango: "",
    managedApps: [],
    activerNfcSimulation: false,
    uidSimule: "",
    maxTentatives: 5,
    dureeBlocage: 15,
    dureeExpiration: 30,
    verrouillageAuto: true,
    apiAnsUrl: "",
    dbHost: "",
    dbPort: "5432",
    dbName: "",
    dbUsername: "",
    dbPassword: "",
    kioskApiKey: "",
    kioskUrl: "",
    kioskAdminPassword: "",
    mieNfcEnabled: false,
    mieCpsEnabled: false,
    mieFidoEnabled: false,
    mieCartePsEnabled: false,
    miePsiEnabled: false,
    // SMTP Mail Configuration
    smtpHost: "",
    smtpPort: "587",
    smtpUsername: "",
    smtpPassword: "",
    smtpEnableSsl: true,
    smtpFromEmail: "",
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dbTesting, setDbTesting] = useState(false);
  const [ldapTesting, setLdapTesting] = useState(false);
  const [status, setStatus] = useState({ type: "", message: "" });
  const [maintenanceLoading, setMaintenanceLoading] = useState({
    backup: false,
    restore: false,
    reset: false,
  });
  const [passwordModal, setPasswordModal] = useState({
    isOpen: false,
    action: null,
    password: "",
    error: "",
  });
  const [activeKiosks, setActiveKiosks] = useState([]);
  const [testingKiosks, setTestingKiosks] = useState(false);
  const [fleetConfigHash, setFleetConfigHash] = useState("");
  const [copiedField, setCopiedField] = useState(null);
  const { triggerRefresh, user } = useApp();
  const [activeTab, setActiveTab] = useState("connexion");

  useEffect(() => {
    fetchSettings();
  }, []);

  // Auto-poll active kiosks every 5 seconds when viewing the kiosk tab
  useEffect(() => {
    if (activeTab !== "kiosk") return;
    const pollKiosks = async () => {
      try {
        const response = await api.get("/fleet/workstations");
        const nodes = Array.isArray(response.data) ? response.data : [];
        setActiveKiosks(nodes);
        setFleetConfigHash("");
      } catch (err) {
        // Silent fail
      }
    };
    pollKiosks(); // Immediate first fetch
    const interval = setInterval(pollKiosks, 5000);
    return () => clearInterval(interval);
  }, [activeTab]);

  useEffect(() => {
    if (status.message) {
      const timer = setTimeout(() => {
        setStatus({ type: "", message: "" });
      }, 5000);
      return () => clearTimeout(timer);
    }
  }, [status]);

  const handleCopy = (text, fieldId) => {
    navigator.clipboard.writeText(text);
    setCopiedField(fieldId);
    setTimeout(() => setCopiedField(null), 2000);
  };

  const fetchSettings = async () => {
    try {
      const availability = await api.get("/settings/availability");
      if (!availability.data?.available) {
        setStatus({
          type: "warning",
          message: availability.data?.message || "Les paramètres globaux ne sont pas disponibles pour cette instance.",
        });
        return;
      }
      const response = await api.get("/settings");
      // Ensure no null or undefined values are passed to controlled inputs
      const sanitizedData = { ...response.data };
      Object.keys(settings).forEach((key) => {
        if (sanitizedData[key] === null || sanitizedData[key] === undefined) {
          sanitizedData[key] =
            typeof settings[key] === "boolean"
              ? false
              : Array.isArray(settings[key])
                ? []
                : "";
        }
      });

      setSettings(sanitizedData);
    } catch (err) {
      setStatus({
        type: "error",
        message: "Erreur lors du chargement des paramètres.",
      });
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setSettings((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleAppChange = (index, field, value) => {
    const newApps = [...settings.managedApps];
    newApps[index] = { ...newApps[index], [field]: value };
    setSettings((prev) => ({ ...prev, managedApps: newApps }));
  };

  const addApp = () => {
    const appIdBytes = new Uint8Array(9);
    globalThis.crypto.getRandomValues(appIdBytes);
    const newApp = {
      id: `APP_${Array.from(appIdBytes, (byte) => byte.toString(16).padStart(2, "0")).join("").toUpperCase()}`,
      name: "",
      path: "",
      icon: "Monitor",
      description: "",
    };
    setSettings((prev) => ({
      ...prev,
      managedApps: [...(prev.managedApps || []), newApp],
    }));
  };

  const removeApp = (index) => {
    const newApps = settings.managedApps.filter((_, i) => i !== index);
    setSettings((prev) => ({ ...prev, managedApps: newApps }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setStatus({ type: "", message: "" });

    try {

      await api.post("/settings", settings);

      setStatus({
        type: "success",
        message: "Paramètres enregistrés avec succès.",
      });
      triggerRefresh();
    } catch {
      setStatus({ type: "error", message: "Erreur lors de la sauvegarde." });
    } finally {
      setSaving(false);
    }
  };

  const handleBackup = () => {
    setPasswordModal({
      isOpen: true,
      action: "backup",
      password: "",
      error: "",
      file: null,
    });
  };

  const handleRestore = () => {
    setPasswordModal({
      isOpen: true,
      action: "restore",
      password: "",
      error: "",
      file: null,
    });
  };

  const handleReset = () => {
    setPasswordModal({
      isOpen: true,
      action: "reset",
      password: "",
      error: "",
      file: null,
    });
  };

  const executeSecureAction = async () => {
    if (!passwordModal.password) {
      setPasswordModal((prev) => ({
        ...prev,
        error: "Veuillez saisir votre mot de passe.",
      }));
      return;
    }

    const { action, password, file } = passwordModal;
    setMaintenanceLoading((prev) => ({ ...prev, [action]: true }));
    setStatus({ type: "", message: "" });
    setPasswordModal({
      isOpen: false,
      action: null,
      password: "",
      error: "",
      file: null,
    });

    try {
      let response;
      if (action === "restore") {
        const formData = new FormData();
        formData.append("password", password);
        if (file) formData.append("backupFile", file);

        response = await api.post("/system/restore", formData, {
          headers: { "Content-Type": "multipart/form-data" },
        });
      } else if (action === "backup") {
        response = await api.post(
          "/system/backup",
          { password },
          { responseType: "blob" },
        );

        // Trigger file download
        const url = window.URL.createObjectURL(new Blob([response.data]));
        const link = document.createElement("a");
        link.href = url;

        // Try to extract filename from content-disposition
        const disposition = response.headers["content-disposition"];
        let fileName = "backup.sql";
        if (disposition && disposition.indexOf("filename=") !== -1) {
          fileName = disposition.split("filename=")[1].replace(/["']/g, "");
        }

        link.setAttribute("download", fileName);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        setStatus({
          type: "success",
          message: "Sauvegarde téléchargée avec succès.",
        });
        return;
      } else {
        response = await api.post(`/system/${action}`, { password });
      }

      if (response.data?.success || response.data?.message) {
        setStatus({
          type: "success",
          message: response.data.message || "Opération réussie.",
        });
        if (action === "reset" || action === "restore") {
          setTimeout(() => {
            window.location.href = "/";
          }, 1500);
        }
      } else {
        setStatus({
          type: "error",
          message: `Échec de l'opération: ${response.data?.output || response.data?.message}`,
        });
      }
    } catch (err) {
      if (action === "backup" && err.response?.data instanceof Blob) {
        const errorText = await err.response.data.text();
        try {
          const errorJson = JSON.parse(errorText);
          setStatus({
            type: "error",
            message: errorJson.message || "Erreur lors de la sauvegarde.",
          });
        } catch {
          setStatus({
            type: "error",
            message: "Erreur lors de la sauvegarde.",
          });
        }
      } else {
        setStatus({
          type: "error",
          message:
            err.response?.data?.message ||
            `Erreur lors de la communication avec le serveur.`,
        });
      }
    } finally {
      setMaintenanceLoading((prev) => ({ ...prev, [action]: false }));
    }
  };

  const handleTestDatabase = async () => {
    setDbTesting(true);
    setStatus({ type: "", message: "" });
    try {
      const response = await api.post("/settings/test-db", {
        dbHost: settings.dbHost,
        dbPort: settings.dbPort,
        dbName: settings.dbName,
        dbUsername: settings.dbUsername,
        dbPassword: settings.dbPassword,
      });
      if (response.data.success) {
        setStatus({ type: "success", message: response.data.message });
      } else {
        setStatus({ type: "error", message: response.data.message });
      }
    } catch (err) {
      setStatus({
        type: "error",
        message:
          "Erreur lors de la communication avec le serveur pour tester la base de données.",
      });
    } finally {
      setDbTesting(false);
    }
  };

  const handleTestLdap = async () => {
    setLdapTesting(true);
    setStatus({ type: "", message: "" });
    try {
      const response = await api.post("/settings/test-ldap", {
        ldapUrl: settings.ldapUrl,
        ldapBaseDn: settings.ldapBaseDn,
        ldapUsername: settings.ldapUsername,
        ldapPassword: settings.ldapPassword,
      });
      if (response.data.success) {
        setStatus({ type: "success", message: response.data.message });
      } else {
        setStatus({ type: "error", message: response.data.message });
      }
    } catch (err) {
      setStatus({
        type: "error",
        message:
          err.response?.data?.message ||
          "Erreur lors de la communication avec le serveur pour tester la connexion LDAP.",
      });
    } finally {
      setLdapTesting(false);
    }
  };

  const [smtpTesting, setSmtpTesting] = useState(false);
  const handleTestSmtp = async () => {
    const targetEmail = prompt(
      "Saisissez l'adresse email de destination pour le test :",
      settings.smtpUsername || "recipient@example.com",
    );
    if (!targetEmail) return;

    setSmtpTesting(true);
    setStatus({ type: "", message: "" });
    try {
      const response = await api.post("/settings/test-email", {
        smtpHost: settings.smtpHost,
        smtpPort: settings.smtpPort,
        smtpUsername: settings.smtpUsername,
        smtpPassword: settings.smtpPassword,
        smtpEnableSsl: settings.smtpEnableSsl,
        smtpFromEmail: settings.smtpFromEmail,
        targetEmail: targetEmail,
      });
      if (response.data.success) {
        setStatus({ type: "success", message: response.data.message });
      } else {
        setStatus({ type: "error", message: response.data.message });
      }
    } catch (err) {
      setStatus({
        type: "error",
        message:
          err.response?.data?.message ||
          "Erreur lors de la communication avec le serveur pour tester la connexion SMTP.",
      });
    } finally {
      setSmtpTesting(false);
    }
  };

  const fetchActiveKiosks = async () => {
    setTestingKiosks(true);
    try {
      const response = await api.get("/fleet/workstations");
      const nodes = Array.isArray(response.data) ? response.data : [];
      setActiveKiosks(nodes);
      setFleetConfigHash("");
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la récupération des kiosques.",
      });
    } finally {
      setTestingKiosks(false);
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
    <div className="max-w-4xl mx-auto space-y-6 animate-in fade-in zoom-in duration-300">
      <div className="flex items-end justify-between">
        <div>
          <h1 className="text-3xl font-bold">Paramètres Système</h1>
          <p className="text-slate-500 mt-1">
            Configuration globale de l'application et gestion des droits
          </p>
        </div>
        <div className="flex items-center gap-2 bg-emerald-500/10 px-3 py-1 rounded-full border border-emerald-500/20">
          <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></div>
          <span className="text-[10px] font-black text-emerald-600 uppercase tracking-widest">
            Version v1.5.2-MIE
          </span>
        </div>
      </div>

      {status.message && (
        <div
          className={`fixed top-10 left-1/2 -translate-x-1/2 z-[9999] min-w-[320px] p-5 rounded-2xl border shadow-[0_20px_50px_rgba(0,0,0,0.3)] flex items-center gap-4 animate-in fade-in zoom-in slide-in-from-top-10 duration-500 ${
            status.type === "success"
              ? "bg-emerald-600 border-emerald-400 text-white"
              : status.type === "warning"
                ? "bg-amber-500 border-amber-300 text-white"
                : "bg-red-600 border-red-400 text-white"
          }`}
        >
          <div className="bg-white/20 p-2 rounded-lg">
            {status.type === "success" ? (
              <CheckCircle size={24} />
            ) : (
              <AlertCircle size={24} />
            )}
          </div>
          <div className="flex-1">
            <h4 className="font-bold text-lg leading-tight">
              {status.type === "success" ? "Succès" : status.type === "warning" ? "Information" : "Erreur"}
            </h4>
            <p className="text-sm opacity-90">{status.message}</p>
          </div>
        </div>
      )}

      {/* Tabs Navigation */}
      <div className="flex border-b border-slate-200">
        {[
          { id: "connexion", label: "Connexion", icon: <Server size={18} /> },
          { id: "smtp", label: "Messagerie (SMTP)", icon: <Mail size={18} /> },
          {
            id: "kiosk",
            label: "Kiosque & MIE",
            icon: <Smartphone size={18} />,
          },
          {
            id: "maintenance",
            label: "Maintenance",
            icon: <Database size={18} />,
          },
          {
            id: "licence",
            label: "Licence & Droits",
            icon: <CreditCard size={18} />,
          },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-6 py-4 font-bold text-sm transition-all flex items-center gap-2 border-b-2 ${
              activeTab === tab.id
                ? "text-emerald-500 border-emerald-500 bg-emerald-500/5"
                : "text-slate-500 border-transparent hover:text-slate-700 hover:bg-slate-50"
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        {/* TAB: CONNEXION */}
        {activeTab === "connexion" && (
          <div className="space-y-6 animate-in fade-in slide-in-from-left-4 duration-500">
            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-500/20 text-blue-500 rounded-lg">
                    <Server size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Active Directory (LDAP)
                    </h2>
                    <p className="text-sm text-slate-500">
                      Connectivité avec le contrôleur de domaine
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={handleTestLdap}
                  disabled={ldapTesting}
                  className="btn-secondary text-xs flex items-center gap-2"
                >
                  {ldapTesting ? (
                    <Loader2 size={14} className="animate-spin" />
                  ) : (
                    <RotateCcw size={14} />
                  )}{" "}
                  Tester
                </button>
              </div>
              <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    URL du serveur
                  </label>
                  <input
                    type="text"
                    name="ldapUrl"
                    value={settings.ldapUrl || ""}
                    onChange={handleChange}
                    className="form-input w-full"
                    placeholder="ldaps://directory.example:636"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Base DN
                  </label>
                  <input
                    type="text"
                    name="ldapBaseDn"
                    value={settings.ldapBaseDn || ""}
                    onChange={handleChange}
                    className="form-input w-full"
                    placeholder="DC=example,DC=com"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Compte de service
                  </label>
                  <input
                    type="text"
                    name="ldapUsername"
                    value={settings.ldapUsername || ""}
                    onChange={handleChange}
                    className="form-input w-full"
                    placeholder="service-account@example.com"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Mot de passe
                  </label>
                  <input
                    type="password"
                    name="ldapPassword"
                    value={settings.ldapPassword || ""}
                    onChange={handleChange}
                    className="form-input w-full"
                    placeholder="••••••••"
                  />
                </div>
              </div>
            </div>

            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-emerald-500/20 text-emerald-500 rounded-lg">
                    <Database size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Base de Données
                    </h2>
                    <p className="text-sm text-slate-500">
                      Persistance PostgreSQL
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={handleTestDatabase}
                  disabled={dbTesting}
                  className="btn-secondary text-xs flex items-center gap-2"
                >
                  {dbTesting ? (
                    <Loader2 size={14} className="animate-spin" />
                  ) : (
                    <RotateCcw size={14} />
                  )}{" "}
                  Tester
                </button>
              </div>
              <div className="p-6 grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Hôte
                  </label>
                  <input
                    type="text"
                    name="dbHost"
                    value={settings.dbHost || ""}
                    onChange={handleChange}
                    className="form-input w-full font-mono"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Port
                  </label>
                  <input
                    type="text"
                    name="dbPort"
                    value={settings.dbPort || ""}
                    onChange={handleChange}
                    className="form-input w-full font-mono"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Nom
                  </label>
                  <input
                    type="text"
                    name="dbName"
                    value={settings.dbName || ""}
                    onChange={handleChange}
                    className="form-input w-full font-mono"
                  />
                </div>
              </div>
            </div>

            <div className="flex justify-end gap-3 pt-4">
              <button
                type="submit"
                disabled={saving}
                className="btn-primary flex items-center gap-2"
              >
                {saving ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <Save size={18} />
                )}{" "}
                Enregistrer la Connexion
              </button>
            </div>
          </div>
        )}

        {/* TAB: SMTP */}
        {activeTab === "smtp" && (
          <div className="space-y-6 animate-in fade-in slide-in-from-left-4 duration-500">
            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-500/20 text-blue-500 rounded-lg">
                    <Mail size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Serveur de Messagerie (SMTP)
                    </h2>
                    <p className="text-sm text-slate-500">
                      Configuration SMTP pour l'envoi des codes PIN temporaires
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={handleTestSmtp}
                  disabled={smtpTesting}
                  className="btn-secondary text-xs flex items-center gap-2"
                >
                  {smtpTesting ? (
                    <Loader2 size={14} className="animate-spin" />
                  ) : (
                    <RotateCcw size={14} />
                  )}{" "}
                  Tester l'envoi
                </button>
              </div>
              <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Serveur SMTP / Hôte
                  </label>
                  <input
                    type="text"
                    name="smtpHost"
                    value={settings.smtpHost || ""}
                    onChange={handleChange}
                    disabled={user?.role !== 'SUPERADMIN'}
                    className="form-input w-full font-mono disabled:opacity-50"
                    placeholder="smtp.example.com"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Port SMTP
                  </label>
                  <input
                    type="text"
                    name="smtpPort"
                    value={settings.smtpPort || ""}
                    onChange={handleChange}
                    disabled={user?.role !== 'SUPERADMIN'}
                    className="form-input w-full font-mono disabled:opacity-50"
                    placeholder="587"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Nom d'utilisateur SMTP
                  </label>
                  <input
                    type="text"
                    name="smtpUsername"
                    value={settings.smtpUsername || ""}
                    onChange={handleChange}
                    disabled={user?.role !== 'SUPERADMIN'}
                    className="form-input w-full disabled:opacity-50"
                    placeholder="user@example.com"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Mot de passe SMTP
                  </label>
                  <input
                    type="password"
                    name="smtpPassword"
                    value={settings.smtpPassword || ""}
                    onChange={handleChange}
                    disabled={user?.role !== 'SUPERADMIN'}
                    className="form-input w-full disabled:opacity-50"
                    placeholder="••••••••"
                  />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-600">
                    Adresse email d'expédition (From)
                  </label>
                  <input
                    type="text"
                    name="smtpFromEmail"
                    value={settings.smtpFromEmail || ""}
                    onChange={handleChange}
                    disabled={user?.role !== 'SUPERADMIN'}
                    className="form-input w-full font-mono disabled:opacity-50"
                    placeholder="noreply@example.com"
                  />
                </div>
                <div className="space-y-2 flex flex-col justify-end">
                  <label
                    className={`flex items-center gap-3 p-4 rounded-xl border transition-colors ${user?.role !== 'SUPERADMIN' ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'} ${settings.smtpEnableSsl ? "bg-blue-50/50 border-blue-200" : "bg-slate-50 border-slate-200"}`}
                  >
                    <input
                      type="checkbox"
                      name="smtpEnableSsl"
                      checked={settings.smtpEnableSsl || false}
                      onChange={handleChange}
                      disabled={user?.role !== 'SUPERADMIN'}
                      className="w-4 h-4 accent-blue-500 rounded disabled:cursor-not-allowed"
                    />
                    <div>
                      <div className="text-sm font-bold text-slate-900">
                        Activer SSL/TLS
                      </div>
                      <div className="text-[10px] text-slate-500">
                        Sécuriser la connexion SMTP
                      </div>
                    </div>
                  </label>
                </div>
              </div>
            </div>

            <div className="flex justify-end gap-3 pt-4">
              <button
                type="submit"
                disabled={saving}
                className="btn-primary flex items-center gap-2"
              >
                {saving ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <Save size={18} />
                )}{" "}
                Enregistrer la Configuration SMTP
              </button>
            </div>
          </div>
        )}

        {/* TAB: KIOSQUE */}
        {activeTab === "kiosk" && (
          <div className="space-y-6 animate-in fade-in slide-in-from-left-4 duration-500">
            {/* ── Déploiement Kiosque ── */}
            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-500/20 text-blue-500 rounded-lg">
                    <Download size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Déploiement Kiosque
                    </h2>
                    <p className="text-sm text-slate-500">
                      Configuration réseau et téléchargement du fichier de
                      configuration
                    </p>
                  </div>
                </div>
              </div>
              <div className="p-6 space-y-5">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  <div className="space-y-2">
                    <label className="text-sm font-bold text-slate-600 flex items-center gap-2">
                      <Globe size={14} /> URL du serveur backend
                    </label>
                    <input
                      type="text"
                      name="kioskUrl"
                      value={settings.kioskUrl || ""}
                      onChange={handleChange}
                      className="form-input w-full font-mono"
                      placeholder="https://identity.example.invalid/api/"
                    />
                    <p className="text-[11px] text-slate-400">
                      Adresse du serveur NexorSys Identity
                      accessible depuis les PCs kiosques
                    </p>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-bold text-slate-600 flex items-center gap-2">
                      <Key size={14} /> Clé API kiosque
                    </label>
                    <div className="flex gap-2">
                      <input
                        type="text"
                        name="kioskApiKey"
                        value={settings.kioskApiKey || ""}
                        onChange={handleChange}
                        className="form-input flex-1 font-mono text-sm"
                        placeholder="Configured by deployment"
                      />
                      <button
                        type="button"
                        onClick={() =>
                          handleCopy(settings.kioskApiKey, "apikey")
                        }
                        className="btn-secondary text-xs px-3"
                        title="Copier"
                      >
                        {copiedField === "apikey" ? (
                          <CheckCircle size={14} className="text-emerald-500" />
                        ) : (
                          <Copy size={14} />
                        )}
                      </button>
                    </div>
                    <p className="text-[11px] text-slate-400">
                      Clé secrète partagée entre l'API et les applications
                      kiosques
                    </p>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-bold text-slate-600 flex items-center gap-2">
                      <Shield size={14} /> Mot de passe d'administration Kiosque
                    </label>
                    <input
                      type="text"
                      name="kioskAdminPassword"
                      value={settings.kioskAdminPassword || ""}
                      onChange={handleChange}
                      className="form-input w-full font-mono text-sm"
                    placeholder="Supplied by secure deployment configuration"
                    />
                    <p className="text-[11px] text-slate-400">
                      Mot de passe pour déverrouiller le menu d'administration
                      sur le terminal kiosque
                    </p>
                  </div>
                </div>

                {/* Download appsettings.json */}
                <div className="mt-4 p-4 bg-blue-50 border border-blue-100 rounded-2xl flex items-center justify-between gap-4">
                  <div>
                    <div className="font-bold text-sm text-slate-800 flex items-center gap-2">
                      <FileText size={16} className="text-blue-500" /> Générer
                      appsettings.json
                    </div>
                    <p className="text-[11px] text-slate-500 mt-1">
                      Télécharge le fichier de configuration prêt à l'emploi
                      pour l'application kiosque NFC. Placez-le dans le même
                      dossier que{" "}
                      <code className="bg-white px-1 rounded text-blue-600">
                        NexorSysKiosk.exe
                      </code>
                      .
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      const config = {
                        ParametresApplication: {
                          Api: {
                            BaseUrl: settings.kioskUrl || "",
                            ApiKey:
                              settings.kioskApiKey ||
                              "",
                          },
                          Nfc: {
                            TypeLecteur: "Auto",
                            ActiverSimulation: false,
                            UidSimule: "A1B2C3D4",
                          },
                          Applications: {
                            CheminEmed:
                              settings.cheminEmed ||
                              "",
                            CheminHestia:
                              settings.cheminHestia ||
                              "",
                            CheminSigems:
                              settings.cheminSigems ||
                              "",
                            CheminBlueKango:
                              settings.cheminBlueKango ||
                              "",
                          },
                          Securite: {
                            MaxTentatives:
                              parseInt(settings.maxTentatives) || 3,
                            DureeBlocageMinutes:
                              parseInt(settings.dureeBlocage) || 5,
                            DureeExpirationSessionMinutes:
                              parseInt(settings.dureeExpiration) || 15,
                            VerrouillageAuto: true,
                            LancementAutomatique: false,
                          },
                          MotDePasseAdmin:
                            settings.kioskAdminPassword || "",
                        },
                      };
                      const jsonStr = JSON.stringify(config, null, 2);
                      const blob = new Blob([jsonStr], {
                        type: "application/json",
                      });
                      const url = window.URL.createObjectURL(blob);
                      const a = document.createElement("a");
                      a.style.display = "none";
                      a.href = url;
                      a.download = "appsettings.json";
                      document.body.appendChild(a);
                      a.click();

                      // Small delay to ensure browser captures the download request
                      setTimeout(() => {
                        window.URL.revokeObjectURL(url);
                        if (document.body.contains(a)) {
                          document.body.removeChild(a);
                        }
                      }, 200);

                      setStatus({
                        type: "success",
                        message:
                          "appsettings.json généré et téléchargé avec succès.",
                      });
                    }}
                    className="btn-primary flex items-center gap-2 whitespace-nowrap"
                  >
                    <Download size={16} /> Télécharger appsettings.json
                  </button>
                </div>
              </div>
            </div>

            {/* ── Configuration des MIE ── */}
            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-indigo-500/20 text-indigo-500 rounded-lg">
                    <Smartphone size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Paramétrage des MIE (Moyens d'Identification)
                    </h2>
                    <p className="text-sm text-slate-500">
                      Activer ou désactiver globalement les modalités
                      d'authentification
                    </p>
                  </div>
                </div>
              </div>
              <div className="p-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {[
                  {
                    id: "mieNfcEnabled",
                    label: "Badge NFC (Sans contact)",
                    desc: "Lecture RFID Mifare/Desfire",
                  },
                  {
                    id: "mieCpsEnabled",
                    label: "Carte CPS",
                    desc: "Carte physique (Puce contact)",
                  },
                  {
                    id: "mieFidoEnabled",
                    label: "Clé USB FIDO2 / YubiKey",
                    desc: "Authentification matérielle USB",
                  },
                  {
                    id: "mieCartePsEnabled",
                    label: "Carte PSÉ",
                    desc: "Carte Professionnel Santé Électronique",
                  },
                  {
                    id: "miePsiEnabled",
                    label: "Identité PSI",
                    desc: "Conformité Pro Santé Identité",
                  },
                ].map((mie) => (
                  <label
                    key={mie.id}
                    className="flex items-start gap-3 p-4 bg-slate-50 border border-slate-200 rounded-xl cursor-pointer hover:border-indigo-300 hover:bg-indigo-50/50 transition-colors"
                  >
                    <input
                      type="checkbox"
                      name={mie.id}
                      checked={settings[mie.id]}
                      onChange={handleChange}
                      className="mt-1 w-4 h-4 accent-indigo-500 rounded border-slate-300 bg-white cursor-pointer"
                    />
                    <div>
                      <div className="text-sm font-bold text-slate-900">
                        {mie.label}
                      </div>
                      <div className="text-[10px] text-slate-500">
                        {mie.desc}
                      </div>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            {/* ── Parc Matériel Actif & Auto-Config ── */}
            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-emerald-500/20 text-emerald-500 rounded-lg">
                    <Monitor size={20} />
                  </div>
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Gestion de Parc & Configuration Automatique
                    </h2>
                    <p className="text-sm text-slate-500">
                      Kiosques en ligne et synchronisation des paramètres
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={fetchActiveKiosks}
                  disabled={testingKiosks}
                  className="btn-secondary text-xs flex items-center gap-2"
                >
                  {testingKiosks ? (
                    <Loader2 size={14} className="animate-spin" />
                  ) : (
                    <RotateCcw size={14} />
                  )}{" "}
                  Actualiser
                </button>
              </div>

              <div className="px-6 py-4 bg-slate-50/80 border-b border-slate-200 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <Server size={18} className="text-blue-500" />
                  <div>
                    <p className="text-xs font-bold text-slate-700">
                      Configuration Centrale (Serveur)
                    </p>
                    <p className="text-[10px] text-slate-500 font-mono">
                      HASH: {fleetConfigHash || "Non généré"}
                    </p>
                  </div>
                </div>
                <div className="text-xs text-slate-500">
                  Les kiosques mettent automatiquement à jour leur config
                  lorsqu'ils détectent un nouveau hash.
                </div>
              </div>

              <div className="p-6">
                <div className="space-y-3">
                  {activeKiosks.length === 0 ? (
                    <div className="p-8 text-center border-2 border-dashed border-slate-100 rounded-2xl">
                      <p className="text-slate-400 text-sm italic">
                        Aucun kiosque connecté sur les 5 dernières minutes.
                      </p>
                    </div>
                  ) : (
                    activeKiosks.map((kiosk, idx) => {
                      const isSynced =
                        kiosk.currentConfigHash === fleetConfigHash;

                      return (
                        <div
                          key={idx}
                          className="flex items-center justify-between p-4 bg-white border border-slate-200 rounded-2xl shadow-sm"
                        >
                          <div className="flex items-center gap-4">
                            <div
                              className={`p-2 rounded-xl bg-emerald-500/10 text-emerald-500`}
                            >
                              <Monitor size={20} />
                            </div>
                            <div>
                              <div className="font-bold text-slate-900">
                                {kiosk.hostname}
                              </div>
                              <div className="text-[10px] text-slate-500 uppercase font-black tracking-widest flex items-center gap-2">
                                <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />{" "}
                                EN LIGNE • {kiosk.ipAddress}
                              </div>
                            </div>
                          </div>
                          <div className="flex items-center gap-3">
                            <div
                              className={`flex items-center gap-2 px-3 py-1.5 rounded-full text-[10px] font-black border transition-all ${
                                isSynced
                                  ? "bg-blue-50 text-blue-600 border-blue-100"
                                  : "bg-amber-50 text-amber-600 border-amber-100"
                              }`}
                            >
                              <Cpu size={12} />
                              {isSynced
                                ? "SYNCHRONISÉ"
                                : "MISE À JOUR PENDANTE..."}
                            </div>
                            <div className="text-[10px] font-bold text-slate-400">
                              v{kiosk.appVersion} • VU :{" "}
                              {new Date(kiosk.lastSeen).toLocaleTimeString()}
                            </div>
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
              {/* ── Aperçu de la Configuration à Déployer ── */}
              <div className="p-6 border-t border-slate-200 bg-slate-50">
                <h3 className="text-sm font-bold text-slate-700 mb-3 flex items-center gap-2">
                  <FileText size={16} className="text-slate-400" /> Contenu de
                  la mise à jour (JSON)
                </h3>
                <pre className="bg-slate-900 text-slate-300 p-4 rounded-xl text-[11px] font-mono overflow-auto max-h-60 border border-slate-700 shadow-inner">
                  {JSON.stringify(
                    {
                      ParametresApplication: {
                        Api: {
                          BaseUrl: settings.kioskUrl || "",
                          ApiKey:
                            settings.kioskApiKey ||
                            "",
                        },
                        Nfc: {
                          TypeLecteur: "Auto",
                          ActiverSimulation: false,
                          UidSimule: "A1B2C3D4",
                        },
                        Applications: {
                          CheminEmed:
                            settings.cheminEmed ||
                            "",
                          CheminHestia:
                            settings.cheminHestia ||
                            "C:\\Program Files\\Hestia\\Hestia.exe",
                          CheminSigems:
                            settings.cheminSigems ||
                            "C:\\Program Files\\SIGEMS\\SIGEMS.exe",
                          CheminBlueKango:
                            settings.cheminBlueKango ||
                            "",
                        },
                        Securite: {
                          MaxTentatives: parseInt(settings.maxTentatives) || 3,
                          DureeBlocageMinutes:
                            parseInt(settings.dureeBlocage) || 5,
                          DureeExpirationSessionMinutes:
                            parseInt(settings.dureeExpiration) || 15,
                          VerrouillageAuto: true,
                          LancementAutomatique: false,
                        },
                        MotDePasseAdmin:
                          settings.kioskAdminPassword || "",
                      },
                    },
                    null,
                    2,
                  )}
                </pre>
                <p className="text-xs text-slate-500 mt-3 italic">
                  Ces paramètres sont construits à partir des différents onglets
                  (Connexion, Sécurité, Kiosque). Toute sauvegarde déploiera ce
                  contenu sur les postes.
                </p>
              </div>
            </div>

            <div className="flex justify-end pt-4">
              <button
                type="submit"
                disabled={saving}
                className="btn-primary flex items-center gap-2 px-6 py-3 bg-emerald-600 hover:bg-emerald-700 text-white shadow-lg shadow-emerald-500/30"
              >
                {saving ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <Server size={18} />
                )}{" "}
                Enregistrer & Déployer vers les Kiosques
              </button>
            </div>
          </div>
        )}

        {/* TAB: MAINTENANCE */}
        {activeTab === "maintenance" && (
          <div className="space-y-6 animate-in fade-in slide-in-from-left-4 duration-500">
            <div className="grid md:grid-cols-3 gap-6">
              <div className="p-4 bg-slate-100/50 rounded-xl border border-slate-200">
                <h4 className="text-xs font-bold uppercase text-slate-500 mb-4">
                  Sauvegarde & Export
                </h4>
                <div className="flex flex-col gap-2">
                  <button
                    type="button"
                    onClick={handleBackup}
                    disabled={maintenanceLoading.backup}
                    className="btn-primary w-full flex items-center justify-center gap-2 bg-slate-100 hover:bg-slate-200 text-slate-900 border-slate-300"
                  >
                    {maintenanceLoading.backup ? (
                      <Loader2 size={18} className="animate-spin" />
                    ) : (
                      <Download size={18} />
                    )}
                    Sauvegarde
                  </button>
                  <p className="text-[10px] text-slate-500 italic text-center">
                    Exportation complète de la base et config.
                  </p>
                </div>
              </div>

              <div className="p-4 bg-slate-100/50 rounded-xl border border-slate-200">
                <h4 className="text-xs font-bold uppercase text-slate-500 mb-4">
                  Restauration Personnalisée
                </h4>
                <div className="flex flex-col gap-2">
                  <button
                    type="button"
                    onClick={handleRestore}
                    disabled={maintenanceLoading.restore}
                    className="btn-primary w-full flex items-center justify-center gap-2 bg-white hover:bg-slate-50 text-slate-700 border-slate-300"
                  >
                    {maintenanceLoading.restore ? (
                      <Loader2 size={18} className="animate-spin" />
                    ) : (
                      <RotateCcw size={18} />
                    )}
                    Restaurer
                  </button>
                  <p className="text-[10px] text-slate-500 italic text-center">
                    Remise au dernier point de contrôle.
                  </p>
                </div>
              </div>

              <div className="p-4 bg-red-500/5 rounded-xl border border-red-500/10">
                <h4 className="text-xs font-bold uppercase text-red-500 mb-4">
                  Remise à Zéro
                </h4>
                <div className="flex flex-col gap-2">
                  <button
                    type="button"
                    onClick={handleReset}
                    disabled={maintenanceLoading.reset}
                    className="btn-primary w-full flex items-center justify-center gap-2 bg-red-600 hover:bg-red-700 text-white border-red-500"
                  >
                    {maintenanceLoading.reset ? (
                      <Loader2 size={18} className="animate-spin" />
                    ) : (
                      <Trash2 size={18} />
                    )}
                    Remise à Zéro
                  </button>
                  <p className="text-[10px] text-red-500/70 italic text-center">
                    Efface toutes les données (utilisateurs, logs) sauf Super
                    Admin.
                  </p>
                </div>
              </div>
            </div>

            <div className="glass-card !p-0 overflow-hidden">
              <div className="p-6 border-b border-slate-200 flex items-center justify-between bg-white/50">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-500/20 text-blue-500 rounded-lg">
                    <Monitor size={20} />
                  </div>
                  <h2 className="text-lg font-bold text-slate-900">
                    Orchestration des Logiciels
                  </h2>
                </div>
                <button
                  type="button"
                  onClick={addApp}
                  className="btn-primary text-xs py-1 px-3"
                >
                  + Ajouter
                </button>
              </div>
              <div className="p-6 space-y-3">
                {(settings.managedApps || []).map((app, index) => (
                  <div
                    key={index}
                    className="flex gap-4 items-center p-3 bg-slate-50 border border-slate-200 rounded-xl group"
                  >
                    <input
                      type="text"
                      value={app.name}
                      onChange={(e) =>
                        handleAppChange(index, "name", e.target.value)
                      }
                      className="form-input flex-1 py-1 text-sm font-bold"
                      placeholder="Nom"
                    />
                    <input
                      type="text"
                      value={app.path}
                      onChange={(e) =>
                        handleAppChange(index, "path", e.target.value)
                      }
                      className="form-input flex-[2] py-1 text-sm font-mono"
                      placeholder="Chemin/URL"
                    />
                    <button
                      type="button"
                      onClick={() => removeApp(index)}
                      className="p-1.5 text-slate-400 hover:text-red-500"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                ))}
              </div>
            </div>

            <div className="flex justify-end pt-4">
              <button
                type="submit"
                disabled={saving}
                className="btn-primary flex items-center gap-2"
              >
                {saving ? (
                  <Loader2 size={18} className="animate-spin" />
                ) : (
                  <Save size={18} />
                )}{" "}
                Enregistrer Orchestration
              </button>
            </div>
          </div>
        )}

        {/* TAB: LICENCE & DROITS */}
        {activeTab === "licence" && (
          <div className="space-y-6 animate-in fade-in slide-in-from-left-4 duration-500">
            <div className="grid md:grid-cols-3 gap-6">
              <div className="md:col-span-2 space-y-6">
                <div className="bg-gradient-to-br from-slate-900 to-slate-800 rounded-3xl p-8 text-white shadow-2xl relative overflow-hidden border border-white/10">
                  <div className="absolute top-0 right-0 p-8 opacity-10">
                    <Shield size={120} />
                  </div>
                  <div className="relative z-10 space-y-6">
                    <div className="flex items-center justify-between">
                      <div className="space-y-1">
                        <div className="text-[10px] font-black text-emerald-400 uppercase tracking-[0.3em]">
                          Statut du Système
                        </div>
                        <h3 className="text-2xl font-black">Statut de licence non vérifié</h3>
                      </div>
                      <div className="bg-amber-300 px-4 py-1 rounded-full text-[10px] font-black uppercase tracking-widest text-slate-900">
                        NON VÉRIFIÉ
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-8 pt-4">
                      <div>
                        <div className="text-[10px] font-bold text-slate-400 uppercase tracking-widest mb-1">
                          Établissement
                        </div>
                        <div className="font-bold text-lg">
                          Customer organization
                        </div>
                      </div>
                      <div>
                        <div className="text-[10px] font-bold text-slate-400 uppercase tracking-widest mb-1">
                          Expiration Licence
                        </div>
                        <div className="font-bold text-lg text-emerald-400">
                          Licence et droits non vérifiés
                        </div>
                      </div>
                    </div>

                    <div className="pt-6 flex items-center gap-4 text-xs text-slate-400 border-t border-white/10">
                      <div className="col-span-2 text-amber-200">Aucun module ni droit commercial n’est confirmé par cet écran.</div>
                    </div>
                  </div>
                </div>

                <div className="glass-card p-8 space-y-6">
                  <div className="flex items-center gap-3">
                    <div className="p-3 bg-blue-500/10 text-blue-500 rounded-2xl">
                      <Key size={24} />
                    </div>
                    <div>
                      <h3 className="text-xl font-bold text-slate-900">Licensing</h3>
                      <p className="text-sm text-slate-500">
                        Signed license verification is not configured. No license is activated by this UI.
                      </p>
                    </div>
                  </div>

                  <div className="space-y-4">
                    <div className="bg-slate-50 p-6 rounded-2xl border border-slate-200 space-y-4">
                      <div className="flex items-center gap-2 font-bold text-sm text-slate-800">
                        <RotateCcw size={16} className="text-blue-500" />{" "}
                        Maintenance Système
                      </div>
                      <p className="text-xs text-slate-500 leading-relaxed italic">
                        Le contrat de maintenance est un engagement annuel
                        garantissant la continuité de service et la conformité
                        Ségur/HOP'EN.
                      </p>
                      <div className="grid grid-cols-2 gap-4">
                        {[
                          "Mises à jour critiques",
                          "Support technique H24",
                          "Conformité ANS/RPPS",
                          "Supervision des flux",
                        ].map((item, i) => (
                          <div
                            key={i}
                            className="flex items-center gap-2 text-[10px] font-medium text-slate-600"
                          >
                            <div className="w-1 h-1 rounded-full bg-blue-400" />{" "}
                            {item}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <div className="space-y-6">
                <div className="glass-card p-6 space-y-6 border-emerald-500/20 bg-emerald-500/[0.02]">
                  <div className="text-center space-y-4">
                    <div className="w-20 h-20 bg-slate-100 rounded-3xl mx-auto flex items-center justify-center text-slate-300">
                      <Shield size={40} />
                    </div>
                    <div>
                      <h4 className="font-black text-slate-900 uppercase tracking-tighter">
                        Équipe NexorSys
                      </h4>
                      <p className="text-[10px] font-bold text-emerald-600 uppercase tracking-[0.2em]">
                        Architecte Logiciel Principal
                      </p>
                    </div>
                  </div>
                  <div className="space-y-3 pt-4 border-t border-slate-200">
                    <p className="text-xs text-slate-600 leading-relaxed text-center italic">
                      "NexorSys Identity fournit les contrôles d'identité,
                      d'accès et d'audit configurés par l'opérateur."
                    </p>
                  </div>
                </div>

                <div className="glass-card p-6 space-y-4">
                  <h4 className="font-bold text-sm text-slate-900 uppercase tracking-tight">
                    Support produit
                  </h4>
                  <p className="text-xs text-slate-500 leading-relaxed">
                    Les coordonnées et procédures de support sont fournies par
                    l'opérateur de déploiement.
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}
      </form>

      {/* Password Verification Modal */}
      {passwordModal.isOpen && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-[9999] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-in zoom-in-95 duration-300">
            <div className="p-6 border-b border-slate-100 flex items-center gap-4 bg-slate-50/50">
              <div
                className={`w-12 h-12 rounded-2xl flex items-center justify-center ${passwordModal.action === "reset" ? "bg-red-100 text-red-600" : "bg-amber-100 text-amber-600"}`}
              >
                <Shield size={24} />
              </div>
              <div>
                <h3 className="font-bold text-slate-900 text-lg">
                  Vérification de sécurité
                </h3>
                <p className="text-xs text-slate-500">
                  Authentification administrateur requise
                </p>
              </div>
            </div>

            <div className="p-6 space-y-4">
              {passwordModal.error && (
                <div className="p-3 rounded-lg bg-red-50 text-red-600 text-sm font-medium border border-red-100 flex items-center gap-2">
                  <AlertCircle size={16} /> {passwordModal.error}
                </div>
              )}

              <div className="space-y-4">
                {passwordModal.action === "restore" && (
                  <div className="space-y-2 p-4 bg-amber-50 rounded-xl border border-amber-200">
                    <label className="text-sm font-bold text-slate-700">
                      Fichier de Sauvegarde (.sql)
                    </label>
                    <input
                      type="file"
                      accept=".sql"
                      onChange={(e) =>
                        setPasswordModal((prev) => ({
                          ...prev,
                          file: e.target.files[0],
                        }))
                      }
                      className="w-full text-sm text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-amber-100 file:text-amber-700 hover:file:bg-amber-200"
                    />
                    <p className="text-[10px] text-amber-600 mt-1">
                      Sélectionnez un fichier pour restaurer, ou laissez vide
                      pour restaurer la dernière sauvegarde locale du serveur.
                    </p>
                  </div>
                )}

                <div className="space-y-2">
                  <label className="text-sm font-bold text-slate-700">
                    Mot de passe Super Admin
                  </label>
                  <input
                    type="password"
                    autoFocus
                    value={passwordModal.password}
                    onChange={(e) =>
                      setPasswordModal((prev) => ({
                        ...prev,
                        password: e.target.value,
                        error: "",
                      }))
                    }
                    onKeyDown={(e) =>
                      e.key === "Enter" && executeSecureAction()
                    }
                    className="form-input w-full border-slate-300 focus:border-sky-500 focus:ring-sky-500"
                    placeholder="Saisissez votre mot de passe pour confirmer"
                  />
                  <p className="text-[10px] text-slate-400 mt-1">
                    {passwordModal.action === "reset"
                      ? "⚠️ Cette action effacera toutes les données. Elle est irréversible."
                      : "Cette action modifie l'état global du système."}
                  </p>
                </div>
              </div>

              <div className="pt-4 flex gap-3">
                <button
                  type="button"
                  onClick={() =>
                    setPasswordModal({
                      isOpen: false,
                      action: null,
                      password: "",
                      error: "",
                    })
                  }
                  className="flex-1 py-3 px-4 font-bold rounded-xl text-slate-600 bg-slate-100 hover:bg-slate-200 transition-colors"
                >
                  Annuler
                </button>
                <button
                  type="button"
                  onClick={executeSecureAction}
                  className={`flex-1 py-3 px-4 font-bold rounded-xl text-white transition-all shadow-lg ${
                    passwordModal.action === "reset"
                      ? "bg-red-600 hover:bg-red-700 shadow-red-500/20"
                      : "bg-amber-500 hover:bg-amber-600 shadow-amber-500/20"
                  }`}
                >
                  Confirmer
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Settings;
