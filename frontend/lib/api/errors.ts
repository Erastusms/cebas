import type { ProblemDetails } from "./types";

export class ApiException extends Error {
  public readonly status: number;
  public readonly statusText: string;

  constructor(status: number, statusText: string, message: string) {
    super(message);
    this.name = "ApiException";
    this.status = status;
    this.statusText = statusText;
  }
}

export class ProblemDetailsException extends ApiException {
  public readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails, statusText = "Bad Request") {
    super(
      problem.status ?? 400,
      statusText,
      problem.detail ?? problem.title ?? "A problem occurred processing your request."
    );
    this.name = "ProblemDetailsException";
    this.problem = problem;
  }

  public get errors(): Record<string, string[]> | undefined {
    return this.problem.errors;
  }

  public get traceId(): string | undefined {
    return this.problem.traceId;
  }
}

export class NetworkException extends Error {
  constructor(message = "Network connection failed. Please verify your internet connection.") {
    super(message);
    this.name = "NetworkException";
  }
}

export class RateLimitException extends ProblemDetailsException {
  public readonly retryAfterSeconds: number;

  constructor(
    problem: ProblemDetails,
    retryAfterSeconds = 60,
    statusText = "Too Many Requests"
  ) {
    super(problem, statusText);
    this.name = "RateLimitException";
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

