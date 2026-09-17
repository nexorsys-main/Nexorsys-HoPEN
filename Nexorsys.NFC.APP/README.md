# 🏥 Pinède Kiosque NFC — Application d'Authentification par Badge

> **Auteur :** Ali HAMIDY — Innovera France  
> **Client :** Clinique La Pinède  
> **Programme :** HOP'EN — Authentification Forte 2FA  
> **Version :** 1.5.1-MIE — Mai 2026  
> **Plateforme :** Windows 10/11 (WPF — net8.0-windows)

---

## 📋 Table des Matières

1. [Vue d'Ensemble](#vue-densemble)
2. [Architecture et Flux](#architecture-et-flux)
3. [Stack Technologique](#stack-technologique)
4. [Structure du Projet](#structure-du-projet)
5. [Installation et Compilation](#installation-et-compilation)
6. [Configuration](#configuration)
7. [Identification Unique des Badges — Stratégies](#identification-unique-des-badges--stratégies)
8. [Flux d'Authentification Complet](#flux-dauthentification-complet)
9. [Écrans et Navigation](#écrans-et-navigation)
10. [Intégration SignalR (Temps Réel)](#intégration-signalr-temps-réel)
11. [Sécurité](#sécurité)
12. [Dépannage](#dépannage)
13. [Publication et Déploiement](#publication-et-déploiement)

---

## 🎯 Vue d'Ensemble

Le **Kiosque NFC Pinède** est une application WPF Windows qui transforme un poste de travail clinique en terminal d'authentification sécurisé. Quand un soignant insère sa **carte CPS** (Carte de Professionnel de Santé) ou son **badge RFID** dans le lecteur, l'application :

1. **Détecte** la carte automatiquement (polling 500 ms via PC/SC)
2. **Extrait** l'identifiant unique du badge via des stratégies hardware multi-niveaux
3. **Retrouve** l'utilisateur associé dans la base Pinède Identity
4. **Demande** le code PIN à 4-6 chiffres
5. **Ouvre** une session sécurisée et lance les applications cliniques autorisées (EMED, BlueKango, SIGEMS, Hestia)
6. **Ferme** automatiquement la session et verrouille le poste au retrait de la carte

L'application est **en écoute permanente** dans le système de notification (tray), invisible jusqu'à l'insertion d'une carte.

---

## 🏛️ Architecture et Flux

```
┌─────────────────────────────────────────────────────────────────┐
│              POSTE CLINIQUE WINDOWS                             │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         Pinede.NFC.APP (WPF — System Tray)               │   │
│  │                                                           │   │
│  │  ┌─────────────┐   ┌──────────────┐   ┌──────────────┐  │   │
│  │  │ NfcService  │   │  WinScard    │   │ SignalR      │  │   │
│  │  │ (PC/SC poll)│   │  Helper      │   │ BridgeService│  │   │
│  │  │ 500ms timer │   │  (CNG/CRYPT) │   │ (push→admin) │  │   │
│  │  └──────┬──────┘   └──────┬───────┘   └──────┬───────┘  │   │
│  │         │                 │                   │          │   │
│  │  BadgeDetecte event       │           OnHardwareDetected  │   │
│  │         │                 │                   │          │   │
│  │  ┌──────▼──────────────────────────────────────────────┐ │   │
│  │  │           EcranAttenteVueModele                     │ │   │
│  │  │    → SetForegroundWindow() → Navigation             │ │   │
│  │  └──────────────────────┬──────────────────────────────┘ │   │
│  │                         │                                │   │
│  │  ┌──────────────────────▼──────────────────────────────┐ │   │
│  │  │       EcranAuthentificationVueModele                │ │   │
│  │  │  Affiche: Nom agent, Photo, Champ PIN               │ │   │
│  │  └──────────────────────┬──────────────────────────────┘ │   │
│  └─────────────────────────┼──────────────────────────────┘   │
│                             │ HTTP + API Key                   │
└─────────────────────────────┼───────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│              Pinede.Identity.API (Backend)                      │
│     POST /api/kiosk/identify-mie                                │
│     POST /api/kiosk/validate-pin                                │
│     POST /api/kiosk/heartbeat                                   │
│     GET  /api/kiosk/reader-status                               │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🛠️ Stack Technologique

| Composant | Technologie | Version |
|-----------|-------------|---------|
| Framework UI | WPF (.NET) | net8.0-windows |
| Pattern | MVVM (CommunityToolkit.Mvvm) | 8.4.2 |
| Lecteur carte | PCSC / PCSC.Iso7816 | 7.0.1 |
| Identification badge | Windows CNG (NCrypt) + CryptoAPI + CertStore | Win32 P/Invoke |
| Temps réel | Microsoft.AspNetCore.SignalR.Client | 10.0.8 |
| DI / Hosting | Microsoft.Extensions.Hosting | 10.0.5 |
| LDAP | System.DirectoryServices.AccountManagement | 10.0.5 |
| APIs Windows | winscard.dll, ncrypt.dll, crypt32.dll, advapi32.dll | P/Invoke |

---

## 📁 Structure du Projet

```
Pinede.NFC.APP/
├── App.xaml / App.xaml.cs          # Point d'entrée, DI, TrayIcon, handlers globaux
├── NativeMethods.cs                # P/Invoke : SetForegroundWindow, ShowWindow
├── appsettings.json                # Configuration (URL API, lecteur NFC, sécurité)
│
├── Modeles/
│   ├── ParametresApplication.cs   # Binding configuration strongly-typed
│   └── ManagedApp.cs              # Modèle application clinique (EMED, BlueKango...)
│
├── Services/
│   ├── INfcService.cs             # Interface NFC : DemarrerEcoute, BadgeDetecte event
│   ├── NfcService.cs              # ★ Implémentation NFC multi-stratégies
│   │                              #   - Polling PC/SC (500ms)
│   │                              #   - Identification multi-niveaux (voir ci-dessous)
│   │                              #   - Gestion retrait carte → déconnexion session
│   ├── WinScardHelper.cs          # ★ Extraction UID bas niveau
│   │                              #   - TryDirectBinaryRead (CPLC + ReadBinary APDU)
│   │                              #   - TryReaderScopedCsp (CryptoAPI PP_UNIQUE_CONTAINER)
│   │                              #   - TryNcryptScoped (Windows CNG NCrypt KSP)
│   │                              #   - TryCertStoreFilteredByReader (CertStore CRYPT_KEY_PROV_INFO)
│   ├── ApiClientService.cs        # Client HTTP vers Pinede.Identity.API
│   ├── SignalRBridgeService.cs    # Push badge detect → portail admin (temps réel)
│   ├── INavigationService.cs
│   ├── NavigationService.cs       # Navigation MVVM entre écrans
│   ├── ITrayIconService.cs
│   ├── TrayIconService.cs         # Icône système de notification
│   ├── IJournalisationService.cs
│   ├── JournalisationService.cs   # Logs événements kiosque → backend
│   ├── IConfigurationService.cs
│   └── ConfigurationService.cs    # Lecture appsettings.json
│
├── VueModeles/
│   ├── FenetrePrincipaleVueModele.cs       # Shell de navigation
│   ├── EcranAttenteVueModele.cs            # ★ Écoute badges, gère SetForegroundWindow
│   ├── EcranAuthentificationVueModele.cs   # Saisie PIN, validation API, ouverture session
│   ├── EcranChangementPinVueModele.cs      # Changement PIN forcé (MustChangePin)
│   ├── EcranAccueilVueModele.cs            # Menu applications après authentification
│   ├── EcranAdminAuthVueModele.cs          # Auth admin (paramètres kiosque)
│   ├── EcranParametresVueModele.cs         # Paramètres système
│   ├── EcranDiagnosticVueModele.cs         # Diagnostic matériel lecteur NFC
│   └── EcranErreurVueModele.cs             # Écran d'erreur
│
├── Vues/
│   ├── FenetrePrincipale.xaml(.cs)         # Fenêtre hôte principale
│   ├── EcranAttente.xaml                   # "Approchez votre badge..."
│   ├── EcranAuthentification.xaml          # Saisie PIN
│   ├── EcranChangementPin.xaml             # Changement PIN
│   ├── EcranAccueil.xaml                   # Lanceur d'applications
│   ├── EcranAdminAuth.xaml                 # Auth admin
│   ├── EcranParametres.xaml                # Paramètres
│   ├── EcranDiagnostic.xaml                # Diagnostic
│   └── EcranErreur.xaml                    # Erreur
│
├── Converters/
│   ├── InverseBooleanConverter.cs
│   └── StringToVisibilityConverter.cs
│
├── Styles/                                 # ResourceDictionaries XAML
│   └── GlobalStyles.xaml                   # Design system clinique
│
├── Assets/
│   ├── logo.png
│   └── app_icon.ico
│
├── Publish_Portable/                       # Build publiée (gitignored — exe trop grand)
│   └── appsettings.json                   # Config de production
│
└── Scripts utilitaires
    ├── Sign-App.ps1                        # Signature de code (certificat Innovera)
    ├── Trust-Cert.ps1                      # Ajout certificat en confiance système
    ├── Fix-Security.ps1                    # Réparation des politiques de sécurité
    └── Publish_Portable.bat                # Build portable monoposte
```

---

## 🚀 Installation et Compilation

### Prérequis
- **Windows 10 version 1903+** ou **Windows 11**
- **.NET 8 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Lecteur de carte PC/SC** compatible Windows (Gemalto IDBridge CT30, ACS ACR122U, etc.)
- **Middleware carte** : Gemalto MiniDriver ou équivalent installé sur le poste
- Accès réseau vers `Pinede.Identity.API` (port 5000)

### Compilation et Lancement (développement)
```powershell
# Cloner le dépôt
git clone https://github.com/innovera-france/Clinique-Pinede-NFC-APP.git
cd Clinique-Pinede-NFC-APP

# Compiler
dotnet build

# Lancer
dotnet run
# ou
.\bin\Debug\net8.0-windows\Pinede.NFC.APP.exe
```

### Publication Portable (production)
```powershell
# Build self-contained Windows x64
.\Publish_Portable.bat
# → Génère Publish_Portable\Pinede.NFC.APP.exe (standalone, ~175 MB)
```

---

## ⚙️ Configuration

### `appsettings.json`
```json
{
  "ParametresApplication": {
    "Api": {
      "BaseUrl": "http://SERVEUR_BACKEND:5000/api/",
      "ApiKey": "votre-cle-api-kiosque"
    },
    "Nfc": {
      "TypeLecteur": "Auto"
    },
    "Applications": {
      "CheminEmed": "https://emed.clinique.local",
      "CheminHestia": "C:\\Program Files\\Hestia\\Hestia.exe",
      "CheminSigems": "C:\\Program Files\\SIGEMS\\SIGEMS.exe",
      "CheminBlueKango": "https://bluekango.clinique.local"
    },
    "Securite": {
      "MaxTentatives": 3,
      "DureeBlocageMinutes": 5,
      "DureeExpirationSessionMinutes": 15,
      "VerrouillageAuto": true,
      "LancementAutomatique": false
    },
    "MotDePasseAdmin": ""
  }
}
```

| Paramètre | Description |
|-----------|-------------|
| `Api.BaseUrl` | URL de l'API backend Pinède Identity |
| `Api.ApiKey` | Clé secrète partagée entre kiosque et API |
| `Nfc.TypeLecteur` | `Auto` détecte le premier lecteur disponible |
| `Securite.MaxTentatives` | Blocage du compte après N PIN incorrects |
| `Securite.DureeExpirationSessionMinutes` | Timeout de session inactive |

---

## 🔬 Identification Unique des Badges — Stratégies

> **Problème :** Les cartes CPS/RFID cliniques utilisent le middleware propriétaire Gemalto (GemXpresso) qui bloque les APDU ISO 7816 standard. L'ATR (Answer To Reset) est identique pour des cartes du même type → collisions.

### Stratégies Implémentées (par priorité dans `WinScardHelper.cs`)

#### 1. DirectBinary — CPLC + ReadBinary APDU
```
SELECT AID (PKCS#15) → GET DATA (CPLC) → READ BINARY
→ Retourne: CPLC-{ICCSerialnumber_hex}
```
Fonctionne si le minidriver autorise les APDU directs (cartes RFID standard).

#### 2. ReaderScopedCsp — CryptoAPI PP_UNIQUE_CONTAINER
```
CryptAcquireContext(pszProvider, "\\.\\{ReaderName}\")
→ CryptGetProvParam(PP_UNIQUE_CONTAINER)
→ Retourne: CSP-{CONTAINER_NAME}
```
Lit le nom du conteneur unique enregistré par le CSP Windows pour cette carte physique.

#### 3. NcryptScoped — Windows CNG NCrypt KSP
```
NCryptOpenStorageProvider(KSP scopé "\\.\\{ReaderName}\")
→ NCryptEnumKeys()  [premier résultat]
→ Retourne: KEY-{KeyName}
```
Enumère les clés CNG directement dans le slot du lecteur — contourne GemXpresso.

#### 4. CertStoreFilteredByReader — Certificate Store + CRYPT_KEY_PROV_INFO
```
CertOpenSystemStore("MY")
→ Filtrer les certificats dont CRYPT_KEY_PROV_INFO.pwszProviderName == fournisseur Gemalto
→ Extraire ContainerName (GUID unique par carte physique)
→ Retourne: CERT-{SHA1Thumbprint} ou CONT-{ContainerName}
```
La stratégie la plus fiable pour les cartes CPS Gemalto — le container name est un GUID unique même si le certificat est partagé entre cards de même type.

#### 5. Encodage Direct Block 0 (CUID / Magic Gen 2)
```
SELECT KEY (FF FF FF FF FF FF) → AUTH Block 0 → READ Block 0
→ Modify bytes 0-3 (UID) & 4 (BCC) → WRITE Block 0
```
Permet de générer aléatoirement un UID de 4 octets et de l'écrire de manière persistante dans le secteur 0 d'une carte "Magic Gen 2" vierge. Utilisé lors de la création d'un badge dans l'administration.

#### 6. Fallback — ATR avec préfixe collision
```
→ Retourne: UNSECURE-ATR-COLLISION-{ATR_hex}
⚠️ Ne pas utiliser pour identifier des utilisateurs différents
```

### Normalisation des UID
```csharp
// Les séparateurs (: - espaces) sont ignorés lors de la comparaison
"CERT-A1B2:C3D4" == "CERT-A1B2C3D4" == "cert-a1b2c3d4"
```

---

## 🔄 Flux d'Authentification Complet

```
Insertion carte
      │
      ▼
NfcService.ScannerUnBadge()
  ├── Stratégie 1: TryDirectBinaryRead → UID CPLC
  ├── Stratégie 2: TryReaderScopedCsp → UID CSP container
  ├── Stratégie 3: TryNcryptScoped    → UID CNG key
  ├── Stratégie 4: TryCertStoreFilteredByReader → UID cert store
  └── Fallback: ATR (collision warning)
      │
      ▼
BadgeDetecte?.Invoke(this, finalUid)
      │
      ├──→ SignalRBridgeService → push "OnHardwareDetected" → Admin Portal
      │
      ▼
EcranAttenteVueModele.SurBadgeDetecte()
  SetForegroundWindow(hwnd)  ← force la fenêtre au premier plan
  ShowWindow(SW_RESTORE)
      │
      ▼
POST /api/kiosk/identify-mie { Identifier, Type: "NFC_Badge" }
  Backend:
    1. Cherche dans UserDevices (registre MIE)
    2. Fallback: User.BadgeUid
    3. Fallback: LDAP
      │
      ▼
EcranAuthentification
  - Affiche: NomAgent, Photo (si disponible)
  - Saisie PIN (masqué, 4-6 chiffres)
      │
      ▼
POST /api/kiosk/validate-pin { UserId, Pin, BadgeUid }
  Backend:
    1. Vérification badge = UserPin.BadgeUid (sécurité)
    2. BCrypt.Verify(pin, pinHash)
    3. Lockout si 3 échecs
    4. StartSessionAsync → KioskSession créée
      │
      ▼
EcranAccueil
  - Liste des applications autorisées
  - Ouverture: EMED / BlueKango / SIGEMS / Hestia

Retrait carte
      │
      ▼
POST /api/kiosk/end-session
Fenêtre minimisée → System Tray
```

---

## 📱 Écrans et Navigation

| Écran | Vue Modèle | Description |
|-------|-----------|-------------|
| **Attente** | `EcranAttenteVueModele` | "Approchez votre badge" — idle screen |
| **Authentification** | `EcranAuthentificationVueModele` | Saisie PIN + info agent |
| **Changement PIN** | `EcranChangementPinVueModele` | Forcé si `MustChangePin = true` |
| **Accueil** | `EcranAccueilVueModele` | Lanceur d'applications cliniques |
| **Erreur** | `EcranErreurVueModele` | Erreur lecteur / réseau / auth |
| **Admin Auth** | `EcranAdminAuthVueModele` | Accès paramètres kiosque |
| **Paramètres** | `EcranParametresVueModele` | Config URL API, clé, etc. |
| **Diagnostic** | `EcranDiagnosticVueModele` | Test lecteur, APDU, stratégies UID |

---

## 📡 Intégration SignalR (Temps Réel)

Le `SignalRBridgeService` connecte le kiosque au **portail d'administration** en temps réel :

```csharp
// Quand un badge est détecté, push immédiat vers le portail admin
_nfcService.BadgeDetecte += (s, uid) => _ = NotifyHardwareDetected(uid, "NFC");

// L'admin voit instantanément le badge dans le UserProfileModal
// et peut l'assigner d'un clic
```

**Hub :** `ws://BACKEND:5000/hubs/identity`  
**Événement :** `OnHardwareDetected` → `{ identifier, type, timestamp }`

---

## 🔐 Sécurité

| Mesure | Implémentation |
|--------|---------------|
| **Clé API** | Header `X-Api-Key` sur tous les appels kiosque |
| **HTTPS** | Recommandé en production (nginx reverse proxy) |
| **PIN masqué** | Jamais transmis en clair — BCrypt côté serveur |
| **Timeout session** | 15 min → verrouillage automatique |
| **Lockout** | 3 tentatives incorrectes → blocage 5 min |
| **Badge + PIN liés** | Le PIN n'est valide qu'avec le badge associé |
| **Session unique** | Retrait carte → `end-session` immédiat |
| **SetForegroundWindow** | Fenêtre toujours visible — impossible d'utiliser le poste sans s'authentifier |

---

## 🔧 Dépannage

### La fenêtre ne s'ouvre pas quand j'insère la carte
1. Vérifier que l'appli tourne dans le tray (icône dans la zone de notification)
2. Vérifier la connexion au backend : `http://localhost:5000/api/health`
3. Vérifier les logs de l'appli (`crash_log.txt` à la racine)

### Badge non reconnu / "Identité non trouvée"
1. Aller dans **Écran Diagnostic** (Paramètres → Diagnostic)
2. Vérifier quelle stratégie UID est retournée
3. S'assurer que ce même UID est enregistré dans le portail admin (profil utilisateur → champ Badge)
4. Si stratégie = `UNSECURE-ATR-COLLISION-...` → le middleware Gemalto bloque les APDU ; vérifier l'installation du minidriver

### Erreur `429 Too Many Requests`
- Le rate limiter global est à 500 req/min — si dépassé, vérifier d'autres processus qui appellent l'API
- L'endpoint `reader-status` est exempté du rate limiter (`[DisableRateLimiting]`)

### Erreur PIN incorrect alors que le PIN est bon
- Vérifier que le `UserPin.BadgeUid` en base correspond à l'UID actuel du badge
- Aller dans le portail admin → profil utilisateur → re-sauvegarder le badge → le PIN est auto-synchronisé

### Middleware Gemalto — Erreur `6A86`
- L'erreur `6A86` (Wrong parameters P1-P2) est normale avec GemXpresso
- Le `NfcService` passe automatiquement aux stratégies CNG/CertStore qui ne requièrent pas d'APDU

---

## 📦 Publication et Déploiement

### Déploiement Simplifié et Unifié (Recommandé)
Le dossier d'installation unifié se trouve dans :
📂 [d:\Innovera\Projects\Web-app\Clinique\PinedeNFC_Setup](file:///d:/Innovera/Projects/Web-app/Clinique/PinedeNFC_Setup)

Il contient :
1. **PinedeNFC_Installer.exe** : L'assistant d'installation graphique. Il extrait automatiquement les configurations, enregistre le protocole `pinedenfc://` dans la base de registre et configure le démarrage automatique Windows.
2. **PinedeIdentityTerminal.exe** : L'application NFC Kiosque sous forme d'exécutable portable et autonome (186 MB).
3. **appsettings.json** : Les paramètres réseau et sécurité de l'application pré-configurés pour la clinique (`192.168.1.60`).
4. **Desinstaller_PinedeNFC.bat** : Script de désinstallation automatique en un clic (à exécuter en tant qu'administrateur).

### Procédure d'installation client
1. Copier le dossier `PinedeNFC_Setup` sur une clé USB ou un partage réseau.
2. Lancer `PinedeNFC_Installer.exe` en mode administrateur.
3. Cliquer sur **"Installer l'application"**. Le programme se ferme automatiquement dès que l'opération réussit.

### Compilation Manuelle (Développement)
Pour régénérer les binaires portables :
```powershell
# Publication autonome Windows x64 sous forme de fichier unique
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o Publish_Portable
```

### Signature du Code (Certificat Innovera)
```powershell
.\Sign-App.ps1        # Signe l'exécutable avec le certificat Innovera
.\Trust-Cert.ps1      # Ajoute le certificat en confiance système
```

---

## 👤 Auteur

**Ali HAMIDY**  
*Chef de Projet IAM — Innovera France*  
Clinique La Pinède — Programme HOP'EN 2026

---

*© 2026 Innovera France / Clinique La Pinède. Tous droits réservés.*
