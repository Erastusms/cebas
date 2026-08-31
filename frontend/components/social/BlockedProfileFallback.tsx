"use client";

import React from "react";
import { ShieldAlert, UserX } from "lucide-react";
import { Button } from "../ui/button";
import { useSocialGraph } from "../../hooks/useSocialGraph";

export interface BlockedProfileFallbackProps {
  targetUserId?: string;
  targetUsername: string;
  isBlockedByMe?: boolean;
}

export function BlockedProfileFallback({
  targetUserId,
  targetUsername,
  isBlockedByMe = false,
}: BlockedProfileFallbackProps) {
  const { unblockUser, isUnblockingLoading } = useSocialGraph(targetUserId, targetUsername);

  if (isBlockedByMe) {
    return (
      <main className="max-w-4xl mx-auto px-4 py-16 text-center space-y-4">
        <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive mb-2">
          <ShieldAlert className="h-8 w-8" />
        </div>
        <h1 className="text-2xl font-bold text-foreground">You have blocked @{targetUsername}</h1>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          You cannot see their posts or interact with this account while they are blocked.
        </p>
        {targetUserId && (
          <div className="pt-2">
            <Button
              variant="outline"
              size="sm"
              isLoading={isUnblockingLoading}
              onClick={() => unblockUser(targetUserId)}
              className="font-medium"
            >
              Unblock @{targetUsername}
            </Button>
          </div>
        )}
      </main>
    );
  }

  return (
    <main className="max-w-4xl mx-auto px-4 py-16 text-center space-y-4">
      <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-muted text-muted-foreground mb-2">
        <UserX className="h-8 w-8" />
      </div>
      <h1 className="text-2xl font-bold text-foreground">Account Unavailable</h1>
      <p className="text-sm text-muted-foreground max-w-md mx-auto">
        This account is not accessible due to privacy or safety settings.
      </p>
    </main>
  );
}
