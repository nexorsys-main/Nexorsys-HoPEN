# 🧪 Guide de Test — Nexorsys NFC Kiosque

> **Document rédigé par Ali HAMIDY — Clinique La Pinède**
> Version Production 1.0 | Mis à jour le 30/03/2026

Ce document regroupe **tout ce dont vous avez besoin** pour tester l'application
Nexorsys NFC Kiosque, que vous disposiez ou non d'un lecteur NFC physique.

---

## 📋 Table des Matières

1. [Prérequis & Lancement](#1-prérequis--lancement)
2. [Credentials & Mots de Passe](#2-credentials--mots-de-passe)
3. [Mode Simulation NFC (sans matériel)](#3-mode-simulation-nfc-sans-matériel)
4. [Scénarios de Test — Écran par Écran](#4-scénarios-de-test--écran-par-écran)
5. [Tests du Panneau d'Administration](#5-tests-du-panneau-dadministration)
6. [Tests avec Lecteur NFC Physique](#6-tests-avec-lecteur-nfc-physique)
7. [Tests de Sécurité & Verrouillage](#7-tests-de-sécurité--verrouillage)
8. [Vérification des Logs](#8-vérification-des-logs)
9. [Résolution de Problèmes Courants](#9-résolution-de-problèmes-courants)
10. [Matrice de Test Complète](#10-matrice-de-test-complète)

---

## 1. Prérequis & Lancement

### Environnement requis
| Élément | Valeur |
|---|---|
| Système d'exploitation | Windows 10 / 11 (64-bit) |
| .NET Runtime | .NET 8.0 Windows (inclus dans la distribution portable) |
| Droits requis | Utilisateur standard (admin uniquement pour l'installation) |
| Dossier de travail | `Publish_Portable\` ou `C:\Program Files\Nexorsys NFC Kiosque\` |

### Lancement de l'application

**Option A — Distribution portable (recommandé pour les tests) :**
```
Publish_Portable\Nexorsys.NFC.APP.exe
```

**Option B — Via Visual Studio (mode Debug) :**
```
F5  →  Lance en mode Debug avec logs détaillés dans la console
```

> **Note :** L'application démarre directement sur l'**Écran d'Attente** (kiosque
> prêt à recevoir un badge). Une icône apparaît dans la barre des tâches système.

---

## 2. Credentials & Mots de Passe

### 🔑 Mot de Passe Administrateur

| Champ | Valeur |
|---|---|
| **Mot de passe admin** | Aucun mot de passe par défaut |
| Configurable dans | `appsettings.json` → `MotDePasseAdmin` |
| Écran d'accès | Bouton ⚙ Administration → Saisie du mot de passe |

> Ce mot de passe protège l'accès au **Panneau de Configuration** de l'application.
> Il peut être changé à tout moment dans le fichier `appsettings.json` sans recompilation.

---

### 👤 Utilisateur de Démonstration (Mode Simulation)

Lorsque le **Mode Simulation NFC** est actif, le badge simulé UID `A1B2C3D4` est
mappé sur l'utilisateur de démonstration suivant :

| Champ | Valeur |
|---|---|
| **UID Badge simulé** | `A1B2C3D4` |
| **Identifiant (login)** | `UtilisateurDemo` |
| **Nom complet affiché** | `Dr. Jean Dupont (Démo)` |
| **Mot de passe AD** | *(n'importe lequel — validation bypassée en démo)* |

> ⚠️ Le compte `UtilisateurDemo` est codé en dur dans `ActiveDirectoryService.cs`
> et accepte **n'importe quel mot de passe** (la validation AD est bypassée).
> Il est réservé aux tests sans infrastructure AD.

---

### 🌐 Configuration Active Directory (Défauts)

Paramètres définis dans `appsettings.json` / `ParametresApplication.cs` :

| Paramètre | Valeur par défaut |
|---|---|
| Domaine | `clinique.local` |
| Serveur LDAP | `dc01.clinique.local` |
| Port LDAP | `389` (standard) / `636` (LDAPS) |
| Attribut NFC | `extensionAttribute1` |
| Groupe restreint | *(vide — aucune restriction de groupe par défaut)* |
| Utilisateur de service | *(vide — utilise le compte machine)* |

---

## 3. Mode Simulation NFC (sans matériel)

Le **Mode Simulation** permet de tester l'intégralité du flux applicatif sans
lecteur NFC physique. Il est activé par défaut dans la configuration.

### Activation du mode simulation

**Via `appsettings.json` :**
```json
{
  "ParametresApplication": {
    "ModeSimulationNfc": true
  }
}
```

**Via le Panneau d'Administration :**
1. Cliquer sur ⚙ Administration
2. Utiliser un secret administrateur injecté par le déploiement, si cette fonction est activée.
3. Onglet **NFC** → activer le commutateur **"Mode Simulation"**
4. Cliquer **Sauvegarder**

### Déclenchement d'un scan simulé

Une fois le mode simulation actif, un **bouton "Simuler un badge"** apparaît
sur l'Écran d'Attente. Un clic sur ce bouton déclenche le scan de l'UID `A1B2C3D4`.

> On peut changer l'UID simulé dans le panneau Admin → NFC → Champ "UID Simulé".

---

## 4. Scénarios de Test — Écran par Écran

### 4.1. Écran d'Attente (`EcranAttente`)

**Objectif :** L'écran de veille du kiosque, en attente d'un badge.

| # | Action | Résultat attendu |
|---|---|---|
| T01 | Lancer l'application | L'écran d'attente s'affiche avec animation. L'icône tray apparaît. |
| T02 | (Mode Simulation) Cliquer "Simuler un badge" | Navigation vers l'Écran d'Authentification avec affichage du nom "Dr. Jean Dupont (Démo)" |
| T03 | (Mode phys.) Passer un badge NFC Mifare enregistré | Navigation vers l'Écran d'Authentification avec affichage du nom AD de l'utilisateur |
| T04 | (Mode phys.) Passer un badge NFC inconnu | Navigation vers l'Écran d'Erreur : "Accès refusé — Votre badge n'est pas reconnu" |
| T05 | Cliquer ⚙ Administration | Navigation vers l'Écran de Saisie du Mot de Passe Admin |

---

### 4.2. Écran d'Authentification (`EcranAuthentification`)

**Objectif :** Saisie du mot de passe AD de l'utilisateur identifié par son badge.

| # | Action | Résultat attendu |
|---|---|---|
| T06 | Entrer n'importe quel mot de passe (démo) | Authentification réussie → Écran d'Accueil |
| T07 | Laisser le champ vide et valider | Message : "Le mot de passe ne peut pas être vide." |
| T08 | Entrer un mauvais mot de passe AD (1ère fois) | Message : "Mot de passe incorrect. Il vous reste 2 tentative(s)." |
| T09 | Entrer un mauvais mot de passe AD (2ème fois) | Message : "Mot de passe incorrect. Il vous reste 1 tentative(s)." |
| T10 | Entrer un mauvais mot de passe AD (3ème fois) | Navigation vers l'Écran d'Erreur : "Authentification bloquée — Trop de tentatives" |
| T11 | Cliquer "Annuler" | Retour à l'Écran d'Attente |

---

### 4.3. Écran d'Accueil (`EcranAccueil`)

**Objectif :** Tableau de bord post-authentification avec les lanceurs d'applications.

| # | Action | Résultat attendu |
|---|---|---|
| T12 | Authentification réussie | Écran affiché avec : nom de l'utilisateur, boutons SIGEMS / Hestia / EMED |
| T13 | Cliquer "SIGEMS" | Lance l'exécutable configuré (`CheminSigems`) |
| T14 | Cliquer "Hestia" | Lance l'exécutable configuré (`CheminHestia`) |
| T15 | Cliquer "EMED" | Ouvre l'URL configurée (`CheminEmed`) dans le navigateur |
| T16 | Attendre expiration de session (défaut : 15 min) | Retour automatique à l'Écran d'Attente |

> **Pour T13/T14/T15 :** Si les chemins ne pointent pas vers des fichiers existants,
> Windows affichera une erreur d'exécutable introuvable — ce comportement est normal.

---

### 4.4. Écran d'Erreur (`EcranErreur`)

**Objectif :** Affichage d'un message d'erreur contextuel + retour kiosque.

| # | Action | Résultat attendu |
|---|---|---|
| T17 | Badge inconnu détecté | Message "Accès refusé" avec description |
| T18 | 3 échecs de mot de passe | Message "Authentification bloquée" |
| T19 | Erreur lecteur NFC physique | Message "Problème de lecteur" avec description technique |
| T20 | Attendre le timeout ou cliquer "Retour" | Retour à l'Écran d'Attente |

---

### 4.5. Écran Auth Admin (`EcranAdminAuth`)

**Objectif :** Protège l'accès au panneau de configuration.

| # | Action | Résultat attendu |
|---|---|---|
| T21 | Saisir le secret administrateur injecté par le déploiement | Navigation vers le Panneau d'Administration |
| T22 | Saisir un mot de passe incorrect | Message d'erreur : "Mot de passe incorrect." |
| T23 | Cliquer "Annuler" | Retour à l'Écran d'Attente |

---

## 5. Tests du Panneau d'Administration

**Accès :** ⚙ Administration → secret injecté par le déploiement

### 5.1. Onglet Active Directory

| # | Action | Résultat attendu |
|---|---|---|
| T24 | Modifier le serveur LDAP et cliquer "Tester la connexion" | Affiche "✅ Connexion réseau au serveur LDAP réussie." ou "❌ Échec..." |
| T25 | Laisser un champ obligatoire vide et Sauvegarder | Indicateurs rouges + message de validation |
| T26 | Activer LDAPS → changer le port à 636 → tester | Test avec SSL/TLS |
| T27 | Renseigner un compte de service → Sauvegarder | Le mot de passe est chiffré AES-256 dans `appsettings.json` |

### 5.2. Onglet NFC

| # | Action | Résultat attendu |
|---|---|---|
| T28 | Activer/Désactiver "Mode Simulation" → Sauvegarder | Le bouton "Simuler" apparaît/disparaît sur l'Écran d'Attente |
| T29 | Modifier l'"UID Simulé" (ex: `DEADBEEF`) → Sauvegarder | Le bouton simule désormais cet UID |

### 5.3. Onglet Applications

| # | Action | Résultat attendu |
|---|---|---|
| T30 | Modifier `CheminSigems` → Sauvegarder → Lancer SIGEMS | La nouvelle application est lancée |
| T31 | Entrer un chemin invalide → Sauvegarder | Indicateur d'erreur de validation |

### 5.4. Onglet Sécurité & Kiosque

| # | Action | Résultat attendu |
|---|---|---|
| T32 | Activer "Lancement automatique" → Sauvegarder | Clé ajoutée dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| T33 | Désactiver "Lancement automatique" → Sauvegarder | Clé supprimée du registre |
| T34 | Modifier "Durée d'expiration session" | Valeur sauvegardée dans `appsettings.json` |

**Vérification registre (T32/T33) :**
```powershell
Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" | Select-Object "Nexorsys*"
```

### 5.5. Onglet Logs

| # | Action | Résultat attendu |
|---|---|---|
| T35 | Ouvrir l'onglet Logs | Historique des événements affiché (badge scans, erreurs, config) |
| T36 | Effectuer un scan simulé puis revenir aux logs | Nouvel événement "Badge détecté" visible |

---

## 6. Tests avec Lecteur NFC Physique

> **Matériel requis :** Lecteur USB PC/SC (ex. ACR122U) + Badge Mifare Classic / ISO14443

### Préparation

1. **Brancher** le lecteur NFC avant de lancer l'application.
2. **Désactiver** le Mode Simulation dans `appsettings.json` :
   ```json
   "ModeSimulationNfc": false
   ```
3. **Associer un badge** à un utilisateur AD en renseignant l'UID du badge dans
   l'attribut `extensionAttribute1` du compte utilisateur via les outils AD.

### Obtenir l'UID d'un badge physique

En mode simulation désactivé, les UIDs lus sont loggués. Consultez le fichier
de log dans `Journaux\` après un passage de badge. Le format est `XX-XX-XX-XX`.

### Scénarios physiques

| # | Action | Résultat attendu |
|---|---|---|
| T37 | Passer un badge Mifare Classic enregistré en AD | UID lu, utilisateur trouvé → Écran Authentification |
| T38 | Passer un badge non enregistré en AD | Écran Erreur "Accès refusé" |
| T39 | Passer un badge d'un utilisateur hors du `GroupeRestreint` | Écran Erreur (si groupe configuré) |
| T40 | Débrancher le lecteur pendant l'écoute | Message d'erreur NFC → Écran Erreur |
| T41 | Rebrancher le lecteur → Redémarrer l'app | Écoute reprend normalement |

---

## 7. Tests de Sécurité & Verrouillage

### 7.1. Blocage après 3 tentatives

```
Scenario : Saisir 3 fois un mauvais mot de passe AD
Expected : Navigation vers EcranErreur avec message "Authentification bloquée"
Reset    : Rebadger (retour EcranAttente → EcranAuthentification)
```

### 7.2. Expiration de session automatique

```
Scenario : S'authentifier → ne rien faire pendant X minutes (défaut: 15 min)
Expected : Retour automatique à l'EcranAttente
Config   : Admin → Sécurité → "Durée d'expiration session"
```

### 7.3. Chiffrement du mot de passe de service AD

Après avoir sauvegardé un mot de passe de service dans l'onglet AD :
```json
// Dans appsettings.json → le mot de passe doit être chiffré (non lisible en clair)
"MotDePasseServiceChiffre": "AQAAANCMnd8BFdERjHoAwE9Clm..."
```
> La clé de chiffrement AES-256 est liée à l'**identifiant machine** —
> le fichier `appsettings.json` ne peut pas être réutilisé sur un autre poste.

---

## 8. Vérification des Logs

### Emplacement des fichiers de log
```
.\Journaux\journal_AAAA-MM-JJ.jsonl
```

### Format d'une entrée de log
```json
{
  "Timestamp": "2026-03-30T21:45:00",
  "Niveau": "INFO",
  "Message": "Badge détecté avant AD",
  "UidBadge": "A1B2C3D4",
  "Identifiant": null
}
```

### Lecture rapide des logs (PowerShell)
```powershell
# Afficher les 20 derniers événements
Get-Content ".\Journaux\journal_$(Get-Date -f 'yyyy-MM-dd').jsonl" -Tail 20 |
  ConvertFrom-Json | Format-Table Timestamp, Niveau, Message, UidBadge
```

### Événements à vérifier après tests
| Événement | Niveau | Déclencheur |
|---|---|---|
| "Badge détecté avant AD" | INFO | Scan badge (réel ou simulé) |
| "Authentification réussie" | INFO | Bonne saisie du mot de passe |
| "Échec d'authentification (X restantes)" | ERROR | Mauvais mot de passe |
| "Badge inconnu ou non associé" | ERROR | Badge non reconnu |
| "Configuration d'entreprise modifiée" | INFO | Sauvegarde admin |

---

## 9. Résolution de Problèmes Courants

### ❌ L'application ne démarre pas
- Vérifier que .NET 8 Runtime est installé : `dotnet --version`
- Ou utiliser la distribution portable qui embarque le runtime.

### ❌ "Le service PC/SC Windows est arrêté"
- Ce message est **normal** si aucun lecteur n'est branché.
- En mode simulation, cliquer simplement le bouton "Simuler un badge".
- Sinon : `services.msc` → vérifier que **"Carte à puce"** est démarré.

### ❌ Badge non reconnu alors qu'il est enregistré en AD
1. Vérifier que l'attribut `extensionAttribute1` contient l'UID en **majuscules sans tirets** (ex: `A1B2C3D4`).
2. Vérifier que l'utilisateur appartient au groupe `GroupeRestreint` configuré.
3. Vérifier la connexion AD (bouton Test dans Admin → Active Directory).

### ❌ Mot de passe admin oublié
Editer directement `appsettings.json` :
```json
"MotDePasseAdmin": "NouveauMotDePasse!"
```

### ❌ Le lancement automatique ne fonctionne pas
Vérifier le registre : `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
```powershell
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
```

### ❌ Les applications métier (SIGEMS, Hestia) ne se lancent pas
- Vérifier les chemins dans Admin → Applications.
- EMED étant une URL, vérifier que le navigateur par défaut est configuré.

---

## 10. Matrice de Test Complète

Utilisez ce tableau pour valider chaque scénario avant mise en production.

| ID | Composant | Scénario | Prérequis | ✅ OK | ❌ KO | Notes |
|---|---|---|---|---|---|---|
| T01 | Démarrage | Lancement de l'app | — | | | |
| T02 | Sim NFC | Simuler un badge | Mode sim. activé | | | |
| T03 | NFC Physique | Badge enregistré | Lecteur + AD | | | |
| T04 | NFC Physique | Badge inconnu | Lecteur | | | |
| T05 | Navigation | Accès admin | — | | | |
| T06 | Auth Démo | MDP quelconque | Mode sim. | | | |
| T07 | Auth | MDP vide | — | | | |
| T08 | Auth | Mauvais MDP (1) | AD actif | | | |
| T09 | Auth | Mauvais MDP (2) | AD actif | | | |
| T10 | Auth | Mauvais MDP (3) | AD actif | | | |
| T11 | Navigation | Annuler auth | — | | | |
| T12 | Accueil | Affichage post-auth | Auth réussie | | | |
| T13 | Lanceur | Lancer SIGEMS | Chemin configuré | | | |
| T14 | Lanceur | Lancer Hestia | Chemin configuré | | | |
| T15 | Lanceur | Ouvrir EMED | URL configurée | | | |
| T16 | Session | Expiration auto | Délai configuré | | | |
| T17 | Erreur | Badge inconnu | — | | | |
| T18 | Erreur | Blocage 3 échecs | — | | | |
| T19 | Erreur | Erreur lecteur | Lecteur physique | | | |
| T21 | Admin Auth | Bon mot de passe | — | | | |
| T22 | Admin Auth | Mauvais MDP | — | | | |
| T24 | Admin AD | Test connexion LDAP | Réseau AD | | | |
| T25 | Admin Validation | Champ vide | — | | | |
| T28 | Admin NFC | Toggle simulation | — | | | |
| T32 | Admin Sécu | Activer auto-start | Droits registre | | | |
| T33 | Admin Sécu | Désactiver auto-start | — | | | |
| T35 | Admin Logs | Afficher historique | Logs existants | | | |
| T36 | Logs | Nouveau log scan | Scan effectué | | | |

---

**✅ Application validée pour la production quand tous les tests critiques (T01, T02, T05, T21, T28, T32) sont cochés.**

---

*Document rédigé par Ali HAMIDY — Clinique La Pinède — Version Production 1.0*
