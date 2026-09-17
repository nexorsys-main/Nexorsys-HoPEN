# Documentation Technique Master : Pinede NFC Kiosque

## 1. Introduction du Projet
**Pinede NFC Kiosque** est une solution d'entreprise Windows (WPF, .NET 8) conçue pour la **Clinique La Pinède**. Elle permet une authentification double facteur (2FA) sécurisée via badge NFC physique et mot de passe Active Directory (AD) pour les terminaux cliniques.

### Objectifs Clés
- Sécuriser l'accès aux terminaux partagés en milieu hospitalier.
- Simplifier le login utilisateur par un simple passage de badge (Mifare / ISO14443).
- Isoler l'accès 2FA via des groupes AD spécifiques tanpa impact sur le reste de l'infrastructure.
- Assurer une identité visuelle premium aux couleurs de la clinique.

---

## 2. Architecture Technique
L'application repose sur une architecture moderne et modulaire :
- **Framework :** .NET 8.0 Windows (WPF).
- **Pattern :** MVVM (Model-View-ViewModel) via `CommunityToolkit.Mvvm`.
- **Injection de Dépendances :** Gestion centralisée via `Microsoft.Extensions.Hosting`.
- **Gestion NFC :** Librairie standard `PCSC-Sharp` & `PCSC.Iso7816`.
- **Annuaire AD :** Communication LDAP/LDAPS via `System.DirectoryServices.AccountManagement`.
- **Persistance :** `appsettings.json` avec chiffrement AES pour les données sensibles.

---

## 3. Fonctionnement du Cœur (Services)

### 3.1. Service NFC (`NfcService.cs`)
Le service monitore en arrière-plan tous les lecteurs PC/SC connectés (ex: ACR122U). Il capture l'UID matériel du badge et renvoi l'événement à l'interface.
- **Support Multi-Lecteurs :** Détecte et utilise n'importe quel lecteur branché, incluant plusieurs modèles identiques en simultané.

### 3.2. Service Active Directory (`ActiveDirectoryService.cs`)
Assure la liaison sécurisée avec le serveur de domaine.
- **Lookup UID :** Recherche l'utilisateur dont l'attribut `extensionAttribute1` correspond à l'UID du badge.
- **Restriction de Groupe :** Vérifie l'appartenance à un groupe de sécurité spécifique (ex: `GroupeRestreint`) pour autoriser le 2FA.
- **Auth Sécurisée :** Effectue une liaison (Bind) LDAP pour valider le mot de passe utilisateur.

### 3.3. Service de Chiffrement (`ServiceChiffrement.cs`)
Utilise l'algorithme **AES-256** pour chiffrer le mot de passe du compte de service LDAP stocké en local. La clé de chiffrement est liée dynamiquement à l'identifiant machine pour empêcher la lecture des credentials si le fichier de conf est volé.

---

## 4. Interface d'Administration V2
Accessible uniquement lorsqu'un secret est explicitement injecté par le déploiement, elle s'organise en "Cartes métier" :
- **Configuration AD :** Serveur, Port (389/636), Utilisateur de service, DN de recherche.
- **Configuration NFC :** Diagnostics des lecteurs NFC physiques branchés.
- **Lanceur d'Applications :** Chemins vers SIGEMS, Hestia, EMED.
- **Sécurité et Kiosque :** Durée de verrouillage, expiration de session et **Lancement automatique Windows**.
- **Logs Système :** Journalisation détaillée en format JSONL pour l'audit informatique.

---

## 5. Sécurité, Signature et Déploiement

### 5.1. Signature Numérique (Certificats)
Pour satisfaire le contrôle de sécurité Windows 11 (Smart App Control), tous les fichiers (`.exe` et `.dll`) sont **signés numériquement** avec un certificat auto-signé au nom de "Clinique La Pinede".

