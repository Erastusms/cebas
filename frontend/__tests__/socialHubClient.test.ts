import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SocialHubClient } from "../lib/realtime/socialHubClient";

const mockInvoke = vi.fn();
const mockStart = vi.fn().mockResolvedValue(undefined);
const mockStop = vi.fn().mockResolvedValue(undefined);
const mockOn = vi.fn();
const mockOnreconnecting = vi.fn();
const mockOnreconnected = vi.fn();
const mockOnclose = vi.fn();

vi.mock("@microsoft/signalr", () => {
  class MockHubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      return {
        state: "Connected",
        start: mockStart,
        stop: mockStop,
        invoke: mockInvoke,
        on: mockOn,
        onreconnecting: mockOnreconnecting,
        onreconnected: mockOnreconnected,
        onclose: mockOnclose,
      };
    }
  }

  return {
    HubConnectionState: {
      Disconnected: "Disconnected",
      Connecting: "Connecting",
      Connected: "Connected",
      Reconnecting: "Reconnecting",
    },
    LogLevel: {
      Warning: 2,
    },
    HttpTransportType: {
      WebSockets: 1,
      LongPolling: 4,
    },
    HubConnectionBuilder: MockHubConnectionBuilder,
  };
});

describe("SocialHubClient Real-Time Engine", () => {
  let client: SocialHubClient;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    client = new SocialHubClient("http://localhost:5226");
  });

  afterEach(async () => {
    await client.stop();
    vi.useRealTimers();
  });

  it("should initialize in disconnected state", () => {
    expect(client.getStatus()).toBe("disconnected");
    expect(client.isConnected()).toBe(false);
  });

  it("should notify on status change", () => {
    const statuses: string[] = [];
    const unsub = client.onStatusChange((s) => statuses.push(s));

    expect(statuses).toEqual(["disconnected"]);
    unsub();
  });

  it("should start connection and transition to connected", async () => {
    await client.start();

    expect(mockStart).toHaveBeenCalled();
    expect(client.getStatus()).toBe("connected");
    expect(client.isConnected()).toBe(true);
  });

  it("should invoke JoinPostGroup on hub when joinPost is called", async () => {
    await client.start();
    await client.joinPost("post-abc");

    expect(mockInvoke).toHaveBeenCalledWith("JoinPostGroup", "post-abc");
  });

  it("should invoke LeavePostGroup on hub when leavePost is called", async () => {
    await client.start();
    await client.leavePost("post-abc");

    expect(mockInvoke).toHaveBeenCalledWith("LeavePostGroup", "post-abc");
  });

  it("should register event listener and allow unregistering", () => {
    const handler = vi.fn();
    const unsub = client.on("NotificationReceived", handler);

    // Unregister should succeed without error
    unsub();
  });

  it("should trigger fallback polling when connection fails to start", async () => {
    mockStart.mockRejectedValueOnce(new Error("Connection refused"));
    const pollCallback = vi.fn();

    client.onFallbackPoll(pollCallback);
    await client.start();

    expect(client.getStatus()).toBe("disconnected");
    // Immediate fallback trigger
    expect(pollCallback).toHaveBeenCalledTimes(1);

    // Advances timer by 15s -> triggers fallback poll again
    vi.advanceTimersByTime(15000);
    expect(pollCallback).toHaveBeenCalledTimes(2);

    vi.advanceTimersByTime(15000);
    expect(pollCallback).toHaveBeenCalledTimes(3);
  });
});
