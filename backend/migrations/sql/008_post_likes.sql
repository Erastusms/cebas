-- ==============================================================================
-- CEBAS Database Migration: 008_post_likes.sql
-- Creates post_likes table with unique constraint on (post_id, user_id),
-- adds like_count column to posts table with CHECK constraint,
-- and creates indexes for engagement access patterns
-- ==============================================================================

-- 1. Create Post Likes Table
CREATE TABLE IF NOT EXISTS post_likes (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_post_likes_post_user UNIQUE (post_id, user_id)
);

-- 2. Indexes for Like Access Patterns
-- Lookup: check if user liked a specific post (covered by unique constraint index)
-- Pagination: user's liked posts ordered by time
CREATE INDEX IF NOT EXISTS idx_post_likes_user_created ON post_likes (user_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_post_likes_post_id ON post_likes (post_id);

-- 3. Add like_count column to posts table
ALTER TABLE posts ADD COLUMN IF NOT EXISTS like_count INT NOT NULL DEFAULT 0;

-- 4. Add CHECK constraint to prevent negative like_count
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_posts_like_count'
    ) THEN
        ALTER TABLE posts ADD CONSTRAINT chk_posts_like_count CHECK (like_count >= 0);
    END IF;
END $$;
