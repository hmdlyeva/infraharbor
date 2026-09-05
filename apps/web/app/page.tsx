const services = [
  { name: "Web API", environment: "Production", state: "Healthy", detail: "28 ms" },
  { name: "Worker", environment: "Production", state: "Healthy", detail: "Online" },
  { name: "PostgreSQL", environment: "Production", state: "Healthy", detail: "12 ms" },
  { name: "Checkout monitor", environment: "Production", state: "Degraded", detail: "2 failures" },
];

const nav = ["Overview", "Servers", "Containers", "Monitors", "Incidents", "Deployments"];

export default function Home() {
  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark">IH</span><div><strong>InfraHarbor</strong><span>Control plane</span></div></div>
        <div className="project-switcher"><span>Project</span><strong>Demo Platform</strong><small>Production</small></div>
        <nav aria-label="Primary navigation">{nav.map((item, index) => (<a className={index === 0 ? "nav-item active" : "nav-item"} href="#" key={item}><span className="nav-dot" />{item}</a>))}</nav>
        <div className="sidebar-footer"><span>Open source · White-label</span><small>Foundation preview</small></div>
      </aside>
      <section className="workspace">
        <header className="topbar"><div><span className="eyebrow">OPERATIONS / OVERVIEW</span><h1>Infrastructure at a glance</h1></div><div className="user-chip" aria-label="Signed in user placeholder"><span>HA</span><div><strong>Operator</strong><small>Owner</small></div></div></header>
        <div className="notice"><strong>Foundation preview</strong><span>Live infrastructure connectivity is intentionally not enabled in this scaffold.</span></div>
        <div className="metric-grid"><article className="metric-card"><span>Registered servers</span><strong>4</strong><small>4 reachable</small></article><article className="metric-card"><span>Running containers</span><strong>18</strong><small>1 needs attention</small></article><article className="metric-card"><span>Open incidents</span><strong>1</strong><small>Production monitor</small></article><article className="metric-card"><span>Last deployment</span><strong className="metric-text">main</strong><small>Successful · 18 min ago</small></article></div>
        <div className="content-grid"><article className="panel services-panel"><div className="panel-heading"><div><span className="eyebrow">CURRENT STATE</span><h2>Operational health</h2></div><button type="button">View incidents</button></div><div className="service-list">{services.map((service) => (<div className="service-row" key={service.name}><span className={service.state === "Healthy" ? "status-dot healthy" : "status-dot degraded"} /><div className="service-name"><strong>{service.name}</strong><small>{service.environment}</small></div><span className={service.state === "Healthy" ? "status healthy-text" : "status degraded-text"}>{service.state}</span><span className="service-detail">{service.detail}</span></div>))}</div></article><article className="panel activity-panel"><div className="panel-heading"><div><span className="eyebrow">RECENT</span><h2>Activity</h2></div></div><ol className="activity-list"><li><span className="timeline-dot" /><div><strong>Deployment succeeded</strong><small>main · 18 min ago</small></div></li><li><span className="timeline-dot warning" /><div><strong>Monitor entered degraded state</strong><small>Checkout monitor · 24 min ago</small></div></li><li><span className="timeline-dot" /><div><strong>Server metrics collected</strong><small>api-prod-01 · 31 min ago</small></div></li></ol></article></div>
      </section>
    </main>
  );
}
