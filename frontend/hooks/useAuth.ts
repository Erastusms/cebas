"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { authApi } from "../lib/api/auth";
import type { User, LoginRequest, RegisterRequest } from "../types/auth";
import { useToast } from "./useToast";

function getStoredUser(): User | null {
  if (typeof window === "undefined") return null;
  try {
    const item = localStorage.getItem("cebas_user");
    return item ? (JSON.parse(item) as User) : null;
  } catch {
    return null;
  }
}

export function useAuth() {
  const queryClient = useQueryClient();
  const router = useRouter();
  const { toast, success } = useToast();

  const {
    data: user,
    isLoading,
    isError,
    refetch: refetchUser,
  } = useQuery<User | null>({
    queryKey: ["currentUser"],
    queryFn: async () => {
      try {
        const response = await authApi.getCurrentUser();
        if (response.data && typeof window !== "undefined") {
          localStorage.setItem("cebas_user", JSON.stringify(response.data));
        }
        return response.data;
      } catch {
        if (typeof window !== "undefined") {
          localStorage.removeItem("cebas_user");
        }
        return null;
      }
    },
    initialData: getStoredUser,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const loginMutation = useMutation({
    mutationFn: async (credentials: LoginRequest) => {
      const res = await authApi.login(credentials);
      return res.data;
    },
    onSuccess: (newUser) => {
      if (typeof window !== "undefined") {
        localStorage.setItem("cebas_user", JSON.stringify(newUser));
      }
      queryClient.setQueryData(["currentUser"], newUser);
      queryClient.invalidateQueries({ queryKey: ["currentUser"] });
      success(`Welcome back, @${newUser.username}!`, "Logged In");
    },
  });

  const registerMutation = useMutation({
    mutationFn: async (data: RegisterRequest) => {
      const res = await authApi.register(data);
      return res.data;
    },
    onSuccess: (newUser) => {
      success(`Account created successfully! Welcome to CEBAS, @${newUser.username}.`, "Registration Complete");
    },
  });

  const logoutMutation = useMutation({
    mutationFn: async () => {
      await authApi.logout();
    },
    onSuccess: () => {
      if (typeof window !== "undefined") {
        localStorage.removeItem("cebas_user");
      }
      queryClient.setQueryData(["currentUser"], null);
      queryClient.clear();
      toast("You have been logged out.", { variant: "info" });
      router.push("/login");
    },
    onError: () => {
      if (typeof window !== "undefined") {
        localStorage.removeItem("cebas_user");
      }
      queryClient.setQueryData(["currentUser"], null);
      queryClient.clear();
      router.push("/login");
    },
  });

  return {
    user: user ?? null,
    isLoading,
    isAuthenticated: !!user,
    isError,
    login: loginMutation.mutateAsync,
    isLoggingIn: loginMutation.isPending,
    loginError: loginMutation.error,
    register: registerMutation.mutateAsync,
    isRegistering: registerMutation.isPending,
    registerError: registerMutation.error,
    logout: logoutMutation.mutateAsync,
    isLoggingOut: logoutMutation.isPending,
    refetchUser,
  };
}
