import assert from 'node:assert/strict';
import { startTestApi } from './test-support.mjs';

const context = await startTestApi();
const { base, sql, psqlUrl } = context;
const productIds = [];
const orderIds = [];
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
const reportPath = restaurant => '/api/restaurants/' + restaurant + '/reports/top-selling-products';
const createProduct = async (restaurant, name, price) => {
    const product = await request('POST', '/api/restaurants/' + restaurant + '/products',
        { name, category: 'Report Test', price }, 201);
    productIds.push(product.id);
    return product;
};
const createOrder = async (restaurant, productId, quantity, status) => {
    const order = await request('POST', '/api/restaurants/' + restaurant + '/orders',
        { items: [{ productId, quantity }] }, 201);
    orderIds.push(order.id);
    sql(`UPDATE "Orders" SET "Status" = '${status}' WHERE "Id" = ${order.id};`);
    return order;
};
try {
    assert.deepEqual(await request('GET', reportPath(3), undefined, 200),
        { restaurantId: 3, products: [] });
    await request('GET', reportPath(999999), undefined, 404);
    for (const id of [0, -1, 'bad', '2147483648'])
        await request('GET', reportPath(id), undefined, 400);

    const first = await createProduct(1, 'Zebra snapshot', 1.25);
    await createOrder(1, first.id, 2, 'Pending');
    let report = await request('GET', reportPath(1), undefined, 200);
    assert.deepEqual(report.products, [{
        productId: first.id, productName: 'Zebra snapshot', totalQuantity: 2, totalRevenue: 2.5
    }]);
    sql(`UPDATE "Products" SET "Name" = 'Alpha snapshot', "Price" = 2.50 WHERE "Id" = ${first.id};`);
    await createOrder(1, first.id, 3, 'Preparing');
    report = await request('GET', reportPath(1), undefined, 200);
    assert.deepEqual(report.products, [{
        productId: first.id, productName: 'Alpha snapshot', totalQuantity: 5, totalRevenue: 10
    }]);
    const second = await createProduct(1, 'Ready item', 3);
    await createOrder(1, second.id, 8, 'Ready');
    report = await request('GET', reportPath(1), undefined, 200);
    assert.equal(report.products[0].productId, second.id);
    assert.equal(report.products[0].totalQuantity, 8);
    const third = await createProduct(1, 'Completed item', 4);
    await createOrder(1, third.id, 8, 'Completed');
    report = await request('GET', reportPath(1), undefined, 200);
    assert.equal(report.products.length, 3);
    assert.deepEqual(report.products.map(product => product.productId), [second.id, third.id, first.id]);
    const fourth = await createProduct(1, 'Fourth', 5);
    await createOrder(1, fourth.id, 7, 'Pending');
    const fifth = await createProduct(1, 'Fifth', 6);
    await createOrder(1, fifth.id, 6, 'Preparing');
    const sixth = await createProduct(1, 'Sixth excluded by limit', 7);
    await createOrder(1, sixth.id, 1, 'Ready');
    const cancelled = await createProduct(1, 'Cancelled only', 8);
    await createOrder(1, cancelled.id, 10000, 'Cancelled');
    sql(`UPDATE "Products" SET "Name" = '000 cancelled name' WHERE "Id" = ${first.id};`);
    await createOrder(1, first.id, 1000, 'Cancelled');
    const foreign = await createProduct(2, 'Other restaurant', 9);
    await createOrder(2, foreign.id, 10000, 'Completed');
    const expected = [
        { productId: second.id, productName: 'Ready item', totalQuantity: 8, totalRevenue: 24 },
        { productId: third.id, productName: 'Completed item', totalQuantity: 8, totalRevenue: 32 },
        { productId: fourth.id, productName: 'Fourth', totalQuantity: 7, totalRevenue: 35 },
        { productId: fifth.id, productName: 'Fifth', totalQuantity: 6, totalRevenue: 36 },
        { productId: first.id, productName: 'Alpha snapshot', totalQuantity: 5, totalRevenue: 10 }
    ];
    report = await request('GET', reportPath(1), undefined, 200);
    assert.equal(report.restaurantId, 1);
    assert.deepEqual(report.products, expected);
    await request('PATCH', '/api/restaurants/1/products/' + first.id, { price: 99 }, 200);
    await request('PATCH', '/api/restaurants/1/products/' + second.id, { isAvailable: false }, 200);
    report = await request('GET', reportPath(1), undefined, 200);
    assert.deepEqual(report.products, expected);
    report = await request('GET', reportPath(2), undefined, 200);
    assert.deepEqual(report.products, [{
        productId: foreign.id, productName: 'Other restaurant', totalQuantity: 10000, totalRevenue: 90000
    }]);
    await request('PATCH', '/api/restaurants/2/orders/' + orderIds.at(-1) + '/status',
        { status: 'Pending' }, 409);
    sql(`UPDATE "Orders" SET "Status" = 'Cancelled' WHERE "Id" = ${orderIds.at(-1)};`);
    assert.deepEqual((await request('GET', reportPath(2), undefined, 200)).products, []);
    const spec = await (await fetch(base + '/openapi/v1.json')).json();
    const operations = Object.values(spec.paths).flatMap(path => Object.values(path))
        .filter(operation => operation.tags?.includes('Reports'));
    assert.equal(operations.length, 1);
    assert.equal(operations[0].operationId, 'GetTopSellingProducts');
    for (const status of ['200', '400', '404', '500']) assert(operations[0].responses[status]);
    assert(spec.components.schemas.TopSellingProductResponse);
    assert.equal((await fetch(base + '/scalar/v1')).status, 200);
    // Inspect actual EF command logging to verify aggregation and limiting execute in PostgreSQL.
    assert.match(context.logs, /GROUP BY/);
    assert.match(context.logs, /sum\(/i);
    assert.match(context.logs, /min\(.*COLLATE "C"/i);
    assert.match(context.logs, /ORDER BY COALESCE\(sum\(.*DESC/i);
    assert.match(context.logs, /LIMIT/);
    assert.match(context.logs, /"Status" <> 'Cancelled'/);
    console.log(`PASS: ${checks} HTTP scenarios; all 18 report requirements, PostgreSQL SQL aggregation and Scalar/OpenAPI.`);
} catch (error) {
    console.error(context.logs.slice(-6000));
    throw error;
} finally {
    await context.stop();
    context.cleanup();
}
