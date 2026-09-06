import type { Metadata } from "next";
import { AuthProvider } from "../lib/auth";
import { ProjectContextProvider } from "../lib/project-context";
import "./globals.css";
import "./auth.css";
import "./admin.css";
import "./context.css";

export const metadata: Metadata = {
  title: "InfraHarbor",
  description: "Open-source infrastructure operations dashboard",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <AuthProvider>
          <ProjectContextProvider>{children}</ProjectContextProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
