"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Smartphone, Monitor, User, RefreshCw, AlertTriangle } from "lucide-react";
import { useSessions } from "../../../hooks/useSessions";
import { AuthGuard } from "../../../components/auth/AuthGuard";
import { Button } from "../../../components/ui/button";
import { Modal } from "../../../components/ui/modal";
import { Skeleton } from "../../../components/ui/skeleton";
import type { SessionItem } from "../../../types/user";

export default function SessionsDashboardPage() {
  const { sessions, isLoading, isError, revokeSession, isRevoking, refetchSessions } = useSessions();
  const [selectedSessionToRevoke, setSelectedSessionToRevoke] = useState<SessionItem | null>(null);

  const handleConfirmRevoke = async () => {
    if (!selectedSessionToRevoke) return;
    try {
      await revokeSession({
        sessionId: selectedSessionToRevoke.id,
        isCurrent: selectedSessionToRevoke.isCurrent,
      });
      setSelectedSessionToRevoke(null);
    } catch {
      // Handled by hook toast
    }
  };

  const parseDeviceName = (userAgent?: string | null) => {
    if (!userAgent) return "Unknown Device";
    if (userAgent.includes("Mobile") || userAgent.includes("Android") || userAgent.includes("iPhone")) {
      return "Mobile Browser";
    }
    if (userAgent.includes("Macintosh") || userAgent.includes("Mac OS")) {
      return "Mac Desktop";
    }
    if (userAgent.includes("Windows")) {
      return "Windows PC";
    }
    if (userAgent.includes("Linux")) {
      return "Linux Workstation";
    }
    return "Web Browser";
  };

  return (
    <AuthGuard>
      <main className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground">Active Sessions & Devices</h1>
          <p className="text-sm text-muted-foreground">
            Inspect active login sessions and revoke access from lost or untrusted devices.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Sidebar Navigation */}
          <div className="space-y-1">
            <Link
              href="/settings"
              className="flex items-center space-x-2.5 px-3 py-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 font-medium text-sm transition"
            >
              <User className="h-4 w-4" />
              <span>Profile & Account</span>
            </Link>
            <Link
              href="/settings/sessions"
              className="flex items-center space-x-2.5 px-3 py-2 rounded-lg bg-muted text-foreground font-medium text-sm"
            >
              <Smartphone className="h-4 w-4 text-primary" />
              <span>Active Sessions</span>
            </Link>
          </div>

          {/* Sessions List */}
          <div className="md:col-span-2 space-y-4">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-foreground">
                {sessions.length} {sessions.length === 1 ? "Active Session" : "Active Sessions"}
              </span>
              <Button
                variant="outline"
                size="sm"
                className="text-xs"
                onClick={() => refetchSessions()}
              >
                <RefreshCw className="h-3 w-3 mr-1.5" />
                Refresh
              </Button>
            </div>

            {isLoading ? (
              <div className="space-y-3">
                <Skeleton className="h-24 w-full rounded-xl" />
                <Skeleton className="h-24 w-full rounded-xl" />
              </div>
            ) : isError ? (
              <div className="p-4 rounded-xl border border-destructive/30 bg-destructive/10 text-destructive text-sm">
                Failed to load active sessions. Please try refreshing.
              </div>
            ) : sessions.length === 0 ? (
              <div className="p-8 rounded-xl border border-border bg-card text-center text-muted-foreground text-sm">
                No active sessions found.
              </div>
            ) : (
              <div className="space-y-3">
                {sessions.map((session) => (
                  <div
                    key={session.id}
                    className={`rounded-xl border p-4 sm:p-5 transition shadow-sm bg-card ${
                      session.isCurrent ? "border-primary/50 bg-primary/5" : "border-border"
                    }`}
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex items-start space-x-3.5">
                        <div className="mt-0.5 p-2 rounded-lg bg-muted text-foreground">
                          {session.userAgent?.includes("Mobile") ? (
                            <Smartphone className="h-5 w-5" />
                          ) : (
                            <Monitor className="h-5 w-5" />
                          )}
                        </div>

                        <div className="space-y-1">
                          <div className="flex items-center space-x-2">
                            <span className="font-semibold text-sm text-foreground">
                              {parseDeviceName(session.userAgent)}
                            </span>
                            {session.isCurrent && (
                              <span className="rounded-full bg-emerald-500/10 px-2 py-0.5 text-xs font-semibold text-emerald-500 border border-emerald-500/20">
                                Current Device
                              </span>
                            )}
                          </div>

                          <div className="text-xs text-muted-foreground space-y-0.5">
                            {session.ipAddress && (
                              <p className="font-mono">IP: {session.ipAddress}</p>
                            )}
                            <p>
                              Logged in on{" "}
                              {new Date(session.createdAt).toLocaleDateString("en-US", {
                                month: "short",
                                day: "numeric",
                                year: "numeric",
                                hour: "2-digit",
                                minute: "2-digit",
                              })}
                            </p>
                            {session.userAgent && (
                              <p className="text-[11px] text-muted-foreground/80 line-clamp-1 max-w-md">
                                {session.userAgent}
                              </p>
                            )}
                          </div>
                        </div>
                      </div>

                      <div>
                        <Button
                          variant={session.isCurrent ? "destructive" : "outline"}
                          size="sm"
                          className="text-xs"
                          onClick={() => setSelectedSessionToRevoke(session)}
                        >
                          {session.isCurrent ? "Log Out Device" : "Revoke"}
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Confirmation Modal */}
        <Modal
          isOpen={!!selectedSessionToRevoke}
          onClose={() => setSelectedSessionToRevoke(null)}
          title={selectedSessionToRevoke?.isCurrent ? "Log Out Current Session?" : "Revoke Active Session?"}
          description={
            selectedSessionToRevoke?.isCurrent
              ? "You are about to log out from this browser session. You will need to sign in again."
              : "This device will be immediately signed out and its authentication cookie invalidated."
          }
        >
          <div className="space-y-4 pt-2">
            <div className="flex items-start space-x-3 p-3 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-500 text-xs">
              <AlertTriangle className="h-5 w-5 shrink-0 mt-0.5" />
              <span>
                {selectedSessionToRevoke?.isCurrent
                  ? "Terminating your current session will immediately return you to the login screen."
                  : "Any active requests from that device will be rejected with 401 Unauthorized."}
              </span>
            </div>

            <div className="flex justify-end space-x-2 pt-3 border-t border-border">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setSelectedSessionToRevoke(null)}
                disabled={isRevoking}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                size="sm"
                onClick={handleConfirmRevoke}
                isLoading={isRevoking}
              >
                {selectedSessionToRevoke?.isCurrent ? "Confirm Logout" : "Revoke Session"}
              </Button>
            </div>
          </div>
        </Modal>
      </main>
    </AuthGuard>
  );
}
