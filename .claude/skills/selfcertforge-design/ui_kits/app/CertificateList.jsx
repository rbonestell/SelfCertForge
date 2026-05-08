// CertificateList.jsx — list of certificates with search and selection
const CertificateRow = ({ cert, selected, onClick }) => (
  <div
    onClick={onClick}
    style={{
      display: "grid",
      gridTemplateColumns: "1.4fr 1fr 110px 90px",
      gap: 14,
      alignItems: "center",
      padding: "12px 16px",
      borderRadius: 10,
      cursor: "pointer",
      background: selected ? "#232631" : "transparent",
      border: `1px solid ${selected ? "#FF6A00" : "transparent"}`,
      boxShadow: selected ? "0 0 0 3px rgba(255,106,0,0.12)" : "none",
      transition: "background 120ms ease",
    }}
    onMouseEnter={(e) => { if (!selected) e.currentTarget.style.background = "#1B1D25"; }}
    onMouseLeave={(e) => { if (!selected) e.currentTarget.style.background = "transparent"; }}
  >
    <div style={{ display: "flex", flexDirection: "column", gap: 3, minWidth: 0 }}>
      <span style={{ fontSize: 13, fontWeight: 600, color: "#F2F3F6", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{cert.name}</span>
      <span style={{ fontFamily: "var(--font-mono)", fontSize: 11, color: "#7C818E", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{cert.subject}</span>
    </div>
    <div style={{ fontFamily: "var(--font-mono)", fontSize: 11, color: "#B7BBC6", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{cert.issuer}</div>
    <div><Pill kind={cert.statusKind}>{cert.status}</Pill></div>
    <div style={{ fontSize: 11, color: cert.expiresWarning ? "#FFB800" : "#7C818E", fontFamily: "var(--font-mono)", textAlign: "right" }}>{cert.expires}</div>
  </div>
);

const CertificateList = ({ certificates, selectedId, onSelect }) => {
  const [query, setQuery] = React.useState("");
  const filtered = certificates.filter(c =>
    c.name.toLowerCase().includes(query.toLowerCase()) ||
    c.subject.toLowerCase().includes(query.toLowerCase())
  );
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", borderRight: "1px solid #2F333D" }}>
      <div style={{ padding: "16px 20px 12px", display: "flex", flexDirection: "column", gap: 12 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <h1 style={{ margin: 0, fontSize: 22, fontWeight: 600, letterSpacing: "-0.01em", color: "#F2F3F6" }}>Certificates</h1>
          <span style={{ fontSize: 11, color: "#7C818E", fontFamily: "var(--font-mono)" }}>{filtered.length} / {certificates.length}</span>
        </div>
        <Input value={query} onChange={setQuery} placeholder="Search by name, CN, or SAN" icon="search" />
      </div>
      <div style={{
        display: "grid",
        gridTemplateColumns: "1.4fr 1fr 110px 90px",
        gap: 14,
        padding: "8px 16px",
        fontSize: 10, textTransform: "uppercase", letterSpacing: ".08em", color: "#7C818E", fontWeight: 500,
        borderBottom: "1px solid #2F333D",
      }}>
        <span>Name · subject</span>
        <span>Issuer</span>
        <span>Status</span>
        <span style={{ textAlign: "right" }}>Expires</span>
      </div>
      <div style={{ flex: 1, overflow: "auto", padding: "6px 8px" }}>
        {filtered.map(c => (
          <CertificateRow key={c.id} cert={c} selected={c.id === selectedId} onClick={() => onSelect(c.id)} />
        ))}
      </div>
    </div>
  );
};

Object.assign(window, { CertificateList, CertificateRow });
