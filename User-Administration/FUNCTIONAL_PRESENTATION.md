# Dossier de Présentation Fonctionnelle : Suite Clinique Pinède
*Une solution intégrée d'Identity Access Management (IAM) et de Sécurisation NFC pour les établissements de Santé.*

Ce document détaille spécifiquement les fonctionnalités et la logique interne de chaque application prise séparément, avant de démontrer la mécanique de connexion et le dialogue sécurisé entre elles.

---

## 🏥 PARTIE 1 : Pinède Identity Platform (Le Web Dashboard DSI)
*L'application centrale web permettant la gouvernance et le commandement de la sécurité.*

### A. Présentation Générale
La **Pinède Identity Platform** est le cerveau de la solution. Destinée aux administrateurs réseaux et techniques, cette plateforme Web (basée sur React et .NET) est le point de vérité unique. Elle a été désignée avec une interface moderne en "Mode Sombre" afin de réduire la fatigue visuelle des opérateurs qui supervisent la clinique toute la journée.

### B. Fonctionnalités Clés Utilisateurs
1. **L'Annuaire Centralisé et Recherche Hybride** : 
   - La plateforme intègre un moteur de recherche performant permettant à l'administrateur de trouver n'importe quel médecin ou agent via son nom ou profil.
   - **La Logique** : Le système interroge d'abord la base de données locale SQL interne de la clinique (réponse immédiate). S'il ne trouve pas, le moteur "escalade" silencieusement la requête vers le serveur central Active Directory (LDAP) de l'hôpital en arrière-plan.
2. **Le Kiosque Hub (Tour de Contrôle des Terminaux)** :
   - Interface permettant d'afficher le statut de sécurité de chaque agent en temps réel (Code PIN révoqué, Code Actif, Badge non scellé).
   - Permet à la DSI d'affecter numériquement un identifiant matériel de Badge NFC à un collègue médical.
3. **Le Module de Piste d'Audit (Conformité HOP'EN)** :
   - Tableau de bord légal listant chronologiquement tous les franchissements de sécurité survenant sur la flotte de bornes de la clinique. 

### C. Logique Interne et IAM (Identity Access Management)
- **Logique de Gestion des Permissions (RBAC)** : La plateforme compartimente l'hôpital. La logique interne permet de "Tick" (cocher) les logiciels du SIH (Ex: Emed, BlueKango) autorisés pour un utilisateur donné, bridant informatiquement tout accès non justifié.
- **Logique Révocatrice Mutuelle** : Dès qu'un administrateur clique sur "Révoquer" ou "Suspendre", la logique désactive immédiatement l'identifiant cryptographique en base centrale de données, tuant l'accès sur un périmètre national en une fraction de seconde, sans supprimer l’agent de la base e-mail classique de l'entreprise.

---

## ⚕️ PARTIE 2 : Pinède NFC APP (Le Client Kiosque)
*Le programme de proximité localisé sur les ordinateurs des soignants dans les unités de soins.*

### A. Présentation Générale
La **Pinède NFC APP** est le "bouclier local". Installée physiquement sur les ordinateurs biomédicaux fonctionnant sous Windows (Chariots de soins, Réanimation), elle se met en plein écran et verrouille totalement l'accès au système de l'ordinateur afin d'empêcher les failles liées aux sessions partagées ouvertes.

### B. Fonctionnalités Clés Médicales
1. **Verrouillage Zéro Confiance** : Remplace le bureau standard par une interface institutionnelle invitant l'utilisateur médical à effleurer son lecteur avec sa carte professionnelle.
2. **Double Facteur "Tap-To-Login" (2FA)** : 
   - L'application permet à l'agent de placer sa carte NFC (Le "Je Possède") sur le lecteur afin d'éveiller l'écran. 
   - Elle demande ensuite un code numérique secret (Le "Je Connais").
