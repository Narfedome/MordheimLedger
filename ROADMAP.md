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
- [~] Compléter le catalogue au-delà d'une seule bande — 10 bandes intégrées, toutes via le pipeline JSON
  `Data/SeedData/*.json` : Morts-Vivants, Chasseurs de Trésors Nains, Mercenaires Averlanders, Mercenaires
  Ostlanders, Mercenaires Reiklander/Middenheimer/Marienburg (les 3 dernières partagent un même roster de
  base — Capitaine/Champion/Jeune Loup/Guerrier/Tireur/Bretteur — avec des règles spéciales et trésorerie
  propres à chaque ville ; Reiklander a quitté le seed historique `OfficialContentSeed.cs` pour rejoindre
  ce pipeline), Kermesse du Chaos (1re bande à utiliser les Mutations restreintes par bande — Bénédictions
  de Nurgle propres aux Impurs — et à prouver que deux bandes peuvent avoir chacune leur propre école de
  magie distincte, ici Rituels de Nurgle vs Nécromancie/Prières de Taal), Culte des Possédés (école de
  magie Rituels du Chaos + pool de Mutations génériques, tous deux non-restreints puisque partagés avec les
  Pillards Hommes-Bêtes à venir — contraste volontaire avec les Bénédictions de Nurgle de Kermesse qui,
  elles, restent exclusives à leur bande). **Limite connue** : `EquipmentItem` n'a pas de déduplication
  find-or-create par nom (contrairement à SpecialRule/Mutation/MagicSchool), donc un objet censé être
  partagé entre plusieurs bandes (ex. Arme Obsidienne / Armure du Chaos, partagées par 6 bandes selon le
  livre de règles) ne peut pas encore être étendu à une bande déjà seedée depuis un JSON ultérieur. Pour
  l'instant ces 2 objets du Culte des Possédés sont restreints à cette seule bande en attendant un vrai
  mécanisme de partage multi-bandes (à généraliser plus tard, refera surface à l'import des Pillards
  Hommes-Bêtes), Horde Orque (1re bande à utiliser le catalogue Mount — Sanglier de guerre, restreint —
  et 1er cas de caractéristique Mouvement non-fixe : les Squigs des cavernes se déplacent de 2D6ps plutôt
  qu'une valeur fixe, d'où l'ajout de `WarriorArchetype.MovementOverride`/`MovementDisplay` — texte libre
  qui prime sur le Mouvement numérique partout où il est affiché, décidé avec l'utilisateur plutôt que de
  transformer `Movement` en texte pur ou de bricoler une valeur numérique approximative). **En cours** :
  import du reste du second lot fourni par l'utilisateur (textes FR bruts, mise en page dégradée) —
  Pillards Hommes-Bêtes, Répurgateurs, Skavens (Clan Eshin), Sœurs de Sigmar, Kislévites. Méthode :
  croiser le texte FR fourni avec mordheimer.net (EN, via le Browser pane —
  WebFetch direct renvoie 403 sur ce site) pour combler les trous de mise en page / vérifier les chiffres
  avant d'écrire le JSON, bande par bande. La Roulotte de la Peste de Kermesse (véhicule à 4 profils
  combinés) reste hors périmètre V1, comme décidé.

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
