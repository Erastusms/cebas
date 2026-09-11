"use client";

import React, { createContext, useContext, useEffect, useRef, useState, useCallback } from "react";
import { ThemeProvider as NextThemesProvider, useTheme as useNextTheme } from "next-themes";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../hooks/useAuth";
import { usersApi } from "../lib/api/users";
import { useToast } from "../hooks/useToast";
import type { ThemePreference } from "../types/auth";

export type ThemeMode = "light" | "dark" | "system";

interface ThemeContextType {
  theme: ThemeMode;
  resolvedTheme?: "light" | "dark";
  setTheme: (mode: ThemeMode) => Promise<void>;
  isSyncing: boolean;
}

const ThemeContext = createContext<ThemeContextType | null>(null);

function ThemeSyncInternal({ children }: { children: React.ReactNode }) {
  const { theme, setTheme: setNextTheme, resolvedTheme } = useNextTheme();
  const { user, isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const { error: showToastError } = useToast();

  const [isSyncing, setIsSyncing] = useState(false);
  
  // Track timestamp of last explicit user action to protect against stale asynchronous GET /me responses
  const lastUserSelectionTimeRef = useRef<number>(0);
  const initialSyncCompletedRef = useRef<boolean>(false);

  // Synchronize server preference on authenticated load
  useEffect(() => {
    if (!isAuthenticated || !user || !user.themePreference) {
      return;
    }

    const serverPref = user.themePreference.toLowerCase() as ThemeMode;

    // If user made an explicit manual change during this session, do not overwrite if the change is newer
    if (lastUserSelectionTimeRef.current > 0) {
      return;
    }

    // Only apply if server preference differs and initial sync hasn't occurred yet or user profile just loaded
    if (!initialSyncCompletedRef.current || theme !== serverPref) {
      initialSyncCompletedRef.current = true;
      if (theme !== serverPref) {
        setNextTheme(serverPref);
      }
    }
  }, [isAuthenticated, user?.themePreference, theme, setNextTheme]);

  // Synchronize user action to localStorage and server
  const handleSetTheme = useCallback(
    async (mode: ThemeMode) => {
      const now = Date.now();
      lastUserSelectionTimeRef.current = now;

      // 1. Immediately apply theme locally via next-themes (zero lag, updates class and localStorage)
      setNextTheme(mode);

      // 2. If unauthenticated, no server request is needed
      if (!isAuthenticated || !user) {
        return;
      }

      // 3. Persist to server transactionally
      setIsSyncing(true);
      try {
        const canonicalTheme = mode.toUpperCase() as ThemePreference;
        const res = await usersApi.updateProfile({
          themePreference: canonicalTheme,
        });

        // Update React Query cache
        if (res.data) {
          queryClient.setQueryData(["currentUser"], res.data);
          if (typeof window !== "undefined") {
            localStorage.setItem("cebas_user", JSON.stringify(res.data));
          }
        }
      } catch (err: unknown) {
        console.error("[ThemeSync] Failed to persist theme to server:", err);
        showToastError(
          "Gagal menyimpan preferensi tema ke akun Anda. Pengaturan lokal tetap aktif.",
          "Sinkronisasi Tema"
        );
      } finally {
        setIsSyncing(false);
      }
    },
    [isAuthenticated, user, setNextTheme, queryClient, showToastError]
  );

  const contextValue: ThemeContextType = {
    theme: (theme as ThemeMode) || "system",
    resolvedTheme: resolvedTheme as "light" | "dark" | undefined,
    setTheme: handleSetTheme,
    isSyncing,
  };

  return <ThemeContext.Provider value={contextValue}>{children}</ThemeContext.Provider>;
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      <ThemeSyncInternal>{children}</ThemeSyncInternal>
    </NextThemesProvider>
  );
}

export function useThemeContext(): ThemeContextType {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    // Graceful fallback if consumed outside ThemeProvider (e.g. in standalone unit tests)
    return {
      theme: "system",
      resolvedTheme: "light",
      setTheme: async () => {},
      isSyncing: false,
    };
  }
  return ctx;
}
