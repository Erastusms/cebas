"use client";

import { useState, useEffect } from "react";
import { useQuery, useInfiniteQuery } from "@tanstack/react-query";
import { searchApi } from "../lib/api/search";
import type { SearchPostItem, SearchUserItem, SearchSummaryResult } from "../lib/api/search";
import type { CursorPagination } from "../lib/api/types";

export function useDebounce<T>(value: T, delayMs = 300): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedValue(value);
    }, delayMs);

    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debouncedValue;
}

export function useSearchPosts(query: string, enabled = true) {
  const trimmed = query.trim();

  return useInfiniteQuery<CursorPagination<SearchPostItem>>({
    queryKey: ["search", "posts", trimmed],
    queryFn: async ({ pageParam }) => {
      const response = await searchApi.searchPosts({
        query: trimmed,
        cursor: pageParam as string | null,
        limit: 20,
      });
      return response.data;
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasNextPage ? (lastPage.nextCursor ?? undefined) : undefined,
    enabled: enabled && trimmed.length > 0,
    staleTime: 30 * 1000,
  });
}

export function useSearchUsers(query: string, enabled = true) {
  const trimmed = query.trim();

  return useQuery<SearchUserItem[]>({
    queryKey: ["search", "users", trimmed],
    queryFn: async () => {
      const response = await searchApi.searchUsers({
        query: trimmed,
        limit: 20,
      });
      return response.data;
    },
    enabled: enabled && trimmed.length > 0,
    staleTime: 30 * 1000,
  });
}

export function useSearchAutocomplete(query: string, enabled = true) {
  const debouncedQuery = useDebounce(query.trim(), 300);

  return useQuery<SearchUserItem[]>({
    queryKey: ["search", "autocomplete", debouncedQuery],
    queryFn: async () => {
      const response = await searchApi.searchUsers({
        query: debouncedQuery,
        limit: 5,
      });
      return response.data;
    },
    enabled: enabled && debouncedQuery.length > 0,
    staleTime: 60 * 1000,
  });
}

export function useSearchSummary(query: string, enabled = true) {
  const trimmed = query.trim();

  return useQuery<SearchSummaryResult>({
    queryKey: ["search", "summary", trimmed],
    queryFn: async () => {
      const response = await searchApi.searchSummary({
        query: trimmed,
        postLimit: 5,
        userLimit: 5,
      });
      return response.data;
    },
    enabled: enabled && trimmed.length > 0,
    staleTime: 30 * 1000,
  });
}
