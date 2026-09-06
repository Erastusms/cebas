-- ==============================================================================
-- CEBAS Database Migration: 018_performance_indexes.sql
-- Phase 9: Performance Hardening, Indexing & Observability
-- Ensures all composite indexes are applied after all schema migrations (including 017).
-- ==============================================================================

-- 1. Redundant / Overlapping Index Cleanup
DROP INDEX IF EXISTS idx_posts_author_id;
DROP INDEX IF EXISTS idx_posts_created_pagination;

-- 2. High-Frequency Post Indexes
CREATE INDEX IF NOT EXISTS idx_posts_author_active 
    ON posts (author_id, created_at DESC, id DESC) 
    WHERE is_deleted = FALSE AND is_hidden = FALSE;

CREATE INDEX IF NOT EXISTS idx_posts_active_timeline_v2 
    ON posts (created_at DESC, id DESC) 
    WHERE is_deleted = FALSE AND is_hidden = FALSE;

CREATE INDEX IF NOT EXISTS idx_posts_author_media_active 
    ON posts (author_id, created_at DESC, id DESC) 
    WHERE is_deleted = FALSE AND is_hidden = FALSE AND media_count > 0;

-- 3. Conversation & Reply Thread Indexes
CREATE INDEX IF NOT EXISTS idx_post_replies_active_thread 
    ON post_replies (post_id, parent_reply_id, created_at ASC, id ASC) 
    WHERE is_deleted = FALSE;

CREATE INDEX IF NOT EXISTS idx_post_replies_author_active 
    ON post_replies (author_id, created_at DESC, id DESC) 
    WHERE is_deleted = FALSE;

-- 4. Notification Keyset Pagination & Unread Counter Indexes
CREATE INDEX IF NOT EXISTS idx_notifications_recipient_feed 
    ON notifications (recipient_id, is_read, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_notifications_unread_fast 
    ON notifications (recipient_id, created_at DESC, id DESC) 
    WHERE is_read = FALSE;

-- 5. Social Graph Covering Indexes
CREATE INDEX IF NOT EXISTS idx_follows_following_follower 
    ON follows (following_id, follower_id);

CREATE INDEX IF NOT EXISTS idx_blocks_bidirectional 
    ON blocks (blocker_id, blocked_id);

-- Update query statistics
ANALYZE posts;
ANALYZE post_replies;
ANALYZE follows;
ANALYZE blocks;
ANALYZE notifications;
ANALYZE post_likes;
ANALYZE post_bookmarks;
