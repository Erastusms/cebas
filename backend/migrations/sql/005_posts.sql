-- ==============================================================================
-- CEBAS Database Migration: 005_posts.sql
-- Creates primary posts table with author foreign key, content payload,
-- soft-deletion fields, atomic counter defaults and invariants, and access indexes
-- ==============================================================================

CREATE TABLE IF NOT EXISTS posts (
    id UUID PRIMARY KEY,
    author_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    content VARCHAR(1000) NOT NULL DEFAULT '',
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    reply_count INT NOT NULL DEFAULT 0,
    media_count INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT chk_posts_reply_count CHECK (reply_count >= 0),
    CONSTRAINT chk_posts_media_count CHECK (media_count >= 0 AND media_count <= 4)
);

-- Fast lookup indexes for timeline queries, author profile posts, and soft-delete filtering
CREATE INDEX IF NOT EXISTS idx_posts_author_created ON posts (author_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_posts_created_at ON posts (created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_posts_is_deleted ON posts (is_deleted);
CREATE INDEX IF NOT EXISTS idx_posts_author_id ON posts (author_id);

-- Idempotent column migrations if table previously existed
ALTER TABLE posts ADD COLUMN IF NOT EXISTS content VARCHAR(1000) NOT NULL DEFAULT '';
ALTER TABLE posts ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS reply_count INT NOT NULL DEFAULT 0;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS media_count INT NOT NULL DEFAULT 0;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
