# Cōntry

Client-side country guessing game inspired by [Wordle](https://wordlegame.org/)/[Countryle](https://countryle.com/). Each round picks a hidden country, and every guess returns clue-based feedback to help narrow down the answer.

>Play it here: [https://contry.app/](https://contry.app/)

## Overview

- Built as a browser-only app
- Persists game state, datasets, settings, and stats locally with IndexedDB
- Uses a world map, clue chips, autocomplete, and round history to guide each guess
- Supports built-in clues and custom user-created clues

## Features

- Country guessing gameplay with clue feedback
- Numeric, categorical, and directional/geographic clues
- Interactive SVG world map
- Local persistence for active games and statistics
- Custom clue creation, editing, and dataset import
- Multi-tab sync for game and clue catalog updates
- Mobile-first interface

## Tech Stack

- Svelte 5
- TypeScript
- Vite
- Bun
- Vanilla CSS
- IndexedDB via `idb`
- D3 + `world-atlas` + `topojson-client` for the map
- `papaparse` for dataset ingestion
- `lucide-svelte` for icons
- `canvas-confetti` for win effects

## Running Locally

```bash
bun install
bun run dev
```

The app runs the dataset manifest generation and validation steps automatically before dev and build.

## Scripts

- `bun run dev` - start the Vite dev server
- `bun run build` - create a production build
- `bun run preview` - preview the production build locally
- `bun run check` - run Svelte and TypeScript checks
- `bun run datasets` - regenerate the dataset manifest
- `bun run datasets:validate` - validate dataset integrity

## Screenshots

### Main Game View

![Main Game View](https://files.catbox.moe/nt63la.png)

### Map / Guess Feedback

![Map / Guess Feedback](https://files.catbox.moe/e3ejcg.png)

### Custom Clues / Settings

![Settings](https://files.catbox.moe/5p83sl.png)
![Add Custom Clue](https://files.catbox.moe/cmkrbz.png)
![Arranging Custom Clues](https://files.catbox.moe/qs16hf.png)

### Stats View

![Stats Modal View](https://files.catbox.moe/vu4vpj.png)
![Discovered Countries](https://files.catbox.moe/bu7xl0.png)

## Notes

- This project does not use a backend.
- Game progress and supporting datasets stay in the browser.
- Dataset metadata is generated from files in `public/datasets/`.
