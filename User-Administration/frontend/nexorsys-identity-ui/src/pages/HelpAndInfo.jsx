import React, { useState, useEffect } from "react";
import {
  BookOpen,
  Info,
  HelpCircle,
  Terminal,
  Mail,
  Phone,
  Code,
  Cpu,
  Shield,
  Users,
  User as UserIcon,
  ShieldCheck,
  Monitor,
  GitPullRequest,
  X,
  Scale,
  FileText,
  Download,
  ChevronRight,
  CheckSquare,
  RefreshCcw,
  Server,
  Activity,
  Database,
  Key,
  Lock,
} from "lucide-react";
import api from "../api";

const HelpAndInfo = () => {
  const [activeTab, setActiveTab] = useState("training");
  const [selectedSection, setSelectedSection] = useState(null);
  const [ansLogs, setAnsLogs] = useState([]);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [currentUser, setCurrentUser] = useState(null);

  // Tailwind JIT compiler safelist for dynamically constructed classes
  const TAILWIND_SAFELIST =
    "bg-emerald-500/10 bg-emerald-50 bg-emerald-100 bg-emerald-500 bg-emerald-600 hover:bg-emerald-500 border-emerald-100 text-emerald-500 text-emerald-600 group-hover:bg-emerald-500 shadow-emerald-500/20 from-emerald-50 " +
    "bg-violet-500/10 bg-violet-50 bg-violet-100 bg-violet-500 bg-violet-600 hover:bg-violet-500 border-violet-100 text-violet-500 text-violet-600 group-hover:bg-violet-500 shadow-violet-500/20 from-violet-50 " +
    "bg-blue-500/10 bg-blue-50 bg-blue-100 bg-blue-500 bg-blue-600 hover:bg-blue-500 border-blue-100 text-blue-500 text-blue-600 group-hover:bg-blue-500 shadow-blue-500/20 from-blue-50 " +
    "bg-rose-500/10 bg-rose-50 bg-rose-100 bg-rose-500 bg-rose-600 hover:bg-rose-500 border-rose-100 text-rose-500 text-rose-600 group-hover:bg-rose-500 shadow-rose-500/20 from-rose-50 " +
    "bg-amber-500/10 bg-amber-50 bg-amber-100 bg-amber-500 bg-amber-600 hover:bg-amber-500 border-amber-100 text-amber-500 text-amber-600 group-hover:bg-amber-500 shadow-amber-500/20 from-amber-50 " +
    "bg-cyan-500/10 bg-cyan-50 bg-cyan-100 bg-cyan-500 bg-cyan-600 hover:bg-cyan-500 border-cyan-100 text-cyan-500 text-cyan-600 group-hover:bg-cyan-500 shadow-cyan-500/20 from-cyan-50";

  useEffect(() => {
    fetchCurrentProfile();
    if (activeTab === "ans_declarations") {
      fetchAnsLogs();
    }
  }, [activeTab]);

  const fetchCurrentProfile = async () => {
    try {
      const res = await api.get("/users/me");
      setCurrentUser(res.data);
    } catch (err) {
    }
  };

  const fetchAnsLogs = async () => {
    setLoadingLogs(true);
    try {
      const res = await api.get("/ansdelegation/logs");
      setAnsLogs(res.data);
    } catch (err) {
    } finally {
      setLoadingLogs(false);
    }
  };

  const trainingSections = [
    {
      title: "Gestion des Identités",
      icon: <Users className="text-emerald-400" size={28} />,
      color: "emerald",
      content:
        "Ce module permet de gérer le cycle de vie des identités. Recherchez, importez ou créez des comptes utilisateurs avec une conformité RGPD stricte.",
      details: [
        "Recherche Hybride : Synchronisation en temps réel avec l'Active Directory et la base PostgreSQL locale.",
        "Sélecteur Haute Performance : Filtrage instantané par service (MED, CHIR, DSI, etc.).",
        "Provisioning Automatique : Importez les attributs LDAP en un clic pour créer le profil IAM.",
        "Sécurité Accrue : Définition des rôles (Admin, Staff, User) avec droits RBAC granulaires.",
      ],
    },
    {
      title: "Gouvernance & Workflows",
      icon: <GitPullRequest className="text-violet-400" size={28} />,
      color: "violet",
      content:
        "Orchestration des demandes d'habilitations (RH, DSI). Traçabilité complète des validations pour répondre aux exigences qualité.",
      details: [
        "Automatisation : Les demandes de changement de poste génèrent des workflows validés par la RH.",
        "Traçabilité des Décisions : Obligation de justifier les rejets avec historisation inaltérable.",
        "Mappage Automatique : Les habilitations logicielles (EMED, HESTIA) sont provisionnées dès validation.",
        "Auditabilité : Cycle de vie consultable pour les certifications (HAS, ISO 27001).",
      ],
    },
    {
      title: "Kiosque MIE & Fédération",
      icon: <Monitor className="text-blue-400" size={28} />,
      color: "blue",
      content:
        "Centre de pilotage pour l'authentification matérielle multiformat (WinSCard, CPS-APDU) et le couplage avec l'identité numérique.",
      details: [
        "Enrôlement Multi-Lecteurs : Support natif des lecteurs CPS classiques et des lecteurs matériels WinSCard via le système d'alias (KEY_AUTH).",
        "Gestion des Badges Inconnus : Récupération des identifiants non reconnus via l'Audit pour un liage manuel aux profils utilisateurs.",
        "Normalisation PIN Stricte : Validation robuste des codes PIN par filtrage alphanumérique (ignorant les symboles des différents lecteurs).",
        "Interopérabilité : Capacité d'absorber des chaînes d'identification CPS étendues sans rupture de base de données.",
      ],
    },
    {
      title: "Traçabilité & Sécurité",
      icon: <Shield className="text-rose-400" size={28} />,
      color: "rose",
      content:
        "Le moteur d'audit surveille, horodate et sécurise toutes les actions sensibles réalisées sur la plateforme IAM.",
      details: [
        "Protection Anti-BruteForce : Limites de requêtes (Rate Limiting) intégrées aux couches API.",
        "Logs Immutables : Chaque action modifiant la base (suspendre un agent, certifier un badge) est journalisée.",
        "Analyse Forensique : Capture des agents utilisateurs (User-Agent), adresses IP et différentiels de données.",
        "Alerting : Détection des anomalies et des tentatives de connexion frauduleuses.",
      ],
    },
    {
      title: "Conformité Pro Santé Connect",
      icon: <CheckSquare className="text-amber-400" size={28} />,
      color: "amber",
      content:
        "Interface native avec l'Agence du Numérique en Santé (ANS) pour la fédération des identités (e-CPS).",
      details: [
        "Substitution Légale : Déclaration des MIE locaux comme équivalents Ségur (délégation d'authentification).",
        "Registre des Consentements : Horodatage et stockage des accords utilisateurs (AIL Modéré/Élevé).",
        "Flux Sécurisés : Transmission cryptée des logs vers les infrastructures nationales.",
        "Veille Technologique : Compatible avec le socle technique PGSSI-S V2.",
      ],
    },
    {
      title: "Administration Système & Déploiement",
      icon: <Database className="text-cyan-400" size={28} />,
      color: "cyan",
      content:
        "Contrôle absolu sur les Kiosques distants, les paramètres d'intégration, et la synchronisation de l'architecture.",
      details: [
        "Auto-Mise à Jour des Kiosques : Système de Heartbeat avec détection de changement de hash de configuration.",
        "Rotation des Clés d'API : Tolérance aux anciennes clés (Legacy Keys) pour permettre aux kiosques déconnectés de se synchroniser et de redémarrer automatiquement.",
        "Configuration Dynamique : Changement à chaud des paramètres globaux propagé instantanément aux bornes.",
        "Haute Disponibilité : Base PostgreSQL avec contraintes étendues (VARCHAR 512) pour éviter les saturations de sessions.",
      ],
    },
  ];

  const rgpdSections = [
    {
      id: "sec1",
      title: "1. Objet du traitement",
      content:
        "Le traitement a pour objet la gestion des accès sécurisés aux postes de travail d'une organisation cliente via les facteurs d'authentification activés par cette organisation. Les événements de sécurité sont journalisés selon la configuration déployée.",
    },
    {
      id: "sec2",
      title: "2. Responsable du traitement",
      content:
        "Le responsable du traitement est l'organisation cliente configurée dans NexorSys Identity. Les responsabilités légales, la conservation et les évaluations de conformité doivent être définies par contrat et par déploiement.",
    },
    {
      id: "sec3",
      title: "3. Données traitées",
      content:
        "Les catégories de données suivantes sont traitées : Données d'identification (Nom, Prénom, Matricule, SamAccountName, UID du badge), Données de connexion (Horodatage, IP de la borne, succès/échec de l'authentification, agent utilisateur). AUCUNE donnée de santé patient (DMP, dossiers médicaux) n'est traitée ni stockée par cette plateforme IAM.",
    },
    {
      id: "sec4",
      title: "4. Base Légale et Finalités",
      content:
        "Le traitement repose sur l'obligation légale du responsable de traitement de sécuriser les accès aux données de santé (Art. 32 du RGPD) et sur le respect du décret de confidentialité des données de santé. La finalité est la protection du SIH contre les accès non autorisés et la garantie de l'imputabilité des actions de soin.",
    },
    {
      id: "sec5",
      title: "5. Durée de Conservation",
      content:
        "Conformément aux recommandations de l'ANSSI et de l'ANS pour le secteur hospitalier, les journaux d'audit (Audit Logs) sont conservés pendant une durée glissante de 12 mois. Les données de profil (badges, PIN hashés) sont conservées tant que l'agent est sous contrat avec l'établissement et sont supprimées ou anonymisées 3 mois après son départ.",
    },
    {
      id: "sec6",
      title: "6. Destinataires et Transferts",
      content:
        "L'hébergement, les transferts et les intégrations externes dépendent de la configuration du déploiement. Aucune certification, conformité réglementaire ou garantie de résidence des données ne doit être déduite de la présence d'une fonctionnalité dans l'interface.",
    },
    {
      id: "sec7",
      title: "7. Sécurité et PGSSI-S",
      content:
        "La plateforme implémente les standards de la PGSSI-S : hachage BCrypt (PIN), chiffrement TLS 1.2+ des flux, et protection contre les attaques par force brute. L'accès aux consoles d'administration est restreint par RBAC (Role-Based Access Control) aux seuls profils DSI et RH dûment habilités.",
    },
    {
      id: "sec8",
      title: "8. Droits des personnes",
      content:
        "Conformément au RGPD, vous disposez d'un droit d'accès, de rectification, de limitation et d'opposition. Compte tenu des enjeux de sécurité hospitalière (HOP'EN), le droit à l'effacement peut être limité pour les journaux d'audit légaux. Pour exercer vos droits, contactez le DSI ou le DPO de l'établissement.",
    },
    {
      id: "sec9",
      title: "9. Rôle de Concepteur",
      content:
        'L\'architecture logicielle, le code source, les interfaces graphiques et les algorithmes de la plateforme "PINÈDE Identity" sont la propriété exclusive de son concepteur principal, Ali HAMIDY. Toute reproduction, modification, ou redistribution de tout ou partie de ce logiciel sans autorisation écrite est strictement interdite.',
    },
  ];

  const handleExportPDF = () => {
    window.print();
  };

  return (
    <div className="max-w-5xl mx-auto space-y-6 animate-in fade-in slide-in-from-bottom-8 duration-700">
      {/* ── Hero Header ── */}
      <div className="relative overflow-hidden rounded-3xl bg-slate-900 border border-slate-800 shadow-2xl print:hidden">
        {/* Dynamic Background */}
        <div className="absolute inset-0 overflow-hidden">
          <div className="absolute -top-40 -right-40 w-96 h-96 bg-emerald-500/20 blur-[100px] rounded-full mix-blend-screen animate-pulse-subtle"></div>
          <div
            className="absolute -bottom-40 -left-40 w-96 h-96 bg-blue-500/20 blur-[100px] rounded-full mix-blend-screen animate-pulse-subtle"
            style={{ animationDelay: "2s" }}
          ></div>
          <div className="absolute inset-0 bg-[url('/noise.svg')] opacity-20 mix-blend-overlay"></div>
        </div>

        <div className="relative p-6 md:p-8 z-10 flex flex-col md:flex-row items-center justify-between gap-6">
          <div className="space-y-3 max-w-2xl">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-xs font-bold uppercase tracking-widest backdrop-blur-md">
              <ShieldCheck size={14} /> Documentation Officielle
            </div>
            <h1 className="text-2xl md:text-3xl font-black text-white tracking-tight leading-tight">
              Centre d'Aide &{" "}
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 to-cyan-400">
                Informations
              </span>
            </h1>
            <p className="text-slate-400 text-sm font-medium leading-relaxed">
              Explorez le fonctionnement de PINÈDE Identity. Des workflows RH à
              l'infrastructure matérielle NFC, retrouvez toute la documentation
              de votre architecture IAM.
            </p>
          </div>
          <div className="hidden lg:flex shrink-0 p-4 bg-white/5 backdrop-blur-xl border border-white/10 rounded-2xl shadow-2xl transform rotate-3 hover:rotate-0 transition-transform duration-500">
            <BookOpen
              size={48}
              className="text-emerald-500/80 drop-shadow-[0_0_20px_rgba(16,185,129,0.4)]"
            />
          </div>
        </div>
      </div>

      {/* ── Custom Navigation Tabs ── */}
      <div className="flex gap-2 p-1 bg-slate-200/50 backdrop-blur-md rounded-2xl print:hidden overflow-x-auto custom-scrollbar border border-slate-200 shadow-inner">
        {[
          { id: "training", icon: BookOpen, label: "Formation & Usage" },
          { id: "info", icon: Info, label: "Informations Système" },
          { id: "legal", icon: Scale, label: "Légal & RGPD" },
          {
            id: "ans_declarations",
            icon: CheckSquare,
            label: "Déclarations Ségur (ANS)",
          },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-xl font-bold text-xs transition-all whitespace-nowrap ${
              activeTab === tab.id
                ? "bg-white text-slate-900 shadow-md shadow-slate-200/50 scale-[1.02]"
                : "text-slate-500 hover:bg-white/60 hover:text-slate-700"
            }`}
          >
            <tab.icon
              size={18}
              className={activeTab === tab.id ? "text-emerald-600" : ""}
            />
            {tab.label}
          </button>
        ))}
      </div>

      {/* ── Content Sections ── */}
      <div className="min-h-[500px]">
        {/* TAB 1: FORMATION */}
        {activeTab === "training" && (
          <div className="grid md:grid-cols-2 gap-6 animate-in fade-in slide-in-from-bottom-4 duration-500 print:hidden">
            {trainingSections.map((section, idx) => (
              <div
                key={idx}
                className="group relative bg-white border border-slate-200 rounded-2xl p-5 hover:shadow-xl hover:shadow-slate-200/50 hover:border-emerald-200 transition-all duration-500 cursor-pointer overflow-hidden flex flex-col"
                onClick={() => setSelectedSection(section)}
              >
                {/* Background glow on hover */}
                <div
                  className={`absolute -right-20 -top-20 w-40 h-40 bg-${section.color}-500/10 blur-[50px] rounded-full group-hover:scale-150 transition-transform duration-700`}
                ></div>

                <div className="flex items-center gap-4 mb-3 relative z-10">
                  <div
                    className={`w-10 h-10 rounded-xl bg-${section.color}-50 flex items-center justify-center shadow-inner border border-${section.color}-100 group-hover:scale-110 transition-transform duration-500`}
                  >
                    {React.cloneElement(section.icon, { size: 20 })}
                  </div>
                  <h3 className="text-base font-black text-slate-800">
                    {section.title}
                  </h3>
                </div>

                <p className="text-slate-600 text-xs leading-relaxed font-medium flex-1 relative z-10">
                  {section.content}
                </p>

                <div className="mt-4 flex justify-end relative z-10">
                  <span
                    className={`inline-flex items-center gap-2 text-[10px] font-bold text-${section.color}-600 bg-${section.color}-50 px-3 py-1.5 rounded-md group-hover:bg-${section.color}-500 group-hover:text-white transition-colors duration-300`}
                  >
                    Lire la suite <ChevronRight size={12} />
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* TAB 2: INFORMATIONS SYSTEME */}
        {activeTab === "info" && (
          <div className="space-y-8 animate-in fade-in slide-in-from-bottom-4 duration-500 print:hidden">
            {/* Version Dashboard */}
            <div className="grid md:grid-cols-3 gap-4">
              <div className="col-span-2 bg-slate-900 border border-slate-800 rounded-2xl p-6 text-white relative overflow-hidden shadow-xl">
                <div className="absolute top-0 right-0 w-48 h-48 bg-emerald-500/10 blur-[60px] rounded-full"></div>
                <div className="relative z-10 flex flex-col h-full justify-between">
                  <div className="flex items-center gap-2 text-emerald-400 mb-4">
                    <Terminal size={16} />
                    <span className="text-[10px] font-bold uppercase tracking-widest">
                      Build Actuel
                    </span>
                  </div>
                  <div>
                    <div className="text-4xl font-black tracking-tighter mb-1">
                      v2.1<span className="text-emerald-400">.Stable</span>
                    </div>
                    <p className="text-slate-400 text-xs font-medium">
                      NexorSys Identity — Multi-lecteurs & synchronisation
                    </p>
                  </div>
                </div>
              </div>

              <div className="bg-white border border-slate-200 rounded-2xl p-5 shadow-sm flex flex-col gap-3">
                <div className="flex items-center gap-2 text-slate-800 border-b border-slate-100 pb-2">
                  <Server size={16} className="text-blue-500" />
                  <span className="font-bold text-sm">Architecture</span>
                </div>
                <div className="space-y-2 flex-1">
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-slate-500 font-medium">Backend</span>
                    <span className="font-bold text-slate-800 bg-slate-100 px-2 py-0.5 rounded">
                      .NET 8.0 API
                    </span>
                  </div>
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-slate-500 font-medium">Frontend</span>
                    <span className="font-bold text-slate-800 bg-slate-100 px-2 py-0.5 rounded">
                      React 18 / Vite
                    </span>
                  </div>
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-slate-500 font-medium">Database</span>
                    <span className="font-bold text-slate-800 bg-slate-100 px-2 py-0.5 rounded">
                      PostgreSQL 14 (Extended Schema)
                    </span>
                  </div>
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-slate-500 font-medium">
                      Remote Fleet
                    </span>
                    <span className="font-bold text-emerald-700 bg-emerald-100 px-2 py-0.5 rounded">
                      Auto-Sync / Fallback
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* Developer Contact */}
            <div className="grid md:grid-cols-2 gap-4">
              <div className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm group">
                <div className="flex items-center gap-3 mb-4">
                  <div className="w-10 h-10 rounded-lg bg-violet-50 flex items-center justify-center text-violet-500 border border-violet-100 group-hover:bg-violet-500 group-hover:text-white transition-colors">
                    <Code size={18} />
                  </div>
                  <h3 className="text-lg font-bold text-slate-800">
                    Crédits & Développement
                  </h3>
                </div>
                <div className="space-y-3">
                  <div className="flex justify-between items-center p-3 bg-slate-50 rounded-xl border border-slate-100">
                    <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">
                      Établissement
                    </span>
                    <span className="text-xs font-black text-slate-700">
                      Organisation cliente
                    </span>
                  </div>
                  <div className="flex justify-between items-center p-3 bg-slate-50 rounded-xl border border-slate-100">
                    <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">
                      Concepteur Principal
                    </span>
                    <span className="text-xs font-black text-violet-600">
                      Ali HAMIDY
                    </span>
                  </div>
                </div>
              </div>

              <div className="bg-emerald-600 border border-emerald-500 rounded-2xl p-6 shadow-xl shadow-emerald-600/20 text-white relative overflow-hidden group">
                <div className="absolute -right-4 -bottom-4 opacity-10 group-hover:scale-110 transition-transform duration-700">
                  <Activity size={100} />
                </div>
                <div className="relative z-10">
                  <div className="flex items-center gap-3 mb-4">
                    <div className="w-10 h-10 rounded-lg bg-white/20 flex items-center justify-center backdrop-blur-md border border-white/30">
                      <Phone size={18} />
                    </div>
                    <h3 className="text-lg font-bold">Support Technique</h3>
                  </div>
                  <p className="text-emerald-100 text-xs leading-relaxed mb-4">
                    Pour toute anomalie bloquante, demande de droits
                    supplémentaires ou questions sur l'intégration du Kiosque
                    NFC.
                  </p>
                  <div className="space-y-2">
                    <a
                      href="mailto:support@nexorsys.example"
                      className="flex items-center gap-2 p-2 bg-emerald-800/40 hover:bg-emerald-800/60 rounded-lg border border-emerald-500/50 transition-colors backdrop-blur-md"
                    >
                      <Mail size={14} className="text-emerald-300" />
                      <span className="font-bold text-xs">
                        support@nexorsys.example
                      </span>
                    </a>
                    <div className="flex items-center gap-2 p-2 bg-emerald-800/40 rounded-lg border border-emerald-500/50 backdrop-blur-md">
                      <Phone size={14} className="text-emerald-300" />
                      <span className="font-bold text-xs">
                        Poste Interne : 6270
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div className="flex justify-end pt-2">
              <button
                onClick={handleExportPDF}
                className="flex items-center gap-2 px-4 py-2 bg-white hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-lg transition-all border border-slate-200 shadow-sm"
              >
                <Download size={14} /> Exporter la Fiche Système
              </button>
            </div>
          </div>
        )}

        {/* TAB 3: LEGAL & RGPD */}
        {activeTab === "legal" && (
          <div className="animate-in fade-in slide-in-from-bottom-4 duration-500">
            <div className="flex flex-col lg:flex-row gap-8">
              {/* Sticky Sidebar */}
              <div className="lg:w-80 shrink-0 print:hidden relative">
                <div className="sticky top-8 bg-white border border-slate-200 rounded-3xl p-6 shadow-sm">
                  <div className="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100">
                    <div className="w-8 h-8 rounded-lg bg-emerald-50 flex items-center justify-center text-emerald-600">
                      <Scale size={16} />
                    </div>
                    <span className="font-black text-slate-800 uppercase tracking-widest text-xs">
                      Sommaire Légal
                    </span>
                  </div>
                  <nav className="space-y-1">
                    {rgpdSections.map((s) => (
                      <a
                        key={s.id}
                        href={`#${s.id}`}
                        className="flex flex-col px-4 py-3 rounded-xl transition-all hover:bg-slate-50 border border-transparent hover:border-slate-100 text-slate-600 hover:text-emerald-600"
                      >
                        <span className="text-[10px] font-bold opacity-50 mb-0.5">
                          {s.title.split(".")[0]}
                        </span>
                        <span className="text-xs font-bold">
                          {s.title.split(". ")[1]}
                        </span>
                      </a>
                    ))}
                  </nav>
                </div>
              </div>

              {/* Main Content */}
              <div className="flex-1 bg-white border border-slate-200 rounded-3xl p-6 lg:p-8 shadow-sm print:border-none print:shadow-none print:p-0">
                <div className="border-b border-slate-200 pb-6 mb-6 flex flex-col md:flex-row md:items-end justify-between gap-4">
                  <div>
                    <div className="inline-flex items-center gap-1.5 px-2 py-0.5 bg-rose-50 text-rose-600 text-[9px] font-black uppercase tracking-widest rounded-md mb-3 border border-rose-100">
                      <Lock size={10} /> Confidentiel & Sécurisé
                    </div>
                    <h2 className="text-2xl font-black text-slate-900 tracking-tight">
                      Notice d'Information RGPD
                    </h2>
                    <p className="text-slate-500 text-sm font-medium mt-1">
                      NexorSys Identity + NexorSys Kiosk
                    </p>
                  </div>
                  <div className="bg-slate-50 p-3 rounded-xl border border-slate-100 text-right min-w-[120px]">
                    <p className="text-[9px] font-bold text-slate-400 uppercase tracking-widest mb-0.5">
                      RÉVISION
                    </p>
                    <p className="text-sm font-black text-slate-800">v2026.1</p>
                    <p className="text-[10px] text-slate-500 font-medium mt-0.5">
                      21/04/2026
                    </p>
                  </div>
                </div>

                <div className="space-y-8">
                  {rgpdSections.map((s) => (
                    <div key={s.id} id={s.id} className="scroll-mt-32">
                      <h3 className="text-base font-black text-emerald-600 mb-3 flex items-center gap-2">
                        <span className="w-6 h-6 rounded-md bg-emerald-50 border border-emerald-100 flex items-center justify-center text-xs">
                          {s.title.split(".")[0]}
                        </span>
                        {s.title.split(". ")[1]}
                      </h3>
                      <p className="text-slate-600 text-sm leading-relaxed font-medium pl-8">
                        {s.content}
                      </p>
                    </div>
                  ))}
                </div>

                <div className="mt-10 pt-6 border-t border-slate-200 flex flex-col sm:flex-row items-center justify-between gap-4 print:hidden">
                  <div className="flex items-center gap-2 text-slate-400 text-xs font-medium">
                    <FileText size={14} /> Document à usage interne
                  </div>
                  <button
                    onClick={handleExportPDF}
                    className="px-4 py-2 text-sm bg-emerald-600 hover:bg-emerald-500 text-white font-bold rounded-lg shadow-lg shadow-emerald-500/20 transition-all flex items-center gap-2"
                  >
                    <Download size={14} /> Télécharger en PDF
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 4: DECLARATIONS ANS */}
        {activeTab === "ans_declarations" && (
          <div className="animate-in fade-in slide-in-from-bottom-4 duration-500 space-y-8">
            <div className="bg-white border border-slate-200 rounded-3xl p-6 shadow-sm flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
              <div className="max-w-2xl">
                <div className="flex items-center gap-2 mb-1">
                  <div className="p-1.5 bg-sky-50 text-sky-500 rounded-lg border border-sky-100">
                    <CheckSquare size={16} />
                  </div>
                  <h2 className="text-xl font-black text-slate-900 tracking-tight">
                    Registre des Consentements MIE
                  </h2>
                </div>
                <p className="text-slate-500 font-medium text-xs leading-relaxed">
                  Ce registre horodate les consentements des professionnels
                  ayant opté pour le badge NFC en tant que moyen
                  d'identification électronique substitutif (Ségur / ANS).
                </p>
              </div>
              <div className="flex gap-2 shrink-0">
                <button
                  onClick={handleExportPDF}
                  className="px-4 py-2 bg-white hover:bg-slate-50 border border-slate-200 text-slate-700 font-bold text-xs rounded-lg transition-all shadow-sm flex items-center gap-2"
                >
                  <Download size={14} /> Rapport PDF
                </button>
                <button
                  onClick={fetchAnsLogs}
                  className="px-4 py-2 bg-sky-500 hover:bg-sky-400 text-white font-bold text-xs rounded-lg transition-all shadow-md shadow-sky-500/20 flex items-center gap-2"
                >
                  <RefreshCcw
                    size={14}
                    className={loadingLogs ? "animate-spin" : ""}
                  />{" "}
                  Rafraîchir
                </button>
              </div>
            </div>

            {currentUser && (
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                <div className="bg-white border border-slate-200 rounded-2xl p-5 shadow-sm flex items-center gap-4 group hover:border-sky-300 transition-colors">
                  <div className="w-12 h-12 rounded-xl bg-slate-50 flex items-center justify-center text-slate-400 group-hover:bg-sky-50 group-hover:text-sky-500 transition-colors">
                    <UserIcon size={24} />
                  </div>
                  <div>
                    <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
                      Ma Session
                    </p>
                    <p className="text-sm font-bold text-slate-800">
                      {currentUser.displayName || currentUser.samAccountName}
                    </p>
                  </div>
                </div>

                <div className="bg-white border border-slate-200 rounded-2xl p-5 shadow-sm flex items-center gap-4 group hover:border-emerald-300 transition-colors">
                  <div className="w-12 h-12 rounded-xl bg-slate-50 flex items-center justify-center text-slate-400 group-hover:bg-emerald-50 group-hover:text-emerald-500 transition-colors">
                    <ShieldCheck size={24} />
                  </div>
                  <div>
                    <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
                      RPPS Actif
                    </p>
                    <p className="text-sm font-bold font-mono text-slate-800 tracking-wider">
                      {currentUser.rppsNumber || "Non renseigné"}
                    </p>
                  </div>
                </div>

                <div className="bg-white border border-slate-200 rounded-2xl p-5 shadow-sm flex items-center gap-4 group hover:border-amber-300 transition-colors">
                  <div className="w-12 h-12 rounded-xl bg-slate-50 flex items-center justify-center text-slate-400 group-hover:bg-amber-50 group-hover:text-amber-500 transition-colors">
                    <Key size={24} />
                  </div>
                  <div>
                    <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
                      Moyen Délégué
                    </p>
                    <p className="text-sm font-bold font-mono text-slate-800">
                      {currentUser.badgeUid || "Aucun badge"}
                    </p>
                  </div>
                </div>

                <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-lg flex items-center gap-4 relative overflow-hidden">
                  <div className="absolute right-0 top-0 bottom-0 w-32 bg-gradient-to-l from-emerald-500/20 to-transparent"></div>
                  <div className="w-12 h-12 rounded-xl bg-white/10 flex items-center justify-center text-emerald-400 backdrop-blur-sm border border-white/10">
                    <CheckSquare size={24} />
                  </div>
                  <div className="relative z-10">
                    <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">
                      Statut Conformité
                    </p>
                    <p
                      className={`text-sm font-bold ${currentUser.badgeUid && currentUser.rppsNumber ? "text-emerald-400" : "text-amber-400"}`}
                    >
                      {currentUser.badgeUid && currentUser.rppsNumber
                        ? "VALIDE - CONFORME"
                        : "INCOMPLET"}
                    </p>
                  </div>
                </div>
              </div>
            )}

            <div className="bg-white border border-slate-200 rounded-3xl overflow-hidden shadow-sm">
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead className="bg-slate-50 border-b border-slate-200">
                    <tr>
                      <th className="px-6 py-4 text-[10px] font-black text-slate-500 uppercase tracking-widest">
                        Horodatage
                      </th>
                      <th className="px-6 py-4 text-[10px] font-black text-slate-500 uppercase tracking-widest">
                        Professionnel
                      </th>
                      <th className="px-6 py-4 text-[10px] font-black text-slate-500 uppercase tracking-widest">
                        N° RPPS
                      </th>
                      <th className="px-6 py-4 text-[10px] font-black text-slate-500 uppercase tracking-widest">
                        Procédure
                      </th>
                      <th className="px-6 py-4 text-[10px] font-black text-slate-500 uppercase tracking-widest">
                        Conformité ANS
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {loadingLogs ? (
                      <tr>
                        <td colSpan="5" className="px-6 py-12 text-center">
                          <div className="inline-flex items-center gap-3 text-sky-500 font-bold">
                            <RefreshCcw className="animate-spin" />{" "}
                            Synchronisation Ségur...
                          </div>
                        </td>
                      </tr>
                    ) : ansLogs.length === 0 ? (
                      <tr>
                        <td
                          colSpan="5"
                          className="px-6 py-12 text-center text-slate-500 font-medium"
                        >
                          Le registre des déclarations est vierge.
                        </td>
                      </tr>
                    ) : (
                      ansLogs.map((log) => (
                        <tr
                          key={log.id}
                          className="hover:bg-slate-50 transition-colors"
                        >
                          <td className="px-6 py-4 text-sm font-medium text-slate-600 whitespace-nowrap">
                            {new Date(log.pairedAt).toLocaleString("fr-FR", {
                              day: "2-digit",
                              month: "2-digit",
                              year: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            })}
                          </td>
                          <td className="px-6 py-4">
                            <span className="font-bold text-slate-800">
                              {log.userName}
                            </span>
                          </td>
                          <td className="px-6 py-4 font-mono text-sm text-sky-600 font-bold">
                            {log.rppsNumber || "N/A"}
                          </td>
                          <td className="px-6 py-4">
                            <span className="inline-flex items-center px-2.5 py-1 rounded-md text-[10px] font-black uppercase tracking-wider bg-slate-100 text-slate-600 border border-slate-200">
                              {log.verificationMethod}
                            </span>
                          </td>
                          <td className="px-6 py-4">
                            {log.isDeclaredToGovernment ? (
                              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg text-[10px] font-black uppercase tracking-widest bg-emerald-50 text-emerald-600 border border-emerald-200">
                                <ShieldCheck size={14} /> Transmis ANS
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg text-[10px] font-black uppercase tracking-widest bg-amber-50 text-amber-600 border border-amber-200">
                                <Server size={14} /> Local
                              </span>
                            )}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ── Detail Modal ── */}
      {selectedSection && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in duration-300">
          <div className="bg-white rounded-3xl w-full max-w-xl overflow-hidden shadow-2xl animate-in zoom-in-95 duration-300 border border-slate-200">
            <div
              className={`p-6 border-b border-slate-100 bg-gradient-to-r from-${selectedSection.color}-50 to-white relative overflow-hidden`}
            >
              <div
                className={`absolute right-0 top-0 w-32 h-32 bg-${selectedSection.color}-500/10 blur-[30px] rounded-full mix-blend-multiply`}
              ></div>
              <div className="flex items-center justify-between relative z-10">
                <div className="flex items-center gap-4">
                  <div
                    className={`w-10 h-10 rounded-xl bg-white shadow-sm border border-${selectedSection.color}-100 flex items-center justify-center text-${selectedSection.color}-500`}
                  >
                    {React.cloneElement(selectedSection.icon, { size: 20 })}
                  </div>
                  <h2 className="text-lg font-black text-slate-900">
                    {selectedSection.title}
                  </h2>
                </div>
                <button
                  onClick={() => setSelectedSection(null)}
                  className="w-8 h-8 rounded-full bg-white border border-slate-200 flex items-center justify-center text-slate-500 hover:text-rose-500 hover:border-rose-200 hover:bg-rose-50 transition-all shadow-sm"
                >
                  <X size={16} />
                </button>
              </div>
            </div>

            <div className="p-6 space-y-6">
              <p className="text-slate-600 leading-relaxed font-medium text-sm">
                {selectedSection.content}
              </p>

              <div className="space-y-3">
                <h4
                  className={`text-${selectedSection.color}-600 font-black text-[10px] uppercase tracking-[0.2em] flex items-center gap-2`}
                >
                  <div
                    className={`w-1.5 h-1.5 rounded-full bg-${selectedSection.color}-500`}
                  />
                  Points Clés & Fonctionnalités
                </h4>
                <ul className="grid gap-2">
                  {selectedSection.details.map((detail, i) => (
                    <li
                      key={i}
                      className="flex gap-3 p-3 rounded-xl bg-slate-50 border border-slate-100 items-start"
                    >
                      <div
                        className={`w-5 h-5 rounded-full bg-${selectedSection.color}-100 text-${selectedSection.color}-600 flex items-center justify-center shrink-0 font-bold text-[10px] mt-0.5`}
                      >
                        {i + 1}
                      </div>
                      <span className="text-xs font-medium text-slate-700 leading-relaxed">
                        {detail}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            <div className="p-4 border-t border-slate-100 flex justify-end bg-slate-50/50">
              <button
                onClick={() => setSelectedSection(null)}
                className={`px-6 py-2 text-sm bg-${selectedSection.color}-600 hover:bg-${selectedSection.color}-500 text-white font-bold rounded-lg transition-all shadow-md shadow-${selectedSection.color}-500/20 active:scale-95`}
              >
                Compris, fermer
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default HelpAndInfo;
