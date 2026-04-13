# Toutes les fonctions de l'Audio Manager de Visual Radio Assist

**L'Audio Manager est le moteur d'automatisation audio-visuelle de VRA : il écoute les entrées audio du studio, détecte parole, musique ou tonalités, et déclenche en temps réel des actions visuelles — changements de caméra, macros, commandes HTTP, contrôle PTZ.** C'est l'une des trois applications locales du système (avec Core et Output Player), fonctionnant sur une machine légère (3+ cœurs CPU, 8 Go+ RAM) et communiquant avec le Core via HTTP TCP (port par défaut 3002). Ce rapport catalogue de façon exhaustive chaque fonction, paramètre et intégration, avec les versions d'introduction identifiées dans les changelogs.

---

## Gestion des entrées audio et monitoring en temps réel

L'Audio Manager ingère le signal audio du studio via les **Audio Inputs**, qui représentent les canaux physiques provenant des consoles de mixage, microphones, entrées ligne ou flux réseau Dante/Ravenna. Chaque entrée audio dispose de paramètres configurables de **gain** et de **sensibilité**, associés à un **live meter** affichant le niveau audio en temps réel directement dans l'interface.

Les **API audio supportées** couvrent les principaux standards : **DirectSound, ASIO et WASAPI** sous Windows, **CoreAudio et JACK** sous macOS. Le protocole **Dante** est intégré via le pilote Dante Audio WDM sous Windows, permettant d'acheminer des flux audio réseau vers l'Audio Manager comme n'importe quel périphérique audio OS. Les flux **Ravenna** sont également mentionnés comme sources compatibles.

Fonctions spécifiques du monitoring des entrées :

- **Rechargement automatique des périphériques audio** toutes les 12 heures, pour gérer les déconnexions ou changements de configuration (v4.0.95)
- **Rechargement manuel via l'icône tray** de l'Audio Manager (v4.0.95)
- **Avertissements pour les entrées audio inactives**, alertant lorsqu'un périphérique configuré n'est plus disponible (v4.5.7)
- **Combobox de recherche** remplaçant le sélecteur basique dans la configuration Audio Manager, facilitant la sélection parmi de nombreux périphériques (v4.6.27)
- **Nouvel algorithme de sample rate** pour une meilleure qualité de détection (v4.0.95)
- **Améliorations de l'affichage du live metering** : alignement et positionnement raffinés des composants de métriques audio (v4.4.5)

---

## Audio Triggers : les cinq types de détection et leurs paramètres

Les **Audio Triggers** constituent le cœur du système. Chaque trigger est une règle qui surveille un canal audio spécifique, détecte quand le niveau dépasse un seuil configuré en décibels, et exécute un ou plusieurs commandos en réponse. Voici l'intégralité des types, paramètres et options disponibles.

### Les 5 types de triggers

| Type | Description | Version |
|---|---|---|
| **Human Speaking** | Détecte un présentateur parlant dans un micro studio | Origine |
| **Human Speaking External** | Détecte la parole d'une source externe ou distante | v4.0.9953 |
| **Voice Call** | Détecte un appel téléphonique entrant | Origine |
| **Music** | Détecte la lecture musicale | Origine |
| **Programmed Action Tone** | Détecte une tonalité audio programmée pour des cues d'automatisation | Origine |

### Paramètres de configuration complets d'un trigger

Chaque trigger dispose des paramètres suivants, tous configurables dans l'interface :

