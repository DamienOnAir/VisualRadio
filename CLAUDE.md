# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Projet : Visual Radio Assist (VRA)

Plateforme hybride **Cloud + Local** pour automatiser la "Visual Radio" (radio → expérience visuelle SDI/NDI/web).
Société : **VisualRadioAssist B.V.** · Version : **4.6.24** (avril 2026)
Docs : https://docs.visualradioassist.live

---

## Architecture

```
CLOUD (visualradio.cloud)
  GraphQL API · Scheduling · Visuals · Dashboards
        │ HTTPS / WebSocket
CORE (Node.js — port 3002)
  REST API · Bus événementiel · Drivers : ATEM · vMix · OBS · PTZ · GPIO
     │ HTTP local                    │ HTTP local
AUDIO MANAGER (.NET/C#)         OUTPUT PLAYER (Electron/Node)
  ASIO · WASAPI · ZenonMedia       NDI · SDI · GPU rendering
```

Ce dépôt héberge le développement du composant **Audio Manager** (`AUDIO_MANAGER/`).

---

## Composants et stack

### Audio Manager (ce dépôt)
- **Runtime** : .NET 6+ / C#
- **Sources audio** : ASIO, WASAPI, WebSocket ZenonMedia, GPIO DHD
- **Rôle** : analyse niveaux audio en temps réel → triggers HTTP vers le Core (port 3002)
- **Gate audio** : détection d'activité micro avec suppression de réverbe studio (éviter la repisse)
- **Notification Core** : `POST http://localhost:3002/commando` avec Basic Auth

### Core (externe, référence)
- Node.js · Express/Fastify · port `3002` · Auth : `Basic base64(EXTERNAL_APP:token)`
- WebSocket embarqué pour bus local bidirectionnel
- Stockage : SQLite ou JSON local

### Output Player (externe)
- Electron/Node · GPU-accelerated (NVIDIA GTX 1660 min) · sorties NDI + SDI (DeckLink PCIe)

---

## APIs locales

```
Core Control API — http://localhost:3002
Auth: Authorization: Basic base64(EXTERNAL_APP:token)

POST /commando          → déclencher un commando
GET  /state             → état courant du studio
POST /automationlink    → injecter métadonnées "Now Playing"
                          Payload: artiste, titre, durée, type (music/talk/jingle)
```

```
Cloud GraphQL — https://visualradio.cloud/graphql
Auth: Bearer token (Machine User, durée max 2 ans)
Accès: scheduling, outputs, variables, presenters, stations
```

---

## Intégrations hardware

| Équipement | Protocole | Port |
|---|---|---|
| ATEM Switcher | TCP binaire (`atem-connection` Bitfocus) | 9910 |
| vMix | TCP XML (`node-vmix`) + HTTP XML | 8099 / 8088 |
| OBS | WebSocket v5 (`obs-websocket-js`) | 4455 |
| HyperDeck | TCP texte (HyperDeck Ethernet Protocol) | 9993 |
| PTZ Camera | VISCA over IP / RS-232 | — |
| Dante Audio | WDM Driver Windows | — |
| GPIO | LWRP / EmberPlus TCP | — |
| DeckLink (SDI) | SDK Blackmagic PCIe | — |

---

## Conventions de nomenclature VRA

- **Commando** : action atomique déclenchable (orthographe VRA, pas "command")
- **Output** : signal vidéo généré par l'Output Player
- **Rundown** : liste ordonnée d'éléments visuels d'un Output
- **Visual** : graphique animé (HTML/CSS/JS, Lottie/Bodymovin)
- **Studio** : instance logique d'un studio radio (multi-studio possible)
- **Station** : entité parente regroupant plusieurs studios
- **AutomationLink** : pont entre logiciel radio automation (Zetta, Selector, RCS) et VRA
- **Machine User** : compte API non-humain avec token révocable

---

## Points d'attention pour le développement

- **Core requis localement** : le Cloud seul ne suffit pas pour l'automatisation.
- **Certificats locaux** : communication locale en HTTPS — installer `vra-local-cert-bundle.zip` dans le certificate store OS.
- **ATEM** : maintenir une instance `atem-connection` persistante — ne pas recréer à chaque commande.
- **vMix TCP** : connexion persistante requise pour les événements tally temps réel (`SUBSCRIBE TALLY`).
- **Automations scoped** (v4.6.11+) : règles d'automation scopées au studio — vigilance en déploiement multi-studio.
- **Machine Users** : préférer aux comptes humains pour toute intégration programmatique.
- **Variables** : deux niveaux — Visual Variables (scope local visuel) et Station Variables (globales à la station).





## Conventions de nommage

### TypeScript (Core, Drivers, API)
- Fonctions & variables : `camelCase`
- Classes, Interfaces, Types : `PascalCase`
- Constantes : `SCREAMING_SNAKE_CASE`
- Fichiers : `kebab-case` (ex: `atem-driver.ts`)
- Event handlers : préfixe `on` en camelCase (ex: `onMicOpen`)

### C# (Audio Manager)
- Méthodes & classes & propriétés : `PascalCase`
- Champs privés : `_camelCase`

### API REST
- Endpoints : `kebab-case` (ex: `/api/audio-triggers`)

### Variables d'environnement
- `SCREAMING_SNAKE_CASE` avec préfixe `VRA_`
