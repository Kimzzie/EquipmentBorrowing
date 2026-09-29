-- ============================================================
-- database-queries.sql
-- Equipment Borrowing System — Laboratory Activity 3, Part C
-- Demonstrates SQL understanding against the schema created by
-- the EF Core migrations. These are illustrative only — the
-- application itself always goes through the repository / EF
-- Core layer, never raw SQL like this.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Basic Retrieval — all equipment
-- ------------------------------------------------------------
SELECT *
FROM "Equipment";

-- ------------------------------------------------------------
-- 2. Filtering — only currently available equipment
-- ------------------------------------------------------------
SELECT "Id", "Name", "Type", "Description", "IsAvailable"
FROM "Equipment"
WHERE "IsAvailable" = 1;

-- ------------------------------------------------------------
-- 3. Join — active borrowings with student and equipment names
-- ------------------------------------------------------------
SELECT
    s."Name" AS "Student",
    e."Name" AS "Equipment",
    b."DateBorrowed" AS "Borrowed",
    b."ExpectedReturnDate" AS "Due"
FROM "Borrowings" b
INNER JOIN "Students" s ON b."StudentId" = s."Id"
INNER JOIN "Equipment" e ON b."EquipmentId" = e."Id"
WHERE b."Status" = 'Active';

-- ------------------------------------------------------------
-- 4. Aggregate — number of active borrowings for a given student
-- ------------------------------------------------------------
SELECT COUNT(*) AS "ActiveBorrowingCount"
FROM "Borrowings"
WHERE "StudentId" = 1
  AND "Status" = 'Active';

-- ------------------------------------------------------------
-- 5. Update — mark a piece of equipment as available again
-- ------------------------------------------------------------
UPDATE "Equipment"
SET "IsAvailable" = 1
WHERE "Id" = 2;