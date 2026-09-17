# Dossier d'Architecture et Déploiement - Suite Clinique Pinède
**Destinataire :** Équipe Technique Sohexawin (Prestataire IT - Clinique La Pinède)
**Auteur :** Ali HAMIDY
**Date :** Avril 2026

---

## 1. Vue d'Ensemble de la Solution

La Suite Clinique Pinède est une solution complète d'Identity Access Management (IAM) et de sécurisation des accès physiques via NFC, conçue spécifiquement pour répondre aux exigences du programme **HOP'EN** et au **RGPD**.

### 1.1 Composants du Système
La suite se compose de 4 modules interconnectés :

1.  **Pinède Identity Platform (Backend API)** : Le moteur central. Développé en C# .NET 8 (ASP.NET Core Web API). Il gère les connexions LDAP, la base de données PostgreSQL, la validation JWT, et expose les routes REST.
2.  **Pinède Identity UI (Dashboard Web)** : Le portail d'administration DSI. Développé en React 18 + Vite. Hébergé via NGINX.
3.  **Pinède NFC Kiosque (Client Lourd)** : Application WPF installée sur les postes clients. Interagit avec les lecteurs NFC (PCSC) et gère le verrouillage Windows (Zero Trust).
4.  **Admin Mobile (App Supervisoire)** : Application React Native pour le suivi DSI en temps réel.

---

## 2. Prérequis Serveur (Infrastructure Sohexawin)

Pour déployer la plateforme IAM (Backend + Frontend + DB), les prérequis serveur sont les suivants :

### 2.1 Serveur Hôte (Linux recommandé, ex: Ubuntu Server 22.04 LTS)
*   **CPU :** 4 vCPU minimum
*   **RAM :** 8 Go minimum (16 Go recommandés pour PostgreSQL en production)
*   **Stockage :** 100 Go SSD (Prévoir une partition dédiée pour les data PostgreSQL)
*   **Réseau :** Accès réseau interne à la clinique (LAN). Le serveur doit pouvoir contacter :
    *   Le serveur contrôleur de domaine (Active Directory / LDAP).
    *   Les postes clients (pour les requêtes API entrantes).

### 2.2 Logiciels requis
*   **Docker & Docker Compose** : (v2.x minimum)
*   **PostgreSQL 14+** : (Si vous n'utilisez pas l'image Docker fournie)
*   **Certificats SSL** : Requis pour configurer les endpoints en HTTPS (Obligatoire en production).

---

## 3. Guide de Déploiement (Environnement Production)

Le déploiement est orchestré via Docker Compose.

### Étape 1 : Préparation de l'environnement
1.  Cloner le dépôt sur le serveur cible.
2.  Créer le fichier `.env` à la racine à partir de `.env.example`.

```ini
# Exemple de fichier .env de Production
POSTGRES_DB=NexorSys_Production
POSTGRES_USER=nexorsys_admin
POSTGRES_PASSWORD=<MotDePasseComplexeDB>

ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=NexorSys_Production;Username=nexorsys_admin;Password=<from-secret-provider>

Jwt__Key=<VotreCléSecrèteLongueDePlusDe32Caracteres!>
Jwt__Issuer=NexorSys.Identity
Jwt__Audience=NexorSys.Identity.Clients

Ldap__Url=ldaps://<ip-controleur-domaine>:636
Ldap__BaseDn=DC=clinique,DC=local
Ldap__ServiceAccount=CN=svc_nexorsys,OU=ServiceAccounts,DC=clinique,DC=local
Ldap__ServicePassword=<MotDePasseCompteService>

Auth__MockMode=false # IMPORTANT : Doit être à false en production

CORS__AllowedOrigins=https://identity.clinique.local,https://kiosque.clinique.local
```

### Étape 2 : Configuration du Reverse Proxy (NGINX)
Le frontend est servi par NGINX. Modifiez `nginx.conf` pour inclure les certificats SSL de la clinique.

```nginx
server {
    listen 443 ssl;
    server_name identity.clinique.local;

    ssl_certificate /etc/ssl/certs/clinique.crt;
    ssl_certificate_key /etc/ssl/private/clinique.key;

    location / {
        root /usr/share/nginx/html;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://backend:5000;
        # ... headers proxy
    }
}
```

