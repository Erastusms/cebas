"use client";

import React, { createContext, useContext, useEffect, useState } from "react";
import { useAuth } from "../hooks/useAuth";
import {
  socialHubClient,
  type RealtimeConnectionStatus,
} from "../lib/realtime/socialHubClient";

interface RealtimeContextValue {
  status: RealtimeConnectionStatus;
  isConnected: boolean;
  joinPost: (postId: string) => Promise<void>;
  leavePost: (postId: string) => Promise<void>;
}

const RealtimeContext = createContext<RealtimeContextValue>({
  status: "disconnected",
  isConnected: false,
  joinPost: async () => {},
  leavePost: async () => {},
});

export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [status, setStatus] = useState<RealtimeConnectionStatus>(
    socialHubClient.getStatus()
  );

  useEffect(() => {
    const unsub = socialHubClient.onStatusChange((newStatus) => {
      setStatus(newStatus);
    });
    return unsub;
  }, []);

  useEffect(() => {
    if (user?.id) {
      socialHubClient.start();
    } else {
      socialHubClient.stop();
    }
  }, [user?.id]);

  return (
    <RealtimeContext.Provider
      value={{
        status,
        isConnected: status === "connected",
        joinPost: (postId: string) => socialHubClient.joinPost(postId),
        leavePost: (postId: string) => socialHubClient.leavePost(postId),
      }}
    >
      {children}
    </RealtimeContext.Provider>
  );
}

export function useRealtimeContext() {
  return useContext(RealtimeContext);
}
