import React from "react";
import Link from "next/link";
import { FileQuestion, Home, ArrowLeft } from "lucide-react";
import { Button } from "../components/ui/button";

export default function NotFound() {
  return (
    <main className="min-h-[calc(100vh-4rem)] flex items-center justify-center p-4 bg-background text-foreground">
      <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 sm:p-8 text-center shadow-lg space-y-6">
        <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-3xl bg-primary/10 text-primary">
          <FileQuestion className="h-10 w-10" aria-hidden="true" />
        </div>

        <div className="space-y-2">
          <p className="text-xs font-bold uppercase tracking-widest text-primary">
            404 Error
          </p>
          <h1 className="text-2xl font-black tracking-tight sm:text-3xl text-foreground">
            Halaman Tidak Ditemukan
          </h1>
          <p className="text-sm text-muted-foreground">
            Halaman atau konten yang Anda cari mungkin telah dihapus, dipindahkan, atau tautan yang Anda gunakan tidak valid.
          </p>
        </div>

        <div className="flex flex-col sm:flex-row items-center justify-center gap-3 pt-2">
          <Link href="/home" className="w-full sm:w-auto">
            <Button variant="default" size="md" className="w-full sm:w-auto">
              <Home className="mr-2 h-4 w-4" />
              Kembali ke Beranda
            </Button>
          </Link>

          <Link href="/" className="w-full sm:w-auto">
            <Button variant="outline" size="md" className="w-full sm:w-auto">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Jelajahi CEBAS
            </Button>
          </Link>
        </div>
      </div>
    </main>
  );
}
