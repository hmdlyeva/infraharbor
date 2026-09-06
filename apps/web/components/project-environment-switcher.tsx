"use client";

import Link from "next/link";
import { useProjectContext } from "../lib/project-context";

export function ProjectEnvironmentSwitcher() {
  const {
    status,
    projects,
    environments,
    selectedProject,
    selectedEnvironment,
    canManageHierarchy,
    selectProject,
    selectEnvironment,
    contextHref,
  } = useProjectContext();

  return (
    <div className="project-switcher" aria-label="Project and environment context">
      <label>
        <span>Project</span>
        <select
          aria-label="Project context"
          disabled={status === "loading" || projects.length === 0}
          value={selectedProject?.id ?? ""}
          onChange={(event) => selectProject(event.target.value)}
        >
          {projects.length === 0 ? <option value="">No projects</option> : null}
          {projects.map((project) => (
            <option key={project.id} value={project.id}>{project.name}</option>
          ))}
        </select>
      </label>
      <label>
        <span>Environment</span>
        <select
          aria-label="Environment context"
          disabled={status === "loading" || environments.length === 0}
          value={selectedEnvironment?.id ?? ""}
          onChange={(event) => selectEnvironment(event.target.value)}
        >
          {environments.length === 0 ? <option value="">No environments</option> : null}
          {environments.map((environment) => (
            <option key={environment.id} value={environment.id}>
              {environment.name}{environment.isProduction ? " · Production" : ""}
            </option>
          ))}
        </select>
      </label>
      {status === "error" ? <small>Context is temporarily unavailable.</small> : null}
      {status === "empty" ? <small>No active projects are configured.</small> : null}
      {canManageHierarchy ? (
        <Link className="project-settings-link" href={contextHref("/projects/settings")}>Project settings</Link>
      ) : null}
    </div>
  );
}
