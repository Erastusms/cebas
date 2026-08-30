export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

export type ApiResponse<T> = {
  success: boolean;
  data: T;
  message?: string | null;
  meta?: Record<string, unknown>;
};

export type CursorPagination<T> = {
  items: T[];
  nextCursor?: string | null;
  hasNextPage: boolean;
  pageSize: number;
};

export type RequestOptions = {
  headers?: Record<string, string>;
  params?: Record<string, string | number | boolean | undefined | null>;
  timeoutMs?: number;
  signal?: AbortSignal;
};
