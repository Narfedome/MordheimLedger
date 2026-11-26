# Mordheim Ledger — Roadmap

> Le grand livre de Mordheim : historique de tes bandes, tes campagnes et tes règles maison.

## V1 — Socle (mono-appareil, sans compte) — livrée (1.0 → 1.5, 2026-11-14)
Détail technique et décisions : [CLAUDE.md](CLAUDE.md). Historique des imports de contenu : git.
- **Bandes et guerriers** : assistant de création avec coûts, fiche de bande façon feuille papier (Héros/Hommes de main,
  piste d'XP, puces cliquables), photo de la figurine, corrections libres hors assistant et hors Fin de partie (sans or)
- **Contenu officiel** : 15 bandes (pipeline JSON `Data/SeedData/`, catalogues communs centralisés, vérifiées contre
  mordheimer.net), Dramatis Personae, synchro du contenu officiel par empreinte avec écran de conflits
- **Codex éditable** : bandes, guerriers, Place du Marché (montures comprises), compétences et leurs effets, sorts et
  écoles, mutations, blessures, règles spéciales — badge `ContentSource` (Official / Modified / Custom), restrictions par bande
- **Fin de partie complète** : blessures graves (D66/D6), XP et progression, promotion d'un Homme de main, Exploration
  (branches au choix), revenus, objets rares, recrutement, soldes, captifs, « Une Poignée d'Or », Historique narré ;
  écritures dans des services Core testés (intégration + mutation Stryker)
- Accueil, visites guidées, bande d'exemple, nouveautés après mise à jour, accessibilité (clavier, lecteurs d'écran)
- Stockage 100 % local (SQLite), Android + Windows (Google Play, Microsoft Store)

## 1.6 — Règles du livre de base encore absentes (validé le 2026-11-26)
Maquettes et récap des décisions : canevas « Chef, Expérience et Scénario » (claude.ai,
https://claude.ai/artifact/3AUYu6x5dHGEDaxRzZhYF5). Principe inchangé : l'appli propose, le joueur décide (ambre, jamais
bloquant) ; compagnon de jeu, pas calculatrice de partie.
- **Valeur des adversaires** au début de partie (1 à 3, facultative), champ partagé avec l'aide des Dramatis Personae.
- **Outsider** : écart 0-50 rien, 51-75 +1, 76-100 +2, 101-150 +3, 151-300 +4, 301+ +5. Multijoueur (Town Cryer 5) :
  seule la bande la plus faible, comparée à la bande juste au-dessus d'elle.
- **Expérience** : saisie simple, pré-remplie (survie +1, chef vainqueur +1, outsider) avec une ligne d'astuce sur
  l'origine ; ennemis hors de combat et bonus de scénario ajoutés par les joueurs.
- **Scénario** : nom en saisie libre à l'étape Résultat, repris dans l'Historique (pas de catalogue).
- **Succession du chef** (livre p. 117 + errata) : étape juste avant le recrutement, si le chef est mort. Tous les Héros
  (promus de la partie compris), pré-coché : Commandement, puis XP, puis D6. Règle « Chef » transférée. Réglages du
  Codex plutôt que du code par bande : « Successeur imposé » et « Rachetable après sa mort » (type de guerrier :
  Nécromancien, Vampire), « Le successeur apprend la magie » (type de bande : Sœurs, Possédés, Kermesse - sort/prière
  au choix à la première progression). Recruter un nouveau chef : ambre (Vampire : à partir de la partie suivante).
  Sans successeur possible : avertissement, « Archiver la bande » ou continuer sans chef.
- **Bandes archivées** : onglet « Archivées » de l'écran des bandes, consultation seule, désarchivables ; base de la
  future fin de campagne.
- Plus tard : objets à usage unique à l'écran de début de partie (effets sur le profil). Écarté : déroute, catalogue de
  scénarios.

## V2 — Partage entre joueurs (ébauche validée le 2026-11-15)
Trois thèmes : **sauvegarde**, **partage de bandes/contenu**, **partie connectée en lobby local**. Tout reste local,
**sans serveur ni compte** (Wi-Fi de la table, fichiers, QR). Ordre : du plus simple au plus risqué, chaque lot
réutilisant le précédent.

0. **Format d'échange (Core)** — socle commun aux trois thèmes : sérialisation versionnée d'une bande (guerriers,
   équipement, Historique, photos en option) et du contenu Library qu'elle référence. Contenu officiel référencé par
   `OfficialId`, contenu Modifié/Custom embarqué en entier. Règles d'import dans Core, tests d'aller-retour.
1. **Sauvegarde / restauration** — Paramètres : exporter toutes les données en un fichier (base + photos) et les
   restaurer, y compris d'Android vers Windows et inversement ; plus la sauvegarde Google native d'Android (Auto Backup),
   sans code serveur.
2. **Partage par fichier** — exporter une bande ou des entrées du Codex via le partage natif de l'OS, ouvrir le fichier
   reçu dans l'appli ; contenu déjà présent et différent → écran de conflits (réutiliser « Versions à départager »).
3. **QR code** pour les petites modifications (quelques objets/règles, ~3000 caractères) — conflit : écran de
   confirmation, puis la donnée scannée écrase la locale. Android scanne avec la caméra ; **Windows n'a pas de webcam**
   (décision 1.3 maintenue) : il affiche le QR et importe l'équivalent sous forme de fichier (même contenu que le QR).
4. **Lobby local, 2 à 4 joueurs dès la v2** (scénarios multijoueurs du livre) — remplace l'ancienne « synchro Wi-Fi ».
   - 4a. **Appairage et partie** : un hôte, jusqu'à 3 invités ; rejoindre par QR (adresse + jeton) ou, sous Windows,
     par découverte sur le réseau local / saisie d'un code ; échange des bandes en début de partie (format du lot 0).
   - 4b. **Fin de partie connectée** : l'appli connaît enfin les bandes adverses — captifs et rançon décidés par le
     capteur (retour des 5 issues du livre, réduites aujourd'hui à « racheté ou mort »), « Une Poignée d'Or » ne propose
     que les personnages réellement chez l'adversaire, aide des Dramatis Personae, résultat de partie partagé.
   - Hors connexion, le fonctionnement actuel de la 1.5 reste disponible tel quel.

**Hors v2** : campagne et classement (le modèle `Campaign` reste sans écran), historique enrichi / stats agrégées.

**À trancher au fil des lots** : transport réseau du lobby (et règle de pare-feu Windows), extension et type MIME du
fichier partagé, contenu exact de la sauvegarde (préférences comprises ?), comportement si un joueur quitte en cours
de partie.

## V3 — Optionnel, si le besoin se confirme
- **Calculateur d'affrontement** : modificateurs à toucher/blesser/sauvegarde, règles spéciales courantes — volontairement mis de côté tant que le socle n'a pas prouvé sa valeur seul

## Sources
- Livre des Règles de Mordheim (Complet+Errata), Niels Delacroix
- https://sites.google.com/view/grande-librairie-de-mordheim/accueil (FR, existe aussi en PDF par bande, plus réduits)
- https://broheim.net/downloads.html (EN, terminologie officielle + règles core en 3 parties)
- https://mordheimer.net (EN, catalogue de bandes le plus complet, jusqu'à Grand Army 2a)
