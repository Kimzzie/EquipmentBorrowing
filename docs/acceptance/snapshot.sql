-- Acceptance-test snapshot (Lab 5). Run against equipment_borrowing_test.
-- Capture the output BEFORE and AFTER each mutation test.

SELECT "Id", "StudentNumber", "Name", "Contact", "IsAllowedToBorrow"
FROM "Students" ORDER BY "Id";

SELECT "Id", "EquipmentCode", "Name", "Description", "IsAvailable"
FROM "Equipment" ORDER BY "Id";

SELECT b."Id", s."StudentNumber", e."EquipmentCode",
       b."DateBorrowed", b."ExpectedReturnDate", b."DateReturned", b."Status"
FROM "Borrowings" b
JOIN "Students"  s ON s."Id" = b."StudentId"
JOIN "Equipment" e ON e."Id" = b."EquipmentId"
ORDER BY b."Id";

-- Consistency check: must return ZERO rows.
-- An item is available exactly when it has no active borrowing. A row here means a partial write.
SELECT e."EquipmentCode", e."IsAvailable",
       COUNT(b."Id") FILTER (WHERE b."Status" = 'Active') AS "ActiveBorrowings"
FROM "Equipment" e
LEFT JOIN "Borrowings" b ON b."EquipmentId" = e."Id"
GROUP BY e."Id", e."EquipmentCode", e."IsAvailable"
HAVING e."IsAvailable" = (COUNT(b."Id") FILTER (WHERE b."Status" = 'Active') > 0);
