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

## Étape 3 — Réglages réseau de Windows : comment ça marche

- Windows classe souvent le réseau ZeroTier en **« Public »**, ce qui bloque les parties LAN.
  PES6 Connect le passe en **« Privé »**.
- PES 6 annonce ses parties LAN par la carte réseau **prioritaire** (celle qui a la plus petite
  « métrique »). PES6 Connect donne la métrique **1** à la carte ZeroTier.
- Les réglages sont lus et modifiés avec PowerShell (`Get/Set-NetConnectionProfile`,
  `Get/Set-NetIPInterface`). La carte ZeroTier est reconnue grâce à son adresse 10.x.x.x.
- Le bouton **Annuler** (visible quand tout est vert) rend à Windows le choix automatique
  de la priorité. Le réseau reste en « Privé ».

## Procédure de test de l'étape 3

| # | Situation de départ | Action | Résultat attendu |
|---|---|---|---|
| C1 | Réseau rejoint, réglages d'origine | Lancer l'exe | ⚠️ « Public » et/ou « pas prioritaire (c'est « Wi-Fi ») », bouton **Corriger** |
| C2 | Suite de C1 | **Corriger** | ✅ « Réseau ZeroTier en Privé et prioritaire » |
| C3 | Vérification à la main | Paramètres Windows → Réseau → ZeroTier One | Type de réseau : **Privé** |
| C4 | Suite de C2 | **Annuler** → OK | ⚠️ « pas prioritaire », bouton **Corriger** de nouveau |
| C5 | Après C2, redémarrer le PC | Relancer l'exe | ✅ (les réglages sont conservés) |

## Étape 4 — PES 6 : comment ça marche

- PES6 Connect cherche `PES6.exe` : chemin choisi à la main (`cheminPes6` dans le fichier de
  configuration), puis registre Windows (installateur Konami), puis
  `C:\Program Files (x86)\KONAMI\Pro Evolution Soccer 6\`.
- Il calcule son **empreinte SHA-256** et la compare à `empreintePes6`.
- Option A retenue : **le PC de l'organisatrice sert de référence**. Bouton **En faire la
  référence** (visible seulement si aucune référence n'est définie) : l'empreinte est
  enregistrée dans le fichier local et copiée pour être ajoutée au fichier distribué à tous.

## Procédure de test de l'étape 4

| # | Situation de départ | Action | Résultat attendu |
|---|---|---|---|
| P1 | `empreintePes6` vide | Lancer l'exe | ⚠️ « PES 6 trouvé (empreinte xxxxxxxx…). Aucune référence… », bouton **En faire la référence** — ou ❌ « introuvable » + **Choisir PES6.exe** |
| P2 | Si introuvable | **Choisir PES6.exe** et sélectionner le fichier | Même résultat que P1 ; `cheminPes6` rempli dans le fichier |
| P3 | Suite de P1 | **En faire la référence** | ✅ « version identique » ; l'empreinte est copiée → l'envoyer |
| P4 | Modifier un caractère de `empreintePes6` dans le fichier | **Tout revérifier** | ❌ « Ta version de PES 6 est différente… » |

## Étapes suivantes (rappel)

5. Règle de pare-feu entrante autorisant `pes6.exe`.
6. Présence entre instances PES6 Connect sur le réseau ZeroTier, ping, connexion directe
   ou relayée, hôte conseillé (pire ping minimal).
