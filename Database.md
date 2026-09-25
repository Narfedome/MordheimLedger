hmm enfin je voulais voir un point essentiel dans une nouvelle branche avec toi. Que se passe niveau bd, on mets à jour le json. On en créer une nouvelle avec loutil de creation. Cependant ce fichier n'est pas exploité en cas de mise à jours. Quel serait le comportement a avoir pour mettre à jour les  bdd en locals


Exécuté 2 commandes (1 échec)
Le heredoc du shell s'emmêle dans les apostrophes. Je passe par l'outil d'écriture de fichier.


Créé convert.mjs, exécuté une commande
+145
-0
The shell a aussi mangé un antislash dans le premier fichier. Je le réécris avec l'outil de fichier.


Créé, lu et modifié jpos.mjs (1 échec), exécuté 2 commandes
+1
-1
J'ai identifié six guerriers référençant des profils raciaux absents de RacialProfiles.json (Plague Bearer, Nurgling, Brethren, Mutant, Darksoul, Rat Ogre). Je conserve le comportement actuel du seed, qui accepte ces références avec un id à 0 sans maximum racial appliqué, en attendant l'ajout ultérieur des profils.


Modifié convert.mjs, exécuté une commande
+5
-2
Je trouve 905 entrées pour 753 id distincts, un écart normal quand une même règle apparaît dans plusieurs fichiers. Il manque cependant un contrôle pour détecter deux noms différents produisant le même slug  je l'ajoute avant d'écrire.


Modifié convert.mjs, exécuté une commande
+3
-0
Le contrôle a trouvé un vrai cas  deux compétences différentes, « Knife Fighter » et « Knife-fighter », donnent le même slug. Je regarde d'où elles viennent.

Found the two Knife Fighter skills
Je confirme qu'il s'agit bien de deux compétences distinctes, que je vais identifier avec leur catégorie dans l'id, par exemple skill.shooting.knife-fighter et skill.combat.knife-fighter.


Exécuté 3 commandes
Les diffs sont propres  26 fichiers, 754 id ajoutés et 1 115 références converties, sans autre changement. Je passe maintenant au code du seed pour vérifier comment mapper l'OfficialId entre entité et modèle afin qu'il survive aux modifications dans la Bibliothèque.

Saw how Source is mapped between entity and model
J'ajoute OfficialId à côté de Source dans chaque correspondance entité ↔ modèle, puis je vérifie les copies manuelles de modèles ailleurs dans l'appli, qui risquent de perdre ce champ à l'enregistrement.

Found manual model copies carrying Source
J'ai repéré 15 copies manuelles dans la Bibliothèque. Avant d'ajouter l'OfficialId, je vérifie qu'aucune n'est utilisée pour dupliquer une entrée, car une copie devenant une nouvelle entrée ne doit pas hériter de cet identifiant.

Checked contexts of manual model copies
J'ajoute la propriété OfficialId aux 15 entités, aux 15 modèles associés, ainsi qu'à toutes les copies utilisées pour l'édition.


Exécuté 2 commandes, créé addprop.mjs
+43
-0
L'OfficialId est ajouté sur les 15 entités, les 15 modèles et les 45 recopies  les 30 correspondances entité ↔ modèle et les 15 copies d'édition. Je compile avant de passer au seed.

Built Core
La compilation passe. Je m'attaque maintenant au seed  les propriétés de référence renommées (...Name → ...Id), et je recherche tous les endroits du code qui les utilisent, y compris les Backfill.


Exécuté 2 commandes
Seuls WarbandSeedData.cs et AppDatabase.cs utilisent ces propriétés, le reste s'appuyant sur les colonnes de la base. Je renomme les propriétés et ajoute l'Id à chaque classe de seed.


Créé seedclasses.mjs, exécuté une commande
+34
-0
Le compilateur pointe 38 endroits à adapter dans WarbandSeedData.cs, et je remarque que la table Exploration, re-seedée à chaque lancement, a besoin de retrouver un nom d'entrée à partir de son id sans seed complet — j'ajoute donc un index SeedCatalog. Je lis maintenant les Backfill concernés.

Read backfills using seed data
Ces Backfill font exactement le travail que la synchro remplacera. Pour l'étape 1a, je les garde tels quels, en traduisant simplement les id en noms grâce à SeedCatalog. J'écris cet index.


Créé SeedCatalog.cs, exécuté 2 commandes
+101
-0
Je garde les index par nom pour race et profil racial, en y ajoutant un index par id, et je bascule tous les autres index internes du seed du nom vers l'id. Je commence par renommer les dictionnaires, puis je corrigerai chaque usage associé.

Renamed seed dictionaries and listed errors
Le compilateur ne signale que les propriétés manquantes, pas les dictionnaires renommés mais toujours remplis avec Name.En, qui compilent bien qu'incorrects. Je vais donc examiner chaque utilisation une par une, dans l'ordre du fichier.

Listed all dictionary usages
Je corrige site par site, en partant du haut. D'abord le Backfill des profils raciaux.


Lu et modifié AppDatabase.cs
+4
-4
SeedEquipmentAsync et BackfillNewEquipmentItemsAsync dupliquent le même initialiseur d'objet. Je factorise cette logique dans une méthode commune qui gère aussi l'OfficialId et la traduction de la compétence accordée.


Modifié et lu AppDatabase.cs (1 échec)
Les deux occurrences sont identiques  je les remplace toutes les deux par l'appel à la méthode factorisée.

Modifié
AppDatabase.cs
+34
-52
Au tour du Backfill de l'équipement de départ des Dramatis Personae.