| Paramètre | Description |
|---|---|
| **Name** | Nom descriptif (ex. : « Host Mic », « Guest Left », « Music Input ») |
| **Type** | L'un des 5 types de détection ci-dessus |
| **Audio Input** | Canal audio assigné à surveiller |
| **Threshold (dB)** | Seuil d'activation en décibels — plus la valeur est basse, plus le trigger est sensible |
| **Commandos** | Actions déclenchées à l'activation (multiples autorisés) |
| **Release Commandos** | Actions déclenchées à la désactivation (ex. : retour PTZ en position home) |
| **Conditions** | Conditions de planning/programme contrôlant quand le trigger est disponible |
| **Priority** | Niveau de priorité pour la préséance entre triggers simultanés |
| **Attack Trigger** | Mode de déclenchement instantané, sans fenêtre de détection standard — utile pour les tonalités d'action programmées |
| **Enabled** | Activation/désactivation du trigger (triggers désactivés affichés en gris, ne traitent pas l'audio) |
| **Lock** | Verrouillage empêchant les modifications accidentelles |
| **Fast Release** | Paramètre de relâchement rapide du trigger |

La **calibration du seuil** s'effectue via le live meter intégré : on observe le niveau typique du présentateur parlant à volume normal, puis on place le seuil juste en dessous de ce niveau. Le processus est documenté en 5 étapes dans l'interface.

---

## Les 6 types de commandos et le système de commandes

Les **Commandos** sont les actions exécutées par un trigger. Un trigger unique peut déclencher **plusieurs commandos simultanément**, et le système distingue les commandos d'activation et les **Release Commandos** (déclenchés quand le trigger se désactive).

| Action Commando | Description |
|---|---|
| **Camera** | Basculer vers un angle de caméra spécifique |
| **Switcher** | Envoyer une commande au mélangeur vidéo (vMix, ATEM, OBS) |
| **Macro** | Exécuter une séquence macro prédéfinie |
| **Playout** | Contrôler un Output Player |
| **HTTP** | Envoyer une requête HTTP vers un service externe |
| **PTZ** | Déplacer une caméra PTZ vers une position preset |

Les **Internal Audio Commandos** ont été introduits en **v4.0.58**, élargissant les capacités au-delà des déclenchements externes pour permettre des commandes internes au système audio. Le support **GML** (GPIO Markup Language) a été ajouté dans la même version.

À partir de la **v4.2.15**, le système de **Macro Triggers** permet de combiner plusieurs commandos VRA en une seule action macro, avec exécution **séquentielle ou parallèle**, des temps d'attente configurables, des **conditions par item de macro**, et une fonction **Block After** empêchant la poursuite de la macro si une condition n'est pas remplie.

---

## Smart Triggering : l'algorithme de commutation intelligente

Le **Smart Triggering** est la logique intelligente qui empêche les changements de caméra rapides et saccadés lorsque plusieurs présentateurs parlent simultanément. Introduit progressivement entre les versions **v4.0.85 et v4.0.95**, il repose sur trois mécanismes clés :

**Keep Active Speaker Time** (défaut : **20 000 ms**) — Détermine combien de temps un trigger reste « chaud » après activation. Un trigger chaud est moins susceptible d'être réactivé par des interjections courtes (rires, exclamations). Cela garantit que la caméra reste sur le locuteur principal pendant les crosstalk. Introduit en **v4.0.87**.

**Dynamic Switching Time** (défaut : **4 000 ms**) — Temps avant que le système cherche un meilleur plan. Contrôle le délai de commutation entre commandos lorsque des microphones sont actifs. Introduit en **v4.0.87**.

**Smart Commando Looping** — Depuis **v4.0.85**, l'algorithme smart commando s'étend aux scénarios à locuteur unique. Quand un seul trigger audio (ex. MIC 1) est actif, le système **cycle automatiquement à travers les commandos disponibles** après un délai configurable en millisecondes, évitant un plan statique sur un seul angle de caméra.

Le **Damping** constitue un paramètre additionnel de lissage du comportement des triggers, documenté dans la page « Tweak Trigger Settings » (dont le contenu détaillé n'a pas pu être extrait directement mais dont l'existence est confirmée par toutes les références croisées).

---

## Fallback scenarios : le comportement en absence de signal

Les **Fallback Scenarios** définissent le comportement automatique lorsqu'aucun trigger n'est actif — typiquement pendant les silences, les transitions ou la diffusion musicale sans présentateur. Après un **délai configurable**, VRA cycle automatiquement à travers des **fallback commandos** pour maintenir un flux visuel engageant.

Les fonctions de fallback identifiées :

- **Fallback Delay** : délai configurable en secondes avant le déclenchement du scénario de fallback, **sans affecter les triggers de conversation (Talktime)** — permet de retarder le fallback sans ralentir la commutation entre présentateurs (introduit ~v4.0.9953)
- **Fallback Commando cycling** : le système boucle sur un ou plusieurs commandos de fallback (ex. : alterner entre deux caméras grand angle)
- **Output Rundown Fallback** : possibilité de basculer vers un rundown de fallback quand d'autres rundowns ne sont plus actifs, facilitant les rundowns événementiels (v4.0.83)
- **Visual Element Fallback** : fallback pour les éléments visuels des outputs (v4.0.83)
- **Enhanced Fallback Handling** : amélioration de la gestion des fallback commandos pour des opérations plus fiables (v4.5.3)
- **Macro-based Fallback** : via les Macro Triggers (v4.2.15), les temps d'attente permettent de construire des séquences de fallback (ex. : boucle sur deux angles de caméra avec des waits)

---

## Système de conditions : automatisation contextuelle

Le système de **Conditions** permet de rendre les triggers actifs ou inactifs selon le contexte de diffusion. Ce système a évolué significativement au fil des versions :

**Conditions de programme/planning** (v4.0.801) — Les triggers peuvent être limités à des programmes de scheduling spécifiques. Un trigger « Guest Mic » ne s'active que pendant les émissions d'interview. Cela permet de maintenir un jeu unique de triggers tout en adaptant automatiquement le comportement selon le format d'émission.

**Condition Program Host** (v4.2.3) — Il est possible d'utiliser le **Program Host** du programme en cours comme condition pour les Audio Triggers. Cela réalise le scénario où « le microphone DJ déclenche un angle de caméra spécifique au présentateur » — permettant des angles adaptés à la taille, position ou préférences de chaque DJ.

**Condition « A Human Speaking »** (v4.0.95) — Se déclenche quand au moins un trigger de type Human Speaking est actif. Utilisable dans les rundowns, visuals et automations.

**Live Condition Status** — L'Audio Manager récupère correctement le dernier statut de condition live au démarrage (corrigé en v4.2.10), garantissant la cohérence après un redémarrage.

---

## Intégrations matérielles : GPIO, LWRP, EmberPlus et Dante

L'Audio Manager s'intègre avec l'infrastructure matérielle du studio via plusieurs protocoles :

**Dante** — Intégration native via le pilote **Dante Audio WDM** sous Windows. Les flux audio Dante sont présentés comme des périphériques audio OS standard, directement accessibles par l'Audio Manager. Aucune configuration spéciale Dante dans VRA n'est nécessaire au-delà de l'installation du pilote.

**LWRP (Livewire Routing Protocol)** — Support introduit en **v4.5.0** avec authentification par mot de passe. Permet le contrôle GPIO via le protocole Livewire/Axia.

**EmberPlus (Ember+)** — Support GPIO via Ember+ depuis **v4.1.15**, avec indexation automatique des nœuds disponibles dans la connexion Ember+ et sélection dans le Cloud. Étendu en **v4.5.0** avec authentification par mot de passe.

**GPIO** — Contrôle général d'entrées/sorties matérielles, avec corrections de bugs dans la gestion GPIO en **v4.5.3**. Les GPO sont indexés et rendus disponibles pour sélection dans l'interface Cloud.

**GML (GPIO Markup Language)** — Support ajouté en **v4.0.58** parallèlement aux Internal Audio Commandos.

**Mélangeurs vidéo supportés** (via les commandos Switcher) : **vMix**, **Blackmagic Design ATEM** (incluant ATEM Audio Auto Switching), **OBS**. Support des Blackmagic Videohubs pour le routage IO (v4.2.3).

**Caméras PTZ** — Contrôle via commandos PTZ, incluant le support complet **Sony VISCA over IP** depuis **v4.1.15**.

---

## Audio Matrix, Dashboard et outils de monitoring

**Audio Matrix** — Interface visuelle permettant de mapper quels triggers écoutent quels canaux d'entrée. Fonctionne comme une matrice de routage audio-vers-triggers, offrant une vue d'ensemble de la configuration complète du studio.

**Audio Status Dashboard** — Tableau de bord de monitoring en direct affichant l'activité de tous les triggers en temps réel, avec indicateurs visuels de l'état de chaque trigger (actif, inactif, désactivé).

**PANE AUDIOMANAGER STATE** (v4.0.96) — Panneau dédié dans le Dashboard Designer personnalisable, affichant l'état de l'Audio Manager. Intégrable dans des dashboards multi-onglets avec gestion des permissions utilisateurs.

**Trigger History & Condition State Cleanup** — Nettoyage de l'historique des triggers et de l'état des conditions pour maintenir la performance (v4.5.7).

---

## Liens avec le Core, le scheduling et le Studio Designer

L'Audio Manager ne fonctionne pas en isolation — il est profondément intégré avec les autres composants VRA :

**Core** — Communication bidirectionnelle via HTTP TCP. Le Core lie les caméras aux Audio Triggers (améliorations v4.0.52), gère les automations et le scheduling. Les **automations** du Core peuvent être déclenchées par les signaux audio, et inversement, le scheduling peut activer/désactiver des triggers Audio Manager par programme.

**Scheduling** — Les programmes de scheduling contrôlent quels triggers sont actifs via le système de conditions. Un programme peut déclencher un Output onair spécifique ou activer/désactiver un Audio Manager Trigger. La gestion des **Program Hosts** permet jusqu'à **15 présentateurs** par programme (v4.4.4), avec fonctionnalité d'édition des présentateurs.

**Studio Designer** (v4.5.0) — Interface visuelle pour planifier la disposition du studio avec positionnement drag-and-drop des **caméras et microphones**, indicateurs visuels, angles de rotation et perspectives de vue. Les positions des microphones dans le Studio Designer sont liées à la configuration des Audio Triggers.

**Output Player** — Les commandos Playout permettent aux Audio Triggers de contrôler directement les Output Players. Le mode Manual (v4.0.95) avec fonctionnalité « Take Onair » s'intègre avec le Dashboard pour le handover producteur.

---

## Évolution version par version : chronologie complète

| Version | Date | Changements Audio Manager |
|---|---|---|
| **v4.0.52** | Jan 2023 | Lien Camera → Audio Trigger amélioré ; UX Release Command améliorée |
| **v4.0.53/55** | ~2023 | Fix récupération correcte des Audio Inputs |
| **v4.0.58** | ~2023 | Internal Audio Commandos ; support GML |
| **v4.0.801** | ~2023 | Lancement des Conditions pour Audio Triggers |
| **v4.0.83** | ~2023 | Output Rundown Fallback ; Visual Element Fallback |
| **v4.0.85** | ~2023 | Smart Commando Looping pour scénarios à locuteur unique |
| **v4.0.87** | ~2023 | Dynamic Switching Time (4000ms) ; Keep Active Speaker Time (20s) |
| **v4.0.95** | ~Mi-2023 | Condition « A Human Speaking » ; Reload audio devices (tray + auto 12h) ; Nouvel algorithme sample rate ; Smart Commando Looping documenté |
| **v4.0.96** | Jun 2023 | PANE AUDIOMANAGER STATE dans Dashboard Designer |
| **v4.0.9953** | ~Mi-2023 | Type Human Speaking External ; Fallback Fine Tuning (delay X sec) ; Stabilité conditions |
| **v4.1.15** | Mar 2024 | Ember+ GPIO : indexation des nœuds ; Sony VISCA over IP |
| **v4.2.3** | ~Déc 2024 | Program Host comme condition Audio Triggers ; Fix trigger désactivé sans condition |
| **v4.2.10** | Jan 2025 | Récupération correcte Live Condition au démarrage ; Fix version Download Center |
| **v4.2.15** | ~Fév 2025 | Macro Triggers (multi-commandos, séquentiel/parallèle, Block After) ; Fix sauvegarde Audio Trigger |
| **v4.3.0** | Avr 2025 | Fix erreur sauvegarde Audio Trigger |
| **v4.4.4** | Août 2025 | Edit presenter ; Program Host jusqu'à 15 hosts |
| **v4.4.5** | Sep 2025 | Affichage Live Metering affiné |
| **v4.5.0** | Sep 2025 | Studio Designer (positionnement caméras/micros) ; LWRP + EmberPlus avec authentification mot de passe |
| **v4.5.3** | Oct 2025 | Restructuration complète Audio Settings ; Terminologie « Commandos » ; Enhanced Fallback Handling ; Fix conditions triggers ; Fix GPIO |
| **v4.5.4** | Oct 2025 | Stabilité vérification triggers actifs |
| **v4.5.7** | Nov 2025 | Avertissements entrées audio inactives ; Nettoyage historique triggers/conditions |
| **v4.5.8** | Déc 2025 | Fix calcul compteur Audio Manager |
| **v4.5.20** | Jan 2026 | Fix conditions Program Host dans Audio Triggers |
| **v4.5.25** | Jan 2026 | Fix null checks interfaces conditionnelles inputs/triggers |
| **v4.6.11** | Fév 2026 | Fix Audio Trigger Watcher (états incorrects, comportements non intentionnels) |
| **v4.6.20** | Mar 2026 | Fix événements audio trigger mal traités |
| **v4.6.24** | Mar 2026 | Renouvellement certificat local (Core, Audio Manager, Output Player) |
| **v4.6.27** | Avr 2026 | Combobox de recherche dans la configuration Audio Manager |

---

## Conclusion

L'Audio Manager de VRA est un système d'automatisation complet articulé autour de **5 types de détection audio, 6 types de commandos, un algorithme de Smart Triggering à 3 paramètres (Keep Active Speaker Time, Dynamic Switching Time, Smart Commando Looping), et un système de Fallback à délai configurable**. Son intégration matérielle couvre Dante, LWRP, EmberPlus et GPIO, tandis que son système de conditions lie chaque trigger au contexte de diffusion (programme, présentateur, état audio). L'Audio Matrix offre une vue synthétique de la configuration, et le Dashboard un monitoring temps réel. Depuis v4.0.52 (janvier 2023) jusqu'à v4.6.27 (avril 2026), le système a connu **plus de 30 itérations** documentées, passant d'un outil de détection basique à une plateforme d'orchestration visuelle pilotée par l'audio avec détection intelligente de la parole, gestion multi-présentateurs, et automatisation contextuelle complète.