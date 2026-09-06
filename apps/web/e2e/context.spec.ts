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

const alphaProjectId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const betaProjectId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
const createdProjectId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
const alphaDevelopmentId = "a1111111-1111-1111-1111-111111111111";
const alphaProductionId = "a2222222-2222-2222-2222-222222222222";
const betaStagingId = "b1111111-1111-1111-1111-111111111111";
const betaProductionId = "b2222222-2222-2222-2222-222222222222";

type BrowserUser = typeof ownerUser;

type Project = {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  isArchived: boolean;
  createdAt: string;
  updatedAt: string;
};

type Environment = {
  id: string;
  projectId: string;
  name: string;
  key: string;
  sortOrder: number;
  isProduction: boolean;
  createdAt: string;
  updatedAt: string;
};

function authPayload(user: BrowserUser) {
  return {
    tokenType: "Bearer",
    accessToken: "context-e2e-access-token",
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

async function installAuthApiMock(page: Page, user: BrowserUser) {
  await page.route(`${apiBase}/api/auth/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (request.method() === "OPTIONS") {
      await fulfillPreflight(route);
      return;
    }

    if (url.pathname === "/api/auth/refresh" || url.pathname === "/api/auth/login") {
      await route.fulfill({
        status: 200,
        headers: corsHeaders(true),
        body: JSON.stringify(authPayload(user)),
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

async function installProjectApiMock(page: Page) {
  const now = new Date().toISOString();
  const projects: Project[] = [
    {
      id: alphaProjectId,
      name: "Platform Alpha",
      slug: "platform-alpha",
      description: "Primary platform",
      isArchived: false,
      createdAt: now,
      updatedAt: now,
    },
    {
      id: betaProjectId,
      name: "Platform Beta",
      slug: "platform-beta",
      description: "Secondary platform",
      isArchived: false,
      createdAt: now,
      updatedAt: now,
    },
  ];

  const environments = new Map<string, Environment[]>([
    [alphaProjectId, [
      {
        id: alphaDevelopmentId,
        projectId: alphaProjectId,
        name: "Development",
        key: "development",
        sortOrder: 10,
        isProduction: false,
        createdAt: now,
        updatedAt: now,
      },
      {
        id: alphaProductionId,
        projectId: alphaProjectId,
        name: "Production",
        key: "production",
        sortOrder: 30,
        isProduction: true,
        createdAt: now,
        updatedAt: now,
      },
    ]],
    [betaProjectId, [
      {
        id: betaStagingId,
        projectId: betaProjectId,
        name: "Staging",
        key: "staging",
        sortOrder: 20,
        isProduction: false,
        createdAt: now,
        updatedAt: now,
      },
      {
        id: betaProductionId,
        projectId: betaProjectId,
        name: "Production",
        key: "production",
        sortOrder: 30,
        isProduction: true,
        createdAt: now,
        updatedAt: now,
      },
    ]],
  ]);

  await page.route(`${apiBase}/api/projects/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (request.method() === "OPTIONS") {
      await fulfillPreflight(route);
      return;
    }

    if (request.method() === "GET" && (url.pathname === "/api/projects" || url.pathname === "/api/projects/")) {
      await route.fulfill({
        status: 200,
        headers: corsHeaders(true),
        body: JSON.stringify(projects.filter((project) => !project.isArchived)),
      });
      return;
    }

    if (request.method() === "POST" && (url.pathname === "/api/projects" || url.pathname === "/api/projects/")) {
      const body = request.postDataJSON() as { name: string; slug: string; description?: string | null };
      const created: Project = {
        id: createdProjectId,
        name: body.name,
        slug: body.slug,
        description: body.description ?? null,
        isArchived: false,
        createdAt: now,
        updatedAt: now,
      };
      projects.push(created);
      environments.set(created.id, [
        {
          id: "c1111111-1111-1111-1111-111111111111",
          projectId: created.id,
          name: "Development",
          key: "development",
          sortOrder: 10,
          isProduction: false,
          createdAt: now,
          updatedAt: now,
        },
        {
          id: "c2222222-2222-2222-2222-222222222222",
          projectId: created.id,
          name: "Staging",
          key: "staging",
          sortOrder: 20,
          isProduction: false,
          createdAt: now,
          updatedAt: now,
        },
        {
          id: "c3333333-3333-3333-3333-333333333333",
          projectId: created.id,
          name: "Production",
          key: "production",
          sortOrder: 30,
          isProduction: true,
          createdAt: now,
          updatedAt: now,
        },
      ]);
      await route.fulfill({ status: 201, headers: corsHeaders(true), body: JSON.stringify(created) });
      return;
    }

    const environmentsMatch = url.pathname.match(/^\/api\/projects\/([^/]+)\/environments\/?$/);
    if (request.method() === "GET" && environmentsMatch) {
      await route.fulfill({
        status: 200,
        headers: corsHeaders(true),
        body: JSON.stringify(environments.get(environmentsMatch[1]) ?? []),
      });
      return;
    }

    if (request.method() === "POST" && environmentsMatch) {
      const body = request.postDataJSON() as {
        name: string;
        key: string;
        sortOrder: number;
        isProduction: boolean;
      };
      const created: Environment = {
        id: "dddddddd-dddd-dddd-dddd-dddddddddddd",
        projectId: environmentsMatch[1],
        name: body.name,
        key: body.key,
        sortOrder: body.sortOrder,
        isProduction: body.isProduction,
        createdAt: now,
        updatedAt: now,
      };
      const projectEnvironments = environments.get(environmentsMatch[1]) ?? [];
      projectEnvironments.push(created);
      environments.set(environmentsMatch[1], projectEnvironments);
      await route.fulfill({ status: 201, headers: corsHeaders(true), body: JSON.stringify(created) });
      return;
    }

    const archiveMatch = url.pathname.match(/^\/api\/projects\/([^/]+)\/archive$/);
    if (request.method() === "POST" && archiveMatch) {
      const target = projects.find((project) => project.id === archiveMatch[1]);
      if (target) {
        target.isArchived = true;
      }
      await route.fulfill({ status: target ? 200 : 404, headers: corsHeaders(true), body: target ? JSON.stringify(target) : "{}" });
      return;
    }

    const updateMatch = url.pathname.match(/^\/api\/projects\/([^/]+)$/);
    if (request.method() === "PATCH" && updateMatch) {
      const target = projects.find((project) => project.id === updateMatch[1]);
      if (!target) {
        await route.fulfill({ status: 404, headers: corsHeaders(true), body: "{}" });
        return;
      }
      const body = request.postDataJSON() as Partial<Project>;
      Object.assign(target, body, { updatedAt: new Date().toISOString() });
      await route.fulfill({ status: 200, headers: corsHeaders(true), body: JSON.stringify(target) });
      return;
    }

    await route.fulfill({ status: 404, headers: corsHeaders(), body: "" });
  });

  await page.route(`${apiBase}/api/environments/**`, async (route) => {
    const request = route.request();
    const url = new URL(request.url());

    if (request.method() === "OPTIONS") {
      await fulfillPreflight(route);
      return;
    }

    const match = url.pathname.match(/^\/api\/environments\/([^/]+)$/);
    if (request.method() === "PATCH" && match) {
      const target = Array.from(environments.values()).flat().find((environment) => environment.id === match[1]);
      if (!target) {
        await route.fulfill({ status: 404, headers: corsHeaders(true), body: "{}" });
        return;
      }
      const body = request.postDataJSON() as Partial<Environment>;
      Object.assign(target, body, { updatedAt: new Date().toISOString() });
      await route.fulfill({ status: 200, headers: corsHeaders(true), body: JSON.stringify(target) });
      return;
    }

    await route.fulfill({ status: 404, headers: corsHeaders(), body: "" });
  });
}

test("project and environment selection is preserved across normal navigation", async ({ page }) => {
  await installAuthApiMock(page, ownerUser);
  await installProjectApiMock(page);

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();
  await expect(page.getByLabel("Project context")).toHaveValue(alphaProjectId);
  await expect(page.getByLabel("Environment context")).toHaveValue(alphaDevelopmentId);

  await page.getByLabel("Project context").selectOption(betaProjectId);
  await expect(page).toHaveURL(new RegExp(`project=${betaProjectId}`));
  await expect(page.getByLabel("Environment context")).toHaveValue(betaStagingId);

  await page.getByLabel("Environment context").selectOption(betaProductionId);
  await expect(page).toHaveURL(new RegExp(`project=${betaProjectId}.*environment=${betaProductionId}`));

  await page.getByRole("link", { name: "Project settings" }).click();
  await expect(page.getByRole("heading", { name: "Projects & environments" })).toBeVisible();
  await expect(page.getByLabel("Settings project")).toHaveValue(betaProjectId);
  await expect(page.getByLabel("Settings environment")).toHaveValue(betaProductionId);

  await page.getByRole("link", { name: "Overview" }).click();
  await expect(page.getByLabel("Project context")).toHaveValue(betaProjectId);
  await expect(page.getByLabel("Environment context")).toHaveValue(betaProductionId);
  await expect(page).toHaveURL(new RegExp(`project=${betaProjectId}.*environment=${betaProductionId}`));
});

test("owner can create a project from settings and it becomes the active context", async ({ page }) => {
  await installAuthApiMock(page, ownerUser);
  await installProjectApiMock(page);

  await page.goto(`/projects/settings?project=${alphaProjectId}&environment=${alphaProductionId}`);
  await expect(page.getByRole("heading", { name: "Projects & environments" })).toBeVisible();

  const createProjectPanel = page.locator("article", { hasText: "Create project" });
  await createProjectPanel.getByLabel("Name").fill("Customer Portal");
  await createProjectPanel.getByLabel("Slug").fill("customer-portal");
  await createProjectPanel.getByLabel("Description").fill("Customer-facing workloads");
  await createProjectPanel.getByRole("button", { name: "Create project" }).click();

  await expect(page.getByLabel("Settings project")).toHaveValue(createdProjectId);
  await expect(page.getByLabel("Settings environment")).toHaveValue("c1111111-1111-1111-1111-111111111111");
  await expect(page).toHaveURL(new RegExp(`project=${createdProjectId}`));
});

test("viewer can switch context but cannot open hierarchy settings", async ({ page }) => {
  await installAuthApiMock(page, viewerUser);
  await installProjectApiMock(page);

  await page.goto(`/\?project=${alphaProjectId}&environment=${alphaProductionId}`);
  await expect(page.getByLabel("Project context")).toHaveValue(alphaProjectId);
  await expect(page.getByLabel("Environment context")).toHaveValue(alphaProductionId);
  await expect(page.getByRole("link", { name: "Project settings" })).toHaveCount(0);

  await page.goto(`/projects/settings?project=${alphaProjectId}&environment=${alphaProductionId}`);
  await expect(page).toHaveURL(new RegExp(`^${browserOrigin}/\\?project=${alphaProjectId}&environment=${alphaProductionId}$`));
  await expect(page.getByRole("heading", { name: "Infrastructure at a glance" })).toBeVisible();
});
