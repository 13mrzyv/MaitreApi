\set ON_ERROR_STOP on
DO $$
BEGIN
    IF right(current_database(), 5) <> '_test' THEN
        RAISE EXCEPTION 'Run this smoke test only on a disposable database ending in _test';
    END IF;
END $$;
BEGIN;
DO $$
DECLARE
    product_id integer;
    order_id integer;
    item_id integer;
BEGIN
    IF (SELECT count(*) FROM "Restaurants") <> 3 THEN
        RAISE EXCEPTION 'Expected three seeded restaurants';
    END IF;

    INSERT INTO "Products" ("RestaurantId", "Name", "Category", "Price")
    VALUES (1, 'Original product', 'Test', 12.34) RETURNING "Id" INTO product_id;
    IF NOT (SELECT "IsAvailable" FROM "Products" WHERE "Id" = product_id) THEN
        RAISE EXCEPTION 'Availability default failed';
    END IF;
    INSERT INTO "Orders" ("RestaurantId") VALUES (1) RETURNING "Id" INTO order_id;
    IF NOT EXISTS (SELECT 1 FROM "Orders" WHERE "Id" = order_id
        AND "Status" = 'Pending' AND "CreatedAt" IS NOT NULL) THEN
        RAISE EXCEPTION 'Order defaults failed';
    END IF;
    INSERT INTO "OrderItems" ("OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice")
    VALUES (order_id, product_id, 'Original product', 2, 12.34) RETURNING "Id" INTO item_id;
    UPDATE "Products" SET "Name" = 'Updated product', "Price" = 99 WHERE "Id" = product_id;
    IF NOT EXISTS (SELECT 1 FROM "OrderItems" WHERE "Id" = item_id
        AND "ProductName" = 'Original product' AND "Quantity" * "UnitPrice" = 24.68) THEN
        RAISE EXCEPTION 'Snapshot or decimal precision failed';
    END IF;

    BEGIN
        UPDATE "Products" SET "Price" = -1 WHERE "Id" = product_id;
        RAISE EXCEPTION 'Negative price accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE "OrderItems" SET "Quantity" = 0 WHERE "Id" = item_id;
        RAISE EXCEPTION 'Zero quantity accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE "OrderItems" SET "UnitPrice" = -1 WHERE "Id" = item_id;
        RAISE EXCEPTION 'Negative unit price accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE "Orders" SET "Status" = 'Invalid' WHERE "Id" = order_id;
        RAISE EXCEPTION 'Invalid status accepted';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE "Products" SET "RestaurantId" = -1 WHERE "Id" = product_id;
        RAISE EXCEPTION 'Invalid restaurant accepted';
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM "Products" WHERE "Id" = product_id;
        RAISE EXCEPTION 'Referenced product deleted';
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM "Orders" WHERE "Id" = order_id;
        RAISE EXCEPTION 'Order with items deleted';
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM "Restaurants" WHERE "Id" = 1;
        RAISE EXCEPTION 'Referenced restaurant deleted';
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN NULL;
    END;
END $$;
ROLLBACK;

