import {
  ApiException,
  NetworkException,
  ProblemDetailsException,
  RateLimitException,
} from "./errors";
import type { ProblemDetails, RequestOptions } from "./types";
import { useRateLimitStore } from "../../stores/useRateLimitStore";


export class ApiClient {
  private readonly baseUrl: string;
  private readonly defaultTimeoutMs: number;

  constructor(baseUrl?: string, defaultTimeoutMs = 15000) {
    this.baseUrl = (
      baseUrl ??
      process.env.NEXT_PUBLIC_API_BASE_URL ??
      "http://localhost:5226"
    ).replace(/\/$/, "");
    this.defaultTimeoutMs = defaultTimeoutMs;
  }

  public async get<T>(path: string, options?: RequestOptions): Promise<T> {
    return this.request<T>("GET", path, undefined, options);
  }

  public async post<T, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions
  ): Promise<T> {
    return this.request<T>("POST", path, body, options);
  }

  public async put<T, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions
  ): Promise<T> {
    return this.request<T>("PUT", path, body, options);
  }

  public async patch<T, B = unknown>(
    path: string,
    body?: B,
    options?: RequestOptions
  ): Promise<T> {
    return this.request<T>("PATCH", path, body, options);
  }

  public async delete<T>(path: string, options?: RequestOptions): Promise<T> {
    return this.request<T>("DELETE", path, undefined, options);
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    options: RequestOptions = {}
  ): Promise<T> {
    const url = this.buildUrl(path, options.params);
    const headers: Record<string, string> = {
      Accept: "application/json, application/problem+json",
      ...options.headers,
    };

    if (body !== undefined && !(body instanceof FormData)) {
      headers["Content-Type"] = "application/json";
    }

    const controller = new AbortController();
    const timeoutId = setTimeout(
      () => controller.abort(),
      options.timeoutMs ?? this.defaultTimeoutMs
    );

    try {
      const response = await fetch(url, {
        method,
        headers,
        body:
          body !== undefined
            ? body instanceof FormData
              ? body
              : JSON.stringify(body)
            : undefined,
        signal: options.signal ?? controller.signal,
        credentials: "include",
      });

      if (!response.ok) {
        await this.handleErrorResponse(response);
      }

      if (response.status === 204) {
        return null as unknown as T;
      }

      const contentType = response.headers.get("content-type");
      if (contentType && contentType.includes("application/json")) {
        return (await response.json()) as T;
      }

      return (await response.text()) as unknown as T;
    } catch (err: unknown) {
      if (
        err instanceof ApiException ||
        err instanceof ProblemDetailsException
      ) {
        throw err;
      }

      if (err instanceof DOMException && err.name === "AbortError") {
        throw new NetworkException(
          "Request timed out. Please check your network connection."
        );
      }

      if (err instanceof Error) {
        throw new NetworkException(err.message);
      }

      throw new NetworkException();
    } finally {
      clearTimeout(timeoutId);
    }
  }

  private buildUrl(
    path: string,
    params?: Record<string, string | number | boolean | undefined | null>
  ): string {
    const cleanPath = path.startsWith("/") ? path : `/${path}`;
    const base = this.baseUrl || (typeof window !== "undefined" ? window.location.origin : "http://localhost:5226");
    const url = new URL(cleanPath, base);

    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          url.searchParams.append(key, String(value));
        }
      });
    }

    return url.toString();
  }

  private async handleErrorResponse(response: Response): Promise<never> {
    const contentType = response.headers.get("content-type");
    let errorJson: ProblemDetails | null = null;

    if (
      contentType &&
      (contentType.includes("application/problem+json") ||
        contentType.includes("application/json"))
    ) {
      try {
        errorJson = (await response.json()) as ProblemDetails;
      } catch {
        // Ignore JSON parse errors
      }
    }

    // Specialized HTTP 429 handling with global countdown state
    if (response.status === 429) {
      const retryHeader = response.headers.get("retry-after");
      let retrySeconds = 60;
      if (retryHeader) {
        const parsed = parseInt(retryHeader, 10);
        if (!isNaN(parsed) && parsed > 0) {
          retrySeconds = parsed;
        }
      }

      const problem: ProblemDetails = errorJson ?? {
        title: "Too Many Requests",
        status: 429,
        detail: `You're doing that a little too quickly. Please try again in ${retrySeconds} seconds.`,
      };

      useRateLimitStore.getState().setRateLimited(retrySeconds, "api", problem.detail);
      throw new RateLimitException(problem, retrySeconds, response.statusText);
    }

    if (errorJson) {
      if (
        errorJson.title ||
        errorJson.detail ||
        errorJson.status ||
        errorJson.errors
      ) {
        throw new ProblemDetailsException(errorJson, response.statusText);
      }
    }

    const fallbackText = await response.text().catch(() => "");
    throw new ApiException(
      response.status,
      response.statusText,
      fallbackText || `HTTP error ${response.status}: ${response.statusText}`
    );
  }
}

export const apiClient = new ApiClient();
