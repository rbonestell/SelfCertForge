// EmptyState.jsx — empty-state pattern with forge gradient circle
const EmptyState = ({ title, body, ctaLabel, onCta, icon = "shield-check" }) => (
  <div style={{
    height: "100%",
    display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
    padding: 40, gap: 18, textAlign: "center",
  }}>
    <div style={{
      width: 72, height: 72, borderRadius: "50%",
      background: "var(--forge-gradient-radial)",
      display: "flex", alignItems: "center", justifyContent: "center",
      boxShadow: "0 0 48px rgba(255,106,0,0.35)",
    }}>
      <Icon name={icon} size={32} color="#0B0C10" strokeWidth={2} />
    </div>
    <div style={{ display: "flex", flexDirection: "column", gap: 6, maxWidth: 360 }}>
      <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: "#F2F3F6", letterSpacing: "-0.01em" }}>{title}</h2>
      <p style={{ margin: 0, fontSize: 13, color: "#B7BBC6", lineHeight: 1.5 }}>{body}</p>
    </div>
    {ctaLabel && <Button variant="primary" icon="hammer" onClick={onCta}>{ctaLabel}</Button>}
  </div>
);

Object.assign(window, { EmptyState });
