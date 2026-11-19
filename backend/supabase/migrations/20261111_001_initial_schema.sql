-- ====================================================================
-- MISSION RACER: COMMERCIAL MULTI-TENANT DATABASE SCHEMA
-- Compliant with Apple App Store (Kids Category) & Google Play Families
-- Platform: Supabase / PostgreSQL
-- ====================================================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. ENUMS
CREATE TYPE user_role AS ENUM ('parent', 'child');
CREATE TYPE vehicle_category AS ENUM ('muscle', 'sport', 'truck', 'bike');
CREATE TYPE driver_rank AS ENUM ('rookie', 'pro', 'veteran', 'champion');
CREATE TYPE mission_status AS ENUM ('pending', 'approved', 'rejected');

-- 2. FAMILIES (Multi-tenant container)
CREATE TABLE families (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_name TEXT NOT NULL,
    parent_owner_id UUID REFERENCES auth.users(id) ON DELETE CASCADE,
    language_code TEXT DEFAULT 'en', -- 'en', 'de', 'pl'
    has_family_pass BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 3. PROFILES (Zero PII for children: callsigns only)
CREATE TABLE profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    display_name TEXT NOT NULL,
    role user_role DEFAULT 'child',
    age_bracket INT DEFAULT 10,
    credits_balance INT DEFAULT 500,
    fuel_units INT DEFAULT 3,
    max_fuel_units INT DEFAULT 5,
    selected_vehicle_id UUID,
    last_fuel_refill TIMESTAMPTZ DEFAULT now(),
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 4. FAMILY ONBOARDING & DEVICE PAIRING (QR / 6-Digit PIN)
CREATE TABLE family_invite_codes (
    code VARCHAR(6) PRIMARY KEY,
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    created_by UUID REFERENCES profiles(id),
    expires_at TIMESTAMPTZ DEFAULT (now() + INTERVAL '24 hours'),
    max_uses INT DEFAULT 5,
    uses_count INT DEFAULT 0
);

-- Child Device Tokens (Child devices authenticate via token, no email/password needed)
CREATE TABLE child_device_tokens (
    token UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    profile_id UUID REFERENCES profiles(id) ON DELETE CASCADE,
    platform TEXT, -- 'ios', 'android'
    device_model TEXT,
    created_at TIMESTAMPTZ DEFAULT now(),
    last_active TIMESTAMPTZ DEFAULT now()
);

-- 5. DIGITAL & PHYSICAL RACING LICENSES
CREATE TABLE racing_licenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    profile_id UUID UNIQUE REFERENCES profiles(id) ON DELETE CASCADE,
    license_code TEXT UNIQUE NOT NULL,      -- e.g. 'MR-777-ROOKIE'
    callsign TEXT NOT NULL,
    driver_rank driver_rank DEFAULT 'rookie',
    is_physical_nfc_linked BOOLEAN DEFAULT false,
    card_uid TEXT,                          -- Hardware UID of NTAG215 (optional)
    total_stamps_received INT DEFAULT 0,
    last_pitstop_at TIMESTAMPTZ,
    last_stamped_by UUID REFERENCES profiles(id),
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 6. VEHICLE DEFINITIONS (Global Catalog)
CREATE TABLE vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code_name TEXT UNIQUE NOT NULL,
    display_name TEXT NOT NULL,
    category vehicle_category NOT NULL,
    description TEXT,
    base_top_speed REAL NOT NULL,
    base_acceleration REAL NOT NULL,
    base_handling REAL NOT NULL,
    base_weight REAL NOT NULL,
    credit_cost INT DEFAULT 0,
    min_driver_level INT DEFAULT 1,
    is_starter BOOLEAN DEFAULT false
);

-- 7. PLAYER GARAGES (Owned vehicles & customizations)
CREATE TABLE player_vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    profile_id UUID REFERENCES profiles(id) ON DELETE CASCADE,
    vehicle_id UUID REFERENCES vehicles(id) ON DELETE CASCADE,
    primary_color_hex TEXT DEFAULT '#FF3B30',
    secondary_color_hex TEXT DEFAULT '#1C1C1E',
    neon_underglow_hex TEXT DEFAULT '#00E5FF',
    spoiler_index INT DEFAULT 0,
    wheel_index INT DEFAULT 0,
    horn_sound_id TEXT DEFAULT 'beep_classic',
    engine_upgrade_level INT DEFAULT 0,
    tire_upgrade_level INT DEFAULT 0,
    handling_upgrade_level INT DEFAULT 0,
    acquired_at TIMESTAMPTZ DEFAULT now(),
    UNIQUE(profile_id, vehicle_id)
);

