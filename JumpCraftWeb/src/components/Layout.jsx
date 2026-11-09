
import { useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router";

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
  "/profile": "EthanEthos",
  "/settings": "Settings",
  "/play": "JumpCraft",
};

function Layout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();

  const isProfile = location.pathname === "/profile";
  const title = pageTitles[location.pathname] || "JumpCraft";

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
          to={isProfile ? "/" : "/profile"}
          className={`profile-button ${isProfile ? "home-button" : ""}`}
        >
          {isProfile ? "Lobby" : "Profile"}
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

            <NavLink
              to="/login"
              className="sidebar-link logout"
              onClick={() => setMenuOpen(false)}
            >
              Logout
            </NavLink>
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
