"use client";

import React, { useState } from "react";
import Link from "next/link";
import { User, Shield, Smartphone, Edit3, Calendar, Mail, CheckCircle2 } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { AuthGuard } from "../../components/auth/AuthGuard";
import { Button } from "../../components/ui/button";
import { EditProfileModal } from "../../components/profile/EditProfileModal";

export default function SettingsPage() {
  const { user } = useAuth();
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);

  return (
    <AuthGuard>
      <main className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground">Account Settings</h1>
          <p className="text-sm text-muted-foreground">Manage your identity, profile, and security preferences.</p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Sidebar Navigation */}
          <div className="space-y-1">
            <Link
              href="/settings"
              className="flex items-center space-x-2.5 px-3 py-2 rounded-lg bg-muted text-foreground font-medium text-sm"
            >
              <User className="h-4 w-4 text-primary" />
              <span>Profile & Account</span>
            </Link>
            <Link
              href="/settings/sessions"
              className="flex items-center space-x-2.5 px-3 py-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 font-medium text-sm transition"
            >
              <Smartphone className="h-4 w-4" />
              <span>Active Sessions</span>
            </Link>
          </div>

          {/* Settings Content */}
          <div className="md:col-span-2 space-y-6">
            {user && (
              <div className="rounded-2xl border border-border bg-card p-6 shadow-sm space-y-6">
                <div className="flex items-center justify-between pb-4 border-b border-border">
                  <div className="flex items-center space-x-3">
                    <div className="flex h-14 w-14 items-center justify-center rounded-full bg-primary text-xl font-bold text-primary-foreground overflow-hidden shadow-sm flex-shrink-0">
                      {user.avatarUrl ? (
                        <img
                          src={user.avatarUrl}
                          alt={user.displayName || user.username}
                          className="h-full w-full object-cover"
                        />
                      ) : (
                        user.displayName?.charAt(0).toUpperCase() || user.username.charAt(0).toUpperCase()
                      )}
                    </div>
                    <div>
                      <div className="flex items-center space-x-1.5 leading-tight">
                        <h2 className="font-semibold text-lg text-foreground">{user.displayName}</h2>
                        {user.isVerified && <CheckCircle2 className="h-4 w-4 text-blue-500" />}
                      </div>
                      <p className="text-xs text-muted-foreground mt-0.5">@{user.username}</p>
                    </div>
                  </div>

                  <Button variant="outline" size="sm" onClick={() => setIsEditModalOpen(true)}>
                    <Edit3 className="h-3.5 w-3.5 mr-1.5" />
                    Edit
                  </Button>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
                  <div className="p-3 rounded-lg bg-muted/30 border border-border space-y-1">
                    <div className="flex items-center text-xs text-muted-foreground space-x-1.5">
                      <Mail className="h-3.5 w-3.5" />
                      <span>Email Address</span>
                    </div>
                    <p className="font-medium text-foreground">{user.email}</p>
                  </div>

                  <div className="p-3 rounded-lg bg-muted/30 border border-border space-y-1">
                    <div className="flex items-center text-xs text-muted-foreground space-x-1.5">
                      <Shield className="h-3.5 w-3.5" />
                      <span>Account Role</span>
                    </div>
                    <p className="font-medium text-foreground">{user.role}</p>
                  </div>

                  <div className="p-3 rounded-lg bg-muted/30 border border-border space-y-1 sm:col-span-2">
                    <div className="flex items-center text-xs text-muted-foreground space-x-1.5">
                      <Calendar className="h-3.5 w-3.5" />
                      <span>Account Created</span>
                    </div>
                    <p className="font-medium text-foreground">
                      {new Date(user.createdAt).toLocaleDateString("en-US", {
                        weekday: "long",
                        year: "numeric",
                        month: "long",
                        day: "numeric",
                      })}
                    </p>
                  </div>
                </div>

                {user.bio && (
                  <div className="p-4 rounded-lg bg-muted/20 border border-border space-y-1">
                    <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Biography</span>
                    <p className="text-sm text-foreground/90 whitespace-pre-wrap">{user.bio}</p>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {user && (
          <EditProfileModal
            isOpen={isEditModalOpen}
            onClose={() => setIsEditModalOpen(false)}
            currentUser={user}
          />
        )}
      </main>
    </AuthGuard>
  );
}
