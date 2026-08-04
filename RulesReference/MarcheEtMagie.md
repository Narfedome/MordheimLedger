# Place du Marché et Magie — Référence de règles

> Source : [Grande Librairie de Mordheim](https://sites.google.com/view/grande-librairie-de-mordheim/accueil)
> (wiki fan FR), consultée le 2026-08-04. Le livre de règles PDF officiel est trop volumineux
> pour être traité directement par l'outil — ce wiki sert de source de vérité provisoire, **à
> revérifier contre le livre** si un chiffre semble faux (même logique que
> `OfficialContentSeed.cs`, voir CLAUDE.md).
>
> Contenu paraphrasé/condensé (pas de citation verbatim, pour respect du droit d'auteur). Les
> tableaux d'objets et de sorts sont des résumés compacts (nom / coût / rareté / effet en une
> ligne) plutôt que la reproduction du texte des règles.

## Pages couvertes

- Page d'accueil (navigation uniquement)
- `place-du-marche` (page mère + règles générales de rareté/achat/vente)
- `place-du-marche/armes-de-corps-a-corps`
- `place-du-marche/armes-de-tir`
- `place-du-marche/armes-a-poudre-noire`
- `place-du-marche/projectiles-et-munitions`
- `place-du-marche/armures`
- `place-du-marche/objets-divers`
- `place-du-marche/consommables`
- `place-du-marche/poisons-et-drogues`
- `place-du-marche/animaux-et-montures`
- `place-du-marche/vehicules`
- `magie` (page mère + règles générales de lancer de sorts/prières)
- `magie/charmes-et-malefices`
- `magie/magie-gobeline`
- `magie/magie-mineure`
- `magie/prières-de-la-dame-du-lac`
- `magie/prieres-de-sigmar`
- `magie/prières-de-taal`
- `magie/prieres-d-ulric`
- `magie/rites-funeraires`
- `magie/runes-norses`

## Non couvert / à vérifier dans une passe ultérieure

- Les pages liées **depuis l'intérieur** de certaines fiches (ex. la fiche "Magie Mineure" pointe
  vers une page de carrière `francs-tireurs/mage`, "Rites Funéraires" vers
  `bandes/mercenaires-marienburgers/pretre-de-morr`) n'ont **pas** été suivies : ce sont des pages
  de bande/carrière hors périmètre "Marché + Magie", pas des sous-pages de ces deux sections.
- Les tableaux d'objets ci-dessous sont **exhaustifs par catégorie visitée** mais issus d'une
  extraction automatisée (WebFetch + résumé par un petit modèle) : les noms, coûts et jets de
  rareté doivent être considérés comme **probablement corrects mais pas garantis à 100 %**,
  notamment pour les objets liés à une bande précise (source de la rareté "spéciale"/bande unique
  parfois abrégée). En cas de doute sur un item utilisé en jeu, revérifier contre le PDF.
- Aucune illustration/qualité d'objet n'a été capturée (uniquement texte).
- Le détail exact des tables de trésor (carte au trésor, patte de singe, lampe magique, etc.) n'a
  pas été extrait au-delà du résumé d'une ligne — à creuser si ces objets sont implémentés un jour.

---

## Place du Marché — mécanique générale

- **Objets communs** : prix fixe, achetables sans limite au marché.
- **Objets rares** : ont une valeur de **Rareté** (nombre). Pour tenter d'en acquérir un pendant
  la phase d'exploration, un héros lance **2D6** : l'objet est disponible si le résultat est
  **supérieur ou égal** à sa Rareté. Un seul jet de recherche par héros et par tour, un seul objet
  rare obtenu par jet réussi. Les guerriers hors combat (sortis de la bataille) ne peuvent pas
  chercher d'objet rare ce tour-là.
- **Revente** : une bande récupère automatiquement **50 % du prix affiché**. Pour les objets à
  coût variable (ex. `30+2D6`), seule la moitié du **coût de base** est reversée.
- Le matériel peut être **réaffecté librement entre guerriers d'une même bande**, mais ne peut pas
  être échangé entre bandes différentes.

## Armes de corps à corps

