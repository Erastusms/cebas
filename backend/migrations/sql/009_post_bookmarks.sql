-- ==============================================================================
-- CEBAS Database Migration: 009_post_bookmarks.sql
-- Creates post_bookmarks table with unique constraint on (post_id, user_id),
-- adds bookmark_count column to posts table with CHECK constraint,
-- and creates indexes for bookmark access patterns
-- ==============================================================================

-- 1. Create Post Bookmarks Table
CREATE TABLE IF NOT EXISTS post_bookmarks (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_post_bookmarks_post_user UNIQUE (post_id, user_id)
);

-- 2. Indexes for Bookmark Access Patterns
-- Lookup: check if user bookmarked a specific post (covered by unique constraint index)
-- Pagination: user's bookmarked posts ordered by time (for /bookmarks page)
CREATE INDEX IF NOT EXISTS idx_post_bookmarks_user_created ON post_bookmarks (user_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_post_bookmarks_post_id ON post_bookmarks (post_id);

-- 3. Add bookmark_count column to posts table
ALTER TABLE posts ADD COLUMN IF NOT EXISTS bookmark_count INT NOT NULL DEFAULT 0;

-- 4. Add CHECK constraint to prevent negative bookmark_count
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_posts_bookmark_count'
    ) THEN
        ALTER TABLE posts ADD CONSTRAINT chk_posts_bookmark_count CHECK (bookmark_count >= 0);
    END IF;
END $$;
