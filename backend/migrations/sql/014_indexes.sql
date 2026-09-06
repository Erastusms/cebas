-- ==============================================================================
-- CEBAS Database Migration: 014_indexes.sql
-- Phase 9: Performance Hardening, Indexing & Observability
-- Deploys high-frequency composite B-Tree and partial indexes for critical read paths.
-- Safely cleans up redundant/overlapping indexes identified during query profiling.
-- ==============================================================================

-- 1. Redundant / Overlapping Index Remediation
-- Profiling confirmed idx_posts_author_id caused PostgreSQL planner to pick a single-column
-- index and perform in-memory quicksort instead of leveraging index ordering.
DROP INDEX IF EXISTS idx_posts_author_id;

-- idx_posts_created_pagination is a 100% duplicate of idx_posts_created_at.
DROP INDEX IF EXISTS idx_posts_created_pagination;


-- 2. High-Frequency Post Indexes (Home Feed, Profile Timelines, Media Feeds)
-- Dynamic DO block ensures safety across all migration run sequences
DO $$
BEGIN
    -- Check if is_hidden column exists on posts (from 017_reports_and_moderation)
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'posts' AND column_name = 'is_hidden'
    ) THEN
        -- Optimized author profile timeline index (active non-deleted, non-hidden)
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_posts_author_active') THEN
            CREATE INDEX idx_posts_author_active 
                ON posts (author_id, created_at DESC, id DESC) 
                WHERE is_deleted = FALSE AND is_hidden = FALSE;
        END IF;

        -- Global / Home timeline keyset cursor pagination index (active non-deleted, non-hidden)
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_posts_active_timeline_v2') THEN
            CREATE INDEX idx_posts_active_timeline_v2 
                ON posts (created_at DESC, id DESC) 
                WHERE is_deleted = FALSE AND is_hidden = FALSE;
        END IF;

        -- Profile Media feed filtered index
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_posts_author_media_active') THEN
            CREATE INDEX idx_posts_author_media_active 
                ON posts (author_id, created_at DESC, id DESC) 
                WHERE is_deleted = FALSE AND is_hidden = FALSE AND media_count > 0;
        END IF;
    ELSE
        -- Fallback if running prior to 017
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_posts_author_active') THEN
            CREATE INDEX idx_posts_author_active 
                ON posts (author_id, created_at DESC, id DESC) 
                WHERE is_deleted = FALSE;
        END IF;
    END IF;
END $$;


-- 3. Conversation & Reply Thread Indexes
-- Accelerates hierarchical reply thread retrieval and active reply queries
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
-- Accelerates follower-set lookups and bidirectional relationship checks
CREATE INDEX IF NOT EXISTS idx_follows_following_follower 
    ON follows (following_id, follower_id);

CREATE INDEX IF NOT EXISTS idx_blocks_bidirectional 
    ON blocks (blocker_id, blocked_id);

-- Verify stats
ANALYZE posts;
ANALYZE post_replies;
ANALYZE follows;
ANALYZE blocks;
ANALYZE notifications;
ANALYZE post_likes;
ANALYZE post_bookmarks;
