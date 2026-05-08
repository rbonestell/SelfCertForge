// Dashboard.jsx — summary view
const StatCard = ({ label, value, sub, accent }) => (
  <div style={{
    background: "#1B1D25",
    border: "1px solid #2F333D",
    borderRadius: 16, padding: 18,
    display: "flex", flexDirection: "column", gap: 6,
  }}>
    <span style={{ fontSize: 10, textTransform: "uppercase", letterSpacing: ".08em", color: "#7C818E", fontWeight: 600 }}>{label}</span>
    <span style={{ fontSize: 26, fontWeight: 700, color: accent || "#F2F3F6", letterSpacing: "-0.02em", lineHeight: 1 }}>{value}</span>
    <span style={{ fontSize: 11, color: "#7C818E", fontFamily: "var(--font-mono)" }}>{sub}</span>
  </div>
);

const ActivityRow = ({ when, action, target, kind }) => (
  <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "10px 14px", borderTop: "1px solid #2F333D" }}>
    <span style={{ width: 6, height: 6, borderRadius: 999, background: kind === "forge" ? "#FF6A00" : kind === "trust" ? "#38C172" : "#7C818E" }} />
    <span style={{ fontSize: 13, color: "#F2F3F6", flex: 1 }}>{action} <span style={{ fontFamily: "var(--font-mono)", fontSize: 12, color: "#B7BBC6" }}>{target}</span></span>
    <span style={{ fontSize: 11, color: "#7C818E", fontFamily: "var(--font-mono)" }}>{when}</span>
  </div>
);

const Dashboard = ({ onForge }) => (
  <div style={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 20 }}>
    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16 }}>
      <div>
        <h1 style={{ margin: 0, fontSize: 28, fontWeight: 600, letterSpacing: "-0.015em", color: "#F2F3F6" }}>Dashboard</h1>
        <p style={{ margin: "4px 0 0", fontSize: 13, color: "#B7BBC6" }}>Local certificate operations and trust state.</p>
      </div>
      <Button variant="primary" icon="hammer" onClick={onForge}>Forge Certificate</Button>
    </div>

    <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 14 }}>
      <StatCard label="Trusted certificates" value="7" sub="installed in trust store" accent="#38C172" />
      <StatCard label="Root authorities" value="2" sub="local CAs" />
      <StatCard label="Expiring soon" value="1" sub="< 14 days" accent="#FFB800" />
      <StatCard label="Self-signed" value="3" sub="not yet trusted" accent="#FF6A00" />
    </div>

    <div style={{ background: "#1B1D25", border: "1px solid #2F333D", borderRadius: 16, overflow: "hidden" }}>
      <div style={{ padding: "14px 18px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <h2 style={{ margin: 0, fontSize: 14, fontWeight: 600, color: "#F2F3F6" }}>Recent activity</h2>
        <span style={{ fontSize: 11, color: "#7C818E", fontFamily: "var(--font-mono)" }}>last 24h</span>
      </div>
      <ActivityRow when="2m ago" action="Forged" target="local.dev.forge" kind="forge" />
      <ActivityRow when="14m ago" action="Installed in trust store" target="api.local.dev" kind="trust" />
      <ActivityRow when="1h ago" action="Forged" target="*.dev.local" kind="forge" />
      <ActivityRow when="3h ago" action="Created root authority" target="Local Root CA" kind="forge" />
      <ActivityRow when="yesterday" action="Removed" target="legacy.test" kind="other" />
    </div>
  </div>
);

Object.assign(window, { Dashboard });
