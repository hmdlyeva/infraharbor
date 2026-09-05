FROM node:24-alpine AS build
WORKDIR /workspace
RUN corepack enable && corepack prepare pnpm@11.25.0 --activate
COPY package.json pnpm-workspace.yaml ./
COPY apps/web/package.json apps/web/package.json
RUN pnpm install --no-frozen-lockfile
COPY apps/web apps/web
RUN pnpm --filter @infraharbor/web build
EXPOSE 3000
ENV NODE_ENV=production
CMD ["pnpm", "--filter", "@infraharbor/web", "start"]
