"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "./auth";

export type ProjectSummary = {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
};

export type EnvironmentSummary = {
  id: string;
  projectId: string;
  name: string;
  key: string;
  sortOrder: number;
  isProduction: boolean;
  createdAt: string;
  updatedAt: string;
};

type ContextStatus = "idle" | "loading" | "ready" | "empty" | "error";

type ProjectContextValue = {
  status: ContextStatus;
  projects: ProjectSummary[];
  environments: EnvironmentSummary[];
  selectedProject: ProjectSummary | null;
  selectedEnvironment: EnvironmentSummary | null;
  canManageHierarchy: boolean;
  selectProject: (projectId: string) => void;
  selectEnvironment: (environmentId: string) => void;
  reloadProjects: (preferredProjectId?: string) => Promise<void>;
  reloadEnvironments: (preferredEnvironmentId?: string) => Promise<void>;
  contextHref: (path: string) => string;
};

const ProjectContext = createContext<ProjectContextValue | null>(null);

function querySelection() {
  if (typeof window === "undefined") {
    return { projectId: null, environmentId: null };
  }

  const params = new URLSearchParams(window.location.search);
  return {
    projectId: params.get("project"),
    environmentId: params.get("environment"),
  };
}

export function ProjectContextProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { status: authStatus, user, authenticatedFetch } = useAuth();
  const [status, setStatus] = useState<ContextStatus>("idle");
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [environments, setEnvironments] = useState<EnvironmentSummary[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null);
  const [selectedEnvironmentId, setSelectedEnvironmentId] = useState<string | null>(null);

  const replaceContextQuery = useCallback(
    (projectId: string | null, environmentId: string | null) => {
      const params = typeof window === "undefined"
        ? new URLSearchParams()
        : new URLSearchParams(window.location.search);

      if (projectId) {
        params.set("project", projectId);
      } else {
        params.delete("project");
      }

      if (environmentId) {
        params.set("environment", environmentId);
      } else {
        params.delete("environment");
      }

      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router],
  );

  const loadEnvironments = useCallback(
    async (projectId: string, preferredEnvironmentId?: string | null) => {
      const response = await authenticatedFetch(`/api/projects/${projectId}/environments/`, {
        cache: "no-store",
      });

      if (!response.ok) {
        setEnvironments([]);
        setSelectedEnvironmentId(null);
        setStatus(response.status === 401 ? "idle" : "error");
        return;
      }

      const nextEnvironments = ((await response.json()) as EnvironmentSummary[])
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
      setEnvironments(nextEnvironments);

      const queryEnvironment = querySelection().environmentId;
      const candidate = preferredEnvironmentId ?? queryEnvironment;
      const selected = nextEnvironments.find((item) => item.id === candidate) ?? nextEnvironments[0] ?? null;
      setSelectedEnvironmentId(selected?.id ?? null);
      setStatus("ready");
    },
    [authenticatedFetch],
  );

  const reloadProjects = useCallback(
    async (preferredProjectId?: string) => {
      if (authStatus !== "authenticated") {
        return;
      }

      setStatus("loading");
      const response = await authenticatedFetch("/api/projects/", { cache: "no-store" });
      if (!response.ok) {
        setProjects([]);
        setEnvironments([]);
        setSelectedProjectId(null);
        setSelectedEnvironmentId(null);
        setStatus(response.status === 401 ? "idle" : "error");
        return;
      }

      const nextProjects = (await response.json()) as ProjectSummary[];
      setProjects(nextProjects);

      if (nextProjects.length === 0) {
        setEnvironments([]);
        setSelectedProjectId(null);
        setSelectedEnvironmentId(null);
        setStatus("empty");
        return;
      }

      const queryProject = querySelection().projectId;
      const candidate = preferredProjectId ?? queryProject;
      const selected = nextProjects.find((item) => item.id === candidate) ?? nextProjects[0];
      setSelectedProjectId(selected.id);
      await loadEnvironments(selected.id);
    },
    [authStatus, authenticatedFetch, loadEnvironments],
  );

  const reloadEnvironments = useCallback(
    async (preferredEnvironmentId?: string) => {
      if (!selectedProjectId) {
        return;
      }

      setStatus("loading");
      await loadEnvironments(selectedProjectId, preferredEnvironmentId);
    },
    [loadEnvironments, selectedProjectId],
  );

  useEffect(() => {
    if (authStatus !== "authenticated") {
      return;
    }

    let cancelled = false;
    queueMicrotask(() => {
      if (!cancelled) {
        void reloadProjects();
      }
    });

    return () => {
      cancelled = true;
    };
  }, [authStatus, reloadProjects]);

  const selectProject = useCallback(
    (projectId: string) => {
      if (!projects.some((item) => item.id === projectId)) {
        return;
      }

      setSelectedProjectId(projectId);
      setEnvironments([]);
      setSelectedEnvironmentId(null);
      setStatus("loading");
      replaceContextQuery(projectId, null);
      void loadEnvironments(projectId);
    },
    [loadEnvironments, projects, replaceContextQuery],
  );

  const selectEnvironment = useCallback(
    (environmentId: string) => {
      if (!selectedProjectId || !environments.some((item) => item.id === environmentId)) {
        return;
      }

      setSelectedEnvironmentId(environmentId);
      replaceContextQuery(selectedProjectId, environmentId);
    },
    [environments, replaceContextQuery, selectedProjectId],
  );

  const contextHref = useCallback(
    (path: string) => {
      const query = querySelection();
      const projectId = selectedProjectId ?? query.projectId;
      const environmentId = selectedEnvironmentId ?? query.environmentId;
      const params = new URLSearchParams();
      if (projectId) {
        params.set("project", projectId);
      }
      if (environmentId) {
        params.set("environment", environmentId);
      }
      const search = params.toString();
      return search ? `${path}?${search}` : path;
    },
    [selectedEnvironmentId, selectedProjectId],
  );

  const selectedProject = projects.find((item) => item.id === selectedProjectId) ?? null;
  const selectedEnvironment = environments.find((item) => item.id === selectedEnvironmentId) ?? null;
  const canManageHierarchy = Boolean(
    user?.roles.includes("Owner") || user?.roles.includes("Admin"),
  );

  const value = useMemo<ProjectContextValue>(
    () => ({
      status,
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
    }),
    [
      canManageHierarchy,
      contextHref,
      environments,
      projects,
      reloadEnvironments,
      reloadProjects,
      selectEnvironment,
      selectProject,
      selectedEnvironment,
      selectedProject,
      status,
    ],
  );

  return <ProjectContext.Provider value={value}>{children}</ProjectContext.Provider>;
}

export function useProjectContext() {
  const context = useContext(ProjectContext);
  if (!context) {
    throw new Error("useProjectContext must be used inside ProjectContextProvider");
  }
  return context;
}
