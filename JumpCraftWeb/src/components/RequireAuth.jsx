import { Navigate, Outlet, useLocation } from "react-router";
import { useAuth } from "../context/AuthContext";

export default function RequireAuth() {
  const { player, checkingSession, sessionError } = useAuth();
  const location = useLocation();

  // Wait until authentication has been checked.
  if (checkingSession) {
    return <p>Checking authentication...</p>;
  }

  // Avoid treating a server connection failure as a logout.
  if (sessionError) {
    return (
      <p role="alert">
        Unable to verify your session: {sessionError}
      </p>
    );
  }

  // Redirect unauthenticated players to login.
  if (!player) {
    return (
      <Navigate
        to="/login"
        state={{
          from: location.pathname + location.search + location.hash
        }}
        replace
      />
    );
  }

  // Player is authenticated.
  return <Outlet />;
}