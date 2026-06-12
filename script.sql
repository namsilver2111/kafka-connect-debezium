SELECT *
FROM [dbo].[teams]
WHERE [team_id] = 1

UPDATE [dbo].[teams]
SET [city] = 'Manchester'
WHERE [team_id] = 1

SELECT *
FROM [dbo].[teams]
WHERE [team_id] = 1

-----------------------------

SELECT *
FROM [dbo].[players]
WHERE [player_id] = 20

UPDATE [dbo].[players]
SET [nationality] = 'VietNam'
WHERE [player_id] = 20

SELECT *
FROM [dbo].[players]
WHERE [player_id] = 20

-----------------------------
INSERT INTO squad (team_id, player_id, join_date, leave_date, contract_value, is_active) VALUES
(1, 20, '2026-06-12', NULL, 20000000.00, 1)
GO

SELECT *
FROM [dbo].[squad]
WHERE [player_id] = 20

DELETE
FROM [dbo].[squad]
WHERE [player_id] = 20 AND [team_id] = 1

SELECT *
FROM [dbo].[squad]
WHERE [player_id] = 20