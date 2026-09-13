import { apiClient } from "./client";
import type { ApiResponse } from "./types";

export type TrendingTopic = {
  rank: number;
  tag: string;
  score: number;
  postCount: number;
};

export type TrendingTopicsResponse = {
  items: TrendingTopic[];
};

export const trendsApi = {
  /**
   * Retrieves top trending topics calculated via rolling 24-hour time-decay algorithm.
   */
  getTrends: async (limit: number = 10): Promise<ApiResponse<TrendingTopicsResponse>> => {
    return apiClient.get<ApiResponse<TrendingTopicsResponse>>(`/api/v1/trends?limit=${limit}`);
  },
};
