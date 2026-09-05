"use client";

import { useEffect, useRef } from "react";
import { useRealtimeContext } from "../providers/RealtimeProvider";
import {
  socialHubClient,
  type RealtimeEventMap,
} from "../lib/realtime/socialHubClient";

export function useRealtime() {
  return useRealtimeContext();
}

/**
 * Hook to subscribe to a specific SignalR real-time event.
 * Automatically handles listener mounting and unmounting cleanly.
 */
export function useRealtimeEvent<K extends keyof RealtimeEventMap>(
  event: K,
  handler: (data: RealtimeEventMap[K]) => void
) {
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    const callback = (data: RealtimeEventMap[K]) => {
      handlerRef.current(data);
    };

    const unsub = socialHubClient.on(event, callback);
    return unsub;
  }, [event]);
}