### 5.2. Installation Automatisée (`Install_Application.ps1`)
Le script de déploiement d'entreprise effectue les actions suivantes :
1. **Élévation Privilèges :** Requiert les droits administrateur.
2. **Confiance de Sécurité :** Enregistre le certificat de la clinique dans le magasin de confiance de Windows (Root CA).
3. **Installation Propre :** Crée `C:\Program Files\Pinede NFC Kiosque`.
4. **Raccourcis :** Génère les icônes officielles sur le Bureau et le Menu Démarrer.

### 5.3. Mode Kiosque (Persistent)
L'activation du "Démarrage Automatique" dans les réglages de sécurité enregistre l'application dans la clé de registre `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, garantissant que le kiosque se relance seul après chaque redémarrage du serveur.

### 5.4. Protocole personnalisé pinedenfc:// et CORS Réseau
- **Protocole Personnalisé :** L'application s'associe au protocole système `pinedenfc://` dans la base de registre Windows (`HKEY_CLASSES_ROOT\pinedenfc`). Cela permet au tableau de bord Web d'ordonner le lancement à distance de l'application de bureau en un clic.
- **Règles CORS dynamiques :** Le backend API (`Pinede.Identity.API`) est configuré pour accepter dynamiquement les connexions provenant des sous-réseaux locaux cliniques (`192.168.1.*`), permettant aux machines de la flotte de communiquer sans restriction CORS.

### 5.5. Package d'Installation Unifié (`PinedeNFC_Setup`)
Pour assurer des déploiements 100% stables et robustes sur toute la flotte, un dossier d'installation propre est fourni :
1. **PinedeNFC_Installer.exe** : Un utilitaire d'installation graphique et case-insensible qui injecte automatiquement les paramètres réseau sans échec lié à la casse des fichiers `.json`. Il s'auto-ferme dès la fin de l'installation.
2. **PinedeIdentityTerminal.exe** : La version autonome monoposte et portable (186 MB - intègre .NET 8).
3. **Desinstaller_PinedeNFC.bat** : Script d'uninstallation automatique qui nettoie proprement les processus, dossiers, raccourcis et clés de registre.

---

## 6. Maintenance et Support
- **Fichiers Logs :** Situés dans le sous-dossier `./Journaux/`.
- **Identité Visuelle :** Icônes et logos embarqués dans les ressources (`Assets/app_icon.ico` et `Assets/logo.png`).
- **Évolutivité :** Le code est prêt pour l'ajout de nouveaux lecteurs ou de nouveaux applicatifs métier via simple configuration XML/JSON.

---

## 7. Feuille de Route Future (Planification et Perspectives)

### 7.1. Réinitialisation de PIN en Libre-Service (AD-Linked Reset)
- **Objectif :** Éviter les blocages cliniques lorsque le gestionnaire de l'application (DSI) est absent, en permettant aux praticiens de débloquer ou modifier eux-mêmes leur code PIN en cas de perte ou de blocage de badge.
- **Fonctionnement planifié :**
  1. L'utilisateur clique sur un lien **"PIN oublié / Débloquer"** disponible sur l'écran de saisie du PIN du Kiosque.
  2. L'application demande l'authentification avec les identifiants de session **Active Directory (LDAP)** de l'utilisateur (nom d'utilisateur et mot de passe Windows habituels).
  3. Le kiosque effectue une liaison (Bind) LDAP sécurisée en arrière-plan.
  4. En cas de succès, l'utilisateur est autorisé à saisir son nouveau code PIN clinique directement sur l'écran du kiosque.
  5. Le nouveau hash de PIN est envoyé à l'API (`Pinede.Identity.API`) pour persistance dans PostgreSQL, remettant à zéro le compteur de tentatives bloquées.
- **Statut :** Planifié & Conçu (en attente d'accès au serveur de domaine AD pour les tests d'intégration réels).

---
**Document rédigé par Ali HAMIDY**
**Clinique La Pinède - Version Production v1.5.1-MIE**
