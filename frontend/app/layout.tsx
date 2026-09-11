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
  metadataBase: new URL(process.env.NEXT_PUBLIC_APP_URL || "http://localhost:3000"),
  title: {
    default: "CEBAS — Celoteh Bebas",
    template: "%s | CEBAS",
  },
  description: "A high-concurrency, real-time social platform for unhindered public conversation.",
  openGraph: {
    title: "CEBAS — Celoteh Bebas",
    description: "A high-concurrency, real-time social platform for unhindered public conversation.",
    type: "website",
    locale: "id_ID",
    siteName: "CEBAS",
  },
  twitter: {
    card: "summary_large_image",
    title: "CEBAS — Celoteh Bebas",
    description: "A high-concurrency, real-time social platform for unhindered public conversation.",
  },
  robots: {
    index: true,
    follow: true,
  },
};

import { ThemeProvider } from "../providers/ThemeProvider";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="id" className={inter.variable} suppressHydrationWarning>
      <body className="min-h-screen bg-background font-sans text-foreground antialiased selection:bg-primary/20 selection:text-primary">
        {/* WCAG 2.2 AA Skip to Content Link */}
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:fixed focus:top-4 focus:left-4 focus:z-50 focus:rounded-xl focus:bg-primary focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-primary-foreground focus:shadow-lg focus:outline-none focus:ring-2 focus:ring-offset-2"
        >
          Lewati ke konten utama
        </a>

        <QueryProvider>
          <ThemeProvider>
            <RealtimeProvider>
              <Navbar />
              <div id="main-content">
                {children}
              </div>
              <FloatingPostButton />
              <RateLimitBanner />
              <ToastContainer />
            </RealtimeProvider>
          </ThemeProvider>
        </QueryProvider>
      </body>
    </html>
  );
}
