import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  HttpTransportType,
} from "@microsoft/signalr";
import type {
  NotificationCreatedEvent,
  PostLikedEvent,
  PostUnlikedEvent,
  ReplyCreatedEvent,
  NewPostAvailableEvent,
} from "../../types/api";

export type RealtimeConnectionStatus =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

export type RealtimeEventMap = {
  NotificationReceived: NotificationCreatedEvent;
  PostLiked: PostLikedEvent;
  PostUnliked: PostUnlikedEvent;
  ReplyCreated: ReplyCreatedEvent;
  NewPostAvailable: NewPostAvailableEvent;
};

export class SocialHubClient {
  private connection: HubConnection | null = null;
  private status: RealtimeConnectionStatus = "disconnected";
  private statusListeners = new Set<(status: RealtimeConnectionStatus) => void>();
  private eventListeners = new Map<keyof RealtimeEventMap, Set<(data: unknown) => void>>();
  private fallbackIntervalId: ReturnType<typeof setInterval> | null = null;
  private fallbackCallbacks = new Set<() => void>();
  private baseUrl: string;

  constructor(baseUrl?: string) {
    this.baseUrl = (
      baseUrl ??
      process.env.NEXT_PUBLIC_API_BASE_URL ??
      "http://localhost:5226"
    ).replace(/\/$/, "");
  }

  public getStatus(): RealtimeConnectionStatus {
    return this.status;
  }

  public isConnected(): boolean {
    return this.status === "connected";
  }

  public onStatusChange(listener: (status: RealtimeConnectionStatus) => void): () => void {
    this.statusListeners.add(listener);
    listener(this.status);
    return () => this.statusListeners.delete(listener);
  }

  public on<K extends keyof RealtimeEventMap>(
    event: K,
    handler: (data: RealtimeEventMap[K]) => void
  ): () => void {
    if (!this.eventListeners.has(event)) {
      this.eventListeners.set(event, new Set());
    }
    const handlers = this.eventListeners.get(event)!;
    const genericHandler = handler as (data: unknown) => void;
    handlers.add(genericHandler);
    return () => handlers.delete(genericHandler);
  }

  public onFallbackPoll(callback: () => void): () => void {
    this.fallbackCallbacks.add(callback);
    return () => this.fallbackCallbacks.delete(callback);
  }

  public async start(): Promise<void> {
    if (typeof window === "undefined") return;
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.setStatus("connecting");

    const hubUrl = `${this.baseUrl}/hubs/social`;
    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        withCredentials: true,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging({
        log(logLevel: LogLevel, message: string) {
          if (logLevel >= LogLevel.Error) {
            console.warn(`[SignalR Error] ${message}`);
          } else if (logLevel >= LogLevel.Warning) {
            console.warn(`[SignalR Warning] ${message}`);
          }
        },
      })
      .build();

    this.setupConnectionHandlers();

    try {
      await this.connection.start();
      this.setStatus("connected");
      this.stopFallbackPolling();
    } catch (err) {
      console.warn("SignalR connection failed to start, falling back to polling:", err);
      this.setStatus("disconnected");
      this.startFallbackPolling();
    }
  }

  public async stop(): Promise<void> {
    this.stopFallbackPolling();
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // ignore disconnect errors
      }
      this.connection = null;
    }
    this.setStatus("disconnected");
  }

  public async joinPost(postId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke("JoinPostGroup", postId);
      } catch (err) {
        console.warn(`Failed to join post group post:${postId}`, err);
      }
    }
  }

  public async leavePost(postId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke("LeavePostGroup", postId);
      } catch (err) {
        console.warn(`Failed to leave post group post:${postId}`, err);
      }
    }
  }

  private setupConnectionHandlers(): void {
    if (!this.connection) return;

    this.connection.onreconnecting(() => {
      this.setStatus("reconnecting");
      this.startFallbackPolling();
    });

    this.connection.onreconnected(() => {
      this.setStatus("connected");
      this.stopFallbackPolling();
    });

    this.connection.onclose(() => {
      this.setStatus("disconnected");
      this.startFallbackPolling();
    });

    const eventNames: (keyof RealtimeEventMap)[] = [
      "NotificationReceived",
      "PostLiked",
      "PostUnliked",
      "ReplyCreated",
      "NewPostAvailable",
    ];

    for (const evt of eventNames) {
      this.connection.on(evt, (payload: unknown) => {
        const handlers = this.eventListeners.get(evt);
        if (handlers) {
          handlers.forEach((h) => {
            try {
              h(payload);
            } catch (err) {
              console.error(`Error in realtime handler for ${evt}:`, err);
            }
          });
        }
      });
    }
  }

  private setStatus(newStatus: RealtimeConnectionStatus): void {
    if (this.status !== newStatus) {
      this.status = newStatus;
      this.statusListeners.forEach((l) => l(newStatus));
    }
  }

  private startFallbackPolling(): void {
    if (this.fallbackIntervalId !== null) return;
    this.fallbackCallbacks.forEach((cb) => {
      try {
        cb();
      } catch (e) {
        console.error("Fallback callback error:", e);
      }
    });
    this.fallbackIntervalId = setInterval(() => {
      this.fallbackCallbacks.forEach((cb) => {
        try {
          cb();
        } catch (e) {
          console.error("Fallback callback error:", e);
        }
      });
    }, 15000);
  }

  private stopFallbackPolling(): void {
    if (this.fallbackIntervalId !== null) {
      clearInterval(this.fallbackIntervalId);
      this.fallbackIntervalId = null;
    }
  }
}

export const socialHubClient = new SocialHubClient();
