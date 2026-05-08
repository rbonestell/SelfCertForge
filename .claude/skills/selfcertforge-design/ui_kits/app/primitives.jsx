// SelfCertForge — primitives
const { useState } = React;

const Icon = ({ name, size = 18, color, style, strokeWidth = 1.6 }) => {
  // Render a Lucide icon via the global lucide library; lucide.createIcons() runs after mount in App.
  return (
    <i
      data-lucide={name}
      style={{ width: size, height: size, color, strokeWidth, display: "inline-flex", ...style }}
    />
  );
};

const Button = ({ variant = "primary", icon, children, onClick, disabled, style }) => {
  const base = {
    fontFamily: "var(--font-ui)",
    fontSize: 13,
    fontWeight: 600,
    borderRadius: 12,
    padding: "9px 14px",
    cursor: disabled ? "not-allowed" : "pointer",
    border: "1px solid transparent",
    display: "inline-flex",
    alignItems: "center",
    gap: 8,
    lineHeight: 1,
    letterSpacing: "-0.005em",
    transition: "background 120ms ease, border-color 120ms ease",
  };
  const variants = {
    primary:   { background: "#FF6A00", color: "#FFFFFF" },
    secondary: { background: "transparent", color: "#F2F3F6", borderColor: "#2F333D" },
    ghost:     { background: "transparent", color: "#B7BBC6" },
    danger:    { background: "transparent", color: "#E5484D", borderColor: "rgba(229,72,77,0.3)" },
  };
  const dis = disabled ? { background: "#3a2a1f", color: "#7C818E", borderColor: "transparent" } : null;
  return (
    <button
      onClick={disabled ? undefined : onClick}
      style={{ ...base, ...variants[variant], ...dis, ...style }}
      onMouseEnter={(e) => {
        if (disabled) return;
        if (variant === "primary") e.currentTarget.style.background = "#FF7F1A";
        if (variant === "secondary") e.currentTarget.style.background = "#232631";
        if (variant === "danger") e.currentTarget.style.background = "rgba(229,72,77,0.12)";
        if (variant === "ghost") e.currentTarget.style.color = "#F2F3F6";
      }}
      onMouseLeave={(e) => {
        if (disabled) return;
        const v = variants[variant];
        e.currentTarget.style.background = v.background;
        e.currentTarget.style.color = v.color;
      }}
    >
      {icon && <Icon name={icon} size={15} />}
      {children}
    </button>
  );
};

const IconButton = ({ icon, label, onClick, active }) => (
  <button
    onClick={onClick}
    title={label}
    style={{
      background: active ? "#232631" : "transparent",
      color: active ? "#F2F3F6" : "#B7BBC6",
      border: "1px solid transparent",
      borderRadius: 8,
      width: 30, height: 30,
      display: "inline-flex", alignItems: "center", justifyContent: "center",
      cursor: "pointer",
    }}
    onMouseEnter={(e) => { e.currentTarget.style.background = "#232631"; e.currentTarget.style.color = "#F2F3F6"; }}
    onMouseLeave={(e) => { e.currentTarget.style.background = active ? "#232631" : "transparent"; e.currentTarget.style.color = active ? "#F2F3F6" : "#B7BBC6"; }}
  >
    <Icon name={icon} size={15} />
  </button>
);

const Pill = ({ kind = "trusted", children }) => {
  const map = {
    trusted:   { fg: "#38C172", glow: true },
    self:      { fg: "#FF6A00" },
    expired:   { fg: "#E5484D" },
    untrusted: { fg: "#7C818E" },
    warn:      { fg: "#FFB800" },
  };
  const { fg, glow } = map[kind];
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 6,
      padding: "3px 10px 3px 8px",
      borderRadius: 999,
      fontSize: 11,
      fontWeight: 600,
      lineHeight: 1,
      color: fg,
      background: `color-mix(in srgb, ${fg} 12%, transparent)`,
      border: `1px solid color-mix(in srgb, ${fg} 28%, transparent)`,
    }}>
      <span style={{
        width: 6, height: 6, borderRadius: 999, background: fg,
        boxShadow: glow ? `0 0 6px ${fg}` : "none",
      }} />
      {children}
    </span>
  );
};

const Field = ({ label, mono, value, copyable, children }) => (
  <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
    <span style={{ fontSize: 10, textTransform: "uppercase", letterSpacing: ".08em", color: "#7C818E", fontWeight: 500 }}>{label}</span>
    {children ?? (
      <span style={{
        fontFamily: mono ? "var(--font-mono)" : "var(--font-ui)",
        fontSize: mono ? 12 : 13,
        color: "#F2F3F6",
        wordBreak: mono ? "break-all" : "normal",
        display: "inline-flex", alignItems: "center", gap: 8,
      }}>
        {value}
        {copyable && (
          <span style={{ color: "#7C818E", cursor: "pointer", display: "inline-flex" }}>
            <Icon name="copy" size={12} />
          </span>
        )}
      </span>
    )}
  </div>
);

const Card = ({ children, style, onClick, selected }) => (
  <div
    onClick={onClick}
    style={{
      background: "#1B1D25",
      border: `1px solid ${selected ? "#FF6A00" : "#2F333D"}`,
      borderRadius: 16,
      padding: 18,
      boxShadow: selected ? "0 0 0 3px rgba(255,106,0,0.18)" : "none",
      cursor: onClick ? "pointer" : "default",
      transition: "border-color 120ms ease, background 120ms ease",
      ...style,
    }}
    onMouseEnter={(e) => { if (onClick && !selected) e.currentTarget.style.background = "#1F2129"; }}
    onMouseLeave={(e) => { if (onClick && !selected) e.currentTarget.style.background = "#1B1D25"; }}
  >
    {children}
  </div>
);

const Input = ({ value, onChange, placeholder, mono, icon, autoFocus }) => {
  const [focused, setFocused] = useState(false);
  return (
    <div style={{
      display: "flex", alignItems: "center", gap: 8,
      background: "#12141A",
      border: `1px solid ${focused ? "#FF6A00" : "#2F333D"}`,
      borderRadius: 10,
      padding: "8px 12px",
      boxShadow: focused ? "0 0 0 3px rgba(255,106,0,0.18)" : "none",
      transition: "border-color 120ms ease, box-shadow 120ms ease",
    }}>
      {icon && <span style={{ color: "#7C818E", display: "inline-flex" }}><Icon name={icon} size={14} /></span>}
      <input
        autoFocus={autoFocus}
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange?.(e.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        style={{
          background: "transparent", border: "none", outline: "none",
          color: "#F2F3F6",
          fontFamily: mono ? "var(--font-mono)" : "var(--font-ui)",
          fontSize: 13, flex: 1,
        }}
      />
    </div>
  );
};

Object.assign(window, { Icon, Button, IconButton, Pill, Field, Card, Input });
