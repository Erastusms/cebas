"use client";

import React from "react";
import Link from "next/link";
import { ExternalLink } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { Button } from "../ui/button";

export function Navbar() {
  const { user, isAuthenticated, isLoading, logout, isLoggingOut } = useAuth();

  return (
    <header className="bg-background/80 sticky top-0 z-40 w-full border-b border-border backdrop-blur">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Brand Logo */}
        <Link href="/" className="group flex items-center space-x-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary font-bold text-primary-foreground shadow-md transition-transform group-hover:scale-105">
            C
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <span className="text-lg font-bold tracking-tight text-foreground">
                CEBAS
              </span>
              <span className="bg-primary/10 border-primary/20 rounded-full border px-2 py-0.5 text-xs font-semibold text-primary">
                Phase 1
              </span>
            </div>
            <p className="hidden text-xs text-muted-foreground sm:block">
              Celoteh Bebas — Real-Time Social
            </p>
          </div>
        </Link>

        {/* Action Controls / Auth Status */}
        <div className="flex items-center space-x-3">
          <a
            href="https://localhost:7090/swagger"
            target="_blank"
            rel="noreferrer"
            className="bg-muted/40 hidden items-center space-x-1.5 rounded-md border border-border px-3 py-1.5 text-xs text-muted-foreground transition hover:bg-muted hover:text-foreground md:inline-flex"
          >
            <span>OpenAPI Docs</span>
            <ExternalLink className="h-3.5 w-3.5" />
          </a>

          {isLoading ? (
            <div className="h-9 w-24 animate-pulse rounded-lg bg-muted" />
          ) : isAuthenticated ? (
            <div className="flex items-center space-x-3">
              {user && (
                <Link
                  href={`/user/${user.username}`}
                  className="flex items-center space-x-2 rounded-full border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted transition"
                  title="View Public Profile"
                >
                  <div className="flex h-6 w-6 items-center justify-center rounded-full bg-primary text-xs font-bold text-primary-foreground">
                    {user.displayName?.charAt(0).toUpperCase() ||
                      user.username.charAt(0).toUpperCase()}
                  </div>
                  <span className="max-w-[120px] truncate text-xs font-medium sm:text-sm">
                    @{user.username}
                  </span>
                </Link>
              )}
              <Button
                variant="outline"
                size="sm"
                onClick={() => logout()}
                isLoading={isLoggingOut}
                className="text-xs font-semibold hover:bg-destructive/10 hover:text-destructive hover:border-destructive/30 transition"
              >
                Log Out
              </Button>
            </div>
          ) : (
            <div className="flex items-center space-x-2">
              <Link href="/login">
                <Button variant="ghost" size="sm">
                  Log In
                </Button>
              </Link>
              <Link href="/register">
                <Button variant="default" size="sm">
                  Register
                </Button>
              </Link>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
