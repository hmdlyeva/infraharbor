import { expect, test, type Page, type Route } from "@playwright/test";

const apiBase = "http://localhost:8080";
const browserOrigin = "http://127.0.0.1:3100";

const ownerUser = {
  id: "11111111-1111-1111-1111-111111111111",
  email: "owner@infraharbor.test",
  displayName: "InfraHarbor Owner",
  roles: ["Owner"],
};

const viewerUser = {
  id: "22222222-2222-2222-2222-222222222222",
  email: "viewer@infraharbor.test",
  displayName: "InfraHarbor Viewer",
  roles: ["Viewer"],
};

type BrowserUser = typeof ownerUser;

type ManagedUser = {
  id: string;
  email: string;
  displayName: string;
  status: string;
  roles: string[];
  createdAt: string;
  updatedAt: string;
};

function authPayload(user: BrowserUser) {
  return {
    tokenType: "Bearer",
    accessToken: "browser-e2e-access-token",
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user,
  };
}

function corsHeaders(contentType = false) {
  return {
    "Access-Control-Allow-Origin": browserOrigin,
    "Access-Control-Allow-Credentials": "true",
    "Access-Control-Allow-Headers": "content-type,authorization",
    "Access-Control-Allow-Methods": "GET,POST,PATCH,OPTIONS",
    ...(contentType ? { "Content-Type": "application/json" } : {}),
  };
}

async function fulfillPreflight(route: Route) {
  await route.fulfill({ status: 204, headers: corsHeaders(), body: "" });
}

async function installAuthApiMock(
  page: Page,
  options: { refreshIsValid?: () => boolean; user?: BrowserUser } = {},
) {
  const currentUser = options.user ?? ownerUser;
  await page.route(`${apiBase}/api/auth/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (request.method() === "OPTIONS") {
      await fulfillPreflight(route);
      return;
    }

    if (url.pathname === "/api/auth/refresh") {
      if (options.refreshIsValid?.()) {
        await route.fulfill({
          status: 200,
          headers: corsHeaders(true),
          body: JSON.stringify(authPayload(currentUser)),
        });
      } else {
        await route.fulfill({
          status: 401,
          headers: corsHeaders(true),
          body: JSON.stringify({ code: "session_invalid" }),
        });
      }
      return;
    }

    if (url.pathname === "/api/auth/login") {
      await route.fulfill({
        status: 200,
        headers: corsHeaders(true),
        body: JSON.stringify(authPayload(currentUser)),
      });
      return;
    }

    if (url.pathname === "/api/auth/logout") {
      await route.fulfill({ status: 204, headers: corsHeaders(), body: "" });
      return;
    }

    await route.fulfill({ status: 404, headers: corsHeaders(), body: "" });
  });
}

async function installUserAdminApiMock(page: Page) {
  const now = new Date().toISOString();
  const users: ManagedUser[] = [
    {
      id: ownerUser.id,
      email: ownerUser.email,
      displayName: ownerUser.displayName,
      status: "Active",
      roles: ["Owner"],
      createdAt: now,
      updatedAt: now,
    },
  ];

  await page.route(`${apiBase}/api/users/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (request.method() === "OPTIONS") {
      await fulfillPreflight(route);
      return;
    }

    if (request.method() === "GET" && (url.pathname === "/api/users" || url.pathname === "/api/users/")) {
      await route.fulfill({ status: 200, headers: corsHeaders(true), body: JSON.stringify(users) });
      return;
    }

    if (request.method() === "POST" && (url.pathname === "/api/users" || url.pathname === "/api/users/")) {
      const body = request.postDataJSON() as {
        email: string;
        displayName: string;
        password: string;
        roles: string[];
      };
      const created: ManagedUser = {
        id: "33333333-3333-3333-3333-333333333333",
        email: body.email,
        displayName: body.displayName,
        status: "Active",
        roles: body.roles,
        createdAt: now,
        updatedAt: now,
      };
      users.push(created);
      await route.fulfill({ status: 201, headers: corsHeaders(true), body: JSON.stringify(created) });
      return;
    }

    const roleMatch = url.pathname.match(/^\/api\/users\/([^/]+)\/roles$/);
    if (request.method() === "POST" && roleMatch) {
      const body = request.postDataJSON() as { roles: string[] };
      const target = users.find((item) => item.id === roleMatch[1]);
      if (!target) {
        await route.fulfill({ status: 404, headers: corsHeaders(true), body: JSON.stringify({ code: "user_not_found" }) });
        return;
      }
      target.roles = body.roles;
      target.updatedAt = new Date().toISOString();
      await route.fulfill({ status: 204, headers: corsHeaders(), body: "" });
      return;
    }

    const disableMatch = url.pathname.match(/^\/api\/users\/([^/]+)\/disable$/);
    if (request.method() === "POST" && disableMatch) {
      const target = users.find((item) => item.id === disableMatch[1]);
      if (!target) {
        await route.fulfill({ status: 404, headers: corsHeaders(true), body: JSON.stringify({ code: "user_not_found" }) });
        return;
      }
      target.status = "Disabled";
      target.updatedAt = new Date().toISOString();
      await route.fulfill({ status: 204, headers: corsHeaders(), body: "" });
      return;
    }

    await route.fulfill({ status: 404, headers: corsHeaders(), body: "" });
  });
}

