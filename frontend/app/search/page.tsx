import React, { Suspense } from "react";
import type { Metadata } from "next";
import { SearchResultsClient } from "../../components/search/SearchResultsClient";

export const metadata: Metadata = {
  title: "Pencarian — CEBAS",
  description: "Cari celotehan, akun pengguna, dan topik diskusi di CEBAS.",
};

export default function SearchPage() {
  return (
    <Suspense
      fallback={
        <div className="mx-auto max-w-3xl px-4 py-8 space-y-4">
          <div className="h-10 w-full rounded-full bg-muted animate-pulse" />
          <div className="h-48 w-full rounded-2xl bg-muted animate-pulse" />
        </div>
      }
    >
      <SearchResultsClient />
    </Suspense>
  );
}
