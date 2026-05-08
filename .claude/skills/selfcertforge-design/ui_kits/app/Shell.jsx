// Shell.jsx — title bar, sidebar nav, content area
const { useState: useStateShell } = React;

const NavItem = ({ icon, label, badge, active, onClick }) => (
  <button
    onClick={onClick}
    style={{
      display: "flex", alignItems: "center", gap: 10,
      width: "100%",
      padding: "8px 12px",
      borderRadius: 8,
      border: "none",
      background: active ? "#232631" : "transparent",
      color: active ? "#F2F3F6" : "#B7BBC6",
      fontFamily: "var(--font-ui)",
      fontSize: 13,
      fontWeight: 500,
      cursor: "pointer",
      textAlign: "left",
      position: "relative",
    }}
    onMouseEnter={(e) => { if (!active) { e.currentTarget.style.background = "#1B1D25"; e.currentTarget.style.color = "#F2F3F6"; } }}
    onMouseLeave={(e) => { if (!active) { e.currentTarget.style.background = "transparent"; e.currentTarget.style.color = "#B7BBC6"; } }}
  >
    {active && (
      <span style={{ position: "absolute", left: -8, top: 8, bottom: 8, width: 2, background: "#FF6A00", borderRadius: 2 }} />
    )}
    <Icon name={icon} size={15} color={active ? "#FF6A00" : "currentColor"} />
    <span style={{ flex: 1 }}>{label}</span>
    {badge != null && (
      <span style={{
        fontFamily: "var(--font-mono)",
        fontSize: 10, fontWeight: 600,
        color: "#7C818E",
        background: "#12141A",
        border: "1px solid #2F333D",
        borderRadius: 999, padding: "1px 6px",
      }}>{badge}</span>
    )}
  </button>
);

const Sidebar = ({ active, onChange }) => {
  const items = [
    { id: "dashboard",   icon: "layout-dashboard", label: "Dashboard" },
    { id: "authorities", icon: "shield-check",      label: "Root Authorities", badge: 2 },
    { id: "certificates",icon: "scroll-text",       label: "Certificates", badge: 7 },
    { id: "forge",       icon: "hammer",            label: "Forge New" },
    { id: "trust",       icon: "key-round",         label: "Trust Store" },
    { id: "settings",    icon: "settings-2",        label: "Settings" },
  ];
  return (
    <aside style={{
      width: 220,
      background: "#12141A",
      borderRight: "1px solid #2F333D",
      display: "flex",
      flexDirection: "column",
      padding: 12,
      gap: 2,
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "6px 8px 14px" }}>
        <img src="../../assets/icon.png" alt="" style={{ width: 32, height: 32 }} />
        <div style={{ display: "flex", flexDirection: "column", lineHeight: 1.1 }}>
          <span style={{ fontSize: 13, fontWeight: 700, color: "#F2F3F6", letterSpacing: "-0.01em" }}>
            SelfCert<span style={{ background: "var(--forge-gradient)", WebkitBackgroundClip: "text", backgroundClip: "text", color: "transparent" }}>Forge</span>
          </span>
          <span style={{ fontSize: 9, color: "#7C818E", letterSpacing: ".18em", marginTop: 2 }}>LOCAL · v1.0</span>
        </div>
      </div>
      {items.map((it) => (
        <NavItem key={it.id} {...it} active={active === it.id} onClick={() => onChange(it.id)} />
      ))}
      <div style={{ flex: 1 }} />
      <div style={{
        padding: 10,
        borderRadius: 10,
        background: "#1B1D25",
        border: "1px solid #2F333D",
        display: "flex", flexDirection: "column", gap: 4,
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11, color: "#B7BBC6", fontWeight: 600 }}>
          <span style={{ width: 6, height: 6, borderRadius: 999, background: "#38C172", boxShadow: "0 0 6px #38C172" }} />
          Trust store synced
        </div>
        <span style={{ fontSize: 10, color: "#7C818E", fontFamily: "var(--font-mono)" }}>~/.selfcertforge/store</span>
      </div>
    </aside>
  );
};

const TitleBar = ({ onForge }) => (
  <div style={{
    height: 44,
    background: "#0B0C10",
    borderBottom: "1px solid #2F333D",
    display: "flex", alignItems: "center",
    padding: "0 14px",
    gap: 12,
    WebkitAppRegion: "drag",
  }}>
    <div style={{ display: "flex", gap: 6 }}>
      <span style={{ width: 11, height: 11, borderRadius: 999, background: "#E5484D" }} />
      <span style={{ width: 11, height: 11, borderRadius: 999, background: "#FFB800" }} />
      <span style={{ width: 11, height: 11, borderRadius: 999, background: "#38C172" }} />
    </div>
    <span style={{ marginLeft: 12, fontSize: 12, color: "#7C818E", fontWeight: 500 }}>SelfCertForge</span>
    <span style={{ flex: 1 }} />
    <Button variant="primary" icon="hammer" onClick={onForge} style={{ WebkitAppRegion: "no-drag" }}>Forge Certificate</Button>
  </div>
);

const Shell = ({ active, onNav, onForge, children }) => (
  <div style={{
    height: "100vh",
    background: "#0B0C10",
    display: "flex", flexDirection: "column",
    fontFamily: "var(--font-ui)",
    color: "#F2F3F6",
    overflow: "hidden",
  }}>
    <TitleBar onForge={onForge} />
    <div style={{ flex: 1, display: "flex", overflow: "hidden" }}>
      <Sidebar active={active} onChange={onNav} />
      <main style={{ flex: 1, overflow: "auto" }}>{children}</main>
    </div>
  </div>
);

Object.assign(window, { Shell, Sidebar, TitleBar, NavItem });
