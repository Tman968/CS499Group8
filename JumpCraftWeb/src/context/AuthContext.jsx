import {
  createContext,
  useContext,
  useEffect,
  useState
} from "react";

const AuthContext = createContext(null);

const API_URL =
  import.meta.env.VITE_API_URL || "http://localhost:3001";

async function apiRequest(path, options = {}) {
  let response;

  try {
    response = await fetch(`${API_URL}${path}`, {
      credentials: "include",
      ...options
    });
  } catch {
    throw new Error("Cannot connect to JumpCraft server.");
  }

  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    const error = new Error(data.error || "Request failed");
    error.status = response.status;
    throw error;
  }

  return data;
}

export function AuthProvider({ children }) {
  const [player, setPlayer] = useState(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const [sessionError, setSessionError] = useState("");

  useEffect(() => {
    let active = true;

    apiRequest("/api/auth/me")
      .then((data) => {
        if (active) setPlayer(data.player);
      })
      .catch((error) => {
        if (active && error.status !== 401) {
          setSessionError(error.message);
        }
      })
      .finally(() => {
        if (active) setCheckingSession(false);
      });

    return () => {
      active = false;
    };
  }, []);

  async function login(username, password) {
    const data = await apiRequest("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password })
    });

    setPlayer(data.player);
    setSessionError("");
    return data.player;
  }

  async function register(username, password) {
    return apiRequest("/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password })
    });
  }

  async function logout() {
    await apiRequest("/api/auth/logout", {
      method: "POST"
    });

    setPlayer(null);
  }

  return (
    <AuthContext.Provider
      value={{
        player,
        checkingSession,
        sessionError,
        login,
        register,
        logout
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth requires AuthProvider");
  }

  return context;
}