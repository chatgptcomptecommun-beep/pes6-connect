# PES6 Connect

Petit outil Windows pour jouer à **Pro Evolution Soccer 6** (PC, version 1.0 française)
en réseau local virtuel avec **ZeroTier**, entre amis vivant dans différents pays.

## Télécharger

- **Version publiée** : onglet [Releases](../../releases) → `PES6Connect.zip`.
- **Version de test** (après chaque modification) : onglet **Actions** → dernière exécution
  « Fabrication » réussie → rubrique **Artifacts** → `PES6Connect` (connexion GitHub requise).

Mode d'emploi joueur : [docs/LISEZMOI.txt](docs/LISEZMOI.txt).

## Ce que fait l'outil

| Étape | Fonction | État |
|---|---|---|
| 0 | Lire `pes6connect.json` (nom du joueur, ID du réseau) | ✅ fait |
| 1 | Vérifier que ZeroTier est installé et démarré, l'installer sinon | ✅ fait, à tester |
| 2 | Rejoindre le réseau ZeroTier en un clic | à venir |
| 3 | Réseau ZeroTier en « Privé » + carte ZeroTier prioritaire | à venir |
| 4 | Trouver PES 6 et comparer l'empreinte de `pes6.exe` | à venir |
| 5 | Règle de pare-feu Windows pour PES 6 | à venir |
| 6 | Joueurs en ligne, ping, meilleur hôte | à venir |
| — | Phase 2 : site web des championnats | plus tard |

Détails des choix et procédures de test : [docs/plan.md](docs/plan.md).

## Pour les développeurs

```bash
dotnet test tests/PES6Connect.Core.Tests          # tests (fonctionne aussi sous Linux/macOS)
dotnet publish src/PES6Connect.App -c Release -o publish   # fabrique publish/PES6Connect.exe
```

- `src/PES6Connect.Core` : logique pure, sans Windows, entièrement testée.
- `src/PES6Connect.App` : fenêtre WinForms et accès au système Windows.
- `tests/` : tests automatiques de `Core`.
- `.github/workflows/build.yml` : fabrication de l'exe sur un serveur Windows de GitHub.
