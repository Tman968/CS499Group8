import { useState } from "react";
import { useNavigate, useLocation } from "react-router";
import { useAuth } from "../context/AuthContext";

export default function AccountPage() {
  const navigate = useNavigate();
  const location = useLocation();

  const {
    player,
    checkingSession,
    sessionError,
    login,
    register,
    logout
  } = useAuth();

  const [registering, setRegistering] = useState(false);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");

  async function handleSubmit(event) {
    event.preventDefault();
    setLoading(true);
    setMessage("");

    try {
      if (registering) {
        await register(username, password);
        setRegistering(false);
        setMessage("Account created! Please log in.");
      } else {
        await login(username, password);
        const requested = location.state?.from;

        const destination =
          typeof requested === "string" &&
          requested.startsWith("/") &&
          !requested.startsWith("//")
            ? requested
            : "/";

        navigate(destination, { replace: true });
      }

      setPassword("");
    } catch (error) {
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  async function handleLogout() {
    setLoading(true);
    setMessage("");

    try {
      await logout();
      setPassword("");
      setMessage("Successfully logged out.");
    } catch (error) {
      setMessage(error.message);
    } finally {
      setLoading(false);
    }
  }

  if (checkingSession) {
    return <p>Checking account session...</p>;
  }

  if (player) {
    return (
      <section className="jc-account">
        <h2>JumpCraft Account</h2>

        <p>Welcome, {player.username}!</p>
        <p>Player ID: {player.id}</p>

        <button
          type="button"
          onClick={handleLogout}
          disabled={loading}
        >
          Log Out
        </button>

        {message && <p role="status">{message}</p>}
      </section>
    );
  }

  return (
    <section className="jc-account">
      <h2>{registering ? "Create Account" : "Sign In"}</h2>

      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="username">Username</label>
          <input
            id="username"
            type="text"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            minLength={3}
            maxLength={20}
            pattern="[A-Za-z0-9_]{3,20}"
            required
          />
        </div>

        <div>
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete={
              registering ? "new-password" : "current-password"
            }
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={registering ? 12 : undefined}
            maxLength={128}
            required
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading
            ? "Please wait..."
            : registering
              ? "Create Account"
              : "Log In"}
        </button>
      </form>

      <button
        type="button"
        onClick={() => {
          setRegistering(!registering);
          setMessage("");
          setPassword("");
        }}
      >
        {registering
          ? "Already have an account? Log in"
          : "Don't have an account? Register"}
      </button>

      {(message || sessionError) && (
        <p role="status">{message || sessionError}</p>
      )}
    </section>
  );
}