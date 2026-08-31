-- ==============================================================================
-- CEBAS Database Migration: 010_follows.sql
-- Creates follows table with directed relationship, self-follow check constraint,
-- composite unique constraint, foreign keys, and indexes for cursor pagination
-- ==============================================================================

CREATE TABLE IF NOT EXISTS follows (
    id UUID PRIMARY KEY,
    follower_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    following_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT chk_follows_no_self_follow CHECK (follower_id <> following_id),
    CONSTRAINT uq_follows_follower_following UNIQUE (follower_id, following_id)
);

-- Keyset/cursor pagination and lookup indexes
CREATE INDEX IF NOT EXISTS idx_follows_following_created ON follows (following_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_follows_follower_created ON follows (follower_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_follows_follower_id ON follows (follower_id);
CREATE INDEX IF NOT EXISTS idx_follows_following_id ON follows (following_id);
