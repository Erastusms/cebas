"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { registerSchema, type RegisterFormData } from "../../lib/validations/auth";
import { useAuth } from "../../hooks/useAuth";
import { AuthCard } from "../../components/auth/AuthCard";
import { Input } from "../../components/ui/input";
import { Button } from "../../components/ui/button";
import { ProblemDetailsException } from "../../lib/api/errors";
import { Eye, EyeOff } from "lucide-react";

export default function RegisterPage() {
  const router = useRouter();
  const { register: registerUser, isRegistering, isAuthenticated, login } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      router.replace("/");
    }
  }, [isAuthenticated, router]);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      username: "",
      email: "",
      displayName: "",
      password: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (data: RegisterFormData) => {
    setServerError(null);
    try {
      await registerUser({
        username: data.username,
        email: data.email,
        password: data.password,
        displayName: data.displayName || undefined,
      });

      // Automatically log in newly registered user for smooth onboarding
      await login({
        identifier: data.username,
        password: data.password,
      });

      router.push("/");
    } catch (err: unknown) {
      if (err instanceof ProblemDetailsException) {
        setServerError(err.problem.detail || err.problem.title || "Registration failed.");
      } else if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("Unable to connect to the server. Please try again.");
      }
    }
  };

  return (
    <AuthCard
      title="Create an Account"
      description="Join CEBAS for unhindered public conversations."
      footer={
        <p>
          Already have an account?{" "}
          <Link href="/login" className="font-semibold text-primary hover:underline">
            Sign in
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
          label="Handle (Username)"
          placeholder="e.g. johndoe"
          autoComplete="username"
          helperText="3-30 alphanumeric characters or underscores"
          {...register("username")}
          error={errors.username?.message}
        />

        <Input
          label="Email Address"
          type="email"
          placeholder="you@example.com"
          autoComplete="email"
          {...register("email")}
          error={errors.email?.message}
        />

        <Input
          label="Display Name (Optional)"
          placeholder="e.g. John Doe"
          {...register("displayName")}
          error={errors.displayName?.message}
        />

        <Input
          label="Password"
          type={showPassword ? "text" : "password"}
          placeholder="At least 8 characters"
          autoComplete="new-password"
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

        <Input
          label="Confirm Password"
          type={showConfirmPassword ? "text" : "password"}
          placeholder="Repeat your password"
          autoComplete="new-password"
          {...register("confirmPassword")}
          error={errors.confirmPassword?.message}
          endAdornment={
            <button
              type="button"
              onClick={() => setShowConfirmPassword(!showConfirmPassword)}
              className="flex items-center justify-center text-muted-foreground hover:text-foreground focus-visible:outline-none transition-colors"
              aria-label={showConfirmPassword ? "Hide confirm password" : "Show confirm password"}
            >
              {showConfirmPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          }
        />

        <Button type="submit" variant="default" className="w-full mt-2" isLoading={isRegistering}>
          Create Account
        </Button>
      </form>
    </AuthCard>
  );
}
