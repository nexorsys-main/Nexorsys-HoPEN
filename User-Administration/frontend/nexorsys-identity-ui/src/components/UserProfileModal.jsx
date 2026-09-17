import React, { useState, useEffect } from "react";
import {
  X,
  Save,
  User,
  Shield,
  ShieldCheck,
  CreditCard,
  Wifi,
  Key,
  Lock,
  RefreshCcw,
  CheckCircle,
  XCircle,
  Grid,
  Settings,
  Monitor,
} from "lucide-react";
import { useApp } from "../AppContext";
import api from "../api";

const secureRandomInt = (maxExclusive) => {
  if (!Number.isInteger(maxExclusive) || maxExclusive < 1) {
    throw new RangeError("maxExclusive must be a positive integer");
  }
  const limit = Math.floor(0x100000000 / maxExclusive) * maxExclusive;
  const values = new Uint32Array(1);
  do {
    globalThis.crypto.getRandomValues(values);
  } while (values[0] >= limit);
  return values[0] % maxExclusive;
};

const UserProfileModal = ({ isOpen, onClose, user, onSave }) => {
  const [formData, setFormData] = useState({
    samAccountName: "",
    email: "",
    firstName: "",
    lastName: "",
    department: "",
    title: "",
    role: "User",
    passwordHash: "",
    rppsNumber: "",
    employeeId: "",
    badgeUid: "",
    cpsId: "",
    fidoId: "",
    mieType: "NFC_Badge",
    isActive: true,
    mustChangePin: false,
    authorizedApps: [],
    segurConsent: false,
    tempPin: "",
  });

  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState({ type: "", message: "" });
  const [detectedCard, setDetectedCard] = useState(null);
  const [generatedPin, setGeneratedPin] = useState(null);
  const [readerStatus, setReaderStatus] = useState({ isConnected: false });
  const [availableApps, setAvailableApps] = useState([]);

  useEffect(() => {
    const initForm = async () => {
      if (isOpen) {
        try {
          const settingsRes = await api.get("/settings");
          if (settingsRes.data?.managedApps) {
            setAvailableApps(settingsRes.data.managedApps);
          }
        } catch (e) {
          console.error("Failed to load apps", e);
        }
      }

      if (user && isOpen) {
        setLoading(true);
        try {
          // If this is a new user imported from HR, just use the passed data
          if (user.isImport) {
            setFormData({
              samAccountName: user.samAccountName || "",
              email: user.email || "",
              firstName: user.firstName || "",
              lastName: user.lastName || "",
              department: user.department || "",
              title: user.title || "",
              role: "User",
              passwordHash: "",
              rppsNumber: "",
              employeeId: user.employeeId || "",
              badgeUid: "",
              cpsId: "",
              fidoId: "",
              mieType: "NFC_Badge",
              isActive: true,
              mustChangePin: true,
              authorizedApps: [],
              segurConsent: false,
              tempPin: "",
            });
            return;
          }

          const res = await api.get(`/users/id/${user.id}`);
          const fullUser = res.data;

          // Migration logic: if a CPS ID is in the badge slot, move it
          let initialBadge = fullUser.badgeUid || "";
          let initialCps = fullUser.cpsId || "";
          if (initialBadge.startsWith("CPS-") && !initialCps) {
            initialCps = initialBadge;
            initialBadge = "";
          }

          setFormData({
            samAccountName: fullUser.samAccountName || "",
            email: fullUser.email || "",
            firstName: fullUser.firstName || "",
            lastName: fullUser.lastName || "",
            department: fullUser.department || "",
            title: fullUser.title || "",
            role: fullUser.role || "User",
            passwordHash: "",
            rppsNumber: fullUser.rppsNumber || "",
            employeeId: fullUser.employeeId || "",
            badgeUid: initialBadge || "",
            cpsId: initialCps || "",
            fidoId: fullUser.fidoId || "",
            mieType: fullUser.badgeType || "NFC_Badge",
            isActive: fullUser.isActive ?? true,
            mustChangePin: fullUser.mustChangePin ?? false,
            authorizedApps:
              fullUser.userPermissions
                ?.map((p) => p.application?.clientId)
                .filter(Boolean) || [],
            segurConsent: !!fullUser.rppsNumber,
          });
        } catch {
        } finally {
          setLoading(false);
        }
      } else if (isOpen) {
        setFormData({
          samAccountName: "",
          email: "",
          firstName: "",
          lastName: "",
          department: "",
          title: "",
          role: "User",
          passwordHash: "",
          rppsNumber: "",
          employeeId: "",
          badgeUid: "",
          cpsId: "",
          fidoId: "",
          mieType: "NFC_Badge",
          isActive: true,
          mustChangePin: false,
          authorizedApps: [],
          segurConsent: false,
          tempPin: "",
        });
      }
    };
    initForm();
  }, [user, isOpen]);

  const { lastEvent, connection: globalConnection } = useApp();

  useEffect(() => {
    if (!isOpen) return undefined;

    if (globalConnection && globalConnection.state === "Connected") {
      globalConnection.invoke("JoinEnrollmentRoom").catch(() => undefined);
    }

    let cancelled = false;
    const refreshReaderStatus = async () => {
      try {
        const res = await api.get("/kiosk/reader-status");
        if (cancelled) return;
        const connected =
          res.data.isConnected ||
          res.data.isReaderConnected ||
          res.data.IsConnected;
        setReaderStatus({ ...res.data, isConnected: !!connected });
      } catch {
        if (!cancelled) setReaderStatus((previous) => ({ ...previous, isConnected: false }));
      }
    };

    refreshReaderStatus();
    const statusTimer = window.setInterval(refreshReaderStatus, 1000);
    return () => {
      cancelled = true;
      window.clearInterval(statusTimer);
    };
  }, [isOpen, globalConnection]);

  useEffect(() => {
    if (lastEvent?.type === "OnUserStatusChanged") {
      const data = lastEvent.data;
      if (
        user &&
        (data.userId === user.id || data.samAccountName === user.samAccountName)
      ) {
        setFormData((prev) => ({ ...prev, isActive: data.isActive }));
        setStatus({
          type: "info",
          message: `Le statut de l'agent a été mis à jour en direct (${data.isActive ? "ACTIF" : "INACTIF"})`,
        });
      }
    }
  }, [lastEvent, user]);

  useEffect(() => {
    if (isOpen && globalConnection) {
      const handleHardware = (data) => {
        const id = data.identifier || data.Identifier || data.uid || data.Uid;
        if (id) {
          setDetectedCard(id);
          setStatus({ type: "success", message: `Matériel détecté : ${id}` });
        }
      };
      const handleReader = (data) => {
        const connected =
          data.isConnected || data.IsConnected || data.isReaderConnected;
        // The Hub now sends the full kiosks list in the event
        setReaderStatus({
          ...data,
          isConnected: !!connected,
          kiosks: data.kiosks || [],
        });
      };

      globalConnection.on("OnHardwareDetected", handleHardware);
      globalConnection.on("OnReaderStatusChanged", handleReader);

      return () => {
        globalConnection.off("OnHardwareDetected", handleHardware);
        globalConnection.off("OnReaderStatusChanged", handleReader);
      };
    }
  }, [isOpen, globalConnection]);

  useEffect(() => {
    if (status.message) {
      const timer = setTimeout(
        () => setStatus({ type: "", message: "" }),
        4000,
      );
      return () => clearTimeout(timer);
    }
  }, [status]);

  const handleSave = async () => {
    setLoading(true);
    try {
      if (user) {
        const identifier = user.id || user.samAccountName;
        await api.put(`/users/${identifier}`, formData);
      } else {
        await api.post("/users", formData);
      }

      setStatus({ type: "success", message: "Profil enregistré avec succès" });
      setTimeout(() => {
        onSave();
        onClose();
      }, 1000);
    } catch (err) {
      const data = err.response?.data;
      const message =
        typeof data === "string"
          ? data
          : data?.message || data?.code || "Erreur lors de l'enregistrement";
      setStatus({ type: "error", message });
    } finally {
      setLoading(false);
    }
  };

  const handleAssignCard = () => {
    if (detectedCard) {
      // Smart detection: if it starts with CPS- it's a CPS card regardless of selected tab
      const isCps = detectedCard.startsWith("CPS-") || detectedCard.length > 30;
      const type = isCps ? "CPS" : formData.mieType;

      const fieldMap = {
        NFC_Badge: "badgeUid",
        CPS: "cpsId",
        FIDO2: "fidoId",
      };
      const targetField = fieldMap[type];

      setFormData((prev) => ({
        ...prev,
        [targetField]: detectedCard,
        mieType: type, // Auto-switch tab to match detected hardware
      }));

      setStatus({ type: "success", message: `${type} assigné avec succès.` });
      setDetectedCard(null);
    }
  };

  const clearField = (field) => {
    setFormData((prev) => ({ ...prev, [field]: "" }));
    setStatus({
      type: "info",
      message:
        "Identifiant effacé. Cliquez sur 'Mettre à jour' pour confirmer.",
    });
  };

  const handleGeneratePin = async () => {
    if (!user) {
      // Client-side random 6-digit PIN generation for new users
      const randomPin = (100000 + secureRandomInt(900000)).toString();
      setGeneratedPin(randomPin);
      setFormData((prev) => ({ ...prev, tempPin: randomPin }));
      setStatus({
        type: "success",
        message: `PIN temporaire généré : ${randomPin}`,
      });
      return;
    }
    try {
      const res = await api.post(`/kiosk/generate-temp-pin/${user.id}`);
      setGeneratedPin(res.data.pin);
      setStatus({ type: "success", message: `PIN généré : ${res.data.pin}` });
    } catch (err) {
      setStatus({ type: "error", message: "Erreur PIN" });
    }
  };

  const handleCreateAndWriteUid = async () => {
    try {
      const targetMachine =
        readerStatus.kiosks?.[0]?.machineName || readerStatus.machineName;
      if (!targetMachine) {
        setStatus({
          type: "error",
          message: "Aucun kiosque connecté pour l'écriture.",
        });
        return;
      }
      setStatus({
        type: "info",
        message: "Génération du CUID et envoi à la borne...",
      });
      await api.post("/nfcmanagement/write-cuid", { targetMachine });
      setStatus({
        type: "success",
        message:
          "Ordre d'écriture envoyé. Maintenez le badge vierge sur le lecteur...",
      });
    } catch {
      setStatus({
        type: "error",
        message: "Erreur lors de la demande d'écriture",
      });
    }
  };

  const toggleApp = (appId) => {
    setFormData((prev) => ({
      ...prev,
      authorizedApps: prev.authorizedApps.includes(appId)
        ? prev.authorizedApps.filter((id) => id !== appId)
        : [...prev.authorizedApps, appId],
    }));
  };

  if (!isOpen) return null;

  return (
    <>
      {status.message && (
        <div
          className={`fixed bottom-8 left-1/2 -translate-x-1/2 z-[200] px-6 py-3 rounded-xl shadow-lg flex items-center gap-3 border animate-in fade-in slide-in-from-bottom-2 duration-300 ${
            status.type === "success"
              ? "bg-emerald-50 border-emerald-200 text-emerald-800"
              : "bg-red-50 border-red-200 text-red-800"
          }`}
        >
          {status.type === "success" ? (
            <CheckCircle size={18} />
          ) : (
            <XCircle size={18} />
          )}
          <span className="text-sm font-bold">{status.message}</span>
        </div>
      )}

      <div className="fixed inset-0 z-[100] overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-start justify-center p-4 sm:p-8">
        <div className="bg-white w-full max-w-4xl rounded-2xl shadow-xl border border-slate-200 overflow-hidden animate-in zoom-in-95 duration-200">
          <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50/50">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-emerald-600 flex items-center justify-center font-bold text-white text-sm uppercase">
                {(
                  formData.firstName ||
                  formData.samAccountName ||
                  "AD"
                ).substring(0, 2)}
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-800">
                  {user
                    ? `${formData.firstName} ${formData.lastName}`
                    : "Nouveau Collaborateur"}
                </h3>
                <p className="text-xs text-slate-400 font-medium">
                  {formData.samAccountName || "Compte local en création"}
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-slate-100 rounded-lg text-slate-400 transition-colors"
            >
              <X size={20} />
            </button>
          </div>

          <div className="p-6 space-y-8 max-h-[75vh] overflow-y-auto custom-scrollbar">
            <div className="space-y-4">
              <div className="flex items-center gap-2 text-slate-500 border-b border-slate-100 pb-2">
                <User size={16} />
                <h4 className="text-xs font-bold uppercase tracking-wider">
                  Profil & Accès
                </h4>
              </div>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase">
                    Identifiant
                  </label>
                  <input
                    type="text"
                    value={formData.samAccountName}
                    onChange={(e) =>
                      setFormData({
                        ...formData,
                        samAccountName: e.target.value,
                      })
                    }
                    className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-200 focus:border-emerald-500 outline-none text-sm font-medium"
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase">
                    Rôle Système
                  </label>
                  <select
                    value={formData.role}
                    onChange={(e) =>
                      setFormData({ ...formData, role: e.target.value })
                    }
                    className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-200 focus:border-emerald-500 outline-none text-sm font-medium"
                  >
                    <option value="VIEWER">Utilisateur Standard</option>
                    <option value="USER">Utilisateur</option>
                    <option value="ADMIN_DSI">Staff Admin</option>
                    <option value="ADMIN">Administrateur</option>
                    <option value="SUPERADMIN">Super Administrateur</option>
                  </select>
                </div>
                <div className="flex items-center pt-5">
                  <label className="flex items-center gap-2 cursor-pointer group">
                    <div
                      className={`w-10 h-5 rounded-full p-1 transition-colors ${formData.isActive ? "bg-emerald-500" : "bg-slate-300"}`}
                    >
                      <div
                        className={`w-3 h-3 bg-white rounded-full transition-transform ${formData.isActive ? "translate-x-5" : "translate-x-0"}`}
                      />
                      <input
                        type="checkbox"
                        checked={formData.isActive}
                        onChange={(e) =>
                          setFormData({
                            ...formData,
                            isActive: e.target.checked,
                          })
                        }
                        className="hidden"
                      />
                    </div>
                    <span className="text-[11px] font-bold text-slate-600 group-hover:text-slate-800 transition-colors uppercase">
                      Compte Actif (Accès Kiosque)
                    </span>
                  </label>
                </div>
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase">
                    Prénom
                  </label>
                  <input
                    type="text"
                    value={formData.firstName}
                    onChange={(e) =>
                      setFormData({ ...formData, firstName: e.target.value })
                    }
                    className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-200 text-sm font-medium"
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase">
                    Nom
                  </label>
                  <input
                    type="text"
                    value={formData.lastName}
                    onChange={(e) =>
                      setFormData({ ...formData, lastName: e.target.value })
                    }
                    className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-200 text-sm font-medium"
                  />
                </div>
                <div className="space-y-1">
                  <label className="text-[10px] font-bold text-slate-500 uppercase">
                    Email
                  </label>
                  <input
                    type="email"
                    value={formData.email}
                    onChange={(e) =>
                      setFormData({ ...formData, email: e.target.value })
                    }
                    className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-200 text-sm font-medium"
                  />
                </div>

                {(formData.role?.toUpperCase() === "ADMIN" ||
                  formData.role?.toUpperCase() === "SUPERADMIN") && (
                  <div className="col-span-2 md:col-span-3 mt-2 bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-3">
                    <div className="flex items-center gap-2 text-slate-600 border-b border-slate-200 pb-2">
                      <Lock size={14} />
                      <h5 className="text-[10px] font-bold uppercase tracking-wider">
                        Accès Administration
                      </h5>
                    </div>
                    <div className="flex items-end gap-3">
                      <div className="flex-1 space-y-1">
                        <label className="text-[10px] font-bold text-slate-500 uppercase">
                          Mot de passe (pour connexion au portail d'admin)
                        </label>
                        <input
                          type="text"
                          value={formData.passwordHash || ""}
                          onChange={(e) =>
                            setFormData({
                              ...formData,
                              passwordHash: e.target.value,
                            })
                          }
                          className="w-full px-3 py-2 rounded-lg bg-white border border-slate-200 focus:border-emerald-500 outline-none text-sm font-medium"
                          placeholder={
                            user
                              ? "Laissez vide pour conserver l'actuel"
                              : "Définissez un mot de passe"
                          }
                        />
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          const chars =
                            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
                          let password = "";
                          for (let i = 0; i <= 14; i++) {
                            password += chars.charAt(secureRandomInt(chars.length));
                          }
                          setFormData({ ...formData, passwordHash: password });
                        }}
                        className="px-4 py-2 bg-slate-800 text-white rounded-lg text-xs font-bold hover:bg-slate-700 transition-colors"
                      >
                        Générer
                      </button>
                    </div>
                    <p className="text-[10px] text-amber-600 font-medium">
                      Attention: Notez bien ce mot de passe et transmettez-le de
                      façon sécurisée à l'administrateur.
                    </p>
                  </div>
                )}
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center gap-2 text-slate-500 border-b border-slate-100 pb-2">
                <Grid size={16} />
                <h4 className="text-xs font-bold uppercase tracking-wider">
                  Habilitations Applicatives
                </h4>
              </div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                {availableApps.map((app) => (
                  <label
                    key={app.id}
                    className={`flex items-center gap-3 p-3 rounded-xl border cursor-pointer transition-all ${
                      formData.authorizedApps.includes(app.id)
                        ? "bg-emerald-50 border-emerald-200 shadow-sm"
                        : "bg-slate-50 border-slate-100 opacity-60 hover:opacity-100"
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={formData.authorizedApps.includes(app.id)}
                      onChange={() => toggleApp(app.id)}
                      className="hidden"
                    />
                    <div
                      className={`w-3.5 h-3.5 rounded border flex items-center justify-center shrink-0 ${formData.authorizedApps.includes(app.id) ? "bg-emerald-500 border-emerald-500 text-white" : "bg-white border-slate-300"}`}
                    >
                      {formData.authorizedApps.includes(app.id) && (
                        <CheckCircle size={10} />
                      )}
                    </div>
                    <span className="text-[10px] font-bold text-slate-700 truncate">
                      {app.name}
                    </span>
                  </label>
                ))}
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center gap-2 text-slate-500 border-b border-slate-100 pb-2">
                <CreditCard size={16} />
                <h4 className="text-xs font-bold uppercase tracking-wider">
                  Identité Numérique MIE
                </h4>
              </div>

              <div className="flex gap-2 mb-6 p-1 bg-slate-100 rounded-xl w-fit">
                {["NFC_Badge", "CPS", "FIDO2"].map((type) => (
                  <button
                    key={type}
                    onClick={() => {
                      setFormData({ ...formData, mieType: type });
                      setDetectedCard(null); // Clear detection when switching types
                    }}
                    className={`px-4 py-1.5 rounded-lg text-[10px] font-bold uppercase tracking-tight transition-all ${
                      formData.mieType === type
                        ? "bg-white text-emerald-600 shadow-sm"
                        : "text-slate-400 hover:text-slate-600"
                    }`}
                  >
                    {type === "NFC_Badge"
                      ? "Badge NFC"
                      : type === "CPS"
                        ? "Carte CPS"
                        : "Clef FIDO2"}
                  </button>
                ))}
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {formData.mieType === "NFC_Badge" ? (
                  <div className="p-5 rounded-2xl border border-slate-200 bg-slate-900 text-white space-y-4 shadow-lg relative overflow-hidden group">
                    <div className="absolute top-0 right-0 w-32 h-32 bg-emerald-500/5 blur-3xl rounded-full -mr-16 -mt-16 group-hover:bg-emerald-500/10 transition-all duration-700"></div>
                    <div className="flex items-center justify-between relative z-10">
                      <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                        Lecteur NFC
                      </span>
                      <div
                        className={`flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[9px] font-bold ${
                          !readerStatus.kiosks?.length
                            ? "bg-amber-500/20 text-amber-400"
                            : readerStatus.isConnected
                              ? "bg-emerald-500/20 text-emerald-400"
                              : "bg-rose-500/20 text-rose-400"
                        }`}
                      >
                        {!readerStatus.kiosks?.length
                          ? "BRIDGE MANQUANT"
                          : readerStatus.isConnected
                            ? "LECTEUR OK"
                            : "DÉCONNECTÉ"}
                      </div>
                    </div>
                    <div className="bg-white/5 border border-white/10 rounded-xl p-3 text-center relative z-10 flex flex-col items-center justify-center min-h-[50px]">
                      {detectedCard ? (
                        <div className="flex flex-col items-center gap-1">
                          <span className="text-sm font-mono tracking-widest text-emerald-400 animate-pulse">
                            {detectedCard}
                          </span>
                          <button
                            onClick={() => setDetectedCard(null)}
                            className="text-[8px] text-slate-500 hover:text-rose-400 font-bold uppercase transition-colors"
                          >
                            Ignorer
                          </button>
                        </div>
                      ) : formData.badgeUid ? (
                        <div className="flex flex-col items-center gap-1">
                          <span className="text-sm font-mono tracking-widest text-blue-400">
                            {formData.badgeUid}
                          </span>
                          <button
                            onClick={() => clearField("badgeUid")}
                            className="text-[8px] text-slate-500 hover:text-rose-400 font-bold uppercase transition-colors"
                          >
                            Effacer
                          </button>
                        </div>
                      ) : (
                        <span className="text-[10px] text-slate-500 font-bold tracking-widest">
                          SCAN EN ATTENTE
                        </span>
                      )}
                    </div>
                    <div className="flex gap-2 relative z-10">
                      <button
                        type="button"
                        onClick={() => {
                          setDetectedCard(null);
                          setStatus({
                            type: "info",
                            message: "Présentez la carte sur le lecteur NFC...",
                          });
                        }}
                        disabled={!readerStatus.isConnected}
                        className="flex-1 py-2 bg-white/10 hover:bg-white/20 text-[10px] font-bold uppercase rounded-lg border border-white/10 transition-colors"
                      >
                        Détecter
                      </button>

                      <button
                        type="button"
                        onClick={handleCreateAndWriteUid}
                        disabled={!readerStatus.isConnected}
                        className="flex-1 py-2 bg-amber-500 hover:bg-amber-400 text-slate-900 text-[10px] font-bold uppercase rounded-lg transition-colors shadow-lg shadow-amber-500/20"
                        title="Génère un CUID et l'écrit sur un badge vierge"
                      >
                        Créer & Écrire UID
                      </button>

                      <button
                        type="button"
                        onClick={handleAssignCard}
                        disabled={!detectedCard}
                        className="flex-1 py-2 bg-emerald-600 hover:bg-emerald-500 text-[10px] font-bold uppercase rounded-lg transition-colors shadow-lg shadow-emerald-600/20"
                      >
                        Assigner
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="p-5 rounded-2xl border border-slate-200 bg-[#0f172a] text-white space-y-4 shadow-xl relative overflow-hidden group">
                    <div className="absolute top-0 right-0 w-32 h-32 bg-emerald-500/5 blur-3xl rounded-full -mr-16 -mt-16 group-hover:bg-emerald-500/10 transition-all duration-700"></div>
                    <div className="flex items-center justify-between relative z-10">
                      <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                        {formData.mieType === "CPS"
                          ? "Lecteur CPS"
                          : "Clef Sécurité"}
                      </span>
                      <div
                        className={`flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[9px] font-bold ${
                          !readerStatus.kiosks?.length
                            ? "bg-amber-500/20 text-amber-400"
                            : readerStatus.isConnected
                              ? "bg-emerald-500/20 text-emerald-400"
                              : "bg-rose-500/20 text-rose-400"
                        }`}
                      >
                        {!readerStatus.kiosks?.length
                          ? "BRIDGE MANQUANT"
                          : readerStatus.isConnected
                            ? "PRÊT"
                            : "DÉCONNECTÉ"}
                      </div>
                    </div>
                    <div className="bg-white/5 border border-white/10 rounded-xl p-3 text-center relative z-10 flex flex-col items-center justify-center min-h-[60px]">
                      {detectedCard ? (
                        <div className="flex flex-col items-center gap-1">
                          <span className="text-[11px] font-mono tracking-tight text-emerald-400 animate-pulse break-all leading-tight px-4">
                            {detectedCard}
                          </span>
                          <button
                            onClick={() => setDetectedCard(null)}
                            className="text-[8px] text-slate-500 hover:text-rose-400 font-bold uppercase transition-colors"
                          >
                            Ignorer
                          </button>
                        </div>
                      ) : (
                          formData.mieType === "CPS"
                            ? formData.cpsId
                            : formData.fidoId
                        ) ? (
                        <div className="flex flex-col items-center">
                          <span className="text-[11px] font-mono tracking-tight text-blue-400 break-all leading-tight px-4 text-center">
                            {formData.mieType === "CPS"
                              ? formData.cpsId
                              : formData.fidoId}
                          </span>
                          <button
                            onClick={() =>
                              clearField(
                                formData.mieType === "CPS" ? "cpsId" : "fidoId",
                              )
                            }
                            className="text-[8px] text-slate-500 hover:text-rose-400 font-bold uppercase mt-1 transition-colors"
                          >
                            Détacher cette carte
                          </button>
                        </div>
                      ) : (
                        <span className="text-[10px] text-slate-500 font-bold tracking-widest uppercase">
                          {readerStatus.kiosks?.length
                            ? formData.mieType === "CPS"
                              ? "INSÉREZ CARTE"
                              : "BRANCHEZ CLEF"
                            : "BRIDGE MANQUANT"}
                        </span>
                      )}
                    </div>
                    <div className="flex gap-2 relative z-10">
                      <button
                        type="button"
                        onClick={() => {
                          setDetectedCard(null);
                          setStatus({
                            type: "info",
                            message: "Présentez le matériel sur le poste kiosk...",
                          });
                        }}
                        className="flex-1 py-2 bg-white/10 hover:bg-white/20 text-[10px] font-bold uppercase rounded-lg border border-white/10 transition-colors"
                      >
                        Détecter
                      </button>
                      <button
                        type="button"
                        onClick={handleAssignCard}
                        disabled={!detectedCard}
                        className="flex-1 py-2 bg-emerald-600 hover:bg-emerald-500 text-[10px] font-bold uppercase rounded-lg transition-colors shadow-lg shadow-emerald-600/20"
                      >
                        Assigner
                      </button>
                    </div>
                  </div>
                )}
                <div className="p-5 rounded-2xl border border-slate-200 bg-slate-50 space-y-4 shadow-sm">
                  <div className="flex items-center justify-between">
                    <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">
                      Code PIN de secours
                    </span>
                    <Key size={14} className="text-slate-300" />
                  </div>
                  <div className="bg-white border border-slate-200 rounded-xl p-3 text-center h-[46px] flex items-center justify-center">
                    {generatedPin ? (
                      <span className="text-2xl font-black tracking-[0.3em] text-slate-800">
                        {generatedPin}
                      </span>
                    ) : (
                      <span className="text-xs text-slate-300 font-bold tracking-widest">
                        --- ---
                      </span>
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={handleGeneratePin}
                    className="w-full py-2 bg-slate-800 hover:bg-slate-700 text-white text-[10px] font-bold uppercase rounded-lg transition-colors"
                  >
                    Générer nouveau PIN
                  </button>
                </div>
              </div>
            </div>

            <div className="space-y-4 pt-2">
              <div className="flex items-center gap-2 text-slate-500 border-b border-slate-100 pb-2">
                <ShieldCheck size={16} />
                <h4 className="text-xs font-bold uppercase tracking-wider">
                  Conformité ANS / Ségur
                </h4>
              </div>
              <div className="bg-blue-50/30 p-5 rounded-2xl border border-blue-100/50 space-y-5">
                <div className="flex flex-col gap-3">
                  <label className="flex items-start gap-3 cursor-pointer group">
                    <input
                      type="checkbox"
                      checked={formData.segurConsent}
                      onChange={(e) =>
                        setFormData({
                          ...formData,
                          segurConsent: e.target.checked,
                        })
                      }
                      className="mt-1 w-4 h-4 rounded border-blue-200 text-blue-600"
                    />
                    <span className="text-[11px] font-medium text-slate-600 leading-relaxed group-hover:text-slate-800 transition-colors">
                      Le professionnel certifie accepter l'utilisation de ce
                      badge nominatif comme moyen d'authentification forte
                      (MIE), conformément au cadre Ségur du Numérique en Santé.
                    </span>
                  </label>
                  <label className="flex items-center gap-3 cursor-pointer group bg-amber-50/50 p-2 rounded-lg border border-amber-100">
                    <input
                      type="checkbox"
                      checked={formData.mustChangePin}
                      onChange={(e) =>
                        setFormData({
                          ...formData,
                          mustChangePin: e.target.checked,
                        })
                      }
                      className="w-4 h-4 rounded border-amber-200 text-amber-600"
                    />
                    <span className="text-[10px] font-bold text-amber-800 uppercase tracking-tight">
                      Forcer le changement de PIN à la prochaine connexion
                    </span>
                  </label>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-slate-400 uppercase">
                      Service / Unité
                    </label>
                    <select
                      value={formData.department}
                      onChange={(e) =>
                        setFormData({ ...formData, department: e.target.value })
                      }
                      className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-100 text-xs font-bold focus:border-emerald-500 outline-none"
                    >
                      <option value="">Sélectionner un service</option>
                      <option value="MED">Médecine (MED)</option>
                      <option value="CHIR">Chirurgie (CHIR)</option>
                      <option value="URG">Urgences (URG)</option>
                      <option value="ADM">Administration (ADM)</option>
                      <option value="DSI">DSI / Support</option>
                      <option value="RH">Ressources Humaines</option>
                      <option value="ACC">Accueil / Standard</option>
                      <option value="IDE">Soins Infirmiers</option>
                      <option value="BLOC">Bloc Opératoire</option>
                    </select>
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-slate-400 uppercase">
                      Fonction / Titre
                    </label>
                    <input
                      type="text"
                      value={formData.title}
                      onChange={(e) =>
                        setFormData({ ...formData, title: e.target.value })
                      }
                      className="w-full px-3 py-2 rounded-lg bg-slate-50 border border-slate-100 text-xs font-bold focus:border-emerald-500 outline-none"
                      placeholder="Ex: Infirmier DE, Médecin..."
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-blue-800 uppercase">
                      N° RPPS / ADELI
                    </label>
                    <input
                      type="text"
                      value={formData.rppsNumber}
                      onChange={(e) =>
                        setFormData({ ...formData, rppsNumber: e.target.value })
                      }
                      className="w-full px-3 py-2 rounded-lg bg-white border border-blue-100 text-xs font-bold focus:border-blue-400 outline-none"
                      placeholder="123456789012"
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-[10px] font-bold text-blue-800 uppercase">
                      Matricule Interne
                    </label>
                    <input
                      type="text"
                      value={formData.employeeId}
                      onChange={(e) =>
                        setFormData({ ...formData, employeeId: e.target.value })
                      }
                      className="w-full px-3 py-2 rounded-lg bg-white border border-blue-100 text-xs font-bold focus:border-blue-400 outline-none"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex justify-end gap-3">
            <button
              onClick={onClose}
              className="px-5 py-2 text-xs font-bold text-slate-500 hover:bg-slate-200 rounded-lg transition-colors uppercase tracking-wider"
            >
              Fermer
            </button>
            <button
              onClick={handleSave}
              disabled={loading}
              className="px-8 py-2 bg-slate-900 text-white text-xs font-bold rounded-lg hover:bg-slate-800 shadow-lg flex items-center gap-2 uppercase tracking-wider"
            >
              {loading ? (
                <RefreshCcw size={14} className="animate-spin" />
              ) : (
                <Save size={14} />
              )}
              {user ? "Mettre à jour" : "Créer l'utilisateur"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
};

export default UserProfileModal;
