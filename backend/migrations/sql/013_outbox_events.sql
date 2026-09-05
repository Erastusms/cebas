-- ==============================================================================
-- CEBAS Database Migration: 013_outbox_events.sql
-- Creates outbox_events table for transactional outbox pattern
-- ==============================================================================

CREATE TABLE IF NOT EXISTS outbox_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    event_type VARCHAR(100) NOT NULL,
    aggregate_type VARCHAR(100) NOT NULL,
    aggregate_id UUID NOT NULL,
    payload JSONB NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    attempt_count INT NOT NULL DEFAULT 0,
    max_retries INT NOT NULL DEFAULT 5,
    next_attempt_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    error_message TEXT,
    correlation_id VARCHAR(100),
    causation_id VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_outbox_status CHECK (status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED'))
);

-- Primary polling index for background worker
CREATE INDEX IF NOT EXISTS idx_outbox_events_polling 
    ON outbox_events (next_attempt_at ASC, created_at ASC) 
    WHERE status IN ('PENDING', 'PROCESSING');

-- Operational and debugging indexes
CREATE INDEX IF NOT EXISTS idx_outbox_events_status_created 
    ON outbox_events (status, created_at ASC);

CREATE INDEX IF NOT EXISTS idx_outbox_events_aggregate 
    ON outbox_events (aggregate_type, aggregate_id);

CREATE INDEX IF NOT EXISTS idx_outbox_events_correlation 
    ON outbox_events (correlation_id) 
    WHERE correlation_id IS NOT NULL;
