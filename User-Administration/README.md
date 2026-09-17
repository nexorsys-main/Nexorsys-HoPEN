# 🏥 Pinède Identity — Plateforme IAM Clinique (Projet HOP'EN)

> **Auteur :** Ali HAMIDY — Innovera France  
> **Client :** Clinique La Pinède  
> **Programme :** HOP'EN — Hôpital Numérique Ouvert sur son Environnement  
> **Version :** 2.1.0 — Mai 2026

---

## 📋 Table des Matières

1. [Vue d'Ensemble](#vue-densemble)
2. [Architecture Technique](#architecture-technique)
3. [Stack Technologique](#stack-technologique)
4. [Structure du Workspace](#structure-du-workspace)
5. [Installation et Démarrage](#installation-et-démarrage)
6. [Configuration](#configuration)
7. [API Backend — Endpoints](#api-backend--endpoints)
8. [Fonctionnalités](#fonctionnalités)
9. [Sécurité et Conformité](#sécurité-et-conformité)
10. [Base de Données](#base-de-données)
11. [Déploiement](#déploiement)
12. [Variables d'Environnement](#variables-denvironnement)

---

## 🎯 Vue d'Ensemble

**Pinède Identity** est une plateforme IAM (Identity & Access Management) complète développée pour la **Clinique La Pinède** dans le cadre du programme national **HOP'EN**. Elle implémente une authentification forte **2FA NFC + Code PIN** conforme aux exigences PGSSI-S (Politique Générale de Sécurité des Systèmes d'Information de Santé) et prépare la clinique à la fédération d'identité via **Pro Santé Identité (PSI)**.

### Objectifs Principaux
- **Authentification forte 2FA** : Badge NFC (carte CPS/RFID) + Code PIN
- **Gestion des identités** : Synchronisation avec Active Directory (LDAP)
- **Registre MIE** : Conformité PSI — suivi des Moyens d'Identification Électronique
- **Kiosque NFC** : Accès sécurisé aux logiciels cliniques (EMED, BlueKango, SIGEMS, Hestia)
- **Audit & Traçabilité** : Journalisation complète des accès (RGPD, HOP'EN)

---

## 🏛️ Architecture Technique

```
┌─────────────────────────────────────────────────────────────────┐
│                    COUCHE PRÉSENTATION                          │
│  ┌──────────────────┐   ┌──────────────────────────────────┐   │
│  │  Admin Frontend   │   │     Kiosque NFC (WPF App)        │   │
│  │  React + Vite    │   │     Nexorsys.NFC.APP                │   │
│  │  :3005           │   │     Windows (net8.0-windows)      │   │
│  └────────┬─────────┘   └──────────────┬───────────────────┘   │
└───────────┼──────────────────────────── ┼ ───────────────────────┘
            │  HTTP/REST + JWT            │ HTTP + API Key
            │  WebSocket SignalR          │ SignalR (push)
┌───────────┼──────────────────────────── ┼ ───────────────────────┐
│                    COUCHE API                                    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │          Nexorsys.Identity.API (.NET 8)                   │    │
│  │          http://0.0.0.0:5000                            │    │
│  │                                                         │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐   │    │
│  │  │ UsersCtrl    │  │ KioskCtrl    │  │ AuthCtrl    │   │    │
│  │  │ DevicesCtrl  │  │ AuditCtrl    │  │ FederCtrl   │   │    │
│  │  │ WorkflowCtrl │  │ SettingsCtrl │  │ HealthCtrl  │   │    │
│  │  └──────────────┘  └──────────────┘  └─────────────┘   │    │
│  │                   SignalR Hub (IdentityHub)              │    │
│  └─────────────────────────────────────────────────────────┘    │
└────────────────────────────┬────────────────────────────────────┘
                             │ EF Core + Npgsql
┌────────────────────────────┼────────────────────────────────────┐
│                    COUCHE DONNÉES                                │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         PostgreSQL 18 — Base NexorSys_Dev                │   │
│  │  users · user_pins · user_devices · audit_logs           │   │
│  │  kiosk_sessions · workflows · applications               │   │
│  │  identity_providers · federation_tokens · migration_phases│   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         Active Directory / LDAP                          │   │
│  │         ldap://localhost:389                             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🛠️ Stack Technologique

### Backend
| Composant | Technologie | Version |
|-----------|-------------|---------|
| Framework | .NET / ASP.NET Core | 8.0 |
| ORM | Entity Framework Core | 8.x |
| Base de données | PostgreSQL + Npgsql | 18 |
| Authentification | JWT Bearer | — |
| Temps réel | SignalR | — |
| Hachage PIN | BCrypt.Net | — |
| Couche LDAP | System.DirectoryServices | — |
| Rate Limiting | AspNetCore.RateLimiting | — |
| Sérialisation | Newtonsoft.Json (camelCase) | — |

### Frontend Admin
| Composant | Technologie | Version |
|-----------|-------------|---------|
| Framework | React | 18 |
| Build tool | Vite | 5 |
| HTTP Client | Axios | — |
| Temps réel | @microsoft/signalr | — |
| Routing | React Router DOM | 6 |
| Styles | CSS Modules / Vanilla CSS | — |

---

## 📁 Structure du Workspace

```
User-Administration/
├── backend/
│   └── src/
│       ├── Nexorsys.Identity.API/           # Couche API — Controllers, Hubs, Program.cs
│       │   ├── Controllers/
│       │   │   ├── UsersController.cs     # CRUD utilisateurs + badge assignment → MIE auto-upsert
│       │   │   ├── KioskController.cs     # Auth kiosque NFC (identify, validate-pin, session)
│       │   │   ├── AuthController.cs      # Login JWT (local + LDAP mock)
│       │   │   ├── DevicesController.cs   # Registre MIE (GET/POST/DELETE UserDevices)
│       │   │   ├── AuditController.cs     # Journaux d'audit (RGPD)
│       │   │   ├── WorkflowController.cs  # Workflows habilitation RH
│       │   │   ├── FederationController.cs # Fédération PSI/eCPS
│       │   │   ├── SettingsController.cs  # Paramètres système
│       │   │   ├── HealthController.cs    # Health check
│       │   │   ├── SystemController.cs    # Sauvegarde/restauration DB
│       │   │   └── AnsDelegationController.cs # Délégations ANS/RPPS
│       │   ├── Hubs/
│       │   │   └── IdentityHub.cs         # SignalR — push badge detect vers admin UI
│       │   ├── BackgroundServices/
│       │   │   └── InactiveUserRevocationService.cs
│       │   ├── Filters/
│       │   │   └── ApiKeyAuth.cs          # Filtre clé API pour les kiosques
│       │   ├── Services/
│       │   │   └── ActiveKioskService.cs  # Suivi heartbeat des kiosques actifs
│       │   ├── Middlewares/
│       │   │   └── ErrorHandlingMiddleware.cs
│       │   ├── Program.cs                 # Bootstrap, DI, Rate Limiting, CORS, DB init
│       │   ├── appsettings.json
│       │   └── custom_settings.json       # Overrides locaux (connexion DB, etc.)
│       │
│       ├── Nexorsys.Identity.Core/          # Domaine — Entités et interfaces
│       │   ├── Entities.cs                # User, UserPin, UserDevice, KioskSession, AuditLog...
│       │   └── Abstractions/
│       │       ├── IKioskService.cs
│       │       ├── ILdapService.cs
│       │       ├── IAuditService.cs
│       │       └── IIdentityProvider.cs
│       │
│       └── Nexorsys.Identity.Infrastructure/ # Infrastructure — EF Core, Services
│           ├── AppDbContext.cs             # DbContext + model configuration (HasMaxLength, etc.)
│           ├── KioskService.cs             # Auth badge : IdentifyByMie → ValidatePin → StartSession
│           ├── LdapService.cs              # Intégration Active Directory
│           ├── AuditService.cs
│           ├── KioskService.cs
│           ├── Repositories/
│           │   ├── UserRepository.cs
│           │   └── IRepository.cs
│           └── Migrations/                 # EF Core migrations
│
├── frontend/
│   └── nexorsys-identity-ui/
│       ├── src/
│       │   ├── api/index.js               # Axios instance + interceptors JWT
│       │   ├── app/
│       │   │   ├── dashboard/Dashboard.jsx # Vue d'ensemble des métriques
│       │   │   ├── kiosk/Kiosk.jsx        # Kiosk Hub — registre MIE, sessions actives
│       │   │   ├── audit/AuditLogs.jsx    # Journal des accès
│       │   │   ├── workflows/Workflows.jsx # Workflows habilitation
│       │   │   └── settings/Settings.jsx  # Paramètres
│       │   ├── components/
│       │   │   ├── Layout.jsx             # Navigation principale
│       │   │   └── UserProfileModal.jsx   # Modal profil : badge NFC, PIN, MIE, permissions
│       │   └── main.jsx
│       ├── package.json
│       └── vite.config.ts
│
├── db/                                    # Scripts SQL d'initialisation
├── backups/                               # Sauvegardes automatiques PostgreSQL
├── docker-compose.yml                     # Stack complète Docker
├── Dockerfile.backend
├── Dockerfile.frontend
├── nginx.conf
├── fix_badge_uid_length.sql              # Migration manuelle VARCHAR(100→255)
├── ARCHITECTURE.md
├── DOSSIER_CONFORMITE_SECURITE.md
├── DOSSIER_DE_DEPLOIEMENT_SOHEXAWIN.md
└── DEV_GUIDE.md
```

---

## 🚀 Installation et Démarrage

### Prérequis
- **Windows 10/11** ou **Linux** (Ubuntu 22.04+)
- **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** — [download](https://nodejs.org)
- **PostgreSQL 14+** (PostgreSQL 18 recommandé)
- **Git**

### 1. Cloner le dépôt
```bash
git clone https://github.com/innovera-france/Clinique-User-Administration.git
cd Clinique-User-Administration
```

### 2. Base de Données PostgreSQL
```sql
-- Créer la base
CREATE DATABASE "NexorSys_Dev";
-- L'initialisation des tables est automatique au démarrage du backend
```

### 3. Backend (.NET 8 API)
```bash
cd backend/src/Nexorsys.Identity.API

# Configurer la connexion (ou utiliser custom_settings.json)
# Éditer appsettings.json : ConnectionStrings.DefaultConnection

dotnet restore
dotnet run
# → http://localhost:5000
```

### 4. Frontend Admin (React + Vite)
```bash
cd frontend/nexorsys-identity-ui

npm install
npm run dev
# → http://localhost:3005
```

### 5. Démarrage Docker (optionnel)
```bash
docker-compose up --build
# Backend  → :5000
# Frontend → :3005
# Postgres → :5432
```

---

## ⚙️ Configuration

### `appsettings.json` (Backend)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=NexorSys_Dev;Username=nexorsys_dev;Password=<from-secret-provider>"
  },
  "Jwt": {
    "Key": "votre-clé-secrète-jwt-minimum-32-caractères",
    "Issuer": "NexorSysIdentity",
    "Audience": "NexorsysUsers"
  },
  "Ldap": {
    "Url": "ldap://votre-ad-server:389",
    "BaseDn": "DC=nexorsys,DC=local",
    "ServiceAccount": "CN=svc_nexorsys,OU=ServiceAccounts,DC=nexorsys,DC=local",
    "ServicePassword": "mot-de-passe-ad"
  },
  "Auth": {
    "MockMode": true
  },
  "KioskApiKey": "votre-clé-api-kiosque"
}
```

### `custom_settings.json` (Overrides locaux — non versionné)
```json
{
  "dbHost": "localhost",
  "dbPort": "5432",
  "dbName": "NexorSys_Dev",
  "dbUsername": "postgres",
  "dbPassword": null
}
```

Les secrets de connexion doivent être injectés par le fournisseur de secrets de l’environnement ; ils ne doivent pas être écrits dans ce fichier.

---

## 📡 API Backend — Endpoints

### Authentification
| Méthode | Route | Description |
|---------|-------|-------------|
| `POST` | `/api/auth/login` | Login JWT (local + LDAP) |
| `POST` | `/api/auth/refresh` | Renouvellement token |

### Utilisateurs
| Méthode | Route | Description |
|---------|-------|-------------|
| `GET` | `/api/users` | Liste paginée |
| `GET` | `/api/users/me` | Profil utilisateur connecté |
| `GET` | `/api/users/id/{guid}` | Utilisateur par ID |
| `GET` | `/api/users/search` | Recherche (local + AD) |
| `POST` | `/api/users` | Créer utilisateur local |
| `PUT` | `/api/users/{id}` | Mettre à jour + **auto-upsert MIE** si badge modifié |
| `DELETE` | `/api/users/{id}` | Supprimer (SUPERADMIN uniquement) |
| `POST` | `/api/users/sync/{sam}` | Synchroniser depuis Active Directory |

### Kiosque NFC *(clé API requise)*
| Méthode | Route | Description |
|---------|-------|-------------|
| `POST` | `/api/kiosk/identify` | Identifier un badge NFC |
| `POST` | `/api/kiosk/identify-mie` | Identifier un MIE (badge, CPS, eCPS...) |
| `POST` | `/api/kiosk/validate-pin` | Valider le PIN + démarrer la session |
| `POST` | `/api/kiosk/end-session` | Terminer la session (retrait carte) |
| `POST` | `/api/kiosk/assign-badge` | Assigner un badge à un utilisateur |
| `POST` | `/api/kiosk/revoke-badge` | Révoquer un badge |
| `POST` | `/api/kiosk/reset-pin` | Réinitialiser le PIN |
| `POST` | `/api/kiosk/heartbeat` | Ping kiosque → mise à jour statut |
| `GET` | `/api/kiosk/reader-status` | Statut du lecteur NFC *(no rate limit)* |
| `GET` | `/api/kiosk/active-kiosks` | Liste des kiosques actifs |

### Registre MIE (Devices)
| Méthode | Route | Description |
|---------|-------|-------------|
| `GET` | `/api/devices/user/{userId}` | Devices d'un utilisateur |
| `POST` | `/api/devices` | Enregistrer un nouveau MIE |
| `DELETE` | `/api/devices/{id}` | Supprimer un MIE |

### Audit & Workflows
| Méthode | Route | Description |
|---------|-------|-------------|
| `GET` | `/api/audit` | Journal d'audit |
| `GET` | `/api/workflows` | Workflows habilitation |
| `PUT` | `/api/workflows/{id}` | Approuver/refuser un workflow |

### SignalR Hub
| Endpoint | Événement | Description |
|----------|-----------|-------------|
| `ws://localhost:5000/hubs/identity` | `OnHardwareDetected` | Badge détecté par un kiosque → push vers UI admin |
| | `OnSessionStarted` | Session kiosque démarrée |
| | `OnSessionEnded` | Session terminée (retrait carte) |

---

## 🔧 Fonctionnalités

### 1. Authentification 2FA NFC
- Le kiosque NFC lit le badge → extrait l'identifiant unique (CERT-, CONT-, KEY- selon middleware)
- Appel `POST /api/kiosk/identify-mie` → retrouve l'utilisateur
- L'utilisateur saisit son code PIN à 4-6 chiffres
- Appel `POST /api/kiosk/validate-pin` → hash BCrypt + vérification badge/PIN liés
- Session créée → accès aux applications cliniques

### 2. Enrollment Badge (Admin Portal)
- L'admin ouvre le profil utilisateur dans l'interface web
- Optionnel : Clique "Créer & Écrire UID" pour générer un identifiant CUID de 4 octets et l'écrire directement sur le Block 0 d'un badge vierge (Magic Gen 2) via le lecteur du kiosque connecté.
- Clique "Détecter" → SignalR push l'UID capturé par le kiosque en temps réel
- Sauvegarde → `PUT /api/users/{id}` :
  - Met à jour `User.BadgeUid`
  - **Auto-upsert** `UserDevice` (MIE) → visible dans Kiosk Hub
  - **Sync** `UserPin.BadgeUid` → auth kiosque immédiate sans recréer le PIN

### 3. Registre MIE (Conformité PSI)
- Suit tous les dispositifs d'identification : `NFC_Badge`, `CPS`, `eCPS`, `FIDO2`, `CartePS`
- Niveaux d'assurance : Standard / Intermediate / High / Enhanced
- Certification ANS/PSI avec référence de certification
- Statuts : `active`, `suspended`, `revoked`, `expired`, `pending_certification`

### 4. Workflows d'Habilitation RH
- Demande de droits logiciels par un responsable
- Circuit de validation RH → DSI
- Notifications en temps réel via SignalR
- Journalisation RGPD de chaque étape

### 5. Federation Layer PSI (Phase 2+)
- Infrastructure prête pour Pro Santé Identité
- Phases de migration : `Phase1_Operational` → `Phase4_FullFederation`
- Fournisseurs : NFC, ActiveDirectory, PSI, eCPS, OAuth/OIDC

### 6. Gestion Sécurisée des Administrateurs
- Génération automatique de mots de passe forts (15 caractères) pour les profils `Administrateur` et `Super Administrateur`.
- Hachage cryptographique côté serveur (BCrypt).
- Optimisation des politiques d'accès internes (`RhOrAdmin`, `AdminOnly`) pour un support multi-casse des rôles.

---

## 🔐 Sécurité et Conformité

### Mesures Implémentées
| Mesure | Détail |
|--------|--------|
| **Authentification JWT** | Tokens HS256, expiration configurable |
| **Hachage BCrypt** | Codes PIN + mots de passe |
| **Rate Limiting global** | 500 req/min/IP (réseau interne clinique) |
| **Rate Limiting Auth** | 100 req/min (AuthLimiter) |
| **Headers sécurité** | CSP, X-Frame-Options, HSTS, X-XSS-Protection |
| **CORS restreint** | Origins whitelistées explicitement |
| **API Key kiosque** | Endpoints kiosk protégés par clé secrète |
| **Audit trail** | Toutes les actions tracées avec IP + UserAgent |
| **Lockout compte** | Blocage 5 min après 3 PIN incorrects |
| **Snake case DB** | Noms de colonnes PostgreSQL normalisés |

### Conformité
- ✅ **PGSSI-S** — Politique de sécurité des SI de santé
- ✅ **RGPD** — Journalisation, droit à l'oubli, minimisation des données
- ✅ **HOP'EN** — Authentification forte 2FA
- ✅ **ANS** — Préparation délégations RPPS
- 🔜 **PSI** — Pro Santé Identité (Phase 2)

---

## 🗄️ Base de Données

### Tables Principales
| Table | Description | Champs clés |
|-------|-------------|-------------|
| `users` | Comptes clinique + AD | `badge_uid VARCHAR(255)`, `role`, `rpps_number` |
| `user_pins` | Codes PIN actifs | `badge_uid VARCHAR(255)`, `pin_hash`, `is_active` |
| `user_devices` | Registre MIE PSI | `device_type`, `device_identifier`, `status`, `assurance_level` |
| `kiosk_sessions` | Sessions actives | `badge_uid VARCHAR(255)`, `session_token`, `expires_at` |
| `audit_logs` | Journal RGPD | `action`, `resource_type`, `ip_address` |
| `workflows` | Habilitations RH | `type`, `status`, `form_data (JSONB)` |
| `applications` | Apps cliniques | `client_id`, `redirect_uris` |
| `identity_providers` | Fournisseurs PSI | `provider_type`, `health_status` |

### Migration Manuelle
```bash
# Étendre les colonnes badge_uid après upgrade des identifiants NFC
psql -U postgres -d NexorSys_Dev -f fix_badge_uid_length.sql
```

---

## 🐳 Déploiement

### Docker Compose
```bash
# Copier et configurer les variables d'environnement
cp .env.example .env
# Éditer .env avec les vraies valeurs

docker-compose up -d
```

### Variables `.env`
```env
POSTGRES_DB=NexorSys_Dev
POSTGRES_USER=postgres
POSTGRES_PASSWORD=VotreMotDePasse
JWT_KEY=VotreCleSecreteJWT
KIOSK_API_KEY=VotreCleKiosque
```

### Serveur Sohexawin
Voir `DOSSIER_DE_DEPLOIEMENT_SOHEXAWIN.md` pour les instructions de déploiement sur l'infrastructure de la clinique.

---

## 📅 Mises à jour récentes (Juin 2026)

- **Optimisation de la synchronisation Active Directory** : Le "Health Check" LDAP teste désormais la connectivité via le compte de service dédié, éliminant les erreurs de Timeout (`ERR_EMPTY_RESPONSE`) qui survenaient lors d'une recherche globale sur l'annuaire.
- **Paramètres dynamiques MIE & LDAP** : Correction de l'assignation des configurations depuis `custom_settings.json` garantissant une restauration immédiate au redémarrage des conteneurs.
- **Interface Utilisateur (Dashboard)** : Remplacement du terme "Flux RH" par "Demandes en attente" pour s'adapter à la réalité opérationnelle de la clinique (sans pôle RH dédié aux accès logiciels).

---

## 📞 Compte Administrateur

Le produit ne crée aucun compte administrateur ni mot de passe par défaut. Le premier compte doit être provisionné par le processus d'installation sécurisé de l'organisation.

---

## 👤 Auteur

**Ali HAMIDY**  
*Chef de Projet IAM — Innovera France*  
Clinique La Pinède — Programme HOP'EN 2026

---

*© 2026 Innovera France / Clinique La Pinède. Tous droits réservés.*
