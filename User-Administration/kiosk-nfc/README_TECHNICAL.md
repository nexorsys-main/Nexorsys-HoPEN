# Documentation Technique Master : Nexorsys NFC Kiosque

## 1. Introduction du Projet
**Nexorsys NFC Kiosque** est une solution d'entreprise Windows (WPF, .NET 8) conçue pour la **Clinique La Pinède**. Elle permet une authentification double facteur (2FA) sécurisée via badge NFC physique et mot de passe Active Directory (AD) pour les terminaux cliniques.

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
- **Mode Simulation :** Permet des tests sans matériel via le panneau d'administration.

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
- **Configuration NFC :** État des lecteurs et mode simulateur.
- **Lanceur d'Applications :** Chemins vers SIGEMS, Hestia, EMED.
- **Sécurité et Kiosque :** Durée de verrouillage, expiration de session et **Lancement automatique Windows**.
- **Logs Système :** Journalisation détaillée en format JSONL pour l'audit informatique.

---

## 5. Sécurité, Signature et Déploiement

### 5.1. Signature Numérique (Certificats)
Pour satisfaire le contrôle de sécurité Windows 11 (Smart App Control), tous les fichiers (`.exe` et `.dll`) sont **signés numériquement** avec un certificat auto-signé au nom de "Clinique La Nexorsys".

### 5.2. Installation Automatisée (`Install_Application.ps1`)
Le script de déploiement d'entreprise effectue les actions suivantes :
1. **Élévation Privilèges :** Requiert les droits administrateur.
2. **Confiance de Sécurité :** Enregistre le certificat de la clinique dans le magasin de confiance de Windows (Root CA).
3. **Installation Propre :** Crée `C:\Program Files\Nexorsys NFC Kiosque`.
4. **Raccourcis :** Génère les icônes officielles sur le Bureau et le Menu Démarrer.

### 5.3. Mode Kiosque (Persistent)
L'activation du "Démarrage Automatique" dans les réglages de sécurité enregistre l'application dans la clé de registre `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, garantissant que le kiosque se relance seul après chaque redémarrage du serveur.

---

## 6. Maintenance et Support
- **Fichiers Logs :** Situés dans le sous-dossier `./Journaux/`.
- **Identité Visuelle :** L’interface utilise l’identité NexorSys ; le frontend web référence les SVG officiels `logo.svg` et `favicon.svg`. Le Kiosk natif utilise son libellé NexorSys et l’icône système du tray lorsque le format SVG n’est pas disponible.
- **Évolutivité :** Le code est prêt pour l'ajout de nouveaux lecteurs ou de nouveaux applicatifs métier via simple configuration XML/JSON.

---
**Document rédigé par Ali HAMIDY**
**Clinique La Pinède - Version Production 1.0**
