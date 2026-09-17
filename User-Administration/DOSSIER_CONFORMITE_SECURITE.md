# Dossier de Conformité Sécurité & Réglementaire - Détails Techniques
**Projet : Pinède Identity Platform & Kiosque NFC**
**Version : 1.1 (Audit de Qualification Approfondi)**

## 1. Introduction & Périmètre
Ce document détaille les implémentations techniques garantissant la conformité de la solution **Pinède Identity** aux exigences de l'ANS (Agence du Numérique en Santé), de la **PGSSI-S** (v2.1) et de la réglementation européenne **eIDAS**. La solution assure la gestion de l'identité numérique et l'accès sécurisé aux applications cliniques (Emed, Sigems, BlueKango) via des kiosques partagés.

## 2. Architecture d'Authentification Forte (MFA/2FA)
Conformément au référentiel eIDAS pour un niveau de garantie **"Substantiel"**, l'authentification repose sur deux facteurs indépendants :

*   **Facteur 1 (Possession)** : Un badge physique NFC (Mifare/Desfire). L'application `Nexorsys.NFC.APP` utilise la pile **PCSC** pour lire l'UID matériel unique. Ce facteur est validé par le backend via une vérification d'appairage en base de données PostgreSQL.
*   **Facteur 2 (Connaissance)** : Un code PIN personnel. La validation est effectuée exclusivement côté serveur.
*   **Mécanisme de Liaison** : Le backend (`KioskService.cs`) réalise une jointure atomique entre le `BadgeUid` et le `UserId` pour garantir qu'un badge ne peut être associé qu'à un seul utilisateur actif à la fois (Logique de "vol de badge" gérée pour désactiver les anciens liens).

## 3. Sécurité Logique & Durcissement du Backend (.NET 8)

### 3.1 Protection contre les attaques par force brute (PROC-20)
*   **Middleware de Limitation de Débit (Rate Limiting)** : Implémentation via `Microsoft.AspNetCore.RateLimiting`. 
    *   **AuthLimiter** : Restreint à 5 tentatives de login toutes les 5 minutes par adresse IP source.
*   **Politique de Verrouillage de Compte** :
    *   Après **3 échecs** de code PIN, le champ `LockedUntil` de l'utilisateur est mis à jour à `DateTime.UtcNow + 5 minutes`.
    *   Toute tentative durant cette période renvoie un code HTTP 401 avec le message explicatif "Account locked".
    *   Un administrateur peut lever manuellement ce verrou via l'endpoint sécurisé `UnlockAccount`.

### 3.2 Cryptographie & Gestion des Secrets
*   **Hachage des PINs** : Utilisation de **BCrypt (BCrypt.Net-Next)** avec un facteur de coût (Work Factor) adaptatif. Cette méthode inclut nativement un sel (Salt) par utilisateur, rendant les attaques par tables de correspondance (Rainbow Tables) inopérantes.
*   **Gestion des Sessions** : Utilisation de jetons **JWT (JSON Web Tokens)** signés avec une clé asymétrique (ou symétrique forte 256-bit). Les jetons ont une durée de vie limitée (configurée par défaut à 15 minutes pour les kiosques) pour limiter les risques de rejeu.

## 4. Traçabilité, Audit & Non-Répudiation (PROC-21)

### 4.1 Journalisation des Événements de Sécurité
Chaque action critique est enregistrée dans la table `audit_logs` via `AuditService.cs` :
*   **Données Capturées** : Horodatage UTC, ID Utilisateur, Type d'action, Adresse IP, User-Agent, et surtout le **MachineName** (Nom du kiosque physique).
*   **Garantie d'Écriture** : Utilisation de `ExecuteSqlRawAsync` pour contourner le suivi d'état de l'ORM et garantir que les logs sont persistés immédiatement en base de données PostgreSQL, même en cas de crash applicatif partiel.

### 4.2 Appairage RPPS (ANS)
Pour répondre aux exigences du **Ségur Numérique**, la solution permet de lier l'identité locale à l'identité nationale :
*   **Stockage RPPS** : Le numéro RPPS est vérifié et stocké sur le profil utilisateur.
*   **Preuve de Consentement** : L'interface d'enrôlement (`UserProfileModal.jsx`) force l'acceptation des conditions d'utilisation ("Consentement Ségur") avant de permettre l'appairage d'un badge NFC, archivant ainsi la preuve de délégation de confiance.

## 5. Sécurité Réseau & Conformité Web (ANSSI)

L'API `Nexorsys.Identity.API` implémente les en-têtes de sécurité recommandés par l'ANSSI :
*   **Strict-Transport-Security (HSTS)** : Oblige l'utilisation du TLS pour toutes les communications.
*   **Content-Security-Policy (CSP)** : Défini sur `default-src 'none'` pour l'API afin d'interdire toute exécution de script tiers.
*   **CORS (Cross-Origin Resource Sharing)** : Strictement limité aux domaines autorisés (ex: `http://localhost:3001` pour le dashboard).
*   **X-Frame-Options: DENY** : Protection contre le Clickjacking sur les portails d'administration.

## 6. Analyse de Risques & Résilience
*   **Zéro Stockage Local** : En cas de compromission d'un terminal kiosque, aucune donnée sensible (PIN, Hash, Token) n'est stockée de façon permanente sur le disque.
*   **Isolement des Services** : Le backend gère séparément les accès AD (via LDAP sécurisé) et les identités locales, permettant une continuité de service même en cas de coupure de l'Active Directory (mode dégradé sécurisé).

## 7. Conclusion
La solution **Pinède Identity** présente un niveau de maturité conforme aux exigences les plus strictes du secteur de la santé. Le couplage entre l'authentification forte multi-facteur et la traçabilité granulaire assure une conformité totale avec les procédures **PROC-20** et **PROC-21**.
