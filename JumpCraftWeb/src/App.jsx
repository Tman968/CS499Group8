import { Routes, Route, Navigate, Link } from "react-router";
import Layout from "./components/Layout";
import "./App.css";

function Home() {
  return (
    <section className="placeholder-page">
      <h2>Player Lobby</h2>
      <p>Our multiplayer lobby will go here.</p>

      <Link to="/play" className="sprite-test-button">
        Play JumpCraft
      </Link>
    </section>
  );
}

function PlaceholderPage({ name }) {
  return (
    <section className="placeholder-page">
      <h2>{name}</h2>
      <p>This page is under construction.</p>
    </section>
  );
}

function Login() {
  return (
    <div className="login-page">
      <h1>JumpCraft</h1>
      <section className="login-panel">
        <h2>Sign in</h2>
        <p>Login functionality coming soon.</p>
      </section>
    </div>
  );
}

function Game() {
  return (
    <section className="game-page">
      <h2>Play JumpCraft</h2>

      <iframe
        title="JumpCraft Game"
        src="/game/index.html"
        className="game-frame"
        allowFullScreen
      />

      <p>Click inside the game to use keyboard controls.</p>
    </section>
  );
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route element={<Layout />}>
        <Route path="/" element={<Home />} />
        <Route path="/friends" element={<PlaceholderPage name="Friends" />} />
        <Route path="/statistics" element={<PlaceholderPage name="Statistics" />} />
        <Route path="/achievements" element={<PlaceholderPage name="Achievements" />} />
        <Route path="/profile" element={<PlaceholderPage name="Profile" />} />
        <Route path="/settings" element={<PlaceholderPage name="Settings" />} />
        <Route path="/play" element={<Game />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;
