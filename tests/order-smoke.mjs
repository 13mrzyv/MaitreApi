import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
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
const collection = '/api/restaurants/1/orders';
const createProduct = async (restaurant, name, price) => {
    const product = await request('POST', '/api/restaurants/' + restaurant + '/products',
        { name, category: 'Test', price }, 201);
    productIds.push(product.id);
    return product;
};
const createOrder = async (restaurant = 1, items = [{ productId: productIds[0], quantity: 2 }]) => {
    const order = await request('POST', '/api/restaurants/' + restaurant + '/orders', { items }, 201);
    orderIds.push(order.id);
    return order;
};
try {
    const pizza = await createProduct(1, 'Order Test Pizza', 12.5);
    const cola = await createProduct(1, 'Order Test Cola', 3);
    const other = await createProduct(2, 'Other Restaurant', 5);
    const unavailable = await createProduct(1, 'Unavailable', 2);
    await request('PATCH', '/api/restaurants/1/products/' + unavailable.id, { isAvailable: false }, 200);
    const order = await createOrder(1, [
        { productId: pizza.id, quantity: 2 }, { productId: cola.id, quantity: 1 }
    ]);
    assert.equal(order.status, 'Pending');
    assert(order.createdAt.endsWith('Z'));
    assert.equal(order.totalAmount, 28);
    assert.deepEqual(order.items.map(item => item.lineTotal), [25, 3]);
    assert.equal(order.items[0].productName, pizza.name);
    for (const body of [null, {}, { items: null }, { items: [] }, { items: [null] },
        { items: [{ productId: 0, quantity: 1 }] },
        { items: [{ productId: pizza.id, quantity: 0 }] },
        { items: [{ productId: pizza.id, quantity: -1 }] },
        { items: [{ productId: 999999, quantity: 1 }] },
        { items: [{ productId: other.id, quantity: 1 }] },
        { items: [{ productId: unavailable.id, quantity: 1 }] },
        { items: [{ productId: pizza.id, quantity: 1 }, { productId: pizza.id, quantity: 2 }] },
        { items: [{ productId: pizza.id, quantity: 1, unitPrice: 0.01 }] },
        { items: [{ productId: pizza.id, quantity: 1 }], totalAmount: 0.01 }
    ]) await request('POST', collection, body, 400);
    await request('POST', '/api/restaurants/999999/orders',
        { items: [{ productId: pizza.id, quantity: 1 }] }, 404);
    assert.equal(Number(sql('SELECT count(*) FROM "Orders";')), 1);
    await request('PATCH', '/api/restaurants/1/products/' + pizza.id, { price: 20 }, 200);
    sql(`UPDATE "Products" SET "Name" = 'Changed' WHERE "Id" = ${pizza.id};`);
    let page = await request('GET', collection, undefined, 200);
    assert.equal(page.page, 1);
    assert.equal(page.pageSize, 10);
    assert.equal(page.items[0].totalAmount, 28);
    assert.equal(page.items[0].items[0].unitPrice, 12.5);
    assert.equal(page.items[0].items[0].productName, pizza.name);

    const path = collection + '/' + order.id + '/status';
    for (const body of [null, {}, { status: null }, { status: 'Unknown' }, { status: '1' },
        { status: 1 }, { status: 'pending' }])
        await request('PATCH', path, body, 400);
    for (const url of ['/api/restaurants/2/orders/' + order.id + '/status',
        '/api/restaurants/999999/orders/' + order.id + '/status', collection + '/999999/status'])
        await request('PATCH', url, { status: 'Preparing' }, 404);
    const statuses = ['Pending', 'Preparing', 'Ready', 'Completed', 'Cancelled'];
    const allowed = new Set(['Pending>Preparing', 'Pending>Cancelled', 'Preparing>Ready', 'Ready>Completed']);
    for (const current of statuses) {
        for (const next of statuses) {
            sql(`UPDATE "Orders" SET "Status" = '${current}' WHERE "Id" = ${order.id};`);
            const valid = allowed.has(current + '>' + next);
            const result = await request('PATCH', path, { status: next }, valid ? 200 : 409);
            if (valid) assert.equal(result.status, next);
            assert.equal(sql(`SELECT "Status" FROM "Orders" WHERE "Id" = ${order.id};`), valid ? next : current);
        }
    }

    // Hold a row lock while concurrent callers read Pending, then let their conditional updates race.
    sql(`UPDATE "Orders" SET "Status" = 'Pending' WHERE "Id" = ${order.id};`);
    const blocker = spawn('psql', ['-X', '-v', 'ON_ERROR_STOP=1', '-At', '-d', psqlUrl,
        '-c', `BEGIN; SELECT "Id" FROM "Orders" WHERE "Id" = ${order.id} FOR UPDATE; SELECT pg_sleep(3); COMMIT;`],
        { windowsHide: true, env: { ...process.env, PGAPPNAME: 'maitre_order_lock' } });
    let locked = false;
    for (let attempt = 0; attempt < 50; attempt++) {
        locked = sql("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = 'maitre_order_lock' AND wait_event = 'PgSleep');") === 't';
        if (locked) break;
        await new Promise(resolve => setTimeout(resolve, 20));
    }
    assert(locked, 'Concurrency test must acquire its row lock before sending requests.');
    const concurrent = await Promise.all(Array.from({ length: 6 }, (_, index) =>
        fetch(base + path, { method: 'PATCH', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status: index % 2 ? 'Preparing' : 'Cancelled' }) })));
    assert.equal(concurrent.filter(response => response.status === 200).length, 1);
    assert.equal(concurrent.filter(response => response.status === 409).length, 5);
    const conflicts = await Promise.all(concurrent.filter(response => response.status === 409)
        .map(response => response.json()));
    assert(conflicts.some(problem => problem.title === 'Order was changed concurrently.'));
    checks += concurrent.length;
    await new Promise(resolve => { if (blocker.exitCode !== null) resolve(); else blocker.once('exit', resolve); });

    const cancelled = await createOrder();
    await request('PATCH', collection + '/' + cancelled.id + '/status', { status: 'Cancelled' }, 200);
    await createOrder();
    const foreignOrder = await createOrder(2, [{ productId: other.id, quantity: 1 }]);
    sql(`UPDATE "Orders" SET "CreatedAt" = '2026-10-31T10:00:00Z' WHERE "RestaurantId" = 1;`);
    page = await request('GET', collection + '?page=1&pageSize=2', undefined, 200);
    assert.equal(page.totalCount, 3);
    assert.equal(page.totalPages, 2);
    assert.deepEqual(page.items.map(item => item.id), [...orderIds].filter(id => id !== foreignOrder.id).sort((a,b) => b-a).slice(0,2));
    const second = await request('GET', collection + '?page=2&pageSize=2', undefined, 200);
    assert.equal(second.items.length, 1);
    assert.equal(second.items[0].id, order.id);
    assert(second.items.every(item => item.restaurantId === 1));
    page = await request('GET', collection + '?status=Cancelled', undefined, 200);
    assert(page.items.some(item => item.id === cancelled.id));
    assert(page.items.every(item => item.status === 'Cancelled'));
    assert.equal(page.totalCount, page.items.length);
    page = await request('GET', collection + '?page=100', undefined, 200);
    assert.equal(page.items.length, 0);
    page = await request('GET', '/api/restaurants/3/orders', undefined, 200);
    assert.equal(page.totalCount, 0);
    assert.equal(page.totalPages, 0);
    await request('GET', '/api/restaurants/999999/orders', undefined, 404);
    for (const query of ['page=0', 'page=-1', 'page=x', 'pageSize=0', 'pageSize=101',
        'pageSize=-1', 'status=Invalid', 'status=1', 'status=', 'page=2147483647&pageSize=100'])
        await request('GET', collection + '?' + query, undefined, 400);
    const malformed = await fetch(base + collection, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{"items":'
    });
    assert.equal(malformed.status, 400);
    assert.match(malformed.headers.get('content-type'), /application\/problem\+json/);
    const spec = await (await fetch(base + '/openapi/v1.json')).json();
    const operations = Object.values(spec.paths).flatMap(path => Object.values(path))
        .filter(operation => operation.tags?.includes('Orders'));
    assert.equal(operations.length, 3);
    assert(spec.components.schemas.CreateOrderRequest);
    assert.equal((await fetch(base + '/scalar/v1')).status, 200);
    console.log(`PASS: ${checks} HTTP scenarios; all 25 transitions, concurrency, snapshots, pagination and OpenAPI.`);
} catch (error) {
    console.error(context.logs.slice(-6000));
    throw error;
} finally {
    await context.stop();
    context.cleanup();
}
