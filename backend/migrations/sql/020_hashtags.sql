-- ==============================================================================
-- CEBAS Database Migration: 020_hashtags.sql
-- Phase 12: Hashtag Extraction & Real-Time Trending Topics
-- Creates hashtags and post_hashtags tables for durable relational persistence
-- ==============================================================================

CREATE TABLE IF NOT EXISTS hashtags (
    id UUID PRIMARY KEY,
    normalized_name VARCHAR(100) NOT NULL,
    display_name VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_hashtags_normalized_name UNIQUE (normalized_name)
);

CREATE TABLE IF NOT EXISTS post_hashtags (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    hashtag_id UUID NOT NULL REFERENCES hashtags(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_post_hashtags_post_hashtag UNIQUE (post_id, hashtag_id)
);

CREATE INDEX IF NOT EXISTS idx_post_hashtags_hashtag_created ON post_hashtags (hashtag_id, created_at DESC, post_id);
CREATE INDEX IF NOT EXISTS idx_post_hashtags_post_id ON post_hashtags (post_id);

-- Idempotent column addition if post_hashtags was previously created without updated_at
ALTER TABLE post_hashtags ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
