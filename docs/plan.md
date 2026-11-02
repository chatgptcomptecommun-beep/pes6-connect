# Plan et choix techniques

## Choix techniques (en bref)

- **C# / .NET 8 + WinForms** : technologie native de Windows, capable de gérer services,
  pare-feu et cartes réseau. Publiée en **un seul `PES6Connect.exe`** (≈ 65 Mo) qui
  n'a besoin d'aucune installation.
- **Droits administrateur** demandés au lancement (fichier `app.manifest`) : indispensables
  pour installer ZeroTier, régler le pare-feu et le réseau.
- **Pas de certificat** de signature : Windows SmartScreen affiche un avertissement au
  premier lancement (voir `LISEZMOI.txt`).
- **Configuration** : `pes6connect.json` à côté de l'exe. S'il manque, l'outil crée un modèle vide.
- La logique (décisions, messages) est séparée de l'accès à Windows pour pouvoir être testée
  automatiquement sur n'importe quel ordinateur.

## Contexte joueurs

- Windows 10 et 11, au maximum 10 joueurs (= limite du plan gratuit ZeroTier).
- PES 6 version 1.0 française, sans patch.
- Interface en français uniquement.

## Étape 1 — ZeroTier : comment ça marche

1. L'outil interroge le service Windows `ZeroTierOneService`.
   - démarré → ✅ ;
   - arrêté → ⚠️ + bouton **Démarrer ZeroTier** ;
   - absent → ❌ + bouton **Installer ZeroTier**.
2. L'installation utilise `winget` (installateur intégré à Windows 10/11) avec le paquet
   officiel `ZeroTier.ZeroTierOne`. Si winget est absent ou échoue, l'outil propose
   d'ouvrir la page officielle https://www.zerotier.com/download/.

## Procédure de test de l'étape 1

À faire sur un PC Windows. Note le résultat de chaque cas (capture d'écran + « Copier le rapport »).

| # | Situation de départ | Action | Résultat attendu |
|---|---|---|---|
| T1 | `pes6connect.json` supprimé | Lancer l'exe | ⚠️ « Le fichier … vient d'être créé », bouton « Ouvrir le fichier » ouvre le Bloc-notes |
| T2 | Fichier rempli correctement | « Tout revérifier » | ✅ « Joueur : … · Réseau : … » |
| T3 | Enlever une virgule dans le fichier | « Tout revérifier » | ❌ message « erreur de format (vers la ligne N) » |
| T4 | ZeroTier installé et démarré | Lancer l'exe | ✅ « ZeroTier est installé (version …) et fonctionne » |
| T5 | Arrêter le service (Services Windows → ZeroTier One → Arrêter) | « Tout revérifier » puis « Démarrer ZeroTier » | ⚠️ puis ✅ |
| T6 | PC **sans** ZeroTier (ou désinstallé) | « Installer ZeroTier » | ⏳ 1-3 min puis ✅ ; l'icône ZeroTier apparaît dans la barre des tâches |
| T7 | Lancer l'exe et répondre « Non » aux droits administrateur | — | L'outil ne se lance pas (comportement normal de Windows) |

## Étape 2 — Rejoindre notre réseau : comment ça marche

- L'ID du réseau (`633e31d8a269ea13`) est lu dans `pes6connect.json`.
- PES6 Connect parle au service ZeroTier du PC par son « guichet » local
  (`http://127.0.0.1:9993`), protégé par le fichier `authtoken.secret` que seul un
  administrateur peut lire : c'est une des raisons de la demande de droits administrateur.
- Bouton **Rejoindre** → demande envoyée. Tant que l'organisatrice n'a pas accepté le
  joueur, la ligne affiche ⚠️ « Demande envoyée » et l'identifiant ZeroTier du PC, avec un
  bouton **Copier mon identifiant**. L'outil revérifie toutes les 10 secondes : la ligne
  passe au vert toute seule après l'approbation.
- Voir [organisatrice.md](organisatrice.md) pour l'approbation.

## Procédure de test de l'étape 2

| # | Situation de départ | Action | Résultat attendu |
|---|---|---|---|
| R1 | ZeroTier OK, réseau jamais rejoint | Lancer l'exe | ⚠️ « Tu n'as pas encore rejoint notre réseau », bouton **Rejoindre** |
| R2 | Suite de R1 | **Rejoindre** | ⚠️ « Demande envoyée… Ton identifiant ZeroTier : xxxxxxxxxx » ; Windows peut demander si le PC doit être « détectable » : répondre **Oui** |
| R3 | Suite de R2 | **Copier mon identifiant**, puis coller dans un message | L'identifiant correspond à celui visible dans Members sur my.zerotier.com |
| R4 | Suite de R2, sans fermer l'outil | L'organisatrice coche **Auth** | En moins de 10 s : ✅ « Connecté au réseau … Ton adresse : 10.x.x.x » |
| R5 | Réseau déjà rejoint et accepté | Relancer l'exe | ✅ directement |
| R6 | Mettre un faux ID valide (ex. `0000000000000000`) dans le fichier | **Tout revérifier** puis **Rejoindre** | ❌ « Ce réseau n'existe pas » (ou ⚠️ en attente : ZeroTier peut mettre du temps à répondre) |
| R7 | Arrêter le service ZeroTier | **Tout revérifier** | Ligne réseau grisée « En attente : ZeroTier doit d'abord fonctionner » |

## Étapes suivantes (rappel)

3. Passer la carte ZeroTier en réseau « Privé » et lui donner la plus petite métrique.
4. Empreinte SHA-256 de `pes6.exe` comparée à la référence du fichier de config.
5. Règle de pare-feu entrante autorisant `pes6.exe`.
6. Présence entre instances PES6 Connect sur le réseau ZeroTier, ping, connexion directe
   ou relayée, hôte conseillé (pire ping minimal).