### Étape 3 : Lancement
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```
*Le backend exécutera automatiquement les migrations ou le script `init_db.sql` au premier lancement.*

---

## 4. Points d'Audit Sécurité (Checklist Sohexawin)

Afin que Sohexawin puisse valider la sécurité de l'application avant la mise en production, voici les points de contrôle implémentés :

### 4.1 Authentification et Habilitations
*   [ ] **Désactivation du MockMode** : S'assurer que `"Auth:MockMode": false` est défini dans `appsettings.json` et les variables d'environnement.
*   [ ] **Validation Active Directory** : Le backend se connecte au LDAP via `System.DirectoryServices`. Vérifier que le compte de service (`svc_nexorsys`) a uniquement les droits en **Lecture seule** sur l'OU des utilisateurs.
*   [ ] **Cryptographie des PINs** : Les codes PIN des kiosques (pour la 2FA NFC) sont hashés via **BCrypt** (`BCrypt.Net.BCrypt.HashPassword`). *Aucun PIN n'est stocké en clair.*
*   [ ] **Jetons JWT** : L'API génère des tokens signés avec `HMAC-SHA256`. 
    *   Durée de vie Kiosque : 2 Heures.
    *   Durée de vie Dashboard Admin : 8 Heures.
    *   *Action requise Sohexawin :* Remplacer la clé `Jwt__Key` par une clé robuste (min 256-bit).

### 4.2 Application WPF Kiosque (Client Lourd)
L'application WPF tourne sur les postes cliniques.
*   [ ] **Appsettings Kiosque** : Dans le fichier `appsettings.json` du projet WPF, passer `"ActiverSimulation": false` pour utiliser les vrais lecteurs NFC (ex: ACR122U).
*   [ ] **Protocole nexorsysnfc://** : L'application utilise une clé de registre pour écouter le protocole URI. Sohexawin doit s'assurer que les GPO autorisent l'écriture de cette clé lors de l'installation du client (via le package MSI à créer).
*   [ ] **Mode Zero Trust** : L'application WPF masque la Taskbar Windows et intercepte les raccourcis (Alt+Tab, Ctrl+Esc) via le Hook clavier.

### 4.3 Traçabilité (Conformité HOP'EN)
*   [ ] **Table `audit_logs`** : L'application écrit en SQL Raw (`ExecuteSqlRawAsync`) dans la table `audit_logs` pour garantir l'indépendance de l'ORM lors de la journalisation d'événements critiques (Connexion réussie/échouée, création d'utilisateur).
*   [ ] **Rétention des logs** : Sohexawin doit prévoir un plan de backup PostgreSQL (ex: pg_dump quotidien) pour garantir la rétention légale des logs d'accès.

### 4.4 Réseau & Exposition
*   [ ] **CORS Policy** : Actuellement configuré pour `AllowAnyOrigin` en développement. Sohexawin **doit** restreindre `CORS__AllowedOrigins` aux domaines exacts de la clinique dans `.env`.
*   [ ] **Base de données** : Le port 5432 ne doit **pas** être exposé à l'extérieur du réseau Docker (enlever la directive `ports: - "5432:5432"` du docker-compose en prod).

---

## 5. Résolution des Problèmes Connus

1.  **Erreur Npgsql DateTime** : La ligne `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);` est présente dans `Program.cs` pour assurer la compatibilité temporelle avec Postgres 14+. Ne pas la retirer.
2.  **Échec de connexion LDAP** : Si le serveur affiche "LdapServerUnavailable", vérifiez que le serveur Linux hébergeant l'API parvient à pinguer/résoudre le FQDN `dc01.clinique.local`.
3.  **Lecteur NFC non reconnu sur WPF** : Vérifier que le service Windows "Carte à puce" (SCardSvr) est en cours d'exécution sur le poste client.

---
**Signature Validation DSI / Sohexawin**

- [ ] Architecture Validée
- [ ] Code Source Audité
- [ ] Stratégie de Backup validée
- [ ] Go-Live Autorisé
