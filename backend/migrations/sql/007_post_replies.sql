-- ==============================================================================
-- CEBAS Database Migration: 007_post_replies.sql
-- Creates post_replies table supporting self-referencing hierarchical reply threads,
-- author foreign keys, soft-deletion tracking, and pagination indexes
-- ==============================================================================

CREATE TABLE IF NOT EXISTS post_replies (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    author_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    parent_reply_id UUID REFERENCES post_replies(id) ON DELETE CASCADE,
    content VARCHAR(1000) NOT NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ
);

-- Fast indexes for conversation thread retrieval, hierarchical traversal, and author replies
CREATE INDEX IF NOT EXISTS idx_post_replies_post_created ON post_replies (post_id, created_at ASC, id ASC);
CREATE INDEX IF NOT EXISTS idx_post_replies_parent_created ON post_replies (parent_reply_id, created_at ASC, id ASC);
CREATE INDEX IF NOT EXISTS idx_post_replies_post_parent ON post_replies (post_id, parent_reply_id, created_at ASC, id ASC);
CREATE INDEX IF NOT EXISTS idx_post_replies_author_id ON post_replies (author_id);
CREATE INDEX IF NOT EXISTS idx_post_replies_is_deleted ON post_replies (is_deleted);

-- Idempotent column migrations if table previously existed
ALTER TABLE post_replies ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