-- 8. MISSIONS CATALOG (Custom per family)
CREATE TABLE missions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    description TEXT,
    category TEXT DEFAULT 'school',
    credit_reward INT NOT NULL DEFAULT 250,
    fuel_reward INT NOT NULL DEFAULT 1,
    requires_photo_proof BOOLEAN DEFAULT false,
    is_daily BOOLEAN DEFAULT true,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now()
);

-- 9. MISSION SUBMISSIONS & DAD PIT STOP STAMPS
CREATE TABLE mission_submissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    mission_id UUID REFERENCES missions(id) ON DELETE CASCADE,
    child_profile_id UUID REFERENCES profiles(id) ON DELETE CASCADE,
    license_id UUID REFERENCES racing_licenses(id),
    was_physically_stamped BOOLEAN DEFAULT false,
    proof_photo_url TEXT,
    status mission_status DEFAULT 'pending',
    reviewer_id UUID REFERENCES profiles(id),
    review_notes TEXT,
    credits_awarded INT DEFAULT 0,
    fuel_awarded INT DEFAULT 0,
    submitted_at TIMESTAMPTZ DEFAULT now(),
    reviewed_at TIMESTAMPTZ
);

-- 10. MULTIPLAYER RACE SESSIONS
CREATE TABLE race_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_id UUID REFERENCES families(id) ON DELETE CASCADE,
    relay_room_code TEXT NOT NULL,
    track_id TEXT NOT NULL,
    winner_profile_id UUID REFERENCES profiles(id),
    laps_completed INT DEFAULT 3,
    total_duration_seconds REAL,
    played_at TIMESTAMPTZ DEFAULT now()
);

-- ====================================================================
-- ROW LEVEL SECURITY (RLS) FOR PRIVACY & APP STORE COMPLIANCE
-- ====================================================================

ALTER TABLE families ENABLE ROW LEVEL SECURITY;
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE racing_licenses ENABLE ROW LEVEL SECURITY;
ALTER TABLE player_vehicles ENABLE ROW LEVEL SECURITY;
ALTER TABLE missions ENABLE ROW LEVEL SECURITY;
ALTER TABLE mission_submissions ENABLE ROW LEVEL SECURITY;

-- Helper function to get current user's family
CREATE OR REPLACE FUNCTION get_current_family_id()
RETURNS UUID AS $$
    SELECT family_id FROM profiles WHERE id = auth.uid() LIMIT 1;
$$ LANGUAGE sql STABLE SECURITY DEFINER;

-- Family isolation policies
CREATE POLICY family_members_only ON profiles
    FOR ALL USING (family_id = get_current_family_id());

CREATE POLICY missions_family_only ON missions
    FOR ALL USING (family_id = get_current_family_id());

CREATE POLICY submissions_family_only ON mission_submissions
    FOR ALL USING (family_id = get_current_family_id());

CREATE POLICY vehicles_family_only ON player_vehicles
    FOR ALL USING (family_id = get_current_family_id());

-- ====================================================================
-- GLOBAL VEHICLE CATALOG SEED DATA
-- ====================================================================

INSERT INTO vehicles (code_name, display_name, category, description, base_top_speed, base_acceleration, base_handling, base_weight, credit_cost, is_starter) VALUES
('v8_thundercat', 'V8 Thundercat', 'muscle', 'Raw American muscle. Loud V8 roar, massive straight-line speed, and glorious powerslides.', 85.0, 75.0, 60.0, 1600.0, 0, true),
('mud_crusher', 'Mud Crusher 4x4', 'truck', 'Heavy-duty off-road truck. Smashes track barriers and cuts through mud and grass without slowing down.', 75.0, 70.0, 65.0, 2400.0, 0, true),
('apex_gt', 'Apex GT', 'sport', 'Precision sports coupe with razor-sharp cornering, active aero spoiler, and lightning acceleration.', 92.0, 90.0, 95.0, 1200.0, 800, false),
('trail_blazer_250', 'Trail Blazer 250', 'bike', 'Nimble motocross bike. Weaves through tight corners, pops wheelies, and hits monster jumps.', 88.0, 85.0, 90.0, 350.0, 1000, false);