| Arme | Coût | Disponibilité | Effet (résumé) |
|---|---|---|---|
| Aiguillon à Squigs | 15 po | Commune, Gobelins uniquement | Étend le rayon de contrôle des Squigs ; frappe en premier ; maniement difficile |
| Arme à deux mains | 15 po | Commune | +2 Force ; frappe en dernier |
| Arme contondante à une main | 3 po | Commune | Bonus pour étourdir sur un jet de blessure de 2 à 4 |
| Attendrisseur | 3 po | Commune, Mootlanders | Étourdit sur un jet de blessure de 2 à 4 |
| Bâton ardent | 35 po | Rare 7, Repurgators | +1 Force ; peut enflammer la cible ; à deux mains |
| Bâton d'boss | 20 po | Commune, Gobelins | Contrôle du moral de l'unité ; frappe en premier ; maniement difficile |
| Bâton de combat | 15 po | Commune, Moines de Cathay | Léger ; permet la parade ; combinable avec des frappes à mains nues |
| Bâton du serpent | 30 po | Rare 7, Gardiens des Tombes | Un serpent autonome attaque avec CC 4 ; permet la parade |
| Bec de corbin | — | Rare, Pillards de Lustrie | +1 Force ; à deux mains ; atteint à l'initiative en cas de charge |
| Brise-lame | 30 po | Rare 8 | Peut briser l'arme adverse sur une parade réussie (4+) |
| Chaîne et boulet | 15 po | Commune, Gobelins | +2 Force ; nécessite des champignons spéciaux ; déplacement imprévisible |
| Chat à neuf queues | 8 po | Commune, Pirates | +1 Attaque en charge ; ne peut pas être paré |
| Couteau de cuisine | 2 po | Commune, Mootlanders | L'adversaire bénéficie de +1 à son jet de sauvegarde |
| Dague | 2 po | Commune | Fournie gratuitement à chaque guerrier ; l'adversaire bénéficie de +1 à son jet de sauvegarde |
| Dague de la peste | 15 po | Rare 8, Skavens Clan Pestilens | Infection automatique sur 6+ pour toucher ; adversaire +1 sauvegarde |
| Dague empoisonnée hobgobeline | 15 po | Rare 9, Hobgobelins | Toxine ; utilisée en paire pour +1 Attaque ; +1 Initiative |
| Encensoir à peste | 40 po | Rare 9, Skavens Clan Pestilens | +2 Force ; crée un nuage toxique ; porteur plus difficile à cibler s'il porte de la malepierre |
| Épée | 10 po | Commune | Permet la parade |
| Épée bâtarde | 15 po | Commune, Gardiens Bretonniens | +1 Force ; frappe en dernier ; utilisable avec bouclier mais pas d'arme secondaire |
| Épée courte | 7 po | Commune, Gardiens Bretonniens | Permet la parade ; adversaire +1 sauvegarde |
| Épée des étoiles | 30 po | Rare 10, Amazones de Lustrie | +1 Force ; ignore les sauvegardes d'armure normales |
| Épée dragon | 20 po | Rare 10, Moines/Marchands de Cathay | +1 Force ; à deux mains ; permet la parade |
| Faux | — | Rare, Prêtres de Morr | +1 Force ; à deux mains |
| Fléau | 15 po | Commune | +2 Force ; à deux mains ; provoque de la fatigue (bonus seulement au premier round) |
| Fouet | — | Cochers/Muletiers | -1 Force ; bonus d'attaques multiples ; ne peut pas être paré ; peut désarmer |
| Fouet à bêtes | 10+1D6 po | Rare 8, Maîtres des Bêtes Elfes Noirs | -1 Force ; intimide les animaux ; +1 Attaque en charge |
| Fouet barbelé | 15 po | Rare 9, Maraudeurs du Chaos | Excite les Chiens du Chaos proches ; ne peut pas être paré ; +1 attaque en charge |
| Fouet d'acier | 10 po | Commune, Sœurs/Nains du Chaos | Bonus d'attaques multiples en charge ; ne peut pas être paré |
| Fouet d'hédoniste | 15 po | Commune (héros), Cour de Slaanesh | Bonus d'attaque en charge ; ne peut pas être paré |
| Gaffe | 8 po | Commune, Pirates | -1 Force ; frappe en premier ; à deux mains |
| Gantelet à pointe | 15 po | Rare 7, Gladiateurs | Fait office de bouclier et d'arme ; permet la parade et de retenter une parade ratée |
| Grande hache du Chaos | 25 po | Rare 8, Héros du Chaos | +2 Force ; à deux mains ; frappe en dernier ; -1 sur la sauvegarde adverse |
| Griffes de combat | 35 po | Rare 7, Skavens | +1 Initiative à l'escalade ; utilisées en paire pour +1 Attaque ; permet la parade |
| Griffes des anciens | 30 po | Rare 12, Amazones de Mordheim | +2 Force ; ignore les sauvegardes normales ; permet la parade |
| Hache | 5 po | Commune | -1 supplémentaire sur la sauvegarde adverse |
| Hachoir | 3 po | Commune, Mootlanders | -1 supplémentaire sur la sauvegarde adverse |
| Hache naine | 15 po | Rare 8, Nains | Permet la parade ; -1 sur la sauvegarde adverse |
| Hallebarde | 10 po | Commune | +1 Force ; à deux mains |
| Katar | 5 po | Rare 4 | -1 sur la sauvegarde adverse |
| Lame des étoiles | 15 po | Rare 7, Amazones de Lustrie | Permet la parade sur 4+ ; adversaire +1 sauvegarde |
| Lames suintantes | 50 po | Rare 9, Skavens | Utilisées en paire pour +1 Attaque ; permet la parade ; porte une toxine |
| Lance | 10 po | Commune | Bonus de cavalerie +1 Force en charge ; frappe en premier ; maniement difficile |
| Lance à sanglier | 30 po | Rare 10 | +1 Force ; réduit d'1 les Attaques du premier assaillant en cas de charge subie |
| Lance de cavalerie | 40 po | Rare 8 | +2 Force ; nécessite d'être monté |
| Louche | 2 po | Commune, Mootlanders | Sur 6+ pour toucher, désarme l'adversaire ; ignore la plupart des sauvegardes |
| Main-gauche | 7 po | Rare 7, Bandits du Hochland | Permet la parade et de retenter une parade ratée ; adversaire +1 sauvegarde |
| Marteau de cavalerie | 12 po | Rare 10 | +1 Force ; à deux mains ; +1 Force supplémentaire en charge montée |
| Marteau de guerre sigmarite | 15 po | Commune, Meneuses Sœurs | +1 Force ; étourdit sur 2-4 ; +1 contre Possédés/Morts-vivants |
| Massue ogre | 10 po | Commune, Ogres | Attaque écrasante avec -1 sur la sauvegarde ; étourdit sur 2-4 |
| Misericordia | 5 po | Rare 9, Cavalcade Maudite | Ignore les sauvegardes d'armure sur une cible à terre |
| Miséricorde | 10 po | Commune, Pillards de Lustrie | Relance 2D6 contre un ennemi à terre ; adversaire +1 sauvegarde |
| Morgenstern | 15 po | Commune | +1 Force ; fléau à une main ; maniement difficile ; provoque de la fatigue |
| Nunchaku | 20 po | Rare 7, Moines de Cathay | +2 Attaques au premier round ; à deux mains |
| Pince | 25 po | Rare 10, Geôliers Nains du Chaos | Capture une cible à terre au lieu de la blesser ; à deux mains |
| Pince-homme slaaneshi | 30 po | Rare 10, Fléau de Slaanesh | +1 Force ; immobilise la cible ; à deux mains |
| Pique | 10 po | Rare 7 | Frappe toujours en premier ; à deux mains ; utile en terrain dense |
| Poignards empoisonnés | 25 po la paire | Commune, Gobelins de la Nuit | Blessure automatique sur 6+ pour toucher ; utilisés en paire pour +1 Attaque |
| Poing | 0 po | Commune | -1 Force ; adversaire +1 sauvegarde |
| Poing de fer | 15 po | Commune, Ogres | Fait office de bouclier et d'arme ; permet la parade ; peut briser l'armure |

