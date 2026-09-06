"use client";

import React, { useEffect } from "react";
import { AlertOctagon, RotateCcw } from "lucide-react";

interface GlobalErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function GlobalError({ error, reset }: GlobalErrorProps) {
  useEffect(() => {
    console.error("[CEBAS Global Error Boundary]", {
      message: error.message,
      digest: error.digest,
      timestamp: new Date().toISOString(),
    });
  }, [error]);

  return (
    <html lang="id">
      <body className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-4 font-sans antialiased">
        <div
          role="alert"
          className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 p-6 sm:p-8 text-center shadow-xl space-y-6"
        >
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-rose-500/10 text-rose-500">
            <AlertOctagon className="h-8 w-8" aria-hidden="true" />
          </div>

          <div className="space-y-2">
            <h1 className="text-xl font-bold tracking-tight sm:text-2xl text-slate-50">
              Kesalahan Sistem Kritis
            </h1>
            <p className="text-sm text-slate-400">
              Terjadi kegagalan sistem pada tingkat root. Silakan muat ulang halaman atau coba kembali beberapa saat lagi.
            </p>
            {error.digest && (
              <p className="text-xs font-mono text-slate-500 pt-1">
                Ref: {error.digest}
              </p>
            )}
          </div>

          <div className="flex justify-center pt-2">
            <button
              type="button"
              onClick={() => reset()}
              className="inline-flex items-center justify-center rounded-xl bg-primary px-5 py-2.5 text-sm font-semibold text-primary-foreground shadow-sm transition hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 focus:ring-offset-slate-900"
            >
              <RotateCcw className="mr-2 h-4 w-4" />
              Muat Ulang Aplikasi
            </button>
          </div>
        </div>
      </body>
    </html>
  );
}
