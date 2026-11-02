# Guide de l'organisatrice (réseau ZeroTier)

Notre réseau : **`633e31d8a269ea13`** (déjà inscrit dans `pes6connect.json`).

## Réglages à vérifier une fois (sur https://my.zerotier.com)

1. Ouvre le réseau `633e31d8a269ea13`.
2. **Access Control** : *Private* (chaque joueur doit être accepté par toi).
3. **IPv4 Auto-Assign** : activé, avec une plage proposée (par ex. `10.147.20.*`).
   Sans ça, les joueurs restent « en attente d'une adresse ».

## Accepter un nouveau joueur

1. Le joueur clique sur **Rejoindre** dans PES6 Connect.
   Sa ligne affiche « Demande envoyée » et son **identifiant ZeroTier** (10 caractères).
   Il peut cliquer sur **Copier mon identifiant** pour te l'envoyer.
2. Sur my.zerotier.com, section **Members**, une nouvelle ligne apparaît avec cet identifiant
   (rafraîchis la page si besoin).
3. Coche la case **Auth** (ou « Authorized ») et écris son prénom dans **Name**.
4. Dans les 10 secondes, sa ligne passe au vert ✅ dans PES6 Connect, avec son adresse.

Le plan gratuit de ZeroTier autorise **10 appareils** : supprime les anciens appareils
qui ne servent plus (section Members) si vous approchez de la limite.
