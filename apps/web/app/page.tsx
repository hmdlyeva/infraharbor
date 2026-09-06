"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ProjectEnvironmentSwitcher } from "../components/project-environment-switcher";
import { useAuth } from "../lib/auth";
import { useProjectContext } from "../lib/project-context";

const services = [
  { name: "Web API", state: "Healthy", detail: "28 ms" },
  { name: "Worker", state: "Healthy", detail: "Online" },
  { name: "PostgreSQL", state: "Healthy", detail: "12 ms" },
  { name: "Checkout monitor", state: "Degraded", detail: "2 failures" },
];

const baseNav = [
  { label: "Overview", href: "/" },
  { label: "Servers", href: "#" },
  { label: "Containers", href: "#" },
  { label: "Monitors", href: "#" },
  { label: "Incidents", href: "#" },
  { label: "Deployments", href: "#" },
];

function initials(displayName: string) {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("") || "IH";
}

export default function Home() {
  const router = useRouter();
  const { status, user, logout } = useAuth();
  const { selectedProject, selectedEnvironment, contextHref } = useProjectContext();

  useEffect(() => {
    if (status === "anonymous") {
      router.replace("/login");
    }
  }, [router, status]);

  if (status !== "authenticated" || !user) {
    return (
      <main className="session-gate" aria-live="polite">
        <div className="session-gate-card">
          <span className="brand-mark">IH</span>
          <div>
            <strong>Checking session</strong>
            <span>Confirming secure access to InfraHarbor…</span>
          </div>
        </div>
      </main>
    );
  }

  async function handleLogout() {
    await logout();
    router.replace("/login");
  }

  const primaryRole = user.roles[0] ?? "Authenticated";
  const canManageUsers = user.roles.includes("Owner") || user.roles.includes("Admin");
  const nav = canManageUsers ? [...baseNav, { label: "Users", href: "/users" }] : baseNav;
  const contextLabel = selectedProject && selectedEnvironment
    ? `${selectedProject.name} / ${selectedEnvironment.name}`
    : selectedProject?.name ?? "No project context";

  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">IH</span>
          <div><strong>InfraHarbor</strong><span>Control plane</span></div>
        </div>
        <ProjectEnvironmentSwitcher />
        <nav aria-label="Primary navigation">
          {nav.map((item) => {
            const href = item.href === "#" ? "#" : contextHref(item.href);
            return (
              <a className={item.label === "Overview" ? "nav-item active" : "nav-item"} href={href} key={item.label}>
                <span className="nav-dot" />{item.label}
              </a>
            );
          })}
        </nav>
        <div className="sidebar-footer"><span>Open source · White-label</span><small>Foundation preview</small></div>
      </aside>
      <section className="workspace">
        <header className="topbar">
          <div><span className="eyebrow">OPERATIONS / {contextLabel.toUpperCase()}</span><h1>Infrastructure at a glance</h1></div>
          <details className="user-menu">
            <summary className="user-chip" aria-label="Open current user menu">
              <span>{initials(user.displayName)}</span>
              <div><strong>{user.displayName}</strong><small>{primaryRole}</small></div>
            </summary>
            <div className="user-menu-panel">
              <strong>{user.displayName}</strong>
              <span>{user.email}</span>
              <small>{user.roles.length ? user.roles.join(" · ") : "Authenticated"}</small>
              <button type="button" onClick={handleLogout}>Sign out</button>
            </div>
          </details>
        </header>
        <div className="notice"><strong>Foundation preview</strong><span>Live infrastructure connectivity is intentionally not enabled in this scaffold.</span></div>
        <div className="metric-grid">
          <article className="metric-card"><span>Registered servers</span><strong>4</strong><small>4 reachable</small></article>
          <article className="metric-card"><span>Running containers</span><strong>18</strong><small>1 needs attention</small></article>
          <article className="metric-card"><span>Open incidents</span><strong>1</strong><small>Production monitor</small></article>
          <article className="metric-card"><span>Last deployment</span><strong className="metric-text">main</strong><small>Successful · 18 min ago</small></article>
        </div>
        <div className="content-grid">
          <article className="panel services-panel">
            <div className="panel-heading"><div><span className="eyebrow">CURRENT STATE</span><h2>Operational health</h2></div><button type="button">View incidents</button></div>
            <div className="service-list">
              {services.map((service) => (
                <div className="service-row" key={service.name}>
                  <span className={service.state === "Healthy" ? "status-dot healthy" : "status-dot degraded"} />
                  <div className="service-name"><strong>{service.name}</strong><small>{selectedEnvironment?.name ?? "No environment selected"}</small></div>
                  <span className={service.state === "Healthy" ? "status healthy-text" : "status degraded-text"}>{service.state}</span>
                  <span className="service-detail">{service.detail}</span>
                </div>
              ))}
            </div>
          </article>
          <article className="panel activity-panel">
            <div className="panel-heading"><div><span className="eyebrow">RECENT</span><h2>Activity</h2></div></div>
            <ol className="activity-list">
              <li><span className="timeline-dot" /><div><strong>Deployment succeeded</strong><small>main · 18 min ago</small></div></li>
              <li><span className="timeline-dot warning" /><div><strong>Monitor entered degraded state</strong><small>Checkout monitor · 24 min ago</small></div></li>
              <li><span className="timeline-dot" /><div><strong>Server metrics collected</strong><small>api-prod-01 · 31 min ago</small></div></li>
            </ol>
          </article>
        </div>
      </section>
    </main>
  );
}
