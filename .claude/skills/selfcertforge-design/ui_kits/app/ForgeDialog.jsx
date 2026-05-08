// ForgeDialog.jsx — modal for forging a new certificate
const ForgeDialog = ({ open, onClose, onForge, authorities }) => {
  const [cn, setCn] = React.useState("local.dev.forge");
  const [sans, setSans] = React.useState("dev.local, *.dev.local");
  const [days, setDays] = React.useState(365);
  const [authority, setAuthority] = React.useState(authorities[0]?.id);
  const [installLocally, setInstallLocally] = React.useState(true);
  const [forging, setForging] = React.useState(false);

  if (!open) return null;

  const handleForge = () => {
    setForging(true);
    setTimeout(() => {
      setForging(false);
      onForge({ cn, sans, days, authority, installLocally });
    }, 700);
  };

  return (
    <div
      onClick={onClose}
      style={{
        position: "fixed", inset: 0,
        background: "rgba(11,12,16,0.7)",
        backdropFilter: "blur(8px)",
        display: "flex", alignItems: "center", justifyContent: "center",
        zIndex: 100,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          background: "#232631",
          border: "1px solid #2F333D",
          borderRadius: 20,
          width: 520,
          padding: 24,
          boxShadow: "0 12px 32px rgba(0,0,0,0.55)",
          display: "flex", flexDirection: "column", gap: 20,
        }}
      >
        <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16 }}>
          <div>
            <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: "#F2F3F6", letterSpacing: "-0.01em" }}>Forge new certificate</h2>
            <p style={{ margin: "6px 0 0", fontSize: 12, color: "#7C818E" }}>Strike a fresh certificate from a local root authority.</p>
          </div>
          <IconButton icon="x" label="Close" onClick={onClose} />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <div style={{ gridColumn: "1 / -1", display: "flex", flexDirection: "column", gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 500, color: "#B7BBC6" }}>Common name (CN)</label>
            <Input value={cn} onChange={setCn} mono />
          </div>
          <div style={{ gridColumn: "1 / -1", display: "flex", flexDirection: "column", gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 500, color: "#B7BBC6" }}>Subject alternative names (comma separated)</label>
            <Input value={sans} onChange={setSans} mono />
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 500, color: "#B7BBC6" }}>Issued by</label>
            <select
              value={authority}
              onChange={(e) => setAuthority(e.target.value)}
              style={{
                fontFamily: "var(--font-ui)", fontSize: 13,
                background: "#12141A", color: "#F2F3F6",
                border: "1px solid #2F333D", borderRadius: 10,
                padding: "9px 12px", outline: "none",
              }}
            >
              {authorities.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <label style={{ fontSize: 11, fontWeight: 500, color: "#B7BBC6" }}>Validity</label>
            <select
              value={days}
              onChange={(e) => setDays(Number(e.target.value))}
              style={{
                fontFamily: "var(--font-ui)", fontSize: 13,
                background: "#12141A", color: "#F2F3F6",
                border: "1px solid #2F333D", borderRadius: 10,
                padding: "9px 12px", outline: "none",
              }}
            >
              <option value={90}>90 days</option>
              <option value={365}>365 days</option>
              <option value={825}>825 days</option>
            </select>
          </div>
        </div>

        <label style={{ display: "flex", alignItems: "center", gap: 10, cursor: "pointer" }}>
          <span
            onClick={() => setInstallLocally(v => !v)}
            style={{
              width: 16, height: 16, borderRadius: 4,
              background: installLocally ? "#FF6A00" : "transparent",
              border: `1px solid ${installLocally ? "#FF6A00" : "#4A4F5C"}`,
              display: "inline-flex", alignItems: "center", justifyContent: "center",
            }}
          >
            {installLocally && (
              <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="#0B0C10" strokeWidth="3.5"><polyline points="4 12 10 18 20 6" /></svg>
            )}
          </span>
          <span style={{ fontSize: 13, color: "#F2F3F6" }}>Install in local trust store after forging</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, paddingTop: 4, borderTop: "1px solid #2F333D" }}>
          <div style={{ flex: 1 }} />
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button
            variant="primary"
            icon={forging ? "loader-2" : "hammer"}
            onClick={handleForge}
            disabled={forging}
            style={forging ? { boxShadow: "var(--glow-orange)" } : undefined}
          >
            {forging ? "Forging…" : "Forge Certificate"}
          </Button>
        </div>
      </div>
    </div>
  );
};

Object.assign(window, { ForgeDialog });
