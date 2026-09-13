"use client";

import React, { useState, useRef, useEffect } from "react";
import { useRouter, usePathname, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Search, X, Loader2, User, CheckCircle2, ArrowRight } from "lucide-react";
import { useSearchAutocomplete } from "../../hooks/useSearch";
import { SearchHighlight } from "./SearchHighlight";

interface SearchBarProps {
  initialQuery?: string;
  className?: string;
  onSearchSubmitted?: (query: string) => void;
}

export function SearchBar({
  initialQuery = "",
  className = "",
  onSearchSubmitted,
}: SearchBarProps) {
  const router = useRouter();
  const pathname = usePathname() ?? "";
  const searchParams = useSearchParams();
  const [query, setQuery] = useState(initialQuery);
  const [isOpen, setIsOpen] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState<number>(-1);

  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const { data: users, isLoading, isError } = useSearchAutocomplete(query, isOpen);
  const hasUsers = Array.isArray(users) && users.length > 0;

  // Sync with route / pathname:
  // If user navigates away from /search (or is not on /search), clear the query.
  // If user is on /search, sync with URL query param ?q= or initialQuery
  useEffect(() => {
    if (pathname !== "/search") {
      setQuery("");
      setIsOpen(false);
      setSelectedIndex(-1);
    } else {
      const q = searchParams ? searchParams.get("q") ?? "" : "";
      if (initialQuery !== undefined && initialQuery !== "") {
        setQuery(initialQuery);
      } else if (q) {
        setQuery(q);
      }
    }
  }, [pathname, searchParams, initialQuery]);

  // Handle outside click & Escape key
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setIsOpen(false);
        inputRef.current?.blur();
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, []);

  const handleSubmit = (searchQuery: string) => {
    const trimmed = searchQuery.trim();
    if (!trimmed) return;

    setIsOpen(false);
    if (onSearchSubmitted) {
      onSearchSubmitted(trimmed);
    } else {
      router.push(`/search?q=${encodeURIComponent(trimmed)}`);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    const itemsCount = (users?.length ?? 0) + 1; // +1 for "Search all results" option

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setIsOpen(true);
      setSelectedIndex((prev) => (prev + 1) % itemsCount);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setIsOpen(true);
      setSelectedIndex((prev) => (prev - 1 + itemsCount) % itemsCount);
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (selectedIndex >= 0 && users && selectedIndex < users.length) {
        const user = users[selectedIndex];
        setIsOpen(false);
        router.push(`/user/${encodeURIComponent(user.username)}`);
      } else {
        handleSubmit(query);
      }
    }
  };

  const handleClear = () => {
    setQuery("");
    setSelectedIndex(-1);
    inputRef.current?.focus();
  };

  return (
    <div ref={containerRef} className={`relative w-full max-w-md ${className}`}>
      {/* Search Input Container */}
      <div className="relative flex items-center">
        <Search className="absolute left-3 h-4 w-4 text-muted-foreground pointer-events-none" />
        <input
          ref={inputRef}
          type="text"
          role="searchbox"
          aria-label="Cari di CEBAS"
          aria-autocomplete="list"
          placeholder="Cari celotehan, akun, atau topik..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(true);
            setSelectedIndex(-1);
          }}
          onFocus={() => setIsOpen(true)}
          onKeyDown={handleKeyDown}
          className="w-full rounded-full border border-border bg-muted/40 py-2 pl-9 pr-9 text-xs sm:text-sm text-foreground placeholder:text-muted-foreground transition focus:border-primary/50 focus:bg-background focus:outline-none focus:ring-2 focus:ring-primary/20"
        />

        {/* Right side icons: Loading Spinner or Clear button */}
        {isLoading && query.trim().length > 0 ? (
          <div className="absolute right-3 flex items-center pointer-events-none">
            <Loader2 className="h-4 w-4 animate-spin text-primary" />
          </div>
        ) : query.length > 0 ? (
          <button
            type="button"
            onClick={handleClear}
            aria-label="Hapus pencarian"
            className="absolute right-2.5 flex h-5 w-5 items-center justify-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground transition"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        ) : null}
      </div>

      {/* Autocomplete Dropdown */}
      {isOpen && query.trim().length > 0 && (
        <div
          role="listbox"
          className="absolute left-0 right-0 top-full mt-2 rounded-2xl border border-border bg-popover p-1.5 shadow-xl backdrop-blur animate-in fade-in-50 zoom-in-95 z-50 overflow-hidden"
        >
          {/* Autocomplete User Account Results */}
          {hasUsers && (
            <div className="space-y-0.5">
              <div className="px-3 py-1 text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
                Akun
              </div>
              {users.map((user, idx) => {
                const isSelected = selectedIndex === idx;
                return (
                  <Link
                    key={user.id}
                    href={`/user/${encodeURIComponent(user.username)}`}
                    onClick={() => setIsOpen(false)}
                    role="option"
                    aria-selected={isSelected}
                    className={`flex items-center justify-between rounded-xl px-3 py-2 text-xs transition ${
                      isSelected ? "bg-muted text-primary" : "hover:bg-muted/80 text-foreground"
                    }`}
                  >
                    <div className="flex items-center space-x-2.5 min-w-0">
                      {/* Avatar */}
                      <div className="h-8 w-8 rounded-full bg-primary/10 flex items-center justify-center overflow-hidden flex-shrink-0 text-primary font-bold">
                        {user.avatarUrl ? (
                          <img
                            src={user.avatarUrl}
                            alt={user.displayName || user.username}
                            className="h-full w-full object-cover"
                          />
                        ) : (
                          user.displayName?.charAt(0).toUpperCase() || (
                            <User className="h-4 w-4" />
                          )
                        )}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center space-x-1">
                          <span className="font-semibold truncate">
                            <SearchHighlight
                              text={user.highlightedDisplayName}
                              fallbackText={user.displayName || user.username}
                            />
                          </span>
                          {user.isVerified && (
                            <CheckCircle2 className="h-3 w-3 text-primary flex-shrink-0" />
                          )}
                        </div>
                        <p className="text-[11px] text-muted-foreground truncate">
                          @
                          <SearchHighlight
                            text={user.highlightedUsername}
                            fallbackText={user.username}
                          />
                        </p>
                      </div>
                    </div>

                    <ArrowRight className="h-3 w-3 text-muted-foreground flex-shrink-0 ml-2 opacity-50" />
                  </Link>
                );
              })}
            </div>
          )}

          {/* Autocomplete Loading / Error States */}
          {isLoading && !hasUsers && (
            <div className="flex items-center justify-center py-4 text-xs text-muted-foreground space-x-2">
              <Loader2 className="h-4 w-4 animate-spin text-primary" />
              <span>Mencari...</span>
            </div>
          )}

          {isError && (
            <div className="p-3 text-center text-xs text-destructive">
              Layanan pencarian tidak dapat dihubungi.
            </div>
          )}

          {/* Full Search Action Link */}
          <div className="border-t border-border/70 mt-1 pt-1">
            <button
              type="button"
              onClick={() => handleSubmit(query)}
              className={`flex w-full items-center justify-between rounded-xl px-3 py-2 text-xs font-medium transition ${
                selectedIndex === (users?.length ?? 0)
                  ? "bg-primary/10 text-primary"
                  : "text-primary hover:bg-primary/5"
              }`}
            >
              <div className="flex items-center space-x-2 truncate">
                <Search className="h-3.5 w-3.5 flex-shrink-0" />
                <span className="truncate">
                  Cari &quot;<span className="font-bold">{query.trim()}</span>&quot; di Semua Hasil
                </span>
              </div>
              <span className="text-[10px] text-muted-foreground uppercase border border-border px-1.5 py-0.5 rounded">
                ↵ Enter
              </span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
