import { useState } from "react";
import {
  NavLink,
  Outlet,
  useLocation,
  useNavigate
} from "react-router";
import { useAuth } from "../context/AuthContext";

const menuItems = [
  { name: "Play", path: "/" },
  { name: "Friends", path: "/friends" },
  { name: "Statistics", path: "/statistics" },
  { name: "Achievements", path: "/achievements" },
  { name: "Profile", path: "/profile" },
  { name: "Settings", path: "/settings" },
];

const pageTitles = {
  "/": "JumpCraft",
  "/friends": "Friends",
  "/statistics": "Statistics",
  "/achievements": "Achievements",
  "/profile": "Profile",
  "/settings": "Settings",
  "/play": "JumpCraft",
};

function Layout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [logoutError, setLogoutError] = useState("");

  const location = useLocation();
  const navigate = useNavigate();

  const { player, logout } = useAuth();

  const isProfile = location.pathname === "/profile";

  const title = isProfile && player
    ? player.username
    : pageTitles[location.pathname] || "JumpCraft";

  async function handleLogout() {
    setLogoutError("");

    try {
      await logout();
      setMenuOpen(false);
      navigate("/login", { replace: true });
    } catch (error) {
      setLogoutError(error.message);
    }
  }

  return (
    <div className="site-frame">
      <header className="site-header">
        <button
          className="menu-button"
          onClick={() => setMenuOpen(!menuOpen)}
          aria-label="Open navigation menu"
          aria-expanded={menuOpen}
          type="button"
        >
          ☰
        </button>

        <h1 className="site-title">{title}</h1>

        <NavLink
          to={isProfile ? "/" : player ? "/profile" : "/login"}
          className={`profile-button ${isProfile ? "home-button" : ""}`}
        >
          {isProfile ? "Lobby" : player?.username ?? "Log In"}
        </NavLink>
      </header>

      {menuOpen && (
        <>
          <button
            className="menu-overlay"
            aria-label="Close navigation menu"
            onClick={() => setMenuOpen(false)}
            type="button"
          />

          <aside className="sidebar">
            <button
              className="close-menu"
              onClick={() => setMenuOpen(false)}
              aria-label="Close menu"
              type="button"
            >
              ×
            </button>

            <nav className="sidebar-nav" aria-label="Main navigation">
              {menuItems.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === "/"}
                  onClick={() => setMenuOpen(false)}
                  className={({ isActive }) =>
                    `sidebar-link ${isActive ? "active" : ""}`
                  }
                >
                  {item.name}
                </NavLink>
              ))}
            </nav>

            {player ? (
              <button
                type="button"
                className="sidebar-link logout"
                onClick={handleLogout}
              >
                Logout
              </button>
            ) : (
              <NavLink
                to="/login"
                className="sidebar-link"
                onClick={() => setMenuOpen(false)}
              >
                Log In
              </NavLink>
            )}

            {logoutError && (
              <p role="alert">{logoutError}</p>
            )}
          </aside>
        </>
      )}

      <main className="site-content">
        <Outlet />
      </main>
    </div>
  );
}

export default Layout;