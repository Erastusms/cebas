-- ==============================================================================
-- CEBAS Database Migration: 006_post_media.sql
-- Creates post_media junction table linking posts and media records with position
-- indexing, uniqueness constraints, and check constraints enforcing max 4 media attachments
-- ==============================================================================

CREATE TABLE IF NOT EXISTS post_media (
    id UUID PRIMARY KEY,
    post_id UUID NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
    media_id UUID NOT NULL REFERENCES media(id) ON DELETE CASCADE,
    position INT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT uq_post_media_post_position UNIQUE (post_id, position),
    CONSTRAINT uq_post_media_post_media UNIQUE (post_id, media_id),
    CONSTRAINT chk_post_media_position CHECK (position >= 0 AND position < 4)
);

-- Fast lookup indexes for post media ordered traversal and media attachment checks
CREATE INDEX IF NOT EXISTS idx_post_media_post_id ON post_media (post_id, position ASC);
CREATE INDEX IF NOT EXISTS idx_post_media_media_id ON post_media (media_id);

-- Idempotent column migrations if table previously existed
ALTER TABLE post_media ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
