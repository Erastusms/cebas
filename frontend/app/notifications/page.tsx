"use client";

import React from "react";
import { Bell } from "lucide-react";
import { AuthGuard } from "../../components/auth/AuthGuard";
import { NotificationList } from "../../components/notifications/NotificationList";

export default function NotificationsPage() {
  return (
    <AuthGuard>
      <main className="max-w-2xl mx-auto px-4 py-6 sm:py-8 space-y-6">
        {/* Page Header */}
        <div className="flex items-center space-x-3 border-b border-border/80 pb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <Bell className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-foreground">
              Notifikasi
            </h1>
            <p className="text-xs sm:text-sm text-muted-foreground">
              Aktivitas dan interaksi terbaru dengan akun Anda
            </p>
          </div>
        </div>

        {/* Notifications List */}
        <NotificationList />
      </main>
    </AuthGuard>
  );
}
