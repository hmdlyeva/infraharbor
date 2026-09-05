import { expect, test, type Page, type Route } from "@playwright/test";

const apiBase = "http://localhost:8080";
const browserOrigin = "http://127.0.0.1:3100";

const user = {
  id: "11111111-1111-1111-1111-111111111111",
  email: "owner@infraharbor.test",
  displayName: "InfraHarbor Owner",
  roles: ["Owner"],
};

function authPayload() {
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
    "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
    ...(contentType ? { "Content-Type": "application/json" } : {}),
  };
}

async function fulfillPreflight(route: Route) {
  await route.fulfill({ status: 204, headers: corsHeaders(), body: "" });
}

async function installAuthApiMock(
  page: Page,
  options: { refreshIsValid?: () => boolean } = {},
) {
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
          body: JSON.stringify(authPayload()),
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
        body: JSON.stringify(authPayload()),
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

  await page.getByLabel("Email").fill(user.email);
  await page.getByLabel("Password").fill("test-only-password");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();

  await page.locator('summary[aria-label="Open current user menu"]').click();
  const menu = page.locator(".user-menu-panel");
  await expect(menu.getByText(user.email)).toBeVisible();
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
