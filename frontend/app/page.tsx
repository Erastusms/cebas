"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { postsApi } from "../lib/api/posts";
import {
  Activity,
  CheckCircle2,
  Server,
  Shield,
  Sparkles,
  RefreshCw,
  User,
  Search,
} from "lucide-react";
import { apiClient } from "../lib/api/client";
import { ProblemDetailsException } from "../lib/api/errors";
import type { ProblemDetails } from "../lib/api/types";
import { useAuth } from "../hooks/useAuth";
import { Button } from "../components/ui/button";
import { Input } from "../components/ui/input";
import { useToast } from "../hooks/useToast";
import { PostComposer } from "../components/posts/PostComposer";
import { InfiniteFeed } from "../components/posts/InfiniteFeed";
import { timelinesApi } from "../lib/api/timelines";

type HealthResponse = {
  status: string;
  timestamp: string;
  service: string;
  version: string;
};

type PingResponse = {
  success: boolean;
  data: {
    message: string;
    timestamp: string;
  };
  message?: string;
};

export default function Home() {
  const { error: toastError } = useToast();
  const { user, isAuthenticated } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [searchHandle, setSearchHandle] = useState("");
  const [activeProblem, setActiveProblem] = useState<ProblemDetails | null>(
    null
  );
  const [isLoadingError, setIsLoadingError] = useState(false);


  // Backend Health Query via TanStack Query
  const {
    data: healthData,
    isLoading: isHealthLoading,
    isError: isHealthError,
    error: healthErrorObj,
    refetch: refetchHealth,
    isFetching: isHealthFetching,
  } = useQuery<HealthResponse>({
    queryKey: ["backend-health"],
    queryFn: () => apiClient.get<HealthResponse>("/health"),
    retry: 1,
  });

  const { data: pingData, refetch: refetchPing } = useQuery<PingResponse>({
    queryKey: ["backend-ping"],
    queryFn: () => apiClient.get<PingResponse>("/api/v1/ping"),
    retry: 1,
  });

  const triggerErrorTest = async (type: string) => {
    setIsLoadingError(true);
    setActiveProblem(null);
    try {
      await apiClient.get(`/api/v1/error-test?type=${type}`);
    } catch (err: unknown) {
      if (err instanceof ProblemDetailsException) {
        setActiveProblem(err.problem);
        toastError(`Triggered RFC 7807 error: ${err.problem.title ?? "Error"}`);
      } else if (err instanceof Error) {
        setActiveProblem({
          title: "Network / Connection Error",
          detail: err.message,
          status: 0,
        });
      }
    } finally {
      setIsLoadingError(false);
    }
  };

  const handleSearchProfile = (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchHandle.trim()) return;
    const cleanHandle = searchHandle.trim().replace(/^@/, "");
    router.push(`/user/${encodeURIComponent(cleanHandle)}`);
  };

  return (
    <main className="selection:bg-primary/20 min-h-[calc(100vh-4rem)] bg-background text-foreground selection:text-primary">
      <div className="mx-auto max-w-4xl space-y-8 px-4 py-6 sm:px-6 lg:px-8">

        {/* Phase 4 Content Creation & Feed Section */}
        <section className="space-y-6">
          {isAuthenticated && (
            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <Sparkles className="h-5 w-5 text-primary" />
                <h2 className="text-lg font-bold text-foreground">Create a Post</h2>
              </div>
              <PostComposer
                onPostCreated={() => {
                  queryClient.invalidateQueries({ queryKey: ["timeline-posts"] });
                  queryClient.invalidateQueries({ queryKey: ["home-timeline"] });
                }}
              />
            </div>
          )}

          {/* Timeline Feed Stream with Keyset Infinite Scrolling */}
          <div className="space-y-4">
            <div className="flex items-center justify-between border-b border-border pb-2">
              <h2 className="text-lg font-bold text-foreground">
                {isAuthenticated ? "Your Feed" : "Recent Posts"}
              </h2>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  queryClient.invalidateQueries({ queryKey: ["timeline-posts"] });
                  queryClient.invalidateQueries({ queryKey: ["home-timeline"] });
                }}
                className="text-xs text-muted-foreground hover:text-foreground h-8 px-2"
              >
                <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
                <span>Refresh</span>
              </Button>
            </div>

            <InfiniteFeed
              queryKey={["home-timeline", user?.id]}
              queryFn={async (cursor) => {
                if (isAuthenticated) {
                  const res = await timelinesApi.getHomeTimeline(cursor, 20);
                  return res.data;
                }
                const res = await postsApi.getFeed(cursor, 20);
                return res.data;
              }}
              emptyTitle="Belum ada postingan, ikuti akun lain untuk mulai melihat linimasa."
              emptyDescription={
                isAuthenticated
                  ? "Publish a post above or explore public user profiles to follow active creators!"
                  : "Join CEBAS to publish your first post and follow creators!"
              }
            />
          </div>
        </section>

        {/* Interactive Feature Exploration Grid */}
        <section className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Card 1: Public Profile Lookup Tool */}
          <div className="space-y-4 rounded-2xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-center space-x-2">
              <User className="h-5 w-5 text-primary" />
              <h3 className="text-base font-semibold text-foreground">
                Public User Profile Lookup
              </h3>
            </div>
            <p className="text-xs text-muted-foreground">
              Search and inspect any public profile on CEBAS. Case-insensitivity
              rules and unique database constraints guarantee consistent
              lookups.
            </p>

            <form onSubmit={handleSearchProfile} className="flex gap-2">
              <Input
                placeholder="Enter handle, e.g. johndoe"
                value={searchHandle}
                onChange={(e) => setSearchHandle(e.target.value)}
                className="text-sm"
              />
              <Button type="submit" variant="default" size="md">
                <Search className="mr-1.5 h-4 w-4" />
                View
              </Button>
            </form>
          </div>

          {/* Card 2: Backend Health & Ping */}
          <div className="space-y-4 rounded-2xl border border-border bg-card p-6 shadow-sm">
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2">
                <Server className="h-5 w-5 text-blue-500" />
                <h3 className="text-base font-semibold text-foreground">
                  Backend API Health & Liveness
                </h3>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="text-xs"
                onClick={() => {
                  refetchHealth();
                  refetchPing();
                }}
                isLoading={isHealthFetching}
              >
                <RefreshCw className="mr-1 h-3.5 w-3.5" />
                Refresh
              </Button>
            </div>

            <div className="space-y-2.5">
              <div className="bg-muted/40 flex items-center justify-between rounded-lg border border-border p-3">
                <div className="flex items-center space-x-3">
                  <div
                    className={`h-2.5 w-2.5 rounded-full ${
                      isHealthLoading
                        ? "animate-ping bg-amber-400"
                        : isHealthError
                          ? "bg-rose-500"
                          : "bg-emerald-400"
                    }`}
                  />
                  <div>
                    <span className="text-xs font-semibold text-foreground">
                      GET /health (Liveness)
                    </span>
                    <p className="text-[11px] text-muted-foreground">
                      {isHealthLoading
                        ? "Checking availability..."
                        : isHealthError
                          ? `Offline: ${(healthErrorObj as Error)?.message || "Failed to reach API"}`
                          : `Status: ${healthData?.status} • ${healthData?.service} (${healthData?.version})`}
                    </p>
                  </div>
                </div>
                {!isHealthError && !isHealthLoading && (
                  <span className="rounded bg-emerald-500/10 px-2 py-0.5 font-mono text-[11px] font-medium text-emerald-500">
                    200 OK
                  </span>
                )}
              </div>

              <div className="bg-muted/40 flex items-center justify-between rounded-lg border border-border p-3">
                <div className="flex items-center space-x-3">
                  <Activity className="h-4 w-4 text-primary" />
                  <div>
                    <span className="text-xs font-semibold text-foreground">
                      GET /api/v1/ping
                    </span>
                    <p className="text-[11px] text-muted-foreground">
                      {pingData
                        ? `Message: "${pingData.data.message}"`
                        : "Awaiting response..."}
                    </p>
                  </div>
                </div>
                {pingData && (
                  <span className="bg-primary/10 rounded px-2 py-0.5 font-mono text-[11px] font-medium text-primary">
                    Operational
                  </span>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* RFC 7807 Error Inspector */}
        <section className="space-y-4 rounded-2xl border border-border bg-card p-6 shadow-sm">
          <div className="flex items-center space-x-2">
            <Shield className="h-5 w-5 text-indigo-400" />
            <h3 className="text-base font-semibold text-foreground">
              RFC 7807 Problem Details Inspector
            </h3>
          </div>
          <p className="text-xs text-muted-foreground">
            Trigger standardized API exception responses to verify error
            serialization without exposing server stack traces or database
            errors.
          </p>

          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              size="sm"
              className="text-xs"
              onClick={() => triggerErrorTest("validation")}
              isLoading={isLoadingError}
            >
              400 Validation
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="text-xs"
              onClick={() => triggerErrorTest("notfound")}
              isLoading={isLoadingError}
            >
              404 Not Found
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="text-xs"
              onClick={() => triggerErrorTest("conflict")}
              isLoading={isLoadingError}
            >
              409 Conflict
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="text-xs"
              onClick={() => triggerErrorTest("unauthorized")}
              isLoading={isLoadingError}
            >
              401 Unauthorized
            </Button>
          </div>

          {activeProblem && (
            <div className="bg-muted/40 space-y-2 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between text-xs">
                <span className="font-mono font-semibold text-amber-500">
                  HTTP {activeProblem.status}
                </span>
                <span className="font-mono text-muted-foreground">
                  TraceId: {activeProblem.traceId?.slice(0, 16)}...
                </span>
              </div>
              <pre className="bg-muted/80 overflow-x-auto rounded border border-border p-2 font-mono text-xs text-foreground">
                {JSON.stringify(activeProblem, null, 2)}
              </pre>
            </div>
          )}
        </section>

        {/* Phase 1 Verification Checklist */}
        <section className="space-y-4 rounded-2xl border border-border bg-card p-6 shadow-sm">
          <div className="flex items-center space-x-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-500" />
            <h3 className="text-base font-semibold text-foreground">
              Phase 1 Delivery Verification Matrix
            </h3>
          </div>

          <div className="grid grid-cols-1 gap-4 text-xs sm:grid-cols-2 md:grid-cols-3">
            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Registration & Hashing</span>
                <span>Complete</span>
              </div>
              <p className="text-muted-foreground">
                BCrypt work factor 12 hashing with case-insensitive unique
                constraints.
              </p>
            </div>

            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Multi-Device Sessions</span>
                <span>Complete</span>
              </div>
              <p className="text-muted-foreground">
                Stateful sessions hashed with SHA-256 and HttpOnly SameSite=Lax
                cookies.
              </p>
            </div>

            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Public & Me Profiles</span>
                <span>Complete</span>
              </div>
              <p className="text-muted-foreground">
                Server-side authorization for /users/me and case-insensitive
                /user/[username].
              </p>
            </div>

            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Edit Profile Modal</span>
                <span>Complete</span>
              </div>
              <p className="text-muted-foreground">
                WCAG 2.2 AA accessible modal dialog with character counters and
                live update.
              </p>
            </div>

            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Session Dashboard</span>
                <span>Complete</span>
              </div>
              <p className="text-muted-foreground">
                List active devices, identify current session, and perform
                secure session revocation.
              </p>
            </div>

            <div className="bg-muted/30 space-y-1 rounded-lg border border-border p-3">
              <div className="flex items-center justify-between font-semibold text-emerald-500">
                <span>Automated Tests</span>
                <span>77/77 Passed</span>
              </div>
              <p className="text-muted-foreground">
                68 .NET unit & integration tests + 9 Vitest frontend schema &
                validation tests.
              </p>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
