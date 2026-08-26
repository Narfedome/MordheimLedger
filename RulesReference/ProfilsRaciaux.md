# Profils Raciaux — Maximums de Caractéristiques

Couverture : table complète (29 profils), fournie directement par l'utilisateur le 2026-08-24 -
contrairement aux autres fichiers de ce dossier (paraphrasés depuis la Grande Librairie de Mordheim),
celle-ci vient telle quelle de l'annexe "Racial Maximum Characteristics" du livre de règles. Sert de
source de vérité pour `MordheimLedgerApp.Core/Data/SeedData/RacialProfiles.json` (le catalogue
`RacialProfile`, voir Models/Library/RacialProfile.cs) et pour la table de correspondance
`AppDatabase._racialProfileNameByWarriorArchetypeEnglishName` (quel profil racial gouverne les
maximums de chaque `WarriorArchetype`).

## Rôle dans l'appli

Chaque profil borne les gains de caractéristique de l'étape Progression (`Core.Rules.
CharacteristicIncreaseRules.IsAtMax`) - un Héros/Homme de main ne peut jamais dépasser le maximum de
son profil racial. Seuls les profils réellement nécessaires à un archétype qui gagne de l'Expérience
sont consommés par le code ; les autres (aucune bande actuelle ne les utilise) restent seedés pour que
le Picker "Type de créature" d'une bande **personnalisée** (`WarriorArchetypeEditDialog`, onglet
Profil) propose l'éventail complet plutôt que seulement les profils déjà en jeu.

## Table (M / CC / CT / F / E / PV / I / A / Cd)

| Profil | M | CC | CT | F | E | PV | I | A | Cd |
|---|---|---|---|---|---|---|---|---|---|
| Human | 4 | 6 | 6 | 4 | 4 | 3 | 6 | 4 | 9 |
| Elf | 5 | 7 | 7 | 4 | 4 | 3 | 9 | 4 | 10 |
| Dwarf | 3 | 7 | 6 | 4 | 5 | 3 | 5 | 4 | 10 |
| Ogre | 6 | 6 | 5 | 5 | 5 | 5 | 6 | 5 | 9 |
| Halfling | 4 | 5 | 7 | 3 | 3 | 3 | 9 | 4 | 10 |
| Possessed | 6 | 8 | 0 | 6 | 6 | 4 | 7 | 5 | 10 |
| Vampire | 6 | 8 | 6 | 7 | 6 | 4 | 9 | 4 | 10 |
| Skaven | 6 | 6 | 6 | 4 | 4 | 3 | 7 | 4 | 7 |
| Skaven (Clan Pestilens) | 5 | 6 | 6 | 4 | 5 | 3 | 7 | 4 | 7 |
| Ghoul | 5 | 5 | 2 | 4 | 5 | 3 | 5 | 5 | 7 |
| Saurus | 4 | 6 | 0 | 5 | 5 | 3 | 4 | 4* | 10 |
| Skink | 6 | 5 | 6 | 4 | 3 | 3 | 7 | 4 | 8 |
| Goblin | 4 | 5 | 6 | 4 | 4 | 3 | 6 | 4 | 7 |
| Orc | 4 | 6 | 6 | 4 | 5 | 3 | 5 | 4 | 9 |
| Black Orc | 4 | 7 | 6 | 5 | 6 | 3 | 5 | 4 | 9 |
| Werecreature (Norse) Wulfen/Ulfwerenar | 8 | 6 | 0 | 6 | 5 | 4 | 7 | 4 | 9 |
| Tomb Lord (Tomb Guardians) | 4 | 6 | 6 | 5 | 5 | 5 | 5 | 4 | 9 |
| Liche Priest & Acolyte (Tomb Guardians) | 4 | 6 | 6 | 4 | 4 | 3 | 6 | 4 | 9 |
| Liche (Restless Dead) | 5 | 4 | 4 | 4 | 4 | 8 | 6 | 3 | 10 |
| Grave Guard (Restless Dead) | 5 | 5 | 5 | 4 | 4 | 4 | 5 | 4 | 10 |
| Bull Centaur (Black Dwarfs) | 8 | 7 | 6 | 5 | 5 | 4 | 6 | 5 | 10 |
| Bull Centaur (The Sons of Hashut) | 7 | 7 | 3 | 5 | 5 | 4 | 4 | 5 | 9 |
| Hobgoblin (The Sons of Hashut) | 4 | 5 | 5 | 4 | 4 | 3 | 5 | 3 | 8 |
| Marauder of Chaos | 4 | 7 | 7 | 4 | 4 | 3 | 7 | 4 | 9 |
| Warrior of Chaos | 4 | 8 | 8 | 5 | 5 | 3 | 8 | 5 | 9 |
| Ungor (Beastman) | 6 | 6 | 6 | 4 | 4 | 3 | 7 | 4 | 7 |
| Centigor | 9 | 7 | 6 | 4 | 5 | 4 | 6 | 4 | 9 |
| Minotaur | 6 | 6 | 5 | 5 | 5 | 5 | 6 | 5 | 9 |
| Other Beastmen (Bestigor) | 5 | 7 | 6 | 4 | 5 | 4 | 6 | 4 | 9 |

\* Attaques indiqué "4+1" dans la source (bonus type Prédateur non représentable comme un simple
maximum) - seedé à 4 dans `RacialProfiles.json`, à corriger si un jour un objet/une règle Prédateur
est mécanisé.

## Correspondance profil ↔ archétype dans l'appli (2026-08-24)

Seuls les profils suivants sont réellement consommés (archétype qui gagne de l'Expérience, présent
dans une des 15 bandes déjà importées) - voir `AppDatabase._racialProfileNameByWarriorArchetypeEnglishName`
pour la table complète nom d'archétype → profil :

- **Human** : la quasi-totalité des archétypes humains (Reiklander/Marienburg/Middenheim, Averlanders,
  Kislevites, Ostlanders, Sœurs de Sigmar, Répurgateurs, Necromancer/Dreg d'Undead...).
- **Halfling** : Halfling Scout (Averlanders).
- **Dwarf** : tous les archétypes des Chasseurs de Trésors Nains.
- **Skaven** : tous les archétypes gagnant de l'XP du Clan Eshin.
- **Ghoul** : Ghoul (Undead).
- **Vampire** : Vampire (Undead).
- **Orc** : Orc Boss/Shaman/Big 'Un/Orc Boyz (Horde Orque).
- **Goblin** : Goblin Warriors (Horde Orque).
- **Ogre** : Ogre (Mercenaires Ostlanders).
- **Beastman** (= Ungor de la table) : Beastmen Chieftain/Shaman/Ungor/Gor (Pillards Hommes-Bêtes),
  Beastman (Culte des Possédés).
- **Bestigor** (= Other Beastmen de la table) : Bestigor (Pillards Hommes-Bêtes).
- **Centigor**, **Minotaur** : idem, Pillards Hommes-Bêtes.
- **Marauder of Chaos** : Carnival Master/Brute/Tainted One (Kermesse du Chaos), Magister/Initiate
  (Culte des Possédés) - cultistes mutés mais toujours de corps humain, analogue le plus proche dans
  la table pour ces archétypes (aucune ligne "Chaos Human" littérale dans la source).
- **Possessed**, **Mutant**, **Darksoul** : Culte des Possédés (Mutant/Darksoul seedés en placeholder,
  absents de la table fournie).
- **Plague Bearer**, **Nurgling**, **Brethren** : Kermesse du Chaos (seedés en placeholder, absents
  de la table fournie).
- **Rat Ogre** : Skaven de Clan Eshin (seedé en placeholder, absent de la table fournie).

Les autres profils de la table (Elf, Skaven Clan Pestilens, Saurus, Skink, Black Orc, Werecreature,
Tomb Lord, Liche Priest/Acolyte, Liche, Grave Guard, les 2 Bull Centaur, Hobgoblin, Warrior of Chaos)
ne correspondent à aucun archétype des 15 bandes actuelles - seedés pour rester disponibles dans le
Picker "Type de créature" d'une bande personnalisée.

Un archétype dont `GainsExperience` est faux (Zombie, Loup Funeste, Chien de guerre, Squig des
Cavernes, Troll, Rats géants...) n'a besoin d'aucun profil : l'étape Progression ne se déclenche
jamais pour lui (voir `WarriorOutcomeRow.ShowsInExperienceStep`), donc ses maximums ne sont jamais
consultés.
