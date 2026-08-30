-- ==============================================================================
-- Migration: 003_seed_users.sql
-- Description: Seed initial administrative, moderation, and standard user accounts
-- Password for all accounts: Password123!
-- ==============================================================================

INSERT INTO users (id, username, email, password_hash, display_name, bio, role, is_verified, created_at)
VALUES 
(
    '018f0000-0000-7000-8000-000000000001'::uuid,
    'admin',
    'admin@cebas.io',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'CEBAS Admin',
    'Official Administrator of the Celoteh Bebas platform.',
    'ADMIN'::user_role_enum,
    true,
    CURRENT_TIMESTAMP
),
(
    '018f0000-0000-7000-8000-000000000002'::uuid,
    'moderator',
    'moderator@cebas.io',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'Community Moderator',
    'Keeping conversations civil, respectful, and safe across CEBAS.',
    'MODERATOR'::user_role_enum,
    true,
    CURRENT_TIMESTAMP
),
(
    '018f0000-0000-7000-8000-000000000003'::uuid,
    'johndoe',
    'johndoe@example.com',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'John Doe',
    'Full-stack engineer building high-concurrency systems on .NET 10 & Next.js.',
    'USER'::user_role_enum,
    true,
    CURRENT_TIMESTAMP
),
(
    '018f0000-0000-7000-8000-000000000004'::uuid,
    'janedoe',
    'janedoe@example.com',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'Jane Doe',
    'Product designer and open-source enthusiast. Excited for unhindered social conversation!',
    'USER'::user_role_enum,
    false,
    CURRENT_TIMESTAMP
),
(
    '018f0000-0000-7000-8000-000000000005'::uuid,
    'alice',
    'alice@example.com',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'Alice Walker',
    'Cybersecurity researcher & cryptography hobbyist.',
    'USER'::user_role_enum,
    true,
    CURRENT_TIMESTAMP
),
(
    '018f0000-0000-7000-8000-000000000006'::uuid,
    'bob',
    'bob@example.com',
    '$2a$12$BROmH/hrOFCdQGtGFryJwe4UuAF3h7a/tb9U31VjQdmuvUG.w4b3K',
    'Bob Smith',
    'Frontend developer exploring UI micro-interactions and accessibility.',
    'USER'::user_role_enum,
    false,
    CURRENT_TIMESTAMP
)
ON CONFLICT (id) DO NOTHING;
