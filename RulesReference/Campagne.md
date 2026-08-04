# Campagne — La Grande Librairie de Mordheim

Source : https://sites.google.com/view/grande-librairie-de-mordheim/campagne?authuser=0 (site fan
FR) et ses pages filles directes. Contenu reformulé/condensé (pas de copie verbatim, droits
d'auteur GW/communauté) — exception faite du tableau des Blessures Graves, repris sous forme de
table condensée (roll → effet court), ce qui reste un résumé de mécanique de jeu et non une
citation de texte narratif.

> **Pages couvertes** (toutes visitées en profondeur, contenu extrait intégralement depuis le DOM
> rendu, pas seulement l'index nav) :
> - `campagne` (page racine — Commencer une campagne, Jouer une partie, Séquence d'Après-Bataille,
>   Disperser une bande)
> - `campagne/blessures-graves`
> - `campagne/experience`
> - `campagne/experience/mutations`
> - `campagne/revenus`
> - `campagne/commerce`
>
> **Non explorées** : les pages de compétences liées depuis Expérience (Compétences de Combat /
> Tir / Force / Vitesse / Érudition / Équitation, + "Compétences spéciales d'équitation") — ce
> sont des listes de compétences par catégorie, pas nécessaires pour le blocage actuel (wizard de
> fin de partie) mais à ouvrir si un jour la Library doit connaître le détail des compétences. Sur
> la page Revenus, le "Tableau d'Exploration" lui-même (résultats de doubles/triples/quadruples
> etc., p.135-140) n'a pas été développé au-delà des libellés de section — seule la table de
> quantité de fragments et le Tableau des Artefacts Magiques (6 entrées) ont été extraits en
> détail. Sur la page Commerce, aucune sous-page supplémentaire n'était liée.

## Séquence d'après-bataille

Confirmée conforme à la liste fournie en entrée (10 étapes) — texte de la page :

> "Il n'est pas nécessaire de tout faire immédiatement (faites juste les trois premières étapes
> tout de suite après la bataille, vous pourrez faire des achats plus tard) mais tout jet de dé
> doit être fait devant les deux joueurs ou un témoin neutre."

1. Blessures Graves
2. Expérience
3. Revenus
4. Vente de la pierre magique
5. Déterminer la Disponibilité des Vétérans
6. Jets de Rareté et achat d'objets rares
7. Chercher des Personnages Spéciaux (Francs-Tireurs et Dramatis Personae)
8. Engager de nouvelles recrues et acheter des objets communs
9. Allouer l'équipement
10. Mise à jour de la Valeur de Bande

Notes complémentaires (issues d'errata/clarifications citées sur la page, pas du livre de base) :
- Étape 8 : l'équipement des nouvelles recrues s'achète via la liste d'équipement de la bande
  (Mordheim Annual 2002 p.109).
- On peut renvoyer un guerrier à tout moment pendant la séquence (voir "Disperser une bande"), mais
  pour remettre son équipement au magot et le réallouer à un autre guerrier, il faut attendre
  l'étape 9 puis le renvoyer à ce moment-là.

**Disperser une bande** : possible à la fin de n'importe quelle partie pour recommencer une
nouvelle bande. On peut aussi renvoyer n'importe quel guerrier à volonté — sauf le Chef, qui ne
peut pas être chassé (il faut le remplacer autrement, cf. "Mort d'un Chef" ci-dessous).

## Blessures Graves — Héros

**Confirmation explicite : oui, Héros et Hommes de main utilisent deux mécaniques totalement
différentes.**

> "À la fin d'une bataille, lancez 1D66 sur le Tableau des Blessures Graves des Héros pour chaque
> Héros mis hors de combat." (p. 118)

Le tableau (p. 119) est un vrai D66 (premier dé = dizaine 1-6, second dé = unité 1-6 → 36 codes
possibles : 11,12,13,14,15,16,21,22,...,66). Certains résultats occupent plusieurs codes
consécutifs de la table (ex. "11-15" couvre les 5 codes 11/12/13/14/15 ; "16-21" couvre uniquement
les 2 codes valides 16 et 21, pas de codes "17-20" qui n'existent pas en D66). Table condensée,
36/36 codes couverts :

| D66 | Résultat | Effet (condensé) |
|---|---|---|
| 11–15 | Mort | Le guerrier meurt, corps abandonné, tout son équipement est perdu, retiré de la feuille de bande. |
| 16, 21 | Blessures multiples | Pas mort — relancez 1D6 fois sur ce même tableau ; tout nouveau "Mort", "Capturé" ou "Blessures multiples" doit être relancé. |
| 22 | Blessure à la jambe | Mouvement -1 permanent. |
| 23 | Blessure au bras (1D6) | 1 : bras amputé, armes à une main seulement (permanent). 2-6 : blessure légère, rate la prochaine bataille. |
| 24 | Folie (1D6) | 1-3 : devient Stupide (permanent). 4-6 : devient Frénétique (permanent). |
| 25 | Jambe écrasée (1D6) | 1 : ne peut plus courir mais peut charger (permanent). 2-6 : rate la prochaine bataille. |
| 26 | Blessure au torse | Endurance -1 permanent. |
| 31 | Œil crevé | CT -1 permanent ; perte du second œil = retrait obligatoire de la bande. |
| 32 | Vieille blessure | Avant chaque bataille, 1D6 : sur un 1, ne peut pas combattre cette bataille-là (permanent, à retester à chaque partie). |
| 33 | Traumatisme nerveux | Initiative -1 permanent. |
| 34 | Blessure à la main | CC -1 permanent. |
| 35 | Blessure profonde | Hors service pendant 1D3 batailles, ne peut rien faire durant ce temps. |
| 36 | Dépouillé | S'échappe mais perd toutes armes/armures/équipement. |
| 41–55 | Récupération totale | Assommé ou blessure légère, se rétablit complètement, combat normalement la prochaine bataille. |
| 56 | Rancune | Récupère totalement mais hait désormais (1D6) : 1-3 le Héros responsable (ou le Chef ennemi si c'était un Homme de main) / 4 le Chef de la bande responsable / 5 toute la bande responsable / 6 toutes les bandes du même type. |
| 61 | Capturé | Repris conscience chez l'ennemi : rançon, échange, ou vendu comme esclave (1D6×5 CO). Morts-Vivants : peuvent le tuer pour +1 Zombie. Possédés : sacrifice possible pour +1 XP au Chef. Équipement gardé si échangé/revendu vivant, perdu par le captif si vendu/tué/zombifié (récupéré par les ravisseurs). |
| 62–63 | Endurci | Devient immunisé à la Peur (permanent). |
| 64 | Horribles balafres | Provoque désormais la Peur (permanent). |
| 65 | Vendu aux arènes | Combat un gladiateur (Francs-Tireurs). Victoire : +50 CO, +2 XP, rejoint la bande avec son équipement. Défaite : relancez sur ce tableau en ne gardant que 11-35 (mort ou blessure) ; s'il survit, rejoint la bande sans armure ni armes. |
| 66 | Survie miraculeuse | Survit et rejoint la bande, +1 XP. |

## Blessures Graves — Hommes de main

Mécanique **entièrement différente et beaucoup plus simple** que celle des Héros — confirme que
l'hypothèse "placeholder" de l'app était dans le bon esprit mais avec le mauvais seuil de mort.

> "À la fin d'une bataille, lancez 1D6 pour chaque Homme de main mis hors de combat :" (p. 118)

| D6 | Résultat |
|---|---|
| 1–2 | Blessures trop graves / mort / quitte la bande — retiré définitivement de la feuille de bande. |
| 3–6 | Combat normalement lors de la prochaine bataille (pas de séquelle, pas de table détaillée). |

Donc : **pas de D66, pas de table de blessures détaillées pour les Hommes de main** — un simple
1D6, mort/retrait sur 1-2, pleine récupération sans aucune séquelle sur 3-6. L'implémentation
actuelle de l'app (1D6, 1-2 = mort, 3-6 = récupération totale) est **la bonne mécanique** — le seul
point à vérifier dans l'app est que ce soit bien présenté comme "table Hommes de main" séparée et
non comme un fallback approximatif de la table Héros.

## Blessures multiples (résultat 16 ou 21) — procédure exacte

Le résultat "Blessures multiples" (codes 16 et 21 du D66) :
1. Le guerrier n'est pas mort.
2. Lancez 1D6 fois sur le tableau des Blessures Graves des Héros (donc jusqu'à 6 sous-jets).
3. Pour chaque sous-jet : si le résultat est "Mort" (11-15), "Capturé" (61) ou un nouveau
   "Blessures multiples" (16/21), il faut le relancer — on continue de relancer jusqu'à obtenir un
   résultat qui n'est ni Mort, ni Capturé, ni Blessures multiples.
4. Tous les effets valides obtenus s'appliquent et se cumulent sur le même guerrier (malus de
   caractéristiques cumulables, etc.).

À implémenter dans le wizard : ce résultat doit déclencher une boucle de sous-tirages sur la même
table Héros, avec exclusion/relance automatique de Mort/Capturé/Blessures multiples, et cumul de
tous les effets obtenus sur le guerrier concerné.

## Autres règles liées (page Blessures Graves)

- **Mort d'un guerrier (p. 117)** : Héros ou Homme de main, à la mort tout son équipement est
  perdu définitivement — impossible de le redistribuer après coup (l'équipement n'est récupérable
  au magot que si le guerrier est renvoyé vivant, pas s'il meurt).
- **Mort d'un Chef (p. 117)** : le Héros au Commandement le plus élevé prend le relais (règle
  spéciale Chef + accès à la liste d'équipement de Chef + éventuellement sorts si applicable),
  garde son propre tableau de compétences d'origine. Égalité de Cd → celui avec le plus d'XP ;
  égalité totale → 1D6. On ne peut pas recruter un nouveau Chef de l'extérieur, seulement une
  succession interne. Cas particuliers notés : Morts-Vivants (le Nécromancien reprend si le
  Vampire meurt ; sans Nécromancien la bande s'effondre ; un nouveau Vampire recrutable plus tard
  rétrograde le Nécromancien) ; bandes dont le Chef est jeteur de sorts (le successeur peut tirer
  un sort/une prière aléatoire au lieu d'un jet de progression normal, la première fois, sans
  changer de type de guerrier).
- Entre deux batailles, tous les guerriers récupèrent automatiquement tous leurs PV (indépendamment
  des Blessures Graves ci-dessus, qui sont les séquelles/pertes permanentes, pas les PV).

## Expérience

- +1 XP automatique pour avoir survécu à la bataille (même blessé, tant qu'il peut recombattre) ;
  si au moins un membre d'un groupe d'Hommes de main survit, tout le groupe gagne ce +1 XP
  (Mordheim Annual 2002 p.108).
- **Challengers** : bonus d'XP supplémentaire si l'adversaire a une Valeur de Bande plus élevée —
  0-50 de différence = néant, 51-75 = +1, 76-100 = +2, 101-150 = +3, 151-300 = +4, 301+ = +5.
- **Progression** : dès qu'un guerrier atteint une case à bord épais sur sa piste d'XP, jet de
  progression 2D6 immédiat (devant témoin), table différente Héros / Hommes de main.
  - **Héros** (2D6) : 2-5 → Compétence (ou nouveau sort aléatoire si Sorcier) ; 6 → 1D6 (1-3 F+1 /
    4-6 A+1) ; 7 → CC+1 ou CT+1 au choix ; 8 → 1D6 (1-3 I+1 / 4-6 Cd+1) ; 9 → 1D6 (1-3 PV+1 /
    4-6 E+1) ; 10-12 → Compétence.
  - **Hommes de main** (2D6, résultat appliqué à tout le groupe, caractéristiques plafonnées à
    +1 chacune) : 2-4 → I+1 ; 5 → F+1 ; 6-7 → CC+1 ou CT+1 au choix ; 8 → A+1 ; 9 → Cd+1 ;
    10-12 → "Ce gars est doué" (un membre du groupe devient Héros, garde son profil/XP, choisit
    deux tableaux de compétences Héros disponibles pour la bande, jet de progression Héros
    immédiat ; relancer si limite de 6 Héros déjà atteinte ; les autres membres du groupe
    relancent sur la table Hommes de main, en ignorant un nouveau 10-12).
- Caractéristiques plafonnées par un tableau "Profils maximum" (non détaillé plus avant sur cette
  page) ; si les deux options d'une ligne sont déjà au max, +1 sur une autre caractéristique non
  plafonnée à la place.

## Mutations

Extrait des mutations du livre de règles — achetables à chaque fois qu'un guerrier choisit la
compétence Mutant ; 1ère mutation au prix indiqué, chaque suivante au double du prix. Mutants et
Possédés ne peuvent acheter une mutation qu'au recrutement (pas après). Neuf mutations listées :
Âme démoniaque (20 CO, sauvegarde 4+ vs sorts/prières), Bras supplémentaire (40 CO, +1 Attaque CC,
ou pour un Possédé : +1 Attaque sans arme supplémentaire), Épines (35 CO, touche F1 automatique au
contact), Hideux (40 CO, provoque la Peur), Pince (50 CO, +1 Attaque CC à F+1, bras sans arme),
Queue de scorpion (40 CO, +1 Attaque F5 empoisonnée, F2 si cible immunisée poison), Sabots fendus
(40 CO, Mouvement+1), Sang acide (30 CO, touche F3 aux figurines au contact si le mutant est
blessé en CC), Tentacule (35 CO, agrippe et retire 1 Attaque à l'adversaire au CC, min. 1).

## Revenus

- **Procédure d'Exploration** (p. 134) : 1D6 par Héros survivant (non mis hors de combat), +1 dé
  si victoire, +dés bonus (compétences/équipement), max 6 dés retenus au final. Les doublons /
  triplets / etc. donnent accès à des lieux spéciaux ou rencontres sur le Tableau d'Exploration.
  Le total des dés donne la quantité de fragments de pierre magique trouvée : 1-5→1, 6-11→2,
  12-17→3, 18-24→4, 25-30→5, 31-35→6, 36+→7. Un Homme de main tout juste promu Héros peut
  participer à l'Exploration (la Phase d'Expérience a lieu avant, donc la promotion est déjà
  effective — clarification Tuomas Pirinen).
- **Vente de pierre magique** (p. 134) : pas obligatoire de tout vendre tout de suite. Le tableau
  de vente donne directement le profit net en CO après déduction de l'entretien de la bande (plus
  la bande est nombreuse, plus l'entretien coûte cher) — le détail chiffré du tableau n'a pas été
  extrait de cette page (probablement une image/tableau non capturé en texte).
- **Tableau d'Exploration** (p. 135-140) : structure en sections Double / Triple / Quadruple /
  Quintuple / Sextuple — contenu détaillé non extrait (non nécessaire pour le blocage actuel).
- **Artefacts Magiques** (p. 141, via le Tableau d'Exploration) : chaque artefact est unique dans
  une campagne (relancer si déjà possédé, même par un guerrier mort). 6 artefacts listés sur la
  page : Bottes et Corde de Pieter (déplacement libre sur tout terrain y compris vertical), la
  Miséricorde du Comte de Ventimiglia (dague qui compte comme épée, sonne sur 1-3/hors de combat
  sur 4-6), l'Armure d'Att'la (immunité aux sorts + traversée des murs + +1 PV), l'Arc Traqueur
  (touche toujours sur 2+, Dégâts+1, cible dévie vers un nain présent), la Cagoule d'Exécuteur
  (Frénétique permanent, F+1 CC, charge automatique sur toute cible sonnée/à terre à portée), et
  l'Œil Omniscient de Numas (vision totale du champ de bataille, 2D6 en Exploration, sauvegarde
  6+ additionnelle, rend tous les animaux adverses frénétiques).

## Commerce

- Nouvelles recrues : recrutées comme les guerriers de départ mais seulement avec objets communs
  de la liste d'équipement (objets rares uniquement via le commerce normal ensuite) ; contraintes
  habituelles de composition de bande (nombre de Héros/Hommes de main/sorciers...) toujours
  valables.
- Renforcer un groupe d'Hommes de main existant : 2D6 = "budget" d'XP cumulée disponible pour de
  nouvelles recrues à ajouter au groupe, sans dépasser ce total (XP excédentaire perdu) ; +2 CO par
  point d'XP supplémentaire de chaque recrue, en plus de l'arme/armure standard. Les nouvelles
  recrues doivent être équipées identiquement aux membres existants du groupe. Un groupe d'Hommes
  de main est indivisible. Une nouvelle recrue peut être équipée depuis le magot (Mordheim Annual
  2002 p.109).
- Disponibilité (p. 145) : objets communs = achat libre, prix fixe, quantité illimitée. Objets
  rares = un nombre de disponibilité ; jet 2D6 par Héros (un seul jet par Héros et par
  séquence), objet trouvé si le jet est ≥ à ce nombre ; un seul objet rare achetable par jet
  réussi ; les guerriers mis hors de combat lors de la dernière bataille ne peuvent pas chercher
  d'objets rares.
- Ventes (p. 145) : revente automatique à moitié prix (moitié du prix de base pour les objets à
  prix variable/rares). Échanges d'équipement possibles entre guerriers de la même bande
  (ancien matériel stocké en réserve/magot) mais jamais entre bandes différentes.
