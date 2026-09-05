import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { QueryProvider } from "../providers/query-provider";
import { RealtimeProvider } from "../providers/RealtimeProvider";
import { ToastContainer } from "../components/ui/toast";
import { RateLimitBanner } from "../components/ui/RateLimitBanner";
import { Navbar } from "../components/layout/Navbar";

import { FloatingPostButton } from "../components/posts/FloatingPostButton";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

export const metadata: Metadata = {
  title: "CEBAS — Celoteh Bebas",
  description: "A high-concurrency, real-time social platform for unhindered public conversation.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="min-h-screen bg-background font-sans text-foreground antialiased selection:bg-primary/20 selection:text-primary">
        <QueryProvider>
          <RealtimeProvider>
            <Navbar />
            {children}
            <FloatingPostButton />
            <RateLimitBanner />
            <ToastContainer />

          </RealtimeProvider>
        </QueryProvider>
      </body>
    </html>
  );
}
