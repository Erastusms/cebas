-- ==============================================================================
-- CEBAS Database Migration: 019_theme_preference.sql
-- Phase 10: Dark Mode & Dynamic Theme System
-- Adds theme_preference column to users table with DEFAULT 'SYSTEM'
-- ==============================================================================

ALTER TABLE users
ADD COLUMN IF NOT EXISTS theme_preference VARCHAR(10) NOT NULL DEFAULT 'SYSTEM';
