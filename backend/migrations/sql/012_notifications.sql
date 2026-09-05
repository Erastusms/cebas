-- ==============================================================================
-- CEBAS Database Migration: 012_notifications.sql
-- Creates notifications table, indexes, and deduplication constraints
-- ==============================================================================

CREATE TABLE IF NOT EXISTS notifications (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    recipient_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    actor_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type notification_type_enum NOT NULL,
    target_id UUID,
    target_type VARCHAR(50),
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_notifications_no_self_notification CHECK (recipient_id <> actor_id)
);

-- Primary query traversal index for recipient feed
CREATE INDEX IF NOT EXISTS idx_notifications_recipient_created 
    ON notifications (recipient_id, created_at DESC, id DESC);

-- Fast unread counter index
CREATE INDEX IF NOT EXISTS idx_notifications_recipient_unread 
    ON notifications (recipient_id) 
    WHERE is_read = FALSE;

-- Actor lookup index
CREATE INDEX IF NOT EXISTS idx_notifications_actor_id 
    ON notifications (actor_id);

-- Target lookup index
CREATE INDEX IF NOT EXISTS idx_notifications_target 
    ON notifications (target_type, target_id) 
    WHERE target_id IS NOT NULL;

-- Idempotency unique constraints:
-- 1. Prevent duplicate LIKE notifications from the same actor on the same post/reply
CREATE UNIQUE INDEX IF NOT EXISTS uq_notifications_like 
    ON notifications (recipient_id, actor_id, type, target_id)
    WHERE type IN ('POST_LIKED', 'REPLY_LIKED');

-- 2. Prevent duplicate FOLLOW notifications from the same follower
CREATE UNIQUE INDEX IF NOT EXISTS uq_notifications_follow 
    ON notifications (recipient_id, actor_id, type)
    WHERE type = 'USER_FOLLOWED';

-- Implicit String Casts for notification_type_enum
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'varchar' AND t.typname = 'notification_type_enum'
    ) THEN
        CREATE CAST (varchar AS notification_type_enum) WITH INOUT AS IMPLICIT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_cast c 
        JOIN pg_type s ON c.castsource = s.oid 
        JOIN pg_type t ON c.casttarget = t.oid 
        WHERE s.typname = 'text' AND t.typname = 'notification_type_enum'
    ) THEN
        CREATE CAST (text AS notification_type_enum) WITH INOUT AS IMPLICIT;
    END IF;
END $$;

