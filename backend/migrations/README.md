# CEBAS Database Migrations Baseline (Phase 0)

This directory contains deterministic SQL migration scripts and baseline configuration for the CEBAS platform.

## Migration Structure

```
migrations/
├── sql/
│   └── 001_extensions.sql
└── README.md
```

## Migration Execution

Migrations are tracked and applied sequentially to ensure zero-downtime deployments and consistency across development, staging, and production environments.

### Local Development / Manual Execution

To execute the baseline migration against a local PostgreSQL instance:

```bash
# Direct PostgreSQL connection:
psql -h localhost -p 5432 -U cebas_admin -d cebas_db -f backend/migrations/sql/001_extensions.sql

# Via Docker:
docker compose exec -T postgres psql -U cebas_admin -d cebas_db < backend/migrations/sql/001_extensions.sql
```

### Automated Backend Runner

In Development mode, the backend's `DatabaseMigrator` service automatically detects and applies `001_extensions.sql` on startup.

## Roadmap Alignment

| Migration Script | Target Scope | Roadmap Phase Alignment |
|---|---|---|
| `001_extensions.sql` | Enable `uuid-ossp`, `citext`, custom ENUM types | Phase 0 / Phase 1 (Foundation) |
| `002_users.sql` | Create `users` table, role constraints, avatar link | Phase 1 (Identity & Profiles) |
| `003_media.sql` | Create `media` metadata table | Phase 1 (Media Baseline) |
| `004_sessions.sql` | Create `sessions` table for multi-device auth | Phase 1 (Authentication) |
| `005_posts.sql` | Create `posts` table with denormalized counters | Phase 2 (Social Core) |
| `006_post_media.sql` | Create `post_media` attachment junction table | Phase 2 (Content Richness) |
| `007_post_replies.sql` | Create `post_replies` table with hierarchy support | Phase 2 (Conversations) |
| `008_post_likes.sql` | Create `post_likes` engagement table | Phase 2 (Interactions) |
| `009_post_bookmarks.sql` | Create `post_bookmarks` saved items table | Phase 2 (Interactions) |
| `010_follows.sql` | Create `follows` table with check constraints | Phase 2 (Social Graph) |
| `011_blocks.sql` | Create `blocks` table with safety invariants | Phase 3 (Safety & Moderation) |
| `012_notifications.sql` | Create `notifications` table and read state | Phase 3 (Activity & Notifications) |
| `013_outbox_events.sql` | Create `outbox_events` transactional message queue | Phase 3 (Real-Time Engine) |
| `014_indexes.sql` | Deploy high-frequency B-Tree & composite indexes | Phase 4 (Hardening & Launch) |
