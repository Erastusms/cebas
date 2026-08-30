"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { usersApi } from "../lib/api/users";
import type { UserProfile, UpdateProfileRequest, User } from "../types/api";
import { useToast } from "./useToast";

export function useProfile(username?: string) {
  const queryClient = useQueryClient();
  const { success } = useToast();

  const profileQuery = useQuery<UserProfile | null>({
    queryKey: ["profile", username?.toLowerCase()],
    queryFn: async () => {
      if (!username) return null;
      const res = await usersApi.getPublicProfile(username);
      return res.data;
    },
    enabled: !!username,
    retry: 1,
  });

  const updateProfileMutation = useMutation({
    mutationFn: async (data: UpdateProfileRequest) => {
      const res = await usersApi.updateProfile(data);
      return res.data;
    },
    onSuccess: (updatedUser: User) => {
      queryClient.setQueryData(["currentUser"], updatedUser);
      queryClient.invalidateQueries({ queryKey: ["currentUser"] });
      queryClient.invalidateQueries({ queryKey: ["profile", updatedUser.username.toLowerCase()] });
      success("Your profile has been updated.", "Profile Saved");
    },
  });

  return {
    profile: profileQuery.data ?? null,
    isLoading: profileQuery.isLoading,
    isError: profileQuery.isError,
    error: profileQuery.error,
    refetchProfile: profileQuery.refetch,
    updateProfile: updateProfileMutation.mutateAsync,
    isUpdating: updateProfileMutation.isPending,
    updateError: updateProfileMutation.error,
  };
}
