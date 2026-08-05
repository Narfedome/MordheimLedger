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
- [~] Compléter le catalogue au-delà d'une seule bande — 7 bandes intégrées, toutes via le pipeline JSON
  `Data/SeedData/*.json` : Morts-Vivants, Chasseurs de Trésors Nains, Mercenaires Averlanders, Mercenaires
  Ostlanders, Mercenaires Reiklander/Middenheimer/Marienburg (les 3 dernières partagent un même roster de
  base — Capitaine/Champion/Jeune Loup/Guerrier/Tireur/Bretteur — avec des règles spéciales et trésorerie
  propres à chaque ville ; Reiklander a quitté le seed historique `OfficialContentSeed.cs` pour rejoindre
  ce pipeline). **En cours** : import du reste du second lot fourni par l'utilisateur (textes FR bruts,
  mise en page dégradée) — Kermesse du Chaos, Culte des Possédés, Horde Orque, Pillards Hommes-Bêtes,
  Répurgateurs, Skavens (Clan Eshin), Sœurs de Sigmar, Kislévites. Méthode : croiser le texte FR fourni
  avec mordheimer.net (EN, via le Browser pane — WebFetch direct renvoie 403 sur ce site) pour combler les
  trous de mise en page / vérifier les chiffres avant d'écrire le JSON, bande par bande.

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
