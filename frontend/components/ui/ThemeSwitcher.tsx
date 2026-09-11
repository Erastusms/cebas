"use client";

import React, { useState, useRef, useEffect } from "react";
import { Sun, Moon, Monitor, Check } from "lucide-react";
import { useThemeContext, type ThemeMode } from "../../providers/ThemeProvider";
import { cn } from "../../lib/utils";

export interface ThemeSwitcherProps {
  variant?: "dropdown" | "segmented";
  className?: string;
}

const themeOptions: { value: ThemeMode; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
  { value: "light", label: "Terang", icon: Sun },
  { value: "dark", label: "Gelap", icon: Moon },
  { value: "system", label: "Sistem", icon: Monitor },
];

export function ThemeSwitcher({ variant = "dropdown", className = "" }: ThemeSwitcherProps) {
  const { theme, setTheme, isSyncing } = useThemeContext();
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  // Close dropdown on outside click or Escape key
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
      document.addEventListener("keydown", handleKeyDown);
    }

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  // Segmented Variant (used in Settings / Appearance)
  if (variant === "segmented") {
    return (
      <div
        role="radiogroup"
        aria-label="Pilih tema tampilan"
        className={cn("grid grid-cols-3 gap-2 rounded-xl bg-muted/60 p-1.5 border border-border", className)}
      >
        {themeOptions.map((opt) => {
          const Icon = opt.icon;
          const isSelected = mounted ? theme === opt.value : opt.value === "system";

          return (
            <button
              key={opt.value}
              type="button"
              role="radio"
              aria-checked={isSelected}
              disabled={isSyncing}
              onClick={() => setTheme(opt.value)}
              className={cn(
                "flex items-center justify-center space-x-2 rounded-lg px-3 py-2 text-xs font-semibold transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-1 cursor-pointer select-none",
                isSelected
                  ? "bg-card text-foreground shadow-sm border border-border/80"
                  : "text-muted-foreground hover:text-foreground hover:bg-muted/40"
              )}
            >
              <Icon className={cn("h-4 w-4", isSelected ? "text-primary" : "text-muted-foreground")} />
              <span>{opt.label}</span>
            </button>
          );
        })}
      </div>
    );
  }

  // Dropdown Variant (used in Navbar / compact UI)
  const CurrentIcon = !mounted
    ? Monitor
    : theme === "light"
    ? Sun
    : theme === "dark"
    ? Moon
    : Monitor;

  return (
    <div className={cn("relative inline-block text-left", className)} ref={dropdownRef}>
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        aria-expanded={isOpen}
        aria-haspopup="true"
        aria-label={`Tema tampilan: ${theme || "sistem"}`}
        title={`Tema: ${theme || "sistem"}`}
        className="relative flex h-9 w-9 items-center justify-center rounded-full border border-border bg-card text-foreground transition hover:bg-muted focus:outline-none focus:ring-2 focus:ring-primary/40 cursor-pointer"
      >
        <CurrentIcon className="h-4 w-4 text-foreground transition-transform" />
      </button>

      {isOpen && (
        <div
          role="menu"
          aria-label="Pilihan tema"
          className="absolute right-0 top-full mt-2 w-36 rounded-2xl border border-border bg-popover p-1.5 shadow-xl animate-in fade-in-50 zoom-in-95 z-50"
        >
          {themeOptions.map((opt) => {
            const Icon = opt.icon;
            const isSelected = mounted && theme === opt.value;

            return (
              <button
                key={opt.value}
                type="button"
                role="menuitem"
                onClick={() => {
                  setTheme(opt.value);
                  setIsOpen(false);
                }}
                className={cn(
                  "flex w-full items-center justify-between rounded-xl px-3 py-2 text-xs font-medium transition cursor-pointer select-none",
                  isSelected
                    ? "bg-primary/10 text-primary font-semibold"
                    : "text-foreground hover:bg-muted"
                )}
              >
                <div className="flex items-center space-x-2.5">
                  <Icon className={cn("h-4 w-4", isSelected ? "text-primary" : "text-muted-foreground")} />
                  <span>{opt.label}</span>
                </div>
                {isSelected && <Check className="h-3.5 w-3.5 text-primary" />}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