## Armes de tir

| Arme | Coût | Rareté | Portée | Force | Effets clés |
|---|---|---|---|---|---|
| Arbalète | 25 po | Commune | 30 pas | 4 | Ne peut pas bouger et tirer le même tour |
| Arbalète à répétition | 40 po | Rare 8 | 24 pas | 3 | Tire deux fois, -1 par tir |
| Arbalète de poing | 35 po | Rare 9 | CàC/10 pas | 4 | Utilisable au premier round de mêlée à -2 |
| Arc court | 5 po | Commune | 16 pas | 3 | — |
| Arc | 10 po | Commune | 24 pas | 3 | — |
| Arc long | 15 po | Commune | 30 pas | 3 | — |
| Arc elfique | 35+3D6 po | Rare 12 | 36 pas | 3 | -1 sur la sauvegarde adverse |
| Arme de jet | 15 po | Rare 5 | 6 pas | Utilisateur | Aucun malus de portée ; inutilisable en mêlée |
| Bâton solaire (Lustrie) | 35 po | Rare 10 | CàC/12 pas | Util./4 | Projette un rayon d'énergie ignorant l'armure |
| Bâton solaire (Mordheim) | 50 po | Rare 12 | 24 pas | 4 | Précis ; ignore les sauvegardes d'armure |
| Bolas | 5 po | Commune (Skinks) | 16 pas | 4 | Immobilise la cible ; arme risquée |
| Cabillot | 3 po | Commune (Pirates) | 6 pas | Util.-1 | Adversaire +1 sauvegarde |
| Flèches aspic | 10 po | Commune (Rois des Tombes) | 8 pas | Util. | +1 pour toucher sur tir précis |
| Fronde | 2 po | Commune | 18 pas | 3 | Peut tirer deux fois à mi-portée |
| Gantelet du soleil | 40 po | Rare 12 | CàC/12 pas | 4 | Précis ; utilisable à chaque round de mêlée |
| Javelots | 5 po | Commune | 8 pas | Util. | Aucun malus après un déplacement |
| Javelots nehekhariens | 10 po | Commune (Rois des Tombes) | 8 pas | Util. | Jet de javelot précis |
| Kusarigama | 10 po | Rare 7 (Moines de Cathay) | 3 pas | 3 | Peut renverser la cible ; utilisable contre un ennemi engagé |
| Lance-flammes à malepierre | — | — | 6 pas | 4 | Jet de flamme ; peut enflammer la cible |
| Lance-harpon | 50 po | Rare 10 (Ogres) | 30 pas | 5 | Ne peut pas bouger et tirer ; rechargement complet requis |
| Oiseau de proie | 30 po | Rare 10 | 18 pas | 3 | Ignore les couverts ; vise les cibles cachées |
| Sarbacane | 25 po | Rare 7 | 8 pas | 1 | Tir silencieux ; fléchettes empoisonnées ; adversaire +1 sauvegarde |
| Tufenk | 15 po | Rare 10 | 8 pas | 2 | Feu alchimique ; rechargement requis ; plus efficace contre cibles sèches |

## Armes à poudre noire

