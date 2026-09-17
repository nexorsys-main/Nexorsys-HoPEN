# Document d'Architecture et de Conception Technique (Pinède Identity & Kiosque NFC)

## 1. Vue d'Ensemble du Réseau et de l'Architecture
Le système s'articule autour de deux applications majeures interconnectées assurant la gestion centralisée des identités et l'authentification forte (2FA) sur les terminaux partagés en milieu clinique.

L'objectif principal est de conformer les postes (Kiosques) au programme HOP'EN :
- **Pinède Identity Platform** : Un backend web centralisé (.NET 8) et un Dashboard d'administration (React/Vite).
- **Pinède NFC APP** : Une application client lourd (WPF / C#) locale agissant en tant que "verrou" sur les postes Windows partagés, avec lecture de badges NFC matériels.

---

## 2. Pinède Identity Platform (Le Noyau Web)

### 2.1 Backend API (.NET 8)
Le backend sert de "Source de Vérité Unique" et gère le trafic IAM (Identity Access Management), les Journaux d'Audit, et la liaison avec l'Active Directory.

- **Stack Technique** : C# .NET 8, ASP.NET Core Web API, Entity Framework Core (EF Core).
- **Base de Données** : PostgreSQL 14+ (base fournie par la configuration de déploiement, exemple `NexorSys_Dev`).
- **Authentification & Mots de Passe** : Cryptographie BCrypt ("BCrypt.Net-Next") pour la sécurité des PINs locaux (Le système ne stocke jamais de mot de passe en clair). Émission de jetons JWT sécurisés pour l'accès aux endpoints.
- **Connecteur LDAP/AD** : Géré via `System.DirectoryServices.Protocols` (LdapService.cs) utilisant le protocole LDAP v3 (ou LDAPS). Il gère l'importation initiale des utilisateurs vers PostgreSQL, avec réplication optionnelle.

**Contrôleurs Clés :**
- `UsersController` : Recherche d'utilisateurs hybride. Cherche d'abord dans la base PostgreSQL locale via `UserRepository.SearchAsync()`, puis fait un "fallback" sur l'AD via LDAP.
- `KioskAuthController` : Gère le handshake sécurité avec les kiosques distants. Valide la correspondance `Badge UID` + `Code PIN (Hash BCrypt)`.
- `AuditController` : Fournit les journaux de traçabilité au Dashboard Frontend.

### 2.2 Base de Données (PostgreSQL)
L'Intégrité de la base de données contourne la stricte architecture JSONB de Npgsql pour permettre les requêtes natives brutes grâce à `ExecuteSqlRawAsync`.
- `users` : Métadonnées des agents, UUID de l'Active Directory.
- `user_pins` : Codes secrets 2FA (Haschés en format BCrypt avec Salt).
- `user_permissions` : Autorisations applicatives paramétrables ("BlueKango", "Emed", etc).
- `audit_logs` : Journalise les événements selon un typage fort (Succès, Echec PIN, etc.) grâce à des insertions SQL pures sans Entity Tracking, évitant l'erreur `jsonb to string Exception Npgsql`.

### 2.3 Dashboard Frontend (React + Vite)
L'interface de la Direction des Systèmes d'Information (DSI). Permet l'enrôlement et l'audit.
- **Génération** : Node.js via le bundler Vite.
- **Routage** : SPA (Single Page Application) gérant le Kiosque Hub, Tableaux de Bord, et Audit Logs.
- **Design System** : Utilisation exclusive de TailwindCSS 3.x combiné avec les icônes de la librairie "Lucide-React" pour obtenir une esthétique premium mode sombre (composants effet Glassmorphism, animations fluides).
- **Communication réseau** : Requêtes HTTPS gérées par Axios pointant vers le middleware CORS configuré du backend port 5000.

---

## 3. L'Application Pinède NFC (Client Lourd WPF)

### 3.1 Architecture Client (WPF)
Postée localement sur les terminaux médicaux "Kiosques", l'application s'affiche en mode plein écran et agit comme une couche de sécurité "Zero Trust".
- **Design Pattern** : Modèle-Vue-VueModèle (MVVM) soutenu par `CommunityToolkit.Mvvm` pour une séparation stricte entre la logique et l'interface (XAML).
- **Navigation In-App** : `NavigationService` gérant la transition sans état des contrôles utilisateurs (Accueil ➔ Authentification ➔ Applications).
- **Topologie de Stockage** : Ne possède aucune base de données locale. 100% de la vérification est intermédiée par `ApiClientService.cs` vers l'API centrale via des flux chiffrés.

### 3.2 Services Locaux et Sécurisation
- **NfcService** : Interagit avec les librairies PCSC (`PCSC` C#) pour capter périodiquement sur un processeur externe l'UID du badge physique NFC présenté (Ex: lecteur ACR122U). Offre un mode "Simulation" à des fins de TDD.
- **Journalisation Localisée** : Appliquée en tant que backup (NLog / Rolling files log) via `IJournalisationService` pour pallier à toute perte de connectique réseau avec le serveur Identity.
- **Déploiement Custom Protocole** : Implémente le point de montage Registre Windows réseau `nexorsysnfc://`. Ceci permet au Dashboard web (React) d'invoquer l'ouverture ou la configuration du client logiciel WPF directement depuis le navigateur du superviseur réseau sans lignes de commande.

---

## 4. Logiques de Connexion & Exécution des Flux (Workflows)

### Workflow d'Authentification Kiosque (2FA HOP'EN)
La clinique est paramétrée sur le standard de sécurité de Santé :
1. **Étape 1 (Facteur Matériel) - Badge** : L'Agent arrive face au poste Kiosque (qui masque Windows), et passe sa carte physique `A1B2C3D4`. Le lecteur PCSC la capte et active le `EcranAuthentificationVueModele`.
2. **Étape 2 (Facteur Mémoriel) - PIN** : L'Agent tape le code. Sur le clic, l'interface supprime immédiatement la donnée de la SecureBox pour empêcher tout "memory dump" Windows. Le paramètre est compressé dans un JSON (avec l'UID Matériel) puis expédié par `HttpClient` au Backend (POST `/api/kioskauth/badge`).
3. **Step 3 (Traitement Backend)** : 
   - Recherche du Badge (UID) sur le profil local ou via LDAP fallback.
   - Hash du code PIN reçu comparé (BCrypt Validate) au hash figurant dans `user_pins`.
   - Logging Asynchrone forcé (`ExecuteSqlRawAsync`) contournant l'ORM strict pour s'insérer en type `jsonb` de facto vers PostgreSQL `audit_logs`.
   - Délivrance d'un Jetons Signé JWT contenant le périmètre applicatif attribué.
4. **Step 4 (Déverrouillage Applicatif)** : Le client lourd WPF récupère le Token JWT et les "Applications Autorisees". L'interface "Paramètres" (Hestia / BlueKango) s'affiche à l'utilisateur, conditionnant l'accès uniquement aux modules permis par la DSI.

### Pipeline de Réinitialisation
Sur le Dashboard React, la DSI peut suspendre un droit ou réattribuer le PIN (`POST /kiosk/reset-pin`). Ceci altère instantanément le statut en base de données. Au prochain coup de badge de l'utilisateur sur le WPF, un HTTP Status 401 sera renvoyé, forçant l'agent médical à renouveler son PIN sur son téléphone ou à la DSI.
