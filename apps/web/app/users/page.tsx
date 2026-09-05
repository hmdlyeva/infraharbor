"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../lib/auth";

type ManagedUser = {
  id: string;
  email: string;
  displayName: string;
  status: "Active" | "Disabled" | string;
  roles: string[];
  createdAt: string;
  updatedAt: string;
};

const manageableRoles = ["Admin", "Operator", "Viewer"];

export default function UsersPage() {
  const router = useRouter();
  const { status, user, authenticatedFetch } = useAuth();
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Viewer");

  const canManageUsers = Boolean(
    user?.roles.includes("Owner") || user?.roles.includes("Admin"),
  );

  useEffect(() => {
    if (status === "anonymous") {
      router.replace("/login");
    } else if (status === "authenticated" && !canManageUsers) {
      router.replace("/");
    }
  }, [canManageUsers, router, status]);

  const loadUsers = useCallback(async () => {
    const response = await authenticatedFetch("/api/users/", { cache: "no-store" });
    if (!response.ok) {
      setLoadingUsers(false);
      if (response.status !== 401) {
        setError("Unable to load users.");
      }
      return;
    }

    setUsers((await response.json()) as ManagedUser[]);
    setLoadingUsers(false);
  }, [authenticatedFetch]);

  useEffect(() => {
    if (status !== "authenticated" || !canManageUsers) {
      return;
    }

    let cancelled = false;
    void authenticatedFetch("/api/users/", { cache: "no-store" }).then(async (response) => {
      if (cancelled) {
        return;
      }

      if (!response.ok) {
        setLoadingUsers(false);
        if (response.status !== 401) {
          setError("Unable to load users.");
        }
        return;
      }

      const nextUsers = (await response.json()) as ManagedUser[];
      if (!cancelled) {
        setUsers(nextUsers);
        setLoadingUsers(false);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [authenticatedFetch, canManageUsers, status]);

  if (status !== "authenticated" || !user || !canManageUsers) {
    return (
      <main className="session-gate" aria-live="polite">
        <div className="session-gate-card">
          <span className="brand-mark">IH</span>
          <div>
            <strong>Checking authorization</strong>
            <span>Confirming user administration access…</span>
          </div>
        </div>
      </main>
    );
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    const response = await authenticatedFetch("/api/users/", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, displayName, password, roles: [role] }),
    });

    if (!response.ok) {
      const payload = (await response.json().catch(() => null)) as { errors?: string[] } | null;
      setError(payload?.errors?.[0] ?? "Unable to create user.");
      setSubmitting(false);
      return;
    }

    setEmail("");
    setDisplayName("");
    setPassword("");
    setRole("Viewer");
    setSubmitting(false);
    await loadUsers();
  }

  async function handleRoleChange(target: ManagedUser, nextRole: string) {
    setError(null);
    const response = await authenticatedFetch(`/api/users/${target.id}/roles`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ roles: [nextRole] }),
    });

    if (!response.ok) {
      setError("Unable to update the user role.");
      return;
    }

    await loadUsers();
  }

  async function handleDisable(target: ManagedUser) {
    setError(null);
    const response = await authenticatedFetch(`/api/users/${target.id}/disable`, {
      method: "POST",
    });

    if (!response.ok) {
      setError("Unable to disable the user.");
      return;
    }

    await loadUsers();
  }

  async function handleRefresh() {
    setLoadingUsers(true);
    setError(null);
    await loadUsers();
  }

  return (
    <main className="admin-page">
      <header className="admin-topbar">
        <div>
          <Link className="back-link" href="/">← Overview</Link>
          <span className="eyebrow">IDENTITY / USERS</span>
          <h1>Users &amp; roles</h1>
          <p>Manage installation users without exposing ownership transfer through the generic admin flow.</p>
        </div>
        <div className="admin-actor">
          <strong>{user.displayName}</strong>
          <span>{user.email}</span>
          <small>{user.roles.join(" · ")}</small>
        </div>
      </header>

      {error ? <div className="admin-error" role="alert">{error}</div> : null}

      <section className="admin-grid">
        <article className="panel admin-create-panel">
          <div className="panel-heading">
            <div><span className="eyebrow">NEW ACCOUNT</span><h2>Create user</h2></div>
          </div>
          <form className="admin-form" onSubmit={handleCreate}>
            <label>
              <span>Display name</span>
              <input required maxLength={120} value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </label>
            <label>
              <span>Email</span>
              <input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
            </label>
            <label>
              <span>Temporary password</span>
              <input required type="password" autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} />
            </label>
            <label>
              <span>Role</span>
              <select value={role} onChange={(event) => setRole(event.target.value)}>
                {manageableRoles.map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
            </label>
            <button className="primary-button" disabled={submitting} type="submit">
              {submitting ? "Creating…" : "Create user"}
            </button>
          </form>
          <p className="admin-help">Owner is intentionally not assignable here. Ownership transfer requires a separate explicit flow.</p>
        </article>

        <article className="panel admin-list-panel">
          <div className="panel-heading">
            <div><span className="eyebrow">INSTALLATION</span><h2>Current users</h2></div>
            <button type="button" onClick={() => void handleRefresh()}>Refresh</button>
          </div>

          {loadingUsers ? (
            <div className="admin-empty">Loading users…</div>
          ) : (
            <div className="admin-user-list">
              {users.map((managedUser) => {
                const isOwner = managedUser.roles.includes("Owner");
                const selectedRole = manageableRoles.find((item) => managedUser.roles.includes(item)) ?? "Viewer";
                return (
                  <div className="admin-user-row" key={managedUser.id}>
                    <div className="admin-user-identity">
                      <strong>{managedUser.displayName}</strong>
                      <span>{managedUser.email}</span>
                    </div>
                    <span className={managedUser.status === "Active" ? "user-status active" : "user-status disabled"}>
                      {managedUser.status}
                    </span>
                    {isOwner ? (
                      <span className="owner-role">Owner</span>
                    ) : (
                      <select
                        aria-label={`Role for ${managedUser.email}`}
                        disabled={managedUser.status !== "Active"}
                        value={selectedRole}
                        onChange={(event) => void handleRoleChange(managedUser, event.target.value)}
                      >
                        {manageableRoles.map((item) => <option key={item} value={item}>{item}</option>)}
                      </select>
                    )}
                    <button
                      className="secondary-danger-button"
                      disabled={isOwner || managedUser.status !== "Active" || managedUser.id === user.id}
                      type="button"
                      onClick={() => void handleDisable(managedUser)}
                    >
                      Disable
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </article>
      </section>
    </main>
  );
}
