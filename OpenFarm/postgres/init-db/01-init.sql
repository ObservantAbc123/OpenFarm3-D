-- Material Type table
CREATE TABLE material_types
(
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    type               varchar(255) UNIQUE NOT NULL,
    bed_temp_floor     integer,
    bed_temp_ceiling   integer,
    print_temp_floor   integer,
    print_temp_ceiling integer
);

-- Color table
CREATE TABLE colors
(
    id    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    color varchar(255) UNIQUE NOT NULL
);

-- Material table
CREATE TABLE materials
(
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    material_type_id  integer NOT NULL REFERENCES material_types (id) ON DELETE CASCADE,
    material_color_id integer NOT NULL REFERENCES colors (id) ON DELETE CASCADE,
    in_stock          BOOLEAN NOT NULL,
    UNIQUE (material_type_id, material_color_id)
);

-- User table
CREATE TABLE users (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name varchar(255) NOT NULL,
    verified BOOLEAN,
    suspended BOOLEAN NOT NULL DEFAULT FALSE,
    org_id varchar(255) UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Email table
CREATE TABLE emails
(
    user_id       BIGINT       NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    email_address varchar(255) NOT NULL,
    is_primary    BOOLEAN      NOT NULL,
    PRIMARY KEY (user_id, email_address)
);

-- Material Price Period table
CREATE TABLE material_price_periods
(
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    created_at  TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at    TIMESTAMPTZ,
    price       NUMERIC(7, 2) NOT NULL,
    material_id integer       NOT NULL REFERENCES materials (id) ON DELETE CASCADE
);

-- Enforce only one active price period per material with index
CREATE UNIQUE INDEX uq_material_one_active_price ON material_price_periods (material_id) WHERE ended_at IS NULL;

-- Printer Models table
CREATE TABLE printer_models
(
    id        INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    model     varchar(255) UNIQUE NOT NULL,
    autostart BOOLEAN             NOT NULL,
    bed_x_min INTEGER,
    bed_x_max INTEGER,
    bed_y_min INTEGER,
    bed_y_max INTEGER,
    bed_z_min INTEGER,
    bed_z_max INTEGER
);

-- Printer Model Price Period table
CREATE TABLE printer_model_price_periods
(
    id               INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    created_at       TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at         TIMESTAMPTZ,
    price            NUMERIC(7, 2) NOT NULL,
    printer_model_id integer REFERENCES printer_models (id) ON DELETE CASCADE
);

-- Enforce only one active price period per material with index
CREATE UNIQUE INDEX uq_printer_one_active_price ON printer_model_price_periods (printer_model_id) WHERE ended_at IS NULL;

-- Printer table
CREATE TABLE printers
(
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    printer            varchar(255) UNIQUE NOT NULL,
    printer_model_id   integer             NOT NULL REFERENCES printer_models (id) ON DELETE CASCADE,
    ip_address         varchar(255),
    api_key            varchar(255),
    enabled            BOOLEAN             NOT NULL,
    autostart          BOOLEAN,
    currently_printing BOOLEAN             NOT NULL DEFAULT FALSE
);

-- Printers Loaded Materials table
CREATE TABLE printers_loaded_materials
(
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    printer_id  integer NOT NULL REFERENCES printers (id) ON DELETE CASCADE,
    material_id integer NOT NULL REFERENCES materials (id) ON DELETE CASCADE,
    UNIQUE (printer_id, material_id)
);

-- Print Job table
CREATE TABLE print_jobs
(
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id           BIGINT       REFERENCES users (id) ON DELETE SET NULL,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    submitted_at      TIMESTAMPTZ  NOT NULL,
    completed_at      TIMESTAMPTZ,
    printer_model_id  integer      REFERENCES printer_models (id) ON DELETE SET NULL,
    material_id       integer      REFERENCES materials (id) ON DELETE SET NULL,
    response_id       varchar(255),
    num_copies        integer      NOT NULL DEFAULT 1,
    print_cost        NUMERIC(7, 2),
    print_weight      DOUBLE PRECISION,
    print_time        DOUBLE PRECISION,
    paid              BOOLEAN      NOT NULL DEFAULT FALSE,
    finished_byte_pos BIGINT,
    job_status        varchar(255) NOT NULL DEFAULT 'received' CHECK ( job_status IN ('received', 'systemApproved',
                                                                                      'operatorApproved', 'printing',
                                                                                      'completed', 'cancelled',
                                                                                      'rejected'))
);

-- Prints table
CREATE TABLE prints
(
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    print_job_id BIGINT REFERENCES print_jobs (id) ON DELETE CASCADE,
    printer_id   integer      REFERENCES printers (id) ON DELETE SET NULL,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    started_at   TIMESTAMPTZ,
    finished_at  TIMESTAMPTZ,
    print_status varchar(255) NOT NULL DEFAULT 'pending' CHECK ( print_status IN
                                                                 ('pending', 'printing', 'completed', 'failed',
                                                                  'cancelled'))
);

-- Threads table for communication threads with users
CREATE TABLE threads (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    job_id BIGINT REFERENCES print_jobs (id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    thread_status varchar(255) NOT NULL DEFAULT 'active' CHECK ( thread_status IN ('active', 'archived', 'closed'))
);

-- Messages table for both email communications and system messages within threads
CREATE TABLE messages (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    thread_id BIGINT NOT NULL REFERENCES threads (id) ON DELETE CASCADE,
    message_content text NOT NULL,
    message_subject text,
    message_type varchar(255) NOT NULL DEFAULT 'email' CHECK ( message_type IN ('email', 'system')),
    sender_type varchar(255) NOT NULL DEFAULT 'user' CHECK ( sender_type IN ('user', 'system')),
    from_email_address varchar(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    message_status varchar(255) NOT NULL DEFAULT 'unseen' CHECK ( message_status IN ('unseen', 'seen', 'processed'))
);

-- thumbnail storage
CREATE TABLE thumbnails
(
    print_job_id BIGINT REFERENCES print_jobs (id) ON DELETE CASCADE PRIMARY KEY,
    thumb_string text NOT NULL
);

-- maintenance tracking
CREATE TABLE maintenance (
    maintenance_report_id INTEGER REFERENCES printers (id) ON DELETE CASCADE PRIMARY KEY,
    date_of_last_service TIMESTAMPTZ,
    date_of_next_service TIMESTAMPTZ,
    session_uptime INTEGER NOT NULL DEFAULT 0, -- interpret as Seconds
    thermal_load_f NUMERIC(8, 3) NOT NULL DEFAULT 0.0,
    thermal_load_c NUMERIC(8, 3) NOT NULL DEFAULT 0.0,
    session_extrusion_volume_m3 NUMERIC(8, 3) NOT NULL DEFAULT 0.0,
    session_extruder_traveled_m NUMERIC(8, 3) NOT NULL DEFAULT 0.0,
    session_error_count INTEGER NOT NULL DEFAULT 0,
    session_prints_completed INTEGER NOT NULL DEFAULT 0,
    session_prints_failed INTEGER NOT NULL DEFAULT 0
);
-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- AI Generated Responses table
CREATE TABLE ai_generated_responses (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    thread_id BIGINT NOT NULL REFERENCES threads (id) ON DELETE CASCADE,
    message_id BIGINT NOT NULL REFERENCES messages (id) ON DELETE CASCADE,
    generated_content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending'
);

-- RAG Documents table
CREATE TABLE rag_documents (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    content TEXT NOT NULL,
    embedding vector(4096),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE emailautoreplyrules (
                                     emailautoreplyruleid  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                     label                 TEXT      NOT NULL,
                                     ruletype              INTEGER   NOT NULL,
                                     startdate             DATE      NULL,
                                     enddate               DATE      NULL,
                                     starttime             TIME      NULL,
                                     endtime               TIME      NULL,
                                     daysofweek            INTEGER   NOT NULL,
                                     createdatutc          TIMESTAMPTZ NOT NULL,
                                     updatedatutc          TIMESTAMPTZ NOT NULL,
                                     subject               TEXT      NOT NULL,
                                     body                  TEXT      NOT NULL,
                                     isenabled             BOOLEAN   NOT NULL,
                                     priority              INTEGER   NOT NULL
);