test("unauthenticated user cannot access the application shell", async ({ page }) => {
  await installAuthApiMock(page);

  await page.goto("/");

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Sign in to InfraHarbor" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toHaveCount(0);
});

test("login opens the protected shell without persisting tokens in browser storage", async ({ page }) => {
  await installAuthApiMock(page);

  await page.goto("/login");
  await expect(page.getByRole("button", { name: "Sign in" })).toBeEnabled();

  await page.getByLabel("Email").fill(ownerUser.email);
  await page.getByLabel("Password").fill("test-only-password");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();

  await page.locator('summary[aria-label="Open current user menu"]').click();
  const menu = page.locator(".user-menu-panel");
  await expect(menu.getByText(ownerUser.email)).toBeVisible();
  await expect(menu.getByText("Owner", { exact: true })).toBeVisible();

  const storage = await page.evaluate(() => ({
    local: Object.keys(window.localStorage),
    session: Object.keys(window.sessionStorage),
  }));
  expect(storage.local).toEqual([]);
  expect(storage.session).toEqual([]);

  await menu.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("an expired or revoked refresh session returns the user to login safely", async ({ page }) => {
  let refreshIsValid = true;
  await installAuthApiMock(page, { refreshIsValid: () => refreshIsValid });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();

  refreshIsValid = false;
  await page.reload();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading", { name: "Sign in to InfraHarbor" })).toBeVisible();
});

test("owner can create, re-role and disable a managed user from the browser UI", async ({ page }) => {
  await installAuthApiMock(page, { refreshIsValid: () => true, user: ownerUser });
  await installUserAdminApiMock(page);

  await page.goto("/users");
  await expect(page.getByRole("heading", { name: "Users & roles" })).toBeVisible();

  await page.getByLabel("Display name").fill("Managed Operator");
  await page.getByLabel("Email").fill("managed@infraharbor.test");
  await page.getByLabel("Temporary password").fill("test-only-password");
  await page.getByLabel("Role").selectOption("Operator");
  await page.getByRole("button", { name: "Create user" }).click();

  let row = page.locator(".admin-user-row", { hasText: "managed@infraharbor.test" });
  await expect(row).toBeVisible();

  const roleMutation = page.waitForResponse((response) =>
    response.url().endsWith("/api/users/33333333-3333-3333-3333-333333333333/roles") &&
    response.request().method() === "POST",
  );
  await row.getByLabel("Role for managed@infraharbor.test").selectOption("Admin");
  expect((await roleMutation).status()).toBe(204);

  row = page.locator(".admin-user-row", { hasText: "managed@infraharbor.test" });
  await expect(row.getByLabel("Role for managed@infraharbor.test")).toHaveValue("Admin");

  const disableMutation = page.waitForResponse((response) =>
    response.url().endsWith("/api/users/33333333-3333-3333-3333-333333333333/disable") &&
    response.request().method() === "POST",
  );
  await row.getByRole("button", { name: "Disable" }).click();
  expect((await disableMutation).status()).toBe(204);

  row = page.locator(".admin-user-row", { hasText: "managed@infraharbor.test" });
  await expect(row.getByText("Disabled", { exact: true })).toBeVisible();
});

test("viewer cannot open user administration", async ({ page }) => {
  await installAuthApiMock(page, { refreshIsValid: () => true, user: viewerUser });

  await page.goto("/users");

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Users & roles" })).toHaveCount(0);
});
