"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../lib/auth";

export default function LoginPage() {
  const router = useRouter();
  const { status, login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status === "authenticated") {
      router.replace("/");
    }
  }, [router, status]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await login(email, password);
      router.replace("/");
    } catch {
      setError("Unable to sign in with those credentials.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-card" aria-labelledby="login-title">
        <div className="auth-brand">
          <span className="brand-mark">IH</span>
          <div>
            <strong>InfraHarbor</strong>
            <span>Infrastructure control plane</span>
          </div>
        </div>

        <div className="auth-heading">
          <span className="eyebrow">SECURE ACCESS</span>
          <h1 id="login-title">Sign in to InfraHarbor</h1>
          <p>Use your installation account to access infrastructure operations.</p>
        </div>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            <span>Email</span>
            <input
              autoComplete="email"
              inputMode="email"
              name="email"
              onChange={(event) => setEmail(event.target.value)}
              required
              type="email"
              value={email}
            />
          </label>

          <label>
            <span>Password</span>
            <input
              autoComplete="current-password"
              name="password"
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>

          {error ? (
            <div className="auth-error" role="alert">
              {error}
            </div>
          ) : null}

          <button className="primary-button" disabled={submitting || status === "loading"} type="submit">
            {submitting ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <p className="auth-security-note">
          Session refresh credentials are protected by an HttpOnly cookie and are not stored in browser storage.
        </p>
      </section>
    </main>
  );
}
