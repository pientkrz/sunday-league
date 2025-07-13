# React + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and [`typescript-eslint`](https://typescript-eslint.io) in your project.

## Tech Stack

- **Frontend:** React (with Vite)

## Frontend App Description
The application is a football league management system with the following features:
- **Leagues**: The app can manage multiple leagues.
- **Seasons**: Each league can have multiple seasons (e.g., 2023-2024, 2024-2025).
- **Teams**: View team information and their seasonal match history.
- **Match History**: View all matches for a given season, filterable by team.
- **Standings**: A full leaderboard table for each season, calculated based on standard football rules (3 points for a win, 1 for a draw). Standings are sorted by points, then goal difference, then goals for.
- **Matchmaking**: A feature to automatically generate a round-robin schedule for a season, where every team plays every other team once. The frequency of matches (e.g., weekly) can be configured per league.