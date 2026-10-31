import assert from 'node:assert/strict';
import { startTestApi } from './test-support.mjs';

const context = await startTestApi();
const { base, sql } = context;
const ids = [];
let orderId;
let checks = 0;
async function request(method, path, body, status) {
    const response = await context.fetch(path, {
        method, headers: { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body)
    });
    const result = await response.json();
    assert.equal(response.status, status, JSON.stringify(result));
    if (status === 201)
        assert.equal(response.headers.get('location'), path + '/' + result.id);
    if (status >= 400) {
        assert.match(response.headers.get('content-type'), /application\/problem\+json/);
        assert.equal(result.status, status);
    }
    checks++;
    return result;
}
const collection = '/api/restaurants/1/products';
try {
    const created = await request('POST', collection,
        { name: '  Test Pizza  ', category: ' Pizza ', price: 12.50 }, 201);
    ids.push(created.id);
    assert.deepEqual(created, { id: created.id, restaurantId: 1, name: 'Test Pizza',
        category: 'Pizza', price: 12.5, isAvailable: true });
    const duplicate = await request('POST', collection,
        { name: 'Test Pizza', category: 'Pizza', price: 12.5 }, 201);
    ids.push(duplicate.id);
    await request('POST', '/api/restaurants/999999/products',
        { name: 'Pizza', category: 'Pizza', price: 12.5 }, 404);
    for (const body of [
        null,
        { name: 'Bad\u0000name', category: 'Pizza', price: 12 },
        { name: 'Pizza', category: 'Bad\u0000category', price: 12 },
        { name: '', category: 'Pizza', price: 12 },
        { name: '   ', category: 'Pizza', price: 12 },
        { name: null, category: 'Pizza', price: 12 },
        { name: 'x'.repeat(201), category: 'Pizza', price: 12 },
        { name: 'Pizza', category: ' ', price: 12 },
        { name: 'Pizza', category: 'x'.repeat(101), price: 12 },
        ...[-1, 0, 1.234, 10000000000000000].map(price => ({ name: 'Pizza', category: 'Pizza', price })),
        { name: 'Pizza', category: 'Pizza', price: 12, restaurantId: 2 }
    ]) await request('POST', collection, body, 400);

    orderId = Number(sql('INSERT INTO "Orders" ("RestaurantId") VALUES (1) RETURNING "Id";').split('\n')[0]);
    sql(`INSERT INTO "OrderItems" ("OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice")
         VALUES (${orderId}, ${created.id}, 'Test Pizza', 2, 12.50);`);
    const path = collection + '/' + created.id;
    let updated = await request('PATCH', path, { price: 18.50 }, 200);
    assert.equal(updated.price, 18.5);
    assert.equal(updated.isAvailable, true);
    updated = await request('PATCH', path, { isAvailable: false }, 200);
    assert.equal(updated.isAvailable, false);
    assert.equal(updated.price, 18.5);
    updated = await request('PATCH', path, { isAvailable: true }, 200);
    assert.equal(updated.isAvailable, true);
    updated = await request('PATCH', path, { price: 15, isAvailable: false }, 200);
    assert.equal(updated.price, 15);
    assert.equal(updated.isAvailable, false);
    assert.equal(updated.name, 'Test Pizza');
    assert.equal(updated.category, 'Pizza');
    await request('PATCH', '/api/restaurants/2/products/' + created.id, { price: 20 }, 404);
    await request('PATCH', '/api/restaurants/999999/products/' + created.id, { price: 20 }, 404);
    await request('PATCH', collection + '/999999', { price: 20 }, 404);
    for (const body of [{}, { price: null }, { isAvailable: null }, { price: -1 },
        { price: 0 }, { price: 1.234 }, { price: 10000000000000000 },
        { price: 20, isAvailable: null }, { name: 'Changed' }, { category: 'Other' },
        { restaurantId: 2 }, { isAvailable: 'false' }])
        await request('PATCH', path, body, 400);
    const unchanged = sql(`SELECT "ProductName" || '|' || "UnitPrice" FROM "OrderItems" WHERE "OrderId" = ${orderId};`);
    assert.equal(unchanged, 'Test Pizza|12.50');
    assert.equal(sql(`SELECT "Price" || '|' || "IsAvailable" FROM "Products" WHERE "Id" = ${created.id};`), '15.00|false');
    const spec = await (await fetch(base + '/openapi/v1.json')).json();
    const operations = Object.values(spec.paths).flatMap(path => Object.values(path))
        .filter(operation => operation.tags?.includes('Products'));
    assert.equal(operations.length, 2);
    for (const operation of operations) assert.deepEqual(operation.tags, ['Products']);
    assert(spec.components.schemas.UpdateProductRequest);
    assert.equal((await fetch(base + '/scalar/v1')).status, 200);
    console.log(`PASS: ${checks} HTTP scenarios, snapshots, isolation and OpenAPI/Scalar.`);
} catch (error) {
    console.error(context.logs.slice(-6000));
    throw error;
} finally {
    await context.stop();
    try {
        if (orderId) {
            sql(`DELETE FROM "OrderItems" WHERE "OrderId" = ${orderId}; DELETE FROM "Orders" WHERE "Id" = ${orderId};`);
        }
    } finally {
        context.cleanup();
    }
}
