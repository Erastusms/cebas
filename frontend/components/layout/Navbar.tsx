"use client";

import React, { useState, useRef, useEffect } from "react";
import Link from "next/link";
import { ExternalLink, Smartphone, User, Bookmark, LogOut, ChevronDown, Home, Bell } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { useUnreadNotificationCount } from "../../hooks/useNotifications";
import { Button } from "../ui/button";

export function Navbar() {
  const { user, isAuthenticated, isLoading, logout, isLoggingOut } = useAuth();
  const { unreadCount } = useUnreadNotificationCount();
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click or Escape key
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsMenuOpen(false);
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setIsMenuOpen(false);
      }
    };

    if (isMenuOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      document.addEventListener("keydown", handleKeyDown);
    }

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isMenuOpen]);

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
                Social
              </span>
            </div>
            <p className="hidden text-xs text-muted-foreground sm:block">
              Celoteh Bebas — Real-Time Social
            </p>
          </div>
        </Link>

        {/* Action Controls / Auth Status */}
        <div className="flex items-center space-x-2 sm:space-x-3">
          <Link
            href="/home"
            className="hidden items-center space-x-1.5 rounded-md border border-border bg-card px-3 py-1.5 text-xs font-semibold text-foreground transition hover:bg-muted sm:inline-flex"
          >
            <Home className="h-3.5 w-3.5 text-primary" />
            <span>Linimasa</span>
          </Link>

          <a
            href={`${process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5226"}/swagger`}
            target="_blank"
            rel="noreferrer"
            className="bg-muted/40 hidden items-center space-x-1.5 rounded-md border border-border px-3 py-1.5 text-xs text-muted-foreground transition hover:bg-muted hover:text-foreground md:inline-flex"
          >
            <span>OpenAPI Docs</span>
            <ExternalLink className="h-3.5 w-3.5" />
          </a>

          {isLoading ? (
            <div className="h-9 w-24 animate-pulse rounded-lg bg-muted" />
          ) : isAuthenticated && user ? (
            <div className="flex items-center space-x-2">
              {/* Notification Bell Button */}
              <Link
                href="/notifications"
                aria-label={`Notifikasi${unreadCount > 0 ? ` (${unreadCount} belum dibaca)` : ""}`}
                className="relative flex h-9 w-9 items-center justify-center rounded-full border border-border bg-card text-foreground transition hover:bg-muted focus:outline-none focus:ring-2 focus:ring-primary/40"
              >
                <Bell className="h-4 w-4" />
                {unreadCount > 0 && (
                  <span
                    data-testid="notification-badge"
                    className="absolute -top-1 -right-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-primary px-1 text-[10px] font-bold text-primary-foreground shadow-sm"
                  >
                    {unreadCount > 99 ? "99+" : unreadCount}
                  </span>
                )}
              </Link>

              {/* Single User Profile Dropdown Menu */}
              <div className="relative" ref={menuRef}>
                <button
                  type="button"
                  onClick={() => setIsMenuOpen((prev) => !prev)}
                  aria-expanded={isMenuOpen}
                  aria-haspopup="true"
                  className="flex items-center space-x-2 rounded-full border border-border bg-card py-1.5 pl-2 pr-3 text-sm font-medium text-foreground transition hover:bg-muted focus:outline-none focus:ring-2 focus:ring-primary/40"
                >
                  <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary text-xs font-bold text-primary-foreground overflow-hidden flex-shrink-0">
                    {user.avatarUrl ? (
                      <img
                        src={user.avatarUrl}
                        alt={user.displayName || user.username}
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      user.displayName?.charAt(0).toUpperCase() ||
                      user.username?.charAt(0).toUpperCase() || (
                        <User className="h-3.5 w-3.5" />
                      )
                    )}
                  </div>
                  <span className="max-w-[120px] truncate text-xs font-semibold sm:text-sm">
                    @{user.username}
                  </span>
                  <ChevronDown
                    className={`h-3.5 w-3.5 text-muted-foreground transition-transform duration-200 ${
                      isMenuOpen ? "rotate-180" : ""
                    }`}
                  />
                </button>

                {/* Dropdown Menu Items */}
                {isMenuOpen && (
                  <div
                    role="menu"
                    className="absolute right-0 top-full mt-2 w-56 rounded-2xl border border-border bg-popover p-1.5 shadow-xl animate-in fade-in-50 zoom-in-95 z-50"
                  >
                    {/* User info summary header */}
                    <div className="px-3 py-2 border-b border-border/70">
                      <p className="text-xs font-bold text-foreground truncate">
                        {user.displayName || user.username}
                      </p>
                      <p className="text-[11px] text-muted-foreground truncate">
                        @{user.username}
                      </p>
                    </div>

                    <div className="py-1 space-y-0.5">
                      {/* Item 1: Linimasa */}
                      <Link
                        href="/home"
                        onClick={() => setIsMenuOpen(false)}
                        role="menuitem"
                        className="flex items-center space-x-2.5 rounded-xl px-3 py-2 text-xs font-medium text-foreground hover:bg-muted transition"
                      >
                        <Home className="h-4 w-4 text-primary" />
                        <span>Linimasa</span>
                      </Link>

                      {/* Item 2: Notifikasi */}
                      <Link
                        href="/notifications"
                        onClick={() => setIsMenuOpen(false)}
                        role="menuitem"
                        className="flex items-center justify-between rounded-xl px-3 py-2 text-xs font-medium text-foreground hover:bg-muted transition"
                      >
                        <div className="flex items-center space-x-2.5">
                          <Bell className="h-4 w-4 text-primary" />
                          <span>Notifikasi</span>
                        </div>
                        {unreadCount > 0 && (
                          <span className="rounded-full bg-primary/20 px-1.5 py-0.5 text-[10px] font-bold text-primary">
                            {unreadCount}
                          </span>
                        )}
                      </Link>

                      {/* Item 3: My Profile */}
                      <Link
                        href={`/user/${encodeURIComponent(user.username)}`}
                        onClick={() => setIsMenuOpen(false)}
                        role="menuitem"
                        className="flex items-center space-x-2.5 rounded-xl px-3 py-2 text-xs font-medium text-foreground hover:bg-muted transition"
                      >
                        <User className="h-4 w-4 text-primary" />
                        <span>My Profile</span>
                      </Link>

                      {/* Item 4: Bookmarks */}
                      <Link
                        href="/bookmarks"
                        onClick={() => setIsMenuOpen(false)}
                        role="menuitem"
                        className="flex items-center space-x-2.5 rounded-xl px-3 py-2 text-xs font-medium text-foreground hover:bg-muted transition"
                      >
                        <Bookmark className="h-4 w-4 text-primary" />
                        <span>Bookmarks</span>
                      </Link>

                      {/* Item 5: My Sessions */}
                      <Link
                        href="/settings/sessions"
                        onClick={() => setIsMenuOpen(false)}
                        role="menuitem"
                        className="flex items-center space-x-2.5 rounded-xl px-3 py-2 text-xs font-medium text-foreground hover:bg-muted transition"
                      >
                        <Smartphone className="h-4 w-4 text-muted-foreground" />
                        <span>My Sessions</span>
                      </Link>
                    </div>

                    <div className="border-t border-border/70 pt-1">
                    {/* Item 3: Logout */}
                    <button
                      type="button"
                      onClick={() => {
                        setIsMenuOpen(false);
                        logout();
                      }}
                      disabled={isLoggingOut}
                      role="menuitem"
                      className="flex w-full items-center space-x-2.5 rounded-xl px-3 py-2 text-xs font-medium text-destructive hover:bg-destructive/10 transition"
                    >
                      <LogOut className="h-4 w-4" />
                      <span>{isLoggingOut ? "Logging out..." : "Logout"}</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
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
