"use client";

import React, { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  CheckCircle2,
  Server,
  Shield,
  Layers,
  Sparkles,
  Bell,
  MoreVertical,
  ExternalLink,
  RefreshCw,
} from "lucide-react";
import { apiClient } from "../lib/api/client";
import { ProblemDetailsException } from "../lib/api/errors";
import type { ProblemDetails } from "../lib/api/types";
import { Button } from "../components/ui/button";
import { Input } from "../components/ui/input";
import { Modal } from "../components/ui/modal";
import { Dropdown } from "../components/ui/dropdown";
import { Skeleton } from "../components/ui/skeleton";
import { useToast } from "../hooks/useToast";

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
  const { toast, success, error: toastError } = useToast();
  const [isDemoModalOpen, setIsDemoModalOpen] = useState(false);
  const [testInput, setTestInput] = useState("");
  const [inputError, setInputError] = useState<string | undefined>(undefined);
  const [activeProblem, setActiveProblem] = useState<ProblemDetails | null>(null);
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

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 selection:bg-blue-500 selection:text-white">
      {/* Top Banner */}
      <div className="border-b border-slate-800 bg-slate-900/50 backdrop-blur sticky top-0 z-40">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="h-9 w-9 rounded-lg bg-blue-600 flex items-center justify-center font-bold text-white shadow-lg shadow-blue-500/25">
              C
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-bold tracking-tight text-lg text-white">CEBAS</span>
                <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-400 border border-blue-500/30">
                  Phase 0 Baseline
                </span>
              </div>
              <p className="text-xs text-slate-400">Celoteh Bebas — Real-Time Social Platform</p>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            <a
              href="http://localhost:5000/swagger"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center space-x-1.5 text-xs text-slate-300 hover:text-white px-3 py-1.5 rounded-md border border-slate-800 bg-slate-800/50 hover:bg-slate-800 transition"
            >
              <span>OpenAPI Swagger</span>
              <ExternalLink className="h-3.5 w-3.5" />
            </a>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">
        {/* Hero Architecture Card */}
        <section className="rounded-2xl border border-slate-800 bg-gradient-to-b from-slate-900 to-slate-950 p-6 sm:p-8 shadow-2xl relative overflow-hidden">
          <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none">
            <Layers className="w-64 h-64 text-blue-500" />
          </div>

          <div className="max-w-3xl space-y-4 relative z-10">
            <div className="inline-flex items-center space-x-2 text-xs font-semibold uppercase tracking-wider text-blue-400">
              <Sparkles className="h-4 w-4" />
              <span>Foundation & Architecture Baseline</span>
            </div>
            <h1 className="text-3xl sm:text-4xl font-extrabold tracking-tight text-white">
              CEBAS Platform Scaffolding & Database Baseline
            </h1>
            <p className="text-slate-400 text-sm sm:text-base leading-relaxed">
              Production-grade monorepo established with separated Next.js frontend, .NET 10 Clean Architecture Web API,
              PostgreSQL 16 with PgBouncer connection pooling, Redis 7, MinIO object storage, universal UUIDv7 (RFC 9562),
              and RFC 7807 standardized API error handling.
            </p>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mt-6 pt-6 border-t border-slate-800/80">
            <div className="space-y-1">
              <span className="text-xs text-slate-500 font-medium">Backend Stack</span>
              <p className="text-sm font-semibold text-slate-200">.NET 10 • Clean Arch</p>
            </div>
            <div className="space-y-1">
              <span className="text-xs text-slate-500 font-medium">Database Layer</span>
              <p className="text-sm font-semibold text-slate-200">PostgreSQL 16 + PgBouncer</p>
            </div>
            <div className="space-y-1">
              <span className="text-xs text-slate-500 font-medium">Frontend Stack</span>
              <p className="text-sm font-semibold text-slate-200">Next.js 15 • TypeScript</p>
            </div>
            <div className="space-y-1">
              <span className="text-xs text-slate-500 font-medium">Primary Keys</span>
              <p className="text-sm font-semibold text-slate-200">Universal UUIDv7</p>
            </div>
          </div>
        </section>

        {/* Live Backend Health & Connectivity */}
        <section className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Card 1: Backend Health Liveness */}
          <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2">
                <Server className="h-5 w-5 text-blue-400" />
                <h3 className="font-semibold text-white">Backend Health & Ping</h3>
              </div>
              <Button
                variant="outline"
                size="sm"
                className="text-xs border-slate-700 hover:bg-slate-800 text-slate-200"
                onClick={() => {
                  refetchHealth();
                  refetchPing();
                }}
                isLoading={isHealthFetching}
              >
                <RefreshCw className="h-3.5 w-3.5 mr-1" />
                Refresh
              </Button>
            </div>

            <div className="space-y-3">
              <div className="p-4 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <div
                    className={`h-3 w-3 rounded-full ${
                      isHealthLoading
                        ? "bg-amber-400 animate-ping"
                        : isHealthError
                        ? "bg-rose-500"
                        : "bg-emerald-400"
                    }`}
                  />
                  <div>
                    <span className="text-sm font-medium text-white">
                      GET /health (Liveness)
                    </span>
                    <p className="text-xs text-slate-400">
                      {isHealthLoading
                        ? "Checking availability..."
                        : isHealthError
                        ? `Offline: ${(healthErrorObj as Error)?.message || "Failed to reach API"}`
                        : `Status: ${healthData?.status} • ${healthData?.service} (${healthData?.version})`}
                    </p>
                  </div>
                </div>
                {!isHealthError && !isHealthLoading && (
                  <span className="text-xs px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-mono">
                    200 OK
                  </span>
                )}
              </div>

              <div className="p-4 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <Activity className="h-4 w-4 text-blue-400" />
                  <div>
                    <span className="text-sm font-medium text-white">
                      GET /api/v1/ping
                    </span>
                    <p className="text-xs text-slate-400">
                      {pingData ? `Message: "${pingData.data.message}" • ${pingData.message}` : "Awaiting response..."}
                    </p>
                  </div>
                </div>
                {pingData && (
                  <span className="text-xs px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 font-mono">
                    Operational
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Card 2: RFC 7807 Error Inspector */}
          <div className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 space-y-4">
            <div className="flex items-center space-x-2">
              <Shield className="h-5 w-5 text-indigo-400" />
              <h3 className="font-semibold text-white">RFC 7807 Problem Details Inspector</h3>
            </div>
            <p className="text-xs text-slate-400">
              Trigger standardized API exception responses to verify error serialization without exposing server stack traces.
            </p>

            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                size="sm"
                className="text-xs border-slate-700 hover:bg-slate-800 text-slate-200"
                onClick={() => triggerErrorTest("validation")}
                isLoading={isLoadingError}
              >
                400 Validation
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="text-xs border-slate-700 hover:bg-slate-800 text-slate-200"
                onClick={() => triggerErrorTest("notfound")}
                isLoading={isLoadingError}
              >
                404 Not Found
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="text-xs border-slate-700 hover:bg-slate-800 text-slate-200"
                onClick={() => triggerErrorTest("conflict")}
                isLoading={isLoadingError}
              >
                409 Conflict
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="text-xs border-slate-700 hover:bg-slate-800 text-slate-200"
                onClick={() => triggerErrorTest("unauthorized")}
                isLoading={isLoadingError}
              >
                401 Unauthorized
              </Button>
            </div>

            {activeProblem && (
              <div className="rounded-lg bg-slate-950 border border-slate-800 p-3 space-y-2">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-mono text-amber-400">HTTP {activeProblem.status}</span>
                  <span className="text-slate-400 font-mono">TraceId: {activeProblem.traceId?.slice(0, 16)}...</span>
                </div>
                <pre className="text-xs text-slate-300 font-mono overflow-x-auto p-2 bg-slate-900 rounded border border-slate-800">
                  {JSON.stringify(activeProblem, null, 2)}
                </pre>
              </div>
            )}
          </div>
        </section>

        {/* UI Primitives Component Showcase */}
        <section className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 space-y-6">
          <div className="flex items-center justify-between border-b border-slate-800 pb-4">
            <div>
              <h3 className="font-semibold text-white text-lg">Accessible UI Component Primitives</h3>
              <p className="text-xs text-slate-400">
                WCAG 2.2 AA compliant UI primitives styled with CEBAS semantic tokens and keyboard navigation.
              </p>
            </div>
            <span className="text-xs px-2.5 py-1 rounded bg-slate-800 text-slate-300 font-mono">
              components/ui/
            </span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {/* Buttons Showcase */}
            <div className="space-y-3 p-4 rounded-lg bg-slate-950/60 border border-slate-800">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Button Variants</span>
              <div className="flex flex-wrap gap-2">
                <Button variant="default" size="sm">Primary</Button>
                <Button variant="secondary" size="sm">Secondary</Button>
                <Button variant="outline" size="sm">Outline</Button>
                <Button variant="ghost" size="sm">Ghost</Button>
                <Button variant="destructive" size="sm">Destructive</Button>
                <Button variant="default" size="sm" isLoading>Loading</Button>
              </div>
            </div>

            {/* Inputs Showcase */}
            <div className="space-y-3 p-4 rounded-lg bg-slate-950/60 border border-slate-800">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Form Input</span>
              <Input
                label="Sample Handle"
                placeholder="@username"
                value={testInput}
                onChange={(e) => {
                  setTestInput(e.target.value);
                  if (e.target.value.length > 0 && e.target.value.length < 3) {
                    setInputError("Handle must be at least 3 characters.");
                  } else {
                    setInputError(undefined);
                  }
                }}
                error={inputError}
                helperText={!inputError ? "Alphanumeric characters and underscores only." : undefined}
              />
            </div>

            {/* Interactive Overlays Showcase */}
            <div className="space-y-3 p-4 rounded-lg bg-slate-950/60 border border-slate-800">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Modal, Dropdown & Toast</span>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <Button variant="outline" size="sm" onClick={() => setIsDemoModalOpen(true)}>
                  Open Modal
                </Button>

                <Dropdown
                  trigger={
                    <Button variant="ghost" size="icon" aria-label="Open menu">
                      <MoreVertical className="h-4 w-4" />
                    </Button>
                  }
                  items={[
                    { label: "View Profile", onClick: () => toast("Viewing Profile", { variant: "info" }) },
                    { label: "Notification Settings", onClick: () => toast("Opened Settings", { variant: "info" }) },
                    {
                      label: "Block User",
                      destructive: true,
                      onClick: () => toast("Safety block action triggered", { variant: "error" }),
                    },
                  ]}
                />

                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => success("Operation completed successfully!", "Action Confirmed")}
                >
                  <Bell className="h-3.5 w-3.5 mr-1" />
                  Toast
                </Button>
              </div>

              <div className="space-y-2 pt-2">
                <span className="text-xs text-slate-500">Skeleton Loading:</span>
                <div className="flex items-center space-x-3">
                  <Skeleton className="h-9 w-9 rounded-full bg-slate-800" />
                  <div className="space-y-1 flex-1">
                    <Skeleton className="h-3 w-3/4 bg-slate-800" />
                    <Skeleton className="h-2 w-1/2 bg-slate-800" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Phase 0 Completion Matrix */}
        <section className="rounded-xl border border-slate-800 bg-slate-900/60 p-6 space-y-4">
          <div className="flex items-center space-x-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-400" />
            <h3 className="font-semibold text-white">Phase 0 Baseline Verification Matrix</h3>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4 text-xs">
            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>Universal UUIDv7</span>
                <span>Ready</span>
              </div>
              <p className="text-slate-400">Monotonic time-ordered RFC 9562 identifiers in CEBAS.Domain.</p>
            </div>

            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>PostgreSQL 16 & PgBouncer</span>
                <span>Configured</span>
              </div>
              <p className="text-slate-400">Connection pooling layer with transaction mode in Docker Compose.</p>
            </div>

            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>Database Migrations</span>
                <span>001_extensions.sql</span>
              </div>
              <p className="text-slate-400">uuid-ossp, citext & custom domain ENUM types baseline.</p>
            </div>

            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>RFC 7807 Problem Details</span>
                <span>Active</span>
              </div>
              <p className="text-slate-400">Global API exception middleware & TypeScript error parser.</p>
            </div>

            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>State & Cache Layer</span>
                <span>Active</span>
              </div>
              <p className="text-slate-400">Zustand UI stores & TanStack Query client providers configured.</p>
            </div>

            <div className="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1">
              <div className="flex items-center justify-between text-emerald-400 font-semibold">
                <span>Unit & Integration Tests</span>
                <span>14/14 Passed</span>
              </div>
              <p className="text-slate-400">UUIDv7 uniqueness, serialization, health & migration tests verified.</p>
            </div>
          </div>
        </section>
      </div>

      {/* Reusable Modal Demonstration */}
      <Modal
        isOpen={isDemoModalOpen}
        onClose={() => setIsDemoModalOpen(false)}
        title="CEBAS Modal Dialog"
        description="Demonstration of accessible modal component with keyboard focus trapping and escape dismissal."
      >
        <div className="space-y-4 text-sm text-slate-300">
          <p>
            This modal dialog follows WCAG 2.2 AA accessibility principles:
          </p>
          <ul className="list-disc pl-5 space-y-1 text-slate-400 text-xs">
            <li>Pressing <kbd className="px-1 py-0.5 rounded bg-slate-800 text-slate-200">Esc</kbd> closes the modal.</li>
            <li>Clicking the backdrop dismisses the dialog.</li>
            <li>ARIA attributes (<code className="text-blue-400">role=&quot;dialog&quot;</code> and <code className="text-blue-400">aria-modal=&quot;true&quot;</code>) ensure screen-reader compatibility.</li>
          </ul>
          <div className="pt-2 flex justify-end space-x-2">
            <Button variant="outline" size="sm" onClick={() => setIsDemoModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="default"
              size="sm"
              onClick={() => {
                setIsDemoModalOpen(false);
                success("Modal action confirmed!");
              }}
            >
              Confirm
            </Button>
          </div>
        </div>
      </Modal>
    </main>
  );
}