| Arme | Coût | Rareté | Portée | Force | Effets clés |
|---|---|---|---|---|---|
| Arquebuse | 35 po | Rare 8 | 24 pas | 4 | Malus d'armure à la cible ; bouger OU tirer ; rechargement |
| Arquebuse à répétition | 60+2D6 po | Rare 11 | 24 pas | 4 | Expérimentale ; triple tir ; rechargement long |
| Canon croche-plomb | 80+2D6 po | Rare 12 | Variable | Variable | Mortier portatif (Mangeurs d'Hommes) |
| Long fusil du Hochland | 200 po | Rare 11 | 48 pas | 4 | Choix de la cible ; malus d'armure ; bouger OU tirer |
| Mortier portable | 200 po | Rare 11 | 48 pas | 4 | Choix de la cible ; malus d'armure ; bouger OU tirer |
| Pierrier | 65 po | Rare 8 | Variable | Variable | Pièce massive ; plusieurs types de munitions |
| Pigeon explosif | 30+2D6 po | Rare 8 | Illimitée | 4 | Déploiement aléatoire ; explosion à rayon |
| Pistolet | 15 po (30 la paire) | Rare 8 | CàC/6 pas | 4 | Utilisable en mêlée ; malus d'armure ; rechargement |
| Pistolet à malepierre | 30+2D6 po | Rare 9 | 6 pas | 4 | Expérimental ; triple tir ; rechargement rapide |
| Pistolet de duel | 30 po (60 la paire) | Rare 10 | CàC/10 pas | 4 | Bonus pour toucher ; malus d'armure ; utilisable en mêlée |
| Tromblon | 30 po | Rare 9 | Gabarit | 3 | Gabarit d'explosion ; usage unique par bataille |
| Tromblon Nain du Chaos | 40 po | Rare 9 | 16 pas | 3 | Gabarit d'explosion ; rechargeable |
| Option double canon | Variable | Artilleurs/Ostlanders | — | — | Deux canons ; tir simple ou double, rechargement séparé |

## Projectiles et Munitions

| Nom | Coût | Rareté | Utilisé avec | Effet |
|---|---|---|---|---|
| Bombe fumigène | 30+2D6 po | Rare 10 | Arme de jet | Nuage de fumée de 2 pas de rayon ; bloque vision, tir et combat dedans |
| Bombe incendiaire | 35+2D6 po | Rare 9 | Arme de jet | 1D3 touches Force 4 sur la cible, Force 3 aux alentours ; explose sur l'utilisateur si 1 pour toucher |
| Eau bénite | 10+3D6 po | Rare 6 | Arme de jet | Blessure automatique (sans sauvegarde) contre Morts-vivants/Démons/Possédés ; indisponible aux bandes maléfiques |
| Filet | 5 po | Commune | Arme de jet | Immobilise la cible en cas d'échec au test de Force ; réutilisable d'une bataille à l'autre |
| Flèches de chasse | 25+1D6 po | Rare 8 | Arcs (tous types) | +1 aux jets de blessure ; dure toute la campagne |
| Flèches enflammées | 30+1D6 po | Rare 9 | Arcs (tous types) | Enflamme la cible sur 4+ ; dure une bataille |
| Fusée | — | — | Pyromane uniquement | Projectile auto-propulsé, touche Force 4 ; peut enflammer ; trajectoire imprévisible |
| Grenade de Cathay | 25+1D6 po | Rare 9 | Arme de jet | Force 6 ; enflamme sur 5+ ; explose dans la main de l'utilisateur sur 1 pour toucher |
| Grenade de Miragliano | — | Razzieurs Lustriens | Arme de jet | Force 2 ; provoque fumée/malus de vision |
| Pétards | 20 po | Rare 9 | Arme de jet | Effraie animaux/montures ; provoque l'échec des tests de charge ; dure une bataille |
| Poudre noire supérieure | 30 po | Rare 11 | Toutes armes à poudre noire | +1 Force ; dure toute la campagne |

## Armures

| Nom | Coût | Disponibilité | Sauvegarde | Règles clés |
|---|---|---|---|---|
| Armure cathayenne en soie matelassée | 15 po | Rare 10, Aristocrates uniquement | 6+ / +1 | Cumulable avec une autre armure |
| Armure du Chaos | 185 po | Rare 13, bandes du Chaos uniquement | 4+ | Ne peut pas être forgée ; coût réduit par XP ; se fond au porteur |
| Armure en gromril | 150 po | Rare 11 | 4+ | Métal nain le plus lourd ; pas de malus de Mouvement avec bouclier |
| Armure en ithilmar | 90 po | Rare 11 | 5+ | Métal argenté elfique ; léger ; pas de malus de Mouvement avec bouclier |
| Armure lamellaire | 120 po | Rare 9 | 4+ | Plaques superposées cathayennes ; -1 Mouvement avec bouclier |
| Armure légère | 20 po | Commune | 6+ | Cuir/maille standard ; pas de malus de Mouvement |
| Armure lourde | 50 po | Commune | 5+ | Cotte de mailles ; -1 Mouvement avec bouclier |
| Armure lourde de maître | — | Razzieurs Lustriens uniquement | 4+ | Fabrication tiléenne ornée ; -1 Mouvement en permanence |
| Caparaçon | 30 po | Rare 11, montures uniquement | 6+ / 5+ | Armure de cheval ; +1 à la sauvegarde monté |
| Cuir durci | 5 po | Commune, héros uniquement | 6+ | Ne peut être ni revendu ni invoqué ; seule option d'armure |
| Exosquelette | 225 po | Rare 14, Nains du Chaos uniquement | 4+ | Armure du Chaos ; +3 Mouvement |
| Bouclier | 5 po | Commune | +1 | Bois ou métal ; protection de base |
| Écu | 10 po | Rare, Bretonniens uniquement | +2 / +1 | Bouclier supérieur ; 5+ à pied ou 6+ monté |
| Pavois | 25 po | Rare 8 | +1 | Bouclier-tour ; -1 contre les projectiles ; divise le Mouvement par deux |
| Rondache | 5 po | Commune | — | Petit bouclier d'acier ; mécanique de parade |
| Amulette lunaire | 50 po | Rare 12, Amazones uniquement | — | -1 aux tirs adverses ; 6+ contre les projectiles |
| Cape en peau de dragon des mers | 50+2D6 po | Rare 10, héros Elfes Noirs uniquement | 5+ / 4+ | Peau de monstre marin ; -1 Mouvement avec bouclier |
| Cape en peau de loup | 10 po | Spéciale, régionale | — | Nécessite un test de Force ; +1 aux sauvegardes contre le tir |
| Cape en peau des hommes-lézards | 10 po | Commune, Chasseurs Saurus uniquement | — | Cape en peau d'homme-lézard |
| Casque | 10 po | Commune | — | Empêche le résultat « étourdi » ; sauvegarde spéciale 4+ |
| Casque marmite | 10 po | Commune, Halflings uniquement | — | Marmite halfling ; sauvegarde spéciale 5+ contre étourdissement |
| Peaux enchantées | 20 po | Rare 6, Amazones uniquement | — | Protection magique ; 6+ contre les blessures, 5+ contre la magie |

## Objets divers

| Nom | Coût | Rareté | Effet (résumé) |
|---|---|---|---|
| Amulette de malepierre | 10 po | Rare 5 | Une relance par bataille ou par phase d'exploration |
| Anneau du scorpion | 10+1D6 po | Rare 11 | Invoque un scorpion de combat sur test de Commandement réussi |
| Anneau venimeux | 20+2D6 po | Rare 10 | Immunité aux poisons |
| Assistants gnoblars | 20-30 po | Rare 8-10 | Divers bonus (vision, relance, attaque supplémentaire) |
| Attirail tribal Dent Rouj' | 40 po | Rare 9 | Frénésie permanente |
| Bannière | 10 po | Rare 5 | Alliés à 12 pas relancent les tests de moral ratés |
| Bannière maison noble | 30 po | Rare 10 | +1 Commandement au porteur |
| Bannière Nagarythe | 75+3D6 po | Rare 9 | Point de ralliement secondaire ; haïe si capturée |
| Bannière Clan Pestilens | 10 po | Rare 5 | Relance de moral à 12 pas ; arme à deux mains |
| Bidules magiques | 50 po | Rare 9 | Relance possible au lancer de sort (4+ sur 1D6) |
| Boussole | 45+2D6 po | Rare 9 | Relance de l'ordre de déploiement |
| Brouette | 5 po | Rare 5 | Transporte des objets encombrants sans malus de Mouvement |
| Cape des bois | 50 po | Rare 10 | -1 supplémentaire au tir si dissimulé par le terrain |
| Cape elfique | 100+1D6×10 po | Rare 12 | -1 pour être touché au tir |
| Carte au trésor | 75+4D6 po | Rare 10 | Table de trésor aléatoire |
| Carte Cathay | 20+4D6 po | Rare 9 | Résultats d'exploration bénéfiques aléatoires |
| Carte Mordheim | 20+4D6 po | Rare 9 | Bénéfices variés, du résultat bidon au choix de scénario |
| Carte Nehekhara | 20+4D6 po | Rare 10 | Fonctionne comme la carte de Mordheim |
| Carte tarot | 50 po | Rare 7 | Modifie un jet d'exploration de ±1 (test de moral requis) |
| Chapelet | 10 po | Rare 6 | Le prêtre relance un test de difficulté raté s'il reste immobile |
| Coffre | 5 po | Commune | Conteneur encombrant, à porter à deux |
| Collet | 15 po | Commune | Piège infligeant Force 4 quand déclenché |
| Collier griffes d'ours | 75+3D6 po | Rare 9 | Frénésie |
| Conque musicale | 25 po | Rare 8 | Relance de l'ordre de déploiement, une fois |
| Cor de guerre | 30+2D6 po | Rare 8 | +1 Commandement jusqu'au tour suivant (une fois par bataille) |
| Cor Nagarythe | 25+1D6 po | Rare 6 | Fonctionne comme un cor de guerre standard |
| Corde et grappin | 5 po | Commune | Relance les tests d'Initiative d'escalade ratés |
| Crochet | 4 po | Commune | Remplace une main/un bras perdu |
| Échelle | 5-10 po | Commune/Rare 5 | Aide à l'escalade, portée à deux ou par une grande créature |
| Familier | 20+1D6 po | Rare 8 | Relance de sort une fois par tour (lanceurs uniquement) |
| Flûte charmeuse de serpents | 10+1D6 po | Rare 9 | Immobilise les serpents à 6 pas pendant un tour |
| Fragments de malepierre | 100+1D6×10 po | Rare 9 | -1 au tir contre le porteur |
| Gourde magique | 10 po | Rare 7 | Produit 1D3 rations d'eau ; se brise sur un 6 |
| Grimoire de magie | 200+1D6×25 po | Rare 12 | Nouveau sort permanent pour un lanceur |
| Habits en fourrure | 5 po | Commune | Immunité aux conditions climatiques rudes |
| Habits nomades | 25 po | Rare 8 | Réduit de moitié les malus météo ; effets spécifiques divers |
| Habits en soie de Cathay | 50+2D6 po | Rare 9 | Relance le premier test de moral raté ; détruits sur 1-3 si le porteur est blessé |
| Jambe de bois | 8 po | Commune | -1 Mouvement ; sauvegarde 6+ contre les blessures aux jambes |
| Jolly Roger | 40+2D6 po | Commune | Les alliés non-pirates à 12 pas ne sont jamais seuls face à tous |
| Lampe magique | 50+2D6 po | Rare 12 | Table de trois vœux, avec table d'ombre correspondante |
| Lanterne | 10 po | Commune | +4 pas de portée de détection des ennemis cachés |
| Liber Bubonicus | 200+1D6×25 po | Rare 12 | Nouveau sort pour un lanceur Pestilens, ou enseigne la sorcellerie ratière |
| Liber Necris | 200+1D6×25 po | Rare 12 | Enseigne la nécromancie à un vampire ayant la compétence sorcellerie |
| Liturgicus Infectus | 30+2D6 po | Rare 8 | +1 Commandement jusqu'à la fin du tour (Pestilens uniquement) |
| Livre de cuisine halfling | 30+3D6 po | Rare 7 | +1 à la taille maximale de la bande |
| Livre des damnés | 100 po | Rare 10 | Haine des bandes du Chaos (Repurgators uniquement) |
| Livre saint | 100+1D6×10 po | Rare 8 | +1 à la difficulté de lancer pour prêtres/sœurs |
| Longue-vue | 20 po | Rare 8 | Détecte les ennemis cachés sur 4+ ; empêche de courir/charger |
| Lunette de visée | 75+3D6 po | Rare 10 | +1D6 pas de portée d'arme ; triple la distance de détection des cachés |
| Masque à crâne | 15 po | Commune | Provoque la peur |
| Masques maudits | 30-70 po | Variable | Capacités spéciales diverses (immunité, relance de sauvegarde, soin...) |
| Outils de crochetage | 15 po | Rare 8 | Ouvre les portes verrouillées sur test d'Initiative, sans dégâts |
| Outre | 5 po | Commune | +1 ration d'eau supplémentaire |
| Parchemin du rat familier | 25+1D6 po | Rare 8 | Enchante un rat géant ; offre une relance de sort au lanceur |
| Pardessus | 10 po | Commune | Protège l'équipement des dégâts d'eau |
| Patte de lapin | 10 po | Rare 5 | Une relance (bataille ou exploration) |
| Patte de singe | 50+1D6 po | Rare 10 | Trois vœux avec conséquences en ombre ; disparaît après usage |
| Peau de cerf bénie | 40 po | Rare 10 | Relance un test d'Initiative raté, une fois par tour |
| Pendule en pierre magique | 25+3D6 po | Rare 9 | Test de Commandement post-bataille pour relance d'exploration |
| Perroquet | 15 po | Rare 8 | L'ennemi doit réussir un test de Commandement sous peine de -1 pour toucher au premier round |
| Pierres runiques elfiques | 50+2D6 po | Rare 11 | Dissipe un sort entrant sur test de difficulté réussi |
| Porte-bonheur | 10 po | Rare 6 | Annule la première touche subie (4+ sur 1D6) |
| Relique sacrée/maudite | 15+3D6 po | Rare 6-8 | Réussite automatique au premier test de Commandement |
| Tonneau de poudre | 15 po | Rare 7 | Conteneur explosif (Force 6, rayon 1D6+3 pas) |
| Trophée coiffe Slann | 35 po | Commune | +2 sauvegarde (Chasseurs Saurus uniquement) |

## Consommables

| Objet | Coût | Disponibilité | Effet |
|---|---|---|---|
| Ail | 1 po | Commune | Les vampires doivent réussir un test de Commandement pour charger le porteur |
| Bière de Bugman | 50+3D6 po | Rare 9 | Immunité à la Peur pour la bande pendant une bataille ; inutilisable par les elfes |
| Biscuit de mer | 5 po | Commune (Pirates uniquement) | +1 Endurance temporaire pendant deux tours ; 1/6 de chance de gâter, absence à la bataille suivante |
| Chausse-trappes | 15+2D6 po | Rare 6 | Réduit la distance de charge adverse de 1D6 pouces |
| Fiole de pestilence | 25+2D6 po | Rare 9 (Skavens) | Jetée au contact ; test d'Endurance ou mise à terre au lieu de sortie de bataille |
| Gourde d'huile | 30+1D6 po | Rare 7 | Combustible à usage unique pour déclencher un feu |
| Herbes de soin | 20+2D6 po | Rare 8 | Soigne tous les points de vie perdus en phase de ralliement ; usage unique |
| Larmes de Shallya | 10+2D6 po | Rare 7 | Immunité complète au poison pour toute la bataille |
| Poudre-éclair | 25+2D6 po | Rare 8 | L'ennemi chargeant échoue sa charge en cas d'échec au test d'Initiative |
| Torche | 2 po | Commune | +4 pouces de portée de vision ; sert d'arme improvisée ; effraie les animaux |
| Victuailles | 8 po | Commune | Réduit la catégorie de taille de bande pour le calcul de vente de trésors |
| Vin elfique | 30+3D6 po | Rare 10 (Guerriers Fantômes) | Immunité à la Peur pour toute la bataille |
| Vodka | 35+2D6 po | Rare 8 (Kislévites) | +1 Commandement ; tous les membres doivent réussir un test d'Endurance sous peine de -1 Initiative |

## Poisons et drogues

| Nom | Coût | Disponibilité | Effet |
|---|---|---|---|
| Champignons chapeau-fou | 30+3D6 po | Rare 9 (commun pour les Gobelins de la Nuit) | Provoque la frénésie ; 1/6 de chance de stupidité permanente ensuite |
| Lotus noir | 10+1D6 po | Rare 9 (Rare 7 Skavens, commun Hommes-lézards) | Blessure automatique sur 6 pour toucher ; relance possible pour critique sur 6 |
| Ombre pourpre | 35+1D6 po | Rare 8 | +1D3 Initiative et +1 Mouvement/Force ; risque de dépendance ou +1 Initiative permanent |
| Poison de manticore | 30+2D6 po | Rare 9 | Dégâts continus (1 PV par tour sur un 1) ; l'effet cesse sur un 6 |
| Racine de mandragore | 25+1D6 po | Rare 8 | +1 Endurance ; convertit un « étourdi » en « à terre » ; risque de -1 Endurance permanent |
| Toxine d'arachnide | 25 po | Commune (Gobelins des Bois uniquement) | +1 aux jets de blessure ; appliquée immédiatement à l'achat |
| Venin d'araignée | 30+1D6 po | Rare 7 | La cible doit réussir un test d'Endurance ou est paralysée jusqu'à la prochaine phase de ralliement |
| Venin de reptile | 5 po par homme de main | Commune (javelots Skinks uniquement) | +1 Force à l'arme sans malus d'armure |
| Venin fumant | 30+2D6 po | Rare 8 (commun Hommes-lézards) | +1 Force à tous les coups portés avec l'arme empoisonnée |

## Animaux et Montures

| Nom | Coût | Rareté | Résumé |
|---|---|---|---|
| Chien de guerre | 25+2D6 po | Rare 10 | M6 CC4 ; combat comme un membre de bande, ne gagne pas d'XP, compte dans la taille de bande |
| Guerrier gnoblar | 15+1D6 po | Rare 9 | M4 CC2 CT3 ; créature esclave besogneuse ; anecdotique en déroute ; sujette aux querelles |
| Araignée géante | 100 po | Rare 11 | M7 CC3 ; attaques empoisonnées ; grimpe aux murs ; monture gobeline uniquement |
| Cauchemar | 95 po | Rare 11 | M8 CC2 ; monture morte-vivante ; immunisée poison/psychologie ; vampires/nécromanciens |
| Cheval | 40 po | Rare 8 | M8 CC1 ; non-combattant ; mobilité pure ; humains uniquement |
| Coursier elfique | 90 po | Rare 10 | M9 CC3 ; entraîné au combat ; relance les tests de contrôle ; elfes uniquement |
| Destrier | 80 po | Rare 11 | M8 CC3 F3 ; monture de guerre humaine puissante |
| Destrier du Chaos | 90 po | Rare 11 | M8 CC3 F4 ; variante difforme ; bandes du Chaos uniquement ; refuse les cavaliers Possédés |
| Lion de pierre | 250+1D6×10 po | Rare 13 | M6 CC5 F5 E5 W3 ; gardien magique ; provoque la peur ; sauvegarde 5+ non modifiable |
| Loup géant | 85 po | Rare 10 | M9 CC3 ; monture agressive ; gobelins uniquement ; incompatible avec les araignées géantes |
| Mule | 30 po | Rare 7 | M6 CC2 F3 ; obstinée ; refuse d'avancer ; pacifique ; fuit le combat |
| Rhinox | 200+1D6×10 po | Rare 15 | M7 CC3 F5 E5 ; dangereux ; provoque la peur ; charge inflige 1D3 touches d'impact |
| Sang-froid | 100 po | Rare 11 | M7 CC3 F4 E4 ; stupide ; provoque la peur ; +2 à la sauvegarde |
| Sanglier de guerre | 90 po | Rare 11 | M7 CC3 F3 E4 ; monture sauvage ; +2 Force en charge ; peau épaisse |
| Tapis volant | 50+4D6 po | Rare 12 | M16, insensible au terrain ; transporte trois humains ; magiquement indestructible |

## Véhicules

| Véhicule | Coût | Disponibilité | Résumé |
|---|---|---|---|
| Chariot/Diligence | 100 po | Rare 7 | Transport à quatre roues tiré par chevaux/mules ; règles de chariot standard |
| Carrosse opulent | 250 po | Rare 10 | Chariot luxueux, +3 pour trouver des objets rares grâce au prestige |
| Chariot de marchandise | 180 po | Commune (Caravane uniquement) | Chariot marchand stockant équipement/trésors ; inclut deux chevaux ; perdu si détruit |
| Char squelette | 200+10D6 po | Rare 10 (Gardiens uniquement) | Char squelettique tiré par des destriers d'os ; charges dévastatrices Force 4, -2 armure |
| Machine du Chaos | 195 po | Rare 10 (Nains du Chaos uniquement) | Chariot-prison démoniaque, jusqu'à six prisonniers sacrifiés pour Hashut |
| Pousse-pousse | 70 po | Rare 8 (humains uniquement) | Chariot à deux roues tiré par un homme, une place passager ; +1 pour toucher au tireur |
| Barge fluviale | 200 po | Rare 9 | Bateau fluvial, douze figurines de taille humaine ou fret équivalent |
| Barque | 40 po | Rare 7 | Petit bateau, six figurines ou fret équivalent |
| Gabare | 100 po | Rare 8 | Bateau moyen, huit figurines ou fret équivalent |

---

## Magie — mécanique générale

- **Apprentissage des sorts** : un lanceur commence avec un sort déterminé aléatoirement dans sa
  liste autorisée (jet de 1D6). Quand il peut apprendre un nouveau sort, le tirage reste
  aléatoire ; **un doublon réduit la difficulté de lancer de ce sort de -1** (au lieu d'en
  apprendre un nouveau).
- **Lancer un sort** : se fait en **phase de Tir**. Le joueur lance **2D6** et doit égaler ou
  dépasser la **difficulté** du sort. **Un seul sort par tour**. Un lanceur ne peut pas tirer avec
  une arme à distance le même tour où il lance un sort, mais peut courir et lancer un sort.
- **Restrictions** : un lanceur de sorts ne peut généralement pas porter d'armure, de bouclier, de
  heaume ni de rondache (exception notable : les **prières** — Sigmar, Ulric, Taal, Dame du Lac,
  Morr — ne sont pas soumises à cette restriction sur le port d'armure, et ne sont pas non plus
  affectées par les protections/dissipations anti-magie, car ce sont des prières et non des
  sorts). Un lanceur à terre, étourdi ou sonné ne peut pas lancer de sort. Un sort nécessite une
  ligne de vue vers sa cible. Les dégâts directs d'un sort autorisent une sauvegarde d'armure sauf
  mention contraire. Un sort ne peut jamais provoquer de coup critique.
- **Neuf traditions couvertes par le wiki**, chacune liée à un type de bande/carrière précis (voir
  ci-dessous). Chaque liste comporte 6 sorts/prières, un par résultat de 1D6 au tirage.

## Charmes et Maléfices

**Utilisateurs :** Sorcières.

| Sort | Difficulté | Effet |
|---|---|---|
| Prédiction | 6 | Un allié peut relancer jusqu'à 1D3 dés et ajuster le résultat de ±1 |
| Malédiction | 6 | La cible doit relancer ses jets réussis pendant deux tours |
| Poussière Aveuglante | 9 | Aveugle un ennemi (ne peut ni tirer ni charger, CC divisée par deux, déplacement aléatoire) |
| Vieillissement Brutal | 8 | Réduit toutes les caractéristiques de la cible de -1 pendant deux tours |
| Fléau du Guerrier | 7 | La cible combat à mains nues (sans arme) pendant deux tours |
| Guérison | 6 | Soigne 1 PV à un allié à portée ; relève les alliés à terre/étourdis |

*(Portées variables selon le sort, de 6 à 18 pas.)*

## Magie Gobeline

**Utilisateurs :** Chamans Gobelins des Bois.

| D6 | Sort | Difficulté | Effet |
|---|---|---|---|
| 1 | Vent de Gork | 6 | Souffle explosif qui met à terre la première cible à 12 pas (test d'Endurance ou Force 2) |
| 2 | Regard de Mork | 8 | Foudre divine sur une cible à 12 pas, 1D3 touches Force 3 |
| 3 | Têtechercheuz' | 8 | Le chaman canalise plusieurs projectiles (autant que son nombre d'Attaques de base) ; risque de se blesser sur un 1 |
| 4 | Saut de Waaagh ! | 7 | Téléporte le chaman ou un gobelin allié proche de 12 pas ; compte comme une charge si à portée d'un ennemi |
| 5 | Idole de Gork | 8 | +1 CC, +1 F, +1 A au chaman jusqu'à la prochaine blessure subie |
| 6 | On y va ! | 8 | Les alliés à 6 pas convertissent un « étourdi » en « à terre » ; dure jusqu'à ce que le chaman soit blessé |

## Magie Mineure

**Utilisateurs :** Mages (carrière franc-tireur).

| D6 | Sort | Difficulté | Effet |
|---|---|---|---|
| 1 | Flammes de U'Zhul | 7 | Boule de feu à 18 pas sur le premier ennemi touché, Force 4, -1 sauvegarde |
| 2 | Vol de Zimmeran | 7 | Le mage se téléporte de 12 pas ; compte comme une charge si à portée d'un ennemi |
| 3 | Frayeur d'Aramar | 7 | Une cible à 12 pas doit fuir 2D6 pas ou être paralysée de peur ; sans effet sur les morts-vivants/insensibles à la peur |
| 4 | Flèches Argentées d'Arha | 7 | Crée 1D6+2 flèches magiques (portée 24 pas, Force 3 chacune) |
| 5 | Chance de Shemtek | 6 | Le lanceur peut relancer un test raté avant le début du tour suivant |
| 6 | Lame de Rezhebel | 8 | Épée enflammée : +1 Attaque, +2 Force, +2 CC ; test de Commandement requis chaque tour pour la maintenir |

## Prières de la Dame du Lac

**Utilisatrices :** Damoiselles (bande Gardiens de la Chapelle, Bretonniens).

| D6 | Nom | Difficulté | Effet |
|---|---|---|---|
| 1 | Faveurs de la Dame | Auto | Bénéficie des effets d'un porte-bonheur ; relances sur les sauvegardes de charme ratées |
| 2 | Protection bénie | 8 | Sauvegarde 4+ non modifiable contre sorts/prières pour les Bretonniens proches |
| 3 | Pas vifs | 7 | Bonus au corps-à-corps et déplacement supplémentaire vers l'ennemi |
| 4 | Courroux de la Dame | 5 | Un ennemi doit réussir un test de Commandement pour tirer sur la Damoiselle, sinon il perd son tir |
| 5 | Élixir de vie | 7 | Restaure tous les PV d'un allié ; relève un allié à terre/étourdi proche |
| 6 | Vision inspirante | 6 | Un allié désigné relance un dé et ajuste le résultat de ±1 |

## Prières de Sigmar

**Utilisateurs :** Prêtres-Guerriers Repurgators et Matriarches des Sœurs de Sigmar.

| Prière | Difficulté | Effet |
|---|---|---|
| Marteau de Sigmar | 7 | Un combattant en mêlée gagne +2 Force et double ses dégâts ; test à retenter chaque tour |
| Cœur d'acier | 8 | Les alliés proches deviennent immunisés à la peur et à la déroute ; +1 aux tests de moral pour la bande |
| Feu de l'âme | 9 | Force 3 (Force 5 contre morts-vivants/possédés) aux ennemis à 4 pas, sans sauvegarde |
| Bouclier de Sigmar | 6 | Immunité totale à la magie ; dure jusqu'à un test de moral raté |
| Imposition des mains | 5 | Soigne entièrement les alliés à 2 pas ; relève les étourdis/à terre |
| Armure du Juste | 9 | Sauvegarde 2+ remplaçant l'armure normale ; provoque la peur ; dure jusqu'à la prochaine phase de tir |

## Prières de Taal

**Utilisateurs :** Prêtres de Taal (bande Chasseurs Cornus ; ne peuvent pas porter d'armure lourde).

| # | Nom | Difficulté | Effet |
|---|---|---|---|
| 1 | Bond du cerf | 7 | Téléportation de 9 pas, y compris au contact d'un ennemi (+1 Force si charge) |
| 2 | Bière bénite | 5 | Soigne entièrement un allié à 2 pas ; les ennemis proches perdent 1 Attaque au tour suivant |
| 3 | Patte d'ours | 7 | +2 Force à un allié à 6 pas jusqu'au prochain tour du prêtre |
| 4 | Secousse tellurique | 9 | Détruit un bâtiment à 4 pas, Force 3 aux ennemis au contact |
| 5 | Enchevêtrement | 8 | Divise par deux le Mouvement de tous les combattants à 12 pas (sauf fanatiques) |
| 6 | Convocation d'écureuils | 7 | Invoque des écureuils enragés, 2D6 touches Force 1 sans sauvegarde sur une cible à 12 pas |

## Prières d'Ulric

**Utilisateurs :** Prêtres-Loups d'Ulric.

| Prière | Difficulté | Effet |
|---|---|---|
| Souffle glacial | 6 | -1 pour toucher aux ennemis engagés en mêlée contre le prêtre |
| Marteau divin | 10 | Attaque Force 4 sur une cible proche ; les résultats 2-4 comptent comme étourdissant |
| Fureur sanguinaire | 7 | +2 Force et critique sur 5+ ; à retenter chaque tour |
| Faim du loup | 7 | Rend un membre de la bande frénétique |
| Hurlement d'Ulric | 10 | Toute la bande devient immunisée à la peur/terreur et améliore ses tests de moral |
| Appel d'Ulric | 10 | Le prêtre se transforme en loup (profil 4/4/1/5/2/6) ; peut tenter de reprendre forme humaine à chaque phase de tir |

## Rites Funéraires

**Utilisateurs :** Prêtres de Morr.

| D6 | Nom | Difficulté | Effet |
|---|---|---|---|
| 1 | Protection de Morr | 6 | Bloque les attaques magiques directes de Nécromanciens, Magisters ou démons ; se dissipe sur 1-2 chaque tour |
| 2 | La Mort ne me fait pas peur | Auto | Immunité à la peur pour le reste de la bataille |
| 3 | Sainteté des défunts | 7 | Empêche un combattant à terre à 6 pas d'être relevé par un Nécromancien |
| 4 | Main de Morr | 9 | Neutralise instantanément un mort-vivant au contact ; fait fuir les serviteurs de morts-vivants |
| 5 | Savez-vous qui je suis ? | 7 | Étourdit le mort-vivant le plus proche à 6 pas ; le met à terre s'il ne peut pas être étourdi |
| 6 | Je suis la mort ! | 8 | Sauvegarde 6+ et +1 Capacité de Combat (minimum 4) |

## Runes Norses

**Utilisateurs :** Chamans Norses.

| D6 | Nom | Difficulté | Effet |
|---|---|---|---|
| 1 | Mugissement du Nord | 9 | Le chaman devient immunisé aux tirs ; dure jusqu'à un jet de 1-2 en phase de ralliement |
| 2 | Fureur d'Angvar | 7 | Les alliés à 8 pas gagnent +1 pour toucher en mêlée jusqu'au prochain tour du chaman |
| 3 | Lance de Glace d'Elvek | 7 | Tir à 18 pas sur la première cible en ligne, Force 4, sauvegarde normale autorisée |
| 4 | Don de Clairvoyance | 7 | Ajuste un dé de ±1 avant la prochaine phase de ralliement ; un 6 modifié ne déclenche pas de critique |
| 5 | Baiser de Givre | 6 | La cible à 12 pas doit réussir un test d'Initiative ou est mise à terre |
| 6 | Puissance de l'Ours | 9 | +2 Force, +2 Endurance, +1 Attaque, -2 Initiative au chaman ; test de Commandement chaque tour ; une fois par bataille |
