import assert from 'node:assert/strict';
import { startTestApi } from './test-support.mjs';

const original = process.env.PGTEST_URL;
const wrongTarget = new URL(original);
wrongTarget.port = wrongTarget.port === '55433' ? '55434' : '55433';
process.env.PGTEST_URL = wrongTarget.href;
await assert.rejects(startTestApi(), /Test ports differ/);
process.env.PGTEST_URL = original;

const context = await startTestApi({ environment: 'Production', failDatabaseQueries: true });
try {
    // Force an undefined-table error through a process-local search path; no schema is changed.
    const response = await context.fetch('/api/restaurants/1/reports/top-selling-products');
    assert.equal(response.status, 500);
    assert.match(response.headers.get('content-type'), /application\/problem\+json/);
    const text = await response.text();
    const problem = JSON.parse(text);
    assert.equal(problem.status, 500);
    assert.doesNotMatch(text, /exception|stacktrace|npgsql|maitre_missing_schema|password|SELECT/i);
    for (const path of ['/openapi/v1.json', '/scalar/v1'])
        assert.equal((await context.fetch(path)).status, 404);
} finally {
    await context.stop();
    context.cleanup();
}

// An assertion immediately after a successful write must not leave the fixture behind.
const failureContext = await startTestApi();
try {
    await assert.rejects(async () => {
        const response = await failureContext.fetch('/api/restaurants/1/products', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: 'Failure cleanup probe', category: 'Test', price: 1 })
        });
        assert.equal(response.status, 201);
        throw new Error('Intentional assertion failure');
    }, /Intentional assertion failure/);
} finally {
    await failureContext.stop();
    failureContext.cleanup();
}
console.log('PASS: mismatched target rejected, production 500 is sanitized, docs hidden, failure fixtures cleaned.');
