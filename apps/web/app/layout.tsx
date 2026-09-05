import type { Metadata } from "next";
import { AuthProvider } from "../lib/auth";
import "./globals.css";
import "./auth.css";

export const metadata: Metadata = {
  title: "InfraHarbor",
  description: "Open-source infrastructure operations dashboard",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body><AuthProvider>{children}</AuthProvider></body>
    </html>
  );
}
