-- ==============================================================================
-- CEBAS Database Migration: 021_post_hashtags_updated_at.sql
-- Adds updated_at column to post_hashtags table to align with Entity base class
-- ==============================================================================

ALTER TABLE post_hashtags ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
