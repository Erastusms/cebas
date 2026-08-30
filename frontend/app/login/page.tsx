"use client";

import React, { useState, useEffect, Suspense } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema, type LoginFormData } from "../../lib/validations/auth";
import { useAuth } from "../../hooks/useAuth";
import { AuthCard } from "../../components/auth/AuthCard";
import { Input } from "../../components/ui/input";
import { Button } from "../../components/ui/button";
import { ProblemDetailsException } from "../../lib/api/errors";
import { Eye, EyeOff } from "lucide-react";

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("returnUrl") || "/";
  const { login, isLoggingIn, isAuthenticated } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      router.replace(returnUrl);
    }
  }, [isAuthenticated, router, returnUrl]);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      identifier: "",
      password: "",
    },
  });

  const onSubmit = async (data: LoginFormData) => {
    setServerError(null);
    try {
      await login(data);
      router.push(returnUrl);
    } catch (err: unknown) {
      if (err instanceof ProblemDetailsException) {
        setServerError(err.problem.detail || err.problem.title || "Invalid username/email or password.");
      } else if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("Unable to connect to the server. Please try again.");
      }
    }
  };

  return (
    <AuthCard
      title="Welcome Back"
      description="Sign in to your CEBAS account to continue conversations."
      footer={
        <p>
          Don&apos;t have an account?{" "}
          <Link href="/register" className="font-semibold text-primary hover:underline">
            Create an account
          </Link>
        </p>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {serverError && (
          <div
            role="alert"
            className="p-3 text-xs rounded-lg border border-destructive/50 bg-destructive/10 text-destructive font-medium leading-relaxed"
          >
            {serverError}
          </div>
        )}

        <Input
          label="Username or Email"
          placeholder="e.g. johndoe or john@example.com"
          autoComplete="username"
          {...register("identifier")}
          error={errors.identifier?.message}
        />

        <Input
          label="Password"
          type={showPassword ? "text" : "password"}
          placeholder="••••••••"
          autoComplete="current-password"
          {...register("password")}
          error={errors.password?.message}
          endAdornment={
            <button
              type="button"
              onClick={() => setShowPassword(!showPassword)}
              className="flex items-center justify-center text-muted-foreground hover:text-foreground focus-visible:outline-none transition-colors"
              aria-label={showPassword ? "Hide password" : "Show password"}
            >
              {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          }
        />

        <Button type="submit" variant="default" className="w-full mt-2" isLoading={isLoggingIn}>
          Sign In
        </Button>
      </form>
    </AuthCard>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center">Loading...</div>}>
      <LoginForm />
    </Suspense>
  );
}
