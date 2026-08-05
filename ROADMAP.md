# Mordheim Ledger — Roadmap

> Le grand livre de Mordheim : historique de tes bandes, tes campagnes et tes règles maison.

## V1 — Socle (mono-appareil, sans compte)
- **Bandes** : gestion complète (création, roster de guerriers), bandes officielles incluses nativement (seedées depuis le livre de règles), + possibilité de créer des bandes custom non publiées
- **Système ouvert** : bandes officielles ET objets/sorts sont éditables par le joueur (pas de contenu figé), avec un flag `ContentSource` (Official / Modified / Custom) visible dans l'UI
- **Fiche Guerrier** : stats (Movement/WeaponSkill/BallisticSkill/Strength/Toughness/Wounds/Initiative/Attacks/Leadership), équipement porté, statut (Active / Dead)
- **Catalogues de référence** (`Models/Library/`) : types de bandes (`WarbandArchetype`), types de guerriers (`WarriorArchetype`), objets de la Place du Marché (`EquipmentItem`)
- **Campagne** : trésorerie de bande, gain d'XP, avancement des guerriers, historique simple des batailles
- **Table des Blessures Graves** (tirage aléatoire) — pas encore implémentée
- Pas de calcul de combat : les règles spéciales (ex. la Dague) restent du texte descriptif, appliqué manuellement par le joueur
- Stockage 100% local (SQLite)

### État actuel (voir CLAUDE.md pour le détail technique)
- [x] Modèle de données complet (Library + Campagne), archétypes avec pré-remplissage au recrutement
- [x] Services CRUD (`ILibraryService`, `IWarbandService`)
- [x] Seed d'une première bande officielle (Reiklander Mercenaries) — stats à vérifier contre le livre de règles
- [x] Écrans de bout en bout : liste de bandes, création, détail/roster, recrutement
- [x] Écrans de gestion de la bibliothèque (créer/éditer archétypes et objets) — Types de Bande (avec Types de Guerrier imbriqués) et Trading Post, tuiles en grille + dialogs Create/Edit (wizard 3 étapes pour les guerriers)
- [ ] Table des Blessures Graves
- [ ] Historique de campagne / trésorerie détaillée
- [x] Restrictions par bande (Équipement/Compétences/Montures) réellement éditables + filtrées dans les pickers
- [x] Écoles de magie normalisées en entité propre (`MagicSchool`), liée à la bande plutôt qu'au guerrier
- [x] Catalogue complet du second lot fourni par l'utilisateur — 15 bandes intégrées au total, toutes via
  le pipeline JSON `Data/SeedData/*.json` : Morts-Vivants, Chasseurs de Trésors Nains, Mercenaires
  Averlanders, Mercenaires Ostlanders, Mercenaires Reiklander/Middenheimer/Marienburg (les 3 dernières
  partagent un même roster de base — Capitaine/Champion/Jeune Loup/Guerrier/Tireur/Bretteur — avec des
  règles spéciales et trésorerie propres à chaque ville ; Reiklander a quitté le seed historique
  `OfficialContentSeed.cs` pour rejoindre ce pipeline), Kermesse du Chaos (1re bande à utiliser les
  Mutations restreintes par bande — Bénédictions de Nurgle propres aux Impurs — et à prouver que deux
  bandes peuvent avoir chacune leur propre école de magie distincte, ici Rituels de Nurgle vs Nécromancie/
  Prières de Taal), Culte des Possédés (école de magie Rituels du Chaos + pool de Mutations génériques,
  tous deux non-restreints — partagés ensuite avec les Pillards Hommes-Bêtes, qui référencent la même
  école et le même pool sans rien redéclarer, plutôt qu'un contraste volontaire avec les Bénédictions de
  Nurgle de Kermesse qui, elles, restent exclusives à leur bande), Horde Orque (1re bande à utiliser le
  catalogue Mount — Sanglier de guerre, restreint — et 1er cas de caractéristique Mouvement non-fixe : les
  Squigs des cavernes se déplacent de 2D6ps plutôt qu'une valeur fixe, d'où l'ajout de
  `WarriorArchetype.MovementOverride`/`MovementDisplay` — texte libre qui prime sur le Mouvement numérique
  partout où il est affiché, décidé avec l'utilisateur plutôt que de transformer `Movement` en texte pur
  ou de bricoler une valeur numérique approximative), Pillards Hommes-Bêtes (réutilise Rituels du Chaos +
  Mutations génériques du Culte des Possédés sans rien redéclarer, aucun équipement/monture propre — la
  monture optionnelle « Destrier du Chaos » du texte FR vient d'un article Town Cryer distinct, absente de
  la fiche canonique mordheimer.net, donc volontairement omise), Répurgateurs (1re bande à utiliser une
  nouvelle école Prières de Sigmar — 6 entrées traduites depuis mordheimer.net, le texte FR fourni pour
  cette section étant vide/illisible à l'extraction —, objets rares propres Brasero de Fer/Marteau des
  Sorcières/Livre Saint), Skavens - Clan Eshin (magie du Rat Cornu, 6 entrées ; équipement signature
  Griffes de Combat/Lames Suintantes/Sarbacane/Pistolet à Malepierre confirmant bien Eshin et non
  Pestilens), Sœurs de Sigmar (réutilise l'école Prières de Sigmar créée par les Répurgateurs sans
  redéclarer de sorts), Kislévites (aucune école de magie, aucun jeteur de sorts dans le roster). Tous les
  jeteurs de sorts, quelle que soit la bande, réutilisent la même règle spéciale générique « Wizard »/
  « Sorcier » (texte identique, dédupliquée par nom anglais) — l'affiliation à une école de magie donnée
  se fait uniquement via `WarbandSeedData.MagicSchools`/`WarriorSeedData.IsSpellcaster`, jamais via une
  règle spéciale dédiée par bande. Méthode d'import utilisée pour tout le second lot : croiser le texte FR
  fourni par l'utilisateur (extraits bruts GW/GLM, reproduits proches du texte original par choix
  explicite, fan-licence) avec mordheimer.net (EN, via le Browser pane — WebFetch direct renvoie 403 sur
  ce site) pour combler les trous de mise en page/vérifier les chiffres/compléter les tables de sorts
  manquantes, bande par bande. Toutes les données ainsi importées ont ensuite été vérifiées une seconde
  fois contre BSData/mordheim (dépôt GitHub communautaire au format BattleScribe) puis, en cas de
  désaccord entre les deux sources, tranchées contre mordheimer.net (seule source de confiance retenue) —
  4 vraies erreurs corrigées (règle du Capitaine Reiklander, rareté du Long Fusil du Hochland, compétences
  de Tueur des Nains, exclusion Rancuniers), le reste des signalements BSData s'étant révélé faux positif.
  La Roulotte de la Peste de Kermesse (véhicule à 4 profils combinés) reste hors périmètre V1, comme
  décidé.
