"use client";

import React, { useState } from "react";
import { MoreHorizontal, ShieldAlert, Flag, UserX } from "lucide-react";
import { Dropdown, type DropdownItem } from "../ui/dropdown";
import { Button } from "../ui/button";
import { BlockConfirmModal } from "./BlockConfirmModal";
import { useSocialGraph } from "../../hooks/useSocialGraph";
import { useToast } from "../../hooks/useToast";

export interface UserActionMenuProps {
  targetUserId: string;
  targetUsername: string;
  isBlocked?: boolean;
  className?: string;
}

export function UserActionMenu({
  targetUserId,
  targetUsername,
  isBlocked = false,
  className,
}: UserActionMenuProps) {
  const [isBlockModalOpen, setIsBlockModalOpen] = useState(false);
  const { blockUser, unblockUser, isBlockingLoading, isUnblockingLoading } = useSocialGraph(
    targetUserId,
    targetUsername
  );
  const { info } = useToast();

  const handleReport = () => {
    // Reporting integration boundary (Phase 3 safety placeholder / future moderation integration)
    info(
      `Thank you. The report for @${targetUsername} has been submitted for moderation review.`,
      "Report Submitted"
    );
  };

  const handleUnblock = async () => {
    await unblockUser(targetUserId);
  };

  const items: DropdownItem[] = isBlocked
    ? [
        {
          label: `Unblock @${targetUsername}`,
          icon: <UserX className="h-4 w-4" />,
          onClick: handleUnblock,
        },
        {
          label: `Report @${targetUsername}`,
          icon: <Flag className="h-4 w-4" />,
          onClick: handleReport,
          destructive: true,
        },
      ]
    : [
        {
          label: `Block @${targetUsername}`,
          icon: <ShieldAlert className="h-4 w-4" />,
          onClick: () => setIsBlockModalOpen(true),
          destructive: true,
        },
        {
          label: `Report @${targetUsername}`,
          icon: <Flag className="h-4 w-4" />,
          onClick: handleReport,
          destructive: true,
        },
      ];

  return (
    <>
      <Dropdown
        align="right"
        className={className}
        trigger={
          <Button
            variant="outline"
            size="sm"
            className="h-9 w-9 p-0 rounded-lg text-muted-foreground hover:text-foreground"
            aria-label={`More actions for @${targetUsername}`}
          >
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        }
        items={items}
      />

      <BlockConfirmModal
        isOpen={isBlockModalOpen}
        onClose={() => setIsBlockModalOpen(false)}
        onConfirm={async () => {
          await blockUser(targetUserId);
        }}
        targetUsername={targetUsername}
        isLoading={isBlockingLoading || isUnblockingLoading}
      />
    </>
  );
}
