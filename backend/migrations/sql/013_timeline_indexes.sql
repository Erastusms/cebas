-- ==============================================================================
-- CEBAS Database Migration: 013_timeline_indexes.sql
-- Creates composite and filtered indexes for timeline keyset cursor pagination (ADR-03 / Phase 6)
-- Supports deterministic chronological traversal (created_at DESC, id DESC)
-- ==============================================================================

-- Keyset cursor pagination traversal index for global and timeline feeds
CREATE INDEX IF NOT EXISTS idx_posts_created_pagination ON posts (created_at DESC, id DESC);

-- Partial index for active non-deleted posts
CREATE INDEX IF NOT EXISTS idx_posts_active_timeline ON posts (created_at DESC, id DESC) WHERE is_deleted = FALSE;
