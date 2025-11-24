-- Populate material types
INSERT INTO material_types (type, bed_temp_floor, bed_temp_ceiling, print_temp_floor, print_temp_ceiling)
VALUES
    ('PLA', 50, 60, 190, 230);

-- Populate colors
INSERT INTO colors (color)
VALUES ('Red'),
       ('Blue'),
       ('Green'),
       ('Black'),
       ('White');

-- Populate materials
INSERT INTO materials (material_type_id, material_color_id, in_stock)
VALUES
    (1, 1, TRUE);

-- Printer models
INSERT INTO printer_models (model, autostart, bed_x_min, bed_x_max, bed_y_min, bed_y_max, bed_z_min, bed_z_max)
VALUES ('MK4S', TRUE, 0, 250, -4, 210, 0, 220);

-- Printer model price periods
INSERT INTO printer_model_price_periods (price, printer_model_id)
VALUES
    (5.00, 1);

-- Printers
INSERT INTO printers (printer, ip_address, api_key, printer_model_id, enabled, autostart)
VALUES
    ('Prusa_1', 'http://octoprint_1:80', '-', 1, TRUE, TRUE),
    ('Prusa_2', 'http://octoprint_2:80', '-', 1, TRUE, TRUE);

-- Printers loaded materials
INSERT INTO printers_loaded_materials (printer_id, material_id)
VALUES
    (1, 1),
    (2, 1);

-- Material price periods
INSERT INTO material_price_periods (price, material_id)
VALUES
    (20.00, 1);

-- Initial Maintenance Templates
INSERT INTO maintenance (maintenance_report_id, date_of_last_service, date_of_next_service)
VALUES
    (1,NOW() - INTERVAL '5 days',NOW() + INTERVAL '25 days'),
    (2,NOW() - INTERVAL '15 days',NOW() + INTERVAL '15 days');

-- Test user: Josh
INSERT INTO users (name, verified, suspended)
VALUES ('Josh Schell', TRUE, FALSE);

-- Josh's email
INSERT INTO emails (user_id, email_address, is_primary)
VALUES (1, 'josh@schell.me', TRUE);

-- Threads for Josh
INSERT INTO threads (user_id, job_id, thread_status)
VALUES 
    (1, NULL, 'active'),
    (1, NULL, 'active');

-- Josh's messages asking about opening times and materials (in separate threads)
INSERT INTO messages (thread_id, message_content, message_subject, message_type, sender_type, from_email_address, message_status)
VALUES 
    (1, 'Hi there! What times are you guys open?', 'Question about hours', 'email', 'user', 'josh@schell.me', 'unseen'),
    (2, 'What materials do you have available?', 'Question about materials', 'email', 'user', 'josh@schell.me', 'unseen');