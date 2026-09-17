# Nexorsys Identity - Windows Credential Provider

Ce module est responsable de l'authentification de session Windows via NFC, selon l'approche **Option B (Stockage local sécurisé du mot de passe avec chiffrement fort)**.

## Architecture & Cryptographie (AES-256)

L'application backend ne stocke **jamais** de mot de passe en clair. Le mot de passe de l'Active Directory est chiffré et stocké uniquement en local sur le poste de travail lors du premier enrôlement de l'utilisateur.

### Dérivation de la clé (PBKDF2)
La clé de chiffrement `K` est générée dynamiquement au moment du tap :
```
K = PBKDF2(Password: PIN, Salt: (BadgeUID + LocalMachineSecret), Iterations: 100000)
```

### Flux d'enrôlement (Premier passage sur un poste)
1. L'utilisateur badge et tape son PIN.
2. Le système détecte qu'aucun blob chiffré n'existe pour ce `BadgeUID` sur cette machine.
3. Le Credential Provider affiche un champ supplémentaire : "Mot de passe Windows".
4. Le système vérifie le mot de passe auprès de l'Active Directory local.
5. Si valide, le mot de passe est chiffré avec la clé `K` (AES-256-GCM) et stocké dans le registre local sécurisé ou le Credential Vault de Windows.

### Flux de connexion (Passages suivants)
1. L'utilisateur badge et tape son PIN.
2. Le Credential Provider lit le blob chiffré local correspondant au `BadgeUID`.
3. Le module dérive la clé `K` à partir du PIN et du BadgeUID.
4. Le module déchiffre le mot de passe AD.
5. Si le PIN est erroné, le déchiffrement échoue (AES-GCM Auth Tag invalide).
6. Si le déchiffrement réussit, le mot de passe en clair est passé au LSA (Local Security Authority) de Windows pour ouvrir la session de manière transparente.

## Composants
- `Nexorsys.Identity.CredentialProvider` : Composant C++ implémentant l'interface `ICredentialProvider` de Windows.
- `Nexorsys.Identity.CryptoWrapper` : Wrapper C# gérant la logique PCSC (lecteur NFC), l'API web (`/api/win-auth`) et le chiffrement AES-256.
- `CredentialProviderSetup.msi` : Installateur WiX.
