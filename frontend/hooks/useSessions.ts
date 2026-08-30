"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { usersApi } from "../lib/api/users";
import type { SessionItem } from "../types/user";
import { useToast } from "./useToast";

export function useSessions() {
  const queryClient = useQueryClient();
  const router = useRouter();
  const { success, toast } = useToast();

  const sessionsQuery = useQuery<SessionItem[]>({
    queryKey: ["sessions"],
    queryFn: async () => {
      const res = await usersApi.getSessions();
      return res.data;
    },
    retry: 1,
  });

  const revokeSessionMutation = useMutation({
    mutationFn: async ({ sessionId, isCurrent }: { sessionId: string; isCurrent: boolean }) => {
      await usersApi.revokeSession(sessionId);
      return { sessionId, isCurrent };
    },
    onSuccess: ({ isCurrent }) => {
      if (isCurrent) {
        queryClient.setQueryData(["currentUser"], null);
        queryClient.clear();
        toast("Current session revoked. Please log in again.", { variant: "info" });
        router.push("/login");
      } else {
        queryClient.invalidateQueries({ queryKey: ["sessions"] });
        success("Session revoked successfully.", "Session Terminated");
      }
    },
  });

  return {
    sessions: sessionsQuery.data ?? [],
    isLoading: sessionsQuery.isLoading,
    isError: sessionsQuery.isError,
    error: sessionsQuery.error,
    refetchSessions: sessionsQuery.refetch,
    revokeSession: revokeSessionMutation.mutateAsync,
    isRevoking: revokeSessionMutation.isPending,
  };
}
