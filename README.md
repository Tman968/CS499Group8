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
