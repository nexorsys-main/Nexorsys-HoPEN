if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) { throw 'Supply the PostgreSQL password through the process environment before running this script.' }
$psqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe";
$adminId = "b0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11";

$sql = @"
INSERT INTO workflows (id, type, user_id, status, comments, form_data, created_at, updated_at) VALUES
(uuid_generate_v4(), 'HABILITATION', '$adminId', 'pending', 'Demande accès SAP Production', '{"details": "L''agent a besoin d''un accès en lecture seule sur le module FI de SAP pour les audits trimestriels.", "requested_by": "Audit Team"}', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'),
(uuid_generate_v4(), 'RH', '$adminId', 'pending', 'Validation période d''essai - Jean Dupont', '{"details": "Période d''essai validée par le manager N+1. En attente de validation RH finale.", "employee_name": "Jean Dupont"}', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'),
(uuid_generate_v4(), 'SECURITY', '$adminId', 'approved', 'Accès Zone Serveurs (Nexorsys-01)', '{"details": "Accès exceptionnel pour maintenance on-site demandée par le prestataire.", "ticket_ref": "TICKET-1234"}', NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days'),
(uuid_generate_v4(), 'HABILITATION', '$adminId', 'rejected', 'Demande droits Administrator Domaine', '{"details": "Demande rejetée car contraire à la politique de sécurité (Privileged Access Management).", "reason": "Security Policy Violation"}', NOW() - INTERVAL '10 days', NOW() - INTERVAL '9 days');
"@

$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
& $psqlPath -U postgres -h localhost -d $databaseName -c $sql
