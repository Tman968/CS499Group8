# CS499Group8

The purpose of this project is to develop an interactive video game that can be played directly through a website. 

Unity Version: 6000.6.0f1

## Project layout

- `JumpCraftGame/` – the Unity project (open this folder in Unity Hub with **Add > Add project from disk**)

## Building for the web (WebGL)

Requires the **Web Build Support** module for 6000.6.0f1 (Unity Hub > Installs > ⚙ > Add modules).

In the Unity Editor, use the **JumpCraft > WebGL** menu:

- **Switch Platform to WebGL** – do this once after cloning so you work with web-accurate settings.
- **Build and Run (local test)** – builds and opens the game in your browser.
- **Build** – outputs to `JumpCraftGame/Builds/WebGL/` (not committed to git). Upload the contents of that folder to any static web host.

Builds use Gzip compression with decompression fallback, so they work on hosts that don't send compression headers (e.g. GitHub Pages, itch.io).

## Publishing to the website (GitHub Pages)

The game is served at **https://tman968.github.io/CS499Group8/** from the `gh-pages` branch.

1. In Unity: **JumpCraft > WebGL > Build**
2. From the repo folder in Git Bash (Windows) or a terminal (macOS/Linux):
   ```
   ./Tools/deploy-pages.sh
   ```

The script replaces the `gh-pages` branch with the new build, so only the latest build is kept. The site updates a minute or two after the push.

One-time setup (repo admin): **Settings > Pages > Build and deployment** — Source: *Deploy from a branch*, Branch: `gh-pages` / `(root)`.

# JumpCraft Website Dev/Build

The JumpCraft website is the React-based frontend for JumpCraft. It is built with **React** and **Vite**, uses **React Router** for navigation, and is designed to host the JumpCraft **Unity WebGL** game.

This guide explains how to set up, run, and build the React website locally.

## Prerequisites

Install this first

- **Node.js and npm:** Install a current Node.js LTS version compatible with the project's Vite version from [nodejs.org](https://nodejs.org/). npm is included with Node.js.
- **Git:** [git-scm.com](https://git-scm.com/)
- **Code editor:** [Visual Studio Code](https://code.visualstudio.com/) (recommended)

Open a terminal and check that Node.js, npm, and Git are installed:

```powershell
node --version
npm --version
git --version
```

## 1. Clone the repository

If you do not already have the repository on your computer, clone it:

```powershell
git clone https://github.com/Tman968/CS499Group8.git
cd CS499Group8
```

## 2. Open the React website folder

From the repository root, navigate to the React project:

```powershell
cd JumpCraftWeb
```

## 3. Install dependencies

Run:

```powershell
npm install
```

This installs the packages we need including Vite and React

## 4. Start the development server

Run:

```powershell
npm run dev
```

Open http://localhost:5173/ or whatever cmd prompt tells you to view dev server

* No need to relaunch Vite server to see changes


## 5. Build the website

To generate a production build of the React website, run:

```powershell
npm run build
```

Vite outputs the website's production-ready static files to:

```text
JumpCraftWeb/dist/
```

**Note:** This builds the **React website only**. The Unity game needs its own Unity WebGL export, which can later be integrated into the website.

## 6. Preview the production build

After a successful build, run:

```powershell
npm run preview
```

Open http://localhost:4173/ or whatever cmd prompt tells you to view the preview server
## Common development commands

Run these commands from the **`JumpCraftWeb`** folder:

| Command | Purpose |
| --- | --- |
| `npm install` | Install project dependencies |
| `npm run dev` | Start the development server |
| `npm run build` | Build the React website for production |
| `npm run preview` | Preview the production build locally |
| `npm run lint` | Run the project's linter, if the script is defined in `package.json` |
