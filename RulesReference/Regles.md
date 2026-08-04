# Règles Mordheim — Référence condensée (Règles de base)

> Source : [La Grande Librairie de Mordheim](https://sites.google.com/view/grande-librairie-de-mordheim/regles) (fan wiki FR), section **Règles** uniquement.
> Contenu paraphrasé/condensé (pas de copie verbatim) à but de référence pour le développement de l'app.
>
> **Pages couvertes** : Livre des Règles de Mordheim, Caractéristiques, Le Tour, Phase de Ralliement,
> Phase de Mouvement, Phase de Tir, Phase de Corps à corps, Blessures, Commandement et Psychologie,
> Règles maison (index) + Règles maison › Armes de tir.
>
> **Non couvert par ce fichier** (hors périmètre de cette passe, à faire par un autre agent/passe si besoin) :
> la section **Règles optionnelles** entière (Antre de la Folie, Chaos dans les Rues, Étendues Sauvages,
> Feux de l'Enfer, Guerriers Montés, Incidents de tir, Pouvoir des Pierres, Quoi de Neuf Docteur?,
> Véhicules de l'Empire, Aristocratie de la Nuit, Seigneurs de la Nuit, Big Bazar, Nouvel Équipement,
> Vieille Échoppe aux Curiosités), ainsi que la page **Blessures Graves** (post-bataille, section
> **Campagne** — couverte séparément).

---

## Livre des Règles de Mordheim

- Référence officielle du site : **Livre des Règles de Mordheim V1.2fFr** (édition française), basée
  sur la publication originale de 1999 avec toutes les errata appliquées jusqu'en 2023.
- Deux versions disponibles :
  - **Version complète (V1.2fFr)** : contient toutes les errata, les encarts couleur (pages 97-112),
    le contenu complet (fluff inclus).
  - **Version condensée (V1.2cFr)** : règles seules, sans le texte d'ambiance.
- Sources officielles reconnues par le site : le livre de règles de base + le supplément
  **"Empire en Flammes"** + une sélection d'articles du **"Mordheim Annual 2002"**. Cinq nouvelles
  bandes, deux mercenaires, deux personnages nommés et six ajouts de règles/équipement en sont issus.
- Historique d'errata notables :
  | Sujet | Correction |
  |---|---|
  | Décompte des Chiens de guerre | Comptent bien dans l'effectif max de la bande (V1.2d) |
  | Incantation de l'Éveil | Portée corrigée de 11-16 à 11-15 (V1.2d) |
  | Succession du chef à sa mort | Clarifié : sort/prière suivant tiré aléatoirement, pas choisi (V1.2e) |
  | Séquence post-bataille | Errata enfin appliquée en V1.2 (manquante depuis la version 2006) |
- Structure du livre : caractéristiques, séquence de tour, ralliement, mouvement, tir, corps à corps,
  blessures, commandement/psychologie, mécaniques de campagne (expérience, revenus, commerce),
  50+ bandes avec profils complets, 50+ mercenaires solo, 60+ scénarios, systèmes de magie et tables
  d'équipement.
- L'édition **"Living Rulebook" (2006)** et les versions suivantes intègrent les mises à jour de
  **"The Mordheim Rules Review 2005"**.

---

## Caractéristiques

Chaque guerrier a un profil avec les caractéristiques suivantes :

| Abrév. | Nom | Rôle |
|---|---|---|
| **M** | Mouvement | Distance parcourue par tour, en pas (1 pas = 2,5 cm) |
| **CC** | Capacité de Combat | Habileté au corps à corps (chances de toucher en mêlée) |
| **CT** | Capacité de Tir | Précision au tir (arcs, pistolets, armes à distance) |
| **F** | Force | Puissance physique, influence les dégâts en mêlée |
| **E** | Endurance | Résistance aux dégâts ; plus elle est haute, plus il est dur de blesser le guerrier |
| **PV** | Points de Vie | Nombre de blessures encaissables avant mise hors de combat/mort |
| **I** | Initiative | Vivacité ; détermine l'ordre d'attaque en mêlée et affecte les déplacements en ruines |
| **A** | Attaques | Nombre d'attaques en mêlée par round |
| **Cd** | Commandement | Bravoure/leadership, utilisé pour les tests de moral |

**Tests de caractéristique** : jet de 1D6, réussi si résultat ≤ valeur de la caractéristique.
Un résultat de 6 est **toujours un échec automatique**.

**Tests de Commandement** : jet de **2D6**, réussi si le total ≤ Cd.

**Caractéristique à 0** : effets spécifiques —
- M(0) → immobile
- CC(0) → touché automatiquement en mêlée
- E(0) → blessé automatiquement
- PV(0) → mort
- A(0) → ne peut pas attaquer

---

## Le Tour

- Le jeu alterne entre joueurs : chaque joueur effectue un tour complet (ses 4 phases) avant que
  l'adversaire ne joue le sien.
- **Séquence des 4 phases**, dans l'ordre :
  1. **Ralliement** — tenter de rallier les guerriers en fuite, relever ceux à terre/étourdis.
  2. **Mouvement** — déplacer ses guerriers.
  3. **Tir** — tirer avec les armes appropriées ; les lanceurs de sorts peuvent aussi lancer leurs
     sorts pendant cette phase.
  4. **Corps à corps** — tous les combattants engagés en mêlée combattent.
- **Point clé** : le tir est **à sens unique** (seul le joueur actif tire), mais le corps à corps est
  **simultané** — les deux camps résolvent leurs actions de mêlée pendant le tour du joueur actif,
  qu'ils aient ou non initié l'engagement.

---

## Phase de Ralliement

- **Tests de ralliement** (guerriers en fuite) : test de Commandement (2D6 ≤ Cd).
  - **Réussite** : le guerrier arrête de fuir, peut être réorienté librement, mais ne peut ni bouger
    ni tirer ce tour (lancer un sort reste autorisé).
  - **Échec** : la figurine continue de fuir vers le bord de table le plus proche.
- **Restriction** : impossible de rallier un guerrier si le modèle ennemi le plus proche de lui est...
  un ennemi (logique : ne peut pas se rallier sous la menace directe). Les figurines en fuite, à
  terre, étourdies ou cachées ne comptent pas dans ce calcul de proximité.
- **Récupération de statut** :
  - **À terre (Knocked Down)** : peut se relever pendant cette phase.
  - **Étourdi (Stunned)** : passe au statut "à terre" lors de cette phase (pas de récupération directe
    à l'état normal en un seul tour).

---

## Phase de Mouvement

- **Mouvement de base** : jusqu'à la valeur de M, facultatif (pas obligé d'utiliser tout son mouvement).
- **Ordre de résolution des mouvements** :
  1. Charges (en premier)
  2. Mouvements obligatoires (conditions spéciales)
  3. Autres mouvements (reste des figurines)
- **Charge** :
  - Effectuée à **double Mouvement**, chemin le plus court jusqu'au contact socle à socle.
  - Le chargeur **frappe en premier** en mêlée.
  - Impossible de charger une cible dissimulée à plus de 4 pas (test d'Initiative requis à 4 pas ou moins).
  - Peut charger plusieurs cibles simultanément si toutes sont à portée.
  - **Interception** : un ennemi non engagé à moins de 2 pas de la trajectoire de charge peut intercepter ;
    les tests de Peur s'appliquent alors.
  - **Charge plongeante** (depuis une hauteur) : peut viser des ennemis jusqu'à 6 pas plus bas si à
    moins de 2 pas à l'horizontale ; test d'Initiative requis par tranche de 2 pas de hauteur
    (réussite = +1 pour toucher/Force ; échec = dégâts de chute).
  - Une charge mal calculée (qui n'atteint pas sa cible) se transforme en mouvement normal sans frapper.
- **Course** :
  - **Double Mouvement**.
  - Impossible si un ennemi est à moins de 8 pas en début de tour.
  - Empêche de tirer ce tour, mais permet de lancer un sort.
  - Ne permet pas d'engager le corps à corps.
- **Se cacher** : une figurine terminant son mouvement derrière un couvert devient "cachée" (marqueur).
  Une figurine cachée perd la dissimulation si elle tire ou lance un sort. Un ennemi détecte toujours
  une figurine cachée à portée d'Initiative (en pas).
- **Escalade/Descente** : nécessite un contact avec un mur ; distance ≤ valeur de M. Test d'Initiative
  requis — réussite = déplacement terminé ; échec = ne peut pas bouger (escalade) ou chute avec dégâts
  (descente).
- **Saut** :
  - **Saut horizontal** : max 3 pas, déduit du Mouvement, test d'Initiative requis.
  - **Chute de hauteur** : **1D3 touches à Force = hauteur en pas, sans jet de sauvegarde d'armure** —
    un test par tranche de 2 pas de hauteur. Hauteur de chute maximale : 6 pas.
- **Effets du terrain** :
  | Type | Effet sur le Mouvement |
  |---|---|
  | Dégagé | Normal |
  | Difficile | ÷2 |
  | Très difficile | ÷4 |
  | Infranchissable | Figurine retirée |
  | Obstacle bas (<1 pas) | Sauté sans pénalité de mouvement |

---

## Phase de Tir

- **Qui peut tirer** : une figurine avec une arme à distance et une ligne de vue, une fois par tour.
  **Ne peut pas tirer si** : engagée en mêlée, a couru ou raté une charge ce tour, ou s'est ralliée ce tour.
  Une figurine à terre ou étourdie ne peut pas tirer ; une figurine qui vient de se relever le peut.
- **Priorité de cible** : doit tirer sur l'ennemi le plus proche, sauf si : une autre cible est plus
  facile à toucher, est une grande cible, ou si les ennemis les plus proches sont étourdis/à
  terre/en fuite. **Interdiction** de tirer sur un combat de mêlée impliquant des membres de sa
  propre bande (risque de tir ami). Un tireur en hauteur (>2 pas d'élévation) choisit librement sa
  cible mais doit prioriser les ennemis visibles dans le même bâtiment.
- **Portée et ligne de vue** : mesurer la distance à la cible. Au-delà de la portée maximale de l'arme,
  le tir rate automatiquement. Ligne de vue dégagée requise (tracer la ligne, vérifier l'arc à 360°).
  Une cible partiellement masquée est "en couvert".
- **Jet pour toucher (1D6)**, seuil selon CT :
  | CT | Seuil pour toucher |
  |---|---|
  | 1-2 | 6 |
  | 3 | 5 |
  | 4 | 4 |
  | 5 | 3 |
  | 6 | 2 |
  | 7+ | 1 |
- **Modificateurs** :
  - Couvert : **-1**
  - Longue portée (au-delà de la moitié de la portée de l'arme) : **-1**
  - A bougé puis tiré (au-delà d'un pivotement sur place) : **-1**
  - Grande cible : **+1**
  - **Règle du couvert** : si le tir rate d'exactement 1 point contre une cible en couvert, il touche
    le couvert au lieu de la figurine.

---

## Phase de Corps à corps

- **Qui combat** : les figurines en contact socle à socle s'engagent en mêlée. Un guerrier peut
  combattre des ennemis de face, de dos et sur les flancs. Un combattant engagé ne peut pas tirer.
- **Ordre de combat** :
  1. **Frappe en premier** (ex. chargeurs) — par Initiative décroissante.
  2. **Normal** — reste des combattants, par Initiative décroissante.
  3. **Frappe en dernier** — pénalité de certains équipements.
  4. **Vient de se rallier** — toujours en dernier.
  - Égalités tranchées par un jet de 1D6.
- **Jet pour toucher** : 1D6 par attaque ; le seuil dépend de la comparaison CC attaquant vs CC
  défenseur (table de correspondance), de 3+ à 5+ selon les cas.
- **Armes doubles (une arme à une main dans chaque main)** : accorde une attaque supplémentaire. Une
  attaque avec l'arme choisie, les attaques restantes avec l'arme secondaire. Jets pour
  toucher/blesser résolus séparément par arme.
- **Parade** :
  - Nécessite un équipement permettant la parade (bouclier, épée).
  - Jet de 1D6 : doit battre le meilleur jet pour toucher de l'adversaire pour annuler les dégâts.
  - **Restrictions** : une seule parade par phase de corps à corps ; impossible de parer un 6 ;
    impossible de parer si la Force de l'attaquant est ≥ 2× la Force de base du défenseur.
- **Cibles à terre/étourdies** :
  - **À terre** : touché automatiquement ; sauvegarde ratée = hors de combat ; aucune parade possible.
  - **Étourdi** : hors de combat automatiquement en cas d'attaque.
- **Quitter le combat** : un combattant engagé ne peut pas se désengager pendant la phase de Mouvement.
  Seule échappatoire : fuir via les règles de panique (voir Commandement et Psychologie).

---

## Blessures (mécanique de combat)

> **Important — ne pas confondre avec la table "Blessures Graves"** : ceci est la mécanique
> **pendant la bataille** (Stunned / Knocked Down / Out of Action), qui détermine ce qui arrive à un
> guerrier dès qu'il perd son dernier PV, et ses effets **jusqu'à la fin de la partie**. La table
> **Blessures Graves** (section Campagne, page séparée) est résolue **après la bataille** pour les
> guerriers finis "Hors de combat", et détermine s'ils meurent, sont blessés durablement, etc. Ce
> sont deux mécaniques distinctes et successives.

- **Déclenchement** : quand un guerrier perd son dernier point de vie, le joueur qui a infligé les
  dégâts lance 1D6. Si plusieurs PV ont été perdus au-delà de 0, lancer une fois par point excédentaire
  et appliquer le résultat le plus grave.
- **Table de résultat** :
  | Jet | Résultat | Effet |
  |---|---|---|
  | 1-2 | **À terre (Knocked Down)** | Figurine couchée sur le dos |
  | 3-4 | **Étourdi (Stunned)** | Figurine couchée sur le ventre |
  | 5-6 | **Hors de combat (Out of Action)** | Retiré du jeu immédiatement |
- **Statut "À terre"** :
  - Peut seulement ramper 2" par tour, et uniquement si l'adversaire est engagé avec un autre combattant.
  - Ne peut ni tirer, ni lancer de sort, ni contre-attaquer.
  - Récupération en Phase de Ralliement : se déplace à mi-vitesse, peut tirer/lancer un sort, mais ne
    peut ni charger ni courir.
  - Reste toujours en dernier dans l'ordre de combat, quels que soient son arme/son Initiative.
  - Ne peut pas se désengager s'il est engagé.
  - Les adversaires obtiennent des touches automatiques ; si les dégâts passent l'armure, la victime
    passe "Hors de combat".
  - Risque de chute s'il est à moins de 1" du bord d'un toit/bâtiment (test d'Initiative ou dégâts de chute).
- **Statut "Étourdi"** :
  - Totalement incapable d'agir, ne réalise aucune action.
  - Devient "À terre" à la prochaine Phase de Ralliement.
  - Passe automatiquement "Hors de combat" s'il est attaqué en mêlée.
  - Même risque de chute que "À terre" si à moins de 1" d'un bord de toit/bâtiment.
- **Statut "Hors de combat"** :
  - Retiré immédiatement de la table de jeu.
  - La détermination de sa survie et de ses blessures permanentes se fait **séparément, après la
    bataille** (voir table Blessures Graves, section Campagne).

---

## Commandement et Psychologie

- **Test de Débandade (Rout)** : une bande doit tester la débandade en début de tour si 25% ou plus
  de son effectif est hors de combat. Jet de 2D6 ; si résultat ≤ Cd du chef de bande, la bataille
  continue. En cas d'échec, la bande **perd automatiquement la bataille**. Si le chef est à
  terre/étourdi, il est remplacé par le guerrier ayant le Cd le plus élevé restant dans la bande.
- **Chefs** : les guerriers à moins de 6" de leur chef peuvent utiliser le Cd de celui-ci pour leurs
  tests de psychologie (effet de leadership inspirant). Ce bonus est perdu si le chef est à
  terre, étourdi ou en fuite.
- **Seul contre tous** : un combattant seul (aucun allié à moins de 6") face à 2 ennemis ou plus doit
  réussir un test de Commandement à la fin de la phase de corps à corps. Réussite = il tient sa
  position ; échec = il fuit (les ennemis le touchent automatiquement). S'il survit, il se déplace de
  2D6" pour s'éloigner.
- **Fuite** : un modèle en fuite doit retester son Commandement à chaque Phase de Ralliement.
  Réussite = arrête de fuir, mais ne peut que lancer des sorts ce tour. Échec = se déplace de 2D6"
  vers le bord de table le plus proche, en évitant les ennemis. S'il est chargé pendant qu'il fuit,
  l'attaquant entre en contact normalement, puis le fuyard avance de 2D6" avant résolution du combat.
- **Frénésie** : un modèle frénétique doit charger tout ennemi à portée et voit ses Attaques doublées.
  Une fois à portée de charge, il est immunisé aux autres règles de psychologie. Être mis à terre ou
  étourdi met fin à la frénésie pour le reste de la bataille.
- **Haine** : un guerrier combattant un ennemi haï peut relancer ses jets pour toucher ratés, mais
  uniquement lors du premier round de corps à corps.
- **Peur** : test requis quand chargé par un ennemi qui cause la Peur, ou en tentant de charger un tel
  ennemi. Une charge ratée empêche tout mouvement ; un test défensif raté force à ne toucher que sur
  un jet de 6.
- **Stupidité** : un modèle stupide teste son Commandement en début de tour. Échec = ne peut ni
  attaquer en mêlée ni lancer de sort. S'il n'est pas engagé : jet de 1D6 — 1-3 = avance à mi-vitesse
  sans pouvoir charger, 4-6 = reste immobile. Retest à chaque tour.

---

## Règles maison (index)

- Page d'index des règles maison du site. Philosophie affichée : Mordheim est **volontairement
  déséquilibré, mortel et parfois injuste** — c'est un choix de design pour le jeu narratif, pas un
  défaut. Avant d'appliquer des règles maison, le site recommande de :
  - Jouer 4 à 6 parties avec les règles de base d'abord.
  - S'assurer d'une densité de décor suffisante sur la table.
  - Vérifier que les règles officielles sont bien appliquées (mouvement, ligne de vue, couvert, se
    cacher, escalade, chute, verticalité, objectifs de scénario).
  - Constat du site : **beaucoup de règles maison corrigent en réalité des problèmes d'environnement
    de jeu (manque de décor, mauvaise application des règles, objectifs de scénario mal compris)
    plutôt que de véritables défauts du système.**
- Seule sous-page de règle maison actuellement en ligne : **Armes de tir**.

### Armes de tir (règle maison)

- **Problème identifié** : la phase de tir peut décider une bataille de façon trop rapide/létale
  avant même que le corps à corps ne commence, ce qui peut frustrer les nouveaux joueurs ne montant
  pas de bandes orientées tir.
- **Checklist à vérifier avant de modifier les règles** (souvent suffisant pour résoudre le problème
  sans y toucher) :
  - Au moins 1/3 des bâtiments placés au centre de la table pour bloquer les lignes de vue.
  - Plusieurs niveaux de hauteur et d'approches des bâtiments.
  - Un couvert disponible à proximité de chaque espace dégagé.
  - Application correcte de la règle "se cacher" par les joueurs.
- **Règles maison recommandées** (par ordre d'impact croissant) :
  - **Limitation de composition** (la moins intrusive) : plafonner les tireurs longue portée à 50%
    de l'effectif max de la bande (javelots/pistolets souvent exemptés du fait de leur courte portée).
  - **Réduction de portée** :
    - Arc : 18" au lieu de 24"
    - Fronde : 14" au lieu de 18"
    - Objectif : rendre les tireurs atteignables en un tour, introduisant un risque sans changer les
      profils d'armes.
  - **Règle d'enrayage** : un 1 naturel pour toucher épuise les munitions ; le tireur rate son
    prochain tir. Impact estimé : ~14% de perte d'efficacité pour un tir simple, ~23% pour un tir double.
    Conserve les coûts/stats officiels.
  - **Autres options mentionnées** : désactiver le tir rapide uniquement sur les arbalètes, augmenter
    le coût de l'équipement (casse la compatibilité inter-groupes officielle).
  - **À éviter** : mécaniques de tir de réaction/vigilance (créent des situations de blocage/standoff).
- **Principe directeur** : privilégier l'intervention minimale qui préserve la compatibilité de
  composition officielle pour le jeu multi-groupes.
