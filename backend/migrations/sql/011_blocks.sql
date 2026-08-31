-- ==============================================================================
-- CEBAS Database Migration: 011_blocks.sql
-- Creates blocks table with directed blocking relationship, self-block check constraint,
-- composite unique constraint, foreign keys, and indexes for bidirectional isolation
-- ==============================================================================

CREATE TABLE IF NOT EXISTS blocks (
    id UUID PRIMARY KEY,
    blocker_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    blocked_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    CONSTRAINT chk_blocks_no_self_block CHECK (blocker_id <> blocked_id),
    CONSTRAINT uq_blocks_blocker_blocked UNIQUE (blocker_id, blocked_id)
);

-- Fast lookup indexes for bidirectional block queries
CREATE INDEX IF NOT EXISTS idx_blocks_blocker_id ON blocks (blocker_id);
CREATE INDEX IF NOT EXISTS idx_blocks_blocked_id ON blocks (blocked_id);
CREATE INDEX IF NOT EXISTS idx_blocks_composite ON blocks (blocker_id, blocked_id);
CREATE INDEX IF NOT EXISTS idx_blocks_reverse ON blocks (blocked_id, blocker_id);
