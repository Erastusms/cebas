"use client";

import React, { useEffect } from "react";
import Link from "next/link";
import { AlertTriangle, RotateCcw, Home } from "lucide-react";
import { Button } from "../components/ui/button";

interface ErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function RootError({ error, reset }: ErrorProps) {
  useEffect(() => {
    // Structured client-side error reporting hook
    console.error("[CEBAS Client Error Boundary]", {
      message: error.message,
      digest: error.digest,
      timestamp: new Date().toISOString(),
    });
  }, [error]);

  return (
    <main
      role="alert"
      className="min-h-[calc(100vh-4rem)] flex items-center justify-center p-4 bg-background text-foreground"
    >
      <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 sm:p-8 text-center shadow-lg space-y-6">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive">
          <AlertTriangle className="h-8 w-8" aria-hidden="true" />
        </div>

        <div className="space-y-2">
          <h1 className="text-xl font-bold tracking-tight sm:text-2xl text-foreground">
            Terjadi Kesalahan
          </h1>
          <p className="text-sm text-muted-foreground">
            Aplikasi mengalami kendala yang tidak terduga saat memproses halaman ini.
          </p>
          {error.digest && (
            <p className="text-xs font-mono text-muted-foreground/80 pt-1">
              ID Masalah: {error.digest}
            </p>
          )}
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-center gap-3 pt-2">
          <Button
            type="button"
            variant="default"
            size="md"
            onClick={() => reset()}
            className="w-full sm:w-auto"
          >
            <RotateCcw className="mr-2 h-4 w-4" />
            Coba Lagi
          </Button>

          <Link href="/home" className="w-full sm:w-auto">
            <Button
              type="button"
              variant="outline"
              size="md"
              className="w-full sm:w-auto"
            >
              <Home className="mr-2 h-4 w-4" />
              Ke Beranda
            </Button>
          </Link>
        </div>
      </div>
    </main>
  );
}