3. **Le Smart Desktop (Lanceur Dynamique d'Applications de Santé)** :
   - Une fois déverrouillée, la Pinède NFC App ne rend volontairement pas la main standardisée à Windows. Elle propulse l'infirmier sur un bureau éphémère contenant exclusivement les raccourcis métiers dont il a besoin (Consultations, Dossier Patient).
   - Permet de fermer les apps externes ciblées sans avoir de droits administrateur. 

### C. Logique Interne et Résilience
- **Logique Matérielle Sans Contact (PCSC Scanners)** : La logique du Kiosque embarque une boucle infinie qui interroge continuellement le lecteur matériel par radiofréquence sans encombrer la mémoire (RAM) du terminal hôte. 
- **Mode Débrayage et Fichier Logs Indépendants** : L'application possède une logique ultra-résiliente (MockMode). Même en perte totale du réseau internet et wifi clinique, l'application peut se mettre en mode "simulation logicielle" ou préserver les traces de "Badges invalides" sur son propre disque dur de secours avant transfert pour ne rien perdre.

---

## 🔗 PARTIE 3 : La Logique de Connexion Entre les 2 Applications
*Comment la sécurité opère-t-elle le pont réseau entre "La Pinède Platform Web" (DSI) et "L'Interface NFS Kiosque" (Étage médical) ?*

Les deux pôles logiciels ne sont pas isolés ; il s'agit d'un système hautement synchronisé. Voici la logique mécanique qui les unit :

### 1. La Poignée de Main Sécuritaire ("The Secure Handshake")
La règle stricte est : **Le Kiosque physique n'a aucun pouvoir décisionnel, l'Identité Platforme décide de tout.**
- L'infirmier tape son code "1234" sur le Kiosque (Pinède NFC).
- La NFC APP détruit immédiatement le texte de la RAM, compacte l'UID du badge (`A1B2C3D4`) + le PIN en un flux HTTP non déchiffrable et l'envoie en requête POST directe vers la Platform Identity Backend.
- La Platform Identity lit la requête, vérifie avec sa cryptographie de pointe (`BCrypt Hashing`), valide l'identité, insère le log de suivi (`Success`) en base Postgres, et répond avec un **Jeton d'Accès Sécurisé (JWT)** valide quelques heures uniquement.
- La NFC APP ouvre le Kiosque en fonction de ce que contient ce jeton de liaison. Ainsi, les deux applications sont constamment sur la même longueur d'onde temporelle.

### 2. Le Pont Applicatif Automatisé (Protocole URI `nexorsysnfc://`)
L'un des leviers les plus puissants entre ces deux mondes se situe au sens "Serveur Web ➔ Vers ➔ Kiosque Local".
Normalement, un navigateur internet n'a aucun droit de lancer un logiciel de force sur votre ordinateur à cause la sécurité navigateur. 
Cependant, la logique de connexion implémente l'injection à froid d'une signature standard dans les Entrées du Registre Windows (`nexorsysnfc://`).
- **En Pratique** : Lorsque le support (DSI) clique sur *"Lancer Kiosque Bureau"* sur l'application Web Identity Platform Chrome/Edge, le navigateur détecte la logique, reconnaît l'application, court-circuite le système et lance ou configure à distance l'application WPF "Pinède NFC" présente sur son ordinateur.

---

## 🔄 L'Architecture Vue par Le Cas Pratique (Chronologie)
Pour sceller la boucle, voici le chemin interconnecté des deux applications :
1. **Sur le Hub de Commandement (Identity Platform)** : L'Administrateur enrôle depuis son dashboard Monsieur Dupont, lui attribue les droits logiciels "BlueKango", écrit le badge `UID:XXX` et clique sur Créer. *La Platforme est mise à jour*.
2. **Sur le Pont Réseau** : L'ingénieur IT clique sur le fameux script Protocol depuis son web, qui déclenche l'ouverture de **Pinède NFC APP** sur la machine qu'ils testent.
3. **À La Frontière Locale (NFC Kiosque)** : Monsieur Dupont passe son badge. Le Hub local l'interroge, récupère ses droits JWT signés depuis la centrale et génère dynamiquement ses deux raccourcis métiers uniques. Tout accès frauduleux aurait été traqué et visible dix secondes plus tard sur l'écran d'un superviseur sécurité.
