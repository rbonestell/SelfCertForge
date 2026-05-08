// CertificateDetail.jsx — right pane showing details of a selected certificate
const Section = ({ title, children, columns = 2 }) => (
  <section style={{ display: "flex", flexDirection: "column", gap: 12 }}>
    <h2 style={{ margin: 0, fontSize: 11, textTransform: "uppercase", letterSpacing: ".08em", color: "#7C818E", fontWeight: 600 }}>{title}</h2>
    <div style={{ display: "grid", gridTemplateColumns: `repeat(${columns}, 1fr)`, gap: 14 }}>{children}</div>
  </section>
);

const CertificateDetail = ({ cert }) => {
  if (!cert) return (
    <div style={{ padding: 40, color: "#7C818E", fontSize: 13 }}>Select a certificate to view details.</div>
  );

  return (
    <div style={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 24, overflow: "auto", height: "100%" }}>
      {/* Header */}
      <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16 }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 6, minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <h1 style={{ margin: 0, fontSize: 22, fontWeight: 600, letterSpacing: "-0.01em", color: "#F2F3F6" }}>{cert.name}</h1>
            <Pill kind={cert.statusKind}>{cert.status}</Pill>
          </div>
          <span style={{ fontFamily: "var(--font-mono)", fontSize: 12, color: "#7C818E" }}>{cert.subject}</span>
        </div>
        <div style={{ display: "flex", gap: 8, flexShrink: 0 }}>
          <Button variant="secondary" icon="arrow-up-from-line">Export PEM</Button>
          <Button variant="secondary" icon="arrow-up-from-line">Export PFX</Button>
          <Button variant="secondary" icon="key-round">Trust Locally</Button>
        </div>
      </div>

      <Section title="Identity">
        <Field label="Common name (CN)" value={cert.cn} mono copyable />
        <Field label="Organization" value={cert.org} mono />
      </Section>

      <Section title="Subject alternative names" columns={1}>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {cert.sans.map(s => (
            <span key={s} style={{
              fontFamily: "var(--font-mono)", fontSize: 12, color: "#F2F3F6",
              background: "#12141A", border: "1px solid #2F333D",
              padding: "4px 10px", borderRadius: 6,
            }}>{s}</span>
          ))}
        </div>
      </Section>

      <Section title="Validity" columns={3}>
        <Field label="Issued" value={cert.issued} mono />
        <Field label="Expires" value={cert.expires} mono />
        <Field label="Algorithm" value={cert.algorithm} mono />
      </Section>

      <Section title="Issuer">
        <Field label="Authority" value={cert.issuer} mono />
        <Field label="Serial" value={cert.serial} mono copyable />
      </Section>

      <Section title="Thumbprints" columns={1}>
        <Field label="SHA-256" value={cert.sha256} mono copyable />
        <Field label="SHA-1" value={cert.sha1} mono copyable />
      </Section>

      <Section title="PEM" columns={1}>
        <pre style={{
          margin: 0,
          background: "#12141A",
          border: "1px solid #2F333D",
          borderRadius: 10,
          padding: 14,
          fontFamily: "var(--font-mono)",
          fontSize: 11.5,
          color: "#B7BBC6",
          overflow: "auto",
          lineHeight: 1.5,
        }}>{`-----BEGIN CERTIFICATE-----
MIIDazCCAlOgAwIBAgIUO5l7VqQy3Tm2vO3w8sYnB0kF3hQwDQYJKoZIhvcNAQEL
BQAwRTELMAkGA1UEBhMCVVMxEzARBgNVBAgMClNvbWUtU3RhdGUxITAfBgNVBAoM
GEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDAeFw0yNTA1MDMxOTQ1MTNaFw0yNjA1
MDMxOTQ1MTNaMEUxCzAJBgNVBAYTAlVTMRMwEQYDVQQIDApTb21lLVN0YXRlMSEw
HwYDVQQKDBhJbnRlcm5ldCBXaWRnaXRzIFB0eSBMdGQwggEiMA0GCSqGSIb3DQEB
-----END CERTIFICATE-----`}</pre>
      </Section>
    </div>
  );
};

Object.assign(window, { CertificateDetail, Section });
