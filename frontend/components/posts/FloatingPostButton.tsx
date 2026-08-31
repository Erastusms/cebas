"use client";

import React, { useState } from "react";
import { Feather } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { Modal } from "../ui/modal";
import { PostComposer } from "./PostComposer";
import { useQueryClient } from "@tanstack/react-query";

export function FloatingPostButton() {
  const { isAuthenticated, user } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const queryClient = useQueryClient();

  if (!isAuthenticated || !user) {
    return null;
  }

  const handlePostCreated = () => {
    setIsOpen(false);
    queryClient.invalidateQueries({ queryKey: ["timeline-posts"] });
    queryClient.invalidateQueries({ queryKey: ["user-posts"] });
    queryClient.invalidateQueries({ queryKey: ["public-profile", user.username] });
  };

  return (
    <>
      {/* Floating Action Button */}
      <div className="fixed bottom-6 right-6 z-40">
        <button
          type="button"
          onClick={() => setIsOpen(true)}
          aria-label="Create new post"
          className="group relative flex h-14 w-14 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-xl transition-all duration-200 hover:scale-105 hover:bg-primary-hover focus:outline-none focus:ring-4 focus:ring-primary/30 active:scale-95"
        >
          <Feather className="h-6 w-6 transition-transform duration-200 group-hover:rotate-12" />
          <span className="sr-only">Create a Post</span>
          
          {/* Subtle glow effect */}
          <div className="absolute inset-0 -z-10 rounded-full bg-primary/25 blur-md transition group-hover:bg-primary/40" />
        </button>
      </div>

      {/* Quick Post Composer Modal */}
      {isOpen && (
        <Modal
          isOpen={isOpen}
          onClose={() => setIsOpen(false)}
          title="Create a New Post"
          className="max-w-xl p-6"
        >
          <div className="pt-2">
            <PostComposer
              placeholder="What's happening?"
              onPostCreated={handlePostCreated}
            />
          </div>
        </Modal>
      )}
    </>
  );
}