- [x] Données communes centralisées — `SpecialRules.json`/`Equipment.json`/`Mutations.json`/
  `Skills.json`/`MagicSchools.json` (`Data/SeedData/`), seedés avant les 15 fichiers de bande (voir
  `AppDatabase.SeedOfficialContentAsync`). Avant ce refactor, chaque bande redéclarait le texte complet
  des règles/objets/mutations vraiment génériques (ex. « Leader » dupliqué dans 14 fichiers) ; seule la
  1re copie seedée comptait réellement (dédup find-or-create par nom anglais pour SpecialRule/Mutation/
  MagicSchool), le reste étant du texte mort — et `EquipmentItem` n'avait aucune dédup du tout, d'où 3
  vrais doublons à coût différent (Arc court, Fléau, Livre Saint) qui ont été fusionnés en une seule
  entrée canonique par ce refactor (prix vérifiés contre mordheimer.net). Les fichiers de bande ne
  déclarent plus que ce qui leur est vraiment propre ; les règles/objets/mutations communs y sont
  référencés par un stub `{name}` minimal (sans description) pour continuer à créer la ligne de jointure
  par guerrier/bande (`WarriorArchetypeSpecialRuleEntity` etc.) sans dupliquer le texte. `OfficialContent
  Seed.cs` (l'ancien pool d'équipement commun écrit à la main, avant le pipeline JSON) est retiré,
  entièrement absorbé par `Equipment.json`. `Skills.json` seede pour la **première fois** les ~34
  compétences core du livre de règles (Combat/Tir/Érudition/Force/Vitesse, non-restreintes) — l'onglet
  Compétences du Codex était vide jusqu'ici. Les tableaux de compétences spéciales propres à chaque bande
  (Tueur de Troll, Hommes-Bêtes, Skavens...) restent du texte libre dans une `SpecialRule`, pas de vraies
  entrées `Skill` — décision explicite de l'utilisateur, à reprendre plus tard avec la restriction par
  héros.

## V2 — Partage entre joueurs
- **Export/Import fichier** (partage natif OS) pour bandes/objets modifiés — capacité illimitée
- **Partage par QR code** pour les petites modifications (quelques objets/règles, ~3000 caractères suffisent largement en pratique) — en cas de conflit, écran de confirmation puis la donnée scannée écrase la locale
- **Synchro locale Wi-Fi** à la table de jeu (à confirmer selon usage réel)
- Historique de campagne enrichi (classement, stats agrégées entre bandes)

## V3 — Optionnel, si le besoin se confirme
- **Calculateur d'affrontement** : modificateurs à toucher/blesser/sauvegarde, règles spéciales courantes — volontairement mis de côté tant que le socle n'a pas prouvé sa valeur seul

## Sources
- Livre des Règles de Mordheim (Complet+Errata), Niels Delacroix
- https://sites.google.com/view/grande-librairie-de-mordheim/accueil (FR, existe aussi en PDF par bande, plus réduits)
- https://broheim.net/downloads.html (EN, terminologie officielle + règles core en 3 parties)
- https://mordheimer.net (EN, catalogue de bandes le plus complet, jusqu'à Grand Army 2a)
