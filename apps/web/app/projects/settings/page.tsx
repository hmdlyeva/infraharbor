"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../../lib/auth";
import {
  useProjectContext,
  type EnvironmentSummary,
  type ProjectSummary,
} from "../../../lib/project-context";

function mutationError(payload: unknown, fallback: string) {
  if (payload && typeof payload === "object" && "errors" in payload) {
    const errors = (payload as { errors?: unknown }).errors;
    if (Array.isArray(errors) && typeof errors[0] === "string") {
      return errors[0];
    }
  }
  return fallback;
}

export default function ProjectSettingsPage() {
  const router = useRouter();
  const { status: authStatus, user, authenticatedFetch } = useAuth();
  const {
    status: contextStatus,
    projects,
    environments,
    selectedProject,
    selectedEnvironment,
    canManageHierarchy,
    selectProject,
    selectEnvironment,
    reloadProjects,
    reloadEnvironments,
    contextHref,
  } = useProjectContext();

  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [newProjectName, setNewProjectName] = useState("");
  const [newProjectSlug, setNewProjectSlug] = useState("");
  const [newProjectDescription, setNewProjectDescription] = useState("");
  const [projectName, setProjectName] = useState("");
  const [projectSlug, setProjectSlug] = useState("");
  const [projectDescription, setProjectDescription] = useState("");
  const [newEnvironmentName, setNewEnvironmentName] = useState("");
  const [newEnvironmentKey, setNewEnvironmentKey] = useState("");
  const [newEnvironmentSortOrder, setNewEnvironmentSortOrder] = useState("40");
  const [newEnvironmentProduction, setNewEnvironmentProduction] = useState(false);
  const [environmentName, setEnvironmentName] = useState("");
  const [environmentKey, setEnvironmentKey] = useState("");
  const [environmentSortOrder, setEnvironmentSortOrder] = useState("");
  const [environmentProduction, setEnvironmentProduction] = useState(false);

  useEffect(() => {
    if (authStatus === "anonymous") {
      router.replace("/login");
    } else if (authStatus === "authenticated" && !canManageHierarchy) {
      router.replace(contextHref("/"));
    }
  }, [authStatus, canManageHierarchy, contextHref, router]);

  useEffect(() => {
    setProjectName(selectedProject?.name ?? "");
    setProjectSlug(selectedProject?.slug ?? "");
    setProjectDescription(selectedProject?.description ?? "");
  }, [selectedProject]);

  useEffect(() => {
    setEnvironmentName(selectedEnvironment?.name ?? "");
    setEnvironmentKey(selectedEnvironment?.key ?? "");
    setEnvironmentSortOrder(selectedEnvironment ? String(selectedEnvironment.sortOrder) : "");
    setEnvironmentProduction(selectedEnvironment?.isProduction ?? false);
  }, [selectedEnvironment]);

  if (authStatus !== "authenticated" || !user || !canManageHierarchy) {
    return (
      <main className="session-gate" aria-live="polite">
        <div className="session-gate-card">
          <span className="brand-mark">IH</span>
          <div>
            <strong>Checking authorization</strong>
            <span>Confirming project settings access…</span>
          </div>
        </div>
      </main>
    );
  }

  async function handleCreateProject(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    const response = await authenticatedFetch("/api/projects/", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: newProjectName,
        slug: newProjectSlug,
        description: newProjectDescription || null,
      }),
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      setError(mutationError(payload, "Unable to create project."));
      setBusy(false);
      return;
    }

    const created = (await response.json()) as ProjectSummary;
    setNewProjectName("");
    setNewProjectSlug("");
    setNewProjectDescription("");
    await reloadProjects(created.id);
    router.replace(`/projects/settings?project=${encodeURIComponent(created.id)}`, { scroll: false });
    setBusy(false);
  }

  async function handleUpdateProject(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedProject) {
      return;
    }

    setBusy(true);
    setError(null);
    const response = await authenticatedFetch(`/api/projects/${selectedProject.id}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: projectName,
        slug: projectSlug,
        description: projectDescription || null,
      }),
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      setError(mutationError(payload, "Unable to update project."));
      setBusy(false);
      return;
    }

    await reloadProjects(selectedProject.id);
    setBusy(false);
  }

  async function handleArchiveProject() {
    if (!selectedProject || !window.confirm(`Archive ${selectedProject.name}?`)) {
      return;
    }

    setBusy(true);
    setError(null);
    const response = await authenticatedFetch(`/api/projects/${selectedProject.id}/archive`, {
      method: "POST",
    });

    if (!response.ok) {
      setError("Unable to archive project.");
      setBusy(false);
      return;
    }

    await reloadProjects();
    router.replace("/projects/settings", { scroll: false });
    setBusy(false);
  }

  async function handleCreateEnvironment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedProject) {
      return;
    }

    setBusy(true);
    setError(null);
    const response = await authenticatedFetch(`/api/projects/${selectedProject.id}/environments/`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: newEnvironmentName,
        key: newEnvironmentKey,
        sortOrder: Number(newEnvironmentSortOrder),
        isProduction: newEnvironmentProduction,
      }),
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      setError(mutationError(payload, "Unable to create environment."));
      setBusy(false);
      return;
    }

    const created = (await response.json()) as EnvironmentSummary;
    setNewEnvironmentName("");
    setNewEnvironmentKey("");
    setNewEnvironmentSortOrder("40");
    setNewEnvironmentProduction(false);
    await reloadEnvironments(created.id);
    router.replace(
      `/projects/settings?project=${encodeURIComponent(selectedProject.id)}&environment=${encodeURIComponent(created.id)}`,
      { scroll: false },
    );
    setBusy(false);
  }

  async function handleUpdateEnvironment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedProject || !selectedEnvironment) {
      return;
    }

    setBusy(true);
    setError(null);
    const response = await authenticatedFetch(`/api/environments/${selectedEnvironment.id}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: environmentName,
        key: environmentKey,
        sortOrder: Number(environmentSortOrder),
        isProduction: environmentProduction,
      }),
    });

    if (!response.ok) {
      const payload = await response.json().catch(() => null);
      setError(mutationError(payload, "Unable to update environment."));
      setBusy(false);
      return;
    }

    await reloadEnvironments(selectedEnvironment.id);
    setBusy(false);
  }

  return (
    <main className="admin-page">
      <header className="admin-topbar">
        <div>
          <Link className="back-link" href={contextHref("/")}>← Overview</Link>
          <span className="eyebrow">PRODUCT CORE / CONTEXT</span>
          <h1>Projects &amp; environments</h1>
          <p>Choose the active operating context and manage hierarchy metadata through the existing authorized backend lifecycle.</p>
        </div>
        <div className="admin-actor">
          <strong>{user.displayName}</strong>
          <span>{user.email}</span>
          <small>{user.roles.join(" · ")}</small>
        </div>
      </header>

      {error ? <div className="admin-error" role="alert">{error}</div> : null}

      <section className="context-summary panel">
        <div>
          <span className="eyebrow">ACTIVE CONTEXT</span>
          <strong>{selectedProject?.name ?? "No project"}</strong>
          <small>{selectedEnvironment?.name ?? "No environment"}</small>
        </div>
        <label>
          <span>Project</span>
          <select
            aria-label="Settings project"
            disabled={contextStatus === "loading" || projects.length === 0}
            value={selectedProject?.id ?? ""}
            onChange={(event) => selectProject(event.target.value)}
          >
            {projects.length === 0 ? <option value="">No projects</option> : null}
            {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
          </select>
        </label>
        <label>
          <span>Environment</span>
          <select
            aria-label="Settings environment"
            disabled={contextStatus === "loading" || environments.length === 0}
            value={selectedEnvironment?.id ?? ""}
            onChange={(event) => selectEnvironment(event.target.value)}
          >
            {environments.length === 0 ? <option value="">No environments</option> : null}
            {environments.map((environment) => <option key={environment.id} value={environment.id}>{environment.name}</option>)}
          </select>
        </label>
      </section>

      <section className="settings-grid">
        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow">NEW PROJECT</span><h2>Create project</h2></div></div>
          <form className="admin-form" onSubmit={handleCreateProject}>
            <label><span>Name</span><input required maxLength={120} value={newProjectName} onChange={(event) => setNewProjectName(event.target.value)} /></label>
            <label><span>Slug</span><input required maxLength={80} value={newProjectSlug} onChange={(event) => setNewProjectSlug(event.target.value)} /></label>
            <label><span>Description</span><textarea maxLength={2000} value={newProjectDescription} onChange={(event) => setNewProjectDescription(event.target.value)} /></label>
            <button className="primary-button" disabled={busy} type="submit">Create project</button>
          </form>
        </article>

        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow">CURRENT PROJECT</span><h2>Project metadata</h2></div></div>
          {selectedProject ? (
            <form className="admin-form" onSubmit={handleUpdateProject}>
              <label><span>Name</span><input required maxLength={120} value={projectName} onChange={(event) => setProjectName(event.target.value)} /></label>
              <label><span>Slug</span><input required maxLength={80} value={projectSlug} onChange={(event) => setProjectSlug(event.target.value)} /></label>
              <label><span>Description</span><textarea maxLength={2000} value={projectDescription} onChange={(event) => setProjectDescription(event.target.value)} /></label>
              <div className="settings-actions">
                <button className="primary-button" disabled={busy} type="submit">Save project</button>
                <button className="secondary-danger-button" disabled={busy} type="button" onClick={() => void handleArchiveProject()}>Archive project</button>
              </div>
            </form>
          ) : <div className="admin-empty">Create a project to begin.</div>}
        </article>

        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow">NEW ENVIRONMENT</span><h2>Add environment</h2></div></div>
          {selectedProject ? (
            <form className="admin-form" onSubmit={handleCreateEnvironment}>
              <label><span>Name</span><input required maxLength={120} value={newEnvironmentName} onChange={(event) => setNewEnvironmentName(event.target.value)} /></label>
              <label><span>Key</span><input required maxLength={64} value={newEnvironmentKey} onChange={(event) => setNewEnvironmentKey(event.target.value)} /></label>
              <label><span>Sort order</span><input required min={0} type="number" value={newEnvironmentSortOrder} onChange={(event) => setNewEnvironmentSortOrder(event.target.value)} /></label>
              <label className="checkbox-field"><input type="checkbox" checked={newEnvironmentProduction} onChange={(event) => setNewEnvironmentProduction(event.target.checked)} /><span>Production environment</span></label>
              <button className="primary-button" disabled={busy} type="submit">Add environment</button>
            </form>
          ) : <div className="admin-empty">Select a project first.</div>}
        </article>

        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow">CURRENT ENVIRONMENT</span><h2>Environment metadata</h2></div></div>
          {selectedEnvironment ? (
            <form className="admin-form" onSubmit={handleUpdateEnvironment}>
              <label><span>Name</span><input required maxLength={120} value={environmentName} onChange={(event) => setEnvironmentName(event.target.value)} /></label>
              <label><span>Key</span><input required maxLength={64} value={environmentKey} onChange={(event) => setEnvironmentKey(event.target.value)} /></label>
              <label><span>Sort order</span><input required min={0} type="number" value={environmentSortOrder} onChange={(event) => setEnvironmentSortOrder(event.target.value)} /></label>
              <label className="checkbox-field"><input type="checkbox" checked={environmentProduction} onChange={(event) => setEnvironmentProduction(event.target.checked)} /><span>Production environment</span></label>
              <button className="primary-button" disabled={busy} type="submit">Save environment</button>
            </form>
          ) : <div className="admin-empty">No environment is selected.</div>}
        </article>
      </section>
    </main>
  );
}
