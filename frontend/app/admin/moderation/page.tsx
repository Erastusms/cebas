"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Shield,
  ShieldAlert,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Eye,
  EyeOff,
  UserX,
  UserCheck,
  Search,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  Loader2,
  ArrowLeft,
  Calendar,
  Flag,
  Users,
} from "lucide-react";
import { useAuth } from "../../../hooks/useAuth";
import { safetyApi, type ModerationReportItem, type SuspendedUserItem } from "../../../lib/api/safety";
import { useToast } from "../../../hooks/useToast";
import { Button } from "../../../components/ui/button";
import { Skeleton } from "../../../components/ui/skeleton";
import { formatPostTimestamp } from "../../../lib/utils/time";

export default function ModerationPage() {
  const { user, isAuthenticated, isLoading: isAuthLoading } = useAuth();
  const queryClient = useQueryClient();
  const { success, error: showErrorToast } = useToast();

  // Filters and Pagination State
  const [statusFilter, setStatusFilter] = useState<string>("PENDING");
  const [categoryFilter, setCategoryFilter] = useState<string>("");
  const [targetTypeFilter, setTargetTypeFilter] = useState<string>("");
  const [currentPage, setCurrentPage] = useState<number>(1);
  const pageSize = 15;

  // Selected Report for Review Modal
  const [selectedReport, setSelectedReport] = useState<ModerationReportItem | null>(null);

  // Confirmation Modal State for Destructive Actions
  const [confirmActionState, setConfirmActionState] = useState<{
    isOpen: boolean;
    action: "RESOLVE" | "DISMISS" | "HIDE_POST" | "SUSPEND_USER";
    title: string;
    description: string;
    requiresReason: boolean;
  }>({
    isOpen: false,
    action: "RESOLVE",
    title: "",
    description: "",
    requiresReason: false,
  });
  const [actionReason, setActionReason] = useState<string>("");

  const isStaff =
    isAuthenticated &&
    user &&
    (user.role?.toUpperCase() === "MODERATOR" || user.role?.toUpperCase() === "ADMIN");

  // Fetch Reports Query
  const {
    data: reportsData,
    isLoading: isReportsLoading,
    isError,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ["adminReports", statusFilter, categoryFilter, targetTypeFilter, currentPage],
    queryFn: async () => {
      const res = await safetyApi.getAdminReports({
        status: statusFilter === "ALL" ? undefined : statusFilter,
        category: categoryFilter || undefined,
        targetType: targetTypeFilter || undefined,
        page: currentPage,
        pageSize,
      });
      return res.data;
    },
    enabled: !!isStaff,
    refetchInterval: 10000,
    refetchOnWindowFocus: true,
  });

  // Action Mutation
  const actionMutation = useMutation({
    mutationFn: async ({
      reportId,
      action,
      reason,
    }: {
      reportId: string;
      action: string;
      reason?: string;
    }) => {
      const res = await safetyApi.executeModerationAction(reportId, action, reason);
      return res.data;
    },
    onSuccess: (data) => {
      success(data.message || `Action ${data.action} completed successfully.`, "Moderation Action Executed");
      queryClient.invalidateQueries({ queryKey: ["adminReports"] });
      setConfirmActionState((prev) => ({ ...prev, isOpen: false }));
      setActionReason("");
      setSelectedReport(null);
    },
    onError: (err: Error) => {
      showErrorToast(err.message || "Failed to execute moderation action.", "Action Failed");
    },
  });

  // Top-Level Navigation Tab
  const [activeTab, setActiveTab] = useState<"reports" | "suspended">("reports");

  // Suspended Users State
  const [suspendedSearch, setSuspendedSearch] = useState<string>("");
  const [suspendedPage, setSuspendedPage] = useState<number>(1);
  const [selectedSuspendedUser, setSelectedSuspendedUser] = useState<SuspendedUserItem | null>(null);
  const [isUnsuspendModalOpen, setIsUnsuspendModalOpen] = useState<boolean>(false);
  const [unsuspendReason, setUnsuspendReason] = useState<string>("");

  // Suspended Users Query
  const {
    data: suspendedData,
    isLoading: isSuspendedLoading,
    refetch: refetchSuspended,
    isFetching: isFetchingSuspended,
  } = useQuery({
    queryKey: ["adminSuspendedUsers", suspendedSearch, suspendedPage],
    queryFn: async () => {
      const res = await safetyApi.getSuspendedUsers({
        search: suspendedSearch || undefined,
        page: suspendedPage,
        pageSize: 15,
      });
      return res.data;
    },
    enabled: !!isStaff,
  });

  // Unsuspend Mutation
  const unsuspendMutation = useMutation({
    mutationFn: async ({
      userId,
      reason,
    }: {
      userId: string;
      reason?: string;
    }) => {
      const res = await safetyApi.unsuspendUser(userId, reason);
      return res.data;
    },
    onSuccess: (data) => {
      success(data.message || `Pengguna @${data.username} telah diaktifkan kembali.`, "Akun Diaktifkan Kembali");
      queryClient.invalidateQueries({ queryKey: ["adminSuspendedUsers"] });
      queryClient.invalidateQueries({ queryKey: ["adminReports"] });
      setIsUnsuspendModalOpen(false);
      setSelectedSuspendedUser(null);
      setUnsuspendReason("");
    },
    onError: (err: Error) => {
      showErrorToast(err.message || "Gagal mengaktifkan kembali pengguna.", "Aksi Gagal");
    },
  });

  if (isAuthLoading) {
    return (
      <main className="max-w-7xl mx-auto px-4 py-8 space-y-6">
        <Skeleton className="h-10 w-64 rounded-xl" />
        <Skeleton className="h-48 w-full rounded-2xl" />
      </main>
    );
  }

  // Route protection
  if (!isStaff) {
    return (
      <main className="max-w-2xl mx-auto px-4 py-16 text-center space-y-4">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive">
          <ShieldAlert className="h-8 w-8" />
        </div>
        <h1 className="text-xl font-bold text-foreground">Access Restricted</h1>
        <p className="text-sm text-muted-foreground">
          The moderation operations dashboard is restricted to authorized platform moderators and administrators.
        </p>
        <div className="pt-2">
          <Link href="/home">
            <Button variant="outline" size="sm">
              <ArrowLeft className="mr-2 h-4 w-4" /> Return to Home
            </Button>
          </Link>
        </div>
      </main>
    );
  }

  const reports = reportsData?.items || [];
  const totalCount = reportsData?.totalCount || 0;
  const totalPages = reportsData?.totalPages || 1;

  const triggerActionConfirmation = (
    action: "RESOLVE" | "DISMISS" | "HIDE_POST" | "SUSPEND_USER",
    report: ModerationReportItem
  ) => {
    setSelectedReport(report);
    setActionReason("");

    switch (action) {
      case "RESOLVE":
        setConfirmActionState({
          isOpen: true,
          action: "RESOLVE",
          title: "Resolve Report",
          description: "Mark this report as resolved without taking additional content actions.",
          requiresReason: false,
        });
        break;
      case "DISMISS":
        setConfirmActionState({
          isOpen: true,
          action: "DISMISS",
          title: "Dismiss Report",
          description: "Dismiss this report as not violating community standards.",
          requiresReason: false,
        });
        break;
      case "HIDE_POST":
        setConfirmActionState({
          isOpen: true,
          action: "HIDE_POST",
          title: "Hide Violating Post",
          description: "This will remove the post from public timelines and search feeds. The author will be notified.",
          requiresReason: true,
        });
        break;
      case "SUSPEND_USER":
        setConfirmActionState({
          isOpen: true,
          action: "SUSPEND_USER",
          title: "Suspend User Account",
          description: "This will immediately revoke all active user sessions and prevent further posting or interaction.",
          requiresReason: true,
        });
        break;
    }
  };

  const handleConfirmActionSubmit = () => {
    if (!selectedReport) return;
    actionMutation.mutate({
      reportId: selectedReport.id,
      action: confirmActionState.action,
      reason: actionReason.trim() || undefined,
    });
  };

  return (
    <main className="max-w-7xl mx-auto px-4 py-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border pb-6">
        <div className="space-y-1">
          <div className="flex items-center space-x-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-amber-500/10 text-amber-500">
              <Shield className="h-5 w-5" />
            </div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Moderation Operations
            </h1>
          </div>
          <p className="text-xs text-muted-foreground">
            Review user-submitted safety reports, inspect content context, and enforce community standards.
          </p>
        </div>

        <div className="flex items-center space-x-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              if (activeTab === "reports") {
                refetch();
              } else {
                refetchSuspended();
              }
            }}
            disabled={activeTab === "reports" ? isFetching : isFetchingSuspended}
            className="text-xs"
          >
            <RefreshCw
              className={`mr-1.5 h-3.5 w-3.5 ${
                (activeTab === "reports" ? isFetching : isFetchingSuspended) ? "animate-spin" : ""
              }`}
            />
            {activeTab === "reports" ? "Refresh Queue" : "Refresh Pengguna"}
          </Button>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="flex border-b border-border space-x-6 text-sm font-medium">
        <button
          type="button"
          onClick={() => setActiveTab("reports")}
          className={`pb-3 border-b-2 flex items-center space-x-2 transition ${
            activeTab === "reports"
              ? "border-primary text-primary font-bold"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          <Flag className="h-4 w-4" />
          <span>Laporan Konten ({reportsData?.totalCount ?? 0})</span>
        </button>
        <button
          type="button"
          onClick={() => setActiveTab("suspended")}
          className={`pb-3 border-b-2 flex items-center space-x-2 transition ${
            activeTab === "suspended"
              ? "border-primary text-primary font-bold"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          <UserX className="h-4 w-4" />
          <span>Pengguna Ditangguhkan ({suspendedData?.totalCount ?? 0})</span>
        </button>
      </div>

      {activeTab === "reports" ? (
        <>
          {/* Filter Toolbar */}
          <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 p-4 rounded-2xl border border-border bg-card">
        {/* Status Filter */}
        <div className="space-y-1">
          <label className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
            Status
          </label>
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-xs text-foreground focus:ring-1 focus:ring-primary focus:outline-none"
          >
            <option value="PENDING">Pending Review</option>
            <option value="RESOLVED">Resolved</option>
            <option value="DISMISSED">Dismissed</option>
            <option value="ALL">All Statuses</option>
          </select>
        </div>

        {/* Category Filter */}
        <div className="space-y-1">
          <label className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
            Category
          </label>
          <select
            value={categoryFilter}
            onChange={(e) => {
              setCategoryFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-xs text-foreground focus:ring-1 focus:ring-primary focus:outline-none"
          >
            <option value="">All Categories</option>
            <option value="SPAM">Spam</option>
            <option value="HARASSMENT">Harassment</option>
            <option value="HATE_SPEECH">Hate Speech</option>
            <option value="INAPPROPRIATE_CONTENT">Inappropriate Content</option>
          </select>
        </div>

        {/* Target Type Filter */}
        <div className="space-y-1">
          <label className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
            Target Type
          </label>
          <select
            value={targetTypeFilter}
            onChange={(e) => {
              setTargetTypeFilter(e.target.value);
              setCurrentPage(1);
            }}
            className="w-full rounded-xl border border-border bg-background px-3 py-2 text-xs text-foreground focus:ring-1 focus:ring-primary focus:outline-none"
          >
            <option value="">All Targets</option>
            <option value="Post">Post Only</option>
            <option value="User">User Only</option>
          </select>
        </div>

        {/* Summary Counter */}
        <div className="flex flex-col justify-end">
          <div className="rounded-xl bg-muted/50 p-2.5 text-center text-xs">
            <span className="text-muted-foreground">Total Reports: </span>
            <strong className="text-foreground font-bold">{totalCount}</strong>
          </div>
        </div>
      </div>

      {/* Queue List Table / Cards */}
      {isReportsLoading ? (
        <div className="space-y-3">
          {[1, 2, 3, 4].map((i) => (
            <Skeleton key={i} className="h-24 w-full rounded-2xl" />
          ))}
        </div>
      ) : isError ? (
        <div className="rounded-2xl border border-destructive/30 bg-destructive/5 p-8 text-center space-y-3">
          <AlertTriangle className="mx-auto h-8 w-8 text-destructive" />
          <p className="text-sm font-semibold text-destructive">Failed to load moderation queue</p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            Retry
          </Button>
        </div>
      ) : reports.length === 0 ? (
        <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-3">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
            <CheckCircle2 className="h-6 w-6" />
          </div>
          <h3 className="text-base font-bold text-foreground">Queue is clear</h3>
          <p className="text-xs text-muted-foreground max-w-sm mx-auto">
            No reports matching your selected criteria were found.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {reports.map((report) => {
            const isPending = report.status === "PENDING";
            const isPost = report.targetType === "Post";

            return (
              <div
                key={report.id}
                className="rounded-2xl border border-border bg-card p-5 shadow-sm space-y-4 hover:border-primary/30 transition"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-border/60 pb-3">
                  <div className="flex flex-wrap items-center gap-2">
                    {report.reportCount && report.reportCount > 1 && (
                      <span className="inline-flex items-center rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider bg-primary/10 text-primary border border-primary/20">
                        <Flag className="h-3 w-3 mr-1" />
                        Stack: {report.reportCount} Laporan
                      </span>
                    )}

                    {(report.categories && report.categories.length > 0 ? report.categories : [report.category]).map((cat) => (
                      <span
                        key={cat}
                        className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ${
                          cat === "SPAM"
                            ? "bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20"
                            : cat === "HARASSMENT"
                              ? "bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20"
                              : cat === "HATE_SPEECH"
                                ? "bg-red-500/10 text-red-600 dark:text-red-400 border border-red-500/20"
                                : "bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-500/20"
                        }`}
                      >
                        {cat.replace("_", " ")}
                      </span>
                    ))}

                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                        isPending
                          ? "bg-blue-500/10 text-blue-500"
                          : report.status === "RESOLVED"
                            ? "bg-emerald-500/10 text-emerald-500"
                            : "bg-muted text-muted-foreground"
                      }`}
                    >
                      {report.status}
                    </span>

                    <span className="text-xs text-muted-foreground">
                      Target: <strong>{report.targetType}</strong>
                    </span>
                  </div>

                  <div className="flex items-center space-x-2 text-xs text-muted-foreground">
                    <Calendar className="h-3.5 w-3.5" />
                    <span>{formatPostTimestamp(report.createdAt)}</span>
                  </div>
                </div>

                {/* Target context summary */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {/* Reported Target Details */}
                  <div className="rounded-xl border border-border/70 bg-muted/30 p-3 space-y-2 text-xs">
                    <div className="flex items-center justify-between font-semibold text-foreground">
                      <span>Reported Content</span>
                      {report.targetPost?.isHidden && (
                        <span className="text-[10px] font-bold text-destructive bg-destructive/10 px-1.5 py-0.5 rounded">
                          HIDDEN
                        </span>
                      )}
                      {report.targetUser?.isSuspended && (
                        <span className="text-[10px] font-bold text-destructive bg-destructive/10 px-1.5 py-0.5 rounded">
                          SUSPENDED
                        </span>
                      )}
                    </div>

                    {isPost && report.targetPost ? (
                      <div className="space-y-1.5">
                        <p className="text-muted-foreground leading-relaxed line-clamp-3">
                          &ldquo;{report.targetPost.content}&rdquo;
                        </p>
                        <p className="text-[11px] text-muted-foreground">
                          Author: <strong>@{report.targetPost.authorUsername}</strong> ({report.targetPost.authorDisplayName})
                        </p>
                      </div>
                    ) : report.targetUser ? (
                      <div className="space-y-1">
                        <p className="text-foreground font-semibold">
                          @{report.targetUser.username} ({report.targetUser.displayName})
                        </p>
                        <p className="text-[11px] text-muted-foreground">
                          Role: {report.targetUser.role} • Joined: {formatPostTimestamp(report.targetUser.createdAt)}
                        </p>
                      </div>
                    ) : (
                      <p className="text-muted-foreground italic">Target content unavailable or deleted.</p>
                    )}
                  </div>

                  {/* Reporter context & optional description */}
                  <div className="rounded-xl border border-border/70 bg-muted/30 p-3 space-y-1.5 text-xs">
                    <div className="flex items-center justify-between font-semibold text-foreground">
                      <span>Reporter Details</span>
                      {report.reportCount && report.reportCount > 1 && (
                        <span className="text-[10px] font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full">
                          {report.reportCount} Pelapor
                        </span>
                      )}
                    </div>
                    <p className="text-muted-foreground">
                      Dilaporkan oleh: <strong>@{report.reporterUsername}</strong>
                      {report.reportCount && report.reportCount > 1 ? ` dan ${report.reportCount - 1} lainnya` : ""}
                    </p>
                    {report.reason ? (
                      <p className="text-muted-foreground italic line-clamp-2">
                        Alasan: &ldquo;{report.reason}&rdquo;
                      </p>
                    ) : (
                      <p className="text-[11px] text-muted-foreground italic">
                        {report.reportCount && report.reportCount > 1
                          ? "Buka View Report Details untuk melihat seluruh rincian pelapor."
                          : "No additional context provided."}
                      </p>
                    )}
                  </div>
                </div>

                {/* Actions bar */}
                <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border/60">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setSelectedReport(report)}
                    className="text-xs"
                  >
                    <Eye className="mr-1.5 h-3.5 w-3.5" />
                    {report.reportCount && report.reportCount > 1
                      ? `View Report Details (${report.reportCount} Laporan)`
                      : "View Report Details"}
                  </Button>

                  {isPending && (
                    <div className="flex flex-wrap items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => triggerActionConfirmation("DISMISS", report)}
                        className="text-xs"
                      >
                        <XCircle className="mr-1 h-3.5 w-3.5" />
                        Dismiss
                      </Button>

                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => triggerActionConfirmation("RESOLVE", report)}
                        className="text-xs"
                      >
                        <CheckCircle2 className="mr-1 h-3.5 w-3.5 text-emerald-500" />
                        Resolve
                      </Button>

                      {isPost && report.targetPost && !report.targetPost.isHidden && (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => triggerActionConfirmation("HIDE_POST", report)}
                          className="text-xs"
                        >
                          <EyeOff className="mr-1 h-3.5 w-3.5" />
                          Hide Post
                        </Button>
                      )}

                      {(!report.targetUser || !report.targetUser.isSuspended) && (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => triggerActionConfirmation("SUSPEND_USER", report)}
                          className="text-xs"
                        >
                          <UserX className="mr-1 h-3.5 w-3.5" />
                          Suspend User
                        </Button>
                      )}
                    </div>
                  )}
                </div>
              </div>
            );
          })}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between p-4 rounded-2xl border border-border bg-card">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                disabled={currentPage <= 1 || isFetching}
              >
                <ChevronLeft className="h-4 w-4 mr-1" /> Previous
              </Button>
              <span className="text-xs text-muted-foreground">
                Page <strong className="text-foreground">{currentPage}</strong> of{" "}
                <strong className="text-foreground">{totalPages}</strong>
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                disabled={currentPage >= totalPages || isFetching}
              >
                Next <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </div>
      )}
      </>
      ) : (
        /* Suspended Users View */
        <div className="space-y-4">
          {/* Suspended Users Toolbar */}
          <div className="flex flex-col sm:flex-row items-center justify-between gap-3 p-4 rounded-2xl border border-border bg-card">
            <div className="relative w-full sm:w-80">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                type="text"
                value={suspendedSearch}
                onChange={(e) => {
                  setSuspendedSearch(e.target.value);
                  setSuspendedPage(1);
                }}
                placeholder="Cari username atau nama..."
                className="w-full rounded-xl border border-border bg-background pl-9 pr-3 py-2 text-xs text-foreground focus:ring-1 focus:ring-primary focus:outline-none"
              />
            </div>
            <div className="text-xs text-muted-foreground">
              Total Akun Ditangguhkan: <strong className="text-foreground">{suspendedData?.totalCount ?? 0}</strong>
            </div>
          </div>

          {/* Suspended Users List */}
          {isSuspendedLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map((i) => (
                <div key={i} className="p-5 rounded-2xl border border-border bg-card space-y-3">
                  <div className="flex items-center space-x-3">
                    <Skeleton className="h-10 w-10 rounded-full" />
                    <div className="space-y-1">
                      <Skeleton className="h-4 w-32" />
                      <Skeleton className="h-3 w-24" />
                    </div>
                  </div>
                  <Skeleton className="h-12 w-full rounded-xl" />
                </div>
              ))}
            </div>
          ) : !suspendedData || suspendedData.items.length === 0 ? (
            <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-3">
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-500">
                <CheckCircle2 className="h-6 w-6" />
              </div>
              <h3 className="text-base font-bold text-foreground">Tidak Ada Pengguna Ditangguhkan</h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                Saat ini tidak ada akun yang sedang dalam status penangguhan.
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {suspendedData.items.map((suspUser) => (
                <div
                  key={suspUser.id}
                  className="rounded-2xl border border-border bg-card p-5 shadow-sm space-y-4 hover:border-primary/30 transition"
                >
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-border/60 pb-3">
                    <div className="flex items-center space-x-3">
                      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-destructive/10 text-destructive font-bold text-sm overflow-hidden">
                        {suspUser.avatarUrl ? (
                          <img
                            src={suspUser.avatarUrl}
                            alt={suspUser.displayName || suspUser.username}
                            className="h-full w-full object-cover"
                          />
                        ) : (
                          suspUser.displayName?.charAt(0).toUpperCase() ||
                          suspUser.username?.charAt(0).toUpperCase()
                        )}
                      </div>
                      <div>
                        <div className="flex items-center space-x-2">
                          <span className="text-sm font-bold text-foreground">{suspUser.displayName}</span>
                          <span className="text-xs text-muted-foreground">@{suspUser.username}</span>
                          <span className="inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider bg-destructive/10 text-destructive border border-destructive/20">
                            Suspended
                          </span>
                        </div>
                        <div className="flex items-center space-x-2 text-xs text-muted-foreground mt-0.5">
                          <span>Role: {suspUser.role}</span>
                          <span>•</span>
                          <span>{suspUser.totalPosts} Posts</span>
                          {suspUser.suspendedAt && (
                            <>
                              <span>•</span>
                              <span>Ditangguhkan {formatPostTimestamp(suspUser.suspendedAt)}</span>
                            </>
                          )}
                        </div>
                      </div>
                    </div>

                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        setSelectedSuspendedUser(suspUser);
                        setUnsuspendReason("");
                        setIsUnsuspendModalOpen(true);
                      }}
                      className="text-xs text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 hover:bg-emerald-500/10 border-emerald-500/30"
                    >
                      <UserCheck className="mr-1.5 h-3.5 w-3.5" />
                      Aktifkan Kembali
                    </Button>
                  </div>

                  <div className="text-xs text-muted-foreground bg-muted/30 p-3 rounded-xl">
                    <strong className="text-foreground">Alasan Penangguhan: </strong>
                    <span>{suspUser.suspensionReason || "Tidak ada alasan spesifik."}</span>
                  </div>
                </div>
              ))}

              {/* Suspended Pagination */}
              {suspendedData.totalPages > 1 && (
                <div className="flex items-center justify-between p-4 rounded-2xl border border-border bg-card">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setSuspendedPage((p) => Math.max(1, p - 1))}
                    disabled={suspendedPage <= 1 || isFetchingSuspended}
                  >
                    <ChevronLeft className="h-4 w-4 mr-1" /> Previous
                  </Button>
                  <span className="text-xs text-muted-foreground">
                    Page <strong className="text-foreground">{suspendedPage}</strong> of{" "}
                    <strong className="text-foreground">{suspendedData.totalPages}</strong>
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setSuspendedPage((p) => Math.min(suspendedData.totalPages, p + 1))}
                    disabled={suspendedPage >= suspendedData.totalPages || isFetchingSuspended}
                  >
                    Next <ChevronRight className="h-4 w-4 ml-1" />
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Review Detail Modal */}
      {selectedReport && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm animate-in fade-in duration-200"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-2xl max-h-[85vh] overflow-y-auto rounded-2xl border border-border bg-card p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between border-b border-border pb-3">
              <h2 className="text-base font-bold text-foreground flex items-center space-x-2">
                <Flag className="h-4 w-4 text-primary" />
                <span>
                  View Report Details: {selectedReport.targetType === "Post" ? "Postingan" : "Pengguna"}
                  {selectedReport.reportCount && selectedReport.reportCount > 1
                    ? ` (${selectedReport.reportCount} Laporan)`
                    : ""}
                </span>
              </h2>
              <button
                type="button"
                onClick={() => setSelectedReport(null)}
                className="p-1 text-muted-foreground hover:text-foreground rounded-lg"
              >
                ✕
              </button>
            </div>

            <div className="space-y-4 text-xs">
              {/* Target Content Details */}
              {selectedReport.targetPost && (
                <div className="p-4 rounded-xl border border-border bg-background space-y-2">
                  <div className="flex items-center justify-between">
                    <p className="font-semibold text-foreground">
                      Postingan oleh @{selectedReport.targetPost.authorUsername} ({selectedReport.targetPost.authorDisplayName})
                    </p>
                    <div className="flex items-center space-x-2 text-[11px] text-muted-foreground">
                      {selectedReport.targetPost.isHidden && (
                        <span className="text-[10px] font-bold text-destructive bg-destructive/10 px-1.5 py-0.5 rounded">
                          HIDDEN
                        </span>
                      )}
                      <span>{formatPostTimestamp(selectedReport.targetPost.createdAt)}</span>
                    </div>
                  </div>
                  <p className="text-sm leading-relaxed text-foreground whitespace-pre-wrap">
                    {selectedReport.targetPost.content}
                  </p>
                  {selectedReport.targetPost.mediaUrls.length > 0 && (
                    <div className="grid grid-cols-2 gap-2 pt-2">
                      {selectedReport.targetPost.mediaUrls.map((url, idx) => (
                        <img
                          key={idx}
                          src={url}
                          alt="Attached media"
                          className="rounded-lg object-cover max-h-40 w-full"
                        />
                      ))}
                    </div>
                  )}
                </div>
              )}

              {selectedReport.targetUser && (
                <div className="p-4 rounded-xl border border-border bg-background space-y-1.5">
                  <div className="flex items-center justify-between">
                    <p className="font-semibold text-foreground">
                      Profil Pengguna: @{selectedReport.targetUser.username}
                    </p>
                    {selectedReport.targetUser.isSuspended && (
                      <span className="text-[10px] font-bold text-destructive bg-destructive/10 px-1.5 py-0.5 rounded">
                        SUSPENDED
                      </span>
                    )}
                  </div>
                  <p className="text-muted-foreground">
                    Nama Tampilan: {selectedReport.targetUser.displayName}
                  </p>
                  <p className="text-muted-foreground">
                    Role: {selectedReport.targetUser.role} • Terdaftar:{" "}
                    {formatPostTimestamp(selectedReport.targetUser.createdAt)}
                  </p>
                </div>
              )}

              {/* All Associated Reporters List */}
              <div className="space-y-2.5">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-foreground flex items-center space-x-1.5">
                    <Users className="h-3.5 w-3.5 text-primary" />
                    <span>Daftar Pelapor ({selectedReport.reports?.length ?? selectedReport.reportCount ?? 1} Laporan)</span>
                  </h3>
                  <span className="text-[11px] text-muted-foreground">
                    Status Utama: <strong className="text-foreground">{selectedReport.status}</strong>
                  </span>
                </div>

                <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                  {(selectedReport.reports && selectedReport.reports.length > 0
                    ? selectedReport.reports
                    : [
                        {
                          id: selectedReport.id,
                          reporterUserId: selectedReport.reporterUserId,
                          reporterUsername: selectedReport.reporterUsername,
                          reporterDisplayName: selectedReport.reporterDisplayName,
                          reporterAvatarUrl: selectedReport.reporterAvatarUrl,
                          category: selectedReport.category,
                          status: selectedReport.status,
                          reason: selectedReport.reason,
                          createdAt: selectedReport.createdAt,
                          resolvedAt: selectedReport.resolvedAt,
                          resolvedByUserId: selectedReport.resolvedByUserId,
                        },
                      ]
                  ).map((r, idx) => (
                    <div
                      key={r.id || idx}
                      className="p-3 rounded-xl bg-muted/40 border border-border space-y-2"
                    >
                      <div className="flex items-center justify-between flex-wrap gap-2">
                        <div className="flex items-center space-x-2">
                          <div className="flex h-6 w-6 items-center justify-center rounded-full bg-primary text-[10px] font-bold text-primary-foreground overflow-hidden flex-shrink-0">
                            {r.reporterAvatarUrl ? (
                              <img
                                src={r.reporterAvatarUrl}
                                alt={r.reporterUsername}
                                className="h-full w-full object-cover"
                              />
                            ) : (
                              r.reporterUsername?.charAt(0).toUpperCase()
                            )}
                          </div>
                          <div>
                            <span className="font-semibold text-foreground">@{r.reporterUsername}</span>
                            <span className="text-[11px] text-muted-foreground ml-1.5">
                              ({r.reporterDisplayName})
                            </span>
                          </div>
                        </div>

                        <div className="flex items-center space-x-2">
                          <span
                            className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider ${
                              r.category === "SPAM"
                                ? "bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20"
                                : r.category === "HARASSMENT"
                                  ? "bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20"
                                  : r.category === "HATE_SPEECH"
                                    ? "bg-red-500/10 text-red-600 dark:text-red-400 border border-red-500/20"
                                    : "bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-500/20"
                            }`}
                          >
                            {r.category.replace("_", " ")}
                          </span>

                          <span
                            className={`inline-flex items-center rounded-full px-1.5 py-0.5 text-[9px] font-semibold ${
                              r.status === "PENDING"
                                ? "bg-blue-500/10 text-blue-500"
                                : r.status === "RESOLVED"
                                  ? "bg-emerald-500/10 text-emerald-500"
                                  : "bg-muted text-muted-foreground"
                            }`}
                          >
                            {r.status}
                          </span>

                          <span className="text-[10px] text-muted-foreground">
                            {formatPostTimestamp(r.createdAt)}
                          </span>
                        </div>
                      </div>

                      {r.reason ? (
                        <p className="text-muted-foreground italic pl-8 whitespace-pre-wrap leading-relaxed">
                          &ldquo;{r.reason}&rdquo;
                        </p>
                      ) : (
                        <p className="text-[11px] text-muted-foreground italic pl-8">
                          Tidak ada keterangan tambahan dari pelapor.
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 pt-3 border-t border-border">
              {selectedReport.status === "PENDING" ? (
                <div className="flex flex-wrap items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      triggerActionConfirmation("DISMISS", selectedReport);
                    }}
                    className="text-xs"
                  >
                    <XCircle className="mr-1 h-3.5 w-3.5" />
                    Dismiss All
                  </Button>

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      triggerActionConfirmation("RESOLVE", selectedReport);
                    }}
                    className="text-xs"
                  >
                    <CheckCircle2 className="mr-1 h-3.5 w-3.5 text-emerald-500" />
                    Resolve All
                  </Button>

                  {selectedReport.targetType === "Post" && selectedReport.targetPost && !selectedReport.targetPost.isHidden && (
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => {
                        triggerActionConfirmation("HIDE_POST", selectedReport);
                      }}
                      className="text-xs"
                    >
                      <EyeOff className="mr-1 h-3.5 w-3.5" />
                      Hide Post
                    </Button>
                  )}

                  {(!selectedReport.targetUser || !selectedReport.targetUser.isSuspended) && (
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => {
                        triggerActionConfirmation("SUSPEND_USER", selectedReport);
                      }}
                      className="text-xs"
                    >
                      <UserX className="mr-1 h-3.5 w-3.5" />
                      Suspend User
                    </Button>
                  )}
                </div>
              ) : <div />}

              <Button variant="outline" size="sm" onClick={() => setSelectedReport(null)}>
                Close
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Confirmation Modal for Destructive Moderation Action */}
      {confirmActionState.isOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm animate-in fade-in duration-200"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-2xl space-y-4">
            <div className="flex items-center space-x-2.5 text-foreground">
              <AlertTriangle className="h-5 w-5 text-amber-500" />
              <h3 className="text-base font-bold">{confirmActionState.title}</h3>
            </div>

            <p className="text-xs text-muted-foreground leading-relaxed">
              {confirmActionState.description}
            </p>

            {confirmActionState.requiresReason && (
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-foreground">
                  Reason for Action <span className="text-destructive">*</span>
                </label>
                <textarea
                  rows={3}
                  maxLength={1000}
                  value={actionReason}
                  onChange={(e) => setActionReason(e.target.value)}
                  placeholder="State the rationale for audit logging..."
                  className="w-full rounded-xl border border-border bg-background p-2.5 text-xs focus:ring-1 focus:ring-primary focus:outline-none"
                />
              </div>
            )}

            <div className="flex justify-end space-x-2 pt-2 border-t border-border">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setConfirmActionState((prev) => ({ ...prev, isOpen: false }))}
                disabled={actionMutation.isPending}
              >
                Cancel
              </Button>
              <Button
                variant={
                  confirmActionState.action === "HIDE_POST" ||
                  confirmActionState.action === "SUSPEND_USER"
                    ? "destructive"
                    : "default"
                }
                size="sm"
                onClick={handleConfirmActionSubmit}
                disabled={
                  (confirmActionState.requiresReason && !actionReason.trim()) ||
                  actionMutation.isPending
                }
              >
                {actionMutation.isPending ? (
                  <>
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    Executing...
                  </>
                ) : (
                  "Confirm Action"
                )}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Confirmation Modal for Unsuspend User */}
      {isUnsuspendModalOpen && selectedSuspendedUser && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm animate-in fade-in duration-200"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-md rounded-2xl border border-border bg-card p-6 shadow-2xl space-y-4">
            <div className="flex items-center space-x-2.5 text-foreground">
              <UserCheck className="h-5 w-5 text-emerald-500" />
              <h3 className="text-base font-bold">Aktifkan Kembali Pengguna</h3>
            </div>

            <p className="text-xs text-muted-foreground leading-relaxed">
              Anda akan mengaktifkan kembali akun <strong>@{selectedSuspendedUser.username}</strong> ({selectedSuspendedUser.displayName}). Seluruh postingan pengguna ini akan dipulihkan dan dapat dilihat kembali oleh publik.
            </p>

            <div className="space-y-1.5">
              <label className="text-xs font-semibold text-foreground">
                Alasan / Catatan Pemulihan (Opsional)
              </label>
              <textarea
                rows={3}
                maxLength={1000}
                value={unsuspendReason}
                onChange={(e) => setUnsuspendReason(e.target.value)}
                placeholder="Tulis alasan pemulihan akun untuk catatan audit log..."
                className="w-full rounded-xl border border-border bg-background p-2.5 text-xs focus:ring-1 focus:ring-primary focus:outline-none"
              />
            </div>

            <div className="flex justify-end space-x-2 pt-2 border-t border-border">
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setIsUnsuspendModalOpen(false);
                  setSelectedSuspendedUser(null);
                }}
                disabled={unsuspendMutation.isPending}
              >
                Batal
              </Button>
              <Button
                variant="default"
                size="sm"
                onClick={() => {
                  unsuspendMutation.mutate({
                    userId: selectedSuspendedUser.id,
                    reason: unsuspendReason.trim() || undefined,
                  });
                }}
                disabled={unsuspendMutation.isPending}
                className="bg-emerald-600 hover:bg-emerald-700 text-white"
              >
                {unsuspendMutation.isPending ? (
                  <>
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    Memproses...
                  </>
                ) : (
                  "Aktifkan Akun"
                )}
              </Button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
