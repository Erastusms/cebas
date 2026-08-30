import React from "react";
import Link from "next/link";
import { cn } from "../../lib/utils";

interface AuthCardProps {
  title: string;
  description: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  className?: string;
}

export function AuthCard({ title, description, children, footer, className }: AuthCardProps) {
  return (
    <div className="flex min-h-[calc(100vh-4rem)] items-center justify-center px-4 py-12 sm:px-6 lg:px-8">
      <div className={cn("w-full max-w-md space-y-6 rounded-2xl border border-border bg-card p-6 sm:p-8 shadow-xl text-card-foreground", className)}>
        <div className="space-y-2 text-center">
          <Link href="/" className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-primary font-bold text-xl text-primary-foreground shadow-lg shadow-primary/20 mb-2">
            C
          </Link>
          <h1 className="text-2xl font-bold tracking-tight text-foreground">{title}</h1>
          <p className="text-sm text-muted-foreground">{description}</p>
        </div>

        <div>{children}</div>

        {footer && <div className="border-t border-border pt-4 text-center text-xs text-muted-foreground">{footer}</div>}
      </div>
    </div>
  );
}
